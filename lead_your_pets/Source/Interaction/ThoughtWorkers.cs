using RimWorld;
using Verse;

namespace LeadYourPet
{
    public class ThoughtWorker_LeadingPet : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.AnyLeashedPetFor(p);
        }
    }

    public class ThoughtWorker_MouseEggOwner : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.AnyMouseEggPetFor(p);
        }
    }

    public class ThoughtWorker_MouseEggFear : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.IsMouseEggPet(p);
        }
    }

    public class ThoughtWorker_MouseEggParentPet : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (LeadYourPetUtility.Component == null || p?.relations == null)
            {
                return false;
            }

            foreach (Pawn child in p.relations.Children)
            {
                if (LeadYourPetUtility.Component.IsMouseEggPet(child))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class ThoughtWorker_MouseEggCaretaker : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.AnyColonistMouseEggPetFor(p);
        }
    }

    public class ThoughtWorker_MouseEggWatched : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.IsColonistMouseEggPet(p);
        }
    }

    public class ThoughtWorker_RatkinMotherLeash : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.AnyRatkinMotherBabyFor(p);
        }
    }

    public class ThoughtWorker_RatkinBabyLeashed : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.IsRatkinMotherBaby(p);
        }
    }

    public class ThoughtWorker_MouseDisasterBeggarMotherBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialMotherSourceMood(p, MouseEggSpecialSource.MouseDisasterBeggarFamily);
        }
    }

    public class ThoughtWorker_MouseDisasterBeggarBabyBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialBabySourceMood(p, MouseEggSpecialSource.MouseDisasterBeggarFamily);
        }
    }

    public class ThoughtWorker_MouseDisasterChildExchangeMotherBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialMotherSourceMood(p, MouseEggSpecialSource.MouseDisasterChildExchange);
        }
    }

    public class ThoughtWorker_MouseDisasterChildExchangeBabyBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialBabySourceMood(p, MouseEggSpecialSource.MouseDisasterChildExchange);
        }
    }
}
