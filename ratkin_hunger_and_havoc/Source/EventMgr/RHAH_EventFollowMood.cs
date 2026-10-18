using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal static class RHAH_EventFollowMood
    {
        internal const string HurtKey = "rhah:hurt";
        internal const string BitFoodKey = "rhah:bitFood";
        internal const string BitPersonKey = "rhah:bitPerson";
        internal const string BoughtKey = "rhah:bought";
        internal const string GaveChildKey = "rhah:gave";
        internal const string FoodKey = "rhah:foodSub";
        internal const string MoodKey = "rhah:mood";

        internal static void OnChoice(RHAH_ChoiceRecord record)
        {
            if (record == null || record.Settled == RHAH_ChoiceAction.None)
            {
                return;
            }

            List<Verse.Pawn> pawns = Present(record);
            bool substitute = RHAH_ChildExchange.FoodAlternative(record);
            bool gave = (record.DisplayId == "I-013" || record.DisplayId == "I-019") && record.Settled == RHAH_ChoiceAction.Deliver && !substitute;
            bool bought = (record.DisplayId == "I-012" || record.DisplayId == "I-038") && record.Settled == RHAH_ChoiceAction.Deliver;
            for (int i = 0; i < pawns.Count; i++)
            {
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawns[i]);
                if (comp == null)
                {
                    continue;
                }

                if (bought)
                {
                    comp.State.SetExtra(BoughtKey, "1");
                }

                if (gave)
                {
                    comp.State.SetExtra(GaveChildKey, "1");
                }

                if (substitute)
                {
                    comp.State.SetExtra(FoodKey, "1");
                }

                if (record.DisplayId == "I-006" &&
                    (record.Settled == RHAH_ChoiceAction.Join || record.Settled == RHAH_ChoiceAction.Recruit || record.Settled == RHAH_ChoiceAction.Hire) &&
                    comp.State.TryGetExtra(HurtKey, out string _))
                {
                    Apply(pawns[i], RHAH_EventFollowRules.OnJoinedAfterHarm(Subject(pawns[i], comp), Rand.Value));
                }
            }
        }

        internal static void OnRelief(string displayId, IList<Verse.Pawn> living)
        {
            if (living == null || living.Count == 0)
            {
                return;
            }

            Verse.Pawn oldest = living[0];
            for (int i = 1; i < living.Count; i++)
            {
                if (living[i]?.ageTracker != null && (oldest?.ageTracker == null || living[i].ageTracker.AgeBiologicalYears > oldest.ageTracker.AgeBiologicalYears))
                {
                    oldest = living[i];
                }
            }

            for (int i = 0; i < living.Count; i++)
            {
                Verse.Pawn pawn = living[i];
                if (pawn == null || pawn.Dead)
                {
                    continue;
                }

                bool sick = Sick(pawn);
                Apply(pawn, RHAH_EventFollowRules.OnRelief(displayId, true, true, sick, pawn == oldest, Rand.Value));
            }
        }

        internal static void OnBirthday(Verse.Pawn pawn, int age)
        {
            if (age < (Core.RHAH_Mod.Settings == null ? RHAH_EventFollowRules.AdultAge : Core.RHAH_Mod.Settings.followAdultAge))
            {
                return;
            }

            TickPawn(pawn);
        }

        internal static void TickPawn(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null || pawn.Dead || pawn.ageTracker == null || comp.State.TryGetExtra(MoodKey, out string _))
            {
                return;
            }

            if (pawn.ageTracker.AgeBiologicalYearsFloat < (Core.RHAH_Mod.Settings == null ? RHAH_EventFollowRules.AdultAge : Core.RHAH_Mod.Settings.followAdultAge))
            {
                return;
            }

            Verse.Pawn mother = Mother(comp.State.parentPawnLoadId);
            bool motherAlive = mother != null && !mother.Dead;
            bool motherOnMap = motherAlive && mother.Spawned && mother.Map == pawn.Map;
            RHAH_FollowOutcome outcome = RHAH_EventFollowRules.AtFourteen(Subject(pawn, comp), motherAlive, motherOnMap, Rand.Value);
            if (!outcome.Applies)
            {
                return;
            }

            Apply(pawn, outcome);
            comp.State.SetExtra(MoodKey, outcome.Thought);
        }

        internal static void NoteHurt(Verse.Pawn pawn)
        {
            CompRHAH_Pawn.TryGet(pawn)?.State.SetExtra(HurtKey, "1");
        }

        internal static void NoteBit(Verse.Pawn pawn, bool person)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return;
            }

            comp.State.SetExtra(person ? BitPersonKey : BitFoodKey, "1");
        }

        internal static void NoteBirth(Verse.Pawn mother, Verse.Pawn baby)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(mother);
            if (comp == null || baby == null)
            {
                return;
            }

            string id = comp.State.sourceIncidentDisplayId;
            if (id != "I-029" && id != "I-044")
            {
                return;
            }

            comp.State.SetExtra("rhah:birth", Find.TickManager == null ? "0" : Find.TickManager.TicksGame.ToString());
            comp.State.SetExtra("rhah:baby", baby.thingIDNumber.ToString());
            HediffDef plague = DefDatabase<HediffDef>.GetNamedSilentFail("RHAH_Plague");
            if (plague != null && baby.health?.hediffSet != null && baby.health.hediffSet.HasHediff(plague))
            {
                Apply(mother, RHAH_EventFollowRules.OnBirth(id, true, true, true, 0, Has(mother, "Kind"), Has(mother, "Psychopath"), Rand.Value));
            }
        }

        internal static void NoteTraderSpread(Map map)
        {
            IReadOnlyList<Verse.Pawn> pawns = map?.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawns[i]);
                if (comp != null && comp.State.sourceIncidentDisplayId == "I-038")
                {
                    comp.State.SetExtra("rhah:spread", "1");
                    return;
                }
            }
        }

        internal static void Apply(Verse.Pawn pawn, RHAH_FollowOutcome outcome)
        {
            if (!outcome.Applies || pawn?.needs?.mood?.thoughts?.memories == null)
            {
                return;
            }

            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(outcome.Thought);
            if (def == null)
            {
                return;
            }

            Thought_Memory memory = (Thought_Memory)ThoughtMaker.MakeThought(def);
            memory.moodOffset = outcome.Mood;
            if (outcome.Duration == RHAH_FollowMood.Permanent)
            {
                memory.permanent = true;
                memory.durationTicksOverride = -1;
            }

            pawn.needs.mood.thoughts.memories.TryGainMemory(memory);
            Gain(pawn, outcome.Trait);
        }

        static bool Sick(Verse.Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff is Hediff_Injury || (hediff.def != null && hediff.def.makesSickThought))
                {
                    return true;
                }
            }

            return false;
        }

        static void Gain(Verse.Pawn pawn, RHAH_FollowTrait trait)
        {
            if (trait == RHAH_FollowTrait.None || pawn.story?.traits == null)
            {
                return;
            }

            string name = trait == RHAH_FollowTrait.Kind ? "Kind" : "Cannibal";
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(name);
            if (def == null || pawn.story.traits.HasTrait(def))
            {
                return;
            }

            pawn.story.traits.GainTrait(new Trait(def, 0, false), false);
        }

        static RHAH_FollowSubject Subject(Verse.Pawn pawn, CompRHAH_Pawn comp)
        {
            RHAH_PawnState state = comp.State;
            return new RHAH_FollowSubject(
                state.sourceIncidentDisplayId,
                state.role,
                pawn.thingIDNumber,
                state.parentPawnLoadId,
                pawn.ageTracker == null ? 0f : pawn.ageTracker.AgeBiologicalYearsFloat,
                !pawn.Dead,
                pawn.Spawned,
                state.lifecycle == RHAH_Lifecycle.Released,
                state.TryGetExtra(HurtKey, out string _),
                state.TryGetExtra(BitFoodKey, out string _),
                state.TryGetExtra(BitPersonKey, out string _),
                Has(pawn, "Kind"),
                Has(pawn, "Psychopath") || Has(pawn, "Cannibal"),
                false,
                !state.carriesPlague,
                state.TryGetExtra(BoughtKey, out string _),
                state.TryGetExtra(GaveChildKey, out string _),
                state.TryGetExtra(FoodKey, out string _),
                false,
                false,
                false);
        }

        static bool Has(Verse.Pawn pawn, string defName)
        {
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            return def != null && pawn.story?.traits != null && pawn.story.traits.HasTrait(def);
        }

        static Verse.Pawn Mother(int loadId)
        {
            if (loadId <= 0)
            {
                return null;
            }

            return HungerAndHavoc.Storyteller.Suiyin.RHAH_PawnIndex.Find(loadId, Find.TickManager == null ? 0 : Find.TickManager.TicksGame);
        }

        static List<Verse.Pawn> Present(RHAH_ChoiceRecord record)
        {
            List<Verse.Pawn> result = new List<Verse.Pawn>();
            if (record.PawnLoadIds == null)
            {
                return result;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            for (int i = 0; i < record.PawnLoadIds.Count; i++)
            {
                Verse.Pawn pawn = HungerAndHavoc.Storyteller.Suiyin.RHAH_PawnIndex.Find(record.PawnLoadIds[i], tick);
                if (pawn != null && !pawn.Destroyed)
                {
                    result.Add(pawn);
                }
            }

            return result;
        }
    }
}
