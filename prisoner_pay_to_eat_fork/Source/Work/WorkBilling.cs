using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    internal enum WorkUnit { Job, Bill, Mining, Construction, Plant, Sowing, Cleaning, Hauling, Research }

    internal static class WorkBilling
    {
        internal static bool HasIndividualRates(AttributedWork work) =>
            PrisonersPayToEat2Mod.Settings.enableIndividualWorkGiverBilling && work.Giver != null;

        internal static bool IsKnown(AttributedWork work) => work.WorkType != null || HasIndividualRates(work);

        internal static float HourlyWage(AttributedWork work)
        {
            var settings = PrisonersPayToEat2Mod.Settings;
            if (HasIndividualRates(work) && settings.workGiverWages.TryGetValue(work.Giver.defName, out float wage))
                return wage;
            return settings.GetWageForWorkType(work.WorkType?.defName);
        }

        internal static bool PieceMode(AttributedWork work)
        {
            var settings = PrisonersPayToEat2Mod.Settings;
            if (HasIndividualRates(work) && settings.workGiverPieceModes.TryGetValue(work.Giver.defName, out bool piece))
                return piece;
            return work.WorkType != null && settings.pieceRateWorkTypes.Contains(work.WorkType.defName);
        }

        internal static float PieceWage(AttributedWork work)
        {
            var settings = PrisonersPayToEat2Mod.Settings;
            if (HasIndividualRates(work) && settings.workGiverPieceWages.TryGetValue(work.Giver.defName, out float wage))
                return wage;
            if (work.WorkType != null && settings.workTypePieceWages.TryGetValue(work.WorkType.defName, out wage))
                return wage;
            return PieceRateWorker.GetPrefilledPieceWage(work.WorkType, HourlyWage(work));
        }

        internal static bool UsesUnit(AttributedWork work, WorkUnit unit)
        {
            string type = work.WorkType?.defName;
            switch (unit)
            {
                case WorkUnit.Mining: return type == "Mining";
                case WorkUnit.Construction: return type == "Construction";
                case WorkUnit.Plant: return type == "Growing" || type == "PlantCutting";
                case WorkUnit.Sowing: return type == "Growing";
                case WorkUnit.Cleaning: return type == "Cleaning";
                case WorkUnit.Hauling: return type == "Hauling";
                case WorkUnit.Research: return type == "Research";
                case WorkUnit.Bill:
                    return work.Giver?.giverClass != null
                        && typeof(WorkGiver_DoBill).IsAssignableFrom(work.Giver.giverClass);
                default: return !HasSpecificUnit(work);
            }
        }

        internal static bool HasSpecificUnit(AttributedWork work)
        {
            switch (work.WorkType?.defName)
            {
                case "Mining": case "Construction": case "Growing": case "PlantCutting":
                case "Cleaning": case "Hauling": case "Research": return true;
            }
            if (work.Giver != null) return UsesUnit(work, WorkUnit.Bill);
            return false;
        }
    }
}
