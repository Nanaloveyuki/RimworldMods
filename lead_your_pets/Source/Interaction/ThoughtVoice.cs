using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LeadYourPet
{
    internal static class ThoughtVoice
    {
        private const string RatkinPackage = "Solaris.RatkinRaceMod";
        private const string KiiroPackage = "Ancot.KiiroRace";
        private static readonly Dictionary<ThoughtDef, string[]> Keys = new Dictionary<ThoughtDef, string[]>();
        private static readonly string[] Voices = { "Generic", "RatkinYoung", "RatkinAdult", "KiiroYoung", "KiiroAdult" };

        internal static bool TryText(ThoughtDef def, Pawn speaker, Pawn other, bool description, out string text)
        {
            text = null;
            LoadedLanguage language = LanguageDatabase.activeLanguage;
            if (speaker == null || def == null || language == null
                || def.defName == null || !def.defName.StartsWith("LeadYourPet_", StringComparison.Ordinal)) return false;

            int race = Race(speaker);
            int voice = race == 0 ? 0 : 1 + (race - 1) * 2 + (Young(speaker) ? 0 : 1);
            if (!Keys.TryGetValue(def, out string[] keys))
            {
                keys = new string[Voices.Length * 2];
                for (int i = 0; i < Voices.Length; i++)
                {
                    keys[i * 2] = def.defName + "_" + Voices[i] + "_Label";
                    keys[i * 2 + 1] = def.defName + "_" + Voices[i] + "_Description";
                }
                Keys.Add(def, keys);
            }

            // Voice packs are language-local; English can use neutral text without borrowing Chinese speech.
            int field = description ? 1 : 0;
            if (!language.TryGetTextFromKey(keys[voice * 2 + field], out TaggedString translated)
                && (voice == 0 || !language.TryGetTextFromKey(keys[field], out translated))) return false;
            text = translated.ToString();
            if (text.Contains("{Race}")) text = text.Replace("{Race}", OtherRace(other ?? Counterpart(def, speaker)));
            return true;
        }

        internal static string Format(Thought thought, string original, bool description)
        {
            if (thought?.def?.defName == null
                || !thought.def.defName.StartsWith("LeadYourPet_", StringComparison.Ordinal)) return original;
            Pawn other = (thought as Thought_Memory)?.otherPawn;
            if (TryText(thought.def, thought.pawn, other, description, out string text))
            {
                // Vanilla appends belief and nullification notes after a blank line.
                int notes = description && original != null ? original.IndexOf("\n\n", StringComparison.Ordinal) : -1;
                return notes < 0 ? (description ? text : text.CapitalizeFirst()) : text + original.Substring(notes);
            }
            return original != null && original.Contains("{Race}")
                ? original.Replace("{Race}", OtherRace(other ?? Counterpart(thought.def, thought.pawn)))
                : original;
        }

        internal static Pawn Counterpart(ThoughtDef def, Pawn pawn)
        {
            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component == null || pawn == null) return null;
            string name = def.defName;
            switch (name)
            {
                case "LeadYourPet_MouseEggFearMood":
                case "LeadYourPet_MouseEggWatchedMood":
                case "LeadYourPet_RatkinBabyLeashedMood":
                case "LeadYourPet_MouseDisasterBeggarBabyBonusMood":
                case "LeadYourPet_MouseDisasterChildExchangeBabyBonusMood":
                    return component.GetLinkForPet(pawn)?.Master ?? component.GetYoungRatkinState(pawn)?.CurrentMaster;
                case "LeadYourPet_MouseEggParentPetMood":
                    if (pawn.relations == null) return null;
                    foreach (Pawn child in pawn.relations.Children)
                    {
                        LeashLink childLink = component.GetLinkForPet(child);
                        if (childLink?.Kind == LeashLinkKind.YoungRatkinPet) return childLink.Master;
                    }
                    return null;
                default:
                    if (name != "LeadYourPet_LeadingPetMood" && name != "LeadYourPet_MouseEggOwnerMood"
                        && name != "LeadYourPet_MouseEggCaretakerMood" && name != "LeadYourPet_RatkinMotherLeashMood"
                        && name != "LeadYourPet_MouseDisasterBeggarMotherBonusMood"
                        && name != "LeadYourPet_MouseDisasterChildExchangeMotherBonusMood") return null;
                    List<LeashLink> links = component.GetLinksForMaster(pawn);
                    Pawn counterpart = null;
                    for (int i = 0; i < links.Count; i++)
                    {
                        LeashLink link = links[i];
                        if (link.Pet == null) continue;
                        if (name == "LeadYourPet_MouseEggOwnerMood" && link.Kind != LeashLinkKind.YoungRatkinPet) continue;
                        if (name == "LeadYourPet_MouseEggCaretakerMood"
                            && (link.Kind != LeashLinkKind.YoungRatkinPet || !LeadYourPetUtility.IsColonistYoungRatkin(link.Pet))) continue;
                        if ((name == "LeadYourPet_RatkinMotherLeashMood" || name.Contains("MotherBonusMood"))
                            && link.Kind != LeashLinkKind.RatkinMotherBaby) continue;
                        if (counterpart != null && (counterpart.def != link.Pet.def || Young(counterpart) != Young(link.Pet))) return null;
                        counterpart = link.Pet;
                    }
                    return counterpart;
            }
        }

        internal static int Race(Pawn pawn)
        {
            if (pawn?.def?.race?.Humanlike != true) return 0;
            ModContentPack mod = pawn.def.modContentPack;
            if (mod == null) return 0;
            string package = mod.PackageIdPlayerFacing;
            int race = string.Equals(package, RatkinPackage, StringComparison.OrdinalIgnoreCase) ? 1
                : string.Equals(package, KiiroPackage, StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            return race != 0 && LoadedModManager.RunningModsListForReading.Contains(mod) ? race : 0;
        }

        internal static bool Young(Pawn pawn) => pawn?.ageTracker != null && pawn.ageTracker.AgeBiologicalYears < 14;

        internal static string OtherRace(Pawn other)
        {
            int race = Race(other);
            if (other != null && race == 0) return other.def?.label ?? string.Empty;
            string key = "LeadYourPet_ThoughtRace_" + (race == 0 ? "Unknown"
                : race == 1 ? (Young(other) ? "RatkinYoung" : "RatkinAdult")
                : (Young(other) ? "KiiroYoung" : "KiiroAdult"));
            return LanguageDatabase.activeLanguage != null
                && LanguageDatabase.activeLanguage.TryGetTextFromKey(key, out TaggedString translated)
                ? translated.ToString() : other?.def?.label ?? string.Empty;
        }
    }

    [HarmonyPatch(typeof(Thought), nameof(Thought.LabelCap), MethodType.Getter)]
    internal static class Patch_Thought_LeashVoiceLabel
    {
        private static void Postfix(Thought __instance, ref string __result)
        {
            if (!(__instance is Thought_Memory)) __result = ThoughtVoice.Format(__instance, __result, false);
        }
    }

    // Memory labels are cached by vanilla without age or language in the cache key.
    [HarmonyPatch(typeof(Thought_Memory), nameof(Thought_Memory.LabelCap), MethodType.Getter)]
    internal static class Patch_ThoughtMemory_LeashVoiceLabel
    {
        private static void Postfix(Thought_Memory __instance, ref string __result) =>
            __result = ThoughtVoice.Format(__instance, __result, false);
    }

    [HarmonyPatch(typeof(Thought), nameof(Thought.Description), MethodType.Getter)]
    internal static class Patch_Thought_LeashVoiceDescription
    {
        private static void Postfix(Thought __instance, ref string __result) =>
            __result = ThoughtVoice.Format(__instance, __result, true);
    }
}
