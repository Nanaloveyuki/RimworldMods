# Washing Machine · 洗衣机

RimWorld **1.6 / 1.7** 电力滚筒洗衣机模组。包标识：`nanaloveyuki.washingmachine`；程序集名称与存档类型名保持不变。1.6 使用保留的 .NET Framework 4.8 二进制；当前源码构建 1.7 的 .NET 10 / UnityStubs / Harmony 3 版本。

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
- **NewRatkinPlus**（`fxz.Solaris.RatkinRaceMod.odyssey`）：以既有 `Ratkin` / `Ratkin_Su` 种族标识选择鼠族专属清洗记忆。
- **Dubs Bad Hygiene**（`Dubwise.DubsBadHygiene`）或 **Dubs Bad Hygiene Lite**（`Dubwise.DubsBadHygiene.Lite`）：清洗完成后恢复目标已有的 `DubsBadHygiene.Need_Hygiene` 卫生需求。两个卫生模组本身互斥，选择其中一个即可。可选模组仍须自行支持当前游戏版本；这里不宣称第三方 1.7 适配已经过游戏验证。

上述模组只在 `loadAfter` 声明加载顺序。未安装时仍使用核心清洗、心情和风险逻辑，不需要额外兼容程序集，也不复制这些模组的DLL。

## 构建与部署

需要 **.NET SDK 10**、合法安装的 **RimWorld 1.7** 与 **Harmony 3**。游戏引用是安装目录根部的 `Assembly-CSharp.dll`、`UnityStubs.dll`，不是旧版 `RimWorldWin64_Data/Managed`。默认游戏位置是 `/mnt/e/Apps/Steam/steamapps/common/RimWorld`（Windows 对应 `E:\Apps\Steam\steamapps\common\RimWorld`），默认 Harmony 引用是对应 Steam Workshop 的 `2009463077/1.7/Assemblies/0Harmony.dll`。部署目标固定为该游戏的 `Mods/WashingMachine`。

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

`CONFIGURATION=Debug` 可切换构建配置，默认 `Release`。这些脚本只构建 1.7，不重编译或替换包内的 1.6 DLL。

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
  -p:RimWorldVersion=1.7 \
  -p:HarmonyAssemblyPath="/path/to/0Harmony.dll"
```

输出为 `1.7/Assemblies/WashingMachine.dll`，中间文件为 `tmp/obj/1.7/`。既有 `tmp/build/` 与根部 `obj/` 不作为新构建输出。构建后会把根部 `Defs/`、`Languages/`、`Textures/` 同步复制到只含数据的 `Common/`；根部数据不移动、不改写。

`LoadFolders.xml` 明确路由：1.6 只加载根目录（含保留的 `Assemblies/WashingMachine.dll`）；1.7 只加载 `Common/` 与 `1.7/`，**不加载根部旧 DLL**。请完整安装发布包，不要只覆盖旧的根部 DLL。1.6 运行文件保留，不提供从当前源码重新构建旧版的入口。

完整发布包保留两个版本的自有 DLL 与各自所需数据；部署脚本只更新 `1.7/Assemblies/WashingMachine.dll`、`Common/`、`About/`、`LoadFolders.xml`、本说明与许可证，**不写入目标的根部 1.6 DLL 或旧数据**。需要 1.6 时请完整安装发布包。脚本不复制游戏、Harmony、IrisMenus 或其他模组程序集、源码及临时构建内容；检查源/目标包标识，拒绝不属于本模组的已有目录、非空无身份目录及符号链接/重解析点；游戏运行中拒绝部署，并逐文件比较 SHA-256。它们不修改 `ModsConfig.xml`，不删除目标中的无关文件；启用模组请在游戏里自行操作。

## 纹理来源

根目录保留用户提供的 `474eeca1ed0a4ccaec86d1ab9705bde8_720.png`，不修改原图。四向游戏纹理放在 `Textures/Things/Building/WashingMachine/`：正面对应南、原侧面对应东、侧面镜像对应西、背面对应北。

原图已有Alpha透明度，黑色查看器背景不是机身的一部分。导出按Alpha而非黑色RGB识别外部残留，只保留每个视图的主体连通区域；黑色机身轮廓、滚筒内部和水管保留。四向使用相同400×400裁切画布并按机身中心对齐，最近邻放大为512×512透明PNG，避免按水管外缘分别缩放导致转向时机身体积变化。`About/Preview.png` 是从同一资源生成的预览。

## 真实游戏验证界限

本次迁移包含 1.7 引用、隔离装载、构建部署更新，以及 `IThingHolderTickable` 适配：由洗衣机显式 tick 内容物，避免 1.7 的 `Thing.DoTick` 再次推进被洗生物。可选 IrisMenus 仍通过公开 `MenuRegistry.Register(Mod, string, Func<string>, Action<Rect>, Action)` 反射注册，无强制依赖。

集成验证：Release 构建 0 警告、0 错误；Bash／PowerShell 的只构建入口通过。未修改游戏 DLL 的 headless smoke 通过 19 项检查：真实 Harmony 3 补丁、内容物每 tick 仅推进一次、断电暂停、1800 tick 才清除死人标记、阻塞出口保留内容物并重试、耐久和品质不变、取消不清洗、被洗生物任务保护、缺少卫生需求时不创建需求，以及实际 IrisMenus 1.7 注册；118 个生产方法完成 JIT 绑定。游戏原生 `ModLoadFolders` 确认 1.6／1.7 不混载。保留 DLL 的 SHA-256 与现有 1.6 成品一致，未部署到玩家游戏安装。

未启动游戏画面。上述 headless 检查不等于实际游戏验收；本说明不宣称已在运行中的 RimWorld 完成以下检查，需要在测试存档中逐项观察：

- 仅启用 Core、Harmony 与本模组，确认启动无Def/类型错误、杂项建筑可建造，四向透明纹理与滚筒中的真实Pawn渲染正常。
- 衣物清洗结束后死人标记消失，耐久和品质保持原值；开启搬运后自动上料。
- 确认1800 ticks完成、断电暂停、复电续洗、取消释放，以及清洗中存档/读档后的行为。
- 体型上限等于/略超过设置值的目标；0.25与严格小于0.25的风险边界；0%与100%死亡概率；随机记忆-24与1游戏天持续时间。
- 当前地图无已知洗衣机时不显示生物搬运选项，有已知洗衣机时按可用性处理。
- 分别启用可选设置、鼠族、卫生完整版或Lite，观察设置页、专属文案及卫生需求恢复；不要同时启用两个卫生模组。

## 本次交付

保留洗衣机建筑、搬运与清洗任务、七阶段清洗记忆、英简中文案、四向透明纹理及全部既有存档字段。新增隔离的 1.7 装载目录与数据副本；1.6 既有二进制不重编译，当前构建与部署入口转为 1.7。未新增第二种可建造物。
