# 进阶类型、生命周期与常见陷阱

先用普通函数与命名组合表达问题，再考虑 embed、trait 和泛型。类型抽象帮助检查数据和能力契约，不增加游戏权限，也不使变量自动持久化。本页纯语言示例均是独立普通文件，检查时 `isModule: false`。

## struct：固定形状的引用对象

struct 只能在文件顶层声明。字段写成 `name [: Type] [= defaultExpression]`；省略类型为 any，省略默认值表示构造时必填。

构造调用必须全部具名：`Stats(health: 80)`，不能按位置或用 `new`、`Stats { ... }`。缺失必填字段、未知／重复字段、类型不匹配都会报错。所有字段和方法公开，不支持继承、成员可见性或构造函数体。

### 示例 05-A：字段、实例方法与默认值

纯语言文件。

```oxygen
struct Stats {
    health: int = 100
    tags: list<string> = []
    fn alive(): bool { return self.health > 0 }
}
struct NamedActor {
    name: string
    stats: Stats = Stats()
}

let first: NamedActor = NamedActor(name: "A")
let second: NamedActor = NamedActor(name: "B")
first.stats.health = 80
first.stats.tags.add("injured")
let separate: bool = len(second.stats.tags) == 0
let alias = first
alias.stats.health = 70
let health: int = first.stats.health
let living: bool = first.stats.alive()
```

health 为 70。struct 是引用语义，赋值和传参不复制；同名字段的两个不同 struct 仍是不同名义类型。`{health: 100}` 是 object，不是 Stats。

## 默认表达式与初始化顺序

字段默认值每次构造按字段声明顺序分别求值，可引用模块名字，不能引用 self 或其它实例字段。

- `[]`、`{...}`、`Stats()` 每次求值建立新容器／实例，不会使所有对象共用一个默认列表。
- 默认表达式直接读取已有引用，或显式实参传入已有引用，就会共享，不会隐式深复制。
- 显式实参先按源码顺序求值，再按字段顺序初始化。
- 构造失败不返回半成品，但已经发生的实参副作用不回滚。

### 示例 05-B：明确共享

纯语言文件。

```oxygen
let shared: list<string> = []
struct Bag {
    labels: list<string> = shared
}

let first: Bag = Bag()
let second: Bag = Bag()
first.labels.add("shared")
let count: int = len(second.labels)
```

count 为 1。const 也不是深度不可变；需要独立容器可显式用 list／array 构造器浅复制，引用元素依然共享。

## impl、static fn、self 和 Self

实例方法中的 self 是隐式只读接收者，不在实参列表；可修改可写字段，不可重绑，也不可把形参命名为 self。

`impl Type { ... }` 把方法与字段布局分开。多个 impl 合并检查，只能增加方法，不能增加字段或覆盖已有成员。`static fn` 没有接收者，只能通过类型取得，不可通过实例调用。

Self 表示当前具体 struct，可用于字段、参数、返回和构造。普通文件全局函数不能把 Self 当作全局类型。

### 示例 05-C：静态工厂与绑定方法

纯语言文件。

```oxygen
struct Counter {
    value: int = 0
}
impl Counter {
    fn add(amount: int): void { self.value += amount }
    fn same(other: Self): bool { return self == other }
    static fn starting(value: int): Self { return Self(value: value) }
}

let counter: Counter = Counter.starting(10)
let add_later = counter.add
add_later(2)
let result: int = counter.value
let identical: bool = counter.same(counter)
```

result 为 12。保存实例方法保留当时的接收者和参数约束，不复制实例。保存静态方法得到没有接收者的函数。不能写 `counter.starting(...)`，也不能直接写未限定的 `starting(...)`。

### 示例 05-D：可空 Self 链接

纯语言文件。

```oxygen
struct Link {
    value: int = 0
    next: Self? = null
}

let first: Link = Link(value: 1)
let second: Link = Link(value: 2)
first.next = second
second.next = first
```

普通引用字段可以组成环；这不是 embed 类型环。遍历这种数据须明确终止条件，不能假定 null 必定出现。

## 普通组合与 embed

普通字段只通过 `actor.stats.health` 访问。`embed stats: Stats` 保留这个真实字段，并额外递归提升该嵌入类型的字段与实例方法。

### 示例 05-E：嵌入转发与保存的接收者

纯语言文件。

```oxygen
struct Stats {
    health: int = 100
    fn alive(): bool { return self.health > 0 }
}
struct Actor {
    name: string
    embed stats: Stats = Stats()
}

let actor: Actor = Actor(name: "A", stats: Stats(health: 80))
actor.health = 70
let old_check = actor.alive
actor.stats = Stats(health: 0)
let current: bool = actor.alive()
let previous: bool = old_check()
```

current 为 false，previous 为 true。提升调用转发到当前 stats；此前保存的方法仍绑定保存时的旧 Stats。

embed 的约束：

- 明确标注非空 struct，包括导入的 alias.Type；不能嵌入 any、object、集合或 T?。
- 构造只接受真正字段名 `stats: Stats(...)`，不能把提升成员 health 当 Actor 的构造参数。
- 访问转发不复制、不继承，不把 Actor 当作 Stats。
- 静态方法和 trait 实现身份不提升。
- 直接字段、方法、嵌入字段名和全部提升名称不能冲突；没有本地覆盖优先级。
- 多条路径提供同名成员或嵌入类型图出现环会拒绝。改为普通命名组合、调整名字或缩减嵌入解决。

## nominal trait：明确实现契约

trait 是名义能力契约，不是 RimWorld 的 TraitDef。它只声明签名：参数与返回类型全部明确，不能带函数体、默认实现或泛型方法。可声明实例或 static 签名；Self 表示实际实现类型。

必须显式 `impl Trait for Type`，仅有同名方法不算实现。实现必须完整匹配方法名、static 状态、参数数量、参数名字、参数类型和返回类型，不能多出额外成员。参数名也参与契约，因为调用者可用命名实参。

trait／impl 只能在文件顶层。impl 只能位于 struct 的定义模块，不能从别的模块为导入的类型追加方法。普通额外方法应放 `impl Type`，不要混入 trait 实现块。

### 示例 05-F：名义 trait 与约束函数

纯语言文件，不需要游戏 TraitDef。

```oxygen
trait Named {
    fn name(): string
}
struct Point {
    x: int = 0
    y: int = 0
}
impl Point {
    fn coordinates(): string { return f"{self.x},{self.y}" }
}
impl Named for Point {
    fn name(): string { return self.coordinates() }
}

fn describe<T: Named>(value: T): string {
    return value.name()
}

let point: Point = Point(x: 3, y: 4)
let inferred: string = describe(point)
let from_constructor: string = describe(Point(x: 5, y: 6))
let dynamic_value = Point(x: 7, y: 8)
let explicit: string = describe<Point>(dynamic_value)
```

泛型函数体只可使用 trait 声明的能力，不能因为某个实际 struct 恰好还有 x 字段就在 value 上读 x。未调用函数和不可达分支也检查，不把它们当延迟检查的漏洞。

同名 trait 在不同模块中是不同契约；导入别名不同不改变同一契约身份。不同 trait 在同一 struct 上要求重复成员名仍会冲突，不存在函数重载或自动分派消歧。

## 受约束泛型的范围

普通函数可有多个类型参数，每个参数必须有且只有一个 trait 约束，例如 `fn pair<T: Named, U: Named>(...)`。没有无约束的 `<T>`，也没有多个 trait 交集约束。

静态推断来自受标注变量、直接构造表达式、明确返回类型和相应集合类型上下文。无标注 let 或未标注返回值不自动变成固定类型，无法据此推断时用显式 `describe<Point>(value)`；显式类型仍检查实际值。

实现类型须为满足约束的具体非空 struct，不把 int、any、object、可空 struct 或 trait 当成实现类型。泛型函数可转发约束到其它泛型函数；动态别名不自动保留推断来源，也不能借此扩大可访问能力。

当前不支持泛型 struct、trait、impl、方法或 trait 对象。泛型集合 list／array 与 trait 约束函数是两种不同入口。

### 示例 05-G：静态 Self 工厂

纯语言文件。

```oxygen
trait Factory {
    static fn create(text: string): Self
}
struct Label {
    text: string
}
impl Factory for Label {
    static fn create(text: string): Self { return Self(text: text) }
}

fn make<T: Factory>(text: string): T {
    return T.create(text)
}

let result: Label = make<Label>("Oxygen")
let text: string = result.text
```

这里参数 text 本身不能推断 T，须显式指定 Label。静态工厂回调依然使用实际实现的 Self，不开放任意“按类名创建对象”。

## 预算与性能

语言默认执行预算为 10000 步、调用深度 64；宿主可配置。条件和准备好的回调调用各自重置预算。预算不是游戏 tick、耗时保证或持久冷却，也不替可信原生 C# 回调限制执行时间。

- 循环设有限边界，递归设终止条件。
- 不用忙等等待 game.tick 改变；当前同步调用中游戏不会因循环自行前进。
- range 产生实际集合且受数量限制，避免超大 range、嵌套循环和每次回调重建大容器。
- 将固定配置和可复用函数放在初始化阶段；在回调里只读取必要事实和计算结果。
- 避免把频繁调用的条件做成随机或有副作用的动作。应用验证、立即评估和正常周期都可能调用它。
- 动态心情默认每 150 tick 评估，不是每 Pawn 每 tick 解释；减小间隔仍会增加工作量。

设置标题、visible、enabled、options 回调也会在使用时求值，不是点击一次才调用。原生回调在游戏主线程执行，耗时动作会阻塞游戏；预算无法中断已经开始的任意 C# 回调。

## 生命周期与持久状态

区分四种东西：

- **编辑文本**：尚未应用的草稿，不等于当前有效版本。
- **脚本实例**：顶层变量、模块全局值、闭包和绑定方法；应用或读档重建，不自动持久化。
- **宿主状态**：动态心情效果、冷却、固定选择；设置存储；原生规则停用／布局签名。只按各宿主契约保存。
- **启动内容**：原生 Def 和 patch；需要重启，不能用规则重载改布局。

失败更新保留相应旧有效计划，不保证跨所有 Mod 的事务，也不回滚脚本主动调用的原生副作用。读档重新读取依赖，不能假定此前模块源码会随主脚本保存。

## 常见错误与正确处理

以下是说明，不是待执行围栏，便于把所有代码围栏当完整正例检查。

- **`let fixed: int = "1"`**：约束不接受文本，用 `int("1")` 明确转换。float 有小数不能转 int。
- **`let xs = [1]; let typed: list<int> = xs`**：xs 是已有 list<any>，用 `list<int>(xs)`。直接标注的字面量才有目标上下文。
- **空 array 后写 `[0]`**：固定长度不扩容；先构造具有元素的 array 或选 list.add。
- **把 list<int> 当 list<float>**：集合不变；显式 list<float> 转换。赋给 any 不会移除元素约束。
- **const 初始化调用、读取 let／Pawn**：静态表达式限制，改用 let；const 不保证深度不可变。
- **以为 if 花括号隔离变量**：只有函数建立独立局部作用域。重新设计声明位置，不靠分支隔离重名。
- **把函数名当条件**：须调用 `ready()`；注册回调恰好相反，传 `when: ready`，不是调用结果。
- **int 返回函数没有 return**：隐式 null 不匹配；补上真实返回，或按契约改 void／T?，不要乱填零值。
- **unknown member 被 `??` 捕获**：?? 只处理 null；用 exists 探测存在，或按宿主规定判 null。
- **object 当 struct／同字段 struct 互赋**：名义类型不看形状，使用正确构造器。
- **embed 成员冲突**：没有覆盖优先级；改普通组合或改名。embed 不传播 trait 身份。
- **trait 方法形参改名**：命名实参契约变化，实现必须与签名一致。
- **动态绑定推断泛型失败**：给变量／返回标注，或在调用处指定具体类型；不要假定默认绑定是自动类型推断。
- **optional 导入坏源码被报错**：这是预期行为，optional 只跳过不存在；修复坏模块。
- **可选模块的 Type 在跳过分支中仍报错**：类型准备早于分支执行；不要引用缺失模块的类型。
- **导入失败却找到另一个 Mod 的同名文件**：加载器不搜索替代提供方；核对 packageId、启用状态、模块路径、pub 和大小写。
- **设置 default 改了但旧值不变**：default 仅处理缺失，不覆盖已有存储；需要消费端明确迁移旧值。
- **changed 报错但设置已改变**：Set 已完成，回调失败不撤销；按消费端真实状态恢复，不宣称整个操作未发生。
- **content 改了但本局无变化**：需要重启；规则函数热重载不等于原生布局热重建。
- **保存了积木但游戏没变**：积木 JSON 不是执行入口，检查并导出 `.oxy`，再保存／应用正确文件。

## 权限与文件边界

语言容器可写不意味着宿主对象可写；any、别名、struct、embed 和 cs 包装都不能绕过白名单。当前没有任意反射、按类名字符串执行 C#、文件／网络访问、线程、Harmony 或通用 Job 操作。

主脚本从文件读取是窗口显式操作；库只查对应活动 Mod 的 Oxygen 目录。网页编辑器是另一受限宿主：只监听 127.0.0.1 随机端口，要求会话 token 和同源，不提供任意文件或远程执行。

网页编辑资格来自活动 Mod 的 Oxygen 目录或注册文档，不再要求 Dependencies 声明 ODF；仍不开放任意 Mod 文件访问。非官方 About 依赖用于加载顺序与缺依赖提示，应保持 packageId、displayName 和有效 downloadUrl／steamWorkshopUrl，避免被游戏元数据校验移除。

物理编辑文件限定活动加载目录内的 `.oxy`／`.oxyblocks`；创建新文件不能覆盖现有文件，拒绝路径逃逸和重定向。源码上限为 1048576 个 UTF-16 code units，JSON 请求体上限 8 MiB。版本冲突返回 409 并保留本地编辑，选择重新载入或保留，不假装保存成功。

切换存档使旧存档文档失效；安装文件保存立即改变 Mod，存档草稿还要保存游戏才落盘。浏览器关闭／主菜单不自动停止服务，用 IrisMenus 的退出编辑器入口或退出游戏释放监听器。

## 推荐检查顺序

1. 将一个示例／实际文件单独交给 parser/analyzer，库用 isModule 模式。确认语法、作用域和局部类型约束。
2. 给真实编译器注入所需模块、翻译和注册宿主。设置例交给设置编译器；动态心情例交给 ScriptCompiler。
3. 原生消费包用 check 检查字段和引用，未知目录保留 Unknown，不把不完整 catalog 宣称完整。
4. preview 只运行显式 JSON 事实，不替代游戏 Job、原生思想资格和真实对象行为。
5. 在备份／隔离存档观察应用、撤销、保存、读档和依赖变化，错误看行列、规则／对象和有效版本。

VSCode 检查器不是完整 Mod 加载器；网页“检查成功”也不执行玩法。检查范围与宿主验证层次要一起看。返回[入门](00-overview.md)选择入口，或按需查[语法](01-keywords-and-syntax.md)、[容器](02-values-and-functions.md)、[玩法](03-gameplay-and-content.md)和[模块设置](04-modules-and-settings.md)。
