using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Def class for a play desire — the play-system equivalent of
    /// <see cref="LearningDesireDef"/>.
    ///
    /// Each desire defines a specific activity a child (3–13) wants to do.
    /// Every 8 hours a child receives up to 2 active desires, chosen from
    /// the pool of desires their age group is eligible for.
    /// </summary>
    public class PlayDesireDef : Def
    {
        /// <summary>
        /// Weight used when randomly selecting this desire for a child.
        /// Higher values make the desire more likely to be picked.
        /// </summary>
        public float selectionWeight = 1f;

        /// <summary>
        /// Path to the 2×2 icon shown on the play-need bar for this desire.
        /// </summary>
        [NoTranslate]
        public string iconPath = string.Empty;

        /// <summary>
        /// The worker class that decides whether the desire can be fulfilled
        /// and generates the <see cref="Job"/> for it.
        /// </summary>
        public Type workerClass = typeof(PlayDesireGiver);

        /// <summary>
        /// The job definition that will be given to the pawn when this desire
        /// is selected.
        /// </summary>
        public JobDef jobDef;

        /// <summary>
        /// Minimum biological age (inclusive) for this desire. -1 means no minimum.
        /// </summary>
        public int minAge = -1;

        /// <summary>
        /// Maximum biological age (inclusive) for this desire. -1 means no maximum.
        /// </summary>
        public int maxAge = -1;

        /// <summary>
        /// How much of the play need is filled per tick while the child is
        /// performing this activity. If left at 0, the job driver must handle
        /// gain itself.
        /// </summary>
        public float playNeedGainPerTick;

        /// <summary>
        /// Optional: the vanilla Biotech <see cref="LearningDesireDef"/> that
        /// represents "the same activity" as this play desire (e.g. our
        /// TPE_FloorDrawing play desire vs. vanilla's own Floordrawing learning
        /// desire). When set, this desire and its Learning-side counterpart are
        /// mutually exclusive for a given child: the child will never have both
        /// active as a play desire AND a learning desire at the same time —
        /// enforced from BOTH directions (see
        /// <see cref="Pawn_PlayTracker.AddNewPlayDesire"/> for the play-excludes-
        /// learning direction, and
        /// <c>Patch_Pawn_LearningTracker_AddNewLearningDesire_ExcludeActivePlayDesires</c>
        /// in HarmonyPatches.cs for the learning-excludes-play direction).
        /// Left null (the default) for desires with no vanilla Learning
        /// equivalent (e.g. Playing With Plush Toys, Watching Television,
        /// Bubble Popping) — those are never restricted by this system.
        /// Only set via XML with a <c>MayRequire="Ludeon.RimWorld.Biotech"</c>
        /// guard on the element, since LearningDesireDef only exists when the
        /// Biotech DLC is active — see PlayDesires.xml.
        /// </summary>
        public LearningDesireDef conflictingLearningDesire;

        /// <summary>Cached icon texture.</summary>
        private Texture2D icon;

        /// <summary>Lazily-created worker instance.</summary>
        private PlayDesireGiver workerInt;

        /// <summary>
        /// Returns the icon texture for this desire, loading it from
        /// <see cref="iconPath"/> if necessary.
        /// </summary>
        public Texture2D Icon
        {
            get
            {
                if (icon == null && !string.IsNullOrEmpty(iconPath))
                {
                    icon = ContentFinder<Texture2D>.Get(iconPath);
                }
                return icon;
            }
        }

        /// <summary>
        /// Returns the <see cref="PlayDesireGiver"/> worker for this def,
        /// creating it lazily on first access.
        /// </summary>
        public PlayDesireGiver Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (PlayDesireGiver)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        /// <summary>
        /// Validates XML configuration. Every desire must have a <c>jobDef</c>.
        /// </summary>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;

            if (jobDef == null)
                yield return $"PlayDesireDef {defName} is missing a jobDef.";
        }
    }
}
