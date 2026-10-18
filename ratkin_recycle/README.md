# 鼠族回收行为 RatkinRecycle

原作者为菜地里的稽，Nanaloveyuki 经授权接手维护。本目录保留 1.6 发布内容，并提供独立的 1.7 构建。本文记录导入来源、构建方法和版本边界，后续维护可继续更新。

- 包名：`nanaloveyuki.ratkinrecycle`，原包名为 `dsh.ratkinrecycle`。
- 游戏版本：RimWorld **1.6 / 1.7**，分别加载各自程序集；[迁移记录与验证范围](docs/references/migration-1.7.md)。
- 许可证：**MIT OR Apache-2.0**，任选其一；完整文本见 [LICENSE](LICENSE)，署名和授权依据见 [NOTICE](NOTICE)。原作者版权保留。
- 依赖：Harmony、NewRatkinPlus；原说明建议同时启用 Toddlers。

## 导入内容

来源为 `E:\Apps\Steam\steamapps\common\RimWorld\Mods\鼠族回收行为 RatkinRecycle`。原目录共有 6 个文件：1 个 DLL、3 个 Def XML、1 张贴图和 `About.xml`，没有源码、许可证、预览图或工坊发布 ID。

[原始发行包归档](docs/references/upstream-original.tar.gz) 保留这 6 个文件的原始字节；[SHA-256 清单](docs/references/upstream-manifest.json) 记录逐文件大小、哈希和归档哈希。这些是长期来源资料，不随构建清理。归档仅用于核对，不应解压到启用中的 Mod 目录。

`Source/` 使用 ILSpy 11.0.0.9375 反编译恢复，不是原作者工程，也不包含原始注释。反编译时解析了 1.6 的游戏、Unity 和 Harmony 引用。`1.6/Assemblies/RatkinRecycle.dll` 是 vendor 时重建的接手版 DLL，已冻结；原作者 DLL 留在归档中。1.7 使用相同玩法源码独立编译为 .NET 10。

本次改动：

- 修改 About 包名和 Harmony ID，补充维护者署名及依赖工坊链接。
- 添加源码工程、许可证和来源记录。
- 添加 960×540 的排版预览图，使用原自动止血装置图标。

保留原程序集名 `RatkinRecycle`、命名空间、Def 名称、玩法 XML 和贴图，不修正或重写原玩法逻辑。使用接手版时应取消旧版勾选，不要同时启用两份。原版按安装目录名和 Mod 类名定位设置文件，目录改为 `ratkin_recycle` 后不会自动找到旧目录名的设置；本次不添加配置迁移兼容层。

## 构建

安装 .NET 10 SDK 后，在仓库根目录执行（默认构建 1.7）：

```sh
dotnet build ratkin_recycle/Source/RatkinRecycle.csproj -c Release
```

1.7 目标为 `net10.0 / x64`，直接引用当前 1.7 游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 及 Harmony 3 的 `1.7/Assemblies/0Harmony.dll`。默认路径见 `Directory.Build.props`；跨机器可通过 `-p:RimWorldDir=... -p:HarmonyAssemblyPath=...` 或环境变量 `RIMWORLD_DIR`、`HARMONY_ASSEMBLY_PATH` 覆盖。

1.7 输出仅到 `1.7/Assemblies/`，不会覆盖冻结的 1.6 DLL。需要核对 1.6 源码可重建性时执行：

```sh
dotnet build ratkin_recycle/Source/RatkinRecycle.csproj -c Release -p:RimWorldVersion=1.6
```

1.6 固定引用 `Krafs.Rimworld.Ref 1.6.4633`、`Lib.Harmony 2.4.1` 和 `Microsoft.NETFramework.ReferenceAssemblies 1.0.3`，输出到忽略目录 `tmp/rebuild-1.6/Assemblies/`，不会自动替换已发布 DLL。首次 NuGet 还原需要网络或本地缓存。

`LoadFolders.xml` 让 1.6 仅加载 `1.6/`，1.7 仅加载 `Common/` 和 `1.7/`；`Common` 中的 XML、贴图与冻结的 1.6 文件逐字节相同。中间文件位于按版本隔离的 `tmp/obj/`，外部引用不随 Mod 复制。冻结文件哈希见 [1.6 发布清单](docs/references/release-1.6-manifest.json)。

## 导入验证

2026-10-18：Release 构建通过，0 警告、0 错误。一次性产物检查已执行：原始归档 6/6 文件通过 SHA-256 核对；3 个 Def XML 和 1 张贴图与原件一致；About 包名和静态初始化器 IL 中的 Harmony ID 均为新值；公开类型、字段和方法名/参数数量清单的 135 项一致；2 处 XML 类型引用可在重建 DLL 中解析；预览图为 960×540 PNG；未混入外部依赖 DLL。检查不证明游戏内行为等价，尚未进行 1.6 游戏内验证。

1.7 迁移验证：正式 1.7 Release 与 1.6 源码重建均为 0 警告、0 错误；冻结的 1.6 文件哈希不变。实际 1.7 DLL 的无界面运行时冒烟验证了幼儿心情需求、生产/食物历史与时间边界、啃咬 deadline/营养、流血伤口治疗和版本目录选择。生产/吞食引擎主体、伤害应用和渲染被隔离；未验证地图寻路搬运、真实致死、范围扫描、设置界面或存读档，详见 [迁移记录](docs/references/migration-1.7.md)。

本次保留原年龄换算、倒地目标过滤、未持久化历史、搬运失败处理及设置精度等既有问题；不要将版本迁移理解为这些问题已修复。当前本机 Toddlers 仅声明支持到 1.6，不能据此宣称其 1.7 兼容。

## 授权记录

2026-10-18，维护者明确确认已取得原作者同意，可 vendor、反编译，并以 MIT / Apache-2.0 接手。提供的聊天截图中有原作者身份确认及“放心做”的回复，但没有写出具体许可证名称；本目录按维护者的明确授权确认记录双许可，不将截图解释为版权转让。未将私人聊天截图复制进仓库。
