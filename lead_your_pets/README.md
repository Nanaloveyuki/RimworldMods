# 牵着你的宠物 Continued

原作者：乐织终。Continued 维护：Nanaloveyuki。原作者于 2026-09-09 许可续作维护并要求保留署名，见 NOTICE。

## 兼容范围

- 保留原版玩法、Def、`LeadYourPet` 程序集与命名空间、存档类型、Scribe 字段及枚举值。源码按 Core、Leash、MouseEgg、Interaction、Patches、Compat 分目录，类型全名仍是 `LeadYourPet.*`。
- 修复鼠灾原版 / Continued 的可选联动：包名检测、生成方法重载与可选参数、访客敌对入口。
- 鼠灾 Continued 需安装同时更新的版本，才能主动识别本模组的新包名。
- 新包名：`nanaloveyuki.leadyourpet.continued`。`About/PublishedFileId.txt` 是本续作的创意工坊文件 ID，不是原版 ID。
- IrisMenus（`Nanaloveyuki.IrisMenus`，1.6 / 1.7）可选。运行时检测到已启用且声明支持当前游戏版本，并且公开菜单 API 存在时，用反射登记牵引、幼年鼠族、年龄、来客、离图、主人喂食六页，写入同一套设置。主程序集不引用 IrisMenus.dll。未安装、未启用、游戏版本不匹配或 API 对不上时不登记，原版设置窗口照常可用。不写入 `modDependencies`。
- 饥与祸通过 `LeadYourPet.LeadYourPetApi` 调用。本模组不检测饥与祸，也不引用它的程序集。
- PA’s God Hands（`Palpha.godhands`）可选。抓取鼠蛋、牵引者或锚定 Pawn 时暂停牵引和互动动画，不完成旧动画的传送；隐藏手持期间的绳子，其他超出硬绳长的绳子也不绘制。释放后恢复牵引，并清理相关 Pawn 遗留的神之手 `Wait(99999)`，不结束普通等待或无关 Pawn 的任务。主程序集不引用 PA_GodHands.dll。
- 访客的离场集合与入场集合分开识别；1.7 也识别独立 `TravelAndExit` 的集合阶段。原版 Lord 控制牵引者离场，同 Lord 的旅行鼠蛋继续实际跟随；失能成员保留 Lord 归属，由搬运流程处理，搬运不抢战斗、紧急任务或外部幼儿抱持。主人离图时仅出口附近成员同步离开，远处可移动成员通过退出 Job 走到出口，不套用玩家的「留在原地」设置。

## 替换原版

1. 备份旧存档，退出游戏。
2. 禁用原版，只启用 Continued，保持其他模组与加载顺序不变。
3. 用存档副本检查牵引、锚定、鼠蛋归属、母婴心情、交易和访客；另存后退出并重新读取。

重复启用时，Guard 将原版从启用列表移除并提示重启。本次启动暂不加载续作同名内容，重启后使用续作；不删除原版、不修改存档。模组设置按安装目录存储，换目录后可能需要重新设置绳长等选项。

结构兼容测试不是实机旧档验收。未宣称任意模组组合、任意历史版本存档绝对兼容，也不承诺在进行中的游戏里直接卸载。

## 主人喂食设置

IrisMenus 下新增「主人喂食」SubItem，列出所有已加载、提供营养且能用于喂食的食物。默认勾选原版及官方扩展来源，模组食物逐项启用；设置按食物名称保存，后来加入的模组食物仍默认关闭。药物、尸体和食物分配器不属于可喂食物品。

可搜索食物名称、内部名称、模组名称和包名，并按 Mod、A–Z（内部名称首字母）、原版/模组来源分类。IrisMenus 全局搜索也能定位到具体食物；没有 IrisMenus 时，从原版模组设置的「主人喂食」按钮打开同一套列表。

手动喂食、自动喂食和主人进食时的分享均遵守该列表；库存优先，地图选食在允许的食物中选择，不会因更优先的食物被禁用而漏掉其余食物。饮食政策、禁用物品、腐败和可达性等原有选食限制保留。

验证：Release 构建零警告、零错误；针对生产程序集的 10 项 headless 检查通过，覆盖默认来源、逐项覆盖、新模组默认禁用、药物拒绝、搜索、A–Z 分类和实际 IrisMenus DLL 的公开 API 绑定。未运行 Unity 菜单画面或实机存档验收。

## 可牵引年龄

IrisMenus 的「年龄」页及原版模组设置新增「不限制牵引年龄」。开启后，动物和智人均忽略年龄上下限，成年囚犯也可使用现有宠物牵引流程；宠物开关、距离、可达性和其他牵引条件仍有效。关闭后恢复原有的生物年龄范围，下限包含、上限不包含，默认 0 至未满 14 岁。也可直接提高上限，例如 40 表示未满 40 岁。该开关不改变角色实际年龄，原有范围值继续保存。

验证：Release 构建零警告、零错误；实际生产方法的 6 项 headless 检查通过，使用 30 岁智人囚犯覆盖默认拒绝、提高上限、上限边界、取消年龄限制、宠物开关仍生效及恢复下限。未运行游戏内牵引动画或实机存档验收。

## 成人牵引与中英文案

- 恢复自动互动冷却：玩家牵引间隔 1800–3600 tick，非玩家 4200–7200 tick；没有可用互动时也进入冷却。牵引者征召、移动或睡眠时暂停随机互动，手动互动保留。
- 修复互相牵引形成的控制环：新牵引方向优先，释放新牵引者的旧反向牵引；普通链式牵引和一人牵多个目标保留。加载已有控制环时按创建时间恢复，保留最新方向；解绑同时结束对应跟随任务并取消旧动画。
- 智人菜单、头顶互动、任务报告和共享心情说明改用年龄中性的中英文案，不再把成年人称作孩子或鼠蛋。真正的母婴、子女和生成鼠蛋内容保留相应称谓；鼠族为 `Ratkin`，鼠蛋为 `Young Ratkin`。

English: Automatic interactions now respect their cooldown and pause while the leader is drafted, moving or sleeping. Reversing a leash breaks reciprocal control; loading a cyclic leash relationship keeps the newest direction. Adult-facing menus, interaction labels, job reports and shared thoughts use age-neutral wording in English and Simplified Chinese. `Ratkin` names the race; `Young Ratkin` names the young pawns.

验证：Release 构建零警告、零错误；Mono headless 回归 158 项通过、0 项失败，包含新增 8 项冷却、征召/移动、方向反转、链式牵引和旧存档控制环回归。生产程序集 smoke 覆盖冷却暂停/恢复、30 岁智人反转牵引，以及中英各 20 条成人殖民者互动、20 条成人非殖民者互动和 3 条任务报告；125 个 Keyed 键两语言一致，DefInjected 路径有效。临时游戏 DLL 仅移除 UI/配置静态初始化器，未替换寻路或任务行为。没有用户存档，不能确认其反向移动是否来自控制环；未运行 Unity 画面、真实逐格寻路或实机存档验收，未部署。

## 1.6.1 — 退场、固定物与照料修复

- 商队退场保留绳子；只有端点真正离图、无效或越过硬绳长时才停止绘制。NPC 旅行鼠蛋不因主人离图被套用玩家的留图/消失设置。
- 锚定物体或地面时结束旧的跟随主人任务、取消旧互动动画与拖拽状态。地面锚点不再回退到原主人；固定物范围内恢复原有 AI，越界返回使用可结束的牵引任务，不再直接改写正常任务的寻路。
- 新增「牵着的敌对生物可以攻击」，默认关闭。原版设置与 IrisMenus「牵引」页均可修改和保存，中英双语。开启后保留原版敌对判定与战斗 AI，不强制创造敌意，仍保留硬绳长约束。
- 固定物上的 pawn 可正常找食物、走向床铺并睡觉，进食/睡眠期间暂停拉回。自动喂食不再要求与主人互相好感大于 20，且不启动会打断睡眠的互动动画；尝试间隔按每条牵引记录计算，不会因分桶 tick 错过全部喂食。选中任意可操控殖民者右键固定物上的 pawn，可直接「喂食」，无需先接管绳子，继续遵守主人喂食列表与目标饮食限制。

English: Departing caravans keep visible leashes while vanilla departure AI controls movement. Fixed anchors end stale leader-follow jobs and let pawns eat and sleep without repeated pullback. Any controllable colonist can feed a fixed-anchored pawn from its context menu. The new “Leashed hostile creatures can attack” option is off by default; enabling it preserves normal hostility and combat AI.

验证：Release 构建零警告、零错误；165 项 Mono 回归通过。生产程序集 11 项 headless smoke 通过：物体锚定清理旧跟随任务、地面锚点不跟旧主人、进食移动与睡眠保留、实际消耗食物恢复营养且不改睡眠任务、敌对攻击任务保留及双方退场绘绳目标。中英 133 个 Keyed 键一致，模组与程序集版本为 1.6.1。临时游戏 DLL 仅禁用 UI/材质、配置静态初始化器，使用真实任务与食物消耗方法；未运行 Unity 菜单画面、逐格寻路、整队离图或用户旧档验收。

## 1.7.0 — RimWorld 1.7

- 对照 RimWorld 1.7 源码，使用已安装的 1.7.4979 rev109 游戏程序集编译，目标框架改为 .NET 10；游戏引用来自安装根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll`。Harmony 由外部 Harmony 模组提供；1.7 需使用它的 Harmony 3 版本，不随本模组打包。
- `LoadFolders.xml` 保持 1.6 加载 `Guard` 和根目录；1.7 仅加载 `1.7/Guard` 和 `1.7`，不混载旧程序集。两版均保留重复启用原版时只加载 Guard 的条件。
- 根目录原有 `Assemblies`、`Defs`、`Languages`、`Guard` 保持不变；1.7 使用独立的 Def、翻译与主/守卫程序集。模组包名、存档类型、Scribe 字段和 Def 标识不变。IrisMenus 按当前游戏主次版本检测，不再固定要求 1.6。
- 验证：主程序集与 Guard 的 Release 构建零警告、零错误；161 项 .NET 10 回归通过。真实游戏 DLL 与已安装的 Harmony 3 成功应用 29 个原版补丁目标；headless smoke 实际执行公开牵引查询、目标复制、地面锚定、绳索目标、特殊来源状态和解绑，并绑定实际 IrisMenus 1.7 DLL、登记六页。游戏 DLL 未修改。
- 已部署到 `Mods/LeadYourPetsContinued`，SHA-256 校验运行时文件；仓库原有 17 个、部署目录原有 14 个旧版运行时文件均与升级前指纹一致。未修改启用列表或存档。未启动游戏画面，未验收逐格寻路、动画显示、整队离图或旧档；可选模组的实机兼容仍需使用各自对应的 1.7 版本验证。

## 1.8.0 — 牵引逻辑与渲染修复

- 跟随统一使用非玩家强制的独立 Job；不再改写工作、搬运等旧任务的寻路。靠近目标时不反复派发短命 Job。普通牵引与固定锚点都保留进食和睡眠，主人醒来不会无条件拉回；睡眠任务不再被伪装的任务报告替代。
- 当前倒地及失去移动能力的目标不接收跟随路径；特殊幼年/母婴牵引保留关系，必要时拖动或由退场搬运接管。睡眠、倒地和昏迷时禁止身体互动，睡眠喂食不启动动画。按主人解绑会解除其全部宠物，不影响其他主人的关系。
- 动画不再将世界坐标写入头像、缓存或雕像绘制；特殊地图渲染局部绕过远景缓存。自定义绳使用人物动画的可视端点，模拟仍使用格子位置，已有原版绳不叠画。击退选点与完成落位都校验当前地图、锚点、绳长和障碍；换主、改锚、死亡、离图、退场或照料状态变化时取消旧动画，不执行旧传送。
- 对应问题：[撤离 #1](https://github.com/Nanaloveyuki/RimworldMods/issues/1)、[移动控制 #2](https://github.com/Nanaloveyuki/RimworldMods/issues/2)、[渲染 #3](https://github.com/Nanaloveyuki/RimworldMods/issues/3)。本版本更新 1.7 运行时；保留原有 1.6 已部署内容，不修改启用列表或玩家存档。
- 性能：[优化 #4](https://github.com/Nanaloveyuki/RimworldMods/issues/4) 在 bug 验证通过后实施。动画改为按 Pawn 索引，新增、替换、取消和结束同步维护；活动/空查询各 100000 次的线程分配从 8800000 B 降至 0 B。跟随 driver 在已有同目标路径时不再重复 `CanReach`，集合保护扫描对齐原版 205 tick 检查；保留原有牵引分桶与更新频率，未声称 FPS/TPS 收益。
- 验证：207 项 .NET 10 回归通过；生产 DLL smoke 覆盖倒地不派发跟随、睡眠任务保留、睡眠喂食、昏迷移动与互动边界、按主人全部解绑及头像局部坐标。已安装 Harmony 3 的全生产 `PatchAll` 注册成功；真实补丁下的 `ShouldRemovePawn` 与 `Notify_PawnLost(Incapped)` 保留旅行目标的双向成员关系，普通访客及死亡移除仍有效。未运行游戏画面、逐格寻路、实际抱持落地或整队退出，不能视为实机验收。


## Thought 种族文案

简中 Thought 按思想所属角色的种族与生物年龄选择内心声音：鼠族（`Solaris.RatkinRaceMod`）和绮罗族（`Ancot.KiiroRace`）各分未满 14 岁、14 岁及以上两套，共覆盖 45 个思想的标题与说明。鼠蛋使用错音、叠词和吱叽，成年鼠族保留正常表达；绮罗族使用较柔和的呐／喵语气。其他角色沿用普通口吻。

种族识别使用角色种族 Def 所属的已加载 Mod 包名，不增加硬依赖，也不按名字包含某个词就套用口癖。互动记忆优先使用当时记录的对方，持续牵引心情查询当前关系；对方称呼区分鼠蛋、成年鼠族、小绮罗和成年绮罗族，多种族或年龄组混合的宠物使用泛称。英文使用正常表达，鼠蛋称为 `Young Ratkin`；缺少对应语言文案时保留该语言原有 Thought。

仅调整 Thought 显示，保留原有 Def、存档类型、触发条件、心情数值、持续时间及叠加设置；菜单、头顶提示和任务报告不变。普通简中文案与新增语言文件同步放入根目录和 `1.7/`，本次仅构建并验证 1.7 主程序集，未重建 1.6 程序集，未部署到游戏目录。

验证：218 项 .NET 10 回归通过，其中 11 项覆盖双方种族与年龄独立选择、14 岁边界、未加载种族、语言回退、旧记忆与原版附注保留。生产程序集读取真实语言 XML 完成 1800 次选择，真实 Harmony 3 补丁下的标题跨年龄及语言切换有效；当前牵引双方查询与英文 `Young Ratkin` 输出通过。XML、键覆盖、占位符和两套运行时语言副本一致性检查通过。游戏 DLL 未修改；未启动游戏需求栏画面，不能视为实机存档验收。

## 开发

需要 .NET 10 SDK 和实际游戏安装。默认 `RimWorldVersion=1.7`，输出到 `1.7/Assemblies`、`1.7/Guard/Assemblies`；`RimWorldDir` / `RIMWORLD_DIR` 指向游戏根目录，不能指向反编译源码。默认路径为 Windows 的 `E:/Apps/Steam/steamapps/common/RimWorld` 或 WSL 的对应挂载路径。

```powershell
dotnet test Source/Tests/LeadYourPet.Tests.csproj -c Release
python ../scripts/python/deploy.py lead_your_pets --build-only
# 以下反射/守卫验证使用运行于 .NET 10 的 PowerShell 7。
./scripts/verify-guard.ps1
./scripts/verify-guard.ps1 -NoDuplicate
./scripts/verify-integration.ps1 -HarmonyAssembly="E:/Apps/Steam/steamapps/workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll"
# 可追加 -MouseDisasterAssembly=<对应 1.7 的 MouseDisaster.dll> 验证可选联动。
python ../scripts/python/deploy.py lead_your_pets --rimworld-dir "E:/Apps/Steam/steamapps/common/RimWorld"
```

```bash
# 从 monorepo 根目录执行
bash scripts/deploy.sh lead_your_pets --build-only
bash scripts/deploy.sh lead_your_pets
```

部署只升级所选运行时和顶层元数据，校验 SHA-256；默认 1.7 部署不复制、覆盖或删除原有 1.6 运行时。完整双版本安装使用整个模组目录；仅运行 1.7 部署脚本不会补齐新目标中缺失的 1.6 文件。`-RimWorldVersion=1.6` / `RIMWORLD_VERSION=1.6` 仅供明确安排的旧版维护，需要独立 1.6 游戏，且会覆盖原有 1.6 构建输出；本次升级未执行该构建。

测试包含规则回归、反射重载/默认参数/out 参数，以及抓取、绘绳边界、撤离阶段、归属和搬运授权的行为回归；不以源码文本或文件哈希断言代替行为。部署仅复制运行时文件，校验 SHA-256，不改启用列表、旧版目录或玩家存档。仓库是独立历史，不导入原版 Git 记录。

### 抓取与撤离修复验证

- 对 RimWorld 1.6.4871 rev590 和 PA’s God Hands 2.0 的实际 DLL 核对契约；构建零警告、零错误。
- Mono 反射 runner 执行现有 Fact/Theory 与 xUnit 断言：150 项通过、0 项失败。生产程序集 headless smoke：27 项通过，包括真实搬运 Job 生成与预约、抱持容器中的 Lord 成员保留、恢复集合检查、阶段切换/外部抓取取消条件、后续幼儿选择、睡眠/忙碌清理及释放后的长等待清理。
- 可选兼容检查：未加载神之手时跳过两处补丁；加载实际 DLL 后抓取目标及私有 `core` 字段绑定成功，真实 `ForceReleaseGrab` 经 Harmony 补丁执行后清除抓取记录。
- 消融均使用临时独立程序集、独立进程，不向生产代码添加开关：

| 临时禁用的修复 | 实际观察到的回归 |
| --- | --- |
| 抓取状态识别 | 抓取宠物或牵引者时仍提供绘绳目标，2 项失败 |
| 抓取时取消旧动画 | 旧互动动画仍然存活，1 项失败 |
| 独立绘绳长度上限 | 16 格与 254 格边界仍提供绘绳目标，2 项失败；15 格负控通过 |
| 撤离阶段识别 | 离场识别、合法搬运与两种牵引交接回归，7 项失败；9 项负控通过 |
| 提前交接/解绑 | 宠物牵引与母婴牵引仍存在，2 项失败 |
| 撤离搬运选择 | 不能移动的成员和后续幼儿均无搬运 Job，2 项失败 |
| 抱持期间的集合保护 | 抱持成员不再阻止退出集合检查，1 项失败 |
| 释放后的任务恢复 | 已解绑撤离鼠蛋残留 `Wait(99999)`，1 项失败 |

验证边界：headless fixture 只在临时游戏 DLL 中禁用三个 UI 材质静态初始化器及 ModsConfig 文件加载初始化器，未替换游戏行为方法。没有运行完整 Unity 场景、逐格寻路、真实落地生成或整队离图，也没有实机旧档验收；以上不等同于完整游戏内验收。本次未部署、未修改启用列表或玩家存档；临时 smoke 和消融工具完成后移除。

