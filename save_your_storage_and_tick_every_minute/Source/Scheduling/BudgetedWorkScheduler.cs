using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SaveYourStorageAndTickEveryMinute.Scheduling
{
    internal readonly struct DrainResult
    {
        public DrainResult(int completed, int discarded, bool budgetExhausted)
        {
            Completed = completed;
            Discarded = discarded;
            BudgetExhausted = budgetExhausted;
        }

        public int Completed { get; }
        public int Discarded { get; }
        public bool BudgetExhausted { get; }
    }

    internal sealed class BudgetedWorkScheduler<TKey>
    {
        internal delegate bool WorkItem();

        private readonly CoalescingDeadlineQueue<TKey, WorkItem> queue;

        public BudgetedWorkScheduler(IEqualityComparer<TKey> keyComparer = null)
        {
            queue = new CoalescingDeadlineQueue<TKey, WorkItem>(keyComparer);
        }

        public int Count => queue.Count;

        public void Schedule(TKey key, long dueTick, WorkItem work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));
            queue.Schedule(key, dueTick, work);
        }

        public bool Cancel(TKey key)
        {
            return queue.Cancel(key);
        }

        public DrainResult Drain(long currentTick, int maximumItems, double maximumMilliseconds)
        {
            if (maximumItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumItems));
            if (maximumMilliseconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(maximumMilliseconds));

            int completed = 0;
            int discarded = 0;
            bool budgetExhausted = false;
            long started = Stopwatch.GetTimestamp();
            long allowedTicks = maximumMilliseconds <= 0d
                ? long.MaxValue
                : (long)(Stopwatch.Frequency * (maximumMilliseconds / 1000d));

            while (completed + discarded < maximumItems)
            {
                if (completed + discarded > 0
                    && Stopwatch.GetTimestamp() - started >= allowedTicks)
                {
                    budgetExhausted = true;
                    break;
                }

                TKey key;
                WorkItem work;
                if (!queue.TryTakeDue(currentTick, out key, out work))
                    break;

                if (work())
                    completed++;
                else
                    discarded++;
            }

            if (!budgetExhausted
                && completed + discarded >= maximumItems
                && queue.HasDue(currentTick))
                budgetExhausted = true;

            return new DrainResult(completed, discarded, budgetExhausted);
        }

        public void Clear()
        {
            queue.Clear();
        }
    }
}
