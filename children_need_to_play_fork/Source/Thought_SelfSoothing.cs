using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Dynamic mood thought for the Self Soothing toddler play activity.
    /// The description is built at read-time and references:
    ///   <list type="bullet">
    ///     <item>The adult the toddler has the highest opinion of (stored in <see cref="Thought_Memory.otherPawn"/>).</item>
    ///     <item>Whether that adult is a parent → replaced with "Mommy" or "Daddy".</item>
    ///     <item>The toddler's gender → "girl" or "boy" (positive outcome only).</item>
    ///   </list>
    ///
    /// Vanilla comparison:
    ///   - Extends <see cref="Thought_Memory"/> like most vanilla memory thoughts.
    ///   - Overrides <see cref="Thought.Description"/> like
    ///     <see cref="Thought_MemorySocial"/> and other dynamic thoughts.
    ///   - Unlike vanilla social thoughts, the text is fully custom-built in C#
    ///     rather than using the standard <c>Formatted</c> pipeline, because we
    ///     need parent-detection logic ("Mommy"/"Daddy") and gendered nouns.
    /// </summary>
    public class Thought_SelfSoothing : Thought_Memory
    {
        /// <summary>
        /// True = positive outcome ("I calmed my self down").
        /// False = negative outcome ("I'm starting to feel really bad!!").
        /// </summary>
        public bool isPositive;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isPositive, "isPositive", false);
        }

        /// <summary>
        /// Dynamically builds the thought description based on the stored
        /// <see cref="otherPawn"/> and <see cref="isPositive"/> flag.
        /// </summary>
        public override string Description
        {
            get
            {
                string adultName = GetAdultDisplayName();
                string genderNoun = pawn.gender switch
                {
                    Gender.Female => "girl",
                    Gender.Male => "boy",
                    _ => "kid"
                };

                string description = isPositive
                    ? $"I was having a really big big feeling so i did what {adultName} always says to do and i was able to calm my self down all on my own! I feel like such a big {genderNoun}!"
                    : $"I was feeling really really worried but there wasn't anyone around that I know! So I tried to do what {adultName} always says to do but it didnt work and now i feel worse! I want {adultName} to come give me a hug!!!";

                // BUGFIX (psychopath mood toggle): vanilla's Thought.Description
                // (the base property we're overriding here) appends a
                // "(Nullified by: X)" explanation whenever ThoughtUtility finds
                // a nullifying trait/hediff/tale/precept/gene for this thought.
                // Because this override fully replaces Description with custom
                // flavour text and never calls base.Description, that line was
                // silently dropped — a psychopathic toddler's Self Soothing
                // thought would drop to zero mood with NO visible explanation,
                // and (more importantly for the settings toggle) toggling
                // "keep play mood for psychopaths" produced no visible change
                // in this tooltip either way, even though the underlying
                // Thought_Memory.MoodOffset() math (and Patch_ThoughtUtility_
                // NullifyingTrait_PlayMood) were already working correctly.
                // This made the feature look completely broken for babies while
                // it worked for children, whose TPE_ChildPlayHigh/TPE_ChildPlayEmpty
                // thoughts use the unmodified base Description and therefore
                // always showed the nullification line correctly.
                //
                // Fix: reproduce vanilla's exact append pattern here so this
                // thought behaves identically to every other nullifiable thought
                // in the game, and so the setting's effect is visible.
                string nullifiedText = ThoughtUtility.ThoughtNullifiedMessage(pawn, def);
                if (!string.IsNullOrEmpty(nullifiedText))
                {
                    description = description + "\n\n(" + nullifiedText + ")";
                }

                return description;
            }
        }

        /// <summary>
        /// Returns the display name for <see cref="otherPawn"/>.
        /// If the adult is a direct parent of the toddler, returns "Mommy"
        /// (female) or "Daddy" (male). Otherwise returns the adult's short
        /// label. Falls back to "someone" if no adult was found.
        /// </summary>
        string GetAdultDisplayName()
        {
            if (otherPawn == null)
                return "someone";

            // Check whether otherPawn is a direct parent of the toddler.
            // GetDirectRelation returns non-null only for explicit relations.
            if (pawn.relations?.GetDirectRelation(PawnRelationDefOf.Parent, otherPawn) != null)
            {
                return otherPawn.gender == Gender.Female ? "Mommy" : "Daddy";
            }

            return otherPawn.LabelShort;
        }
    }
}
