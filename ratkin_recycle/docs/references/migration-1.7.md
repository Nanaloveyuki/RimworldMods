# RatkinRecycle 1.7 迁移记录

本页保留 1.6 vendor 后的可行性调查、1.7 迁移实现与验证范围。当前同时提供隔离的 1.6 / 1.7 发布内容，不包含原玩法缺陷修复。游戏或依赖更新后可继续追加新的兼容证据，已完成事项不再作为待办。

## 基线与实测

- Vendor 基线：`4be94c4`，`feat: vendor and decompile RatkinRecycle under dual license`。
- 当前安装的游戏为 `1.7.4981 rev136`，见 [Version.txt](/mnt/e/Apps/Steam/steamapps/common/RimWorld/Version.txt)。实际 `Assembly-CSharp` AssemblyVersion 为 `1.7.9786.4087`。
- 本机 .NET SDK `10.0.401`、运行时 `.NET 10.0.12`。Harmony 工坊 About 声明 Mod 版本 `3.0.1.0`，实际 DLL AssemblyVersion 为 `3.0.0.0`，两者不是同一版本字段。见 [Harmony About](/mnt/e/Apps/Steam/steamapps/workshop/content/294100/2009463077/About/About.xml)。
- 用户提供的 [1.7 源码快照](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/) 的 [AssemblyInfo.cs](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Properties/AssemblyInfo.cs) 声明 `1.7.9782.37458`，早于当前安装的二进制。源码用于理解调用链；关键 API 以当前 DLL 反射、反编译和实际执行为准。

迁移准备阶段已执行两项隔离探针，当时未修改正式源码、About、1.6 DLL 或游戏安装：

1. 将全部现有 `Source/**/*.cs` 编译到临时输出，目标 `net10.0`、`x64`，直接引用游戏根目录 `Assembly-CSharp.dll`、`UnityStubs.dll` 和 Harmony `1.7/Assemblies/0Harmony.dll`。结果：**0 警告、0 错误**。没有为了通过编译改动源码。
2. 在独立 .NET 10 进程载入该临时 DLL，以隔离的 Harmony owner 执行真实 `PatchAll`，成功安装全部 3 个补丁，并解析 `Pawn_NeedsTracker.pawn` 私有字段；退出前移除探针补丁。

第二项证明当前补丁可安装，不证明补丁执行、幼儿捕食、生产结果、心情 UI 或存档恢复正确。未启动游戏，不宣称游戏内验收通过。临时工程及产物在调查结束后删除。

### 正式迁移后的验证

- 正式 1.7 Release 构建、1.6 源码重建均通过，分别为 0 警告、0 错误。1.7 发布 DLL SHA-256：`de355aff65b0e4647f25fd714451d65dfc381ff02c41c3b2dd641c34b57afc18`。
- 冻结清单中的 5 个 1.6 文件逐项通过 SHA-256 校验；Common 的 3 个 XML、1 张贴图与原作者归档及 1.6 内容逐字节一致；两个发布目录均未混入外部依赖 DLL。
- 一次性无界面 .NET 10 冒烟实际载入 `1.7/Assemblies/RatkinRecycle.dll` 和当前游戏 DLL：安装全部 3 个真实 Postfix；实际 `ShouldHaveNeed` 验证 Ratkin 幼儿心情开关、非 Ratkin、无关需求及现有年龄阈值；实际调用被补丁包裹的生产/进食目标验证历史重置、精致/普通食物差异、2500 tick 食物窗口和产后窗口的截止边界。
- 直接执行真实 Job 的 `CarryAction` / `ChewTick`：验证首口与后续 deadline、同 tick 不重复啃咬、10 tick 最小间隔、伤害参数、营养增量与上限。对真实 `Hediff_Injury` 执行 `HealBleeding`：流血伤口按配置减轻、不流血伤口不变、0.001 最小治疗及无流血边界均通过。
- 同一进程使用实际 `ModLoadFolders.LoadDataFromXmlCustom` 解析配置，确认 1.6 选择 `1.6`，1.7 选择 `Common + 1.7`，每个版本恰好选到自身 RatkinRecycle.dll。About 的包名、双版本、依赖、顺序、旧包名冲突及 Def XML 另行通过解析核对。

冒烟边界：使用未生成到地图的受控 Pawn。为隔离引擎，跳过原版生产和吞食主体，仅执行真实 Mod Postfix；捕获啃咬发出的 `DamageInfo`，不执行护甲、实际伤害和死亡；贴图读取返回空，Hediff 只失效流血缓存，不触发渲染/头像/能力缓存通知。真实 Mod 的伤口严重度、需求和历史方法均执行。结果共 39 项断言通过，但不代表地图搬运、完整生产、致死、范围扫描、UI 或持久化通过。一次性工程及构建产物验证后删除；没有添加永久测试或改变玩法代码。

### 当前 DLL 的绑定结果

| 目标 | 当前参数 | 结果 |
| --- | --- | --- |
| `Pawn_NeedsTracker.ShouldHaveNeed` | `NeedDef nd` | Postfix 可安装；`pawn` 字段仍为 `Pawn` |
| `PregnancyUtility.ApplyBirthOutcome` | `RitualOutcomePossibility outcome, float quality, Precept_Ritual ritual, List<GeneDef> genes, Pawn geneticMother, Thing birtherThing, Pawn father, Pawn doctor, LordJob_Ritual lordJobRitual, RitualRoleAssignments assignments, bool preventLetter` | 现有命名参数绑定可安装 |
| `Thing.Ingested` | `Pawn ingester, float nutritionWanted` | 现有 Postfix 可安装 |

来源：[现有补丁源码](../../Source/RatkinRecycle/)、当前游戏 DLL 的运行时 Harmony 安装输出。

### 调度结论

不能因 1.7 提供 interval API 就批量把旧回调改成 interval 回调；提供的 1.6 源码中也已有这两套入口。

- 当前二进制 `Thing.DoTick()` 仍先调用 `Tick()`，再按更新率调用 `TickInterval(delta)`；`ThingWithComps.Tick()` 继续调用 `CompTick()`。原自动止血组件的入口没有仅因升级而消失。
- 当前二进制 `JobDriver.DriverTick()` 仍执行 `CurToil.tickAction()`；`DriverTickInterval(delta)` 是另一条入口。原啃咬 `tickAction` 不需要仅为编译迁移而替换。
- 实际迁移应保持现有游戏 tick 截止时间和间隔，不双挂两套回调，避免重复啃咬或治疗。

证据：当前 DLL 的 `ilspycmd --member M:Verse.Thing.DoTick` 与 `M:Verse.AI.JobDriver.DriverTick` 输出；源码快照 [Thing.cs:840](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Verse/Thing.cs)、[ThingWithComps.cs:344](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Verse/ThingWithComps.cs)、[JobDriver.cs:449](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Verse/AI/JobDriver.cs)；本项目 [JobDriver_EatRatEgg.cs:47](../../Source/RatkinRecycle/JobDriver_EatRatEgg.cs)、[CompAutoStopBleeding.cs:14](../../Source/RatkinRecycle/CompAutoStopBleeding.cs)。

## 已实施的版本隔离

1. Vendor 基线中的 `Assemblies/`、`Defs/`、`Textures/` 已按原字节迁入 `1.6/`，冻结清单见 [release-1.6-manifest.json](release-1.6-manifest.json)。原作者 DLL 仍单独归档，不覆盖接手版 DLL。
2. `Common/` 复制相同的 XML、贴图。`LoadFolders.xml` 中 1.6 仅加载 `1.6`，1.7 仅加载 `Common` 和 `1.7`；根目录不再放旧程序集。
3. 默认构建 1.7 `net10.0 / x64`，引用游戏根目录及 Harmony 3，输出到 `1.7/Assemblies/`，不复制外部 DLL。1.6 仍可通过 `-p:RimWorldVersion=1.6` 使用固定 NuGet 引用重建，输出仅到 `tmp/rebuild-1.6/`，不能误写冻结的 1.6 发布件；中间目录也按版本隔离。
4. 包名与 Harmony ID、程序集名、命名空间、Def 名称、玩法代码不变。About 增加 1.7、必要依赖加载顺序与旧包名冲突提示；更正鼠族链接，预览标注双版本。
5. 本次不部署、不修改玩家配置或旧 Mod 安装；游戏内 UI、完整捕食/生产和存读档场景的未测边界仍在下方列明。

参考：[KeepYourIdentity.csproj](../../../keep_your_identity/Source/KeepYourIdentity.csproj)、[Directory.Build.props](../../../keep_your_identity/Directory.Build.props)、[OhYouAreSoFast LoadFolders.xml](../../../oh_you_are_so_fast/LoadFolders.xml)。1.7 加载目录规则见 [ModContentPack.cs:302](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Verse/ModContentPack.cs)。

## 已核实的依赖

正确依赖为 **NewRatkinPlus / `Solaris.RatkinRaceMod` / 工坊 `1578693166`**。本机 [About.xml](/mnt/e/Apps/Steam/steamapps/common/RimWorld/Mods/1578693166/About/About.xml) 明确列出 `1.5 / 1.6 / 1.7`；[PublishedFileId.txt](/mnt/e/Apps/Steam/steamapps/common/RimWorld/Mods/1578693166/About/PublishedFileId.txt) 为 `1578693166`，并存在 `1.7/Assemblies/NewRatkin.dll`。当前 [About.xml](../../About/About.xml) 已使用这个核实的链接，保留原依赖包名，不切换鼠族分支。

导入时从 RHAH 复制的 `3497673755` 实际属于 [fxz 分支](/mnt/e/Apps/Steam/steamapps/workshop/content/294100/3497673755/About/About.xml)，包名为 `fxz.Solaris.RatkinRaceMod.odyssey`、只列 1.6；此次已纠正，不修改 RHAH 的元数据。RimCrow 的 `1578693166` 备份比当前本地 Mods / 工坊副本更旧，仍只列 1.5 / 1.6，不能用于否定当前副本的 1.7 支持。

可选 [Toddlers](/mnt/e/Apps/Steam/steamapps/common/RimWorld/Mods/2903359152/About/About.xml) 当前本机只声明 1.4–1.6，没有 1.7 目录。1.7 兼容未证实；不将它升级为必要依赖，也不以其旧版作为本次兼容验证前提。鼠族的 HAR 依赖由鼠族元数据声明；本项目不引用 HAR 类型。

## 与版本迁移分开的既有问题

以下问题已存在于恢复源码，不能归因于 1.7，本次迁移仍保留：

| 问题 | 证据与风险 | 后续处理 |
| --- | --- | --- |
| 年龄单位 | `AgeYears()` 用 `AgeBiologicalTicks / 60000f`，返回的是游戏天数却用于 1、3 岁阈值 | 单独修复并验证年龄边界；不能在声称玩法不变的迁移中悄悄改掉 |
| 倒地目标被排除 | `TickScan()` 收集捕食者和目标前统一要求 `!pawn.Downed`，会排除倒地婴儿 | 单独决定目标规则；不要放宽捕食者不能倒地的限制 |
| 状态没有持久化 | `RatkinRecycleTracker` 的生产、精致食物和冷却记录都是静态字典；`RatkinRecycleGameComp` 没有保存这些数据 | 设计逐存档状态、加载/新游戏清理和兼容策略后再修，不能宣称原状态会随存档恢复 |
| 捕食 Job 没有保存自身状态 | `victimStored`、`carried`、`finished`、下一口时间等没有自定义 `ExposeData()` | 验证捕食中存读档，另定恢复或安全中止规则 |
| 搬运失败后仍啃咬 | `CarryAction()` 记录 `TryStartCarry()` 失败却继续进入啃咬，后续不验证持有者或距离；成功结束时又直接丢下当前携带物 | 单独验证失败拾取、被他人抱走、活体转尸体及放置失败；不要借迁移增添手工预约释放机制 |
| 设置界面精度和宽度 | 控件横向延伸到 x=770；所有浮点滑条统一舍入到 0.05，治疗默认值却是 0.03 | [INFERENCE] 仅打开设置可能改变治疗量；单独修复精度和窄窗口布局，实机验证 |

源码：[AgeYears](../../Source/RatkinRecycle/RatkinRecycleUtility.cs)、[TickScan](../../Source/RatkinRecycle/RatkinRecycleUtility.cs)、[RatkinRecycleTracker](../../Source/RatkinRecycle/RatkinRecycleTracker.cs)、[GameComponent](../../Source/RatkinRecycle/RatkinRecycleGameComp.cs)、[捕食 Job](../../Source/RatkinRecycle/JobDriver_EatRatEgg.cs)。

年龄对照已在当前 DLL 反编译确认：`Pawn_AgeTracker.AgeBiologicalYearsFloat` 除以 `3600000f`，原 `AgeYears()` 的 `/60000f` 差 60 倍。倒地过滤的具体影响还取决于所选鼠族/Toddlers 的生命阶段定义，不能据此断言每种鼠族婴儿都使用原版阶段。静态历史没有保存不等于全部心情状态都不保存，原版需求与记忆另有序列化。

UI 风险来源：[RatkinRecycleSettings.cs:245、326、378](../../Source/RatkinRecycle/RatkinRecycleSettings.cs)；滑条舍入规则见提供的 [Widgets.cs:2012](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Verse/Widgets.cs)。设置文件在提供的 [Mod.cs:37](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Verse/Mod.cs) 按目录名和 Mod 类名定位，不能仅凭 packageId 判断是否沿用旧配置；实际发布目录改名后需确认配置路径。

## 尚未进行的完整游戏场景验收

- 冷启动：新旧版不同启；仅载入一份 RatkinRecycle DLL，三个补丁安装，无重复 Def、缺失类型或依赖提示。
- 设置：打开、滚动到底、修改阈值、恢复默认、保存后重启；确认控件可用、布局不被裁切、值持久化，观察真实 1.7 界面。
- 生产与进食：成功生产记录正确母亲；失败、死产、代孕/生长舱结果不被错误解释为成功生产；精致食物对母亲窗口的影响符合原功能约定。
- 捕食：预约、靠近、抱起、啃咬、饱食度停止、死亡结束、强制中断、目标移除、捕食者倒地；无悬空搬运对象，伤害和营养不会双算。
- 自动止血：友方/敌方/野生/动物开关、半径、周期、无伤口场景；治疗间隔与设置一致。
- 心情与信件：幼儿心情需求能显示，对应 ThoughtDef 生效，完整和中途停止信件符合设置。
- 存读档与多存档：捕食中存读档；关闭游戏后重载；返回主菜单再开另一存档。记录既有缺陷，不把状态丢失当成 1.7 迁移成功。
- 1.6 回归：冻结文件的哈希不变，迁移后的 1.6 加载目录只指向冻结内容；1.7 构建不得改写 1.6 DLL。

## 调查用文件指纹

| 文件 | SHA-256 |
| --- | --- |
| 冻结的 1.6 接手版 `1.6/Assemblies/RatkinRecycle.dll` | `42be0985a792cf40842720259c11120035b15872c47f3e1e5e66db10cd0ba284` |
| 当前游戏 `Assembly-CSharp.dll` | `eb0826b85af430d13962e5c097ef53d869cf6a031656fb6a3673168a761c9f23` |
| 当前游戏 `UnityStubs.dll` | `b9a47e34d975a6b41fdf2af7a2effd8080a543d34d0153ee5d8614efbb019270` |
| Harmony `1.7/Assemblies/0Harmony.dll` | `c8ef7856b2be3cf24b345c9b5c2f197f68fcf92649635064fce7b3ccc8f77cb0` |

游戏更新或依赖替换后，以上探针结果需要针对新二进制重新取得，不能只对照旧源码快照。
