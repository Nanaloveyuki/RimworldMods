# 原生 UI 组件与动效

`IrisMenus.UI` 是独立于旧设置 UI 的 C#／IMGUI 可复用组件。本文记录 `Source/UI/` 当前源码接口及调用约束；注册页面与保存仍见[接入指南](guide.md)。使用这些组件不会自动替换 `MenuWindow`，也不会注册设置页或写入消费者设置。

当前 1.7 已接入显式全屏宿主和 `MenuPresentation`，旧窗口仍为默认入口；消费者接入与生命周期见[接入契约](../agents/integration.md#native-full-screen-presentation-current-17)。保留的 1.6 二进制未更新，不能因共享源码推定其具有新 API 或已验收。

## 目录职责

所有公开类型都在 `IrisMenus.UI` 命名空间，目录按前端常见职责分类，不为每个目录另建命名空间。

|目录|职责与类型|
|---|---|
|`Core/`|`UiContext`：每个 UI surface 的帧快照、输入、焦点、捕获、裁切和透明度；`UiId`、`UiInteraction`：稳定身份与交互结果|
|`Theme/`|`UiTheme`、`UiPalette`：Light／Dark 语义色与主题混合|
|`Animation/`|`UiTween`、`UiMotion`：真实时间、可反向的标量过渡及公共时长|
|`Controls/`|`UiButton`、`UiSwitch`、`UiCheckbox`、`UiSlider`、`UiSegments`、`UiTextField`、`UiSelect`、`UiTooltip`；内部 `UiControlVisual` 统一基础反馈|
|`Navigation/`|`UiSideRail`、`UiTabs`；内部 `UiNavigation` 共享项目身份、选中指示器和键盘导航|
|`Containers/`|`UiAccordion`、`UiScrollView`、`UiPageTransition`、`UiFooter`：布局、裁切、章节定位和页面呈现|
|`Overlays/`|`UiDrawer`、`UiDialog`；内部 `UiModalState` 管理模态保留、关闭请求和焦点恢复|
|`Examples/`|内部 `UiShowcase`／`UiShowcaseWindow`：组合全部组件的开发者示例，不是新设置宿主|

## 实例、值与资源归属

- 每个窗口或独立 UI surface 保留一个 `UiContext`；在初始化时创建组件、页面 `UiId`、标签数组和 `Action<Rect>` 回调，不在每次 `Draw` 内重建。组件持有身份、动画和临时交互状态；身份不取自显示标题、Rect、当前帧 control ID 或列表位置的临时 hash。
- 开关、勾选、数值、字符串、选择索引、展开和模态打开状态由调用方持有。把 `Draw` 的返回值写回自己的字段。按钮返回本次激活，抽屉／对话框返回关闭请求；是否保存、拒绝关闭或确认变更由调用方决定。
- `UiSideRail`／`UiTabs` 保留调用方的标签数组。可原位更新翻译文字，但同一实例的目的地顺序必须稳定；增删或重排目的地应重建对应导航实例。它们不生成 Mod／页数量或 badge。
- `UiSegments.SetOptions`／`UiSelect.SetOptions` 用于配置变化，不能逐帧调用。它们保留传入的标签数组；数量变化会重建项目身份。Segments 至少一项；Select 允许空数组，此时不可打开。标签不能为 `null`。Select 配置变化会重置搜索和滚动。
- 页面回调中的 Rect 通常是组件建立 GUI group 后的**局部自然尺寸**。不要再加宿主 Rect 的 x／y。布局高度和 tooltip 尺寸由调用方提供；没有自动布局树、反射字段发现或文本高度测量承诺。
- `UiTextField` 使用原生 `GUI.TextField` 编辑器。字段从 surface 永久移除时调用它的 `Dispose()`，只释放该字段的原生焦点／捕获；surface 关闭时先释放文本字段，再 `UiContext.Dispose()`。不要在未退出的 scope 内释放 context。
- 这些实例是窗口会话状态，不提供序列化、设置存储、保存回调、跨重开／重启记忆或无障碍平台语义桥接。

矩形控件、导航背景、下拉／提示／模态面板和滚动条使用直角；Switch 胶囊轨道／圆 thumb、Slider 圆端轨道／圆 thumb／halo 保留圆形。共享背景把 `Palette.Hover` 作为带 alpha 的叠加色合成到原底色，不把原底色插值成半透明：悬停停留期间保持目标颜色，移出后才恢复。

## 每个 IMGUI pass 的固定顺序

同一个游戏帧仍可能有 Layout、输入和 Repaint 多个 pass。每个 pass 都执行完整流程；不能用 `frameCount` 缓存来跳过 Draw、输入或 Begin／End。

1. `context.BeginFrame(viewport, theme, reducedMotion)`。需要可控时钟时使用 `BeginFrame(int frame, double time, Rect viewport, UiTheme theme, bool reducedMotion)`；同一帧应传相同真实时间。Context 仅在帧号变化时采样时间，并保证时间不倒退。
2. 在任何背景控件之前，为保留的抽屉／对话框调用 `Prepare(open)`，**从后到前**排列。最后准备的可见模态拥有输入；退出动画尚未结束的模态也必须准备。
3. 若 Select 的 popup 是最前层，在这些模态之后调用 `select.Prepare()`。有多个模态／Select 时，调用方要明确实际叠放顺序，使 Prepare 顺序与绘制顺序一致；必要时先关闭已失去所属页面的 Select，不能让旧 popup 遮挡新的页面。
4. 绘制正文、导航、滚动内容和底栏；在所属位置调用 `select.Draw(...)` 更新触发器的屏幕坐标。绘制后调用 `tooltip.Observe(rect, text, targetId)`，不要为 tooltip 再调用一次 `Interact`。
5. 退出正文所有 scroll／clip group。依照相同的后到前顺序绘制仍 `IsVisible` 的抽屉／对话框，包含关闭后的退出动画。
6. 在 surface 的根坐标空间调用 `selected = select.DrawPopup(viewport, selected)`。不能把 popup 画在触发器的滚动／裁切组里；触发器和 popup 两处返回值都应写回。
7. 最后调用 `tooltip.DrawOverlay(viewport, explicitSize)`，然后在 `finally` 中调用 `context.EndFrame()`。

`Prepare` 不能延迟到 Repaint。它在背景控件响应输入之前建立所有权，避免打开 overlay 时点击／键盘穿透。`EndFrame` 处理 Tab 顺序与失效捕获；不平衡的 scope 会抛出异常。

`context.Clip(rect)` 同时缩小绘制和命中范围，并建立以 rect 左上角为原点的 GUI group。`Input(false)` 同时约束组件输入与 `GUI.enabled`；`Opacity` 乘算透明度；`Layer(id)` 只切换输入层身份，不自动建立模态。使用 `using` 保证所有 scope 按嵌套顺序退出。完全裁切、禁用或非当前模态层的控件不参与可用交互顺序。第三方绘制仍需自行平衡 GUI group；这些 scope 不能修复任意旧 drawer 的失衡。

### 最小组合示例

下面的对象由宿主创建一次，每次 IMGUI 回调调用 `Draw`，关闭后 `Dispose`。示例值只存在内存中；`Theme` 和 `ReducedMotion` 必须由调用方提供，不会读取或持久化系统偏好。

```csharp
using System;
using IrisMenus.UI;
using UnityEngine;

sealed class NativePanel : IDisposable
{
    readonly UiContext ui = new UiContext();
    readonly UiSwitch toggle;
    readonly UiTextField field;
    readonly UiSelect select;
    readonly UiScrollView scroll;
    readonly UiButton openDrawer, openDialog, closeDialog;
    readonly UiDrawer drawer;
    readonly UiDialog dialog;
    readonly UiTooltip tooltip;
    readonly Action<Rect> drawContent, drawDrawer, drawDialog;
    bool enabled, drawerOpen, dialogOpen;
    string text = string.Empty;
    int selected;

    public UiTheme Theme = UiTheme.Dark;
    public bool ReducedMotion;

    public NativePanel()
    {
        toggle = new UiSwitch(ui);
        field = new UiTextField(ui, 120);
        select = new UiSelect(ui, new[] { "Default", "Compact", "Spacious" });
        scroll = new UiScrollView(ui);
        openDrawer = new UiButton(ui);
        openDialog = new UiButton(ui);
        closeDialog = new UiButton(ui);
        drawer = new UiDrawer(ui);
        dialog = new UiDialog(ui);
        tooltip = new UiTooltip(ui);
        drawContent = DrawContent;
        drawDrawer = DrawDrawer;
        drawDialog = DrawDialog;
    }

    public void Draw(Rect bounds)
    {
        ui.BeginFrame(bounds, Theme, ReducedMotion);
        try
        {
            drawer.Prepare(drawerOpen); // Back to front, before any controls.
            dialog.Prepare(dialogOpen);
            select.Prepare();
            ui.Fill(bounds, ui.Palette.Surface);
            var body = new Rect(bounds.x + 16f, bounds.y + 16f,
                Mathf.Max(0f, bounds.width - 32f), Mathf.Max(0f, bounds.height - 32f));
            scroll.Draw(body, 340f, drawContent);
            if (drawer.IsVisible && drawer.Draw(bounds, 320f, "Details", drawDrawer))
                drawerOpen = false;
            if (dialog.IsVisible && dialog.Draw(bounds, new Vector2(400f, 240f), "Confirm", drawDialog))
                dialogOpen = false;
            selected = select.DrawPopup(bounds, selected); // Outside scroll and modal groups.
            tooltip.DrawOverlay(bounds, new Vector2(240f, 64f));
        }
        finally { ui.EndFrame(); }
    }

    void DrawContent(Rect local)
    {
        enabled = toggle.Draw(new Rect(0f, 0f, local.width, 40f), enabled, "Enable");
        text = field.Draw(new Rect(0f, 56f, local.width, 40f), text, "Name");
        selected = select.Draw(new Rect(0f, 112f, local.width, 40f), selected);
        var button = new Rect(0f, 168f, Mathf.Min(180f, local.width), 40f);
        if (openDrawer.Draw(button, "Details")) drawerOpen = true;
        tooltip.Observe(button, "Open the details drawer", openDrawer.Id);
    }

    void DrawDrawer(Rect local)
    {
        if (openDialog.Draw(new Rect(0f, 0f, local.width, 40f), "Confirm")) dialogOpen = true;
    }

    void DrawDialog(Rect local)
    {
        if (closeDialog.Draw(new Rect(0f, 0f, local.width, 40f), "Return")) dialogOpen = false;
    }

    public void Dispose() { field.Dispose(); ui.Dispose(); }
}
```

示例中 popup 高于对话框；若产品需要其他关系，应同时调整准备与绘制顺序。模态打开字段在正文按钮激活时改变，下一次 `Prepare` 开始对应转场。不要为了立即显示而额外执行正文回调。

## 组件接口要点

|类型|调用与返回约定|
|---|---|
|`UiButton`|`Draw(rect, label, enabled, primary)` 返回激活；`Id` 可用于焦点和 tooltip。`primary` 选择强调样式，不隐含提交行为|
|`UiSwitch`／`UiCheckbox`|`Draw(rect, value, label, enabled)` 返回调用方的新布尔值；初次绘制直接匹配已有值|
|`UiSlider`|构造时配置有限的 min／max 和非负 step，max 必须大于 min；`Draw` 返回限制并量化后的值。`IsDragging` 暴露拖动状态。抓住 thumb 保持偏移，点轨道跳到相应值；拖动用屏幕坐标增量，布局移动不会被当成手指移动|
|`UiSegments`|构造或 `SetOptions(labels)` 配置；`Draw(rect, selected, enabled)` 返回索引，越界输入会限制到有效项；`GetItemId` 提供稳定项目身份|
|`UiTextField`|`Draw(rect, value, placeholder, enabled, invalid)` 返回字符串；`Submitted` 仅表示本 pass 的 Enter 提交。`invalid` 只控制反馈，不负责验证、错误信息或阻止保存|
|`UiSelect`|`Open(selected)`／`Close()`、`IsOpen`／`IsVisible` 区分逻辑打开与退出动画；必须配套 `Prepare`、触发器 `Draw` 和根层 `DrawPopup`。选项提交发生于 popup 返回值，取消不修改所选索引；可配置可见行数与行高，长列表有滚轮和可拖动滚动条|
|`UiTooltip`|默认延迟 0.5 秒；`Observe` 支持 hover 和传入目标 `Id` 的键盘焦点，不参与捕获或 Tab 顺序。点击／Esc 抑制当前提示，`Dismiss` 保留退出文本并淡出；`DrawOverlay(bounds, size)` 由调用方给出尺寸并约束在 bounds 内|
|`UiSideRail`／`UiTabs`|`Draw` 返回选择索引，`itemEnabled` 若提供必须与标签数一致；`ItemId` 用于稳定焦点。`visible=false` 继续绘制退出但停止输入；SideRail 可用 `NaturalHeight` 计算完整高度，不自行提供滚动|
|`UiAccordion`|先 `Measure(expanded, naturalContentHeight, headerHeight)` 为后续兄弟布局留位置，再用该高度 `Draw` 并保存返回的展开状态；默认 header 44。回调保持自然高度，通过裁切隐藏折叠部分；逻辑折叠即禁用内容输入，退出期间可继续绘制|
|`UiScrollView`|`Draw(rect, naturalContentHeight, cachedCallback, enabled)` 每 pass 调用一次内容回调；返回是否解析了待定位的 anchor 请求。`Position`／`VisibleRect` 暴露滚动位置和自然内容中的可见范围，`SetPosition`／`Reset` 由调用方显式控制|
|`UiPageTransition`|用缓存的页面 `UiId`、当前请求页面的回调调用 `Draw`；可指定 `direction`、`visible`、`initialFocus`。每次至多执行一个回调，切页退出区间不执行任何旧／新页面回调，不做双页实时 crossfade，也不保留已删除页面的 drawer|
|`UiFooter`|`Draw` 接收 leading／trailing 文本及可选缓存 action 回调、action 宽度和 visible；action 使用自己的局部 Rect。两种主题都采用深色底及 `Palette.FooterInk`。文字不表示真实保存／修改状态，需调用方提供事实|
|`UiDrawer`／`UiDialog`|先 `Prepare(open)`，再在 `IsVisible` 时 `Draw`；返回关闭请求，不修改调用方 open 值。Drawer 可选左右侧，默认允许点 scrim 关闭；Dialog 默认不因 scrim 关闭。`dismissible=false` 隐藏关闭按钮且不以 Esc／scrim 请求关闭；退出期继续遮挡背景但不编辑内容|

### 显式 anchor 与逻辑可用性

构造 `UiScrollView(ui, anchorCount)` 时分配固定 anchor 槽。每个 `Draw` pass 的缓存内容回调内调用 `Anchor(index, rect, available, focusTarget)`，同一索引只注册一次。Rect 是当前 GUI group 的局部坐标，包含嵌套 accordion group 时会转换为自然滚动内容坐标。

- `available` 表示**逻辑上可定位**，不等于当前在屏幕可见。屏幕外的有效章节仍应注册，才能滚动过去。折叠、正在收起或条件隐藏的章节传 `available: false`，即使退出动画仍在绘制也如此。
- `TryFocusAnchor` 仅接受最近内容 pass 已注册的目标，返回是否排入请求；`FocusAnchor` 可在首个内容 pass 前无条件排入请求。真正是否解析成功以随后 `Draw` 的返回值为准，目标必须在该次 pass 重新注册，不使用过期 Rect。
- 定位不会自动展开祖先、扫描 drawer 或创建章节。调用方先更新逻辑展开状态，再让内容回调注册目标。
- 定位先把焦点移交滚动容器；平滑滚动结束、目标实际可见且允许输入后，才移交可选的 `focusTarget`。到达区域有短暂高亮。普通滚轮／滚动条输入会打断程序滚动，不为动画强抢位置。
- `VisibleRect` 可辅助调用方裁剪昂贵绘制；`UiScrollView` 本身没有把任意全量 IMGUI 回调变成虚拟列表。不要因为跳过屏幕外绘制而遗漏仍有效的 anchor 注册。

例如隐藏章节的退出回调仍执行时，应显式传入状态：

```csharp
// Inside the cached scroll callback; chapterExpanded is caller-owned logical state.
scroll.Anchor(1, chapterRect, available: chapterExpanded, focusTarget: firstField.Id);
```

## 动效清单

`UiTween.Step` 先采样当前曲线再重定向，使用 cubic ease-out，不因反向操作跳回旧起点。公共时长为 `Fast=0.12s`、`Standard=0.18s`、`Enter=0.24s`、`Exit=0.16s`。同帧多 pass 使用相同 `context.Time`，不会重复累加 delta。`ReducedMotion` 在 `BeginFrame` 显式传入，所绘制组件的 tween 立即到目标；它不读取系统偏好，也不负责保存该偏好。延迟 tooltip 的 0.5 秒等待和 anchor 高亮保持时间不是位移动画，不因此取消。

|对象|已实现的局部动画|
|---|---|
|主题|Light／Dark 的所有语义颜色按 Standard 混合；初次主题直接匹配，不先闪另一主题|
|共享控件反馈|hover／press／focus 按 Fast 过渡；显式 disabled 按 Standard 过渡至 0.42 透明度；键盘焦点使用 Signal 描边，指针点击不启用焦点框|
|Button|普通／primary 背景和边框反馈；按下时文字下移约 1 UI 单位，不移动命中 Rect|
|Switch|thumb 横向位置与轨道／thumb 颜色按 Standard 切换；按下 thumb 加宽，hover 影响关闭轨道颜色|
|Checkbox|选中底色／边框变化，勾线分两段描出并调整透明度；取消沿当前进度反向|
|Slider|非拖动值的 thumb／填充位置按 Standard 过渡，拖动直接跟随值；halo、thumb 半径和颜色有 hover／press／focus 反馈|
|Segments|选中底块按 Standard 横移，各标签按当前位置混合前景色，项目保留独立交互反馈|
|TextField|背景／focus 反馈；invalid 的 Error 色与描边宽度按 Standard 过渡；光标、选区、输入法由原生编辑器负责|
|Select|触发器箭头翻转；popup Enter／Exit 淡入淡出及 6 UI 单位偏移，真实 panel Rect 同时用于命中；选中行颜色／勾标和键盘高亮过渡；滚动位置按 Fast 过渡，滚动条带交互反馈|
|Tooltip|等待后 Enter／Exit 淡入淡出及 4 UI 单位偏移；离开目标保留文字至退出结束，不截获输入|
|SideRail／Tabs|整体 Enter／Exit；选中指示器的 x／y／宽／高按 Standard 过渡，无选中项时指示器淡出；项目 hover／press／focus；rail 选中面板／侧标、tabs 下划线|
|Accordion|展开 Enter／收起 Exit 改变可见高度、内容透明度和箭头 90° 旋转；标题 hover／press／focus，内容自然布局不被压扁|
|ScrollView|章节定位和键盘滚动按 Enter 过渡；到达章节的 Signal 高亮按 Standard 进出，保持约 2.5 秒；直接滚动打断程序动画|
|PageTransition|进入透明度及 16 UI 单位起始位移，退出目标为相反方向 12 UI 单位；切页有无回调的退出间隔，随后只进入最新请求页；可显式移交初始焦点|
|Footer|Enter／Exit 透明度；隐藏即禁用 action 输入，退出时仍绘制|
|Drawer|左右边缘真实 panel 位置滑入／滑出，scrim 与内容透明度同步；关闭按钮 hover／press／focus|
|Dialog|panel 从 0.94 到 1 的实际尺寸变化及淡入淡出，内容保留自然布局并裁切，不靠 `GUI.matrix` 缩放输入；scrim 与关闭按钮反馈|

保留的 Drawer／Dialog 从打开时记录焦点，最上层退出完成后尝试恢复原焦点。调用方仍需保证目的控件有效，并让组件继续参与 IMGUI pass。组件动效不包括主 `MenuWindow` 开关、保存失败门控、暂停／Frost 资源持有或第三方 drawer 的内部动画。

## 键盘与鼠标

- Context 在 `EndFrame` 处理 Tab／Shift+Tab，按本 pass 实际可交互控件顺序循环。按钮、开关、勾选、segments、导航项和 accordion 用 Space／Enter 的按下／抬起完成激活；Esc 可撤销当前按压／捕获。关闭、禁用或裁切的控件不应保留可交互焦点。
- `Focused`／`IsFocused` 表示键盘输入所有权，`FocusVisible` 表示是否显示焦点描边。Context 在每个 pass 开始遇到 KeyDown 时显示、MouseDown 时隐藏；鼠标点击仍保留焦点，随后可继续用方向键／Space／Enter 操作。程序性焦点转移不改变当前输入方式，打开或关闭模态不会凭空增加黄色框。
- Slider：方向键按 step（未配置时为范围 1%）调整；Shift 放大十倍；PageUp／PageDown 再按十个增量调整；Home／End 到端点。拖动有稳定捕获，释放或失去输入资格时结束。
- Segments：左右／上下、Home／End 改变选中与焦点。Tabs 使用左右，SideRail 使用上下，另支持 Home／End，并跳过禁用或完全裁切项目。
- TextField：原生编辑器提供光标、选区、剪贴板快捷键、水平滚动和输入法路径；Enter 设置 `Submitted`，Tab 留给 context，后台／非当前模态的文本编辑器不能消费输入。具体输入法与平台行为仍须实机验收。
- Select：焦点在触发器时 Space／Enter 或上下键打开；popup 中上下、Home／End、PageUp／PageDown 移动高亮，Enter／Space 提交，Esc／点外部取消。Tab 关闭 popup 并消费本次按键，下一次 Tab 才遍历焦点；可键入前缀寻找选项，输入间隔超过 0.75 秒重新开始。长 popup 支持滚轮和滚动条拖动。
- Accordion：键盘 LeftArrow 折叠、RightArrow 展开，也可 Space／Enter 切换。聚焦 ScrollView 后方向键、PageUp／PageDown、Home／End 滚动。
- Drawer／Dialog：只有最上层处理 Esc；是否点 scrim 关闭由配置决定。退出期间保留模态输入遮挡。Tooltip 可由目标键盘焦点触发，点击／Esc 抑制；它不是可聚焦弹窗。

这些是组件的输入契约，不等同于屏幕阅读器、无障碍树或操作系统 accessibility bridge 已接通。

## 开发者入口与验证状态

开发模式调试动作中使用 **IrisMenus > Native UI components**。源码允许在主菜单与游戏内打开；独立 `UiShowcaseWindow` 展示主题、减少动态效果、各类控件、导航、accordion、滚动定位、tooltip、drawer／dialog 和页转场。展示值不写入 Mod 设置，窗口不是当前设置入口的替代品。

已执行的验证：1.7 provider／API consumer 构建成功，0 warnings／0 errors；`RuntimeChecks` 通过 205 项断言（engine doubles，不验证渲染）；隔离输出的 1.6 源码构建成功，未修改保留的 1.6 发布产物。早期真实 `UnityStubs` 临时运行器通过 90 项组件断言；本轮另通过 37 项焦点／几何／悬停断言，覆盖鼠标激活后无黄色框、键盘激活与 Tab／Shift+Tab 焦点、方向键操作、直角与必要圆形，以及 Light／Dark 下 Select 和 TextField 的渐变中间态、持续停留 5 秒、移出恢复和底色 alpha 保持。运行器使用生产组件和安装目录程序集，绘制采集后端的字体度量近似，不代表游戏栅格化或真实桌面输入。

已在隔离 RimWorld 1.7.4985 主菜单实例打开并查看组件展厅的 Light 基础控件与 Dark 抽屉截图，确认直角面板及保留的圆形结构。真实鼠标／键盘自动化被 Windows PowerShell 执行策略阻止，未更改执行策略；因此本轮不把自动化冒烟结论写成游戏内点击、连续 hover 或输入法验收。临时运行器用后清理，保留开发者展示窗口供游戏内操作。

最终组件构建已通过标准部署脚本同步至本地 `Mods/IrisMenus`，22 个暂存／部署文件经 SHA-256 校验；保留安装目录中的 1.6 文件与 PublishedFileId。隔离测试实例已正常退出，未加载生存存档。上述悬停 5 秒为可控时钟推进的组件场景，不冒充实际鼠标停留录像。

以上为组件阶段的历史记录。当前全屏宿主、页面呈现注解、开关动效和设置持久化已实现；当前验收记录见下节。操作系统无障碍语义桥接仍未提供。

## Full-screen host status — 2026-10-24

Development and testing paused at the user's request. The current implementation is
incomplete and has known UI issues. Full acceptance remains deferred, not passed.
The 1.7 provider/API consumer build succeeded with no warnings or errors; the existing
engine-double suite passed 205 assertions. Limited isolated-game checks exercised
factory/draw retry, save-failure navigation/close gates, keyboard focus and dynamic
page removal. These checks do not establish full UI, lifecycle or compatibility acceptance.
The isolated instance was stopped normally. No release version was assigned.

