using System;
using Verse;

namespace Odf.PrisonerNames
{
    public sealed class PrisonerNameState : IExposable
    {
        Name original;
        Name applied;
        bool captured;

        public Name Original(Pawn pawn)
        {
            if (!captured)
            {
                original = pawn.Name;
                captured = true;
            }
            return original;
        }

        public bool Observe(Pawn pawn)
        {
            Name expected = applied ?? Original(pawn);
            if (Same(pawn.Name, expected)) return false;
            original = pawn.Name;
            applied = null;
            return true;
        }

        public Name Desired(Pawn pawn, string alias)
        {
            Name source = Original(pawn);
            if (string.IsNullOrEmpty(alias)) return source;
            if (pawn.Name != null && string.Equals(pawn.Name.ToStringShort, alias, StringComparison.Ordinal)) return pawn.Name;
            NameTriple triple = source as NameTriple;
            return triple == null ? new NameSingle(alias) : new NameTriple(triple.First, alias, triple.Last);
        }

        public void Applied(Name value, bool hasAlias)
        {
            applied = hasAlias ? value : null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref captured, "captured", false);
            Scribe_Deep.Look(ref original, "original");
            Scribe_Deep.Look(ref applied, "applied");
        }

        internal static bool Same(Name left, Name right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is NameTriple a && right is NameTriple b)
                return a.First == b.First && a.Last == b.Last && a.NickSet == b.NickSet && a.Nick == b.Nick;
            if (left is NameSingle single && right is NameSingle other)
                return single.Name == other.Name && single.Numerical == other.Numerical;
            return false;
        }
    }
}
