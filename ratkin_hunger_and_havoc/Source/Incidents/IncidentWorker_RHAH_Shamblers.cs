using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using Oxygendsl.Mod.Events;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_RHAH_Shamblers : IncidentWorker
    {
        string DisplayId => RHAH_IncidentCatalog.GetByDefName(def.defName).DisplayId;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            if (!ModsConfig.AnomalyActive || !RHAH_Runtime.AllowsNewContent || map?.IsPlayerHome != true ||
                RHAH_Mod.Settings?.IsIncidentEnabled(DisplayId) == false || Faction.OfEntities == null) return false;
            if (RHAH_Shamblers.IsChild(DisplayId) &&
                (!Find.Storyteller.difficulty.ChildrenAllowed || !Find.Storyteller.difficulty.childShamblersAllowed)) return false;
            return DisplayId != RHAH_Shamblers.HordeId || (OxygenEventChainHost.Runtime != null &&
                RHAH_ShamblerReinforcements.DueTick(Find.TickManager.TicksGame) >= 0);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            int count = RHAH_Shamblers.Count(DisplayId, parms.points,
                RHAH_Mod.Settings?.maxEventPawns ?? RHAH_IncidentScale.DefaultEventPawns);
            if (!RHAH_Shamblers.TrySpawn(map, DisplayId, count, out List<Verse.Pawn> pawns)) return false;
            if (DisplayId == RHAH_Shamblers.HordeId)
            {
                int tick = Find.TickManager.TicksGame;
                if (RHAH_ShamblerReinforcements.Schedule(map, pawns.Count, tick) <= 0)
                    throw new InvalidOperationException("ODF rejected the ratkin shambler reinforcement check after arrival.");
            }
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(DisplayId);
            string textKey = RHAH_Shamblers.IsChild(DisplayId) ? "RHAH_Incident_ShamblerChild_Text"
                : DisplayId == RHAH_Shamblers.HordeId ? "RHAH_Incident_ShamblerHorde_Text" : "RHAH_Incident_ShamblerGroup_Text";
            Find.LetterStack.ReceiveLetter(entry.LabelKey.Translate(), textKey.Translate(),
                DisplayId == RHAH_Shamblers.HordeId ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent, new LookTargets(pawns));
            return true;
        }
    }
}
