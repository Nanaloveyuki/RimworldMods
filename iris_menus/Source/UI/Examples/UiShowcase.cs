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
        private readonly UiNumberField number;
        private readonly UiRadioGroup radio;
        private readonly UiField reusableField;
        private readonly UiSectionTitle sectionTitle;
        private readonly UiPanel panel;
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
        private int selectedRail, selectedTab, selectedTheme, selectedOption, selectedRadio;
        private bool reducedMotion, switched = true, checkedValue, expanded = true, drawerOpen, dialogOpen;
        private float strength = 35f, numericValue = 12.5f;
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
            number = new UiNumberField(context, 0f, 100f, 0.5f);
            radio = new UiRadioGroup(context, new[] { "自动", "手动", "继承" });
            reusableField = new UiField(context);
            sectionTitle = new UiSectionTitle(context);
            panel = new UiPanel(context);
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
            sectionTitle.Draw(new Rect(0f, 0f, width, sectionTitle.Measure()), "可复用表单控件");
            panel.Draw(new Rect(0f, 46f, width, 410f), content =>
            {
                reusableField.Draw(new Rect(content.x, content.y, content.width, 54f), "开关", "悬停查看提示", control => switched = toggle.Draw(control, switched, "启用选项"));
                checkedValue = checkbox.Draw(new Rect(content.x, content.y + 62f, content.width, 40f), checkedValue, "保留当前选择");
                reusableField.Draw(new Rect(content.x, content.y + 112f, content.width, 54f), "数值", "按回车提交并限制在范围内", control => numericValue = number.Draw(control, numericValue));
                reusableField.Draw(new Rect(content.x, content.y + 176f, content.width, 54f), "强度", null, control => strength = slider.Draw(control, strength));
                selectedRadio = radio.Draw(new Rect(content.x, content.y + 244f, content.width, 120f), selectedRadio);
            });
            if (drawerButton.Draw(new Rect(0f, 470f, 180f, 40f), "查看说明")) drawerOpen = true;
            tooltip.Observe(new Rect(0f, 470f, 180f, 40f), "说明以抽屉展开；Esc 只关闭当前最上层。", drawerButton.Id);
            disabledButton.Draw(new Rect(196f, 470f, Mathf.Max(0f, width - 196f), 40f), "不可用", false);
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

        internal void Dispose() { field.Dispose(); number.Dispose(); reusableField.Dispose(); context.Dispose(); }
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
