using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_WarlordVictimBody
    {
        internal static bool Apply(Verse.Pawn pawn)
        {
            BodyPartRecord preservedLeg = null;
            List<BodyPartRecord> legs = new List<BodyPartRecord>();
            List<BodyPartRecord> arms = new List<BodyPartRecord>();
            List<BodyPartRecord> kidneys = new List<BodyPartRecord>();
            List<BodyPartRecord> lungs = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == BodyPartDefOf.Leg) legs.Add(part);
                else if (part.def == BodyPartDefOf.Arm) arms.Add(part);
                else if (part.def.defName == "Kidney") kidneys.Add(part);
                else if (part.def == BodyPartDefOf.Lung) lungs.Add(part);
            }
            for (int i = 0; i < legs.Count; i++)
            {
                BodyPartRecord leg = legs[i];
                if (pawn.health.hediffSet.GetPartHealth(leg) < leg.def.GetMaxHealth(pawn) ||
                    pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(leg)) continue;
                preservedLeg = leg;
                break;
            }
            if (preservedLeg == null) return false;

            bool organRemoved = kidneys.Count > 1 && Remove(pawn, kidneys.RandomElement());
            if (lungs.Count > 1 && (!organRemoved || Rand.Bool)) organRemoved |= Remove(pawn, lungs.RandomElement());
            bool limbRemoved = arms.Count > 0 && Remove(pawn, arms.RandomElement());
            if (legs.Count > 1 && Rand.Bool)
            {
                legs.Remove(preservedLeg);
                limbRemoved |= Remove(pawn, legs.RandomElement());
            }
            return organRemoved && limbRemoved && !pawn.Dead &&
                !pawn.health.hediffSet.PartIsMissing(preservedLeg) &&
                pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        static bool Remove(Verse.Pawn pawn, BodyPartRecord part)
        {
            Hediff_MissingPart missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, part);
            missing.IsFresh = false;
            if (pawn.health.WouldDieAfterAddingHediff(missing)) return false;
            pawn.health.AddHediff(missing);
            return true;
        }
    }
}
