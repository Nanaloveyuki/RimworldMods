# 囚犯付费吃饭2（Prisoners Pay To Eat 2）

## Monorepo vendor fork

本目录作为普通源码目录 vendor 到 [RimworldMods](https://github.com/Nanaloveyuki/RimworldMods/tree/main/prisoner_pay_to_eat_fork)，不是 Git submodule。此节记录导入来源与维护边界，后续同步上游时应更新版本记录；下文保留上游使用说明。

- 上游：[ikun2522-art/PPTE2](https://github.com/ikun2522-art/PPTE2)，原作者：AAA不玩抽象。
- 导入日期：2026-10-18；分支：`main`；版本：`v2.3.2`。
- 上游 commit：[`5e5bab4db0b5cd4a55f161197a0a0caa085df08d`](https://github.com/ikun2522-art/PPTE2/commit/5e5bab4db0b5cd4a55f161197a0a0caa085df08d)。
- 许可证：[MIT](LICENSE)；保留上游版权声明，不改为根目录双许可。
- 当前 fork：`2.5.0`，主线仅构建 RimWorld 1.7 / .NET 10；包名为 `nanaloveyuki.prisonerspaytoeat2.fork`，Harmony 标识同步独立。1.6 的原 DLL 和完整加载资源冻结保留，不重编、不回填修复，也不维护第二套源码。

### Fork 维护与验收

- [ADR 0001：先修账务与工种兼容性，再缩减运行期开销](docs/adr/0001-correctness-before-performance.md)
- [ADR 0002：工种发现与任务归属分离，精确适配第三方直接派工](docs/adr/0002-work-attribution-and-direct-jobs.md)
- [ADR 0003：迁移到 RimWorld 1.7，冻结 1.6 发布内容](docs/adr/0003-rimworld-1-7-migration.md)
- [饭票经济词表](GLOSSARY.md)；bug 与验收跟踪：[免费食物误拒绝 #5](https://github.com/Nanaloveyuki/RimworldMods/issues/5)、[通用计件归池漏薪 #6](https://github.com/Nanaloveyuki/RimworldMods/issues/6)、[失败搬运计薪 #7](https://github.com/Nanaloveyuki/RimworldMods/issues/7)、[第三方工种漏管 #8](https://github.com/Nanaloveyuki/RimworldMods/issues/8)。

三份 ADR 已接受。#5–#8 的正确性修复已在真实 1.7 验收并关闭对应 issue；迁移与正确性提交为 `592fa7e`，性能改动在其后独立实施。原 1.6 发布版仍保留既有缺陷。

#### 已实施

- 找食与实际进食共用收费判定；个体免费、个体强制收费、全局免费及指定食物免费按同一优先级生效。营养膏按输出食物判定，保留实际份数收费和赊账。
- 通用计件在任务清理前用值类型保存角色与工种；归池后不再读取已清空的 Job/Driver。搬运只在成功的 Direct 放置后支付。
- NewRatkinPlus 囚犯独立派工按已加载的 packageId、Job、driver、目标和工种精确识别；原生 WorkGiver 来源优先，不修改第三方 Job，不扩大派工权限。工资总门槛仍为内置劳工启用或 Prison Labor 在场。
- 仓鼠轮沿用按时采样；显式按量时，一次自然完成的工作会话为一个单位。已在安装的 1.7 NewRatkin driver 验证先行走、再工作 4000 tick；仅成功且已走完两段 toil 才计件，超时、中断和清理回调不算完成。没有按发电量计价。
- Defs 加载后重建工资工种列表；劳工许可页按实际行数计算滚动高度，保留隐藏与禁止工种策略。中英设置说明同步说明仓鼠轮会话单位。
- 2.5.0：饮食与娱乐赊账下限默认 `-100`，设置范围 `-1000…0`；找食、实际进食和娱乐结算共用剩余额度。额度不足的整顿饭不消费；父母只贡献正余额，超额由孩子自己的额度承担。调高下限或关闭赊账不免除已有欠款，后续收入正常还债。卫生费沿用正余额支付。
- 器官申请阈值默认 `0`，设置范围 `-1000…100`；余额严格低于阈值才申请。申请独立于借款/乞讨/抢劫开关，玩家批准后才添加一项医疗手术。拒绝/撤除申请后冷却两天，已有申请或摘取手术时不重复请求。
- 器官选择顺序为肾、肺、其他成对内部器官、其他内部器官；仅选择原版可生成物品且直接摘除不会致死的自然器官。批准和执行时重新判断身份、余额、开关与身体状况。手术失败仍可能伤残或致死；失败不支付饭票。成功摘取肾支付40、肺35、其他30，每类器官终身限售一次；原有肾/肺记录继续有效。
- 原版人体在肾/肺之外没有其他可安全出售的掉落器官：胃没有摘除掉落物，心脏/肝脏直接摘除会致死。通用分支供具有额外可掉落器官的身体定义使用，不新增原版胃物品或伪造收益。

#### 构建与部署（1.7）

从 monorepo 根目录执行：

```sh
bash scripts/deploy.sh prisoner_pay_to_eat_fork --build-only --rimworld-dir /path/to/RimWorld
bash scripts/deploy.sh prisoner_pay_to_eat_fork --rimworld-dir /path/to/RimWorld
```

Windows 使用 `python scripts/python/deploy.py` 及相同参数。构建引用真实 1.7 游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 及 Harmony 3，不再使用 RimRef/net48。`RIMWORLD_DIR` / `HARMONY_ASSEMBLY_PATH` 或 MSBuild `RimWorldDir` / `HarmonyAssemblyPath` 可覆盖本机路径；部署入口可用 `--property HarmonyAssemblyPath=/path/to/0Harmony.dll`。

输出为 `1.7/Assemblies/PrisonersPayToEat2.dll`；正式暂存为 `artifacts/prisoner_pay_to_eat_fork/release`。正式部署要求退出游戏，默认目标为游戏 `Mods/PrisonersPayToEat2`；不会覆盖工坊原 Mod。启用列表须从原版 PPTE2 切换为本 fork，新旧包名不同但共享程序集与 Def，About 声明二者互斥，不能同时启用。存档类型、字段和程序集名不变；已有 fork 安装原地更新。部署保留目标已有的 `PublishedFileId.txt` 和 1.6 文件；新安装补齐冻结的完整 1.6 内容。

README、ADR 和其他开发文档仅保留在源码仓库，不纳入发布或部署。About 描述只介绍玩家可见功能。

`LoadFolders.xml` 明确分流：1.6 加载 `1.6/Released` 的上游资源和 `1.6/Assemblies` 原 DLL；1.7 只加载 `Common` 和 `1.7`。源码只维护 1.7；不要将新的设置文案或 DLL 同步回旧版快照。

#### 1.7 验收与支持范围

- 运行目标：RimWorld `1.7.4981 rev136`，独立游戏目录与独立 profile，不修改玩家存档或当前 Mod 列表。测试通过 RimCarbon14 调用真实生产程序集、原版 API 和第三方 driver，不以编译引用或替身代替游戏。
- 已观察进食豁免/强制收费、真实三份进食扣款、赊账与收入还债；真实 Job 清理归池/不归池/池满/连续任务、失败和中断；真实地图 Direct 放置失败后重试成功。
- 已运行 NewRatkinPlus 的普通 WorkGiver 与无来源囚犯 JobGiver，验证实际工种、模式与倍率、自然完成 4000 tick 会话仅付一次、中断不付；工资设置实际显示 `gen power`，键为 `RK_WorkType_GeneratePower`。本兼容不接管其派工权限。
- Dubs Bad Hygiene 1.7 已加载并验证真实厕所预留：队列检查不扣款，实际 driver 预留扣一次，余额不足拒绝。娱乐验证实际 `JoyTickCheckEnd` 的末 tick 同步结束和 `StartJob` 替换结算，避免漏费/双费。
- 已运行原版采矿、播种/收获、清洁、加工单和科研完成入口，核对专用单位与通用计件不双付；研究继续既有采样份额口径，不改为新经济规则。
- 已观察真实设置、个体配置、饭票菜单及囚犯总览/工作/日程/分配界面；1.7 存读档已核对余额、个体倍率/模式、借款、赎身/索要状态、真实工资键、劳作区、加工单权限和需求值。
- 最终真实运行探针通过 90 项行为断言，存读档另通过 11 项检查。包含加工单两种权限、派系索要即时到账/冷却、赎身实际释放、儿童先自付再由父母补差、身份释放与离图工资收尾、原版建造完成。器官出售 smoke 固定随机手术结果后调用真实配方、原版摘取/生成与付款，验证非致命和二次出售不可用；没有声称统计验证原版手术成功率。
- Prison Labor：用户已在个人生存档实测运行良好；本轮不修改接管判断、派工或兼容对接。先前自动化 profile 未加载它，不将用户实测写成自动化验收，也不再以包的版本声明否定该实测。
- 最终生产版 `2.4.0` 还在不启用 NewRatkinPlus、Dubs Bad Hygiene、Prison Labor 和临时测试 Mod 的独立 profile 启动并新建游戏，确认内置劳工启用、实际推进 500 tick，日志无 Error。
- 2.5.0 真实运行追加45项断言：20项消费/家庭额度、6项申请边界、5项信件存读档、11项账单存读档/真实摘除/掉落物与收入、3项失败摘除不付款。胃无原版掉落物已实测；通用器官分支通过临时赋予可掉落身体定义验证，未声称原版胃可以出售。设置与申请窗口有实际截图，回执 `debt-organ-smoke.json`；临时 Mod 不发布。
- 最终2.5.0生产包在移除临时测试 Mod、重置独立 profile 设置后重新启动、新建游戏并推进500 tick，日志无Error；新包名已加载，默认赊账下限-100、器官申请阈值0、内置劳工启用。证据为 `250-production-log.json` / `250-production-state.json`。
- 从 vendor commit `8539fff` 比对完整旧发布：25 个 DLL/Defs/Patches/Languages/Textures 文件逐字节一致；原 1.6 DLL SHA-256 为 `1885eeee58107c13067322562fdc55c3c9304b216d9c8a7a66fbc07422a85fa9`。这不是 1.6 游戏重验，也不承诺 1.6 存档跨游戏版本转换。

旧 37 项替身边界检查仅是修复过程证据，不充当本次游戏验收。真实 1.7 回执与截图保留在本机 `C:/RimCarbon14/ppte2-17-20261018/PPTE2-evidence`；临时测试 Mod 不随正式包发布。

#### 2.4.1 性能重写

- 工资、赎身保留原节拍和即时入账，普通地图使用原版维护的 spawned 囚犯集合；存在已跟踪的容器囚犯时保留完整集合语义。社交目标到实际需要选目标时才发现并复用，保留容器目标与实时余额，不按整轮缓存账户。
- 健康显示由捕获/释放和生成事件维护，已跟踪状态每 120 tick 收尾，旧档、容器与绕开事件的 Mod 路径每 2500 tick 完整兜底。零囚犯不再反复遍历全部殖民者；未完成战斗、坏账仍清理，劳作区和仅囚犯账单限制仍生效。
- 非目标进食不创建收费事件；计件补丁先排除非囚犯/无关记录；家庭代付复用单次账户权重；劳作区使用地图管理器弱键缓存。未调整 Prison Labor 对接，也未改研究贡献采样、还债顺序和工资反馈。

以下为独立真实 `1.7.4981` 进程中的 **manager 调度段**，不是完整模拟或 TPS。每图 100 名自由殖民者；囚犯静态工作、个体社交禁用，详细日志关闭。逐 tick 调用生产 manager，预热 60000 tick，再测 60000 tick，耗时为三次中位数；getter 计数补丁的开销前后均包含。单图测试另含游戏自然生成的野生动物，耗时只作该受控场景对照。

| 地图 / 囚犯数 | 调度耗时前→后（ms） | 线程分配前→后（bytes） | 完整发现前→后（次） |
| --- | ---: | ---: | ---: |
| 1 / 0 | 239.318 → 4.084 | 22,400 → 0 | 7,004 → 24 |
| 1 / 1 | 211.947 → 3.932 | 48,040 → 0 | 7,004 → 24 |
| 1 / 20 | 270.745 → 8.072 | 514,400 → 0 | 7,004 → 24 |
| 1 / 100 | 495.310 → 43.451 | 2,474,800 → 0 | 7,004 → 24 |
| 2 / 0 | 409.863 → 3.103 | 42,400 → 0 | 14,008 → 48 |
| 2 / 20 | 488.925 → 11.113 | 534,400 → 0 | 14,008 → 48 |
| 2 / 100 | 797.651 → 45.739 | 2,494,800 → 0 | 14,008 → 48 |

同场景工资结果逐次与 `592fa7e` 基线一致。另通过 65 项真实运行检查：三种家庭付款模式及负余额/资金耗尽、全局社交总开关、零囚犯战斗/坏账清理与禁工、真实休眠舱收纳/弹出及服刑初始化、实际进食/赊账、任务归池计件、真实存读档与死亡健康清理。10000 次非囚犯进食前后补丁调用分配由 320192 降至 96 bytes；该局部调用不含原版进食自身分配。回执为前述本机证据目录的 `perf-before.json`、`perf-after.json`、`perf-summary.json`、`perf-smoke.json`。

临时探针曾将成人改为儿童而保留成人衣物/胡须，触发原版贴图与外观校验日志；这不作为生产版无错误日志的证据。探针不发布，生产包单独启动验证。

## 上游说明

[![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-blue)](https://store.steampowered.com/app/294100/RimWorld/)
[![Steam Workshop](https://img.shields.io/badge/Steam-Workshop-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3780939234)

> ⚠️ 实验性版本（Exp）· Experimental build

囚犯再也不能白嫖殖民地的食物了！每一位囚犯进食都必须支付「饭票」，没有饭票就只能挨饿。

Prisoners can no longer freeload off your colony's food supply! Every meal a prisoner eats must be paid for with a meal ticket — no tickets, no food.

---

## ✨ 功能特性 / Features

### 🍚 饭票经济 / Meal Ticket Economy
- 囚犯进食必须支付饭票，价格由食物的**市场价值**决定（精致食物贵，简单食物便宜，营养膏按营养值折算）
- 支持小数饭票（如 0.01 张），价格与余额精确到百分位
- 支持**逐种食物单独定价**：设置页可对每种食物单独设置饭票价格
- **赊账机制**（默认开启）：余额不足时囚犯仍可吃饭，余额扣成负数（欠款），之后工资等收入优先还债；健康页会以红字显示欠款。关闭赊账后，余额不足的囚犯将吃不到饭（食物保留不浪费）
- Prisoners must pay meal tickets to eat. Price is based on the food's **market value**.
- Fractional tickets supported (e.g. 0.01); prices and balances work down to two decimals.
- Per-food pricing: override the ticket cost of any individual food item.
- **Meal credit** (on by default): broke prisoners may still eat and the balance goes negative (debt); wages and other income repay the debt first. Debt shows in red in the health tab. With credit off, a prisoner who can't afford a meal simply cannot eat it (the food is kept, not wasted).

### ⛏️ 囚犯工作赚饭票 / Prisoners Work for Tickets
- **内置囚犯劳工系统（v2.3+）**：无需安装 Prison Labor，囚犯也能打工！默认只开放粗活（采矿/建造/种植/割除/搬运/清洁），其余工种可在设置中勾选；看守/医生/驯兽/灭火/狩猎等敏感工种永不开放
- 新增**「囚犯」主标签页**统一管理：**总览**（每囚犯工作开关、动机条、饭票余额、当前状态）· **工作**（1-4 优先级表）· **日程**（时间表 + 活动区限制）· **分配**（医疗/着装方案/饮食/药品）
- **动机系统**：无人看管或挨饿的囚犯动机下降，过低会怠工并降低工作速度；被看管、吃饱饭、领到工资会提升干劲（可在设置中关闭/调阈值）
- **囚犯劳作区**：建筑规划 → 区域中划定后，殖民者不会在区内工作，把活留给囚犯
- **账单限制**：生产台账单可设「仅囚犯 / 仅殖民者与奴隶」
- 安装了 **Prison Labor** 时内置系统自动停用，由它接管工作分配（工资结算不受影响）
- 按**工作种类**分别设置每小时饭票；**按时 / 按量双计费**，按产出发饭票——生产台按批次、采矿按矿脉、建造按建筑、种植/砍伐/播种按株、搬运按次、清洁按块、研究按科技（多人按贡献比例分）
- **未知工种通用兜底**：其他 MOD 新增的工种自动支持按量（按完成一次工作计费），无需额外配置
- 每个囚犯可单独覆盖计费方式（跟随全局 / 按时 / 按量）；全局工资倍率 + 个人工资倍率可叠加
- **Built-in prisoner labour system (v2.3+)**: prisoners work without Prison Labor! Dumb labour (mining/construction/growing/cutting/hauling/cleaning) is on by default; more work types can be enabled in settings; warden/doctor/handling/firefighting/hunting are never available.
- New **"Prisoners" main tab**: **Overview** (per-prisoner work toggle, motivation bar, ticket balance, status) · **Work** (1-4 priorities) · **Schedule** (timetable + allowed area) · **Assign** (medical/outfit/food/drugs).
- **Motivation system**: unsupervised or starving prisoners lose motivation and refuse to work below a threshold; supervision, full bellies and wages raise it (toggleable).
- **Prisoner labor area**: designate an area where colonists won't work, leaving the jobs to prisoners.
- **Bill restriction**: production bills can be set to "prisoners only / colonists & slaves only".
- With **Prison Labor** installed, the built-in system steps aside and lets it assign work (wages keep flowing).
- Per-work-type hourly wages; **hourly or piece-rate billing** — piece rate pays per completed output: production per recipe batch, mining per vein, construction per building, growing/cutting per plant, hauling per haul, cleaning per filth, research per tech (split among contributors by work share).
- **Generic fallback for unknown work types**: work types added by other MODs support piece rate automatically (paid per completed job).
- Per-prisoner billing override (follow global / hourly / piece-rate); global wage multiplier + per-prisoner wage multiplier stack.

### 🏥 贩卖器官换饭票 / Sell Organs for Tickets
- 新增两种医疗手术：**摘取肾脏** / **摘取肺**，成功后囚犯获得饭票
- 非致命保障：仅当囚犯仍有两颗肾/两叶肺时才可摘取其一
- 摘下的器官作为物品正常生成；每个器官只能卖一次
- 可在设置页一键关闭，或对单个囚犯单独覆盖
- Two new surgeries: **remove kidney** / **remove lung**, paying tickets on success.
- Non-lethal safety: an organ can only be harvested while a matching one remains.
- Harvested organs spawn as items; each organ sells only once.
- Can be disabled globally in settings, or overridden per prisoner.

### 🔓 囚犯赎身 / Prisoner Ransom
- 囚犯攒够一定数量的饭票（默认 **1000**，可配置）并满足最短囚禁时间后，有一定概率提出**赎身请求**
- 请求需要**玩家同意**：同意后自动扣除对应饭票并**自动释放**囚犯；拒绝后需等待一段时间才能再次请求
- 全局设置：赎身所需饭票数、最短囚禁天数（自成为囚犯起算）
- 单个囚犯可单独覆盖所需饭票数与最短囚禁天数（配置囚犯窗口，0 = 跟随全局）
- Once a prisoner has saved up enough tickets (default **1000**, configurable) and served the minimum imprisonment time, they may occasionally **ask to buy their freedom**
- The request needs **your approval**: approving automatically deducts the tickets and **releases the prisoner**; rejecting starts a cooldown before they may ask again
- Global settings: required ticket count and minimum imprisonment time (since capture)
- Per-prisoner override for both values (prisoner config window, 0 = follow global)

### 🎛️ 玩家管理 / Player Controls
- 选中囚犯：一个**「囚犯饭票」按钮**整合全部操作——饭票余额、发放/扣除饭票、配置囚犯、赎身批准
- 囚犯的**健康标签页**会以「健康状态」形式直接显示饭票余额，颜色随余额变化（绿/黄/橙/红），悬停可见具体数量；欠款显示为红字
- 每个囚犯单独配置：个人食物倍率、个人工资倍率、计费方式（按时/按量）、是否允许贩卖器官、赎身所需饭票与最短囚禁天数、**借款/抢劫/乞讨/强制工作各自允许/禁止**
- 手动发放/扣除支持小数
- One **"Prisoner tickets" button** on a selected prisoner gathers every control — balance, give/take tickets, configure, ransom approval.
- The prisoner's **health tab** shows the ticket balance as a "health status" row, colored by how much is left (green/yellow/orange/red); debt shows in red; hover for details.
- Per-prisoner settings: food multiplier, wage multiplier, billing mode (hourly/piece-rate), organ-sale permission, ransom ticket count & minimum imprisonment time, and **per-feature allow/deny for loans, robbery, begging and forced work**.
- Manual give/take supports decimals.

### 🗣️ 囚犯社会行为 / Prisoner Social Behaviors
- 吃不起饭的囚犯会自己想办法：**借款 / 乞讨 / 抢劫**（AI 自动触发，可全局开关或单囚犯允许/禁止）
- **借款**：向其他囚犯借饭票，**利率随好感度浮动**（好感越低利息越高）；借方收入自动优先还贷；借方被释放/死亡则坏账，贷方心情受损；有未还借款的囚犯无法赎身
- **乞讨**：成功率取决于对方对自己的好感度，成功讨到少量饭票，失败则丢脸（心情减益）
- **抢劫**：真实近战！谁先倒地谁输（可能致死），赢家拿走目标余额的一部分；输家挨打还丢脸
- 可开启「向玩家借款」：囚犯会弹出请求窗口，由玩家逐笔批准（默认关闭）
- Broke prisoners take matters into their own hands: **borrowing, begging and robbery** (AI-driven; global toggles plus per-prisoner allow/deny).
- **Loans**: borrow tickets from other prisoners — **interest scales with opinion** (the worse they like you, the higher the interest). Income automatically repays loans first; if the borrower is released or dies it's bad debt and the lender's mood suffers. Prisoners with outstanding loans can't ransom themselves.
- **Begging**: success depends on how much the target likes the beggar; small handouts on success, embarrassment on failure.
- **Robbery**: a real melee fight! Whoever falls first loses (can be lethal); the winner takes a share of the victim's balance. The loser gets beaten up and humiliated.
- Optional "borrow from the player": prisoners pop a request window you approve one by one (off by default).

### 👶 儿童父母代付 / Parents Pay for Children
- 儿童囚犯（Biotech，成人年龄线以下）吃饭时，费用可由**在押的父母**代付——生父母/养父母都算，父母被释放/招募/越狱/死亡后代付自动失效
- 三种扣费模式（全局设置）：**先自己**（孩子余额优先，父母补差）/ **先父母**（父母按余额比例分摊，孩子补差）/ **合并钱包**（家庭视为一个账户，按各自余额比例共同分摊，健康页显示合计余额）
- 多位在押父母按**余额比例**平摊（负余额的父母不参与）
- 父母有钱的儿童不会去乞讨/借钱/抢劫；抢劫目标仍只看被抢者自己的实际余额，抢不走父母的饭票
- **赎身同样支持父母代付**：儿童可用余额（含父母）满足赎身要求即可提出请求，批准后从家庭余额中扣除
- 可全局开关（默认开），也可在「配置囚犯」窗口对单个儿童允许/禁止
- Child prisoners (Biotech, below the adult age) can have their meals paid from their **imprisoned parents'** tickets — biological and adopted parents both count; support ends automatically when a parent is released, recruited, escapes or dies.
- Three payment modes (global setting): **Own first** (child's balance pays first, parents cover the shortfall) / **Parents first** (parents split by balance share, child covers the shortfall) / **Shared wallet** (the family is one account; costs are split by each member's balance share, and the health tab shows the combined balance).
- Multiple imprisoned parents **split costs proportionally to their balances** (parents with negative balances don't contribute).
- A child backed by well-off parents won't beg, borrow or rob; robbery targets still count only the victim's own balance — nobody can steal the parents' tickets by attacking the child.
- **Ransom also supports parent pay**: a child can request freedom once the combined available balance (including parents) meets the ransom cost; approving deducts from the family balance.
- Global toggle (on by default) plus a per-prisoner allow/deny in the prisoner config window.

### ⚙️ 其他 / Extras
- **自定义饭票名称**（如改成"代币""劳动券"）
- 设置页五个标签页：通用设置 / 按工种饭票 / 食物价格（带搜索）/ 囚犯社会 / 囚犯工作
- 越狱 / 精神崩溃期间跳过饭票检查（可关闭）
- 中英双语
- Custom ticket name (e.g. "token", "labor voucher").
- Settings tabs: General / Work-type wages / Food prices (searchable) / Prisoner social / Prisoner work.
- Ticket check skipped during prison breaks / mental breaks (toggleable).
- Bilingual UI (简体中文 / English).

---

## 🐛 更新日志 / Changelog

### v2.3.2
- 🐛 **修复：与其他 MOD 共存时可能导致「地图无法生成 / 渲染报错」**。劳作区避让与 Dubs Bad Hygiene 兼容补丁原先在 Mod 构造函数里执行，会枚举并补丁其他 MOD 的类型，而触碰其他 MOD 的方法会触发对方的静态构造器 —— 此时 RimWorld 尚未加载 Defs，凡是在静态字段里解析 Def 的 MOD（如 RJW 的 `xxx` 类，约 110 个 Def）都会把查找结果缓存成 `null`，随后生成 Pawn 时抛 `NullReferenceException`，世界 / 地图生成与渲染一并崩溃。现改为在 Defs 加载完成后（`[StaticConstructorOnStartup]`）再应用这类补丁。
- 🐛 修复：自带贴图类型缺少 `[StaticConstructorOnStartup]` 导致的启动警告（贴图须在主线程加载）。
- Fix: mods that resolve defs in static field initializers could have those lookups poisoned (cached as `null`) because our cross-mod patches ran from the Mod constructor, before defs were loaded — that broke world/map generation and rendering when such a mod was loaded alongside us. Those patches are now applied from a `[StaticConstructorOnStartup]` class, i.e. after defs load.

---

## 📦 依赖 / Requirements

| 依赖 | 必需 | 说明 |
|------|------|------|
| [Harmony](https://github.com/pardeike/HarmonyRimWorld/releases/latest) | ✅ 必须 | 补丁框架 |
| [Prison Labor](https://steamcommunity.com/sharedfiles/filedetails/?id=1899474310) | ⭕ 可选 | v2.3 起内置囚犯劳工系统；安装后由 Prison Labor 接管工作分配 |

RimWorld **1.6** required.

---

## 🚀 安装 / Installation

**方式一：Steam 创意工坊（推荐）**
1. 在 [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3780939234) 订阅「囚犯付费吃饭2（Prisoners Pay To Eat 2）」
2. 游戏启动器 → MOD → 启用「囚犯付费吃饭2」

> ⚠️ 当前为**实验性（Exp）版本**，功能仍在完善中。

**方式二：直接放入 Mods 目录**
1. 将整个 `PrisonersPayToEat2` 文件夹复制到 `RimWorld/Mods/` 下（注意不是把里面的文件散开，是整个文件夹）
2. 启动游戏 → 游戏启动器 → MOD → 勾选启用「囚犯付费吃饭2」

**方式三：从 GitHub 拉取**
```bash
git clone https://github.com/ikun2522-art/PPTE2.git RimWorld/Mods/PrisonersPayToEat2
```

> ⚠️ 加载顺序：确保本 MOD 在 **Prison Labor** 之后加载（About.xml 已声明 loadAfter）。

---

## 🔨 从源码构建 / Building from Source

需要 .NET Framework 4.8（或支持其的 SDK）+ 本机 RimWorld 安装。

1. 复制 `Source/LocalPaths.props.example` 为 `Source/Directory.Build.props`（该文件已被 git 忽略）
2. 在 `Directory.Build.props` 中填写你自己的路径：
   ```xml
   <RimWorldDir>你的RimWorld安装目录</RimWorldDir>
   <HarmonyDir>包含 0Harmony.dll 的目录</HarmonyDir>
   ```
3. 编译：
   ```bash
   cd Source
   dotnet build PrisonersPayToEat2.csproj -c Release
   ```
4. DLL 与 PDB 自动复制到 `1.6/Assemblies/`

---

## 🗂️ 目录结构 / Structure

```
PrisonersPayToEat2/
├── About/About.xml            # MOD 元数据
├── Defs/                      # 手术配方 / 思维树 / 需求 / 标签页 / 区域等定义
├── Patches/                   # XML 补丁（劳作区画笔、动机工作速度）
├── Languages/                 # 中英双语翻译
├── Source/                    # C# 源码
└── 1.6/Assemblies/            # 编译产物 (dll + pdb)
```

---

## 📄 许可 / License

本项目基于 [MIT License](LICENSE) 发布。

---

*作者：AAA不玩抽象 · Author: AAA不玩抽象 — Made with ❤️ for the RimWorld community*
