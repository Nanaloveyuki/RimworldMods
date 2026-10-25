using System;
using HarmonyLib;
using Verse;

namespace LeadYourPet
{
    internal static class LegacyRatkinNames
    {
        // Scribe's Class attribute uses a full name (or an ignored-namespace short name),
        // not an assembly-qualified name. Keep aliases exact and local to this mod.
        internal static Type ResolveType(string name)
        {
            switch (name)
            {
                case "MouseEggState":
                case "LeadYourPet.MouseEggState":
                    return typeof(YoungRatkinState);
                case "MouseEggSpecialSource":
                case "LeadYourPet.MouseEggSpecialSource":
                    return typeof(YoungRatkinSpecialSource);
                case "MouseEggInteractionAnimation":
                case "LeadYourPet.MouseEggInteractionAnimation":
                    return typeof(YoungRatkinInteractionAnimation);
                case "MouseEggOwnershipStateMachine":
                case "LeadYourPet.MouseEggOwnershipStateMachine":
                    return typeof(YoungRatkinOwnershipStateMachine);
                case "MouseEggOwnershipSnapshot":
                case "LeadYourPet.MouseEggOwnershipSnapshot":
                    return typeof(YoungRatkinOwnershipSnapshot);
                case "LeashedMouseEggDutyState":
                case "LeadYourPet.LeashedMouseEggDutyState":
                    return typeof(LeashedYoungRatkinDutyState);
                case "JobDriver_MouseEggInteraction":
                case "LeadYourPet.JobDriver_MouseEggInteraction":
                    return typeof(JobDriver_YoungRatkinInteraction);
                case "JobDriver_CarryTravelMouseEgg":
                case "LeadYourPet.JobDriver_CarryTravelMouseEgg":
                    return typeof(JobDriver_CarryTravelYoungRatkin);
                case "Thought_Memory_MouseEggOwnerList":
                case "LeadYourPet.Thought_Memory_MouseEggOwnerList":
                    return typeof(Thought_Memory_YoungRatkinOwnerList);
                case "ThoughtWorker_MouseEggOwner":
                case "LeadYourPet.ThoughtWorker_MouseEggOwner":
                    return typeof(ThoughtWorker_YoungRatkinOwner);
                case "ThoughtWorker_MouseEggFear":
                case "LeadYourPet.ThoughtWorker_MouseEggFear":
                    return typeof(ThoughtWorker_YoungRatkinFear);
                case "ThoughtWorker_MouseEggParentPet":
                case "LeadYourPet.ThoughtWorker_MouseEggParentPet":
                    return typeof(ThoughtWorker_YoungRatkinParentPet);
                case "ThoughtWorker_MouseEggCaretaker":
                case "LeadYourPet.ThoughtWorker_MouseEggCaretaker":
                    return typeof(ThoughtWorker_YoungRatkinCaretaker);
                case "ThoughtWorker_MouseEggWatched":
                case "LeadYourPet.ThoughtWorker_MouseEggWatched":
                    return typeof(ThoughtWorker_YoungRatkinWatched);
                case "ThoughtWorker_MouseDisasterBeggarMotherBonus":
                case "LeadYourPet.ThoughtWorker_MouseDisasterBeggarMotherBonus":
                    return typeof(ThoughtWorker_RatkinDisasterBeggarMotherBonus);
                case "ThoughtWorker_MouseDisasterBeggarBabyBonus":
                case "LeadYourPet.ThoughtWorker_MouseDisasterBeggarBabyBonus":
                    return typeof(ThoughtWorker_RatkinDisasterBeggarBabyBonus);
                case "ThoughtWorker_MouseDisasterChildExchangeMotherBonus":
                case "LeadYourPet.ThoughtWorker_MouseDisasterChildExchangeMotherBonus":
                    return typeof(ThoughtWorker_RatkinDisasterChildExchangeMotherBonus);
                case "ThoughtWorker_MouseDisasterChildExchangeBabyBonus":
                case "LeadYourPet.ThoughtWorker_MouseDisasterChildExchangeBabyBonus":
                    return typeof(ThoughtWorker_RatkinDisasterChildExchangeBabyBonus);
                default:
                    return null;
            }
        }

        internal static object ResolveEnum(Type type, string name)
        {
            if (type == typeof(LeashLinkKind) && name == "MouseEggPet")
            {
                return LeashLinkKind.YoungRatkinPet;
            }

            if (type == typeof(YoungRatkinSpecialSource))
            {
                switch (name)
                {
                    case "MouseDisasterBeggarFamily":
                        return YoungRatkinSpecialSource.RatkinDisasterBeggarFamily;
                    case "MouseDisasterChildExchange":
                        return YoungRatkinSpecialSource.RatkinDisasterChildExchange;
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly), typeof(string), typeof(string))]
    internal static class Patch_GenTypes_LegacyRatkinNames
    {
        internal static void Prefix(ref string typeName, string namespaceIfAmbiguous)
        {
            if (!string.IsNullOrEmpty(namespaceIfAmbiguous)
                && namespaceIfAmbiguous != "LeadYourPet" && typeName?.Contains(".") != true)
            {
                return;
            }

            Type replacement = LegacyRatkinNames.ResolveType(typeName);
            if (replacement != null)
            {
                typeName = replacement.FullName;
            }
        }
    }

    [HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.BackCompatibleEnum), typeof(Type), typeof(string))]
    internal static class Patch_BackCompatibility_LegacyRatkinNames
    {
        internal static void Postfix(Type enumType, string enumName, ref object __result)
        {
            if (__result == null)
            {
                __result = LegacyRatkinNames.ResolveEnum(enumType, enumName);
            }
        }
    }
}
