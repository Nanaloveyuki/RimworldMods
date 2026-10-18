using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    public static class DebugActions_MouseDisaster
    {
        [DebugAction("鼠灾事件", "鼠灾", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static DebugActionNode MouseDisaster_DebugCategory_MouseDisaster()
        {
            return CreateCategoryNode(MouseDisasterIncidentCategory.MouseDisaster);
        }

        [DebugAction("鼠灾事件", "鼠疫", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static DebugActionNode MouseDisaster_DebugCategory_Plague()
        {
            return CreateCategoryNode(MouseDisasterIncidentCategory.Plague);
        }

        [DebugAction("鼠灾事件", "强制遣散可移动非幼儿", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MouseDisaster_ForceDismissMobileNonInfants()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("MouseDisaster_UI_NoCurrentMap".Translate().Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            int dismissed = MouseDisasterUtility.ForceDismissMobileNonInfantEventPawns(map);
            Messages.Message(
                "MouseDisaster_UI_ForceDismissedMobileNonInfants".Translate(dismissed).Resolve(),
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }

        private static DebugActionNode CreateCategoryNode(MouseDisasterIncidentCategory category)
        {
            DebugActionNode root = new DebugActionNode();
            for (int i = 0; i < MouseDisasterIncidentCatalog.AllEntries.Count; i++)
            {
                MouseDisasterIncidentEntry entry = MouseDisasterIncidentCatalog.AllEntries[i];
                if (entry.Category != category)
                {
                    continue;
                }

                root.AddChild(new DebugActionNode(entry.DisplayLabel, DebugActionType.Action, delegate
                {
                    TryExecuteCatalogIncident(entry);
                }));
            }

            return root;
        }

        private static void TryExecuteCatalogIncident(MouseDisasterIncidentEntry entry)
        {
            if (!MouseDisasterRuntime.AllowsNewContent)
            {
                Messages.Message("MouseDisaster_Settings_NewContentDisabledDebugMessage".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (entry == null)
            {
                return;
            }

            switch (entry.TargetKind)
            {
                case MouseDisasterIncidentTargetKind.Caravan:
                    TryExecuteCaravanIncident(entry.DefName, entry.DebugPoints);
                    break;
                default:
                    TryExecuteMapIncident(entry.DefName, entry.DebugPoints);
                    break;
            }
        }

        private static void TryExecuteMapIncident(string defName, float points)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("MouseDisaster_UI_NoCurrentMap".Translate().Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            IncidentDef incident = IncidentDef.Named(defName);
            if (incident == null)
            {
                Messages.Message("MouseDisaster_UI_IncidentDefMissing".Translate().Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!MouseDisasterIncidentCatalog.ShouldAllowDebugTrigger(defName, MouseDisasterMod.Settings?.disabledIncidentDefNames))
            {
                Messages.Message("MouseDisaster_UI_IncidentDisabled".Translate().Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.points = points;
            incident.Worker.TryExecute(parms);
        }

        private static void TryExecuteCaravanIncident(string defName, float points)
        {
            Caravan caravan = Find.WorldObjects?.Caravans?.FirstOrDefault(item => item != null && item.IsPlayerControlled);
            if (caravan == null)
            {
                Messages.Message("MouseDisaster_UI_NoPlayerCaravan".Translate().Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            IncidentDef incident = IncidentDef.Named(defName);
            if (incident == null)
            {
                Messages.Message("MouseDisaster_UI_IncidentDefMissing".Translate().Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!MouseDisasterIncidentCatalog.ShouldAllowDebugTrigger(defName, MouseDisasterMod.Settings?.disabledIncidentDefNames))
            {
                Messages.Message("MouseDisaster_UI_IncidentDisabled".Translate().Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, caravan);
            parms.points = points;
            incident.Worker.TryExecute(parms);
        }
    }
}
