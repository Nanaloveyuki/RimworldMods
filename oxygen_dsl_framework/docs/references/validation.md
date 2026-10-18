# Oxygendsl 验证记录

> 本页保留各日期的历史验收证据。记录中的旧部署命令属于当时入口，已不再提供；当前操作从 monorepo 根目录使用 `bash scripts/deploy.sh oxygen_dsl_framework`。目录整理未重跑这些验收。

## RimWorld 1.7 迁移（2026-10-15）

- 游戏运行时切换至 .NET 10，引用游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 和 Harmony 1.7。运行时、可选适配器、离线检查器及适配加载检查已迁移；语言核心保持 `netstandard2.0`，独立作者工具保持原目标。移除编辑器对象上已无实际作用的 `hideFlags` 写入，保留主线程更新与退出回收。
- 1.7 构建输出位于 `tmp/build/1.7/`、`tmp/tool/1.7/`；发布主体位于 `1.7/Assemblies/`，适配器分别位于 `1.7/IrisMenus/Assemblies/`、`1.7/Rhah/Assemblies/`。保留 `v1.6` 加载项、旧思想定义和现有 1.6 已编译内容，不要求当前源码双版本构建。对正式安装的旧发布制作隔离副本后执行新版部署，`1.6/`、`Rhah/Assemblies/`、`IrisMenus/Assemblies/` 共 7 个原文件 SHA-256 完全不变。
- `bash scripts/deploy.sh --build-only` 完整构建链通过；框架 Release 零警告、零错误。既有 548 项行为检查通过；.NET 10 适配加载检查按最终发布目录完成 ODF 优先类型扫描、随后加载真实 API、重复激活单次注册。未把旧版 RHAH 主体列为已兼容 1.7。
- 实际离线工具读取 1.7 Core Def 目录，完成六个示例 Trait／Thought 定义和 `Industriousness.commonality` 修改检查；RHAH 示例试算覆盖 allow／deny／abstain，并拒绝非对象 JSON 事实。独立 .NET 10 进程用真实游戏程序集验证生成 XML／assetlookup 提交、三态闸门，以及真实 Scribe 对 provider、instance、receipt 未知 XML 的保存保留。临时探针第一次使用错误的 `when` 参数，改为已有 `evaluate` 契约后通过，未修改语言语法迎合探针。
- 独立 RimWorld 1.7.4979 rev109，配置 `C:/RimCarbon14/odf-upgrade17-20261015-profile`：Headless 新游戏中原生示例特质与载体思想加载成功，真实殖民者取得 +5 思想，热重载为 +12 且思想对象不变；无效脚本保留有效效果，分配计数可用，实际写出存档。未改玩家安装、配置或原始存档。
- 最终分版本发布包在新进程读取上述存档成功：恢复有效源码与唯一 +12 思想，重新评估保持同一思想对象，显式停用删除归属效果。首次读档进程曾退出于 `MonoMod.Core` 的 `Core60Runtime.CompileMethodHook`，stderr 报 `Internal CLR error (0x80131506)`；最终成功不能证明该原生崩溃已修复，框架未屏蔽异常。Headless 另有原生 0×0 分辨率错误及关闭阶段分配警告；没有把日志称为零错误。两次成功证明位于隔离配置的 `upgrade17-proof.txt` 与 `upgrade17-reload-proof.txt`；临时执行代码和游戏副本验收后删除。
- 本轮不覆盖设置窗口视觉、浏览器编辑、全部 DLC／大型模组组合、旧 1.6 存档直接跨版迁移和长时性能；1.6 的历史验收记录保持原版本含义。

## IrisMenus 示例 Mod Item 清理（2026-10-11）

- 两个独立示例启用后，网页编辑操作使 IrisMenus 为 `nanaloveyuki.oxygen.example.traits` 与 `nanaloveyuki.oxygen.example.rhah` 自动生成 launcher。仅在 `OxygenEditorMenus` 跳过这两个包的菜单注册；不修改 IrisMenus、内容加载、编辑文档资格或正式消费 Mod 的入口。
- `dotnet build Source/IrisMenus/Oxygendsl.IrisMenus.csproj -c Release --nologo`：零警告、零错误。独立 RimWorld 1.6.4871，配置 `C:/RimCarbon14/odf-menu-examples-20261011`：旧适配器实例 `029766eab80e441ab053417a1bd63097` 的实际导航包含框架及两个 example；新适配器实例 `f077dafced9a4ad8bd4c30d8c20269b4` 保持两个 example 活动，但实际导航只保留框架，截图 `674df2ef97fd46eea140181309e291c0.png`。点击框架后「设置」「Oxygen 语言文档」和「在浏览器编辑 Mod」仍可见，截图 `60efaa37a623457fa49aaaefafb7823a.png`；没有在本轮重复打开浏览器或运行历史全套测试。
- 首次复用旧原生验收配置失败：RHAH 位于 ODF 之前，加载到旧 Core 后缺少 EventChain 类型，未进入桥接。停止该隔离失败进程后，新建配置沿用已验证的 ODF 在前顺序、对齐当前 Core／Mod，再对比旧／新菜单适配器；没有修改生产加载规则以掩盖该失败。两次有效游戏正常退出，结果与回执位于 `tmp/menu-example-results.json`、`tmp/menu-example-receipts.jsonl`。

## RHAH 可选适配器延后加载（2026-10-11）

- RHAH v1.0.6 发布 API 与当前 API 一致，接口没有删除；故障是 ODF 先加载却通过 LoadFolders 立即扫描反向依赖适配器。移除 Rhah 提前扫描项和适配 DLL 的自动静态启动入口，框架主体在全部模组程序集和静态启动完成后，从自身安装路径加载并调用既有 Activate。不改 RHAH API、不随包复制 API、不倒置框架依赖
- Release 构建零警告、零错误，既有 548 项行为检查通过。新增 `Tests/AdapterLoading` 独立 Mono 回归按实际发布加载目录扫描框架、随后才载入 API，并检查真实 TypeRef 和重复激活单次注册；旧安装失败于 `01000011`，修后安装通过
- 真实 RimWorld 1.6.4871 独立副本 `C:/RimCarbon14/rhah-adapter-fixed-game-20261011`，配置 `C:/RimCarbon14/rhah-adapter-fixed-20261011`：正常 ODF→RHAH 顺序启动无此类型加载错误，适配器注册一次。双地图旧档上的真实访客经 RHAH_Api 消费脚本 deny/allow/abstain，Pawn 显式覆盖优先，重复 Activate 不重复注册；恢复原样例规则并推进 120 tick
- 配置 `C:/RimCarbon14/odf-adapter-without-rhah-20261011` 未启用 RHAH 和其示例，新游戏与 120 tick 通过；实际 AppDomain 没有 API 或 RHAH 适配 DLL。两次测试进程正常退出
- RHAH 修复卡 `docs/bug-cards/2026-10-11/odf-rhah-adapter-load-order.md` 与忽略目录 `tmp/odf-adapter-fix-receipts.jsonl` 在相邻 RHAH 仓库。两份配置的 Player.log 留存。共享旧隔离安装另有游戏进程运行，本轮复制独立安装，没有修改该运行进程、正式安装、玩家配置或原始存档；尚未正式部署
- 更新须同时替换 LoadFolders、Oxygendsl.Mod 与 Oxygendsl.Rhah 后完全重启；只重编适配器或只更新 RHAH 不解决提前扫描。不泛化为全部外部适配器或玩家完整模组组合验收

## ODF 接管 RHAH EventChain（2026-10-11）

- ODF 提供版本化 provider／定义、按归属分区保存的实例和动作回执、到期堆及 `events.ctx@event` 决议宿主。RHAH 切换现有 51 个短链、八种长链及 120 tick 捕食到达排程；旧扫描引擎和注册表删除，旧记录仅作迁移输入或失败保留。本批没有新增物品、心情、事件、剧情或 Harmony 补丁，Pawn／捕食 AI 与 NarrativeState 仍归 RHAH。
- 框架 Release 零警告、零错误，548 项行为检查通过；RHAH 的 41 项事件链检查和 9 项卸载清理检查通过，结构／存档键／翻译对称检查通过。RHAH 测试构建存在 5 条既有 xUnit 分析警告；未把它写成零警告。首图 `uniqueID=0` 的到达迁移先失败，修正地图判断后回归通过。
- 独立 Windows Unity Mono／RimWorld 1.6.4871，游戏目录 `C:/RimCarbon14/oxygendsl-native-game-20261009`、配置 `C:/RimCarbon14/odf-rhah-eventchain-20261011`：51 个旧短链各提交一次抽取回执；八种旧长链保留 `id % 2500` 相位；原 ID、站点、payload、开始时间与期限保持。首图待到达实例在 tick 122 不执行、tick 123 提交。保存成功后不再写旧事件链或旧待到达键，重新加载不重复抽取。
- 实际触发 `RHAH_AbandonedRatkinChildren`，短链选中捕食者后在 120 tick 前无捕食者、到期后产生一个真实捕食者；长链的两个孩子达到成年条件后，Oxygen 决议调用具名通知动作，新增真实来信并写 Completed。新进程 `8d0ca94f6dc245539740194a98960068` 读回有效存档，保留完成回执、5 个来客、1 个捕食者和叙事事实，零重放回调。
- 暂停／停用保持实例和世界事实、零回调；恢复把活动下一检查与期限一次性平移暂停时长，开始时间不变；取消只写终态。损坏旧输入整批失败，原记录值、计数器和地图待到达 tick 继续保存，失败诊断分区读回仍暂停。Scribe 正常省略默认 XML 字段，值未丢失；初次按 XML 字段完整性比较的断言失败后改用保存格式语义比较，不改生产保存器迎合断言。
- 显式空分区不重新导入；未知 owner 的 schema、属性和扩展字段随真实保存保留；pending 回执即使切换模式仍不重试且原扩展字段保留；根 schema 77 不执行迁移或动作，原混合 provider XML 保留。未知版本检查覆盖框架／provider／定义的独立边界，不能作为第三方兼容性保证。
- 真实卸载导出先备份 Active 分区，再把 live provider 设为 Disabled，live 来客／捕食者／叙事事实不删。清理副本仅删除 RHAH 分区，保留 ODF 根、schema、meta 和未知 owner 原 XML。配置 `C:/RimCarbon14/odf-rhah-eventchain-removed-20261011`、新进程 `1908e240cef040fcb231ecf8ed29113a` 不启用 RHAH 及其闸门样例，实际加载清理副本、运行、保存、重新加载和再运行通过。
- 同一 Mono 主线程对照旧程序集的扫描调度与新到期调度：5000 tick 调用、一个缺失地图的开放长链、0／10000 条已关闭历史。新调度每组仅两次决议，旧调度每组 5000 次链入口；无历史的热样本为旧 2.7182／3.2928 ms、新 0.3284／0.3252 ms，10000 历史的中位数为旧 105.8383 ms、新 0.2627 ms。新路径首次未预热调用为 9.3325 ms，含首次脚本编译／JIT；不代表整体游戏 TPS 或分配字节。最初临时样本遗漏 SiteId，Prepare 拒绝并产生零回调，该组废弃；修正临时输入后才记录上述有效结果。
- 运行检查窗口实际显示 EventChain API、provider、due 和暂停原因，截图 `16304f4bf9ef45ea803df2f1e042b2cd.png`；导出截图 `67fb0207438942d798e9060e1c35e372.png`。47 项运行断言通过，另外保留一次 XML 比较失误的失败记录。回执、结果和有效计时在忽略目录 `tmp/eventchain-acceptance/`；临时执行／探针代码验收后移除。所有本轮隔离游戏正常退出，未覆盖玩家安装、配置或当前存档；未实测全部事件生成、跨地图／商队迁移、大型 Mod 组合或长时 GC。
- 玩家另提供混合 Mod 存档的 profiler 截图：同时启用基于 ODF 的囚犯代号与尚未使用 ODF 的旧版 RHAH。`GameComponent_Oxygendsl.GameComponentTick` 为 409 次、总计 0.052 ms、平均 0.13 μs／次、占比 0.01%；这是当前版本的组件 tick 基线，不覆盖消费端自身更新、UI、加载或其它调用路径，也不是 RHAH 接管后的性能对照。采样窗口短，不据此推断长时 GC、事件密集负载或整体 TPS。

## 任意 Oxygen 文件名与活动 Mod 读取语义（2026-10-11）

- 递归发现所有活动 Mod 的 `Oxygen/**/*.oxy`，版本／DLC目录同相对路径覆盖；文件名不决定设置、原生声明或运行规则用途。统一编译准备文件一次，设置与规则共用其回调状态；含原生声明的文件在 Def 解析前准备，后续不重复执行，改动仍要求重启。
- 网页编辑器不再按 ODF Dependencies 筛选，也不按 settings／rules 文件名选择 Apply；VSCode auto 模式按语法识别库与脚本。设置持久化仍归消费端，通过已有 Register 接入，不自动创建平行存储。
- 旧 CLI 对仅有 `Oxygen/config.oxy` 的 fixture 输出 `Content changes: none`；修正后同一 fixture 输出 `Define TraitDef/ArbitraryConfigTrait` 和 `restart required`。定向临时 Mono 场景通过递归／大小写扩展名与版本覆盖、同文件原生声明／设置标题／gate 回调、Pawn 状态变化、真实 Mod 文件宿主延后执行和原生文件改动拒绝热应用。使用真实游戏程序集，但未启动 Unity，不声称设置 UI 视觉验收。
- Release 构建零警告、零错误；VSIX 0.2.0 重新打包成功。Mono 加载真实 Assembly-CSharp 时提示缺 Unity.Burst 属性程序集，定向执行仍通过；未把它记作 Unity 游戏验证。按本轮要求未运行历史全套 smoke/tests，未新增永久测试；临时验证代码验证后移除。

## 框架设置与 Oxygen 语言文档接入 IrisMenus（2026-10-11）

- 检测 IrisMenus 后自动注册框架 `general` 与 `documentation` 两个 SubItem，不要求玩家手动添加。设置复用原生绘制入口；未安装 IrisMenus 时保留原版 Mod 设置。六篇中文手册覆盖入门、关键词与语法、值与函数、游戏规则与内容、模块／翻译／设置、进阶类型与注意事项，嵌入可选适配 DLL；界面标签提供中英翻译，正文当前仅中文。
- 最终 `dotnet build Oxygendsl.slnx -c Release --nologo` 零警告、零错误，框架 508 项行为检查通过。手册 37 个独立示例通过实际 Editor parser/analyzer；37 个实际 Core 执行／ScriptCompiler／ContentCompiler／OxygenSettingsCompiler 场景通过。没有新增文案或源码匹配型永久测试。
- 独立 RimWorld 1.6.4871／Unity Mono，配置 `C:/RimCarbon14/odf-framework-docs-20261011`，实例 `a563a32bf0804277838eb7c923a6544d`，1280×720：实际 IM 导航只有唯一框架父级和上述两页；设置在主菜单显示读档提示，文档主题／章节导航与正文滚动正常。Mod 搜索 `impl`、`lang：查询当前语言`、`静态 Self 工厂` 得到文档结果，点击翻译与泛型章节结果后滚动到对应章节。搜索提供器同时供全局／Mod 索引使用，本轮 UI 只实际操作 Mod 搜索，不将其记为全局搜索点击验收。
- 实际切换简体中文后界面标签更新；泛型 `fn make<T: Factory>`、`make<Label>` 和 `Self` 原样显示，不被 Markdown 富文本吞掉。只读原文开关生效，复制全文得到与源文件一致的 8590 字符，Windows 剪贴板仅省略末尾换行。截图 `a08fefa44f1243a8a4267f6e4ae8064a.png` 为总览，`6ac6102a9dcc4e928c998b63e0f4cf28.png` 为中文控件与泛型静态工厂。缩窄窗口的外部 Rect 写入被桥接拒绝，未获得窄窗口视觉证明；不改 IM 背景或透明度。
- 最终程序集另用无 IrisMenus 配置 `C:/RimCarbon14/odf-framework-native-20261011`、实例 `e1ba331029f34d31a078758cac584de9` 验证：活动 Mod 不含 IM，实际原版 Options → Mod options → Oxygendsl 正常打开原生设置与读档提示，截图 `3e2e2fee1ff3468ea4154c06182e7476.png`。两个隔离游戏均正常退出，未修改玩家安装或配置；回执与结果在忽略目录 `tmp/framework-docs-20261011/`。临时示例执行工程验证后移除。

## 玩家设置文案与 Markdown 使用指南（2026-10-11）

- PrisonerNames 的设置和名单说明改为玩家用语：用生日解释年龄段，用加减分解释积分，用「升级所需分数」替代数学下限，用「默认等级」替代 −∞，不改底层取值与行为。设置页只留简短提示和名称示例，不再展示整段占位符列表、函数签名或开发者状态接口。
- 在已有三个设置页后注册一个 `guide` 页，长期持有 `Markdown.Dynamic(() => "OPN_UserGuide".Translate().ToString())`，通过 `MenuControls.Markdown` 测量、绘制和 Listing 滚动。中英正文存于同一 Keyed 资源体系，每种语言只有一份完整指南；内容覆盖入门、保存、名称示例、年龄／种族、积分等级、单独设置、可跳过的高级自定义与排错。没有新文件读取、平行持久状态或 IrisMenus 改动。
- 消费端 Release 构建零警告、零错误，47 项行为检查通过；13 文件发布包逐项 SHA-256 验证，未新增文本断言型永久测试。
- 隔离 RimWorld 1.6.4871 进程 `04970fd3bcdf43afacd14ee4718c001e` 验证中文主菜单指南、标题／强调转换和滚动到底；进入已有存档后检查命名短说明、抵抗降低的加分标签、升级要求及默认等级。截图：`965194a153c243edbda7f58ec4a76a38.png`（指南开头）、`58837c06bc26497d86cf2f6905058c2d.png`（末尾）、`70b173a0e5b34c3aa5264cad1731c296.png`（命名）。初次实际渲染发现中文标点紧邻 ** 时未解析强调，按 IrisMenus 现有 Markdown 约定加空格，后续实际回执不含裸 **。
- 英文进程 `83992d69a2ba45908674e50493f0e224` 验证主菜单 User guide 与存档 Naming，截图 `7184a704af2945baaafb67868dc9c8f1.png`、`3560066cee514d0692c3efffbb95231e.png`。两种语言在 1280×720 无水平溢出，Markdown 文档长内容可滚动；本轮不对 IrisMenus 主菜单背景作额外改动。回执与结果在 `tmp/editor-settings-20261011/player-guide-*`。
- 本轮隔离游戏均正常退出，未修改玩家安装或配置，未启动 subagent。

## PrisonerNames 重复导航修正（2026-10-11）

- 用户截图中的第二个「囚犯代号」是消费端 `Oxygen/settings.oxy` 注册的 general 页，不是 IrisMenus 多生成了父级。原始 UI 回执也记录该子页与命名／积分／等级同时存在。移除旧 DSL 入口、专用设置值／模块桥接；只用现有 C# 三页，开关继续写当前 `GameComponent.Enabled`，名单按钮继续调用 `PrisonerNamesApi.OpenWindow`，不新增状态。
- 消费端 Release 构建零警告、零错误，47 项行为检查通过；未新增只检查页面注册的永久测试。发布包清除旧 `settings.oxy`，共 13 个文件逐项 SHA-256 验证。
- 独立 RimWorld 1.6.4871 进程 `fd4b8183184e47d1b0ea4a7334cb9d91` 读取已有存档，实际导航为「全局选项／ODF 囚犯代号／命名／积分／等级」，没有重复子页。点击移入命名页的代号开关，实际 runtime 依次变为 false／true；名单按钮打开真实 `NamingWindow`，积分页显示初始分数与事件系数，等级页显示阈值与等级编辑。
- 实际中文界面截图 `b3ca83ac1e7b4a39b20fab9443c5af5d.png` 显示唯一父级、三个子页及合并后的开关／名单按钮。截图和回执位于隔离配置与 `tmp/editor-settings-20261011/navigation-fix-*`。语义 UI 自动操作遇到 stale-layout 拒绝后重新取布局完成；一次 smoke 断言误写了初始分数标签，读取真实界面标签后纠正，没有修改生产文案迎合断言。隔离游戏正常退出；未修改玩家安装或 IrisMenus 代码，本轮没有启动子任务，四个先前失败子任务已退出。

## 囚犯设置分页与年龄命名（2026-10-11）

- 框架与消费端 Release 构建 0 警告、0 错误；消费端 47 项行为检查通过，覆盖英文占位符与多语言正文、旧中文键拒绝、小数年龄及边界、种族同段覆盖／缺段继承、禁用覆盖、深拷贝隔离和非法配置拒绝。本轮未重跑框架历史全套检查。
- 编辑器拒绝 PrisonerNames 的根因是 `About.xml` 中 ODF／IrisMenus 非官方依赖没有安装 URL，被 RimWorld 元数据校验剔除。补齐 `downloadUrl` 后，真实 IrisMenus「在浏览器编辑 Mod」确认打开生产 HTTP 服务；活动包筛选没有放宽。实际 Chromium 读取安装文档及 `current/naming`／`current/events`，最终预览使用已存年龄／种族配置，事实 `ODF_SmokeEnglish / age=1.5 / number=7` 得到 `RaceKid007`。Windows 游戏与 WSL Chromium 之间仅加临时 TCP 转发，保留原 Host／Origin／token 校验；没有替身文档后端。
- Windows Unity Mono／RimWorld 1.6.4871，独立安装 `C:/RimCarbon14/oxygendsl-native-game-20261009`、配置 `C:/RimCarbon14/odf-editor-settings-20261011`。实际设置页观察命名／积分／等级分别导航，年龄草稿改为 `Kid{number_text}` 后跨页改初始分数 42，从积分页保存同时保留年龄改动；添加 21 岁边界生成六段，并单独覆盖一个种族的 1–7 岁段。
- 活动 Pawn 种族目录返回 160 个定义，不包括继承了 race 数据的尸体 ThingDef。中文有翻译的 fixture 显示「本地化种族」，缺译 fixture 显示 `english fallback race`。中文所属 Mod 显示「ODF 囚犯代号」，另启英文进程实际显示 `ODF Prisoner Names`，其 Naming／Scores／Grades 分页正常。不是只检查字典注册。
- 实际囚犯生理年龄 0.999／1／6.999／7／17.999／18 得到 `幼001`／`Kid001`／`Kid001`／`少001`／`青001`／`P001`，原 `Pawn.Name` 对象未变。未知占位符配置拒绝并保留旧结果；应用自定义命名后保存设置不改有效源码或未应用草稿。旧 CRLF 配置解析保持单一样式。
- 实际保存 `odf-age-settings-pawn-final` 并用新进程读回，保留初始分数 42、六段下限与名称、种族覆盖、编号和实际 `P001`。第一次真实读档暴露 Windows Scribe 将文本换行为 CRLF，原解析器未去 `\r`，导致年龄 section 未识别且基础样式含控制字符；修正行分隔后最终进程 `3bde3586930542ca929b3ec05490559c` 读回成功、LastError 为空。此失败未记为通过。
- 视觉验收同时修正等级下限缺译。最终中文等级截图 `54843170e3db42e098543e81177c6652.png`、英文所属名称截图 `fa301256d1b94794bb931da35b0ccc60.png`；回执与结果位于忽略目录 `tmp/editor-settings-20261011/`。浏览器初次跨 WSL 连接失败后，用本地转发完成真实预览；没有将失败连接计为 UI 通过。所有本轮隔离游戏与编辑器服务正常退出。
- 不改玩家安装或配置；没有新增原生事件补丁、跨地图／商队、全部 DLC 或大型 Mod 集合的实测结论。

## lang 与 0.0.1 发布（2026-10-10）

- 框架与 PrisonerNames Release 构建均为 0 警告、0 错误；框架 508 项行为检查、消费端 20 项命名检查及 Blockly 11 项检查通过。
- 独立 Mono 进程运行真实 `Verse.Translator`、`NamedArgument` 与格式化器：18 个 smoke 场景覆盖已打包中英 Keyed、当前语言／默认回退／缺键（含开发者模式）、富文本保留、导入设置回调随语言变化、已迁移的 `settings.oxy`、心情注册、运行思想规则、重复回调标量参数替换、实际 `NamingPreview`、加载期内容拒绝翻译宿主缺失、离线错误、无活动语言及已加载自定义 Def 完整命名空间目录。预置真实语言对象的 Keyed 字典和 worker，未启动 Unity 或加载完整游戏。原生开发者模式会伪翻译完全缺失的键；实际 smoke 复现后修正为保留键名，只对真实默认语言回退保留原生伪翻译行为。
- 真实 CLI 的 7 个临时 Mod fixture：`SiteDefs/RHAH_RecordSite` 明确报告 `Verse.MapGeneratorDef`，`IncidentDefs/RHAH_RefugeeCamp` 明确报告 `Verse.GenStepDef`，退出 1；正确类型目录与兼容复数目录退出 0；不完整目录的缺失外部 Def／未知自定义类型退出 2。报告只校验类型与 Def 身份，不声称字段路径或注入完成。
- 实际 Editor 进程的 4 个请求验证免导入 `lang`、导出函数使用命名参数、保留内置名称及消费端设置脚本；17 个片段已包含 `lang`。更新的 VSIX 0.2.0 打包成功，发布 Core／Editor 程序集，不携带游戏程序集。
- 0.0.1 元数据与框架程序集版本已设置；网页资产重新打包。发布目录为 `tmp/releases/0.0.1/`，框架 54 个文件、PrisonerNames 12 个文件逐项 SHA-256 校验一致，不携带共享依赖或游戏程序集；框架另带自己的可选适配 DLL。未修改 RHAH，未生成真实游戏翻译报告，未新增游戏窗口视觉验收，也未覆盖玩家安装目录。发布包仍应与此前游戏验收边界分开看待。

## 本机网页编辑器与囚犯玩家配置（2026-10-10）

- 框架与 PrisonerNames Release 编译均为 0 警告、0 错误；ODF 494 项行为检查、消费端 20 项命名行为检查、Blockly 11 项生成／文档检查通过。最终 VSIX 打包成功，包含囚犯模板及本地资源，不携带游戏程序集。
- 链接生产 `GameComponent_PrisonerNames` 的 30 项状态 smoke：新囚犯初始分数、配置系数与脚本分数叠加、阈值升级、各囚犯独立值、手动分数／等级／代号优先、事件异常原子回滚、原名保留、暂停／释放间隔不计托管时间、独立命名／事件草稿、配置与自定义规则切换、记录恢复及旧存档自定义规则保留。游戏／Pawn 事实与 Scribe 边界用替身，不算游戏渲染或完整存读档证明。
- 实际 RimWorld `Scribe_Deep`／`Scribe_Values` 在独立 Mono 进程往返 XML，保存并恢复单人代号／分数／等级、编号、托管余量／时间基线及数字／文本／布尔／null。关闭独立进程中的 DeepProfiler 并预置类型缓存后完成；没有加载完整游戏或执行 Pawn 交叉引用，不能据此宣布游戏存读档验收完成。
- 链接生产事件总线并用 Harmony patch 引擎替身：按实际 before／after 抵抗差发布、无变化不发布、未确认越狱不发布、同次越狱去重、新游戏隔离和取消订阅通过。未在真实 RimWorld 执行新增原生事件补丁。
- IrisMenus 173 项引擎替身断言通过，包含所属 Mod 操作与确认期资格重查。链接生产文件 host 的独立 smoke 通过活动依赖筛选、真实文件保存／创建、路径逃逸与覆盖拒绝、同会话重开、手动停止及游戏退出回收。没有新的 Unity 设置窗口视觉验收。
- 生产 net48 HTTP 后端在 Mono 中运行，真实 Chromium 加载打包的 CodeMirror／Blockly。实际源码编辑／物理保存、`ART2005` 错误位置、显式事实预览 `Ada-007`、有效应用 `Ada-001`、错误应用保留有效结果、积木新建／字段编辑／JSON 保存／指定目标导出及重开通过。最终画布显示修改后的前缀「俘」，Human／number=2／score=20／Ben 试算为 `俘002·Noted·Ben`；无浏览器 JavaScript 错误。
- 同一 UI 观察外部文件修改后保存返回冲突，保留本地草稿；积木重载后为已保存而非假脏状态。实际 HTTP 验证 token、Host／Origin、CORS、固定静态资产、提前拒绝过大请求体、旧版本保存／应用冲突。后端 fixture 断言所有文档回调在 `Pump` 线程执行。
- 共用 Blockly 生成器 → 生产 HTTP → 实际 `NamingPreview`／`NamingRules` 得到 Ratkin=`鼠007·Valued·Ada`、Human=`囚002·Noted·Ben`；离线 CLI 读取生成源码也输出 `鼠007·Valued·Ada`。只执行显式事实，不模拟游戏行为。
- 实际结束后端后，页面保留画布并显示断连；修正断连遮罩宽度与 Blockly 层级遮挡后，在桌面和 640px 窄视口观察居中提示，焦点在重连按钮，保存／检查积木／导出均禁用。服务正常退出码为 0；页面不报告假保存成功。
- 最终本地发布打包校验：框架 54 个文件、PrisonerNames 12 个文件 SHA-256 一致。框架携带完整网页资源与许可证；消费包不重复携带共享程序集，未覆盖玩家游戏安装目录。
- 本轮未部署到玩家游戏目录，未实测新的 IrisMenus 窗口、真实 Pawn 事件、完整游戏存读档、跨地图／商队及大型 Mod 组合；此前的旧版代号游戏证据与本轮新增能力分开记录。

## VSCode 积木编辑器（2026-10-10）

- `npm test`：11 项生成／文档行为检查通过，覆盖表达式括号、字符串与控制字符转义、缺失连接、脱离语句的值积木、重复 ID／字段、路径边界、生成摘要和最小范围错误映射。
- 实际 Blockly 工作区分别加载、保存四个模板，再由共用生成器生成源码，无连接错误。实际 Core 消费端编译并执行心情模板：food=0.5 为 true，0.25／null 为 false；原生内容模板接受 TraitDef／ThoughtDef，设置模板在 IrisMenus 可用时注册一页、缺失时跳过。
- 实际 Editor 进程与文件系统配合 VSCode API shim：首次导出、原摘要复用、手改文件取消覆盖、确认期间目标变化、未保存目标、过期积木提交及未信任工作区拒绝均通过。shim 不算真实 UI 证明。
- 在真实 VSCode Server 的自定义编辑器打开 `.oxyblocks`，通过字段表单与画布原生编辑修改阈值，观察源码预览同步；`Ctrl+S` 后读取实际 JSON，重载后仍为修改值。临时自建工作区通过 VSCode 的 Trust 按钮授予信任，实际「检查积木」通过并由「导出 .oxy」写出匹配预览的文件。
- 最终 VSIX 在同一真实界面再次观察数值 0.6 → 0.7 → 撤销 0.6 → 重做 0.7，字段面板与预览同步。命令面板执行 **Oxygen: New Block Program**，选择模板、保存 `created.oxyblocks` 后自动进入画布；改输出路径并导出 `created.oxy`。实际 Core 消费端读取该 UI 导出文件，food=0.5 为 true，0.25／null 为 false，保存的积木含目标 URI 与 SHA-256。
- 真实撤销／重做复现重复文档回执覆盖尚未提交状态，修正为同版本回执不重载画布；最终路径观察 0.4 → 0.6 → 撤销 0.4 → 重做 0.6。切换焦点保留字段面板选中块，不再依赖 Blockly 全局焦点对象。
- 实际 MissingType 返回标注得到 `ART3018`，错误按钮定位函数块并高亮对应源码；`missing_rule` 动态引用没有静态报错，确认当前检查器不能证明所有动态绑定，不据此宣称完整类型检查。
- `npm run package:vsix` 成功，生成 `tmp/vscode/oxygen-language-support-0.2.0.vsix`，包含本地 Blockly、资源与 Apache-2.0 许可，无 CDN、独立网页服务或游戏／Unity 程序集。截图在忽略目录 `tmp/editor-ui/blocks-final.png`、`blocks-undo.png`、`blocks-diagnostic.png`。
- 未运行新的 RimWorld 游戏场景；这是编辑工具验收，不是新玩法验收。未覆盖 Windows／macOS 桌面、全部拖拽组合和大型画布；复杂 trait／impl 等仍使用文本入口，不支持任意源码反向转换。

## VSCode 作者工具（2026-10-10）

- `npm run package:vsix` 成功发布 Core 与 Editor 检查器，生成 `tmp/vscode/oxygen-language-support-0.1.0.vsix`；最终包在隔离的 VSCode Server 扩展目录安装成功，不携带游戏／Unity 程序集。
- 16 个代码片段展开默认值后全部通过实际 parser/analyzer。TextMate 运行时检查 trait 方法签名、泛型、块注释、插值内注释、比较运算及 optional use 别名；语言配置正则在 JavaScript 中可编译，中文标识符和点号分隔通过定向检查。
- 实际 Editor 进程配合 VSCode API shim，观察错误文本 → 未保存修正 → 保存错误的诊断更新、Outline 声明、关闭文件撤销诊断、关闭自动诊断后手动检查仍有效。未受信任时不返回诊断或 Outline，授予信任后恢复检查。shim 结果不视为真实 UI 验收。
- 在真实浏览器中的 VSCode Server 打开 `.oxy`，观察 Oxygen 语言识别和 trait／struct／impl／泛型语法高亮。受限模式执行 **Oxygen: Check Current Document** 显示需要工作区信任的提示。高亮截图：忽略目录 `tmp/editor-ui/oxygen-highlight-final.png`。
- 当时未获得临时工作区 Trust 操作确认，未绕过信任；文本 Problems、Outline、片段插入及受信任状态下的编辑更新尚未完成 UI 验收。未覆盖 Windows／macOS、SSH／容器环境。该阶段 Blockly 仅完成设计；后续积木实现与验收见上节。

## IrisMenus 设置适配、语言扩展与 optional use（2026-10-10）

- 框架解决方案及 PrisonerNames 真实消费端 Release 编译：0 警告、0 错误；Oxygen 行为检查 492 项通过。
- 定向 smoke 通过 11 个场景：trait 约束与静态 `Self` 工厂、不同 struct 特化、重复推断回调、动态别名成员约束、`Self` 字段与参数、泛型转发、插值中的块注释、多行注释语句分隔。
- smoke 直接链接实际 `OxygenSettingsCompiler`、`ScriptCompiler` 与 `OxygenSettingsHost`：设置页回调和 mood 规则回调均执行成功；导入模块坏载入时保留旧设置计划，修复后才替换新计划。
- IrisMenus 引擎边界检查 164 项通过；覆盖设置页注册／滚动、number／slider／text／select 写入、同 ID 页面保留、旧弹窗拒绝写入、空计划移除、适配器较晚初始化、optional use 缺失／存在、读取异常和坏源码等边界。
- 发布脚本在临时目录执行：框架 29 文件、PrisonerNames 11 文件 SHA-256 校验通过；可选适配程序集分目录，消费包不捆绑共享依赖。临时 smoke 工程与发布副本在验证后删除。
- 未重启正在运行的玩家游戏，未部署新框架／消费端到游戏安装目录；本轮没有真实 Unity 设置 UI、屏幕缩放或新入口的存读档实测，不将引擎 doubles 当作视觉验收。未覆盖大型模组集合、全部 DLC 组合和长时 GC。

## 原生内容与 RHAH 第一批验收

- 最终 `dotnet build Oxygendsl.slnx -c Release --nologo`：0 警告、0 错误；`dotnet Tests/bin/Release/net9.0/Oxygendsl.Checks.dll`：373 项行为检查通过。
- 离线真实消费端读取 `Examples/NativeTraits`，声明 3 个 TraitDef、3 个 ThoughtDef 并修改 Industriousness commonality；规则试算得到幼儿阶段 1、自身疾病未激活、群体疾病阶段 0。目录记录 Core Def SHA-256；RHAH 示例明确事实试算为 allow，未指定完整目录时退出 2／不完整而非假成功。
- 定向修正 smoke：原生 Def 继承按 defName 解析，嵌套 `degreeDatas.0.label`／`statOffsets.0.value` 修改成功；原生 XML 继承列表追加／移除保留其它成员，连续字段修改保留先前修改和正确前后值。
- Windows Unity Mono／RimWorld 1.6.4871，隔离安装 `C:/RimCarbon14/oxygendsl-native-game-20261009`，配置 `C:/RimCarbon14/oxygendsl-native-acceptance-20261009`。执行文件和 Mods 独立，游戏 Data 只读共享；没有覆盖另一正在运行的玩家游戏安装。
- 实例 `493bfc13e18e4695a14fed50d9f21e77`：原生 Def 归属正确，Industriousness commonality 2 → 1.5；授予 HardWorker 后实际 WorkSpeedGlobal 1 → 1.15。幼儿饥饿产生阶段 0／-4，添加 Flu 后阶段 1／-8；移除所需特质后原生思想消失。
- 同进程规则重载把实际思想阶段 1 改为 0；未知 Pawn 字段导致重载失败，仍保留阶段 0；恢复原规则回到阶段 1。内容倍率编辑拒绝热更新并显示待重启，仍保留上一规则。重复闸门注册失败不清空原快照，停用／启用提供者不影响其它提供者。
- 保存并读回 `oxygendsl-native-disabled`：已授予特质保留、该提供者规则仍停用；启用后恢复原生阶段 1／-8。不是原生内容卸载验收。
- 加入真实 HAR、NewRatkinPlus、IrisMenus 和已部署 RHAH 后，实例 `6f4adb58727a4cc7b4e064aa597336f0`：真实 Ratkin／RHAH_Api.Allows 消费脚本许可，覆盖 hungry allow、Pawn deny 优先、fed abstain／默认允许、Leaving deny、停用策略恢复原默认。不是 mock API 或周期 SetGate。
- 实际脚本窗口「重新加载 Mod 规则」按钮返回 activated，截图显示加载反馈和原生内容检查；截图在隔离配置 `RimCarbon14/artifacts/6f4adb58727a4cc7b4e064aa597336f0/7a49bc4a3db949a5867e369ddcbec2f0.png`。
- 最终编译候选实例 `eb850b20be4244e7ae660de832a85247`／PID 17808 再加载隔离存档，特质仍在、阶段 1／-8、规则重载成功、真实 RHAH 权限路径通过；另存 `oxygendsl-native-final` 后正常退出。原始回执在忽略的 `tmp/carbon-live.jsonl`，简要结果 `tmp/native-results.json`；临时原生探针不进入发布包。

本批未新增动作 DSL，没有证明新的寻路／Job 完整生命周期、大型 Mod 组合或无损卸载。不重跑原 MVP 性能矩阵；最终只集中做定向 checks/smoke 与上述游戏路径，后续问题按实际反馈修正。

## 类型、struct 与模块实现验收

- `dotnet run --project Tests/Oxygendsl.Checks.csproj -c Release`：360 项行为检查通过，含数值边界、可空与名义类型、上下文字面量、方法接收者、失败写入原子性、模块实例/导出隔离、错误位置及调用预算。集合状态模型使用种子 `20261009`，20 条、每条 30 步增删改轨迹，与独立 C# list 模型逐步比较。
- 独立消费端 smoke 读取真实 `Oxygen/helpers.oxy` 与 `Examples/typed-mood.oxy`，完成导入、类型化组合和心情注册，检查 food=0.5/0.25/null、倒地分支、250 次复用条件调用、+5/+8 重新编译及依赖/字段错误拒绝。
- `bash scripts/deploy.sh` 构建 0 警告、0 错误，SHA-256 校验 19 个发布文件；发布清单包含 `Oxygen/` 辅助库。
- 真实游戏进程 PID `16348`、实例 `29cb67652b9242edb9f8f078e8d15850`，使用下方相同隔离配置与 Mod 集合。`tmp/syntax_live.py` 已运行至保存恢复与文件按钮；应用按钮遇到桥接 `stale_layout`，重新读取布局后仅重做该未执行动作，观察成功，没有将整组失败当作通过。
- 30 个实际殖民者获得 `fed_comfort` +5，改为「模块化 +8」后思想对象句柄不变；缺失模块及错误 bonus 类型拒绝应用，版本和已有效果不变。另存并读回 `oxygendsl-syntax-dcc4479a` 后仍为 +8。
- 实际「从文件读取」按钮加载已部署的 typed-mood，效果变为 +5；实际「应用脚本」按钮提交 +8，截图显示活动效果 30、匹配 30、跳过 0。停用后活动效果为 0。
- 截图：隔离配置 `RimCarbon14/artifacts/29cb67652b9242edb9f8f078e8d15850/22005d58754c4ff79f187962cd4700c9.png`。回执与恢复记录保留在 `tmp/carbon-live.jsonl`、`tmp/syntax-results.json`；可复用客户端与验收脚本保留，不进入发布包。
- 最终程序集另在 PID `30792`、实例 `96f66df9e8a345dcaf798fb72d4276b0` 的 Unity Mono 中验证：加载上述存档、应用发布示例得到 30 个效果；绑定方法的 array 字面量与 present-null/缺失索引 exists 查询正常；错误 `_ : int` 参数及未调用函数内的缺失导入类型均拒绝，保留版本和 30 个效果。停用清理后正常退出；结果在 `tmp/syntax-release-results.json`。

当时没有重跑下方旧版性能矩阵，不能用旧数字声称类型/模块无性能影响。模块可变状态和源码快照不持久化，读档重新读取依赖；当时游戏能力仍是动态心情宿主，不含任意 C#、Harmony 或文件权限。

## 原始动态心情验收环境与证据

- RimWorld `1.6.4871`，Windows x64 / Unity Mono；1280×720，简体中文。
- 隔离配置：`C:/RimCarbon14/oxygendsl-20261009`，不覆盖玩家存档。
- 模组集合：Harmony `2.4.2`、Core、Royalty、Ideology、Biotech、Anomaly、Odyssey、RimCarbon14、Oxygendsl；隔离配置自动启用了已安装 DLC，不把此结论扩大到大型模组列表。
- 最终候选进程：PID `5324`，实例 `2fa3c3c5ee584c20911ac3f63005b877`。脚本修改和性能窗口均在同一进程；为加载修正过的程序集而进行的开发重启不算脚本热更新。
- `dotnet build Source/Mod/Oxygendsl.Mod.csproj -c Release`：0 警告、0 错误。
- `dotnet run --project Tests/Oxygendsl.Checks.csproj -c Release`：26 个行为检查通过，含注册表达式、错误位置、只读上下文、边界与调用预算。
- 部署：`bash scripts/deploy.sh`，逐文件 SHA-256 校验。发布包仅包含两份 Oxygendsl DLL，不包含 Harmony、游戏程序集或临时测量程序集。

原始回执和可复用工具保留在忽略的 `tmp/`：`carbon.py`、`live_smoke.py`、`edge_smoke.py`、`final_edges.py`、`perf_run.py`，以及 `smoke-results.json`、`edge-results.json`、`perf-results.json`、`carbon-live.jsonl`。`tmp/Perf` 原生探针由桥接显式加载，不注册 GameComponent、不创建 Harmony 补丁、不进入发布包。跨进程对象句柄和方法 ID 必须重新发现。

## 游戏内行为

`tmp/live_smoke.py` 在最终候选通过 50 个断言，保存并加载 `oxygendsl-smoke-20112ce0`：

验证结束另存 `oxygendsl-demo-db520fa6`（30 人、实际编辑 +12），随后正常停止隔离测试进程；玩家原配置和存档未改动。

| 契约 | 实际结果 |
| --- | --- |
| 新增与修改持续效果 | 3 个真实殖民者得到 +5；改为 +12，思想对象身份不变，进程实例不变 |
| 条件切换 | 真条件保留，假条件清理；同 Pawn 的不同规则分别判断 |
| 固定选择 | 缩小到一个 Pawn；临时切换 UI 选择不改变绑定 |
| 反复应用 | 持续规则连续应用 20 次不累积；记忆连续应用 20 次不重置触发时间或冷却 |
| 修改失败 | 语法或条件运行错误保留有效版本和效果；错误包含行列与规则／Pawn |
| 归属隔离 | 同标签不同 ID 共存；删除其中一条不删除另一条 |
| 限时记忆 | 冷却到期刷新同一对象；叠加不超上限；条件变假保留已有记忆，到期贡献为零 |
| 保存恢复 | 触发 tick、截止 tick、精确保存年龄和固定选择恢复；立即评估不重复触发 |
| 停用／启用 | 停用清理拥有的全部效果；重新启用尊重尚未到期冷却 |
| 空脚本 | 清理规则和效果；周期入口不执行目标评估 |

`tmp/edge_smoke.py` 最终通过 3 个断言：叠加增长到三条、热更新上限降为一立即清理、改为刷新立即保留一条。修正前真实游戏复现了后两个场景失败。

`tmp/final_edges.py` 的 18 个实际断言已观察通过：空固定选择、显式重新捕获、每次应用单次评估、地图目标跳过动物、周期错误保留效果并恢复、清理不影响原版记忆、暂停应用不改写当前心情条、规则类型切换、空脚本保存恢复、实际文件读取、键鼠输入编辑与按钮应用。`tmp/final-edge-results.json` 保存各断言和截图。桥接桌面输入受到本机 PowerShell 策略阻止时，调用已安装的同一输入脚本，仅对该进程使用 `-ExecutionPolicy Bypass`，不修改系统策略。

### 实际 UI

脚本窗口使用原版控件，需求页显示“热更新 +12”和实际 `12` 点数。真实“应用脚本”按钮已执行，UI 与对象读取一致。UI 缩放从 1.0 改为 1.25 后，窗口仍完整位于屏幕，关闭与文件读取按钮可见。

最终候选截图位于隔离配置的 `RimCarbon14/artifacts/2fa3c3c5ee584c20911ac3f63005b877/`：

- `713766a867ae480ea402f41d615f1abf.png`：原版需求页与 +12 效果。
- `a311c57ca7a64cef9ac5926c4bf8cea7.png`：1.25 缩放后的脚本窗口。
- `f84d935ce9d3438b9e9b879c0b66dbfb.png`：完整边界脚本通过后，真实键鼠编辑并应用的 30 人 +12 规则与文件来源。

## 性能方法

条件统一为 `pawn.food != null && pawn.food > 0.25 && !pawn.downed`。原生对照只把同一 `RuleProgram` 的条件替换为等价 C# `IRuleCondition`；目标收集、上下文、效果维护、清理和运行检查仍经过同一个宿主路径。不是拿空循环代替 DSL 玩法。

真实殖民者逐步增加至 3、10、30 人；规则数为 1、10，匹配时维持 `Pawn 数 × 规则数` 个真实思想对象。评估间隔 150 tick。

每组先预热 20 次，再在同一主线程方法内执行 200 次立即评估，记录宿主 `LastEvaluationMs` 的中位数和 p95；桥接请求序列化不在单次计时范围内。该计时包含目标收集、条件、效果同步和运行检查，但不含解析／注册。

随后使用 RimCarbon14 `watch`、深度 0、确切 Oxygendsl 覆盖方法 `GameComponentTick`，观察 600 个真实 tick。每组入口调用 600 次；非空组周期评估 4 次，空组 0 次。15 个窗口覆盖 tick `4755..13755`。表中的“组件 ms/tick”包含观测器开销；“总 ms/tick”只覆盖同步模拟，不含渲染。顺序采样的地图行为与 JIT／GC 会变化，不能把总 ms/tick 相减当作精确 Mod 净开销。

| Pawn | 规则 | 条件 | 评估中位数 ms | 评估 p95 ms | 组件 ms/tick | 总 ms/tick |
| --- | --- | --- | --- | --- | --- | --- |
| 3 | 0 | 空 | 0.0001 | 0.0001 | 0.0070 | 0.7120 |
| 3 | 1 | 原生 | 0.0037 | 0.0046 | 0.0027 | 0.7001 |
| 3 | 1 | DSL | 0.0194 | 0.0220 | 0.0032 | 0.8711 |
| 3 | 10 | 原生 | 0.0711 | 0.0768 | 0.0032 | 0.7371 |
| 3 | 10 | DSL | 0.2245 | 0.2660 | 0.0044 | 0.7153 |
| 10 | 0 | 空 | 0.0001 | 0.0001 | 0.0023 | 0.8409 |
| 10 | 1 | 原生 | 0.0102 | 0.0114 | 0.0027 | 0.8556 |
| 10 | 1 | DSL | 0.0620 | 0.0678 | 0.0036 | 0.8488 |
| 10 | 10 | 原生 | 0.3135 | 0.3630 | 0.0047 | 0.8170 |
| 10 | 10 | DSL | 0.8394 | 1.1110 | 0.0088 | 0.8339 |
| 30 | 0 | 空 | 0.0001 | 0.0001 | 0.0024 | 1.1728 |
| 30 | 1 | 原生 | 0.0306 | 0.0424 | 0.0028 | 1.1582 |
| 30 | 1 | DSL | 0.1919 | 0.2625 | 0.0047 | 1.1836 |
| 30 | 10 | 原生 | 1.7627 | 2.2519 | 0.0142 | 1.3189 |
| 30 | 10 | DSL | 3.4474 | 4.5744 | 0.0278 | 1.3180 |

最大规模 DSL 批次最大值为 14.3604 ms，中位数不能掩盖长尾。当前数据只支持该条件、规模和 150 tick 间隔。

### 修改到应用的反馈

30 个真实殖民者、1 条条件规则，预热 20 次后连续应用 200 次，交替修改 +5／+12。完整 `Apply` 包含解析、分析、注册、候选条件与效果提交：中位数 0.2259 ms，p95 0.2820 ms，最大值 0.6596 ms。原始结果在 `tmp/feedback-results.json`，临时程序集在 `tmp/Feedback`；同主线程内部计时，不含桥接或屏幕刷新延迟。首次 JIT 和未预热应用不由此数值代表。

作者只需修改一个脚本文档，不必在 XML Def 与 C# 间切换；本次实际验证了窗口编辑与一个 UTF-8 文件导入两条路径。无效果时可在同窗检查有效版本、活动数量、条件假／无心情需求原因与带规则的错误；尚未由真实作者衡量学习和定位成本。

### 分配计数限制

本机 Mono 虽提供 `GC.GetAllocatedBytesForCurrentThread`，真实调用始终返回零。用实际数组分配校准后判定不可用；UI 和结果记为 `-1`／不可用，而非“零分配”。因此本次没有可信的每次评估分配字节结论。

## 决策

这组数据没有证明必须先实现编译后端。30 人、10 条规则时，原生条件加同一宿主也有可见成本；继续扩大能力前，优先检查宿主的重复查找、效果维护与解释器函数调用分配，再用同一对照复测。当前周期默认值仍为 150 tick，不把立即评估成本乘到每 tick。

热更新的玩法闭环已观察到；尚无真实 Mod 作者试用，不能据此宣称易用性已经验证。未测大型模组集合、跨地图迁移、死亡／世界 Pawn 路径、长时 GC 和全部 DLC 组合；不承诺零冲突。
