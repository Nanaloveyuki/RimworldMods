# Prison Labor Reachability Fix

Prison Labor 1.6 在 RimWorld 1.7 下运行时的可达性修复。加载顺序在 Prison Labor 之后，只补它自带派工扫描缺失的可达性判定。

## 问题

Prison Labor 用自己的 `PrisonLabor.Core.AI.JobGivers.JobGiver_Labor` 取代原版 `JobGiver_Work` 的囚犯派工。它的格子扫描只按距离（或优先级）挑目标：

```csharp
// Prison Labor 1.6, TryGetJobOnCell
else if (num3 < num && !ForbidUtility.IsForbidden(item, pawn) && scanner.HasJobOnCell(pawn, item, false))
```

原版 1.7 的 `JobGiver_Work.TryIssueJobPackage` 在选择格子作业前还会检查 `pawn.CanReach(cell, scanner.PathEndMode, scanner.MaxPathDanger(pawn))`，Prison Labor 的拷贝没有这一步。于是囚犯可能被派到根本走不到的目标，起步即寻路失败，然后反复领取同一个失败作业，旁边可达的工作却一直不做。

`WorkGiver_RemoveRoof` 这类只用 `HasJobOnCell`、自己不检查路径的扫描器最容易暴露：它只看禁区和预约。

### 现场证据

`E:\Games\RimCrow\data\profiles\...\PrisonerWaitTrace-20261020-085600.log`（1.7.4981，Prison Labor 1.6 程序集）：

- 囚犯 `pawn=60546`（鼠条-3-松树）在 `t=2541708` 被 `JobGiver_Labor` 派发 `RemoveRoof`，目标格 `(105, 0, 55)`，当拍 `PATH_FAILED`、`CLEANUP ... condition=ErroredPather`；之后约每 500 tick 重复同一目标，共 14 次。
- 同一日志里，殖民者 `pawn=1075` 对同一格的原版 `Reachability.CanReach` 结果始终是 `result=False`（`mode=Touch danger=Some`），说明该格确实不可达，原版殖民者扫描会跳过它，而 Prison Labor 的囚犯扫描不会。

## 修复方式

一个 Harmony 前缀整体接管 `JobGiver_Labor.TryGetJobOnCell`（返回 `false`，原方法不再执行），在保留 Prison Labor 原有选择顺序的前提下加入可达性判定：

| 分支 | 保留的原行为 | 新增 |
| --- | --- | --- |
| `Prioritized` 扫描器 | 囚犯专用禁区判定 `CustomForbidenUtil.IsForbiddenForPrisoner`、`GetPriority`、优先级高者优先、同优先级取近 | `pawn.CanReach(cell, PathEndMode, MaxPathDanger)` |
| 距离扫描器 | 原版 `ForbidUtility.IsForbidden`、最近格优先 | 同上 |

- `scanner.AllowUnreachable` 为真时不检查可达性，与原版 1.7 一致（屋顶建造、种植区等故意登记不可达目标）。
- 物品扫描分支不动：Prison Labor 那边已经走 `GenClosest.ClosestThingReachable`。
- Prison Labor 缺席、目标方法改名/改签名、或囚犯专用禁区判定无法绑定时，只输出日志，不安装补丁、不抛出；游戏照常加载。去掉禁区判定改用原版 `IsForbidden` 会改变哪些格子算禁区，因此宁可整体禁用。
- 不复制 Prison Labor 程序集，不修改其文件。

## 构建与部署

```sh
/root/.dotnet/dotnet build prison_labor_reach_fix/Source/PrisonLaborReachFix.csproj -c Release
bash scripts/deploy.sh prison_labor_reach_fix --build-only
bash scripts/deploy.sh prison_labor_reach_fix --rimworld-dir /mnt/e/Apps/Steam/steamapps/common/RimWorld
```

默认引用本机 1.7 游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 与 Harmony 3（`2009463077/1.7/Assemblies/0Harmony.dll`）。`-p:RimWorldVersion=1.6` 会被拒绝：本 Mod 只构建 1.7。

## 验证状态

- 生产前缀在真实程序集上验证过：加载 `Assembly-CSharp` + `PrisonLabor.dll`，把 `Thing.Map`、`ReachabilityUtility.CanReach`、`ForbidUtility.IsForbidden` 替换为可控桩，通过反射调用**打过补丁的** `TryGetJobOnCell`。覆盖：补丁绑定成功（含参数名与 by-ref 签名）、不可达最近格被跳过、可达最近格仍优先、全部不可达时无目标、`AllowUnreachable` 保持原语义、优先级不能压过可达性、同优先级仍按距离、无作业的最近格不遮挡远处可达作业，以及未打补丁时确实会选不可达最近格（修复前后对照）。
- 另验证了 Prison Labor 缺席时 `TryInstall` 只记录日志、不抛出，程序集本身不引用 Prison Labor 类型。
- 未在真实 1.7 存档内跑过。加载本 Mod 后打开原存档，日志应出现 `[PLReachFix] Cell work targets now require prisoner reachability.`，且原先反复 `PATH_FAILED RemoveRoof` 的囚犯应改为接取可达工作。
