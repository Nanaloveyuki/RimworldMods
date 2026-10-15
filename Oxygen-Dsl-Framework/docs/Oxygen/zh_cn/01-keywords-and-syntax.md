# 关键词与完整语法要点

本页先区分词法关键字、类型名和宿主 API，再说明日常写法。所有名称大小写敏感；`Self` 和 `self` 含义不同。

## 哪些是真正的关键字

以下名称由词法器直接识别，不能当普通变量名：

- 字面值：`true`、`false`、`null`。
- 绑定与函数：`let`、`const`、`fn`、`return`。
- 分支与循环：`if`、`else`、`for`、`in`、`while`、`break`、`continue`。
- 类型与方法：`struct`、`embed`、`trait`、`impl`、`static`。
- 模块：`use`、`optional`、`as`、`pub`。

`optional` 的用法是前置的 `optional use`；`static` 用于类型中的 `static fn`；`pub` 用于库模块顶层的 `fn`、`struct`、`trait`、`const`。这些词并不是可以任意排列的修饰符。

## 类型名、特殊名字和 API 不要混淆

- 内置类型名：`any`、`int`、`float`、`bool`、`string`、`list`、`array`、`object`、`void`。它们不是词法关键字，但声明和导入别名不能遮蔽它们。`string(value)` 是合法转换。
- `self`：实例方法里的隐式只读接收者绑定，可以写字段，不能重绑接收者。
- `Self`：`struct`／对应 `impl` 中的当前具体类型；trait 签名中代表实现类型。不是一个全局类型。
- 默认函数与命名空间：`range`、`len`、`exists`、`random`、`lang`、`core` 等，是内置能力，不是语法关键字。
- 游戏上下文：`pawn`、`game` 是宿主对象身份；只有相应宿主执行上下文提供值。
- 注册 API：`mood`、`memory`、`template`、`define`、`patch`、`thought_rule`、`gate_rule`。
- 内容值 API：`def_ref`、`native_type`。
- 导入模块 API：例如 `math.clamp`、`logic.all`、`cs.int32`、`irismenus.registry`，必须先导入且由对应宿主提供。
- `id`、`fields`、`extends`、`mixins`、`when`、`scope`、`available` 是参数或成员名，不是关键字。`new`、`class`、`extends` 也不是 Oxygen 的类声明语法。

宿主注册名称和内置名称有自己的保留／冲突检查。名字看起来像函数，不代表可以用它调用任意 C# 方法。

## 文件、标识符和语句分隔

Oxygen 源文件使用 UTF-8，通常以 `.oxy` 结尾。代码直接写在文件里，不加 Arti 的 `{{% ... %}}` 边界。

标识符以字母或 `_` 开头，后接字母、数字或 `_`；支持中文等 Unicode 字母。模块路径使用点分标识符。需要连字符的 packageId 可以写成字符串。建议给稳定 ID、模块和导出名使用容易在不同系统中保持一致的名称。

换行分隔语句，分号可选；花括号界定语句列表，缩进只影响阅读。在参数、调用、集合和分组括号内部可以换行，参数、实参和集合字面量允许尾逗号。不要依赖普通换行把未完成的二元表达式接起来；必要时用括号。

### 示例 01-A：注释和多行调用

纯语言文件，无游戏宿主要求。

```oxygen
// 单行注释到行末
fn add(left: int, right: int): int {
    /* 块注释可跨行，
       但不能嵌套。 */
    return left + right
}

let result: int = add(
    1,
    right: 2,
)
let next: int = result + 1; let last: int = next + 1
```

同一行的块注释不分隔表达式；包含换行的块注释仍提供语句分隔。字符串里的 `//`、`/*` 是普通文本。

## 变量、常量与赋值

`let name [: Type] = expression` 必须初始化，可重新赋值。无类型标注等价于动态的 `any`，不会根据初始值固定类型。已有类型约束不会因重新赋值而消失。

`const name [: Type] = expression` 创建只读绑定。初始化只允许静态表达式：字面量、const 引用、它们的纯运算、插值、集合和索引／成员组合。调用函数、读取 `let` 或宿主动态值不合法。实例构造和 `lang()` 都是调用，不能用于 const 初始化。

赋值是语句，不是表达式。支持 `=`、`+=`、`-=`、`*=`、`/=`、`%=`；必须命中已有可写绑定、语言字段或容器元素。不能用赋值创建未声明的变量，也不能据此改写宿主 Pawn。

### 示例 01-B：动态绑定与类型约束

纯语言文件。

```oxygen
const maximum: int = 10
let flexible = 1
flexible = "one"
let count: int = 1
count += 2
let ratio: float = count / maximum
```

`const` 只保护绑定，不表示容器深度不可变。const 保存的语言容器仍可能通过其可写成员修改；需要不可变数据时，不要向外暴露可变引用。

## 作用域不是块作用域

只有函数建立独立局部作用域。`if`、`for`、`while` 的花括号不隔离绑定；函数可以捕获外部变量。同一语句列表里重复声明报错。

### 示例 01-C：分支与闭包读取

纯语言文件。

```oxygen
let amount: int = 2
fn read_amount(): int {
    return amount
}
if true {
    amount = 5
}
let result: int = read_amount()
```

结果是 5。不要把不同分支当作独立的临时变量作用域，也不要假定每次循环都会自动建立一份新的绑定。分支中声明但从未执行初始化的名字不能安全读取。

顶层函数和类型先预声明，可以引用稍后声明的函数／类型；顶层 `let`、`const` 的初始化仍按源码顺序，读取尚未初始化的绑定会失败。

## 字符串与插值

单引号、双引号、三引号都可用。小写 `f` 前缀启用表达式插值；字面大括号写 `{{`、`}}`。

### 示例 01-D：三种字符串

纯语言文件。

```oxygen
let name: string = 'Oxygen'
let greeting: string = f"你好，{name}。"
let quoted: string = "一行\n另一行"
let block: string = """第一行
第二行"""
let braces: string = f"{{{name}}}"
```

三引号保留换行与缩进，物理 CRLF／CR 统一为 LF。转义有 `\n`、`\r`、`\t`、`\0`、`\\`、`\"`、`\'`、`\uXXXX`。普通引号跨行、相邻字符串拼接及反斜杠紧接物理换行的续行也被保留；为便于编辑与检查，长文本优先使用三引号。

插值表达式从左到右各执行一次，文本化规则与字符串拼接相同。不支持 Python 的格式后缀和调试插值。返回给消费端的字符串仍受消费端长度、富文本和控制字符限制。

## 表达式、优先级与短路

从高到低：

1. 成员 `value.member`、调用 `fn(...)`、索引 `value[index]`。
2. 单目 `!`、`+`、`-`。
3. `*`、`/`、`%`。
4. `+`、`-`。
5. `<`、`<=`、`>`、`>=`。
6. `==`、`!=`。
7. `??`。
8. `&&`。
9. `||`。
10. 管道 `|`。

二元运算左结合，用括号明确非默认顺序。`??` 只在左值为 null 时取右值；不会处理不存在的名字／成员。`&&`、`||` 短路且返回 bool；管道把左值作为右侧可调用对象的第一个实参，不是位运算。

### 示例 01-E：空值、条件与管道

纯语言文件。

```oxygen
let note: string? = null
let shown: string = note ?? "未填写"
let safe: bool = note != null && len(note) > 0
let total: int = 1 + 2 * 3
let grouped: int = (1 + 2) * 3
let cleaned: string = "  oxygen  " | trim | upper
```

`total` 为 7，`grouped` 为 9，`cleaned` 为 `OXYGEN`。不要用 `||` 模拟返回原值的“默认值”操作，它返回的是 bool。

条件使用真值：null、false、数值零、空白字符串和空集合为假，其他实例为真；函数直接作为条件报错，须先调用。这个宽松规则只用于条件／逻辑和显式 `bool(...)`，不会让 `let enabled: bool = 1` 合法。

## 分支与循环

- `if condition { ... } else { ... }`；可继续写 `else if`。
- `for value in values { ... }` 遍历集合；数值序列可用 `range`。
- `while condition { ... }` 重复检查条件。
- `break` 退出当前循环；`continue` 跳到当前循环下一轮。只能写在循环中。

### 示例 01-F：有限循环

纯语言文件。

```oxygen
let total: int = 0
for value in range(0, 5) {
    if value == 2 { continue }
    total += value
}
while total < 10 {
    total += 1
    if total == 10 { break }
}
let state: string = ""
if total == 10 {
    state = "完成"
} else if total > 10 {
    state = "超过"
} else {
    state = "不足"
}
```

循环和递归受执行预算限制。不要让 `while` 依赖当前调用中不会改变的游戏 tick 来等待游戏推进。

## 函数、参数与返回

函数使用 `fn name(parameters) [: ReturnType] { ... }`。参数标注是 `name: Type`，返回标注是冒号，不是 `->`。不支持重载、默认函数参数或匿名函数。

位置实参必须在命名实参之前。未知、重复、遗漏参数，或同一参数同时被位置和命名绑定，均报错。函数可保存到变量供稍后调用；传回调应写函数名，不要提前写 `callback()`。

### 示例 01-G：调用与多值解包

纯语言文件。

```oxygen
fn add(left: int, right: int): int {
    return left + right
}
fn pair(value: int) {
    return value, value + 1
}
fn stop(): void {
    return
}

let operation = add
let sum: int = operation(1, right: 2)
let first, second = pair(3)
first, second = second, first
let _, kept = pair(5)
stop()
```

多值返回和解包使用有序集合，没有独立 tuple 类型；`_` 丢弃对应位置。多目标解包不支持逐项类型标注，数量必须与右值对应。

未标注返回值可以返回任意值，裸 `return` 或执行到函数末尾返回 null。`void` 只允许裸 return／执行到末尾；非空返回类型必须在实际每次返回（包括隐式 null）满足约束。没有结果的函数应写 `void`，允许缺失的结果应写 `T?`。

## 顶层声明与宿主限制

语法支持控制流和调用，不等于每个宿主入口允许相同顶层语句：

- 通用语言执行器可执行普通顶层语句。
- 动态心情主脚本允许导入、类型／函数／变量声明和直接 `mood`／`memory` 注册；不要用顶层 `if` 包住注册或用顶层赋值更新变量。
- 库模块顶层只允许 `use`、`fn`、`struct`、`trait`、`impl`、`let`、`const`，用 `pub` 明确导出。
- 原生内容、规则与设置入口再按自己的编译器验证注册调用。

[下一篇：值、容器与标准函数](02-values-and-functions.md) 说明类型和内置函数；[进阶篇](05-advanced-and-cautions.md) 说明 struct、trait 与泛型的完整示例。
