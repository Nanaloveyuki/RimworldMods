# IrisMenus 接入指南

IrisMenus 是 RimWorld 1.6/1.7 的共享 Mod 设置窗口，需要 Harmony，不依赖 RimIris。
设置值仍由你的 Mod 持有、修改和保存；IrisMenus 负责页面、导航和搜索。
Markdown 动态内容和外部只读文件的接入方式见
[docs/markdown.md](docs/markdown.md)。

## 1. 引用与加载

按游戏版本引用 `1.6/Assemblies/IrisMenus.dll`（`net48`）或 `1.7/Assemblies/IrisMenus.dll`（`net10.0`），设置 `Private=false`，不要把它再打包进自己的 Mod。两版提供相同的接入 API。
在你的 `About/About.xml` 的 `modDependencies` 中加入：

```xml
<li>
  <packageId>Nanaloveyuki.IrisMenus</packageId>
  <displayName>IrisMenus</displayName>
</li>
```

让 Harmony、IrisMenus 排在消费方之前。通常在 Mod 构造函数中、取得自己的设置对象后注册页面。
注册和 UI 操作都在游戏主线程执行，不要在每次绘制时重复注册。

### 按语言指定 Mod 名称

名称别名从 1.1.1 起可用。用 RimWorld 的语言目录名（如 `English`、`ChineseSimplified`），不要用显示名称或 `en` / `zh-CN`：

```csharp
// 自己的名称：Mod 或 ModContentPack 均可。
MenuRegistry.RegisterModNameAlias(this, "English", "Hunger and Havoc");
MenuRegistry.RegisterModNameAlias(Content, "ChineseSimplified", "饥与祸");

// 翻译 Mod 为另一个 Mod 指定名称，不需要拿到它的 Mod 实例。
MenuRegistry.RegisterModNameAlias("author.targetmod", "English", "Hunger and Havoc");
string name = MenuRegistry.GetModDisplayName(Content);
```

目标使用 `About.xml` 的 `packageId`，可以在目标创建实例之前注册。包 ID 和语言名不区分大小写；注册参数去掉首尾空白，空值会抛出参数异常。同一包、同一语言最后一次注册生效，其他语言的别名不受影响。

只在当前语言有别名时替换 Mod 级名称；否则使用原名，不回退到其他语言。父级导航、单页 Mod 导航、托管页标题、名称提示和搜索路径统一使用别名；显式分类标题仍由页面回调控制。原名和当前别名均可搜索。

`GetModDisplayName` 接受 `Mod` 或 `ModContentPack`。注册后已打开窗口会刷新，语言切换也会重建搜索目录。别名不修改 `Content.Name` / `About.xml`，不改变页面、托管勾选或记忆状态的 ID；注册仅在本次游戏进程中生效，不写入用户设置。名称 API 不会自动启用托管。


## 2. 普通页面与分类

```csharp
MenuRegistry.RegisterSubItemListing(this, "general", () => "常规", DrawGeneral);
MenuRegistry.RegisterSubItemListing(this, "advanced", () => "高级", DrawAdvanced);
```

两个绘制方法的参数都是 `Listing_Standard`。分类 ID 在同一个 Mod 内必须唯一且稳定，不要使用翻译后的标题作 ID。
标题回调可以返回 `"YourTranslationKey".Translate().ToString()`。

旧的 `RegisterListing` 仍可使用：只有一页时直接打开，多页时自动按 Mod 分组。
`RegisterSubItemListing` 明确声明分类，即使只有一个分类也显示父级。仅选中的 Mod 展开，并记住它上次打开的页面。

动态分类不要反复调用 `RegisterSubItemListing`。用同一个 `registrationId` 整体替换：

```csharp
MenuRegistry.ReplaceSubItemListings(this, "generated", new[]
{
    new MenuSubItemListing("rules", () => "规则", DrawRules)
});
```

`registrationId` 只标识这一组动态页，不显示在导航中。新列表会原子替换该组：相同页面 ID 保留页面和滚动位置，并更新标题、绘制、保存和搜索回调；省略的页面会被移除。空列表只移除这一组。替换前会检查空值、重复 ID，以及与本组之外已注册页面的冲突，失败时不改变现有注册。保存回调省略时仍默认 `owner.WriteSettings`。窗口已打开时，当前页 ID 不变则继续停留；当前页被移除后优先选择同注册组中最接近原位置的页面，再回退到同 Mod 的可用页面或全局目录，不再绘制旧页。

```csharp
private void DrawGeneral(Listing_Standard list)
{
    MenuControls.Section(list, "基础设置");
    MenuControls.Anchor(list, "enabled");
    MenuControls.Checkbox(list, "启用功能", ref settings.Enabled);
}
```

`settings` 是你自己的设置对象。可用控件还有 `Slider`、整数/浮点 `Number` 和下拉 `Select`。
`Number` 的编辑字符串必须保存在实例字段里，不能每帧重新创建。

## 3. 搜索并跳转到控件

在注册页面后声明搜索元数据：

```csharp
MenuRegistry.RegisterSearchProvider(this, "general", () => new[]
{
    new MenuSearchEntry("enabled", () => "启用功能",
        keywords: () => "开关 activate toggle",
        context: () => "控制本 Mod 的主要功能")
});
```

搜索条目 ID 与绘制时的 `MenuControls.Anchor` ID 相同。锚点放在目标控件前，不占布局高度；默认高亮高度是 30，可用第三个参数调整。
点击结果后会打开所属页，按本次布局测量的位置滚动并短暂高亮。Listing 的滚动在下一次绘制生效。
条件隐藏的控件无法定位，应提供可见的分节锚点，或自定义 focus 回调先展开条件。

左侧“全局选项”是分页目录，不是所有控件拼成的长表。全局搜索覆盖已接入的页面；选中 Mod 后搜索该 Mod 的全部分类。
结果只显示名称、Mod、分类和上下文，不直接修改设置。返回箭头保留关键词、结果页和结果滚动位置。
无关键词时全局目录也包含显式声明的设置条目，每页最多 20 条。关键词以空白分词，所有词都匹配才返回，不区分大小写。

元数据应轻量，不调用绘制函数。大量条目建议用 `yield return` 分批提供；语言切换自动重建索引，其他元数据变化后调用 `MenuRegistry.InvalidateSearchIndex()`。

## 4. 自定义页面与超长页面

`Register` / `RegisterSubItem` 接受 `Action<Rect>`，页面负责自己的滚动。
普通 `RegisterListing` 会执行整页回调，滚动裁剪不等于虚拟列表。
超长页面可以用 `MenuSectionView`：

```csharp
var view = new MenuSectionView(new[]
{
    new MenuSection("details", width => 100f,
        rect => Widgets.Label(rect, "详细设置"))
});
MenuRegistry.RegisterSubItem(this, "details", () => "详细", view.Draw);
MenuRegistry.RegisterSearchProvider(this, "details", () => new[]
{
    new MenuSearchEntry("details", () => "详细设置")
}, view.Focus);
```

实际使用时，高度回调应按可用宽度准确测量内容。分节仅在可见时绘制；宽度或语言变化时自动重新测量。
动态内容可以把内容快照作为 `MenuSection` 的第四个 `layoutKey` 参数；没有 layout key 时，展开状态、内容或字体变化导致高度改变后调用 `view.InvalidateLayout()`。
同一个 view 应保持为长期实例，不能每帧创建。

## 5. 原版设置页与保存

用户可以在添加设置页面中手动选择原版 Mod；普通适配页直接调用它的 `DoSettingsWindowContents(Rect)`，不再套一层滚动。
没有额外元数据时只能按 Mod/页面名称搜索并打开整页，无法自动识别控件。
添加列表同时显示内容包的 ModIcon（32×32 等比缩放），没有自定义图标时使用游戏提供的默认图标。长名称按扣除图标与勾选框后的宽度换行。
`RegisterAdapterSearch(owner, provider, focus)` 可以补充元数据和自定义定位，但不会自动启用托管，也不会自动拆分分类。
需要独立分类时，应显式注册多个页面。

Character Editor（创意工坊 1874644848）的该方法实际是独立窗口启动器，不能逐帧调用。
托管后，其页面显示“打开原生设置窗口”按钮；点击才打开，已经打开时只聚焦，关闭后不会自动重开。
请在 Character Editor 原生窗口中点击“保存”。已有手动勾选继续有效，无需取消再添加。

各注册接口最后一个可选参数是保存回调，默认 `owner.WriteSettings`。
切换页面或关闭窗口时保存，失败会保留当前页/窗口供重试。设置是即时修改的，关闭不是取消。
同一页面重复点击不触发离页保存；搜索结果返回时会保存。
窗口底栏和原版 IrisMenus 设置页新增“记住上次页面”，默认开启：重开或重启后恢复页面、搜索范围与关键词、结果分页、添加页面的筛选、宿主滚动和注册 Listing 页的滚动位置。显式打开某个 Mod 时优先进入该 Mod；页面移除后退回同 Mod 的可用分类或全局目录。
关闭记忆会清除宿主浏览记录，不影响 Mod 设置、托管勾选、窗口位置和大小。自定义页与适配页内部的折叠、输入和滚动仍由接入方维护。窗口背景、底色与边框同步上游 12 单位圆角，关闭毛玻璃或透明度归零时也保留圆角。
原版 IrisMenus 设置页提供“显示底部 IrisMenus 按钮”，默认开启，切换立即生效并保存。隐藏后仍可通过“选项 → Mod 设置 → IrisMenus → 打开 IrisMenus”进入，也不影响已接入 Mod 的原版设置入口。

## 6. 所属 Mod 操作

操作不是设置页，也不保存设置。可挂在已有 `Mod` 上，也可挂在没有 `Mod` 句柄的 `ModContentPack` 上。同一内容包的两种注册属于同一组。

```csharp
MenuRegistry.RegisterOwnerAction(this, "reload", () => "重新加载", Reload,
    visible: () => settings.Enabled, enabled: () => !busy, confirmation: () => "确认重新加载？");
MenuRegistry.RegisterOwnerAction(content, "export", () => "导出", Export);
```

选中该内容包的任一页面时，窗口在页面上方绘制可换行的操作条。没有设置页的内容包会得到一个稳定的仅操作页，页面 ID 为内容包标识加 `:actions`；它不计入原生设置，也不阻止用户继续托管原版设置页。全局目录没有选中的 Mod，因此不显示操作条。

仅操作页直接持有 `ModContentPack`，不会创建额外的 `Mod` 子类或加入游戏的 Mod 句柄列表；该内容包之后出现真实 Mod 句柄时仍会接管并保持同组。

IrisMenus 窗口底栏和原版 IrisMenus 设置页都提供“显示底部 IrisMenus 按钮”开关，使用同一份设置，切换后立即生效并保存。隐藏后仍可从“选项 → Mod 设置 → IrisMenus → 打开 IrisMenus”进入。

`visible`、`enabled` 和 `confirmation` 都可省略。点击时重新检查可见和可用；确认文本非空时先用 `Dialog_MessageBox` 确认。回调异常只记录并提示，不关闭窗口，也不结束调用方自己的会话。同一所有者和操作 ID 再次注册会替换旧回调，不要每帧注册。


完整、参与编译验证的示例位于仓库 `tests/ApiConsumer/ExampleMod.cs`。
引擎替身测试和编译不能替代游戏实测：接入后需检查切页、定位、长翻译、窗口缩放、保存与重启。

## 7. 构建与部署

需要 .NET 10 SDK、本地 RimWorld 1.7 游戏文件；运行现有引擎替身测试还需要 .NET 9 runtime。
默认构建 1.7，从已安装游戏读取 DLL，没有对应的 NuGet 引用包。
现有 1.6 DLL 和 Shader bundle 保持原样；本次构建、验证和部署脚本不重建、不复制任何 1.6 文件。

```bash
# 默认编译 1.7，输出到 1.7/Assemblies。
dotnet build Source/IrisMenus.csproj -c Release
# 显式选择 1.7。
dotnet build Source/IrisMenus.csproj -c Release -p:RimWorldVersion=1.7
# 只构建 1.7，不部署。
./scripts/deploy.sh --build-only
# 构建并部署 1.7。
./scripts/deploy.sh
RIMWORLD_DIR='/mnt/d/Steam/steamapps/common/RimWorld' ./scripts/deploy.sh
```

默认 Release，1.7 DLL 目录是 `/mnt/e/Apps/Steam/steamapps/common/RimWorld`；Windows 下为 `E:\Apps\Steam\steamapps\common\RimWorld`。
1.7 的 DLL 位于游戏根目录，已不在 `RimWorldWin64_Data/Managed`。`-p:RimWorldManagedDir=...` 可覆盖 DLL 目录；反编译源码目录（如 `D:\References\Rimworld\Vanilla\1.7\Game`）不能用于编译引用。
保留 `-p:RimWorldVersion=1.6` 编译配置，仅供明确安排的旧版维护使用；它会覆盖 `1.6/Assemblies` 的现有输出，本次升级不要调用。该配置可从 `Krafs.Rimworld.Ref` 1.6.4633 获取引用，或用 `-p:RimWorldManagedDir=<1.6游戏>/RimWorldWin64_Data/Managed` 指向独立的 1.6 游戏，不能使用 1.7 DLL。

WSL 脚本只构建和打包 1.7。`CONFIGURATION=Debug` 可切换配置，`RIMWORLD_DIR` 可覆盖部署用游戏目录（兼容 `RimWorldDir`）；`RIMWORLD_1_7_MANAGED_DIR` 可单独指定 1.7 DLL 目录。
不启动 Unity，不触碰目标中的整个 `1.6/` 目录。部署前需退出游戏；脚本检查进程、目标包标识、完整文件清单及符号链接，复制后校验 SHA-256。
部署内容为 1.7 DLL、原生 shader、共享资源和说明，不复制 Harmony/游戏程序集，不删除目标文件，也不修改 ModsConfig。全新暂存包不含 1.6 DLL/bundle，旧版继续使用原有产物。

Windows 或 PowerShell 7 下可使用：

```powershell
./scripts/build-and-deploy.ps1 -SkipGpu
./scripts/validate.ps1 -SkipGpu
./scripts/build-and-deploy.ps1 -SkipGpu -GameModPath 'E:\Apps\Steam\steamapps\common\RimWorld\Mods\IrisMenus'
```

两份脚本均可用 `-RimWorldManagedDir` 指定 1.7 DLL 目录。构建脚本只有传入 `-GameModPath` 才部署；`-SkipBuild` 仅适用于 1.7 刚完成构建后的暂存/复制。
验证脚本只编译 1.7 API 示例和提供方 DLL、运行引擎替身检查、校验 XML/翻译及加载目录，再暂存 1.7 包并验证哈希，明确检查包内没有 1.6 文件，不部署到游戏。
无论是否传入 `-SkipGpu`，两份脚本均不启动 Unity、不重建 1.6 bundle；省略开关时会提示需要实际游戏 GPU/UI 验收。历史 Unity 2022.3.35/D3D11 测试只覆盖 1.6，不在本次流程中运行。1.7 毛玻璃方向、圆角裁切和界面交互需在 1.7 游戏中检查。

1.6 的原有 Def 和语言文件保持在 `1.6/`；1.7 使用 `Common/Defs`、`Common/Languages`。`LoadFolders.xml` 分别加载 `[1.6]` 和 `[1.7, Common]`，1.7 不会加载 1.6 DLL 或 Unity bundle。部署包包含 `1.7/Rendering/Frost17.frag.glsl`，游戏 shader 缓存直接读取源码路径。1.7 需要 Harmony Mod 中支持 .NET 10 的 Harmony 3。

1.6 毛玻璃使用 Unity 相机和 bundle；1.7 在 `ImGuiRenderFeature.Record` 的渲染提交中借用场景颜色，由原生片段 shader 绘制模糊、圆角底色和边框。

