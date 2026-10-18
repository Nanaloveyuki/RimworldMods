using System;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace LeadYourPet
{
    public static class LeadYourPetApi
    {
        public static bool Available => LeadYourPetUtility.Component != null;

        public static bool TryStartMotherLeash(Pawn adult, Pawn child)
        {
            if (adult == null || child == null)
            {
                return false;
            }

            return Call(component => component.TryStartRatkinMotherLeash(adult, child, false), false);
        }

        public static bool TryStartChildLeash(Pawn adult, Pawn child)
        {
            if (adult == null || child == null)
            {
                return false;
            }

            return Call(component => component.StartLeash(adult, child, true, false), false);
        }

        public static bool TryAssignTravelChildren(Lord lord)
        {
            if (lord == null)
            {
                return false;
            }

            return Call(component =>
            {
                int before = component.LinkCount;
                component.TryAssignTravelMouseEggs(lord);
                return component.LinkCount > before;
            }, false);
        }

        public static bool TryAnchor(Pawn master, IntVec3 cell)
        {
            if (master == null || !cell.IsValid)
            {
                return false;
            }

            return Call(component =>
            {
                if (!master.Spawned || master.Map == null || !cell.InBounds(master.Map))
                {
                    return false;
                }

                component.AnchorLeashedPetsToCell(master, cell);
                return true;
            }, false);
        }

        public static void EndForPet(Pawn pet)
        {
            if (pet == null)
            {
                return;
            }

            Call(component => component.EndLeashForPet(pet, false));
        }

        public static void EndForMaster(Pawn master)
        {
            if (master == null)
            {
                return;
            }

            Call(component => LeadYourPetApiRules.EndForMaster(
                CopyPets(component.GetLinksForMaster(master)),
                pet => component.EndLeashForPet(pet, false)));
        }

        public static void ClearOwnership(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            Call(component =>
            {
                component.ClearMouseEggPetState(pawn);
                component.ClearTravelStock(pawn);
            });
        }

        public static bool TryCopyLinkedPets(Pawn master, List<Pawn> into)
        {
            if (master == null || into == null)
            {
                return false;
            }

            return Call(component => LeadYourPetApiRules.CopyLinkedPets(CopyPets(component.GetLinksForMaster(master)), into), false);
        }

        public static void SetSpecialSource(Pawn pawn, int source)
        {
            if (pawn == null || !LeadYourPetApiRules.TryResolveSpecialSource(source, out MouseEggSpecialSource resolved))
            {
                return;
            }

            Call(component => component.SetMouseEggSpecialSource(pawn, resolved));
        }

        public static bool IsLeashed(Pawn pet)
        {
            if (pet == null)
            {
                return false;
            }

            return Call(component => component.GetLinkForPet(pet) != null, false);
        }

        private static List<Pawn> CopyPets(List<LeashLink> links)
        {
            List<Pawn> pets = new List<Pawn>(links?.Count ?? 0);
            if (links == null)
            {
                return pets;
            }

            for (int i = 0; i < links.Count; i++)
            {
                Pawn pet = links[i]?.Pet;
                if (pet != null)
                {
                    pets.Add(pet);
                }
            }

            return pets;
        }

        private static void Call(Action<LeadYourPetGameComponent> action)
        {
            Call(component =>
            {
                action(component);
                return true;
            }, false);
        }

        private static T Call<T>(Func<LeadYourPetGameComponent, T> action, T fallback)
        {
            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component == null)
            {
                return fallback;
            }

            try
            {
                return action(component);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Public leash API failed: " + exception, 19462006);
                return fallback;
            }
        }
    }

    internal static class LeadYourPetApiRules
    {
        public const int None = (int)MouseEggSpecialSource.None;
        public const int BeggarFamily = (int)MouseEggSpecialSource.MouseDisasterBeggarFamily;
        public const int ChildExchange = (int)MouseEggSpecialSource.MouseDisasterChildExchange;

        public static bool IsAcceptedSpecialSource(int source)
        {
            return TryResolveSpecialSource(source, out _);
        }

        public static bool TryResolveSpecialSource(int source, out MouseEggSpecialSource resolved)
        {
            if (!Enum.IsDefined(typeof(MouseEggSpecialSource), source) || source == (int)MouseEggSpecialSource.None)
            {
                resolved = MouseEggSpecialSource.None;
                return false;
            }

            resolved = (MouseEggSpecialSource)source;
            return true;
        }

        public static bool CopyLinkedPets<T>(IReadOnlyList<T> pets, List<T> into) where T : class
        {
            if (pets == null || into == null || pets.Count == 0)
            {
                return false;
            }

            int added = 0;
            for (int i = 0; i < pets.Count; i++)
            {
                T pet = pets[i];
                if (pet == null || into.Contains(pet))
                {
                    continue;
                }

                into.Add(pet);
                added++;
            }

            return added > 0;
        }

        public static void EndForMaster<T>(IReadOnlyList<T> pets, Action<T> endPet) where T : class
        {
            if (pets == null || endPet == null)
            {
                return;
            }

            for (int i = 0; i < pets.Count; i++)
            {
                if (pets[i] != null)
                {
                    endPet(pets[i]);
                }
            }
        }
    }
}
