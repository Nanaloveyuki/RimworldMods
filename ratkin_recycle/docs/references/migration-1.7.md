# RatkinRecycle 1.7 迁移记录

本页保留 1.6 vendor 后的可行性调查、最初 1.7 迁移证据，以及随后独立进行的缺陷修复与设置扩展。当前同时提供隔离的 1.6 / 1.7 发布内容；历史迁移探针不代表后续版本的完整游戏验收。游戏或依赖更新后可继续追加兼容证据。

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

- 最初迁移阶段的正式 1.7 Release、1.6 源码重建均通过，分别为 0 警告、0 错误。当时的 1.7 DLL SHA-256 为 `de355aff65b0e4647f25fd714451d65dfc381ff02c41c3b2dd641c34b57afc18`，不是后续缺陷修复版本的指纹。
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
3. 默认构建 1.7 `net10.0 / x64`，引用游戏根目录及 Harmony 3，输出到 `1.7/Assemblies/`，不复制外部 DLL。1.6 仍可通过 `-p:RimWorldVersion=1.6` 使用固定 NuGet 引用重建，输出仅到 `tmp/rebuild-1.6/`，不能误写冻结的 1.6 发布件；中间目录也按版本隔离。后续可选 IrisMenus 适配器输出到 `1.7/IrisMenus/Assemblies/`，仅在启用 IrisMenus 时加载。
4. 包名与 Harmony ID、程序集名、命名空间、Def 名称、玩法代码不变。About 增加 1.7、必要依赖加载顺序与旧包名冲突提示；更正鼠族链接，预览标注双版本。
5. 本次不部署、不修改玩家配置或旧 Mod 安装；游戏内 UI、完整捕食/生产和存读档场景的未测边界仍在下方列明。

参考：[KeepYourIdentity.csproj](../../../keep_your_identity/Source/KeepYourIdentity.csproj)、[Directory.Build.props](../../../keep_your_identity/Directory.Build.props)、[OhYouAreSoFast LoadFolders.xml](../../../oh_you_are_so_fast/LoadFolders.xml)。1.7 加载目录规则见 [ModContentPack.cs:302](/mnt/d/References/Rimworld/Vanilla/1.7/Game/Assembly-CSharp/Verse/ModContentPack.cs)。

## 已核实的依赖

正确依赖为 **NewRatkinPlus / `Solaris.RatkinRaceMod` / 工坊 `1578693166`**。本机 [About.xml](/mnt/e/Apps/Steam/steamapps/common/RimWorld/Mods/1578693166/About/About.xml) 明确列出 `1.5 / 1.6 / 1.7`；[PublishedFileId.txt](/mnt/e/Apps/Steam/steamapps/common/RimWorld/Mods/1578693166/About/PublishedFileId.txt) 为 `1578693166`，并存在 `1.7/Assemblies/NewRatkin.dll`。当前 [About.xml](../../About/About.xml) 已使用这个核实的链接，保留原依赖包名，不切换鼠族分支。

导入时从 RHAH 复制的 `3497673755` 实际属于 [fxz 分支](/mnt/e/Apps/Steam/steamapps/workshop/content/294100/3497673755/About/About.xml)，包名为 `fxz.Solaris.RatkinRaceMod.odyssey`、只列 1.6；此次已纠正，不修改 RHAH 的元数据。RimCrow 的 `1578693166` 备份比当前本地 Mods / 工坊副本更旧，仍只列 1.5 / 1.6，不能用于否定当前副本的 1.7 支持。

可选 [Toddlers](/mnt/e/Apps/Steam/steamapps/common/RimWorld/Mods/2903359152/About/About.xml) 当前本机只声明 1.4–1.6，没有 1.7 目录。1.7 兼容未证实；不将它升级为必要依赖，也不以其旧版作为本次兼容验证前提。鼠族的 HAR 依赖由鼠族元数据声明；本项目不引用 HAR 类型。

## 迁移后独立修复的既有问题

这些问题存在于恢复源码，不是 1.7 引入；以下为后续 1.7 修复结果，冻结的 1.6 仍保留原行为。

| 问题 | 1.7 处理 |
| --- | --- |
| 年龄单位相差 60 倍 | 使用原版 `AgeBiologicalYearsFloat`，不再将游戏天数当作岁数；双方年龄分别配置，含下限、不含上限，支持单独取消上限 |
| 倒地目标被排除 | 仅捕食者要求未倒地；扫描器保留倒地的新生儿目标 |
| 静态历史串存档、未保存 | 历史迁入当前 GameComponent，以 Pawn 引用键保存；不存在记录用 `-1`，tick 0 有效；存活离图母亲保留记录，死亡/销毁角色与到期冷却清理 |
| 生产母亲与食物记录错误 | 依据实际生产者及返回的新生儿/死产尸体记录，排除代孕的遗传母亲和生长舱捐献者；只记录实际获得正营养的精致食物 |
| 捕食 Job 状态丢失 | 保存目标、种类、亲生关系、心情阶段、持有/完成/进食状态与下一口截止时间；缺失旧状态时保守恢复，不补发奖励 |
| 搬运失败仍啃咬、误丢他物 | 检查预约、拾取结果和双向持有关系；已有携带物不接单；只放下自身目标/尸体；Common Job 关闭原版通用丢弃，放置失败保留持有关系 |
| 外来回收误扣玩家关系 | 仅对准确回收伤害及其可追溯死亡，拦截原版派系通知内的负好感/转敌调用；玩家攻击、玩家拘押捕食者、混合来源流血仍处理 |
| 设置改精度、窄窗口裁切 | 数字输入保留 `0.03` 等精度，六页自适应宽度；IrisMenus 为独立可选适配器，原版入口保留 |

默认双方排除自由殖民者。囚犯、奴隶、非敌对外来者、敌对外来者、无派系分别配置；新身份键不读取旧笼统阵营开关。部位白名单按匹配种族实际身体列出，人工/缺失部位排除；禁用直接选中不等于避免上级部位丢失或流血。中文“鼠蛋”的英文采用 RatkinYoung，旧 Job/Def 标识保留以维持存档引用。

当前实现：[策略](../../Source/RatkinRecycle/RatkinRecyclePolicy.cs)、[扫描与部位选择](../../Source/RatkinRecycle/RatkinRecycleUtility.cs)、[逐存档历史](../../Source/RatkinRecycle/RatkinRecycleGameComp.cs)、[捕食任务](../../Source/RatkinRecycle/JobDriver_EatRatEgg.cs)、[外交归因](../../Source/RatkinRecycle/RatkinYoungDiplomacy.cs)、[设置页面](../../Source/RatkinRecycle/RatkinRecycleSettingsPages.cs)。

性能处理保留现有扫描节奏：地图只遍历维护中的 `AllPawnsSpawned`，候选使用复用列表；目标搜索以距离平方进行单遍筛选，只对更近候选做预约/可达性检查，不再生成排序列表。部位选择使用复用候选列表及白名单/优先级缓存，种族匹配不再逐角色转换整串小写或复制关键字列表；母性与目标完整策略在啃咬期限前检查，而不是每 tick 重复遍历亲缘关系。历史按原版时间保存，不因离图清除存活母亲。

### 后续运行时证据

在独立 .NET 10 进程实际加载当前游戏 DLL、正式 Mod DLL 与 Harmony 3，执行一次性场景：

- 策略场景：1 岁、3 岁上限、10000 岁与无上限、双方身份隔离、囚犯/奴隶优先分类；白名单穷尽不回退，缺失及人工祖先排除。预热后部位选择器连续 20000 次调用的线程分配为 **0 字节**；这不是地图整体耗时基准。
- 扫描器：实际执行 `TickScan`，近处不可达目标被跳过，选择较远可达的倒地新生儿；冷却、关闭开关、已有携带物均阻止重复派单。寻路、预约和 `StartJob` 的引擎边界被替换，未实际走路。
- 历史：实际 Scribe 保存/加载新组件和替换 Pawn 引用，覆盖三类历史、tick 0、缺失引用键、到期清理、跨 Game 隔离；生产/吞食场景直接调用正式 Postfix，未生成完整生产过程。
- 任务与外交：实际 Scribe 保存/加载目标引用及任务状态，执行拾取前已有携带物的失败分支；直接调用正式伤害/死亡/流血钩子，验证外来回收豁免、普通嵌套攻击/玩家捕食者/混合流血不豁免、伤口归因保存；当前 DLL 两个派系方法的 IL 形状检查与完整生产补丁 `PatchAll` 安装均通过。未模拟完整地图伤害链。
- 设置：实际 Scribe 数字/开关/白名单往返；六页在中英文与 500/900 宽度执行，检查输入、重置、禁用上限、0.03 精度、GUI 状态恢复；实际 IrisMenus 注册六个 SubItem，并执行搜索锚点滚动。字体测量、绘制和输入边界被拦截，不能作为实际渲染截图或视觉验收。
- 版本目录：实际执行 `ModLoadFolders.LoadDataFromXmlCustom` 与 `LoadFolder.ShouldLoad`，分别模拟 IrisMenus 未启用/已启用，1.7 恰好加载一个核心 DLL、按条件加载一个适配器；1.6 始终只加载冻结目录。模组启用查询为受控边界。
- Release 核心、可选 IrisMenus 适配器、1.6 源码重建均通过，0 警告、0 错误；冻结的 1.6 清单 5/5 哈希一致。验证不覆盖正在运行的其他项目测试游戏，未覆盖安装目录或玩家配置。一次性场景工程验证后删除。


## 尚未进行的完整游戏场景验收

- 冷启动：新旧版不同启；仅载入一份 RatkinRecycle 核心 DLL，可选适配器只在 IrisMenus 启用时加载；全部现有补丁安装，无重复 Def、缺失类型或依赖提示。
- 设置：打开、滚动到底、修改阈值、恢复默认、保存后重启；确认控件可用、布局不被裁切、值持久化，观察真实 1.7 界面。
- 生产与进食：成功生产记录正确母亲；失败、死产、代孕/生长舱结果不被错误解释为成功生产；精致食物对母亲窗口的影响符合原功能约定。
- 捕食：预约、靠近、抱起、啃咬、饱食度停止、死亡结束、强制中断、目标移除、捕食者倒地；无悬空搬运对象，伤害和营养不会双算。
- 自动止血：友方/敌方/野生/动物开关、半径、周期、无伤口场景；治疗间隔与设置一致。
- 心情与信件：幼儿心情需求能显示，对应 ThoughtDef 生效，完整和中途停止信件符合设置。
- 存读档与多存档：捕食中保存完整游戏后关闭并重载；返回主菜单再开另一存档。已通过组件/Job 的独立 Scribe 往返，仍需检查地图、世界 Pawn 和引擎任务恢复的完整组合。
- 1.6 回归：冻结文件的哈希不变，迁移后的 1.6 加载目录只指向冻结内容；1.7 构建不得改写 1.6 DLL。

## 调查用文件指纹

| 文件 | SHA-256 |
| --- | --- |
| 冻结的 1.6 接手版 `1.6/Assemblies/RatkinRecycle.dll` | `42be0985a792cf40842720259c11120035b15872c47f3e1e5e66db10cd0ba284` |
| 当前游戏 `Assembly-CSharp.dll` | `eb0826b85af430d13962e5c097ef53d869cf6a031656fb6a3673168a761c9f23` |
| 当前游戏 `UnityStubs.dll` | `b9a47e34d975a6b41fdf2af7a2effd8080a543d34d0153ee5d8614efbb019270` |
| Harmony `1.7/Assemblies/0Harmony.dll` | `c8ef7856b2be3cf24b345c9b5c2f197f68fcf92649635064fce7b3ccc8f77cb0` |

游戏更新或依赖替换后，以上探针结果需要针对新二进制重新取得，不能只对照旧源码快照。
