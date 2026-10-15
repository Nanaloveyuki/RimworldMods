using System.Collections.Generic;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_VisitorBatch
    {
        internal const int AnestheticDurationTicks = 12 * GenDate.TicksPerHour;

        internal static bool HasEligible(List<Verse.Pawn> pawns, int mapId, bool enslave)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                if (CanConvert(pawns[i], mapId, enslave))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool CanConvert(Verse.Pawn pawn, int mapId, bool enslave)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned ||
                pawn.guest == null || pawn.Map == null || pawn.Map.uniqueID != mapId ||
                !RHAH_Api.IsVisitor(pawn) || pawn.Faction == Faction.OfPlayer ||
                pawn.IsPrisoner || pawn.IsSlave || pawn.InMentalState ||
                !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Imprison))
            {
                return false;
            }

            return !enslave || ModsConfig.IdeologyActive;
        }

        internal static int Enslave(List<Verse.Pawn> pawns, int mapId)
        {
            int converted = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                if (!CanConvert(pawn, mapId, true) ||
                    !RHAH_Api.ReleaseToColony(pawn, RHAH_ReleaseReason.Enslaved))
                {
                    continue;
                }

                if (pawn.Faction != null && pawn.Faction.Hidden)
                {
                    pawn.SetFactionDirect(null);
                }

                bool everEnslaved = pawn.guest.EverEnslaved;
                pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                if (!pawn.IsSlaveOfColony)
                {
                    continue;
                }

                // 反抗意志清零 与囚犯抵抗值无关
                pawn.guest.will = 0f;
                pawn.apparel?.UnlockAll();
                // 选择信没有实际执行者 不伪造殖民者的个人戒律记录
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(
                    HistoryEventDefOf.EnslavedPrisoner, pawn.Named(HistoryEventArgsNames.Victim)));
                if (!everEnslaved)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(
                        HistoryEventDefOf.EnslavedPrisonerNotPreviouslyEnslaved, pawn.Named(HistoryEventArgsNames.Victim)));
                }

                converted++;
            }

            return converted;
        }

        internal static int Capture(List<Verse.Pawn> pawns, int mapId)
        {
            int converted = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                if (!CanConvert(pawn, mapId, false))
                {
                    continue;
                }

                pawn.guest.CapturedBy(Faction.OfPlayer);
                if (!pawn.IsPrisonerOfColony)
                {
                    continue;
                }

                ApplyAnesthetic(pawn);
                converted++;
            }

            return converted;
        }

        static void ApplyAnesthetic(Verse.Pawn pawn)
        {
            Hediff anesthetic = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic);
            if (anesthetic == null)
            {
                anesthetic = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn);
                pawn.health.AddHediff(anesthetic);
            }

            anesthetic.Severity = 1f;
            anesthetic.TryGetComp<HediffComp_Disappears>().SetDuration(AnestheticDurationTicks);
            // 半天只衰减到 0.9 保持原版昏迷阶段且存读保留衰减值
            anesthetic.TryGetComp<HediffComp_SeverityPerDay>().severityPerDay = -0.2f;
        }
    }
}
