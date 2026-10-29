using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// A dedicated play need for children aged 3–13.
    ///
    /// Decays over ~7 days (configurable via mod settings). When children engage
    /// in play activities, this need fills up. Low play levels trigger mood
    /// debuffs and reduce growth point gain.
    ///
    /// Mirrors vanilla <see cref="Need_Learning"/> in structure but with a
    /// warmer color scheme and child-specific behavior.
    /// </summary>
    public class Need_ChildPlay : Need
    {
        /// <summary>
        /// UNUSED legacy constant (the old fixed 10-day decay rate). Actual decay is
        /// computed in <see cref="NeedInterval"/> from TPE_Settings.playNeedDecayDays
        /// (default 6 days, slider 3–14).
        /// </summary>
        public const float DefaultFallPerInterval = 0.00025f;

        /// <summary>Threshold below which the need is considered empty.</summary>
        public const float ThresholdEmpty = 0.01f;

        /// <summary>Threshold below which the need is considered very low.</summary>
        public const float ThresholdLow = 0.15f;

        /// <summary>Threshold below which the need is considered low.</summary>
        public const float ThresholdSatisfied = 0.3f;

        /// <summary>Threshold above which the need is considered high.</summary>
        public const float ThresholdHigh = 0.7f;

        /// <summary>Threshold above which the need is considered very high.</summary>
        public const float ThresholdVeryHigh = 0.85f;

        /// <summary>Returns true when play need is at or below the low threshold.</summary>
        public bool IsLow => CurLevelPercentage <= ThresholdLow;

        /// <summary>
        /// Categorizes the current play level into discrete tiers for UI and
        /// thought system consumption.
        /// </summary>
        public PlayCategory CurCategory
        {
            get
            {
                if (CurLevel < ThresholdEmpty)
                    return PlayCategory.Empty;
                if (CurLevel < ThresholdLow)
                    return PlayCategory.VeryLow;
                if (CurLevel < ThresholdSatisfied)
                    return PlayCategory.Low;
                if (CurLevel < ThresholdHigh)
                    return PlayCategory.Satisfied;
                if (CurLevel < ThresholdVeryHigh)
                    return PlayCategory.High;
                return PlayCategory.Extreme;
            }
        }

        /// <summary>
        /// Constructs the play need and sets up threshold markers for the UI bar.
        /// </summary>
        public Need_ChildPlay(Pawn pawn)
            : base(pawn)
        {
            threshPercents = new List<float> { ThresholdLow, ThresholdSatisfied, ThresholdHigh, ThresholdVeryHigh };
        }

        /// <summary>
        /// Returns the <see cref="Pawn_PlayTracker"/> for this pawn,
        /// creating it via the game component if necessary.
        /// </summary>
        public Pawn_PlayTracker? PlayTracker
        {
            get
            {
                if (pawn == null || Current.Game == null)
                    return null;
                var comp = Current.Game.GetComponent<GameComponent_PlayTracker>();
                return comp?.GetTracker(pawn);
            }
        }

        /// <summary>
        /// Called when a child completes a play activity. Adds <paramref name="amount"/>
        /// to the current level, clamped to 0–1.
        /// </summary>
        public void Play(float amount)
        {
            if (amount <= 0f)
                return;
            if (PlayTracker?.lockPlayNeed == true)
                return;
            CurLevelPercentage += amount;
            CurLevelPercentage = Mathf.Clamp01(CurLevelPercentage);
        }

        /// <summary>
        /// Called every 150 ticks. Decays the play need based on the mod setting
        /// for decay rate. Also ticks the pawn's play-desire tracker so that
        /// new desires are assigned on schedule.
        /// </summary>
        public override void NeedInterval()
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return;

            if (IsFrozen)
                return;

            // Calculate fall rate from the mod setting (days to drain 0→1).
            // Default is 6 days (configurable 3–14 via mod settings).
            if (PlayTracker?.lockPlayNeed == true)
            {
                CurLevel = 0.5f;
            }
            else
            {
                float days = TPE_Mod.Settings?.playNeedDecayDays ?? 6f;
                // 1.0 need / (days * 60000 ticks/day) * 150 ticks/interval
                float fallPerInterval = 1f / (days * 60000f) * 150f;
                CurLevel -= fallPerInterval;
            }

            // Tick the desire tracker (150 ticks per NeedInterval call).
            PlayTracker?.PlayTickInterval(150);
        }

        /// <summary>
        /// Renders the need bar in the needs tab, drawing active play-desire
        /// icons to the right of the bar exactly like vanilla learning desires.
        /// </summary>
        public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = int.MaxValue,
            float customMargin = -1f, bool drawArrows = true, bool doTooltip = true,
            Rect? rectForTooltip = null, bool drawLabel = true)
        {
            Pawn_PlayTracker? tracker = PlayTracker;
            if (tracker != null)
            {
                List<PlayDesireDef> activeDesires = tracker.ActivePlayDesires;
                if (!activeDesires.NullOrEmpty())
                {
                    float iconTotalWidth = activeDesires.Count * 30f;
                    float iconX = rect.xMax - iconTotalWidth;

                    for (int i = 0; i < activeDesires.Count; i++)
                    {
                        Rect iconRect = new Rect(iconX + i * 30f, rect.y, 30f, 30f);
                        Texture2D? icon = activeDesires[i].Icon;
                        if (icon != null)
                        {
                            GUI.DrawTexture(iconRect.ContractedBy(5f), icon);
                        }

                        if (Mouse.IsOver(iconRect))
                        {
                            Widgets.DrawHighlight(iconRect);
                            // Capture loop variable locally so the lambda closes over the current value, not the mutable index.
                            PlayDesireDef desireForTip = activeDesires[i];
                            TooltipHandler.TipRegion(iconRect,
                                () => desireForTip.LabelCap
                                      .Colorize(ColoredText.TipSectionTitleColor)
                                      + "\n\n"
                                      + desireForTip.description,
                                desireForTip.GetHashCode());
                            doTooltip = false;
                        }
                    }
                }
            }

            base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows,
                doTooltip, rectForTooltip, drawLabel);
        }
    }

    /// <summary>
    /// Discrete categories for the child play need, matching vanilla LearningCategory
    /// naming for UI consistency.
    /// </summary>
    public enum PlayCategory
    {
        Empty,
        VeryLow,
        Low,
        Satisfied,
        High,
        Extreme
    }
}
