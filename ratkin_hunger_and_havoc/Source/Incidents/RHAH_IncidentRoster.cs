using System.Collections.Generic;
using Verse;
using HungerAndHavoc.Api;
using RimWorld;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_IncidentRoster
    {
        internal static RHAH_PawnRole RoleAt(string displayId, RHAH_PawnRole fallback, int index)
        {
            switch (displayId)
            {
                case "I-002":
                case "I-037":
                    return RHAH_PawnRole.BeggarChild;
                case "I-003":
                    return index == 0 ? RHAH_PawnRole.Mother : RHAH_PawnRole.RatkinYoung;
                case "I-004":
                    return index == 0 ? RHAH_PawnRole.BeggarMother : RHAH_PawnRole.BeggarChild;
                case "I-007":
                case "I-043":
                    return RHAH_PawnRole.ThiefChild;
                case "I-009":
                case "I-009-01":
                case "I-041":
                    return RHAH_PawnRole.WildChild;
                case "I-013":
                    return index == 0 ? RHAH_PawnRole.Refugee : RHAH_PawnRole.RatkinYoung;
                case "I-032":
                case "I-033":
                case "I-046":
                case "I-047":
                    return RHAH_PawnRole.RatkinYoung;
                case "I-029":
                case "I-044":
                    return RHAH_PawnRole.BeggarMother;
                case "I-012":
                case "I-038":
                    return index == 0 ? RHAH_PawnRole.Trader : RHAH_PawnRole.RatkinYoung;
                default:
                    return fallback;
            }
        }

        internal static Gender? GenderAt(string displayId, int index)
        {
            if (displayId == "I-029" || displayId == "I-044")
            {
                return Gender.Female;
            }

            // 来信与后续都把首位成年来客写成鼠妈,生成时必须保证确实是女性
            if ((displayId == "I-003" || displayId == "I-004" || displayId == "I-013") && index == 0)
            {
                return Gender.Female;
            }

            return null;
        }

        // 母亲必须在孩子出生前就已成年,避免成年家长带着学龄孩子
        // I-013 的家长角色是 Refugee 而非 Mother,所以这里只要求首位不是幼年角色
        internal static void RaiseMotherAge(string displayId, IList<RHAH_PawnRole> roles, IList<float?> ages)
        {
            if (!LinksFamily(displayId) || roles == null || ages == null ||
                roles.Count == 0 || ages.Count == 0 || Pawn.RHAH_VisitorRules.IsYoungRole(roles[0]) ||
                !ages[0].HasValue)
            {
                return;
            }

            float oldest = 0f;
            for (int i = 1; i < roles.Count && i < ages.Count; i++)
            {
                if (!Pawn.RHAH_VisitorRules.IsYoungRole(roles[i]) || !ages[i].HasValue) continue;
                // 生成器会把无人抱的婴儿抬到儿童年龄,母亲下限按实际到场年龄计算
                float childAge = Pawn.RHAH_VisitorRules.LiftBabyToChildAge(ages[i], BabiesCarriable(), ImmobileBabiesAllowed())
                    ?? ages[i].Value;
                if (childAge > oldest) oldest = childAge;
            }

            if (oldest <= 0f) return;
            // 只抬不压:配置的母亲下限已经由 GenerationAge 合入 ages[0]
            float floor = oldest + Pawn.RHAH_VisitorRules.AdultStageAge;
            if (ages[0].Value < floor) ages[0] = floor;
        }

        static bool BabiesCarriable()
        {
            return Pawn.Compat.RHAH_ToddlerPresence.BabiesCarriable;
        }

        static bool ImmobileBabiesAllowed()
        {
            return Core.RHAH_Mod.Settings != null && Core.RHAH_Mod.Settings.allowImmobileBabies;
        }

        // 首位成年来客是原孩子母亲的来源,生成后要建立真实亲子关系
        internal static bool LinksFamily(string displayId)
        {
            return displayId == "I-003" || displayId == "I-004" || displayId == "I-013";
        }

        internal static bool StartsLabor(string displayId)
        {
            return displayId == "I-029" || displayId == "I-044";
        }

        internal static bool Shatters(string displayId, int index)
        {
            return displayId == "I-003" && index == 0;
        }
    }
}
