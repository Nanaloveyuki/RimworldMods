# Radius UI - Mission Control Fixes

RimWorld 1.6 的独立 Harmony 补丁 Mod，修复 Radius UI - Mission Control 的信件入口、小地图位置保存和悬浮全文。不修改、替换或打包 RadiusUI 原始源码和程序集，不新增存档数据。

## 修复范围

- 自定义信件的文字标签和信封左键都调用原信件的虚方法 `Letter.OpenLetter()`，不再因有效定位目标而只跳镜头。保留原版 ChoiceLetter 选项和成长信件等专用窗口入口。
- 原 Mod 的右键可否移除判定保持不变。
- 面板关闭时，恢复原版信件按钮、悬浮说明和布局，不再被自定义信件开关拦截。
- 面板开启、自定义信件关闭时，调用原版 `LetterStack.LettersOnGUI()`，信件从底部主按钮栏上方向上排列。
- 两项都开启时保留 RadiusUI 的信件外观、折叠布局和堆叠逻辑。
- 浮动小地图拖动结束后保存位置，不必先关闭窗口；锁定、隐藏、停靠和关闭时也保存当前坐标。沿用原 Mod 的 `mmWinX`、`mmWinY`、`mmWinSize` 设置，不改缩放方式，也不逐帧写盘。
- 信件、消息列表、自由通知和快速弹出面板中的警报恢复完整悬浮文本。悬浮窗按实际回调识别自有窗口，不再依赖每帧清空的 ID 集合；其他窗口的遮挡仍然有效。
- 悬浮窗的长标题按实际宽度换行。正文超过屏幕高度时，鼠标留在来源信件/消息/通知上，用滚轮阅读剩余内容；保留 RadiusUI 外观与原来的点击、右键行为。

| Bottom-right panel | Styled letters | 修复后路径 |
| --- | --- | --- |
| 开 | 开 | RadiusUI 自定义信件；点击打开原信件 |
| 开 | 关 | RadiusUI 面板 + 原版信件绘制 |
| 关 | 开 | 原版面板 + 原版信件绘制 |
| 关 | 关 | 原版面板 + 原版信件绘制 |

不处理信件业务选项本身，也不修改上述范围以外的 HUD 模块。启用 classic Modern Notifications 导致 Mission Control 自行停用时，本补丁同样不安装。

## 安装与加载顺序

1. Harmony (`brrainz.harmony`)
2. Radius UI Framework (`astryl.RadiusUI.Framework`)
3. Radius UI - Mission Control (`astryl.RadiusUI.MissionControl`)
4. Radius UI - Mission Control Fixes (`nanaloveyuki.radiusui.missioncontrol.fixes`)

将补丁文件夹放到游戏 `Mods/RadiusUI-MissionControl-Fixes` 并在 Mod 列表启用。修复对象是原 Mod `Mods/3788256848`，不要用本补丁覆盖它。更新程序集后重启游戏。停用本补丁会恢复原 Mod 的行为，已保存的小地图坐标仍留在原 Mod 设置中。

验证对象：提供的 Mission Control 3.0.0 DLL，SHA-256：

```text
303c25e90ce708e79142181bff7ed9147981d2575071dd711c81a1ae3b9f5c7f
```

启动成功日志：`[Mission Control Fixes] Letter routing, minimap persistence and full-text hover patches installed.`

补丁通过运行时解析 Mission Control 的方法与字段，构建不需要其 DLL。启动时检查目标 IL 结构；上游版本改变导致不匹配时会记录错误并撤销本补丁已安装的修改，不伪装成修复成功。

## 构建与部署

需要 .NET SDK、Python 3.9+ 和已安装的 RimWorld 1.6。WSL 部署还需要 `powershell.exe` 和 `wslpath`，用于检查 Windows 游戏进程和目录联接。编译引用游戏程序集；NuGet 的 Harmony 只供编译使用，不随 Mod 分发。

目录沿用参考项目：`Source/`、`1.6/Assemblies/`、`About/`、`LoadFolders.xml`、`scripts/`。

### WSL / Linux

```bash
./scripts/deploy.sh --build-only
./scripts/deploy.sh
# 自定义游戏位置
RIMWORLD_DIR=/path/to/RimWorld ./scripts/deploy.sh
```

默认路径：`/mnt/e/Apps/Steam/steamapps/common/RimWorld`。

### Windows PowerShell

```powershell
./scripts/build-and-deploy.ps1 -BuildOnly
./scripts/build-and-deploy.ps1
./scripts/build-and-deploy.ps1 -RimWorldDir 'E:\Apps\Steam\steamapps\common\RimWorld'
```

支持 `Debug` / `Release`，默认 `Release`。直接构建：

```bash
dotnet build Source/RadiusUI.MissionControl.Fixes.csproj -c Release -p:RimWorldDir=/path/to/RimWorld
```

部署只复制本补丁 DLL、About.xml、LoadFolders.xml、README 和 LICENSE。不会复制 Harmony、RadiusUI 或游戏 DLL，也不复制源码。游戏运行、目标路径是符号链接/联接、已有目标目录缺少或不匹配本补丁 packageId 时拒绝部署。逐文件验证 SHA-256，不清理目标里的其他文件。

## 验证与限制

- 1.1.0 Release 构建通过：0 警告、0 错误，WSL build-only 脚本执行通过。
- 在真实 Mission Control 3.0.0 DLL 上成功安装全部三组 Harmony 补丁。复现修复前的锁定关闭丢失坐标、存活自有悬浮窗被判为遮挡、长标题仅分配 22px 高度。
- 小地图拖动结束只写入一次，空闲重绘不写盘；锁定关闭仍保存坐标且只沿用一次原始设置写入；延迟缩放完成后保存原来的宽度式尺寸。原 `ExposeData()` 的 XML 保存/加载后，新小地图实例恢复 `x=400, y=250, size=544`。
- 消息行和自由通知输出完整多行正文；快速弹出面板警报保留原说明；信件仍从原虚方法获取正文。现有停靠/固定宽度卡片不新增重复提示，外部窗口仍阻挡悬浮。
- 多行长标题分配的高度从 22px 增至 84px。100 行正文保留完整内容，悬浮窗口限制在 720px 屏幕高度；来源卡片上的滚轮推进 72px，外部窗口遮挡时不滚动。
- 上述验证隔离了 Unity 原生 GUI、字体测量和设置写入边界；XML 序列化使用游戏原实现。消息/警报卡片本体绘制被跳过，不等于游戏内完整鼠标操作与视觉验收。临时验证程序不随 Mod 分发。
- 1.0.0 已验证文字/信封、折叠/展开、有效/无效目标以及 ChoiceLetter/其他 Letter 共 16 个点击分支与四种设置组合。本次未修改 `LetterPatches.cs`。
- 1.0.0 已验证安全部署允许文件、身份检查和链接拒绝；Windows PowerShell 脚本未完整运行其构建部署路径。
- 实际游戏目录部署预检发现 RimWorld 正在运行，已拒绝部署；未绕过检查、未关闭游戏。

游戏内验收：拖动浮动小地图，松开后锁定，切换地图/世界并重启检查位置；悬浮信件标签和信封、消息列表、停靠/自由通知、快速面板警报，检查完整正文与滚轮阅读，并确认外部对话框遮挡有效。继续检查信件点击、四种开关组合和不可右键移除的原限制。

## 更新记录

### 1.1.0

- 保存浮动小地图的拖动位置，修复锁定后关闭丢失坐标。
- 修复信件、消息与通知的悬浮全文，补齐消息列表和自由通知的缺失入口。
- 悬浮长标题支持换行，超屏正文支持在来源卡片上滚轮阅读。

### 1.0.0

- 恢复自定义信封的原信件打开行为。
- 修复面板与自定义信件开关组合造成的原版入口丢失。
- 添加独立构建和安全部署脚本。
