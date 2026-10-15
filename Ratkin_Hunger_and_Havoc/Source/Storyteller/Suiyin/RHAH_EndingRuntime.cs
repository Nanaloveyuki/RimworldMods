using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using HungerAndHavoc.Narrative;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_EndingRuntime
    {
        internal const string NarratorDefName = "RHAH_Suiyin";
        internal const int CheckInterval = 1500;

        internal static bool IsNarrator(string defName)
        {
            return defName == NarratorDefName;
        }

        internal static bool CountsNow()
        {
            return Counts(Find.Storyteller?.def?.defName == NarratorDefName, RHAH_Mod.Settings);
        }

        internal static bool Counts(bool narrator, RHAH_Settings settings)
        {
            return narrator;
        }

        internal static void Tick(int tick)
        {
            if (tick < 0 || tick % CheckInterval != 0 || Current.Game == null)
            {
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (state == null)
            {
                return;
            }

            HungerAndHavoc.Incidents.RHAH_GrainHole.ExpireMissingMaps(state, tick);

            bool narrator = Find.Storyteller?.def?.defName == NarratorDefName;
            if (!Counts(narrator, settings))
            {
                DeliverNotices(state, narrator);
                DropReward(state);
                return;
            }

            DeliverNotices(state, narrator);
            DropReward(state);

            int trustFloor = settings == null ? RHAH_EndingRules.TrustFloor : settings.endingTrustFloor;
            int hopeTrust = settings == null ? RHAH_EndingRules.HopeTrust : settings.endingHopeTrust;
            if (state.IdentityDue(narrator, settings == null || settings.endingIdentity, trustFloor, hopeTrust))
            {
                RHAH_IdentityTier offer = RHAH_EndingRules.IdentityOffer(state.Snapshot().Trust, trustFloor, hopeTrust);
                state.MarkIdentity(offer, false);
                ShowIdentity(offer);
            }
        }
        static void DeliverNotices(NarrativeState state, bool narrator)
        {
            SuiyinBook book = state.Book;
            if (book == null)
            {
                return;
            }

            book.Narrator = narrator;
            List<SuiyinNotice> notices = new List<SuiyinNotice>();
            if (book.TakeNotices(notices) == 0)
            {
                return;
            }

            if (Find.LetterStack == null)
            {
                for (int i = 0; i < notices.Count; i++)
                {
                    book.Pending.Add(notices[i]);
                }

                return;
            }

            for (int i = 0; i < notices.Count; i++)
            {
                SuiyinNotice notice = notices[i];
                if (notice.Private && !narrator)
                {
                    continue;
                }


                string key = SuiyinBook.LetterKey(notice.Letter, notice.Arg);
                if (key == null)
                {
                    continue;
                }

                switch (notice.Letter)
                {
                    case SuiyinLetter.N005Accepted:
                    case SuiyinLetter.N005Rejected:
                    case SuiyinLetter.N005TimedOut:
                    case SuiyinLetter.N005Broken:
                    case SuiyinLetter.N006Sealed:
                    case SuiyinLetter.N006BaitSet:
                    case SuiyinLetter.N006Stopped:
                    case SuiyinLetter.N006Cleaned:
                    case SuiyinLetter.N008Trade:
                    case SuiyinLetter.N008Proof:
                    case SuiyinLetter.N008Refuse:
                    case SuiyinLetter.N008Drive:
                    case SuiyinLetter.N009Take:
                    case SuiyinLetter.N009Leave:
                    case SuiyinLetter.N009Hand:
                    case SuiyinLetter.N009Share:
                    case SuiyinLetter.N009Destroy:
                        Messages.Message(NoticeText(notice, book.Config), MessageTypeDefOf.NeutralEvent, false);
                        break;
                    default:
                        Find.LetterStack.ReceiveLetter((key + "_Label").Translate(), NoticeText(notice, book.Config), LetterDefOf.NeutralEvent);
                        break;
                }
            }
        }

        internal static TaggedString NoticeText(SuiyinNotice notice, SuiyinConfig config)
        {
            string key = SuiyinBook.LetterKey(notice.Letter, notice.Arg) + "_Text";
            switch (notice.Letter)
            {
                case SuiyinLetter.N004Banished:
                    return key.Translate(config.RevisitYears);
                case SuiyinLetter.N004RescuePaid:
                    return key.Translate(notice.Arg, config.RevisitYears, config.RescueReward);
                case SuiyinLetter.N008Trade:
                    Verse.Pawn pawn = RHAH_PawnIndex.Find(notice.Arg, Find.TickManager == null ? 0 : Find.TickManager.TicksGame);
                    string name = pawn == null ? "RHAH_Choice_Visitors_Label".Translate().ToString() : pawn.LabelShort;
                    return key.Translate(name);
                case SuiyinLetter.N009Arrive:
                    return key.Translate(config.RelicDays);
                default:
                    return key.Translate(notice.Arg);
            }
        }
        static void DropReward(NarrativeState state)
        {
            int due = state.TakeRewardDue();
            Map map = Find.AnyPlayerHomeMap;
            if (due <= 0)
            {
                return;
            }

            if (map == null || ThingDefOf.Silver == null)
            {
                state.RestoreReward(due);
                return;
            }

            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = due;
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(map),
                map,
                new List<Thing> { silver },
                110,
                false,
                false,
                true,
                false);
        }




        internal static string IdentityKey(RHAH_IdentityTier tier)
        {
            return tier == RHAH_IdentityTier.Full ? "RHAH_Ending_IdentityFull" : "RHAH_Ending_IdentityPartial";
        }

        static void ShowIdentity(RHAH_IdentityTier tier)
        {
            string key = IdentityKey(tier);
            Find.LetterStack.ReceiveLetter((key + "_Label").Translate(), (key + "_Text").Translate(), LetterDefOf.NeutralEvent);
        }

    }

    // 只改穗音自己的家园 RandomMain 不改其它叙事者、任务袭击、商队和本模组事件池
    [HarmonyPatch(typeof(StorytellerComp_RandomMain), "ChooseRandomCategory")]
    internal static class RHAH_ThreatTempoPatch
    {
        internal static bool Prefix(
            StorytellerComp_RandomMain __instance,
            IIncidentTarget target,
            List<IncidentCategoryDef> skipCategories,
            ref IncidentCategoryDef __result)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (settings != null && !settings.suiYinThreatTempo)
            {
                return true;
            }

            if (Find.Storyteller?.def?.defName != RHAH_EndingRuntime.NarratorDefName || !(target is Map map) || !map.IsPlayerHome)
            {
                return true;
            }

            NarrativeState state = Current.Game?.GetComponent<NarrativeState>();
            int trust = state == null ? 0 : state.Snapshot().Trust;
            if (trust == 0 || __instance?.props is not StorytellerCompProperties_RandomMain props)
            {
                return true;
            }

            float factor = RHAH_EndingRules.ThreatFactor(trust);
            if (!skipCategories.Contains(IncidentCategoryDefOf.ThreatBig) &&
                Find.TickManager.TicksGame - target.StoryState.LastThreatBigTick > 60000f * props.maxThreatBigIntervalDays / factor)
            {
                __result = IncidentCategoryDefOf.ThreatBig;
            }
            else
            {
                __result = Weighted(props, skipCategories, factor);
            }

            return __result != null ? false : true;
        }

        static IncidentCategoryDef Weighted(StorytellerCompProperties_RandomMain props, List<IncidentCategoryDef> skip, float factor)
        {
            float total = 0f;
            List<IncidentCategoryEntry> weights = props.categoryWeights;
            for (int i = 0; i < weights.Count; i++)
            {
                IncidentCategoryEntry entry = weights[i];
                if (entry?.category == null || skip.Contains(entry.category))
                {
                    continue;
                }

                total += entry.category == IncidentCategoryDefOf.ThreatBig ? entry.weight * factor : entry.weight;
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = Rand.Value * total;
            for (int i = 0; i < weights.Count; i++)
            {
                IncidentCategoryEntry entry = weights[i];
                if (entry?.category == null || skip.Contains(entry.category))
                {
                    continue;
                }

                roll -= entry.category == IncidentCategoryDefOf.ThreatBig ? entry.weight * factor : entry.weight;
                if (roll <= 0f)
                {
                    return entry.category;
                }
            }

            return null;
        }
    }
}
