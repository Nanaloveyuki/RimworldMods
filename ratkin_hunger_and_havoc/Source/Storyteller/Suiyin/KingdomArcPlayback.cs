using System;
using System.Globalization;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal sealed class KingdomArcPlayback
    {
        private readonly int lastIndex;
        private readonly int choiceIndex;
        private string text = string.Empty;
        private int[] textElements = Array.Empty<int>();
        private int visibleElements;
        private double revealElapsed;
        private double holdElapsed;
        internal const double CharactersPerSecond = 34d;
        internal const double ReadPauseSeconds = 2.5d;

        internal int Index { get; private set; }
        internal string VisibleText { get; private set; } = string.Empty;
        internal bool TextComplete => visibleElements == textElements.Length;

        internal KingdomArcPlayback(int index, int count, int choiceIndex)
        {
            lastIndex = count - 1;
            this.choiceIndex = choiceIndex;
            MoveTo(index);
        }

        internal void MoveTo(int index)
        {
            Index = Math.Max(0, Math.Min(lastIndex, index));
            SetText(string.Empty);
        }

        internal void SetText(string value)
        {
            text = value;
            textElements = StringInfo.ParseCombiningCharacters(text);
            visibleElements = 0;
            revealElapsed = 0d;
            holdElapsed = 0d;
            VisibleText = string.Empty;
        }

        internal void RevealAll()
        {
            visibleElements = textElements.Length;
            VisibleText = text;
            holdElapsed = 0d;
        }

        internal bool Advance(double deltaSeconds, double durationSeconds, bool running, bool fast, bool automatic)
        {
            if (!running || deltaSeconds <= 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds)) return false;
            double delta = deltaSeconds * (fast ? 4d : 1d);
            if (!TextComplete)
            {
                revealElapsed += delta;
                int count = (int)Math.Min(textElements.Length, revealElapsed * CharactersPerSecond);
                if (count != visibleElements)
                {
                    visibleElements = count;
                    VisibleText = TextComplete ? text : text.Substring(0, textElements[count]);
                }
                // 完整显示后另起等待，不补算这一帧跨过的时间
                return false;
            }
            if (!automatic || Index >= choiceIndex || durationSeconds <= 0d) return false;
            holdElapsed += delta;
            if (holdElapsed < ReadPauseSeconds) return false;
            MoveTo(Index + 1);
            return true;
        }
    }
}
