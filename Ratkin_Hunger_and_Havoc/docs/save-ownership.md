# 存档与卸载归属

本页是存档键和卸载归属的唯一清单。改 `Scribe_*.Look`、可序列化类型名、`hediffClass` / `workerClass` / XML `Class=`，或新增会进 `.rws` 的 Def，必须同步本页。相关缺陷与迁移工作项由 [GitHub Issues](agents/issue-tracker.md) 跟踪。

本页登记当前存档契约，不是旧草案。不把本页当成下一次改键或迁命名空间的理由。本模组不读旧鼠灾档。

卸载动作只有 `Remove` 或 `Replace`。导出器见 [unload-export.md](unload-export.md)。没有登记的持久化产物视为漏了卸载保护。本页的动作行是导出器的契约，规划不能改写它。

## 破坏性重建（1.0.0 前）

M0 工程对齐对实验性存档格式做 **破坏性重建**，不读旧键、不兼容旧 XML 类型名、不提供迁移。含本模组身份标记的旧档加载后，旧类型节点与旧键视为未知或不完整。

旧契约（废弃，不读）：

- 存档键 `sourceIncidentId`
- `HungerAndHavoc.Hediff_HungerMark`
- `HungerAndHavoc.Api.CompHungerPawn`
- `HungerAndHavoc.Api.CompProperties_HungerPawn`

新契约类型名（与 XML `hediffClass` / Comp `Class` 同步）：

- `HungerAndHavoc.Identity.Hediff_RHAH_Mark`
- `HungerAndHavoc.Identity.CompRHAH_Pawn`
- `HungerAndHavoc.Identity.CompProperties_RHAH_Pawn`


穗音深存档类型从 `HungerAndHavoc.Narrative.Suiyin*` 迁到 `HungerAndHavoc.Storyteller.Suiyin.Suiyin*`。破坏性重建，不读旧类型名，不提供迁移。受影响的是 `SuiyinMember`、`SuiyinN004Case`、`SuiyinN005Case`、`SuiyinN006Case`、`SuiyinN007Case`、`SuiyinN008Case`、`SuiyinN009Case`、`SuiyinJournalCase`、`SuiyinNotice`。`NarrativeState` 仍是 `HungerAndHavoc.Narrative.NarrativeState`，存档键不变。

Scribe 默认值必须等于字段默认值。集合在 `PostLoadInit` 补空集合；null 与空集合加载后语义相同，都是空集合。`SetExtra(key, null)` 删除键。

## 已发布版本自动迁移

1.0.3（简称 1.3）到 1.0.4（简称 1.4）：`LordJob_RHAH_Visitor.ExposeData` 在 `LoadingVars` 读取字段前，仅预处理当前 Lord 的 XML。旧单人 `foodReceiver` 引用转为 `foodReceivers/li`；旧键为空、`null` 或 `IsNull="True"` 时转为空列表。新键已存在时以新键为准，包括显式空列表；不合并旧引用。转换幂等，之后由原版 Scribe 解析 Pawn 引用，保存只写新键。不修改磁盘原档、不改 `foodDef`、`foodCount` 或 Pawn 的 `foodWaitUntilTick`，不受当前存档停用新内容影响，不增加 tick 扫描。旧键只作加载迁移输入，不再作运行时字段；卸载随整个访客 Lord Remove。

1.1.0 安置观察改为在场累计存活超过三天，仅营养不良暂停。`SuiyinMember.careTicks` 保留既有累计值；新增 `lastCareTick` 默认 -1，旧档首次观察只建立时间基准，不从批次到达时间倒推有效存活时间。新记录以创建 tick 建立基准；未知去向时清除基准，返回后的首次观察不补算失联期间。`care` 保留旧 Free/Captive/Hungry/Plague 编号，追加 Malnutrition=4，首次观察改写当前状态，保存后使用当前值。已关闭的结果不重新结算。旧全局 `narrativeCareDays` 设置不再读取或写出，统一使用三天条件；IrisMenus 进度快照只在内存中缓存，不进入存档。

安置记录和批次保护由当前 `Game` 的组件写入各自 `.rws`，不共用外部记录文件。`spawnBatchId` / `BatchId` 是生成时游戏 tick 加避重得到的内部标识，不是累计来访次数。安置页以已有 `startedTick` 显示记录日，从成员身份中的 `sourceIncidentDisplayId` 读取实际事件；缺失或冲突时显示来源不详，不从多对一的日记类别反推事件。日期、事件树、选中项和折叠状态只在内存中存在，切换存档后清空；角色查询缓存同时按 `Game` 和 tick 隔离。

事件链切换到 Oxygen DSL Framework（ODF）：只读本模组旧 `eventChains`、`nextEventChainId` 和地图 `predationPendingTick`，不读旧鼠灾数据。所有引用解析完成后、ODF Activate 之前自动整批导入，保留实例 ID、`siteId`、`startedTick`、payload 和合法结局，不重抽随机结果。开放 stage 0 的下次检查是加载 tick + 1；受支持的开放 stage 1 长链在激活前转为 stage 3，旧 `deadlineTick` 只补入尚未设置的长链 `dueTick`，ODF deadline 置 -1，下次检查严格晚于加载 tick 且与 `id % 2500` 同相位。1.0.6 可通过设置生成无具体长后续的合法 stage 1；这些实例保留原阶段、期限并休眠，不使整个分区迁移失败。stage 2 保持 `NextTick=-1`，只保留原期限，不重跑已完成短阶段。时间计算溢出时不能回绕。`closed=true` 且 end=0 映射 Completed，其它 end 按 1 完成、2 失败、3 到期、4 取消映射。

迁移包含地图尚未到达的捕食者，保留旧 pending tick、猎物引用和已选结果；新地图键 `predationArrivalInstanceId` 引用 ODF 内部到达定义实例。安居点和事件后续共用该定义，新请求在选中结果后 120 tick 执行，每图只留一个待到达实例，重复请求按旧规则合并。地图的捕食者 AI 状态不转交 ODF。

地图 `uniqueID=0` 是合法首图 ID，迁移和到达 payload 接受非负地图 ID；负站点继续表示商队。失败保留使用原 Scribe 默认值语义，保存时可能省略默认 XML 字段，但不修正、丢弃或重编号原记录值。

整批导入和激活前长链迁移成功后才清空旧记录和 pending tick，后续保存不写旧键。迁移先验证整批长链 payload，再恢复身份并提交；已有 ODF 分区同样在 Activate 前迁移。若有非法或重复 ID、非法 stage/end、未知定义或错误 payload，整个导入失败，不丢弃记录、不重新抽取、不执行旧引擎；原 `eventChains`、`nextEventChainId` 和各地图 `predationPendingTick` 只在失败时原样存回。`eventChainMigrationStatus` 保存诊断；ODF provider 分区保持 Paused，在日志与 ODF 运行检查显示原因。失败分区不新增长链记录或改写阶段。

已经存在的 ODF provider 分区是权威格式，包括明确为空的分区；忽略旧输入，不再次导入。唯一失败保留例外：分区 `MigrationStatus` 以 `failed:` 开头时，继续保留原失败迁移输入，不将该诊断分区视为成功迁移。停用新内容不阻止读档迁移；迁移完成后 provider 设为 Disabled，保留所有实例和世界事实。

2026-10-11 全局事件设置按作者明确授权直接切换为 20 类。旧 `disabledIncidentDisplayIds`、`incidentDebugPoints`、`incidentWeights`、`incidentAttitudes`、`incidentLongChains` 只检测是否存在，不读取或迁移值。`GetSettings` 完成全部加载后，存在任一旧键即自动写回设置文件，移除旧键，只保存新类集合和仍有效的其它设置。新旧键共存时只读取新键；各集合的空值语义见下方字段表，`incidentGroupLongChains` 明确为空表示全短后续。`ObsoleteIncidentSettingsLoaded` 仅为本次启动的重写标记，不入档。此授权不扩展到 `.rws`：既有 Pawn 来源、队列、开放请求和 ODF 实例仍兼容，`I-019` 只退出新抽取与广播。
2026-10-11 独立检疫退休：`RHAH_QuarantineMigration` 在 `Game.ExposeSmallComponents` 的 `LoadingVars` 前置处理中，仅改加载中的 `game` XML。删除历史档案和信件队列中的 `HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Quarantine` / `RHAH_QuarantineLetter` 节点，以及对应 `Letter_ID` 引用；这些类型和 Def 只作迁移输入，不保留运行时占位。删除 `quarantineCases`（含旧 `SuiyinN007Case`）、待发 N007 通知（旧数字 30..38、58）与检疫转使者插话（Aside=57、arg=3）。节点数组保留第 7 槽并重置，其它节点和信件枚举编号不变；移除 `asidesSent` 中的 3，其它值原样保留。

旧 `GameComponent_RHAH_Game` 的 `plagueReturnLoadId`、`plagueReturnMapId`、`plagueReturnPhase`、`plagueReturnDueTick`、`plagueReturnLeaveTick` 在同一入口丢弃。地图旧 `plagueQuarantineLoadIds`、`plagueRecovered`、`plagueDied` 停止读取和保存，`plagueLastSpreadDay` 保留。`CompRHAH_Pawn.PostLoadInit` 移除 `extraData` 的 `rhah:quarantineHold`；无使者或长链重叠持有时，按两字符原覆盖恢复仍为 false 的 LeaveAfterFed、ExitMap，外部后来设置的 true 保留。有重叠持有时继续由现行持有者最后恢复。迁移不受停用新内容影响，幂等且不修改磁盘原档，存新只写现行格式。退休全局设置 `plagueReturnEnabled`、`plagueReturnDelayDays`、`plagueReturnStayDays`、`narrativeReturnDays`、`trustQuarantineStay`、`trustQuarantineRecover`、`trustQuarantineFail` 不再读取或保存；托孤共享的 `narrativeObserveDays` 保留。

2026-10-13 普通结局 E-01..E-05 退休：删除判定、触发、谢幕与开发预览，不增加替代终局。已发布叙事状态的旧 E 标记、成年计数与检查 tick 等字段保留原键、默认值和 Scribe 存读；无清空、改键或加载迁移。全局退休结局设置也保留原值，但退出菜单与行为消费，不再归一化。信任、普通通知、奖励、N 节点和身份来信消费仍走现行路径。

历史旧结局信由原版 `PositiveEvent` 保存翻译后文本，没有本模组持久唯一身份。因此不按标题、正文、语言或本模组前缀猜删，不改历史档案与信件队列；原版历史信保留。身份信与普通通知同样保留。此限制只关乎既有信件的精确识别，不恢复旧结局触发。

2026-10-14 母子关系补齐：I-003 耗子分妈、I-004 母亲乞讨的初次生成保留现有 `parentPawnLoadId` / `childPawnLoadIds`，同时使用原版 `Parent` 关系。`GameComponent_RHAH_Game.FinalizeInit` 在引用解析完成后，一次性遍历地图、世界、商队及临时角色；仅当双方已存 ID 相互确认、来源事件/批次/关系组一致且母亲为女性时补关系。已有另一位女性 Parent 时保留现状；缺失身份不猜补，不按姓名、年龄或同批来客推断。停用新内容不阻止补齐，重复加载不增加重复关系，下一次保存由原版 relations 写入；原 ID 字段和默认值不变，不增加 tick 扫描。





## 游戏存档键

`RHAH_Settings` 是全局 `ModSettings`，不写入 `.rws`，见下方非存档表。

### CompRHAH_Pawn

类型名：`HungerAndHavoc.Identity.CompRHAH_Pawn`。字段名不是存档键；本表键名与字段一致，camelCase。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| sourceIncidentDisplayId | sourceIncidentDisplayId | null | 否 | 取代旧键 `sourceIncidentId`；破坏性重建，不读旧键 |
| spawnBatchId | spawnBatchId | 0 | 否 | |
| relationshipGroupId | relationshipGroupId | 0 | 否 | |
| role | role | Unspecified | 否 | `RHAH_PawnRole` |
| lifecycle | lifecycle | Arriving | 否 | `RHAH_Lifecycle` |
| hasBeenFed | hasBeenFed | false | 否 | |
| leaveAfterGameTick | leaveAfterGameTick | -1 | 否 | |
| stayKind | stayKind | 0 | 否 | 0 无，1 收留，2 雇佣，3 临时征募。到期、Released 或 Dead 清为 0；Released 终态优先于旧期限 |
| stayRemainingTicks | stayRemainingTicks | 0 | 否 | 医疗倒地时冻结的剩余 tick，暂停期间滚动原离场期限；年龄不能行走不暂停。到期或终态清为 0 |
| foodWaitUntilTick | foodWaitUntilTick | -1 | 否 | 断粮或交接等待截止。未收齐交接者按本人截止离场；本人收齐清为 -1，真实进食才写 Fed |
| carriesPlague | carriesPlague | false | 否 | |
| attitudeAtArrival | attitudeAtArrival | Neutral | 否 | `RHAH_Attitude` |
| attitude | attitude | Neutral | 否 | 当前 `RHAH_Attitude`，forceSave 保留明确 Neutral 停战。仅旧档缺键时回退到 `attitudeAtArrival`；键、类型和默认值不变 |
| parentPawnLoadId | parentPawnLoadId | 0 | 否 | 明确的原父母身份；I-003/I-004 与母亲 childPawnLoadIds 双向确认后补齐原版 Parent |
| childPawnLoadIds | childPawnLoadIds | 空集合 | 是 | `PostLoadInit` 补 `List<int>`；null 与空集合语义相同。I-003/I-004 生成时同时建立原版 Parent，不重复登记孩子 |
| gateOverrides | gateOverrides | 空集合 | 是 | `PostLoadInit` 补字典；null 与空集合语义相同 |
| extraData | extraData | 空集合 | 是 | `PostLoadInit` 补字典；null 与空集合语义相同；`SetExtra(key, null)` 删除键 |
| droppedChildLoadIds | droppedChildLoadIds | 空集合 | 是 | 已放下的孩子 Load ID。`PostLoadInit` 补空列表 |
| begSlapCount | begSlapCount | 0 | 否 | 实际抽中的巴掌次数。旧档缺键按 0。成功获赠或吃完真实食物后回到 0 |
| shortStayLockedApparel | shortStayLockedApparel | 空集合 | 是 | 本模组在这次短工开始时锁定的衣物引用。`PostLoadInit` 补空列表；null 与空集合语义相同。只在短工结束或永久身份转换时解开这些引用，不调用 `UnlockAll`。归属 Remove |
| gnawSearchUntilTick | gnawSearchUntilTick | -1 | 否 | 啃食搜索失败后的冷却截止 tick。-1 表示没有冷却。旧档缺键按 -1 |

计算属性不入档：`IsReleased`、`IsActiveVisitor`、`RoleLabelKey`。
叙事临时留驻保存于 `extraData`：`rhah:envoyHold`、`rhah:longHold`，缺键表示不持有。值为两字符，依次记录 `LeaveAfterFed` 与 `ExitMap` 原覆盖：`u` 无覆盖、`t` true、`f` false。重叠持有复制首持有者原值，删除自身键后仅最后一个持有者恢复仍为 false 的覆盖；不覆写外部后来设置的 true。卸载随 `CompRHAH_Pawn` Remove。旧使者持有迁移见 `SuiyinN008Case.holdMigrated`；退休 `rhah:quarantineHold` 的读档解除见上方自动迁移。


### LordJob_RHAH_Visitor

类型名：`HungerAndHavoc.Pawn.LordJob_RHAH_Visitor`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| faction | faction | null | 否 | 访客 Lord 使用的态度派系引用 |
| waitSpot | waitSpot | IntVec3.Invalid | 否 | 寻食集合点 |
| familyRole | familyRole | Unspecified | 否 | `RHAH_PawnRole` |
| foodReceivers | foodReceivers | 空集合 | 是 | 等待玩家亲手交食物的来客引用列表。PostLoadInit 补空列表；1.0.4 在字段读取前自动迁移 1.0.3 的 foodReceiver，已有新键时优先保留新键，每人份数由 foodCount 保存 |
| foodDef | foodDef | null | 否 | 第一次交货后锁定的食物 |
| foodCount | foodCount | 0 | 否 | 需要的份数。0 表示没有在等 |
| passerbyExitCell | passerbyExitCell | IntVec3.Invalid | 否 | I-031/I-039 对边可达出口。旧 Lord 首次按原来源识别后选择，保存重读保持；卸载随 Lord Remove |
| armedChoiceId | armedChoiceId | 0 | 否 | 对应本批 I-014/I-030/I-045 的既有 RHAH_ChoiceRecord；0 无索粮政策。旧 Lord 缺键继续原行为，不自动启动倒计时 |
| armedAssault | armedAssault | false | 否 | 本请求真实拒绝/逾期后进入索粮进攻；足额交付或关闭索粮设置清除并中立离场。旧图索引 0..3 保持，进攻 toil 追加为 4；卸载随 Lord Remove |


### ChoiceLetter_RHAH_Request

类型名：`HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Request`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| choiceId | choiceId | 0 | 否 | 对应 `RHAH_ChoiceRecord.id` |
| mapId | mapId | 0 | 否 | |
| kind | kind | None | 否 | `RHAH_RequestKind` |
| site | site | None | 否 | `RHAH_IntelSiteKind` |
| amount | amount | 0 | 否 | |
| expireTick | expireTick | -1 | 否 | |

### ChoiceLetter_RHAH_Visitors

类型名：`HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Visitors`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| choiceId | choiceId | 0 | 否 | 对应 `RHAH_ChoiceRecord.id` |
| choice | choice | Visitors | 否 | `RHAH_ChoiceKind` |

### ChoiceLetter_RHAH_GrainHole

类型名：`HungerAndHavoc.Incidents.ChoiceLetter_RHAH_GrainHole`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| mapId | mapId | 0 | 否 | 洞所在地图 |
| followUp | followUp | false | 否 | false 是入口四选，true 是诱饵到期后的追踪或停止 |

### ChoiceLetter_RHAH_Envoy

类型名：`HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Envoy`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| mapId | mapId | 0 | 否 | 使者所在地图 |
| pawnId | pawnId | 0 | 否 | 使者 thingID |

### ChoiceLetter_RHAH_Revisit

类型名：`HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Revisit`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| caseId | caseId | 0 | 否 | 对应 `SuiyinN004Case.id` |
| pawnId | pawnId | 0 | 否 | 发信时固定的真实相遇者，点击不顺延替换母亲或孩子 |
| meetingTile | meetingTile | PlanetTile.Invalid | 否 | 原相遇 Tile，含 layer；旧信无法可靠还原，显示失效并允许关闭，不扣费或扣信任 |
| meetingMapId | meetingMapId | 0 | 否 | 原目标 MapHeld 的 uniqueID，0 表示不在地图 |
| targetCaravanId | targetCaravanId | 0 | 否 | 原目标持有商队 ID，0 表示无商队 |

### RHAH_ChoiceRecord

类型名：`HungerAndHavoc.Incidents.RHAH_ChoiceRecord`。嵌在 `openChoices` 里。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| Id | id | 0 | 否 | |
| DisplayId | displayId | 空字符串 | 否 | `PostLoadInit` 把 null 补成空字符串 |
| MapId | mapId | 0 | 否 | |
| BatchId | batchId | 0 | 否 | |
| Kind | kind | None | 否 | `RHAH_RequestKind` |
| Site | site | None | 否 | `RHAH_IntelSiteKind` |
| Choice | choice | None | 否 | `RHAH_ChoiceKind` |
| Amount | amount | 0 | 否 | |
| ExpireTick | expireTick | -1 | 否 | |
| Settled | settled | None | 否 | `RHAH_ChoiceAction`。旧文本 `Prison` 和旧数值 `12` 读取为 `Capture`（关闭状态）；再存档写 `Capture`。其它名称和编号保持原有枚举存读，缺键仍为 `None` |
| PawnLoadIds | pawnLoadIds | 空集合 | 是 | `PostLoadInit` 补 `List<int>`；null 与空集合语义相同 |
| AllyFactionId | allyFactionId | 0 | 否 | 转送目标派系 loadID，0 表示没有记录目标。转送完成后仍保留，作为回礼来源；待入籍成员以 AllyPawnIds 判断。旧档缺键为 0 |
| AllyPawnIds | allyPawnIds | 空集合 | 是 | 已下令离场、离图后才改入盟友的 pawn thingID。`PostLoadInit` 补空列表；null 与空集合语义相同。人离图或死亡后移除 |
| AllyTransferredValue | allyTransferredValue | 0 | 否 | 本批成功离场来客的累计市场价值；仅成功转送时累加，旧档缺键为 0 |
| AllyGoodwillGranted | allyGoodwillGranted | 0 | 否 | 本批已发放的转送好感；每次按累计目标 `min(10, ceil(AllyTransferredValue / 500))` 减去此值补发，非正价值目标为 0。旧档缺键为 0 |
| AllyGiftChecked | allyGiftChecked | false | 否 | 首名来客成功离场时置 true，只判定一次工业及以上盟友的 50% 回礼；旧档缺键为 false |
| AllyGiftDueTick | allyGiftDueTick | -1 | 否 | 回礼投递到期 tick；-1 表示无待投递回礼，旧档缺键为 -1。到期投递或无家园取消后恢复 -1，不追加重试 |
| TransactionVersion | transactionVersion | 0 | 否 | 0 是已存在情报请求；1 是新食物换实物报价，旧信不重新报价、不改回报 |
| QuotedFoodDefName | quotedFoodDefName | 空字符串 | 否 | 新交易锁定一种库存食物，null 加载后补空 |
| QuotedFoodCount | quotedFoodCount | 0 | 否 | 锁定报价份数，选库存总数量最多的同种有效食物，按实际到达人数每人 2.7 营养换算并向上取整，最多 100 份；旧信保留原 Amount |
| RewardDefNames | rewardDefNames | 空集合 | 是 | 携带奖励 defName，与 RewardCounts 同索引；RewardThingIds 保存这些奖励的实际堆身份；PostLoadInit 补空，null 与空集合相同 |
| RewardCounts | rewardCounts | 空集合 | 是 | 携带奖励数量，成交前核对真实物品 |
| RewardThingIds | rewardThingIds | 空集合 | 是 | 实际奖励 Thing ID，避免读档或重复点击生成第二份 |
| OutgoingChildThingId | outgoingChildThingId | 0 | 否 | 实际交给来客的玩家孩子 thingID；0 未交付 |
| ResponsibleAdultThingId | responsibleAdultThingId | 0 | 否 | 实际出面的玩家成年人 thingID；0 无记录 |
| ChildExchangeVersion | childExchangeVersion | 0 | 否 | 0 是既有婴儿索取信，1 是新换子交接；保留原索取数量与期限 |
| ChildFoodAlternative | childFoodAlternative | false | 否 | true 表示确实用食物替代，不能启动“换出去的孩子”长链 |

`settled` 在同键内迁移，不增加持久化字段或枚举别名；`Prison = 12` 已删除，`Capture = 10`、`Ally = 13` 与其它显式编号不变。除旧 `Prison` / `12` 外仍交由原版枚举 Scribe 读取，未知数值保留，非法文本沿用原版失败处理（记录错误并回退 `None`）。本次不恢复旧送监狱操作已脱图的角色，卸载动作不变。

盟友转送的累计价值、已发放好感、回礼判定与待投递时间均归本批 `RHAH_ChoiceRecord` 所有，随 `openChoices` 删除，不建立独立回礼队列。目标选择分页是临时对话节点，没有新增分页存档字段。

### WorldObject_RHAH_RefugeeCamp

类型名：`HungerAndHavoc.Incidents.WorldObject_RHAH_RefugeeCamp`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| residents | residents | 空集合 | 是 | 居民引用。`PostLoadInit` 补空列表；null 与空集合语义相同 |
| cleared | cleared | false | 否 | |
| nextCheck | nextCheck | -1 | 否 | 下次检查 tick |

### WorldObject_RHAH_Approach

类型名：`HungerAndHavoc.Incidents.WorldObject_RHAH_Approach`。没有 Pawn。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| displayId | displayId | 空字符串 | 否 | 到达后生成的显示 ID。`PostLoadInit` 把 null 补成空字符串 |
| points | points | 0 | 否 | 到达后交给 Worker 的点数 |
| mapId | mapId | 0 | 否 | 目标玩家家园 `uniqueID`。地图已销毁时物体销毁且不生成 |
| nextTileId | nextTileId | -1 | 否 | 正在走入的下一格。-1 表示这一格已经走完 |
| costLeft | costLeft | 0 | 否 | 进入下一格或到达后等待重试还要的 tick；暂时失败等待一游戏小时，读档不重置 |
| crossing | crossing | false | 否 | 岛上出发时为 true，允许走进海洋格。旧档缺键时按陆地走 |
| arrivalPending | arrivalPending | false | 否 | 已进入到达事务，等待时不再寻路；旧档缺键继续原行走，到达后设为 true |

### WorldObject_RHAH_Interception

类型名：`HungerAndHavoc.Incidents.WorldObject_RHAH_Interception`。临时拦截地图的父物体，继承 `MapParent`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| displayId | displayId | 空字符串 | 否 | 被拦截事件的显示 ID。`PostLoadInit` 把 null 补成空字符串。父物体负责保存 |
| spawnBatchId | spawnBatchId | 0 | 否 | 拦截批次。0 表示没有批次。父物体负责保存 |
| settled | settled | false | 否 | 这批拦截是否已经结算。父物体负责保存 |

### LordJob_RHAH_Intercept

类型名：`HungerAndHavoc.Pawn.LordJob_RHAH_Intercept`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| exitCell | exitCell | IntVec3.Invalid | 否 | 离图目标格，按原版 `IntVec3` 写入键 `exitCell`。默认 `(-1, -1, -1)` |

穿图与离场 duty 按每名 pawn 的当前位置重建，单 toil 索引保持 0，无新增存档字段或 toilData。



## 可序列化类型

| 类型 | 出现位置 | 卸载 |
| --- | --- | --- |
| HungerAndHavoc.Identity.Hediff_RHAH_Mark | Hediff `Class` / `hediffClass` | Remove，随身份 Hediff 删除 |
| HungerAndHavoc.Identity.CompRHAH_Pawn | HediffComp `Class` | Remove，随身份 Hediff 删除 |
| HungerAndHavoc.Identity.CompProperties_RHAH_Pawn | Def XML `Class` | 不单独出现在 `.rws` |
| HungerAndHavoc.Pawn.LordJob_RHAH_Visitor | Lord `lordJob` | Remove。卸载后该 Lord 必须消失，pawn 回原版 ThinkTree |
| HungerAndHavoc.Pawn.LordJob_RHAH_Intercept | Lord `lordJob` | Remove。卸载后该 Lord 必须消失，pawn 回原版 ThinkTree。`exitCell` 随 Lord 删除，不改地图上的玩家 |
| HungerAndHavoc.Incidents.WorldObject_RHAH_Interception | WorldObjectDef `worldObjectClass` | 已有地图或居民：Replace，类名改成 `RimWorld.Planet.Site`。没有地图也没有居民：Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_Beg | Job `driverClass` | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_Gnaw | Job `driverClass` | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_DropChild | Job `driverClass` | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_MotherFeed | Job `driverClass` | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_Scavenge | Job `driverClass` | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_TailBite | Job `driverClass` | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_Expel | Job `driverClass` | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_BroadcastHope | Job `driverClass`；无新增存档字段 | Remove |
| HungerAndHavoc.Pawn.JobDriver_RHAH_CarryYoung | Job `driverClass`；无新增存档字段 | Remove，连同当前 Job 或排队 Job 清理 |
| HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Request | Letter `letterClass` | Remove |
| HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Visitors | Letter `letterClass` | Remove |
| HungerAndHavoc.Incidents.ChoiceLetter_RHAH_GrainHole | Letter `letterClass` | Remove |
| HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Envoy | Letter `letterClass` | Remove |
| HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Revisit | Letter `letterClass` | Remove |
| HungerAndHavoc.Storyteller.Suiyin.ChoiceLetter_RHAH_KingdomArc | Letter `letterClass`，键 `kingdomInstanceId` 默认 0，绑定发信时 ODF 实例 | Remove，随选择信删除 |
| HungerAndHavoc.Storyteller.Suiyin.KingdomArcState | `NarrativeState.kingdomArc` 深存档 | Remove，随叙事组件删除 |
| HungerAndHavoc.Storyteller.Suiyin.KingdomArcProgress, HungerAndHavoc.Storyteller.Suiyin.KingdomMaterialRecord | progress/materials 深存档 | Remove，随叙事组件删除 |
| HungerAndHavoc.Storyteller.Suiyin.WorldObject_RHAH_KingdomSite | 世界物体 | Remove，人员与原版粮食背包保留 |
| HungerAndHavoc.Storyteller.Suiyin.CaravanArrivalAction_RHAH_KingdomSite | 商队到达动作 | Remove |
| HungerAndHavoc.Storyteller.Suiyin.Comp_RHAH_KingdomMaterial | 物品 Comp | Remove，随文书物品删除 |
| HungerAndHavoc.Pawn.ThinkNode_ConditionalRHAH_Visitor | ThinkTree XML `Class` | 不单独出现在 `.rws` |
| HungerAndHavoc.Pawn.JobGiver_RHAH_* | Duty / ThinkTree XML `Class` | 不单独出现在 `.rws` |
| HungerAndHavoc.Pawn.ThoughtWorker_RHAH_ClaySatiety, HungerAndHavoc.Pawn.ThoughtWorker_RHAH_FamineHunger | ThoughtDef `workerClass`，situational 无独立存档字段 | 不单独出现在 `.rws`；思想 Def 引用 Remove |
| HungerAndHavoc.Pawn.Thought_Memory_RHAH_FamineFed | ThoughtDef `thoughtClass`，memories 内沿用原版记忆字段 | Remove，随该记忆删除 |
| HungerAndHavoc.Pawn.Thought_Memory_RHAH_EnslavedServing | ThoughtDef `thoughtClass`，沿用原版记忆字段；无新增存档键，`ShouldDiscard=false` 保证读档后仍不过期 | Remove，随该记忆删除 |
| HungerAndHavoc.Trade.StockGenerator_RHAH_Food | TraderKindDef stockGenerators XML `Class` | 不单独出现在 `.rws` |
| HungerAndHavoc.Pawn.Area_RHAH_Relief | AreaManager `areas` | Remove。卸载后区域节点消失，格子不迁到家区 |
| HungerAndHavoc.Pawn.Hediff_RHAH_ClaySatiety | Hediff `Class` / `hediffClass` | Remove，随饱腹 Hediff 删除 |
| HungerAndHavoc.Storyteller.Suiyin.Building_RHAH_RecordBox | Thing `Class` / `thingClass` | Remove。随旧箱子删除，不替换成原版建筑 |
| HungerAndHavoc.Pawn.Comp_RHAH_Clay | ThingComp `Class` | Remove，随观音土物品删除 |
| HungerAndHavoc.Pawn.CompProperties_RHAH_Clay | Def XML `Class` | 不单独出现在 `.rws` |
| HungerAndHavoc.Generation.RHAH_GenerationExtension | ThingDef `modExtensions` XML `Class` | 不单独出现在 `.rws`。保留旧 XML 字段 `allowRefugeeApparel` 以便解析既有外部 Def，但不再参与默认准入；没有 `ratkinApparel` 字段或快照存储 |
| HungerAndHavoc.Pawn.StatPart_RHAH_TemperatureApparel | StatDef `parts` XML `Class` | 不单独出现在 `.rws` |
| HungerAndHavoc.Incidents.RHAH_PredatorRecord | 捕食者深存档，嵌在 `predators` | Remove。随地图组件删除，不替换成原版动物 |
| HungerAndHavoc.EventMgr.RHAH_EventChainRecord | 旧 `eventChains` 的只读迁移记录；只有失败迁移再次保存 | Remove。随游戏组件删除，不执行或替换成原版事件 |
| HungerAndHavoc.EventMgr.RHAH_LongFollowRecord | `longFollows` 深存档 | Remove，随游戏组件删除；真实 Pawn 和关系保留 |
| HungerAndHavoc.EventMgr.ChoiceLetter_RHAH_LongFollow | Letter `letterClass`，键 `chainId` 默认 0、`phase` 默认 0 | Remove，随选择信删除 |
| HungerAndHavoc.EventMgr.JobDriver_RHAH_LongMeeting | Job `driverClass`，沿用原版 Job 的孩子、来客与出口目标 | Remove，清理当前和排队 Job，保留被携带孩子 |
| HungerAndHavoc.EventMgr.JobDriver_RHAH_LongMeetWalk | Job `driverClass`，沿用原版 Job 的来客目标 | Remove，清理当前和排队 Job |

### RHAH_PredatorRecord

类型名：`HungerAndHavoc.Incidents.RHAH_PredatorRecord`。嵌在 `predators` 里。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| pawn | pawn | null | 否 | 活跃捕食者引用 |
| outside | outside | false | 否 | 本地捕食者为 false，外边生成的为 true |
| nextSearchTick | nextSearchTick | -1 | 否 | 下次允许搜索的 tick。空闲地图不因这个值扫描 |

### RHAH_EventChainRecord

类型名：`HungerAndHavoc.EventMgr.RHAH_EventChainRecord`。仅作旧 `eventChains` 迁移输入；导入失败时原样保存，成功后不再保存。该类型没有可运行的事件链职责。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| id | id | 0 | 否 | 原实例 ID，必须为正且整批唯一。迁移保留；非法或重复时整批失败，不丢弃或重新编号 |
| displayId | displayId | 空字符串 | 否 | 原定义 ID，必须是注册的 `I-001`..`I-051`；未知或空 ID 使整批迁移失败 |
| siteId | siteId | 0 | 否 | 地图为正数 uniqueID，商队为负数 -ID，0 为世界格 |
| stage | stage | 0 | 否 | 0 短阶段、1 长阶段、2 短阶段已做完。迁移保留；其它值整批失败 |
| startedTick | startedTick | 0 | 否 | 原开始 tick，迁移不重置 |
| deadlineTick | deadlineTick | -1 | 否 | -1 表示没有期限。迁移保留原期限，不归一化非法值后继续 |
| payload | payload | 空字符串 | 否 | 原出现物文本；Prepare 校验并解析一次。错误载荷使整批失败，不重新生成 |
| closed | closed | false | 否 | |
| end | end | 0 | 否 | 0 未结束，1 完成，2 失败，3 到期，4 取消 |

Letter 与 Quest 的类型见上表。WorldObject `RHAH_RefugeeCamp`、`RHAH_Approach` 与 `RHAH_Interception` 已登记。GameComponent 与 MapComponent 的键在下一节。

### GameComponent_RHAH_FamineYear

类型名：`HungerAndHavoc.Core.GameComponent_RHAH_FamineYear`。所有字段随组件 Remove；旧档缺组件由 Verse 建立默认状态，不读取旧鼠灾年度键。鼠潮次数按年重置，两个冷却 tick 不随跨年重置；成功接受入队或即时触发时消费限额，赶路失败不返还。

| 字段 / 存档键 | 默认值 | 说明 |
| --- | --- | --- |
| calendarMapId | -1 | 年度日历采用的家园 ID |
| calendarLongitude | 0 | 对应当地日历经度 |
| judgedYear | -1 | 已判定年份；首次年中开启允许补判 |
| famineYear | false | 已判定荒年，仅在开关开启且年份仍匹配时生效 |
| disasterSucceeded | false | 本年已成功触发一次额外天灾 |
| lastDisasterAttemptDay | -1 | 去重每日 06:00 天灾尝试 |
| swarmCount | 0 | 本年度已接受鼠潮次数，最多两次 |
| lastSwarmTick | -1 | 上次鼠潮接受 tick，15 天冷却 |
| lastPleadingTick | -1 | 上次祈求加入接受 tick，180 天冷却 |

鼠潮反抗复用已有 `RHAH_PawnState` 的态度、生命周期、等待/离场 tick 和保存的行为闸门覆盖，不新增第二套敌对状态。卸载时身份标记与闸门随本模组清除，原版 Pawn 保留。

### Generation runtime

| 类型 | 字段 | 存档键 | 卸载 |
| --- | --- | --- | --- |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | active generation batches | activeGenerationBatches | Remove |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | pending incident display IDs | pendingIncidentDisplayIds | Remove |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | pending incident points | pendingIncidentPoints | Remove。与显示 ID 等长。旧档缺列表时按当前调试点补齐 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | pending incident target IDs | pendingIncidentTargetIds | Remove。与显示 ID 等长。地图为正数 uniqueID，明确首图 0 编码为 int.MinValue；商队为负数 -ID（不包含 int.MinValue），旧 0 为未指定。旧档缺列表仍按 0 补齐，不重释为首图。null 与空集合都是空队列 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | open choices | openChoices | Remove |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | next choice id | nextChoiceId | Remove |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | broadcast cooldown tick | broadcastCooldownUntilTick | Remove |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | generation cursor | generationCursor | Remove |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | beg cooldown pawn ids | begCooldownPawnIds | Remove。与冷却 tick 等长。旧档缺列表时为空。null 与空集合相同 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | beg cooldown ticks | begCooldownTicks | Remove。与 pawn id 等长。缺一边时按另一边截齐，短的一边补 -1 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | begged pawn ids | beggedPawnIds | Remove。与殖民者 id 等长。旧档缺列表时为空 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | begged colonist ids | beggedColonistIds | Remove。与乞讨者 id 等长。不等长时按短的一边截齐 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | interactive long followups | longFollows | Remove。默认空列表，null 与空集合加载后相同；保存人物、分支、期限、实物和当前选择，不保存第二套调度器 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | exclusive royal claim chain | longClaimChainId | Remove。默认 0 尚未认领；正数记录唯一 I-041 长链，完成后仍保留以阻止再次触发 |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | visitor pawn load IDs | visitorPawnLoadIds | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | food search ticks | foodSearchTicks | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | plague last spread day | plagueLastSpreadDay | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | wall gnaw counts | wallGnawCounts | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | camp predation prey | predationPrey | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | camp predators | predators | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | camp predation rolled | predationRolled | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | camp predation selected | predationSelected | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | legacy camp predation pending tick | predationPendingTick | Remove。默认 -1。只作加载迁移输入；失败时原样保存，成功或已有权威 ODF 分区时清空且不再保存 |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | predation arrival instance id | predationArrivalInstanceId | Remove。默认 0 表示无待到达实例；正数引用本 provider 的 ODF 到达实例，每图至多一个 |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | camp predation next tick | predationNextTick | Remove |
| HungerAndHavoc.Core.MapComponent_RHAH_Map | grain hole thing id | holeThingId | Remove |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | legacy event chains | eventChains | Remove。旧档缺列表时为空，null 与空集合都是无旧链。只作加载迁移输入；失败时原样保存，成功后清空且不再保存 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | legacy next event chain id | nextEventChainId | Remove。默认 1。迁移验证与已有 ID 的关系并保留有效分配游标，不重用 ID；只在失败时保存原值 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | event chain migration status | eventChainMigrationStatus | Remove。默认空字符串；持久化迁移结果或失败原因。`failed:...` 保留旧输入且不运行旧引擎 |
| HungerAndHavoc.Core.GameComponent_RHAH_Game | new content disabled | newContentDisabled | Remove。仅此存档停用新内容。默认 false。清理时整个组件删除，不迁移到副本。null 与缺键都是 false |

生成队列和批次保护仍由 `GameComponent_RHAH_Game` 负责；本图访客索引、寻食缓存、猎物引用和捕食者 AI 属于地图组件。事件链只由 ODF 游戏组件的一个运行时执行；RHAH 没有第二个事件链 tick 或扫描器。地图拆除时由 MapComponent 随地图卸载，不能保留 Pawn 或 Map 引用。

### ODF provider 分区

ODF 是必需依赖，事件链存档由 `Oxygendsl.Mod.GameComponent_Oxygendsl` 所有。RHAH 只拥有 `providerId` 为 `nanaloveyuki.ratkin.hungerandhavoc` 的分区，包括本分区实例、到达实例、receipts、模式和迁移诊断；不拥有 ODF 类型或其它 provider 数据。分区内 ID 只在本 provider 内分配，不能与 `predationArrivalInstanceId` 之外的地图自有事实混为一项。

| 所有者 | 存档位置 | 卸载动作 |
| --- | --- | --- |
| ODF 根组件 | `GameComponent_Oxygendsl` 及 `eventChainSchemaVersion` | 保留。ODF 仍安装，根 schema 不归 RHAH 所有 |
| RHAH provider | `eventChainProviders/li` 中 `providerId=nanaloveyuki.ratkin.hungerandhavoc` 的整项 | Remove。仅在序列化副本调用 `Oxygendsl.Mod.Events.OxygenEventChainHost.RemoveProviderState(document, providerId)` |
| 其它 provider | `eventChainProviders/li` 中其它 `providerId` 的整项 | 保留，不删实例、receipts、暂停原因或空分区 |
| ODF 模组元数据 | save meta 中 `nanaloveyuki.oxygendsl` | 保留。导出只移除 RHAH 的 meta，不把依赖一起卸载 |

`newContentDisabled` 把本 provider 设为 Disabled，不删除实例或世界物体。导出先备份原当前状态，随后禁用本 provider；`RemoveProviderState` 只处理序列化副本，不清理正在运行的 ODF runtime。失败迁移的旧记录仍随 RHAH 游戏/地图组件 Remove。完整玩家流程见 [unload-export.md](unload-export.md)。

### 王国篇阅读与邀请

`NarrativeState` 新增深存档键 `kingdomArc`，默认新建 `KingdomArcState`；旧档缺键或显式 null 在 `PostLoadInit` 补齐，表示尚未开启。不读取或累计普通事件、既有穗音节点或信任来解锁。

| 字段 | 存档键 | 默认值 | 说明 |
| --- | --- | --- | --- |
| Segment | segment | 0 | 当前阅读段索引；加载后夹到现有目录范围。回顾使用窗口独立游标，不改此值 |
| Act | act | 1 | 只表示当前幕，接受后启动第二幕抄件寄送；第五幕为初步合作回信，不是终局 |
| InstanceId | instanceId | 0 | 当前邀请的 RHAH provider ODF 实例；拒绝后主动重联分配新实例，不复用旧编号 |
| Invitation | invitation | None | None=0、Pending=1、Accepted=2、Declined=3、Disabled=4。暂缓/关闭保持 Pending；仅此存档停用把待答复邀请改为 Disabled |

ODF 定义 `RHAH_KingdomInvitation` 版本 1，stage=0、payload 为空、无期限。首次到期执行一次 `notify_invitation` 后 `NextTick=-1` 休眠；接受结束为 Completed，拒绝结束为 Cancelled。通知和答复均核对当前实例编号；档案信只读，旧实例信不能操作新邀请。卸载删除整个 `kingdomArc` 所在叙事组件、王国篇信件与本 provider 分区，保留 ODF 及其它 provider。

创建邀请、发送通知与答复仅限当前叙事者 `RHAH_Suiyin`。换走穗音不删除邀请、阅读段或已收到信件；已有信件可阅读，不能答复。换回穗音恢复主动入口，不新建或重置待答复实例。待通知邀请在穗音缺席时按 ODF 一小时间隔等待，回归后通知；不自动创建邀请

自动/手动、四倍速、阅读暂停、逐字位置、读完后停留计时、回顾游标、纹理及翻译缓存仅属于窗口，不存档；恢复阅读使用已保存段索引并从该段重新逐字显示。全屏窗口通过原版 `forcePause` 暂停游戏，不修改或持久化游戏速度。第二至第五幕游标保存于 progress/cursor，回顾游标不覆盖当前游标。材料选择弹窗只保存内存引用，暂停游戏，取消不产生副作用，不存档。



### 王国篇 K2～K5 初步合作

`KingdomArcState.Progress` 深存档键 `progress`，默认新建 `KingdomArcProgress`。以下字段随 `NarrativeState` Remove。集合 PostLoadInit 补空。ODF 定义 `RHAH_KingdomCollaboration` 版本 1，stage 0，空 payload；只由接受手动邀请创建，休眠选择由明确操作唤醒。接受的寄件、期限与世界办理不因切换叙事者停止；新委托和私信受穗音闸门约束，`pendingLetter` 保留尚未显示的结果。

| 存档键 | 默认值 | 说明 |
| --- | --- | --- |
| stage | KingdomStage.None | Stage |
| chainId | 0 | ChainId |
| revision | 0 | Revision |
| cursor | 0 | Cursor |
| due | -1 | Due |
| deadline | -1 | Deadline |
| homeId | -1 | HomeId |
| siteId | 0 | SiteId |
| safeSiteId | 0 | SafeSiteId |
| consentSafeSiteId | 0 | 书记明确同意的安全目的地，不与发布第二机构复用 |
| actorId | 0 | ActorId |
| progress | 0 | Progress |
| lastWorkTick | -1 | LastWorkTick |
| charityStart | -1 | CharityStart |
| charityEnd | -1 | CharityEnd |
| courtSiteId | 0 | CourtSiteId |
| clerkSiteId | 0 | ClerkSiteId |
| consentDue | -1 | ConsentDue |
| consentRequest | KingdomClerkReply.None | ConsentRequest |
| hallResult | KingdomNodeResult.None | HallResult |
| envoyResult | KingdomNodeResult.None | EnvoyResult |
| charityResult | KingdomNodeResult.None | CharityResult |
| guardResult | KingdomNodeResult.None | GuardResult |
| formal | false | Formal |
| verified | false | Verified |
| privateConsent | false | PrivateConsent |
| namedConsent | false | NamedConsent |
| transferConsent | false | TransferConsent |
| statementReceived | false | StatementReceived |
| envoyHandedOver | false | EnvoyHandedOver |
| charityObserved | false | CharityObserved |
| charityInterrupted | false | CharityInterrupted |
| guardPetition | false | GuardPetition |
| guardReceived | false | GuardReceived |
| resultSent | false | ResultSent |
| pendingLetter | false | PendingLetter |
| delivered | 0 | Delivered |
| publicMask | 0 | PublicMask |
| publicDueA | -1 | PublicDueA |
| publicDueB | -1 | PublicDueB |
| attention | 0 | Attention |
| route | KingdomRoute.None | Route |
| outcome | KingdomOutcome.None | Outcome |
| failure | KingdomFailure.None | Failure |
| clerk, envoy | null | Pawn 引用，真实人员由原版 worldPawns/商队/地图持有 |
| materials, routeHistory | 空集合 | 材料来源事实深存档及不可逆路线结果记录；档案不等于当前持有材料 |

`KingdomMaterialRecord` 深存档键 `number` 默认 0、`kind` 默认 ReceiptA、`source` 默认空字符串、`envoy` 默认 false、`externalMask` 默认 0。事实只在实物交付后记录；免费副本复用来源编号，不添加事实。

`Comp_RHAH_KingdomMaterial` 的键 `materialNumber` 默认 0、`materialKind` 默认 ReceiptA、`materialSource` 默认空字符串、`materialRedacted` 默认 false。归属 Remove，随 `RHAH_KingdomMaterial` 实物删除，不能以档案重建已失去的文件。

`WorldObject_RHAH_KingdomSite` 键 `kingdomSiteKind` 默认 Hall、`kingdomRecipients` 空 Pawn 引用集合、`kingdomSupply` 空 Thing 深集合、`kingdomRecipientGroup` 默认 0、`kingdomPacketReceived` 默认 false。发布接收组 1 为地方文书所，2 为独立赈济登记处。送达前文件仍为待送包裹，真实单程路程计算的往返回执期限到达才确认。世界地点 Remove；原版 worldPawns 中的人员保留，已交粮在其原版背包保留。未交粮在导出副本中按 recipient 转入原版背包，模组文件 Remove，不改当前游戏。

`CaravanArrivalAction_RHAH_KingdomSite` 键 `kingdomArrivalSite` 默认 null，归属 Remove，随商队到达动作删除；抵达只通知，明确选择开始办理后才记时。

王国篇选择信额外键：`kingdomStage` 默认 None、`kingdomRevision` 默认 0、`kingdomBodyKeys` / `kingdomSpeakerKeys` / `kingdomTexturePaths` 空集合、`kingdomConditions` 默认空字符串。发信保存当时场景/身份/条件，历史信不读取后来变化的案件；归属 Remove。全屏当前读段在进度保存，逐字位置与速度仍不保存。

### RHAH_LongFollowRecord

类型名：`HungerAndHavoc.EventMgr.RHAH_LongFollowRecord`。嵌在 `longFollows`，卸载 Remove，随游戏组件删除。

| 存档键 | 默认值 | 说明 |
| --- | --- | --- |
| chainId, batchId, mapId, phase | 0 | ODF 实例、原批次、目标地图与具体分支阶段；地图 0 有效 |
| displayId | 空字符串 | 原事件显示 ID，null 加载后补空 |
| dueTick, deadlineTick, lastTick | -1 | 延迟、选择期限与医学暂停推进基准；-1 未安排 |
| subjectId, sourceAdultId, responsibleId | 0 | 真实孩子、原母亲和相关成年人 thingID；0 无可靠身份，不从无关人物补造 |
| childIds, visitorIds, departedIds, selectedIds | 空集合 | 真实原孩子、重访者、已转移者和待交接者 ID；PostLoadInit 补空，null 与空集合相同 |
| playerWoundIds | 空集合 | 来客被玩家造成的实际 Hediff_Injury loadID；仅仍存在且出血的这些伤口导致致死失血时归因，愈合或自然死亡不由历史受伤标记推断；PostLoadInit 补空，卸载随记录 Remove |
| opened, completed, foodAlternative, askedEarly | false | 已打开、已结束、食物替代与提早追问 |
| keepsakeTaken, keepsakeDiscarded, medicalHold, playerKilledVisitor | false | 记物处置、医学暂停与玩家致死归因 |
| letterOpen, followupSelected, returnRequested, claimAcknowledged, revengePending | false | 当前选择、待续分支、返还、头衔确认与待报复 |
| rehomeCount, revisitCount, branch, nextDependentBatchId | 0 | 再交接/再访次数、固定随机分支与相关来客批次 |
| foodCount | 6 | 选择中的简单餐份数 |
| medicineCount | 2 | 选择中的药品份数 |
| keepsake | null | 真实旧窝记物 Thing 引用；被拿走也继续追踪，不深存第二份物品 |

原 Comp 的 `extraData` 新增 `rhah:longHold`（暂存 Leave/Join 闸门，沿用叙事留驻格式）和 `rhah:longEdge`（边缘停留格 `x,z`）。治疗时保留原停留格，`medicalHold` 暂停边缘职责；康复后回到原格再恢复选择。解除留驻时归还闸门并删除键，卸载随身份 Remove。新 I-002/I-037 初次事件会建立原母亲和真实亲子关系，并保留世界 Pawn；旧档只按原批次身份与已存 parent ID 恢复，历史上未生成的母亲无法还原。

ODF 的 51 个 definition ID、Version=1、StateVersion=1 和 payload 保持。Activate 前受支持的开放 stage 1 长链转 stage 3，旧 deadline 只补入未设置的记录 dueTick，ODF deadline 置 -1，保持实例 ID/开始时间/人物；终态实例不改、不重复行动，停用存档也完成迁移。stage 3 调度仍归 ODF，人物生日、死亡、离图及相关来客通知只唤醒该实例；医学暂停只推进记录期限，婴儿年龄导致的 alwaysDowned 不算患病。地图未准备、成年等待、返访未到期和无可执行互动返回 Wait，不追加 receipt；Wait 仍更新 LastTick 和医疗暂停期限。实际行动继续写 receipt。王国 Response 已发出结果且无其它计时事项时休眠。


## Def

| defName | 种类 | 卸载 |
| --- | --- | --- |
| RHAH_HungerMark | HediffDef | Remove。身份标记，不是伤病，不替换成原版 Hediff |
| RHAH_Plague | HediffDef | Remove。不替换成原版 Plague |
| RHAH_RefeedingSyndrome | HediffDef | Remove。不替换成原版 Hediff |
| RHAH_GnawedBark | HediffDef | Remove。不替换成原版 Hediff |
| RHAH_GnawedWall | HediffDef | Remove。不替换成原版 Hediff |
| RHAH_OvergnawedWall | HediffDef | Remove。不替换成原版 Hediff |
| RHAH_ClaySatiety | HediffDef | Remove。不替换成原版 Hediff |
| RHAH_GuanyinTu | ThingDef | Remove。不替换成原版食物 |
| RHAH_Heat_WetCloth, RHAH_Heat_MudCoat, RHAH_Heat_ReedWrap, RHAH_Heat_BarkWrap, RHAH_Heat_MudMantle, RHAH_Heat_MudShell | ThingDef | Remove。不替换成原版衣物 |
| RHAH_Cold_ThinHemp, RHAH_Cold_LayeredHemp, RHAH_Cold_StrawQuilt, RHAH_Cold_FurCloak, RHAH_Cold_SmokedBlanket, RHAH_Cold_HideWrap | ThingDef | Remove。不替换成原版衣物 |
| RHAH_MakeGuanyinTu | RecipeDef | Remove |
| RHAH_LargeRefugeeWave | IncidentDef | Remove |
| RHAH_ThiefRatkinGroup | IncidentDef | Remove |
| RHAH_AbandonedRatkinChildren | IncidentDef | Remove |
| RHAH_ShatteredMother | IncidentDef | Remove |
| RHAH_BeggarFamily | IncidentDef | Remove |
| RHAH_BeggarGroup | IncidentDef | Remove |
| RHAH_ThiefRatkinChildGroup | IncidentDef | Remove |
| RHAH_WildRatkinWandersIn | IncidentDef | Remove |
| RHAH_WildRatkinChildWandersIn | IncidentDef | Remove |
| RHAH_WildRatkinGroupWandersIn | IncidentDef | Remove |
| RHAH_FeralRatkinWandersIn, RHAH_FeralRatkinChildWandersIn, RHAH_FeralRatkinGroupWandersIn | IncidentDef | Remove。生成成员使用原版 WildMan PawnKind，不附加饥荒来源标记；导出时保留鼠族 Pawn 和原版野人状态 |
| RHAH_FamineRefugees | IncidentDef | Remove |
| RHAH_AidSimpleMeal, RHAH_AidFineMeal, RHAH_AidMedicine, RHAH_AidSilver, RHAH_AidBaby | IncidentDef | Remove |
| RHAH_IntelTreasureSimpleMeal, RHAH_IntelTreasureHerbal, RHAH_IntelTreasureSilver | IncidentDef | Remove |
| RHAH_IntelStructureSimpleMeal, RHAH_IntelStructureHerbal, RHAH_IntelStructureSilver | IncidentDef | Remove |
| RHAH_IntelSettlementSimpleMeal, RHAH_IntelSettlementHerbal, RHAH_IntelSettlementSilver | IncidentDef | Remove |
| RHAH_LaboringRefugees, RHAH_StrongSiege, RHAH_Passersby, RHAH_AirdropMistake, RHAH_MisguidedKinship | IncidentDef | Remove |
| RHAH_GreatFamine, RHAH_CaravanMuggers, RHAH_PlagueWanderers, RHAH_PlagueAbandonedBabies, RHAH_PlagueTraderCaravan, RHAH_PlaguePassersby | IncidentDef | Remove |
| RHAH_PlagueCaravanMuggers | IncidentDef | Remove |
| RHAH_PlagueRefugees, RHAH_PlagueOrphan, RHAH_PlagueBeggarGroup, RHAH_PlagueThiefGroup, RHAH_PlagueLaboringRefugees | IncidentDef | Remove |
| RHAH_PlagueStrongSiege, RHAH_PlagueAirdropMistake, RHAH_PlagueMisguidedKinship, RHAH_PlagueGreatFamine, RHAH_PlagueRevenge | IncidentDef | Remove |
| RHAH_RefugeeMassacre | IncidentDef | Remove |
| RHAH_ChildExchange | IncidentDef | Remove |
| RHAH_MouseSwarm, RHAH_PleadingJoin, RHAH_BanditAttack | IncidentDef | Remove。I-052/I-053/I-054；保留真实鼠族 Pawn、原版特质、技能与原版装备 |
| HungerAndHavoc.Narrative.NarrativeState | revealedCount, trust, rescued, lost, failed, suiyinEnabled, suiyinStarted, suiyinDeadlineTick, seenKinds, theftMaps, theftCounts, journalNoted, asidesSent, openingSent, progressSent, rewardClaimed, rewardPaid, rewardDue, envoyClue, relicClue, lastAsideTick, nextCaseId, aidCount, broadcastCount, expulsionCount, adultCount, completedKindCount, completedJournals, firstFactTick, nextAdultCheckTick, relicDone, endingE01, endingE02, endingE03, endingE04, endingE05, identityTier, identityRefused, entrustCases, exchangeCases, holeCases, envoyCases, relicCase, journalCases, pendingNotices | 同上 | Remove。1.0.4 停用普通 E-01..E-05 判定、触发与预览；`endingE01..endingE05`、`adultCount`、`firstFactTick`、`nextAdultCheckTick` 等已发布字段仍按原键与默认值存读，不改写、不清空。无新旧键迁移。`trust`、N 节点、通知与奖励不变；`relicDone`、`identityTier`、`identityRefused` 继续控制身份来信。`rewardDue` 默认 0，读档后小于 0 归 0。`completedJournals` 保留已完成记录编号，空集合与 null 相同，读档后 `completedKindCount` 仍以它的数量为准 |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinMember | loadId, presence, care, child, careTicks, missingSince, lastCareTick | 嵌在案子里 | Remove。随叙事组件删除；`lastCareTick` 默认 -1，旧累计保留，首次观察不补算未记录时间 |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinN004Case | id, motherId, mapId, startedTick, outcome, mother, missingSince, revisit, revisitSeen, revisitDeadline, meetingCaravanId, rescueDueTick, rescuePaid, breakUntil, breakTrait, effectsApplied, children | 嵌在 entrustCases | Remove |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinN005Case | id, mapId, startedTick, outcome, careClosed, children | 嵌在 exchangeCases | Remove |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinN006Case | mapId, thefts, startedTick, ignoreUntil, baitUntil, hole, foodPresent, wood, baitStock, outcome, losses, nextLossTick | 嵌在 holeCases | Remove |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinN008Case | mapId, pawnId, startedTick, deadline, checkUntil, presence, missingSince, outcome, mealsReady, proofAvailable, holdMigrated | 嵌在 envoyCases | Remove。`holdMigrated` 默认 false；新到达为 true。旧记录首次找到 N-008 来源且两闸门仍为 false 的使者时，以 `rhah:envoyHold=uu` 接管旧留驻；缺少历史覆盖信息时只能假定两项 false 来自旧使者持有，迁移仅做一次 |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinN009Case | startedTick, deadline, mapPresent, playersInside, envoyHere, boxDestroyed, outcome, siteId, boxId, mapEntered | relicCase | Remove |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinJournalCase | id, mapId, batchId, startedTick, delivered, driven, failed, empty, closed, counted, people | 嵌在 journalCases | Remove。`id=0` 是拦截失败标记，`failed=true`、`counted=false`；`closed=false` 表示尚未扣二信任，终态关闭后不重复扣除。普通观察不关闭这个标记；`people` 的 Presence 为 Left，不启动原事件照护判定 |
| HungerAndHavoc.Storyteller.Suiyin.SuiyinNotice | letter, arg, privateNotice | 嵌在 pendingNotices | Remove。既有枚举编号与键保持；发送时丢弃旧待发 Journal、N004Entry、N006Entry、N008Arrive 到来复述，原选择信、观察记录和奖励继续保留，其余通知按当前信件或短消息规则发送 |
| RHAH_BeggarSiege | IncidentDef | Remove |
| RHAH_Beg | JobDef | Remove |
| RHAH_Gnaw | JobDef | Remove |
| RHAH_DropChild | JobDef | Remove |
| RHAH_MotherFeed | JobDef | Remove |
| RHAH_Scavenge | JobDef | Remove |
| RHAH_TailBite | JobDef | Remove |
| RHAH_Expel | JobDef | Remove |
| RHAH_CarryYoung | JobDef | Remove，连同当前驱动和相关预约清理 |
| RHAH_RefugeeMassacre | QuestScriptDef | Remove |
| RHAH_RefugeeCamp | WorldObjectDef | 没有地图、也没有居民：Remove，删整个世界物体。已有地图或居民：Replace。保留世界物体 ID、地图和居民，类名与 Def 改成已加载的原版 `Site`，去掉本模组部件和生成步骤。`Site` 不能接住已生成地图就中止，不删地图 |
| RHAH_RefugeeCamp | SitePartDef | Remove。同名地点部件单独删除，不把它当成世界物体，也不改成原版部件 |
| RHAH_RefugeeCamp | MapGeneratorDef | Replace。已生成地图在 `maps/li/generatorDef` 保存；按 `mapInfo/parent` 匹配保留地点，只将自有生成器改为已加载、非本模组的 `Site.mapGenerator`，为空时使用原版 `Encounter`。替代 Def 缺失时中止 |
| RHAH_RefugeeCamp | GenStepDef | Remove，随地点部件清理，不改其它地图 |
| RHAH_Approach | WorldObjectDef | Remove。不替换成原版商队。卸载后物体消失，未到达的事件不再生成 |
| RHAH_Interception | WorldObjectDef | 没有地图、也没有居民：Remove，删整个世界物体。已有地图或居民：Replace。保留世界物体 ID、地图、玩家 pawn 和 `retainedCaravanData`，类名与 Def 改成已加载的原版 `Site`，去掉 `displayId`、`spawnBatchId`、`settled`。地图生成器已是原版 `Encounter`，不另换。`Site` 不能接住已生成地图就中止，不删地图。只带重组远行队组件，不带限时侦测或战场袭击。旧档里的原版 `AttackedNonPlayerCaravan` / `CaravansBattlefield` 只是当时的临时地图父物体，保持原样，不改成 `Site`，也不当作本模组地图迁移 |
| RHAH_RecordSite | WorldObjectDef | Replace。`worldObjectClass` 已是原版 `Site`。没有地图也没有居民：删整个世界物体。已有地图或居民：保留世界物体 ID、地图和居民，Def 改成已加载的原版 `Site`，地图生成器改成 `Site` 自带的生成器。`Site` 不能接住已生成地图就中止 |
| RHAH_RecordSite | SitePartDef | Remove。同名地点部件单独删除，不把它当成世界物体 |
| RHAH_RecordSite | MapGeneratorDef | Replace。按 `maps/li/mapInfo/parent` 匹配保留地点，将该地图的自有 `generatorDef` 改为已加载、非本模组的 `Site.mapGenerator`，为空时使用原版 `Encounter`。替代 Def 缺失时中止 |
| RHAH_RecordBox | ThingDef | Remove。不替换成原版建筑 |
| RHAH_MigrationRecord | ThingDef | Remove。不替换成原版物品 |
| RHAH_RecordLetter | LetterDef | Remove。不替换成原版信 |
| RHAH_VisitorSeek | DutyDef | Remove |
| RHAH_VisitorLeave | DutyDef | Remove |
| RHAH_VisitorFallback | ThinkTreeDef | Remove |
| RHAH_BroadcastHope | JobDef | Remove，清除当前和排队广播 Job，保留原版通讯台 |
| RHAH_Gene_ThinRations | GeneDef | Remove。不替换成原版基因 |
| RHAH_Gene_LargeLitter | GeneDef | Remove。不替换成原版基因 |
| RHAH_Gene_EarlyFertility | GeneDef | Remove。不替换成原版基因 |
| RHAH_Gene_HighFertility | GeneDef | Remove。不替换成原版基因 |
| RHAH_Gene_RoomFertility | GeneDef | Remove。不替换成原版基因 |
| RHAH_Gene_FastBirth | GeneDef | Remove。不替换成原版基因 |
| RHAH_Xenotype_Ratkin | XenotypeDef | Replace。异种引用改成已加载的 `Baseliner`。本模组基因从基因列表删除，其它模组基因留下。`Baseliner` 未加载就中止 |
| RHAH_XenotypeIcon_Ratkin | XenotypeIconDef | Remove |
| RHAH_Faction_Hostile, RHAH_Faction_LeaningHostile, RHAH_Faction_Neutral, RHAH_Faction_LeaningFriendly, RHAH_Faction_Friendly | FactionDef | Replace。派系 loadID 保留，Def 改成已加载且不属于本模组的原版 `Ancients`。目标缺失就中止。玩家派系和其它模组派系不改 |
| RHAH_History_* | BackstoryDef | Replace。童年或成年槽换成已加载、不属于本模组、同槽的 Backstory。按 defName 序选第一个，不按技能挑。背景带来的技能变化不保留。没有同槽背景就中止 |
| RHAH_Trait_* | TraitDef | Remove。不替换成原版特质 |
| RHAH_Thought_EggKeeperYoung, RHAH_Thought_HungerRage, RHAH_Thought_FoodSnatcher, RHAH_Thought_PlagueDreadSick, RHAH_Thought_PlagueDreadNearby | ThoughtDef | Remove |
| RHAH_Thought_NightTerrors, RHAH_Thought_GrainGreed, RHAH_Thought_Chillblood, RHAH_Thought_FamineGloom, RHAH_Thought_AilingMother, RHAH_Thought_FamilyThief | ThoughtDef | Remove |
| RHAH_Thought_BeggingSucceeded, RHAH_Thought_BeggingRejected, RHAH_Thought_BeggingSlapped | ThoughtDef | Remove |
| RHAH_Thought_MotherGoneBad, RHAH_Thought_MotherGoneGood, RHAH_Thought_AteMotherGuilt, RHAH_Thought_AteMotherFine, RHAH_Thought_MotherSorry, RHAH_Thought_MotherFine | ThoughtDef | Remove |
| RHAH_Thought_ChildDeadGlad, RHAH_Thought_ChildDeadSad, RHAH_Thought_ChildStarvedSad, RHAH_Thought_ChildStarvedGlad, RHAH_Thought_StealHurtBad, RHAH_Thought_StealHurtGood | ThoughtDef | Remove |
| RHAH_Thought_StealMine, RHAH_Thought_StealGot, RHAH_Thought_SoldAway, RHAH_Thought_SoldFed, RHAH_Thought_TradedAway, RHAH_Thought_TradedFed | ThoughtDef | Remove |
| RHAH_Thought_AidAgain, RHAH_Thought_AidEmpty, RHAH_Thought_FineGood, RHAH_Thought_FineKeep, RHAH_Thought_MedicineEnough, RHAH_Thought_MedicineNext | ThoughtDef | Remove |
| RHAH_Thought_SilverLight, RHAH_Thought_SilverHard, RHAH_Thought_TakenBad, RHAH_Thought_TakenFed, RHAH_Thought_ExtraMouth, RHAH_Thought_BornAlive | ThoughtDef | Remove |
| RHAH_Thought_FellBad, RHAH_Thought_FellGood, RHAH_Thought_WrongKinBad, RHAH_Thought_WrongKinGood, RHAH_Thought_PlagueMotherDeadBad, RHAH_Thought_PlagueMotherDeadGood | ThoughtDef | Remove |
| RHAH_Thought_PlagueLeft, RHAH_Thought_PlagueLived, RHAH_Thought_OrphanBad, RHAH_Thought_OrphanGood, RHAH_Thought_TraderSilent | ThoughtDef | Remove |
| RHAH_Thought_CleanBirth, RHAH_Thought_NextBirth, RHAH_Thought_BornSick, RHAH_Thought_DropSick, RHAH_Thought_DropLived, RHAH_Thought_WrongSick, RHAH_Thought_WrongFed | ThoughtDef | Remove |
| RHAH_Thought_ScavengedFilth, RHAH_Thought_TailBitten, RHAH_Thought_BitATail, RHAH_Thought_AteRatEggMeal | ThoughtDef | Remove |
| 外部 Hediff 的 SpokenThoughtTicks | 字符串思想阶段缓存 | Remove。仅在 healthTracker/hediffSet/hediffs 下的外部 Hediff 中，按本包已加载 ThoughtDef 的实际 stages 生成精确 defName_stageIndex 白名单；删除匹配的 keys/li 及同索引 values/li。外部 Hediff、外部键和 tick 配对保留；未知阶段或其它位置同名文本仍中止导出，不按前缀猜删 |
| HungerAndHavoc.Pawn.ThoughtWorker_RHAH_YoungInNeed | 无存档字段 | Remove |
| HungerAndHavoc.Pawn.ThoughtWorker_RHAH_NearbyDisease | 无存档字段 | Remove |

| RHAH_Suiyin | StorytellerDef | Replace。目标写死已加载的原版 `Randy`。未加载就中止，不保留穗音定义 |
| RHAH_ChoiceRequest | LetterDef | Remove。选择信随本模组删除，不替换成原版信 |
| RHAH_ChoiceVisitors | LetterDef | Remove。选择信随本模组删除，不替换成原版信 |
| RHAH_ChoiceRequestGreen | LetterDef | Remove。选择信随本模组删除，不替换成原版信 |
| RHAH_ChoiceVisitorsGreen | LetterDef | Remove。选择信随本模组删除，不替换成原版信 |
| RHAH_GreenIncident | LetterDef | Remove。选择信随本模组删除，不替换成原版信 |
| RHAH_EnvoyLetter, RHAH_GrainHoleLetter, RHAH_RevisitLetter | LetterDef | Remove。选择信随本模组删除，不替换成原版信 |
| RHAH_LongFollowLetter | LetterDef | Remove。交互长链选择信 |
| RHAH_BanditAttackLetter | LetterDef | Remove。历史档案与信件队列中的 `HungerAndHavoc.Incidents.ChoiceLetter_RHAH_BanditAttack` 一并删除；无新增信件字段 |
| RHAH_KingdomArcLetter | LetterDef | Remove。王国篇插图选择信，历史档案与待到达信件一并删除 |
| RHAH_KingdomMaterial | ThingDef | Remove。文书实物及其 Comp |
| RHAH_KingdomSite | WorldObjectDef | Remove。原版 worldPawns 人员与已交粮背包保留 |
| RHAH_LongMeeting, RHAH_LongMeetWalk | JobDef | Remove。清理当前及排队 Job，携带中的 Pawn 保留 |
| RHAH_LongKeepsake | ThingDef | Replace。改成已加载原版 `Cloth`，保留 Thing ID 与数量，当前新物品 stackCount=1；目标缺失时中止 |
| RHAH_Thought_MotherCameWithoutTaking, RHAH_Thought_MotherOutside, RHAH_Thought_MotherDiedAtDoor, RHAH_Thought_ChildStillAlive | ThoughtDef | Remove |
| RHAH_LongKeepsakeTaken, RHAH_LongKeepsakeLost, RHAH_LongClaimScared, RHAH_LongClaimRelieved, RHAH_LongClaimRecognized, RHAH_LongClaimDenied, RHAH_LongClaimRetracted, RHAH_LongClaimViolent, RHAH_LongClaimUnchallenged | ThoughtDef | Remove |
| RHAH_LongExchangeAlive, RHAH_LongRecognizedUnfed, RHAH_LongFoodAnyway, RHAH_LongYouHandedMeAway, RHAH_LongFoundYou, RHAH_LongWhyMe, RHAH_LongHandedAwayAgain | ThoughtDef | Remove |
| RHAH_LongSomeoneCameForMe, RHAH_LongTradedEverywhere, RHAH_LongBiteForFood, RHAH_LongDoorChildGone, RHAH_LongPushedAway, RHAH_LongMealsWereReal, RHAH_LongFollowedForYears | ThoughtDef | Remove |
| RHAH_RatkinTraderCaravan | IncidentDef | Remove |
| RHAH_PrisonerNeeds | ThinkTreeDef | Remove |
| RHAH_Thought_LeftAlone, RHAH_Thought_CaptiveYear, RHAH_Thought_FamilyHere, RHAH_Thought_Regret | ThoughtDef | Remove |
| RHAH_Thought_ClaySatiety, RHAH_Thought_FamineHunger, RHAH_Thought_FamineFed | ThoughtDef | Remove。前两项 situational，后一项真实吃饱记忆 |
| RHAH_Thought_Sheltered, RHAH_Thought_Captured, RHAH_Thought_EnslavedFed, RHAH_Thought_EnslavedServing, RHAH_Thought_Expelled, RHAH_Thought_AteFood | ThoughtDef，行为记忆 | Remove。奴役两种记忆互斥；永久负面分支没有期限，进食及驱赶同 Def 的两档互相替换 |
| RHAH_Trader_Famine | TraderKindDef | Replace。改成已加载原版 `Visitor_Outlander_Standard`，保留 trader Tracker 和真实原版库存 |
| RHAH_PawnKind_Ratkin | PawnKindDef | Replace。保留 pawn 的 thingID。`kindDef` 改成仍会随 NewRatkinPlus 加载、种族同为 `Ratkin`、不属于本模组的 PawnKind。按 defName 序选第一个。没有这种种类就中止，不换成人类 |

替代 Def 必须已经加载，且不属于本模组。写死的目标是 `Ancients`、`Baseliner`、`Randy`、`Site`、`Visitor_Outlander_Standard`。种类和背景的具体 defName 在导出时按上表从已加载 Def 里选，不写进本页。

### Hediff_RHAH_ClaySatiety

类型名：`HungerAndHavoc.Pawn.Hediff_RHAH_ClaySatiety`。

| 字段 | 存档键 | 默认值 | 集合 | 说明 |
| --- | --- | --- | --- | --- |
| windowStartTick | windowStartTick | -1 | 否 | 当前十五天窗口起点。-1 表示没有窗口 |
| ingestionCount | ingestionCount | 0 | 否 | 本窗口已吃块数，读档后夹到 0..3 |

## 非存档

这些不进游戏 `.rws`，卸载导出器也不清理。

| 名称 | 说明 |
| --- | --- |
| nanaloveyuki.ratkin.hungerandhavoc | packageId。清理副本的 meta 里应去掉本包，但不在运行时存档字段中 |
| RHAH_Settings.enableNewContent | 全局 ModSettings，默认 true |
| RHAH_Settings.optimizeGeneration | 全局 ModSettings，默认 true。关闭后不套权重异种，事件 Profile 仍生效 |
| RHAH_Settings.positiveIncidentDays | 全局 ModSettings，默认 15。正池平均天数，式子里的 averageDays，0 关闭，上限 60 |
| RHAH_Settings.negativeIncidentDays | 全局 ModSettings，默认 15。负池平均天数，式子里的 averageDays，0 关闭，上限 60 |
| RHAH_Settings.positiveIncidentPace | 全局 ModSettings，默认 `averageDays`。正池间隔式。空、超长或求值失败时回到默认式。结果仍夹到 0 到 60 |
| RHAH_Settings.negativeIncidentPace | 全局 ModSettings，默认 `averageDays`。负池间隔式。空、超长或求值失败时回到默认式。结果仍夹到 0 到 60 |
| RHAH_Settings.frequencyWindowDays | 全局 ModSettings，默认 15。频率图显示天数，范围 5 到 360。不改变抽池 |
| RHAH_Settings.xenotypeWeights | 全局 ModSettings，默认空字典。缺键用登记建议权重。空字典不是全部禁用 |
| RHAH_Settings.enabledXenotypeDefNames | 全局 ModSettings，默认空。玩家加入的外部异种 defName |
| RHAH_Settings.enabledGeneDefNames | 全局 ModSettings，默认空。只允许 `RHAH_` 基因在生成后追加 |
| RHAH_Settings.litterMin | 全局 ModSettings，默认 2。多崽一次分娩的最少数量，范围 1 到 12 |
| RHAH_Settings.litterPeak | 全局 ModSettings，默认 4。多崽概率图的峰，夹在最少和最多之间 |
| RHAH_Settings.litterMax | 全局 ModSettings，默认 6。多崽一次分娩的最多数量，不低于最少 |
| RHAH_Settings.fertileMinAge | 全局 ModSettings，默认 1。早熟允许受孕的最小生理年龄，范围 1 到 14 |
| RHAH_Settings.fertilityPercent | 全局 ModSettings，默认 300。高育相对常人的怀孕几率，范围 100 到 1000 |
| RHAH_Settings.gestationDays | 全局 ModSettings，默认 5.661。速产最短孕期天数，范围 3 到原版下限 5.661 |
| RHAH_Settings.lovinPregnancyPercent | 全局 ModSettings，默认 50。乱起爱爱成功后的基础受孕几率，范围 0 到 100。旧档缺键用默认 |
| RHAH_Settings.maternalMilkPercent | 全局 ModSettings，默认 150。母爱本能在哺乳期的产奶速度与储奶上限倍率，单位 %，范围 1 到 1000。旧配置缺键用默认 |
| RHAH_Settings.maternalHungerPercent | 全局 ModSettings，默认 115。母爱本能在哺乳期的饥饿速度倍率，单位 %，范围 1 到 1000。旧配置缺键用默认 |
| RHAH_Settings.maternalNutritionPercent | 全局 ModSettings，默认 125。母爱本能在哺乳期的食物需求上限倍率，单位 %，范围 1 到 1000。旧配置缺键用默认 |
| RHAH_Settings.paternalWorkPercent | 全局 ModSettings，默认 120。父爱本能在同图有活着的直接亲生子女时的工作速度倍率，子女年龄不限。单位 %，范围 1 到 1000。旧配置缺键用默认 |
| RHAH_Settings.paternalHungerPercent | 全局 ModSettings，默认 110。父爱本能在同图有活着的直接亲生子女时的饥饿速度倍率，子女年龄不限。单位 %，范围 1 到 1000。旧配置缺键用默认 |
| RHAH_Settings.paternalLearningPercent | 全局 ModSettings，默认 110。父爱本能在同图有活着的直接亲生子女时的全局学习速度倍率，子女年龄不限。单位 %，范围 1 到 1000。旧配置缺键用默认 |
| RHAH_Settings.paternalDamagePercent | 全局 ModSettings，默认 90。父爱本能在同图有活着的直接亲生子女时的承受伤害倍率，子女年龄不限。单位 %，范围 1 到 100。旧配置缺键用默认 |
| RHAH_Settings.reliefEnabled | 全局 ModSettings，默认 true。关闭后访客不受赈灾区限制 |
| RHAH_Settings.allowEatOutsideRelief | 全局 ModSettings，默认 false。空值不是允许区外取食 |
| RHAH_Settings.ignoreReliefAfterFed | 全局 ModSettings，默认 false |
| RHAH_Settings.leaveAfterFed | 全局 ModSettings，默认 true |
| RHAH_Settings.disabledReliefFoodDefNames | 全局 ModSettings，默认空。空名单表示当前食物可用，不是全部禁用 |
| RHAH_Settings.disabledGiveFoodDefNames | 全局 ModSettings，默认空。空名单表示当前食物可以交给来客，不是全部禁止。不随赈灾区开关 |
| RHAH_Settings.giveFoodListMode | 全局 ModSettings，默认 0。0 按模组，1 按名称，2 按原版分类。只影响给予食物菜单 |
| RHAH_Settings.disabledBegFoodDefNames | 全局 ModSettings，默认空。空名单表示当前正餐可以被乞讨拿走，不是全部禁止。生食不进名单。不随给予食物名单 |
| RHAH_Settings.begFoodListMode | 全局 ModSettings，默认 0。0 按模组，1 按名称，2 按原版分类。只影响乞讨食物菜单 |
| RHAH_Settings.aidRequestsEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.intelTradesEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.visitorChoicesEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.foodGiveHintDismissed | 全局 ModSettings，默认 false。第一次投喂后玩家选择不再提示 |
| RHAH_Settings.traderIgnoresHarshEnvironment | 全局 ModSettings，默认 true。商队不因恶劣环境离图 |
| RHAH_Settings.traderIgnoresEnclosedSpace | 全局 ModSettings，默认 true。商队在封闭房间里不挖路离开 |
| RHAH_Settings.famineArrivalEnabled | 全局 ModSettings，默认 true。只在新角色来源登记后初始化饱食零与营养不良；不用于重入图、读档或已加入玩家的角色 |
| RHAH_Settings.famineArrivalMalnutrition | 全局 ModSettings，默认 0.25，范围 0.01..0.5 |
| RHAH_Settings.famineMoodEnabled | 全局 ModSettings，默认 true。关闭饥饿思想及吃饱记忆效果 |
| RHAH_Settings.clayMoodEnabled | 全局 ModSettings，默认 true。窗口实际次数对应三阶段思想，关闭后 Inactive；不改观音土累计 |
| RHAH_Settings.famineTradeEnabled | 全局 ModSettings，默认 true。只控制活跃商队定向价格；关闭后保留专用商人库存、买粮能力与通常报价 |
| RHAH_Settings.famineFoodSaleFactor | 全局 ModSettings，默认 1.5，范围 1..3；玩家卖食物倍率，同项原买价封顶 |
| RHAH_Settings.famineGoodsBuyFactor | 全局 ModSettings，默认 0.75，范围 0.1..1；玩家买非食物倍率，同项原卖价托底 |
| RHAH_Settings.famineYearEnabled | 全局 ModSettings，默认 false。停用后人数倍率、额外天灾与鼠潮新触发停止；已有状态保留 |
| RHAH_Settings.famineYearChancePercent | 全局 ModSettings，默认 40，范围 0..100 |
| RHAH_Settings.famineYearPawnMultiplier | 全局 ModSettings，默认 3，范围 1..4；非有限值回默认。普通多人事件与营地人数乘倍，单人、固定家庭及鼠潮不乘 |
| RHAH_Settings.famineYearDisasterChancePercent | 全局 ModSettings，默认 20，范围 0..100；每日尝试、本年最多成功一次 |
| RHAH_Settings.armedDemandEnabled | 全局 ModSettings，默认 true。仅 Siege 三场景建立带数量期限的请求，不受援助/访客信开关；关闭已有请求停止该 Lord 的索粮进攻并离场，不改其它袭击 |
| RHAH_Settings.armedWeaponsEnabled | 全局 ModSettings，默认 true。只装备新 Siege 成年且可战者；原版低级武器品质普通及以下 |
| RHAH_Settings.spaceApproachEnabled | 全局 ModSettings，默认 false。关闭时太空层家园不生成远行队。缺键保持关闭 |
| RHAH_Settings.childExchangeFoodSubstitution | 全局 ModSettings，默认 true。易子而食玩家侧可用简单餐代替婴幼儿 |
| RHAH_Settings.familyDropEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.motherFeedEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.prisonerScavengeEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.tailBiteEnabled | 全局 ModSettings，默认 false |
| RHAH_Settings.famineVisitorsOpenDoors | 全局 ModSettings，默认 false。开启后饥民可以开门，地图上有敌人时仍不开 |
| RHAH_Settings.plagueSafeMode | 全局 ModSettings，默认 false。开启后鼠疫停在致死严重度以下 |
| RHAH_Settings.greenIncidentLetters | 全局 ModSettings，默认 true。关闭后鼠饥事件信用原版颜色 |
| RHAH_Settings.broadcastEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.broadcastCooldownDays | 全局 ModSettings，默认 3，范围 0 到 10 |
| RHAH_Settings.staggerGeneration | 全局 ModSettings，默认 true |
| RHAH_Settings.disabledIncidentGroupIds | 全局 ModSettings，默认空。按 23 类首个场景显示 ID 为键，关闭同类全部场景 |
| RHAH_Settings.incidentGroupPoints | 全局 ModSettings，默认空字典。同类共用规模，缺键用场景目录点数，范围 1 到 10000 |
| RHAH_Settings.incidentGroupWeights | 全局 ModSettings，默认空字典。同类共用出现权重，缺键 100，范围 0 到 100；0 关闭整类 |
| RHAH_Settings.incidentGroupAttitudes | 全局 ModSettings，默认空字典。同类共用玩家选定态度，缺键沿用具体场景的目录态度，值 0 到 4 |
| RHAH_Settings.incidentGroupLongChains | 全局 ModSettings，新建或恢复默认为 I-002、I-012，连同 I-037、I-038 同类场景生效。加载缺字段使用新默认；字段存在时保留所存名单，显式空或 IsNull 均表示全部短后续。只在已登记后续的场景启用交互长链 |
| RHAH_Settings.refugeeCampEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.refugeePredationChancePercent | 全局 ModSettings，默认 10，范围 0 到 100。每个安居点地图独立掷一次 |
| RHAH_Settings.refugeePredationFightBack | 全局 ModSettings，默认 true。关闭后被追猎的安居点鼠族逃跑 |
| RHAH_Settings.outsidePredatorsFollowDifficulty | 全局 ModSettings，默认 false。开启后外来捕食者无目标时走原版觅食 |
| RHAH_Settings.pawnHistoriesEnabled | 全局 ModSettings，默认 true。关闭后新来客不抽本模组经历 |
| RHAH_Settings.pawnTraitsEnabled | 全局 ModSettings，默认 true。关闭后新来客不抽本模组特质 |
| RHAH_Settings.disabledHistoryDisplayIds | 全局 ModSettings，默认空。空名单表示经历可抽 |
| RHAH_Settings.disabledTraitDisplayIds | 全局 ModSettings，默认空。空名单表示特质可抽 |
| RHAH_Settings.traitWeights | 全局 ModSettings，默认空字典。缺键用目录概率乘 100。0 不抽 |
| RHAH_Settings.maxEventPawns | 全局 ModSettings，默认 30，范围 1 到 100。低于事件最低人数时保留最低人数。母子固定组合不拆 |
| RHAH_Settings.minGeneratedAge | 全局 ModSettings，默认 0。普通来客年龄下限 |
| RHAH_Settings.maxGeneratedAge | 全局 ModSettings，默认 50。普通来客年龄上限，不超过 100 |
| RHAH_Settings.minMotherAge | 全局 ModSettings，默认 14，范围 1 到 14。`Mother` 与 `BeggarMother` 未指定年龄时的生成下界。普通到来下限更高时用普通下限。上界更低时固定为下界。幼年和已指定年龄不改。旧档缺键用默认。卸载不清理 |
| RHAH_Settings.youngAgeFollowsRange | 全局 ModSettings，默认 false。打开后幼年角色使用普通年龄区间。关闭时乞讨幼崽 1 天到 2.9 岁，其它幼年 3 到 6.9 岁。已指定年龄不改 |
| RHAH_Settings.allowImmobileBabies | 全局 ModSettings，默认 false。打开后无幼童模组也可以生成 4 岁以下。幼童模组启用时不抬龄 |
| RHAH_Settings.genderMode | 全局 ModSettings，默认 0。0 随机，1 按比例，2 女性，3 男性 |
| RHAH_Settings.femaleSharePercent | 全局 ModSettings，默认 50，范围 0 到 100。只在按比例时使用 |
| RHAH_Settings.playerIdeoPercent | 全局 ModSettings，默认 100，范围 0 到 100。新来客和后来入籍的人按这个比例改成玩家主要文化。0 保留到达时的文化。奴隶和囚犯不计入主要文化。没有 Ideology 时不使用 |
| RHAH_Settings.apparelMode | 全局 ModSettings，默认 0。0、1、2 都清空生成器衣物，先穿温度衣再叠穿兼容普通衣；3 不穿衣。显式衣列表不重配 |
| RHAH_Settings.apparelListMode | 全局 ModSettings，默认 0。0 按来源，1 按名称，2 按类别。只影响菜单 |
| RHAH_Settings.disabledRefugeeApparelDefNames | 全局 ModSettings，默认空。空名单表示固定准入池内衣物可生成：NewRatkinPlus 原始 55 项且来源匹配、官方部落装与四件幼年衣，再按制材、遮裸体、科技等级和年龄筛选；第三方衣物不自动加入 |
| RHAH_Settings.maxOwnedTraits | 全局 ModSettings，默认 1，范围 0 到 3。0 不抽本模组特质 |
| RHAH_Settings.allowVanillaTraits | 全局 ModSettings，默认 true。关闭后新来客不获得随年龄出现的原版特质 |
| RHAH_Settings.traitAgeFilter | 全局 ModSettings，默认 true。关闭后幼年特质和成年特质不再按年龄分开 |
| RHAH_Settings.contentListMode | 全局 ModSettings，默认 0。0 按来源，1 按名称，2 按类别。只影响菜单 |
| RHAH_Settings.reliefFoodScoreBonus | 全局 ModSettings，默认 0.1。赈灾区食物额外加分，0 到 1 |
| RHAH_Settings.fedWanderEnabled | 全局 ModSettings，默认 true。开启后吃饱先闲逛再离开，关闭后立刻走向出口。仍受 leaveAfterFed 控制 |
| RHAH_Settings.fedWanderHours | 全局 ModSettings，默认 12，范围 1 到 48。吃饱后闲逛的小时数 |
| RHAH_Settings.waitWhenNoFood | 全局 ModSettings，默认 true。关闭后找不到食物直接离开 |
| RHAH_Settings.noFoodWaitDays | 全局 ModSettings，默认 0.5，范围 0 到 5 |
| RHAH_Settings.shelterDays | 全局 ModSettings，默认 5，范围 5 到 240。短工，1 年按 60 天 |
| RHAH_Settings.hireDays | 全局 ModSettings，默认 240，范围 5 到 2400。长工，1 年按 60 天 |
| RHAH_Settings.shortStayLockApparel | 全局 ModSettings，默认 true。只在短工开始时锁定当时穿的衣物。短工结束或永久身份转换只解开本模组这次加上的锁，不调用 `UnlockAll`。关闭后新开始的短工不锁，已经开始的短工不追溯 |
| RHAH_Settings.coldClothesEnabled | 全局 ModSettings，默认 true。气温超出舒适范围时给新来客一件温度衣 |
| RHAH_Settings.temperatureApparelInsulation | 全局 ModSettings。温度衣 defName 到隔热，范围 0 到 100，缺省用 Def 默认值 |
| RHAH_Settings.disabledTemperatureApparelDefNames | 全局 ModSettings。关闭的温度衣 defName，默认空 |
| RHAH_Settings.minimumEventTemperature | 全局 ModSettings，默认 -35。地图事件下限 |
| RHAH_Settings.maximumEventTemperature | 全局 ModSettings，默认 70。地图事件上限。商队不受限 |
| RHAH_Settings.countEndingsWithoutNarrator | 全局 ModSettings，默认 true。退休兼容键，原值继续存读，不参与行为，不显示菜单 |
| RHAH_Settings.endingsWithoutNarrator | 全局 ModSettings，默认 true。退休兼容键，原值继续存读，不参与行为，不显示菜单 |
| RHAH_Settings.endingAidGoal | 全局 ModSettings，默认 99。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单 |
| RHAH_Settings.endingBroadcastGoal | 全局 ModSettings，默认 3。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单 |
| RHAH_Settings.endingExpulsionLimit | 全局 ModSettings，默认 3。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单 |
| RHAH_Settings.endingAdultGoal | 全局 ModSettings，默认 100。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单 |
| RHAH_Settings.endingWaitDays | 全局 ModSettings，默认 30。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单 |
| RHAH_Settings.endingE01 | 全局 ModSettings，默认 true。退休兼容键，原值继续存读，不参与行为，不显示菜单 |
| RHAH_Settings.endingE02 | 全局 ModSettings，默认 true。退休兼容键，原值继续存读，不参与行为，不显示菜单 |
| RHAH_Settings.endingE03 | 全局 ModSettings，默认 true。退休兼容键，原值继续存读，不参与行为，不显示菜单 |
| RHAH_Settings.endingE04 | 全局 ModSettings，默认 true。退休兼容键，原值继续存读，不参与行为，不显示菜单 |
| RHAH_Settings.endingE05 | 全局 ModSettings，默认 true。退休兼容键，原值继续存读，不参与行为，不显示菜单 |
| RHAH_Settings.endingIdentity | 全局 ModSettings，默认 true。只控制穗音身份来信，设置位于叙事页 |
| RHAH_Settings.plagueEnabled | 全局 ModSettings，默认 true。关闭后新来客不感染，也不再传播 |
| RHAH_Settings.plagueSpreadChancePerCarrier | 全局 ModSettings，默认 0.005，范围 0 到 1 |
| RHAH_Settings.plagueSpreadChanceCap | 全局 ModSettings，默认 0.30，范围 0 到 1 |
| RHAH_Settings.plagueSpreadDayInterval | 全局 ModSettings，默认 3，范围 1 到 30 |
| RHAH_Settings.plagueSpreadHour | 全局 ModSettings，默认 6，范围 0 到 23 |
| RHAH_Settings.plagueBloodPumpingSkipPercent | 全局 ModSettings，默认 120，范围 0 到 300 |
| RHAH_Settings.beggingEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.begAutoGiveEnabled | 全局 ModSettings，默认 false。开启后乞讨成功才从对方背包拿走一份允许乞讨的食物 |
| RHAH_Settings.begSuccessChancePercent | 全局 ModSettings，默认 35，范围 0 到 100。对同一殖民者第一次乞讨的基础成功率 |
| RHAH_Settings.begSocialBonusPercent | 全局 ModSettings，默认 3，范围 0 到 20。乞讨者每级社交额外增加的成功率 |
| RHAH_Settings.begFailCooldownHours | 全局 ModSettings，默认 3，范围 3 到 12。合适的人都讨不到后闲逛这么多小时才再乞讨 |
| RHAH_Settings.begSlapChancePercent | 全局 ModSettings，默认 50，范围 0 到 100。同一殖民者第二次及以后被乞讨时抽巴掌的几率 |
| RHAH_Settings.stealingEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.fightingEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.gnawingEnabled | 全局 ModSettings，默认 true |
| RHAH_Settings.batchTurnsHostile | 全局 ModSettings，默认 true |
| RHAH_Settings.batchLeavesTogether | 全局 ModSettings，默认 true |
| RHAH_Settings.suiYinThreatTempo | 全局 ModSettings，默认 true。关闭后穗音不再按信任改大型威胁节奏 |
| RHAH_Settings.weightWild | 全局 ModSettings，默认 1.4，范围 0 到 5 |
| RHAH_Settings.weightBeggar | 全局 ModSettings，默认 1 |
| RHAH_Settings.weightThief | 全局 ModSettings，默认 0.7 |
| RHAH_Settings.weightTrade | 全局 ModSettings，默认 0.5 |
| RHAH_Settings.weightSiege | 全局 ModSettings，默认 0.35 |
| RHAH_Settings.weightAid | 全局 ModSettings，默认 0.25 |
| RHAH_Settings.weightSpecial | 全局 ModSettings，默认 0.2 |
| RHAH_Settings.weightIntel | 全局 ModSettings，默认 0.12 |
| RHAH_Settings.weightSeason | 全局 ModSettings，默认 1.1。春冬系数 |
| RHAH_Settings.weightPlague | 全局 ModSettings，默认 0.5。鼠疫事件系数 |
| RHAH_Settings.requestMinSimple | 全局 ModSettings，默认 6。简单餐索取下限，0 到 200 |
| RHAH_Settings.requestMaxSimple | 全局 ModSettings，默认 28。简单餐索取上限，不低于下限 |
| RHAH_Settings.requestMinFine | 全局 ModSettings，默认 4。精致餐索取下限 |
| RHAH_Settings.requestMaxFine | 全局 ModSettings，默认 18。精致餐索取上限 |
| RHAH_Settings.requestMinMedicine | 全局 ModSettings，默认 2。药品索取下限 |
| RHAH_Settings.requestMaxMedicine | 全局 ModSettings，默认 10。药品索取上限 |
| RHAH_Settings.requestMinSilver | 全局 ModSettings，默认 80。白银索取下限 |
| RHAH_Settings.requestMaxSilver | 全局 ModSettings，默认 1200。白银索取上限 |
| RHAH_Settings.requestMinHerbal | 全局 ModSettings，默认 3。草药索取下限 |
| RHAH_Settings.requestMaxHerbal | 全局 ModSettings，默认 15。草药索取上限 |
| RHAH_Settings.requestDays | 全局 ModSettings，默认 1，范围 0 到 30。接济和情报信的期限 |
| RHAH_Settings.requestPointScale | 全局 ModSettings，默认 300。索取量相对这个点数缩放 |
| RHAH_Settings.foodPerChild | 全局 ModSettings，默认 10。易子而食每个孩子的简单餐 |
| RHAH_Settings.foodPerVisitor | 全局 ModSettings，默认 1。投喂按人数乘的份数 |
| RHAH_Settings.maxFoodRequest | 全局 ModSettings，默认 12。一次投喂的份数上限 |
| RHAH_Settings.envoyMealCost | 全局 ModSettings，默认 6。使者成交的简单餐 |
| RHAH_Settings.campMinAdults | 全局 ModSettings，默认 2。安居点成人下限 |
| RHAH_Settings.campMaxAdults | 全局 ModSettings，默认 4。安居点成人上限 |
| RHAH_Settings.campMinChildren | 全局 ModSettings，默认 8。安居点幼年下限 |
| RHAH_Settings.campMaxChildren | 全局 ModSettings，默认 16。安居点幼年上限 |
| RHAH_Settings.campGoodwill | 全局 ModSettings，默认 12。清掉安居点的好感 |
| RHAH_Settings.campDays | 全局 ModSettings，默认 15。安居点天数 |
| RHAH_Settings.campHuts | 全局 ModSettings，默认 4。安居点棚屋 |
| RHAH_Settings.holeWoodCost | 全局 ModSettings，默认 20。封粮洞的木材 |
| RHAH_Settings.holeCleanPortions | 全局 ModSettings，默认 5。清粮洞取走的食物 |
| RHAH_Settings.holeLossRange | 全局 ModSettings，默认 12。粮洞取食距离 |
| RHAH_Settings.holeMaxLosses | 全局 ModSettings，默认 3。粮洞最多损失次数 |
| RHAH_Settings.apparelAwfulPercent | 全局 ModSettings，默认 35。破旧衣几率 |
| RHAH_Settings.apparelPoorPercent | 全局 ModSettings，默认 50。较差衣几率，与破旧合计不超过 100 |
| RHAH_Settings.apparelMinDurabilityPercent | 全局 ModSettings，默认 10。新衣耐久下限 |
| RHAH_Settings.apparelMaxDurabilityPercent | 全局 ModSettings，默认 60。新衣耐久上限 |
| RHAH_Settings.apparelCorpsePercent | 全局 ModSettings，默认 25。尸衣几率 |
| RHAH_Settings.apparelClothPercent | 全局 ModSettings，默认 65。布料相对人皮的几率 |
| RHAH_Settings.apparelMaxPieces | 全局 ModSettings，默认 4。成年来客最多衣物 |
| RHAH_Settings.begFailMood | 全局 ModSettings，默认 -5。乞讨被拒心情 |
| RHAH_Settings.begSuccessMood | 全局 ModSettings，默认 3。乞讨成功心情 |
| RHAH_Settings.begSlapMood | 全局 ModSettings，默认 -10。被抽耳光心情 |
| RHAH_Settings.begSlapKnockoutHours | 全局 ModSettings，默认 0。0 表示半小时。读档时仍是旧默认 3 的改成 0，其它已保存值不改 |
| RHAH_Settings.begBruiseSeverity | 全局 ModSettings，默认 4。第一下瘀伤 |
| RHAH_Settings.begBruiseStep | 全局 ModSettings，默认 4。瘀伤加重 |
| RHAH_Settings.begBruiseMax | 全局 ModSettings，默认 16。瘀伤改割伤的上限 |
| RHAH_Settings.barkNutrition | 全局 ModSettings，默认 0.25。啃树皮营养。已保存的自定义值保留，缺键才用新默认 |
| RHAH_Settings.barkDamage | 全局 ModSettings，默认 12。啃树皮一次扣除的固定耐久，不受材料、建筑或伤害倍率影响。已保存的自定义值保留，缺键才用新默认 |
| RHAH_Settings.wallNutrition | 全局 ModSettings，默认 0.20。啃墙营养。已保存的自定义值保留，缺键才用新默认 |
| RHAH_Settings.wallDamage | 全局 ModSettings，默认 10。啃墙一次扣除的固定耐久，不受材料、建筑或伤害倍率影响。已保存的自定义值保留，缺键才用新默认 |
| RHAH_Settings.clayMaxBites | 全局 ModSettings，默认 3，范围 1..3。观音土窗口口数；不因加载较低设置削减已存实际累计 |
| RHAH_Settings.clayWindowDays | 全局 ModSettings，默认 15。观音土窗口天数 |
| RHAH_Settings.claySeverityPerBite | 全局 ModSettings，默认 0.33。每口饱腹 |
| RHAH_Settings.childHungryPercent | 全局 ModSettings，默认 30。母亲喂食的饥饿线 |
| RHAH_Settings.prisonerHungryPercent | 全局 ModSettings，默认 20。囚犯舔污和咬尾的饥饿线 |
| RHAH_Settings.tailBiteAge | 全局 ModSettings，默认 3。可咬的最大年龄 |
| RHAH_Settings.scavengeNutrition | 全局 ModSettings，默认 0.15。舔污营养 |
| RHAH_Settings.tailNutrition | 全局 ModSettings，默认 0.35。咬尾营养 |
| RHAH_Settings.tailFailDamage | 全局 ModSettings，默认 4。咬尾失败伤害 |
| RHAH_Settings.satisfiedFoodPercent | 全局 ModSettings，默认 82。吃饱线 |
| RHAH_Settings.refeedMalnutrition | 全局 ModSettings，默认 0.4。再吃的营养不良 |
| RHAH_Settings.plagueSeverityMax | 全局 ModSettings，默认 0.1。新感染严重度上限 |
| RHAH_Settings.followPredatorPercent | 全局 ModSettings，默认 10。短链捕食几率 |
| RHAH_Settings.followBirthWatchDays | 全局 ModSettings，默认 3。出生观察天数 |
| RHAH_Settings.followPlagueBirthDays | 全局 ModSettings，默认 5。鼠疫出生观察天数 |
| RHAH_Settings.followMotherReturnDays | 全局 ModSettings，默认 15。鼠疫母亲回归天数 |
| RHAH_Settings.followLongReturnYears | 全局 ModSettings，默认 2。长链额外年数 |
| RHAH_Settings.followAdultAge | 全局 ModSettings，默认 14。后续心情和长链年龄 |
| RHAH_Settings.followMoodScalePercent | 全局 ModSettings，默认 100。后续心情缩放 |
| RHAH_Settings.endingTrustFloor | 全局 ModSettings，默认 50。部分身份披露信任，范围 -100 到 100；沿用已发布键，不再用于普通结局 |
| RHAH_Settings.endingHopeTrust | 全局 ModSettings，默认 75。完整身份披露信任，不低于部分身份披露门槛且不高于 100；沿用已发布键，不再用于普通结局 |
| RHAH_Settings.endingHaltTrust | 全局 ModSettings，默认 -75。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单 |
| RHAH_Settings.endingLowKinds | 全局 ModSettings，默认 6。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单 |
| RHAH_Settings.endingThreatDays | 全局 ModSettings，默认 13。退休兼容键，原值继续存读，不再归一化，不参与行为，不显示菜单。信任干涉威胁节奏仍读取叙事者自身间隔 |
| RHAH_Settings.trustKill | 全局 ModSettings，默认 -10。放逐回访杀死 |
| RHAH_Settings.trustCaptive | 全局 ModSettings，默认 -5。俘虏，按人记 |
| RHAH_Settings.trustEntrustGood | 全局 ModSettings，默认 5。托孤安顿 |
| RHAH_Settings.trustEntrustCaptive | 全局 ModSettings，默认 -5。托孤被俘 |
| RHAH_Settings.trustEntrustStory | 全局 ModSettings，默认 -2。托孤只听故事 |
| RHAH_Settings.trustEntrustRegret | 全局 ModSettings，默认 -3。托孤后悔 |
| RHAH_Settings.trustEntrustBanished | 全局 ModSettings，默认 -1。托孤放逐 |
| RHAH_Settings.trustExchange | 全局 ModSettings，默认 3。交换照料完成 |
| RHAH_Settings.trustHoleOpen | 全局 ModSettings，默认 1。封上粮洞 |
| RHAH_Settings.trustHoleIgnore | 全局 ModSettings，默认 -1。清掉粮洞 |
| RHAH_Settings.trustHoleBait | 全局 ModSettings，默认 3。粮洞追踪 |
| RHAH_Settings.trustEnvoyFail | 全局 ModSettings，默认 -2。赶走使者 |
| RHAH_Settings.trustRelicFail | 全局 ModSettings，默认 -2。毁掉记录 |
| RHAH_Settings.trustHold | 全局 ModSettings，默认 -1。扣下，按人记 |
| RHAH_Settings.trustDeliver | 全局 ModSettings，默认 1。交付，按人记 |
| RHAH_Settings.trustLeave | 全局 ModSettings，默认 1。安全离开，按人记 |
| RHAH_Settings.trustExpel | 全局 ModSettings，默认 -1。驱逐，按人记 |
| RHAH_Settings.narrativeRewardSilver | 全局 ModSettings，默认 300。记录来信白银 |
| RHAH_Settings.narrativeRescueCost | 全局 ModSettings，默认 250。放逐赎回 |
| RHAH_Settings.narrativeRescueReward | 全局 ModSettings，默认 2500。赎回后的空投 |
| RHAH_Settings.narrativeRelicTake | 全局 ModSettings，默认 200。取走记录 |
| RHAH_Settings.narrativeRelicHand | 全局 ModSettings，默认 100。交出记录 |
| RHAH_Settings.narrativeTrustBonusPercent | 全局 ModSettings，默认 25。信任满值时的银两加成 |
| RHAH_Settings.narrativeMissingDays | 全局 ModSettings，默认 1。失踪天数 |
| RHAH_Settings.narrativeObserveDays | 全局 ModSettings，默认 30。去向不明的救济观察上限及托付成长观察期限；在场者可继续累计三天存活时间 |
| RHAH_Settings.narrativeHoleIgnoreDays | 全局 ModSettings，默认 3。粮洞忽视天数 |
| RHAH_Settings.narrativeEnvoyWaitDays | 全局 ModSettings，默认 3。使者等待 |
| RHAH_Settings.narrativeEnvoyCheckDays | 全局 ModSettings，默认 1。使者核对 |
| RHAH_Settings.narrativeRelicDays | 全局 ModSettings，默认 15。记录地点开放天数 |
| RHAH_Settings.narrativeRevisitYears | 全局 ModSettings，默认 4。放逐回访年数 |
| RHAH_Settings.narrativeAsideCooldownDays | 全局 ModSettings，默认 3。插话间隔 |
| RHAH_Settings.narrativeAsideCutoff | 全局 ModSettings，默认 -75。插话关闭的信任 |
| RHAH_Settings.narrativeAdultYears | 全局 ModSettings，默认 18。使者年龄 |
| RHAH_Settings.narrativeTheftKinds | 全局 ModSettings，默认 2。粮洞需要的偷窃种类 |
| RHAH_Settings.narrativeProgressKinds | 全局 ModSettings，默认 3。记录来信种类 |
| RHAH_Settings.narrativeRewardKinds | 全局 ModSettings，默认 8。白银来信种类 |
| RHAH_Settings.narrativeEnvoyKinds | 全局 ModSettings，默认 5。使者种类 |
| RHAH_Settings.narrativeRelicKinds | 全局 ModSettings，默认 8。记录地点种类 |
| HungerAndHavoc.Guard.* | Guard 始终加载，无存档类型 |
| RHAH_Mod / HarmonyBootstrap / RHAH_Runtime | 运行时入口，无 ExposeData |

## 卸载边界

- 按本页识别所有权，不删除其它模组的特质、种族、基因或 Hediff
- 事件链只删除 RHAH provider 分区；保留 ODF 根组件、根 schema、其它 provider 和 ODF meta，依赖仍安装
- 原档不覆盖；失败则中止，不把半成品当成可卸载副本
- 未知本模组 Def 或类型引用必须中止，不猜测删除其所属节点
- 清理交叉引用时保持 Scribe 字典 keys/values 同步
- 全局 ModSettings 不改；卸模组后设置文件可残留
