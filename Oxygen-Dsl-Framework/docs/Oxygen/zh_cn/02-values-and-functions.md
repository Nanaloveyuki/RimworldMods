# 值、容器与标准函数

Oxygen 的默认绑定是动态的。类型标注是可选约束，不是自动推断；`any` 也不会绕过运行时的成员、调用和写入检查。本页示例均为独立纯语言文件，除 `lang()` 的说明外不需要游戏宿主。

## 内置类型

- `any`：任意值，包括 null；省略标注等价于 any。
- `int`：有符号 32 位整数。
- `float`：64 位浮点数。
- `bool`：true 或 false。
- `string`：Unicode 文本。
- `list<T>`：长度可变、元素受约束的集合；裸 `list` 为 `list<any>`。
- `array<T>`：长度固定、元素可写的集合；裸 `array` 为 `array<any>`。
- `object`：动态字符串键对象，不是任意 CLR object，也不是 struct 的基类。
- `T?`：T 或 null；除 any 外，普通类型默认不接受 null。
- `void`：只能标注函数／方法返回位置，不能保存为变量或容器元素。
- 用户 `struct`、导入的 `alias.Type`：名义类型，须用对应构造器创建。

类型可嵌套。`list<int?>` 是元素可空，`list<int>?` 是整个集合可空；`list<array<int>>` 是固定数组的列表。不支持联合类型、泛型 struct 或任意 CLR 类型名。

### 示例 02-A：逐项标注

```oxygen
let flexible = 1
flexible = "one"
let tick: int = 150
let ratio: float = 0.25
let enabled: bool = true
let label: string = "Oxygen"
let note: string? = null
let optional_elements: list<int?> = [1, null]
let optional_list: list<int>? = null
let nested: list<array<int>> = [[1, 2], [3, 4]]
```

初始化、赋值、参数、返回、字段和容器写入都检查约束。any 进入受约束位置时按实际值检查；静态可确定的不匹配可能提前诊断。

## 数字、转换与相等

只允许隐式 `int → float`。不隐式把 float 转 int、把数字文本转数字、把数字转 bool，或把 list 转 array。

- `int(value)`：int、范围内且无小数的有限 float、可解析的十进制整数文本。
- `float(value)`：数值或可解析的数值文本。
- `string(value)`：null 变为空串；bool 为 `true`／`false`；数字使用无区域依赖的格式。
- `bool(value)`：使用条件真值规则。

int／float 转换拒绝 bool 和 null，失败报错，不返回零兜底。整数溢出、除零和无效转换报错；`/` 返回 float，不提供整数除法运算符。整数运算不能静默回绕。

### 示例 02-B：显式转换

```oxygen
let count: int = int("12")
let whole: int = int(3.0)
let ratio: float = float("0.25")
let half: float = count / 2
let flag: bool = bool(1)
let text: string = "count=" + count
let absent_text: string = string(null)
let equal: bool = 1 == 1.0
```

只要 `+` 任一侧是 string，就将另一侧文本化并拼接；其他数值运算不接受数字文本。数值相等允许 int／float 同值相等，其他不同类型不自动互转。

string、数值、bool 按值比较。list、array、object、struct 的 `==`／`!=` 比较引用身份，不隐含深度相等；如需比较内容，应自己逐项比较。

## list 和 array 的构造

无类型上下文的 `[]`、`[1, 2]`、`[1, "two"]` 都创建 `list<any>`，不会根据元素推断为 `list<int>`。

直接放在受标注的初始化、赋值、参数、返回或字段位置的集合字面量，会按目标 `list<T>`／`array<T>` 创建并逐项检查。可空集合也能提供非空部分的类型上下文，括号分组不改变这一点。已有变量、函数结果或复合表达式不会这样隐式转换。

### 示例 02-C：目标上下文与显式复制

```oxygen
fn first(values: array<int>): int {
    return values[0]
}

let loose = [1, 2]
let names: list<string> = ["A", "B"]
let slots: array<int> = [10, 20]
let checked: list<int> = list<int>(loose)
let converted: array<int> = array<int>(checked)
let empty: list<string> = list<string>()
let from_literal: int = first([3])
let from_variable: int = first(array<int>(loose))
```

`list()`、`array()` 创建空的 any 集合；带类型参数的构造器创建受约束集合。传入一个 list 或 array 时，检查全部元素并建立新的浅层容器；裸构造器的目标元素类型为 any。

转换不改变原容器的类型，失败不暴露半成品。元素为引用时，两份容器仍共享这些元素。已有 `list<int>` 不是 `list<float>`；需要 `list<float>(source)` 逐项转换。把受约束集合赋给 any 不会取消其元素约束。

## 读取、追加和写入

list 与 array 都从 0 开始索引，索引必须是 int；负数或越界报错，不自动扩容。

两者共有：

- `[index]`：读写元素，支持赋值和复合赋值。
- `.count`、`.size`、`.length`：只读数量。
- `.first`、`.last`：首尾元素；空集合返回 null。

仅 list 提供：

- `add(value)`：追加一个符合约束的值，返回 void。
- `remove_at(index)`：移除一个元素，返回 void；越界报错。

array 不提供 add 或 remove_at。`array<int>()` 是长度为 0 的数组，不是等待写入自动扩展的容量。

### 示例 02-D：可变长度与固定长度

```oxygen
let values: list<int> = [10, 20]
values.add(30)
values[0] += 1
values.remove_at(1)
let slots: array<int> = [4, 5]
slots[1] = 9
let size: int = len(values)
let first_value: int = values[0]
let empty: list<int> = []
let missing: int? = empty.first
```

容器赋值与传参都不复制。若 `let alias = values`，修改 alias 也修改 values。类型不匹配的元素写入失败时保留原元素。

## 动态 object

`{key: expression, "key": expression}` 创建 object。键是字符串，字面量重复键报错；实例可新增或更新字段。点访问和字符串键索引均可用，不存在的键直接读取会报错。

### 示例 02-E：对象与存在性

```oxygen
let config: object = {level: 1, "label": "Oxygen"}
config.level = 2
config["enabled"] = true
let count: int = len(config)
let has_level: bool = exists(config.level)
let has_missing: bool = exists(config.missing)
let nullable: object = {value: null}
let present_null: bool = exists(nullable.value)
let label: string = config.label
```

`has_missing` 为 false，`present_null` 为 true。`exists(value)` 检查访问是否存在，不检查是否为 null，也不检查是否非空。它对名称／成员／索引访问作存在性探测；不要把任意失败调用包进 exists，其他计算或调用错误仍会传播。

object 没有固定字段约束，不会因为键恰好相同自动转换为 struct；要获得名义类型，使用那个 struct 的构造器。

## 序列与长度

`range` 只接受一到三个位置参数：

- `range(stop)`：从 0 到 stop，不含 stop。
- `range(start, stop)`：默认步长 1。
- `range(start, stop, step)`：按非零步长生成，支持负步长。

方向与边界不匹配时得到空集合；零步长报错。range 建立实际整数集合，不是无限惰性序列，生成数量也受执行预算限制。

`len(value)` 返回字符串的 UTF-16 长度、list／array 元素数、object 键数；null 返回 0。字符串索引也是 UTF-16 位置，不代表一个完整 Unicode 字形。

### 示例 02-F：序列

```oxygen
let indices = range(3)
let descending = range(3, 0, -1)
let sum: int = 0
for value in descending {
    sum += value
}
let characters: int = len("Oxygen")
let first_character: string = "Oxygen"[0]
```

## 字符串函数

以下函数免导入，可写成全局调用、`core.string.name(...)`，或以字符串作接收者的 `text.name(...)`。接收者形式省略第一个 `value` 参数。

- `len(value)`：长度。
- `remove_space(value)`／`remove_spaces(value)`：移除空白字符。
- `append(value, suffix)`、`prepend(value, prefix)`：尾部／头部拼接。
- `upper(value)`／`uppercase(value)`、`lower(value)`／`lowercase(value)`：区域无关大小写转换。
- `capitalize(value)`：首字符大写；`title(value)`：按内置英文式标题规则处理，不用于中文分词。
- `trim(value)`、`trim_start(value)`、`trim_end(value)`：清理两端／开头／结尾空白。
- `replace(value, old, replacement)`：替换文本。
- `contains(value, part)`、`starts_with(value, part)`、`ends_with(value, part)`：区分大小写的文本查询。
- `substring(value, start, length)`：从零基位置截取；length 可省略，省略时取至末尾；越界报错。
- `split(value, separator)`：返回 `list<string>`，保留空项；空 separator 按 UTF-16 字符拆分。
- `repeat(value, count)`：重复非负次数；负数报错。
- `is_empty(value)`：文本长度是否为 0；空白字符串不是空串。

### 示例 02-G：清理文本

```oxygen
let text: string = "  oxygen  ".trim().upper()
let tag: string = core.string.append(text, "!")
let contains_name: bool = contains(tag, "OXYGEN")
let part: string = substring(tag, 0, length: 3)
let pieces: list<string> = split("A,B,", ",")
let repeated: string = repeat("-", 3)
let blank: bool = is_empty("")
```

## 空值与文本组合

- `value ?? fallback`：只处理 null。
- `core.value.default(value, fallback)`：为空时选 fallback；空包括 null、空白字符串和空集合，不把数字 0 或 false 当成空。
- `core.value.coalesce(...)`：返回第一个非空值；也可传一个集合，没有候选时返回 null。
- `core.text.join_nonempty(separator, values)`：也可传多个位置候选；文本化、清理候选两端空白，跳过空白文本，再拼接。
- `core.text.trim_lines(text)`：清理各行两端空白、移除首尾空行，并把连续空行压成一个。
- `core.text.indent(text, level)`：每层为各行增加两个空格；level ≤ 0 保留原文本。

这些不是自动的错误恢复函数；参数先求值，不存在的成员、错误调用仍会失败。

### 示例 02-H：空文本回退

```oxygen
let note: string? = null
let a: string = note ?? "未填写"
let b: string = core.value.default("  ", "未填写")
let c: string = core.value.coalesce([null, "  ", "正文"])
let joined: string = core.text.join_nonempty(" / ", [" A ", "", "B"])
let trimmed: string = core.text.trim_lines("\n  A  \n  B  \n")
let indented: string = core.text.indent(trimmed, 1)
```

## 随机函数

`random(min, max)` 与 `core.random.int(min, max)` 返回含两端的整数随机值。`core.random.float(min, max)` 返回区间内的浮点随机值；`core.random.pick(...)` 从位置候选或一个集合中选一项，空候选返回 null。

随机值是动态运行结果，不能用作 const 初始化。条件函数可能被多次调用；不要用随机条件代替需要稳定保存的玩法决定。

### 示例 02-I：随机候选

```oxygen
let roll: int = random(1, 6)
let another: int = core.random.int(1, 6)
let ratio: float = core.random.float(0, 1)
let choice: string = core.random.pick(["A", "B"])
```

## 显式标准扩展

基础四则、比较、逻辑、转换、集合、上述函数不需要 use。下面三个扩展需要明确导入；所有 use 放在文件开头。

### 示例 02-J：完整的内置扩展表面

需要默认内置扩展提供方，不需要游戏对象。

```oxygen
use math@std
use logic@std
use cs@std

let ratio: float = math.clamp(0.75, 0, 1)
let all: bool = logic.all([true, false])
let any_match: bool = logic.any([false, true])
let native: cs.Int32 = cs.int32(42)
let number: int = int(native)
```

- `math.clamp(value: float, min: float, max: float): float`；min > max 报错，int 实参可拓宽。
- `logic.all(values: list<bool>): bool`；空集合为 true。
- `logic.any(values: list<bool>): bool`；空集合为 false。
- `cs.int32(value: int): cs.Int32`；显式包装，再用 int 显式解包。包装类型不与 DSL int 隐式互换。

`cs` 没有任意 CLR 反射、内联 C#、文件／网络访问或 Harmony 操作。`lang()` 也是默认函数，但实际执行需要翻译宿主，详见[模块、翻译与设置](04-modules-and-settings.md)。
