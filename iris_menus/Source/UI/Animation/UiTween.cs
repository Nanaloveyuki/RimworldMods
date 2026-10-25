using System;

namespace IrisMenus.UI
{
    public enum UiEase { Standard, Exit, Iris }

    public static class UiMotion
    {
        public const float Fast = 0.2f;
        public const float Standard = 0.2f;
        public const float Enter = 0.3f;
        public const float Exit = 0.2f;
        public const float Sheet = 0.22f;

        public static float Lerp(float from, float to, float progress) => from + (to - from) * progress;

        internal static float Ease(float progress, UiEase ease)
        {
            if (progress <= 0f || progress >= 1f) return progress;
            float x1 = ease == UiEase.Exit ? 0f : ease == UiEase.Iris ? 0.2f : 0.25f;
            float y1 = ease == UiEase.Exit ? 0f : ease == UiEase.Iris ? 0.7f : 0.1f;
            float x2 = ease == UiEase.Exit ? 0.58f : ease == UiEase.Iris ? 0.2f : 0.25f;
            float t = progress, low = 0f, high = 1f;
            // Solve the CSS curve's time axis; t itself is not elapsed progress.
            for (int i = 0; i < 10; i++)
            {
                float inverse = 1f - t;
                float x = 3f * inverse * inverse * t * x1 + 3f * inverse * t * t * x2 + t * t * t;
                float error = x - progress;
                if (Math.Abs(error) < 0.00001f) break;
                if (error > 0f) high = t; else low = t;
                float slope = 3f * inverse * inverse * x1 + 6f * inverse * t * (x2 - x1) + 3f * t * t * (1f - x2);
                float next = slope > 0.00001f ? t - error / slope : -1f;
                t = next > low && next < high ? next : (low + high) * 0.5f;
            }
            float remaining = 1f - t;
            return 3f * remaining * remaining * t * y1 + 3f * remaining * t * t + t * t * t;
        }
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
        private UiEase easing;
        private bool initialized;

        public UiTween(float initial)
        {
            origin = value = target = initial;
            started = sampled = 0d;
            duration = 0f;
            easing = UiEase.Standard;
            initialized = false;
        }

        public float Value => value;
        public float Target => target;
        public bool IsRunning => value != target;

        public float Step(float next, double time, float seconds, bool reducedMotion = false, UiEase ease = UiEase.Standard)
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
                easing = ease;
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
            value = progress >= 1f ? target : UiMotion.Lerp(origin, target, UiMotion.Ease(progress, easing));
        }
    }
}
