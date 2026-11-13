# Combat Crash Probe

为 RimWorld 1.7 战斗期间的栈溢出收集证据，重点观察 No One Left Behind 的救援、携带伤员，以及伤害、死亡和工作切换。此 Mod 是临时诊断工具；定位问题后从加载列表移除，并可删除存档数据目录下的 `CombatCrashProbe/` 日志。

## 使用

1. 启用 Harmony 和 Combat Crash Probe，将探针放在加载列表末尾。保留产生问题时的其他 Mod，包括 No One Left Behind。
2. 重启游戏并读取存档。`Player.log` 中应出现 `[CombatCrashProbe] Monitoring ...`；检查独立日志中的 `PATCH FAILED` 或 `UNAVAILABLE`，它们表示对应路径未被监控。
3. 正常进行战斗。崩溃后保留 `Player.log`、本次独立日志及同名 `.previous` 文件。

日志目录是实际 `-savedatafolder` 下的 `CombatCrashProbe/`，并非固定的 Windows 默认目录。本次配置对应：

```text
E:\Games\RimCrow\data\profiles\6ef8fc86606d4e0bb677cb78dbe44229\CombatCrashProbe\
```

## 记录内容与限制

- `RESCUE_SELECTED` 记录救援者、目标伤员、撤退目的地和工作类型。
- 救援筛选会计算好感和市场价值，因此同时监控好感及嵌套于已监控路径内的属性计算，并在快照中记录属性定义名。
- `ENTER` / `EXIT` 记录死亡、销毁、离图、开始携带及放下物体，包含角色 ID、定义、地图状态、容器所属者和当前工作。
- 被监控路径的嵌套深度达到 16、32、64、128、256、512 时，立即写入活动调用、最近 96 次进入/退出、实时 Harmony 补丁来源，以及完整托管调用栈。普通可捕获异常也会触发快照。
- 关键记录同步刷新至磁盘。单文件达到 16 MiB 后保留上一段为 `.previous`；每次启动创建独立会话文件。
- 不修改工作返回值，不阻止伤害或救援，不捕获已经发生的栈溢出，不吞掉游戏原有异常。记录和补丁包装会带来额外开销。
- 递归必须经过至少一个被监控的方法才能提前捕获；全部发生在其他路径内的递归、原生崩溃和断电不在保证范围内。

## 构建与部署

### 完整崩溃转储

如果已确认栈溢出但探针没有捕获递归，使用微软 ProcDump 在崩溃发生时收集完整内存，以便还原未监控方法的调用栈。`Diagnostics/Capture-Crash.ps1` 接收 `-ProcDumpPath` 和 `-DumpDirectory` 参数；从微软官网下载的 `procdump64.exe` 必须有有效签名。

本次诊断将脚本、工具和许可文件放在上述日志目录的 `Tools/`，在 Windows PowerShell 中运行 `Tools/Capture-Crash.ps1` 即可等待游戏启动。捕获一次 `C00000FD` 后退出，完整转储保存在 `Dumps/`；可能占用数 GB，写入时游戏会暂停。此命令接受工具许可，不注册全局调试器，也不上传转储。可用 Ctrl+C 结束等待或监控；问题解决后删除 `Tools/` 和 `Dumps/`。

### Mod 构建

```sh
scripts/deploy.sh combat_crash_probe --rimworld-dir /mnt/e/Apps/Steam/steamapps/common/RimWorld --build-only
scripts/deploy.sh combat_crash_probe --rimworld-dir /mnt/e/Apps/Steam/steamapps/common/RimWorld
```

依赖安装中的 RimWorld 1.7 和 Harmony 3；不打包游戏或 Harmony 程序集。后续可根据捕获到的调用栈缩小监控范围，问题解决后删除此临时诊断模块。
