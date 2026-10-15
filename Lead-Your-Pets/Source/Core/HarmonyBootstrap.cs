using HarmonyLib;
using Verse;

namespace LeadYourPet
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            new Harmony(LeadYourPetUtility.HarmonyId).PatchAll();
        }
    }
}
