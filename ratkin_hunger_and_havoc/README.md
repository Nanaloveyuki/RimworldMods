# 鼠族: 饥与祸

<!-- 只放面向用户的说明!!! 
面向开发者/Agent的放在开发者贡献文档或 Agents.md
-->

RimWorld 1.6 / 1.7 模组。`packageId`：`nanaloveyuki.ratkin.hungerandhavoc`。

需要 NewRatkinPlus、Harmony、Biotech 和 Oxygen DSL Framework（`nanaloveyuki.oxygendsl`）。Oxygen DSL Framework 必须排在本模组前面。IrisMenus 用于分页设置。

独立作品，不是“鼠灾-大荒年”的 Continued。Def 前缀 `RHAH_`，普通事件显示 ID 用 `I-`。
仅卸载本模组前，在已加载存档的设置里先停用这个存档的新内容，再备份并导出卸载用副本。退出后只卸本模组，保留鼠族、Harmony、Biotech 和 Oxygen DSL Framework，然后加载 `RHAH-removed-*`。`RHAH-backup-*` 仍含本模组数据，加载它必须留着本模组。

## 1.2.1 · 层级地图入场修复

- 修复 Layered Atmosphere and Orbit 大气层与轨道地图因边缘全是空气而无法接收入场来客的问题。有可用步行入口时保持原样；否则寻找可达殖民地的实体地面，以原版空投舱入场。普通来客、后续事件、地图商队和野人共用这一规则，已有身份、同行组及信件流程保持原样。
- 大气层使用 `OrbitLayer` 但可能不是太空，不再用 `isSpace` 代替地图入场几何。月面保留地表边缘路径；没有合法落点时继续等待，不把来客生在空气中。
- 温度和已有开关不变：高层室外温度超出「最低／最高事件温度」时，受该限制的事件仍等待；太空家园仍需开启对应选项。

1.7 构建与 870 项离线回归通过。真实程序集探针使用空气包围的合成平台，确认原版边缘入场失败后，大气层和高轨能选到实体地面并启用空投；全空气地图仍拒绝生成。探针只替代殖民地可达性，地形、可站立、迷雾、物理空投许可与随机选格使用实际实现；未完成实际落舱或完整模组组合的游戏内验收。隔离启动器仍请求不存在的 `RimWorldWin64.exe`。本次发布仅更新 1.7，1.6 产物保持不变。

MCP 反馈「分娩鼠妈任务无法生育」仍未关闭：截图仅含 `KeyNotFoundException` 首行。当前 1.7 真实程序集探针中，事件初始化正常转入产程，后续转接到用力产程时保留同一父母与基因记录，登记可正常移除；未验证最终出生和反馈者的第三方补丁组合，尚无足够证据确定该反馈根因。需要首次异常完整堆栈或问题存档，不对既有卡住产程自动补生孩子。

## 开发中 · IrisMenus 设置与开门权限

- 1.7 设置按用途分为通用、事件、来客行为、救济与食物、来客生成、基因、信任与安置、兼容与诊断；相关选项使用页内章节，保留独立用途的分类入口。
- 「手动触发事件」单独成页，不混入普通事件配置或兼容与诊断；触发按钮和具体事件搜索仅在开发者模式启用，来客诊断仍仅在开发者模式显示。
- 开关、数值、选择、搜索、按钮和滚动接入新版 IrisMenus，基因页的恢复默认权重按钮与提示也使用新版样式；重置同时清除旧权重输入缓存。物品名单保留共享列表的图标、价值配色、分类、搜索、折叠与批量选择。
- 页内章节标记使用本次滚动结束后的实际位置；滚到页尾时识别无法顶端对齐的最后一节，向上滚动后恢复对应章节。
- 「允许来客开门」恢复到来客行为；默认关闭，关闭时仍允许来客离场开门。开启允许在场来客通过原本可通行的门，地图有敌情时仍阻止；不会绕过原版禁止。已有旧配置继续读取。
- 未安装 IrisMenus 时仍使用原版设置入口。新版接入针对 1.7，1.6 已发布内容保持不变。

## 开发中 · 卸载导出、中途加入与远行队

- 卸载导出可清理自定义异种使用的本模组图标，保留其自定义名称、其它模组基因和图标。只解除本模组短工添加的服装锁；导出失败不删除已有临时文件。
- 中途加入时补齐缺失的态度派系，并立即初始化新派系的双向关系；不重建已有派系、不重编号，也不重置已有派系之间或它们与外部派系的关系。停用新内容的存档仍保持停用。
- 同格的多支鼠族远行队不再互相阻止拦截；选中玩家远行队后增加同格拦截按钮，不依赖重叠图标的右键命中。已有据点等不允许生成遭遇地图的地点仍阻止拦截。
- 远行队实际已在目标格时，先交接到达，不再继续消耗遗留行程；已进入等待的队伍仍沿用原有一游戏小时重试，不复制事件。

1.7 构建、结构检查与 830 项离线回归通过；构建保留一项既有 xUnit2004 警告。真实程序集探针覆盖同格多队拦截资格及按钮生成、目标格交接、清理副本落盘再读，以及补齐五个派系后重复初始化不增生；使用合成世界，派系生成重操作由替身提供，未做视觉验收。无法确认截图中重合不入图的唯一原因，也未完成卸载后游戏内加载、运行、保存、重载：隔离启动器仍请求不存在的 `RimWorldWin64.exe`，本机 1.7 使用 `RimWorld.exe`。未修改玩家存档、未部署，1.6 产物未更新。


## 开发中 · 携婴入场与玩家反馈修复

- 有成年陪同者的来客会将无法自行行走的幼童接到殖民地外的停留点，不再把他们留在地图入口。已经入图且仍属于同一批来客的幼童也适用；原有弃婴事件、离场权限和玩家接管继续生效。
- 「易子而食」恢复为一名成年陪同者带一名孩子到来。成交只接收孩子，食物费用只按孩子计算，陪同者随后离开。
- 修正饥荒鼠族派系的默认成员和异种组成；其它模组通过原版入口生成这类来客时，使用本模组的鼠族异种权重和种族许可。已经错误生成的人物保持原样，第三方鼠教会派系不在本次修改范围内。

1.7 构建、结构检查及 808 项离线回归通过。真实游戏程序集机制探针覆盖入口接送、继续抱持、到达放下和原版异种抽取；地图寻路与地面放置使用替身。尚未进行游戏内完整行走、新世界派系界面或 MO’Event 救生舱实测。本次未部署，1.6 已编译内容保持不变。

## 1.1.5 · 来客离场修复

- 来客准备离场后可正常通过原本允许通行的门，未离场时仍不能自行开门进入仓库；移除「允许饥民开门」设置。地图上有敌情时仍不放行。
- 确实没有出口时尝试破障离开；抱着孩子时不派发会自行放下孩子的单人破障任务，孩子的离图权限与原有弃婴设置继续生效。
- 单独离场的牵引来客在开始破障前解除牵引，不再被跟随逻辑拉回。失败重试的冷却不覆盖禁止离图、倒地或照护孩子的判定。
- 未离场来客的开门检查不再重复查询地图敌情。

1.7 构建、770 项离线回归及真实程序集机制探针通过。探针覆盖抱持保护、冷却后的权限与倒地变化、实际 Lead Your Pet API 解绑和门权限；地图寻路使用替身，不是完整游戏内离图验收。另通过缓存 1.6 参考程序集编译，未做 1.6 运行时验证。本次未部署，保留 1.6 已编译内容。

## 当前兼容 · 边缘生存空岛

- 启用 RimSkyBlock_Skyland（`yexiaoyang.rsbskyland`）后，原版设置和 IrisMenus「通用」页显示「允许空岛刷新事件」，默认关闭，旧配置也保持关闭。地面据点不受影响，不与太空／SOS2 开关共用。
- 关闭时，空岛不抽取本模组的新地图事件、不响应广播呼叫，也不派出新的鼠族远行队。已经排队或在途的事件保留原目标并等待重新允许，不直接生成或转投其它地图；已经入图的来客保持原样。
- 开启只解除生成限制，不修复空岛的入场寻路，远行队仍可能无法进入地图。

1.7 完整门禁及 763 项离线回归通过；真实游戏程序集与所提供空岛 DLL 的 19 项探针检查通过，覆盖岛屿判定、两个开关互不代替、队列和到达等待、设置保存重读及两个设置入口的条件显示与交互。探针使用合成地图并替代底层绘制，不是游戏内寻路或视觉验收。已部署至本机 `Mods/RatkinHungerAndHavoc` 并通过逐文件 SHA-256 校验，保留已安装的 1.6 文件和 `PublishedFileId.txt`；1.6 产物未更新。

## 当前修复 · 生成年龄与事件信件

- 未满18岁的新来客只分配幼年或全年龄的本模组特质，关闭「特质跟随年龄」也保留这一限制。青少年继续抽取幼年经历；生成时清除不合年龄的成年经历。已有角色的特质与经历保持原样。
- 「大批逃难者」改用可处理来客的选择信，提供招募、加入、雇佣等适用选项。关闭对应来客选择设置时，仍使用普通到来通知。
- 已处理、过期或失去关联记录的来客、请求及长后续选择信可右键关闭，并在每个游戏内小时清理。待处理决策、仍在进行的武装索粮和暂停计时的长后续保留；普通通知及其他模组信件保持原样。

结构与依赖检查、1.7 构建及763项离线回归通过。真实1.7程序集探针执行了6、13、17.999、18、30岁的内容分配，以及大批逃难者的信件创建、接收、右键资格与定时清理。探针使用合成角色、定义与游戏状态，并非玩家存档或游戏内界面验收。本次未部署，1.6产物未更新。

## 当前修复 · 安置与离场

- 「安置进度」在角色实际离图时记录「已离开」，之后角色不再保留在世界名单中也不会改成失踪。没有正常离图记录、又无法找到的角色，仍按原等待时间记为失踪；离开原据点的世界角色不再累计在场照料时间。
- 删除安置预览底部的两段说明，保留分组与各角色的进度、去向。
- 驱逐时清理原有待办任务，离场途中不再折返觅食或啃食。生成时的饥饿度与饥饿效果保持不变；殖民者、照护孩子和明确禁止离场的留驻行为保持原样。

旧档已误记为失踪的角色，只有仍可查到真实去向或之后实际离图时才能纠正；角色已经被清理且没有离图记录的情况无法追溯。已结算的历史结果不重算。

结构检查、1.7 构建与 713 项离线回归通过，覆盖离图后角色消失、无离图记录的失踪、携带孩子、旧记录纠正与保存重读。真实 1.7 程序集探针通过中英日详情绘制、离图状态转换，以及零营养时连续 20 次进食任务打断检查；字体、底层绘制与地图寻路边界使用替身，不是游戏内完整离图或 RuntimeGC／BetterGC 兼容实测。本次未部署，1.6 产物未更新。

## 当前变更 · 穗音仅保留信任

穗音剧情、王国篇、身份揭示、日记回读及剧情奖励已移除，设置中也移除了强制启用入口。普通 `I-` 事件、八条长后续及「安置进度」保留。原版设置和 IrisMenus「穗音信任」页显示当前存档的信任，只能查看，不能直接改分。

旧档信任值继续保留。穗音担任叙事者时，原有的信任累计与殖民地袭击概率修正保持不变。「穗音按信任调整大型威胁」开关仍可调整。1.7 默认不按信任修改来客态度和本模组事件频率，也不额外结算托孤、换子照料的信任；旧档原已启用的这些信任效果按原状态保留。移除剧情不会启用新的信任效果，也不恢复旧剧情流程。

穗音的叙事者小头像改为等比裁剪的面部近景，不再缩小整张立绘；大立绘保持不变，1.6 / 1.7 共用此修正。

下文带有“历史”说明的剧情内容仅记录旧版本行为，以本节为准。升级前请备份存档；下文已有构建、部署和回归记录不代表本次修改已经验证或发布。

本次通过完整结构与依赖检查、1.7 构建和 684 项离线回归；1.6 构建及 77 项针对性回归通过，缓存程序集构建仍有 UnityEngine 引用与 System.Runtime 版本警告。真实 1.7 游戏程序集探针确认信任累计、叙事者切换、正负信任权重、跳过类别、大型威胁超期保障及关闭开关后的原版路径。IrisMenus 信任页执行了中英日三种语言、有／无存档及开关交互；地图家园判定、底层绘制与字体测量使用替身，不是游戏内布局或玩家存档验收。本次未部署。

升级兼容审查补充：修复王国收货人休眠后无法转交库存、剧情地图转为空 Site、旧使者禁止离场状态残留，以及删除剧情物品时破坏逆重飞船物品与坐标配对的问题。旧档单独关闭的托孤／换子信任结算继续关闭，已经结算的记录不重新结算。迁移只修改加载中的内存 XML，不覆盖原存档；找不到原收货人的有效背包时保留原档并中止加载，不静默丢弃物资。

本轮用已发布 1.7 DLL 实际保存信任、关闭节点、部分照料进度和已完成记录，再用新版 Scribe 读取、保存并重读通过；同时验证地图父对象 ID 保留及原版 Camp 标签。完整 1.7 门禁和 692 项回归、1.6 构建与 85 项针对性回归通过。上述为真实序列化和合成升级场景，不是完整玩家存档或原生游戏加载验收；升级前仍请备份。本次未部署。

Suiyin now retains trust only. Her stories, kingdom arc, identity revelations, journal reader, story rewards and force-enable controls have been removed. Ordinary I-series incidents, their eight long follow-ups and Settlement progress remain. Saved trust and its existing raid-probability effect are preserved. Default 1.7 saves keep visitor-attitude, incident-frequency and passive care trust effects disabled; older saves that already enabled those trust effects keep them. Historical story descriptions below do not describe the current version.

## 1.1.4 · 索粮食物与请求修复

- IrisMenus「来客」页新增独立的索粮食物名单，默认允许原版、官方 DLC 和鼠族本体的合格正餐，包括硬饼干；其它模组的食物列在名单中，需玩家勾选。此设置不改变普通投喂和乞讨的食物名单。
- 名单沿用按模组、按名称、按原版分类三种方式，支持搜索、折叠、逐项选择、全部允许、全部禁止和恢复默认；每项显示单份营养与简单餐等价值。
- 信件交付允许混合食物，按每份简单餐 0.9 营养计算需求。手动交付只累计实际入包数量；搬运数量按当前食物营养折算，并扣除同批其它搬运者已携带的食物。最后一件向上取整，不拆分单件食物。
- 旧存档已经交付的简单餐份数继续抵扣，新增的营养交付记录可保存重读；不会把来客原有背包食物追认为交付。

1.7 离线回归 709 项、结构检查和真实游戏程序集交付探针通过；探针覆盖搬运数量、食物实际入包、旧份数抵扣及信件补足，地图、寻路和营养属性使用替身。另通过真实菜单控件的 26 项交互断言，覆盖三种分类、两处滚动位置、搜索、复选框、批量按钮和中英日绘制；录制使用合成字体测量和占位资源，不是游戏截图，实际字体与完整模组食物列表仍待游戏内验收。本版更新 1.7，保留 1.6 已编译内容。

### 围攻乞食补充修复

- 信件交粮会依次尝试本批合格收货者，不再因第一名来客不可送达或容器拒收而误拒整批交付。保留有效仓储、禁止、可达性及同组成员可食要求；当前可交付的食物见上方索粮名单。
- 已结算武装请求遗留的右侧通知在游戏内下一次整点清理，不重新扣粮、发动进攻或改变原结算结果。普通请求的目标地图已经消失时直接关闭，不再为确认离场遍历全世界角色。
- 请求名单按一次世界角色快照批量解析，不再每个人重新构建并遍历世界名单；保持原名单顺序、重复 ID、死亡与销毁过滤，以及携带或容器内角色的原有判定。不缓存跨 Tick 的角色状态，不改进攻或离场时机。

补充修复阶段的 1.7 离线回归 698 项通过；真实游戏程序集机制探针覆盖死亡、地图移除、到期后连续三日 Tick、旧信清理和食物实际入包，地图和寻路边界使用替身，尚非玩家存档或逆重飞船实测。连续三日推进未出现世界角色查询或请求数量增长。另在 2000 名世界角色、每次查询 40 人的同机机制测量中，单次查询约从 474µs 降至 14µs，世界名单构建由 40 次降为 1 次；这不是玩家卡顿原档的性能结果。玩家报告的持续 1.6–2ms Tick 占用仍未归因，需要可复现卡顿的原存档及对应模组配置。不将消息清理或预防性优化等同于性能问题已经解决。

## 1.1.3 · 分娩、设置与乞食修复

- 多崽的额外孩子改在原版产程移除之后生成，并按原版出生入口安置；额外生成失败不再让产程留在健康列表反复移除。已经缺失父母登记的旧卡住产程不自动重生孩子，以免重复生产；这类存档仍需首次异常日志定位。
- 原版设置窗口改为单列纵向滚动。IrisMenus 页改用宿主的内容坐标可见区，修复非 100% UI 缩放下的裁剪错位；旧 IrisMenus 没有此接口时只关闭额外裁剪，仍保留宿主滚动。
- 围攻乞食交付改查殖民者能否送达，不要求来客能打开仓库门。手动交付的简单餐按实际入包量累计，可与信件交付合并补足。
- 玩家攻击会启动该批来客的索粮反应。最后一名可行动来客死亡、倒地、被俘、加入殖民地或离开 Lord 后关闭请求并移除通知；旧档残留按游戏内每小时检查。普通难民全部失去来客身份后也关闭残留请求。
- 更换远程武器后通过 CE 公开接口补充装填和一匣备用弹。所提供 CE 版本只声明支持 1.6，尚未验证 CE 在 1.7 的实际装填和战斗。

此版本已部署至本机 1.7 Mods，保留 1.6 已编译内容。构建与机制验证针对 1.7，设置字体与点击、完整分娩及复杂模组组合仍待游戏内复测。事件后每帧 4ms 的渲染反馈未取得可归因测量；后续新增的 Tick 性能反馈见上方补充修复。

## 未部署 · 信件与远行队修复

- 修复武装索粮信答复后窗口仍留在屏幕上的问题。“无视”或“拒绝”后可以关窗，请求仍保留，之后可重新打开交粮止战；交付失败时保留选择窗口。
- 大灾荒与鼠疫大灾荒成功入图后会来信，并可定位到场鼠族。其它没有选择信的来客同样补发入图通知；已有选择信的事件保持一封入图信。
- 鼠族远行队默认按原速度的 2 倍赶路，可在原版设置或 IrisMenus「实验」页调整为 0.5x～4x，在途队伍立即生效。1x 平地约每天 3 格，玩家远行队速度保持原样。
- 核对 RimWorld 1.7 XML 字段。当前源码已经使用 `thoughtClass`，没有截图中的 `isMemory` 字段；静态检查会拦截心情定义中的未知字段。

本次仅修改源码并做静态检查，不构建或部署，1.6 与 1.7 已编译内容保持不变。

## 1.1.2 · 生育遗传修复

- 「多崽」额外出生的孩子逐个使用原版父母基因抽样，不再合并父母的全部内源基因，也不再补装异种定义中的整套基因。外源基因仍不遗传，原版的基因冲突与代谢限制继续生效。
- 额外孩子的异种身份按原版分娩规则设置：同一可遗传异种保留身份，不同可遗传异种显示为混合，修复母亲携带「多崽」时额外孩子误显示为智人的问题。保留同种自定义异种的名称和图标。
- 「巢居易孕」受孕时保存父母和原版遗传基因快照，后续正常进入原版妊娠与分娩流程。

本次部署更新 1.7，保留 1.6 已编译内容。修复作用于之后的受孕和额外孩子生成，不回改已经出生的孩子。

Extra litter children now sample inherited genes separately through vanilla inheritance and receive vanilla birth xenotype identities, instead of merging all parental endogenes or installing a full xenotype genome. Same heritable xenotypes retain their identity; different heritable xenotypes produce hybrids. Room-fertility conceptions store the parents and inherited gene set at conception. This deployment updates 1.7 and preserves the existing 1.6 binaries; existing children are not rewritten.

## 1.1.1 · RimWorld 1.7 更新（剧情说明为历史记录）

本节的剧情暂停、保留及强制启用说明属于旧版行为；当前已移除剧情和强制启用入口。

- 穗音剧情任务线默认隐藏并停止刷新，包括信任触发剧情、累计事件处理来信和王国篇。信任继续累计，仅用于袭击概率修正；已有剧情记录和人物保留。加载存档后可在模组设置，或 IrisMenus「开发者」页，勾选「强制启用穗音剧情（当前存档）」；此开关只影响当前存档。
- I-053「祈求加入」两次接受触发至少间隔 15 年（900 天），旧档保留上次触发时间。
- I-024 食物交易改为检查殖民者能否把仓库食物送到来客处，来客无法进入仓库不再单独阻止成交；食物和奖励仍按实际数量交付。
- 已经吃过的来客再次饥饿时可以继续接收简单餐、干肉饼等食物；历史投喂记录保留，不重复结算首次吃饱的效果。
- 配给厅世界地图图标改用 1.7 中实际存在的原版贴图。

当时的行为变更仅适用于 1.7；下文旧版剧情和三年冷却说明为历史记录。

In RimWorld 1.7, Suiyin's storylines are hidden and paused by default. Trust still accumulates and affects raid probability, but does not trigger story or cumulative-incident letters. Existing story records and people are preserved. Enable the per-save force option in mod settings or IrisMenus' Developer page to resume stories. Pleading Join has a fifteen-year cooldown. Food trades use colonist delivery access, hungry visitors can receive food again, and the kingdom site's world icons use available 1.7 textures.

## 中途加入 / Adding mid-save

支持在已有存档中加入本模组及所需前置。加载完成时会补齐缺失的五个态度派系，保留已有派系并固定它们对玩家的好感，不需要先推进游戏时间。不导入“鼠灾-大荒年”或其 Continued 的存档数据，也不能与这些模组同时启用。

This mod and its required dependencies can be added to an existing save. Missing attitude factions are created when loading finishes, and existing factions are kept without duplicates. Their goodwill toward the player is fixed before game time advances. Save data from Great Famine Year or its Continued versions is not imported; do not enable those mods alongside this one.

## 1.0.4 存档兼容

从 1.0.3 升级时，旧的单人投喂等待记录会在加载时自动转为新的等待名单，保留接收者、请求数量和等待期限，不需要手工编辑存档。再次保存只写新格式；原存档不会在加载时被覆盖。“仅此存档停用新内容”仍保留，也不会阻止已有记录迁移。

历史剧情记录：王国篇 K1～K5 曾用于背景与初步合作，由穗音设置页或 IrisMenus 手动开启；当前已移除。

## 事件链存档兼容 / Event-chain save compatibility

事件后续和捕食者的延迟到达统一由 Oxygen DSL Framework 执行；本模组不再运行旧事件链。已有饥与祸存档在加载时自动迁移，保留实例编号、阶段、开始时间、期限、已选结果和载荷，不重新抽取随机结果。旧的长事件保持原来的检查时点；已经完成短阶段的记录不会再执行一次。迁移成功后保存新格式，加载过程不覆盖原档。

旧记录损坏时保留原记录和待到达时间，暂停本模组事件链，并在日志和 Oxygen DSL Framework 的运行检查里显示原因。不要手工删记录来继续。已有的新格式记录优先，包括明确为空的记录；旧记录不会再次导入。

“仅此存档停用新内容”也会停下事件后续和尚未到达的捕食者，保留事件记录、已有来客、信件和世界地点。导出卸载用副本只移除饥与祸自己的事件记录，保留 Oxygen DSL Framework 和其它模组的数据。

Oxygen DSL Framework is required and must load before this mod. It is the only runtime for event follow-ups and delayed predator arrivals. Existing Hunger and Havoc saves migrate automatically, keeping instance IDs, stages, start times, deadlines, payloads, and selected results without another random roll. Long follow-ups keep their original check timing, and completed short stages do not run again. Successful migration saves only the new format; loading does not overwrite the original file.

Malformed legacy data is kept, including pending arrivals. Hunger and Havoc chains pause with a reason in the log and Oxygen DSL Framework's runtime inspection. Existing new-format state takes precedence, even when explicitly empty; legacy records are not imported again. Turning off new content also stops follow-ups and pending arrivals while keeping existing people, letters, places, and event records. Removal exports delete only Hunger and Havoc's event state. Keep Oxygen DSL Framework installed, along with the other required dependencies.

## 事件去同质化

自然事件与广播按 23 类选择，再决定人数、年龄、染疫等具体场景。旧事件身份仍保留；“难民接济（婴儿）”退出新事件池，已有请求和后续继续。

- “做个交易”：来客带着白银、零部件、药品等物资换食物。报价使用仓库里数量最多的合格食物，生食和熟食都可；按实际人数折算营养，最多 100 件。成交时当场交付实物，附赠情报可自行决定是否探索。升级前已打开的情报信仍按原报价和地点奖励处理。
- “路过的鼠族”：从入场边走到地图对边离开，不再到居住区停留乞讨。已有来客自动接上穿图路线；收留、囚禁或已下达离场令的成员保持原处理。
- “易子而食”：可选择实际殖民地孩子交接，或按设置交餐。成功后来客孩子加入殖民地，成人离开。患有鼠疫的孩子不能交接。
- 已登记的八条长后续有实际人物、选择与后果，包括鼠妈回访、旧窝记物、换子追问、空投认领、认亲依恋与孤儿自称身份。新配置默认开启遗弃孩子和流民商队两类（含鼠疫场景），其它长后续可在事件类设置中开启。升级保留已经保存的选择，包括全部短后续；既有长事件照常继续。

事件设置同步为 23 类。升级时旧逐事件配置恢复为类默认值，其它设置保留。历史剧情记录：当时曾调整穗音来信、日记、资助、粮洞、使者、记录取舍与身份揭示；这些穗音剧情内容现已移除，普通来客的安置记录保留。

鼠疫来客的病情、照料需求和传播风险并入主事件信，使用原事件的处置选项。独立检疫信、留观流程、专属信任结算和康复回访已移除；旧档加载时自动清理对应记录并解除检疫留驻，原文件保留。鼠疫疾病和传播仍按原规则运行。

Plague arrivals describe their illness, care needs, and transmission risk in the main event letter and use that event's existing choices. Separate quarantine letters, observation holds, quarantine trust outcomes, and recovery visits have been removed. Existing saves automatically discard those retired records and release quarantine holds without overwriting the original file. The disease and its spread retain their existing rules.



## 年度荒年与新事件

年度荒年默认关闭。开启后每年首日当地 06:00 判定，默认 40% 为荒年；首次年中开启会补判并来信，不导入旧鼠灾数据。荒年每日 06:00 默认有 20% 概率尝试寒流、热浪、毒尘或火山冬天，本年最多成功一次；两项概率均可调。

荒年多人事件在原人数限制之后乘 1×～4×，默认 3×；单人、母子和固定家庭场景保持原规模，难民营同样按倍率扩展。鼠潮单独维持 15～50 人，不再乘倍。

- **I-052 鼠潮**：仅在启用年度荒年且本年为荒年时触发，每年最多两次，至少间隔 15 天。未处置时为中立，优先偷取高价值物品、可搬走建筑和食物；不能搬走的贵重建筑会被啃咬。驱赶、攻击、整批俘虏或奴役选择会停止原行为，改为敌对反抗，而不是直接收监整批。
- **I-053 祈求加入**：非荒年专属，三年（180 天）冷却。单只成年鼠族，所有技能随机 10～20，具有善良、乐观、钢铁意志和工作勤奋四个有效特质，无初始社交关系；不提供交付食物选项。
- **I-054 盗匪袭击**：固定敌对，沿用袭击 Lord 的偷窃和绑架；必须携带既有白名单中的原版武器，品质为极差至普通，信件只有关闭选项。

通讯台“广播希望”仍需完成实际作业。普通年请求一条回应，荒年请求倍率向上取整条（默认三条），不足时按实际接受数回执；不重复已排队事件，仍遵守稀有事件限额。只有成功排队才开始冷却，默认 3 天，可调 0～10 天，目标固定为通讯台地图。鼠潮与祈求加入在接受触发时占用限额，后续赶路失败不退还。

## 来客行为心情

“饥荒来客心情”默认开启。以下追加效果适用于本模组来源、具有心情需求的鼠族，加入殖民地后仍适用：

| 行为 | 心情 | 持续 |
| --- | --- | --- |
| 收留加入或临时庇护 | +12 | 24 天 |
| 拘捕为玩家俘虏 | -8 | 3 天 |
| 奴役 | 各半概率：+2 或 -2 | +2 为 24 天；-2 永久 |
| 驱赶 | -8；若实际转为敌对则 -16 | 3 天 |
| 吃到单份营养值低于精致餐的食物 | +6 | 1 天 |
| 吃到单份营养值不低于精致餐的食物 | +12 | 1 天 |

进食两档互相替换，重复进食刷新期限。原版精致餐、简单餐、营养膏的单份营养值均为 0.9，因此均获得 +12；原有“总算吃饱了”心情另行保留。关闭该设置时，已有的上述心情也停止生效。

## 本次修复 / Current fixes

- 卸载导出会清掉其它模组语音缓存中引用的饥与祸心情，保留其它心情和语音记录。
- 商队劫掠发生后发送红色袭击信，定位遭遇地图上的袭击者。
- 敌对来客接受送粮等待后转为中立；收齐一人的份额时，其余来客继续等自己的食物。可战斗的敌对成年鼠族会攻击殖民者。
- 短工、长工和临时征募到期后恢复访客身份。因年龄还不能走路的孩子照常到期，有合格的同组照护者时由其带走；因伤病倒地的来客继续暂停期限。永久加入后清除临时期限。
- 短工、长工和临时征募吃饱后保留合同期限，按殖民地工作安排继续工作。已经永久加入的成员可正常工作，即使旧档还留着临时期限。
- 灾民收齐食物后会结束等待，优先吃自己背包里的合格食物。背包食物仍受取食种类和饮食限制。
- 广播回应固定前往广播所在据点，包括最早建立的地图。切换查看另一据点不会改变目的地。
- IrisMenus 1.1.1 可正常显示本模组的分类设置。
- 1.0.6 的旧长后续会在事件链激活前迁移，旧返访期限不会被误判为事件到期；停用新内容的存档也会迁移，保存后只写新格式。旧设置产生的无具体长后续记录保留，不阻塞其它事件。
- 等待孩子成年、返访或地图准备时不再累积行动记录；关闭武装索粮后，历史请求不再反复解析来客名单。
- 历史剧情修复：卸载导出时，王国站点剩余粮食进入接收者背包；王国篇现已移除。
- “耗子分妈”和母亲乞讨事件会建立真实母子关系；旧档按双方已保存的母子 ID 补齐，已有母亲关系保留。
- 事件鼠蛋可通过殖民者右键菜单或人物操作栏的“收养”永久加入；两处都会结束访客停留，收养后继续保留玩家派系。
- 右键菜单提供“驱逐全部”（目标的同批来客）和“只驱逐某人”。心情与信任按实际驱逐人数计算；鼠潮仍会反抗，单只操作只激怒目标本人。
- 历史剧情修复：穗音身份开关的搜索定位和王国文书贴图曾得到修正；这些内容现已移除。

Removal exports clear Hunger and Havoc thought references from other mods' speech caches while keeping their other records. Caravan attacks send a red threat letter targeting the attackers on the encounter map. Hostile visitors become neutral when they accept a food handoff, and each recipient waits for their own share. Combat-capable hostile adult ratkin can attack colonists.

Temporary stays expire for children who cannot walk because of their age; an eligible adult in their group carries them away. Injury or illness still pauses the stay. Permanent joining clears the temporary deadline. Broadcast responses keep the broadcasting colony as their destination, including the first map. IrisMenus 1.1.1 displays the categorized settings again.

Meals preserve temporary workers' contract deadlines, and workers can continue their assigned work. Permanently joined colonists can work even if an old save retains an expired temporary contract. After a food handoff, visitors stop waiting and look for an acceptable meal in their own inventory. Food settings and dietary restrictions still apply.

Legacy long follow-ups migrate before event-chain activation, including disabled saves, and subsequent saves use only the new format. Waiting no longer accumulates action receipts; settled armed requests no longer repeatedly resolve visitors. Mother-and-child incidents establish real parent relations, and saved reciprocal family IDs repair missing relations without replacing an existing mother. Earlier kingdom-site, document-texture and identity-toggle fixes are historical; those story features have now been removed.

Event-spawned young ratkin can join permanently through either the colonist's right-click menu or the pawn's Adopt command. Both end their visitor stay and preserve player faction membership. The right-click menu separates expelling the target's entire group from expelling only the named visitor. Mood and trust changes follow the actual recipients. Mouse swarms still resist; a single-target order provokes only that visitor.

历史剧情记录：五条普通事件结局先前已停用；当时保留的穗音来信、奖励、身份揭示及王国篇也已在本次移除，当前仅保留信任及普通事件的安置观察。

Historical story record: the five ordinary-event endings were retired earlier. Suiyin's letters, rewards, identity revelations and kingdom arc, retained at that time, have now also been removed. Trust and ordinary-event settlement observation remain.

## 设置

原版模组设置页和 IrisMenus 总览页都有“重置本模组配置”。确认后恢复全部默认配置并保存；存档中的信任、安置记录、来客和仅此存档停用状态保留。

IrisMenus 在「穗音信任」旁保留「安置进度」：左栏按记录日和事件展开来客队伍，选中后右侧显示该队伍的姓名、剩余时间、营养不良暂停及失踪等情况，并可手动刷新。记录日是开始追踪的日期；来源缺失时显示不详。各存档独立保存记录，队伍标签不再把内部标识显示成“第几批”。普通救济与换来的孩子按在场累计存活超过 3 天判定安置；营养不良暂停累计，康复后继续，普通饥饿、鼠疫等其它疾病及囚禁不阻断。在场者可继续等待，母子托付的安置观察仍按成长和去向推进。旧档保留已有照护累计和已完成结果；普通来客过去未记录的有效时间从升级后的首次观察起累计。

IrisMenus keeps Settlement progress beside Suiyin trust. Expand recorded days and events in the left sidebar, then select a group to see its names, time remaining, malnutrition pauses and whereabouts on the right. Refresh is available. The recorded day marks the start of tracking; missing sources are shown as unknown. Records belong to each save. Ordinary relief and exchanged children settle after more than 3 accumulated days alive at the colony. Malnutrition pauses the timer; recovery resumes it. Hunger alone, plague, other illnesses and captivity do not block it. Present visitors can keep waiting; entrusted-family settlement observation keeps its growth and whereabouts rules. Existing care progress and completed results are preserved; previously unrecorded time starts accumulating after the upgrade.

开发者事件页保留原场景的“即时刷新”，退出新池的婴儿索取除外。直接生成到当前地图，跳过排队和世界地图行进，窗口暂停时也可用。商队劫掠直接生成袭击者，危险的鼠族安居点直接生成居民，不要求派出远行队。原有“触发”仍走正常流程；新内容和类开关仍生效。

新增“母爱本能”“父爱本能”和“同类相食”基因，可在 IrisMenus 的基因页选择追加给新来客。母爱仅在哺乳期生效：默认产奶速度与储奶上限均为150%，饥饿速度115%，营养上限125%。父爱在同图有活着的直接亲生子女时生效，子女年龄不限：默认工作速度120%、饥饿速度110%、全局学习速度110%、承伤90%。七项数值均可调整；普通倍率范围1%至1000%，承伤范围1%至100%。同时携带父母两种基因时，雌性保留母爱，雄性保留父爱；单独携带时不受性别限制。

“同类相食”豁免食尸、食用人肉和观察尸体造成的相关负面心情及文化戒律限制，包括观察腐尸。屠宰、人皮衣物和普通生食仍遵循原有规则。

## 文案

50 条特质、121 条编号经历及两条基础经历已审校并同步中英。事件和穗音文案保留人物说话与情绪，价格、期限和人数不再写死默认值；必要风险另列提示。英文中的鼠蛋称为 young ratkin，不是蛋。

鼠族姓名库新增 179 个现实花卉名称，如芍药、铃兰、耧斗菜、葡萄风信子。中文花名为 2～5 字，并提供英文、日文对应名称；与 NewRatkinPlus 原有花草、茶名规则共同抽取，适用于使用这套姓名规则的新生成鼠族，已有角色姓名保留。

The ratkin name pool now includes 179 additional real-world flower names, with Chinese, English, and Japanese versions. Chinese entries contain 2–5 characters. New names join NewRatkinPlus's existing naming rules for newly generated ratkin; existing characters keep their names.

中文 Keyed 的设置提示、来客信、长后续、穗音来信、结局和王国篇已清理空泛总结、指向不明的省略句和无来由的否定解释。玩法条件、数量与期限保留；“易子而食”的中文提示按现有孩子交接和食物替代方式说明。旧存档中已经打开的选择信仍保留原来的正文。

日语覆盖当前设置、事件、穗音来信与结局、特质、经历、心情、物品和 Biotech 基因，另有旧模组冲突提示。游戏语言选择日本語即可使用。鼠族沿用「ラットキン」，鼠蛋称为子供或子ラットキン，不译成卵。旧档中已经打开的选择信仍保留打开时的文字。

语言 XML 已按通用操作、设置、菜单、访客、事件、穗音叙事和王国篇分类。设置说明与故事正文分开，经历和心情另按内容拆分；既有键名与译文保留。菜单的标签与数值、分组数量、事件搜索分类和未知版本提示均使用中英日翻译。

“易子而食”的孩子选择与食物替代均可交接，原有选择信保存的正文继续保留。历史存档未记录的原母亲或交出孩子身份无法还原，对应后续会结束，不会用其它角色顶替。

## 结构

- `1.6/`：保留的 1.6 程序集、Def、Patch
- `1.7/`：1.7 的 .NET 10 程序集、Def、Patch 与 Guard
- `Guard/`：1.6 与旧鼠灾包冲突时只加载提示，不加载主体
- `docs/adr/`：设计决策
- `docs/agents/`：协作、问题跟踪与自动化说明
- `docs/references/`：存档契约、事件参考、背景与王国篇设计；作者原稿保留原文，不代表已实施机制
- `Source/`：SDK-style 工程，默认构建 1.7，输出到 `1.7/Assemblies/`

其它模组请使用 `HungerAndHavoc.Api.RHAH_Api`，不要扫描经历或私有类型。

实现、默认值和配置以源码、Def 与项目文件为准。根及就近 `AGENTS.md` 只记录项目或目录约束；文档保留设计理由、剧情稿、[存档与卸载契约](docs/references/save-ownership.md) 及必要操作说明。[缺陷与实施工作项](docs/agents/issue-tracker.md) 统一通过本仓库 GitHub Issues 跟踪。

## 构建

退出 RimWorld 后，从 monorepo 根目录执行统一入口：

```bash
bash scripts/deploy.sh ratkin_hunger_and_havoc
```

`--build-only` 仅构建并暂存根 `artifacts/ratkin_hunger_and_havoc/release`。RHAH 专用 CI、发布与调查操作仍由模块内流水线维护，开发自动化与 MCP 调试见 [自动化说明](docs/agents/automation.md)。

默认构建和部署 RimWorld 1.7，使用游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 与对应 1.7 前置。1.6 的程序集和加载分支保留，不在 1.7 构建时重编译。显式构建 1.6 时使用 `--rimworld-version 1.6 --rimworld-dir <1.6 游戏目录>`，并通过 `--oxygen-dsl-dir` 指向保有 1.6 构建产物的 ODF 目录。

只检查仓库结构：

```bash
python3 scripts/verify-scaffold.py
```

