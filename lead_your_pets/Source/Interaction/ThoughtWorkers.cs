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

    public class ThoughtWorker_YoungRatkinOwner : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.AnyYoungRatkinPetFor(p);
        }
    }

    public class ThoughtWorker_YoungRatkinFear : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.IsYoungRatkinPet(p);
        }
    }

    public class ThoughtWorker_YoungRatkinParentPet : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (LeadYourPetUtility.Component == null || p?.relations == null)
            {
                return false;
            }

            foreach (Pawn child in p.relations.Children)
            {
                if (LeadYourPetUtility.Component.IsYoungRatkinPet(child))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class ThoughtWorker_YoungRatkinCaretaker : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.AnyColonistYoungRatkinPetFor(p);
        }
    }

    public class ThoughtWorker_YoungRatkinWatched : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.IsColonistYoungRatkinPet(p);
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

    public class ThoughtWorker_RatkinDisasterBeggarMotherBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialMotherSourceMood(p, YoungRatkinSpecialSource.RatkinDisasterBeggarFamily);
        }
    }

    public class ThoughtWorker_RatkinDisasterBeggarBabyBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialBabySourceMood(p, YoungRatkinSpecialSource.RatkinDisasterBeggarFamily);
        }
    }

    public class ThoughtWorker_RatkinDisasterChildExchangeMotherBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialMotherSourceMood(p, YoungRatkinSpecialSource.RatkinDisasterChildExchange);
        }
    }

    public class ThoughtWorker_RatkinDisasterChildExchangeBabyBonus : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return LeadYourPetUtility.Component != null && LeadYourPetUtility.Component.HasSpecialBabySourceMood(p, YoungRatkinSpecialSource.RatkinDisasterChildExchange);
        }
    }
}
