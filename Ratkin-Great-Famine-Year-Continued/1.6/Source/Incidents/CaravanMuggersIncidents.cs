using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{

    public abstract class IncidentWorker_MouseDisasterCaravanMuggersBase : IncidentWorker
    {
        protected abstract bool InfectsWithPlague { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Caravan caravan &&
                   caravan.Visibility >= 0.5f &&
                   CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(caravan.Tile) &&
                   base.CanFireNowSub(parms) &&
                   MouseDisasterPhase3Utility.ResolveVisitorFaction() != null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Caravan caravan = (Caravan)parms.target;
            Faction faction = MouseDisasterPhase3Utility.ResolveVisitorFaction();
            List<ThingCount> demands = MouseDisasterPhase3CaravanUtility.GenerateCaravanDemands(caravan, caravan.PlayerWealthForStoryteller * Rand.Range(0.03f, 0.08f));
            if (demands.NullOrEmpty())
            {
                return false;
            }

            List<Pawn> attackers = MouseDisasterPhase3CaravanUtility.CreateCaravanMuggerParty(faction, parms.points, InfectsWithPlague);
            if (attackers.Count == 0)
            {
                return false;
            }
            int behaviorGroupId = MouseDisasterEventExecution.Current?.groupId ?? 0;
            GameComponent_MouseDisasterEventBehavior.Component?.Register(behaviorGroupId, attackers, apply: false);

            CameraJumper.TryJumpAndSelect(caravan);
            DiaNode root = new DiaNode("MouseDisaster_UI_CaravanMuggersDemand".Translate(faction.Name, GenLabel.ThingsLabel(demands)).Resolve());

            DiaOption give = new DiaOption("MouseDisaster_UI_GiveSupplies".Translate().Resolve());
            give.action = delegate
            {
                MouseDisasterPhase3CaravanUtility.TakeDemandFromCaravan(caravan, demands);
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordCaravanNarrative(true);
                for (int i = 0; i < attackers.Count; i++)
                {
                    Find.WorldPawns.PassToWorld(attackers[i], PawnDiscardDecideMode.Discard);
                }
            };
            give.resolveTree = true;
            root.options.Add(give);

            DiaOption fight = new DiaOption("MouseDisaster_UI_RefuseAndFight".Translate().Resolve());
            fight.action = delegate
            {
                TaleRecorder.RecordTale(TaleDefOf.CaravanAmbushedByHumanlike, caravan.RandomOwner());
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordCaravanNarrative(false);
                LongEventHandler.QueueLongEvent(delegate
                {
                    Map map = CaravanIncidentUtility.SetupCaravanAttackMap(caravan, attackers, sendLetterIfRelatedPawns: true);
                    LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: true, canTimeoutOrFlee: false), map, attackers);
                    GameComponent_MouseDisasterEventBehavior.Component?.Register(behaviorGroupId, attackers);
                    Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                    CameraJumper.TryJump(attackers[0]);
                }, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
            };
            fight.resolveTree = true;
            root.options.Add(fight);

            TaggedString title = def.letterLabel.NullOrEmpty() ? "MouseDisaster_UI_CaravanMuggersLabel".Translate().Resolve() : def.letterLabel;
            Find.WindowStack.Add(new Dialog_NodeTreeWithFactionInfo(root, faction, delayInteractivity: true, radioMode: false, title));
            Find.Archive.Add(new ArchivedDialog(root.text, title, faction));
            return true;
        }
    }

    public class IncidentWorker_MouseDisasterCaravanMuggers : IncidentWorker_MouseDisasterCaravanMuggersBase
    {
        protected override bool InfectsWithPlague => false;
    }

    public class IncidentWorker_MouseDisasterPlagueCaravanMuggers : IncidentWorker_MouseDisasterCaravanMuggersBase
    {
        protected override bool InfectsWithPlague => true;
    }
}
