using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Adds right-click float menu options when the player right-clicks a toy
    /// with a toddler selected: "Play with [toy]" and "Carry [toy]".
    /// Only shows if the selected pawn is a walk-capable toddler.
    /// </summary>
    public class FloatMenuOptionProvider_ToddlerToys : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => false;

        public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
        {
            if (!base.SelectedPawnValid(pawn, context))
                return false;
            // FIX G1: Only humanlike toddlers can get toy float menu options.
            // Animals should never show "Play with [toy]" or "Carry [toy]" options.
            if (!pawn.RaceProps.Humanlike)
                return false;
            return ToddlerUtility.IsToddler(pawn);
        }

        public override bool TargetThingValid(Thing thing, FloatMenuContext context)
        {
            if (!base.TargetThingValid(thing, context))
                return false;
            return IsToy(thing);
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn toddler = context.FirstSelectedPawn;

            // Play with toy
            var playJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_PlayWithToy, clickedThing);
            playJob.playerForced = true;
            yield return new FloatMenuOption("Play with " + clickedThing.LabelShort, delegate
            {
                toddler.jobs.TryTakeOrderedJob(playJob, JobTag.Misc);
            }, MenuOptionPriority.Default, null, clickedThing);

            // Carry toy
            var carryJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_CarryToy, clickedThing);
            carryJob.playerForced = true;
            carryJob.count = 1;
            yield return new FloatMenuOption("Carry " + clickedThing.LabelShort, delegate
            {
                toddler.jobs.TryTakeOrderedJob(carryJob, JobTag.Misc);
            }, MenuOptionPriority.Default, null, clickedThing);
        }

        static bool IsToy(Thing thing)
        {
            if (thing?.def == null) return false;
            string defName = thing.def.defName;
            return defName.StartsWith("TPE_Plush") || defName == "TPE_ToySword";
        }
    }
}
