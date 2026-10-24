---
status: accepted
date: 2026-10-18
---

# ADR 0002：工种发现与任务归属分离，精确适配第三方直接派工

“人力发电”出现在工作优先级界面，不保证囚犯实际执行的任务具有 WorkGiver 来源。本 fork 为 [issue #8](https://github.com/Nanaloveyuki/RimworldMods/issues/8) 采用“原生来源优先、已确认直接派工精确映射、未知不猜测”的归属规则，工资和计件共用归属结果；不替第三方重派工作，也不把识别结果当成派工许可。用户已批准实施；问题状态和运行验收以 issue 为准。

本文的 1.6 文件证据保留为历史依据；后续目标和验收环境按 [ADR 0003](0003-rimworld-1-7-migration.md) 切换到 1.7。归属与派工分离的决定继续有效，第三方具体 driver 与会话完成条件必须按实际 1.7 版本重新取证。

## 已确认的失配

PPTE2 的 [工资配置列表](../../Source/PrisonersPayToEat2Settings.cs) 在 `EnsureWorkTypesCached` 枚举全部 `WorkTypeDef`，没有按六种默认粗活过滤；[劳工许可](../../Source/Work/PrisonerWorkSystem.cs) 才有默认允许列表及不可开放工种。用户所见缺失不能直接简化为“工资列表硬编码漏了人力发电”。还须在实际设置窗口检查列表缓存、布局与配置持久化，不将源码枚举当成视觉验收。

基线 [PrisonLaborBridge.CurrentWorkTypeDef](../../Source/Patches/Harmony_PrisonLabor.cs) 只读取 `job.workGiverDef?.workType`；当前实现已收敛到共享归属入口。最初依据本机 `D:\References\Rimworld\Mods\NewRatkinPlus\1.6`，实施时又核对实际安装的 Workshop `1578693166/1.6/Released/1.6` 源码与 Defs；根 LoadFolders 选择的是 Released 子树。证据如下：

| 证据入口 | 内容 |
| --- | --- |
| `Defs/WorkTypeDefs/worktype.xml:4–15` | 工种为 `RK_WorkType_GeneratePower`；1.5 的 `RK_HamsterWheel` 不是本次版本标识 |
| `Defs/WorkGiverDefs/workgiver.xml:4–12` | `RK_Work_HamsterWheel` 指向该工种 |
| `Source/HamsterWheel/Work.cs:75–85` | 普通 WorkGiver 排除牢房内轮子；工作 Job 由常规工作分配接管来源标记 |
| `Source/HamsterWheel/JobGiver.cs:35–50` | 独立 `JobGiver_RunningWheelPrisoner` 针对囚犯、牢房轮子直接 `new Job(RK_JobDefOf.RK_Job_HamsterWheel, wheel)`，没有写 `workGiverDef` |
| `Patches/CompPatch.xml:3–6` | 独立 JobGiver 注入原版囚犯思考树，不经过 PPTE2 的默认工种优先级派工 |
| `Source/HamsterWheel/Job.cs:30–73` | 先走向轮子，再使用轮子；使用 toil 持续 4,000 tick，结束时调用 `UsingDone` |
| `Defs/JobDefs/jobdef.xml:4–12` | 同一 JobDef 含 `joyKind`；不能用该字段单独认定这是收费娱乐 |
| `About/About.xml:3–9` | packageId 为 `Solaris.RatkinRaceMod` |

原版 1.6 `RimWorld/JobGiver_Work.cs:219,288,299` 为常规工作写入 `workGiverDef`。因此两条任务虽然涉及相同建筑，来源信息并不相同；已确认的直接派工会被 PPTE2 识别为“没有工作”。这是参考源码机制证据，不冒充当前安装版本的游戏采样。

实际 Released 源码还确认：囚犯独立 Job 默认无 expiry；普通 WorkGiver 设置 `1500, checkOverrideOnExpire=true`，含义是检查是否需要替换任务，不是 1500 tick 自动成功。driver 先行走、再执行 4000 tick Delay toil；自然完成后 `CurToilIndex` 已越过两段 toil，且工作剩余 tick 耗尽。实现同时要求成功条件和该完成状态，不使用任务年龄或 `UsingDone` 清理事件。driver 没有调用娱乐 tick/收益入口，尚无游戏内双扣费观察；本批未添加全局娱乐豁免。

源码实施和运行证据见 [README](../../README.md#fork-维护与验收)。旧 1.6 文件证据不直接构成 1.7 契约；现已用真实 1.7 NewRatkin 程序集验证两条派工、4000 tick 自然完成/中断、工资模式/倍率，实际界面观察发电状态与余额，存读档保存真实工种键。Prison Labor 未纳入先前自动化 profile；用户随后已在个人生存档实测运行良好，本轮按要求不修改兼容对接。

## 决定

### 1. 同一工种，分开四项能力

采用 [词表](../../GLOSSARY.md) 的区分：

- **可发现**：已加载工种可配置工资，键为真实 `defName`，不依赖本地化显示名。
- **可派工**：仍由内置劳工、Prison Labor 或第三方各自控制；本兼容不移除原版/第三方的能力、区域、优先级及牢房限制，不默认开放所有新增工种。
- **可归属**：具体 Job 能被确认属于哪个工种；归属不隐含能否派发这份 Job。
- **可按量结算**：必须存在明确成功事件或可定义的完整会话。持续工作的存在不自动形成产出计件单位。

工资设置必须完整呈现已加载工种；劳工许可页保留可见性与禁止策略。工种数量增加时列表按实际内容高度计算，且核对隐藏/禁止项不影响工资发现；若实测仍有 UI 缺失，先在 #8 补充触发条件和回归，不凭显示名称添加特判。

### 2. 最小共享归属接口，不污染第三方 Job

将现有当前工作识别收敛为接收 `Pawn` 与**明确 Job**的内部解析入口；工资轮询、专用计件、通用结束快照均使用同一规则。解析结果按值携带 `WorkTypeDef` 和必要的归属来源/结算能力，不保留对下一次任务的隐式读取。

解析次序：

1. `job.workGiverDef?.workType` 存在时直接使用，绝不被兼容映射覆盖。
2. 来源标记缺失时，只检查已安装、已解析且与当前 Job/driver/目标匹配的直接派工适配。
3. 没有精确匹配返回未知，不根据 pawn 当前最高优先级、建筑相似名字、`joyKind` 或遍历全部 WorkGiver 猜测。

2026-10-24 增加可选的单项工作计费：解析结果同时保留任务自带的真实 `WorkGiverDef`。高级开关启用时允许其工资／模式覆盖父工种，`workType` 为空也能以真实来源键结算；关闭时忽略逐项配置。此项不推断没有来源的未知 Job、不注入来源、不开放派工。成功完成快照与科研贡献都保留该来源；无父工种且没有专用产出事件时按成功任务计件。

首个适配仅针对已确认的 NewRatkinPlus 仓鼠轮。Defs 加载后通过已知 packageId、`RK_Job_HamsterWheel`、`RK_Work_HamsterWheel` 解析真实工种，并校验 `RK_WorkType_GeneratePower`、driver 类型名 `NewRatkin.JobDriver_HamsterWheel` 与目标 `RK_HamsterWheelGenerator` 的预期。依赖未加载不注册；已加载但缺项/不匹配时至多给出一次兼容不可用说明，不在 tick 循环查 Def/打日志、不猜用 1.5 标识。

不硬引用 NewRatkinPlus DLL，不使用反射逐 tick 扫方法，不给所有 Job 写假的 `workGiverDef`。首个实现使用一个小型专用适配即可；未来出现第二种已证实直接派工再扩展内部规则，不预建公开注册框架。

### 3. 识别工资，不接管派工权

保留现有工资总门槛：内置劳工启用或 Prison Labor 在场时参与工资结算；两者都关闭时，不因解析出第三方工种就自动开工资。第三方直接派工是否继续执行由第三方决定，兼容适配不将其强行改造成内置工作优先级任务。

- 工资只针对存活的殖民地囚犯，并沿用当前 mental-state 等排除条件。
- `prisonerAllowedWorkTypes` 只约束内置派工，不拿它二次否决由其他系统合法安排、且原本已参与工资体系的任务。
- 被两个识别来源命中仍只有一份归属和一次工资处理。
- 普通殖民者、非殖民地囚犯、错误目标/driver、未知 Job 不产生囚犯工资。

这一选择不改变原有工资开关含义。如果要让 NewRatkinPlus 成为独立于现有总门槛的第三种工资来源，属于单独行为决策，不能藏在补映射中。

### 4. 默认按时，明确会话，不伪造发电量

- 仓鼠轮新适配默认按时，沿用既有 Job 存续期采样口径；本批不把所有工种改成仅实际 toil 运行时付薪。走向工作点是否计薪的统一口径属于后续独立决定，不能只对仓鼠轮暗改。
- 仓鼠轮没有可用的“发出一份电”成功事件。若玩家显式选择通用按量，则单位只能明确标为“成功完成一轮工作会话”，不是瓦特或发电 tick；只有自然成功完成才付一次，超时、失败、中断不算完成。
- 同一兼容任务的按时与按量选择必须互斥；如果实际版本的会话完成信号不可靠，UI 应明确不支持该按量能力，不能静默吞掉显式按量配置或回退到另一工资模式。
- #6 的 prefix 按值快照也用于无 WorkGiver 兼容任务，归池后不再重新识别；专用完成事件仍与通用完成互斥。
- `joyKind` 不是劳动/娱乐分类依据。先核对该 driver 是否实际调用娱乐计费入口；已确认为发电劳动的会话不得因复用字段被同时当作付费娱乐，真正娱乐路径也不得被粗暴全局豁免。本项必须观察实际调用，不先宣称存在双扣费。

## 验收及取证

实施顺序受 [ADR 0001](0001-correctness-before-performance.md) 控制；#8 不因解析器返回一个 Def 就关闭。

1. 取实际加载的 NewRatkinPlus packageId/版本、工种/WorkGiver/JobDef、当前 Job 与 driver；记录是否带 WorkGiver，核对用户“人力发电”在工资页和工作优先级页的实际呈现。已有用户现象不重复确认，只补机制证据。
2. 分别走普通 WorkGiver 与牢房独立囚犯 JobGiver；未修改任务本身的情况下归属同一真实工种。测试不把缺失的 `workGiverDef` 手工补上冒充修复。
3. 设置该工种不同于默认的时薪，验证配置重开与存读档保存；按既有采样口径核对实际余额、倍率、债务偿还及停工后的累计收尾。关计件、全局计件开但本工种按时、个体按时覆盖均有正确结果。
4. 显式按会话计件时验证完整成功、工作会话内中断、归池、连续两轮不双付；普通工作来源与适配来源都走相同结算入口。先按实际 1.7 driver 核对时长、toil 结构和成功事件，不直接复用 1.6 的 4000 tick/索引 2；若实际 Job expiry 导致没有自然成功，明确该模式不可支持，不用 timeout 冒充成功。
5. 覆盖内置劳工开/关、Prison Labor 接管、两者全关、囚犯释放/死亡/离图/读档、依赖未加载及错误 Def/driver/目标。缺失依赖不得导致红字，错误匹配不得发薪。
6. 最终按 ADR 0003 在真实 1.7 游戏中观察发电状态和饭票变化；机制探针或只编译不等于此验收。不再要求真实 1.6 游戏作为迁移前置；性能阶段仍须证明稳态解析不新增全 Def/建筑/WorkGiver 枚举和按次堆分配。

## 不采用

- 只把中文“人力发电”加进工资界面或默认粗活列表：既不能修复无来源任务，也会混淆派工许可与结算。
- patch 第三方 JobGiver 后强塞 WorkGiver：可能改变其他 mod 对任务来源的判断；优先在自有解析入口适配。
- 按所有建筑或 JobDef 自动反推 WorkType：映射多义，容易给娱乐、移动、失败任务发工资，并引入全局发现成本。
- 扩大默认囚犯工种权限、修 NewRatkinPlus 自身逐 tick 朝向/抖动性能或重写其牢房派工：不属于本 bug 范围。
