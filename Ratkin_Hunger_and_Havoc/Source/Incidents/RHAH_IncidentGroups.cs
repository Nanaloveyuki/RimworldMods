using System;
using System.Collections.Generic;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_IncidentGroups
    {
        static readonly RHAH_IncidentEntry[][] groups =
        {
            Resolve("I-001", "I-005", "I-042"),
            Resolve("I-004"),
            Resolve("I-011", "I-040"),
            Resolve("I-015", "I-016", "I-017", "I-018"),
            Resolve("I-020", "I-021", "I-022", "I-023", "I-024", "I-025", "I-026", "I-027", "I-028"),
            Resolve("I-006", "I-007", "I-043"),
            Resolve("I-008", "I-009", "I-010", "I-036", "I-041", "I-008-01", "I-009-01", "I-010-01"),
            Resolve("I-002", "I-037"),
            Resolve("I-003"),
            Resolve("I-012", "I-038"),
            Resolve("I-013"),
            Resolve("I-014", "I-030", "I-045"),
            Resolve("I-029", "I-044"),
            Resolve("I-031", "I-039"),
            Resolve("I-032", "I-046"),
            Resolve("I-033", "I-047"),
            Resolve("I-034", "I-048"),
            Resolve("I-035", "I-050"),
            Resolve("I-049"),
            Resolve("I-051"),
            Resolve("I-052"),
            Resolve("I-053"),
            Resolve("I-054")
        };
        static readonly Dictionary<string, int> groupByDisplayId = IndexGroups();
        static readonly string[] labelKeys = BuildLabelKeys();

        internal static int Count => groups.Length;

        internal static IReadOnlyList<RHAH_IncidentEntry> Scenarios(int group) => groups[group];

        internal static string LabelKey(int group) => labelKeys[group];

        internal static RHAH_AttitudePool PoolOf(int group) => groups[group][0].DefaultAttitudePool;

        internal static RHAH_AttitudePool PoolOf(string displayId)
        {
            int group = GroupOf(displayId);
            return group < 0 ? RHAH_AttitudePool.Negative : PoolOf(group);
        }

        internal static int GroupOf(string displayId)
        {
            return displayId != null && groupByDisplayId.TryGetValue(displayId, out int group) ? group : -1;
        }

        static string[] BuildLabelKeys()
        {
            string[] keys = new string[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                keys[i] = "RHAH_IncidentGroup_" + groups[i][0].DisplayId.Replace("-", string.Empty);
            }
            return keys;
        }

        static Dictionary<string, int> IndexGroups()
        {
            Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < groups.Length; i++)
            {
                for (int j = 0; j < groups[i].Length; j++)
                {
                    index.Add(groups[i][j].DisplayId, i);
                }
            }
            return index;
        }

        static RHAH_IncidentEntry[] Resolve(params string[] ids)
        {
            RHAH_IncidentEntry[] entries = new RHAH_IncidentEntry[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                entries[i] = RHAH_IncidentCatalog.GetByDisplayId(ids[i]);
                if (entries[i] == null)
                {
                    throw new InvalidOperationException("Unknown RHAH incident scenario: " + ids[i]);
                }
            }
            return entries;
        }
    }
}
