# 鼠族: 饥与祸

<!-- 只放面向用户的说明!!! 
面向开发者/Agent的放在开发者贡献文档或 Agents.md
-->

RimWorld 1.6 模组。`packageId`：`nanaloveyuki.ratkin.hungerandhavoc`。

需要 NewRatkinPlus、Harmony、Biotech 和 Oxygen DSL Framework（`nanaloveyuki.oxygendsl`）。Oxygen DSL Framework 必须排在本模组前面。IrisMenus 用于分页设置。

独立作品，不是“鼠灾-大荒年”的 Continued。Def 前缀 `RHAH_`，事件显示 ID 用 `I-`，剧情用 `N-` / `J-` / `R-`。
仅卸载本模组前，在已加载存档的设置里先停用这个存档的新内容，再备份并导出卸载用副本。退出后只卸本模组，保留鼠族、Harmony、Biotech 和 Oxygen DSL Framework，然后加载 `RHAH-removed-*`。`RHAH-backup-*` 仍含本模组数据，加载它必须留着本模组。

## 中途加入 / Adding mid-save

支持在已有存档中加入本模组及所需前置。加载完成时会补齐缺失的五个态度派系，保留已有派系并固定它们对玩家的好感，不需要先推进游戏时间。不导入“鼠灾-大荒年”或其 Continued 的存档数据，也不能与这些模组同时启用。

This mod and its required dependencies can be added to an existing save. Missing attitude factions are created when loading finishes, and existing factions are kept without duplicates. Their goodwill toward the player is fixed before game time advances. Save data from Great Famine Year or its Continued versions is not imported; do not enable those mods alongside this one.

## 1.0.4 存档兼容

从 1.0.3 升级时，旧的单人投喂等待记录会在加载时自动转为新的等待名单，保留接收者、请求数量和等待期限，不需要手工编辑存档。再次保存只写新格式；原存档不会在加载时被覆盖。“仅此存档停用新内容”仍保留，也不会阻止已有记录迁移。

王国篇 K1～K5 初步合作已接入。仅在穗音下由设置页或 IrisMenus 手动开启，不自动触发。接受后的寄件与商队行动在切换叙事者后仍按约定期限继续；新的委托及穗音私人回信等她回来。调查使用实际文书与免费副本，公开、补发和护送按真实交接结果回复，第五幕不是终局。既有信任及普通 I/N 事件不变

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

事件设置同步为 23 类。升级时旧逐事件配置恢复为类默认值，其它设置保留。穗音来信已依据作者的小故事调整：她会讲宫墙外的见闻、日记、有限资助和自己的犹疑。普通来访记录可在 IrisMenus 主动查看；交换答复、封洞、使者交易和记录取舍等直接结果改为短消息，重要线索、照料结果与身份揭示继续来信。已有选择信保留打开时的正文。

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
- 卸载导出时，王国站点剩余粮食会进入接收者背包，保留原有堆叠数量。
- “耗子分妈”和母亲乞讨事件会建立真实母子关系；旧档按双方已保存的母子 ID 补齐，已有母亲关系保留。
- IrisMenus 穗音搜索中的身份开关使用独立条目 ID 和定位锚点，不再使搜索索引失败；王国文书贴图路径改为原版资源的实际路径。

Removal exports clear Hunger and Havoc thought references from other mods' speech caches while keeping their other records. Caravan attacks send a red threat letter targeting the attackers on the encounter map. Hostile visitors become neutral when they accept a food handoff, and each recipient waits for their own share. Combat-capable hostile adult ratkin can attack colonists.

Temporary stays expire for children who cannot walk because of their age; an eligible adult in their group carries them away. Injury or illness still pauses the stay. Permanent joining clears the temporary deadline. Broadcast responses keep the broadcasting colony as their destination, including the first map. IrisMenus 1.1.1 displays the categorized settings again.

Meals preserve temporary workers' contract deadlines, and workers can continue their assigned work. Permanently joined colonists can work even if an old save retains an expired temporary contract. After a food handoff, visitors stop waiting and look for an acceptable meal in their own inventory. Food settings and dietary restrictions still apply.

Legacy long follow-ups migrate before event-chain activation, including disabled saves, and subsequent saves use only the new format. Waiting no longer accumulates action receipts; settled armed requests no longer repeatedly resolve visitors. Removal exports preserve remaining kingdom-site food stacks in recipients' inventories. Mother-and-child incidents establish real parent relations, and saved reciprocal family IDs repair missing relations without replacing an existing mother. Suiyin's identity toggle has a unique IrisMenus search ID, and kingdom documents use the correct base-game texture path.

原来的五条普通事件结局已停用，旧结局设置和预览入口移除。穗音信任、普通来信、奖励与身份揭示保留；旧档中的历史结局和已经收到的信件继续保存。王国篇 K1～K5 用于背景与初步合作，远期政治结局仍在规划中。

The five ordinary-event endings have been retired, along with their settings and previews. Suiyin's trust, ordinary notices, rewards, and identity letters remain. Existing ending history and received letters are kept. K1–K5 cover background and early cooperation; later political endings are still being planned.

## 设置

原版模组设置页和 IrisMenus 总览页都有“重置本模组配置”。确认后恢复全部默认配置并保存；存档中的剧情进度、来客和仅此存档停用状态保留。

IrisMenus 在“叙事”旁新增“安置进度”：左栏按记录日和事件展开来客队伍，选中后右侧显示该队伍的姓名、剩余时间、营养不良暂停及失踪等情况，并可手动刷新。记录日是开始追踪的日期；来源缺失时显示不详。各存档独立保存记录，队伍标签不再把内部标识显示成“第几批”。普通救济与换来的孩子按在场累计存活超过 3 天判定安置；营养不良暂停累计，康复后继续，普通饥饿、鼠疫等其它疾病及囚禁不阻断。在场者可继续等待，母子托付仍按成长和去向推进。旧档保留已有照护累计和已完成结果；普通来客过去未记录的有效时间从升级后的首次观察起累计。

IrisMenus has a Settlement progress subpage beside Narrative. Expand recorded days and events in the left sidebar, then select a group to see its names, time remaining, malnutrition pauses and whereabouts on the right. Refresh is available. The recorded day marks the start of tracking; missing sources are shown as unknown. Records belong to each save, and internal group identifiers are no longer presented as visit counts. Ordinary relief and exchanged children settle after more than 3 accumulated days alive at the colony. Malnutrition pauses the timer; recovery resumes it. Hunger alone, plague, other illnesses and captivity do not block it. Present visitors can keep waiting; entrusted families retain their growth and whereabouts rules. Existing care progress and completed results are preserved. Unrecorded effective time for ordinary visitors starts accumulating after the first post-upgrade observation.

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

- `1.6/`：当前版本的程序集、Def、Patch
- `Guard/`：与旧鼠灾包冲突时只加载提示，不加载主体
- `docs/adr/`：设计决策
- `Source/`：SDK-style 工程，输出到 `1.6/Assemblies/`

其它模组请使用 `HungerAndHavoc.Api.RHAH_Api`，不要扫描经历或私有类型。

实现、默认值和配置以源码、Def 与项目文件为准。根及就近 `AGENTS.md` 只记录项目或目录约束；文档保留设计理由、剧情稿、[存档与卸载契约](docs/save-ownership.md) 及必要操作说明。[缺陷与实施工作项](docs/agents/issue-tracker.md) 统一通过本仓库 GitHub Issues 跟踪。

## 构建

退出 RimWorld 后：

```bash
scripts/deploy.sh
```

脚本通过结构检查、Release 构建和测试后，部署到游戏 `Mods/RatkinHungerAndHavoc` 并核对 SHA-256。游戏目录用 `RIMWORLD_DIR`。开发自动化与 MCP 调试见 [自动化说明](docs/automation.md)。只检查仓库结构：

```bash
python3 scripts/verify-scaffold.py
```

