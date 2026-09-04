# 测试指南

本文用于验证单格多组 Thing 优化。测试者不需要修改或反编译任何 Mod。

## 准备

- RimWorld 1.6、Harmony、本 Mod、Dubs Performance Analyzer。
- 能让单格容纳至少 25 个不同 Thing 组的高容量存储建筑或测试 Mod。
- 一份可重复加载的测试存档。
- 测试前关闭自动保存，并保持相同地图、镜头、倍速和 Mod 列表。

建议准备 `12 / 25 / 50 / 100` 组四档。每档尽量分别准备：

- 相同 Def 的多个不满堆。
- 不同 Def 的混合物品。
- 武器混合物品。
- 不能互相堆叠的带 Comp、品质或耐久物品。

不要把普通地面格强行作为容量测试目标；目标建筑的 `MaxItemsInCell` 必须真实允许对应数量。

## A/B 方法

每项均使用同一存档分别测试：

1. 关闭对应优化，重新进入存档，等待 10 秒预热。
2. 记录 2,000 tick 的 Average、Median、Max、Calls 和 TPS/FPS。
3. 开启对应优化，重新进入同一存档并重复。
4. 至少重复三轮，单独标注首次缓存构建尖峰，不只比较一个 Max 值。

除正在比较的设置外，其余设置保持不变。

## 1. 物品位置缓存

Analyzer 目标：

- `RimWorld.GenThing:ItemCenterAt`
- `SaveYourStorageAndTickEveryMinute.Things.DenseCellLayoutCache:TryGetCenter`

操作：

1. 镜头切到最近缩放，使物品数量标签可见。
2. 让高密度格进入和离开屏幕。
3. 切换到其他地图后返回。
4. 向格内搬入一组、取出一组、合并一组并拆分一组。
5. 对相同 Def、混合 Def 和武器格分别截图。

验收：

- 每个 Thing 的显示位置在开关前后一致。
- 搬入、取出、拆分后下一帧布局立即更新，无重影或旧位置。
- `ItemCenterAt` 不再随单格组数表现为 N 次完整扫描。
- 低于设置阈值的格保持原版路径。

## 2. 单次存储检查

Analyzer 目标：

- `RimWorld.StoreUtility:NoStorageBlockersIn`

操作：

1. 强制搬运可与格内不满堆合并的物品。
2. 强制搬运不能合并、但格内仍有容量的物品。
3. 填满容量后再次尝试搬入。
4. 测试含不可站立建筑、蓝图/框架或静态火焰的候选格。

验收：

- 开关前后可接受/拒绝结果一致。
- 满格、可合并、阻挡和火焰行为一致。
- 高密度格的检查不再出现第二次 `GetItemCount` 全格扫描。

## 3. 合并工作候选索引

Analyzer 目标：

- `RimWorld.WorkGiver_Merge:JobOnThing`
- `SaveYourStorageAndTickEveryMinute.Things.MergeableCandidateCache:GetCandidates`

操作：

1. 在同一储存组放置大量不满堆，让搬运者自动执行“合并物品堆”。
2. 混入相同 Def、不同 Stuff，以及因 Comp/品质等原因不能合并的物品。
3. 禁止其中一堆、限制 Pawn 允许区域、预留目标格，再观察候选变化。
4. 把多个存储建筑加入同一 StorageGroup 后重复。
5. 搬运过程中保存并重新加载。

验收：

- 只创建原版也能成功的合并工作。
- 禁止、区域、预留、容量和火焰检查仍生效。
- 没有 Pawn 反复领取后立即失败的工作。
- 同一储存组的大量源堆不再各自完整枚举全部 `HeldThings`。

## 4. 回归检查

- 新建地图、加载旧存档、保存后重载。
- 多地图切换和关闭地图。
- 普通地面、原版货架、书架与低于阈值的 Mod 存储。
- 商队装载、交易、建造、烹饪、医疗取药。
- Outerrealm 存入/取出、唯一物品、书籍、多人竞争预留。
- 选中联合全息打印机，确认不再出现 `Parameter name: input`。

日志中不应出现：

- `WorkGiver_Merge candidate cache could not find its expected call site`
- Harmony patch exception
- `Collection was modified`
- 新增的 reservation/job failure 循环

## 报告模板

```text
RimWorld 版本：
本 Mod DLL SHA256：
完整 Mod 列表：
存储建筑与容量：
单格 Thing 组数：
物品构成：相同 Def / 混合 / 武器 / 带 Comp
游戏倍速与镜头缩放：

关闭优化：Average / Median / Max / Calls / TPS / FPS
开启优化：Average / Median / Max / Calls / TPS / FPS

行为差异：
Player.log：
存档或复现步骤：
```
