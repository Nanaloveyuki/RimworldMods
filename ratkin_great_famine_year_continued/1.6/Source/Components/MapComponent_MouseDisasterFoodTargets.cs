using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public sealed class MouseDisasterFoodSearchState
    {
        public MapComponent_MouseDisasterFoodTargets cache;
        public int key;
        public bool hit;
    }

    public sealed class MapComponent_MouseDisasterFoodTargets : MapComponent
    {
        private const int EmptySearchRetryTicks = 250;
        private sealed class Entry
        {
            public Thing food;
            public ThingDef def;
            public IntVec3 position;
            public int retryAt;
            public int foodSources;
            public int inventoryCount;
            public int reliefCells;
            public float reliefBonus;
            public int foodSettingsStamp;
            public bool allowOutsideRelief;
        }

        private readonly Dictionary<Pawn, Dictionary<int, Entry>> targets = new Dictionary<Pawn, Dictionary<int, Entry>>();
        private readonly Dictionary<int, Entry> sharedTargets = new Dictionary<int, Entry>();
        private Thing anyFood;
        private int nextAnyFoodScan;
        private int previousFoodCount = -1;

        public MapComponent_MouseDisasterFoodTargets(Map map) : base(map) { }

        public static bool Eligible(Pawn pawn)
        {
            return pawn?.Map != null && !MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn) &&
                (MouseDisasterUtility.IsThiefPawn(pawn) || MouseDisasterUtility.IsBeggarPawn(pawn) ||
                    GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn, MouseDisasterPawnBehavior.SeekFood));
        }

        public static void Prime(Pawn pawn)
        {
            if (!Eligible(pawn) || MouseDisasterFeeding.IsSeekingSuppressed(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.needs?.food == null) return;
            FoodUtility.TryFindBestFoodSourceFor(pawn, pawn, pawn.needs.food.CurCategory == HungerCategory.Starving,
                out _, out _, canRefillDispenser: true, canUseInventory: true, canUsePackAnimalInventory: false,
                allowForbidden: false, allowCorpse: !MouseDisasterUtility.IsThiefPawn(pawn), allowSociallyImproper: true,
                allowHarvest: false, forceScanWholeMap: true, ignoreReservations: false, calculateWantedStackCount: false, allowVenerated: false);
        }

        public static int Key(bool desperate, bool refill, bool inventory, bool packInventory, bool forbidden,
            bool corpse, bool improper, bool harvest, bool wholeMap, bool ignoreReservations, bool wantedCount,
            bool venerated, FoodPreferability minimum, bool areaSearch = false, bool reliefOnly = false)
        {
            return (desperate ? 1 : 0) | (refill ? 2 : 0) | (inventory ? 4 : 0) | (packInventory ? 8 : 0) |
                (forbidden ? 16 : 0) | (corpse ? 32 : 0) | (improper ? 64 : 0) | (harvest ? 128 : 0) |
                (wholeMap ? 256 : 0) | (ignoreReservations ? 512 : 0) | (wantedCount ? 1024 : 0) |
                (venerated ? 2048 : 0) | (areaSearch ? 4096 : 0) | (reliefOnly ? 8192 : 0) | (((int)minimum + 1) << 16);
        }

        public bool TryRead(Pawn pawn, int key, out Thing food, out ThingDef def, out bool found)
        {
            food = null; def = null; found = false;
            if (!Eligible(pawn) || pawn.Map != map) return false;
            Entry entry = null;
            if (targets.TryGetValue(pawn, out var requests)) requests.TryGetValue(key, out entry);
            if (entry == null && !sharedTargets.TryGetValue(key, out entry)) return false;
            if (entry.reliefBonus != (MouseDisasterMod.Settings?.reliefFoodScoreBonus ?? 0.1f)) return false;
            if (entry.reliefCells != (MouseDisasterUtility.GetReliefArea(map)?.TrueCount ?? 0)) return false;
            if (entry.foodSettingsStamp != (MouseDisasterMod.Settings?.EventFoodSettingsStamp ?? 0)) return false;
            if (entry.allowOutsideRelief != (MouseDisasterMod.Settings?.allowEventPawnsEatOutsideReliefArea == true)) return false;
            if (entry.food == null) return entry.foodSources == FoodCount && entry.inventoryCount == (pawn.inventory?.innerContainer.Count ?? 0) &&
                Find.TickManager.TicksGame < entry.retryAt;
            Thing candidate = entry.food;
            if (candidate.Destroyed || !candidate.IngestibleNow || (candidate.Spawned && candidate.PositionHeld != entry.position) ||
                (candidate.MapHeld != map && candidate.ParentHolder != pawn.inventory)) return false;
            if (!candidate.Spawned && ((key & 4) == 0 || candidate.ParentHolder != pawn.inventory)) return false;
            if ((key & 16) == 0 && candidate.IsForbidden(pawn)) return false;
            if (((key & 1) == 0 && candidate.IsNotFresh()) || candidate.IsDessicated()) return false;
            if ((key & 64) == 0 && !candidate.IsSociallyProper(pawn)) return false;
            if ((key & 4096) != 0 && (MouseDisasterUtility.GetReliefArea(map)?[candidate.PositionHeld] == true) != ((key & 8192) != 0)) return false;
            if (!pawn.WillEat(candidate, pawn, careIfNotAcceptableForTitle: true, allowVenerated: (key & 2048) != 0)) return false;
            int wanted = (key & 1024) != 0
                ? System.Math.Max(1, FoodUtility.WillIngestStackCountOf(pawn, entry.def, FoodUtility.GetNutrition(pawn, candidate, entry.def)))
                : 1;
            if (candidate.stackCount < wanted) return false;
            if (candidate.Spawned && (((key & 512) == 0 && !pawn.CanReserve(candidate, 10, wanted)) ||
                !pawn.CanReach(candidate, PathEndMode.ClosestTouch, Danger.Some))) return false;
            food = candidate; def = entry.def; found = true;
            if (requests == null) targets[pawn] = requests = new Dictionary<int, Entry>();
            requests[key] = entry;
            return true;
        }

        public void Store(Pawn pawn, int key, Thing food, ThingDef def, bool found)
        {
            if (!Eligible(pawn) || pawn.Map != map) return;
            // Dispensers, harvest and corpses retain the original specialized search semantics.
            if (found && (food == null || def != food.def || food is Building || food is Plant || food is Corpse ||
                (!food.Spawned && food.ParentHolder != pawn.inventory))) return;
            if (!targets.TryGetValue(pawn, out var requests)) targets[pawn] = requests = new Dictionary<int, Entry>();
            var entry = new Entry
            {
                food = found ? food : null, def = def, position = food?.PositionHeld ?? IntVec3.Invalid,
                retryAt = Find.TickManager.TicksGame + EmptySearchRetryTicks + pawn.thingIDNumber % 60,
                foodSources = FoodCount, inventoryCount = pawn.inventory?.innerContainer.Count ?? 0,
                reliefCells = MouseDisasterUtility.GetReliefArea(map)?.TrueCount ?? 0,
                reliefBonus = MouseDisasterMod.Settings?.reliefFoodScoreBonus ?? 0.1f,
                foodSettingsStamp = MouseDisasterMod.Settings?.EventFoodSettingsStamp ?? 0,
                allowOutsideRelief = MouseDisasterMod.Settings?.allowEventPawnsEatOutsideReliefArea == true
            };
            requests[key] = entry;
            if (found && food.Spawned) sharedTargets[key] = entry;
        }

        private int FoodCount => map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource).Count;

        public bool HasAnyFood()
        {
            if (anyFood?.Map == map && MouseDisasterUtility.IsAreaFoodSourceThing(anyFood, true)) return true;
            int count = FoodCount;
            if (anyFood == null && count == previousFoodCount && Find.TickManager.TicksGame < nextAnyFoodScan) return false;
            previousFoodCount = count;
            nextAnyFoodScan = Find.TickManager.TicksGame + EmptySearchRetryTicks;
            anyFood = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource).FirstOrDefault(t => MouseDisasterUtility.IsAreaFoodSourceThing(t, true));
            return anyFood != null;
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % GenDate.TicksPerHour != 0) return;
            foreach (Pawn pawn in targets.Keys.Where(p => p.Destroyed || p.Dead || p.Map != map || MouseDisasterUtility.IsPlayerAffiliatedRatkin(p)).ToList())
                targets.Remove(pawn);
        }
    }
}
