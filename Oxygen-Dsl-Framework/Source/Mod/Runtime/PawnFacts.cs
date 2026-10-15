using System;
using System.Collections.Generic;
using System.Reflection;
using Oxygendsl.Core;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Oxygendsl.Mod.Runtime
{
    public static class PawnFacts
    {
        public static PawnContext Create(Pawn pawn)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            PawnContext context = new PawnContext
            {
                Id = pawn.thingIDNumber,
                Name = pawn.Name == null ? pawn.LabelShort : pawn.Name.ToStringShort,
                Mood = Level(pawn.needs == null ? null : pawn.needs.mood),
                Food = Level(pawn.needs == null ? null : pawn.needs.food),
                Rest = Level(pawn.needs == null ? null : pawn.needs.rest),
                Drafted = pawn.Drafted,
                Downed = pawn.Downed,
                Colonist = pawn.IsColonist
            };
            context.Facts = new NativePawnFacts(pawn);
            return context;
        }

        static double? Level(Need need)
        {
            return need == null ? (double?)null : need.CurLevelPercentage;
        }

        sealed class NativePawnFacts : IOxygenHostValue
        {
            readonly Pawn pawn;
            readonly TraitQuery traits;
            GroupCache group;

            public NativePawnFacts(Pawn pawn)
            {
                this.pawn = pawn;
                traits = new TraitQuery(pawn);
            }

            public bool TryGetMember(string name, out object value)
            {
                switch (name)
                {
                    case "has_trait": value = traits; return true;
                    case "trait_degree": value = traits.Degree; return true;
                    case "trait_suppressed": value = traits.Suppressed; return true;
                    case "faction_id": value = pawn.Faction == null ? 0 : pawn.Faction.loadID; return true;
                    case "faction_def": value = pawn.Faction == null || pawn.Faction.def == null ? string.Empty : pawn.Faction.def.defName; return true;
                    case "prisoner": value = pawn.IsPrisoner; return true;
                    case "slave": value = pawn.IsSlave; return true;
                    case "stage": value = StageName(pawn.DevelopmentalStage); return true;
                    case "hunger": value = HungerName(pawn); return true;
                    case "rest_category": value = RestName(pawn); return true;
                    case "sick": value = Sick(pawn); return true;
                    case "young_hungry": value = Group().YoungHungry; return true;
                    case "young_sick": value = Group().YoungSick; return true;
                    case "group_sick": value = Group().GroupSick; return true;
                    case "origin": value = Origin() != null; return true;
                    case "source": value = OriginText("SourceIncidentDisplayId"); return true;
                    case "role": value = OriginText("Role"); return true;
                    case "lifecycle": value = OriginText("Lifecycle"); return true;
                    case "active_visitor": value = OriginFlag("IsActiveVisitor"); return true;
                    case "carries_plague": value = OriginFlag("CarriesPlague"); return true;
                    case "has_been_fed": value = OriginFlag("HasBeenFed"); return true;
                    default: value = null; return false;
                }
            }

            GroupCache Group()
            {
                if (group == null) group = GroupCache.Read(pawn);
                return group;
            }

            object Origin()
            {
                return RhahOriginReader.Read(pawn);
            }

            string OriginText(string member)
            {
                object value = RhahOriginReader.Member(Origin(), member);
                return value == null ? string.Empty : value.ToString();
            }

            bool OriginFlag(string member)
            {
                return RhahOriginReader.Member(Origin(), member) is bool flag && flag;
            }
        }

        sealed class TraitQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public readonly DegreeQuery Degree;
            public readonly SuppressedQuery Suppressed;

            public TraitQuery(Pawn pawn)
            {
                this.pawn = pawn;
                Degree = new DegreeQuery(pawn);
                Suppressed = new SuppressedQuery(pawn);
            }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                string name = RequiredName(positional, named);
                int degree;
                bool hasDegree = TryDegree(positional, named, out degree);
                TraitSet traits = pawn.story == null ? null : pawn.story.traits;
                if (traits == null) return false;
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(name);
                if (def == null) return false;
                return hasDegree ? traits.HasTrait(def, degree) : traits.HasTrait(def);
            }
        }

        sealed class DegreeQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public DegreeQuery(Pawn pawn) { this.pawn = pawn; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                TraitSet traits = pawn.story == null ? null : pawn.story.traits;
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(RequiredName(positional, named));
                return traits == null || def == null ? 0 : traits.DegreeOfTrait(def);
            }
        }

        sealed class SuppressedQuery : IOxygenHostCallable
        {
            readonly Pawn pawn;
            public SuppressedQuery(Pawn pawn) { this.pawn = pawn; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                TraitSet traits = pawn.story == null ? null : pawn.story.traits;
                if (traits == null || traits.allTraits == null) return false;
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(RequiredName(positional, named));
                if (def == null) return false;
                for (int i = 0; i < traits.allTraits.Count; i++)
                {
                    Trait trait = traits.allTraits[i];
                    if (trait != null && trait.def == def && trait.Suppressed) return true;
                }
                return false;
            }
        }

        static string RequiredName(IList<object> positional, IDictionary<string, object> named)
        {
            object value = null;
            if (named != null && named.TryGetValue("name", out value)) return Convert.ToString(value);
            if (positional != null && positional.Count > 0) return Convert.ToString(positional[0]);
            throw new ScriptEvaluationException("Trait query requires a defName.", 0, 0);
        }

        static bool TryDegree(IList<object> positional, IDictionary<string, object> named, out int degree)
        {
            object value;
            if (named != null && named.TryGetValue("degree", out value))
            {
                degree = Convert.ToInt32(value);
                return true;
            }
            if (positional != null && positional.Count > 1)
            {
                degree = Convert.ToInt32(positional[1]);
                return true;
            }
            degree = 0;
            return false;
        }

        public static string StageName(DevelopmentalStage stage)
        {
            if (stage == DevelopmentalStage.None) return "None";
            string text = string.Empty;
            AppendStage(ref text, stage, DevelopmentalStage.Newborn, "Newborn");
            AppendStage(ref text, stage, DevelopmentalStage.Baby, "Baby");
            AppendStage(ref text, stage, DevelopmentalStage.Child, "Child");
            AppendStage(ref text, stage, DevelopmentalStage.Adult, "Adult");
            return text.Length == 0 ? stage.ToString() : text;
        }

        static void AppendStage(ref string text, DevelopmentalStage stage, DevelopmentalStage flag, string name)
        {
            if ((stage & flag) == 0) return;
            text = text.Length == 0 ? name : text + "|" + name;
        }

        static string HungerName(Pawn pawn)
        {
            Need_Food food = pawn.needs == null ? null : pawn.needs.food;
            return food == null ? string.Empty : food.CurCategory.ToString();
        }

        static string RestName(Pawn pawn)
        {
            Need_Rest rest = pawn.needs == null ? null : pawn.needs.rest;
            return rest == null ? string.Empty : rest.CurCategory.ToString();
        }

        public static bool Sick(Pawn pawn)
        {
            return pawn != null && pawn.health != null && pawn.health.hediffSet != null && pawn.health.hediffSet.AnyHediffMakesSickThought;
        }

        sealed class GroupCache
        {
            public readonly bool YoungHungry;
            public readonly bool YoungSick;
            public readonly bool GroupSick;

            GroupCache(bool youngHungry, bool youngSick, bool groupSick)
            {
                YoungHungry = youngHungry;
                YoungSick = youngSick;
                GroupSick = groupSick;
            }

            public static GroupCache Read(Pawn pawn)
            {
                if (pawn == null || pawn.Dead || pawn.Suspended || pawn.Map == null || pawn.Map.mapPawns == null)
                    return new GroupCache(false, false, false);
                Lord lord = pawn.GetLord();
                if (lord != null && lord.ownedPawns != null)
                    return Scan(pawn, lord.ownedPawns, true);
                if (pawn.IsColonist && pawn.Map.mapPawns.FreeColonistsSpawned != null)
                    return Scan(pawn, pawn.Map.mapPawns.FreeColonistsSpawned, false);
                return new GroupCache(false, false, false);
            }

            static GroupCache Scan(Pawn pawn, List<Pawn> others, bool lordGroup)
            {
                bool hungry = false;
                bool youngSick = false;
                bool groupSick = false;
                for (int i = 0; i < others.Count; i++)
                {
                    Pawn other = others[i];
                    if (other == null || other == pawn || other.Dead) continue;
                    if (!groupSick && Sick(other)) groupSick = true;
                    if (other.DevelopmentalStage.Adult() || !Related(pawn, other, lordGroup)) continue;
                    if (other.Downed || Sick(other)) youngSick = true;
                    else if (other.needs != null && other.needs.food != null && other.needs.food.CurCategory >= HungerCategory.Hungry)
                        hungry = true;
                }
                return new GroupCache(hungry && !youngSick, youngSick, groupSick);
            }

            static bool Related(Pawn pawn, Pawn other, bool lordGroup)
            {
                if (pawn.Faction != null && pawn.Faction == other.Faction) return true;
                if (lordGroup && other.GetLord() == pawn.GetLord()) return true;
                return pawn.Faction == Faction.OfPlayer && (other.IsColonist || other.IsPrisonerOfColony || other.IsSlaveOfColony);
            }
        }
    }

    static class RhahOriginReader
    {
        static Func<Pawn, object> read;
        static bool bound;

        public static object Read(Pawn pawn)
        {
            if (!bound)
            {
                bound = true;
                read = Bind();
            }
            if (read == null || pawn == null) return null;
            try { return read(pawn); }
            catch (Exception) { return null; }
        }

        public static object Member(object snapshot, string name)
        {
            if (snapshot == null || name == null) return null;
            PropertyInfo property = snapshot.GetType().GetProperty(name);
            if (property == null) return null;
            try { return property.GetValue(snapshot, null); }
            catch (Exception) { return null; }
        }

        static Func<Pawn, object> Bind()
        {
            if (ModLister.GetActiveModWithIdentifier("nanaloveyuki.ratkin.hungerandhavoc", true) == null) return null;
            Type api = null;
            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i].GetName().Name != "HungerAndHavoc.Api") continue;
                api = loaded[i].GetType("HungerAndHavoc.Api.RHAH_Api");
                break;
            }
            if (api == null) return null;
            MethodInfo method = api.GetMethod("Get", new[] { typeof(Pawn) });
            if (method == null) return null;
            return pawn => method.Invoke(null, new object[] { pawn });
        }
    }
}
