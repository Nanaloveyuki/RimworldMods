using System;
using System.Collections.Generic;

namespace HungerAndHavoc.Api
{
    public static class RHAH_PawnBehaviors
    {
        static readonly List<IRHAH_PawnBehavior> handlers = new List<IRHAH_PawnBehavior>();

        public static void Register(IRHAH_PawnBehavior behavior)
        {
            if (behavior == null || handlers.Contains(behavior))
            {
                return;
            }

            handlers.Add(behavior);
        }

        public static void Unregister(IRHAH_PawnBehavior behavior)
        {
            if (behavior == null)
            {
                return;
            }

            handlers.Remove(behavior);
        }

        internal static bool? Query(Func<IRHAH_PawnBehavior, bool?> ask)
        {
            if (ask == null)
            {
                return null;
            }

            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                bool? result = ask(handlers[i]);
                if (result.HasValue)
                {
                    return result;
                }
            }

            return null;
        }

        internal static void ResetForTests()
        {
            handlers.Clear();
        }
    }
}
