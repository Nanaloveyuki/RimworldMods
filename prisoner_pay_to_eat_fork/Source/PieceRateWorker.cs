using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Per-prisoner wage billing mode. Follow = use the global per-work-type setting.
    /// </summary>
    public enum PieceRateMode
    {
        Follow = 0,
        PerHour = 1,
        PerItem = 2
    }

    internal readonly record struct ResearchWorker(Pawn Pawn, WorkTypeDef WorkType, WorkGiverDef Giver);

    /// <summary>
    /// Piece-rate ("按量计费") wage settlement.
    ///
    /// When the global master switch is on, a (prisoner, work type) pair is billed by output instead
    /// of by the hour. Vanilla work types have a dedicated, exact unit:
    ///   - production tables (DoBill): one completed recipe batch  -> RecordsUtility.Notify_BillDone
    ///   - Mining: one mined deposit                               -> Mineable.DestroyMined
    ///   - Construction: one finished building/floor              -> Frame.CompleteConstruction
    ///   - Growing/PlantCutting: one sown/harvested/cut plant     -> Plant.PlantCollected / PlantsSown record
    ///   - Hauling: one carried stack placed on floor/shelf       -> Pawn_CarryTracker.TryDropCarriedThing
    ///   - Cleaning: one cleaned filth                            -> MessesCleaned record
    ///   - Research: one completed tech, split by contribution    -> ResearchManager.FinishProject
    ///
    /// Everything else — including work types added by other MODs — falls back to the generic
    /// "one completed work job = one unit" payment (Pawn_JobTracker.EndCurrentJob), so unknown
    /// work types still support piece rate. Work types with a dedicated unit are excluded from the
    /// generic fallback to avoid double payment.
    /// </summary>
    public static class PieceRateWorker
    {

        /// <summary>
        /// Master switch, then prisoner override, then individual giver mode or parent work-type flag.
        /// </summary>
        internal static bool IsPieceRateActive(Pawn pawn, AttributedWork work)
        {
            if (!WorkBilling.IsKnown(work)) return false;
            var settings = PrisonersPayToEat2Mod.Settings;
            if (!settings.enablePieceRate) return false;
            var d = PrisonersPayToEat2Manager.Current?.DataFor(pawn);
            if (d != null)
            {
                if (d.wageMode == PieceRateMode.PerHour) return false;
                if (d.wageMode == PieceRateMode.PerItem) return true;
            }
            return WorkBilling.PieceMode(work);
        }


        /// <summary>
        /// Sensible default per-unit wage when a work type is first switched to piece rate,
        /// tuned so an average worker earns roughly the hourly default (player can edit).
        /// Research is priced per tech (a tech represents hours of work), hence hourly * 5.
        /// </summary>
        public static float GetPrefilledPieceWage(WorkTypeDef wt, float hourlyWage)
        {
            float hourly = Mathf.Max(hourlyWage, 0f);
            if (wt == null) return Mathf.Max(hourly / 10f, 0.01f);
            switch (wt.defName)
            {
                case "Research":     return Mathf.Max(hourly * 5f, 0.01f);    // per completed tech
                case "Mining":       return Mathf.Max(hourly / 20f, 0.01f);   // deposits per hour
                case "Construction": return Mathf.Max(hourly / 8f, 0.01f);    // buildings per hour
                case "Growing":      return Mathf.Max(hourly / 30f, 0.01f);   // sow/harvest per hour
                case "PlantCutting": return Mathf.Max(hourly / 30f, 0.01f);   // cuts per hour
                case "Hauling":      return Mathf.Max(hourly / 60f, 0.01f);   // hauls per hour
                case "Cleaning":     return Mathf.Max(hourly / 100f, 0.01f);  // filths per hour
                default:             return Mathf.Max(hourly / 10f, 0.01f);   // production tables & generic per-job
            }
        }

        /// <summary>Credit a piece-rate payout for a completed unit. Guards: prisoner, some labour provider active, mode active.</summary>
        internal static void Credit(Pawn pawn, AttributedWork work, float units)
        {
            if (pawn == null || !WorkBilling.IsKnown(work) || units <= 0f) return;
            if (!pawn.IsPrisonerOfColony || pawn.Dead || pawn.InMentalState) return;
            if (!PrisonerWorkSystem.WorkIncomeEnabled) return;
            if (!IsPieceRateActive(pawn, work)) return;
            float wage = WorkBilling.PieceWage(work);
            if (wage <= 0f) return;
            var settings = PrisonersPayToEat2Mod.Settings;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;
            float amount = units * wage * settings.wageMultiplier * mgr.EffectiveWageMultiplier(pawn);
            if (amount <= 0f) return;

            int id = pawn.thingIDNumber;
            float accum = mgr.GetWorkWageAccum(id) + amount;
            if (accum >= 0.01f)
            {
                mgr.AddTickets(pawn, accum);
                Need_Motivation.NotifyWage(pawn, accum);
                if (settings.logVerbose)
                    Log.Message($"[PPTE2] {pawn.LabelShortCap} earned {accum:0.##} tickets (piece rate: {work.Giver?.defName ?? work.WorkType?.defName}). Balance={mgr.Balance(pawn):0.##}");
                accum = 0f;
            }
            mgr.SetWorkWageAccum(id, accum);
        }


        // ===== Research: preserve the existing sampled research-speed * ticks contribution shares =====

        /// <summary>Accumulate a prisoner's research contribution to the current project (called from the wage ticker).</summary>
        public static void RecordResearchContribution(Pawn pawn, int delta)
        {
            if (pawn == null) return;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;
            var proj = Find.ResearchManager?.GetProject();
            if (proj == null) return;
            if (!mgr.researchContrib.TryGetValue(proj, out var dict))
            {
                dict = new Dictionary<ResearchWorker, float>();
                mgr.researchContrib[proj] = dict;
            }
            float add = pawn.GetStatValue(StatDefOf.ResearchSpeed) * delta;
            var work = WorkAttribution.Resolve(pawn, pawn.jobs?.curJob);
            var key = new ResearchWorker(pawn, work.WorkType, work.Giver);
            dict.TryGetValue(key, out float cur);
            dict[key] = cur + add;
        }

        /// <summary>Called when a research project completes: split the per-tech wage among contributors proportionally.</summary>
        public static void DistributeResearch(ResearchProjectDef project)
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;
            if (!mgr.researchContrib.TryGetValue(project, out var contribs)) return;
            mgr.researchContrib.Remove(project);
            if (contribs == null || contribs.Count == 0) return;
            float total = 0f;
            foreach (var kv in contribs) total += kv.Value;
            if (total <= 0f) return;
            var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Research");
            foreach (var kv in contribs)
            {
                var pawn = kv.Key.Pawn;
                if (pawn == null || pawn.Dead) continue;
                Credit(pawn, new AttributedWork(kv.Key.WorkType ?? wt, false, kv.Key.Giver), kv.Value / total);
            }
        }
    }
}
