using System;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_Shamblers
    {
        internal const string ChildId = "I-059";
        internal const string GroupId = "I-060";
        internal const string HordeId = "I-061";

        internal static bool IsEvent(string displayId)
        {
            return displayId == ChildId || displayId == "I-059-01" || displayId == GroupId ||
                displayId == "I-060-01" || displayId == HordeId;
        }

        internal static bool Available(string displayId)
        {
            return !IsEvent(displayId) || ModsConfig.AnomalyActive;
        }

        internal static bool IsChild(string displayId) => displayId == ChildId || displayId == "I-059-01";
        internal static bool Expires(string displayId) => displayId == "I-059-01" || displayId == "I-060-01";

        internal static int Count(string displayId, float points, int cap)
        {
            if (IsChild(displayId)) return 1;
            float budget = RHAH_IncidentTuning.ClampPoints(points);
            int count = (int)(budget / 40f * (displayId == HordeId ? 1f : 0.35f));
            return displayId == HordeId ? Math.Max(10, Math.Min(200, count))
                : Math.Max(2, Math.Min(Math.Max(2, cap), count));
        }

        internal static void ConfigureLifetime(HediffComp_DisappearsAndKills comp, string displayId)
        {
            comp.disabled = !Expires(displayId);
            if (!comp.disabled)
                comp.ticksToDisappear = IsChild(displayId)
                    ? Rand.RangeInclusive(25000, 45000)
                    : Rand.RangeInclusive(150000, 210000);
        }

        internal static LordJob CreateLordJob(string displayId, IntVec3 entry, IntVec3 destination)
        {
            return displayId == HordeId ? (LordJob)new LordJob_ShamblerAssault()
                : new LordJob_ShamblerSwarm(entry, destination);
        }

        internal static bool TrySpawn(Map map, string displayId, int count, out List<Verse.Pawn> pawns)
        {
            pawns = new List<Verse.Pawn>();
            if (!ModsConfig.AnomalyActive || map == null || Faction.OfEntities == null ||
                RHAH_DefOf.RHAH_PawnKind_Ratkin == null || MutantDefOf.Shambler == null) return false;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Hostile, false, null))
                return false;
            IntVec3 destination = map.Center;
            if (displayId != HordeId && !RCellFinder.TryFindTravelDestFrom(entry, map, out destination)) return false;

            for (int i = 0; i < count; i++)
            {
                RHAH_PawnRole role = IsChild(displayId) ? RHAH_PawnRole.RatkinYoung : RHAH_PawnRole.Refugee;
                float? age = RHAH_IncidentFacts.GenerationAge(role, displayId);
                age = IsChild(displayId) ? Math.Max(4f, Math.Min(11.99f, age ?? 6f)) : Math.Max(12f, age ?? 25f);
                Verse.Pawn pawn = RHAH_PawnFactory.Generate(new RHAH_PawnRequest
                {
                    SourceIncidentDisplayId = displayId,
                    Role = role,
                    PawnKind = RHAH_DefOf.RHAH_PawnKind_Ratkin,
                    BiologicalAge = age,
                    Map = map
                });
                if (pawn == null)
                {
                    Discard(pawns);
                    return false;
                }
                pawns.Add(pawn);
                MutantUtility.SetFreshPawnAsMutant(pawn, MutantDefOf.Shambler);
                HediffComp_DisappearsAndKills lifespan = pawn.mutant.Hediff?.TryGetComp<HediffComp_DisappearsAndKills>();
                if (!pawn.IsShambler || lifespan == null)
                {
                    Log.Error("[RHAH] Ratkin could not be converted to a vanilla shambler.");
                    Discard(pawns);
                    return false;
                }
                ConfigureLifetime(lifespan, displayId);
            }

            Rot4 rotation = Rot4.FromAngleFlat((map.Center - entry).AngleFlat);
            for (int i = 0; i < pawns.Count; i++)
                GenSpawn.Spawn(pawns[i], CellFinder.RandomClosewalkCellNear(entry, map, 10), map, rotation);
            LordMaker.MakeNewLord(Faction.OfEntities, CreateLordJob(displayId, entry, destination), map, pawns);
            return true;
        }

        static void Discard(List<Verse.Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++) pawns[i].Destroy(DestroyMode.Vanish);
            pawns.Clear();
        }

        internal static bool MobileRatkinShambler(Verse.Pawn pawn, Map map)
        {
            return pawn != null && pawn.Spawned && pawn.Map == map && !pawn.Dead && !pawn.Destroyed &&
                pawn.def == RHAH_DefOf.RHAH_PawnKind_Ratkin?.race && pawn.IsShambler &&
                (pawn.Downed ? pawn.health.CanCrawl : pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving));
        }

        internal static bool HasSurvivor(Map map)
        {
            IReadOnlyList<Verse.Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (MobileRatkinShambler(pawns[i], map)) return true;
            return false;
        }

        internal static bool Reinforce(Map map, int count)
        {
            if (!HasSurvivor(map) || !TrySpawn(map, HordeId, count, out List<Verse.Pawn> pawns)) return false;
            Find.LetterStack.ReceiveLetter("RHAH_Shambler_Reinforcements_Label".Translate(),
                "RHAH_Shambler_Reinforcements_Text".Translate(), LetterDefOf.ThreatBig, new LookTargets(pawns));
            return true;
        }
    }
}
