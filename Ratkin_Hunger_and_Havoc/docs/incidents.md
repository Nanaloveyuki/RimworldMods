# 事件目录

当前新抽取与设置使用 23 个事件类，包含 56 个活动来源场景；I-019 已退出新抽取，仅保留旧档请求与后续。既有 20 类继续在 `docs/events/` 合并记录，新三类规则列于下文。51 个原来源 ID、IncidentDef、Worker 和既有存档身份保持不变，不重新编号；I-008-01、I-009-01、I-010-01 是原版野人子事件，I-052/I-053/I-054 是新增鼠潮、祈求加入和盗匪袭击。

本页保留事件玩法和来源归属；实际执行、配置默认值与公开签名以 [事件源码](../Source/Incidents/)、[访客源码](../Source/Pawn/) 和 [事件后续源码](../Source/EventMgr/) 为准，不以历史文档覆盖代码。

显示 ID 是目录数据，不是列表下标。`I-001`..`I-054` 都有 IncidentDef 和 Worker。`baseChance` 为 0，不进入原版类别池。`RHAH_IncidentSchedule` 在讲述者每次检查时先求正负两池当天的平均间隔，再用 `Rand.MTBEventOccurs` 决定是否抽池，抽中后用 `RHAH_IncidentWeight` 选一条，交给现有 `QueueIncident`。间隔来自该池的算术式。默认为 `averageDays`，也就是原来的平均天数。式子可读 `day`、`averageDays`、`trust`、`season`，可用 `+ - * /`、括号、`min`、`max`、`abs`、`clamp`。`day` 是定居后天数，`season` 为 0 到 3。空式、超长、语法错误、除以 0 或非有限结果都回到 `averageDays`。求出的天数仍夹在 0 到 60，0 关闭该池。失败不重试、不补抽。入队保存当时的目标：正数地图 `uniqueID` 原样保存，明确首图 0 编码为 `int.MinValue`，商队用负数 `-ID`，旧 0 表示未指定。执行只解析保存的目标，不再重新随机。目标暂时不可用时留在队列并继续尝试后面的事件；目标已销毁、目录或 Worker 缺失、Worker 确定失败时只丢掉这一项。旧档没有目标列表时按 0 补齐，仍走未指定的旧解析。奥德赛空间层和生物群系 `OuterSpaceBiome` 的家园默认不抽地图事件，也不跑地图事件链；`spaceApproachEnabled` 打开后才列入。地图事件出队后不立刻生成 Pawn。`RHAH_Approach` 在目标家园外 4 到 6 个可走格生成一个没有 Pawn 的世界物体，平地每天 3 格，道路和地形仍按原版难度乘。家园在岛上、4 格内没有可走陆地时，改从对岸至少 4 格外的最近陆地出发，并允许穿过海洋。世界队伍沿用队列解析出的原始非负 mapId，包括首图 0，不重新选择家园。明确目标缺失时取消，不改投另一据点。没有合格家园时不创建远行队，改为直接在已解析地图生成。已经存在的太空远行队寻路失败时取消，不挡住穿梭机和运输舱选格。物体点击不挡住下层世界物体。

队伍到达后只有生成成功才完成事件。目标暂时不可用或 Worker 返回失败时，队伍保留，每游戏小时再试一次；到达阶段和剩余等待写入存档，读档后继续同一目标，不再重新走路。目标销毁、目录或 Worker 缺失、当前存档已停用新内容时取消队伍。Worker 抛异常可能已生成部分角色，记录错误并终止，不自动重跑以免重复生成。

玩家远行队与鼠族远行队在同一地表格时才显示拦截菜单。拦截在地图绘制初始化完成后直接生成敌对鼠族，不执行原事件 Worker，因此空投、索取、分娩和后续事件链不进入临时地图。人数沿用事件点数与人数上限；幼年成员至少四岁，保证能穿图。鼠族从地图最外缘入场，各自以 `TravelOrWait` 向可达的对边出口移动；进入出口 10 格半径后才切换离图 duty，出生边缘不能提前离场。倒地成员不阻挡其余成员离场。玩家原库存保留、入图后征召。成功后消费世界队伍，避免在家园再生成一次。

临时父物体 `RHAH_Interception` 使用 `Encounter`，只带重组远行队组件，没有原版战场判胜和袭击倒计时。最后一名活动鼠族离场、死亡或归入玩家后，本批穗音救济与完成判定保持否，信任减二，每批一次；死亡、捕获和离图不另算逐人信任。批次在既有 `journalCases` 中以 `id=0` 保存失败标记，终态写 `closed`，重读不重复扣除。事件不计入穗音经历种类，不开启 N 后续。已结束拦截地图可复用；玩家成员和入境运输器都不存在时，地图和父物体按原版规则移除。既有原版战场不迁移，旧倒计时继续由原版管理。

每条场景权重是 `family × season × plague × trust × target × 玩家生成权重 / 100`，同类入口取可匹配目标的场景权重平均值，先抽类，再按类内权重抽场景；场景数量不叠加抽取份额。族系数默认 Wild 1.4、Beggar 1、Thief 0.7、Trade 0.5、Siege 0.35、Aid 0.25、Special 0.2、Intel 0.12，可在频率页改，范围 0 到 5，0 不再抽这一类。春冬默认 ×1.1，鼠疫默认 ×0.5。只有负池吃信任：`1 - clamp(trust, -100, 100) / 400`。23 类设置共用类内开关、权重、到达态度、长链选择和调试点。生成权重范围 0 到 100，0 不抽；关闭类也阻止调试新触发，既有队列和开放记录保持原显示 ID。调试点写入 `IncidentParms.points`，范围 1 到 10000，自然触发和调试触发都用点数决定人数；接济与旧情报请求仍按 `requestPointScale` 缩放并夹在原上下限内，新交易按实际人数和库存食物报价。单人事件、交易商队和难民营人数不随点数变化。平均天数默认 15，范围 0 到 60，0 关闭该池。两池各有一条间隔式，默认 `averageDays`。频率页的曲线显示该池每日至少一次事件的概率；`frequencyWindowDays` 只改横轴，范围 5 到 360。定居未满 1 天不抽。I-019 退出自然抽取、广播和新调试触发，已有请求与后续继续。
接济和交易在生成后来信。特殊事件按显示 ID 选专用正文，其余复用适合的选择类别；I-013 的换子正文先于通用婴儿请求。原叙述保留，新规则另列报价、按钮和提示。实际人数、物资量和名称传入翻译；幼年正文根据已生成角色年龄选择，I-033/I-047 不满一岁不用直接台词，满 14 岁不用鼠蛋错音。答复期限和拒绝风险放独立提示，鼠疫提示读实际开关。选择信和索取信的 `lookTargets` 是本批 Pawn。新 I-020..I-028 使用 `transactionVersion=1`：选玩家仓库中同种总件数最多的合格食物，按每位实际来客 2.7 营养向上取整，最多 100 件；白银、零部件、药品等实际携带于来客库存，按实际报价价值生成。成交前核对食物、原奖励身份、数量和双方可达交接格，足额一次转移；失败不部分扣费，重复选择不再成交。可选情报是实物之外的赠品。旧开放情报信的版本为 0，保留原报价、期限与 `ItemStash`、`Outpost` 或 `BanditCamp` 回报，不重新报价。普通请求的拒绝和 `requestDays` 天超时按到达态度处理，默认 1 天；投喂让每名合格来客等待自己的食物，到期或收到离开令回到寻食。普通请求忽视只关信。启用武装索粮的 I-014/I-030/I-045 保留信与原期限，无视或暂缓不取消倒计时；拒绝立即进攻，逾期也进攻，请求仍可足额交粮止战离场。同一批次读档后不再开第二封未结算信。`I-051` 继续生成任务与临时安居点，正文读实际人数范围、期限和好感；人数写预计。每图独立掷捕食概率，默认 10%。关闭遵循难度时，外来捕食者饥饿且无可接近的鼠族或尸体就离图。居住区内鼠族尸体不可吃，已加入玩家的鼠族不是强制目标；击倒和俘虏不算任务完成。
救济成功不在交付时计数。交付或接受只把这一批标成已答应。还活着的人后来全部离开原地图，或在原地入籍、吃饱、没病地住满五天，这一批才算一次。冻死、热死、饿死、病死、被其它派系或动物杀死、老死的人退出判定，不扣信任。没有活人且没有玩家直接杀死时，这一批关闭，不计成功。玩家派系的枪、近战、陷阱或其它外部暴力杀死任何一人，这一批立刻失败，信任 −10，每批一次。失败后其余人安置也不补记。监狱、奴役、拒绝、超时、驱逐不算成功。情报交付不算救济。普通事件结局已退休；这些事实与信任不解锁王国篇政治结局。救济边界的设计理由见 [ADR 0004](adr/0004-relief-and-endings.md)，尚未实施的信任额度提案见 [ADR 0006](adr/0006-trust-event-budget.md)。
商队 `I-012` 与 `I-038` 默认 3 人，进入访客 Lord，I-038 仍携带鼠疫。`traderIgnoresHarshEnvironment` 和 `traderIgnoresEnclosedSpace` 默认开启；关闭后分别恢复恶劣环境离场和封闭空间离场。他们不使用原版交易 Lord。I-013 可以选实际玩家孩子交给来客，或按 `childExchangeFoodSubstitution` 用简单餐替代，默认每名来客孩子 10 份。成功后在场来客孩子永久加入，成人继续作为访客；玩家交出的孩子转为来客派系并保留真实身份供后续使用。加入、转移、当前鼠疫病情、库存不足均在交接前校验，因年龄不能行走的健康婴儿不当作医疗倒地。旧 I-019 已有婴儿请求继续提供真实孩子选择。`childExchangeVersion` 与 `childFoodAlternative` 区分新交接结果；旧记录按原 Kind 判断替代，不重新打开。商人不卖食物，Lead Your Pet 不接入食物替代；亲子且不是出售囚犯时可以牵母亲绳。
饥荒商队高价收购玩家食物、低价出售其它商品时，收购价最高为通常玩家购入价，出售价最低为通常玩家卖出价，避免同物买卖套利；物品自身交易限制与既有禁售规则仍适用。
来客选择里的投喂按人保存等待名单，每人索取 `foodPerVisitor` 份，不把整批份数堆给最年长者。殖民者右键具体来客时，进入同一条交食流程，不直接写成吃饱；只列出 `disabledGiveFoodDefNames` 没关掉、而且至少是糟糕食物的库存。名单为空时这些食物都可以交。这张名单不看来客自己能不能在赈灾区取食。只有该人实际进食达到阈值后才标成 Fed；一个人收齐不结束其它人的等待。
有效投喂进入等待后，仅本图本批当前敌对的活动访客转为中立，保留原 Lord、成员与到达态度，友好成员不降级。未收齐者按本人交接截止等待，不受历史吃饱离场期限影响；收齐清掉本人旧断粮计时，仍需实际吃饱。敌对且允许 Fight 的可行动成年人由访客工作入口委派原版攻击任务；儿童、婴儿及交接等待者不安排成人攻击。
短工和长工只出现在可整批处理的来客信上，期限看设置。短工最短 5 天、最长 1 年，默认 5 天。长工最短 5 天、最长 10 年，默认 1 年。一年按 60 天。到期恢复当前态度访客派系并离开，同批共用照护 Lord；不能自走的孩子由合格成人携出。伤病倒地暂停，年龄不能行走继续计时。永久加入清临时期限。右键单只来客仍可收留为短工或雇佣为长工。
选中短工、长工或临时征募时显示剩余离场时间，医疗倒地显示暂停，永久加入不显示。通讯台“广播希望”在殖民者完成实际广播作业后入队并固定通讯台所在地图，包括首图 0；普通年请求一条，荒年请求人数倍率向上取整条，默认三条。跳过不可用与已经排队的来源，逐个尝试剩余候选，回执显示实际接受数，不保证到场。至少接受一条才开始冷却，默认 3 天，可调 0..10 天。当前存档停用新内容后不能再广播。
到达态度按事件类保存，五档是敌对、偏敌对、中立、偏友好、友善；未配置的类内场景仍用各自目录缺省态度。当前叙事者是穗音且信任小于 0 时，生成再降一档，敌对保持敌对。其它叙事者不降。已经在场的来客不改写。

年度荒年默认关闭，默认年判概率 40%、每日额外天灾概率 20%，均可调。每年首日当地 06:00 判定；首次年中开启补判并来信。荒年每日 06:00 尝试四种原版天灾，本年最多成功一次。年度状态、配额和冷却保存于独立组件；跨年旧判定立即失效，不读旧鼠灾数据。多人事件在原人数限制之后乘 1..4 倍，默认三倍；单人和 I-003/I-004/I-029/I-044 固定家庭不乘，营地成人/孩子两项人数分别乘倍。

I-052 鼠潮仅在年度荒年启用且本年为荒年时可新触发，每年最多两次、至少间隔 15 天，随机 15..50 人，不再乘倍或受普通人数上限截断。到达固定中立；未答复不超时离场，也不因吃饱或断粮自动离开。优先偷食物与贵重可携物、可卸载建筑；不可搬走的贵重建筑走实际啃咬。驱赶、攻击、整批俘虏/奴役选择或玩家伤害/原版拘捕会阻止原任务与离场，令自由同批成员敌对反抗，只攻击玩家派系；整批俘虏/奴役按钮此时不直接转换身份，已经敌对也不能用陈旧送粮选择恢复中立。

I-053 祈求加入不要求荒年，单人至少 18 岁，三年（180 天）冷却。四个有效原版特质为 Kind:0、NaturalMood:1、Nerves:2、Industriousness:1，每项技能实际等级随机 10..20，无初始关系；不提供投喂或交粮入口。I-054 盗匪袭击固定敌对，必须带既有武器白名单中的 Core 武器、品质极差至普通，清掉其它生成武器；沿用可偷窃绑架的袭击 Lord，威胁信仅有关闭选项。两项稀有事件限额在接受触发时消费，赶路失败不退还。

`I-035`、`I-050` 是袭击例外：不使用可配置的中立到达态度或信任降档，到达与当前态度固定敌对。远行队与调试地图共享入场前的双向敌对锁定；调试已有地图另刷新原版攻击目标缓存。攻击 Lord 保留偷窃、绑架且不超时撤退，不由访客离场替换。
I-035/I-050 两条成功入场路径建立袭击 Lord 后发送原版 ThreatBig 威胁信，定位本批实际袭击者。远行队生成的新遭遇地图调用原版敌对地图暂停通知。失败路径不发送袭击成功信，不增加通知存档状态。

拒绝、一天超时和驱逐使用到达后的态度。敌对转敌对。偏敌对只离开。偏友好和友善离开。中立离开，不转敌对。玩家派系的外部暴力打伤时，敌对和偏敌对转敌对，偏友好和友善离开，中立不改。`batchTurnsHostile` 或 `batchLeavesTogether` 关闭后，对应的一半不执行。

访客信的攻击是独立的显式敌对动作，不复用拒绝；先改当前态度再问 Fight。驱逐只统计实际新离场人数，真实伤害才写 hurt，拒绝和超时不伪装主动驱逐。首个记录成员离图后仍从本图本批合格成员处理，不操作已加入、雇佣、囚奴或远处成员。

来客选择信的“送往盟友…”先打开目标派系选择页，每页最多 6 个派系，可上一页、下一页或返回原选项。玩家选择接纳派系后，合格来客先离场，只有成功离开原地图的人才入籍该盟友；死亡或未能离场的人不计入转送奖励。分页只使用临时对话节点，不增加存档字段。
转送在下达离场令后的状态预检离图权限；默认等待状态可以选择转送，剧情留驻和第三方明确禁止离图仍阻止转送。预检不修改来客状态。

每批按成功离场时的 Pawn 市场价值累计盟友好感：目标总额为 `min(10, ceil(累计市场价值 / 500))`，非正价值为 0。每人成功离场后只发放目标总额与本批已发放额的差值，整次转送最多 +10；原版成员救治离场的好感奖励不叠加。

本批首名来客成功离场时，只做一次回礼判定：工业及以上科技的盟友有 50% 概率回礼，工业以下不回礼。命中后随机等待 0.25 到 3 天，以该次离场为起点；投递时预留 110 tick 开舱时间，保证物资在三天内送达。每批最多一份回礼，从零部件、简单餐、木材和钢铁中随机选两种不同物资，数量分别为零部件 1..5、简单餐 10..30、木材 75..200、钢铁 50..150。到期才生成真实物资并空投，优先原家园地图，否则使用 `AnyPlayerHomeMap`；没有玩家家园地图则取消，不追加重试。转送结束后仍保留目标派系身份，作为回礼来源。

I-013 的穗音交换照料仍只在穗音下打开。新 I-002/I-037 在初次事件建立真实母亲和亲子关系，母亲作为世界 Pawn 保留，不凭重访随机角色冒充；旧档只恢复已有 parent ID。I-037 的穗音托孤入口当前只从原选择信成员寻找成年母亲，没有读取世界原母亲，因此单个遗弃孩子不能自动开案；具体边界见 [遗弃幼年鼠族](events/I-002.md)。其它叙事者照常结算交接及所选事件长链，不记穗音专用照料。

穗音新来信以作者小故事中的宫墙见闻、日记、有限资助和软弱矛盾为人物依据。普通观察仍保存和结算，在 IrisMenus 主动查看，不再自动发送 Journal 或 N004Entry；粮洞和使者只保留原选择信，不另发 N006Entry、N008Arrive 到来复述。交换答复、封洞/诱饵/停止/清理、使者交易/核对/拒绝/驱逐和记录取舍共 17 种直接结果改为短消息；照料结果、重要线索、人物阶段来信与身份揭示继续发信。旧档待发的上述四类到来复述在发送时丢弃，已打开的信不改正文；信任、奖励、记录及剧情状态不因通知删减改变。

新配置与恢复默认仅开启遗弃孩子 I-002/I-037、流民商队 I-012/I-038 两类长后续。加载缺失 incidentGroupLongChains 字段时采用这两类；字段存在则保留玩家名单，包括明确为空或 IsNull 的全短后续配置。其它已登记长后续仍可手动开启，既有实例不因改默认值改变期限或重新启动。

`Source/EventMgr` 提供 ODF 的事件实现，不抽 `I-001`..`I-054`，也不推进 `N-`。短链阶段 0 按 `followPredatorPercent` 排一次捕食者，处理后进入阶段 2，不重复投掷。选择长链的类按原来源开启 I-002、I-012、I-013、I-019、I-032、I-033、I-037、I-041 的具体后续，以及商队、空投和认亲的同类鼠疫场景；I-019 仅处理已有记录。后续通过真实母亲、商人、认领者、记物和原孩子执行选择、重访、照料、交接、心情、偷食和报复，按各自文档条件推进，不再统一达到年龄后只发通知。身份、选择、固定随机分支、期限、返访和实际物品存于 `longFollows`。ODF stage 3 是新互动调度入口，每小时错开检查，生日、死亡、交接和来客通知可唤醒实例；医疗倒地暂停相应期限，婴儿年龄导致的 alwaysDowned 不暂停。

原 51 个 ODF definition ID、版本与 payload 保持，新增 I-052/I-053/I-054 定义复用短后续。ODF Activate 前，受支持且未结束的 stage 1 自动转 stage 3；原 deadline 只补入尚未设置的记录 dueTick，ODF deadline 置 -1，保留实例 ID、开始时间、原人物与站点。停用存档同样迁移；失败分区保留原记录和阶段。1.0.6 设置允许的无具体长后续 stage 1 保留原阶段与期限并休眠，不冻结分区。既有终态实例不重开；旧 `notify_long` 重定向互动入口。历史上未生成或未保存的母亲、交出孩子身份不能还原，对应后续结束，不用无关角色代替。旧队列不改 ID，已到场来源不重写。地图临时不可用、成年或返访等待、暂无可执行互动时返回 Wait，不累积行动 receipt；医疗暂停仍顺延期限。原地图已销毁则结案并解除留驻。排队点数仍传入实际 IncidentParms，商队直达与地图回退保留。

一般物资请求足额检查后反向消费实际库存，跨完整堆不跳项，不足不部分扣费；武装索粮的 I-014/I-030/I-045 先验证实际库存与来客领取食物的能力，实际简单餐进入来客背包后才结算，并让来客止战离场。情报先验证并构造未发布站点，扣费成功才发布站点与结算。站点失败留信留窗且不扣库存；关闭选择开关不阻止既有请求超时。记录地点期限前保留；到期仅空置、未进图、无地图、无人且箱子未毁时移除。

关闭武装索粮后，已有请求取消并让对应 Lord 停止索粮进攻、离场；已结算请求不再解析 pawn 名单。对应 Lord 缺失的请求也会取消，不重复逐 tick 查找历史来客。

使者核对后保留或恢复原案件选择信，Checking 期限读 `checkUntil`；实时核对原使者、原地图和期限，失效终态归还本事件持有的两项离场闸门。使者与长链重叠留驻时，最后一个解除后才恢复原覆盖。

独立 N-007 检疫节点已退休。鼠疫主事件信统一追加病情、治疗照料和传播风险说明，关闭鼠疫时显示健康到来说明；处置沿用原事件。鼠疫感染与传播保留，检疫名单、结果计数、留观闸门和康复回访全部移除。旧档信件、案件、待发通知与临时留驻按 [save-ownership.md](save-ownership.md) 自动迁移，不保留退休类型或 Def。

粮洞封洞使用配置成本（含免费）及实际木材库存，Pending 到期无论洞是否仍在都关闭；诱饵洞毁结为 Gone，追踪站点失败保留原信，损失距离读配置。重逢信固定发信时相遇者、原 Tile、地图与目标持有商队；答复实时核对原玩家商队仍在相遇地点，不远程扣银或杀人，不以另一名孩子替代失效目标。重逢期限仍只限制首次相遇，不额外限制已发信答复。

开发者事件页每条事件保留“触发”，另有“即时刷新”。即时入口同步调用 Worker，不入队、不创建 `RHAH_Approach`，优先用当前地图，无当前地图时沿用地图解析。点数来自该事件配置，仍遵守全局新内容、仅此存档停用与事件开关。`I-035`、`I-050` 在选定地图边缘直接放置袭击者并建立袭击 Lord，不寻找玩家远行队或创建伏击地图；鼠疫携带者仍实际患病。`I-051` 复用安居点居民生成与防守 Lord，直接在选定地图放置居民，不创建任务、站点或棚屋，也不改殖民地建筑。自然触发不变。暂停中连续生成采用地图内尚未登记的正数批次 ID，避免同 tick 的后一次刷新被当作重复批次拒绝。

## 当前事件类

| 事件类 | 类配置键 | 保留的来源 ID | 交互长后续 |
| --- | --- | --- | --- |
| [乞讨队伍](events/I-001.md) | I-001 | I-001、I-005、I-042 | 无 |
| [乞讨的母亲](events/I-004.md) | I-004 | I-004 | 无 |
| [请求收留的逃难者](events/I-011.md) | I-011 | I-011、I-040 | 无 |
| [难民接济](events/I-015.md) | I-015 | I-015..I-018 | 无 |
| [做个交易](events/I-020.md) | I-020 | I-020..I-028 | 无 |
| [偷窃队伍](events/I-006.md) | I-006 | I-006、I-007、I-043 | 无 |
| [游荡鼠族](events/I-008.md) | I-008 | I-008..I-010、I-036、I-041、I-008-01、I-009-01、I-010-01 | 可选，仅 I-041；原版野人子事件无后续 |
| [遗弃幼年鼠族](events/I-002.md) | I-002 | I-002、I-037 | 默认开启 |
| [耗子分妈](events/I-003.md) | I-003 | I-003 | 无 |
| [流民商队](events/I-012.md) | I-012 | I-012、I-038 | 默认开启 |
| [易子而食](events/I-013.md) | I-013 | I-013 | 可选 |
| [乞食围攻](events/I-014.md) | I-014 | I-014、I-030、I-045 | 无 |
| [待产流民](events/I-029.md) | I-029 | I-029、I-044 | 无 |
| [路过的鼠族](events/I-031.md) | I-031 | I-031、I-039 | 无 |
| [空投失误](events/I-032.md) | I-032 | I-032、I-046 | 可选 |
| [认错亲人](events/I-033.md) | I-033 | I-033、I-047 | 可选 |
| [大灾荒](events/I-034.md) | I-034 | I-034、I-048 | 无 |
| [商队劫掠](events/I-035.md) | I-035 | I-035、I-050 | 无 |
| [鼠疫报复](events/I-049.md) | I-049 | I-049 | 无 |
| [危险的鼠族安居点](events/I-051.md) | I-051 | I-051 | 无 |
| 鼠潮 | I-052 | I-052 | 无 |
| 祈求加入 | I-053 | I-053 | 无 |
| 盗匪袭击 | I-054 | I-054 | 无 |

类配置键沿用首个来源 ID，不是新增显示 ID。合并的是新抽取入口和设置，不改写已到场角色、旧队列、开放请求或 ODF 实例的来源。长后续不要求穗音；穗音专属节点另按叙事设置处理。选择长阶段后不再另掷同一条短阶段捕食概率；未登记长后续的来源仍走短阶段。

[难民接济（婴儿）I-019](events/I-019.md) 仅为旧档保留说明，不计入活动类。其原后续已并入同页。

I-031/I-039 使用访客 Lord 的穿图 toil，保存可达对边出口，只在出口附近切换离场 duty。来客不在居住区停留乞讨；已加载的旧停留或寻食 toil 按原来源安全重定向，囚奴、玩家成员、临时留驻和已离场状态不重定向。普通乞讨、拦截的敌对和信任规则不变。

## 保留的场景目录

| 显示 ID | defName | 名称 | 旧 ID | Family | Origin | Category | Target | 所属事件文档 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| I-001 | RHAH_LargeRefugeeWave | 大批逃难者 | O-001 | Beggar | Original | Hunger | Map | [乞讨队伍](events/I-001.md) |
| I-002 | RHAH_AbandonedRatkinChildren | 幼年鼠族遗弃 | O-002 | Special | Original | Hunger | Map | [遗弃幼年鼠族](events/I-002.md) |
| I-003 | RHAH_ShatteredMother | 耗子分妈 | O-003 | Special | Original | Hunger | Map | [耗子分妈](events/I-003.md) |
| I-004 | RHAH_BeggarFamily | 乞讨的饥荒鼠族母亲 | O-004 | Beggar | Original | Hunger | Map | [乞讨的母亲](events/I-004.md) |
| I-005 | RHAH_BeggarGroup | 乞讨的鼠族队伍 | O-005 | Beggar | Original | Hunger | Map | [乞讨队伍](events/I-001.md) |
| I-006 | RHAH_ThiefRatkinGroup | 偷窃的鼠族 | O-006 | Thief | Original | Hunger | Map | [偷窃队伍](events/I-006.md) |
| I-007 | RHAH_ThiefRatkinChildGroup | 偷窃的幼年鼠族 | O-007 | Thief | Original | Hunger | Map | [偷窃队伍](events/I-006.md) |
| I-008 | RHAH_WildRatkinWandersIn | 游荡的野生鼠族 | O-008 | Wild | Original | Hunger | Map | [游荡鼠族](events/I-008.md) |
| I-009 | RHAH_WildRatkinChildWandersIn | 游荡的野生幼年鼠族 | O-009 | Wild | Original | Hunger | Map | [游荡鼠族](events/I-008.md) |
| I-010 | RHAH_WildRatkinGroupWandersIn | 游荡的野生鼠群 | O-010 | Wild | Original | Hunger | Map | [游荡鼠族](events/I-008.md) |
| I-008-01 | RHAH_FeralRatkinWandersIn | 游荡的野生鼠族（原版野人） | — | Wild | Original | Hunger | Map | [游荡鼠族](events/I-008.md) |
| I-009-01 | RHAH_FeralRatkinChildWandersIn | 游荡的野生幼年鼠族（原版野人） | — | Wild | Original | Hunger | Map | [游荡鼠族](events/I-008.md) |
| I-010-01 | RHAH_FeralRatkinGroupWandersIn | 游荡的野生鼠群（原版野人） | — | Wild | Original | Hunger | Map | [游荡鼠族](events/I-008.md) |
| I-011 | RHAH_FamineRefugees | 灾荒逃难者 | O-011 | Beggar | Original | Hunger | Map | [请求收留的逃难者](events/I-011.md) |
| I-012 | RHAH_RatkinTraderCaravan | 流民商队 | O-012 | Trade | Original | Hunger | Map | [流民商队](events/I-012.md) |
| I-013 | RHAH_ChildExchange | 易子而食 | O-013 | Special | Original | Hunger | Map | [易子而食](events/I-013.md) |
| I-014 | RHAH_BeggarSiege | 乞食围攻 | O-014 | Siege | Original | Hunger | Map | [乞食围攻](events/I-014.md) |
| I-015 | RHAH_AidSimpleMeal | 难民接济（简单餐） | N-011 | Aid | Sequel | Hunger | Map | [难民接济](events/I-015.md) |
| I-016 | RHAH_AidFineMeal | 难民接济（精致餐） | N-012 | Aid | Sequel | Hunger | Map | [难民接济](events/I-015.md) |
| I-017 | RHAH_AidMedicine | 难民接济（药品） | N-013 | Aid | Sequel | Hunger | Map | [难民接济](events/I-015.md) |
| I-018 | RHAH_AidSilver | 难民接济（白银） | N-014 | Aid | Sequel | Hunger | Map | [难民接济](events/I-015.md) |
| I-019 | RHAH_AidBaby | 难民接济（婴儿） | N-015 | Aid | Sequel | Hunger | Map | [仅旧档](events/I-019.md) |
| I-020 | RHAH_IntelTreasureSimpleMeal | 情报（藏货，简单餐） | N-016 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-021 | RHAH_IntelTreasureHerbal | 情报（藏货，草药） | N-017 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-022 | RHAH_IntelTreasureSilver | 情报（藏货，白银） | N-018 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-023 | RHAH_IntelStructureSimpleMeal | 情报（建筑，简单餐） | N-019 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-024 | RHAH_IntelStructureHerbal | 情报（建筑，草药） | N-020 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-025 | RHAH_IntelStructureSilver | 情报（建筑，白银） | N-021 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-026 | RHAH_IntelSettlementSimpleMeal | 情报（据点，简单餐） | N-022 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-027 | RHAH_IntelSettlementHerbal | 情报（据点，草药） | N-023 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-028 | RHAH_IntelSettlementSilver | 情报（据点，白银） | N-024 | Intel | Sequel | Hunger | Map | [做个交易](events/I-020.md) |
| I-029 | RHAH_LaboringRefugees | 待产流民 | N-025 | Beggar | Sequel | Hunger | Map | [待产流民](events/I-029.md) |
| I-030 | RHAH_StrongSiege | 强势围攻 | N-026 | Siege | Sequel | Hunger | Map | [乞食围攻](events/I-014.md) |
| I-031 | RHAH_Passersby | 路过的鼠族 | N-027 | Beggar | Sequel | Hunger | Map | [路过的鼠族](events/I-031.md) |
| I-032 | RHAH_AirdropMistake | 空投失误 | N-028 | Special | Sequel | Hunger | Map | [空投失误](events/I-032.md) |
| I-033 | RHAH_MisguidedKinship | 认错亲人 | N-029 | Special | Sequel | Hunger | Map | [认错亲人](events/I-033.md) |
| I-034 | RHAH_GreatFamine | 大灾荒 | N-030 | Thief | Sequel | Hunger | Map | [大灾荒](events/I-034.md) |
| I-035 | RHAH_CaravanMuggers | 商队劫掠 | N-031 | Thief | Sequel | Hunger | Caravan | [商队劫掠](events/I-035.md) |
| I-036 | RHAH_PlagueWanderers | 鼠疫游荡者 | N-032 | Wild | Sequel | Plague | Map | [游荡鼠族](events/I-008.md) |
| I-037 | RHAH_PlagueAbandonedBabies | 鼠疫遗弃幼年鼠族 | N-033 | Special | Sequel | Plague | Map | [遗弃幼年鼠族](events/I-002.md) |
| I-038 | RHAH_PlagueTraderCaravan | 鼠疫商队 | N-034 | Trade | Sequel | Plague | Map | [流民商队](events/I-012.md) |
| I-039 | RHAH_PlaguePassersby | 鼠疫路过 | N-035 | Beggar | Sequel | Plague | Map | [路过的鼠族](events/I-031.md) |
| I-040 | RHAH_PlagueRefugees | 鼠疫逃难者 | N-036 | Beggar | Sequel | Plague | Map | [请求收留的逃难者](events/I-011.md) |
| I-041 | RHAH_PlagueOrphan | 鼠疫孤儿 | N-037 | Wild | Sequel | Plague | Map | [游荡鼠族](events/I-008.md) |
| I-042 | RHAH_PlagueBeggarGroup | 鼠疫乞讨队伍 | N-038 | Beggar | Sequel | Plague | Map | [乞讨队伍](events/I-001.md) |
| I-043 | RHAH_PlagueThiefGroup | 鼠疫偷窃队伍 | N-039 | Thief | Sequel | Plague | Map | [偷窃队伍](events/I-006.md) |
| I-044 | RHAH_PlagueLaboringRefugees | 鼠疫待产流民 | N-040 | Beggar | Sequel | Plague | Map | [待产流民](events/I-029.md) |
| I-045 | RHAH_PlagueStrongSiege | 鼠疫强势围攻 | N-041 | Siege | Sequel | Plague | Map | [乞食围攻](events/I-014.md) |
| I-046 | RHAH_PlagueAirdropMistake | 鼠疫空投失误 | N-042 | Special | Sequel | Plague | Map | [空投失误](events/I-032.md) |
| I-047 | RHAH_PlagueMisguidedKinship | 鼠疫认错亲人 | N-043 | Special | Sequel | Plague | Map | [认错亲人](events/I-033.md) |
| I-048 | RHAH_PlagueGreatFamine | 鼠疫大灾荒 | N-044 | Thief | Sequel | Plague | Map | [大灾荒](events/I-034.md) |
| I-049 | RHAH_PlagueRevenge | 鼠疫报复 | N-045 | Special | Sequel | Plague | Map | [鼠疫报复](events/I-049.md) |
| I-050 | RHAH_PlagueCaravanMuggers | 鼠疫商队劫掠 | N-046 | Thief | Sequel | Plague | Caravan | [商队劫掠](events/I-035.md) |
| I-051 | RHAH_RefugeeMassacre | 危险的鼠族安居点 | N-047 | Special | Sequel | Hunger | Map | [危险的鼠族安居点](events/I-051.md) |
| I-052 | RHAH_MouseSwarm | 鼠潮 | — | Thief | Original | Hunger | Map | 本页新增事件规则 |
| I-053 | RHAH_PleadingJoin | 祈求加入 | — | Beggar | Original | Hunger | Map | 本页新增事件规则 |
| I-054 | RHAH_BanditAttack | 盗匪袭击 | — | Thief | Original | Hunger | Map | 本页新增事件规则 |
