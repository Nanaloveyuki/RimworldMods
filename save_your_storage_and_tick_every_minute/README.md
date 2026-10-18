# Save Your Storage and Tick Every Minute

RimWorld 1.7 存储与突发 Tick 性能兼容 Mod，同时保留 1.6 发布版。源码与问题反馈已迁入 [RimworldMods monorepo](https://github.com/Nanaloveyuki/RimworldMods/tree/main/save_your_storage_and_tick_every_minute)，本目录保留原 MIT 许可证。

## 安装

1. 订阅并启用 Harmony。
2. 将本 Mod 放入 RimWorld 的 `Mods` 目录并在 Mod 列表中启用。
3. 让本 Mod 排在以下已安装 Mod 之后：
   - Outerrealm Tech / 超维科技
   - UF Light Industries / 联合轻工
   - Turn It On and Off - RePowered
4. 重启游戏。已有存档可以直接使用，不会修改存档格式。

`LoadFolders.xml` 按游戏版本只加载 `1.6` 或 `1.7`，语言文件各加载一次。1.6 程序集由既有安装原样保全；当前源码只构建 1.7，不重新生成或覆盖 1.6 DLL。更新部署脚本只写入 `1.7`、About、Analyzer 与 LoadFolders，保留安装中的 `1.6` 和 `PublishedFileId.txt`。

## 设置

在 `选项 -> Mod 设置 -> Save Your Storage and Tick Every Minute` 中可以分别开关：

- **全局 Thing 列表成员索引**：加速大型全局 Thing 列表中的成员检查。
- **高密度格扫描与物品位置缓存**：缓存同格物品数量并一次计算所有绘制位置，降低武器/普通物品的地图 Mesh 重建和位置查询开销。
- **高密度格单次存储检查**：避免存储位置检查重复扫描同一格。
- **合并工作候选索引**：减少自动合并物品堆时对整个储存组的重复枚举。
- **高密度优化阈值**：前两项从单格多少组 Thing 开始生效，默认 `12`。普通地面、原版货架和书架不会进入该路径。
- **超维科技锚点归位节流**：把租约到期后的集中归位分散到多个 tick；可调整每 tick 数量与时间预算。

所有功能默认开启，并可独立关闭用于兼容排查。关闭后重新进入存档即可恢复对应原版路径。

## 排错

出现异常时，先单独关闭相关设置并重新进入存档：

- 物品显示位置异常：关闭“高密度格物品位置缓存”。
- 搬运目标或存储容量判断异常：关闭“高密度格单次存储检查”。
- 自动合并工作异常：关闭“合并工作候选索引”。
- 超维存储出现归位或预留异常：关闭“超维科技锚点归位节流”。

提交问题时请附上 `Player.log`、完整 Mod 列表、存档，以及启用/关闭对应设置后的对比结果。

开发资料见 [开发说明](docs/agents/development.md) 与 [测试边界](docs/agents/testing.md)。从 monorepo 根目录执行 `bash scripts/deploy.sh save_your_storage_and_tick_every_minute --build-only` 只编译并暂存；退出游戏后去掉 `--build-only` 才部署。Windows 使用 `python scripts/python/deploy.py` 及相同参数。
