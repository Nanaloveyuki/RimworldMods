using HarmonyLib;
using RimWorld;
using Verse;

namespace KeepYourIdentity
{
    public sealed class KeepYourIdentityMod : Mod
    {
        public KeepYourIdentityMod(ModContentPack content) : base(content)
        {
            new Harmony("nanaloveyuki.keepyouridentity").PatchAll();
        }
    }

    internal readonly struct BirthIdentity
    {
        internal readonly GuestStatus Status;
        internal readonly Faction HomeFaction;
        internal readonly Pawn Mother;
        internal bool Applies => Mother != null;

        internal BirthIdentity(Pawn mother)
        {
            Mother = mother;
            Status = mother.guest.GuestStatus;
            HomeFaction = mother.IsSlave ? mother.guest.SlaveFaction : mother.Faction;
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    internal static class BirthPatch
    {
        private static void Prefix(Thing birtherThing, out BirthIdentity __state)
        {
            Pawn mother = birtherThing as Pawn;
            __state = mother != null && mother.RaceProps.Humanlike &&
                (mother.IsPrisonerOfColony || mother.IsSlaveOfColony)
                ? new BirthIdentity(mother) : default;
        }

        private static void Postfix(Thing __result, BirthIdentity __state)
        {
            if (!__state.Applies || __result is not Pawn baby || baby.Dead || baby.Destroyed || baby.guest == null)
                return;

            IdentityUtility.Apply(baby, __state.Status, __state.HomeFaction);
            var letter = (ChoiceLetter_BirthIdentity)LetterMaker.MakeLetter(
                "KYI_BirthIdentityLabel".Translate(),
                "KYI_BirthIdentityText".Translate(baby.Named("BABY"), __state.Mother.Named("MOTHER"),
                    IdentityUtility.Label(__state.Status).Named("IDENTITY")),
                DefDatabase<LetterDef>.GetNamed("KYI_BirthIdentity"), baby);
            letter.Initialize(baby, __state.Status, __state.HomeFaction);
            Find.LetterStack.ReceiveLetter(letter);
        }
    }

    internal static class IdentityUtility
    {
        internal static string Label(GuestStatus status)
        {
            return (status == GuestStatus.Prisoner ? "KYI_Prisoner" :
                status == GuestStatus.Slave ? "KYI_Slave" : "KYI_Colonist").Translate();
        }

        internal static void Apply(Pawn baby, GuestStatus status, Faction homeFaction)
        {
            Faction player = Faction.OfPlayer;
            if (status == GuestStatus.Prisoner)
            {
                if (baby.IsPrisonerOfColony && baby.Faction == homeFaction)
                    return;
                // Clear slavery first: SetGuestStatus(Prisoner) otherwise restores SlaveFaction itself.
                baby.guest.SetGuestStatus(null);
                if (baby.Faction != homeFaction)
                    baby.SetFaction(homeFaction);
                baby.guest.SetGuestStatus(player, GuestStatus.Prisoner);
            }
            else if (status == GuestStatus.Slave)
            {
                if (baby.IsSlaveOfColony)
                    return;
                baby.guest.SetGuestStatus(null);
                if (baby.Faction != homeFaction)
                    baby.SetFaction(homeFaction);
                baby.guest.SetGuestStatus(player, GuestStatus.Slave);
            }
            else
            {
                if (baby.Faction != player)
                    baby.SetFaction(player);
                else if (baby.IsPrisoner || baby.IsSlave)
                    baby.guest.SetGuestStatus(null);
            }
        }
    }
}
