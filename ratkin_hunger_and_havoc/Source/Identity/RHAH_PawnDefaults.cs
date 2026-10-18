using HungerAndHavoc.Api;

namespace HungerAndHavoc.Identity
{
    internal static class RHAH_PawnDefaults
    {
        public static bool Allows(IRHAH_Pawn pawn, RHAH_BehaviorGate gate)
        {
            if (pawn == null)
            {
                return AllowsInactive(gate);
            }

            return Allows(pawn.Role, pawn.Lifecycle, pawn.Attitude, gate);
        }

        public static bool Allows(
            RHAH_PawnRole role,
            RHAH_Lifecycle lifecycle,
            RHAH_Attitude attitude,
            RHAH_BehaviorGate gate)
        {
            if (lifecycle == RHAH_Lifecycle.Released || lifecycle == RHAH_Lifecycle.Dead)
            {
                return AllowsInactive(gate);
            }

            switch (gate)
            {
                case RHAH_BehaviorGate.Beg:
                    return MasterOn(RHAH_BehaviorGate.Beg) && IsBeggar(role);
                case RHAH_BehaviorGate.Steal:
                    return MasterOn(RHAH_BehaviorGate.Steal) && IsThief(role);
                case RHAH_BehaviorGate.Fight:
                    return MasterOn(RHAH_BehaviorGate.Fight) &&
                           (role == RHAH_PawnRole.Siege || attitude == RHAH_Attitude.Hostile);
                case RHAH_BehaviorGate.LeaveAfterFed:
                    return true;
                case RHAH_BehaviorGate.EatOutsideRelief:
                    return attitude != RHAH_Attitude.Friendly;
                case RHAH_BehaviorGate.FeedFromRelief:
                    return true;
                case RHAH_BehaviorGate.Gnaw:
                    return MasterOn(RHAH_BehaviorGate.Gnaw);
                case RHAH_BehaviorGate.TailBite:
                    return true;
                case RHAH_BehaviorGate.Leash:
                    return IsYoungRole(role);
                case RHAH_BehaviorGate.Carry:
                    return !IsYoungRole(role);
                case RHAH_BehaviorGate.JoinColony:
                    return true;
                case RHAH_BehaviorGate.Hire:
                    return true;
                case RHAH_BehaviorGate.Transfer:
                    return true;
                case RHAH_BehaviorGate.Imprison:
                    return true;
                case RHAH_BehaviorGate.DropOffChild:
                    return role == RHAH_PawnRole.Mother ||
                           role == RHAH_PawnRole.BeggarMother;
                case RHAH_BehaviorGate.ExitMap:
                    return lifecycle == RHAH_Lifecycle.Leaving;
                default:
                    return false;
            }
        }

        static bool AllowsInactive(RHAH_BehaviorGate gate)
        {
            return gate == RHAH_BehaviorGate.JoinColony ||
                   gate == RHAH_BehaviorGate.Hire ||
                   gate == RHAH_BehaviorGate.Transfer ||
                   gate == RHAH_BehaviorGate.Imprison ||
                   gate == RHAH_BehaviorGate.Leash ||
                   gate == RHAH_BehaviorGate.Carry;
        }
        static bool MasterOn(RHAH_BehaviorGate gate)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (settings == null)
            {
                return true;
            }

            switch (gate)
            {
                case RHAH_BehaviorGate.Beg: return settings.beggingEnabled;
                case RHAH_BehaviorGate.Steal: return settings.stealingEnabled;
                case RHAH_BehaviorGate.Fight: return settings.fightingEnabled;
                case RHAH_BehaviorGate.Gnaw: return settings.gnawingEnabled;
                default: return true;
            }
        }


        static bool IsBeggar(RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.Beggar ||
                   role == RHAH_PawnRole.BeggarMother ||
                   role == RHAH_PawnRole.BeggarChild ||
                   role == RHAH_PawnRole.Refugee ||
                   role == RHAH_PawnRole.Labor;
        }

        static bool IsThief(RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.Thief || role == RHAH_PawnRole.ThiefChild;
        }

        internal static bool IsYoungRole(RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.RatkinYoung ||
                   role == RHAH_PawnRole.BeggarChild ||
                   role == RHAH_PawnRole.ThiefChild ||
                   role == RHAH_PawnRole.WildChild;
        }
    }
}
