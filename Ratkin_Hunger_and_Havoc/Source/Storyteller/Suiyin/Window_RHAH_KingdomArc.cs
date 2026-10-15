using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal sealed class Window_RHAH_KingdomArc : Window
    {
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Color Ink = new Color(0.03f, 0.025f, 0.02f, 0.95f);
        private static readonly Color Paper = new Color(1f, 0.97f, 0.9f);
        private static readonly Color Accent = new Color(1f, 0.83f, 0.5f);
        private readonly Dictionary<string, string> translations = new Dictionary<string, string>();
        private readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private readonly KingdomArcState state;
        private readonly bool archive;
        private readonly int instanceId;
        private readonly KingdomArcPlayback playback;
        private readonly KingdomArcSegment[] scenes;
        private readonly KingdomStage stage;
        private readonly int revision;
        private readonly string archivedConditions;
        private int Count => scenes == null ? KingdomArcScenes.Count : scenes.Length;
        private int ChoiceIndex => Count - 1;
        private KingdomArcSegment Scene(int index) => scenes == null ? KingdomArcScenes.Get(index) : scenes[index];
        private bool reviewing;
        private bool automatic = true;
        private bool fast;
        private bool paused;
        private bool hadFocus;
        private double lastTime;
        private Vector2 scroll;
        private string speaker;
        private string body;
        private Texture2D texture;
        private KingdomInvitation shownInvitation;
        private float measuredWidth = -1f;
        private float bodyHeight;

        internal Window_RHAH_KingdomArc(KingdomArcState state, bool review, KingdomStage stage = KingdomStage.None, int revision = 0, KingdomArcSegment[] snapshot = null, string conditions = "")
        {
            this.state = state;
            this.stage = stage;
            this.revision = revision;
            scenes = stage == KingdomStage.None ? null : snapshot ?? KingdomArcPresentation.Scenes(stage, state.Progress);
            archivedConditions = conditions;
            archive = review;
            reviewing = review;
            instanceId = state == null ? 0 : state.InstanceId;
            playback = new KingdomArcPlayback(review || state == null ? 0 : scenes == null ? state.Segment : state.Progress.Cursor, Count, ChoiceIndex);
            doCloseX = false;
            doWindowBackground = false;
            drawShadow = false;
            closeOnCancel = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            forcePause = true;
            preventCameraMotion = true;
            preventDrawTutor = true;
            CacheTranslations();
            RefreshSegment();
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);
        protected override float Margin => 0f;
        private bool ActiveCursor => !reviewing && state != null && state.InstanceId == instanceId && (scenes == null || state.Progress.Matches(stage, revision));
        private bool CanReply => ActiveCursor && KingdomArcRuntime.CanUse && state.CanReply && scenes == null;

        private void CacheTranslations()
        {
            string[] keys =
            {
                "Manual", "Auto", "Fast", "Pause", "Resume", "Previous", "Next", "Skip", "Review", "Return", "ReadOnly", "Close",
                "Accept", "Defer", "Decline", "Conditions", "Accepted", "Declined", "AcceptedReply", "DeclinedReply", "Unavailable", "ReplyFailed"
            };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = "RHAH_Kingdom_" + keys[i];
                translations.Add(key, key.Translate().ToString());
                labels.Add(keys[i], translations[key]);
            }
            for (int i = 0; i < Count; i++)
            {
                KingdomArcSegment segment = Scene(i);
                Cache(segment.SpeakerKey);
                Cache(segment.BodyKey);
            }
        }

        private void Cache(string key)
        {
            if (!translations.ContainsKey(key)) translations.Add(key, key.Translate().ToString());
        }

        private string Ui(string suffix) => labels[suffix];

        private void RefreshSegment()
        {
            KingdomArcSegment segment = Scene(playback.Index);
            speaker = translations[segment.SpeakerKey];
            body = translations[segment.BodyKey];
            shownInvitation = state == null ? KingdomInvitation.None : state.Invitation;
            if (scenes == null && playback.Index == ChoiceIndex)
            {
                body = translations["RHAH_Kingdom_E4_01"] + "\n\n" + translations["RHAH_Kingdom_E4_02"] + "\n\n" + translations["RHAH_Kingdom_E4_03"] + "\n\n" + translations["RHAH_Kingdom_E4_04"];
                if (!archive && shownInvitation == KingdomInvitation.Pending) body += "\n\n" + Ui("Conditions");
                if (shownInvitation == KingdomInvitation.Accepted) body += "\n\n" + Ui("AcceptedReply") + "\n\n" + Ui("Accepted");
                else if (shownInvitation == KingdomInvitation.Declined) body += "\n\n" + Ui("DeclinedReply") + "\n\n" + Ui("Declined");
            }
            if (scenes != null && playback.Index == ChoiceIndex) body += archive ? archivedConditions : KingdomArcPresentation.Conditions(state.Progress);
            playback.SetText(body);
            if (reviewing || scenes == null && (shownInvitation == KingdomInvitation.Accepted || shownInvitation == KingdomInvitation.Declined)) playback.RevealAll();
            if (!textures.TryGetValue(segment.TexturePath, out texture))
            {
                texture = ContentFinder<Texture2D>.Get(segment.TexturePath, false);
                textures.Add(segment.TexturePath, texture);
            }
            scroll = Vector2.zero;
            measuredWidth = -1f;
        }

        private void MoveTo(int index)
        {
            playback.MoveTo(index);
            if (ActiveCursor)
            {
                if (scenes == null) state.SetSegment(playback.Index);
                else state.Progress.Cursor = playback.Index;
            }
            RefreshSegment();
            ResetClock();
        }

        private void ResetClock()
        {
            lastTime = Time.realtimeSinceStartup;
            hadFocus = false;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            double now = Time.realtimeSinceStartup;
            double delta = now - lastTime;
            lastTime = now;
            WindowStack stack = Find.WindowStack;
            bool focused = Application.isFocused && stack.Count > 0 && stack[stack.Count - 1] == this && stack.GetsInput(this);
            bool running = focused && hadFocus && !paused && !reviewing;
            hadFocus = focused;
            if (state != null && shownInvitation != state.Invitation)
            {
                RefreshSegment();
                return;
            }
            if (playback.Advance(delta, Scene(playback.Index).Seconds, running, fast, automatic))
            {
                if (ActiveCursor)
                {
                    if (scenes == null) state.SetSegment(playback.Index);
                    else state.Progress.Cursor = playback.Index;
                }
                RefreshSegment();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(inRect, Ink);
                if (texture != null) GUI.DrawTexture(inRect, texture, ScaleMode.ScaleAndCrop);
                float pad = Mathf.Clamp(inRect.width * 0.035f, 20f, 64f);
                float controlHeight = 44f;
                float controlWidth = Mathf.Min(140f, (inRect.width - pad * 2f) / 5f);
                Text.Font = GameFont.Medium;
                if (TextButton(new Rect(pad, 20f, controlWidth, controlHeight), reviewing && !archive ? Ui("Return") : Ui("Review"), !archive))
                {
                    reviewing = !reviewing;
                    playback.MoveTo(reviewing || scenes != null ? 0 : state == null ? 0 : state.Segment);
                    RefreshSegment();
                    ResetClock();
                }
                if (TextButton(new Rect(inRect.width - pad - controlWidth, 20f, controlWidth, controlHeight), Ui("Close"))) Close();

                float panelHeight = Mathf.Clamp(inRect.height * 0.34f, 220f, Mathf.Max(220f, inRect.height * 0.55f));
                float panelTop = inRect.height - panelHeight;
                Widgets.DrawBoxSolid(new Rect(0f, panelTop, inRect.width, panelHeight), new Color(0.035f, 0.03f, 0.025f, 0.68f));
                OutlineLabel(new Rect(pad, panelTop + 18f, inRect.width - pad * 2f, 36f), speaker, Accent);
                Rect textRect = new Rect(pad, panelTop + 58f, inRect.width - pad * 2f, panelHeight - 122f);
                float width = textRect.width - 24f;
                if (measuredWidth != width)
                {
                    measuredWidth = width;
                    bodyHeight = Text.CalcHeight(body, width) + 12f;
                }
                // 用完整正文测量，逐字显示不改变滚动范围
                Widgets.BeginScrollView(textRect, ref scroll, new Rect(0f, 0f, width, Mathf.Max(textRect.height, bodyHeight)));
                OutlineLabel(new Rect(0f, 0f, width, bodyHeight), playback.VisibleText, Paper);
                Widgets.EndScrollView();

                Rect controls = new Rect(pad, inRect.height - 56f, inRect.width - pad * 2f, controlHeight);
                DrawControls(controls);
                if (playback.Index == ChoiceIndex) DrawChoices(new Rect(pad, panelTop - 60f, inRect.width - pad * 2f, controlHeight));
                HandleInput();
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        private static void OutlineLabel(Rect rect, string text, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Text.Anchor = anchor;
            Color oldColor = GUI.color;
            GUI.color = Ink;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x != 0 || y != 0) Widgets.Label(new Rect(rect.x + x, rect.y + y, rect.width, rect.height), text);
                }
            }
            GUI.color = color;
            Widgets.Label(rect, text);
            GUI.color = oldColor;
        }

        private bool TextButton(Rect rect, string label, bool enabled = true, bool selected = false)
        {
            bool hover = enabled && Mouse.IsOver(rect);
            Color color = enabled ? hover || selected ? Accent : Paper : new Color(Paper.r, Paper.g, Paper.b, 0.4f);
            OutlineLabel(rect, label, color, TextAnchor.MiddleCenter);
            if (hover || selected) Widgets.DrawBoxSolid(new Rect(rect.x + 8f, rect.yMax - 5f, rect.width - 16f, 1f), color);
            // 不调用原版按钮绘制或悬停音效
            if (Find.WindowStack.GetsInput(this) && Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return enabled;
            }
            return false;
        }

        private void DrawControls(Rect row)
        {
            float gap = 8f;
            float width = (row.width - 5f * gap) / 6f;
            if (TextButton(new Rect(row.x, row.y, width, row.height), Ui("Previous"), playback.Index > 0)) MoveTo(playback.Index - 1);
            if (TextButton(new Rect(row.x + width + gap, row.y, width, row.height), automatic ? Ui("Auto") : Ui("Manual"), !reviewing, automatic))
            {
                automatic = !automatic;
                fast = false;
                paused = false;
                ResetClock();
            }
            if (TextButton(new Rect(row.x + 2f * (width + gap), row.y, width, row.height), paused ? Ui("Resume") : Ui("Pause"), !reviewing, paused))
            {
                paused = !paused;
                ResetClock();
            }
            if (TextButton(new Rect(row.x + 3f * (width + gap), row.y, width, row.height), Ui("Next"), playback.Index < Count - 1 || !playback.TextComplete)) ContinueReading();
            if (TextButton(new Rect(row.x + 4f * (width + gap), row.y, width, row.height), Ui("Skip"), playback.Index != ChoiceIndex))
            {
                MoveTo(ChoiceIndex);
                playback.RevealAll();
            }
            if (TextButton(new Rect(row.x + 5f * (width + gap), row.y, width, row.height), Ui("Fast"), !reviewing, fast))
            {
                fast = !fast;
                if (fast) automatic = true;
                paused = false;
                ResetClock();
            }
        }

        private void DrawChoices(Rect row)
        {
            if (scenes != null)
            {
                string[] actions = KingdomArcPresentation.Choices(stage);
                float itemWidth = row.width / Math.Max(1, actions.Length);
                for (int i = 0; i < actions.Length; i++)
                {
                    string action = actions[i];
                    bool external = stage == KingdomStage.Hall || stage == KingdomStage.Charity || stage == KingdomStage.Action;
                    bool enabled = ActiveCursor && KingdomArcRuntime.CanAct && playback.TextComplete && state.Progress.ResultSent && (external || KingdomArcActions.Narrator);
                    if (action == "Reconsider" && state.Progress.Outcome == KingdomOutcome.Success) enabled = false;
                    if (TextButton(new Rect(row.x + i * itemWidth, row.y, itemWidth, row.height), ("RHAH_Kingdom_Action_" + action).Translate(), enabled))
                    {
                        if (action == "Locate")
                        {
                            KingdomArcProgress progress = state.Progress;
                            bool escorted = progress.Route == KingdomRoute.Protect && progress.Clerk != null && progress.Clerk.GetCaravan() != null;
                            WorldObject_RHAH_KingdomSite site = KingdomArcActions.Site(escorted ? progress.SafeSiteId : progress.SiteId);
                            if (site != null) CameraJumper.TryJump(site);
                            Close();
                        }
                        else if (action == "Defer") Close();
                        else if (KingdomArcActions.Choose(stage, revision, action)) Close();
                        else Messages.Message(Ui("ReplyFailed"), MessageTypeDefOf.RejectInput, false);
                    }
                }
                return;
            }
            if (CanReply)
            {
                float width = Mathf.Min(240f, (row.width - 32f) / 3f);
                float start = row.x + (row.width - width * 3f - 32f) / 2f;
                bool enabled = playback.TextComplete;
                if (TextButton(new Rect(start, row.y, width, row.height), Ui("Accept"), enabled)) Reply(KingdomInvitation.Accepted);
                if (TextButton(new Rect(start + width + 16f, row.y, width, row.height), Ui("Defer"), enabled)) Close();
                if (TextButton(new Rect(start + 2f * (width + 16f), row.y, width, row.height), Ui("Decline"), enabled)) Reply(KingdomInvitation.Declined);
            }
            else
            {
                string status = reviewing ? Ui("ReadOnly") : shownInvitation == KingdomInvitation.Accepted ? Ui("Accepted") : shownInvitation == KingdomInvitation.Declined ? Ui("Declined") : Ui("Unavailable");
                OutlineLabel(row, status, Paper, TextAnchor.MiddleCenter);
            }
        }

        private void ContinueReading()
        {
            if (!playback.TextComplete) playback.RevealAll();
            else if (playback.Index < Count - 1) MoveTo(playback.Index + 1);
            ResetClock();
        }

        private void HandleInput()
        {
            if (!Find.WindowStack.GetsInput(this)) return;
            Event current = Event.current;
            if (current.type == EventType.MouseDown)
            {
                if (current.button == 0) ContinueReading();
                current.Use();
            }
            else if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.LeftArrow) MoveTo(playback.Index - 1);
                else if (current.keyCode == KeyCode.RightArrow || current.keyCode == KeyCode.Space) ContinueReading();
                current.Use();
            }
        }

        public override void OnAcceptKeyPressed()
        {
            ContinueReading();
            Event.current.Use();
        }

        private void Reply(KingdomInvitation reply)
        {
            if (!CanReply || !KingdomArcRuntime.Reply(reply, instanceId))
            {
                Messages.Message(Ui("ReplyFailed"), MessageTypeDefOf.RejectInput, false);
                return;
            }
            paused = true;
            RefreshSegment();
            ResetClock();
        }
    }
}
