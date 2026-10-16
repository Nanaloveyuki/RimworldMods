using System.Collections.Generic;
using RimWorld;
using Verse;

namespace KeepYourIdentity
{
    public sealed class ChoiceLetter_BirthIdentity : ChoiceLetter
    {
        private Pawn baby;
        private Faction homeFaction;
        private GuestStatus defaultStatus;
        private bool resolved;

        internal void Initialize(Pawn newborn, GuestStatus status, Faction originalFaction)
        {
            baby = newborn;
            defaultStatus = status;
            homeFaction = originalFaction;
            StartTimeout(12 * GenDate.TicksPerHour);
        }

        public override bool CanDismissWithRightClick => false;
        public override bool ShouldAutomaticallyOpenLetter => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (resolved || ArchivedOnly || baby == null || baby.Dead || baby.Destroyed)
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return IdentityOption(GuestStatus.Guest);
                yield return IdentityOption(GuestStatus.Prisoner);
                yield return IdentityOption(GuestStatus.Slave);
                yield return Option_Postpone;
            }
        }

        private DiaOption IdentityOption(GuestStatus status)
        {
            return new DiaOption(IdentityUtility.Label(status))
            {
                action = () => Resolve(status),
                resolveTree = true
            };
        }

        internal void Tick()
        {
            if (TimeoutPassed || baby == null || baby.Dead || baby.Destroyed)
                Resolve(defaultStatus);
        }

        private void Resolve(GuestStatus status)
        {
            // Stale dialog actions and archived letters must never change a resolved identity.
            if (resolved)
                return;
            if (TimeoutPassed)
                status = defaultStatus;
            if (baby != null && !baby.Dead && !baby.Destroyed && baby.guest != null)
                IdentityUtility.Apply(baby, status, homeFaction);
            resolved = true;
            Find.LetterStack.RemoveLetter(this);
        }

        public override void Removed()
        {
            // LetterStackUpdate can remove an expired letter before the next simulation tick.
            // Also retain the default if another mod removes an unanswered letter early.
            if (!resolved)
            {
                if (baby != null && !baby.Dead && !baby.Destroyed && baby.guest != null)
                    IdentityUtility.Apply(baby, defaultStatus, homeFaction);
                resolved = true;
            }
            base.Removed();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref baby, "baby");
            Scribe_References.Look(ref homeFaction, "homeFaction");
            Scribe_Values.Look(ref defaultStatus, "defaultStatus", GuestStatus.Prisoner);
            Scribe_Values.Look(ref resolved, "resolved");
        }
    }

    public sealed class BirthIdentityComponent : GameComponent
    {
        public BirthIdentityComponent(Game game) { }

        public override void GameComponentTick()
        {
            // The stack and archive already persist the letter and its absolute deadline.
            List<Letter> letters = Find.LetterStack.LettersListForReading;
            for (int i = letters.Count - 1; i >= 0; i--)
            {
                if (letters[i] is ChoiceLetter_BirthIdentity identityLetter)
                    identityLetter.Tick();
            }
        }
    }
}
