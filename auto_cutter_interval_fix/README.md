# Auto Cutter Interval Fix

为 RimWorld 1.7 的 Auto Cutter 修复旧存档工作间隔为零导致的循环除零异常。

## 原因与范围

2026-11-12 检查的 `NetLingItemCuter.dll` 新增 `WorkTick`，构造默认值为 60，但 `ExposeData` 使用 `Scribe_Values.Look<int>(..., "WorkTick", 0)`。旧存档缺少字段时会得到 0；`Tick` 随即传入 `Gen.IsHashIntervalTick`，其取模运算抛出 `DivideByZeroException`。异常发生在选择切割对象之前，因此也会影响空闲机器。

用户日志中的 `LingItemCuter50841` 位于 `(121, 0, 133)`。检查的三个存档均缺少该机器的 `WorkTick` 字段；安装目录保留的 1.6 实现没有该字段，使用 `TickRare`。

补丁在原 `ExposeData` 读取变量后恢复非正间隔，并在原 `Tick` 前执行相同检查。恢复值是新版默认的 60 tick。正数间隔、切割设置、产物和其他建筑保持原逻辑；不修改原 Mod 文件。原作者修复默认值及非法间隔处理后，可移除此补丁；正常保存会将恢复后的间隔写入原字段。

## 安装与构建

需要 [Harmony](steam://url/CommunityFilePage/2009463077) 和 [Auto Cutter](steam://url/CommunityFilePage/3492083520)，两个工坊 ID 均核对自已安装 Mod 的 `About/PublishedFileId.txt`。在 Mod 列表启用补丁，并放在二者之后。

使用 .NET 10 SDK，`RimWorldDir` 指向包含 `Assembly-CSharp.dll` 和 `UnityStubs.dll` 的 1.7 游戏根目录，`HarmonyAssemblyPath` 指向 Harmony 的 1.7 DLL。可用同名 MSBuild 属性或环境变量 `RIMWORLD_DIR`、`HARMONY_ASSEMBLY_PATH` 覆盖本地路径。

```sh
dotnet build auto_cutter_interval_fix/Source/AutoCutterIntervalFix.csproj -c Release
dotnet run --project auto_cutter_interval_fix/tests/IntervalChecks -c Release
bash scripts/deploy.sh auto_cutter_interval_fix --rimworld-dir /mnt/e/Apps/Steam/steamapps/common/RimWorld
```

部署产物位于 `artifacts/auto_cutter_interval_fix/release`，安装目标为游戏的 `Mods/AutoCutterIntervalFix`。检查程序使用已安装的原 Mod DLL 和游戏读档代码，比较修复前后的读档与周期调用；隔离建筑基类的地图相关操作和实际切割副作用。完整存档内的切割结果仍需在游戏中复验。
