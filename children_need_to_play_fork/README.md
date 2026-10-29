**不要上传到STEAM创意工坊, 本Fork版经原作者许可允许Fork但不允许发布, 切勿私自发布**

# The Children Need To Play

RimWorld 1.6 / 1.7 mod (`drati.ToddlersPlayExpanded.fork`), fork **1.3.0** — an expansion of Cyanobot's **Toddlers** mod (requires Harmony + Toddlers).

## Fork 来源与维护边界

- 原作者：**Dratic95**；上游：[The Children Need To Play](https://steamcommunity.com/sharedfiles/filedetails/?id=3738445489)。
- 2026-10-29 从本机 Steam Workshop 目录导入 **1.2.2**，作为普通源码目录维护，不是 Git submodule。
- 保留上游 [MIT 许可证](LICENSE)及版权署名；根仓库双许可不覆盖本目录的上游内容。
- 原作者于 2026-10-29 在 Discord 回复：`u can fork it just dont release it please`。本 fork 遵守该不发布约定；上述警告不改写 MIT 许可证。
- 本 fork 包名追加 `.fork`，移除 `About/PublishedFileId.txt`；不上传 Steam 创意工坊，也不作为独立模组发布。
- 首次导入保留上游玩法源码与程序集；当前 fork 另含下述派工、哭闹、移除、本地化及轮询改动。

## 设置与 IrisMenus

设置分为「幼儿情绪」「幼儿活动」「儿童情绪」「儿童游玩」「玩偶睡眠」「视听效果」「调试」七类，沿用已有保存字段；新增哭闹选项单独保存。

- **未安装 IrisMenus**：游戏原生 Mod 设置窗口提供全部分类和选项，无需额外依赖。
- **安装 IrisMenus**：自动注册七个分类子页，不重复绘制原生分类按钮。当前 1.7 IrisMenus 的 `MenuPresentation.Listing` 提供 Light／Dark 控件、原生滚动条及说明浮层；成人玩偶心情模式使用原生互斥选择控件。
- 不引用、不附带 IrisMenus DLL，不新增硬依赖；旧版 IrisMenus 缺少原生页面工厂时仍可使用分类列表。
- 切换页面、关闭时沿用 IrisMenus 的 `WriteSettings` 保存流程；原生窗口仍使用游戏自身的保存流程。

构建：`dotnet build children_need_to_play_fork/Source/ToddlersPlayExpanded.csproj -c Release`。默认读取本机 Steam 的 Toddlers 1.6 程序集，可用 `-p:ToddlersDll=/path/to/Toddlers.dll` 覆盖。仍以 `net472` 构建，不升级或改写上游玩法程序集接口。

本地部署：`scripts/deploy.sh children_need_to_play_fork --rimworld-dir /path/to/RimWorld`。目标为 `Mods/ChildrenNeedToPlayFork`，与上游目录分离；保留 fork 警告，不包含创意工坊发布 ID。

## Fork 玩法调整

- 自主游玩仅在「随意」「娱乐」时段发起；工作、睡眠、学习及其他专用时段优先。已有工作分配与玩家强制命令受保护，囚犯仍可在空闲时游玩。
- 正常、紧急、空闲和婴儿床活动共用睡眠／医疗休养判断；运行中的自主游玩每 50 tick 复查，含前往活动地点的过程。
- 家长对每次哭闹各自决定是否冷处理，默认概率 **80%**，范围 **0–100%**；结果随该次哭闹保存。冷处理的家长忽略这次哭声的干扰，孩子获得「我要闹了!没有人理我!」：**−5 心情，持续 12 小时**。
- **7–13 岁哭闹默认关闭**，与低龄儿童分别控制，且要求游戏仍将角色视为儿童。启用后，持续时间和对周围人的心情惩罚减半；普通人类 13 岁进入成人阶段后不适用。
- 泥土城堡可以选中、受攻击及拆除，保留原有自动消失时间。
- 内置简体中文，覆盖设置、活动报告、菜单、物品和思想；翻译不改变存档标识。
- 减少心情轮询中的临时列表、重复角色筛选与定义查找；保留环境与目击判断所需的定期检查。耗时改善需要实际存档分析，未给出 TPS 提升承诺。

验证：Release 构建通过（0 错误、307 条警告）；临时运行器对本机 1.7 引擎程序集执行排程规则、城堡定义／拆除资格、游玩状态清理及哭闹决策保存回读，九个新增补丁组均使用游戏安装的 Harmony 成功挂接；语言 XML 与中英占位符检查通过。游戏内点击移除、实际卧床恢复、哭声范围和完整存档性能仍需玩家验证。

## 上游功能

- **Toddlers:** many new play activities, toys and a Toy Workbench, the Biotech toy box as toy storage, crib lock / crib gate, random and event-triggered moods, plush-to-bed sleep, a Bubble Machine.
- **Children (3–13):** an optional Play need with rolled Play Desires (two at a time, every 8 hours), a Learning schedule assignment, child crying (3–6), and plush-to-bed sleep.
- **Adults (13+):** optional plush-to-bed sleep with configurable moods.

See `Architecture.md` for the code map and `CHANGELOG.md` for release notes.
