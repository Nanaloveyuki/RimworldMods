using System.Collections.Generic;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Core;
using HungerAndHavoc.Trade;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    // 自有访客图 不走 Ideology 门槛的原版乞讨 Lord
    public class LordJob_RHAH_Visitor : LordJob
    {
        Faction faction;
        IntVec3 waitSpot = IntVec3.Invalid;
        RHAH_PawnRole familyRole;
        List<Verse.Pawn> foodReceivers;
        ThingDef foodDef;
        int foodCount;
        IntVec3 passerbyExitCell = IntVec3.Invalid;
        bool waitSpotResolved;
        int armedChoiceId;
        bool armedAssault;
        internal int ArmedChoiceId { get => armedChoiceId; set => armedChoiceId = value; }
        internal bool ArmedAssault { get => armedAssault; set => armedAssault = value; }

        internal bool IsPasserby
        {
            get
            {
                return RHAH_PasserbyRules.IsPasserbyLord(lord);
            }
        }

        internal IntVec3 PasserbyExitCell
        {
            get
            {
                ResolvePasserbyExit();
                return passerbyExitCell;
            }
        }
        internal bool CanRoutePasserby
        {
            get
            {
                if (!IsPasserby || lord?.ownedPawns == null)
                {
                    return false;
                }

                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Verse.Pawn pawn = lord.ownedPawns[i];
                    IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
                    if (snapshot == null || snapshot.Lifecycle == RHAH_Lifecycle.Leaving ||
                        !RHAH_VisitorRules.AllowsModBehavior(RHAH_VisitorStay.Kind(pawn)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        internal bool TryRedirectLegacyPasserby()
        {
            if (!CanRoutePasserby || lord.Graph?.lordToils == null)
            {
                return false;
            }

            for (int i = 0; i < lord.Graph.lordToils.Count; i++)
            {
                LordToil_RHAH_VisitorTravel travel = lord.Graph.lordToils[i] as LordToil_RHAH_VisitorTravel;
                if (travel != null && lord.CurLordToil != travel)
                {
                    lord.GotoToil(travel);
                    return true;
                }
            }

            return false;
        }

        void ResolvePasserbyExit()
        {
            if (!IsPasserby || passerbyExitCell.IsValid || lord?.lordManager?.map == null)
            {
                return;
            }

            List<Verse.Pawn> members = lord.ownedPawns;
            IntVec3 exit;
            if (RHAH_PasserbyRules.TryFindOppositeExit(lord.lordManager.map, members, out exit))
            {
                passerbyExitCell = exit;
            }
        }
 

        internal IntVec3 WaitSpot
        {
            get
            {
                ResolveWaitSpot();
                return waitSpot;
            }
        }

        void ResolveWaitSpot()
        {
            if (waitSpotResolved || lord?.lordManager?.map == null)
            {
                return;
            }

            Verse.Pawn searcher = null;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn member = lord.ownedPawns[i];
                if (!member.Spawned || member.Dead)
                {
                    continue;
                }
                searcher = member;
                if (!member.Downed)
                {
                    break;
                }
            }
            if (searcher == null)
            {
                return;
            }

            // 沿用原版访客的室外停留点 按来客自身权限检查路径
            if (RCellFinder.TryFindRandomSpotJustOutsideColony(searcher, out IntVec3 destination))
            {
                waitSpot = destination;
            }
            waitSpotResolved = true;
        }

        internal bool PreserveForAttitudeChange { get; set; }

        internal void SetFaction(Faction value)
        {
            faction = value;
            if (lord != null)
            {
                lord.faction = value;
            }
        }

        public override bool ShouldRemovePawn(Verse.Pawn pawn, PawnLostCondition reason)
        {
            return reason != PawnLostCondition.Incapped &&
                !(reason == PawnLostCondition.ChangedFaction && PreserveForAttitudeChange);
        }

        public override void Notify_PawnLost(Verse.Pawn pawn, PawnLostCondition cond)
        {
            RefreshArmedRequest();
        }

        public override void Notify_PawnDowned(Verse.Pawn pawn)
        {
            RefreshArmedRequest();
        }

        public override void Notify_LordDestroyed()
        {
            RefreshArmedRequest();
        }

        void RefreshArmedRequest()
        {
            if (armedChoiceId <= 0) return;
            RHAH_ChoiceRecord record = RHAH_ArmedDemand.Find(armedChoiceId);
            if (record != null) RHAH_ArmedDemand.Refresh(record);
        }

        public LordJob_RHAH_Visitor()
        {
        }

        public LordJob_RHAH_Visitor(Faction faction, IntVec3 waitSpot, RHAH_PawnRole familyRole)
        {
            this.faction = faction;
            this.waitSpot = waitSpot;
            this.familyRole = familyRole;
        }

        public override bool LostImportantReferenceDuringLoading
        {
            get
            {
                if (faction == null)
                {
                    return true;
                }

                if (lord != null && (lord.lordManager == null || lord.lordManager.map == null))
                {
                    return true;
                }

                return false;
            }
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            LordToil_Travel travel = new LordToil_RHAH_VisitorTravel(this);
            graph.AddToil(travel);
            graph.StartingToil = travel;

            LordToil_RHAH_VisitorSeek seek = new LordToil_RHAH_VisitorSeek(this);
            graph.AddToil(seek);
            LordToil_RHAH_WaitFood waitFood = new LordToil_RHAH_WaitFood(this);
            graph.AddToil(waitFood);

            LordToil_RHAH_VisitorLeave leave = new LordToil_RHAH_VisitorLeave();
            graph.AddToil(leave);

            // 空派系只提供敌对检查 不切原版防守或袭击
            Transition toFood = new Transition(seek, waitFood, false, true);
            toFood.AddSource(travel);
            toFood.AddTrigger(new Trigger_Memo("RHAH_WaitFood"));
            toFood.AddTrigger(new Trigger_Custom(_ => FoodWaitReady()));
            toFood.AddPostAction(new TransitionAction_EndAllJobs());

            Transition arrived;
            if (IsPasserby)
            {
                arrived = new Transition(travel, leave, false, true);
                arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
                arrived.AddPostAction(new TransitionAction_Custom((System.Action)(() =>
                {
                    if (lord?.ownedPawns == null)
                    {
                        return;
                    }
                    for (int i = 0; i < lord.ownedPawns.Count; i++)
                    {
                        Verse.Pawn pawn = lord.ownedPawns[i];
                        if (RHAH_BatchAttitude.CanOrderLeave(pawn))
                        {
                            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
                        }
                    }
                })));
            }
            else
            {
                arrived = new Transition(travel, seek, false, true);
                arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            }
            graph.AddTransition(arrived, false);
            graph.AddTransition(toFood, false);
            Transition toLeave = new Transition(seek, leave, false, true);
            toLeave.AddSource(travel);
            toLeave.AddSource(waitFood);
            toLeave.AddTrigger(new Trigger_Custom(_ => AllReadyToLeave()));
            toLeave.AddTrigger(new Trigger_Memo("RHAH_Leave"));
            toLeave.AddTrigger(new Trigger_Custom(signal => TraderMustLeave(signal)));
            toLeave.AddPostAction(new TransitionAction_Custom((System.Action)(() =>
            {
                if (lord?.ownedPawns == null)
                {
                    return;
                }

                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Compat.RHAH_LeashBridge.ClearDeparture(lord.ownedPawns[i]);
                }
            })));
            toLeave.AddPostAction(new TransitionAction_Custom((System.Action)(() => EndFoodWait())));
            toLeave.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(toLeave, false);
            Transition foodDone = new Transition(waitFood, seek, false, true);
            foodDone.AddTrigger(new Trigger_Custom(_ => FoodFilled()));
            foodDone.AddTrigger(new Trigger_Custom(_ => FoodWaitExpired()));
            foodDone.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(foodDone, false);
            // 追加 toil 保持已发布访客图的旧索引不变
            LordToil assault = new LordToil_RHAH_ArmedAssault();
            graph.AddToil(assault);
            Transition toAssault = new Transition(seek, assault, false, true);
            toAssault.AddSource(travel);
            toAssault.AddSource(waitFood);
            toAssault.AddTrigger(new Trigger_Memo("RHAH_ArmedAssault"));
            toAssault.AddPostAction(new TransitionAction_Custom((System.Action)(() => EndFoodWait())));
            toAssault.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(toAssault, false);
            Transition assaultLeave = new Transition(assault, leave, false, true);
            assaultLeave.AddTrigger(new Trigger_Memo("RHAH_Leave"));
            assaultLeave.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(assaultLeave, false);


            return graph;
        }
        public override void LordJobTick()
        {
            if (ArmedChoiceId > 0 && !RHAH_ArmedDemand.Enabled && Find.TickManager.TicksGame % 250 == 0)
            {
                RHAH_ChoiceRecord record = RHAH_ArmedDemand.Find(ArmedChoiceId);
                if (record != null) RHAH_ArmedDemand.Resolve(record, RHAH_ChoiceAction.Ignore);
            }
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                MigrateFoodReceiver(Scribe.loader.curXmlParent);
            }
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref waitSpot, "waitSpot", IntVec3.Invalid);
            Scribe_Values.Look(ref passerbyExitCell, "passerbyExitCell", IntVec3.Invalid);
            Scribe_Values.Look(ref familyRole, "familyRole", RHAH_PawnRole.Unspecified);
            Scribe_Collections.Look(ref foodReceivers, "foodReceivers", LookMode.Reference);
            Scribe_Defs.Look(ref foodDef, "foodDef");
            Scribe_Values.Look(ref foodCount, "foodCount", 0);
            Scribe_Values.Look(ref armedChoiceId, "armedChoiceId", 0);
            Scribe_Values.Look(ref armedAssault, "armedAssault", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && foodReceivers == null)
            {
                foodReceivers = new List<Verse.Pawn>();
            }
        }

        // 只预处理当前 Lord 的旧键 引用仍交给原版解析
        static void MigrateFoodReceiver(System.Xml.XmlNode node)
        {
            System.Xml.XmlElement legacy = node?["foodReceiver"];
            if (legacy == null)
            {
                return;
            }

            if (node["foodReceivers"] == null)
            {
                System.Xml.XmlElement receivers = node.OwnerDocument.CreateElement("foodReceivers");
                string reference = legacy.InnerText.Trim();
                if (legacy.GetAttribute("IsNull") != "True" && reference.Length > 0 && reference != "null")
                {
                    System.Xml.XmlElement item = node.OwnerDocument.CreateElement("li");
                    item.InnerText = reference;
                    receivers.AppendChild(item);
                }
                node.InsertBefore(receivers, legacy);
            }
            node.RemoveChild(legacy);
        }

        internal bool BeginFoodWait(Verse.Pawn receiver, int count)
        {
            if (receiver == null || count <= 0)
            {
                return false;
            }

            if (foodReceivers == null)
            {
                foodReceivers = new List<Verse.Pawn>();
            }

            bool added = !foodReceivers.Contains(receiver);
            if (added)
            {
                foodReceivers.Add(receiver);
            }

            foodCount = count;
            return added;
        }

        // 请求量按默认餐营养折算 与交付账本同量纲
        double FoodRequestedNutrition
        {
            get
            {
                return foodCount * RHAH_DemandFood.MealNutrition;
            }
        }

        // 交付账记在接收者本人身上 同批其它人收的食物不算给他
        static double DeliveredNutrition(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            return comp == null ? 0 : comp.State.foodNutritionDelivered;
        }

        double MissingNutrition(Verse.Pawn pawn)
        {
            double left = FoodRequestedNutrition - DeliveredNutrition(pawn);
            return left <= RHAH_DemandFood.Tolerance ? 0 : left;
        }

        internal bool WaitingForFood(Verse.Pawn pawn)
        {
            RHAH_ChoiceRecord request = RHAH_ArmedDemand.FoodRequest(pawn);
            if (request != null) return ListedForFood(pawn) && RHAH_ArmedDemand.Remaining(request) > 0;
            return RHAH_RequestRules.FoodWaiting(
                ListedForFood(pawn),
                pawn != null && !pawn.Dead,
                pawn != null && pawn.Spawned,
                DeliveredNutrition(pawn),
                FoodRequestedNutrition);
        }

        // 对外仍是件数 按这次要给的食物的营养折算
        internal int FoodStillNeeded(Verse.Pawn pawn, ThingDef food)
        {
            RHAH_ChoiceRecord request = RHAH_ArmedDemand.FoodRequest(pawn);
            if (request != null) return RHAH_ArmedDemand.Remaining(request);
            if (!WaitingForFood(pawn))
            {
                return 0;
            }

            return RHAH_DemandFood.Units(MissingNutrition(pawn), RHAH_DemandFood.EdibleNutrition(food));
        }

        // 这一轮还差多少件 未登记时按设置里的份数预演 供菜单显示
        internal int FoodUnitsFor(Verse.Pawn pawn, ThingDef food, int mealsIfNotListed)
        {
            double requested = ListedForFood(pawn)
                ? FoodRequestedNutrition
                : mealsIfNotListed * RHAH_DemandFood.MealNutrition;
            return RHAH_DemandFood.Units(requested - DeliveredNutrition(pawn), RHAH_DemandFood.EdibleNutrition(food));
        }

        // 这一趟该搬几件 按这趟手上的食物折件 只扣别人正在搬的 0 表示已经够了
        internal int HaulUnitsFor(Verse.Pawn receiver, Thing food, JobDriver excluding)
        {
            double nutrition = RHAH_DemandFood.EdibleNutrition(food == null ? null : food.def);
            if (nutrition <= 0)
            {
                return 0;
            }

            double left = FoodRequestedNutrition - DeliveredNutrition(receiver) - RHAH_FoodHandoff.BeingHauledTo(receiver, excluding);
            return RHAH_DemandFood.Units(left, nutrition);
        }

        // 原版只拿它当「还要不要再搬一趟」的信号 件数由搬货那一刻按实际食物重算
        internal int FoodLeftToCollect(Verse.Pawn pawn)
        {
            return WaitingForFood(pawn) ? 1 : 0;
        }

        // 只表示最近一次给的是哪种 不参与过滤
        internal ThingDef RequestedFood
        {
            get
            {
                return armedChoiceId > 0 && RHAH_ArmedDemand.Find(armedChoiceId)?.Open == true ? null : foodDef;
            }
        }

        internal void NoteFood(ThingDef def)
        {
            if (armedChoiceId > 0 && RHAH_ArmedDemand.Find(armedChoiceId)?.Open == true) return;
            foodDef = def;
        }

        // 普通投喂只认殖民者实际入包的量 索粮走 RHAH_ChoiceRecord 自己的账
        internal void NoteFoodDelivered(Verse.Pawn receiver, Thing item, int count)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(receiver);
            if (comp == null || item == null || count <= 0)
            {
                return;
            }

            comp.State.foodNutritionDelivered += RHAH_DemandFood.EdibleNutrition(item.def) * count;
        }

        internal bool FoodFilled()
        {
            if (foodReceivers == null || foodReceivers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < foodReceivers.Count; i++)
            {
                if (WaitingForFood(foodReceivers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool ListedForFood(Verse.Pawn pawn)
        {
            return pawn != null && foodReceivers != null && foodReceivers.Contains(pawn);
        }
        internal void EndFoodWait()
        {
            if (foodReceivers != null)
            {
                for (int i = 0; i < foodReceivers.Count; i++)
                {
                    Verse.Pawn receiver = foodReceivers[i];
                    if (receiver == null)
                    {
                        continue;
                    }

                    // 先判是否还在等他 再清本人的交付账
                    bool waiting = WaitingForFood(receiver);
                    CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(receiver);
                    if (comp == null)
                    {
                        continue;
                    }

                    comp.State.foodNutritionDelivered = 0;
                    if (!waiting)
                    {
                        comp.SetFoodWait(-1);
                    }
                }
            }
            foodReceivers?.Clear();
            foodDef = null;
            foodCount = 0;
        }


        const int FoodWaitPathCheckInterval = 120;

        // 已登记等饭的来客是否该真的停下来等 到点 走不到 超期任一成立
        bool FoodWaitReady()
        {
            if (foodReceivers == null || foodReceivers.Count == 0)
            {
                return false;
            }

            IntVec3 spot = WaitSpot;
            int now = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            bool checkPath = now % FoodWaitPathCheckInterval == 0;
            for (int i = 0; i < foodReceivers.Count; i++)
            {
                Verse.Pawn pawn = foodReceivers[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }

                // 寻路是 A* 按 tick 节流 纯几何判据每 tick 可跑
                bool reachable = !checkPath ||
                    (spot.IsValid && pawn.CanReach(spot, PathEndMode.ClosestTouch, Danger.Deadly));
                if (RHAH_VisitorRules.FoodWaitArrived(spot, pawn.Position, reachable))
                {
                    return true;
                }

                if (FoodWaitExpired(pawn, RHAH_Api.Get(pawn)))
                {
                    return true;
                }
            }

            return false;
        }


        bool FoodWaitExpired()
        {
            if (foodReceivers == null || foodReceivers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < foodReceivers.Count; i++)
            {
                Verse.Pawn receiver = foodReceivers[i];
                if (WaitingForFood(receiver) && !FoodWaitExpired(receiver, RHAH_Api.Get(receiver)))
                {
                    return false;
                }
            }

            return true;
        }

        static bool FoodWaitExpired(Verse.Pawn pawn, IRHAH_Pawn snapshot)
        {
            if (pawn == null || snapshot == null || Find.TickManager == null)
            {
                return false;
            }

            return RHAH_VisitorRules.NoFoodWaitExpired(
                true,
#if RIMWORLD_1_7
                snapshot.HasBeenFed && !RHAH_Feeding.NeedsFood(pawn),
#else
                snapshot.HasBeenFed,
#endif
                Find.TickManager.TicksGame,
                FoodWaitDeadline(pawn));
        }

        static int FoodWaitDeadline(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            return comp == null ? -1 : comp.State.foodWaitUntilTick;
        }

        bool AllReadyToLeave()
        {
            if (lord == null || lord.ownedPawns == null)
            {
                return false;
            }

            int count = lord.ownedPawns.Count;
            if (count == 0)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
                if (snapshot == null || !RHAH_BatchAttitude.CanOrderLeave(pawn))
                {
                    return false;
                }

                if (ReadyToLeave(pawn, snapshot))
                {
                    continue;
                }


                return false;
            }

            return true;
        }
        internal static bool ReadyToLeave(Verse.Pawn pawn, IRHAH_Pawn snapshot)
        {
            if (snapshot.Lifecycle == RHAH_Lifecycle.Leaving)
            {
                return true;
            }

            if (RHAH_MouseSwarm.IsSource(snapshot.SourceIncidentDisplayId)) return false;
            Lord lord = pawn.GetLord();
            LordJob_RHAH_Visitor visitor = lord?.LordJob as LordJob_RHAH_Visitor;
            if (RHAH_ArmedDemand.Holds(pawn)) return false;
            if (lord?.CurLordToil is LordToil_RHAH_WaitFood && visitor.ListedForFood(pawn))
            {
                if (visitor.WaitingForFood(pawn))
                {
                    int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
                    int deadline = FoodWaitDeadline(pawn);
                    return deadline >= 0 && tick >= deadline;
                }
                CompRHAH_Pawn.TryGet(pawn)?.SetFoodWait(-1);
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            int now = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            return (RHAH_Api.Allows(pawn, RHAH_BehaviorGate.LeaveAfterFed) &&
                RHAH_VisitorRules.FedLeaveDue(settings == null || settings.leaveAfterFed,
                    snapshot.HasBeenFed, now, snapshot.LeaveAfterGameTick, pawn.Downed)) ||
                FoodWaitExpired(pawn, snapshot);
        }

        bool TraderMustLeave(TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick || lord?.ownedPawns == null)
            {
                return false;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            if (tick % 197 != 0)
            {
                return false;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            bool ignoreHarsh = settings == null || settings.traderIgnoresHarshEnvironment;
            bool ignoreEnclosed = settings == null || settings.traderIgnoresEnclosedSpace;
            if (ignoreHarsh && ignoreEnclosed)
            {
                return false;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                if (RHAH_CaravanStay.MemberMustLeave(pawn, ignoreHarsh, ignoreEnclosed))
                {
                    return true;
                }
            }

            return false;
        }

    }

    internal sealed class LordToil_RHAH_VisitorTravel : LordToil_Travel
    {
        readonly LordJob_RHAH_Visitor job;
        bool dutiesReady;

        internal LordToil_RHAH_VisitorTravel(LordJob_RHAH_Visitor job) : base(job.WaitSpot)
        {
            this.job = job;
        }

        public override void UpdateAllDuties()
        {
            SetDestination(job.IsPasserby && job.PasserbyExitCell.IsValid ? job.PasserbyExitCell : job.WaitSpot);
            dutiesReady = true;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                PawnDuty duty = RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Leaving
                    ? RHAH_VisitorRules.CreateLeaveDuty()
                    : HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.HasEdgeHold(pawn)
                        ? new PawnDuty(RHAH_DefOf.RHAH_VisitorSeek, pawn.Position, 0f)
                        : new PawnDuty(RHAH_DefOf.RHAH_VisitorTravel, Data.dest, -1f);
                pawn.mindState.duty = duty;
            }
        }

        public override void LordToilTick()
        {
            // 读档会恢复旧赶路数据 首次运行时按停留点重建职责
            if (!dutiesReady)
            {
                UpdateAllDuties();
            }
            if (Find.TickManager.TicksGame % 205 != 0)
            {
                return;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                // 倒地和不能自走的幼年来客留在照护组 不阻挡其他人寻食
                if (!pawn.Spawned || !Compat.RHAH_ChildMovement.CanWalkOut(pawn) ||
                    RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Leaving ||
                    HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.HasEdgeHold(pawn))
                {
                    continue;
                }
                if (!pawn.Position.InHorDistOf(Data.dest, AllArrivedCheckRadius) ||
                    !pawn.CanReach(Data.dest, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    return;
                }
            }
            if (job.IsPasserby)
            {
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Verse.Pawn pawn = lord.ownedPawns[i];
                    if (RHAH_BatchAttitude.CanOrderLeave(pawn))
                    {
                        RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
                    }
                }

                for (int i = 0; i < lord.Graph.lordToils.Count; i++)
                {
                    LordToil_RHAH_VisitorLeave leave = lord.Graph.lordToils[i] as LordToil_RHAH_VisitorLeave;
                    if (leave != null)
                    {
                        // 这里直接换 toil 不走 arrived 过渡 名单与账本要当场清
                        job.EndFoodWait();
                        lord.GotoToil(leave);
                        return;
                    }
                }
            }
            lord.ReceiveMemo("TravelArrived");
        }
    }

    internal sealed class LordToil_RHAH_VisitorSeek : LordToil
    {
        readonly LordJob_RHAH_Visitor job;
        bool dutiesReady;

        public LordToil_RHAH_VisitorSeek(LordJob_RHAH_Visitor job)
        {
            this.job = job;
        }

        public override void UpdateAllDuties()
        {
            if (job.TryRedirectLegacyPasserby())
            {
                return;
            }

            IntVec3 waitSpot = job.WaitSpot;
            dutiesReady = true;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                pawn.mindState.duty = RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Leaving
                    ? RHAH_VisitorRules.CreateLeaveDuty()
                    : new PawnDuty(RHAH_DefOf.RHAH_VisitorSeek, waitSpot, 10f);
            }
        }

        public override void LordToilTick()
        {
            if (!dutiesReady)
            {
                UpdateAllDuties();
            }
        }
    }

    internal sealed class LordToil_RHAH_VisitorLeave : LordToil
    {
        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                if (!RHAH_BatchAttitude.CanOrderLeave(pawn))
                {
                    continue;
                }
                RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
                pawn.mindState.duty = RHAH_VisitorRules.CreateLeaveDuty();
            }
        }
    }

    internal sealed class LordToil_RHAH_WaitFood : LordToil, IWaitForItemsLordToil
    {
        readonly LordJob_RHAH_Visitor job;

        public LordToil_RHAH_WaitFood(LordJob_RHAH_Visitor job)
        {
            this.job = job;
        }

        public int CountRemaining => job == null ? 0 : job.FoodLeftToCollect(Receiver());

        public bool HasAllRequestedItems => job != null && job.FoodFilled();

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                pawn.mindState.duty = RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Leaving
                    ? RHAH_VisitorRules.CreateLeaveDuty()
                    : new PawnDuty(DutyDefOf.WanderClose_NoNeeds, pawn.Position, 3f);
            }
        }
        public override void LordToilTick()
        {
            if (Find.TickManager.TicksGame % 197 != 0)
            {
                return;
            }
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
                if (snapshot == null || snapshot.Lifecycle == RHAH_Lifecycle.Leaving ||
                    !RHAH_BatchAttitude.CanOrderLeave(pawn) || !LordJob_RHAH_Visitor.ReadyToLeave(pawn, snapshot))
                {
                    continue;
                }
                RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
                pawn.mindState.duty = RHAH_VisitorRules.CreateLeaveDuty();
                if (pawn.CurJob != null)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

        public override void Cleanup()
        {
            job.EndFoodWait();
        }


        public override IEnumerable<FloatMenuOption> ExtraFloatMenuOptions(Verse.Pawn requester, Verse.Pawn current)
        {
            yield break;
        }

        Verse.Pawn Receiver()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                if (job.WaitingForFood(lord.ownedPawns[i]))
                {
                    return lord.ownedPawns[i];
                }
            }

            return null;
        }
    }
}
