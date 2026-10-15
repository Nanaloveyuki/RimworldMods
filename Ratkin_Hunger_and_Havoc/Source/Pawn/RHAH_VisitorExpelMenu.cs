using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using Verse.AI.Group;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    // 原版扫描公开子类 殖民者右键在场来客时出现
    public class RHAH_VisitorExpelMenu : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Verse.Pawn clickedPawn, FloatMenuContext context)
        {
            if (!RHAH_BatchAttitude.CanOrderLeave(clickedPawn))
            {
                yield break;
            }

            Verse.Pawn actor = context.FirstSelectedPawn;
            if (actor == null || actor.Faction != Faction.OfPlayer || actor.Dead)
            {
                yield break;
            }

            yield return new FloatMenuOption(
                "RHAH_Menu_Expel".Translate(clickedPawn.LabelShort),
                () => AssignExpel(actor, clickedPawn),
                MenuOptionPriority.Default,
                null,
                clickedPawn);
            if (RHAH_Api.Allows(clickedPawn, RHAH_BehaviorGate.JoinColony))
            {
                yield return new FloatMenuOption("RHAH_Choice_Join".Translate(), () =>
                {
                    RHAH_Api.ReleaseToColony(clickedPawn, RHAH_ReleaseReason.JoinedPlayerFaction);
                    RHAH_VisitorRules.ApplyPlayerIdeo(
                        clickedPawn,
                        RHAH_Mod.Settings == null ? 100 : RHAH_Mod.Settings.playerIdeoPercent,
                        Rand.Value);
                    clickedPawn.SetFaction(Faction.OfPlayer);
                    RHAH_VisitorMood.NotifySheltered(clickedPawn);
                });
            }

            if (RHAH_Api.Allows(clickedPawn, RHAH_BehaviorGate.Hire))
            {
                yield return new FloatMenuOption("RHAH_Choice_Hire".Translate(StayText(true)), () =>
                    RHAH_VisitorStay.Begin(clickedPawn, RHAH_StayKind.Hire));
            }
            if (RHAH_Api.Allows(clickedPawn, RHAH_BehaviorGate.JoinColony))
            {
                yield return new FloatMenuOption("RHAH_Choice_Shelter".Translate(StayText(false)), () =>
                    RHAH_VisitorStay.Begin(clickedPawn, RHAH_StayKind.Shelter));
            }

            string blocked = RHAH_FoodHandoff.Refusal(clickedPawn);
            if (blocked != null)
            {
                yield return new FloatMenuOption("RHAH_Choice_Feed".Translate(), null)
                {
                    Disabled = true,
                    tooltip = new TipSignal(blocked.Translate(clickedPawn.LabelShort))
                };
            }
            else
            {
                int each = RHAH_RequestRules.FoodRequestCount(1);
                if (each <= 0 || JobDefOf.GiveToPawn == null)
                {
                    yield return new FloatMenuOption("RHAH_Choice_Feed".Translate(), null)
                    {
                        Disabled = true,
                        tooltip = new TipSignal("RHAH_Choice_FeedBlocked".Translate(clickedPawn.LabelShort))
                    };
                }
                else
                {
                    LordJob_RHAH_Visitor job = clickedPawn.GetLord() == null
                        ? null
                        : clickedPawn.GetLord().LordJob as LordJob_RHAH_Visitor;
                    List<Thing> foods = new List<Thing>();
                    RHAH_FoodHandoff.CollectFood(actor, foods);
                    ThingDef requested = job == null ? null : job.RequestedFood;
                    List<Thing> offered = new List<Thing>();
                    for (int i = 0; i < foods.Count; i++)
                    {
                        Thing food = foods[i];
                        if (food != null && food.def != null && (requested == null || food.def == requested))
                        {
                            offered.Add(food);
                        }
                    }

                    if (offered.Count == 0)
                    {
                        yield return new FloatMenuOption(
                            "RHAH_Choice_NoFood".Translate(each, clickedPawn.LabelShort),
                            null);
                    }

                    for (int i = 0; i < offered.Count; i++)
                    {
                        Thing food = offered[i];
                        yield return new FloatMenuOption(
                            "RHAH_Choice_GiveFood".Translate(each + " " + food.def.label, clickedPawn.LabelShort),
                            () => GiveFood(actor, clickedPawn, food, each));
                    }
                }
            }
        }

        static void GiveFood(Verse.Pawn actor, Verse.Pawn receiver, Thing food, int count)
        {
            if (actor == null || actor.Dead || actor.jobs == null || food == null || receiver == null || count <= 0)
            {
                return;
            }

            if (JobDefOf.GiveToPawn == null)
            {
                Messages.Message(
                    "RHAH_Choice_FeedBlocked".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            if (!RHAH_FoodHandoff.Start(receiver, count))
            {
                Messages.Message(
                    "RHAH_Choice_FeedNoVisitor".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            LordJob_RHAH_Visitor job = receiver.GetLord() == null
                ? null
                : receiver.GetLord().LordJob as LordJob_RHAH_Visitor;
            if (job != null && job.RequestedFood != null && job.RequestedFood != food.def)
            {
                Messages.Message(
                    "RHAH_Choice_FeedBlocked".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            int needed = job == null ? count : job.FoodStillNeeded(receiver);
            if (needed <= 0)
            {
                Messages.Message(
                    "RHAH_Choice_FeedNotHungry".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            if (job != null)
            {
                job.NoteFood(food.def);
            }

            Job give = JobMaker.MakeJob(JobDefOf.GiveToPawn, food, receiver);
            give.haulMode = HaulMode.ToContainer;
            give.count = needed;
            give.lord = receiver.GetLord();
            actor.jobs.TryTakeOrderedJob(give, JobTag.Misc);
        }

        static void AssignExpel(Verse.Pawn actor, Verse.Pawn target)
        {
            if (actor == null || actor.Dead || actor.jobs == null || RHAH_DefOf.RHAH_Expel == null)
            {
                return;
            }

            if (!RHAH_BatchAttitude.CanOrderLeave(target) || target.Map != actor.Map)
            {
                return;
            }

            Job job = JobMaker.MakeJob(RHAH_DefOf.RHAH_Expel, target);
            job.playerForced = true;
            actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        static string StayText(bool hire)
        {
            HungerAndHavoc.Core.RHAH_Settings settings = HungerAndHavoc.Core.RHAH_Mod.Settings;
            int days = hire
                ? (settings == null ? RHAH_VisitorRules.DefaultHireDays : settings.hireDays)
                : (settings == null ? RHAH_VisitorRules.DefaultShelterDays : settings.shelterDays);
            return RHAH_VisitorRules.StayLabel(days);
        }
    }

    // 原版按整个 Lord 扣正在搬运的数量 同批第二人会被第一人的搬运清成 0
    [HarmonyLib.HarmonyPatch(typeof(GiveItemsToPawnUtility), nameof(GiveItemsToPawnUtility.ItemCountLeftToCollect))]
    internal static class RHAH_GiveToPawnCountPatch
    {
        static bool Prefix(Verse.Pawn requester, ref int __result)
        {
            if (!RHAH_FoodHandoff.TryCountLeft(requester, out int left))
            {
                return true;
            }

            __result = left;
            return false;
        }
    }
}
