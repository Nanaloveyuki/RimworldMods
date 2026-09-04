using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Things
{
    internal static class MergeableCandidateCache
    {
        private struct StackKey : IEquatable<StackKey>
        {
            public readonly ThingDef Def;
            public readonly ThingDef Stuff;

            public StackKey(Thing thing)
            {
                Def = thing.def;
                Stuff = thing.Stuff;
            }

            public bool Equals(StackKey other)
            {
                return ReferenceEquals(Def, other.Def) && ReferenceEquals(Stuff, other.Stuff);
            }

            public override bool Equals(object obj)
            {
                return obj is StackKey && Equals((StackKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Def) * 397)
                        ^ (Stuff == null ? 0 : RuntimeHelpers.GetHashCode(Stuff));
                }
            }
        }

        private sealed class GroupCache
        {
            public readonly Dictionary<StackKey, List<Thing>> Buckets =
                new Dictionary<StackKey, List<Thing>>();
            public int[] CellIndices = Array.Empty<int>();
            public int[] ListVersions = Array.Empty<int>();
            public int MapRevision;
            public int ValidatedTick = -1;
            public bool Initialized;

            public bool Matches(ISlotGroup group, Map map)
            {
                List<IntVec3> cells = group.CellsList;
                if (cells.Count != CellIndices.Length)
                    return false;

                for (int i = 0; i < cells.Count; i++)
                {
                    IntVec3 cell = cells[i];
                    if (!cell.InBounds(map)
                        || map.cellIndices.CellToIndex(cell) != CellIndices[i])
                    {
                        return false;
                    }

                    int version;
                    if (!TryGetVersion(map.thingGrid.ThingsListAtFast(cell), out version)
                        || version != ListVersions[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public bool Rebuild(ISlotGroup group, Map map, int mapRevision, int tick)
            {
                Buckets.Clear();
                List<IntVec3> cells = group.CellsList;
                if (CellIndices.Length != cells.Count)
                {
                    CellIndices = new int[cells.Count];
                    ListVersions = new int[cells.Count];
                }
                for (int i = 0; i < cells.Count; i++)
                {
                    IntVec3 cell = cells[i];
                    int version;
                    if (!cell.InBounds(map)
                        || !TryGetVersion(map.thingGrid.ThingsListAtFast(cell), out version))
                    {
                        Initialized = false;
                        return false;
                    }

                    CellIndices[i] = map.cellIndices.CellToIndex(cell);
                    ListVersions[i] = version;
                }

                foreach (Thing thing in group.HeldThings)
                {
                    var key = new StackKey(thing);
                    List<Thing> bucket;
                    if (!Buckets.TryGetValue(key, out bucket))
                    {
                        bucket = new List<Thing>();
                        Buckets.Add(key, bucket);
                    }
                    bucket.Add(thing);
                }

                MapRevision = mapRevision;
                ValidatedTick = tick;
                Initialized = true;
                return true;
            }
        }

        private static readonly ConditionalWeakTable<object, GroupCache> Groups =
            new ConditionalWeakTable<object, GroupCache>();

        private static readonly Dictionary<Type, bool> StackContractByType =
            new Dictionary<Type, bool>();

        private static readonly FieldInfo ListVersionField =
            AccessTools.Field(typeof(List<Thing>), "_version");

        private static readonly Assembly RimWorldAssembly = typeof(Thing).Assembly;

        public static IEnumerable<Thing> GetCandidates(ISlotGroup group, Thing source)
        {
            SaveYourStorageSettings settings = SaveYourStorageMod.Settings;
            Map map = source == null ? null : source.Map;
            if (settings == null
                || !settings.mergeCandidateBuckets
                || group == null
                || source == null
                || map == null
                || ListVersionField == null
                || !HasVanillaStackContract(source.GetType())
                || !HasVanillaSlotGroupContract(group.GetType()))
            {
                return group == null ? Array.Empty<Thing>() : group.HeldThings;
            }

            int tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            int mapRevision = ThingGridMutationTracker.RevisionFor(map);
            GroupCache cache = Groups.GetOrCreateValue(group);
            bool valid = cache.Initialized
                && cache.ValidatedTick == tick
                && cache.MapRevision == mapRevision;

            if (!valid)
            {
                if (!cache.Initialized || !cache.Matches(group, map))
                {
                    if (!cache.Rebuild(group, map, mapRevision, tick))
                        return group.HeldThings;
                }
                else
                {
                    cache.MapRevision = mapRevision;
                    cache.ValidatedTick = tick;
                }
            }

            List<Thing> candidates;
            return cache.Buckets.TryGetValue(new StackKey(source), out candidates)
                ? candidates
                : Array.Empty<Thing>();
        }

        private static bool HasVanillaStackContract(Type type)
        {
            bool supported;
            if (StackContractByType.TryGetValue(type, out supported))
                return supported;

            MethodInfo method = type.GetMethod(
                nameof(Thing.CanStackWith),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Thing) },
                null);
            MethodInfo baseMethod = AccessTools.Method(
                typeof(Thing),
                nameof(Thing.CanStackWith),
                new[] { typeof(Thing) });
            supported = method != null
                && method.DeclaringType.Assembly == RimWorldAssembly
                && !HasHarmonyPatches(method)
                && (ReferenceEquals(method, baseMethod) || !HasHarmonyPatches(baseMethod));
            StackContractByType[type] = supported;
            return supported;
        }

        private static bool HasHarmonyPatches(MethodBase method)
        {
            Patches patches = method == null ? null : Harmony.GetPatchInfo(method);
            return patches != null && patches.Owners.Count > 0;
        }

        private static bool HasVanillaSlotGroupContract(Type type)
        {
            return type.Assembly == RimWorldAssembly
                && (typeof(SlotGroup).IsAssignableFrom(type)
                    || typeof(StorageGroup).IsAssignableFrom(type));
        }

        private static bool TryGetVersion(List<Thing> things, out int version)
        {
            object value = ListVersionField.GetValue(things);
            if (value is int)
            {
                version = (int)value;
                return true;
            }

            version = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Merge), nameof(WorkGiver_Merge.JobOnThing))]
    internal static class Patch_WorkGiver_Merge_JobOnThing
    {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var rewritten = new List<CodeInstruction>(original.Count + 1);
            MethodInfo getter = AccessTools.PropertyGetter(
                typeof(ISlotGroup),
                nameof(ISlotGroup.HeldThings));
            MethodInfo replacement = AccessTools.Method(
                typeof(MergeableCandidateCache),
                nameof(MergeableCandidateCache.GetCandidates));
            int replacements = 0;

            for (int i = 0; i < original.Count; i++)
            {
                CodeInstruction instruction = original[i];
                if (getter != null && instruction.Calls(getter))
                {
                    var loadSource = new CodeInstruction(OpCodes.Ldarg_2);
                    loadSource.labels.AddRange(instruction.labels);
                    loadSource.blocks.AddRange(instruction.blocks);
                    instruction.labels.Clear();
                    instruction.blocks.Clear();
                    rewritten.Add(loadSource);
                    rewritten.Add(new CodeInstruction(OpCodes.Call, replacement));
                    replacements++;
                }
                else
                {
                    rewritten.Add(instruction);
                }
            }

            if (replacements == 1 && replacement != null)
                return rewritten;

            Log.Warning(
                "[SaveYourStorage] WorkGiver_Merge candidate cache could not find its "
                + "expected call site; the original method remains unchanged.");
            return original;
        }
    }
}
