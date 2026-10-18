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

        public static int TailBiteStartTextCooldown => TailBiteStartTextCooldownTicks;

        public static int TailBiteResultTextCooldown => TailBiteResultTextCooldownTicks;

        public static Filth FindScavengeableFilth(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.Position.IsValid)
            {
                return null;
            }

            Filth found = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Filth),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                40f,
                thing => thing is Filth filth && IsScavengeableFilth(filth) && pawn.CanReserve(thing)) as Filth;

            if (found != null)
            {
                return found;
            }

            if (pawn.needs?.food == null || pawn.needs.food.CurCategory < HungerCategory.Starving)
            {
                return null;
            }

            return TryCreateEmergencyScavengeFilth(pawn);
        }

        private static Filth TryCreateEmergencyScavengeFilth(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.Position.IsValid)
            {
                return null;
            }

            ThingDef fallbackFilthDef = MouseDisasterDefOf.Filth_MouseDisasterPoop ?? ThingDefOf.Filth_Dirt;
            if (fallbackFilthDef == null)
            {
                return null;
            }

            FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, fallbackFilthDef, 1, FilthSourceFlags.None);
            Filth filth = pawn.Position.GetThingList(pawn.Map).OfType<Filth>().FirstOrDefault(item => item.def == fallbackFilthDef && pawn.CanReserve(item));
            if (IsPrisonerScavengeDebugLogEnabled && filth != null)
            {
                LogPrisonerScavenge(pawn, "fallback: spawned emergency filth target=" + filth.def.defName);
            }

            return filth;
        }

        public static bool IsScavengeableFilth(Filth filth)
        {
            return filth != null && filth.Spawned && filth.MapHeld != null && ResolvePrisonerScavengeProfile(filth) != null;
        }

        public static PrisonerScavengeProfile ResolvePrisonerScavengeProfile(Filth filth)
        {
            if (filth == null)
            {
                return BuildPrisonerScavengeProfile(MouseDisasterDefOf.MouseDisaster_ScavengedDirtyFilth, PrisonerScavengeDirtyTextKeys);
            }

            string source = BuildFilthMatchSource(filth);
            if (ContainsAnyKeyword(source, PrisonerScavengeVomitKeywords))
            {
                return BuildPrisonerScavengeProfile(MouseDisasterDefOf.MouseDisaster_ScavengedVomit, PrisonerScavengeVomitTextKeys);
            }

            if (ContainsAnyKeyword(source, PrisonerScavengeAmnioticKeywords))
            {
                return BuildPrisonerScavengeProfile(MouseDisasterDefOf.MouseDisaster_ScavengedAmnioticFluid, PrisonerScavengeAmnioticTextKeys);
            }

            if (ContainsAnyKeyword(source, PrisonerScavengeBloodKeywords))
            {
                return BuildPrisonerScavengeProfile(MouseDisasterDefOf.MouseDisaster_ScavengedBlood, PrisonerScavengeBloodTextKeys);
            }

            if (ContainsAnyKeyword(source, PrisonerScavengeFloorDustKeywords))
            {
                return BuildPrisonerScavengeProfile(MouseDisasterDefOf.MouseDisaster_ScavengedFloorDust, PrisonerScavengeFloorDustTextKeys);
            }

            return BuildPrisonerScavengeProfile(MouseDisasterDefOf.MouseDisaster_ScavengedDirtyFilth, PrisonerScavengeDirtyTextKeys);
        }

        public static string RandomPrisonerScavengePoisonedText()
        {
            return RandomText(PrisonerScavengePoisonedTextKeys);
        }

        private static PrisonerScavengeProfile BuildPrisonerScavengeProfile(ThoughtDef thoughtDef, string[] textKeys)
        {
            ThoughtDef resolvedThought = thoughtDef ?? MouseDisasterDefOf.MouseDisaster_ScavengedDirtyFilth;
            string[] resolvedText = textKeys ?? PrisonerScavengeDirtyTextKeys;
            return new PrisonerScavengeProfile(resolvedThought, resolvedText, 0.1f);
        }

        private static string BuildFilthMatchSource(Filth filth)
        {
            if (filth?.def == null)
            {
                return string.Empty;
            }

            string defName = filth.def.defName ?? string.Empty;
            string label = filth.def.label ?? string.Empty;
            return (defName + " " + label).ToLowerInvariant();
        }

        private static bool ContainsAnyKeyword(string source, string[] keywords)
        {
            if (source.NullOrEmpty() || keywords == null || keywords.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < keywords.Length; i++)
            {
                if (source.Contains(keywords[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
