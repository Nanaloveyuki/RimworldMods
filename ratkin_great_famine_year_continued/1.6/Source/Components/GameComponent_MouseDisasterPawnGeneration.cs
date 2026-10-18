using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public enum MouseDisasterPawnBatchKind
    {
        LargeRefugeeWave,
        GreatFamine,
        TraderCaravan,
        BeggarGroup,
        ThiefGroup,
        WildGroup,
        TravelerGroup,
        FamineRefugees,
        SiegeBeggarGroup,
        StrongSiegeGroup,
        AirdropMistake,
        ThiefChildGroup,
        EggBombRetaliation,
        PlagueRevengeWave
    }

    public class MouseDisasterPawnBatch : IExposable
    {
        public MouseDisasterPawnBatchKind kind;
        public Map map;
        public Faction faction;
        public IncidentDef incidentDef;
        public IncidentParms parms;
        public IntVec3 entryCell;
        public int adultsRemaining;
        public int childrenRemaining;
        public int remainingCount;
        public int nextSpawnTick;
        public int deadlineTick;
        public int intervalTicks = 1;
        public int randomSeed;
        public int generatedSlots;
        public int behaviorGroupId;
        public bool infectsWithPlague;
        public float foodPercentage = 0.35f;
        public IntVec3 exitCell;
        public bool thiefLike;
        public bool pureNegative;
        public int stayHours;
        public Faction sourceFaction;
        public LetterDef specialLetterDef;
        public List<Pawn> pawns = new List<Pawn>();
        public Pawn traderPawn;
        public Pawn escortPawn;
        public Lord gatheringLord;

        public bool Complete => adultsRemaining <= 0 && childrenRemaining <= 0 && remainingCount <= 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind", MouseDisasterPawnBatchKind.LargeRefugeeWave);
            Scribe_Values.Look(ref behaviorGroupId, "behaviorGroupId", 0);
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Defs.Look(ref incidentDef, "incidentDef");
            Scribe_Deep.Look(ref parms, "parms");
            Scribe_Values.Look(ref entryCell, "entryCell", IntVec3.Invalid);
            Scribe_Values.Look(ref adultsRemaining, "adultsRemaining", 0);
            Scribe_Values.Look(ref childrenRemaining, "childrenRemaining", 0);
            Scribe_Values.Look(ref remainingCount, "remainingCount", 0);
            Scribe_Values.Look(ref nextSpawnTick, "nextSpawnTick", 0);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", 0);
            Scribe_Values.Look(ref intervalTicks, "intervalTicks", 1);
            Scribe_Values.Look(ref randomSeed, "randomSeed", 0);
            Scribe_Values.Look(ref generatedSlots, "generatedSlots", 0);
            Scribe_Values.Look(ref infectsWithPlague, "infectsWithPlague", false);
            Scribe_Values.Look(ref foodPercentage, "foodPercentage", 0.35f);
            Scribe_Values.Look(ref exitCell, "exitCell", IntVec3.Invalid);
            Scribe_Values.Look(ref thiefLike, "thiefLike", false);
            Scribe_Values.Look(ref pureNegative, "pureNegative", false);
            Scribe_Values.Look(ref stayHours, "stayHours", 0);
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_References.Look(ref traderPawn, "traderPawn");
            Scribe_References.Look(ref escortPawn, "escortPawn");
            Scribe_References.Look(ref gatheringLord, "gatheringLord");
            Scribe_References.Look(ref sourceFaction, "sourceFaction");
            Scribe_Defs.Look(ref specialLetterDef, "specialLetterDef");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new List<Pawn>();
                intervalTicks = Mathf.Max(1, intervalTicks);
            }
        }
    }

    public partial class GameComponent_MouseDisasterPawnGeneration : GameComponent
    {
        private const int TargetGenerationTicks = 100;
        private const int MaxBatchLifetimeTicks = 10000;
        private const int MaxGeneratedSlots = 128;

        private List<MouseDisasterPawnBatch> batches = new List<MouseDisasterPawnBatch>();

        public GameComponent_MouseDisasterPawnGeneration(Game game)
        {
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<MouseDisasterPawnBatch> saveBatches = batches?
                    .Where(IsBatchMapAvailable)
                    .ToList() ?? new List<MouseDisasterPawnBatch>();
                Scribe_Collections.Look(ref saveBatches, "mouseDisaster_pawnGenerationBatches", LookMode.Deep);
                return;
            }

            Scribe_Collections.Look(ref batches, "mouseDisaster_pawnGenerationBatches", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                batches ??= new List<MouseDisasterPawnBatch>();
                batches.RemoveAll(batch => batch == null);
                int nowTick = Find.TickManager?.TicksGame ?? 0;
                for (int i = 0; i < batches.Count; i++)
                {
                    NormalizeLoadedBatch(batches[i], nowTick);
                }
            }
        }

        public override void GameComponentTick()
        {
            TickManager tickManager = Find.TickManager;
            if (tickManager == null || tickManager.TicksThisFrame != 0 || batches == null || batches.Count == 0)
            {
                return;
            }

            int nowTick = tickManager.TicksGame;
            for (int i = 0; i < batches.Count; i++)
            {
                MouseDisasterPawnBatch batch = batches[i];
                if (!IsBatchMapAvailable(batch))
                {
                    ReleaseTraderGatheringLord(batch);
                    Log.Warning("[MouseDisaster] Discarding staged pawn generation because its map was removed: " + (batch?.kind.ToString() ?? "unknown") + ".");
                    batches.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!HasRequiredState(batch))
                {
                    TryTruncateBatch(batch, "required saved state is missing");
                    batches.RemoveAt(i);
                    return;
                }

                if (batch.kind == MouseDisasterPawnBatchKind.TraderCaravan && !TryEnsureTraderGatheringLord(batch))
                {
                    TryTruncateBatch(batch, "trader gathering duty could not be restored");
                    batches.RemoveAt(i);
                    return;
                }

                if (batch.Complete)
                {
                    TryFinalizeBatch(batch);
                    batches.RemoveAt(i);
                    return;
                }

                if (batch.generatedSlots >= MaxGeneratedSlots || nowTick >= batch.deadlineTick)
                {
                    TryTruncateBatch(batch, "generation limit or deadline reached");
                    batches.RemoveAt(i);
                    return;
                }

                if (nowTick < batch.nextSpawnTick)
                {
                    continue;
                }

                try
                {
                    ProcessNextPawn(batch);
                    if (batch.Complete)
                    {
                        TryFinalizeBatch(batch);
                        batches.RemoveAt(i);
                    }
                    else
                    {
                        batch.nextSpawnTick = nowTick + batch.intervalTicks;
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("[MouseDisaster] Staged pawn generation failed for " + batch.kind + ": " + exception);
                    TryTruncateBatch(batch, "an exception interrupted generation");
                    batches.RemoveAt(i);
                }

                return;
            }
        }

        internal bool HasPendingBehaviorGroup(int groupId)
        {
            return batches != null && batches.Any(batch => batch != null && batch.behaviorGroupId == groupId);
        }

        public static bool TryStartLargeRefugeeWave(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, int adults, int children)
        {
            if (adults < 0 || children < 0 || adults > MaxGeneratedSlots || children > MaxGeneratedSlots - adults)
            {
                return false;
            }

            MouseDisasterUtility.TryFindFormerFaction(out Faction faction);
            if (faction != null)
            {
                MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
                MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(map, faction);
            }

            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.LargeRefugeeWave,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                adultsRemaining = adults,
                childrenRemaining = children
            }, adults + children);
        }

        public static bool TryStartGreatFamine(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, Faction faction, int count, bool infectsWithPlague)
        {
            if (count <= 0 || count > MaxGeneratedSlots)
            {
                return false;
            }

            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.GreatFamine,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                remainingCount = count,
                infectsWithPlague = infectsWithPlague
            }, count);
        }

        public static bool TryStartTraderCaravan(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, Faction faction, int saleChildren, bool infectsWithPlague = false)
        {
            if (faction == null || saleChildren <= 0 || saleChildren > MaxGeneratedSlots - 2)
            {
                return false;
            }

            int totalCount = saleChildren + 2;
            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.TraderCaravan,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                remainingCount = totalCount,
                infectsWithPlague = infectsWithPlague
            }, totalCount);
        }

        private static bool TryStartBatch(MouseDisasterPawnBatch batch, int totalCount)
        {
            GameComponent_MouseDisasterPawnGeneration component = Current.Game?.GetComponent<GameComponent_MouseDisasterPawnGeneration>();
            TickManager tickManager = Find.TickManager;
            bool standalonePayload = IsStandalonePayloadBatch(batch);
            if (!MouseDisasterRuntime.AllowsNewContent || component == null || tickManager == null || totalCount <= 0 || totalCount > MaxGeneratedSlots ||
                !IsBatchMapAvailable(batch) || !batch.entryCell.IsValid ||
                (!standalonePayload && (batch.incidentDef == null || batch.parms == null)))
            {
                return false;
            }

            int minimum = batch.kind == MouseDisasterPawnBatchKind.TraderCaravan ? 3 :
                batch.kind == MouseDisasterPawnBatchKind.LargeRefugeeWave ? 10 :
                batch.kind == MouseDisasterPawnBatchKind.GreatFamine ? 8 :
                batch.kind == MouseDisasterPawnBatchKind.BeggarGroup || batch.kind == MouseDisasterPawnBatchKind.ThiefGroup ||
                batch.kind == MouseDisasterPawnBatchKind.ThiefChildGroup || batch.kind == MouseDisasterPawnBatchKind.SiegeBeggarGroup ? 3 :
                batch.kind == MouseDisasterPawnBatchKind.TravelerGroup || batch.kind == MouseDisasterPawnBatchKind.StrongSiegeGroup ||
                batch.kind == MouseDisasterPawnBatchKind.AirdropMistake || batch.kind == MouseDisasterPawnBatchKind.EggBombRetaliation ? 4 :
                batch.kind == MouseDisasterPawnBatchKind.WildGroup ? 2 :
                batch.adultsRemaining > 0 && batch.childrenRemaining > 0 ? 2 : 1;
            int limited = MouseDisasterUtility.LimitEventPawnCount(totalCount, minimum);
            if (limited < totalCount)
            {
                if (batch.adultsRemaining + batch.childrenRemaining > 0)
                {
                    int children = batch.childrenRemaining > 0 ? Mathf.Clamp(Mathf.RoundToInt(limited * batch.childrenRemaining / (float)totalCount), 1, limited - 1) : 0;
                    batch.childrenRemaining = children;
                    batch.adultsRemaining = limited - children;
                }
                else batch.remainingCount = limited;
                totalCount = limited;
            }
            MouseDisasterTrace.Log("pawn batch start; kind=" + batch.kind + "; requested=" + totalCount +
                "; incident=" + batch.incidentDef?.defName + "; " + MouseDisasterTrace.DescribeMap(batch.map) +
                "; entry=" + batch.entryCell);
            batch.intervalTicks = Mathf.Max(1, Mathf.RoundToInt(TargetGenerationTicks / (float)totalCount));
            batch.randomSeed = Rand.Int;
            batch.deadlineTick = tickManager.TicksGame + MaxBatchLifetimeTicks;
            bool disableMultiFrameGeneration = MouseDisasterMod.Settings?.disableMultiFrameIncidentGeneration == true;
            try
            {
                if (disableMultiFrameGeneration)
                {
                    while (!batch.Complete && batch.generatedSlots < MaxGeneratedSlots)
                    {
                        ProcessNextPawn(batch);
                    }
                }
                else
                {
                    ProcessNextPawn(batch);
                }
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Could not start staged pawn generation for " + batch.kind + ": " + exception);
                TryTruncateBatch(batch, "the initial generation step failed");
                return false;
            }

            if (batch.pawns.Count == 0)
            {
                return false;
            }

            if (batch.Complete)
            {
                TryFinalizeBatch(batch);
            }
            else if (disableMultiFrameGeneration)
            {
                TryTruncateBatch(batch, "synchronous generation limit reached");
            }
            else
            {
                batch.nextSpawnTick = tickManager.TicksGame + batch.intervalTicks;
                component.batches.Add(batch);
            }

            MouseDisasterTrace.Log("pawn batch staged; kind=" + batch.kind + "; generated=" + batch.generatedSlots +
                "; pawns=" + batch.pawns.Count + "; remaining=" + batch.remainingCount +
                "; intervalTicks=" + batch.intervalTicks);

            return true;
        }

        private static bool IsBatchMapAvailable(MouseDisasterPawnBatch batch)
        {
            return batch?.map != null && !batch.map.Disposed && Find.Maps != null && Find.Maps.Contains(batch.map);
        }

        private static bool HasRequiredState(MouseDisasterPawnBatch batch)
        {
            return batch != null &&
                   Enum.IsDefined(typeof(MouseDisasterPawnBatchKind), batch.kind) &&
                   (batch.incidentDef != null || IsStandalonePayloadBatch(batch)) &&
                   (batch.parms != null || IsStandalonePayloadBatch(batch)) &&
                   batch.pawns != null &&
                   batch.entryCell.IsValid;
        }

        private static void NormalizeLoadedBatch(MouseDisasterPawnBatch batch, int nowTick)
        {
            batch.intervalTicks = Mathf.Clamp(batch.intervalTicks, 1, TargetGenerationTicks);
            batch.generatedSlots = Mathf.Clamp(batch.generatedSlots, 0, MaxGeneratedSlots);
            batch.pawns ??= new List<Pawn>();
            batch.pawns = batch.pawns.Where(pawn => pawn != null).Distinct().ToList();
            batch.generatedSlots = Mathf.Max(batch.generatedSlots, batch.pawns.Count);

            switch (batch.kind)
            {
                case MouseDisasterPawnBatchKind.LargeRefugeeWave:
                    batch.adultsRemaining = Mathf.Clamp(batch.adultsRemaining, 0, 50);
                    batch.childrenRemaining = Mathf.Clamp(batch.childrenRemaining, 0, 50 - batch.adultsRemaining);
                    batch.remainingCount = 0;
                    break;
                case MouseDisasterPawnBatchKind.GreatFamine:
                    batch.adultsRemaining = 0;
                    batch.childrenRemaining = 0;
                    batch.remainingCount = Mathf.Clamp(batch.remainingCount, 0, 28);
                    break;
                case MouseDisasterPawnBatchKind.TraderCaravan:
                    batch.adultsRemaining = 0;
                    batch.childrenRemaining = 0;
                    batch.remainingCount = Mathf.Clamp(batch.remainingCount, 0, 32);
                    if (MouseDisasterDefOf.MouseDisaster_TraderRatkinAdult != null &&
                        (!batch.pawns.Contains(batch.traderPawn) ||
                         batch.traderPawn?.kindDef != MouseDisasterDefOf.MouseDisaster_TraderRatkinAdult))
                    {
                        batch.traderPawn = batch.pawns.FirstOrDefault(pawn => pawn.kindDef == MouseDisasterDefOf.MouseDisaster_TraderRatkinAdult);
                    }
                    if (MouseDisasterDefOf.MouseDisaster_TraderRatkinEscort != null &&
                        (!batch.pawns.Contains(batch.escortPawn) ||
                         batch.escortPawn?.kindDef != MouseDisasterDefOf.MouseDisaster_TraderRatkinEscort))
                    {
                        batch.escortPawn = batch.pawns.FirstOrDefault(pawn => pawn.kindDef == MouseDisasterDefOf.MouseDisaster_TraderRatkinEscort);
                    }
                    batch.faction ??= batch.traderPawn?.Faction ?? batch.escortPawn?.Faction ?? batch.pawns.FirstOrDefault()?.Faction;
                    break;
                case MouseDisasterPawnBatchKind.BeggarGroup:
                case MouseDisasterPawnBatchKind.SiegeBeggarGroup:
                case MouseDisasterPawnBatchKind.TravelerGroup:
                    batch.adultsRemaining = Mathf.Clamp(batch.adultsRemaining, 0, 50);
                    batch.childrenRemaining = Mathf.Clamp(batch.childrenRemaining, 0, 50 - batch.adultsRemaining);
                    batch.remainingCount = 0;
                    if (!batch.exitCell.IsValid && batch.map != null)
                    {
                        batch.exitCell = batch.map.Center;
                    }
                    break;
                case MouseDisasterPawnBatchKind.ThiefGroup:
                case MouseDisasterPawnBatchKind.ThiefChildGroup:
                case MouseDisasterPawnBatchKind.WildGroup:
                case MouseDisasterPawnBatchKind.FamineRefugees:
                case MouseDisasterPawnBatchKind.StrongSiegeGroup:
                case MouseDisasterPawnBatchKind.AirdropMistake:
                case MouseDisasterPawnBatchKind.EggBombRetaliation:
                case MouseDisasterPawnBatchKind.PlagueRevengeWave:
                    batch.adultsRemaining = 0;
                    batch.childrenRemaining = 0;
                    batch.remainingCount = Mathf.Clamp(batch.remainingCount, 0, MaxGeneratedSlots);
                    break;
            }

            if (batch.deadlineTick <= 0)
            {
                batch.deadlineTick = nowTick + MaxBatchLifetimeTicks;
            }
        }

        private static void ProcessNextPawn(MouseDisasterPawnBatch batch)
        {
            if (batch.behaviorGroupId == 0)
                batch.behaviorGroupId = MouseDisasterEventExecution.Current?.groupId ??
                    GameComponent_MouseDisasterEventBehavior.Component?.CreateGroup(batch.incidentDef?.defName ?? batch.kind.ToString()) ?? 0;
            batch.faction = GameComponent_MouseDisasterEventBehavior.Component?.FactionForGroup(batch.behaviorGroupId) ?? batch.faction;
            int firstNewPawn = batch.pawns.Count;
            int slot = batch.generatedSlots++;
            MouseDisasterTrace.Log("pawn generation slot begin; kind=" + batch.kind + "; slot=" + slot +
                "; existingPawns=" + firstNewPawn + "; " + MouseDisasterTrace.DescribeMap(batch.map));
            Rand.PushState(Gen.HashCombineInt(batch.randomSeed, slot));
            try
            {
                using (MouseDisasterPawnHistoryCatalog.PushContext(batch.incidentDef?.defName, batch.map))
                {
                    switch (batch.kind)
                    {
                        case MouseDisasterPawnBatchKind.LargeRefugeeWave:
                            GenerateLargeRefugee(batch);
                            break;
                        case MouseDisasterPawnBatchKind.GreatFamine:
                            GenerateGreatFaminePawn(batch);
                            break;
                        case MouseDisasterPawnBatchKind.TraderCaravan:
                            GenerateTraderCaravanPawn(batch, slot);
                            break;
                        case MouseDisasterPawnBatchKind.BeggarGroup:
                        case MouseDisasterPawnBatchKind.SiegeBeggarGroup:
                            GenerateBeggarGroupPawn(batch);
                            break;
                        case MouseDisasterPawnBatchKind.ThiefGroup:
                        case MouseDisasterPawnBatchKind.ThiefChildGroup:
                        case MouseDisasterPawnBatchKind.StrongSiegeGroup:
                            GenerateThiefGroupPawn(batch);
                            break;
                        case MouseDisasterPawnBatchKind.WildGroup:
                            GenerateWildGroupPawn(batch);
                            break;
                        case MouseDisasterPawnBatchKind.TravelerGroup:
                            GenerateTravelerGroupPawn(batch);
                            break;
                        case MouseDisasterPawnBatchKind.FamineRefugees:
                            GenerateFamineRefugeePawn(batch);
                            break;
                        case MouseDisasterPawnBatchKind.AirdropMistake:
                            GenerateAirdropEggPawn(batch);
                            break;
                        case MouseDisasterPawnBatchKind.EggBombRetaliation:
                        case MouseDisasterPawnBatchKind.PlagueRevengeWave:
                            GenerateSpecialPayloadPawn(batch);
                            break;
                        default:
                            throw new InvalidOperationException("Unknown pawn batch kind: " + batch.kind);
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
            if (batch.kind == MouseDisasterPawnBatchKind.TraderCaravan)
                EnsureTraderGatheringLord(batch);
            if (batch.pawns.Count > firstNewPawn && !IsPayloadBatch(batch))
                GameComponent_MouseDisasterEventBehavior.Component?.Register(batch.behaviorGroupId,
                    batch.pawns.GetRange(firstNewPawn, batch.pawns.Count - firstNewPawn), apply: batch.kind != MouseDisasterPawnBatchKind.TraderCaravan);
            MouseDisasterTrace.Log("pawn generation slot end; kind=" + batch.kind + "; slot=" + slot +
                "; created=" + (batch.pawns.Count - firstNewPawn) + "; totalPawns=" + batch.pawns.Count +
                "; remaining=" + batch.remainingCount);
        }

        private static void GenerateLargeRefugee(MouseDisasterPawnBatch batch)
        {
            DevelopmentalStage stage;
            PawnKindDef kindDef;
            if (batch.adultsRemaining > 0)
            {
                batch.adultsRemaining--;
                stage = DevelopmentalStage.Adult;
                kindDef = MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult;
            }
            else
            {
                batch.childrenRemaining--;
                stage = DevelopmentalStage.Child;
                kindDef = MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild;
            }

            Pawn pawn = MouseDisasterUtility.GenerateBeggarPawn(kindDef, batch.faction, stage, allowViolenceDisabledTraits: true);
            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 6), batch.map);
            batch.pawns.Add(pawn);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            pawn.jobs?.StartJob(MouseDisasterUtility.CreateGotoJob(batch.map.Center), JobCondition.InterruptForced);
            pawn.health.AddHediff(stage == DevelopmentalStage.Adult
                ? MouseDisasterDefOf.MouseDisaster_LargeRefugeeAdult
                : MouseDisasterDefOf.MouseDisaster_LargeRefugeeChild);
            pawn.mindState?.mentalStateHandler?.Reset();
        }

        private static void GenerateGreatFaminePawn(MouseDisasterPawnBatch batch)
        {
            batch.remainingCount--;
            Pawn pawn = MouseDisasterPhase3Utility.CreateAnyAgeFaminePawn(batch.faction, batch.infectsWithPlague);
            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 7), batch.map);
            batch.pawns.Add(pawn);
            pawn.health.AddHediff(pawn.DevelopmentalStage == DevelopmentalStage.Adult
                ? MouseDisasterDefOf.MouseDisaster_GreatFamineAdult
                : MouseDisasterDefOf.MouseDisaster_GreatFamineChild);
            pawn.mindState?.mentalStateHandler?.Reset();
        }

        private static void GenerateTraderCaravanPawn(MouseDisasterPawnBatch batch, int slot)
        {
            batch.remainingCount--;
            Pawn pawn;
            if (slot == 0)
            {
                pawn = IncidentWorker_RatkinTraderCaravan.GenerateFemaleTraderPawn(batch.faction);
            }
            else if (slot == 1)
            {
                pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_TraderRatkinEscort, batch.faction, DevelopmentalStage.Adult, 0.6f);
            }
            else
            {
                pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, batch.faction, DevelopmentalStage.Baby, 0.5f);
                if (pawn != null)
                {
                    MouseDisasterUtility.SetBiologicalAgeYears(pawn, MouseDisasterUtility.RandomRatEggAgeYears());
                }
            }

            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 6), batch.map);
            batch.pawns.Add(pawn);
            if (slot == 0)
            {
                batch.traderPawn = pawn;
                MouseDisasterUtility.EnsureTradeLeader(pawn, MouseDisasterUtility.ResolveSlaveTraderKind());
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_TraderCaravanTrader);
                if (batch.infectsWithPlague)
                {
                    MouseDisasterPlagueUtility.InfectWithPlague(pawn);
                }
            }
            else if (slot == 1)
            {
                batch.escortPawn = pawn;
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_TraderCaravanEscort);
            }
            else
            {
                MouseDisasterUtility.StripRatEggInventory(pawn);
                IncidentWorker_RatkinTraderCaravan.PrepareChattelChild(pawn, batch.faction);
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_TraderCaravanChild);
            }
        }

        private static void EnsureTraderGatheringLord(MouseDisasterPawnBatch batch)
        {
            // Spawned members can run the ordinary non-colonist exit AI between generation ticks.
            // Also restore a gathering duty for batches saved before this field existed.
            if (batch?.map?.lordManager?.lords == null || batch.pawns == null)
            {
                return;
            }

            if (batch.Complete)
            {
                return;
            }

            if (batch.gatheringLord != null && !IsValidTraderGatheringLord(batch, batch.gatheringLord))
            {
                ReleaseTraderGatheringLord(batch);
            }

            if (batch.gatheringLord == null)
            {
                batch.gatheringLord = batch.map.lordManager.lords.FirstOrDefault(lord => IsValidTraderGatheringLord(batch, lord));
            }

            foreach (Pawn pawn in batch.pawns)
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned || pawn.Map != batch.map) continue;
                if (MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn)) continue;
                Lord currentLord = pawn.GetLord();
                if (!TryDetachTraderCaravanPawn(pawn, batch.gatheringLord, "gathering"))
                {
                    continue;
                }
                if (batch.gatheringLord == null)
                    batch.gatheringLord = LordMaker.MakeNewLord(batch.faction ?? pawn.Faction,
                        new LordJob_DefendPoint(batch.entryCell, wanderRadius: 3f), batch.map);
                if (batch.gatheringLord == null)
                {
                    continue;
                }

                if (currentLord == batch.gatheringLord && batch.gatheringLord.ownedPawns?.Contains(pawn) == true)
                {
                    continue;
                }
                batch.gatheringLord.AddPawn(pawn);
                pawn.jobs?.StopAll();
            }
        }

        private static bool TryDetachTraderCaravanPawn(Pawn pawn, Lord expectedLord, string phase)
        {
            if (pawn == null)
            {
                return false;
            }

            Lord currentLord = pawn.GetLord();
            if (currentLord == null)
            {
                return true;
            }

            if (currentLord == expectedLord && currentLord.ownedPawns?.Contains(pawn) == true)
            {
                return true;
            }

            try
            {
                currentLord.RemovePawn(pawn);
            }
            catch (Exception exception)
            {
                Log.Warning("[MouseDisaster] Could not detach trader caravan pawn " + pawn +
                    " from an existing lord during " + phase + ": " + exception);
                return false;
            }

            if (pawn.GetLord() != null)
            {
                Log.Warning("[MouseDisaster] Trader caravan pawn " + pawn +
                    " remained in another lord during " + phase + ".");
                return false;
            }

            try
            {
                pawn.jobs?.StopAll();
            }
            catch (Exception exception)
            {
                Log.Warning("[MouseDisaster] Could not stop a detached trader caravan pawn: " + exception);
            }

            return true;
        }

        private static bool TryClaimTraderCaravanPawns(IEnumerable<Pawn> pawns, string phase)
        {
            foreach (Pawn pawn in pawns ?? Enumerable.Empty<Pawn>())
            {
                if (pawn == null || MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn))
                {
                    continue;
                }

                if (!TryDetachTraderCaravanPawn(pawn, expectedLord: null, phase))
                {
                    Log.Warning("[MouseDisaster] Could not claim trader caravan pawn " + pawn + " during " + phase + ".");
                    return false;
                }
            }

            return true;
        }

        private static bool TryEnsureTraderGatheringLord(MouseDisasterPawnBatch batch)
        {
            try
            {
                EnsureTraderGatheringLord(batch);
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Could not restore trader gathering duty: " + exception);
                return false;
            }
        }

        private static bool IsValidTraderGatheringLord(MouseDisasterPawnBatch batch, Lord lord)
        {
            return batch != null && batch.faction != null && batch.pawns != null && batch.map?.lordManager?.lords != null && lord != null &&
                   batch.map.lordManager.lords.Contains(lord) && lord.faction == batch.faction &&
                   lord.LordJob is LordJob_DefendPoint && lord.ownedPawns != null &&
                   lord.ownedPawns.Any(pawn => batch.pawns.Contains(pawn)) &&
                   lord.ownedPawns.All(pawn => batch.pawns.Contains(pawn));
        }

        private static void ReleaseTraderGatheringLord(MouseDisasterPawnBatch batch)
        {
            Lord lord = batch?.gatheringLord;
            if (lord == null)
            {
                return;
            }

            try
            {
                List<Pawn> members = batch.pawns?
                    .Where(pawn => pawn != null && pawn.GetLord() == lord)
                    .Distinct()
                    .ToList() ?? new List<Pawn>();
                foreach (Pawn pawn in members)
                {
                    try
                    {
                        lord.RemovePawn(pawn);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[MouseDisaster] Could not release trader gathering pawn " + pawn + ": " + exception);
                        if (pawn.GetLord() == lord)
                        {
                            pawn.lord = null;
                        }
                    }

                    try
                    {
                        pawn.jobs?.StopAll();
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[MouseDisaster] Could not stop a released trader gathering pawn: " + exception);
                    }
                }
            }
            finally
            {
                batch.gatheringLord = null;
            }
        }

        private static string DescribeTraderMember(Pawn pawn)
        {
            return pawn == null ? "null" : pawn.ThingID + "(dead=" + pawn.Dead +
                ", destroyed=" + pawn.Destroyed + ", spawned=" + pawn.Spawned +
                ", playerAffiliated=" + MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn) +
                ", map=" + pawn.Map + ", heldMap=" + pawn.MapHeld + ")";
        }

        private static void TryRestoreTraderCaravanMembers(MouseDisasterPawnBatch batch)
        {
            if (batch?.kind != MouseDisasterPawnBatchKind.TraderCaravan || batch.map == null || batch.pawns == null)
            {
                return;
            }

            // Only the trader and escort are recoverable requirements. Children that are dead,
            // carried, or already traveling remain outside the active trade group.
            foreach (Pawn pawn in new[] { batch.traderPawn, batch.escortPawn })
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed || MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn))
                {
                    continue;
                }

                TryRestoreTraderCaravanMember(batch, pawn);
            }
        }

        private static bool TryRestoreTraderCaravanMember(MouseDisasterPawnBatch batch, Pawn pawn)
        {
            if (pawn.Spawned)
            {
                if (pawn.Map != batch.map)
                {
                    return false;
                }

                return TryDetachTraderCaravanPawn(pawn, expectedLord: null, "member recovery");
            }

            // Do not pull a pawn out of a caravan or a live holder. Those states need their owner to
            // release the pawn first; forcing a map spawn here would corrupt that container.
            if (pawn.Map != null || pawn.MapHeld != null || pawn.ParentHolder != null || pawn.IsCaravanMember())
            {
                return false;
            }

            if (!TryDetachTraderCaravanPawn(pawn, expectedLord: null, "member recovery"))
            {
                return false;
            }

            try
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 6);
                if (!cell.InBounds(batch.map) || !cell.Standable(batch.map))
                {
                    Log.Warning("[MouseDisaster] Could not find a recovery cell for trader caravan pawn " + pawn + ".");
                    return false;
                }

                GenSpawn.Spawn(pawn, cell, batch.map);
                if (!pawn.Spawned || pawn.Map != batch.map)
                {
                    return false;
                }

                if (pawn == batch.traderPawn)
                {
                    MouseDisasterUtility.EnsureTradeLeader(pawn, MouseDisasterUtility.ResolveSlaveTraderKind());
                }

                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
                Log.Message("[MouseDisaster] Restored missing trader caravan pawn " + pawn + " before finalization.");
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("[MouseDisaster] Could not restore missing trader caravan pawn " + pawn + ": " + exception);
                return false;
            }
        }

        private static void FinalizeBatch(MouseDisasterPawnBatch batch)
        {
            ReleaseTraderGatheringLord(batch);
            TryRestoreTraderCaravanMembers(batch);
            if (IsPayloadBatch(batch))
            {
                FinalizePayloadBatch(batch);
                return;
            }

            List<Pawn> pawns = ActivePawns(batch);
            MouseDisasterTrace.Log("pawn batch finalize; kind=" + (batch?.kind.ToString() ?? "unknown") +
                "; generated=" + (batch?.generatedSlots ?? 0) + "; activePawns=" + pawns.Count +
                "; " + MouseDisasterTrace.DescribeMap(batch?.map));
            if (pawns.Count == 0)
            {
                Log.Warning("[MouseDisaster] Staged pawn generation completed without any active pawns for " + batch.kind + ".");
                return;
            }

            switch (batch.kind)
            {
                case MouseDisasterPawnBatchKind.LargeRefugeeWave:
                case MouseDisasterPawnBatchKind.GreatFamine:
                    FinalizeAssaultBatch(batch, pawns);
                    break;
                case MouseDisasterPawnBatchKind.TraderCaravan:
                    if (!FinalizeTraderCaravan(batch, pawns))
                    {
                        SendIncompleteTraderCaravanAway(batch, ActivePawns(batch));
                    }
                    break;
                case MouseDisasterPawnBatchKind.BeggarGroup:
                case MouseDisasterPawnBatchKind.ThiefGroup:
                case MouseDisasterPawnBatchKind.ThiefChildGroup:
                case MouseDisasterPawnBatchKind.WildGroup:
                case MouseDisasterPawnBatchKind.TravelerGroup:
                case MouseDisasterPawnBatchKind.FamineRefugees:
                case MouseDisasterPawnBatchKind.SiegeBeggarGroup:
                case MouseDisasterPawnBatchKind.StrongSiegeGroup:
                    FinalizeMultiFrameGroup(batch, pawns);
                    break;
            }
            try
            {
                GameComponent_MouseDisasterEventBehavior.Component?.Register(batch.behaviorGroupId, ActivePawns(batch));
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Could not register finalized pawn batch " + batch.kind + ": " + exception);
            }
        }

        private static void TryFinalizeBatch(MouseDisasterPawnBatch batch)
        {
            try
            {
                FinalizeBatch(batch);
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Could not finalize staged pawn generation for " + (batch?.kind.ToString() ?? "unknown") + ": " + exception);
                try
                {
                    if (IsPayloadBatch(batch))
                    {
                        FinalizePayloadBatch(batch);
                        return;
                    }

                    List<Pawn> pawns = ActivePawns(batch);
                    if (pawns.Count == 0 || !IsBatchMapAvailable(batch))
                    {
                        return;
                    }

                    if (batch.kind == MouseDisasterPawnBatchKind.TraderCaravan)
                    {
                        SendIncompleteTraderCaravanAway(batch, pawns);
                    }
                    else
                    {
                        TruncateBatch(batch, "an exception interrupted finalization");
                    }
                }
                catch (Exception recoveryException)
                {
                    Log.Error("[MouseDisaster] Could not recover after staged pawn finalization failed: " + recoveryException);
                }
            }
        }

        private static void TruncateBatch(MouseDisasterPawnBatch batch, string reason)
        {
            ReleaseTraderGatheringLord(batch);
            TryRestoreTraderCaravanMembers(batch);
            Log.Warning("[MouseDisaster] Truncating staged pawn generation for " + batch.kind + ": " + reason + ".");
            if (IsPayloadBatch(batch))
            {
                FinalizePayloadBatch(batch);
                return;
            }

            List<Pawn> pawns = ActivePawns(batch);
            if (pawns.Count == 0 || !IsBatchMapAvailable(batch))
            {
                return;
            }

            if (batch.kind == MouseDisasterPawnBatchKind.TraderCaravan)
            {
                if (!HasRequiredState(batch) || !FinalizeTraderCaravan(batch, pawns))
                {
                    SendIncompleteTraderCaravanAway(batch, ActivePawns(batch));
                }
                return;
            }

            if (IsMultiFrameVisitorBatch(batch))
            {
                FinalizeMultiFrameGroup(batch, pawns);
                return;
            }

            GameComponent_MouseDisasterEventBehavior.Component?.Register(batch.behaviorGroupId, pawns);

            if (HasRequiredState(batch))
            {
                SendBatchLetter(batch, pawns);
            }
        }

        private static void TryTruncateBatch(MouseDisasterPawnBatch batch, string reason)
        {
            try
            {
                TruncateBatch(batch, reason);
                if (batch != null)
                    GameComponent_MouseDisasterEventBehavior.Component?.Register(batch.behaviorGroupId, ActivePawns(batch));
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Could not finish truncating staged pawn generation for " + (batch?.kind.ToString() ?? "unknown") + ": " + exception);
            }
        }

        private static void FinalizeAssaultBatch(MouseDisasterPawnBatch batch, List<Pawn> pawns)
        {
            GameComponent_MouseDisasterEventBehavior.Component?.Register(batch.behaviorGroupId, pawns);
            SendBatchLetter(batch, pawns);
        }

        private static bool FinalizeTraderCaravan(MouseDisasterPawnBatch batch, List<Pawn> pawns)
        {
            if (batch == null || batch.map == null || batch.pawns == null || pawns == null)
            {
                return false;
            }

            ReleaseTraderGatheringLord(batch);
            TryRestoreTraderCaravanMembers(batch);
            pawns = ActivePawns(batch);
            Pawn traderPawn = batch.traderPawn;
            Pawn escortPawn = batch.escortPawn;
            List<Pawn> children = batch.pawns?
                .Where(pawn => pawn != traderPawn && pawn != escortPawn && pawns.Contains(pawn))
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (traderPawn == null || escortPawn == null || children.Count == 0 || !pawns.Contains(traderPawn) || !pawns.Contains(escortPawn))
            {
                Log.Warning("[MouseDisaster] Trader caravan generation did not produce its required members. " +
                    "trader=" + DescribeTraderMember(traderPawn) + "; escort=" + DescribeTraderMember(escortPawn) +
                    "; activeChildren=" + children.Count + "; active=" + pawns.Count +
                    "; generated=" + batch.pawns.Count + "; slots=" + batch.generatedSlots +
                    "; remaining=" + batch.remainingCount + "; map=" + batch.map +
                    (children.Count == 0 ? "; children=" + string.Join(", ", batch.pawns
                        .Where(pawn => pawn != traderPawn && pawn != escortPawn).Select(DescribeTraderMember)) : ""));
                return false;
            }

            if (!TryClaimTraderCaravanPawns(pawns, "trade finalization"))
            {
                return false;
            }

            try
            {
                MouseDisasterUtility.MarkChildExchangeMoodChildren(children);
                List<Pawn> childrenWithMother = children
                    .InRandomOrder()
                    .Take(MouseDisasterGeneRestorePolicy.ResolveTraderCaravanChildrenToLinkCount(children.Count))
                    .ToList();
                MouseDisasterUtility.LinkIncidentParentToChildren(traderPawn, childrenWithMother);
                MouseDisasterUtility.TryStartLeadYourPetRelatedAdultLeashes(pawns);
                IncidentWorker_RatkinTraderCaravan.FillTraderInventory(traderPawn);
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Trader caravan preparation failed: " + exception);
                return false;
            }

            IntVec3 exitCell = batch.map.Center;
            try
            {
                if (!RCellFinder.TryFindRandomSpotJustOutsideColony(traderPawn.Position, batch.map, traderPawn, out exitCell))
                {
                    exitCell = batch.map.Center;
                }
            }
            catch (Exception exception)
            {
                Log.Warning("[MouseDisaster] Could not find a trader caravan exit cell; using the map center: " + exception);
            }

            Faction tradeFaction = batch.faction ?? traderPawn.Faction;
            if (tradeFaction == null)
            {
                return false;
            }

            Lord tradeLord;
            try
            {
                tradeLord = LordMaker.MakeNewLord(tradeFaction, new LordJob_MouseDisasterTrade(tradeFaction, exitCell), batch.map, pawns);
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Could not create trader caravan lord: " + exception);
                return false;
            }

            if (tradeLord == null || pawns.Any(pawn => pawn.GetLord() != tradeLord))
            {
                Log.Warning("[MouseDisaster] Trader caravan lord did not acquire every generated member; sending the group away.");
                return false;
            }

            try
            {
                MouseDisasterUtility.TryAssignLeadYourPetTravelMouseEggs(tradeLord);
            }
            catch (Exception exception)
            {
                Log.Warning("[MouseDisaster] Lead Your Pet travel assignment failed; trade remains available: " + exception);
            }

            try
            {
                MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(batch.map, tradeFaction);
                MouseDisasterVisitorUtility.RegisterVisitors(pawns);
                if (!MouseDisasterVisitorUtility.SendVisitorChoiceLetter(batch.incidentDef, batch.parms, batch.map, pawns))
                {
                    IncidentWorker.SendIncidentLetter(batch.incidentDef.letterLabel, batch.incidentDef.letterText, batch.incidentDef.letterDef, batch.parms, pawns, batch.incidentDef);
                }
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Trader caravan notification failed after the trade Lord was created: " + exception);
            }
            return true;
        }

        private static void SendIncompleteTraderCaravanAway(MouseDisasterPawnBatch batch, List<Pawn> pawns)
        {
            if (batch?.map == null || pawns == null)
            {
                return;
            }

            ReleaseTraderGatheringLord(batch);
            Faction faction = batch.faction ?? pawns.FirstOrDefault(pawn => pawn?.Faction != null)?.Faction;
            try
            {
                MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(batch.map, faction);
                MouseDisasterUtility.MakeTravelAndExitLord(batch.map, pawns, batch.map.Center);
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Could not send incomplete trader caravan away: " + exception);
            }
        }

        private static void SendBatchLetter(MouseDisasterPawnBatch batch, List<Pawn> pawns)
        {
            IncidentWorker.SendIncidentLetter(batch.incidentDef.letterLabel, batch.incidentDef.letterText, batch.incidentDef.letterDef, batch.parms, pawns, batch.incidentDef);
        }

        private static List<Pawn> ActivePawns(MouseDisasterPawnBatch batch)
        {
            if (batch?.pawns == null || batch.map == null)
            {
                return new List<Pawn>();
            }

            return batch.pawns
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned && pawn.Map == batch.map)
                .Where(pawn => batch.kind != MouseDisasterPawnBatchKind.TraderCaravan || !MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn))
                .Distinct()
                .ToList();
        }
    }
}
