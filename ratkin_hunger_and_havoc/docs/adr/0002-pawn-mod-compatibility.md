# ADR 0002: Pawn 身份与其它模组可改行为

- Status: Implemented
- Date: 2026-09-20
- Superseded in part by: [0003](0003-engineering-standards.md)（对外身份是 `IRHAH_Pawn`，不是 `CompRHAH_Pawn`）

## Context

旧模组用童年/成年经历判定“灾鼠”，再用二十多个空 Hediff 标角色。其它模组（牵着你的宠物、Toddlers、囚犯扩张、IrisMenus）只能 Harmony 私有方法或猜 Backstory。种族 `ThingDef` 上挂 ThingComp 会和 NewRatkinPlus / HAR 的种族补丁抢 Def。

需要：身份随 pawn 存档、不依赖经历、其它模组能查询和改行为而不打我们的内部补丁。

## Decision

1. 身份是 Hediff `RHAH_HungerMark` + `CompRHAH_Pawn`（HediffComp），不打种族 ThingDef 补丁。`Hediff_RHAH_Mark.ShouldRemove` 恒为 false。
2. 对外只保证 `HungerAndHavoc.Api.RHAH_Api` 与 `IRHAH_PawnBehavior`。查询、标记来源、释放到殖民地、闸门覆盖都走这里。
3. 行为不在 JobGiver 写死。询问顺序：单 pawn `Allow:`/`Deny:` 标签 → 后注册的 `IRHAH_PawnBehavior` → 本模组默认。闸门枚举第一期就列齐，未实现的 AI 也先占位。
4. `extraData`（`packageId:key` → string）给其它模组存私有状态，避免它们再挂一个容易被清掉的 Hediff。
5. `TryMarkOrigin` 允许其它模组生成的鼠族计入来源（例如牵引/交易带进的幼年鼠族）。

## Consequences

- 健康面板可以显示身份句，但清 Hediff 的模组理论上仍能拆标记；自定义 `ShouldRemove` 降低误伤，不能防恶意移除。API 对“没有标记”必须当成非来源。
- 其它模组 `loadAfter` 本包后注册策略即可。我们 `loadAfter` Lead Your Pet 以便可选检测，但不在内部写死对方类型名以外的反射契约。
- Harmony 补丁只用于原版/种族框架缺口；模组间协作不走私有方法名。

## 2026-10-27 原版角色生成边界

自有派系的 `basicMemberKind` 使用自有鼠族 PawnKind，派系与 PawnKind 都声明基础鼠族异种池。自有工厂的强制异种只影响自己的请求，不能代替原版派系展示或其它模组通过 `PawnGenerator` 发起的普通请求。

- 只在原版 `PawnGenerator.XenotypesAvailableFor` 修正自有鼠族 PawnKind 的有效池，与工厂共用设置权重、上游原生池和 HAR 公开许可；不改第三方派系、PawnKind 或事件代码
- 原版强制异种、自定义异种、基准人概率和允许名单保留既有选择顺序，最终结果仍需满足 HAR 许可；无合法候选时停止生成，不使用原版空池的隐式基准人回退
- 只在五个自有派系描述读取时同步展示池；有效池未变时保留原版描述缓存，变化时清除缓存。HAR 许可和用户权重可能在运行中变化，因此不把首次加载结果永久缓存
- 关闭生成优化时，自有 PawnKind 的声明池不作为原生池来源，仍读取上游 `RatkinColonist`；不把自有展示池回读成新的权重依据
- 出生请求使用 `Newborn`、`ForceNoIdeo` 和强制基准人作为遗传空模板；仅实际返回该模板时跳过随机生成许可校验，保留随后原版遗传身份赋值。空遗传列表在请求构造后可能为 null，不用列表是否存在识别出生。第三方改出的其它结果仍检查 HAR 许可

## 2026-10-31 种族白名单被整体替换

HAR 的 `RaceRestrictionSettings.CanUseXenotype` 对声明了 `onlyUseRaceRestrictedXenotypes` 的种族只放行 `whiteXenotypeList` 里的异种。Ratkin 种族就这样声明，而 Ratkin Gene Expanded 用 `PatchOperationReplace` 把白名单整体换成 `Ratkin_*` 亚种，`RK_XenoType_Ratkin` 与基准人都不在其中。

上一节“无合法候选时停止生成”把这种配置判定成没有合法异种并抛异常。异常从 `PawnGenerator.GetXenotypeForGeneratedPawn` 抛出，会连带打断用同一 PawnKind 生成的所有第三方流程：RJW 产程每次生婴儿都失败并移除产程，事件生成的鼠族也全部失败。

同一处还有第二个错误：上一节用 `Newborn` + `ForceNoIdeo` + 强制基准人识别出生模板，但 `ForceNoIdeo` 是原版出生请求的细节。RJW 的新生请求同样强制基准人、带 `forcedEndogenes`，却不设 `ForceNoIdeo`，于是被当成“没有合法异种”。

决定：

- 池解析按实际 HAR 许可回退：用户权重 → `RK_XenoType_Ratkin` → 上游 `RatkinColonist` 池 → 种族当前许可的全部异种（`DefDatabase<XenotypeDef>` 过滤）。种族白名单被替换后仍能生成，权重选择留给用户
- 出生模板以 `Newborn` + 强制基准人为准，不再要求 `ForceNoIdeo`；只有实际返回该模板时跳过校验，真实异种仍由原版或 RJW 在生成后设置
- 调用方显式选择的异种（强制、自定义、基准人概率、允许名单）不改选，交给调用方与 HAR 裁决；本模组不再用异常代替他人的生成流程
- 无显式选择却拿到基准人，说明原版空池回退了，改选池内异种；`MustBeCapableOfViolence` 仍过滤非战斗异种，改不出来只记录一次 `Log.ErrorOnce` 并保留原版结果

后果：`RK_XenoType_Ratkin` 缺席时饥荒鼠族取自鼠族扩展的亚种池，不再固定成基础鼠族；玩家可用设置权重把这些亚种纳入或排除。异常式“停止生成”不再是行为契约。

## 2026-11-15 自有角色模板的异种声明

`RatHole.RatkinGeneExpanded` 的 `Patch_XenotypeLock` 在生成结束后直接检查 `PawnKindDef.xenotypeSet.Contains`，忽略请求中的强制异种。自有角色模板的静态声明只有普通鼠族，与动态有效池不一致，导致已经选出的白鼠、实验鼠被改回普通鼠族，再被工厂校验销毁。

- 在原版 `PawnGenerator.GeneratePawn(PawnGenerationRequest)` 的 Prefix 中刷新自有角色模板的声明池，与现有有效池共用设置权重、上游池和 HAR 公开许可
- 合法的强制异种不在随机池时，以零权重登记成员资格，保留调用方的显式选择和既有随机权重；每次请求重新生成声明，避免上一次强制选择泄漏到后续请求
- 只修改本模组自有 PawnKind，不修改第三方代码或移除其补丁，不依赖它的私有方法及补丁执行顺序；关闭优化时仍读取上游原生池
- 生成后的种族、异种及基因归属校验继续执行；HAR 不允许的强制异种不加入声明，失败仍停止该请求

该决定补充 2026-10-27 的生成边界：自有角色模板的声明与查询结果同时反映有效池。第三方模组未安装时采用同样的数据一致性规则。

## 2026-11-15 成年鼷鼠的普通衣物

鼠窝通过原版 `ApparelProperties.PawnCanWear(Pawn, bool)` 让带小体型基因的成年鼷鼠穿童装。按实际年龄预先排除童装，会与这个公开的可穿结果冲突，使普通衣物池无可用候选。

普通衣物仍从固定名称、来源及设置白名单中选择，阶段准入统一交给公开可穿接口和身体部位检查。衣物件数仍按实际成长阶段计算，最终穿戴继续验证可穿性；不读取鼠窝私有类型或按其基因名猜测阶段。

## 2026-11-15 鼠窝多胎叠加

鼠窝的 `RK_Gene_ShortGestationMultiLitter` 会重复调用原版出生方法，饥与祸的“一胎多崽”在每次成功出生后追加幼崽。两者同时生效时，胎数相乘。

保留这一默认行为，提供“与鼠窝多胎效果叠加”设置。关闭后，仅当母亲同时有两种活跃基因时停用本方的追加幼崽；鼠窝自行决定胎数。其它母亲仍使用本方胎数设置，基因受抑制时不触发兼容分支。设置写入 Mod 配置，不撤销或修改第三方补丁。

## 2026-11-15 本地鼠窝 1.7 审读范围

本次检查安装包 `RatHole.RatkinGeneExpanded` 的 1.7 DLL 反编译所得全部 745 个 C# 文件，以及当前版本的 140 个非翻译 XML 文件。DLL 的 SHA-256 为 `b073a919e0bc15f3a81ff992a0933d62f341bdbd9ad4c65d109f964c5bd96386`；后续升级需重新核对，不能沿用本次结论。

鼠窝确有针对 RatkinAffiliativeBehavior、GentleRatkinHaven、旧婴儿缩放器、IntimacyNoAgeLimit 和 WarCrimeAllStages 的功能禁用或撤补丁行为，目标条件未命中饥与祸的 Harmony owner。通用事件开关按用户禁用列表执行，本机该列表为空。年龄保护的回滚区间位于 `GeneratePawn` 的 Postfix 之间，本方年龄在请求中给定，其余配置在方法返回后应用；Toddlers 正常幼儿阶段也不满足强制倒地条件。

鼠窝的低龄囚犯或奴隶转移、正式越狱范围爆炸、玩家主动牵引外派系角色导致敌对等规则仍会作用于符合条件的本方角色。兼容修复保留这些规则，不增加来源免疫。实际 DLL 探针验证异种锁与成年童装问题，完整 Mod 列表的游戏内事件验证仍由玩家执行。

## 2026-11-15 育儿袋放出后的照护恢复

鼠窝的育儿袋收纳幼鼠时移除原 Lord，放出时只恢复派系和访客身份。普通幼鼠访客因此可能失去照护队伍；原逻辑只重建离场组。

复用现有每两游戏小时的访客扫描，为仍活跃、未离场、无 Lord 的普通幼鼠恢复照护。同地图、批次、家庭组和派系的既有普通照护 Lord 优先；原组已消失时创建普通照护组并直接进入寻食阶段，保留个人进食状态与绝对期限，不重开交易或武装索粮。玩家成员、收留或雇佣角色、囚犯、奴隶、已有 Lord、成年角色和拦截批次不在此范围。

主动弃养使用已有母亲的 `droppedChildLoadIds` 判断，母亲已转入世界角色时仍保留该事实。照护恢复与原离场恢复共用这个排除，避免重新接管已被主动放下的孩子；不新增持久字段，不依赖鼠窝私有方法。
