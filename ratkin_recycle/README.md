# 鼠族回收行为 RatkinRecycle

原作者为菜地里的稽，Nanaloveyuki 经授权接手维护。本目录保留 1.6 发布内容，并提供独立的 1.7 构建。本文记录导入来源、构建方法和版本边界，后续维护可继续更新。

- 包名：`nanaloveyuki.ratkinrecycle`，原包名为 `dsh.ratkinrecycle`。
- 游戏版本：RimWorld **1.6 / 1.7**，分别加载各自程序集；[迁移记录与验证范围](docs/references/migration-1.7.md)。
- 当前维护版本：**0.0.1**；版本与更新规则适用于 1.7，1.6 发布件保持冻结。
- 许可证：**MIT OR Apache-2.0**，任选其一；完整文本见 [LICENSE](LICENSE)，署名和授权依据见 [NOTICE](NOTICE)。原作者版权保留。
- 依赖：Harmony、NewRatkinPlus；原说明建议同时启用 Toddlers。

## 导入内容

来源为 `E:\Apps\Steam\steamapps\common\RimWorld\Mods\鼠族回收行为 RatkinRecycle`。原目录共有 6 个文件：1 个 DLL、3 个 Def XML、1 张贴图和 `About.xml`，没有源码、许可证、预览图或工坊发布 ID。

[原始发行包归档](docs/references/upstream-original.tar.gz) 保留这 6 个文件的原始字节；[SHA-256 清单](docs/references/upstream-manifest.json) 记录逐文件大小、哈希和归档哈希。这些是长期来源资料，不随构建清理。归档仅用于核对，不应解压到启用中的 Mod 目录。

`Source/` 使用 ILSpy 11.0.0.9375 反编译恢复，不是原作者工程，也不包含原始注释。反编译时解析了 1.6 的游戏、Unity 和 Harmony 引用。`1.6/Assemblies/RatkinRecycle.dll` 是 vendor 时重建的接手版 DLL，已冻结；原作者 DLL 留在归档中。后续缺陷修复与设置扩展仅随 1.7 发布，1.7 独立编译为 .NET 10。

最初导入时的改动：

- 修改 About 包名和 Harmony ID，补充维护者署名及依赖工坊链接。
- 添加源码工程、许可证和来源记录。
- 添加 960×540 的排版预览图，使用原自动止血装置图标。

保留原程序集名 `RatkinRecycle`、命名空间、Def 名称和贴图。使用接手版时应取消旧版勾选，不要同时启用两份。原版按安装目录名和 Mod 类名定位设置文件，目录改为 `ratkin_recycle` 后不会自动找到旧目录名的设置；不添加旧目录配置迁移兼容层。1.7 的新身份选项采用安全默认值，不沿用旧的笼统阵营开关。

## 1.7 回收规则与设置

- 捕食者和被捕食者分别设置身份与生理年龄。年龄下限包含、上限不包含；两侧各有“年龄无上限”，有限年龄也可输入 10000 等数值，不限制在 100 岁。
- 默认双方都排除玩家自由殖民者；囚犯、奴隶、非敌对外来者、敌对外来者、无派系角色分别勾选。默认捕食者从 1 岁起且无年龄上限，被捕食者为 `[0, 3)` 岁。倒地目标可被选中，倒地捕食者不可行动。
- “啃食与部位”列出匹配种族身体中实际存在的部位类型，耳朵、鼠尾、手、脚、腿、鼻子等可分别勾选。只选择仍存在的天然部位；未勾选的部位不被直接咬中，但失去上级部位可能连带失去下级部位，伤口仍可能流血。新模组添加的部位默认不选。
- 没有可咬部位时停止；连续三口没有造成实际伤害（包括空伤害结果）时结束任务，经清理流程尝试放下目标，不发完成奖励或“吃饱”信件。有效伤害会清零连续失败次数，计数随任务存档保存。只有造成实际伤害才增加每口营养。完整奖励只结算本次回收啃咬直接造成的死亡，外来死亡不算完成。已携带其他物品的角色不会启动回收，任务中断只放下自身持有的目标或其尸体。
- 访客、商队等外来捕食者自身回收时，免除该行为引起的玩家好感损失与直接转敌；可追溯的回收伤口或纯回收流血导致延迟死亡时也适用。普通玩家攻击、玩家所属或玩家拘押捕食者、混合其他来源的流血仍按原版处理。伤害来源、死亡通知、任务与击杀归属不被整体跳过。
- 生产、精致食物、冷却历史归当前存档所有；捕食任务状态及伤口外交归因随存档保存。旧存档此前未保存的历史无法恢复；旧任务缺少状态时仅保守恢复持有关系，不补发历史奖励。

原版 Mod 设置入口保留六页：常规、捕食双方、啃食与部位、心情、止血装置、高级。可选启用 IrisMenus 后，相同页面注册为六个 SubItem，并支持搜索定位；IrisMenus 不是必要依赖。数字输入保留精度，打开界面不会将止血量 `0.03` 舍入。

## 构建

安装 .NET 10 SDK 后，在仓库根目录执行（默认构建 1.7）：

```sh
dotnet build ratkin_recycle/Source/RatkinRecycle.csproj -c Release
```

1.7 目标为 `net10.0 / x64`，直接引用当前 1.7 游戏根目录的 `Assembly-CSharp.dll`、`UnityStubs.dll` 及 Harmony 3 的 `1.7/Assemblies/0Harmony.dll`。默认路径见 `Directory.Build.props`；跨机器可通过 `-p:RimWorldDir=... -p:HarmonyAssemblyPath=...` 或环境变量 `RIMWORLD_DIR`、`HARMONY_ASSEMBLY_PATH` 覆盖。

可选 IrisMenus 适配器需另行构建，工程会先构建核心：

```sh
dotnet build ratkin_recycle/Source/IrisMenus/RatkinRecycle.IrisMenus.csproj -c Release
```

默认引用 monorepo 中的 `iris_menus/1.7/Assemblies/IrisMenus.dll`，可用 `-p:IrisMenusAssemblyPath=...` 覆盖。输出仅到 `1.7/IrisMenus/Assemblies/`，不复制 IrisMenus 或游戏 DLL。

1.7 输出仅到 `1.7/Assemblies/`，不会覆盖冻结的 1.6 DLL。需要核对 1.6 源码可重建性时执行：

```sh
dotnet build ratkin_recycle/Source/RatkinRecycle.csproj -c Release -p:RimWorldVersion=1.6
```

1.6 固定引用 `Krafs.Rimworld.Ref 1.6.4633`、`Lib.Harmony 2.4.1` 和 `Microsoft.NETFramework.ReferenceAssemblies 1.0.3`，输出到忽略目录 `tmp/rebuild-1.6/Assemblies/`，不会自动替换已发布 DLL。首次 NuGet 还原需要网络或本地缓存。

`LoadFolders.xml` 让 1.6 仅加载 `1.6/`，1.7 加载 `Common/` 和 `1.7/`；只有启用 IrisMenus 时额外加载 `1.7/IrisMenus/`。`Common` 的回收 Job 定义关闭原版通用携带物丢弃，让任务自身负责安全放下目标；其余 XML、贴图与冻结的 1.6 文件一致。中间文件位于按版本隔离的 `tmp/obj/`，外部引用不随 Mod 复制。冻结文件哈希见 [1.6 发布清单](docs/references/release-1.6-manifest.json)。

## 部署

关闭 RimWorld 后，从仓库根目录执行：

```sh
bash scripts/deploy.sh ratkin_recycle --rimworld-dir /mnt/e/Apps/Steam/steamapps/common/RimWorld
```

统一入口构建 1.7 核心和可选适配器，暂存至 `artifacts/ratkin_recycle/release`，逐文件校验 SHA-256 后复制到游戏 `Mods/ratkin_recycle`。首次安装携带冻结的 1.6 内容；后续部署保留目标中已有的 1.6 文件和 `PublishedFileId.txt`。原作者的旧目录保持不动，启用接手版前请取消旧版勾选。仅构建暂存可加 `--build-only`。

发布清单不包含 `README.md` 和 `docs/`；这些资料仅留在源码仓库。`LICENSE` 和 `NOTICE` 随 Mod 分发。

## 导入验证

2026-10-18：Release 构建通过，0 警告、0 错误。一次性产物检查已执行：原始归档 6/6 文件通过 SHA-256 核对；3 个 Def XML 和 1 张贴图与原件一致；About 包名和静态初始化器 IL 中的 Harmony ID 均为新值；公开类型、字段和方法名/参数数量清单的 135 项一致；2 处 XML 类型引用可在重建 DLL 中解析；预览图为 960×540 PNG；未混入外部依赖 DLL。检查不证明游戏内行为等价，尚未进行 1.6 游戏内验证。

1.7 迁移验证：正式 1.7 Release 与 1.6 源码重建均为 0 警告、0 错误；冻结的 1.6 文件哈希不变。实际 1.7 DLL 的无界面运行时冒烟验证了幼儿心情需求、生产/食物历史与时间边界、啃咬 deadline/营养、流血伤口治疗和版本目录选择。生产/吞食引擎主体、伤害应用和渲染被隔离；未验证地图寻路搬运、真实致死、范围扫描、设置界面或存读档，详见 [迁移记录](docs/references/migration-1.7.md)。

后续 1.7 缺陷修复已处理上述年龄换算、倒地目标、历史/任务持久化、搬运归属与设置精度问题。当前程序集冒烟覆盖真实 Scribe 存读档、身份/年龄/部位策略、扫描器调度、外交归因和设置交互；绘制、字体、寻路、预约及启动 Job 的引擎边界按场景隔离。真实地图的完整寻路搬运、伤害致死链和渲染界面尚未验收，不能将这些冒烟当成完整游戏验证。当前本机 Toddlers 仅声明支持到 1.6，不能据此宣称其 1.7 兼容。

## 版本记录

### 0.0.1 — 2026-10-20

- 修复伤害持续被阻挡时，非限时啃食阶段无法自行结束的风险：连续三口无伤害后终止，有效伤害清零计数，存读档保留计数。既有任务缺少该字段时从零开始。
- About、1.7 核心与 IrisMenus 适配器统一标记版本；冻结的 1.6 文件不变。
- Release 核心与适配器构建均为 0 警告、0 错误。一次性实际 DLL 冒烟在修复前捕获第三口后仍不结束，修复后 11 项行为检查通过，覆盖零伤害、空结果、有效伤害重置、结束清理与 Scribe 值存读档。
- 冒烟隔离了资格/持有判断、部位选择、伤害应用、饱腹判断、放置与调度器，验证了任务结束会调用清理动作；不证明真实地图上一定能放下目标，也不是本次“等待中”现象的归因或游戏内验收。无可复用的真实地图测试入口，未保留依赖私有方法与引擎替身的一次性探针。

## 授权记录

2026-10-18，维护者明确确认已取得原作者同意，可 vendor、反编译，并以 MIT / Apache-2.0 接手。提供的聊天截图中有原作者身份确认及“放心做”的回复，但没有写出具体许可证名称；本目录按维护者的明确授权确认记录双许可，不将截图解释为版权转让。未将私人聊天截图复制进仓库。
