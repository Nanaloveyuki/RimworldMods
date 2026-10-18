using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{

    [HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), typeof(Thing), typeof(Thing))]
    public static class Patch_GenHostility_ThingThing
    {
        public static void Postfix(Thing a, Thing b, ref bool __result)
        {
            if (__result && LeadYourPetUtility.IsSuppressedAgainstThing(a, b))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), typeof(Thing), typeof(Faction))]
    public static class Patch_GenHostility_ThingFaction
    {
        public static void Postfix(Thing t, Faction fac, ref bool __result)
        {
            Pawn pawn = t as Pawn;
            if (__result && pawn != null && LeadYourPetUtility.IsSuppressedAgainstMasterFaction(pawn, fac))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(LordMaker), nameof(LordMaker.MakeNewLord))]
    public static class Patch_LordMaker_MakeNewLord
    {
        public static void Postfix(Lord __result)
        {
            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            component?.TryAssignTravelMouseEggs(__result);
            component?.AddPlayerLeashedPetsToFormingCaravan(__result);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap), typeof(bool), typeof(Rot4))]
    public static class Patch_Pawn_ExitMap
    {
        public static void Prefix(Pawn __instance, bool allowedToJoinOrCreateCaravan, Rot4 exitDir)
        {
            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component == null || __instance == null || !__instance.Spawned)
            {
                return;
            }

            if (allowedToJoinOrCreateCaravan
                && LeadYourPetUtility.IsPlayerCaravanMaster(__instance)
                && CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(__instance))
            {
                return;
            }

            component.HandleMasterExitMap(__instance, exitDir);
        }
    }

    [HarmonyPatch]
    public static class Patch_CaravanExitMapUtility_ExitMapAndCreateCaravan
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CaravanExitMapUtility),
                nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
                new[] { typeof(IEnumerable<Pawn>), typeof(Faction), typeof(PlanetTile), typeof(PlanetTile), typeof(PlanetTile), typeof(bool) });
        }

        public static void Prefix(ref IEnumerable<Pawn> pawns, Faction faction)
        {
            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component != null)
            {
                pawns = component.IncludePlayerLeashedPawnsInCaravan(pawns, faction);
            }
        }

        public static void Postfix(Caravan __result)
        {
            LeadYourPetUtility.Component?.EndLeashesForCaravan(__result);
        }
    }

    [HarmonyPatch]
    public static class Patch_CaravanExitMapUtility_ExitMapAndJoinOrCreateCaravan
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CaravanExitMapUtility),
                nameof(CaravanExitMapUtility.ExitMapAndJoinOrCreateCaravan),
                new[] { typeof(Pawn), typeof(Rot4) });
        }

        public static void Prefix(Pawn pawn, ref Caravan __state)
        {
            __state = LeadYourPetUtility.Component?.AddPlayerLeashedPetsToJoinableCaravan(pawn);
        }

        public static void Postfix(Caravan __state)
        {
            LeadYourPetUtility.Component?.EndLeashesForCaravan(__state);
        }
    }

    [HarmonyPatch(typeof(TraderCaravanUtility), nameof(TraderCaravanUtility.GetTraderCaravanRole))]
    public static class Patch_TraderCaravanUtility_GetRole
    {
        public static void Postfix(Pawn p, ref TraderCaravanRole __result)
        {
            if (LeadYourPetUtility.CanUseAsTraderChattel(p))
            {
                __result = TraderCaravanRole.Chattel;
            }
        }
    }

    [HarmonyPatch(typeof(TransferableUIUtility), nameof(TransferableUIUtility.TransferableIsCaptive))]
    public static class Patch_TransferableUIUtility_Captive
    {
        public static void Postfix(Transferable trad, ref bool __result)
        {
            Pawn pawn = trad?.AnyThing as Pawn;
            if (!__result && pawn != null && LeadYourPetUtility.CanUseAsTraderChattel(pawn))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreTraded))]
    public static class Patch_Pawn_PreTraded
    {
        public static void Prefix(Pawn __instance, TradeAction action, ref bool __state)
        {
            MouseEggState state = LeadYourPetUtility.Component?.GetMouseEggState(__instance);
            __state = MouseEggOwnershipStateMachine.ShouldHandleBoughtTravelMouseEgg(
                actionIsPlayerBuys: action == TradeAction.PlayerBuys,
                wasTravelStock: state != null && state.IsTravelStock,
                wasSellable: state != null && state.SellAsPrisoner);
        }

        public static void Postfix(Pawn __instance, TradeAction action, bool __state)
        {
            if (__state)
            {
                LeadYourPetUtility.Component?.HandleBoughtTravelMouseEgg(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.TraderWillTrade), MethodType.Getter)]
    public static class Patch_Tradeable_TraderWillTrade
    {
        public static void Postfix(Tradeable __instance, ref bool __result)
        {
            if (!LeadYourPetRules.ShouldEvaluateTradeableYoungRatkin(__result, __instance?.AnyThing is Pawn))
            {
                return;
            }

            Pawn pawn = __instance.AnyThing as Pawn;
            if (!LeadYourPetUtility.TryGetCachedTradeableYoungRatkin(pawn.thingIDNumber, out bool tradeable))
            {
                tradeable = LeadYourPetUtility.IsTradeableYoungRatkin(pawn);
                LeadYourPetUtility.CacheTradeableYoungRatkin(pawn.thingIDNumber, tradeable);
            }

            __result = LeadYourPetRules.TraderWillTradeYoungRatkin(__result, tradeable);
        }
    }

    [HarmonyPatch(typeof(TradeSession), nameof(TradeSession.SetupWith))]
    public static class Patch_TradeSession_SetupWith
    {
        public static void Prefix()
        {
            LeadYourPetUtility.ClearTradeableYoungRatkinCache();
        }
    }

    [HarmonyPatch(typeof(TradeSession), nameof(TradeSession.Close))]
    public static class Patch_TradeSession_Close
    {
        public static void Postfix()
        {
            LeadYourPetUtility.ClearTradeableYoungRatkinCache();
        }
    }


    [HarmonyPatch(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), typeof(Thing))]
    public static class Patch_PawnCarryTracker_TryStartCarry
    {
        internal static bool TryAllowPrisonerTransfer(Pawn_CarryTracker tracker, Thing item, ref bool state)
        {
            if (item is Pawn pawn && LeadYourPetUtility.ShouldAllowProtectedMouseEggPrisonerTransfer(tracker?.pawn, pawn))
            {
                state = true;
                return true;
            }

            return false;
        }

        internal static void CompletePrisonerTransfer(Thing item, bool result, bool state)
        {
            if (state && result && item is Pawn pawn)
            {
                LeadYourPetUtility.Component?.EndLeashForPet(pawn, false);
            }
        }

        public static bool Prefix(Pawn_CarryTracker __instance, Thing item, ref bool __result, ref bool __state)
        {
            if (TryAllowPrisonerTransfer(__instance, item, ref __state))
            {
                return true;
            }

            if (item is Pawn pawnToBlock && LeadYourPetUtility.ShouldBlockCarryOfProtectedMouseEgg(__instance?.pawn, pawnToBlock))
            {
                __result = false;
                return false;
            }

            return true;
        }

        public static void Postfix(Thing item, bool __result, bool __state)
        {
            CompletePrisonerTransfer(item, __result, __state);
        }
    }

    [HarmonyPatch(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), typeof(Thing), typeof(int), typeof(bool))]
    public static class Patch_PawnCarryTracker_TryStartCarryStack
    {
        public static bool Prefix(Pawn_CarryTracker __instance, Thing item, ref int __result, ref bool __state)
        {
            if (Patch_PawnCarryTracker_TryStartCarry.TryAllowPrisonerTransfer(__instance, item, ref __state))
            {
                return true;
            }

            if (item is Pawn pawn && LeadYourPetUtility.ShouldBlockCarryOfProtectedMouseEgg(__instance?.pawn, pawn))
            {
                __result = 0;
                return false;
            }

            return true;
        }

        public static void Postfix(Thing item, int __result, bool __state)
        {
            Patch_PawnCarryTracker_TryStartCarry.CompletePrisonerTransfer(item, __result > 0, __state);
        }
    }

    [HarmonyPatch]
    public static class Patch_PawnCarryTracker_TryDropCarriedThing
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing), new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(System.Action<Thing, int>) });
        }

        public static void Postfix(Pawn_CarryTracker __instance, bool __result, Thing resultingThing)
        {
            if (__result && resultingThing is Pawn pawn)
            {
                LeadYourPetUtility.Component?.TryRestoreTravelMouseEggLeashAfterCarry(__instance?.pawn, pawn);
            }
        }
    }

    [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.CanHaulBaby))]
    public static class Patch_ChildcareUtility_CanHaulBaby
    {
        public static bool Prefix(Pawn hauler, Pawn baby, ref bool __result)
        {
            if (LeadYourPetUtility.ShouldBlockCarryOfProtectedMouseEgg(hauler, baby))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.CanHaulBabyNow))]
    public static class Patch_ChildcareUtility_CanHaulBabyNow
    {
        public static bool Prefix(Pawn hauler, Pawn baby, bool ignoreOtherReservations, ref bool __result)
        {
            if (LeadYourPetUtility.ShouldBlockCarryOfProtectedMouseEgg(hauler, baby))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_PawnJobTracker_StartJob
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        public static bool Prefix(Pawn_JobTracker __instance, Job newJob)
        {
            Pawn pawn = __instance == null ? null : PawnField(__instance);
            Pawn targetPawn = newJob?.targetA.Thing as Pawn;
            bool departureCarry = LeadYourPetUtility.CanPerformDepartureHold(pawn, targetPawn, newJob?.def?.defName);
            if (!departureCarry && (LeadYourPetUtility.ShouldBlockExternalToddlerPickup(targetPawn, newJob?.def?.defName)
                || LeadYourPetUtility.ShouldBlockExternalToddlerPickup(pawn, newJob?.def?.defName)))
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), typeof(Job), typeof(JobTag?), typeof(bool))]
    public static class Patch_PawnJobTracker_TryTakeOrderedJob
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        public static bool Prefix(Pawn_JobTracker __instance, Job job, ref bool __result)
        {
            Pawn pawn = __instance == null ? null : PawnField(__instance);
            Pawn targetPawn = job?.targetA.Thing as Pawn;
            if (!LeadYourPetUtility.CanPerformDepartureHold(pawn, targetPawn, job?.def?.defName)
                && (LeadYourPetUtility.ShouldBlockExternalToddlerPickup(targetPawn, job?.def?.defName)
                    || LeadYourPetUtility.ShouldBlockExternalToddlerPickup(pawn, job?.def?.defName)))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public static class Patch_RimTalk_CustomDialogueService_CanTalk
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("RimTalk.Service.CustomDialogueService") != null;
        }

        static MethodBase TargetMethod()
        {
            System.Type type = AccessTools.TypeByName("RimTalk.Service.CustomDialogueService");
            return type == null ? null : AccessTools.Method(type, "CanTalk", new[] { typeof(Pawn), typeof(Pawn) });
        }

        public static bool Prefix(Pawn __0, Pawn __1, ref bool __result)
        {
            if (LeadYourPetUtility.ShouldBlockExternalDialogue(__0, "RimTalk")
                || LeadYourPetUtility.ShouldBlockExternalDialogue(__1, "RimTalk"))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public static class Patch_RimTalk_PawnUtil_IsTalkEligible
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("RimTalk.Util.PawnUtil") != null;
        }

        static MethodBase TargetMethod()
        {
            System.Type type = AccessTools.TypeByName("RimTalk.Util.PawnUtil");
            return type == null ? null : AccessTools.Method(type, "IsTalkEligible", new[] { typeof(Pawn) });
        }

        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (LeadYourPetUtility.ShouldTreatMouseEggAsRimTalkEligible(pawn, __result))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    public static class Patch_RimTalk_FloatMenuPatch_IsValidPawnToPawnConversation
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("RimTalk.Patch.FloatMenuPatch") != null;
        }

        static MethodBase TargetMethod()
        {
            System.Type type = AccessTools.TypeByName("RimTalk.Patch.FloatMenuPatch");
            return type == null ? null : AccessTools.Method(type, "IsValidPawnToPawnConversation", new[] { typeof(Pawn), typeof(Pawn) });
        }

        public static void Postfix(Pawn __0, Pawn __1, ref bool __result)
        {
            if (LeadYourPetUtility.ShouldAllowRimTalkMouseEggConversation(__0, __1, __result))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    public static class Patch_MentalStateBabyCry_AuraEffect
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MentalState_BabyCry), "AuraEffect");
        }

        public static bool Prefix(Thing source, Pawn hearer)
        {
            Pawn mouseEgg = source as Pawn;
            if (!LeadYourPetUtility.ShouldOverrideBabyCryMoodForMouseEgg(mouseEgg, hearer))
            {
                return true;
            }

            hearer.HearClamor(source, ClamorDefOf.BabyCry);
            MemoryThoughtHandler memories = hearer.needs.mood.thoughts.memories;
            memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.MyCryingBaby, mouseEgg);
            memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.CryingBaby, mouseEgg);
            memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.BabyCriedSocial, mouseEgg);
            memories.TryGainMemory(LeadYourPetDefOf.LeadYourPet_MouseEggCryingNearbyMood, mouseEgg);
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    public static class Patch_PawnRenderer_GetDrawParms
    {
        public static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if ((__result.flags & (PawnRenderFlags.Portrait | PawnRenderFlags.Cache | PawnRenderFlags.Statue)) != 0
                || !LeadYourPetUtility.TryGetSpecialRenderData(___pawn, out Vector3 drawPos, out float angle, out bool _, out Rot4 facing))
            {
                return;
            }

            __result.matrix = Matrix4x4.TRS(drawPos + ___pawn.ageTracker.CurLifeStage.bodyDrawOffset, Quaternion.AngleAxis(angle, Vector3.up), Vector3.one);
            __result.facing = facing;
        }
    }

#if RIMWORLD_1_7
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults", typeof(Vector3), typeof(Rot4?), typeof(bool), typeof(bool))]
    public static class Patch_PawnRenderer_ParallelGetPreRenderResults
    {
        public static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            // Cached atlas meshes use bodyPos/bodyAngle, bypassing the custom draw matrix.
            if (!disableCache && LeadYourPetUtility.TryGetSpecialRenderData(___pawn, out _, out _, out _, out _))
            {
                disableCache = true;
            }
        }
    }
#endif

    [HarmonyPatch(typeof(Pawn_RopeTracker), nameof(Pawn_RopeTracker.RopingDraw))]
    public static class Patch_PawnRopeTracker_RopingDraw
    {
        private static readonly FieldInfo RopeLineMatField = AccessTools.Field(typeof(Pawn_RopeTracker), "RopeLineMat");

        internal static bool TryGetCustomRopeEndpoints(Pawn pawn, out Vector3 start, out Vector3 end)
        {
            start = end = default(Vector3);
            if (!LeadYourPetUtility.TryGetCustomRopeTarget(pawn, out LocalTargetInfo target))
            {
                return false;
            }

            start = LeadYourPetUtility.GetVisualDrawPos(pawn).Yto0();
            end = (target.HasThing ? LeadYourPetUtility.GetVisualDrawPos(target.Thing) : target.Cell.ToVector3Shifted()).Yto0();
            return true;
        }

        public static void Postfix(Pawn_RopeTracker __instance, Pawn ___pawn)
        {
            if (__instance == null || __instance.IsRoped
                || !TryGetCustomRopeEndpoints(___pawn, out Vector3 start, out Vector3 end))
            {
                return;
            }

            GenDraw.DrawLineBetween(start, end, AltitudeLayer.PawnRope.AltitudeFor(), (Material)RopeLineMatField.GetValue(null), 0.2f);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!LeadYourPetUtility.CanPlayerCommand(__instance) || LeadYourPetUtility.Component == null || !LeadYourPetUtility.Component.AnyLeashedPetFor(__instance))
            {
                return;
            }

            List<Gizmo> list = new List<Gizmo>(__result);
            if (LeadYourPetUtility.Component.AnyMouseEggPetFor(__instance) || LeadYourPetUtility.Component.AnyRatkinMotherBabyFor(__instance))
            {
                list.Add(new Command_Action
                {
                    defaultLabel = "LeadYourPet_TightenLeash".Translate().Resolve(),
                    defaultDesc = "LeadYourPet_TightenLeashDesc".Translate().Resolve(),
                    icon = ContentFinder<Texture2D>.Get("UI/Overlays/Rope"),
                    action = delegate
                    {
                        LeadYourPetUtility.Component.TightenMouseEggLeashes(__instance);
                    }
                });
            }

            if (LeadYourPetUtility.Component.HasAnchoredPetsFor(__instance))
            {
                list.Add(new Command_Action
                {
                    defaultLabel = "LeadYourPet_ClearAnchors".Translate().Resolve(),
                    defaultDesc = "LeadYourPet_ClearAnchorsDesc".Translate().Resolve(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = delegate
                    {
                        LeadYourPetUtility.Component.ClearAnchorsForMaster(__instance);
                    }
                });
            }

            __result = list;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested), typeof(Pawn), typeof(float))]
    public static class Patch_Thing_Ingested
    {
        public static void Postfix(Thing __instance, Pawn ingester, float __result)
        {
            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (__instance == null || ingester == null || component == null)
            {
                return;
            }

            if (__result <= 0f || !LeadYourPetFeedFood.Allows(__instance.def) || !component.AnyMouseEggPetFor(ingester))
            {
                return;
            }

            List<LeashLink> masterLinks = component.GetLinksForMaster(ingester);
            if (masterLinks == null || masterLinks.Count == 0)
            {
                return;
            }

            for (int i = 0; i < masterLinks.Count; i++)
            {
                LeashLink link = masterLinks[i];
                if (link?.Pet == null || link.Kind != LeashLinkKind.MouseEggPet || link.AnchorMode != LeashAnchorMode.None)
                {
                    continue;
                }

                if (!LeadYourPetUtility.TryShareMasterMealNutrition(ingester, link.Pet, __result))
                {
                    continue;
                }

                component.NotifyMouseEggFedByMasterMeal(link);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetJobReport))]
    public static class Patch_Pawn_GetJobReport
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (LeadYourPetUtility.TryGetMouseEggDutyReport(__instance, out string report))
            {
                __result = report;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_Pawn_GetInspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!LeadYourPetUtility.TryGetMouseEggDutyReport(__instance, out string report))
            {
                return;
            }

            bool alreadyContains = !__result.NullOrEmpty() && __result.Contains(report);
            if (!LeadYourPetRules.ShouldAppendCustomMouseEggDutyInspectLine(
                hasCustomReport: true,
                inspectStringAlreadyContainsReport: alreadyContains))
            {
                return;
            }

            StringBuilder builder = new StringBuilder(__result ?? string.Empty);
            if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
            {
                builder.AppendLine();
            }

            builder.AppendLine(report.CapitalizeFirst().EndWithPeriod());
            __result = builder.ToString().TrimEnd('\r', '\n');
        }
    }
}
