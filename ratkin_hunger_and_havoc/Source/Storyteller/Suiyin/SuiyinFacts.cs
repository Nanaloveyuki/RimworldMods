using System.Collections.Generic;
namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum SuiyinNode
    {
        N001 = 1,
        N002 = 2,
        N003 = 3,
        N004 = 4,
        N005 = 5,
        N006 = 6,
        N008 = 8,
        N009 = 9,
        N010 = 10,
        R01 = 11
    }

    internal readonly struct SuiyinIncidentFact
    {
        internal readonly string DisplayId;
        internal readonly int MapId;
        internal readonly int Tick;
        internal readonly int BatchId;
        internal readonly int PawnCount;

        internal SuiyinIncidentFact(string displayId, int mapId, int tick, int batchId, int pawnCount)
        {
            DisplayId = displayId;
            MapId = mapId;
            Tick = tick;
            BatchId = batchId;
            PawnCount = pawnCount;
        }
    }


    internal static class SuiyinNodes
    {
        internal const int Count = 11;
        internal const int TrustMin = -100;
        internal const int TrustMax = 100;

        internal static int Index(SuiyinNode node)
        {
            return (int)node - 1;
        }

        internal static bool Known(SuiyinNode node)
        {
            int index = (int)node;
            return index >= 1 && index <= Count && index != 7;
        }

        internal static int ClampTrust(int trust)
        {
            if (trust < TrustMin)
            {
                return TrustMin;
            }

            if (trust > TrustMax)
            {
                return TrustMax;
            }

            return trust;
        }
    }

    internal sealed class SuiyinLedger
    {
        bool[] enabled = Fill(true);
        bool[] started = Fill(false);
        int[] deadlineTick = Fill(-1);

        internal bool Enabled(SuiyinNode node)
        {
            return SuiyinNodes.Known(node) && enabled[SuiyinNodes.Index(node)];
        }

        internal bool Started(SuiyinNode node)
        {
            return SuiyinNodes.Known(node) && started[SuiyinNodes.Index(node)];
        }

        internal int Deadline(SuiyinNode node)
        {
            return SuiyinNodes.Known(node) ? deadlineTick[SuiyinNodes.Index(node)] : -1;
        }

        internal bool Allows(SuiyinNode node, int tick)
        {
            if (!SuiyinNodes.Known(node))
            {
                return false;
            }

            int index = SuiyinNodes.Index(node);
            if (started[index])
            {
                return deadlineTick[index] < 0 || tick <= deadlineTick[index];
            }

            return enabled[index];
        }

        internal void Start(SuiyinNode node, int deadline)
        {
            if (!SuiyinNodes.Known(node))
            {
                return;
            }

            int index = SuiyinNodes.Index(node);
            if (started[index])
            {
                return;
            }

            started[index] = true;
            deadlineTick[index] = deadline;
        }

        internal void SetEnabled(SuiyinNode node, bool value)
        {
            if (!SuiyinNodes.Known(node) || started[SuiyinNodes.Index(node)])
            {
                return;
            }

            enabled[SuiyinNodes.Index(node)] = value;
        }

        internal void Export(List<bool> enabledOut, List<bool> startedOut, List<int> deadlineOut)
        {
            Write(enabled, enabledOut);
            Write(started, startedOut);
            Write(deadlineTick, deadlineOut);
        }

        internal void Import(List<bool> enabledIn, List<bool> startedIn, List<int> deadlineIn)
        {
            enabled = Read(enabledIn, true);
            started = Read(startedIn, false);
            deadlineTick = Read(deadlineIn, -1);
        }

        static void Write<T>(T[] source, List<T> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        static T[] Read<T>(List<T> source, T fill)
        {
            T[] next = Fill(fill);
            if (source == null)
            {
                return next;
            }

            int count = source.Count < next.Length ? source.Count : next.Length;
            for (int i = 0; i < count; i++)
            {
                next[i] = source[i];
            }

            return next;
        }

        internal static T[] Fill<T>(T value)
        {
            T[] values = new T[SuiyinNodes.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = value;
            }

            return values;
        }

        static T[] Normalize<T>(T[] source, T fill)
        {
            T[] next = Fill(fill);
            if (source == null)
            {
                return next;
            }

            int count = source.Length < next.Length ? source.Length : next.Length;
            for (int i = 0; i < count; i++)
            {
                next[i] = source[i];
            }

            return next;
        }

        static void Copy<T>(T[] source, T[] destination)
        {
            if (destination == null)
            {
                return;
            }

            int count = source.Length < destination.Length ? source.Length : destination.Length;
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }
    }
}
