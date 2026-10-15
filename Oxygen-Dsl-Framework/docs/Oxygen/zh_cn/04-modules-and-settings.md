# 模块、翻译与 IrisMenus 设置

模块把可复用逻辑与玩法入口分开；翻译宿主把文本与稳定 ID 分开；设置宿主把页面声明与消费 Mod 自己的持久状态分开。它们都不是任意文件或 CLR 访问接口。

## use 的完整写法

`use module.path@provider [as alias]` 导入确定提供方的模块；可选依赖写成 `optional use module.path@provider [as alias]`。

- module.path：一个或多个标识符，用点分隔。
- provider：Mod packageId 或标准提供方 std，不是程序集名、文件夹或 Workshop ID。含连字符等字符时用引号。
- 默认别名：模块路径最后一段；as 可改名。
- 模块路径／导出符号大小写敏感；游戏包名匹配不区分大小写。保持文件路径大小写一致，文件查找仍受操作系统规则影响。
- use 只允许在文件顶层，必须放在所有非导入声明／语句之前。

同一模块可以导入成两个不同别名，共享当前脚本实例中的模块实例与类型身份；相同别名重复、与声明／内置名字冲突会报错。导入绑定只读，不能重新赋值。

不支持无 @ 的旧 Arti 导入、星号导入、选择性导入、group use 或后置 use optional。不会自动下载、搜索其他 Mod 的同名模块或回退到当前工作目录。

## 写一个库和调用者

脚本库默认私有，只有 `pub fn`、`pub struct`、`pub trait`、`pub const` 导出；不导出 let，不写 pub impl。库顶层只允许导入和声明，不放 mood、memory 或其他玩法注册。

### 示例 04-A：`Oxygen/helpers.oxy`

**库模块文件，检查时 `isModule: true`（export 模式）**。假设它属于 packageId 为 `example.balance` 的活动消费 Mod；除模块加载外没有游戏要求。

```oxygen
pub const maximum: int = 10

pub struct Stats {
    bonus: int = 5
}

pub fn clamp_bonus(value: int): int {
    if value < 0 { return 0 }
    if value > maximum { return maximum }
    return value
}
```

### 示例 04-B：导入该库的主脚本

完整普通语言主脚本，检查时 `isModule: false`。需要示例 04-A 作为 `helpers@example.balance`；不作为动态心情主脚本直接应用。

```oxygen
use helpers@example.balance as helpers

let stats: helpers.Stats = helpers.Stats(bonus: 12)
let amount: int = helpers.clamp_bonus(stats.bonus)
let maximum: int = helpers.maximum
```

导入的类型要写限定名 `helpers.Stats`，构造同样用该名字。它的名义身份包含提供方、模块路径和声明名，不因本地 alias 改变。一个 Mod 里两个不同模块导出的同名 Stats 是不同类型。

游戏内 `helpers@example.balance` 查对应活动 Mod 的 `Oxygen/helpers.oxy`；`gameplay.health@example.balance` 查 `Oxygen/gameplay/health.oxy`。不需要额外 XML 模块注册清单，但 Mod 仍需正常 About 和加载目录包装。导入库不会自动启用提供方的内容注册入口。

## optional use 只处理真正缺失

真正缺失的可选模块只提供 `alias.available == false`；可用模块统一提供 available 为 true。available 是加载器保留成员，模块不能自己导出或控制它。

### 示例 04-C：可选辅助模块

完整普通语言主脚本，`isModule: false`。可以缺少 example.balance；模块存在时须使用示例 04-A 的导出。没有游戏宿主要求。

```oxygen
optional use helpers@example.balance as helpers

let amount: int = 5
if helpers.available {
    amount = helpers.clamp_bonus(12)
}
```

optional 不吞掉文件读取异常、语法／分析错误、初始化异常或导入循环。坏模块不是缺失模块，不能静默跳过。先可选导入缺失模块，再用另一个别名普通导入同一模块，普通导入仍失败。

类型在执行分支前检查：即使 `if helpers.available` 在运行时为假，未调用函数或不可达分支出现未知 `helpers.Stats`／`helpers.Named` 也会拒绝载入。可选依赖中避免引用缺失模块的类型；需要该类型时，应把依赖明确设为必需，或由已有宿主提供固定契约。

## 模块初始化、缓存与失败

先验证依赖，再初始化依赖和导入者。每个模块只在当前脚本实例初始化一次，不是跨脚本的全局单例。模块顶层 let 可被导出函数捕获，但重新应用和读档重新建立该状态。

导入缺失提供方／未启用包、不存在模块、未导出符号和循环依赖都会报错；循环错误带依赖链。库更新不是文件监视：下一次明确应用或读档才重新读取。

动态心情应用或设置重载失败保留上一份有效计划；读档没有可假定的旧模块快照，依赖缺失时停用。保存主脚本不等于保存导入模块源码。可信原生回调已发生的副作用不随失败回滚，所以库初始化应限于建立自身状态。

## 默认能力和标准扩展

基础类型、转换、容器、range、len、exists、字符串函数和 core 命名空间免导入。基础 `+ - * / % ! && ||` 不需要 math 或 logic。

当前内置扩展为：

- `use math@std`：`clamp(value, min, max)`。
- `use logic@std`：`all(list<bool>)`、`any(list<bool>)`。
- `use cs@std`：受控 `Int32` 与 `int32(value)`。

其它 std 名称必须由宿主明确提供；`use string@std` 不会替基础字符串“解锁”。`pawn`、`game`、`mood`、`memory` 保留免导入的游戏宿主身份，不属于游戏外通用标准库。

第三方安全原生模块可由 `IOxygenModuleProvider`／`OxygenHostModuleRegistry` 提供只读命名空间和显式调用，设置宿主通过 `OxygenSettingsHost.Modules` 注册。它不必挤入 std，也没有自动反射消费 Mod 的对象或字段。

## lang：查询当前语言

`lang("Key", name: value, ...)` 免导入，返回 string。唯一位置参数必须是非空字符串键，其余参数全部具名。替换值只能是 string、有限数字、bool 或 null，不能是 Pawn、struct、集合、函数和任意 CLR 对象。

游戏宿主查询已加载 `Languages/.../Keyed`，使用原版默认语言回退；找不到键返回键名。查询别的 Mod 的键不会自动加载那个 Mod。翻译里的 `{name}` 对应命名参数，不是 Oxygen 表达式。

### 示例 04-D：延迟翻译的函数

完整普通语言文件，`isModule: false`。定义时无需翻译数据；执行最后一行需要 `IOxygenLocalization` 或游戏翻译宿主。MyMod_Welcome 由消费 Mod 的 Keyed 提供，未找到时显示键名。

```oxygen
fn welcome(name: string): string {
    return lang("MyMod_Welcome", name: name)
}

let greeting: string = welcome("Oxygen")
```

对应 Keyed 文件可写：

```xml
<LanguageData>
  <MyMod_Welcome>你好，{name}。</MyMod_Welcome>
</LanguageData>
```

英文文件使用相同键。显示文本可以翻译，但规则 ID、DefName、页面 ID、设置 key、选项 value 和等级 ID 必须稳定，不能使用翻译结果。

需要跟随语言变化的页面标题和控件文本，在回调里调用 lang。顶层 `let title = lang(...)` 只在载入时取一次。输出不会自动转义或校验富文本，消费端的代号长度／控制字符等限制仍生效。

Core 不引用 Verse；离线实际调用时没有翻译宿主会报带位置错误，现有 CLI 不自动读取玩家语言库。含原生声明的文件在加载期不能实际执行 lang，Def 的 label／description 用 DefInjected；延后调用的设置／规则回调可查询当前语言。

## IrisMenus 的接入前提

IrisMenus 启用时，Mod 文件宿主提供 `irismenus@std`；未启用时视为缺失。消费 Mod 必须接入 `OxygenSettingsHost` 并提供现有设置存储。页面可声明在自己的 `Oxygen/` 下任意 `.oxy` 文件，例如 `config.oxy`，也可与原生内容／运行规则共存；文件名没有特殊语义。动态心情主脚本仍是独立宿主。

C# 消费端使用 `OxygenSettingsHost.Register(owner, values)` 注册，用 `OxygenSettingsHost.Reload(owner)` 明确重载。values 实现 `IOxygenSettingsValues`：Get 接收 key、scope、defaultValue，Set 接收 key、scope、value，GameAvailable 表示当前是否有可操作的存档。Mod 构造时注册，加载会延后以适应可选适配器初始化。

这些是现有宿主接口，不会自动生成第二份设置存储，不反射字段。消费者保持原设置 key 和保存范围。没有 IrisMenus 时应保留原版设置绘制；可选依赖不会强制安装菜单 Mod。

## registry 和页面结构

`irismenus.registry(pages: [...])` 只接受 pages 命名实参。一次编译可以调用多次，最终合并验证并一次替换本 owner 的 `oxygen.settings` 注册组。

每页必填：

- `id`：非空，所有页中唯一。
- `title`：string 或返回 string 的零参函数。
- `items`：控件列表。
- 可选 `scope`：默认 global，可用 game。

控件通用成员：

- 必填 `id`、`kind`、`label`；控件 ID 在本页内唯一。
- `label`、`description`：string 或零参 string 回调；description 默认空文本。
- `visible`、`enabled`：bool 或零参 bool 回调，默认 true。
- 可写值控件的 `key`、`default`；可选 `changed(value)`。

未知页面／控件字段和无效值拒绝。设置声明使用动态 object；不要用 struct 替代页面或控件对象。

## 所有控件种类

- `label`：说明文字，不写设置。
- `section`：分节文字，不写设置。
- `checkbox`：key、bool default。
- `number`：key、int 或 float default；可设 min／max。整数框限制 Int32 和整数边界。
- `slider`：key、数值 default，使用 float 滑条；默认范围 0..1，可设 min／max。
- `text`：key、string default。
- `select`：key、string default、options。options 为 `[{value: "id", label: "标题"}]`，或返回该列表的零参函数；value 不重复。
- `button`：零参 action 函数，不持有设置值。

数值和边界须有限，min ≤ default ≤ max；default 是缺失值读取的回退参数，不会自动写入或覆盖旧值。数值框／滑条改变的是消费 Mod 的既有值，不能仅凭 default 宣称改变原生内容。

### 示例 04-E：包含所有控件的设置页

完整 `Oxygen/settings.oxy`，`isModule: false`。需要 `OxygenSettingsCompiler`；实际页面和持久值需要消费端设置宿主及 IrisMenus。使用固定中文文本，执行无需翻译宿主。按钮统计本次脚本实例内的点击，重载后清零；这不是存档状态。

```oxygen
optional use irismenus@std

let clicks: int = 0
let changes: int = 0
fn click_title(): string { return f"点击计数：{clicks}" }
fn count_click(): void { clicks += 1 }
fn count_change(value): void { changes += 1 }
fn change_title(): string { return f"本次页面修改次数：{changes}" }
fn modes(): list<object> {
    return [
        {value: "normal", label: "普通"},
        {value: "quiet", label: "安静"}
    ]
}

if irismenus.available {
    irismenus.registry(pages: [{
        id: "general",
        title: "常规",
        scope: "global",
        items: [
            {id: "intro", kind: "label", label: "调整此 Mod 的现有设置。"},
            {id: "main_section", kind: "section", label: "常规选项"},
            {id: "enabled", kind: "checkbox", label: "启用效果",
                key: "enabled", default: true, changed: count_change},
            {id: "interval", kind: "number", label: "评估间隔（tick）",
                key: "interval", default: 150, min: 1, max: 60000},
            {id: "strength", kind: "slider", label: "强度",
                key: "strength", default: 0.5, min: 0, max: 1},
            {id: "prefix", kind: "text", label: "文本前缀",
                key: "prefix", default: ""},
            {id: "mode", kind: "select", label: "模式",
                key: "mode", default: "normal", options: modes},
            {id: "clicks", kind: "button", label: click_title, action: count_click},
            {id: "changes", kind: "label", label: change_title}
        ]
    }, {
        id: "current_game",
        title: "当前存档",
        scope: "game",
        items: [
            {id: "game_enabled", kind: "checkbox", label: "在本局启用效果",
                description: "此值由消费 Mod 随存档保存。",
                key: "game_enabled", default: true}
        ]
    }])
}
```

改变这些 key 后，消费 Mod 还须在自己的玩法中读取它们。页面声明不是任意修改玩法的自动接线工具。

### 示例 04-F：随语言变化的页面

独立完整 `Oxygen/settings.oxy`，不与示例 04-E 拼接。需要设置宿主；IrisMenus 可选；显示回调实际运行需要翻译宿主和 MyMod_Settings／MyMod_Enabled 的 Keyed。示例采用现有 lang 契约，不要求新增 API。

```oxygen
optional use irismenus@std

fn page_title(): string { return lang("MyMod_Settings") }
fn enabled_label(): string { return lang("MyMod_Enabled") }

if irismenus.available {
    irismenus.registry(pages: [{
        id: "general",
        title: page_title,
        scope: "global",
        items: [{id: "enabled", kind: "checkbox", label: enabled_label,
            key: "enabled", default: true}]
    }])
}
```

## 设置重载、保存与回调失败

- 同 ID 页面重载保留导航和滚动；省略页面被移除，空计划清除此注册组。
- 编译或注册冲突失败保留旧页；页面结构重载时更新，文本和状态回调在使用时求值。
- 游戏页在没有存档时仍显示说明，交互禁用；global 页离开／关闭时调用 owner.WriteSettings，game 页由消费 Mod 正常存档保存。
- changed 在 Set 完成后读取实际值再调用；回调失败不撤销已经完成的 Set。
- 动态 options 在点击时重新验证；重载前的旧弹出菜单不能继续向替换后的页面写入。
- 回调在游戏线程执行，要短小；原生 C# 回调耗时和副作用不受解释器步数预算约束。

IrisMenus 以可用宽度绘制并滚动，标签和描述可换行。需要回读设置或执行消费端行为时，只调用消费 Mod 实际导出的原生模块，不虚构 `irismenus.get`／`set`／`save` 等接口。

## 编辑器与离线检查如何配合

当前文件检查不解析导出成员、依赖文件或 Def。例 04-A 用模块模式；其余主脚本用普通模式。检查例 04-B／04-C 的实际行为要给执行器正确模块目录／注册表；设置例交给设置编译器，lang 实际调用交给翻译宿主。

原生消费包可用 `Oxygendsl.Tool check MOD_ROOT --catalog DEFS_ROOT --mod OTHER_MOD_ROOT` 检查，`--catalog` 可重复；`preview MOD_ROOT --facts facts.json` 只读显式事实运行规则，不创造游戏状态。仅确认 Def 目录完整时才加 `--catalog-complete`；退出码 0 成功、1 错误、2 不完整或引用未知。

网页编辑器与 VSCode 都能查未保存文本；只有保存／导出到正确 `.oxy` 再按入口应用，才改变玩法。[进阶篇](05-advanced-and-cautions.md) 继续说明类型抽象和常见失败边界。
