# 开发说明

## 目标与边界

本项目保留 RimWorld 的真实 `Thing`、`ThingGrid`、`ListerThings`、`SlotGroup` 和 Reservation 作为事实源，只优化已经确认的重复查询和突发工作。所有游戏状态访问和 Harmony 补丁仍在主线程执行；项目不把可变 Thing 状态移到后台线程。

不在当前范围内：

- 不替换 `ThingGrid` 返回的内部 `List<Thing>`。
- 不虚拟化、合并或 DeSpawn 任意 Mod 的物品。
- 不缓存任意 `Predicate<Thing>` 的最终结果。
- 不更改存档结构。
- 不保证消除大量真实 Thing 在其他 Mod 全图扫描中的基础成本。

## 组件

### 调度器

`CoalescingDeadlineQueue<TKey, TValue>` 按键合并延期任务；`BudgetedWorkScheduler<TKey>` 同时限制单 tick 执行数量和耗时。Outerrealm 适配器使用它分散租约到期后的身份锚点归位，并在真正执行前重新读取存活、租约和预留状态。

### ListerThings 成员索引

只接管 `ListerThingsUse.Global` 且数量达到阈值的 `Contains`。索引同时记录源列表的 `Count` 和私有 `_version`；无法证明集合与列表完全一致时执行原版方法。区域 Lister 不接管。

### 高密度单格布局

原版 `GenThing.ItemCenterAt` 每次调用都会扫描同格全部 Thing。N 个物品分别绘制或打印 Mesh 时形成 O(N²) 扫描。

`DenseCellLayoutCache` 在达到设置阈值后：

1. 复制同格 Item 引用并按 `thingIDNumber` 排序。
2. 一次计算是否包含武器、是否为同 Def，以及每个 Thing 的原版 rank。
3. 使用与 RimWorld 1.6 相同的常量和分支生成所有中心点。
4. 按 Thing 引用缓存结果，使同一批绘制从 O(N²) 降为一次 O(N log N) 构建和 N 次 O(1) 查询。

缓存记录源列表 `_version`、地图结构 revision、游戏 tick 与生效阈值。标准 `Register`、`Deregister`、位置变化和储存组变化会立即失效；下一 tick 也会核对原始列表版本。低于阈值或无法读取版本时回退原版。

武器布局还依赖西侧连续高密度格的奇偶数。为保持原版位置，任何地图 Item 结构变化都会使已缓存布局失效，而不是只失效变化格。

### 存储阻挡检查

原版 `StoreUtility.NoStorageBlockersIn` 先扫描阻挡物和可合并堆，最后通过 `GetItemCount` 再扫描同一格。替换路径只在达到高密度阈值后启用，在第一次循环中同步统计 Item 数量，其余判断顺序和条件保持一致。

### 合并工作候选

原版 `WorkGiver_Merge.JobOnThing` 会为每个源堆完整枚举 `ISlotGroup.HeldThings`。候选缓存按引用相等的 `(ThingDef, Stuff)` 建桶，因为原版 `Thing.CanStackWith` 先要求二者相同；真正候选仍执行原方法中的全部即时判断：

- `CanStackWith`
- stack limit
- Forbidden 与 allowed area
- Cell 和 Thing Reservation
- `IsValidStorageFor`
- 静态火焰

每个原版 `SlotGroup` 或 `StorageGroup` 保存逐格 `(cellIndex, List._version)` 快照。每组每 tick 最多验证一次，同 tick 的标准 ThingGrid 变化会立即触发重验。

以下情况直接返回原版 `HeldThings`：

- SlotGroup 实现来自第三方程序集。
- 源 Thing 的最终 `CanStackWith` override 来自第三方程序集。
- `CanStackWith` 相关方法存在其他 Harmony 补丁。
- 无法读取列表版本或储存格越界。

补丁只用 transpiler 替换 `HeldThings` 的候选来源，`JobOnThing` 的其余代码以及其他 Mod 的 postfix 仍然保留。

## 可选兼容层

### Outerrealm Tech

运行时检测 `FullyAutomaticOmniCrafter`，没有编译期引用。当前包含：

- lease-return burst spreading
- book-search enumeration guard
- reservation recheck compatibility
- vault-delivery reservation guard

私有签名变化时适配器保持关闭并写入警告。

### UF Recipes 与 Turn It On and Off

UF Recipes 会为 `Building_SuperWorkbench` 把 `CompPowerTrader.CompInspectStringExtra` 结果设为 `null`；Turn It On and Off 随后的正则 postfix 要求非空输入。桥接补丁严格排在二者之间，仅把该类型层级的 `null` 规范化为空字符串。

## 构建

```powershell
dotnet build Source\SaveYourStorageAndTickEveryMinute.csproj -c Release
```

输出位于 `1.6\Assemblies`，构建产物不提交到 Git。

## 验证

```powershell
.\scripts\verify.ps1
```

该命令执行：

- Release 构建
- 调度器测试程序
- XML 解析
- 可选依赖引用检查
- Outerrealm 已安装 DLL 私有合同检查
- RimWorld 高密度 Thing 私有方法、字段及 IL 调用点检查

仅验证 Outerrealm 或 RimWorld 合同时可分别运行：

```powershell
.\scripts\verify-outerrealm-contract.ps1
.\scripts\verify-rimworld-contract.ps1
```

## 部署

必须先完全退出 RimWorld：

```powershell
.\scripts\build-and-deploy.ps1
```

脚本核对目标 `packageId`，复制 About、Analyzer、程序集和语言文件，并比较源/目标哈希。

## 已知限制

- 第三方代码如果直接改写 `ThingGrid.ThingsListAt()` 返回列表且在同一 tick 内绕过 `Register/Deregister`，缓存到下一次版本核对前可能看不到变化。这不是 RimWorld 的标准生命周期。
- 第三方代码直接修改 `Thing.def` 或 `Stuff` 且不重新注册时，会破坏原版也默认成立的身份稳定假设。
- 布局缓存消除位置计算的 O(N²)，但仍会绘制每个 Thing 的图形和 GUI 标签；极端密度下仍存在 O(N) 渲染成本。
- 合并候选缓存优化原版自动合并工作，不接管自定义 WorkGiver。
