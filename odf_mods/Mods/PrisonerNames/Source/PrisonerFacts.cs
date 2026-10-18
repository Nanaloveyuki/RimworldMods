using System;
using System.Collections.Generic;
using Oxygendsl.Core;
using Oxygendsl.Language;
using Oxygendsl.Mod.Runtime;
using RimWorld;
using Verse;

namespace Odf.PrisonerNames
{
    public static class PrisonerFacts
    {
        public static PawnContext Create(Pawn pawn, int number)
        {
            return Create(pawn, number, null);
        }

        public static PawnContext Create(Pawn pawn, int number, object record)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            PawnContext context = PawnFacts.Create(pawn);
            context.Facts = new PrisonerHostFacts(pawn, number, context.Facts, record);
            return context;
        }

        sealed class PrisonerHostFacts : IOxygenHostValue
        {
            readonly Pawn pawn;
            readonly int number;
            readonly string numberText;
            readonly IOxygenHostValue inherited;
            readonly object record;
            readonly SkillQuery skills;
            readonly PassionQuery passions;
            readonly HediffQuery hediffs;
            readonly GeneQuery genes;
            readonly InteractionQuery interactions;
            readonly ValueCallable values;
            readonly FormatCallable format;

            public PrisonerHostFacts(Pawn pawn, int number, IOxygenHostValue inherited, object record)
            {
                this.pawn = pawn;
                this.number = number;
                numberText = number.ToString("D3");
                this.inherited = inherited;
                this.record = record;
                skills = new SkillQuery(pawn);
                passions = new PassionQuery(pawn);
                hediffs = new HediffQuery(pawn);
                genes = new GeneQuery(pawn);
                interactions = new InteractionQuery(pawn);
                values = new ValueCallable(RecordValues(record));
                format = new FormatCallable(pawn, number, record);
            }

            public bool TryGetMember(string name, out object value)
            {
                switch (name)
                {
                    case "number": value = number; return true;
                    case "number_text": value = numberText; return true;
                    case "score": value = RecordScore(record); return true;
                    case "grade": value = RecordGrade(record); return true;
                    case "manual_grade": value = RecordManualGrade(record); return true;
                    case "manual_score": value = RecordManualScore(record); return true;
                    case "value": value = values; return true;
                    case "formatted": value = format; return true;
                    case "original_name": value = OriginalShort(pawn); return true;
                    case "original_short": value = OriginalNick(pawn); return true;
                    case "original_full_name": value = OriginalFull(pawn); return true;
                    case "first_name": value = FirstName(pawn); return true;
                    case "last_name": value = LastName(pawn); return true;
                    case "race": value = RaceName(pawn); return true;
                    case "race_label": value = RaceLabel(pawn); return true;
                    case "race_prefix": value = RacePrefix(record, pawn); return true;
                    case "age": value = Age(pawn); return true;
                    case "gender": value = pawn.gender.ToString(); return true;
                    case "interaction": value = InteractionName(pawn); return true;
                    case "interaction_label": value = InteractionLabel(pawn); return true;
                    case "resistance": value = Resistance(pawn); return true;
                    case "will": value = Will(pawn); return true;
                    case "recruitable": value = Recruitable(pawn); return true;
                    case "health": value = Health(pawn); return true;
                    case "bleeding": value = Bleeding(pawn); return true;
                    case "skill": value = skills; return true;
                    case "passion": value = passions; return true;
                    case "has_hediff": value = hediffs; return true;
                    case "has_gene": value = genes; return true;
                    case "has_interaction": value = interactions; return true;
                    default:
                        if (inherited != null && inherited.TryGetMember(name, out value)) return true;
                        value = null;
                        return false;
                }
            }
        }

        sealed class FormatCallable : IOxygenHostCallable
        {
            readonly Pawn pawn;
            readonly int number;
            readonly object record;
            public FormatCallable(Pawn pawn, int number, object record)
            {
                this.pawn = pawn;
                this.number = number;
                this.record = record;
            }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                string pattern = positional != null && positional.Count > 0 ? Convert.ToString(positional[0]) : null;
                if (named != null && named.ContainsKey("pattern")) pattern = Convert.ToString(named["pattern"]);
                NamePolicy policy = CurrentPolicy(record);
                bool chinese = NamingLanguage.Chinese;
                if (pattern == null && policy != null) pattern = policy.PatternFor(RaceName(pawn), Age(pawn), chinese);
                var tokens = new NameTokens
                {
                    Number = number,
                    NumberText = number.ToString("D3"),
                    Score = RecordScore(record),
                    Grade = RecordGrade(record),
                    Race = RaceName(pawn),
                    RaceLabel = RaceLabel(pawn),
                    RacePrefix = RacePrefix(record, pawn),
                    OriginalName = OriginalShort(pawn),
                    OriginalShort = OriginalNick(pawn),
                    OriginalFullName = OriginalFull(pawn),
                    FirstName = FirstName(pawn),
                    LastName = LastName(pawn),
                    InteractionLabel = InteractionLabel(pawn)
                };
                string formatted = NameFormatter.Format(pattern, tokens);
                return policy == null ? formatted : NameFormatter.ApplyStyle(policy.Style, formatted, tokens);
            }
        }

        static NamePolicy CurrentPolicy(object record)
        {
            GameComponent_PrisonerNames runtime = GameComponent_PrisonerNames.Current;
            return runtime == null ? null : runtime.Policy;
        }

        static ScalarBag RecordValues(object record)
        {
            var stored = record as RecordView;
            return stored == null ? null : stored.Values;
        }

        static double RecordScore(object record)
        {
            var stored = record as RecordView;
            if (stored != null) return stored.Score;
            GameComponent_PrisonerNames runtime = GameComponent_PrisonerNames.Current;
            return runtime == null ? NamePolicy.DefaultNeutralScore : runtime.Policy.NeutralScore;
        }

        static string RecordGrade(object record)
        {
            var stored = record as RecordView;
            if (stored != null && !string.IsNullOrEmpty(stored.ManualGrade)) return stored.ManualGrade;
            GameComponent_PrisonerNames runtime = GameComponent_PrisonerNames.Current;
            NamePolicy policy = runtime == null ? null : runtime.Policy;
            return policy == null ? NamePolicy.DefaultGrade : policy.GradeFor(RecordScore(record));
        }

        static string RecordManualGrade(object record)
        {
            var stored = record as RecordView;
            return stored == null || stored.ManualGrade == null ? string.Empty : stored.ManualGrade;
        }

        static bool RecordManualScore(object record)
        {
            var stored = record as RecordView;
            return stored != null && stored.ManualScore;
        }

        static string RacePrefix(object record, Pawn pawn)
        {
            GameComponent_PrisonerNames runtime = GameComponent_PrisonerNames.Current;
            NamePolicy policy = runtime == null ? null : runtime.Policy;
            return policy == null ? string.Empty : policy.PrefixFor(RaceName(pawn), NamingLanguage.Chinese);
        }

        static string OriginalNick(Pawn pawn)
        {
            NameTriple triple = pawn == null ? null : pawn.Name as NameTriple;
            return triple == null ? OriginalShort(pawn) : triple.Nick ?? string.Empty;
        }

        static string FirstName(Pawn pawn)
        {
            NameTriple triple = pawn == null ? null : pawn.Name as NameTriple;
            return triple == null ? string.Empty : triple.First ?? string.Empty;
        }

        static string LastName(Pawn pawn)
        {
            NameTriple triple = pawn == null ? null : pawn.Name as NameTriple;
            return triple == null ? string.Empty : triple.Last ?? string.Empty;
        }

        sealed class SkillQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public SkillQuery(Pawn pawn) { this.pawn = pawn; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                SkillRecord record = Record(pawn, RequiredName(positional, named, "Skill query"));
                return record == null ? 0 : record.Level;
            }
        }

        sealed class PassionQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public PassionQuery(Pawn pawn) { this.pawn = pawn; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                SkillRecord record = Record(pawn, RequiredName(positional, named, "Passion query"));
                return record == null ? 0 : (int)record.passion;
            }
        }

        sealed class HediffQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public HediffQuery(Pawn pawn) { this.pawn = pawn; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                HediffSet set = pawn.health == null ? null : pawn.health.hediffSet;
                if (set == null) return false;
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(RequiredName(positional, named, "Hediff query"));
                return def != null && set.HasHediff(def);
            }
        }

        sealed class GeneQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public GeneQuery(Pawn pawn) { this.pawn = pawn; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                Pawn_GeneTracker tracker = pawn.genes;
                if (tracker == null) return false;
                GeneDef def = DefDatabase<GeneDef>.GetNamedSilentFail(RequiredName(positional, named, "Gene query"));
                return def != null && tracker.HasActiveGene(def);
            }
        }

        sealed class InteractionQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public InteractionQuery(Pawn pawn) { this.pawn = pawn; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                Pawn_GuestTracker guest = pawn.guest;
                if (guest == null) return false;
                PrisonerInteractionModeDef def = DefDatabase<PrisonerInteractionModeDef>.GetNamedSilentFail(RequiredName(positional, named, "Interaction query"));
                return def != null && guest.IsInteractionEnabled(def);
            }
        }

        static SkillRecord Record(Pawn pawn, string name)
        {
            if (pawn.skills == null) return null;
            SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(name);
            return def == null ? null : pawn.skills.GetSkill(def);
        }

        static string RequiredName(IList<object> positional, IDictionary<string, object> named, string query)
        {
            object value = null;
            if (named != null && named.TryGetValue("name", out value)) return Convert.ToString(value);
            if (positional != null && positional.Count > 0) return Convert.ToString(positional[0]);
            throw new ScriptEvaluationException(query + " requires a defName.", 1, 1);
        }

        public static string OriginalShort(Pawn pawn)
        {
            if (pawn == null || pawn.Name == null) return pawn == null ? string.Empty : pawn.KindLabel;
            return pawn.Name.ToStringShort;
        }

        public static string OriginalFull(Pawn pawn)
        {
            if (pawn == null || pawn.Name == null) return OriginalShort(pawn);
            return pawn.Name.ToStringFull;
        }

        static string RaceName(Pawn pawn)
        {
            return pawn.def == null ? string.Empty : pawn.def.defName ?? string.Empty;
        }

        static string RaceLabel(Pawn pawn)
        {
            if (pawn.def == null) return string.Empty;
            return pawn.def.LabelCap.RawText ?? string.Empty;
        }

        static double Age(Pawn pawn)
        {
            return pawn.ageTracker == null ? 0d : pawn.ageTracker.AgeBiologicalYearsFloat;
        }

        static string InteractionName(Pawn pawn)
        {
            PrisonerInteractionModeDef mode = pawn.guest == null ? null : pawn.guest.ExclusiveInteractionMode;
            return mode == null ? string.Empty : mode.defName ?? string.Empty;
        }

        static string InteractionLabel(Pawn pawn)
        {
            PrisonerInteractionModeDef mode = pawn.guest == null ? null : pawn.guest.ExclusiveInteractionMode;
            return mode == null ? string.Empty : mode.LabelCap.RawText ?? string.Empty;
        }

        static double Resistance(Pawn pawn)
        {
            return pawn.guest == null ? 0d : pawn.guest.resistance;
        }

        static double Will(Pawn pawn)
        {
            return pawn.guest == null ? 0d : pawn.guest.will;
        }

        static bool Recruitable(Pawn pawn)
        {
            return pawn.guest != null && pawn.guest.Recruitable;
        }

        static double Health(Pawn pawn)
        {
            if (pawn.health == null || pawn.health.summaryHealth == null) return 1d;
            return pawn.health.summaryHealth.SummaryHealthPercent;
        }

        static bool Bleeding(Pawn pawn)
        {
            return pawn.health != null && pawn.health.hediffSet != null && pawn.health.hediffSet.BleedRateTotal > 0f;
        }
    }
}
