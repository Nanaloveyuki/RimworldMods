# Oxygen DSL 语法

本文定义已实现的 Oxygen DSL 语法与可观察语义：保留 Arti 的可读、简洁写法，增加可选的后置类型约束、`struct` 与两种组合，以及明确的模块导入机制。

当前宿主包含 [动态心情](mvp.md) 和下方的原生内容／规则接口；类型和模块本身不授予任意游戏操作权限。后续方向见 [需求解析](requirements.md)，已运行证据见 [validation.md](validation.md)。

## 阅读约定

- 省略类型标注表示动态类型，不表示自动推断为固定类型。
- `struct` 采用引用语义，支持普通命名字段组合和显式嵌入；没有隐式复制或继承。
- 基础标准能力免导入；内置扩展使用 `use name@std`，Mod 模块使用 `use name@packageId`。
- Mod 模块由活动 Mod 根目录下的 `Oxygen/` 提供；具体路径与嵌入式使用见模块章节。

## 基础写法

保留 Arti 的 `fn`、`let`、`const`、`return`、命名实参与花括号，不改用 `function`、`def` 或缩进块。换行分隔语句，分号可选；缩进只用于阅读。注释使用 `//` 或 `/* ... */`；块注释可跨行、不嵌套，若包含换行仍提供语句分隔。

```oxygen
let value = 1
value = "one"                 // 无标注绑定允许改变类型
let count: int = 1
const title: string = "Oxygen"

fn a(b: int) {}                // 合法；返回类型未标注
fn add(a: int, b: int): int {
    return a + b
}
fn describe(value): string {
    return f"value={value}"
}
let result = add(1, b: 2)
```

控制流继续使用 `if condition { ... } else { ... }`、`for value in values { ... }`、`while condition { ... }`，以及循环内的 `break`、`continue`。

```oxygen
let total = 0
for value in range(0, 3) {
    total += value
}
while total < 5 {
    total += 1
}
if total == 5 {
    total = 0
} else {
    total = -1
}
```

### 参数、返回与多值

- 声明与参数的标注为 `name: Type`；返回标注统一为 `fn name(...): Type { ... }`，不另设 `-> Type` 写法。
- 位置实参必须在命名实参之前。重复、未知、遗漏参数或同一参数被位置与命名实参重复绑定，均报错。
- 参数、实参、集合字面量允许尾逗号，括号和集合内部允许换行；这些换行不分隔外层语句。
- 逗号多值返回与解包继续使用有序集合，不引入独立 tuple 类型；`_` 是丢弃位置。多目标解包暂不增加逐项类型标注。
- 不提供函数重载、默认函数参数或匿名函数。

```oxygen
fn pair(value) {
    return value, value + 1
}
let first, second = pair(1)
first, second = second, first
let _, kept = pair(3)
```

### 绑定与作用域

`let` 必须初始化，可重新赋值；重新赋值必须命中已有的可写绑定。支持 `= += -= *= /= %=`，赋值是语句，不是表达式。成员与索引的可写范围见集合和 struct 章节，不据此开放宿主对象写入。

`const` 是只读绑定，不是深度不可变。初始化保留静态表达式限制：字面量、const 引用及其纯运算、插值、集合字面量、索引/成员组合；不允许调用、let 引用或宿主动态值。因此 `const x = Stats()` 非法，实例应使用 let 保存。

沿用 Arti 作用域：只有函数建立独立局部作用域，控制流块不隔离绑定；函数可捕获外部变量。同一语句列表重复声明报错，不把花括号改成 Kotlin/Rust 风格的块作用域。

### 字符串与表达式

保留单引号、双引号、三引号与小写 `f` 插值：

```oxygen
let name = 'Oxygen'
let greeting = f"hello {name}"
let text = """第一行
第二行"""
let braces = f"{{{name}}}"
```

三引号保留换行和缩进，物理 CRLF/CR 统一为 LF；普通引号直接跨行、相邻字符串拼接及反斜杠紧接物理换行的续行仍沿用 Arti。转义为 `\n`、`\r`、`\t`、`\0`、`\\`、`\"`、`\'`、`\uXXXX`。插值按从左到右的顺序各求值一次；字面大括号写 `{{` / `}}`，不支持 Python 格式后缀或调试插值。

表达式优先级由高到低：后缀访问/调用/索引，单目 `! + -`，`* / %`，`+ -`，`< <= > >=`，`== !=`，`??`，`&&`，`||`，管道 `|`。二元运算左结合；`??` 只针对 null，管道把左值作为右侧可调用对象的第一个实参。

## 弱类型与后置标注

### 可选约束，不是固定类型推断

无标注变量、参数、字段与返回值等价于 `any`；类型标注可逐项省略，不自动推断为固定类型。标注后的约束附着于绑定、字段或容器，后续重新赋值不能移除；参数绑定也遵守这个规则。

```oxygen
let flexible = 1
flexible = "one"               // 合法
let fixed: int = 1
fixed = "one"                  // 类型错误
fn mixed(first: int, second) {
    second = "changed"         // 未标注参数仍是动态绑定
    first = "changed"          // 类型错误
}
```

标识符与类型名大小写敏感。内置类型名是类型上下文中的名字，不是禁止在表达式中使用的词法关键字，例如 `string(value)` 仍合法；用户声明和导入别名不能遮蔽内置类型名。

| 类型 | 含义 | 例子 |
| --- | --- | --- |
| `any` | 任意值，含 null；不免除实际调用与成员访问检查 | `let x: any = 1` |
| `int` | 有符号 32 位整数 | `let tick: int = 150` |
| `float` | 64 位浮点数，沿用现有浮点字面量精度 | `let ratio: float = 0.25` |
| `bool` | true / false | `let enabled: bool = true` |
| `string` | Unicode 文本 | `let name: string = "穗音"` |
| `list<T>` / `list` | 可变长度集合；裸 list 等价于 `list<any>` | `let names: list<string> = []` |
| `array<T>` / `array` | 长度固定、元素可写的集合；裸 array 等价于 `array<any>` | `let slots: array<int> = [1, 2]` |
| `object` | 动态字符串键对象；不是任意 CLR object，也不是 struct 基类 | `let config: object = {level: 1}` |
| `T?` | T 或 null；除 any 外，普通类型默认不接受 null | `let note: string? = null` |
| `void` | 只用于函数/方法返回位置，不是可保存的值类型 | `fn reset(): void {}` |
| 用户 struct / `alias.Name` | 名义类型，导入类型使用模块别名限定 | `let state: State = State()`（假设已声明 State） |

集合类型可嵌套：`list<array<int>>`、`list<int?>`、`list<int>?`；后两者分别是可空元素与可空集合。不提供泛型 struct、联合类型、继承或任意 CLR 类型名语法。

### 检查与返回

在初始化、重新赋值、参数绑定、函数返回、字段写入及集合插入/写入时检查类型。any 值进入受约束位置时检查实际值；已静态确定的不匹配提前诊断。

- 未标注返回值可为任意值，裸 return 或执行至函数底部返回 null。
- 返回类型为 void 时只允许裸 return 或执行至函数底部。
- 返回类型为其他类型时，每次返回都检查约束，包括隐式 null；`fn f(): int {}` 不满足返回约束，`fn f(): int? {}` 则允许返回 null。

只隐式允许 `int → float`，不隐式允许 `float → int`、数字文本 → 数字、数字 → bool、动态 object → struct 或 list → array。整数溢出、除零和无效转换报错，不静默回绕或转为字符串。`/` 返回 float，其他整数算术结果须在 int 范围内。

### 显式转换

基础转换自动可用，不需要 use：

| 写法 | 输入与结果 |
| --- | --- |
| `int(value)` | int、范围内且无小数部分的有限 float、可解析的十进制整数文本 → int |
| `float(value)` | 数值、可解析的数值文本 → float |
| `string(value)` | 保留 Arti 文本化：null → 空串，bool → true/false，数字使用无区域依赖的格式 |
| `bool(value)` | 使用下述条件真值规则 |

int/float 转换拒绝 bool 和 null；失败报错，不返回零或 null 兜底。C# 兼容扩展可显式增加受控包装类型的解包，见模块与库章节。

### 保留的上下文转换

为保持 Arti 可读性，只在两个特定上下文保留宽松转换，不扩散到赋值或数值操作：

1. 条件以及 `!`、`&&`、`||` 使用 Arti 真值：null、false、数值零、空白字符串、空集合为假，其他实例为真；函数直接作条件报错。`&&` / `||` 短路且返回 bool。这不使 `let flag: bool = 1` 合法，显式 `bool(1)` 才合法。
2. 只要 `+` 任意一侧为 string，就文本化另一侧再拼接；插值使用相同文本化。因此 `"count=" + 2` 得到 `"count=2"`，`"2" - 1` 则报数值操作错误。

数值相等允许 int/float 同值相等；其他不同类型不自动互转。引用容器与 struct 的相等规则见对应章节。

## 集合与动态对象

### list 与 array

list 可变长，array 长度固定但元素可写。无类型上下文的 `[1, "two"]` 和空 `[]` 一律创建 `list<any>`；不把它与固定长度 array 混为一谈。

直接位于受标注的初始化、赋值、参数、返回或字段位置的 `[...]`，根据该位置的 `list<T>` / `array<T>` 逐项检查并直接创建目标集合，不先建立动态 list 再复制。可空集合仍以非空部分提供类型上下文，括号分组不改变这个规则。已有变量、函数结果或其他复合表达式不做隐式集合转换。

```oxygen
let loose = [1, "two"]
let names: list<string> = ["A", "B"]
let slots: array<int> = [10, 20, 30]
let converted = array<int>([10, 20, 30])
let empty = list<string>()
names.add("C")
slots[0] = 40
```

### 构造与元素约束

- `list()` / `array()` 创建空的 any 集合；`list<T>()` / `array<T>()` 创建空的受约束集合。
- `list<T>(source)` / `array<T>(source)` 接受一个 list 或 array，检查全部元素，创建新的浅层容器。裸 `list(source)` / `array(source)` 的目标元素类型为 any。
- 转换失败不暴露半成品；不重新贴标签改变原容器类型。元素是引用时，新容器与原容器共享这些引用，不做深复制。
- 类型化集合保存自身元素约束，赋给 any 不会移除约束。
- 泛型集合不变：已有 `list<int>` 不是 `list<float>`，已有 `list<any>` 也不能直接赋给 `list<int>`；需要显式集合构造转换。转换到 float 元素时允许逐项 int → float。
泛型调用可用于 `list`、`array` 构造器，以及带一个 trait 约束的普通函数。

```oxygen
let source = [1, 2]
let checked = list<int>(source)
let alias: any = checked
alias.add("bad")               // 类型错误；checked 仍为 [1, 2]
```

### 读取与修改

list 和 array 都使用零基索引；索引必须是 int。负数与越界报错，不自动扩容。

| 成员或操作 | list | array |
| --- | --- | --- |
| `[index]` | 读取、写入元素 | 读取、写入元素 |
| `.count` / `.size` / `.length` | 元素数量，只读 | 元素数量，只读 |
| `.first` / `.last` | 首/尾元素；空集合返回 null | 首/尾元素；空集合返回 null |
| `add(value: T): void` | 追加一个符合元素约束的值 | 不提供 |
| `remove_at(index: int): void` | 删除指定元素；越界报错 | 不提供 |

元素写入允许 `=` 和既有复合赋值，写入前检查容器约束；失败保留原元素。`len` 对 string、list、array、object 返回长度或键数，空容器返回 0。

list、array、object 均为引用语义：赋值与传参不复制，`==` / `!=` 比较引用身份，不隐含深度相等。

### object 与 struct 的区别

object 保留 `{ key: expr, "key": expr }` 字面量、点访问与字符串键索引。字段无类型标注，重复字面量键直接报错，不覆盖；已有实例可新增或更新动态字段，读取不存在的键时报错。

```oxygen
let config: object = {level: 1, "label": "Oxygen"}
config.level = 2
config["enabled"] = true
```

object 不自动转换为名义 struct。例如，在声明具有 health、tags 字段的 Stats 后，`let s: Stats = {health: 100, tags: []}` 仍是类型错误，必须使用 `Stats(...)` 构造。

## struct 与组合

### 声明与构造

新增 `struct` 声明，限定在文件顶层。字段形状固定，写法为 `name [: Type] [= defaultExpr]`，成员由换行或分号分隔；省略标注为 any，省略默认值表示构造时必填。

构造复用命名调用 `Name(field: value)`，全部参数必须具名，不按字段顺序提供位置构造。缺失必填字段、未知或重复参数、类型不匹配均报错。不增加 new、class、extends 或 `Name { ... }` 构造形式。

```oxygen
struct Stats {
    health: int = 100
    tags: list<string> = []

    fn alive(): bool {
        return self.health > 0
    }
}

struct NamedActor {
    name: string
    stats: Stats = Stats()           // 普通命名字段组合
}

struct Actor {
    name: string
    embed stats: Stats = Stats()     // 显式嵌入并提升成员
}

let nested = NamedActor(name: "A")
let actor = Actor(name: "B")
nested.stats.health = 80
actor.health = 80                    // 等价于 actor.stats.health = 80
let living: bool = actor.alive()     // 接收者是 actor.stats，不是 actor
let same = actor
same.health = 70                     // actor.health 也为 70；不复制
```

文件顶层 fn 与 struct 名称先预声明，再解析字段、参数、返回类型和方法体，允许引用稍后声明的函数或类型。顶层 let/const 初始化按源码顺序；读取尚未初始化的绑定报错。

struct 只能写入已声明字段，不能动态增添字段。字段写入允许 `=` 和已有复合赋值；类型检查失败保留原值。实例存入 any 绑定也不会失去字段或方法的约束。

### 默认值与引用语义

字段默认表达式在每次构造时按字段声明顺序分别求值，可以使用模块名称，但不能引用 self 或其他实例字段。

- `[]`、`{...}`、`Stats()` 等每次求值产生新容器或实例，不共享默认实例。
- 默认表达式直接读取已有引用时按引用语义共享，不隐含深复制；构造时显式传入已有引用同样共享。
- 显式实参先按源码顺序求值，再按字段声明顺序初始化。失败不返回部分初始化实例；已经发生的实参副作用不回滚。

struct 采用引用语义，赋值与传参不复制。它是名义类型：即使字段相同，两个不同 struct 也不能互赋。struct、list、array、object 的 `==` / `!=` 比较引用身份；string、数值和 bool 按值比较，不提供隐式深度相等。

### 方法、`impl` 与 `self`

方法写作 `fn name(params) [: ReturnType] { ... }`，隐式只读绑定 `self` 指向接收实例，不出现在实参列表。允许修改 `self` 的可写字段，不允许重绑 `self`；方法形参不得命名 `self`。

较大的类型可以把方法移入 `impl Type { ... }`；`impl` 只能增加方法，不能增加字段或改变布局。多个 `impl Type` 会合并检查，成员名不能重复。`static fn` 没有接收者，只能通过类型取得，例如 `Counter.starting(10)`；静态方法不通过实例访问，也不被 `embed` 提升。

`Self` 在 struct 或对应 impl 内表示当前具体 struct。字段、内联方法和 impl 方法均公开；不提供成员可见性、继承或构造函数体。

```oxygen
struct Counter { value: int = 0 }
impl Counter {
    fn add(amount: int): void { self.value += amount }
    static fn starting(value: int): Self { return Self(value: value) }
}
let counter: Counter = Counter.starting(10)
counter.add(2)
```

保存实例方法仍保留接收者及参数类型；保存静态方法是无接收者函数。不复制实例。

```oxygen
let state = Stats()
let check = state.alive
state.health = 0
let living = check()                 // false；接收者仍是 state
```

### 普通组合与显式嵌入

普通字段 `stats: Stats` 只通过 `actor.stats.health` 访问，不提升成员。显式 `embed stats: Stats` 仍是可构造、可访问、可替换的普通字段，额外递归提升嵌入类型的字段和方法。

- embed 必须标注明确的非空 struct 类型，含导入的 `alias.Type`；不接受 any、object、list 或 `T?`。
- 提升是访问转发，不复制、不继承，也不把 Actor 当作 Stats。
- 构造只能传 `stats: Stats(health: 80)`，不能传提升后的 `health: 80`。
- 提升方法调用绑定当前嵌入字段值；此前单独保存的绑定方法仍绑定保存时实例。
- 同一 struct 的直接字段、方法、嵌入字段名和全部提升名称不能冲突，成员名不能为 self。
- 直接成员与提升成员同名、多条嵌入路径提供同名成员，都报声明错误；没有“本地覆盖嵌入”优先级。可改为普通命名组合或调整名称解决。
- 嵌入类型图出现环报声明错误；普通 `next: Node? = null` 可以表达环状引用。

### 宿主写入边界

语言容器的字段和元素可写，不等于宿主对象可写。`pawn.food = 1` 仍非法；宿主能力由白名单接口决定，struct 不提供绕过白名单的包装或继承机制。

## 模块与库

### 导入语法与身份

```oxygen
use helpers@author.modid
use gameplay.health@"Author.Mod-With-Dash" as health
use math@std
use logic@std
use cs@std
optional use irismenus@std as menus
```

`@` 左边是模块路径，由一个或多个合法标识符用点分隔；右边是 Mod 的 packageId，不是程序集名、类名、Steam Workshop ID 或目录。packageId 含连字符等字符时使用字符串。std 是宿主扩展提供方，不从普通 Mod 的 Oxygen 文件解析；math、logic、cs 保留，其他名称由宿主显式提供。

默认别名取模块路径最后一段，可用 `as name` 改名。模块路径和导出符号大小写敏感；提供方身份由宿主 packageId 解析规则匹配，不在语法层增加大小写归一算法。

模块身份由 `(提供方 packageId 或 std, 模块路径)` 确定；不根据搜索顺序从其他 Mod 猜同名模块，类型身份也不随导入别名改变。

### 导入位置、冲突与错误

- use 只在文件顶层出现，位于所有非导入声明或语句之前。
- 导入绑定只读，别名不能与内置名字、类型、已有声明或其他导入冲突，不隐式覆盖。
- 同一模块可显式导入为两个不同别名，共享当前脚本实例中的模块实例；相同别名重复声明报错。
- 提供方未安装、未启用，模块未导出，访问未导出符号，或出现循环导入，均报载入错误。诊断包含请求的模块、提供方和源码位置；循环错误还包含依赖链。
- 普通 use 不自动下载、不回退到其他模块。不提供后置 use optional、group use、星号导入、选择性导入或无 @ 的旧 Arti 导入。
- `optional use path@provider as alias` 与普通 use 有相同的顶层位置和只读别名规则。真正缺失时只提供 `alias.available == false`；可用模块统一提供 `available == true`。available 为加载器保留成员，不由模块自行决定。
- optional 只跳过模块不存在，不吞掉读取异常、语法／分析错误、初始化异常或导入循环。先 optional 导入缺失模块、再换别名普通导入同一模块，仍报错。
- 类型标注在执行分支前检查：即使 `if !alias.available` 已跳过玩法，未调用函数或不可达分支中出现未知 `alias.Type` 也拒绝加载。optional 不是禁用类型检查。

### 模块导出与初始化

脚本库模块也是 Oxygen 文件。安全宿主和内置扩展模块可由 C# 提供相同命名空间表面，不要求由 Oxygen 源码实现。

脚本声明默认私有，使用 `pub fn`、`pub struct`、`pub const` 或 `pub trait` 显式导出；不导出可变 `let`。`pub impl` 不允许，`impl Trait for Type` 的归属始终由 Type 定义模块负责。导出的 trait 和 struct 身份包含 `packageId + modulePath + 声明名`，不随导入别名变化。

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

假设该模块导出为 helpers，调用写 `helpers.clamp_bonus(...)`，类型与构造写 `helpers.Stats` / `helpers.Stats(...)`。名义 struct 身份包含模块身份和声明名，不取决于本地别名。

脚本库模块顶层只允许 use、fn、struct、trait、impl、let、const，其中只有 fn/struct/trait/const 可以加 pub。trait 只包含显式签名；`impl Trait for Type` 必须完整实现签名，参数名称也参与检查，因为 Oxygen 支持命名实参。仅有同名方法不算实现，必须显式写 `impl`。

依赖验证成功后，先初始化依赖，再初始化导入者；每个模块在当前脚本实例中只初始化一次，不建立跨脚本实例的全局单例。初始化错误使本次加载失败，不替换上一份有效脚本，保持现有热更新的失败边界。

游戏内按活动 Mod 的 packageId 解析提供方，再从该 Mod 根目录的 `Oxygen/` 读取 UTF-8 源码：`helpers@author.modid` 对应 `Oxygen/helpers.oxy`，`gameplay.health@author.modid` 对应 `Oxygen/gameplay/health.oxy`。不需要额外 XML 注册清单；模块通过 `pub` 控制导出。只查对应活动 Mod，不遍历其他提供方或脚本当前目录。Mod 包名匹配不区分大小写；源码符号区分大小写。模块路径应保持声明大小写一致，实际文件名查找仍受操作系统文件系统规则影响。

游戏外嵌入语言核心时，用 `OxygenModuleRegistry.RegisterSource(packageId, modulePath, source)` 注册源码，再将 registry 传给 `ScriptCompiler.Compile(source, modules)` 或 `ArtiExecutionContext(modules: modules)`；也可实现 `IOxygenModuleCatalog.TryGetSource`。registry 禁止重复身份及 `std` 提供方。当前没有任意 CLR/C# 插件注册接口。

每次应用和读档重新读取模块并编译；只持久化有效主脚本和既有心情宿主状态，不保存模块源码快照、模块可变全局值或任意闭包。模块文件改变会在下一次应用/读档生效；读档依赖缺失或编译失败时停用脚本并显示错误，不能假定旧模块仍可用。

### 默认标准能力与显式扩展

| 层 | 是否需要 use | 内容 |
| --- | --- | --- |
| 语言与默认标准能力 | 不需要 | 内置类型、集合构造、显式基本转换、四则/比较/布尔运算、range、len、exists、已有字符串函数、core.string/core.value/core.text/core.random |
| 内置扩展 | `use name@std` | math、logic、cs 的显式命名空间 |
| Mod 导出模块 | `use name@packageId` | 该 Mod 注册的脚本或安全宿主模块 |

pawn、game、mood、memory 保留免导入的宿主接口身份，不归入游戏外通用标准库。use 不更改 when 的单 Pawn 参数和 bool 结果契约。

基础运算不能被移入需要导入的库：`+ - * / %` 不需要 math，`! && ||` 不需要 logic。`use string@std` 不表示基础类型和字符串能力需要导入；未注册同名扩展时照普通缺失模块报错。

### 内置扩展 API

以下是当前内置扩展的完整导出表面；没有导入时名称不可用。

#### math

`math.clamp(value: float, min: float, max: float): float` 将值夹取到给定区间；min > max 报错。int 实参可拓宽为 float。

```oxygen
use math@std
let ratio: float = math.clamp(0.75, 0, 1)
```

#### logic

`logic.all(values: list<bool>): bool` 检查全部元素，空集合返回 true；`logic.any(values: list<bool>): bool` 检查是否存在 true，空集合返回 false。

```oxygen
use logic@std
let allowed: bool = logic.all([true, false])
```

#### cs

cs.Int32 是扩展导出的受控 CLR 整数包装类型，`cs.int32(value: int): cs.Int32` 显式包装；`int(value)` 可显式解包此类型。它与 DSL int 不隐式互换。

```oxygen
use cs@std
let native: cs.Int32 = cs.int32(42)
let number: int = int(native)
```

其他 C# 适配类型须由宿主显式导出。`use cs@std` 不授予任意 CLR 对象权限，不引入反射、按类名字符串实例化、内联 C#、文件/网络访问、线程或 Harmony 操作。

### 翻译查询 lang()

`lang("Key", name: value, ...)` 是无需导入的普通内置函数，返回 `string`。游戏宿主读取当前已加载语言的 `Languages/.../Keyed`，复用 RimWorld 的默认语言回退与命名参数格式化；找不到键时返回键名。键属于原生全局翻译库，查询其他 Mod 的键不会自动加载该 Mod。

```oxygen
fn grade_text(pawn): string {
    return lang("OPN_PrisonerGrade", name: pawn.original_name, grade: pawn.grade)
}
```

键必须是一个非空位置字符串参数；其余只接受命名实参，值限于字符串、有限数字、布尔或 `null`。不把 Pawn、struct、集合、函数或任意 CLR 对象交给原生翻译器。原生翻译中的 `{name}`／`{grade}` 对应实参名称，不是 Oxygen 的插值表达式。

函数、导入库与设置文本回调共享当前脚本的翻译宿主。需随语言变化重新取文本时，在回调内部调用 `lang()`；顶层 `let text = lang(...)` 只在载入时求值。返回文本不自动转义或校验，代号长度、控制字符和富文本限制仍由消费端执行；等级／页面／存档 ID 不使用翻译后的名称。

Core 不引用 Verse。游戏外执行需显式注入 `IOxygenLocalization`；仅解析或未执行的分支不要求翻译数据，实际调用而缺少宿主时报带位置的错误。现有离线 CLI 不自动读取玩家语言库，也不把返回键名当作成功翻译。含原生声明的文件在加载期拒绝实际调用 `lang`，Def 的 `label`／`description` 仍通过 `DefInjected` 翻译；该文件的设置／规则回调可在加载完成后调用 `lang`。

嵌入语言时实现 `IOxygenLocalization.Translate(string key, IDictionary<string, object> arguments)`，通过 `ArtiExecutionContext(localization: provider)` 或 `ScriptCompiler.Compile`／`ContentCompiler.Compile`／`OxygenSettingsCompiler.Compile` 的 `localization:` 参数注入。命名参数字典仅在此次调用期间读取，不保留或修改；各执行器保有自己的提供者，不共享全局语言状态。游戏消费端可使用 `Oxygendsl.Mod.OxygenLocalization.Instance`，原生回退还保留开发者模式的伪翻译行为。

### IrisMenus 设置页

IrisMenus 启用时，Mod 文件执行宿主提供 `irismenus@std`；未启用时为缺失模块。页面声明可写入消费 Mod 自己的 `Oxygen/` 下任意 `.oxy` 文件，例如 `config.oxy`，也可与原生内容／运行规则共存；文件名不决定宿主能力。页面需要 `OxygenSettingsHost.Register(owner, values)` 接入消费端设置存储。动态心情窗口仍是独立宿主。最小示例：

```oxygen
optional use irismenus@std

fn page_title(): string { return lang("MyMod_Settings") }
fn enabled_label(): string { return lang("MyMod_Enabled") }

if irismenus.available {
    irismenus.registry(pages: [{
        id: "general",
        title: page_title,
        scope: "game",
        items: [
            {id: "enabled", kind: "checkbox", label: enabled_label,
                key: "enabled", default: true}
        ]
    }])
}
```

每次编译收集全部 registry 调用，验证后一次替换本 owner 的 `oxygen.settings` 注册组。页面／控件 ID 必须非空且分别唯一；不要用翻译文本作 ID。页面必填 id、title、items；scope 默认为 global，可设 game。相同页面 ID 保留 IrisMenus 导航与滚动；省略的页移除，空计划清除此注册组。编译或注册冲突失败保留原页。页面结构在重载时更新，标题和控件状态在使用时求值。

| kind | 额外必填成员 | 值与行为 |
| --- | --- | --- |
| label / section | 无 | 说明／分节，不写设置 |
| checkbox | key, default | bool 开关 |
| number | key, default | int 或 float 数值框；整数限制 Int32 |
| slider | key, default | float 滑条，默认范围 0..1 |
| text | key, default | string 输入 |
| select | key, default, options | string 值；options 为 `[{value: "id", label: "标题"}]` 或返回该列表的函数 |
| button | action | 零参函数，不持有设置值 |

所有控件必填 id、kind、label。label／description 可为 string 或返回 string 的零参函数；visible／enabled 可为 bool 或返回 bool 的零参函数，默认为 true。数值控件可设 min／max，default 必须在有限范围内。可写控件可设 changed(value) 回调，Set 完成后读取实际值再调用；回调失败不撤销已经完成的 Set。动态 options 点击时重新检查，已经被重载替换的旧弹出菜单不能继续写入。

IrisMenus 用 Listing 页面按可用宽度排版并滚动；字段标签和描述允许换行。game 页未进入存档时仍显示说明，交互禁用。global 页离开／关闭时委托 owner.WriteSettings，game 页由消费 Mod 正常存档保存。default 是缺失值读取参数，不会自动创建或覆盖旧设置。

消费端 C# 接口（命名空间 Oxygendsl.Mod／Oxygendsl.Core）：

```csharp
// 在 Mod 构造函数中注册；加载延后，兼容可选适配器较晚初始化。
OxygenSettingsHost.Register(this, values); // values: IOxygenSettingsValues
bool applied = OxygenSettingsHost.Reload(this);

// 消费 Mod 实现；必须保持现有设置键与保存范围。
object IOxygenSettingsValues.Get(string key, string scope, object defaultValue);
void IOxygenSettingsValues.Set(string key, string scope, object value);
bool IOxygenSettingsValues.GameAvailable { get; }
```

上面的接口行是签名说明，不是可直接复制的方法实现。宿主不反射消费端字段、不自动生成另一份设置存储。IrisMenus 是可选条件程序集；未安装时脚本应通过 optional 分支跳过声明，消费 Mod 保留原版设置绘制。设置值直接使用消费端已有的持久状态，不新增平行存储。

### 通用原生模块 API

其他 Mod 不需要进入 std。用 `IOxygenModuleProvider.TryGetModule(packageId, modulePath, out IOxygenHostValue module)` 提供只读命名空间；`IOxygenHostCallable.Invoke(positional, named)` 提供显式能力。静态命名空间可用 DictionaryOxygenHostValue。设置宿主共享注册表：

```csharp
OxygenSettingsHost.Modules.Register("author.modid", "tools", module);
// 对应 Oxygen: use tools@author.modid
```

独立语言宿主可创建 OxygenHostModuleRegistry，并传入 `new ArtiExecutionContext(hostModules: registry, standardModules: registry)`。第三方原生模块优先于同身份脚本源码；提供方返回 false／null 才回到对应 Mod 的源码目录。包名匹配不区分大小写，模块路径区分大小写；重复注册拒绝。命名空间成员默认只读，能力仅限显式导出的函数，没有自动 CLR 反射。

`ArtiExecutor.InvokePrepared(callable, params object[] arguments)` 复用准备好的脚本函数／绑定方法／原生回调，每次重置步骤与深度预算，失败抛 ScriptEvaluationException。所有注册、绘制和回调都在游戏线程执行；原生回调是可信 C#，其耗时和副作用不由解释器预算约束。模块库应只初始化自身状态；设置编译失败只保证页面注册不替换，不回滚脚本主动调用的原生副作用。此接口不意味着其它内容宿主已自动接入共享注册表。

## 持久事件链与 events.ctx@event

事件链使用独立的 `EventChainScript` 宿主，复用 Oxygen 的解析、分析与有界函数调用。`event` 是此宿主提供的模块身份，不是语法关键字、Mod packageId 或可从普通 `Oxygen/` 自动加载的目录。动态心情、原生内容与设置宿主不会因此获得事件动作权限。

```oxygen
use events.ctx@event as ctx

fn evaluate(chain) {
    if chain.stage == 0 {
        return ctx.action("roll_predator", next_tick: -1, stage: 2)
    }
    return ctx.wait(chain.tick + 2500)
}
```

这段代码须由已注册的 C# provider 调用。`chain` 的事实由 provider 明确提供；`roll_predator` 也必须存在于该 provider 的动作白名单，不能借动作字符串调用任意 C#。

| 返回值 | 含义 |
| --- | --- |
| `ctx.wait(next_tick)` | 等待指定模拟 tick；`-1` 表示无下一次规则检查，已有 deadline 仍有效 |
| `ctx.action(action_id, next_tick: ..., stage: ...)` | 请求宿主的具名动作；可提交后续阶段与检查时间 |
| `ctx.finish("completed")` | 结束实例；另支持 `failed`、`expired`、`cancelled` |

未来检查 tick 必须大于当前 tick，不能递归立即重试。期限到达时只处理到期实例；终态不再进入规则回调。脚本对象、闭包与临时缓存不写入存档。

C# API 在 `Oxygendsl.Core.Events`，游戏宿主在 `Oxygendsl.Mod.Events`：

- `IEventChainProvider` 声明稳定 provider ID、API／状态版本和定义集合；`IEventChainDefinition` 声明独立定义版本、一次准备、决议和受限动作执行。
- `OxygenEventChainHost.Register(provider)` 注册宿主；`Preparing` 在读档引用就绪、运行时激活前提供旧格式迁移接点，不依赖两个 GameComponent 的载入顺序。
- `EventChainRuntime.Import(...)` 只导入尚不存在的 provider 分区，原子验证；显式空分区也是权威状态，不能从残留旧记录重新生成。
- `Start`、`SetStage`、`End` 使用 provider ID 和实例 ID 定位。实例 ID 在所属 provider 内稳定，地图／商队目标、阶段、原始期限与 payload 随实例保存。
- `SetMode` 区分 Active、Paused、Disabled。暂停与停用均保留实例和已存在世界对象；恢复时按暂停时长平移下一次检查和期限，不重放已确认动作。

框架存档 schema、provider 状态版本、链定义版本和 API 版本分别校验。缺失 owner 或不兼容版本保留原始数据并停摆，运行检查显示原因；没有旧引擎回退。动作执行先登记待确认回执，再同步提交结果；异常或读档发现待确认效果时暂停，不能假定不可逆效果未发生并自动重试。

调度器只访问到期队列和活动索引，不按每 tick 枚举历史终态。provider 注册、迁移、规则求值和动作提交在游戏主线程执行；动作不得递归推进运行时或修改正在执行的同一实例。

`OxygenEventChainHost.RemoveProviderState(document, providerId)` 只删除序列化存档副本中的指定事件链分区，保留 ODF 根和其它 provider。它不清理 Pawn、Def、NarrativeState 或其它世界产物；消费 Mod 仍须完成自己的卸载导出。移除 ODF 前，必须处理仍依赖它的消费 Mod，不能靠停用动态心情保证安全卸载。

## 完整示例

以下辅助模块放在 Oxygendsl 的 `Oxygen/helpers.oxy`；主脚本可从 `Examples/typed-mood.oxy` 读取。两者随发布包部署，math、logic、cs 由运行时内置提供。

### 辅助模块 helpers@nanaloveyuki.oxygendsl

```oxygen
pub struct Stats {
    bonus: int = 5
}

pub fn clamp_bonus(value: int): int {
    if value < 0 { return 0 }
    if value > 10 { return 10 }
    return value
}
```

### 主脚本

```oxygen
use helpers@nanaloveyuki.oxygendsl
use math@std
use logic@std
use cs@std

struct RuleConfig {
    id: string
    embed stats: helpers.Stats = helpers.Stats()
    labels: list<string> = []
    thresholds: array<float> = [0.25, 0.5]
}

let cfg = RuleConfig(id: "fed_comfort", labels: ["吃饱了"])
let amount: int = helpers.clamp_bonus(cfg.bonus)
let limit: float = math.clamp(cfg.thresholds[0], 0, 1)
let native: cs.Int32 = cs.int32(amount)

fn comfortable(pawn): bool {
    if pawn.food == null { return false }
    return logic.all([pawn.food > limit, !pawn.downed])
}

mood(cfg.id,
    label: cfg.labels[0],
    description: f"心情增加 {int(native)} 点。",
    offset: amount,
    target: "colonists",
    when: comfortable)
```

示例遵守 when 的单 Pawn 参数和 bool 结果契约，没有将其改为一般回调注册协议。

cfg.bonus、amount 为 5，limit 为 0.25。独立消费端 smoke 已运行该文件：food=0.5 且未倒地时返回 true，food=0.25、需求缺失或倒地时返回 false；同一准备实例连续调用 250 次，重新编译 bonus=8 得到 offset=8。

## 增量语法骨架

以下 EBNF 只覆盖新增点；未列出的表达式、语句沿用前文保留的 Arti 写法。sep 表示换行或分号，qname 表示标识符点路径，string_literal 表示字符串字面量。`[]` 表示可选，`{}` 表示重复，字面标点以引号包围；参数、实参与集合内部允许换行和尾逗号。

```ebnf
import       = ["optional"], "use", qname, "@", (qname | string_literal), ["as", identifier] ;
typed_name   = identifier, [":", type] ;
type         = type_atom, ["?"] ;
type_atom    = qname | ("list" | "array"), "<", type, ">" ;
variable     = ("let" | "const"), typed_name, "=", expression ;
type_params  = "<", identifier, ":", qname, {",", identifier, ":", qname}, ">" ;
function     = ["pub"], "fn", identifier, [type_params], "(", [parameters], ")", [":", type], block ;
static_fn    = "static", function ;
trait_decl   = ["pub"], "trait", identifier, "{", {signature}, "}" ;
impl_decl    = "impl", [qname, "for"], qname, "{", {function | static_fn}, "}" ;
signature    = ["static"], "fn", identifier, "(", [parameters], ")", ":", type ;
parameters   = typed_name, {",", typed_name}, [","] ;
struct_decl  = ["pub"], "struct", identifier, "{", {sep}, [member, {sep, {sep}, member}, {sep}], "}" ;
member       = field | embedded | function ;
field        = typed_name, ["=", expression] ;
embedded     = "embed", identifier, ":", qname, ["=", expression] ;
export_decl  = "pub", (function | struct_decl | trait_decl | const_decl) ;
const_decl   = "const", typed_name, "=", expression ;
qname        = identifier, {".", identifier} ;
```

- 类型位置的 qname 必须解析为合法类型，void 只能用于返回位置。
- embedded 的 qname 必须解析为非空 struct，pub 只在脚本库模块顶层使用。
- 调用沿用 Arti 实参规则，struct 构造额外限定为全命名实参。
- 类型泛型在类型上下文、内置 list/array 构造器和受 trait 约束的函数调用中解析；普通比较表达式不会猜测泛型。泛型函数只支持一个 trait 约束入口，不支持泛型 struct、trait、impl 或 trait 对象。
- 新增保留关键字为 struct、embed、pub、trait、impl、static；@ 是新增标点，单独的 ? 仅用于类型可空后缀，表达式中的 ?? 保留。

## 正反案例与规格审查

每行是独立场景；Stats、Actor 指 struct 与组合章节中的声明，helpers 指已导入的辅助模块。行为检查覆盖类型约束、集合状态模型、引用/嵌入、导出隔离、错误诊断与宿主只读边界；实际验证范围见 [validation.md](validation.md)。

| 输入或场景 | 预期结果 |
| --- | --- |
| `let x = 1; x = "one"` | 合法；x 变为 string |
| `fn a(b: int) {}; a(1)` | 合法；返回未标注，隐式返回 null |
| `let x: int = "1"` / 调用前述 a 时写 `a("1")` | 类型错误；使用 `int("1")` 后合法 |
| `let x: float = 1` / `let y: int = 1.5` | 前者合法拓宽；后者类型错误 |
| `"2" + 1` / `"2" - 1` | 前者为 "21"；后者报数值操作错误 |
| `let flag: bool = 1` / `if 1 { ... }` | 前者类型错误；后者按条件真值进入分支 |
| `fn f(): int {}; f()` | 隐式 null 不符合 int，可提前诊断，最晚在返回时失败；改为 int? 则可返回 null |
| `let a: array<int> = []; a[0] = 1` | 空 array 创建成功；写入越界错误，仍为空 |
| `let a: array<int> = [1]; a.add(2)` | array 无 add，拒绝；改为 `list<int>` 可追加 |
| `let xs: list<int> = [1]; let x: any = xs; x.add("bad")` | 类型错误，xs 仍为 [1]，any 不移除容器约束 |
| `let xs = [1]; let ys: list<int> = xs` | 拒绝已有 `list<any>`；`list<int>(xs)` 显式构造合法 |
| 两次 `Stats()`，修改其中一个实例的 tags | 默认列表各自独立，不互相污染 |
| `let a = Stats(); let b = a; b.health = 70` | a.health 也为 70，引用语义 |
| `Actor(name: "A").alive()` | 转发到当前 stats 实例的 alive |
| 同一 Actor 声明本地 health，又 embed Stats | 声明冲突，不允许本地覆盖 |
| `Actor(name: "A", health: 80)` | 未知构造参数；应传 `stats: Stats(health: 80)` |
| `let s: Stats = {health: 100, tags: []}` | 类型错误，不自动从动态 object 转为名义 struct |
| `use helpers@example.balance as h` 后 `h.Stats()` | 合法；类型身份不随别名改变 |
| helpers 未导出 / Mod 未启用 / 模块循环导入 | 载入报错，保留上一份有效脚本，不搜索替代模块 |
| 未导入 math 时调用 math.clamp | 名称不可用；四则、比较和 && 仍免导入 |
| `use cs@std` 后尝试任意反射或 Harmony | 不授予权限，只能访问显式安全导出 |
| 完整主脚本，pawn.food=0.5、pawn.downed=false | cfg.bonus=5、limit=0.25、comfortable 返回 true、注册 offset=5；独立消费端 smoke 已运行 |
| `logic.all([])` / `logic.any([])` | 参数上下文创建 `list<bool>`，分别为 true / false |
| `fn f(values: array<int>): int { return values[0] }; f([3])` | 直接实参字面量创建 `array<int>`，返回 3；先 `let xs = [3]` 再 `f(xs)` 则拒绝 `list<any>` |
| `next: Node? = null` 普通字段 / `embed next: Node` 形成类型环 | 前者合法，后者报嵌入类型环错误 |

### 与旧语言行为的差异

use、struct、后置类型约束及固定长度 array 已实现。字符串不再隐式参与数值运算；整数溢出、集合/字段不匹配及重复对象键报错。“保留 Arti 可读性”不代表所有旧宽松行为原样兼容。

单 Pawn 条件参数、严格 bool 条件结果、缺失需求为 null 和宿主只读边界保持不变。无标注参数可以接收普通 DSL 对象或 Pawn；写入权限由运行时对象能力判断，即便经别名传递也不能改宿主字段。

## 原生内容与运行规则

### Mod 入口与生效方式

自动递归发现所有活动 Mod 加载目录中的 `Oxygen/**/*.oxy`，不要求声明 ODF 依赖，不保留特殊入口文件名。`template`／`define`／`patch` 所在文件在传统 XML Patch 之后、原生 Def 解析／引用解析／缓存构建之前执行，原生注册先提交；同文件运行规则与设置页延后生效。其余文件在 Def 加载完成后执行。每个文件有独立作用域，可通过 `use` 导入纯函数／类型库。相同相对路径按 `foldersToLoadDescendingOrder` 覆盖，其余文件按稳定路径顺序执行，提供者按活动 Mod 顺序应用；`use` 不注册被导入文件的玩法声明。

内容提交前验证候选 XML，失败不提交该提供者的半份定义。启动时没有旧内容可恢复，错误会报告并拒绝该内容入口。规则重载逐提供者替换，失败保留该提供者上一有效快照；不是全部 Mod 的跨提供者事务。重复 ThoughtDef 绑定或同一闸门绑定报错，停用的提供者不占用活动绑定。

含原生声明的文件及其导入依赖在启动时固定，任何修改、移除或新增原生声明都要求重启，包括同文件设置与规则修改；需热重载的设置／规则可放入不含原生声明的任意 `.oxy` 文件。重载统一重新读取该 Mod 的全部 Oxygen 文件，失败保留上一有效规则与设置。规则停用状态与原生等级／阶段布局签名随存档保存；不兼容布局停用规则，不自动重解释已有等级和阶段。普通脚本变量仍不持久化。

### 内容声明

| 声明 | 实参与语义 |
| --- | --- |
| `template("name", type: "TraitDef", fields: {...})` | 作者模板，不注册抽象 Def；可用 `extends` 和 `mixins` |
| `define("TraitDef", "StableDefName", fields: {...})` | 注册真实原生 Def；本批内置 TraitDef／ThoughtDef |
| `def_ref("TraitDef", "Industriousness", package: "ludeon.rimworld")` | 带类型／可选来源限制的原生 Def 引用，不是任意字符串 |
| `native_type("RimWorld.ThoughtWorker_AlwaysActive")` | 解析实际 CLR 类型并校验字段要求的基类，不开放调用任意方法 |
| `patch("TraitDef", "Industriousness", set: {...}, append: {...}, remove: {...}, requires: ["mod.id"])` | 明确单目标；目标零项／多项报错，缺少 requires 中任一活动包则跳过 |

模板组合先按 `mixins` 顺序合并，然后合并 `extends` 模板及本项字段；对象递归合并、列表替换，每项展开复制容器。`extends: "name"` 是作者模板，`extends: def_ref(...)` 是原生 Def 的 `defName`，框架展开该 Def 的有效内容；不会把它写成 XML `ParentName`。本项同名原生字段覆盖基项、列表替换。跨提供者的原生基项必须先加载，同一计划可引用其其它定义；循环继承拒绝。

原生字段沿用真实名称：特质 `degreeDatas`／`statOffsets`／`statFactors`／`conflictingTraits`，思想 `stages`／`requiredTraits`／`durationDays`／`stackLimit` 等。嵌套列表可写 object 或 struct 字段，提交时发布为独立内容值。`StatModifier` 示例：`{ stat: def_ref("StatDef", "WorkSpeedGlobal"), value: 1.15 }`；`SkillGain` 用 `{ skill: def_ref("SkillDef", "Plants"), amount: 2 }`。未知字段、错误原生类型／引用类型、非整数 int 字段、空等级／阶段和重复等级拒绝。

`set` 既可设置整项字段，也可用点分路径修改已有项：`{ "degreeDatas.0.label": "worker" }`。索引从 0 开始，越界报错。`append`／`remove` 的值是列表，路径必须指向原生列表；追加按原生引用键／标量值去重，移除一项都未匹配时报错。身份字段不能被 patch 改名。定点修改原生 XML 继承字段时先展开该字段，避免父列表重新合并使删除失效。

完整可安装示例：[NativeTraits](../Examples/NativeTraits)。About／LoadFolders 包装仍使用 XML，但不需要另外的 Def／Patch XML。

### 思想与闸门规则

```oxygen
fn young_state(pawn) {
    if pawn.young_sick { return 1 }
    if pawn.young_hungry { return 0 }
    return -1
}
thought_rule("young_state", thought: def_ref("ThoughtDef", "MyMod_YoungThought"), evaluate: young_state)

fn begging(pawn): bool? {
    if !pawn.origin || !pawn.active_visitor || pawn.colonist { return null }
    if pawn.lifecycle == "Leaving" { return false }
    return pawn.hunger == "Hungry"
}
gate_rule("begging", gate: "Beg", evaluate: begging)
```

`evaluate` 为单 Pawn 参数函数。思想返回 `-1` 未激活或 `[0, stages.Count)` 阶段；无显式 worker／memory class 的新情境思想使用框架原生 Worker。思想资格仍由原版检查所需／抵消特质、等级、基因、Hediff 和发育阶段；固定特质数值走原生属性合成，不另作逐 Pawn 周期加成。

闸门返回严格 `bool?`，`true`／`false`／`null` 分别为允许／拒绝／不参与。支持 RHAH 现有 `Beg`、`Steal`、`Fight`、`LeaveAfterFed`、`EatOutsideRelief`、`FeedFromRelief`、`Gnaw`、`TailBite`、`Leash`、`Carry`、`JoinColony`、`Hire`、`Transfer`、`Imprison`、`DropOffChild`、`ExitMap`。稳定适配器一次注册，角色自身覆盖优先，未参与沿用 RHAH 默认；不写永久 SetGate，不启动 Job。

Pawn 新查询：`has_trait(defName, degree?)`、`trait_degree(defName)`、`trait_suppressed(defName)`、`faction_id`、`faction_def`、`prisoner`、`slave`、`stage`、`hunger`、`rest_category`、`sick`、`young_hungry`、`young_sick`、`group_sick`、`origin`、`source`、`role`、`lifecycle`、`active_visitor`、`carries_plague`、`has_been_fed`。需求类别来自原生枚举；群体为所在 Lord，否则自由殖民者集合，不变成半径扫描。无 RHAH 时来源为 false／空字符串。

### C# 宿主扩展

`OxygenDeclarations.Register(new OxygenDeclaration(name, OxygenDeclarationKind.Registration, reader))` 注册内容读取回调；`ContentRegistration` 提供求值后的命名／位置参数和源位置，回调返回 `ContentRegistrationResult` 的 Definitions／Patches／Thoughts／Gates，经同一身份重复检查进入计划。注册没有 reader 的名字不等于实现了声明。

`NativeContentSchema.Shared.Register(name, clrDefType)` 和 `RegisterField(ownerTypeName, mapping)` 扩展原生内容／字段映射；`NativeContentPipeline.Check` 与 `Apply` 共用候选构建、引用校验、字段操作与加载 XML。扩展仍需正确的原生宿主，不意味着任意 Def 类型和 Job 机制已经内置。


