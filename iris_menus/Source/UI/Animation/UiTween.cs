using System;

namespace IrisMenus.UI
{
    public static class UiMotion
    {
        public const float Fast = 0.12f;
        public const float Standard = 0.18f;
        public const float Enter = 0.24f;
        public const float Exit = 0.16f;

        public static float Lerp(float from, float to, float progress) => from + (to - from) * progress;
    }

    /// <summary>A real-time transition. Retargeting samples the old curve before changing direction.</summary>
    public struct UiTween
    {
        private float origin;
        private float value;
        private float target;
        private double started;
        private double sampled;
        private float duration;
        private bool initialized;

        public UiTween(float initial)
        {
            origin = value = target = initial;
            started = sampled = 0d;
            duration = 0f;
            initialized = false;
        }

        public float Value => value;
        public float Target => target;
        public bool IsRunning => value != target;

        public float Step(float next, double time, float seconds, bool reducedMotion = false)
        {
            if (float.IsNaN(next) || float.IsInfinity(next)) throw new ArgumentOutOfRangeException(nameof(next));
            if (double.IsNaN(time) || double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!initialized) { sampled = started = time; initialized = true; }
            time = Math.Max(time, sampled);
            Sample(time);
            if (reducedMotion || seconds == 0f) { Snap(next); sampled = started = time; initialized = true; return value; }
            if (target != next)
            {
                origin = value;
                target = next;
                started = time;
                duration = seconds;
            }
            sampled = time;
            return value;
        }

        public void Snap(float next)
        {
            origin = value = target = next;
            duration = 0f;
        }

        private void Sample(double time)
        {
            if (value == target) return;
            float progress = duration <= 0f ? 1f : (float)Math.Min(1d, Math.Max(0d, (time - started) / duration));
            float remaining = 1f - progress;
            value = progress >= 1f ? target : UiMotion.Lerp(origin, target, 1f - remaining * remaining * remaining);
        }
    }
}
