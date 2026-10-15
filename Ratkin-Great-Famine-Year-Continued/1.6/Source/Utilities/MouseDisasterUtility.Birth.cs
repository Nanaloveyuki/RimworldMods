using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static void TryGainBloodlinePregnancyThought(Pawn pawn)
        {
            TryGainNonStackingMemory(pawn, MouseDisasterDefOf.MouseDisaster_BloodlineContinuation);
        }

        public static void TryGainBloodlineBirthThought(Pawn pawn)
        {
            TryGainNonStackingMemory(pawn, MouseDisasterDefOf.MouseDisaster_BloodlineRenewed);
        }

        public static void TryInheritBirthStatus(Pawn newborn, Pawn mother)
        {
            if (MouseDisasterMod.Settings?.enableExperimentalIdentityInheritance != true ||
                newborn == null || newborn.Dead || newborn.guest == null || mother == null ||
                !IsRatkin(newborn) || !IsRatkin(mother) || !HasAnyMouseDisasterGene(mother) ||
                !IsColonyBirthMother(mother)) return;
            GuestStatus status = mother.IsPrisonerOfColony ? GuestStatus.Prisoner :
                mother.IsSlaveOfColony ? GuestStatus.Slave : GuestStatus.Guest;
            if (newborn.Faction != mother.Faction) newborn.SetFaction(mother.Faction);
            newborn.guest.SetGuestStatus(status == GuestStatus.Guest ? null : Faction.OfPlayer, status);
        }

        public static void TryNormalizeColonyBornRatkinBabyBackstory(Pawn newborn, Pawn mother)
        {
            if (newborn?.story == null || !IsRatkin(newborn) || !IsColonyBirthMother(mother))
            {
                return;
            }

            if (ShouldUseMouseDisasterBirthIdentity(newborn, mother))
            {
                newborn.story.Childhood = MouseDisasterDefOf.MouseDisaster_Newborn;
                newborn.story.Adulthood = null;
                return;
            }

            BackstoryDef vanillaBabyBackstory = ResolveVanillaNewbornBackstory();
            if (vanillaBabyBackstory != null)
            {
                newborn.story.Childhood = vanillaBabyBackstory;
            }

            newborn.story.Adulthood = null;
        }

        public static void TryRemoveMouseDisasterFatherRelationAfterBirth(Pawn newborn, Pawn mother)
        {
            if (newborn?.relations == null || mother == null || !IsRatkin(newborn) || !IsRatkin(mother) || !HasAnyMouseDisasterGene(mother))
            {
                return;
            }

            List<Pawn> fathersToRemove = newborn.relations.DirectRelations
                .Where(relation => relation != null &&
                                   relation.def == PawnRelationDefOf.Parent &&
                                   relation.otherPawn != null &&
                                   relation.otherPawn != mother &&
                                   relation.otherPawn.gender == Gender.Male)
                .Select(relation => relation.otherPawn)
                .Distinct()
                .ToList();

            for (int i = 0; i < fathersToRemove.Count; i++)
            {
                newborn.relations.TryRemoveDirectRelation(PawnRelationDefOf.Parent, fathersToRemove[i]);
            }
        }

        private static void TryGainNonStackingMemory(Pawn pawn, ThoughtDef thoughtDef)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null || thoughtDef == null || !IsRatkin(pawn))
            {
                return;
            }

            if (thoughtDef == MouseDisasterDefOf.MouseDisaster_BloodlineContinuation || thoughtDef == MouseDisasterDefOf.MouseDisaster_BloodlineRenewed)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(MouseDisasterDefOf.MouseDisaster_BloodlineContinuation);
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(MouseDisasterDefOf.MouseDisaster_BloodlineRenewed);
            }

            pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thoughtDef);
            pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
        }

        private static bool IsColonyBirthMother(Pawn mother)
        {
            return mother != null &&
                   (mother.Faction == Faction.OfPlayer || mother.IsPrisonerOfColony || mother.IsSlaveOfColony);
        }

        private static bool ShouldUseMouseDisasterBirthIdentity(Pawn newborn, Pawn mother)
        {
            return newborn != null &&
                   mother != null &&
                   IsRatkin(mother) &&
                   (IsMouseDisasterPawn(mother) ||
                    IsMouseDisasterIncidentVisitor(mother) ||
                    HasAnyMouseDisasterGene(mother) ||
                    IsMouseDisasterPawn(newborn) ||
                    IsMouseDisasterIncidentVisitor(newborn) ||
                    HasAnyMouseDisasterGene(newborn));
        }

        private static BackstoryDef ResolveVanillaNewbornBackstory()
        {
            if (vanillaNewbornBackstoryPoolResolved)
            {
                return vanillaNewbornBackstoryPool.NullOrEmpty() ? null : vanillaNewbornBackstoryPool.RandomElement();
            }

            vanillaNewbornBackstoryPoolResolved = true;
            vanillaNewbornBackstoryPool = DefDatabase<BackstoryDef>.AllDefsListForReading
                .Where(def => def != null &&
                              def.slot == BackstorySlot.Childhood &&
                              def.spawnCategories != null &&
                              def.spawnCategories.Contains(VanillaNewbornSpawnCategory) &&
                              def != MouseDisasterDefOf.MouseDisaster_Newborn)
                .ToList();

            return vanillaNewbornBackstoryPool.NullOrEmpty() ? null : vanillaNewbornBackstoryPool.RandomElement();
        }

        public static void ProcessChaosPregnancies(Map map)
        {
            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (!MouseDisasterRuntime.AllowsNewContent || !ModsConfig.BiotechActive || map == null || !Find.Storyteller.difficulty.ChildrenAllowed || (settings != null && !settings.enableChaosRoomPregnancy))
            {
                return;
            }

            int checkIntervalTicks = Mathf.Max(60, settings?.chaosPregnancyCheckIntervalTicks ?? DefaultChaosPregnancyCheckInterval);
            float chancePerCheck = Mathf.Clamp01((settings?.chaosPregnancyChancePercent ?? (DefaultChaosPregnancyChancePerCheck * 100f)) / 100f);
            if (chancePerCheck <= 0f)
            {
                return;
            }

            List<Pawn> ratkinPawns = GetCachedRatkinPawns(map);
            ReusablePawnList.Clear();
            for (int i = 0; i < ratkinPawns.Count; i++)
            {
                Pawn pawn = ratkinPawns[i];
                if (pawn == null ||
                    !pawn.Spawned ||
                    pawn.gender != Gender.Female ||
                    !pawn.IsHashIntervalTick(checkIntervalTicks) ||
                    !CanParticipateInMouseDisasterPregnancy(pawn) ||
                    !HasActiveGene(pawn, MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility) ||
                    PregnancyUtility.GetPregnancyHediff(pawn) != null ||
                    !TryGetChaosPregnancyIdentity(pawn, out _))
                {
                    continue;
                }

                ReusablePawnList.Add(pawn);
            }
            if (ReusablePawnList.Count == 0)
            {
                return;
            }

            Dictionary<Room, Dictionary<ChaosPregnancyIdentity, List<Pawn>>> malesByRoom = new Dictionary<Room, Dictionary<ChaosPregnancyIdentity, List<Pawn>>>();
            for (int i = 0; i < ratkinPawns.Count; i++)
            {
                Pawn male = ratkinPawns[i];
                if (male == null || !male.Spawned || male.gender != Gender.Male || !CanParticipateInMouseDisasterPregnancy(male) || !TryGetChaosPregnancyIdentity(male, out ChaosPregnancyIdentity maleIdentity))
                {
                    continue;
                }

                Room maleRoom = male.GetRoom();
                if (maleRoom == null || maleRoom.PsychologicallyOutdoors)
                {
                    continue;
                }

                if (!malesByRoom.TryGetValue(maleRoom, out Dictionary<ChaosPregnancyIdentity, List<Pawn>> byIdentity))
                {
                    byIdentity = new Dictionary<ChaosPregnancyIdentity, List<Pawn>>();
                    malesByRoom[maleRoom] = byIdentity;
                }

                if (!byIdentity.TryGetValue(maleIdentity, out List<Pawn> malesInRoom))
                {
                    malesInRoom = new List<Pawn>();
                    byIdentity[maleIdentity] = malesInRoom;
                }

                malesInRoom.Add(male);
            }

            for (int i = 0; i < ReusablePawnList.Count; i++)
            {
                Pawn female = ReusablePawnList[i];
                if (!TryGetChaosPregnancyIdentity(female, out ChaosPregnancyIdentity femaleIdentity))
                {
                    continue;
                }

                Room room = female.GetRoom();
                if (room == null || room.PsychologicallyOutdoors)
                {
                    continue;
                }

                if (!malesByRoom.TryGetValue(room, out Dictionary<ChaosPregnancyIdentity, List<Pawn>> malesByIdentity) ||
                    !malesByIdentity.TryGetValue(femaleIdentity, out List<Pawn> malesInRoom) ||
                    malesInRoom.Count == 0)
                {
                    continue;
                }

                Pawn father = malesInRoom.RandomElement();
                float effectiveChance = chancePerCheck;
                if (!Rand.Chance(effectiveChance))
                {
                    continue;
                }

                bool inheritedSuccess;
                GeneSet inheritedGeneSet = PregnancyUtility.GetInheritedGeneSet(father, female, out inheritedSuccess);
                if (!inheritedSuccess)
                {
                    continue;
                }

                Hediff_Pregnant pregnancy = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, female);
                pregnancy.SetParents(female, father, inheritedGeneSet);
                female.health.AddHediff(pregnancy);
            }
        }
    }
}
