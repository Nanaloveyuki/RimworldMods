namespace HungerAndHavoc.Pawn
{
    internal readonly struct ClayBite
    {
        internal ClayBite(bool accepted, int count, float severity)
        {
            Accepted = accepted;
            Count = count;
            Severity = severity;
        }

        internal bool Accepted { get; }
        internal int Count { get; }
        internal float Severity { get; }
    }

    internal static class RHAH_ClaySatiety
    {
        internal const int MaxBites = 3;
        internal const int WindowDays = 15;
        internal const float SeverityPerBite = 0.33f;
        internal const float DecayPerDay = 0.1f;
        internal const float MinimumSeverity = 0.001f;
        internal const float HungerRateFactor = 0f;
        internal static int Limit => System.Math.Max(1, System.Math.Min(MaxBites,
            Core.RHAH_Mod.Settings == null ? MaxBites : Core.RHAH_Mod.Settings.clayMaxBites));

        internal static int WindowTicks(int ticksPerDay)
        {
            if (ticksPerDay <= 0)
            {
                return 0;
            }

            int days = Core.RHAH_Mod.Settings == null ? WindowDays : Core.RHAH_Mod.Settings.clayWindowDays;
            return ticksPerDay * days;
        }

        internal static ClayBite Register(int tick, int windowStart, int count, float severity, int ticksPerDay)
        {
            int start = FreshStart(tick, windowStart, ticksPerDay);
            int used = start == windowStart ? count : 0;
            int maxBites = Limit;
            if (used >= maxBites)
            {
                return new ClayBite(false, used, severity);
            }

            if (start < 0)
            {
                start = tick;
            }

            float step = Core.RHAH_Mod.Settings == null ? SeverityPerBite : Core.RHAH_Mod.Settings.claySeverityPerBite;
            float next = severity + step;
            if (next > 1f)
            {
                next = 1f;
            }

            if (next < step)
            {
                next = step;
            }

            return new ClayBite(true, used + 1, next);
        }

        internal static int FreshStart(int tick, int windowStart, int ticksPerDay)
        {
            int window = WindowTicks(ticksPerDay);
            if (windowStart < 0 || window <= 0)
            {
                return -1;
            }

            if ((long)tick >= (long)windowStart + window)
            {
                return -1;
            }

            return windowStart;
        }

        internal static float Decay(float severity, int delta, int ticksPerDay)
        {
            if (delta <= 0 || ticksPerDay <= 0)
            {
                return severity;
            }

            float next = severity - DecayPerDay * delta / ticksPerDay;
            return next < MinimumSeverity ? MinimumSeverity : next;
        }

        internal static bool ShouldRemove(float severity, int count, int windowStart)
        {
            return severity <= MinimumSeverity && count <= 0 && windowStart < 0;
        }
    }
}
