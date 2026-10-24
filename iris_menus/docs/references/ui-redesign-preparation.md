# IrisMenus 完整原生宿主实现准备

本文将双主题网页原型与现有游戏宿主连接起来，供下一步完整实现使用；不代表新宿主、公开呈现接口或游戏验收已经完成。架构方案见 [ADR 0001](../adr/0001-native-settings-presentation.md)，状态仍为 `proposed`。

## 本轮范围与决定状态

用户已认可原生组件总体；本轮修正点击黄色 outline、Endfield 矩形直角风格及追加报告的 choice／input 悬停底色消失，并准备完整新宿主，不直接实现主宿主。点击与键盘导航焦点需要区分，不能通过删掉所有焦点反馈消除黄框。圆形语义图元不因“矩形直角”要求一律变方。组件实现与验收边界见[原生组件参考](native-ui-components.md)。

|状态|内容|
|---|---|
|已明确要求|组件修正；完整新宿主准备；旧版默认入口保留；新入口位于截图所示旧底栏 checkbox 下方／remember 同行；不增加现有底栏高度；不放无功能入口|
|必须保持的现有契约|同一 page ID、单一选择和保存所有权、失败留页／留窗、原版托管 opt-in、唯一内容滚动所有者、搜索不执行未选 drawer|
|本报告建议的实现组织|`MenuWindow` 独占窗口生命周期，旧／新版内部呈现共用 session、browse 和 catalog；可选章节／说明附加在现有 page ID 上|
|仍待产品决策|1.6 是否同步交付、legacy Light 策略、搜索是否扩展字段编辑／计数／重置、owner 范围是否扩大、更多状态是否跨重启记忆、日后是否改用新版默认入口；见文末|

网页原型在 [`../../tmp/light-ui-prototype/`](../../tmp/light-ui-prototype/)，其 `App.tsx` 使用 React 会话状态，`styles.css` 提供双主题、近黑 Light 底栏与窄屏布局。原型阶段的浏览器记录和截图只能作为视觉输入，不能证明游戏行为；示例数据、密度／缩放控件、计数与重置也不是游戏端契约。本轮组件构建、真实程序集冒烟与隔离主菜单截图证据记录在[组件参考](native-ui-components.md#开发者入口与验证状态)；它们不代表新宿主已实现或已通过验收。

## 现有能力与必须保持的语义

|领域|已有行为|重设计约束|
|---|---|---|
|页面注册|`Register` 使用 `OwnerId(owner) + ":" + pageId`，保留 `Action<Rect>` 和 `save ?? owner.WriteSettings`；`RegisterListing` 额外封装 `MenuScrollView`|复用 `MenuRegistry`；不得另建页面树、复制设置值或从任意 drawer 推断控件树|
|页面选择|`MenuSession.Select` 离页先保存，失败不切换；同实例不保存；`Rebind` 直接替换|rail、tab、结果和外部打开共用唯一 session；视觉选中在门控成功后改变|
|关闭|`OnCloseRequest` 先 `session.Save()` 再 `SaveWindowSettings()`，失败保持窗口；`PreClose` 是强制移除兜底和资源释放|关闭动画须在真正移除前完成；失败不进入 Closing；不能让新旧宿主分别保存|
|搜索|`MenuCatalog` 分批枚举文本元数据与匹配，`PageSize=20`；`Focus` 是单独回调|复用 catalog 与有效命中；无元数据／focus 时只导航，不伪造字段编辑或精确定位|
|动态分类|`ReplaceSubItemListings` 复用同 ID 的 `MenuPage`／`MenuScrollView`；`ReplacementFor` 为删除页找替代|Revision／语言变化时重建 catalog 和 Rebind；不走普通离页保存，不执行被移除页淡出 drawer|
|章节绘制|`MenuSectionView` 具有稳定 ID、Measure/Draw、LayoutKey、可见范围绘制和 Focus，自持滚动|复用测量与定位；当前没有标题、目录、说明元数据，不能写成已可自动生成目录|
|旧页面兼容|`SettingsAdapter.Create` 直接转发 `DoSettingsWindowContents`；Character Editor 是点击后启动专用窗口的 adapter|保留 opt-in 和特殊启动语义；不得强加第二层滚动、扫描私有字段或额外绘制 drawer|
|owner actions|现有 `DrawOwnerActions`／`Activate`／`Run` 保留可见、可用、确认与异常反馈|完整宿主必须保留；`ActionLauncher` 不应变成没有内容的设置 tab|

源码：[`MenuRegistry.Register／RegisterListing／ReplaceSubItemListings／ReplacementFor`](../../Source/MenuRegistry.cs)、[`MenuSession.Select／Save／Draw`](../../Source/MenuSession.cs)、[`MenuWindow.OnCloseRequest／PreClose／RefreshCatalog／DrawOwnerActions`](../../Source/MenuWindow.cs)、[`MenuCatalog.Query／Advance`](../../Source/MenuCatalog.cs)、[`MenuSectionView`](../../Source/MenuSectionView.cs)、[`SettingsAdapter.Create`](../../Source/SettingsAdapter.cs)。

搜索的预算是枚举步骤数，不是主线程毫秒保证，也不是后台线程；provider 的单次调用仍可能阻塞。导航按内容包合组，但搜索范围使用 `MenuPage.OwnerId`；同内容包多个 Mod 类型时，现有 owner 范围不天然等于网页里的“整个 Mod”。

保存是消费者持有的即时修改与离页持久化，不是事务提交，也不提供取消回滚。详情以现行 [接入指南](guide.md) 为准。

## 旧宿主入口的精确几何

以下为下一步接入约束，本轮不添加按钮。入口必须真正打开完整新版宿主，不能打开组件展厅，也不能只有占位响应。`MenuRegistry.Open()`、`MainButtonWorker_IrisMenus.Activate()` 和 `SettingsEntryRouter.Prefix` 仍默认进入／定位旧宿主；新模式仅由旧底栏的显式入口进入，是否日后成为默认入口未决定。

几何使用 `MenuLayout.Calculate` 产生的本地 UI 单位，不能把 1280×750 截图当作 `inRect` 坐标。截图中的宽版同行要求适用于 `layout.MainButton.height == 24f` 的 inline 分支：

```csharp
Rect nativeEntry = new Rect(layout.MainButton.x, remember.center.y - 12f,
    layout.MainButton.width, 24f);
```

`remember` 在 `MenuWindow.DrawFooter` 中为 `(toggleX, toggleY + 26f, ..., 22f)`。普通宽版 `toggleY = footer.y + 14f`，因此 `remember.y = footer.y + 40f`，中心为 `footer.y + 51f`；新入口 y 为 `footer.y + 39f`，比 22 高的 remember 向上 1，中心完全对齐。原 mainButton 在 `footer.y + 14f` 到 `+38f`，入口在 `+39f` 到 `+63f`，互不重叠，仍位于原 72 高底栏内。x／width 必须直接复用 `layout.MainButton`，不另算一列。

窄版／长翻译触发换行版时不能声称仍满足截图的 remember 同行：非 inline 的 mainButton 已移到更下方；compact 的 remember 旁边还有现有滑条。固定布局方案是在已有 44 高槽内拆成两行，保持该槽 x／width：

|`MenuLayout.Calculate` 分支|原 Footer 高|既有 mainButton 槽（相对 footer.y）|上行 checkbox|下行新版入口|
|---|---|---|---|---|
|非 compact、inline|72|`+14`，高24|原槽不变|`remember.center.y - 12`，高24|
|非 compact、非 inline|120|`+68`，高44|槽顶起高20|槽顶 `+20` 起高24，即 `footer.y + 88`|
|compact（width < 560）|152|`+100`，高44|槽顶起高20|槽顶 `+20` 起高24，即 `footer.y + 120`|

44 不能容纳两个 24 高命中区。所以上行必须取消长标签自动换行，单行省略并提供完整 tooltip，checkbox 绘制与点击范围都限制在上 20 内；下行入口的文本也单行省略并有完整 tooltip。两行边界相接但不重叠，不把透明命中区扩到另一行；标签测量、tooltip 和实际 hit rect 使用同一几何。继续沿用现有 inline 判定和 72／120／152 高度，不因增加入口而让 body 缩短、底栏外溢或另开一行。

依据：[`MenuLayout.Calculate`](../../Source/MenuLayout.cs)、[`MenuWindow.DrawFooter`](../../Source/MenuWindow.cs)、[`IrisMenusMod.MainButtonWorker_IrisMenus`](../../Source/IrisMenusMod.cs)、[`MenuRegistry.Open／ResolveEntry`](../../Source/MenuRegistry.cs)、[`SettingsEntryRouter.Prefix`](../../Source/SettingsEntryRouter.cs)。

## 单一宿主所有权与状态转换

建议让 `MenuWindow` 继续拥有唯一 `MenuSession`、`MenuBrowseState`、pages、catalog 和 Frost 生命周期，旧版／新版只替换内部呈现实现。这是实现准备方案，不是已存在的宿主切换接口。避免并排新建一个也能保存、恢复 HostView、绘制相同页面的 Window；组件 `UiContext` 只负责呈现／输入，不能再持有业务当前页。若最终采用不同窗口类型，仍须在任意时刻只有一个活动页面绘制／保存所有者。

切到新版时交接同一 selected page、query、scope、ResultPage、resultScroll、ListingScroll、托管选择和错误状态；不调用 `Select` 切同页、不调用关闭保存，不修改旧默认入口。全屏模式需要独立维护显示几何，不将全屏大小写回现有旧窗 `Width/Height/X/Y`；返回旧呈现按旧几何恢复。新版首次打开、退出及返回旧版的动效都服从同一输入层与关闭门控。

|动作|现有符号／顺序|新版必须保持|
|---|---|---|
|rail／tab／全局／管理导航|`session.Select(page/null)` → `browse.Navigate/Manage` → 更新记忆、滚动、导航|保存失败不提交任何视觉导航；普通 Navigate 清 query，scope 为 page.OwnerId，结果分页回0|
|外部指定打开页面|`SettingsEntryRouter.Prefix` → 现存窗口 `OpenPage`，或构造带 requestedPage 的 `MenuWindow`|指定页优先于记忆页；若选择保存失败，保留原窗原页，不额外开窗绕过失败|
|输入／清除查询|`browse.Search` → 清结果和管理列表滚动 → `catalog.Query`|退出 target 模式、ResultPage 回0，保留 scope 和 selected；现有动作不额外保存，不擅自改成离页提交|
|点击搜索结果|`session.Select(hit.Page)` → `browse.OpenTarget` → `RememberPage` → 有效时 `hit.Page.Focus(hit.Anchor)`|保留 query、scope、ResultPage 和结果滚动；保存失败仍在原结果／页状态；focus 异常不撤销已成功的选页，也不关闭窗口|
|目标页返回结果|`session.Save()` → `browse.Back()`|失败保持目标页；成功恢复原查询、范围、分页与结果滚动，不调用 Navigate 清查询|
|目录／说明／抽屉切换|新呈现的会话状态|不建立新的页面身份、不触发离页保存；只展开目标所在内容，待布局后交由原滚动所有者定位|
|Revision／语言变化|`RefreshCatalog` 释放 catalog → `RebindSelection` → 新 catalog；`ResolveReplacement` 同 ID、同 registration 最近项、同 owner/content 后备|不保存失效页；同 ID 保留支持的滚动；被删页旧 focus、overlay 和动画状态作废，不再绘制旧回调|
|正常关闭|`session.Save()` → `SaveWindowSettings()` → Closing → 正常最终移除|任一步失败保持原窗与值，不进入 Closing；成功后不重复保存；暂停、遮挡输入、Frost 保持至移除|
|强制移除／返回主菜单|`PreClose` 现有兜底保存与 finally 释放|不被退出动画阻止；与交互关闭区分，最终释放 catalog、Frost 与呈现状态|

`MenuSession.Draw` 的失败隔离与 Retry 也须保留，`ExitGUIException` 继续抛给 IMGUI；不能为了转场吞掉退出异常或在 Repaint 外少调一次 legacy drawer。保存失败不意味着内存回滚：消费者值已即时修改，`SaveWindowSettings` 也先采集内存状态再 WriteSettings；要求是保留页面／窗口并可重试，不显示“已保存”或假称事务撤销。

记忆沿用 `RememberState`、`LastPage`、按 OwnerId 的 `LastSubItems` 与 `HostView`（mode/query/scope/resultPage/target、列表滚动、`listing:` 前缀）。关闭记忆时沿用 `ClearRememberedView` 并重置 Listing；自定义 `MenuSectionView` 的私有滚动不在现有持久化范围。`RestoreOpeningState` 的显式请求优先、失效范围回退、目标页缺失和索引未完时的结果滚动恢复也要保留。新增 theme／减少动态效果是独立偏好，不应被清除页面记忆连带重置；新抽屉／说明的跨重启记忆仍待决策。

依据：[`MenuSession`](../../Source/MenuSession.cs)、[`MenuBrowseState`](../../Source/MenuBrowseState.cs)、[`MenuWindow.RestoreOpeningState／CaptureHostView／Select／OpenPage／DrawSearch／DrawResults／RebindSelection`](../../Source/MenuWindow.cs)、[`MenuSettings.ClearRememberedView`](../../Source/IrisMenusMod.cs)。

## 建议的最小新增契约

以下均为设计建议，没有选定或新增公开 API 名称。

### 页面呈现元数据

在既有 page ID 上附加可选描述，不创建平行页面注册体系：

- 明确页面是 legacy drawer，还是支持主题的原生呈现；不把换壳等同于全部第三方页面换肤。
- 显式章节 ID、本地化标题和目标锚点；说明 ID、标题及 Markdown 或 Measure/Draw 内容。
- 目录和搜索共用 page/section/anchor 身份；坐标由真实布局产生，不维护第二套位置表。
- 新原生呈现搜索定位到折叠内容时，先展开，再在有效布局后滚动与高亮；用户滚轮／拖动可打断自动定位。不存在的目标只打开有效页面，不对不存在字段显示定位成功。

没有描述的旧页面继续作为整页 drawer 展示，不生成假的目录或说明。Light 宿主搭配深色兼容内容区是待确认建议；若采用，legacy 调用范围内的背景、文字／GUI.skin 与 GUI 状态隔离必须成套处理，结束后还原宿主状态，不能只把背景画黑。任意硬编码白字、纹理或游戏皮肤的第三方页面都不能由宿主保证完整浅色化。

滚动所有权按现有类型保留：`RegisterListing` 使用既有 `MenuScrollView`（Focus 挂起到绘制找到 anchor 后定位），`MenuSectionView` 使用自己的 measure／layout／scroll，`Register(Action<Rect>)` 和 adapter 由原 drawer 决定。新版正文只给 viewport，不能给这些回调统一再套 `BeginScrollView`。新原生页面若由宿主管理滚动，也必须只注册一个明确的滚动所有者，再把目录和搜索交给它。

### 动画与输入上下文

内部统一管理语义主题、每帧时间快照、当前 geometry、绘制裁切与命中裁切、输入层所有权、稳定状态键，以及 focus/drag/closing 状态。legacy drawer 继续接收原 Rect，不因动画改变绘制事件契约或增加调用次数。

### 结构化字段不是换肤的前置条件

rail、tabs、目录和说明不需要先建完整设置 DSL。只有决定交付搜索结果内联编辑、通用修改计数或重置时，才增加消费方 opt-in 的字段 ID、类型、Get/Set、范围/选项、Visible/Enabled、保存归属、默认值/Reset 和脏状态声明。

`MenuSearchEntry` 当前只有文本元数据；不能靠执行原 drawer 来抽出控件。消费者没有提供默认值或脏状态时，应隐藏对应通用入口。

### 宿主设置

至少需要持久化 Theme 与减少动态效果选项。网页里的 density、scale、默认搜索范围、tooltip 延迟和 UI 音量不是现有 `MenuSettings`，只有真正纳入游戏行为后才增加，不能只显示控件。

现有字段见 [`MenuSettings`](../../Source/IrisMenusMod.cs)，其中没有 Theme 或减少动态效果偏好。新宿主需为这些真实行为增加读写，同时复用 FrostEnabled／Blur／Transparency、RememberState、ShowMainButton 和 EnabledAdapters，不再存一份镜像。主题只作用于 IrisMenus；[`GuiState`](../../Source/GuiState.cs) 还原调用范围的状态，不全局改原版 Widgets 或游戏皮肤。

## 完整动效之前的关键底座

1. **真实时间，每帧固定采样。** 使用真实时间而非游戏 ticks；窗口暂停游戏不应暂停 UI。Layout、输入与 Repaint 不能各自累加一次 delta。1.7 dispatch 先 Layout 再 Repaint，输入前也运行 Layout。
2. **语义身份与 IMGUI control ID 分离。** 长期状态使用 owner/page/section/control/channel；不用翻译标题、索引、Rect hash 或当帧控制编号。移动 slider 的布局可能改变原版拖动 ID，按钮按下/抬起也可能依赖 Rect。
3. **可打断、可反向。** 新目标从当前插值值重新定向；覆盖 A→B→C、抽屉开→关→开、主题快速切换和 accordion 反转。移除的稳定 ID 及时释放，不能积累无限状态表。
4. **绘制、命中、裁切共用同一几何快照。** 1.7 `PushGroup` 只裁绘制、不收紧 hit clip；仅用 `GUI.BeginGroup` 裁掉 accordion 并不能证明隐藏控件不可点击。覆盖鼠标、滚轮、拖动和键盘焦点。
5. **保持 Layout/Repaint 结构一致。** legacy drawer 不改成仅 Repaint 调用，也不为了淡出额外绘制旧页。主题、字体、密度变化使测量缓存失效；自然高度和动画可见高度分离。
6. **关闭状态机先保存再退出。** 保存失败保持原窗口；成功后进入 Closing，停止编辑但继续遮挡输入、保持暂停与 Frost 资源；动画结束走正常移除路径，避免重复保存/音效。返回主菜单等强制清理不应被动画阻止。
7. **减少动态效果立即生效。** 开启后当前转场直接到目标，不延迟关闭、不强制平滑滚动；保留选中、focus、hover 和错误反馈。尚未验证游戏的 OS 减少动态效果接口，不承诺自动继承系统设置。
8. **滚动与性能。** 用户滚轮/拖动应打断自动定位；实际 offset、目标 offset 和目录高亮共用布局。Listing 回调仍全量执行，不能称为虚拟化；长页复用显式章节的可见范围绘制。避免每控件每帧分配、重复抓场景、建材质或编译 shader。

游戏源码依据，来自用户提供的本地 1.7 参考树 `/mnt/d/References/Rimworld/Vanilla/1.7/Game/`，不是仓库发布内容：

- `RimWorld/Imgui/ImguiDispatch.cs:89–125`：Layout/Repaint 和输入 pass 顺序。
- `RimWorld/Imgui/ImGuiGuiBackend.cs:204–273`：group/scroll 裁切与 hit clip 分离；`504–538`：鼠标矩阵逆变换。
- `UnityStubs/UnityEngine/GUIUtility.cs:35–62`：递增 control ID；`GUI.cs:838–879`：按钮 Rect 匹配。
- `Assembly-CSharp/Verse/Widgets.cs:1959–1984`：slider 拖动身份取决于位置/范围。
- `Assembly-CSharp/Verse/WindowStack.cs:498–536`：`OnCloseRequest` → 音效 → `PreClose` → 移除 → `PostClose`；`213–218`：WindowUpdate。
- `UnityStubs/UnityEngine/Time.cs:39–79,124–134`、`UnityStubs/Waylith/UnityCompat/UnityTimeState.cs:34–63`：真实时间、frameCount 与兼容运行时来源。

## Frost 与版本边界

|项目|1.6|1.7|
|---|---|---|
|编译|net48，Unity|net10.0，UnityStubs/Waylith/原生渲染|
|背景来源|地图/世界相机；主菜单回退底色|提交帧槽借用场景色；主菜单采样基础插画|
|模糊|半分辨率目标、两次 Blit|fragment shader 25 次采样|
|资源|Camera/CommandBuffer/RenderTexture|Material；场景 GPU 图像由游戏持有|

复用现有 Frost 偏好、失败底色和资源生命周期；新原生矩形表面的直角要求不得因复用旧圆角路径而被忽略，旧宿主外观保持原义。不把 Frost 当作页面截图缓存或完整 UI backdrop blur。1.7 场景采样不包含后绘制的 IrisMenus 控件，主菜单采样也不包含全部叠层。

双主题需要普通底色和 shader `_Fill` 来自同一语义颜色；当前两者取原版深色 `Widgets.WindowBGFillColor`。整体退出 alpha 也要同时覆盖普通填充与 shader，不能只改 `GUI.color`；新版矩形直角几何须与两条绘制路径一致。

依据：[`FrostBackground.cs`](../../Source/FrostBackground.cs)、[`FrostPipeline.cs`](../../Source/FrostPipeline.cs)、[`FrostBackground17.cs:76–140`](../../Source/FrostBackground17.cs#L76)、[`Frost17.frag.glsl`](../../1.7/Rendering/Frost17.frag.glsl)。

现行默认构建和部署针对 1.7，并保留已有 1.6 产物，见 [接入指南的构建部署部分](guide.md#7-构建与部署)。源码计划兼容两版不等于已交付两版 DLL；未独立构建和实测前，不宣称新功能覆盖 1.6。两版模糊算法不同，也不应要求同参数像素一致。

## 推荐实施顺序

1. 固定既有保存、入口路由、原版托管和滚动基线；明确旧／新版共享唯一 Window／session 的实现组织，避免第二套页面和保存状态。
2. 接入现有原生组件与双主题令牌、GUI 状态隔离、底色／Frost 同步；静态完成 rail、真实 tabs、全局／owner 搜索、结果返回、管理页、owner actions 与 IrisMenus 真实设置页。保留绘制失败 Retry 和特殊 adapter。
3. 在同一 page ID 上增加可选章节／说明描述、主题能力与唯一定位／滚动声明；桌面辅助栏和窄窗口抽屉共用元数据，没有声明就不显示虚假目录。
4. 复用组件的帧时间／稳定身份／输入几何，补齐宿主窗口、rail、tabs、accordion、drawer、控件反馈与定位动效，以及减少动态效果持久化和保存门控关闭；legacy 不做双实时 drawer crossfade。
5. 完整新版确实可打开后才接入旧底栏入口，按上文三分支几何实现，不增高底栏、不改变默认打开路径；切换不丢查询、选择、滚动或未保存值。
6. 执行完整游戏验收并更新真实接入说明与消费示例。若产品决定加入字段内联编辑／计数／重置，须先完成其 opt-in 契约，不能放空控件或假成功提示。

## 待定产品边界

|待定事项|建议／当前基线|决定前不得假定|
|---|---|---|
|版本交付|先交付并验收 1.7，保留旧 1.6 产物|不能从共用源码推定 1.6 新宿主已交付|
|legacy Light 内容区|Light 宿主＋显式深色兼容区；支持 Light 的页显式声明|不能保证所有第三方 drawer 完整浅色化，也不能称此策略已被用户接受|
|通用字段能力|先保持文本导航搜索；内联编辑、修改计数、通用重置需另定字段契约|不把原型示例视为已确认交付，也不生成假计数／重置|
|“当前 Mod”范围|沿用 `MenuPage.OwnerId`；rail 仍按 Content 合组|owner 不等于整个内容包；扩大范围须迁移 scope 及记忆键|
|更多状态记忆|保留现有 HostView／Listing；新抽屉／说明为窗口会话状态|不声称自定义 view、accordion 和目录已跨重启保存|
|未来默认入口|本轮和下一步保留旧默认入口，只提供显式新版入口|不增加隐藏自动迁移、宿主偏好开关或默认切新版|

完整宿主动效的准备范围已列出，不包含第三方 drawer 内部动画。以上待定策略不影响本轮组件修正和入口几何的明确要求，也不表示已经获得对整份 ADR 的接受。

## 游戏内验收清单（尚未执行）

- 暂停与主菜单下动画正常；快速切页、切主题和反复开关可连续打断，无旧层输入。
- 入口在截图宽版与 remember 中心对齐；长翻译换行版和 compact 在原44高槽内拆行，单行省略／tooltip、点击边界正确，Footer 始终72／120／152，不挤压更多 body 高度。
- `MenuRegistry.Open`、底部按钮、原版设置路由仍默认旧宿主；只有完整新版存在时显示新版入口；切换保持唯一 session、旧窗口几何与实际设置值。
- 切页/关闭保存失败保持原页面与窗口；重试成功后只关闭、保存和播放音效一次。
- Esc 先关闭最上层 select/确认框/drawer；被裁掉控件和背后页面不接收输入。
- 拖动 slider 时布局与身份不跳；搜索定位先展开并布局，再由唯一滚动所有者定位高亮；无 focus 的结果只打开页；返回保留 query／scope／分页／滚动，失败停在目标页。
- 动态替换删除页不再绘制旧回调；同 ID 更新保留既有状态；索引不执行未选 drawer。
- 长中英标题、窗口缩放、UI scale、长页与大量注册页不破坏测量/裁切；状态表不持续增长。
- 运行中开启减少动态效果立即结束转场；偏好重开后持久化。
- 地图、世界、主菜单验证 Frost 开关、零透明度和 shader 失败底色；无白块、过期图像引用或资源泄漏。
- 普通自滚动页、RegisterListing、MenuSectionView、原版设置页、Character Editor 等特殊 adapter 不增加滚动或绘制副作用；Light 的 legacy 区与下一页 GUI 状态互不污染，owner actions 的确认／禁用／失败语义保留。

现有 [`RuntimeChecks.csproj`](../../tests/RuntimeChecks/RuntimeChecks.csproj) 没有链接生产 `MenuWindow.cs`、`GuiState.cs` 或 `FrostBackground17.cs`。状态测试可覆盖保存、索引与章节逻辑，但不能替代真实窗口、1.7 原生绘制、双主题弹出层和动画输入的游戏实测。
