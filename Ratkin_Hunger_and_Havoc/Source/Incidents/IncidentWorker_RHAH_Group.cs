using HungerAndHavoc.Api;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal abstract class IncidentWorker_RHAH_Group : IncidentWorker
    {
        protected abstract string DisplayId { get; }
        protected abstract RHAH_PawnRole Role { get; }
        protected virtual bool CarriesPlague => false;
        protected virtual int PawnCount => 1;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = ResolveMap(parms);
            return map != null && RHAH_RuntimeAllows() && TemperatureAllows(map) && FindAnySpawnCell(map);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = ResolveMap(parms);
            if (map == null || !CanFireNowSub(parms))
            {
                return false;
            }

            IntVec3 cell;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return false;
            }

            int tick = Core.RHAH_Runtime.NextBatchId(map);
            return SubmitFacts(new RHAH_IncidentContext
            {
                DisplayId = DisplayId,
                SpawnBatchId = tick,
                RelationshipGroupId = tick,
                Role = Role,
                Attitude = RHAH_IncidentArrival.For(DisplayId),
                CarriesPlague = CarriesPlague,
                Map = map,
                SpawnCell = cell,
                PawnCount = RHAH_IncidentScale.Count(DisplayId, parms.points, EventCap(), false),
                Points = parms.points
            });
        }

        static Map ResolveMap(IncidentParms parms)
        {
            return Core.RHAH_MapResolver.Resolve(parms?.target as Map);
        }


        protected abstract bool SubmitFacts(RHAH_IncidentContext context);

        static bool FindAnySpawnCell(Map map)
        {
            IntVec3 cell;
            return RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal, false, null);
        }

        static int EventCap()
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            return settings == null ? RHAH_IncidentScale.DefaultEventPawns : settings.maxEventPawns;
        }
        bool TemperatureAllows(Map map)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            float minimum = settings == null ? -35f : settings.minimumEventTemperature;
            float maximum = settings == null ? 70f : settings.maximumEventTemperature;
            float temperature = map.mapTemperature == null ? 0f : map.mapTemperature.OutdoorTemp;
            return HungerAndHavoc.Pawn.RHAH_VisitorRules.TemperatureAllows(temperature, minimum, maximum, DisplayId == "I-012");
        }
        static bool RHAH_RuntimeAllows()
        {
            return Core.RHAH_Runtime.AllowsNewContent;
        }
    }

    internal abstract class IncidentWorker_RHAH_OriginalGroup : IncidentWorker_RHAH_Group
    {
        protected override bool SubmitFacts(RHAH_IncidentContext context) => RHAH_IncidentFacts.Submit(context);
    }

    internal sealed class IncidentWorker_LargeRefugeeWave : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-001";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Refugee;
        protected override int PawnCount => 3;
    }

    internal sealed class IncidentWorker_AbandonedRatkinChildren : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-002";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.BeggarChild;
        protected override int PawnCount => 2;
    }

    internal sealed class IncidentWorker_ShatteredMother : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-003";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Mother;
    }

    internal sealed class IncidentWorker_BeggarFamily : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-004";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Refugee;
        protected override int PawnCount => 3;
    }

    internal sealed class IncidentWorker_BeggarGroup : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-005";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Refugee;
        protected override int PawnCount => 4;
    }

    internal sealed class IncidentWorker_ThiefRatkinGroup : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-006";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Thief;
        protected override int PawnCount => 3;
    }

    internal sealed class IncidentWorker_ThiefRatkinChildGroup : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-007";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.ThiefChild;
        protected override int PawnCount => 2;
    }

    internal sealed class IncidentWorker_WildRatkinWandersIn : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-008";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Wild;
    }

    internal sealed class IncidentWorker_WildRatkinChildWandersIn : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-009";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.WildChild;
    }

    internal sealed class IncidentWorker_WildRatkinGroupWandersIn : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-010";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Wild;
        protected override int PawnCount => 3;
    }

    internal sealed class IncidentWorker_FamineRefugees : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-011";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Refugee;
        protected override int PawnCount => 4;
    }

    internal sealed class IncidentWorker_RatkinTraderCaravan : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-012";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Trader;
        protected override int PawnCount => 3;
    }

    internal sealed class IncidentWorker_ChildExchange : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-013";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.RatkinYoung;
    }

    internal sealed class IncidentWorker_BeggarSiege : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-014";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Siege;
        protected override int PawnCount => 5;
    }
}
