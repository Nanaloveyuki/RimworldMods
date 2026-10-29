using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Job driver for a child building dirt castles outside.
    /// The child finds an outdoor dirt/stone spot, spends ~3 hours playing,
    /// and (if <see cref="TPE_Settings.showDirtCastles"/> is ON) leaves behind a
    /// dirt castle building — but ONLY when the session fully completes (the job
    /// ends with <see cref="JobCondition.Succeeded"/>, never when interrupted),
    /// and only on a cell where the castle wouldn't override a building or a crop
    /// (see <see cref="DirtCastleUtility"/>).
    /// </summary>
    public class JobDriver_ChildBuildDirtCastles : JobDriver
    {
        /// <summary>How many ticks the dirt-castle session lasts (~3 hours).</summary>
        const int PlayDurationTicks = 7500;

        /// <summary>Play need gained per tick while building.</summary>
        const float PlayNeedGainPerTick = 0.3f / PlayDurationTicks;

        /// <summary>How often the child changes facing direction ("builds").</summary>
        const int FaceChangeIntervalTicks = 250;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (job.targetA.IsValid)
                return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Leave the castle ONLY if the session fully completes. This is a
            // DRIVER-level finish action (JobDriver.AddFinishAction(Action<JobCondition>)),
            // which — unlike a toil-level finish action — is told HOW the job ended.
            // JobCondition.Succeeded is the engine's "finished normally" result:
            //   • the play toil's timer ran out (all toils done),
            //   • PlayUtility.PlayTickCheckEnd ended it because the child's Play
            //     need is satisfied (the child is done playing), or
            //   • the job's own expiryInterval fired (the engine also reports that
            //     as Succeeded; see Architecture.md §4.2a).
            // Every interruption — hunger/sleep/bedtime taking over, being downed,
            // dying or despawning, a mental break, being drafted/ordered elsewhere —
            // is InterruptForced/InterruptOptional/Incompletable/Errored and leaves
            // NO castle. Registered once per driver instance (MakeNewToils runs once
            // in SetupToils), before any toil, so it covers the whole job.
            AddFinishAction(delegate(JobCondition condition)
            {
                if (condition == JobCondition.Succeeded)
                    SpawnDirtCastle(pawn);
            });

            // Step 1: walk to the outdoor spot
            if (job.targetA.IsValid)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            }

            // Step 2: play / build dirt castles for ~3 hours
            Toil playToil = ToilMaker.MakeToil("ChildBuildDirtCastles");
            playToil.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceCell(
                    pawn.Position + GenRadial.RadialPattern[Rand.Range(0, 8)]);
            };
            playToil.tickAction = () =>
            {
                // Fill play need and auto-end when satisfied
                PlayUtility.PlayTickCheckEnd(pawn, PlayNeedGainPerTick);

                // Periodically face a new direction to simulate "building"
                if (pawn.IsHashIntervalTick(FaceChangeIntervalTicks))
                {
                    pawn.rotationTracker.FaceCell(
                        pawn.Position + GenRadial.RadialPattern[Rand.Range(0, 8)]);
                }
            };
            // (No toil-level finish action here any more — the castle is handled by the
            // driver-level, JobCondition-aware action registered at the top.)
            playToil.defaultCompleteMode = ToilCompleteMode.Delay;
            playToil.defaultDuration = PlayDurationTicks;
            yield return playToil;
        }

        /// <summary>
        /// Spawns a dirt castle Building where the child is standing. Only ever
        /// called when the job ended with <see cref="JobCondition.Succeeded"/> (see
        /// the finish action in <see cref="MakeNewToils"/>); on top of that it
        /// requires ALL of:
        ///  1. The "show dirt castles" setting is ON (otherwise the child still
        ///     played and gained Play, but no castle visual is left behind).
        ///  2. The child is still spawned on a map (null-map safety net so
        ///     <c>InBounds(null)</c> can never throw). A Succeeded end can't
        ///     coincide with death/despawn — those stop the job with
        ///     InterruptForced — so this is defence in depth only.
        ///  3. <see cref="DirtCastleUtility.CanSpawnCastleAt"/> passes RIGHT NOW:
        ///     the cell is still natural open-air ground and holds no building
        ///     (incl. frames / other castles), no colony-sown crop and isn't
        ///     growing-zone land. Re-checked here, not just when the cell was
        ///     picked, because the world can change during the ~3h session.
        /// If any check fails the castle is simply skipped — it NEVER spawns over
        /// a building or crop. The guard runs BEFORE <c>ThingMaker.MakeThing</c> so
        /// a refused spawn allocates nothing and consumes no Thing ID. The castle
        /// shows the child's name in its label.
        /// </summary>
        static void SpawnDirtCastle(Pawn builder)
        {
            // 1. Config gate — cheapest check first.
            if (!DirtCastleUtility.CastlesEnabled)
                return;

            // 2. Builder must still be on a map (null-map safety net so
            //    InBounds(null) can never throw).
            if (builder == null || !builder.Spawned)
                return;
            Map map = builder.Map;
            IntVec3 cell = builder.Position;

            // 3. Authoritative "won't override anything" placement guard.
            if (!DirtCastleUtility.CanSpawnCastleAt(cell, map))
                return;

            Building_DirtCastle? castle = ThingMaker.MakeThing(TPE_ThingDefOf.TPE_DirtCastle) as Building_DirtCastle;
            if (castle == null)
                return;

            // LabelShort is null-safe for unnamed pawns (falls back to the
            // race label); the old Name.ToStringShort NREd when Name was null.
            castle.builderName = builder.LabelShort;

            // WipeMode.Vanish is the engine default and is kept explicit on
            // purpose. The guard above already proved the cell holds no building
            // or crop, so the only things this spawn can still wipe are trivial
            // wild plants (grass) and filth — exactly what any vanilla building
            // placed on open ground replaces, and explicitly OK per the user.
            GenSpawn.Spawn(castle, cell, map, WipeMode.Vanish);
        }
    }
}
