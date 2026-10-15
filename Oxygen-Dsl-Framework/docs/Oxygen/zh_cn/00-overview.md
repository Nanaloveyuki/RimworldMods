# Oxygen 入门与阅读路线

Oxygen 是 RimWorld 1.7 的玩法脚本语言。可以在存档中添加、修改和撤销动态心情，也可以让 Mod 用脚本声明原生特质、思想、运行规则和设置页。它保留简洁的 `fn`、花括号和命名实参，支持可选类型约束、引用语义的 `struct`、模块和受 trait 约束的函数。

这套手册说明当前已实现的能力。Oxygen 不是任意 C# 执行器，也不能替代所有 XML 和 C# Mod。宿主决定脚本能看见哪些对象、注册哪些内容、调用哪些操作。

## 先选对入口

同一种语言在不同入口中有不同的宿主能力：

- **动态心情窗口**：框架管理一个随存档保存的主脚本。使用 `mood`、`memory`，进入地图后应用到本局。
- **所有活动 Mod 的 `Oxygen/**/*.oxy`**：自动递归读取，文件名不决定用途。`config.oxy`、子目录文件或传统文件名都可声明原生内容、运行规则和设置页。
- **原生声明**：`template`／`define`／`patch` 在启动时注册，修改需要重启；同文件的规则和设置延后生效。
- **运行规则与设置**：Def 加载完成后生效；无原生声明的文件可重载。设置页仍需消费 Mod 接入设置存储。
- **纯函数／类型库**：可通过 `use` 导入，不会因为导入而注册玩法声明。
- **消费 Mod 自己的入口**：例如命名或事件回调。参数、返回类型和可读成员以该消费 Mod 的契约为准。

Mod 文件可合并设置、原生内容与运行规则；含原生声明的文件整体固定到重启。动态心情窗口和消费 Mod 自己的回调仍遵守各自宿主契约。检查通过不代表换一个宿主也能运行。

## 玩家：添加第一条心情

1. 安装并启用 Oxygen-Dsl-Framework，首次加载程序集需要启动游戏。
2. 进入一个测试存档。在框架 Mod 设置中打开脚本窗口。
3. 将下方完整脚本放入编辑区，选择“应用脚本”。它会立即评估一次。
4. 在运行检查中查看有效版本、规则、目标和条件；在 Pawn 需求页查看思想。
5. 修改 `offset` 后再次应用；要撤销本脚本拥有的效果，使用“停用”。

### 示例 00-A：饱食时的持续心情

需要动态心情宿主和有心情需求的殖民者；不是游戏外纯语言示例。

```oxygen
fn fed(pawn): bool {
    if pawn.food == null { return false }
    return pawn.food > 0.5 && !pawn.downed
}

mood("manual_fed_comfort",
    label: "吃饱了",
    description: "我吃饱了。现在感觉舒服些。",
    offset: 5,
    target: "colonists",
    when: fed)
```

`manual_fed_comfort` 是稳定规则 ID；`label` 和 `description` 是显示文本。条件成立时每个合格目标只有一个该规则的持续效果，条件失效时移除。`offset: 5` 是心情点数，不是把心情条设置为 5 或 0.5。原版心情条仍按自己的机制变化。

暂停时可使用“立即评估”查看结果，但持续时间和冷却不前进。错误应用保留上一份有效脚本，不会把正在生效的规则清空。

## 作者：先写不依赖游戏的逻辑

把计算放在普通函数里，再把结果交给注册接口。下面是一个独立的语言示例，不需要 Pawn、Def、翻译或 IrisMenus。

### 示例 00-B：计算并组织文本

适用于普通语言执行器／当前文件语法检查；不要作为动态心情主脚本直接应用，其中的顶层赋值和调用不是心情注册入口允许的顶层操作。

```oxygen
fn bonus(value: int): int {
    if value < 0 { return 0 }
    if value > 10 { return 10 }
    return value
}

let amount: int = bonus(12)
let messages: list<string> = [f"心情增加 {amount} 点。"]
messages.add("条件失效后移除。")
let text: string = core.text.join_nonempty("\n", messages)
```

`amount` 为 10，`text` 含两行说明。`let amount: int` 的类型约束会保持在后续赋值中；无标注的 `let` 则是动态绑定，不自动推断成固定类型。

## 作者：Mod 文件怎样组织

一个消费 Mod 可以保留现有 `About/About.xml`、加载目录配置和程序集，并按需添加：

- `Oxygen/config.oxy`：原生定义、运行规则、IrisMenus 页面均可声明，也可按需要拆成任意名称的 `.oxy` 文件。
- `Oxygen/helpers.oxy`、`Oxygen/gameplay/health.oxy`：可导入的纯函数／类型库。
- `Languages/ChineseSimplified/Keyed`、`Languages/English/Keyed`：`lang()` 查询的文本。
- `Languages/.../DefInjected/TraitDef`、`ThoughtDef`：原生 Def 显示内容的翻译。

框架 packageId 为 `nanaloveyuki.oxygendsl`。`use helpers@author.modid` 中的提供方是消费包的 packageId，不是文件夹名或程序集名。入口和模块都按活动 Mod 的加载文件夹规则选取，版本／DLC目录要与普通 Mod 包装一致。

## 文本、积木与网页编辑器

### 游戏内脚本窗口

“应用脚本”“立即评估”“停用／启用”“重新捕获选择”“从文件读取”是分开的操作。从文件读取是显式行为，框架不监视尚未写完的文件。编辑区的未应用修改不等于当前有效脚本。

### VSCode

`Tools/vscode` 插件提供 `.oxy` 和 Markdown 代码围栏高亮、片段、当前文件诊断、Outline 和声明导航。高亮与片段不要求 .NET；检查和导航需要插件宿主上的 .NET 9+ 与受信任工作区。未保存文本也会检查。

检查复用 Core 的 parser/analyzer，不执行脚本，不读取游戏 Def，也不解析导入模块的导出和依赖。当前不是 LSP，不能据此期待跨文件语义补全。

### 图形积木

“Oxygen: New Block Program” 创建 `.oxyblocks`。已有心情、原生内容、IrisMenus、囚犯命名和空白模板。画布能生成 `.oxy`，错误可定位回积木。`struct`、`trait`、`impl`、复杂泛型和循环仍使用文本入口。

保存积木 JSON、检查生成源码、导出 `.oxy` 是独立操作。游戏执行 `.oxy`，不执行 `.oxyblocks`；手改源码不会自动还原成积木。导出保护已有文本，文件变化或未保存修改会阻止覆盖。

### 本机网页编辑器

安装可选 IrisMenus 后，符合资格的活动消费 Mod 可提供编辑入口。资源随框架发布，玩家不需要安装 VSCode 或开发运行时。它只监听本机，文档范围由活动依赖 Mod 和消费端明确提供。

“保存”只保存文件或草稿；“应用到本局”仅在消费端提供 Apply 回调且草稿已保存时可用。原生内容没有热应用回调，保存后仍需重启。切换存档后旧的存档文档不能继续保存或应用。

## 示例与检查的阅读约定

每个 `oxygen` 围栏前有示例编号、入口和宿主要求。未特别标为“错误示例”或“片段”的代码是完整独立文件；多文件例子会明确文件名和相互依赖。不要把同一页的所有围栏直接拼接，它们可能重复声明名字。

- 纯语言例子：可由 parser/analyzer 检查，用普通执行器计算值。
- 动态心情例子：还需 `ScriptCompiler` 和游戏目标条件验证。
- 原生内容／规则例子：还需 `check`／`preview` 的 Mod、Def 目录和显式事实，以及游戏加载。
- 设置例子：还需 `OxygenSettingsCompiler`、消费 Mod 的设置存储；实际显示需要 IrisMenus。
- 含实际 `lang()` 调用的例子：执行时需要翻译宿主；离线 CLI 不会自动读取玩家语言库。

这些层次不能互相替代。先检查语法，再检查注册／引用，最后在备份存档观察真实效果。

## 六篇手册怎么读

1. 本页：选择入口，写第一条规则。
2. [关键词与语法](01-keywords-and-syntax.md)：关键字、表达式、控制流、作用域和函数。
3. [值、容器与标准函数](02-values-and-functions.md)：类型、转换、集合、字符串与内置扩展。
4. [玩法与原生内容](03-gameplay-and-content.md)：心情、记忆、Pawn 查询、Def、思想与闸门。
5. [模块、翻译与设置](04-modules-and-settings.md)：导入、库、`lang()` 和完整 IrisMenus 设置声明。
6. [进阶类型与注意事项](05-advanced-and-cautions.md)：`struct`、组合、`impl`、trait、泛型、预算与生命周期。

这六篇正文可以离线阅读。仓库的 `docs/syntax-draft.md` 保留为更细的语法和宿主接口参考；`docs/mvp.md` 说明动态心情契约，`docs/ide.md` 说明编辑器协议与操作边界。
