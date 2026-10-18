# 鼠族回收行为 RatkinRecycle

原作者为菜地里的稽，Nanaloveyuki 经授权接手维护。本目录导入原本地 Mod 的 1.6 内容，并从 DLL 恢复了 C# 源码。本文记录导入来源、构建方法和版本边界，后续维护可继续更新。

- 包名：`nanaloveyuki.ratkinrecycle`，原包名为 `dsh.ratkinrecycle`。
- 游戏版本：仍为 RimWorld **1.6**，本次没有适配 1.7。
- 许可证：**MIT OR Apache-2.0**，任选其一；完整文本见 [LICENSE](LICENSE)，署名和授权依据见 [NOTICE](NOTICE)。原作者版权保留。
- 依赖：Harmony、NewRatkinPlus；原说明建议同时启用 Toddlers。

## 导入内容

来源为 `E:\Apps\Steam\steamapps\common\RimWorld\Mods\鼠族回收行为 RatkinRecycle`。原目录共有 6 个文件：1 个 DLL、3 个 Def XML、1 张贴图和 `About.xml`，没有源码、许可证、预览图或工坊发布 ID。

[原始发行包归档](docs/references/upstream-original.tar.gz) 保留这 6 个文件的原始字节；[SHA-256 清单](docs/references/upstream-manifest.json) 记录逐文件大小、哈希和归档哈希。这些是长期来源资料，不随构建清理。归档仅用于核对，不应解压到启用中的 Mod 目录。

`Source/` 使用 ILSpy 11.0.0.9375 反编译恢复，不是原作者工程，也不包含原始注释。反编译时解析了 1.6 的游戏、Unity 和 Harmony 引用。仓库中的 `Assemblies/RatkinRecycle.dll` 由恢复源码构建，原 DLL 留在归档中。

本次改动：

- 修改 About 包名和 Harmony ID，补充维护者署名及依赖工坊链接。
- 添加源码工程、许可证和来源记录。
- 添加 960×540 的排版预览图，使用原自动止血装置图标。

保留原程序集名 `RatkinRecycle`、命名空间、Def 名称、玩法 XML 和贴图，不修正或重写原玩法逻辑。新旧包名不会自动迁移启用列表或 Mod 设置；使用接手版时应取消旧版勾选，不要同时启用两份。

## 构建

安装 .NET SDK 后，在仓库根目录执行：

```sh
dotnet build ratkin_recycle/Source/RatkinRecycle.csproj -c Release
```

工程目标为 `net472`，固定引用 `Krafs.Rimworld.Ref 1.6.4633`、`Lib.Harmony 2.4.1` 和 `Microsoft.NETFramework.ReferenceAssemblies 1.0.3`。NuGet 首次还原需要网络或本地缓存，不依赖当前已升级为 1.7 的游戏安装。

输出为 `Assemblies/RatkinRecycle.dll`，中间文件在忽略目录 `tmp/obj/`。外部引用只用于编译，不随 Mod 复制。构建成功只能证明 1.6 引用下可编译，不代表已经完成游戏内验证。

## 导入验证

2026-10-18：Release 构建通过，0 警告、0 错误。一次性产物检查已执行：原始归档 6/6 文件通过 SHA-256 核对；3 个 Def XML 和 1 张贴图与原件一致；About 包名和静态初始化器 IL 中的 Harmony ID 均为新值；公开类型、字段和方法名/参数数量清单的 135 项一致；2 处 XML 类型引用可在重建 DLL 中解析；预览图为 960×540 PNG；未混入外部依赖 DLL。检查不证明游戏内行为等价，尚未进行 1.6 游戏内验证。

## 授权记录

2026-10-18，维护者明确确认已取得原作者同意，可 vendor、反编译，并以 MIT / Apache-2.0 接手。提供的聊天截图中有原作者身份确认及“放心做”的回复，但没有写出具体许可证名称；本目录按维护者的明确授权确认记录双许可，不将截图解释为版权转让。未将私人聊天截图复制进仓库。
