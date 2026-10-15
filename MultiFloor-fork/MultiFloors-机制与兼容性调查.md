# MultiFloors 运行机制、兼容风险与 fork 规划

> 调查日期：2026-10-03。对象：本地 MultiFloors-Dev 1.6.0.7，支持 RimWorld 1.6。
> 本文归档已完成的调查，供未来 fork 使用；不代表已完成任何修复或第三方兼容认证。
> Nanaloveyuki 原创调查文字采用 MIT OR Apache-2.0 双重许可，完整条款见 [LICENSE](LICENSE)。第三方源码摘录及调查对象保留原权利；MultiFloors 本身的修改／分发授权仍未确认，见第 8.2 节。

## 1. 结论与适用范围

MultiFloors 不是给一张地图增加真正的 Z 轴，而是把同一世界格上的多张独立地图组织成楼层，再接入工作扫描、楼梯运输、地图 Tick、统计和渲染。兼容问题的核心，是将原本依赖单地图状态的逻辑放进临时跨图扫描及分段执行流程，而不只是 Harmony 补丁顺序冲突。

“一次维护，全局可用”需要分成三个目标：

| 目标 | 判断 | 实现方式或限制 |
| --- | --- | --- |
| 一处维护，所有用户获得更新 | 可实现 | 在 fork 中集中维护核心和条件加载的适配模块 |
| 修一次公共机制，整类 Mod 受益 | 可实现，是优先方向 | 修复地图身份、缓存、任务生命周期和原版扩展入口的公共契约 |
| 任意现有及未来 Mod 无需适配且完整跨层运行 | 不能保证 | 工作、访客、车辆、管网、特殊地图的语义不同，不能靠统一替换 Map getter 补出 |

推荐保留多地图结构，先消除公共契约破坏，再按框架提供能力适配。不要把“集中维护”误解成“不再需要适配”。

### 1.1 证据等级

- **源码证据**：本地可见实现或 XML 可以直接支持结论。
- **隔离验证**：上一轮调查运行真实 DLL 的副本，观察到明确行为；不是完整游戏环境。
- **[INFERENCE]**：从契约推导出的风险，尚未复现具体第三方 Mod 的故障。
- **建议**：未来设计方向，当前没有实施。

未进行游戏内整合实测，没有统计实际不兼容概率；不得将风险类别直接当成某个 Mod 已确认不兼容。当前文件中的反编译伪语法也不能作为发行 DLL 无法编译或运行的证据。

### 1.2 路径与版本

- 文档项目：monorepo 中的 `MultiFloor-fork/`；当前仅归档调查，尚未创建源码 fork。
- 调查对象：`D:\References\Rimworld\Mods\MultiFloorsDev`（WSL：`/mnt/d/References/Rimworld/Mods/MultiFloorsDev`）。
- 下文 **M**：调查对象的 `1.6/Assemblies/Source/MultiFloors/`。
- 下文 **P**：`D:\References\Rimworld\Mods\Prepatcher\Source\`。
- 下文 **V**：`D:\References\Rimworld\Vanilla\Game\Managed\Assembly-CSharp\`。
- 隔离验证引用的游戏：`E:\Apps\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\`。
- 版本依据：[`About/About.xml`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/About/About.xml) 的 packageId 为 `telardo.MultiFloorsDev`，modVersion 为 `1.6.0.7`。
- 下文源码链接指向本机参考资料，不是本项目内已存在的 fork 文件；在其他机器阅读时需按上述根路径定位。

文档归档时读取的 SHA-256，用于未来识别文件变化，不是所有第三方兼容模块的版本清单：

| 文件 | SHA-256 |
| --- | --- |
| `1.6/Assemblies/MultiFloors.dll` | `b37c7e53af6366bc3cca41b326cb4d96379fb52ce299257e6d0118159b2f8171` |
| 相邻 `Prepatcher/Assemblies/PrepatcherImpl.dll` | `41996ac527bd33c352479b45f8952a4f36405bc37c8d41736dd67b55f835b689` |
| 游戏 `Managed/Assembly-CSharp.dll` | `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a` |

## 2. 运行机制

### 2.1 楼层是独立 PocketMap

`LevelMapGenerator.GenerateLevelMap()` 创建 `PocketMapParent`，将其 Tile 设为来源地图的 Tile，调用 `MapGenerator.GenerateMap()`，并登记到世界 pocketMaps。

每张地图用以下关系连接：

- `Level`：相对地面层的楼层编号。
- `UpperMap / LowerMap`：相邻楼层。
- `GroundMap`：关联的地面地图。
- 地面层 `MF_LevelMapComp.MapByLevel`：楼层编号到 Map 的索引。

上层和地下层由不同的地图生成配置生成。建立楼梯入口时获取或生成目标地图，在另一层生成对应出口，并登记楼梯连接。

**后果**：每层仍拥有独立 ThingGrid、MapPawns、房间、寻路、预留和地图组件。同一个 `(x,z)` 在两层不是同一个位置，世界 Tile 相同也不意味着物理 Map 相同。

证据：M `Maps/LevelMapGenerator.cs:17–30,34–84,88–107`；`MF_LevelMapComp.cs:19–31,46–55`；`StairEntrance.cs:26–34,58–64`。

### 2.2 Prepatcher 提供字段注入，不提供自动多楼层语义

MultiFloors 有 31 个 `[PrepatcherField]` 访问器，为 Map、Pawn、Thing、Pawn_WorkSettings、Designation、LayerSubMesh、GameCondition 增加楼层关系、任务状态、设置和缓存。

Prepatcher 的处理是：

1. 读取带属性的字段访问器。
2. 给其第一参数对应的目标类型新增实例字段。
3. 将 extern 访问器改写成 `ldarg.0; ldfld/ldflda; ret`。
4. 按声明处理默认值、初始化器和组件缓存。

`[InjectComponent]` 缓存已有地图组件，不是创建统一跨层地图或自动适配其他组件。持久化另由 Scribe 调用负责；字段存在、字段初始化、字段存档是三个不同环节。

**Prepatcher 的广泛程序集重载，不等于广泛重写其他 Mod 的地图语义。** `FreePatch / FreePatchAll` 只是执行作者明确编写的 Cecil 重写规则；未发现 MultiFloors 核心声明通用的全 Mod 跨层重写规则。

证据：M [`PrepatcherFields.cs`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/1.6/Assemblies/Source/MultiFloors/PrepatcherFields.cs)；`HarmonyPatches/HarmonyPatch_ExposePrepatcherFields.cs:15–53`；P `Implementation/Process/FieldAdder.cs:34–107`、`FieldInjections.cs:43–88`、`FreePatcher.cs:13–43`。

### 2.3 跨层工作采用“远端扫描 → 楼梯前置任务 → 本地执行”

工作链路如下：

```text
当前地图工作扫描
  → 未找到合适工作，且 Pawn 未锁定本层
  → 选择其他楼层的楼梯位置
  → 临时修改 Pawn 的地图索引和位置
  → 再次调用 JobGiver_Work，运行原版及第三方 WorkGiver
  → 找到工作，保存原 ThinkResult
  → 恢复 Pawn 原地图和位置，返回 MultiFloors 楼梯任务
  → 真正 DeSpawn / Spawn 到目标地图
  → 检查预留，启动此前找到的工作
```

默认优先扫描器将工作分为高、低优先级阶段：高优先本层、高优先其他层、低优先本层、低优先其他层。它还可以尝试借其他楼层连接本图的不连通区域。

扫描时的 `UnsafeTeleportTo()` 直接写 `pawn.mapIndexOrState` 和 `pawn.positionInt`；真实迁移时的 `SwitchMap()` 才结束当前 Job、DeSpawn、GenSpawn.Spawn。

**这不是通用跨地图寻路。** 普通 Job 最终仍在目标物理地图执行。跨层搬运和构筑资源配送另有专用 WorkGiver、JobDriver、库存交接和目的地选择流程。

证据：M `LevelUtility.cs:80–104`；`HarmonyPatches/HarmonyPatch_ScanJobsOnOtherLevel.cs:34–102`；`HarmonyPatch_ScanJobsOnOtherLevelPrioritized.cs:65–108`；`Jobs/CrossLevelMoveJobUtility.cs:112–160`；`Jobs/Toils_Stairs.cs:46–76,82–185`。

### 2.4 寻路、环境和显示是独立桥接

- **楼梯导航**：`TileStairPathFinder` 建立楼梯/电梯图，地图内边调用原版寻路，图上运行 Floyd–Warshall。
- **电力**：`TilePowerNetManager` 计算独立电网之间的传输，不是将所有电网变成一个原版 PowerNet。
- **温度、气体、真空**：楼梯出口周期执行同步。
- **事件和统计**：部分原版入口使用地面图或枚举整组楼层；例如故事人口和交易物品。
- **渲染**：替换 MapDrawer 绘制入口，并按可见区域递归绘制下层网格。

这些桥接不会自动覆盖第三方的独立资源网络、特殊路径和绘制入口。

证据：M `Maps/TileStairPathFinder.cs:35–65,319–380`；`Maps/TilePowerNetManager.cs:26–81`；`StairExit.cs:196–226`；`HarmonyPatches/HarmonyPatch_UpperLevelRenderer.cs:31–55`。

## 3. 兼容风险与已确认问题

### 3.1 临时扫描破坏地图登记一致性

**源码证据及隔离验证**：`UnsafeTeleportTo()` 只修改两项真实状态字段，没有迁移 ThingGrid 等登记。

验证中，Pawn 原本登记在 A；伪切换到 B 后：

- `pawn.Map == B`。
- A 的 ThingGrid 仍包含 Pawn。
- B 的 ThingGrid 不包含 Pawn。

因此同一个 Pawn 的 Map getter 与网格登记可以互相矛盾。原版 `Thing.Position` setter 会处理网格注销/登记，`SpawnSetup` 也会处理 lister、网格、动态绘制等；直接写字段绕过了这些流程。

**[INFERENCE]** 只读取 `pawn.Map/Position` 的 WorkGiver 可能工作；同时依赖 MapPawns、地图组件、Lord、真实预留或产生扫描副作用的逻辑，可能漏判、污染缓存或错误操作。具体故障需要按第三方调用链验证。

**建议**：不能只把 helper 包成 IDisposable 就宣布修好；异常安全仅解决恢复，不解决扫描期间的不一致。

证据：M `LevelUtility.cs:80–104`；V `Verse/Thing.cs:294–329,995–1016`。

### 3.2 恢复状态不支持嵌套

**源码证据及隔离验证**：OldMap、OldPosition 是 Pawn 上的单槽状态。

```text
A → 临时 B → 临时 C → 恢复 → 恢复
实际最终停在 B，而不是 A
```

第二次临时切换覆盖第一次保存的 A，第一次恢复后又清空保存槽。

这证明 helper 不具备嵌套恢复能力，**不证明默认扫描必然触发重入**。扫描器有自身递归标志，但其他跨层调用仍需审核。

**建议**：使用局部、可嵌套、try/finally 保证恢复的扫描作用域；明确重入规则，避免用全局 bool 隐含表达全部上下文。

证据：M `LevelUtility.cs:80–104`；`HarmonyPatches/HarmonyPatch_ScanJobsOnOtherLevel.cs:34–47`。

### 3.3 同 tick 缓存缺少地图身份

原版 `WorkGiver_ConstructDeliverResources.NoReachableResourceKey` 只包含 Pawn ID、所需资源、forced，不包含地图或扫描位置；负缓存按 tick 清空。

**隔离验证**：同一个 Pawn 从 A 改到 B 后，用同一种资源和 forced 构造出的真实缓存键相等。

**[INFERENCE]** A 层找不到资源的结果可以影响同 tick 的 B 层扫描。第三方缓存如果采用“同 Pawn 同 tick 不换图”的假设，也有同类风险。

**建议**：为已知缓存增加地图/扫描上下文身份，或在明确入口正确失效。不能宣称统一清缓存能覆盖所有第三方缓存；也不能用真实 Spawn/DeSpawn 去完成每次探测，这会触发生命周期副作用。

证据：V `RimWorld/WorkGiver_ConstructDeliverResources.cs:40–60,139–148,423–453`。

### 3.4 延后任务的身份不足

`TileDeferredJobValidator` 用 `HashSet<int>` 保存任务 hash。hash 包含 JobDef、targetA/B/C 和部分队列，但不包含目的地图。

**隔离验证**：两个独立 Job 使用相同 Def 和格子目标时，第二个被判为冲突。对于不同楼层同坐标的格子工作，现有输入不能区分目的层。

此外，仅存 hash 而不检查结构化相等，不能区分真正重复和普通 hash 碰撞；这属于源码可见的身份设计边界，不是本次运行复现的第二种冲突。

**建议**：使用带 Map 的目标身份、owner/计划 ID 和完整相等判断。预留应按具体目标和实际冲突规则处理，不用整个 Job 的粗略 hash 代替所有预留语义。

证据：M `Maps/TileDeferredJobValidator.cs:13–28,51–74`；V `Verse/LocalTargetInfo.cs:245–278`。

### 3.5 预留探测可能重复执行第三方初始化副作用

源码流程是：创建临时 JobDriver，调用 `TryMakePreToilReservations()`，清预留；后续启动 Job 又进入正常启动流程。

**[INFERENCE]** 第三方 Driver 如果依赖 pawn.CurJob、在预留阶段初始化状态，或产生 `ClearReservationsForJob` 无法撤销的副作用，就不能把这个方法安全当成纯查询。

**建议**：让实际 Job 启动路径完成正常预留；到达前的占用协调采用明确计划/预留协议。只有确实提供无副作用接口的实现才可以做探测。

证据：M `Maps/TileDeferredJobValidator.cs:32–47`、`Jobs/CrossLevelMoveJobUtility.cs:152–160`；V `Verse/AI/Pawn_JobTracker.cs:361–366`。

### 3.6 整体替换地图 Tick 绕过原方法内部扩展

非地面层的 `MapPreTick/MapPostTick` Prefix 自行调用一部分子系统，然后返回 false，跳过原方法体。

与实际游戏 DLL 比较，替代流程缺少的入口包括 WindManager、SteadyEnvironmentEffects、TempTerrainManager、WaterBodyTracker、TileMutatorWorker，以及野生动物/植物生成、过境船、天气决策。

部分省略属于楼层政策，**不能全部直接判为 bug**。兼容风险在于：其他 Mod 在原方法内部插入的代码可能随原方法体一起被绕过，且游戏升级后新增调用不会自动进入手写列表。

边界：替代路径仍明确调用 `MapComponentUtility.MapComponentTick()`；不能说楼上所有 Mod Tick 都停，也不能把所有普通 Postfix 当成已被跳过。

**建议**：保留原 Tick 入口和调用链，只精确控制不应在某类楼层执行的子系统。

证据：M `HarmonyPatches/HarmonyPatch_LevelMapPreTick.cs:14–50`、`HarmonyPatch_LevelMapPostTick.cs:14–130`；V `Verse/Map.cs:1052–1273`。

### 3.7 独立工作排序与仓储覆盖边界

默认优先扫描器替换 JobGiver_Work 中的工作列表 getter，然后自行构建高/低列表。

**[INFERENCE]** 其他 Mod 对原工作列表提供的更细排序、动态内容和缓存规则，不保证被自建列表继承。这不是调整 loadAfter 就能自动合并的算法。

跨层目的地管理只接收 `ISlotGroupParent`，不是所有 `IHaulDestination`；非 SlotGroup 容器因此存在覆盖边界。搬运代码还有不带地图的 BlacklistedCells，需要结合其清空/使用范围继续检查，不能只看字段就断言所有层长期互相屏蔽。

**建议**：从原版及其他 Mod 最终有效的工作序列做分组；复用已有仓储契约，扩展跨层能力，不另建第二套不完整优先级和容量规则。

证据：M `HarmonyPatches/HarmonyPatch_ScanJobsOnOtherLevelPrioritized.cs:163–259`；`Maps/TileHaulDestinationManager.cs:24–45`；`Jobs/CrossLevelHaulingMarker.cs:280`。

### 3.8 自定义事件和渲染也有语义边界

GameCondition 同步先重新创建条件实例，再复制基类状态和几个明确列举的原版子类字段。

**[INFERENCE]** 任意第三方 GameCondition 的额外字段和初始化副作用不能靠这套有限复制自动保留。类似地，地图绘制整体替换可能绕过原绘制方法内部的第三方扩展。

**建议**：按系统明确“物理本层”“楼层组”“地面事件”语义；有状态的条件复制需要明确协议，不对未知类型盲目反射复制全部字段。

证据：M `HarmonyPatches/HarmonyPatch_SyncGameConditionToOtherLevel.cs:25–55,92–139`；`HarmonyPatch_UpperLevelRenderer.cs:31–55`。

## 4. 现有兼容模块说明了什么

[`LoadFolders.xml`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/LoadFolders.xml) 已按 packageId 条件加载兼容目录；这本身就是“一处维护、集中分发”的模式。本地 `1.6/ModCompat` 中有 XML、资源和专用 DLL，没有这些适配模块的 C# 导出。

| 框架/Mod | 可见实现 | 为什么需要专用语义 |
| --- | --- | --- |
| VEF/PipeSystem | 专用跨层资源连接器，XML 明确资源类型、相邻层和容器要求 | 资源种类、容量、储量和传输规则不是 Map getter 的职责 |
| Rimefeller | 专用燃料连接器和组件 | 使用不同的管网 API，不能与 VEF 当成同一实现 |
| Hospitality | 在访客 Relax 决策节点前插入去生活楼层的 JobGiver，增加楼层列 | 访客不是只靠普通 JobGiver_Work 决策 |
| WVCWorkModes | 对四个具体 workMode 的 ThinkTree 插入跨层跟随节点 | 需要保留护卫/充电/工作节点的顺序 |
| VehicleFramework | DLL 元数据及核心接口显示电梯车辆转运入口 | 车辆占地、路径及地图缓存不同于普通 Pawn；具体迁移细节未读取完整方法体 |
| SmarterConstruction | DLL 符号显示针对非地面层封闭判定的定点处理 | 只能确认适配迹象，不能从符号认定完整跨层封闭模拟 |

直接证据：[`Hospitality.xml`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/1.6/ModCompat/Hospitality/Patches/Hospitality.xml)；[`WVCMechEscortWork.xml`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/1.6/ModCompat/WVCWorkModes/Patches/WVCMechEscortWork.xml)；[`VEF_PipeConnecter.xml`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/1.6/ModCompat/VEF/Defs/ThingDefs_Buildings/VEF_PipeConnecter.xml)；[`Rimefeller_PipeConnecter.xml`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/1.6/ModCompat/Rimefeller/Defs/ThingDefs_Buildings/Rimefeller_PipeConnecter.xml)。

VehicleFramework 与 Vehicle Map Framework 不能混为同一 Mod；存在前者的电梯适配，不意味着支持后者的移动内部地图。

## 5. 未来 fork 的推荐架构

本节全部是设计建议，尚未实施。

### 5.1 保留独立物理 Map，明确三个作用域

| 作用域 | 用途 | 不应替代的对象 |
| --- | --- | --- |
| 物理 Map | 网格、房间、路径、预留、对象登记 | 不能用 GroundMap 伪装 |
| 楼层组 | 跨层工作候选、资源、人口、床位等逻辑查询 | 不是一张合并 ThingGrid |
| 地面/事件 Map | 袭击入口、部分世界交互等明确政策 | 不应无条件承接所有第三方事件 |

同一殖民地不等于同一物理地图。保留这条边界，才能让大部分本层代码继续遵守原有契约。

### 5.2 先修核心契约

- 扫描作用域必须可嵌套、异常安全；查询和真实执行分离。
- 跨层目标必须带地图身份，不把 hash 本身作为身份。
- 任务计划应有明确 owner 和生命周期，覆盖取消、失败、目标失效、地图删除及读档。
- 真实迁移统一经过正常 DeSpawn/Spawn，并提供稳定迁移前后通知。
- 预留探测不得默认执行任意第三方 Driver 的初始化阶段。
- 已知缓存按地图/扫描上下文修复或失效；不要承诺能自动发现所有未知缓存。
- 从最终有效工作序列分组，而不是重建另一套排序来源。
- 缩小 Tick 和绘制整体替换范围，保留正常扩展入口。

更彻底的远端扫描需要显式的目标 Map/入口位置，但旧 WorkGiver 没有这个接口。必须承认：无法同时保证任意旧代码零改动、零副作用和完整跨层支持。真实探测、旧扫描桥和明确支持的适配器需要按能力区分。

### 5.3 按能力和框架适配，不按每个内容 Def 特判

优先复用已有原版/框架契约，在统一核心上提供有限接口：跨层目标发现、导航和迁移、远端仓储、资源传输、特殊 Pawn/车辆运输、特殊 AI 导航入口。

目标是“一个 VEF 管网适配器服务符合其契约的一批内容 Mod”，不是给每一种资源复制补丁。框架外特例仍需适配，避免为追求统一设计覆盖一切的巨大抽象层。

兼容模块在一个仓库维护，按依赖条件加载；每个模块声明支持版本、入口和能力边界，不要求其他 Mod 作者修改自己的代码。

### 5.4 区分运行兼容与跨层功能兼容

- **运行兼容**：Mod 的物品、建筑和 Pawn 在所在楼层正常工作，不被核心破坏。
- **跨层功能兼容**：自动跨层工作、物流、网络、AI 等经过明确支持。

未知实现不应被强制加入全部跨层逻辑。无法安全扫描的工作可继续在本层运行，并明确说明跨层能力尚不支持；这不是完整兼容认证，也不是静默吞错。

### 5.5 不推荐的捷径

| 方案 | 问题 | 可用边界 |
| --- | --- | --- |
| 全局将 Thing.Map 改为 GroundMap | 混淆真实网格、预留和逻辑殖民地身份，也覆盖不了缓存的 Map 引用 | 只在语义明确的事件入口使用定点 helper |
| 扫描所有 Mod 的 IL，统一替换 Map/Position 调用 | 无法推断每次调用的语义；直接字段、缓存和后续动态代码也不一定覆盖 | 有限域、版本匹配、可验证的确定性重写规则 |
| 将 MultiFloors 排最后 | 不能补出缺失算法，Harmony 显式优先级也不等于加载顺序 | 解决依赖可用性和明确的顺序要求 |
| 每次扫描都真 DeSpawn/Spawn | 触发生命周期、Job、Lord、预留和其他 Mod 通知副作用 | 只用于实际跨层迁移 |
| 重写为真正三维地图 | 扩大到网格、房间、射击、路径、存档等契约，偏离兼容维护目标 | 不作为本轮 fork 的推荐起点 |

Prepatcher 本身是早期基础设施，不应为“加载最后”将其放到启动流程之后。

## 6. 实施优先级与后续验收

| 优先级 | 工作 | 预期收益 |
| --- | --- | --- |
| P0 | 获取完整原始源码、构建方式、兼容模块源码及修改/发布授权；明确 Prepatcher 依赖实现 | 建立可维护、可构建、可分发的基础 |
| P1 | 扫描恢复、地图感知目标身份、已知缓存、延期任务生命周期、预留探测 | 修复公共正确性问题 |
| P2 | 保留有效工作排序，缩小 Tick/渲染替换范围 | 减少对其他补丁的绕过 |
| P3 | 集中楼层查询、导航、迁移和仓储能力 | 让后续适配复用公共机制 |
| P4 | 按管网、访客、车辆和特殊 AI 框架适配 | 一次适配覆盖符合框架契约的一批内容 Mod |

以下是未来验收要求，不是本次已通过的测试：

- 不同楼层同坐标任务能区分，同目标真正冲突按规则处理。
- 同 tick 在多个楼层查询，不复用其他地图的负缓存。
- 嵌套和异常扫描后，Pawn 状态及上下文正确恢复。
- 楼梯移除、目标销毁、任务取消或换层失败，不残留计划/占用状态。
- 上下楼途中保存/加载后，任务恢复或明确取消，不产生幽灵预留。
- 搬运数量、仓储容量和优先级正确，不复制/丢失物品。
- 第三方工作排序保留，迁移通知和访客/车辆所属状态正确。
- 第三方地图组件与原方法内部扩展按政策执行，不因整体替换意外失效。
- 管网传输遵守资源类型、容量、速率及守恒；特殊移动地图单独验收。

应建立行为回归及实际游戏内 smoke，而不是测试源代码文本、补丁是否被调用或函数仅不抛异常。

## 7. 本次隔离验证记录

上一轮调查实际执行了 Mono/Cecil 隔离验证。只在临时目录处理游戏和 Mod DLL 副本；为运行目标 helper，按 Prepatcher 的字段访问模式补入 OldMap/OldPosition 访问器，并放开副本中直接访问的字段。使用未初始化对象构造最小地图、Pawn、网格和 Job 状态，没有启动 Unity 游戏。

观察结果：

```text
PASS actual UnsafeTeleportTo: Map=B, registered grid=A, absent from B
PASS single scan restores map and position
PASS nested scans lose outer restore: ends at B, not original A
PASS distinct cell jobs with same Def/XYZ collide; validator has no destination-map input
PASS actual vanilla resource-negative-cache key remains identical after map change
```

这里 PASS 表示“验证观察与调查断言一致”，不是这些行为已经修好；单次恢复正常是对照条件。

DLL 元数据另确认 31 个 Prepatcher 字段访问器，45 个标记为 HarmonyTranspiler 的方法，29 个 Prefix、37 个 Postfix。这些是方法属性计数，不是本次游戏运行成功应用的补丁数量。

验证边界：

- 可以证明 helper、网格登记、缓存键及任务判重的局部行为。
- 不证明具体第三方 WorkGiver 会触发重入，也不证明每种风险都会在玩家存档出现。
- 没有复现完整搬运、预留、副作用、渲染和第三方管网链路。
- 初次运行遇到 Cecil 依赖解析与宿主 Mono/游戏 mscorlib 混用问题；补全解析路径，并仅链接非系统依赖后成功运行。未来复核须避免将游戏整套 Managed 目录放入宿主 Mono 的系统类库搜索路径。
- 临时源码、DLL 副本、可执行文件和依赖链接已经删除。本文没有附带可直接重跑的验证脚本；未来复核需要重新建立隔离环境，不能把这次记录当成持续测试套件。
- 本次归档仅新增本文，没有再次运行行为场景，也没有创建 fork 或改动 Mod/game DLL。

## 8. fork 前置问题

### 8.1 本地 Source 是不完整反编译资料

可见 Token/RVA 注释、非法生成标识符、ref-return 伪指针表现和缺失的 iterator/state machine 实现。项目文件包含裸程序集引用和导出 mscorlib 项目引用；不能将其当成上游正式构建链。

本地 ModCompat 缺少对应 C# 工程。因此这些资料适合定位机制，但不足以直接形成可靠、可维护的源码 fork。

证据：M `MultiFloors.csproj:11,23,34,38–46,270–277`；`Elevator.cs:366–372` 等生成状态机引用。

### 8.2 MultiFloors 的授权尚未确认

调查对象目录中找到的 LICENSE/README 属于 PrepatcherTemp；其 MIT 授权不自动覆盖 MultiFloors。未确认 MultiFloors 自身的修改/分发许可或完整原始仓库。

未找到许可不等于作者禁止 fork，也不等于可以默认重新发布。发布前需要明确授权，并保留实际使用依赖所要求的许可文本。

### 8.3 Prepatcher 的 packageId 不一致

- MultiFloors [`About/About.xml`](file:///mnt/d/References/Rimworld/Mods/MultiFloorsDev/About/About.xml) 依赖 `zetrith.prepatcher`。
- 相邻独立 Prepatcher 的 About 声明 `jikulopo.prepatcher`，版本 1.3.3。
- 调查对象目录中的 PrepatcherTemp 仍声明 `zetrith.prepatcher`。

这是确定的元数据差异。没有检查当前游戏启用列表，不能声称实际启动失败。未来 fork 应明确使用哪一实现，并同步依赖、加载规则和 API 引用。

## 9. 后续决策原则

先取得源码和授权，再修公共正确性；保留真实地图身份，保留原版及框架扩展入口，用集中能力接口缩小专用适配规模。

成功标准是：**修一次核心，使一整类 Mod 受益；适配一次框架，使符合其契约的内容 Mod 复用。** 不以“不报错”替代完整跨层行为，也不承诺一个万能 Map 补丁能为任意 Mod 补出跨层语义。
