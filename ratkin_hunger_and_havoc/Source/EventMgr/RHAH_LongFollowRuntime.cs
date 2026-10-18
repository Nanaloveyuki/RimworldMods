using System;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.EventMgr
{
    internal static class RHAH_LongFollowRuntime
    {
        internal const string HoldKey = "rhah:longHold";
        static int dispatchingChainId;
        static GameComponent_RHAH_Game Game => Current.Game?.GetComponent<GameComponent_RHAH_Game>();
        internal static int Now => Find.TickManager == null ? 0 : Find.TickManager.TicksGame;

        internal static string ParentId(string id)
        {
            switch (id)
            {
                case "I-038": return "I-012";
                case "I-046": return "I-032";
                case "I-047": return "I-033";
                default: return id;
            }
        }

        internal static RHAH_LongFollowRecord FindRecord(int chainId)
        {
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; records != null && i < records.Count; i++)
            {
                if (records[i].ChainId == chainId) return records[i];
            }
            return null;
        }

        static RHAH_LongFollowRecord BatchRecord(string id, int batchId)
        {
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; records != null && i < records.Count; i++)
            {
                if (records[i].DisplayId == id && records[i].BatchId == batchId) return records[i];
            }
            return null;
        }

        internal static void NoteArrival(string displayId, int batchId, Map map, IList<Verse.Pawn> arrived, Verse.Pawn sourceMother = null, Verse.Pawn attachmentAdult = null)
        {
            if (Game == null || map == null || !RHAH_EventFollowRules.HasLongFollow(displayId) || BatchRecord(displayId, batchId) != null) return;
            RHAH_PawnIndex.Clear();
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { DisplayId = displayId, BatchId = batchId, MapId = map.uniqueID };
            for (int i = 0; arrived != null && i < arrived.Count; i++)
            {
                Verse.Pawn pawn = arrived[i];
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
                if (comp != null && RHAH_EventFollowRules.IsYoung(comp.State.role))
                {
                    record.ChildIds.Add(pawn.thingIDNumber);
                    Keep(pawn);
                    if (record.SubjectId == 0) record.SubjectId = pawn.thingIDNumber;
                    if (sourceMother == null && comp.State.parentPawnLoadId > 0) sourceMother = Pawn(comp.State.parentPawnLoadId, Now);
                }
            }
            string parent = ParentId(displayId);
            if (parent == "I-002") record.SubjectId = 0;
            if (parent == "I-041") record.MedicalHold = Identity.RHAH_Plague.HasActive(Pawn(record.SubjectId, Now));
            if (sourceMother == null && (parent == "I-002" || parent == "I-037"))
            {
                // 原始母亲只在初次发生时生成并建立真实亲属关系
                sourceMother = CreateAdult(record, null, Gender.Female, parent == "I-037");
            }
            if (sourceMother != null)
            {
                record.SourceAdultId = sourceMother.thingIDNumber;
                Keep(sourceMother);
                CompRHAH_Pawn mother = CompRHAH_Pawn.TryGet(sourceMother);
                for (int i = 0; i < record.ChildIds.Count; i++)
                {
                    Verse.Pawn child = Pawn(record.ChildIds[i], Now);
                    CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(child);
                    if (comp == null) continue;
                    comp.State.parentPawnLoadId = record.SourceAdultId;
                    if (mother != null && !mother.State.childPawnLoadIds.Contains(child.thingIDNumber)) mother.State.childPawnLoadIds.Add(child.thingIDNumber);
                    if (child.relations != null && !child.relations.DirectRelationExists(PawnRelationDefOf.Parent, sourceMother)) child.relations.AddDirectRelation(PawnRelationDefOf.Parent, sourceMother);
                }
            }
            if (parent == "I-033")
            {
                if (attachmentAdult == null)
                {
                    IReadOnlyList<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
                    for (int i = 0; i < colonists.Count; i++)
                    {
                        if (Adult(colonists[i]) && (attachmentAdult == null || colonists[i].thingIDNumber < attachmentAdult.thingIDNumber)) attachmentAdult = colonists[i];
                    }
                }
                record.ResponsibleId = attachmentAdult?.thingIDNumber ?? 0;
            }
            Game.LongFollows.Add(record);
        }

        internal static void RegisterChain(int chainId, string id, int batchId, int mapId, int dueTick)
        {
            RHAH_LongFollowRecord record = BatchRecord(id, batchId);
            if (record == null)
            {
                record = new RHAH_LongFollowRecord { DisplayId = id, BatchId = batchId, MapId = mapId };
                Game?.LongFollows.Add(record);
                RecoverOriginalIds(record);
            }
            record.ChainId = chainId;
            if (record.DueTick < 0) record.DueTick = dueTick;
        }

        static void RecoverOriginalIds(RHAH_LongFollowRecord record)
        {
            List<Verse.Pawn> all = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(all[i]);
                if (comp == null || comp.State.spawnBatchId != record.BatchId || comp.State.sourceIncidentDisplayId != record.DisplayId) continue;
                if (RHAH_EventFollowRules.IsYoung(comp.State.role))
                {
                    if (!record.ChildIds.Contains(all[i].thingIDNumber)) record.ChildIds.Add(all[i].thingIDNumber);
                    if (record.SubjectId == 0) record.SubjectId = all[i].thingIDNumber;
                    if (record.SourceAdultId == 0 && comp.State.parentPawnLoadId > 0) record.SourceAdultId = comp.State.parentPawnLoadId;
                }
            }
        }

        internal static void PrepareGame(EventChainRuntime runtime)
        {
            GameComponent_RHAH_Game game = Game;
            EventChainProviderState state = runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey);
            if (game == null || state == null || state.SchemaVersion != EventChainRuntime.SchemaVersion || state.StateVersion != 1 ||
                (state.MigrationStatus ?? "").StartsWith("failed:", StringComparison.Ordinal)) return;
            List<RHAH_LongFollowRecord> recovered = new List<RHAH_LongFollowRecord>();
            for (int i = 0; i < state.Instances.Count; i++)
            {
                EventChainInstance instance = state.Instances[i];
                if (instance == null || instance.End != EventChainEnd.None || (instance.Stage != 1 && instance.Stage != 3) || !RHAH_EventFollowRules.HasLongFollow(instance.DefinitionId)) continue;
                if (instance.Id <= 0 || instance.Id == int.MaxValue || instance.DeadlineTick < -1 || instance.DefinitionVersion != 1 ||
                    !RHAH_EventFollowRules.ReadPayload(instance.Payload, out string id, out int batch, out int site) || id != instance.DefinitionId || site != instance.SiteId)
                    throw new FormatException("Invalid RHAH long-chain migration payload.");
            }
            for (int i = 0; i < state.Instances.Count; i++)
            {
                EventChainInstance instance = state.Instances[i];
                if (instance == null || instance.End != EventChainEnd.None || (instance.Stage != 1 && instance.Stage != 3) || !RHAH_EventFollowRules.HasLongFollow(instance.DefinitionId)) continue;
                RHAH_EventFollowRules.ReadPayload(instance.Payload, out string id, out int batch, out int site);
                if (BatchRecord(id, batch) == null)
                {
                    bool staged = false;
                    for (int j = 0; j < recovered.Count; j++) staged |= recovered[j].DisplayId == id && recovered[j].BatchId == batch;
                    if (!staged)
                    {
                        RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { DisplayId = id, BatchId = batch, MapId = site };
                        RecoverOriginalIds(record);
                        recovered.Add(record);
                    }
                }
            }
            game.LongFollows.AddRange(recovered);
            for (int i = 0; i < state.Instances.Count; i++)
            {
                EventChainInstance instance = state.Instances[i];
                if (instance == null || instance.End != EventChainEnd.None || (instance.Stage != 1 && instance.Stage != 3) || !RHAH_EventFollowRules.HasLongFollow(instance.DefinitionId)) continue;
                RHAH_EventFollowRules.ReadPayload(instance.Payload, out string id, out int batch, out int site);
                RegisterChain(instance.Id, id, batch, site, instance.DeadlineTick);
                // ODF 持久化 setter 是激活前迁移边界 旧期限只作返访起点
                if (instance.Stage == 1)
                {
                    instance.Stage = 3;
                    instance.DeadlineTick = -1;
                    instance.NextTick = RHAH_EventChainMigration.NextLongTick(Now, instance.Id);
                }
            }
        }

        internal static void NoteChildTransfer(string displayId, int batchId, int mapId, Verse.Pawn outgoingChild, Verse.Pawn responsibleAdult, IList<int> incomingIds, bool foodAlternative)
        {
            RHAH_LongFollowRecord record = BatchRecord(displayId, batchId);
            if (record == null) return;
            record.FoodAlternative = foodAlternative;
            record.ResponsibleId = responsibleAdult?.thingIDNumber ?? record.ResponsibleId;
            if (outgoingChild != null && !foodAlternative)
            {
                record.SubjectId = outgoingChild.thingIDNumber;
                if (!record.DepartedIds.Contains(outgoingChild.thingIDNumber)) record.DepartedIds.Add(outgoingChild.thingIDNumber);
                Keep(outgoingChild);
            }
            Wake(record);
        }
        internal static void NotePlayerWounds(Verse.Pawn pawn, IList<Hediff> wounds)
        {
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; pawn != null && wounds != null && records != null && i < records.Count; i++)
            {
                RHAH_LongFollowRecord record = records[i];
                if (record.Completed || (!record.VisitorIds.Contains(pawn.thingIDNumber) && !record.DepartedIds.Contains(pawn.thingIDNumber))) continue;
                for (int j = 0; j < wounds.Count; j++)
                    if (wounds[j] is Hediff_Injury && !record.PlayerWoundIds.Contains(wounds[j].loadID)) record.PlayerWoundIds.Add(wounds[j].loadID);
            }
        }
        internal static bool PlayerFatalBleeding(Verse.Pawn pawn)
        {
            Hediff bloodLoss = pawn?.health?.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss == null || !bloodLoss.IsLethal) return false;
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            List<Hediff> wounds = pawn.health.hediffSet.hediffs;
            for (int i = 0; records != null && i < records.Count; i++)
            {
                if (!records[i].VisitorIds.Contains(pawn.thingIDNumber) && !records[i].DepartedIds.Contains(pawn.thingIDNumber)) continue;
                for (int j = 0; j < wounds.Count; j++)
                    if (wounds[j] is Hediff_Injury && wounds[j].BleedRate > 0f && records[i].PlayerWoundIds.Contains(wounds[j].loadID)) return true;
            }
            return false;
        }
        internal static void NotifyDeath(Verse.Pawn pawn, bool killedByPlayer)
        {
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; pawn != null && records != null && i < records.Count; i++)
            {
                RHAH_LongFollowRecord record = records[i];
                if (killedByPlayer && (record.VisitorIds.Contains(pawn.thingIDNumber) || record.DepartedIds.Contains(pawn.thingIDNumber))) record.PlayerKilledVisitor = true;
            }
            NotifyPawnChanged(pawn);
        }
        internal static void NotifyTrade(Verse.Pawn pawn, bool bought)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (bought && comp != null && ParentId(comp.State.sourceIncidentDisplayId) == "I-012") comp.State.SetExtra(RHAH_EventFollowMood.BoughtKey, "1");
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; !bought && pawn != null && records != null && i < records.Count; i++)
            {
                RHAH_LongFollowRecord record = records[i];
                if ((record.SubjectId == pawn.thingIDNumber || record.ChildIds.Contains(pawn.thingIDNumber)) && !record.DepartedIds.Contains(pawn.thingIDNumber)) record.DepartedIds.Add(pawn.thingIDNumber);
                if (ParentId(record.DisplayId) == "I-019" && record.SubjectId == pawn.thingIDNumber && record.Opened)
                {
                    Mood(pawn, "RHAH_LongHandedAwayAgain", -10, true);
                    Complete(record);
                }
            }
            NotifyPawnChanged(pawn);
        }

        internal static void NotifyPawnChanged(Verse.Pawn pawn)
        {
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; pawn != null && records != null && i < records.Count; i++)
            {
                RHAH_LongFollowRecord record = records[i];
                int id = pawn.thingIDNumber;
                if (record.SubjectId != id && record.SourceAdultId != id && record.ResponsibleId != id && !record.ChildIds.Contains(id) && !record.VisitorIds.Contains(id)) continue;
                Keep(pawn);
                if (record.ChildIds.Contains(id) && !pawn.Dead && pawn.Faction != Faction.OfPlayer && RHAH_Api.Get(pawn)?.IsReleased == true && !record.DepartedIds.Contains(id)) record.DepartedIds.Add(id);
                if (!record.Completed) Wake(record);
            }
        }

        internal static bool NoteDependentVisitors(string displayId, int mapId, IList<int> pawnIds)
        {
            bool opened = false;
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            Map map = RHAH_EventFollowRuntime.FindMap(mapId);
            for (int i = 0; records != null && i < records.Count; i++)
            {
                RHAH_LongFollowRecord record = records[i];
                if (record.MapId != mapId) continue;
                if (RHAH_LongClaimFollow.Handles(record.DisplayId)) RHAH_LongClaimFollow.OnVisitors(record, map, pawnIds, Now);
                if (!record.Completed && ParentId(record.DisplayId) == "I-019" && record.Phase == 8 && (displayId == "I-019" || displayId == "I-040"))
                {
                    record.VisitorIds.Clear();
                    for (int p = 0; pawnIds != null && p < pawnIds.Count; p++) record.VisitorIds.Add(pawnIds[p]);
                    record.Phase = 9;
                    record.Branch = Rand.Bool ? 1 : 0;
                    OpenLetter(record, map);
                    opened = true;
                }
                if (!record.Completed && ParentId(record.DisplayId) == "I-013" && record.Phase == 9 && (displayId == "I-012" || displayId == "I-038"))
                {
                    Say(record, "RHAH_Long_ExchangeMention");
                    Complete(record);
                    opened = true;
                }
                if (!record.Completed) Wake(record);
            }
            return opened;
        }

        internal static int NextTick(int chainId, int tick)
        {
            RHAH_LongFollowRecord record = FindRecord(chainId);
            if (record != null && RHAH_LongClaimFollow.Handles(record.DisplayId) && ((record.Phase == 20 && record.ClaimAcknowledged) || (record.Phase == 7 && record.Branch == 2)) && record.SelectedIds.Count == 0) return -1;
            if (record != null && ((ParentId(record.DisplayId) == "I-019" && record.Phase == 8) || (ParentId(record.DisplayId) == "I-013" && record.Phase == 9))) return -1;
            return AddTicks(tick, GenDate.TicksPerHour);
        }

        internal static EventChainDecision Evaluate(int chainId, int mapId, int tick)
        {
            RHAH_LongFollowRecord record = FindRecord(chainId);
            if (record != null && record.Completed) return EventChainDecision.Finish(EventChainEnd.Completed);
            int nextTick = NextTick(chainId, tick);
            if (record == null) return EventChainDecision.Action("advance_long", nextTick);
            Map map = RHAH_EventFollowRuntime.FindMap(mapId);
            if (map == null) return EventChainDecision.Action("advance_long", nextTick);
            if (map.mapPawns == null || map.Parent == null || map.Parent.Destroyed)
            {
                record.LastTick = tick;
                return EventChainDecision.Wait(nextTick);
            }
            UpdateClock(record, map, tick);
            bool ready = RHAH_LongMotherFollow.Handles(record.DisplayId) ? RHAH_LongMotherFollow.Ready(record, map, tick) :
                RHAH_LongClaimFollow.Handles(record.DisplayId) ? RHAH_LongClaimFollow.Ready(record, map, tick) :
                RHAH_LongAdoptionFollow.Ready(record, map, tick);
            return ready ? EventChainDecision.Action("advance_long", nextTick) : EventChainDecision.Wait(nextTick);
        }

        static void UpdateClock(RHAH_LongFollowRecord record, Map map, int tick)
        {
            Verse.Pawn subject = Pawn(record.SubjectId, tick);
            bool paused = record.Opened && MedicallyPaused(subject);
            if (RHAH_LongMotherFollow.Handles(record.DisplayId) && record.AskedEarly && !record.MedicalHold)
            {
                Verse.Pawn recovering = Pawn(record.SourceAdultId, tick);
                paused |= Alive(recovering) && !AtOriginalEdge(recovering, map);
            }
            for (int i = 0; i < record.VisitorIds.Count; i++) paused |= MedicallyPaused(Pawn(record.VisitorIds[i], tick));
            if (record.MedicalHold)
            {
                Verse.Pawn mother = Pawn(record.SourceAdultId, tick);
                paused |= Alive(mother) && (ParentId(record.DisplayId) == "I-037" ? Sick(mother) : MedicallyPaused(mother));
            }
            int elapsed = record.LastTick < 0 ? 0 : Math.Max(0, tick - record.LastTick);
            record.LastTick = tick;
            if (paused)
            {
                if (record.DueTick >= 0) record.DueTick = AddTicks(record.DueTick, elapsed);
                if (record.DeadlineTick >= 0) record.DeadlineTick = AddTicks(record.DeadlineTick, elapsed);
            }
        }

        internal static string Dispatch(int chainId, string id, int batchId, int mapId, int tick)
        {
            RHAH_LongFollowRecord record = FindRecord(chainId);
            if (record == null) { RegisterChain(chainId, id, batchId, mapId, -1); record = FindRecord(chainId); }
            if (record == null || record.Completed) return "completed";
            Map map = RHAH_EventFollowRuntime.FindMap(mapId);
            if (map == null)
            {
                Complete(record);
                return "map_unavailable";
            }
            if (map.mapPawns == null || map.Parent == null || map.Parent.Destroyed)
            {
                record.LastTick = tick;
                return "map_waiting";
            }
            dispatchingChainId = record.ChainId;
            try
            {
                if (RHAH_LongClaimFollow.Handles(record.DisplayId)) RHAH_LongClaimFollow.ResolvePending(record, map, tick);
            }
            finally
            {
                dispatchingChainId = 0;
            }
            if (record.Completed) return "completed";
            UpdateClock(record, map, tick);
            dispatchingChainId = record.ChainId;
            try
            {
                if (RHAH_LongMotherFollow.Handles(record.DisplayId)) RHAH_LongMotherFollow.Tick(record, map, tick);
                else if (RHAH_LongClaimFollow.Handles(record.DisplayId)) RHAH_LongClaimFollow.Tick(record, map, tick);
                else TickOther(record, map, tick);
            }
            finally
            {
                dispatchingChainId = 0;
            }
            return record.Completed ? "completed" : "phase=" + record.Phase;
        }

        internal static IEnumerable<RHAH_LongOption> Options(RHAH_LongFollowRecord record, Map map)
        {
            if (RHAH_LongMotherFollow.Handles(record.DisplayId)) return RHAH_LongMotherFollow.Options(record, map);
            if (RHAH_LongClaimFollow.Handles(record.DisplayId)) return RHAH_LongClaimFollow.Options(record, map);
            return OtherOptions(record, map);
        }

        internal static bool Choose(int chainId, int phase, string action, int pawnId)
        {
            RHAH_LongFollowRecord record = FindRecord(chainId);
            Map map = record == null ? null : RHAH_EventFollowRuntime.FindMap(record.MapId);
            if (record == null || record.Completed || record.Phase != phase || map == null || !record.LetterOpen) return false;
            Dispatch(record.ChainId, record.DisplayId, record.BatchId, record.MapId, Now);
            if (record.Completed || record.Phase != phase || !record.LetterOpen) return false;
            bool valid = false;
            foreach (RHAH_LongOption option in Options(record, map))
            {
                if (option.Action == action && option.PawnId == pawnId && option.DisabledKey == null) { valid = true; break; }
            }
            if (!valid) return false;
            bool changed;
            if (RHAH_LongMotherFollow.Handles(record.DisplayId)) changed = RHAH_LongMotherFollow.Choose(record, map, action, pawnId, Now);
            else if (RHAH_LongClaimFollow.Handles(record.DisplayId)) changed = RHAH_LongClaimFollow.Choose(record, map, action, pawnId, Now);
            else changed = ChooseOther(record, map, action, pawnId, Now);
            if (changed) Wake(record);
            return changed;
        }

        internal static void Wake(RHAH_LongFollowRecord record)
        {
            if (record.ChainId == dispatchingChainId) return;
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            EventChainInstance instance = runtime?.Find(RHAH_EventChainProvider.ProviderKey, record.ChainId);
            if (instance != null && instance.End == EventChainEnd.None) runtime.SetStage(RHAH_EventChainProvider.ProviderKey, instance.Id, 3, -1, instance.Payload, AddTicks(Now, 1));
        }

        internal static void OpenLetter(RHAH_LongFollowRecord record, Map map)
        {
            if (record.Completed || map == null) return;
            CloseLetter(record);
            LetterDef def = DefDatabase<LetterDef>.GetNamed("RHAH_LongFollowLetter");
            ChoiceLetter_RHAH_LongFollow letter = (ChoiceLetter_RHAH_LongFollow)LetterMaker.MakeLetter(def);
            letter.chainId = record.ChainId;
            letter.phase = record.Phase;
            string original = "RHAH_Follow_" + record.DisplayId;
            if (!(original + "_Text").CanTranslate()) original = "RHAH_Follow_" + ParentId(record.DisplayId);
            string key = RHAH_LongMotherFollow.Handles(record.DisplayId) ? RHAH_LongMotherFollow.TextKey(record) : RHAH_LongClaimFollow.Handles(record.DisplayId) ? RHAH_LongClaimFollow.TextKey(record) : "RHAH_Long_" + ParentId(record.DisplayId) + "_" + record.Phase;
            Verse.Pawn subject = Pawn(record.SubjectId, Now);
            Verse.Pawn adult = Pawn(record.SourceAdultId, Now);
            if (adult == null && record.VisitorIds.Count > 0) adult = Pawn(record.VisitorIds[0], Now);
            if (adult == null) adult = Pawn(record.ResponsibleId, Now);
            string firstName = RHAH_LongClaimFollow.Handles(record.DisplayId) && record.Phase == 8 ? subject?.LabelShort : adult?.LabelShort;
            string hint = key == null ? "" : key.Translate(firstName ?? "-", subject?.LabelShort ?? "-", record.FoodCount, record.MedicineCount).ToString();
            letter.Label = original.CanTranslate() ? original.Translate() : "RHAH_Long_Label".Translate();
            letter.Text = (original + "_Text").CanTranslate() ? (original + "_Text").Translate() + "\n\n" + hint : hint;
            letter.lookTargets = new LookTargets(subject ?? adult);
            record.LetterOpen = true;
            Find.LetterStack.ReceiveLetter(letter);
        }

        internal static void CloseLetter(RHAH_LongFollowRecord record)
        {
            record.LetterOpen = false;
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            for (int i = letters == null ? -1 : letters.Count - 1; i >= 0; i--)
            {
                if (letters[i] is ChoiceLetter_RHAH_LongFollow choice && choice.chainId == record.ChainId) Find.LetterStack.RemoveLetter(choice);
            }
        }

        internal static void Complete(RHAH_LongFollowRecord record)
        {
            record.Completed = true;
            CloseLetter(record);
            for (int i = 0; i < record.VisitorIds.Count; i++) Unhold(Pawn(record.VisitorIds[i], Now));
            Unhold(Pawn(record.SourceAdultId, Now));
        }

        internal static bool TryReserveClaim(RHAH_LongFollowRecord record)
        {
            if (Game == null || (Game.LongClaimChainId != 0 && Game.LongClaimChainId != record.ChainId)) return false;
            Game.LongClaimChainId = record.ChainId;
            return true;
        }

        internal static Verse.Pawn Pawn(int id, int tick) => RHAH_PawnIndex.Find(id, tick);
        internal static bool Alive(Verse.Pawn pawn) => pawn != null && !pawn.Dead && !pawn.Destroyed;
        internal static bool Adult(Verse.Pawn pawn) => pawn?.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat >= (RHAH_Mod.Settings?.followAdultAge ?? 14f);
        internal static bool AtHome(Verse.Pawn pawn, Map map) => Alive(pawn) && pawn.MapHeld == map && pawn.Faction == Faction.OfPlayer;
        internal static bool MedicallyPaused(Verse.Pawn pawn) => Alive(pawn) && RHAH_VisitorStay.PausesStay(pawn);
        internal static bool Sick(Verse.Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            for (int i = 0; hediffs != null && i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (h is Hediff_Injury || (h.def.isBad && (h.def.lethalSeverity > 0f || h.def.tendable))) return true;
            }
            return false;
        }
        internal static int AddTicks(int tick, int amount) => tick >= int.MaxValue - amount ? int.MaxValue : tick + amount;
        internal static void Keep(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.Spawned || pawn.MapHeld != null || pawn.GetCaravan() != null || pawn.holdingOwner != null || Find.WorldPawns == null) return;
            if (Find.WorldPawns.Contains(pawn)) Find.WorldPawns.RemovePawn(pawn);
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        }

        internal static void Mood(Verse.Pawn pawn, string thoughtDef, int amount, bool permanent = false)
        {
            RHAH_EventFollowMood.Apply(pawn, new RHAH_FollowOutcome(thoughtDef, amount, permanent ? RHAH_FollowMood.Permanent : RHAH_FollowMood.Temporary, RHAH_FollowTrait.None));
        }
        internal static void ShareMood(IList<Verse.Pawn> pawns, string thoughtDef, int total)
        {
            if (pawns == null || pawns.Count == 0) return;
            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamed(thoughtDef);
            for (int i = 0; i < pawns.Count; i++)
            {
                Thought_Memory memory = (Thought_Memory)ThoughtMaker.MakeThought(def);
                memory.moodPowerFactor = (float)total / (def.stages[0].baseMoodEffect * pawns.Count);
                pawns[i].needs?.mood?.thoughts?.memories?.TryGainMemory(memory);
            }
        }
        internal static void RemoveMood(Verse.Pawn pawn, string thoughtDef)
        {
            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtDef);
            if (def != null) pawn?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(def);
        }
        internal static void Say(RHAH_LongFollowRecord record, string key, params object[] args)
        {
            if (args == null || args.Length == 0) args = new object[] { Pawn(record.SourceAdultId, Now)?.LabelShort ?? "-", Pawn(record.SubjectId, Now)?.LabelShort ?? "-", record.FoodCount, record.MedicineCount };
            NamedArgument[] formattedArgs = new NamedArgument[args.Length];
            for (int i = 0; i < args.Length; i++) formattedArgs[i] = new NamedArgument(args[i], null);
            Find.LetterStack.ReceiveLetter("RHAH_Long_Label".Translate(), key.Translate(formattedArgs), LetterDefOf.NeutralEvent, new LookTargets(Pawn(record.SubjectId, Now)));
        }
        internal static void Hold(Verse.Pawn pawn) { if (RHAH_Api.IsVisitor(pawn)) RHAH_NarrativePace.HoldVisitor(pawn, HoldKey); }
        internal static void Unhold(Verse.Pawn pawn) { RHAH_Api.SetExtra(pawn, "rhah:longEdge", null); RHAH_NarrativePace.ReturnVisitorGates(pawn, HoldKey); }

        internal static bool CanBring(Verse.Pawn pawn, Map map)
        {
            return Alive(pawn) && map != null && !pawn.IsPrisoner && !pawn.IsSlave && (pawn.MapHeld == map || (pawn.MapHeld == null && pawn.GetCaravan() == null && pawn.holdingOwner == null));
        }
        internal static bool Bring(Verse.Pawn pawn, Map map)
        {
            if (!CanBring(pawn, map)) return false;
            if (!pawn.Spawned && !RHAH_Revisit.TryBringHome(pawn, map)) return false;
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp != null && pawn.Faction != Faction.OfPlayer)
            {
                comp.State.lifecycle = RHAH_Lifecycle.SeekingFood;
                comp.State.ClearFedTimer();
                comp.State.SetLeaveAfter(-1);
                List<Verse.Pawn> group = new List<Verse.Pawn> { pawn };
                RHAH_VisitorGroup.TryStart(group, map, pawn.Position, comp.State.role);
            }
            Keep(pawn);
            Hold(pawn);
            return pawn.Spawned;
        }
        internal static void Leave(Verse.Pawn pawn)
        {
            if (!Alive(pawn) || pawn.IsPrisoner || pawn.IsSlave) return;
            Unhold(pawn);
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp != null && comp.State.lifecycle != RHAH_Lifecycle.Released) RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            if (!pawn.Spawned || MedicallyPaused(pawn)) return;
            if (pawn.CurJobDef == JobDefOf.Steal) return;
            if (RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exit))
            {
                Job job = JobMaker.MakeJob(JobDefOf.Goto, exit);
                job.exitMapOnArrival = true;
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }
        internal static bool SendChildren(RHAH_LongFollowRecord record, Map map, IList<int> ids, Verse.Pawn receiver)
        {
            if (!Alive(receiver) || receiver.MapHeld != map || receiver.Faction == null || receiver.Faction == Faction.OfPlayer || ids == null || ids.Count == 0) return false;
            for (int i = 0; i < ids.Count; i++)
            {
                Verse.Pawn child = Pawn(ids[i], Now);
                bool purchasedSlave = ParentId(record.DisplayId) == "I-012" && Alive(child) && child.MapHeld == map && child.IsSlaveOfColony && RHAH_Api.TryGetExtra(child, RHAH_EventFollowMood.BoughtKey, out string bought);
                if ((!AtHome(child, map) && !purchasedSlave) || child.IsPrisoner || child.IsSlave && !purchasedSlave || (RHAH_Api.IsOrigin(child) && !RHAH_Api.Allows(child, RHAH_BehaviorGate.Transfer))) return false;
                if (RHAH_Api.IsOrigin(child) && !RHAH_ApiHost.CanReleaseToColony(child, RHAH_ReleaseReason.ModRequest)) return false;
            }
            List<Verse.Pawn> group = new List<Verse.Pawn>();
            List<Verse.Pawn> carriers = new List<Verse.Pawn> { receiver };
            int infants = 0;
            for (int i = 0; i < ids.Count; i++) if (!HungerAndHavoc.Pawn.Compat.RHAH_ChildMovement.CanWalkOut(Pawn(ids[i], Now))) infants++;
            for (int i = 1; i < infants; i++)
            {
                Verse.Pawn escort = CreateAdult(record, map);
                if (escort == null) { for (int j = 1; j < carriers.Count; j++) Leave(carriers[j]); return false; }
                escort.SetFaction(receiver.Faction);
                carriers.Add(escort);
                record.VisitorIds.Add(escort.thingIDNumber);
            }
            for (int i = 0; i < ids.Count; i++)
            {
                Verse.Pawn child = Pawn(ids[i], Now);
                if (RHAH_Api.IsOrigin(child)) RHAH_ApiHost.CommitReleaseToColony(child, RHAH_ReleaseReason.ModRequest);
                if (child.IsSlaveOfColony) child.guest.SetGuestStatus(null);
                child.SetFaction(receiver.Faction);
                CompRHAH_Pawn transferred = CompRHAH_Pawn.TryGet(child);
                if (transferred != null) transferred.State.lifecycle = RHAH_Lifecycle.Leaving;
                Keep(child);
                if (!record.DepartedIds.Contains(ids[i])) record.DepartedIds.Add(ids[i]);
                group.Add(child);
            }
            group.AddRange(carriers);
            RHAH_VisitorGroup.TryStart(group, map, receiver.Position, RHAH_PawnRole.Refugee);
            for (int i = 0; i < group.Count; i++) Leave(group[i]);
            int carrierIndex = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                Verse.Pawn child = Pawn(ids[i], Now);
                if (HungerAndHavoc.Pawn.Compat.RHAH_ChildMovement.CanWalkOut(child)) continue;
                Verse.Pawn carrier = carriers[carrierIndex++];
                if (!RCellFinder.TryFindBestExitSpot(carrier, out IntVec3 exit)) continue;
                Job carry = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RHAH_LongMeeting"), child, exit);
                carry.count = 1;
                carry.exitMapOnArrival = true;
                carrier.jobs.StartJob(carry, JobCondition.InterruptForced);
            }
            return true;
        }

        internal static Verse.Pawn CreateAdult(RHAH_LongFollowRecord record, Map map, Gender? gender = null, bool plague = false)
        {
            IntVec3 edge = IntVec3.Invalid;
            if (map != null && !CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out edge)) return null;
            RHAH_PawnCreationResult result = RHAH_PawnFactory.Create(new RHAH_PawnRequest
            {
                SourceIncidentDisplayId = record.DisplayId,
                SpawnBatchId = Math.Max(1, record.BatchId + 1000000 + record.RevisitCount),
                RelationshipGroupId = Math.Max(1, record.BatchId),
                Role = RHAH_PawnRole.Refugee,
                AttitudeAtArrival = RHAH_Attitude.Neutral,
                Faction = RHAH_AttitudeFactions.Require(RHAH_Attitude.Neutral),
                PawnKind = RHAH_DefOf.RHAH_PawnKind_Ratkin,
                Map = map,
                SpawnCell = edge,
                BiologicalAge = 24f,
                Gender = gender,
                CarriesPlague = plague
            }, false);
            if (!result.Succeeded || result.Pawns.Count == 0) return null;
            Verse.Pawn pawn = result.Pawns[0];
            RHAH_PawnIndex.Clear();
            Keep(pawn);
            if (!pawn.Spawned && !Find.WorldPawns.Contains(pawn)) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            return pawn;
        }
        internal static bool SpawnVisitors(RHAH_LongFollowRecord record, Map map, int count, bool hostile = false)
        {
            List<Verse.Pawn> group = new List<Verse.Pawn>();
            for (int i = 0; i < count; i++)
            {
                Verse.Pawn pawn = CreateAdult(record, map);
                if (pawn == null) { for (int j = 0; j < group.Count; j++) { group[j].DeSpawn(); Find.WorldPawns.PassToWorld(group[j], PawnDiscardDecideMode.KeepForever); } return false; }
                group.Add(pawn);
            }
            for (int i = 0; i < group.Count; i++) { record.VisitorIds.Add(group[i].thingIDNumber); Hold(group[i]); }
            RHAH_VisitorGroup.TryStart(group, map, group[0].Position, RHAH_PawnRole.Refugee);
            if (hostile) return StartRevengeGroup(group, map);
            return true;
        }
        internal static bool StartRevenge(RHAH_LongFollowRecord record, Map map)
        {
            record.VisitorIds.Clear();
            return SpawnVisitors(record, map, 3, true);
        }
        static bool StartRevengeGroup(List<Verse.Pawn> group, Map map)
        {
            Faction faction = RHAH_AttitudeFactions.Require(RHAH_Attitude.Hostile);
            for (int i = 0; i < group.Count; i++)
            {
                Verse.Pawn pawn = group[i];
                Unhold(pawn);
                pawn.GetLord()?.RemovePawn(pawn);
                pawn.SetFaction(faction);
                Thing weapon = ThingMaker.MakeThing(ThingDefOf.MeleeWeapon_Knife, ThingDefOf.Steel);
                pawn.equipment?.AddEquipment((ThingWithComps)weapon);
                Thing food = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RawRice")); food.stackCount = 20; pawn.inventory?.innerContainer.TryAdd(food);
            }
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: true), map, group);
            return true;
        }

        internal static bool WasPlayerKilled(Verse.Pawn pawn)
        {
            if (pawn == null || !pawn.Dead) return false;
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; records != null && i < records.Count; i++)
                if (records[i].PlayerKilledVisitor && (records[i].VisitorIds.Contains(pawn.thingIDNumber) || records[i].DepartedIds.Contains(pawn.thingIDNumber))) return true;
            return false;
        }
        internal static void EdgeWait(Verse.Pawn pawn, Map map)
        {
            if (!Alive(pawn) || !pawn.Spawned || pawn.Map != map || MedicallyPaused(pawn)) return;
            Hold(pawn);
            if (!RHAH_Api.TryGetExtra(pawn, "rhah:longEdge", out string ignored)) RHAH_Api.SetExtra(pawn, "rhah:longEdge", pawn.Position.x + "," + pawn.Position.z);
            pawn.GetLord()?.CurLordToil?.UpdateAllDuties();
            Job job = EdgeJob(pawn);
            if (job != null) pawn.jobs.StartJob(job, JobCondition.InterruptForced);
        }
        static bool TryOriginalEdge(Verse.Pawn pawn, Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (!Alive(pawn) || !pawn.Spawned || pawn.Map != map || !RHAH_Api.TryGetExtra(pawn, "rhah:longEdge", out string saved)) return false;
            string[] coords = saved.Split(',');
            if (coords.Length != 2 || !int.TryParse(coords[0], out int x) || !int.TryParse(coords[1], out int z)) return false;
            cell = new IntVec3(x, 0, z);
            return cell.InBounds(map);
        }
        internal static bool AtOriginalEdge(Verse.Pawn pawn, Map map)
        {
            return TryOriginalEdge(pawn, map, out IntVec3 cell) && pawn.Position == cell;
        }
        internal static bool HasEdgeHold(Verse.Pawn pawn)
        {
            if (!TryOriginalEdge(pawn, pawn?.Map, out IntVec3 ignored)) return false;
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; records != null && i < records.Count; i++)
                if (!records[i].Completed && records[i].SourceAdultId == pawn.thingIDNumber && records[i].MedicalHold) return false;
            return true;
        }
        internal static Job EdgeJob(Verse.Pawn pawn)
        {
            if (!HasEdgeHold(pawn) || MedicallyPaused(pawn) || !TryOriginalEdge(pawn, pawn.Map, out IntVec3 cell)) return null;
            return pawn.Position == cell ? JobMaker.MakeJob(JobDefOf.Wait, GenDate.TicksPerHour) : JobMaker.MakeJob(JobDefOf.Goto, cell);
        }

        internal static void NotifyMeeting(Verse.Pawn child, Verse.Pawn mother)
        {
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; child != null && mother != null && records != null && i < records.Count; i++)
            {
                RHAH_LongFollowRecord record = records[i];
                if (record.Completed || record.SubjectId != child.thingIDNumber || record.SourceAdultId != mother.thingIDNumber ||
                    !AtOriginalEdge(mother, mother.Map) || child.MapHeld != mother.Map || mother.PositionHeld.DistanceToSquared(child.PositionHeld) > 9) continue;
                bool changed = RHAH_LongMotherFollow.AcknowledgeLivingMeeting(record, mother.Map, Now);
                if (record.DisplayId == "I-002" && record.Phase == 2 && !record.ClaimAcknowledged)
                {
                    record.ClaimAcknowledged = true;
                    changed = true;
                }
                if (changed) Wake(record);
            }
        }
        internal static bool CanMeetAtEdge(Verse.Pawn mother, Verse.Pawn child, Map map)
        {
            if (!Alive(mother) || !Alive(child) || mother.MapHeld != map || child.MapHeld != map || !child.Spawned) return false;
            if (HungerAndHavoc.Pawn.Compat.RHAH_ChildMovement.CanWalkOut(child))
                return child.CurJobDef?.defName != "RHAH_LongMeetWalk" && child.CanReach(mother, PathEndMode.Touch, Danger.Some);
            foreach (Verse.Pawn carrier in map.mapPawns.FreeColonistsSpawned)
            {
                if (!Adult(carrier) || carrier.Downed || carrier.Drafted || !carrier.CanReach(child, PathEndMode.Touch, Danger.Some) || !carrier.CanReach(mother, PathEndMode.Touch, Danger.Some)) continue;
                if (carrier.CurJobDef?.defName == "RHAH_LongMeeting") return false;
                return true;
            }
            return false;
        }

        internal static bool MeetAtEdge(Verse.Pawn mother, Verse.Pawn child, Map map)
        {
            if (!Alive(mother) || !Alive(child) || mother.MapHeld != map || child.MapHeld != map || !child.Spawned) return false;
            if (HasMetAtEdge(mother, child, map)) return true;
            if (HungerAndHavoc.Pawn.Compat.RHAH_ChildMovement.CanWalkOut(child))
            {
                if (!child.CanReach(mother, PathEndMode.Touch, Danger.Some)) return false;
                child.jobs.StartJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RHAH_LongMeetWalk"), mother), JobCondition.InterruptForced);
                return true;
            }
            foreach (Verse.Pawn carrier in map.mapPawns.FreeColonistsSpawned)
            {
                if (!Adult(carrier) || carrier.Downed || carrier.Drafted || !carrier.CanReach(child, PathEndMode.Touch, Danger.Some) || !carrier.CanReach(mother, PathEndMode.Touch, Danger.Some)) continue;
                if (carrier.CurJobDef?.defName == "RHAH_LongMeeting") return true;
                Job meeting = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RHAH_LongMeeting"), child, mother);
                meeting.count = 1;
                carrier.jobs.StartJob(meeting, JobCondition.InterruptForced);
                return true;
            }
            return false;
        }
        internal static bool HasMetAtEdge(Verse.Pawn mother, Verse.Pawn child, Map map)
        {
            List<RHAH_LongFollowRecord> records = Game?.LongFollows;
            for (int i = 0; child != null && mother != null && records != null && i < records.Count; i++) if (records[i].SubjectId == child.thingIDNumber && records[i].SourceAdultId == mother.thingIDNumber && records[i].ClaimAcknowledged) return true;
            return Alive(mother) && Alive(child) && AtOriginalEdge(mother, map) && child.MapHeld == map && mother.PositionHeld.DistanceToSquared(child.PositionHeld) <= 9;
        }
        internal static void BeginMedicalCare(Verse.Pawn pawn, Map map)
        {
            if (!Alive(pawn) || pawn.MapHeld != map) return;
            Hold(pawn);
            pawn.jobs?.StopAll();
            if (pawn.playerSettings != null) pawn.playerSettings.medCare = MedicalCareCategory.Best;
            if (pawn.Spawned && !pawn.Downed)
            {
                foreach (Building_Bed bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
                {
                    if (bed.Medical && pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some))
                    {
                        pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced);
                        break;
                    }
                }
            }
        }
        internal static bool StartFoodTheft(Verse.Pawn pawn, Map map, bool depart = true)
        {
            if (!Alive(pawn) || !pawn.Spawned || pawn.Map != map || MedicallyPaused(pawn)) return false;
            List<Thing> food = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            Thing target = null;
            for (int i = 0; i < food.Count; i++)
            {
                Thing item = food[i];
                if (item.def.ingestible == null || !item.def.IsNutritionGivingIngestible || item is Corpse || !pawn.CanReach(item, PathEndMode.Touch, Danger.Some) || !pawn.CanReserve(item)) continue;
                if (target == null || item.Position.DistanceToSquared(pawn.Position) < target.Position.DistanceToSquared(pawn.Position)) target = item;
            }
            if (target == null || !RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exit)) { if (depart) Leave(pawn); return false; }
            if (!depart)
            {
                Job take = JobMaker.MakeJob(JobDefOf.Ingest, target); take.count = 1;
                pawn.jobs.StartJob(take, JobCondition.InterruptForced);
                return true;
            }
            Unhold(pawn);
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp != null && comp.State.lifecycle != RHAH_Lifecycle.Released) RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            Job job = JobMaker.MakeJob(JobDefOf.Steal, target, exit); job.count = Math.Min(6, target.stackCount);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            return true;
        }

        static readonly List<Thing> paymentBuffer = new List<Thing>();
        internal static bool CanPay(Map map, int food, int medicine = 0)
        {
            int foods = 0, meds = 0;
            IReadOnlyList<Thing> things = map?.listerThings?.AllThings;
            for (int i = 0; things != null && i < things.Count; i++)
            {
                Thing t = things[i];
                if (!t.Spawned || t.IsForbidden(Faction.OfPlayer)) continue;
                if (t.def.IsNutritionGivingIngestible && !(t is Corpse)) foods += t.stackCount;
                else if (t.def.IsMedicine) meds += t.stackCount;
            }
            return foods >= food && meds >= medicine;
        }
        internal static bool TryPay(Map map, int food, int medicine = 0, Verse.Pawn recipient = null)
        {
            if (!CanPay(map, food, medicine)) return false;
            paymentBuffer.Clear();
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t.Spawned && !t.IsForbidden(Faction.OfPlayer) && ((food > 0 && t.def.IsNutritionGivingIngestible && !(t is Corpse)) || (medicine > 0 && t.def.IsMedicine))) paymentBuffer.Add(t);
            }
            for (int i = 0; i < paymentBuffer.Count; i++)
            {
                Thing t = paymentBuffer[i];
                bool med = t.def.IsMedicine;
                int left = med ? medicine : food;
                int take = Math.Min(left, t.stackCount);
                if (take <= 0) continue;
                Thing paid = t.SplitOff(take);
                if (recipient != null && !med && recipient.needs?.food != null)
                {
                    float nutrition = paid.Ingested(recipient, recipient.needs.food.NutritionWanted);
                    recipient.needs.food.CurLevel += nutrition;
                    if (!paid.Destroyed && paid.stackCount > 0 && recipient.inventory?.innerContainer.TryAdd(paid) != true) GenPlace.TryPlaceThing(paid, recipient.PositionHeld, map, ThingPlaceMode.Near);
                }
                else if (recipient != null && med && recipient.inventory?.innerContainer.TryAdd(paid) == true) { }
                else paid.Destroy();
                if (med) medicine -= take; else food -= take;
            }
            paymentBuffer.Clear();
            return food == 0 && medicine == 0;
        }
        internal static void Feed(Verse.Pawn pawn)
        {
            if (!Alive(pawn) || !pawn.Spawned || pawn.needs?.food == null) return;
            List<Thing> foods = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            for (int i = 0; i < foods.Count; i++)
            {
                Thing food = foods[i];
                if (!food.def.IsNutritionGivingIngestible || !FoodUtility.WillEat(pawn, food) || !pawn.CanReach(food, PathEndMode.Touch, Danger.Some)) continue;
                Job ingest = JobMaker.MakeJob(JobDefOf.Ingest, food); ingest.count = 1;
                pawn.jobs.StartJob(ingest, JobCondition.InterruptForced);
                break;
            }
        }
        internal static bool Capture(RHAH_LongFollowRecord record, Map map)
        {
            List<Verse.Pawn> visitors = new List<Verse.Pawn>();
            for (int i = 0; i < record.VisitorIds.Count; i++) { Verse.Pawn p = Pawn(record.VisitorIds[i], Now); if (Alive(p)) visitors.Add(p); }
            return RHAH_VisitorBatch.Capture(visitors, map.uniqueID) > 0;
        }

        static bool TickOther(RHAH_LongFollowRecord record, Map map, int tick) => RHAH_LongAdoptionFollow.Tick(record, map, tick);
        static IEnumerable<RHAH_LongOption> OtherOptions(RHAH_LongFollowRecord record, Map map) => RHAH_LongAdoptionFollow.Options(record, map);
        static bool ChooseOther(RHAH_LongFollowRecord record, Map map, string action, int pawnId, int tick) => RHAH_LongAdoptionFollow.Choose(record, map, action, pawnId, tick);
    }
}
