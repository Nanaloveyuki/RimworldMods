using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class SitePartWorker_MouseDisasterRecords : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyRecordSiteGenerated(map);
        }
    }

    public class Building_MouseDisasterRecordCache : Building
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            var narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
            if (narrative == null || !narrative.CanResolveRelic(this)) yield break;
            for (int choice = 1; choice <= 5; choice++)
            {
                if ((choice == 3 || choice == 4) && !narrative.HasRelicWitness) continue;
                int captured = choice;
                yield return new Command_Action
                {
                    icon = choice == 1 ? TexCommand.Install : choice == 2 ? TexCommand.ForbidOn :
                        choice == 3 ? TexCommand.SelectCarriedThing : choice == 4 ? TexCommand.OpenLinkedQuestTex : TexCommand.Attack,
                    defaultLabel = ("MouseDisaster_Story_RelicChoice" + choice).Translate(),
                    defaultDesc = "MouseDisaster_Story_RelicCost".Translate(choice == 1 ? narrative.NarrativeReward(200) : choice == 3 ? narrative.NarrativeReward(100) : 0),
                    action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        ("MouseDisaster_Story_RelicChoice" + captured).Translate() + "\n" + "MouseDisaster_Story_RelicCost".Translate(captured == 1 ? narrative.NarrativeReward(200) : captured == 3 ? narrative.NarrativeReward(100) : 0),
                        () => narrative.ResolveRelic(this, captured)))
                };
            }
        }
    }
}
