# 文案维护

## 编辑位置

- `Languages/ChineseSimplified/Keyed/MouseDisaster.xml`：设置、状态和通用交互。
- `Languages/ChineseSimplified/Keyed/MouseDisasterNarrative.xml`：穗音剧情、选项、记录簿和结局。
- `Languages/ChineseSimplified/Keyed/MouseDisasterUI.xml`：原先写在 C# 中的事件提示、交易和广播文本。
- `Languages/ChineseSimplified/Keyed/MouseDisasterIncidents.xml`：事件菜单名称。
- `Languages/ChineseSimplified/DefInjected/*/MouseDisasterText.xml`：Def 的名称、描述、信件、工作报告和基因效果说明。

修改文本时保留 key、`{0}` 等占位符及 `\n` 换行标记。剧情内容与参数含义需要同步检查实际调用方。

## 回退文件

DefInjected 按当前语言注入，没有逐字段回退到 English 的机制，因此不能直接删除 Def 中的基础文本。已导出的 377 个字段以 `MouseDisasterText.xml` 为编辑源，由脚本同步回 Def；不手动维护两份文案。

运行 `pwsh -NoProfile -File scripts/sync-language-fallbacks.ps1` 同步回退，使用 `-Check` 只检查、不写文件。

新迁移的 UI 和菜单文本在 `Languages/English/Keyed/` 有生成的中文回退。这是保留迁移前其他语言下的显示行为，不代表已经提供英文翻译。现有隐藏派系的 `MouseDisaster_Factions.xml` 保持独立，其原始英文 Def 默认值不被同步覆盖。

新增 Def 文案时，先定义基础字段，再在对应 DefInjected 文件中加入同名路径。嵌套列表路径使用从零开始的索引，例如 `DefName.stages.0.label`。脚本会拒绝不存在的 Def 或字段。

## 不迁移的内容

源码中的兼容性匹配关键字、Def 名称、存档键、包名、日志和程序集元数据不是玩家文案，不参与翻译。调试菜单 Attribute 的分类名需要编译期常量，保留在源码中。README、About 与 NOTICE 分别承担维护说明、模组元数据和归属说明，也不通过游戏语言系统加载。

## 验证

`pwsh -NoProfile -File scripts/verify-refactor.ps1` 检查运行时文案缺键、未使用键、占位符参数、事件目录及回退同步状态；叙事和 Pawn 组件仍使用各自的验证脚本。

在游戏内还需查看选项、信件、设置、基因效果和工作报告，确认语言注入与实际控件显示正常。
