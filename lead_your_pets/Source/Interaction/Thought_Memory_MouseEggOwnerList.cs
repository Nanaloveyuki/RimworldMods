using System.Collections.Generic;
using RimWorld;

namespace LeadYourPet
{
    public class Thought_Memory_MouseEggOwnerList : Thought_Memory
    {
        public override string LabelCap
        {
            get
            {
                if (pawn == null || LeadYourPetUtility.Component == null)
                {
                    return base.LabelCap;
                }

                List<LeashLink> masterLinks = LeadYourPetUtility.Component.GetLinksForMaster(pawn);
                List<string> names = new List<string>(3);
                for (int i = 0; i < masterLinks.Count && names.Count < 3; i++)
                {
                    LeashLink link = masterLinks[i];
                    if (link?.Pet == null || !LeadYourPetUtility.IsMouseEgg(link.Pet))
                    {
                        continue;
                    }

                    string label = link.Pet.LabelShort;
                    if (!names.Contains(label))
                    {
                        names.Add(label);
                    }
                }

                if (names.Count == 0)
                {
                    return base.LabelCap;
                }

                return $"{base.LabelCap} ({string.Join(", ", names)})";
            }
        }
    }
}
