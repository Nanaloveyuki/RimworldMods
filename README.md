# Washing Machine · 洗衣机

RimWorld **1.6** 电力滚筒洗衣机模组。包标识：`nanaloveyuki.washingmachine`；程序集：`WashingMachine.dll`（.NET Framework 4.8）。

## 安装与操作

1. 安装并启用 **Harmony**，把本模组排在 Harmony 后面。其他下文列出的模组都不是必需依赖。
2. 完成原版 **电力（Electricity）** 研究，在 **杂项** 建造一台 **1×1 洗衣机**。成本为80钢铁、2工业零部件；连接电网并打开开关。
3. 开启殖民者的 **搬运** 工作，搬运者会自动将地图上允许搬运的死人衣物装入空闲洗衣机。也可选中搬运者，右键洗衣机，在菜单里指定一件符合条件的衣物。
4. 要清洗生物，选中可搬运的殖民者，右键目标生物，选择 **“搬运 目标名 至洗衣机”**。当前地图必须有已建成、已生成且不在战争迷雾中的己方洗衣机；只有蓝图、框架、其他地图上的洗衣机或迷雾中的洗衣机时，不出现这个右键选项。洗衣机繁忙、目标过大、无法预留或无法抵达时不能搬入。
5. 装入后自动开始工作，每次只清洗一件衣物或一个生物。选择洗衣机可查看进度；用 **“取消清洗并释放内容物”** 立即停止并释放。断电或关闭开关会暂停计时，恢复供电后继续，不会跳过尚未完成的清洗。

## 清洗效果与风险

- **每次30游戏秒，即1800 ticks**。这不是30现实秒，现实耗时随游戏速度变化。
- **衣物仅移除“死人衣物”标记**。不修复耐久，不改变品质，不清除其他模组定义的污渍或状态。取消未完成的清洗不会移除标记。
- 生物搬入上限使用实际 `BodySize`，默认 **≤0.95**；等于上限可以搬入。没有年龄或种族的替代判定。
- 有心情需求的生物洗完后随机获得一条 **-24心情** 的记忆，持续 **1游戏天**，同一思想最多一层。普通生物使用4条文案；识别到的鼠族使用3条专属文案。没有心情需求的动物仍能清洗，但不会生成不存在的心情需求。
- **实际体型严格小于0.25** 的生物，完成每次清洗时默认有 **5%死亡概率**；体型恰好0.25不触发这一风险。请在操作前检查体型，不要把幼小生物当作安全的清洗对象。
- 生物清洗中固定在洗衣机同格内，其真实外观缩小后在滚筒位置顺时针旋转。取消清洗会释放，不执行完成清洗后的心情、卫生或死亡效果。

## 设置

原版 **选项 → 模组设置 → 洗衣机** 可调整以下值；安装 IrisMenus 时也可从其统一设置入口调整。无需安装 IrisMenus 即可使用设置。

| 设置 | 默认 | 范围 |
| --- | ---: | ---: |
| 电力消耗 | 200 W | 0–1000 W |
| 可搬入生物的最大体型 | 0.95 | 0.1–10 |
| 小体型生物死亡概率 | 5% | 0–100% |

死亡概率设置只影响 **体型<0.25** 的生物，不改变这个固定分界。

## 可选兼容

- **IrisMenus**（`Nanaloveyuki.IrisMenus`）：共享设置页面。
- **NewRatkinPlus 1.6**（`fxz.Solaris.RatkinRaceMod.odyssey`）：鼠族清洗记忆使用指定专属文案。
- **Dubs Bad Hygiene**（`Dubwise.DubsBadHygiene`）或 **Dubs Bad Hygiene Lite**（`Dubwise.DubsBadHygiene.Lite`）：清洗完成后恢复目标已有的卫生需求。两个卫生模组本身互斥，选择其中一个即可。

上述模组只在 `loadAfter` 声明加载顺序。未安装时仍使用核心清洗、心情和风险逻辑，不需要额外兼容程序集，也不复制这些模组的DLL。

## 构建与部署

需要 .NET SDK 8或更新版本、合法安装的 RimWorld 1.6 程序集以及 Harmony 的 `0Harmony.dll`。构建目标是 `net48`，不是游戏程序集的替代品。默认游戏位置是 `/mnt/e/Apps/Steam/steamapps/common/RimWorld`（Windows 对应 `E:\Apps\Steam\steamapps\common\RimWorld`），部署目标固定为该游戏的 `Mods/WashingMachine`。

### Linux / WSL

需要 `bash`、`python3` 和 `pgrep`；WSL部署还需要可调用 `powershell.exe` 检查 Windows 游戏进程。先退出游戏，再运行：

```bash
bash scripts/deploy.sh
# 只构建，不接触游戏模组目录；游戏可以继续运行
bash scripts/deploy.sh --build-only
```

其他游戏位置或 Harmony 位置：

```bash
RIMWORLD_DIR="/path/to/RimWorld" \
HARMONY_ASSEMBLY_PATH="/path/to/0Harmony.dll" \
bash scripts/deploy.sh
```

`CONFIGURATION=Debug` 可切换构建配置，默认 `Release`。脚本自动选择游戏的 Windows/Linux Managed 目录。

### PowerShell

```powershell
./scripts/build-and-deploy.ps1
./scripts/build-and-deploy.ps1 -BuildOnly
# 也接受 --build-only 别名
./scripts/build-and-deploy.ps1 -RimWorldDir "E:\Apps\Steam\steamapps\common\RimWorld" -HarmonyAssemblyPath "D:\path\0Harmony.dll"
```

PowerShell脚本兼容 Windows PowerShell 和 PowerShell 7；Linux/WSL 下默认使用 `/mnt/e/...` 游戏路径，Windows下默认使用 `E:\...`。

未显式指定 Harmony 时，构建配置使用 `Directory.Build.props` 中的默认位置。也可直接构建并覆盖引用路径：

```bash
dotnet build WashingMachine.csproj -c Release \
  -p:RimWorldDir="/path/to/RimWorld" \
  -p:RimWorldManagedDir="/path/to/RimWorld/RimWorldLinux_Data/Managed" \
  -p:HarmonyAssemblyPath="/path/to/0Harmony.dll"
```

输出为 `tmp/build/WashingMachine.dll`。部署脚本仅复制这一个自有DLL、`About/`、`Defs/`、`Languages/`、`Textures/`、本说明与许可证；不复制游戏、Harmony、其他模组程序集、源码或临时构建内容。脚本检查源/目标包标识，拒绝不属于本模组的已有目录、非空无身份目录及符号链接/重解析点；游戏运行中拒绝部署，并逐文件比较SHA-256。它们不修改 `ModsConfig.xml`，不删除目标中的无关文件；启用模组请在游戏里自行操作。

## 纹理来源

根目录保留用户提供的 `474eeca1ed0a4ccaec86d1ab9705bde8_720.png`，不修改原图。四向游戏纹理放在 `Textures/Things/Building/WashingMachine/`：正面对应南、原侧面对应东、侧面镜像对应西、背面对应北。

原图已有Alpha透明度，黑色查看器背景不是机身的一部分。导出按Alpha而非黑色RGB识别外部残留，只保留每个视图的主体连通区域；黑色机身轮廓、滚筒内部和水管保留。四向使用相同400×400裁切画布并按机身中心对齐，最近邻放大为512×512透明PNG，避免按水管外缘分别缩放导致转向时机身体积变化。`About/Preview.png` 是从同一资源生成的预览。

## 真实游戏验证界限

构建成功、XML检查和部署哈希一致均不能替代实际游戏验证。本说明不宣称已在运行中的 RimWorld 完成以下检查；需要在测试存档中逐项观察：

- 仅启用 Core、Harmony 与本模组，确认启动无Def/类型错误、杂项建筑可建造，四向透明纹理与滚筒中的真实Pawn渲染正常。
- 衣物清洗结束后死人标记消失，耐久和品质保持原值；开启搬运后自动上料。
- 确认1800 ticks完成、断电暂停、复电续洗、取消释放，以及清洗中存档/读档后的行为。
- 体型上限等于/略超过设置值的目标；0.25与严格小于0.25的风险边界；0%与100%死亡概率；随机记忆-24与1游戏天持续时间。
- 当前地图无已知洗衣机时不显示生物搬运选项，有已知洗衣机时按可用性处理。
- 分别启用可选设置、鼠族、卫生完整版或Lite，观察设置页、专属文案及卫生需求恢复；不要同时启用两个卫生模组。

## 本次交付

新增洗衣机建筑、搬运与清洗任务、七阶段清洗记忆、英文及精准简中文案、四向透明纹理、预览图和安全构建部署脚本。未新增第二种可建造物。
