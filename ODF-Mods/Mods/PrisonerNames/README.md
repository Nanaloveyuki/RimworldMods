# ODF 囚犯动态代号

包名：`odf.nanaloveyuki.prisonernames`。适用 RimWorld 1.6，依赖 Harmony、Oxygendsl 和 IrisMenus。独立实现，不依赖 RHAH；不要与 `Lezhizhong.PrisonerCodename` 同时启用。

发布图片均为黑底白字，主标题分两行显示 Prisoner Name / 囚犯代号。`About/Preview.png`（1536×1024）的小字副标题为 Oxygen DSL Framework；`About/ModIcon.png`（1254×1254）的副标题为 O.D.F.。

## 使用

1. 安装 Oxygendsl、IrisMenus 与本 Mod。IrisMenus 是硬依赖。进入存档后，在 IrisMenus 打开「ODF 囚犯代号」（英文为 ODF Prisoner Names）。
2. 「命名」页选择显示的姓名、设置年龄段和种族名称；「积分」页设置新囚犯的起始分数和自动加减分；「等级」页设置各等级需要的分数。跨页共用草稿；有效修改会自动应用到当前存档，无需点击「保存设置」。无效输入保留上一次有效设置；仍须保存游戏，才能在下次读档时保留。
   不需要编写代码。完整玩家说明集中在 IrisMenus 的「使用指南」页（英文为 User guide），使用 Markdown 显示步骤、名称示例、年龄与种族设置、单独代号、积分等级、保存方式和常见问题；主菜单也可查看。以下源码与 API 内容供高级自定义使用。
3. 完整规则和事件在 Oxygendsl 浏览器编辑器里改。当前存档有两份运行中文档：命名规则和事件规则。事件入口是 `fn on_event(pawn, event, state)`。
4. `state.get` / `state.set` 只读写当前事件的副本。事件出错时，这一名囚犯已保存的数值和分数保持原样，该事件不再重复。`pawn.value(key, default)` 读取已保存的布尔、文本、有限数字或 null。
5. 新囚犯使用当前设置的初始分数。手动分数和手动等级不会被事件改写。名单显示当前分数、等级和下一等级还差多少。
6. 新存档使用配置样式。旧存档若已有自定义规则，仍保持自定义，配置页不会覆盖那份草稿。默认规则读取已安装的 `Oxygen/default-*.oxy` 和 `Oxygen/events.oxy`。
7. 托管满 60000 个未暂停的游戏 tick 才发布一次 `prisoner.custody_day`。暂停、释放后的间隔和读档都不会补发。

代号只替换存活殖民地囚犯的标准显示标签，**不写 `Pawn.Name`**。释放、招募、死亡或关闭后恢复原名；直接读取 Name 的第三方 UI 仍可显示原名。规则、配置、草稿、编号、分数、单人覆盖及自定义值随存档保存，不是跨存档全局设置。设置页的有效修改会自动应用到当前存档；浏览器保存当前草稿并点击「应用到本局」后仍须保存游戏；保存 `Oxygen/` 安装文件则立即改变 Mod 文件，可能被订阅更新覆盖。

离线预览：`dotnet run --project Tools/PrisonerNames.Preview -- --source 文件 --facts '{...}' [--chinese] [--mod 模组目录]`。`--facts` 是 JSON 文本，不是文件路径。`--mod` 可重复，只读取该目录 `About/packageId` 对应的 `Oxygen/*.oxy`。

## 不写脚本的命名与分级

在「命名」页选择代号、短名、全名、名或姓。选择代号时可组合 `{race_prefix}{number_text}·{grade}·{original_name}`。**所有界面的占位符只用英文键**，名称正文和值仍可用中文、日文、韩文等任意语言。旧中文占位符须手工改成下表英文键，不自动迁移。规则内部按稳定 `defName` 精确匹配种族，如 `Human`、`Ratkin`；设置页选择器显示本地化名称并使用稳定代码，无需手工输入 `defName`。

| 英文占位符 | 含义 |
| --- | --- |
| `{number}`／`{number_text}` | 编号／至少三位的编号文本 |
| `{grade}`／`{score}` | 等级／分数 |
| `{race}`／`{race_label}`／`{race_prefix}` | 种族 ID／标签／配置前缀 |
| `{original_name}`／`{original_full_name}`／`{original_short}` | 原短名／原全名／原昵称 |
| `{first_name}`／`{last_name}` | 名／姓 |
| `{interaction_label}` | 主要处置方式 |

`{{` 和 `}}` 输出字面括号。未知占位符、非法代号或坏配置拒绝保存，保留上一有效配置。

### 年龄段与种族覆盖

新存档默认按生理年龄使用 `0–1`、`1–7`、`7–14`、`14–18`、`18+` 五段；下限包含、上限不包含，支持小数下限，最后一段无上限。可修改默认段的下限和名称、添加或删除年龄段；0 岁起始段必须保留，下限不能重复。修改／删除默认边界同步对应种族覆盖。

「种族年龄覆盖」按活动 Pawn 的人形种族选择，显示本地化名称并以稳定代码识别；缺少当前语言翻译时回退英文。选择器会发现当前已启用 Mod（包括依赖 HAR 的人形外星种族）提供的可用种族，本 Mod 不把 HAR 列为硬依赖。旧存档中来自已移除或未启用 Mod 的种族覆盖也会保留并显示，除非玩家主动移除。启用该种族覆盖后，可以只改部分年龄段；未改的段仍使用同一默认段，「恢复默认」撤销该段覆盖。不同种族相互独立。默认与种族规则分别保存中英文样式。

关闭「按年龄段命名」使用单一名称样式；预设按钮也明确切回单一样式。短名／全名／名／姓样式直接显示对应姓名。设置页的自动应用不会覆盖自定义脚本；只有明确切回样式才替换命名规则。旧配置存档没有年龄配置时保持原单一样式，年龄配置随游戏存档保存。


等级取当前分数已达到的**最高下限**，等于下限时立即升级；分数降低后按同一表降级，不依赖输入顺序。例如初始分数 40，基础等级 E，其余下限为 D=20、C=40、B=60、A=80：新囚犯从 C 开始，达到 60 显示 B，降至 39 显示 D。名单显示距下一下限的分数差，不预测尚未发生的行为或自动释放囚犯。

「每点抵抗的分数」「每托管一天的分数」「越狱分数」可以为正或负，默认均为 0。抵抗事件按实际减少量计分，越狱只记已确认事件；托管一天为 60000 未暂停的游戏 tick，不把释放间隔算进去。改初始分数只影响新囚犯，不批量重置已有记录。

名单中可直接设置单人代号、分数和等级。手动分数锁定事件计分但不阻止其他自定义值记录；清空分数再设置，恢复事件计分并保留当前分数。清空等级恢复阈值分级，清除单人代号恢复规则。配置模式和自定义规则模式分开：设置页的有效修改会自动应用，但不会覆盖已有手写规则；明确切回名称样式才替换。

## 浏览器编辑与持久化事件

IrisMenus 的「在浏览器编辑 Mod」二次确认后打开随 ODF 打包的本机网页。不是完整 VSCode；玩家无需 Node、.NET SDK 或网络下载。关闭网页、返回主菜单都不回收服务；手动点击 IrisMenus「退出编辑器」或退出游戏才结束。

若打开时报「Only active Oxygen dependents can be edited」，请更新完整 Mod 并重启游戏。RimWorld 会从元数据中剔除没有有效安装 URL 的非官方依赖；本包 `About.xml` 的 ODF／IrisMenus 依赖已补齐 `downloadUrl`，无需伪造依赖或放宽编辑器资格检查。

选择 `current/naming` 或 `current/events`，检查、保存，再点击「应用到本局」。两份草稿互不覆盖；错误应用保留上一有效规则。切换存档后旧文档拒绝保存／应用，应刷新并选择新存档文档。物理 `.oxy` 文件保存不等于应用到当前存档。

新建囚犯 `.oxyblocks` 模板可图形化组合种族、编号、等级与原名，选择 `current/naming` 导出并确认覆盖，再打开命名草稿应用；没有任意源码反向转积木。右侧 JSON 事实试算使用实际命名执行器，不读取或修改真实 Pawn，不是游戏行为模拟。

持久值通过事件副本修改，命名规则只读：

```oxygen
fn on_event(pawn, event, state) {
    if event.id == "prisoner.resistance_reduced" {
        state.set("cooperations", state.get("cooperations", 0) + 1)
        // state.score 已包含设置页的分数系数；这里可额外奖励。
        state.set("score", state.score + 2)
    }
}
```

支持 `prisoner.resistance_reduced`、`prisoner.custody_day`、`prisoner.prison_break`。`event.id`、`provider`、`sequence` 始终可读；抵抗事件另外含 `before`、`after`、`amount`、`actor_id`、`tick`。`state.score` 读分数，`state.set("score", ...)` 写分数；其他 key 使用 `state.get(key, default)`／`state.set(key, value)`。命名中用 `pawn.value("cooperations", 0)` 读取。只支持布尔、文本、有限数字和 null，不保存任意脚本对象或普通顶层变量。

设置页系数和事件脚本可以叠加；安装的示例脚本系数为 0。单次事件成功后一起提交分数和自定义值；异常全部回滚并报告错误，该事件不重放。此版本只提供上述确认事实，不声称追踪囚犯做过的所有行为。


## 编写规则

```oxygen
fn codename(pawn): string? {
    if pawn.age < 3 { return f"幼{pawn.number_text}" }
    let alias = f"囚{pawn.number_text}·{pawn.interaction_label}"
    if pawn.skill("Crafting") >= 10 { alias += "·工" }
    if pawn.bleeding { return alias + "·出血" }
    if pawn.sick { return alias + "·患病" }
    return alias
}
```

`string` 返回代号；`null` 或空白显示原名。最大 120 字符，不接受换行、控制字符和富文本标签。文件顶层允许 use、fn、struct、let、const，不允许 mood／memory 或任意内容注册。可用 `use math@std` 等现有标准库和活动 Mod 的 `Oxygen/` 模块；无任意文件、反射或游戏写入权限。

命名规则按分支顺序执行。`Oxygen/skill-tags.oxy` 是可粘贴示例。新存档和默认按钮读取已安装的 `Oxygen/default-*.oxy`；程序集内嵌文本只在安装文件缺失时使用。

### 可读值

| 成员 | 语义 |
| --- | --- |
| `number` / `number_text` | 本存档稳定编号；文本至少 3 位，如 `001` |
| `original_name` / `original_full_name` | 原始短名／全名；不包含动态代号 |
| `race` / `race_label` | 种族 defName／当前语言标签；鼠族示例 `Ratkin` |
| `age` / `gender` / `stage` | 生理年龄（含小数）、`Male/Female/None`、原生发育阶段 |
| `interaction` / `interaction_label` | 主要处置方式 defName／本地化标签 |
| `resistance` / `will` / `recruitable` | 当前抵抗、反抗意志、原生招募资格 |
| `health` / `bleeding` / `sick` / `downed` | 健康比例、正在出血、会引起原生疾病思想的疾病、倒地 |
| `food` / `rest` / `mood` / `hunger` | 原生需求值／类别；缺少需求时数值为 null |
| `name` / `id` | 原短名／游戏对象 ID；管理编号使用 number 更合适 |
| `original_short`／`first_name`／`last_name` | 原昵称、名、姓；不读动态代号 |
| `race_prefix`／`score`／`grade` | 当前配置种族前缀、分数及最终等级 |
| `manual_score`／`manual_grade` | 是否锁定手动分数／手动等级文本，未指定等级为 null |

### 查询函数

- `pawn.skill("Crafting")`：技能等级；未知或缺少技能为 0。
- `pawn.passion("Plants")`：0 无热情、1 热情、2 强烈热情。
- `pawn.has_trait("Industriousness", 1)`、`trait_degree(...)`、`trait_suppressed(...)`：沿用 Oxygen 特质查询。
- `pawn.has_hediff("Flu")`：实际存在该健康状态。
- `pawn.has_gene("...")`：实际激活该基因；无生物科技／基因列表时为 false。
- `pawn.has_interaction("HemogenFarm")`：包括供血等非互斥选项，不能只查 interaction 字符串。
- `pawn.value("key", default)`：当前囚犯持久值；省略默认值且 key 缺失时报错。
- `pawn.formatted("{grade}{number_text}")`：填充英文名称占位符；省略 pattern 时按当前种族、生理年龄、语言及配置选择样式。显式 pattern 不使用年龄选择。
- `lang("Key", name: pawn.original_name, grade: pawn.grade)`：读取当前游戏语言的原生 Keyed 翻译并替换命名参数，缺键显示键名；本机网页事实预览使用同一翻译宿主。离线预览未提供语言数据，实际调用 `lang()` 时明确报错，不伪造翻译。返回文本仍须通过代号校验；等级 ID 与显示文本分开。设置脚本用文本回调调用 `lang()`，不再依赖 `prisonernames.title` 等专用翻译成员。

常见 `interaction`：`MaintainOnly`、`AttemptRecruit`、`ReduceResistance`、`Release`、`Execution`；DLC 另有 `Enslave`、`ReduceWill`、`Convert`、`Study`、`Interrogate`。供血可能与招募等并存。

未来囚犯进入尚未命中的错误分支时，窗口显示错误，已有缓存代号保留，不改原名。函数执行有步数／深度上限；普通脚本变量不作为持久存档数据。移除本 Mod 后额外组件记录可能产生缺失类型提示，但原姓名不受影响；未声称完成无提示卸载验收。

## 验证

2026-10-10 的 Release 构建 0 警告、0 错误，20 项命名行为检查通过。直接链接生产代码的 30 项状态 smoke 覆盖初始分数、事件原子提交／回滚、阈值升级、单人覆盖、暂停／释放托管间隔、独立草稿、旧存档自定义规则迁移及记录恢复；游戏／Pawn 边界使用替身，不算真实游戏。

真实 RimWorld Scribe 在 Mono 中往返 XML，保留单人代号／分数／等级、编号、托管余量／基线及数字／文本／布尔／null；没有加载完整游戏或执行 Pawn 交叉引用。真实 Chromium 连接生产网页后端，验证源码保存、错误定位、显式事实试算、错误应用保留、积木编辑／保存／导出、版本冲突及同一会话重开。

通用 `lang()` 接入后再次 Release 构建及 20 项命名检查通过；真实 Verse 翻译器在独立 Mono 进程验证已安装中英 Keyed、动态设置回调、命名参数与实际命名事实预览。未启动完整游戏，未声称完成新增游戏设置窗口或语言切换 UI 验收。

2026-10-11 消费端 Release 构建 0 警告、0 错误，47 项行为检查通过；独立 RimWorld 验证编辑器确认／真实 Chromium 预览、设置分页与共享草稿保存、年龄段添加、种族部分覆盖、缺译英文回退及中英所属名称别名。真实囚犯按小数生理年龄跨段，原姓名不变；无效配置保留旧结果，自定义规则和草稿不被保存设置覆盖。新进程读回实际存档，保留六个年龄段、种族覆盖、分数、编号与代号；修正 Windows Scribe CRLF 导致解析失败。未修改玩家安装／配置。完整证据见相邻仓库 `docs/validation.md`；原生事件补丁、跨地图／商队和大型 Mod 组合未在本轮验收。

本日随后修正消费端重复导航：移除旧 `Oxygen/settings.oxy` 注册的「囚犯代号」general 页及专用桥接，开关和名单入口并入「命名」。IrisMenus 无需改动；隔离游戏实际显示唯一父级和三页，代号开关双向生效，名单按钮打开原有窗口，积分／等级仍可访问。Release 构建零警告、零错误，47 项行为检查通过。

设置文案随后改为面向玩家的简短说明，完整操作说明集中到一个中英双语「使用指南」页，由 IrisMenus Markdown 渲染。真实游戏主菜单验证两种语言的标题与加粗，以及中文指南滚动到文档末尾；进入存档后检查命名页短文案及中文积分／等级标签。Release 构建零警告、零错误，47 项行为检查通过。

本日最后完成 Humanlike 过滤、设置分区和自动应用。Release 构建 0 警告、0 错误，47 项行为检查通过；隔离游戏操作验证有效年龄样式、起始分数及新增等级即时应用，无效占位符保留旧配置并可修正，切换种族／规则范围和修改预览年龄不保存配置。积分自动应用保留高级命名规则和草稿，明确切回页面设置仍须确认。另存后以新进程读回，年龄样式、分数 43 和新增等级保留；真实动物 Pawn 被囚犯资格检查排除。

HAR 验收使用与指定 `Mods/839005762` 安装哈希一致的 HAR 1.6 程序集及实际 NewRatkinPlus。`Ratkin` 以 `AlienRace.ThingDef_AlienRace`、`Humanlike` 智能进入年龄覆盖与种族前缀两个原生下拉框；编辑后即时应用，设置预览显示 `Alien001`。干净新存档名单没有旧验收的 `ODF_SmokeEnglish` 覆盖。截图位于隔离配置的 `RimCarbon14/artifacts/354a000503514f86be0b2d10ce9f3ec3/`，年龄与前缀下拉分别为 `c5d052c812f24966a17181221ca53c23.png` 和 `eb1e74aa012c462f924c066dc2b8d399.png`；回执保留于忽略的 `tmp/settings-autosave-receipts.jsonl`。未部署到玩家安装，也未验证全部 HAR 种族包或大型 Mod 组合。

最终中文验收以正确语言目录 `ChineseSimplified (简体中文)` 启动新进程，实际设置分区、自动应用提示和使用指南无遮挡渲染；读档保留 Ratkin 前缀及 18 岁覆盖，活动候选为 Human、CreepJoiner、Ratkin，日志无异常。此前用反射在游戏中直接切换语言后出现原版 `UIRoot_Play` 空引用，遮挡截图不计为通过；未据此改动 Mod 代码。
