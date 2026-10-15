using System.Collections.Generic;
using HungerAndHavoc.Api;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_FamilyRules
    {
        internal static bool CanDrop(RHAH_PawnRole role, bool leaving, bool enabled)
        {
            return enabled &&
                leaving &&
                (role == RHAH_PawnRole.Mother || role == RHAH_PawnRole.BeggarMother);
        }

        internal static bool MarkDropped(IList<int> dropped, int childLoadId)
        {
            if (dropped == null || childLoadId <= 0 || dropped.Contains(childLoadId))
            {
                return false;
            }

            dropped.Add(childLoadId);
            return true;
        }

        internal static bool AllDropped(IList<int> children, IList<int> dropped)
        {
            if (children == null || children.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] <= 0)
                {
                    continue;
                }

                if (dropped == null || !dropped.Contains(children[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool CanMotherFeed(RHAH_PawnRole role, bool enabled, bool childHungry)
        {
            return enabled && childHungry && (role == RHAH_PawnRole.Mother || role == RHAH_PawnRole.BeggarMother);
        }

        internal static bool CanScavenge(bool enabled, bool prisoner, bool hungry)
        {
            return enabled && prisoner && hungry;
        }

        internal static bool CanTailBite(bool enabled, bool prisoner, bool hungry, bool targetAsleep, float targetAge)
        {
            float age = Core.RHAH_Mod.Settings == null ? TailAge : Core.RHAH_Mod.Settings.tailBiteAge;
            return enabled && prisoner && hungry && targetAsleep && targetAge >= 0f && targetAge < age;
        }

        internal const float ChildHungry = 0.3f;
        internal const float PrisonerHungry = 0.2f;
        internal const float TailAge = 3f;
        internal const float ScavengeNutrition = 0.15f;
        internal const float TailNutrition = 0.35f;
        internal const float TailFailDamage = 4f;
        internal const int WorkTicks = 150;
        internal const string TailPartDef = "RK_BodyPart_Tail";

        internal static bool StillCarried(Verse.Pawn mother, Verse.Pawn child)
        {
            return mother != null && child != null && mother.carryTracker?.CarriedThing == child &&
                child.CarriedBy == mother;
        }

        internal static bool CanGiveFood(bool motherHasFood, bool childCanEat)
        {
            return motherHasFood && childCanEat;
        }

        internal static bool ScavengeFilth(bool spawned, bool reachable, bool reservable)
        {
            return spawned && reachable && reservable;
        }

        internal static bool NaturalTail(bool partExists, bool missing, bool addedPart)
        {
            return partExists && !missing && !addedPart;
        }
    }
}
