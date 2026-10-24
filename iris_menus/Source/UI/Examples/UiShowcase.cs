using System;
using LudeonTK;
using UnityEngine;
using Verse;

namespace IrisMenus.UI
{
    // Developer-only consumer of the primitives; no registration or settings persistence.
    internal sealed class UiShowcase
    {
        private readonly UiContext context = new UiContext();
        private readonly UiSideRail rail;
        private readonly UiTabs tabs;
        private readonly UiSegments theme;
        private readonly UiSwitch motion;
        private readonly UiSwitch toggle;
        private readonly UiCheckbox checkbox;
        private readonly UiSlider slider;
        private readonly UiTextField field;
        private readonly UiSelect select;
        private readonly UiButton drawerButton, dialogButton, nestedDialogButton, closeButton, jumpButton, disabledButton;
        private readonly UiAccordion accordion;
        private readonly UiScrollView scroll;
        private readonly UiDrawer drawer;
        private readonly UiDialog dialog;
        private readonly UiFooter footer;
        private readonly UiPageTransition page;
        private readonly UiTooltip tooltip;
        private readonly UiId formPage, layoutPage;
        private readonly Action<Rect> drawForm, drawLayout, drawScroll, drawAccordion, drawDrawer, drawDialog;
        private int selectedRail, selectedTab, selectedTheme, selectedOption;
        private bool reducedMotion, switched = true, checkedValue, expanded = true, drawerOpen, dialogOpen;
        private float strength = 35f;
        private string text = string.Empty;

        internal UiShowcase()
        {
            rail = new UiSideRail(context, new[] { "基础控件", "布局与叠层" });
            tabs = new UiTabs(context, new[] { "交互", "状态" });
            theme = new UiSegments(context, new[] { "Light", "Dark" });
            motion = new UiSwitch(context);
            toggle = new UiSwitch(context);
            checkbox = new UiCheckbox(context);
            slider = new UiSlider(context, 0f, 100f, 1f);
            field = new UiTextField(context, 120);
            select = new UiSelect(context, new[] { "默认", "紧凑", "宽松", "高对比", "自定义", "选项六", "选项七", "选项八", "选项九", "选项十" }, 5);
            drawerButton = new UiButton(context);
            dialogButton = new UiButton(context);
            nestedDialogButton = new UiButton(context);
            closeButton = new UiButton(context);
            jumpButton = new UiButton(context);
            disabledButton = new UiButton(context);
            accordion = new UiAccordion(context, true);
            scroll = new UiScrollView(context, 2);
            drawer = new UiDrawer(context);
            dialog = new UiDialog(context);
            footer = new UiFooter(context);
            page = new UiPageTransition(context);
            tooltip = new UiTooltip(context);
            formPage = context.CreateId();
            layoutPage = context.CreateId();
            drawForm = DrawForm;
            drawLayout = DrawLayout;
            drawScroll = DrawScroll;
            drawAccordion = DrawAccordion;
            drawDrawer = DrawDrawer;
            drawDialog = DrawDialog;
        }

        internal void Draw(Rect rect, int frame, double time)
        {
            context.BeginFrame(frame, time, rect, selectedTheme == 0 ? UiTheme.Light : UiTheme.Dark, reducedMotion);
            try
            {
                drawer.Prepare(drawerOpen);
                dialog.Prepare(dialogOpen);
                select.Prepare();
                context.Fill(rect, context.Palette.Surface);
                selectedTheme = theme.Draw(new Rect(rect.x + 20f, rect.y + 16f, 200f, 40f), selectedTheme);
                reducedMotion = motion.Draw(new Rect(rect.xMax - 260f, rect.y + 16f, 240f, 40f), reducedMotion, "减少动态效果");
                float bodyY = rect.y + 76f;
                selectedRail = rail.Draw(new Rect(rect.x + 20f, bodyY, 156f, 140f), selectedRail);
                float x = rect.x + 200f;
                float width = Mathf.Max(0f, rect.width - 224f);
                selectedTab = tabs.Draw(new Rect(x, bodyY, width, 40f), selectedTab);
                Rect content = new Rect(x, bodyY + 56f, width, Mathf.Max(0f, rect.height - 212f));
                page.Draw(content, selectedRail == 0 ? formPage : layoutPage, selectedRail == 0 ? drawForm : drawLayout);
                footer.Draw(new Rect(rect.x, rect.yMax - 56f, rect.width, 56f), "IrisMenus · 原生组件", "展示值不写入 Mod 设置");
                if (drawer.IsVisible && drawer.Draw(rect, Mathf.Min(360f, rect.width), "章节说明", drawDrawer)) drawerOpen = false;
                if (dialog.IsVisible && dialog.Draw(rect, new Vector2(440f, 240f), "交互确认", drawDialog)) dialogOpen = false;
                selectedOption = select.DrawPopup(rect, selectedOption);
                tooltip.DrawOverlay(rect, new Vector2(280f, 72f));
            }
            finally { context.EndFrame(); }
        }

        private void DrawForm(Rect rect)
        {
            float width = Mathf.Min(480f, rect.width);
            switched = toggle.Draw(new Rect(0f, 0f, width, 40f), switched, "启用选项");
            checkedValue = checkbox.Draw(new Rect(0f, 52f, width, 40f), checkedValue, "保留当前选择");
            context.Label(new Rect(0f, 104f, width, 28f), "强度 · " + strength.ToString("0"), context.Palette.Ink);
            strength = slider.Draw(new Rect(0f, 136f, width, 36f), strength);
            context.Label(new Rect(0f, 184f, width, 28f), "名称", context.Palette.Ink);
            text = field.Draw(new Rect(0f, 216f, width, 40f), text, "输入文字", invalid: selectedTab == 1 && text.Length == 0);
            context.Label(new Rect(0f, 268f, width, 28f), "显示方式", context.Palette.Ink);
            selectedOption = select.Draw(new Rect(0f, 300f, width, 40f), selectedOption);
            Rect button = new Rect(0f, 356f, Mathf.Min(180f, width), 40f);
            if (drawerButton.Draw(button, "查看说明")) drawerOpen = true;
            tooltip.Observe(button, "说明以抽屉展开；Esc 只关闭当前最上层。", drawerButton.Id);
            disabledButton.Draw(new Rect(196f, 356f, Mathf.Max(0f, width - 196f), 40f), "不可用", false);
        }

        private void DrawLayout(Rect rect)
        {
            if (jumpButton.Draw(new Rect(0f, 0f, 180f, 40f), "定位第二节")) scroll.FocusAnchor(1);
            scroll.Draw(new Rect(0f, 56f, rect.width, Mathf.Max(0f, rect.height - 56f)), 960f, drawScroll);
        }

        private void DrawScroll(Rect rect)
        {
            float height = accordion.Measure(expanded, 160f);
            scroll.Anchor(0, new Rect(0f, 0f, rect.width, height));
            expanded = accordion.Draw(new Rect(0f, 0f, rect.width, height), "展开说明", expanded, 160f, drawAccordion);
            scroll.Anchor(1, new Rect(0f, 700f, rect.width, 180f), focusTarget: dialogButton.Id);
            context.Label(new Rect(12f, 712f, rect.width - 24f, 36f), "第二节", context.Palette.Ink);
            if (dialogButton.Draw(new Rect(12f, 764f, 200f, 40f), "打开确认框", primary: true)) dialogOpen = true;
        }

        private void DrawAccordion(Rect rect)
        {
            context.Label(new Rect(14f, 12f, Mathf.Max(0f, rect.width - 28f), 120f),
                "折叠保留自然布局，通过裁切隐藏内容。导航定位使用显式锚点，不扫描绘制回调，也不会自动展开隐藏章节。", context.Palette.Muted, wrap: true);
        }

        private void DrawDrawer(Rect rect)
        {
            context.Label(new Rect(0f, 0f, rect.width, 112f), "抽屉与对话框共享输入层。打开确认框后，背景和抽屉都不能接收点击。", context.Palette.Ink, wrap: true);
            if (nestedDialogButton.Draw(new Rect(0f, 132f, rect.width, 40f), "打开确认框", primary: true)) dialogOpen = true;
        }

        private void DrawDialog(Rect rect)
        {
            context.Label(new Rect(0f, 0f, rect.width, 56f), "关闭后回到原来的焦点。这里不修改真实设置。", context.Palette.Ink, wrap: true);
            if (closeButton.Draw(new Rect(Mathf.Max(0f, rect.width - 140f), 76f, 140f, 40f), "返回", primary: true)) dialogOpen = false;
        }

        internal void Dispose() { field.Dispose(); context.Dispose(); }
    }

    internal sealed class UiShowcaseWindow : Window
    {
        private readonly UiShowcase showcase = new UiShowcase();
        public override Vector2 InitialSize => new Vector2(1000f, 720f);

        internal UiShowcaseWindow()
        {
            forcePause = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = false;
            absorbInputAroundWindow = true;
        }

        [DebugAction("IrisMenus", "Native UI components", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
        private static void Open() => Find.WindowStack.Add(new UiShowcaseWindow());

        public override void DoWindowContents(Rect inRect) => showcase.Draw(inRect, Time.frameCount, Time.realtimeSinceStartup);
        public override void PostClose() { showcase.Dispose(); base.PostClose(); }
    }
}
