using UnityEngine;
using Verse;

namespace HungerAndHavoc.Core
{
    public class RHAH_Mod : Mod
    {
        public static RHAH_Settings Settings;
        internal static event System.Action SettingsReset;

        public RHAH_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RHAH_Settings>();
            if (Settings.ObsoleteIncidentSettingsLoaded)
            {
                WriteSettings();
                Settings.ObsoleteIncidentSettingsLoaded = false;
            }
#if RHAH_IRISMENUS
            Pawn.Compat.RHAH_IrisMenusCompat.TryRegister(this);
#endif
        }

        public override string SettingsCategory()
        {
            return "RHAH_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            DrawResetSettings(listing);
            listing.CheckboxLabeled("RHAH_Settings_EnableNewContent".Translate(), ref Settings.enableNewContent,
                "RHAH_Settings_EnableNewContent_Tooltip".Translate());
            DrawRemoval(listing);
            listing.Label("RHAH_Menu_Narrative_Kingdom".Translate());
            listing.Label("RHAH_Settings_Kingdom_Summary".Translate());
            Storyteller.Suiyin.KingdomArcRuntime.DrawEntry(listing);
            listing.GapLine();
            listing.CheckboxLabeled("RHAH_Settings_OptimizeGeneration".Translate(), ref Settings.optimizeGeneration,
                "RHAH_Settings_OptimizeGeneration_Tooltip".Translate());
            bool relief = Settings.reliefEnabled;
            listing.CheckboxLabeled("RHAH_Settings_ReliefEnabled".Translate(), ref relief,
                "RHAH_Settings_ReliefEnabled_Tooltip".Translate());
            if (relief != Settings.reliefEnabled)
            {
                Settings.reliefEnabled = relief;
                Settings.InvalidateReliefSearch();
            }

            bool outside = Settings.allowEatOutsideRelief;
            listing.CheckboxLabeled("RHAH_Settings_EatOutsideRelief".Translate(), ref outside,
                "RHAH_Settings_EatOutsideRelief_Tooltip".Translate());
            if (outside != Settings.allowEatOutsideRelief)
            {
                Settings.allowEatOutsideRelief = outside;
                Settings.InvalidateReliefSearch();
            }

            bool ignore = Settings.ignoreReliefAfterFed;
            listing.CheckboxLabeled("RHAH_Settings_IgnoreReliefAfterFed".Translate(), ref ignore,
                "RHAH_Settings_IgnoreReliefAfterFed_Tooltip".Translate());
            if (ignore != Settings.ignoreReliefAfterFed)
            {
                Settings.ignoreReliefAfterFed = ignore;
                Settings.InvalidateReliefSearch();
            }

            bool leave = Settings.leaveAfterFed;
            listing.CheckboxLabeled("RHAH_Settings_LeaveAfterFed".Translate(), ref leave,
                "RHAH_Settings_LeaveAfterFed_Tooltip".Translate());
            if (leave != Settings.leaveAfterFed)
            {
                Settings.leaveAfterFed = leave;
                Settings.InvalidateReliefSearch();
            }
            listing.CheckboxLabeled("RHAH_Settings_FedWander".Translate(), ref Settings.fedWanderEnabled,
                "RHAH_Settings_FedWander_Tooltip".Translate());
            listing.Label("RHAH_Settings_FedWanderHours".Translate(Settings.fedWanderHours));
            Settings.fedWanderHours = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.fedWanderHours, Pawn.RHAH_VisitorRules.MinFedWanderHours, Pawn.RHAH_VisitorRules.MaxFedWanderHours));
            listing.CheckboxLabeled("RHAH_Settings_AidRequests".Translate(), ref Settings.aidRequestsEnabled,
                "RHAH_Settings_AidRequests_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_IntelTrades".Translate(), ref Settings.intelTradesEnabled,
                "RHAH_Settings_IntelTrades_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_VisitorChoices".Translate(), ref Settings.visitorChoicesEnabled,
                "RHAH_Settings_VisitorChoices_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_FoodGiveHint".Translate(), ref Settings.foodGiveHintDismissed,
                "RHAH_Settings_FoodGiveHint_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_TraderIgnoreEnvironment".Translate(), ref Settings.traderIgnoresHarshEnvironment,
                "RHAH_Settings_TraderIgnoreEnvironment_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_TraderIgnoreEnclosed".Translate(), ref Settings.traderIgnoresEnclosedSpace,
                "RHAH_Settings_TraderIgnoreEnclosed_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_FamineYear".Translate(), ref Settings.famineYearEnabled,
                "RHAH_Settings_FamineYear_Tooltip".Translate());
            listing.Label("RHAH_Settings_FamineYearChance".Translate(Settings.famineYearChancePercent.ToString("0.0")));
            Settings.famineYearChancePercent = listing.Slider(GameComponent_RHAH_FamineYear.ClampPercent(Settings.famineYearChancePercent, 40f), 0f, 100f);
            listing.Label("RHAH_Settings_FamineYearMultiplier".Translate(Settings.famineYearPawnMultiplier.ToString("0.00")));
            Settings.famineYearPawnMultiplier = listing.Slider(GameComponent_RHAH_FamineYear.ClampMultiplier(Settings.famineYearPawnMultiplier), 1f, 4f);
            listing.Label("RHAH_Settings_FamineYearDisasterChance".Translate(Settings.famineYearDisasterChancePercent.ToString("0.0")));
            Settings.famineYearDisasterChancePercent = listing.Slider(GameComponent_RHAH_FamineYear.ClampPercent(Settings.famineYearDisasterChancePercent, 20f), 0f, 100f);
            listing.CheckboxLabeled("RHAH_Settings_FamineArrival".Translate(), ref Settings.famineArrivalEnabled,
                "RHAH_Settings_FamineArrival_Tooltip".Translate());
            listing.Label("RHAH_Settings_FamineMalnutrition".Translate(Settings.famineArrivalMalnutrition.ToString("0.00")));
            Settings.famineArrivalMalnutrition = listing.Slider(Settings.famineArrivalMalnutrition, 0.01f, 0.5f);
            listing.CheckboxLabeled("RHAH_Settings_FamineMood".Translate(), ref Settings.famineMoodEnabled,
                "RHAH_Settings_FamineMood_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_ClayMood".Translate(), ref Settings.clayMoodEnabled,
                "RHAH_Settings_ClayMood_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_FamineTrade".Translate(), ref Settings.famineTradeEnabled,
                "RHAH_Settings_FamineTrade_Tooltip".Translate());
            listing.Label("RHAH_Settings_FoodSaleFactor".Translate(Settings.famineFoodSaleFactor.ToString("0.00")));
            Settings.famineFoodSaleFactor = listing.Slider(Settings.famineFoodSaleFactor, 1f, 3f);
            listing.Label("RHAH_Settings_GoodsBuyFactor".Translate(Settings.famineGoodsBuyFactor.ToString("0.00")));
            Settings.famineGoodsBuyFactor = listing.Slider(Settings.famineGoodsBuyFactor, 0.1f, 1f);
            listing.CheckboxLabeled("RHAH_Settings_ArmedDemand".Translate(), ref Settings.armedDemandEnabled,
                "RHAH_Settings_ArmedDemand_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_ArmedWeapons".Translate(), ref Settings.armedWeaponsEnabled,
                "RHAH_Settings_ArmedWeapons_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_SpaceApproach".Translate(), ref Settings.spaceApproachEnabled,
                "RHAH_Settings_SpaceApproach_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_ChildExchangeFood".Translate(), ref Settings.childExchangeFoodSubstitution,
                "RHAH_Settings_ChildExchangeFood_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_FamilyDrop".Translate(), ref Settings.familyDropEnabled,
                "RHAH_Settings_FamilyDrop_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_MotherFeed".Translate(), ref Settings.motherFeedEnabled,
                "RHAH_Settings_MotherFeed_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_PrisonerScavenge".Translate(), ref Settings.prisonerScavengeEnabled,
                "RHAH_Settings_PrisonerScavenge_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_TailBite".Translate(), ref Settings.tailBiteEnabled,
                "RHAH_Settings_TailBite_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_FamineDoors".Translate(), ref Settings.famineVisitorsOpenDoors,
                "RHAH_Settings_FamineDoors_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_PlagueSafe".Translate(), ref Settings.plagueSafeMode,
                "RHAH_Settings_PlagueSafe_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_GreenLetters".Translate(), ref Settings.greenIncidentLetters,
                "RHAH_Settings_GreenLetters_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_BegAutoGive".Translate(), ref Settings.begAutoGiveEnabled,
                "RHAH_Settings_BegAutoGive_Tooltip".Translate());
            listing.Label("RHAH_Settings_BegSuccessChance".Translate(Settings.begSuccessChancePercent));
            Settings.begSuccessChancePercent = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.begSuccessChancePercent, Pawn.RHAH_VisitorRules.MinBegSuccessChancePercent, Pawn.RHAH_VisitorRules.MaxBegSuccessChancePercent));
            listing.Label("RHAH_Settings_BegSocialBonus".Translate(Settings.begSocialBonusPercent));
            Settings.begSocialBonusPercent = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.begSocialBonusPercent, Pawn.RHAH_VisitorRules.MinBegSocialBonusPercent, Pawn.RHAH_VisitorRules.MaxBegSocialBonusPercent));
            listing.Label("RHAH_Settings_BegFailCooldown".Translate(Settings.begFailCooldownHours));
            Settings.begFailCooldownHours = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.begFailCooldownHours, Pawn.RHAH_VisitorRules.MinBegFailCooldownHours, Pawn.RHAH_VisitorRules.MaxBegFailCooldownHours));
            listing.Label("RHAH_Settings_BegSlapChance".Translate(Settings.begSlapChancePercent));
            Settings.begSlapChancePercent = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.begSlapChancePercent, Pawn.RHAH_VisitorRules.MinBegSlapChancePercent, Pawn.RHAH_VisitorRules.MaxBegSlapChancePercent));
            listing.Label("RHAH_Settings_PlayerIdeo".Translate(Settings.playerIdeoPercent));
            Settings.playerIdeoPercent = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.playerIdeoPercent, 0f, 100f));
            listing.CheckboxLabeled("RHAH_Settings_Broadcast".Translate(), ref Settings.broadcastEnabled,
                "RHAH_Settings_Broadcast_Tooltip".Translate());
            listing.Label("RHAH_Settings_BroadcastCooldown".Translate(Settings.broadcastCooldownDays));
            Settings.broadcastCooldownDays = Mathf.RoundToInt(listing.Slider(Settings.broadcastCooldownDays,
                Incidents.RHAH_BroadcastRules.MinCooldownDays, Incidents.RHAH_BroadcastRules.MaxCooldownDays));
            listing.CheckboxLabeled("RHAH_Settings_Stagger".Translate(), ref Settings.staggerGeneration,
                "RHAH_Settings_Stagger_Tooltip".Translate());
            listing.Label("RHAH_Settings_ShelterDays".Translate(Pawn.RHAH_VisitorRules.StayLabel(Settings.shelterDays)));
            Settings.shelterDays = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.shelterDays, Pawn.RHAH_VisitorRules.MinShelterDays, Pawn.RHAH_VisitorRules.MaxShelterDays));
            listing.CheckboxLabeled("RHAH_Settings_ShortStayLockApparel".Translate(), ref Settings.shortStayLockApparel,
                "RHAH_Settings_ShortStayLockApparel_Tooltip".Translate());
            listing.Label("RHAH_Settings_HireDays".Translate(Pawn.RHAH_VisitorRules.StayLabel(Settings.hireDays)));
            Settings.hireDays = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.hireDays, Pawn.RHAH_VisitorRules.MinHireDays, Pawn.RHAH_VisitorRules.MaxHireDays));
            listing.Label("RHAH_Settings_GnawBark".Translate(Settings.barkNutrition.ToString("0.00")));
            Settings.barkNutrition = listing.Slider(Settings.barkNutrition, 0f, 2f);
            listing.Label("RHAH_Settings_GnawBarkDamage".Translate(Settings.barkDamage.ToString("0")));
            Settings.barkDamage = listing.Slider(Settings.barkDamage, 0f, 50f);
            listing.Label("RHAH_Settings_GnawWall".Translate(Settings.wallNutrition.ToString("0.00")));
            Settings.wallNutrition = listing.Slider(Settings.wallNutrition, 0f, 2f);
            listing.Label("RHAH_Settings_GnawWallDamage".Translate(Settings.wallDamage.ToString("0")));
            Settings.wallDamage = listing.Slider(Settings.wallDamage, 0f, 50f);
            listing.CheckboxLabeled("RHAH_Settings_PawnHistories".Translate(), ref Settings.pawnHistoriesEnabled,
                "RHAH_Settings_PawnHistories_Tooltip".Translate());
            listing.CheckboxLabeled("RHAH_Settings_PawnTraits".Translate(), ref Settings.pawnTraitsEnabled,
                "RHAH_Settings_PawnTraits_Tooltip".Translate());
            listing.Label("RHAH_Settings_LitterMin".Translate(Settings.litterMin));
            Settings.litterMin = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.litterMin, Generation.RHAH_FertilityRules.MinLitter, Generation.RHAH_FertilityRules.MaxLitter));
            listing.Label("RHAH_Settings_LitterPeak".Translate(Settings.litterPeak));
            Settings.litterPeak = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.litterPeak, Settings.litterMin, Settings.litterMax));
            listing.Label("RHAH_Settings_LitterMax".Translate(Settings.litterMax));
            Settings.litterMax = UnityEngine.Mathf.RoundToInt(listing.Slider(Settings.litterMax, Settings.litterMin, Generation.RHAH_FertilityRules.MaxLitter));
            listing.Label("RHAH_Settings_FertileAge".Translate(Settings.fertileMinAge.ToString("0")));
            Settings.fertileMinAge = UnityEngine.Mathf.Round(listing.Slider(Settings.fertileMinAge, Generation.RHAH_FertilityRules.MinFertileAge, Generation.RHAH_FertilityRules.MaxFertileAge));
            listing.Label("RHAH_Settings_MotherAge".Translate(Settings.minMotherAge.ToString("0")));
            Settings.minMotherAge = UnityEngine.Mathf.Round(listing.Slider(Settings.minMotherAge, Pawn.RHAH_VisitorRules.MinMotherAge, Pawn.RHAH_VisitorRules.MaxMotherAge));
            listing.Label("RHAH_Settings_FertilityPercent".Translate(Settings.fertilityPercent.ToString("0")));
            Settings.fertilityPercent = UnityEngine.Mathf.Round(listing.Slider(Settings.fertilityPercent, Generation.RHAH_FertilityRules.MinFertilityPercent, Generation.RHAH_FertilityRules.MaxFertilityPercent));
            listing.Label("RHAH_Settings_GestationDays".Translate(Settings.gestationDays.ToString("0.0")));
            Settings.gestationDays = listing.Slider(Settings.gestationDays, Generation.RHAH_FertilityRules.MinGestationDays, Generation.RHAH_FertilityRules.VanillaGestationFloorDays);
            listing.Label("RHAH_Settings_LovinPregnancy".Translate(Settings.lovinPregnancyPercent.ToString("0")));
            Settings.lovinPregnancyPercent = UnityEngine.Mathf.Round(listing.Slider(Settings.lovinPregnancyPercent, Generation.RHAH_FertilityRules.MinLovinPregnancyPercent, Generation.RHAH_FertilityRules.MaxLovinPregnancyPercent));
            Generation.RHAH_FertilityRules.ClampLitter(ref Settings.litterMin, ref Settings.litterPeak, ref Settings.litterMax);

            listing.End();
        }
        internal static void DrawResetSettings(Listing_Standard listing)
        {
            if (listing.ButtonText("RHAH_Settings_Reset".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RHAH_Settings_ResetConfirm".Translate(),
                    () =>
                    {
                        Settings.ResetToDefaults();
                        SettingsReset?.Invoke();
                        Settings.Mod.WriteSettings();
                    },
                    destructive: true));
            }
        }

        static void DrawRemoval(Listing_Standard listing)
        {
            listing.GapLine();
            GameComponent_RHAH_Game game = Current.ProgramState == ProgramState.Playing
                ? Current.Game?.GetComponent<GameComponent_RHAH_Game>()
                : null;
            if (game == null)
            {
                listing.Label("RHAH_RemovalNoGame".Translate());
                return;
            }

            listing.Label("RHAH_RemovalSection".Translate());
            if (game.EventChainMigrationStatus.StartsWith("failed:", System.StringComparison.Ordinal))
            {
                listing.Label("RHAH_EventChainsFailure".Translate(game.EventChainMigrationStatus));
            }

            if (game.NewContentDisabled)
            {
                listing.Label("RHAH_RemovalDisabled".Translate());
            }
            else if (listing.ButtonText("RHAH_RemovalDisable".Translate()))
            {
                game.DisableNewContent();
            }

            if (listing.ButtonText("RHAH_RemovalExport".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RHAH_RemovalConfirm".Translate(),
                    () => LongEventHandler.QueueLongEvent(RHAH_SaveExport.Export, "SavingLongEvent", false, null)));
            }
        }
    }
}
