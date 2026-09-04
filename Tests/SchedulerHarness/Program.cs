using System;
using System.Collections.Generic;
using System.Threading;
using SaveYourStorageAndTickEveryMinute.Scheduling;

internal static class Program
{
    private static int Main()
    {
        try
        {
            RescheduleCoalescesByKey();
            DueWorkRunsInDeadlineOrder();
            CancellationRemovesWork();
            ItemBudgetLeavesBacklog();
            DiscardedWorkIsCounted();
            WorkCanRescheduleItsOwnKey();
            FutureWorkDoesNotExhaustCurrentBudget();
            TimeBudgetStillAdvancesOneItem();
            Console.WriteLine("Scheduler harness passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RescheduleCoalescesByKey()
    {
        var queue = new CoalescingDeadlineQueue<string, int>();
        queue.Schedule("entry", 10, 1);
        queue.Schedule("entry", 20, 2);
        Equal(1, queue.Count, "reschedule count");
        False(queue.TryTakeDue(10, out _, out _), "old deadline must be removed");
        True(queue.TryTakeDue(20, out var key, out var value), "new deadline must run");
        Equal("entry", key, "rescheduled key");
        Equal(2, value, "rescheduled work");
    }

    private static void DueWorkRunsInDeadlineOrder()
    {
        var queue = new CoalescingDeadlineQueue<string, int>();
        queue.Schedule("later", 20, 2);
        queue.Schedule("first", 10, 1);
        True(queue.TryTakeDue(20, out var firstKey, out _), "first item missing");
        True(queue.TryTakeDue(20, out var secondKey, out _), "second item missing");
        Equal("first", firstKey, "deadline order first");
        Equal("later", secondKey, "deadline order second");
    }

    private static void CancellationRemovesWork()
    {
        var queue = new CoalescingDeadlineQueue<string, int>();
        queue.Schedule("cancel", 1, 1);
        True(queue.Cancel("cancel"), "cancel existing item");
        False(queue.Cancel("cancel"), "cancel missing item");
        False(queue.TryTakeDue(1, out _, out _), "cancelled item must not run");
    }

    private static void ItemBudgetLeavesBacklog()
    {
        var scheduler = new BudgetedWorkScheduler<string>();
        var executed = new List<string>();
        scheduler.Schedule("a", 1, () => { executed.Add("a"); return true; });
        scheduler.Schedule("b", 1, () => { executed.Add("b"); return true; });
        scheduler.Schedule("c", 1, () => { executed.Add("c"); return true; });

        DrainResult first = scheduler.Drain(1, 2, 0d);
        Equal(2, first.Completed, "first drain completed");
        Equal(1, scheduler.Count, "first drain backlog");
        True(first.BudgetExhausted, "item budget must report exhaustion");

        DrainResult second = scheduler.Drain(1, 2, 0d);
        Equal(1, second.Completed, "second drain completed");
        Equal(0, scheduler.Count, "second drain backlog");
        Equal(3, executed.Count, "all work executed");
    }

        private static void DiscardedWorkIsCounted()
    {
        var scheduler = new BudgetedWorkScheduler<string>();
        scheduler.Schedule("stale", 1, () => false);
        DrainResult result = scheduler.Drain(1, 1, 0d);
        Equal(0, result.Completed, "discarded completed count");
            Equal(1, result.Discarded, "discarded count");
        }

        private static void WorkCanRescheduleItsOwnKey()
        {
            var scheduler = new BudgetedWorkScheduler<string>();
            int calls = 0;
            BudgetedWorkScheduler<string>.WorkItem work = null;
            work = () =>
            {
                calls++;
                if (calls == 1)
                {
                    scheduler.Schedule("retry", 2, work);
                    return false;
                }
                return true;
            };

            scheduler.Schedule("retry", 1, work);
            DrainResult first = scheduler.Drain(1, 8, 0d);
            Equal(1, first.Discarded, "rescheduled first attempt");
            Equal(1, scheduler.Count, "rescheduled work remains queued");

            DrainResult second = scheduler.Drain(2, 8, 0d);
            Equal(1, second.Completed, "rescheduled second attempt");
            Equal(2, calls, "rescheduled work call count");
        }

        private static void FutureWorkDoesNotExhaustCurrentBudget()
        {
            var scheduler = new BudgetedWorkScheduler<string>();
            scheduler.Schedule("future", 10, () => true);

            DrainResult result = scheduler.Drain(1, 1, 0d);
            Equal(0, result.Completed, "future work must not execute");
            False(result.BudgetExhausted, "future work is not current-tick backlog");
            Equal(1, scheduler.Count, "future work remains queued");
        }

        private static void TimeBudgetStillAdvancesOneItem()
        {
            var scheduler = new BudgetedWorkScheduler<string>();
            scheduler.Schedule("slow", 1, () =>
            {
                Thread.Sleep(5);
                return true;
            });
            scheduler.Schedule("backlog", 1, () => true);

            DrainResult result = scheduler.Drain(1, 8, 0.1d);
            Equal(1, result.Completed, "time budget guarantees one item of progress");
            True(result.BudgetExhausted, "due backlog must report time exhaustion");
            Equal(1, scheduler.Count, "time budget leaves due backlog queued");
        }

    private static void True(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException("Assertion failed: " + message);
    }

    private static void False(bool value, string message)
    {
        True(!value, message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                "Assertion failed: " + message + ". Expected " + expected + ", got " + actual + ".");
        }
    }
}
