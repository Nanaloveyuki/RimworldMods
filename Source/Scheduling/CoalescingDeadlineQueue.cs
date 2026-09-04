using System;
using System.Collections.Generic;

namespace SaveYourStorageAndTickEveryMinute.Scheduling
{
    /// <summary>
    /// Keeps at most one scheduled item per key. Rescheduling removes the old
    /// linked-list node, so repeated refreshes do not leave stale queue tokens.
    /// </summary>
    internal sealed class CoalescingDeadlineQueue<TKey, TWork>
    {
        private sealed class Entry
        {
            public TKey Key;
            public long DueTick;
            public TWork Work;
            public LinkedListNode<Entry> Node;
        }

        private readonly IEqualityComparer<TKey> keyComparer;
        private readonly Dictionary<TKey, Entry> entries;
        private readonly SortedDictionary<long, LinkedList<Entry>> buckets =
            new SortedDictionary<long, LinkedList<Entry>>();

        public CoalescingDeadlineQueue(IEqualityComparer<TKey> keyComparer = null)
        {
            this.keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            entries = new Dictionary<TKey, Entry>(this.keyComparer);
        }

        public int Count => entries.Count;

        public bool HasDue(long currentTick)
        {
            long dueTick;
            LinkedList<Entry> bucket;
            return TryGetFirstBucket(out dueTick, out bucket) && dueTick <= currentTick;
        }

        public void Schedule(TKey key, long dueTick, TWork work)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            Entry entry;
            if (entries.TryGetValue(key, out entry))
            {
                RemoveFromBucket(entry);
                entry.DueTick = dueTick;
                entry.Work = work;
            }
            else
            {
                entry = new Entry
                {
                    Key = key,
                    DueTick = dueTick,
                    Work = work
                };
                entries.Add(key, entry);
            }

            AddToBucket(entry);
        }

        public bool Cancel(TKey key)
        {
            Entry entry;
            if (key == null || !entries.TryGetValue(key, out entry))
                return false;

            RemoveFromBucket(entry);
            entries.Remove(key);
            return true;
        }

        public bool TryTakeDue(long currentTick, out TKey key, out TWork work)
        {
            key = default(TKey);
            work = default(TWork);

            long dueTick;
            LinkedList<Entry> bucket;
            if (!TryGetFirstBucket(out dueTick, out bucket) || dueTick > currentTick)
                return false;

            Entry entry = bucket.First.Value;
            bucket.RemoveFirst();
            if (bucket.Count == 0)
                buckets.Remove(dueTick);

            entry.Node = null;
            entries.Remove(entry.Key);
            key = entry.Key;
            work = entry.Work;
            return true;
        }

        public void Clear()
        {
            entries.Clear();
            buckets.Clear();
        }

        private void AddToBucket(Entry entry)
        {
            LinkedList<Entry> bucket;
            if (!buckets.TryGetValue(entry.DueTick, out bucket))
            {
                bucket = new LinkedList<Entry>();
                buckets.Add(entry.DueTick, bucket);
            }
            entry.Node = bucket.AddLast(entry);
        }

        private void RemoveFromBucket(Entry entry)
        {
            LinkedList<Entry> bucket;
            if (entry.Node == null || !buckets.TryGetValue(entry.DueTick, out bucket))
                return;

            bucket.Remove(entry.Node);
            entry.Node = null;
            if (bucket.Count == 0)
                buckets.Remove(entry.DueTick);
        }

        private bool TryGetFirstBucket(out long dueTick, out LinkedList<Entry> bucket)
        {
            using (IEnumerator<KeyValuePair<long, LinkedList<Entry>>> enumerator =
                buckets.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    dueTick = enumerator.Current.Key;
                    bucket = enumerator.Current.Value;
                    return true;
                }
            }

            dueTick = 0L;
            bucket = null;
            return false;
        }
    }
}
