using System;
using System.Collections.Generic;
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
        [ThreadStatic] private static BirthScope current;

        private sealed class BirthScope
        {
            internal readonly BirthScope Parent;
            internal readonly BirthIdentity Identity;
            internal List<Pawn> Babies;

            internal BirthScope(BirthScope parent, BirthIdentity identity)
            {
                Parent = parent;
                Identity = identity;
            }

            internal void Add(Pawn baby)
            {
                if (!Identity.Applies || baby == null || !baby.RaceProps.Humanlike)
                    return;
                Babies ??= new List<Pawn>();
                if (!Babies.Contains(baby))
                    Babies.Add(baby);
            }
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(Thing birtherThing, out BirthScope __state)
        {
            Pawn mother = birtherThing as Pawn;
            BirthIdentity identity = mother != null && mother.RaceProps.Humanlike &&
                (mother.IsPrisonerOfColony || mother.IsSlaveOfColony)
                ? new BirthIdentity(mother) : default;
            current = __state = new BirthScope(current, identity);
        }

        // Finalizers run after every postfix, including mods that generate additional babies.
        private static void Finalizer(Thing __result, BirthScope __state, Exception __exception)
        {
            if (__state == null)
                return;
            current = __state.Parent;
            if (__exception != null)
                return;
            __state.Add(__result as Pawn);
            if (__state.Babies != null)
                foreach (Pawn baby in __state.Babies)
                    Notify(baby, __state.Identity);
        }

        internal static void Capture(Pawn baby, PawnGenerationRequest request)
        {
            if (request.AllowedDevelopmentalStages == DevelopmentalStage.Newborn)
                current?.Add(baby);
        }

        private static void Notify(Pawn baby, BirthIdentity identity)
        {
            if (baby.Dead || baby.Destroyed || baby.guest == null)
                return;

            IdentityUtility.Apply(baby, identity.Status, identity.HomeFaction);
            var letter = (ChoiceLetter_BirthIdentity)LetterMaker.MakeLetter(
                "KYI_BirthIdentityLabel".Translate(),
                "KYI_BirthIdentityText".Translate(baby.Named("BABY"), identity.Mother.Named("MOTHER"),
                    IdentityUtility.Label(identity.Status).Named("IDENTITY")),
                DefDatabase<LetterDef>.GetNamed("KYI_BirthIdentity"), baby);
            letter.Initialize(baby, identity.Status, identity.HomeFaction);
            Find.LetterStack.ReceiveLetter(letter);
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    internal static class NewbornGenerationPatch
    {
        private static void Postfix(Pawn __result, PawnGenerationRequest request)
        {
            BirthPatch.Capture(__result, request);
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
