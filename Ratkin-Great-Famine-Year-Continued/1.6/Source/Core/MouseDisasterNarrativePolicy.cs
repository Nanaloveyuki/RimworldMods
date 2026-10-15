using System;
using System.Collections.Generic;
using System.Linq;

namespace MouseDisaster
{
    public static class MouseDisasterNarrativePolicy
    {
        public const int CheckTicks = 1500;
        public const int CareTicks = 300000;
        public const int MissingGraceTicks = 60000;
        public const int ObservationTicks = 1800000;
        public static readonly string[] SettingIds = {
            "N001", "N002", "N003", "N004", "N005", "N006", "N007", "N008", "N009", "N010",
            "E01", "E02", "E03", "E04", "E05", "R01", "N004Return", "N007Return", "Echo",
            "S01", "S02", "S03", "S04", "S05", "S06", "S07", "S08", "S09", "S10", "S11", "S12", "S13", "S14"
        };

        private static readonly Dictionary<string, string[]> AttitudeSources = new Dictionary<string, string[]>
        {
            { "N004", new[] { "MouseDisaster_ShatteredMother" } },
            { "N005", new[] { "MouseDisaster_ChildExchange" } },
            { "N007", new[] {
                "MouseDisaster_PlagueWanderers", "MouseDisaster_PlagueTraderCaravan",
                "MouseDisaster_PlaguePassersby", "MouseDisaster_PlagueRefugees", "MouseDisaster_PlagueOrphan",
                "MouseDisaster_PlagueBeggarGroup", "MouseDisaster_PlagueThiefGroup",
                "MouseDisaster_PlagueLaboringRefugees", "MouseDisaster_PlagueStrongSiege"
            } },
            { "N008", new[] { "N008" } }
        };

        public static IReadOnlyList<string> GetAttitudeSources(string id) =>
            id != null && AttitudeSources.TryGetValue(id, out var sources) ? sources : Array.Empty<string>();

        public static bool Enabled(string id, IEnumerable<string> disabled)
        {
            return SettingIds.Contains(id) && !(disabled?.Contains(id) ?? false);
        }

        public static float ScaledChance(float chance, float percent)
        {
            return Math.Max(0f, Math.Min(1f, chance * Math.Max(0f, Math.Min(100f, percent)) / 100f));
        }

        public static int Reward(int basis, int trust, bool narratorActive)
        {
            return basis + (narratorActive ? basis * Math.Max(0, Math.Min(100, trust)) / 400 : 0);
        }

        public static float EchoChance(int trust)
        {
            return trust <= -75 ? 0f : Math.Min(0.75f, Math.Max(0.05f, (trust + 100) / 250f));
        }

        public static float ThreatFrequencyFactor(int trust)
        {
            return 1f - Math.Max(-100, Math.Min(100, trust)) / 400f;
        }

        public static bool IsAidComplete(bool delivered, bool driven, int count, int left, int settled)
        {
            return IsAidComplete(delivered, driven, count, left, settled, 0);
        }

        public static bool IsAidComplete(bool delivered, bool driven, int count, int left, int settled,
            int identityChanged)
        {
            return IsAidComplete(delivered, driven, count, left, settled, identityChanged,
                Math.Min(Math.Max(0, count - left - settled), Math.Max(0, identityChanged)));
        }

        public static bool IsAidComplete(bool delivered, bool driven, int count, int left, int settled,
            int identityChanged, int detainedIdentityChanged)
        {
            int normalizedIdentityChanged = Math.Max(0, Math.Min(count, identityChanged));
            int exempt = Math.Max(0, Math.Min(normalizedIdentityChanged, detainedIdentityChanged));
            return count > 0 && !driven &&
                !IsIdentityChangeFailure(count, normalizedIdentityChanged) &&
                left + settled + exempt == count &&
                (delivered || settled + exempt == count);
        }

        public static bool IsIdentityChangeFailure(int totalPawnCount, int identityChangedCount)
        {
            return totalPawnCount > 0 && identityChangedCount >= totalPawnCount;
        }

        public static string Ending(int trust, bool narrator, int aid, int broadcasts, int driven,
            int adults, int aidGoal, int broadcastGoal, int driveLimit, int adultGoal, bool mature, Func<string, bool> enabled = null)
        {
            bool scale = aid >= aidGoal && broadcasts >= broadcastGoal && driven <= driveLimit;
            if (scale && adults >= adultGoal && (!narrator || trust >= 75) && (enabled?.Invoke("E02") ?? true)) return "E02";
            if (scale && (!narrator || trust >= 50) && (enabled?.Invoke("E01") ?? true)) return "E01";
            if (narrator && trust <= -75 && mature) return (enabled?.Invoke("E05") ?? true) ? "E05" : null;
            if (!mature) return null;
            string ending = narrator && trust < 0 ? "E04" : "E03";
            return (enabled?.Invoke(ending) ?? true) ? ending : null;
        }

        public static string Scene(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (defName.Contains("AirdropMistake")) return "S01";
            if (defName.Contains("MisguidedKinship")) return "S02";
            if (defName.Contains("LaboringRefugees")) return "S03";
            if (defName == "MouseDisaster_Passersby") return "S04";
            if (defName.Contains("Thief")) return "S05";
            if (defName.Contains("LargeRefugee")) return "S06";
            if (defName.Contains("_Intel_")) return "S07";
            if (defName.Contains("TraderCaravan")) return "S08";
            if (defName.Contains("StrongSiege") || defName.Contains("BeggarSiege")) return "S09";
            if (defName.Contains("GreatFamine")) return "S10";
            if (defName.Contains("PlagueRevenge")) return "S11";
            if (defName.Contains("CaravanMuggers")) return "S12";
            if (defName.Contains("WildRatkin") || defName.Contains("PlagueWanderers") || defName.Contains("PlagueOrphan")) return "S13";
            if (defName.Contains("_Aid_") || defName.Contains("Beggar") || defName.Contains("Refugees") || defName.Contains("PlaguePassersby")) return "S14";
            return null;
        }
    }
}
