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
|`Overlays/`|`UiDrawer`、`UiDialog`、`UiToast`；内部 `UiModalState` 管理模态保留、关闭请求和焦点恢复|
|`Examples/`|内部 `UiShowcase`／`UiShowcaseWindow`：组合全部组件的开发者示例，不是新设置宿主|

## 实例、值与资源归属

- 每个窗口或独立 UI surface 保留一个 `UiContext`；在初始化时创建组件、页面 `UiId`、标签数组和 `Action<Rect>` 回调，不在每次 `Draw` 内重建。组件持有身份、动画和临时交互状态；身份不取自显示标题、Rect、当前帧 control ID 或列表位置的临时 hash。
- 开关、勾选、数值、字符串、选择索引、展开和模态打开状态由调用方持有。把 `Draw` 的返回值写回自己的字段。按钮返回本次激活，抽屉／对话框返回关闭请求；是否保存、拒绝关闭或确认变更由调用方决定。
- `UiSideRail`／`UiTabs` 保留调用方的标签数组。可原位更新翻译文字；公开构造的目的地顺序必须稳定。宿主内部通过 page ID 重新绑定 Tabs，增删重排仍保留未删除目的地的身份及动画；同一 Mod 不为每个 SubItem 分别创建 Tabs。它们不生成 Mod 数量或 badge；Tabs 溢出分页仅显示当前页／总页数。
- `UiSegments.SetOptions`／`UiSelect.SetOptions` 用于配置变化，不能逐帧调用。它们保留传入的标签数组；数量变化会重建项目身份。Segments 至少一项；Select 允许空数组，此时不可打开。标签不能为 `null`。Select 配置变化会重置搜索和滚动。
- 页面回调中的 Rect 通常是组件建立 GUI group 后的**局部自然尺寸**。不要再加宿主 Rect 的 x／y。布局高度和 tooltip 尺寸由调用方提供；没有自动布局树、反射字段发现或文本高度测量承诺。
- `UiTextField` 使用原生 `GUI.TextField` 编辑器。字段从 surface 永久移除时调用它的 `Dispose()`，只释放该字段的原生焦点／捕获；surface 关闭时先释放文本字段，再 `UiContext.Dispose()`。不要在未退出的 scope 内释放 context。
- 这些实例是窗口会话状态，不提供序列化、设置存储、保存回调、跨重开／重启记忆或无障碍平台语义桥接。

矩形控件、导航背景、下拉／提示／模态面板和滚动条使用直角；Switch 保留胶囊轨道和圆 thumb，Slider 使用矩形 thumb。共享背景反馈保留底色 alpha；输入框、Select 和 Segments 使用底边线及墨色聚焦擦出效果。

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
|`UiButton`|`Draw` 返回激活；`Id` 可用于焦点和 tooltip。`primary` 默认选择 Control；显式 `variant` 可选 Control／Light／Action／Text／Ghost，并可传 `icon`、图标角度及 `scale`。不隐含提交行为|
|`UiSwitch`／`UiCheckbox`|`Draw(rect, value, label, enabled)` 返回调用方的新布尔值；初次绘制直接匹配已有值|
|`UiSlider`|构造时配置有限的 min／max 和非负 step，max 必须大于 min；`Draw` 返回限制并量化后的值。`IsDragging` 暴露拖动状态。抓住 thumb 保持偏移，点轨道跳到相应值；拖动用屏幕坐标增量，布局移动不会被当成手指移动|
|`UiSegments`|构造或 `SetOptions(labels)` 配置；`Draw(rect, selected, enabled)` 返回索引，越界输入会限制到有效项；`GetItemId` 提供稳定项目身份|
|`UiTextField`|`Draw(rect, value, placeholder, enabled, invalid, icon, shortcut, showClear, scale)` 返回字符串；图标、快捷键提示及清除按钮均可选。`Submitted` 仅表示本 pass 的 Enter 提交；`invalid` 不负责验证、错误信息或阻止保存|
|`UiSelect`|`Open(selected)`／`Close()`、`IsOpen`／`IsVisible` 区分逻辑打开与退出动画；必须配套 `Prepare`、触发器 `Draw` 和根层 `DrawPopup`。选项提交发生于 popup 返回值，取消不修改所选索引；可配置可见行数与行高，长列表有滚轮和可拖动滚动条|
|`UiTooltip`|默认延迟 0.3 秒；键盘焦点立即显示，同一 Context 内相邻提示有 400ms 免等待窗口。`Observe` 不参与捕获或 Tab 顺序。点击／Esc 抑制当前提示，`Dismiss` 保留退出文本；`DrawOverlay(bounds, size, scale)` 优先上方、必要时翻转，尺寸由调用方提供|
|`UiSideRail`／`UiTabs`|`Draw` 返回选择索引，`itemEnabled` 若提供必须与标签数一致；`ItemId` 用于稳定焦点。`visible=false` 继续退出但停止输入；SideRail 可用 `NaturalHeight` 算完整高度，不自行滚动。可选 `Texture2D[] icons` 与标签等长；传数组时，即使元素为 null 也保留图标列，纹理由调用方拥有|
|`UiAccordion`|先 `Measure(expanded, naturalContentHeight, headerHeight)` 为后续兄弟布局留位置，再用该高度 `Draw` 并保存返回的展开状态；默认 header 44。回调保持自然高度，通过裁切隐藏折叠部分；逻辑折叠即禁用内容输入，退出期间可继续绘制|
|`UiScrollView`|`Draw(rect, naturalContentHeight, cachedCallback, enabled)` 每 pass 调用一次内容回调；返回是否解析了待定位的 anchor 请求。`Position`／`VisibleRect` 暴露滚动位置和自然内容中的可见范围，`SetPosition`／`Reset` 由调用方显式控制|
|`UiPageTransition`|用缓存页面 `UiId`、最新页面回调及可选 `visible`／`initialFocus` 调用 `Draw`。切页立即替换，每 pass 至多执行一个回调；只做原位淡入，不保留旧 drawer、不等待空白退出区间|
|`UiFooter`|`Draw` 接收 leading／trailing 文本及可选缓存 action 回调、action 宽度和 visible；action 使用局部 Rect。浅色主题为深色带，深色主题透明，两者保留顶部分隔线。文字需由调用方提供真实状态|
|`UiDrawer`／`UiDialog`|先 `Prepare(open)`，再在 `IsVisible` 时 `Draw`；返回关闭请求，不修改调用方 open 值。Drawer 可选左右侧，默认允许点 scrim 关闭；Dialog 默认不因 scrim 关闭。`dismissible=false` 隐藏关闭按钮且不以 Esc／scrim 请求关闭；退出期继续遮挡背景但不编辑内容|
|`UiThingEntry`|缓存一个 ThingDef 或 Thing 的显示名、单件价值、说明和 tooltip；`Key` 是稳定身份。语言、价值或物品状态变化后由调用方替换快照，不在 Draw 中反复构造|
|`UiItemSlot`／`UiThingsList`|物品图标使用游戏原生 ThingIcon；价值色读取 IrisMenus 当前分档。`Draw`／`DrawRow` 返回选择值；`readOnly: true` 禁止改变选择并隐藏行末勾选框，不把文字变成禁用灰。`SetItems` 替换快照，内置滚动仅绘制可见行；自管滚动时用 `DrawRow`，最后在根层 `DrawTooltip`|
|`UiChoiceSort`|标签加小型互斥选项组；`Draw` 返回新索引，不自行排序或修改消费者数据。`SetOptions` 只在选项变化时调用|
|`UiLoader`|`Draw(rect, label, value, scale)` 显示调用方提供的真实 0–1 进度；`value: null` 明确表示未知进度，只显示活动条，不伪造百分比或剩余时间。注册表使用已处理定义数／总定义数|
|`UiColorPicker`|不透明 RGB 模态编辑器；`Open(color, callback)` 建立草稿，`Prepare` 在底层输入之前调用，根层 `DrawOverlay`。仅“应用”回调提交；取消、Esc、遮罩关闭均丢弃草稿。支持预设色、0–255 滑块和数值输入，非法通道值阻止提交；移除时 Dispose|

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

### 物品列表、价值配色与注册表

- `IrisMenus.MenuThingsList` 为已有 `Listing_Standard` 页面提供分组、搜索、折叠、逐项选择和稳定 anchor。使用 `SetGroups` 提供快照，或 `SetRegistry(include)` 使用注册表的直接分类与分类路径；数据和保存仍归消费者。全屏宿主自动进入 `MenuItemControls` scope，旧窗口保留 Widgets 展示；自行组合宿主时使用 `MenuItemControls.Enter(context)`。
- `MenuItemControls.ChoiceSort` 保留相同的选项及赋值回调：全屏使用 UiChoiceSort，旧窗口使用原版下拉。RHAH 的救济、普通投喂、乞讨、索粮及衣物名单已接入；按模组、名称、原版分类、批量选择与搜索定位语义不变。
- 默认单件市场价值分档为 `[0,50)` 灰、`[50,200)` 绿、`[200,500)` 蓝、`[500,1500)` 紫、`[1500,+∞)` 金；边界属于右侧档位。设置页支持阈值滑块／数值、颜色、增删条目和恢复默认。零起点不可移动或删除，相邻阈值必须严格递增；删除中间条目后前档自然延伸，没有区间空洞。
- `ThingRegistry` 在游戏加载结束后读取所有已注册 ThingDef 与 ThingCategoryDef，每次更新处理至多 256 个定义，不生成 Thing 实例。`ProcessedCount`／`TotalCount`／`Progress`／`IsReady` 公开真实加载状态；`Revision` 随索引推进变化。`TryGet` 按 defName 查询；`InCategory` 只返回直接成员，父级关系使用游戏的 `category.parent`，保留多分类归属；无有效分类的定义进入 `Uncategorized`。
- IrisMenus「物品注册表」可按分类路径浏览，搜索显示名、defName、Mod 名或 package ID，查看诊断并输出完整成员表到日志。全屏页使用主题色控件与只读虚拟行；旧窗口继续使用列表布局。注册表仅索引当前启动的已加载定义，不承诺运行期热重载。

## 动效清单

`UiTween.Step` 先采样当前曲线再重定向，不因反向操作跳回旧起点。`UiEase.Standard` 对应 CSS ease，`Exit` 对应 ease-out，`Iris` 为 `cubic-bezier(.2,.7,.2,1)`。公共时长为 `Fast=Standard=Exit=0.2s`、`Enter=0.3s`、`Sheet=0.22s`；具体组件可单独指定。同帧多 pass 使用相同 `context.Time`。`ReducedMotion` 在 `BeginFrame` 显式传入，所绘制 tween 立即到目标；它不读取或保存系统偏好，tooltip 等待和 anchor 高亮保持时间不因此取消。

|对象|已实现的局部动画|
|---|---|
|主题|Light／Dark 语义色按 180ms Iris 曲线混合；初次主题直接匹配|
|共享控件反馈|hover／press／focus 按 Fast 过渡；disabled 到 0.42 透明度；指针点击不启用键盘焦点框|
|Button|Control／Light 左竖条在 300ms 内连续变为向右等边三角形并右移；终态边长为原竖条长的 60%，一边垂直于按钮底边；反向从当前进度继续。Control 始终深灰底，按下不偏移文字；图标旋转由调用方指定|
|Switch|52×28 自然尺寸，20px thumb、4px 内距、24px 行程；开／关、悬停、禁用色和位移按 200ms 过渡，不按压变形。开启 thumb 在 Light 为黄色、Dark 为反转表面的暗金色；禁用保留专用底色与灰色 thumb，不整体乘通用透明度|
|Checkbox|选中底色、边框及强调色勾线过渡；取消沿当前进度反向|
|Slider|12×20 矩形 thumb、2px surface 外框、4px 墨色轨道填充；拖动时强调中线，不画圆形 halo|
|Segments|选中底块平移、标签颜色与项目反馈；底线及墨色聚焦擦出|
|TextField|搜索框保持底色，Light 黑色外框／黑底线，Dark 主题色外框／白底线；聚焦底线从左向右填充。原生编辑器负责光标几何、选区与输入法；1.7 局部补丁仅为 IrisMenus 文本框提供黑／白高对比光标|
|Select|触发器三角旋转，popup 淡入淡出及偏移；半宽强调顶条，选中行灰底及左侧墨色标记，键盘高亮和滚动反馈保留|
|Tooltip|反转主题、有箭头；200ms 淡入并归位 4px，退出仅淡出；离开目标保留文字，不截获输入|
|SideRail／Tabs|rail 指示面板几何移动，未选中项悬浮时文字与底色按 200ms 过渡，选中项保留反转色。Tabs 文字在 300ms 内左移 12px，为 24px surface-raised 箭头块让位；选中项不再叠加悬浮底色。目录文字在 200ms 内由 Muted 过渡至 Ink，不增加悬浮底块|
|Accordion|300ms 可见高度过渡，无内容淡入；透明分隔列表、展开标题加粗，加号旋转为减号|
|ScrollView|章节定位及键盘滚动按 Enter；到达章节高亮约 2.5 秒；直接滚动打断程序动画。3px 黑／白 thumb、透明轨道、12px 捕获区，不沿用原版粗黑条；Select popup 使用同样的高对比细条|
|PageTransition|最新页面立即原位替换，220ms Iris 透明度入场，无横移或空白等待；可移交初始焦点|
|Footer|Enter／Exit 透明度；隐藏即禁用 action 输入，退出仍绘制|
|Drawer|不透明面板 300ms ease-out 滑动，遮罩独立 200ms 淡入淡出；深色斜纹标题带，关闭图标悬停旋转 90°|
|Dialog|自然尺寸面板从下方 8px 归位（300ms ease-out），200ms 透明度，退出原地淡出；居中标题、深色斜纹带及边缘点阵，不缩放内容|

`UiToast` 每个 surface 保留一个实例，提供 `Show(message, tone, durationSeconds)`、`Dismiss()` 和 `DefaultDurationSeconds`。时长限制为 1–10 秒，默认 3 秒，从首次 Repaint 开始计时；相同的有效消息刷新计时，其他消息替换当前内容。样式为底部居中的非模态深色提示、状态图标、关闭按钮和剩余时间底线，不抢焦点。每个 pass 在根坐标调用 `Draw(viewport, scale)`：非 Repaint 在正文前处理命中，Repaint 在正文后绘制，均位于 BeginFrame／EndFrame 之间。

消费者可在游戏线程调用 `MenuRegistry.ShowToast(message, tone)`；它仅向当前打开且未关闭中的全屏宿主投递，旧版、空消息或无窗口返回 false。宿主遵循玩家的提示开关；停用时丢弃消息。显式消息在打开动画结束后显示，替换当前消息。自动编辑说明只在正文实际修改后等待 3 秒，连续修改重置计时；普通导航、方向键或鼠标移动不触发说明。关闭或新旧模式切换不保留待显示消息；不改变原有保存时机。

保留的 Drawer／Dialog 从打开时记录焦点，最上层退出完成后尝试恢复原焦点。调用方仍需保证目的控件有效，并让组件继续参与 IMGUI pass。组件动效不包括宿主的主窗口开关、保存失败门控、暂停／Frost 资源持有或第三方 drawer 的内部动画。

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

Development and testing paused at the user's request on 2026-10-24; work resumed
for prototype fidelity alignment on 2026-10-25. Full acceptance remains deferred, not passed.
The 1.7 provider/API consumer build succeeded with no warnings or errors; the existing
engine-double suite passed 205 assertions. Limited isolated-game checks exercised
factory/draw retry, save-failure navigation/close gates, keyboard focus and dynamic
page removal. These checks do not establish full UI, lifecycle or compatibility acceptance.
The isolated instance was stopped normally. No release version was assigned.

## 原型样式对齐 — 2026-10-25

本轮按 `tmp/light-ui-prototype/src` 的最终 CSS 与已安装 `@endfield-ui/react` 源码对齐，不复制网页会话中的虚构设置。当前组件接口与时序以上文为准；本节继续记录后续对齐及其实际验证边界。

- 换页改为立即替换与 220ms 原位淡入；Tabs 恢复选中底色和箭头方块。表单底线、按钮竖条变箭头、黄色开关 thumb、矩形 slider thumb、Select 和 Tooltip 装饰及主题已对齐。
- 宿主使用连续 sheet、透明说明区、实际滚动章节标记及单项展开说明。深色 footer 透明；图标、字号层次、预览网格与斜纹、Dialog／Drawer 深色标题带及独立进退场节奏已更新。滚动条改为细墨色 thumb 与透明轨道。
- 不新增字体文件，继续使用游戏字体；字体度量、矢量图标栅格化、环形近似阴影与网页实现并非逐像素相同。网页响应式布局按游戏视口适配，不能据本轮桌面截图推定所有缩放比例一致。
- 最终 1.7 provider／API consumer 构建为 0 warnings／0 errors；既有 `RuntimeChecks` 通过 205 项断言（不覆盖原生 UI 渲染）。标准脚本完成 22 文件暂存及 SHA-256 校验，未部署到安装目录。
- 在隔离 RimWorld 1.7.4985、1280×720、UI scale 0.9375 主菜单实例查看了 Light／Dark 外观页、深色抽屉与帮助对话框；通过页面 `Focus("background")` 观察滚动到背景项及 TOC 标记切换。仅用反射设置隔离状态，不等于真实鼠标点击验收。
- 捕获日志含 `FrostSceneBinding probably needs a StaticConstructorOnStartup attribute` 资源线程警告；本轮没有把日志描述为完全干净。完整交互、拖动、输入法、多 Mod 兼容、动态录像及性能验收未执行。实例正常退出，私有游戏链接及复制目录已清理，截图保留在 `C:/RimCarbon14/iris-fidelity-20261025/RimCarbon14/artifacts/`；未加载生存存档，未变更保留的 1.6 发布文件。

### 控件细化与资源归档

- Sidebar 只读取所属 Mod 的 `About/ModIcon.png`，不借用元数据指定的其他纹理或默认齿轮。缺失时保留相同图标列；窗口持有缓存并在释放时销毁。分类标题缩小淡化，使用 `//` 前缀。
- 滚动区域与 Select popup 改用黑／白高对比细条。搜索框新增明暗主题焦点外框，底线保留 300ms 左至右填充及 200ms 收回；1.7 光标补丁仅作用于命名为 `IrisMenusTextField` 的样式，保留原生编辑器及 IME 路径。
- `UiGlyph` 改为原型同版本 Lucide 0.468.0 的 14 个官方图标，离线栅格化后随包分发；游戏中不下载、不解析 SVG。完整来源、哈希与许可保存在根 `NOTICE`，帮助页不再放版权按钮。endfield-ui v1.2.1 的实际许可为 MIT，未沿用此前错误的 Apache-2.0 标注。
- 本次 1.7 provider 与 API consumer 均构建成功，0 warnings／0 errors；既有 `RuntimeChecks` 通过 205 项断言。标准脚本完成 37 文件暂存与 SHA-256 校验，仅复制到私有游戏实例，未部署至正常安装目录。
- 隔离 1.7.4985、1280×720、UI scale 0.9375 实机截图确认浅色黑外框／黑光标、深色黄外框／白光标、侧栏空图标对齐及帮助页。运行时确认 14 个 glyph 纹理均已加载；缺图标 fixture 的槽位为 null，IrisMenus 槽位为真实纹理。反射设置滚动位置后被限制至 70，实际内容与 thumb 移动；减少动态效果时，焦点底线与外框进入值均为 1、失焦值均为 0，且等于目标值。
- 真实桌面输入脚本被 Windows 执行策略阻止，未绕过；鼠标拖动、真实键入、输入法与快速反向动画未验收。上述焦点及滚动通过反射切换，不冒充真实输入验证。日志保留 `FrostSceneBinding` 资源线程警告和扫描到的其他 Mod 元数据依赖警告；未观察到补丁或纹理加载错误。
- 实例正常停止，未强制终止，未加载生存存档；截图留在 `C:/RimCarbon14/iris-refinements-20261025/RimCarbon14/artifacts/`。私有游戏副本、资源链接和临时输入脚本在本轮结束前清理；未改动保留的 1.6 发布文件。

### 三角形与悬浮反馈补齐

- 修正按钮竖条变形的终态比例；沿相同四个顶点插值，三角形最后两个点重合。添加 Mod 按钮保留独立的 Plus 图标，文字不随记号移动。Tabs 使用原型三角形路径比例与独立箭头底块，未选中文字恢复 Ink，去除非原型的选中加粗。
- 侧栏未选中项的文字随悬浮逐渐提亮；选中侧栏和 Tab 不再叠加 hover tint。右侧目录按项保存颜色动画，当前章节及悬浮目标使用相同 Ink 终态，减少动态效果时直接到达目标。
- 1.7 provider／API consumer 构建均为 0 warnings／0 errors；37 文件暂存与 SHA-256 校验完成。隔离 1.7.4985、1280×720 实机使用临时探针注入物理指针坐标与固定 UI 时钟，执行真实绘制及命中路径；不是 OS 鼠标输入，也未部署探针至正常安装目录。
- 实际按钮顶点在 0／150／300ms 各不相同，终态三边为 11.4003／11.4003／11.4000，原竖条长 19；100ms 退出后再次进入的起始顶点与退出当前顶点一致。减少动态效果时进入立即成为三角形，离开立即恢复竖条。侧栏文字实测 RGB 从 0.329 经 0.182 到 0.145，目录文字也观察到中间色；明暗主题 Tabs、切页箭头块及悬浮背景已截图查看。
- 捕获日志存在 `Prefs.get_UIScale → UI.ApplyUIScale → Root.OnGUI` 空引用，以及此前的 `FrostSceneBinding` 和其他 Mod 元数据警告；未将日志描述为无错误。未观察到新增控件绘制或探针补丁错误。实例正常停止，截图保留在 `C:/RimCarbon14/iris-hover-20261025/RimCarbon14/artifacts/`，临时探针及私有游戏资源在验证后清理。

### 实机反馈修正：输入、导航与设置库

- `UiContext.Input` 只继承显式禁用，不把当前 modal/layer 不匹配写入 `GUI.enabled`；内容、popup、Dialog 在各自输入层判定权限。结束 pass 时消费叠层未处理的输入，防止点击和滚轮落到被遮挡页面。
- 搜索装饰只跟随编辑焦点；点击字段外立即失焦，Esc 优先退出聚焦的搜索框，即使查询为空也不同时关闭宿主。键盘聚焦外框只由 Tab、方向键等导航启用；Dialog 关闭恢复进入前的焦点可见性，避免 Esc 给鼠标点击过的按钮留下主题色外框。
- 面包屑先保留路径文字与分隔箭头宽度，Mod 名称使用与实际绘制一致的字号度量，必要时单行省略；悬停省略名称才显示小字、可换行的全文提示。
- 同一 Mod 的 SubItem 共用以 page ID 为身份的 Tabs，持续更新所有项目的进入／退出进度。页签宽度限制为 120–280 倍界面缩放；达到 16 个中日韩字符或 24 个拉丁字母／数字时中间省略，空间不足继续按实际宽度收缩文本。超出一行使用斜纹分页条，不改为下拉菜单；键盘可跨分页导航。
- SideRail 文字仅在移动选中面板实际覆盖的区域反转颜色，不先于面板整行变白。
- 添加 Mod 使用独立桌面设置库列表页，显示 `About/Preview.png` 与当前游戏语言的注册别名，未注册别名时使用 About 名称。预览按可见项加载并由窗口缓存、释放；缺失时使用中性占位文字。托管设置可选择显示／隐藏，已接入页面始终显示；操作不启停游戏 Mod。
- SubItem 分页条位于右上方；布局或缩放变化后重新定位当前选中项所在页。侧栏长名称单行省略并提供全文提示；SubItem 保留标题首尾，中间省略。
- RimWorld 1.7 的屏幕坐标转换同时计入 GUI group 原点和 `GUI.matrix`，用于下拉定位、命中、滚动锚点及拖动距离。滑块与两类滚动条的拖动行程统一使用屏幕单位，避免缩放后数值变化比例失真。
- 删除页面左下角“离开页面时保存更改”及勾号，只保留右侧恢复默认操作；保存时机不变。
- 最终 1.7 provider／API consumer 构建成功，0 warnings／0 errors；既有 `RuntimeChecks` 通过 205 项断言。XML、本地化与加载目录检查通过，中英文各 98 个唯一且一致的 key。
- 隔离 RimWorld 1.7.4985、1280×720、游戏 UI scale 0.9375 中，通过临时探针向实际 IMGUI pass 注入鼠标／键盘事件，验证搜索失焦与 Esc、下拉提交、Dialog 操作与关闭、重置确认、设置库显示控制、快速切页和缩放后选中页可见。滑块半程拖动从 0.50 到 0.75，内容滚动条半程到 240，下拉滚动条半程到 144；嵌套锚点滚动限制到最大值 480。明暗主题截图确认移除保存提示，减少动态效果时确认框立即开关。
- 上述是实机事件注入及截图验证，不等于真实桌面鼠标、键入或 IME 验收；本轮未做多 Mod 压力、动态录像及性能验收。此前捕获的 `Prefs.get_UIScale` 空引用、`FrostSceneBinding` 和 Mod 元数据警告不能据此视为已修复；最终日志队列未能通过桥接读取，不作日志无错误声明。截图保留在 `C:/RimCarbon14/iris-fixes-20261025/RimCarbon14/artifacts/`。
- 验证窗口通过右上关闭按钮退出，私有游戏进程正常停止；临时探针、私有游戏副本与资源链接已清理。未加载生存存档，未部署到正常安装目录，未改动保留的 1.6 发布文件。


## 物品组件与注册表验收 — 2026-10-26

- 最终 1.7 provider／API consumer 构建为 0 warnings／0 errors；RuntimeChecks 通过 246 项断言，覆盖价值边界、阈值编辑、分档增删及非有限值。RHAH 770 项离线回归通过；测试编译仍有 FormatterServices 过时 API 与既有 xUnit 警告。
- 隔离 RimWorld 1.7.4985、1280×720、游戏 UI scale 0.9375 中，用临时探针向实际 IMGUI pass 注入事件。完整核对 3403 个实际 ThingDef、78 个原版分类、按名查询与直接分类归属，注册表诊断为 0；搜索 Silver 返回 MineableSilver、Silver、Blueprint_SilverTile、Frame_SilverTile，键盘可切换分类。诊断按钮与完整成员日志导出已执行。
- 明暗主题截图检查注册表与物品列表；注册表页改用原生主题控件，避免旧 Widgets 的浅色文字对比度问题。只读注册表行不显示可编辑勾选框。
- 调色盘实测预设只修改草稿，Esc 不提交，“应用”才修改设置；实际增档使数量 5→6，删除中间档使数量 6→5。恢复默认得到 0／50／200／500／1500 及灰色首档；真实 Scribe 深保存重读保留档数、阈值、颜色，加载后非同一条目实例。
- 当前 RHAH 构建的五类适配器在真实游戏绘制路径打开；候选数分别为救济 376、普通投喂 376、乞讨 11、索粮 11、衣物 2。普通投喂逐项点击确实修改隔离设置，三种分类选择确实写回 0／1／2。该实例未启用完整 RHAH，适配器使用私有 unsaved settings，因此出现未加载的 RHAH 翻译键；这不构成完整 Mod、本地化或生存存档验收。
- XML 解析、加载目录与中英文各 124 个唯一且一致的 key 通过；37 文件仅暂存并校验 SHA-256，未部署到正常游戏目录。截图位于 `C:/RimCarbon14/iris-items-20261026/RimCarbon14/artifacts/`。
- 实机事件注入不等于 OS 鼠标、IME、多 Mod 压力或性能验收。最终实例启动日志仍有 FrostSceneBinding 静态初始化提示，先前 Prefs.get_UIScale 空引用也未在本次修复；不作全日志无错误声明。保留的 1.6 发布内容不代表这些新 API 已完成 1.6 运行时验收。
- 验证窗口已关闭、临时补丁已卸载，隔离进程正常停止（未强制终止）；私有游戏副本、探针和保存重读临时文件随后清理，仅保留截图证据。

## 托管主题与开关验收 — 2026-10-26

- 1.7 全屏宿主在选中 drawer 的调用范围内适配常见背景、按钮、滑块、默认勾选／开关及输入框外观。带标签的默认 `Widgets.CheckboxLabeled` 和标准 `GUI.Toggle` 使用开关；自定义勾选纹理和控件背景不替换。输入框保留原生编辑器、控件名称和文本索引；旧窗不启用这层适配。
- 普通及富文本在最终文本片段绘制时修正主题对比度，不改写原字符串或颜色标签。保留颜色语义与调用方／富文本 alpha，低对比颜色向黑或白调整；42 组颜色／常用表面组合在 Light／Dark 实测 RGB 对比度均不低于 4.5:1。故意半透明文字的合成对比度、自绘底色／图片／着色器不在保证范围内。
- provider／API consumer 构建成功，0 warnings／0 errors；既有 RuntimeChecks 通过 246 项断言（engine doubles，不验证绘制）。隔离 1.7.4985、1280×720 主菜单实例查看了明暗主题及旧版截图，默认开关、禁用开关、矩形滑块、输入底线和按钮外观均已检查。
- 临时探针向实际 drawer 注入事件及对应鼠标状态：两类开关均从 true 切到 false，Widgets 滑块从 0.25 到 0.75，GUI 滑块从 0.40 到 0.65，两类按钮合计激活两次，命名输入框从 `editable` 编辑为 `editableZ`。新旧往返后保留这些值；回调外主题作用域为空，旧版截图仍为原版勾选与滑块。
- 上述为隔离实机事件注入，不是 OS 鼠标／IME 验收；未验证连续动画录像、多 Mod 压力、任意布局及性能，也未完成全日志审查。截图保留在 `C:/RimCarbon14/iris-hosted-20261026/RimCarbon14/artifacts/`；未部署到正常游戏安装目录，未改动保留的 1.6 发布文件。

- 验证进程已正常停止（非强制终止）；临时探针、反编译文件与私有游戏目录已清理，仅保留隔离配置和截图证据。


## 托管导航与文档验收 — 2026-10-26

- 最终 provider／API consumer 构建为 0 warnings／0 errors，246 项 RuntimeChecks 通过。ODF 适配程序集与 Advanced RimTalk 构建同样无警告、无错误；Advanced RimTalk PromptChecks 通过。
- 隔离 RimWorld 1.7.4985、1280×720、UI scale 0.9375 中检查 ODF 明暗正文、章节侧栏、只读原文及统一编辑操作；新旧界面往返保留原文模式。复制正文与内嵌源文仅相差末尾换行；独立字符串往返确认 1.7 剪贴板接口读回时同样去掉末尾换行。
- Advanced RimTalk 文档 `language/string.md` 在 Light／Dark 下检查语法色和表格背景。实际 RimTalk 详细页切换至 PromptPreset，滚轮位置达到原生内容最大值；这些结果不构成 RimChat 1.7 兼容承诺。
- 托管标准 Tab 回调只触发一次。实际原生滚动条双轴拖动到 `(313.1342, 725.0937)`，释放后位置保持；补丁仅替换轨道和滑块绘制，保留原生输入。验收使用临时事件和持键注入，未验证 OS 鼠标、IME 或连续动画录像。
- 新界面选择在隔离进程重启后恢复。截图位于 `C:/RimCarbon14/iris-six-20261026/RimCarbon14/artifacts/`。验证进程正常停止，临时输入探针与私有游戏副本清理。
- IrisMenus、Oxygendsl 和 AdvancedRimTalk 已部署到正常安装目录，DLL 与验收版本 SHA-256 一致；15 个既有旧版文件与发布 ID 的哈希保持不变。

## 背景、轻提示与主窗口动效 — 2026-10-26

- 1.7 全屏窗口使用 720ms 逐列方块缩放：8 行，方块边长取视口高度的八分之一；同列同时缩放，按列从左向右打开，关闭反向。打开或关闭捕获一次当前窗口绘制输出，动画期间不重复执行托管页面回调；提前关闭沿原捕获帧反向，视口变化时重捕获。减少动态效果跳过捕获并直接到终态。关闭终点消费绘制，避免最后一帧重新显示完整窗口。
- 捕获借用 1.7 原生 ImGui renderer 的私有接口，仅重定向当前窗口 draw list，不读取屏幕；同步烘焙为 GPU 纹理，动画结束／重开释放纹理，窗口销毁时释放独立 draw list。游戏升级可能改变这些私有接口，捕获异常记录错误并结束转场，不能据此承诺后续游戏版本兼容。1.6 源码仍使用原透明度转场，保留发布二进制未更新。
- 背景增加低对比静态几何印花及左下裁切跑马灯；文字在外观设置编辑，空文字隐藏，减少动态效果时静止。底栏从所选 Mod 的 `About/About.xml` 读取 name／author／modVersion，按可用宽度省略名称和作者；缺失字段留空，不虚构版本。设置库与全局搜索不显示某个 Mod 的元数据。
- 行为设置增加轻提示开关与 1–10 秒显示时长。首次保存说明和保存成功提示使用同一空闲策略；不改变原有离页保存时机。搜索框、普通原生文本框和托管标准输入框共用焦点底线与明暗光标样式，保留游戏编辑器。
- 本轮隔离 RimWorld 1.7.4985、1280×720 实际绘制检查了浅／深色、背景印花、长名称与作者／版本底栏、Toast 状态图标与倒计时底线。冻结在 52% 的打开帧中，页面 Repaint 回调计数始终为 1；关闭样本在约 720ms 内反向结束，捕获期间计数保持不变。关闭终点探针修复前返回 false，修复后返回 true，实际窗口随之退出。
- 空闲探针在最后输入后约 2.57 秒仍待显示，约 3.02 秒启动提示；配置 3 秒后自动关闭。关闭提示开关后等待超过 3 秒仍无待显示或活动提示；减少动态效果时开关窗口直接到终态。这些是反射驱动的实机绘制和状态采样，不是 OS 键入／IME、连续录像、多 Mod 压力或性能验收。截图保留在 `C:/RimCarbon14/iris-polish-20261026/RimCarbon14/artifacts/`。
- 1.3× UI scale 在同一 1280×720 隔离实例中仍保持侧栏、主面板和底栏边界，长侧栏／面包屑／footer 元数据继续省略而不越出视口；模式切换往返完成后窗口正常关闭。跑马灯连续两次间隔 1.2 秒的捕获保持在其裁切区域内。

## 交互修整与手动验收交接 — 2026-10-26

- 已实现单一跨组 Sidebar 指示器、已固定分组、右键固定／别名／组内排序／托管移除。玩家别名优先于 Mod 别名，清空后恢复默认名称；排序不跨组，移除仅关闭托管接入。
- ThingsList 分组使用 300ms 高度过渡与加减号动画；代码块使用独立背景、边框和等宽文字，章节定位高亮不再横切整个正文。Light 普通输入框和列表开关加强浅灰底对比。
- 本轮隔离实例已观察到实际文本编辑重置说明计时、方向键不重置、Toast 关闭不穿透；ThingsList 展开和收起均采样到 0–1 间中间值。托管 HoverCard 延迟显示，移入卡片保持、移出关闭；背景跑马灯持续运动。输入使用临时探针，不等于 OS 输入或 IME 验收。
- 按用户要求停止自动验收；侧栏完整交互、代码块与章节定位、跨重启持久化及多缩放交由手动检查，不宣称全部通过。隔离测试实例保持运行，当前未安装输入注入补丁；未将本轮版本部署到正常游戏目录。
- 修复 ThingsList 分组标题悬浮后不可见：标题与加减号使用 Ghost 按钮当帧返回的前景色，和底色共享 hover／press 进度，不另建颜色计时器。1.7 provider／API consumer 构建为 0 warnings／0 errors；临时绘制边界探针验证 Light／Dark 中间色、反向、按下及减少动态效果，不代替修复后的游戏视觉验收。
- 用户随后确认其余项目基本正常，并允许关闭测试实例；已发出正常退出请求。本次颜色修复尚未部署到正式游戏目录。

