using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 乞讨冷却和已讨对象存在档里 读档后同一人不能再算第一次
    internal static class RHAH_Begging
    {
        static readonly Dictionary<int, int> NextBegTickByPawnId = new Dictionary<int, int>();
        static readonly Dictionary<int, HashSet<int>> BeggedColonists = new Dictionary<int, HashSet<int>>();

        internal static bool PrefersGnaw(Verse.Pawn pawn)
        {
            return Identity.CompRHAH_Pawn.TryGet(pawn)?.State.PrefersGnaw == true;
        }

        internal static void NoteFoodReceived(Verse.Pawn pawn)
        {
            Identity.CompRHAH_Pawn comp = Identity.CompRHAH_Pawn.TryGet(pawn);
            if (comp != null)
            {
                comp.State.begSlapCount = 0;
            }
        }

        internal static void Reset()
        {
            NextBegTickByPawnId.Clear();
            BeggedColonists.Clear();
            Store();
        }

        internal static void Load(List<int> pawnIds, List<int> ticks, List<int> beggarIds, List<int> colonistIds)
        {
            NextBegTickByPawnId.Clear();
            BeggedColonists.Clear();
            int cooldown = pawnIds == null || ticks == null ? 0 : pawnIds.Count < ticks.Count ? pawnIds.Count : ticks.Count;
            for (int i = 0; i < cooldown; i++)
            {
                if (ticks[i] >= 0)
                {
                    NextBegTickByPawnId[pawnIds[i]] = ticks[i];
                }
            }

            int begged = beggarIds == null || colonistIds == null
                ? 0
                : beggarIds.Count < colonistIds.Count ? beggarIds.Count : colonistIds.Count;
            for (int i = 0; i < begged; i++)
            {
                HashSet<int> targets;
                if (!BeggedColonists.TryGetValue(beggarIds[i], out targets))
                {
                    targets = new HashSet<int>();
                    BeggedColonists[beggarIds[i]] = targets;
                }

                targets.Add(colonistIds[i]);
            }
        }

        static void Store()
        {
            Current.Game?.GetComponent<GameComponent_RHAH_Game>()?.StoreBegging(NextBegTickByPawnId, BeggedColonists);
        }

        internal static bool CanBegAgain(Verse.Pawn pawn, int now)
        {
            if (pawn == null)
            {
                return false;
            }

            int until;
            if (!NextBegTickByPawnId.TryGetValue(pawn.thingIDNumber, out until))
            {
                return true;
            }

            if (RHAH_VisitorRules.BegCooldownReady(now, until))
            {
                NextBegTickByPawnId.Remove(pawn.thingIDNumber);
                Store();
                return true;
            }

            return false;
        }

        internal static void StartFailCooldown(Verse.Pawn pawn, int now, int hours)
        {
            if (pawn == null)
            {
                return;
            }

            NextBegTickByPawnId[pawn.thingIDNumber] = RHAH_VisitorRules.NextBegTick(now, hours);
            Store();
        }

        internal static int CooldownHours()
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            int hours = settings == null
                ? RHAH_VisitorRules.DefaultBegFailCooldownHours
                : settings.begFailCooldownHours;
            return RHAH_VisitorRules.ClampBegFailCooldownHours(hours);
        }
        internal static bool AutoGiveEnabled()
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            return settings != null && settings.begAutoGiveEnabled;
        }

        internal static int SuccessChancePercent()
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            int percent = settings == null
                ? RHAH_VisitorRules.DefaultBegSuccessChancePercent
                : settings.begSuccessChancePercent;
            return RHAH_VisitorRules.ClampPercent(
                percent,
                RHAH_VisitorRules.MinBegSuccessChancePercent,
                RHAH_VisitorRules.MaxBegSuccessChancePercent);
        }

        internal static int SocialBonusPercent()
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            int percent = settings == null
                ? RHAH_VisitorRules.DefaultBegSocialBonusPercent
                : settings.begSocialBonusPercent;
            return RHAH_VisitorRules.ClampPercent(
                percent,
                RHAH_VisitorRules.MinBegSocialBonusPercent,
                RHAH_VisitorRules.MaxBegSocialBonusPercent);
        }

        internal static int SlapChancePercent()
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            int percent = settings == null
                ? RHAH_VisitorRules.DefaultBegSlapChancePercent
                : settings.begSlapChancePercent;
            return RHAH_VisitorRules.ClampBegSlapChance(percent);
        }

        internal static bool HasBegged(Verse.Pawn beggar, Verse.Pawn colonist)
        {
            HashSet<int> targets;
            return beggar != null &&
                colonist != null &&
                BeggedColonists.TryGetValue(beggar.thingIDNumber, out targets) &&
                targets.Contains(colonist.thingIDNumber);
        }

        internal static void RememberBeg(Verse.Pawn beggar, Verse.Pawn colonist)
        {
            if (beggar == null || colonist == null)
            {
                return;
            }

            HashSet<int> targets;
            if (!BeggedColonists.TryGetValue(beggar.thingIDNumber, out targets))
            {
                targets = new HashSet<int>();
                BeggedColonists[beggar.thingIDNumber] = targets;
            }

            targets.Add(colonist.thingIDNumber);
            Store();
        }

        internal static bool ShouldSlap(Verse.Pawn beggar, Verse.Pawn colonist, float roll)
        {
            return HasBegged(beggar, colonist) &&
                RHAH_VisitorRules.RollsSlap(SlapChancePercent(), roll);
        }

        internal static bool ToddlersActive()
        {
            return ModsConfig.IsActive("cyanobot.toddlers");
        }

        internal static bool Sleeping(Verse.Pawn pawn)
        {
            return pawn != null && !pawn.Awake();
        }

        internal static bool OnMedicalBed(Verse.Pawn pawn)
        {
            if (pawn == null || !pawn.InBed())
            {
                return false;
            }

            Building_Bed bed = pawn.CurrentBed();
            return bed != null && bed.Medical;
        }

        internal static float Age(Verse.Pawn pawn)
        {
            return pawn != null && pawn.ageTracker != null
                ? pawn.ageTracker.AgeBiologicalYearsFloat
                : 0f;
        }

        internal static bool CanReceive(Verse.Pawn beggar, Verse.Pawn colonist, bool reachable, bool reservable)
        {
            if (colonist == null)
            {
                return false;
            }

            return RHAH_VisitorRules.CanReceiveBeg(
                colonist.Dead,
                colonist.Downed,
                colonist.IsForbidden(beggar),
                Sleeping(colonist),
                OnMedicalBed(colonist),
                reachable,
                reservable,
                Age(colonist),
                ToddlersActive());
        }

        internal static void NoteFailure(Verse.Pawn beggar, Verse.Pawn target)
        {
            if (beggar == null || !beggar.Spawned)
            {
                return;
            }

            ThoughtDef rejected = DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_BeggingRejected");
            if (rejected != null && beggar.needs != null && beggar.needs.mood != null && beggar.needs.mood.thoughts != null)
            {
                Thought_Memory memory = (Thought_Memory)ThoughtMaker.MakeThought(rejected);
                memory.moodOffset = RHAH_Mod.Settings == null ? RHAH_VisitorRules.BegFailMood : RHAH_Mod.Settings.begFailMood;
                beggar.needs.mood.thoughts.memories.TryGainMemory(memory, target);
            }

            string label = target != null ? target.LabelShort : "...";
            int roll = Rand.Range(1, 4);
            MoteMaker.ThrowText(
                beggar.DrawPos,
                beggar.Map,
                ("RHAH_Beg_Text_Fail_" + roll).Translate(label),
                3f);
        }

        internal static bool TryTakeFood(Verse.Pawn beggar, Verse.Pawn colonist)
        {
            if (!AutoGiveEnabled() || beggar == null || colonist == null || colonist.inventory == null)
            {
                return false;
            }

            ThingOwner container = colonist.inventory.innerContainer;
            if (container == null)
            {
                return false;
            }

            Thing best = null;
            float bestNutrition = float.MinValue;
            for (int i = 0; i < container.Count; i++)
            {
                Thing thing = container[i];
                if (thing == null || thing.def == null || thing.stackCount <= 0 || !thing.IngestibleNow)
                {
                    continue;
                }

                if (!RHAH_ReliefFood.BegFoodAllowed(thing.def))
                {
                    continue;
                }

                float nutrition = thing.GetStatValue(StatDefOf.Nutrition);
                if (float.IsNaN(nutrition) || float.IsInfinity(nutrition))
                {
                    nutrition = 0f;
                }

                if (best == null || nutrition > bestNutrition)
                {
                    best = thing;
                    bestNutrition = nutrition;
                }
            }

            if (best == null)
            {
                return false;
            }

            Thing taken = best.SplitOff(1);
            if (taken == null)
            {
                return false;
            }

            if (beggar.inventory != null && beggar.inventory.innerContainer != null &&
                beggar.inventory.innerContainer.TryAdd(taken, true))
            {
                return true;
            }

            if (beggar.Spawned && GenPlace.TryPlaceThing(taken, beggar.Position, beggar.Map, ThingPlaceMode.Near, null, null, null))
            {
                return true;
            }

            if (!taken.Destroyed)
            {
                taken.Destroy(DestroyMode.Vanish);
            }

            return false;
        }

        internal static void NoteSuccess(Verse.Pawn beggar, Verse.Pawn target)
        {
            if (beggar == null || !beggar.Spawned)
            {
                return;
            }

            NoteFoodReceived(beggar);
            ThoughtDef succeeded = DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_BeggingSucceeded");
            if (succeeded != null && beggar.needs != null && beggar.needs.mood != null && beggar.needs.mood.thoughts != null)
            {
                Thought_Memory memory = (Thought_Memory)ThoughtMaker.MakeThought(succeeded);
                memory.moodOffset = RHAH_Mod.Settings == null ? RHAH_VisitorRules.BegSuccessMood : RHAH_Mod.Settings.begSuccessMood;
                beggar.needs.mood.thoughts.memories.TryGainMemory(memory, target);
            }

            int roll = Rand.Range(1, 4);
            MoteMaker.ThrowText(
                beggar.DrawPos,
                beggar.Map,
                ("RHAH_Beg_Text_Success_" + roll).Translate(),
                3f);
        }

        internal static void TrySlap(Verse.Pawn beggar, Verse.Pawn colonist)
        {
            if (beggar == null || colonist == null || beggar.health == null)
            {
                return;
            }

            if (!ShouldSlap(beggar, colonist, Rand.Value))
            {
                return;
            }

            BodyPartRecord head = null;
            if (beggar.RaceProps != null && beggar.RaceProps.body != null)
            {
                List<BodyPartRecord> heads = beggar.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Head);
                if (heads != null && heads.Count > 0)
                {
                    head = heads[0];
                }
            }

            if (head == null)
            {
                return;
            }

            ApplyHeadWound(beggar, head);
            Identity.CompRHAH_Pawn comp = Identity.CompRHAH_Pawn.TryGet(beggar);
            if (comp != null)
            {
                comp.State.NoteBegSlap();
                comp.State.gnawSearchUntilTick = -1;
            }
            if (beggar.stances != null && beggar.stances.stunner != null)
            {
                int hours = RHAH_Mod.Settings == null ? RHAH_VisitorRules.BegSlapKnockoutHours : RHAH_Mod.Settings.begSlapKnockoutHours;
                int ticks = hours <= 0 ? RHAH_VisitorRules.BegSlapKnockoutTicks : hours * RHAH_VisitorRules.TicksPerHour;
                beggar.stances.stunner.StunFor(
                    ticks,
                    colonist,
                    true,
                    true,
                    false);
            }

            ThoughtDef slapped = DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_BeggingSlapped");
            if (slapped != null && beggar.needs != null && beggar.needs.mood != null && beggar.needs.mood.thoughts != null)
            {
                Thought_Memory memory = (Thought_Memory)ThoughtMaker.MakeThought(slapped);
                memory.moodOffset = RHAH_Mod.Settings == null ? RHAH_VisitorRules.BegSlapMood : RHAH_Mod.Settings.begSlapMood;
                beggar.needs.mood.thoughts.memories.TryGainMemory(memory, colonist);
            }

            if (beggar.Spawned)
            {
                MoteMaker.ThrowText(beggar.DrawPos, beggar.Map, "RHAH_Beg_Text_Slap".Translate(), 3f);
            }
        }

        static void ApplyHeadWound(Verse.Pawn beggar, BodyPartRecord head)
        {
            Hediff bruise = null;
            List<Hediff> hediffs = beggar.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff != null && hediff.def == DefDatabase<HediffDef>.GetNamed("Bruise") && hediff.Part == head)
                {
                    bruise = hediff;
                    break;
                }
            }

            int kind = RHAH_VisitorRules.SlapWoundKind(bruise != null, bruise != null ? bruise.Severity : 0f);
            if (kind == 2)
            {
                Hediff bleed = HediffMaker.MakeHediff(HediffDefOf.Cut, beggar, head);
                bleed.Severity = RHAH_VisitorRules.ModerateBleedSeverity;
                beggar.health.AddHediff(bleed, head, null, null);
                return;
            }

            if (bruise == null)
            {
                bruise = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise"), beggar, head);
                bruise.Severity = RHAH_Mod.Settings == null ? RHAH_VisitorRules.MinorBruiseSeverity : RHAH_Mod.Settings.begBruiseSeverity;
                beggar.health.AddHediff(bruise, head, null, null);
                return;
            }

            bruise.Severity = RHAH_VisitorRules.NextBruiseSeverity(bruise.Severity);
        }
    }
}
