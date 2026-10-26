# Oxygendsl

在 RimWorld 1.7 中用 Oxygen 声明原生特质、思想和常用内容修改，并在运行中的存档里重载玩法条件。支持动态心情、限时记忆、类型、struct、模块、Mod 自动内容入口和 RHAH 三态行为闸门；现已加入 IrisMenus 设置页与顶层 optional use。

- 包名：`nanaloveyuki.oxygendsl`
- 命名空间：`Oxygendsl`
- 无 RimTalk 依赖；依赖 Harmony，框架只安装一个原生内容加载接点，脚本不直接操作补丁。
- 发布图片：`About/Preview.png`（960×540）与 `About/ModIcon.png`（1254×1254），白底黑字的 O₂ / DSL Framework 标识。

## 使用

1. 安装并启用 Oxygendsl，启动游戏并进入存档。首次加载程序集仍需要启动游戏。
2. 打开「选项 → Mod 设置 → Oxygendsl → 打开脚本窗口」。
3. 在窗口编辑并点击「应用脚本」，或填写 `Examples/mood.oxy` 的完整路径后点击「从文件读取」。
4. 查看 Pawn 需求页与窗口中的运行检查。应用失败保留上一份有效脚本。
5. 「停用并清理效果」只撤销本脚本拥有的效果；「恢复有效文本」只撤销尚未应用的编辑。

```text
fn comfortable(pawn) {
    return pawn.food != null && pawn.food > 0.25
}

mood("fed_comfort",
    label: "吃饱了",
    description: "我吃饱了。现在感觉不错。",
    offset: 5,
    target: "colonists",
    when: comfortable)
```

修改标签、描述、点数或条件后再次应用，立即生效；暂停时也能评估。框架使用真实思想影响原版目标心情，不直接改写当前心情条。

- `colonists`：当前地图殖民者。
- `map`：当前地图合格 Pawn，无心情需求的对象显示跳过原因。
- `selected`：首次应用时固定选择；之后只由「重新捕获选择」改变。
- 限时记忆与完整契约：[docs/references/mvp.md](docs/references/mvp.md)。
- 类型、`struct`、组合与模块语法：[docs/references/syntax-draft.md](docs/references/syntax-draft.md)。
- 完整类型化示例：[Examples/typed-mood.oxy](Examples/typed-mood.oxy)，辅助库在 [Oxygen/helpers.oxy](Oxygen/helpers.oxy)。
- 真实游戏与性能记录：[docs/references/validation.md](docs/references/validation.md)。
- 后续能力、当前阶段限制与适度验证安排：[docs/references/requirements.md](docs/references/requirements.md)。

脚本和归属状态随存档保存。移除框架前先停用脚本并另存；本次没有验证直接卸载带活动效果的存档。脚本只能使用允许的宿主接口，不能任意读写文件、反射、创建线程或操作 Harmony；主脚本文件读取是窗口中的显式操作，模块读取限定于对应活动 Mod 的 `Oxygen/`。

语言支持无标注动态绑定和可选的后置约束：`fn add(a: int, b: int): int { return a + b }`；提供 `any`、`int`、`float`、`bool`、`string`、`list<T>`、固定长度 `array<T>`、`object` 和 `T?`。`struct` 使用引用语义，支持普通字段组合及 `embed` 成员转发。

Mod 库放在对应活动 Mod 根目录的 `Oxygen/`：`use helpers@author.modid` 读取 `Oxygen/helpers.oxy`，模块用 `pub fn`、`pub struct`、`pub const` 导出。基础标准能力免导入；`use math@std`、`use logic@std`、`use cs@std` 提供数值夹取、布尔集合逻辑和受控整数包装，不开放任意 C# 执行。

应用和读档会重新读取模块；模块状态不跨应用或读档持久化。移除或修改依赖文件会影响下次加载，错误应用仍保留上一份有效脚本与效果。

### IrisMenus 设置页
安装并启用 IrisMenus 后，框架本身自动注册「设置」与「Oxygen 语言文档」两个子页，不需要在 IM 管理中手动勾选适配。设置复用原有绘制与脚本窗口入口；未安装 IM 时保留原版 Mod 设置。

语言文档提供六篇离线手册，覆盖入门、关键词与语法、值与函数、游戏规则与内容、模块与设置、进阶类型和注意事项。正文复用 IM Markdown，代码保留原样；支持主题／章节导航、只读原文和复制全文。篇章与章节正文进入 IM 全局／Mod 搜索，结果直接定位对应章节。手册内嵌在可选适配 DLL 中，无需联网或运行时读取外部文件；当前正文为中文，界面标签提供中英两种语言。

IrisMenus 新版界面使用同主题的主题／章节侧栏、原生滚动条和原文开关；窄布局改为可展开索引。切换新旧界面保留当前主题、章节与原文模式。正文、原文和代码按主题绘制，原文中的标签与标点不作富文本解释；复制直接提交内嵌 Markdown，不改写内容。宿主统一绘制编辑 Mod 操作，文档索引不重复放置。
RimWorld 1.7 剪贴板接口读回时会去掉末尾换行；文档页提交完整内嵌原文，不自行裁剪。

仓库阅读入口：[Oxygen 手册](docs/references/Oxygen/zh_cn/00-overview.md)。详细参考仍见 [语法与接口](docs/references/syntax-draft.md)。


可选安装 IrisMenus 后，消费 Mod 可用 `optional use irismenus@std` 与 `irismenus.registry(pages: [...])` 声明自适应的 SubItem 页。支持开关、数值、滑条、文本、下拉选项、按钮和动态显示条件；未安装时保留消费 Mod 的原版设置入口，不追加硬依赖。

C# 消费端用 `OxygenSettingsHost.Register(owner, values)` 接入设置存储，页面可写在自己 `Oxygen/` 下任意 `.oxy` 文件（例如 `config.oxy`），不要求 `settings.oxy`。设置值及全局／存档保存仍由该 Mod 持有。其他 Mod 可通过通用 `IOxygenModuleProvider`／`OxygenHostModuleRegistry` 接入，不必逐个加入 std。完整声明与 API 见 [语法文档](docs/references/syntax-draft.md#irismenus-设置页)。PrisonerNames 另外硬依赖 IrisMenus，提供无需改脚本的命名样式、分数／等级和单人覆盖。

### 游戏内打开网页编辑器

安装 IrisMenus 后，ODF 自动为有 `Oxygen/` 目录或已注册编辑文档的活动 Mod 注册「在浏览器编辑 Mod」和「退出编辑器」操作，不要求声明 ODF 依赖。随框架提供的 `NativeTraits`／`RhahPolicy` 示例不自动注册独立 Mod Item，示例内容与编辑文档仍保留。点击编辑先二次确认，再打开随 Mod 打包的 CodeMirror／Blockly 页面，不是完整 VSCode。玩家机器不需要 Node、.NET SDK 或联网下载资源。

服务只监听随机端口的 `127.0.0.1`，会话令牌留在 URL fragment；检查／文件读写／应用通过受认证的本机请求和游戏主线程执行。只能编辑活动 Mod 的 `Oxygen/` 下 `.oxy`、`.oxyblocks` 及消费端主动提供的存档草稿。保存安装文件会改变该 Mod，先保留备份；原生内容仍需重启，不能把保存误认为热应用。

关闭浏览器或返回主菜单不会结束服务；在 IrisMenus 明确点击「退出编辑器」或退出游戏才回收。切换存档后旧运行中文档失效，文件编辑仍可用。消费端使用 `OxygenEditorHost.RegisterDocuments(packageId, factory)` 提供 `Read`、`Write`、`Apply` 和显式事实 `Preview` 回调。事件接口 `OxygenEvents.Subscribe/Publish` 传递提供者、事件 ID、Pawn 和只读事实，不开放 Harmony 给脚本。使用与边界见 [编辑器文档](docs/references/ide.md#本机网页编辑器)。

### 持久事件链

ODF 提供 `IEventChainProvider`／`EventChainRuntime` C# API，以及事件宿主内的 `use events.ctx@event`。脚本只读取 provider 提供的事实并返回等待、阶段或白名单动作决议；实际游戏动作仍由消费 Mod 执行，不开放反射、Harmony 或通用 Job DSL。

实例、期限、阶段和动作回执按 provider 保存在 ODF 的 GameComponent 中。到期队列替代每 tick 遍历历史；旧格式通过激活前的原子迁移接点导入。缺失 owner、版本不兼容或未确认动作保留数据并暂停，不运行备用旧引擎。暂停／停用不删除世界对象，卸载导出只清理指定消费端归属的分区。

RHAH 的现有短链、八种长链与 120 tick 捕食到达排程使用这一运行时；事件身份、Pawn／捕食 AI 和叙事事实仍归 RHAH。新版 RHAH 因此需要 ODF 必需前置，本批没有新增物品、事件或剧情。接口与生命周期见[持久事件链](docs/references/syntax-draft.md#持久事件链与-eventsctxevent)。

### VSCode 作者工具

`Tools/vscode` 提供 `.oxy` 与 Markdown 围栏高亮、17 个常用片段（含 `lang`）、当前文件诊断和 Outline／符号导航；检查未保存文本，复用现有 parser/analyzer，不执行脚本。检查器不依赖游戏程序集，诊断与导航需要插件宿主安装 .NET 9+，高亮／片段无需 .NET。

在 `Tools/vscode` 运行 `npm ci && npm run package:vsix`，输出 `tmp/vscode/oxygen-language-support-0.2.0.vsix`，通过 VSCode「Install from VSIX…」安装。配置、模块识别与限制见 [插件说明](Tools/vscode/README.md)。当前不是 LSP，尚不提供跨文件补全／跳转或 Def 校验。

图形积木编辑器已集成到同一插件的 Blockly Webview，无需外部网站或独立服务。执行 **Oxygen: New Block Program**，从心情、原生内容、IrisMenus 设置、囚犯命名或空白模板创建 `.oxyblocks`；支持画布与字段表单、撤销／重做、错误回定位和受保护的 `.oxy` 导出。保存积木不自动覆盖源码，不做任意源码双向转换。网页入口复用同一生成器；范围与数据契约见 [编辑器说明](docs/references/ide.md)。

## 原生内容 Mod

作者仍使用 RimWorld 的 `About/About.xml` 与可选 `LoadFolders.xml` 包装。玩法内容不必另写 Def/Patch XML：

- 自动递归读取所有已启用 Mod 的活动加载目录 `Oxygen/**/*.oxy`，文件名不决定用途；`content.oxy`、`rules.oxy`、`settings.oxy` 只是组织示例，`config.oxy` 同样有效。
- `template`、`define`、`patch` 在原生 Def 解析前注册；`thought_rule`、`gate_rule` 与 IrisMenus 页面在 Def 加载完成后生效。一个文件可同时声明这些内容；设置仍须消费端注册存储。
- 同一相对路径按版本／DLC加载目录优先级覆盖，不同路径按稳定路径顺序执行，提供者按活动 Mod 顺序处理。纯函数／类型库仍可通过 `use` 导入，导入不注册玩法。

```oxygen
template("rare", type: "TraitDef", fields: { commonality: 0 })
define("TraitDef", "MyMod_Worker", extends: "rare", fields: {
    degreeDatas: [{ degree: 0, label: "worker", description: "{PAWN_nameDef} works faster.",
        statFactors: [{ stat: def_ref("StatDef", "WorkSpeedGlobal"), value: 1.15 }] }]
})
patch("TraitDef", "Industriousness", set: { commonality: 1.5 })
```

完整内容包：[Examples/NativeTraits](Examples/NativeTraits)；RHAH 策略：[Examples/RhahPolicy](Examples/RhahPolicy)。分别将示例目录复制为独立 Mod 并启用；不会因安装框架就启用示例。特质授予仍由原生游戏／生成方处理，框架不在读档时强制补回特质。

RHAH 保持排在 ODF 后面。1.7 适配器位于 `1.7/Rhah/Assemblies/Oxygendsl.Rhah.dll`，不进入 Verse 的提前程序集扫描；框架在所有模组程序集和静态启动完成后才加载并注册它。未启用 RHAH 时不加载它，不附带 `HungerAndHavoc.Api.dll`。旧版已编译主体仍从 `Rhah/Assemblies/` 加载原来的 1.6 适配器，两者不能相互覆盖。

本批内置 `TraitDef`／`ThoughtDef` 字段映射、带类型的 Def 引用、原生 Worker 类型校验、模板／组合和 `set`／`append`／`remove`。Def 继承按 `defName` 展开，与 XML `Name`／`ParentName` 分开；嵌套修改可用 `"degreeDatas.0.label"`。完整声明及 C# 扩展接口见 [语法文档](docs/references/syntax-draft.md#原生内容与运行规则)。

内容在传统 XML Patch 完成后、原生继承和引用解析前提交；多个 Oxygen 提供者按活动 Mod 顺序处理。后执行的修改可覆盖前项，运行检查显示本框架的修改顺序与前后值，不追踪外部 C# 的全部后续修改。

**新增定义和内容字段修改当前要求重启；规则条件可热重载。** 重载失败保留该 Mod 的上一有效规则；内容或其依赖被修改时保留文件编辑并报告待重启，不半份热应用。提供者停用状态随存档保存；检测到特质等级／思想阶段布局不兼容时停用规则，不迁移已有 Pawn。停用规则不删除原生 Def、已授予特质或已获取的记忆，不能据此直接卸载内容包。

RHAH 适配只在对应 Mod 活动时加载，使用稳定 `HungerAndHavoc.Api.dll`；`true` 允许、`false` 拒绝、`null` 不参与。保留角色覆盖 → 行为策略 → 原默认规则的顺序，不把条件结果写成永久 `SetGate`。本批未加入新的 Job／动作 DSL。

### 翻译与 DefInjected

运行时文本使用免导入的 `lang("Key", name: value)`；设置标题／标签在回调内查询，可随当前语言变化。复用原生 Keyed、默认语言回退和命名参数格式化，缺键显示键名；离线调用没有翻译宿主则报错。PrisonerNames 已改用通用 `lang()`，不再导出专用翻译成员。完整契约见 [语法文档](docs/references/syntax-draft.md#翻译查询-lang)。

原生 Def 文本继续放在 `Languages/<语言>/DefInjected/<真实Def类型>/`，不要按用途或原始 XML 文件夹猜类型。例如 `MapGeneratorDef` 与 `GenStepDef` 分别使用自己的类型目录；当前游戏程序集没有 `SiteDef`，`SiteDefs` 也无法解析。自定义类型应写完整命名空间，如 `RHAH.SomeDef`。RimWorld 1.7 仍兼容目录名去掉末尾一个字符后解析；不依赖这条兼容规则创建新目录。

`check`／`preview` 检查翻译目录类型和键所属 Def，已知错配报错，缺少外部目录／程序集时标为 Unknown；不验证全部字段路径、翻译完整性或实际注入。最终在主菜单选择非英语、启用开发者模式，运行 **Translation → Save Translation Report**（`Actions/Save Translation Report`），查看报告的 General load errors 与 Def-injected translations load errors。报告写入桌面 `TranslationReport.txt`，桌面不可用时写入游戏保存数据目录；错误目录不保证立即出现在玩家日志里。


## 离线检查与试算

构建生成 `tmp/tool/1.7/Oxygendsl.Tool.dll`，使用 .NET 10 运行。构建目录包含离线工具所需的本机游戏依赖，仅供本机检查，不作为 Mod 发布；不再使用 Mono 或旧的 `RimWorldWin64_Data/Managed` 搜索路径。

```sh
dotnet tmp/tool/1.7/Oxygendsl.Tool.dll check Examples/NativeTraits --catalog /path/to/Core/Defs
dotnet tmp/tool/1.7/Oxygendsl.Tool.dll preview Examples/RhahPolicy --facts Examples/RhahPolicy/preview.json
```

`--catalog` 可重复；`--mod` 添加导入库的 Mod 根目录。只有确认目录封闭完整时使用 `--catalog-complete`；默认标为不完整并记录目录／依赖 SHA-256。退出码：`0` 检查成功，`1` 错误，`2` 目录不完整或引用未知。`preview` 只读取显式 JSON 事实并运行规则函数，不执行寻路、Job、伤害或特质授予，也不替代原生思想资格检查。

## 构建与验证

从 monorepo 根目录执行：

```sh
# 编译并暂存，不覆盖运行中的游戏
bash scripts/deploy.sh oxygen_dsl_framework --build-only
# 退出游戏后构建并部署
bash scripts/deploy.sh oxygen_dsl_framework
# 独立语言行为检查
dotnet run --project oxygen_dsl_framework/Tests/Oxygendsl.Checks.csproj -c Release
```

Windows 使用 `python scripts/python/deploy.py oxygen_dsl_framework`。构建链还需要 Node/npm：先构建网页编辑器，再构建同仓库的 IrisMenus 与框架；正式暂存位于根 `artifacts/oxygen_dsl_framework/release`。

RHAH 加载顺序回归先构建 `Tests/AdapterLoading/AdapterLoading.csproj`，再用 `dotnet Tests/AdapterLoading/bin/Release/net10.0/AdapterLoading.dll` 运行，依次传入已打包的 ODF 目录、RHAH API DLL、游戏根目录与 Harmony DLL 路径。它按实际 `LoadFolders.xml` 的 `v1.7` 扫描框架，之后才加载 API 和适配器，检查真实 TypeRef 解析与重复激活不重复注册；无需启动完整游戏。

构建需要 .NET 10 SDK 与已安装的 RimWorld 1.7；默认游戏路径由 `Directory.Build.props` 与部署脚本定义，可通过 `RIMWORLD_DIR` 指定部署路径。直接调用 MSBuild 时使用 `-p:RimWorldDir=/path/to/RimWorld`；`RimWorldManagedDir` 默认就是游戏根目录，引用 `Assembly-CSharp.dll` 与 `UnityStubs.dll`。Harmony 默认引用创意工坊的 `1.7/Assemblies/0Harmony.dll`，可用 `HarmonyPath` 覆盖。不分发游戏程序集。

部署脚本先以 `RimWorldVersion=1.7` 构建 monorepo 同级目录中的 IrisMenus，再构建框架。适配程序集引用 `../iris_menus/1.7/Assemblies/IrisMenus.dll`；脚本可用 `IRISMENUS_SOURCE_DIR`、直接 MSBuild 可用 `IrisMenusSourceDir` 指定依赖源码目录。发布只携带 `1.7/IrisMenus/Assemblies/Oxygendsl.IrisMenus.dll`，不分发 IrisMenus.dll。RHAH 适配仍引用同级目录中现有的稳定 API，可用 `RhahApiPath` 覆盖；这不代表旧版 RHAH 主体已兼容 1.7。

当前源码构建 1.7，输出独立保存在 `tmp/build/1.7/`。部署仅写入 `1.7/` 下的新程序集和适配器，不删除或覆盖已安装的 `1.6/`、`Rhah/Assemblies/` 与 `IrisMenus/Assemblies/` 编译内容；`LoadFolders.xml` 为两个游戏版本分别选择加载目录。1.6 沿用现有已编译发布，不要求用当前源码重新构建；全新安装若需要 1.6，须保留原 1.6 发布文件。

本机 RimCarbon14 客户端、可复用 Python 验收脚本、临时原生探针和原始结果保留在 `tmp/`，该目录被忽略且不进入发布包。

语言核心复用 AdvancedRimTalk 的 Arti 实现；来源与保留的 MIT 授权见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。Nanaloveyuki 自有贡献采用 MIT OR Apache-2.0 双重许可，完整条款与适用范围见 [LICENSE](LICENSE)；Blockly、CodeMirror 等第三方内容保留原许可。
