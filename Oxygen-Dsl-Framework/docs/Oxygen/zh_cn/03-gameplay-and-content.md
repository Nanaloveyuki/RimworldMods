# 动态心情、原生内容与运行规则

先确定要修改的是“当前存档中的脚本效果”，还是“游戏启动时加载的原生定义”。前者使用动态心情窗口，后者写入活动 Mod 的 `Oxygen/` 下任意 `.oxy` 文件。文件名不决定用途，生命周期由声明决定。

## 动态心情的注册参数

`mood` 和 `memory` 都要求：

- 稳定、非空、同一主脚本内唯一的 `id`，通常作为唯一位置实参。
- `label`、`description`：显示文本。
- `offset`：有限心情点数。
- `target`：`"colonists"`、`"map"` 或 `"selected"`。
- 可选 `when`：接收一个 Pawn、返回严格 bool 的函数；省略表示合格目标条件成立。

`memory` 还要求正整数 tick 的 `duration`、`cooldown`，以及 `mode`：

- `"refresh"`：刷新该规则的单份记忆；不能设置 stack_limit。
- `"stack"`：有上限地叠加，必须设置正整数 `stack_limit`；达到上限替换最早的一份。

传回调写 `when: rested`，不要写 `when: rested(pawn)`。条件里可以用宽松真值，但最终结果必须为 bool；数值、字符串和 null 不能作为 when 结果。

### 示例 03-A：持续效果与限时记忆

完整动态心情主脚本，需要地图上的 Pawn 和动态心情宿主。

```oxygen
fn fed(pawn): bool {
    if pawn.food == null { return false }
    return pawn.food > 0.5 && !pawn.downed
}
fn rested(pawn): bool {
    if pawn.rest == null { return false }
    return pawn.rest > 0.7
}

mood("manual_fed",
    label: "吃饱了",
    description: "我吃饱了。现在感觉舒服些。",
    offset: 5,
    target: "colonists",
    when: fed)

memory("manual_rest",
    label: "休息过了",
    description: "我休息过了。现在没那么累。",
    offset: 3,
    target: "selected",
    duration: 2500,
    cooldown: 2500,
    mode: "refresh",
    when: rested)

memory("manual_small_break",
    label: "稍作休息",
    description: "我歇了一会儿。现在感觉好多了。",
    offset: 1,
    target: "colonists",
    duration: 5000,
    cooldown: 2500,
    mode: "stack",
    stack_limit: 2,
    when: rested)
```

一游戏日通常为 60000 tick；参数采用 tick，不是现实秒数。暂停不推进冷却和记忆年龄。正冷却防止反复“立即评估”重复添加。

## 目标、固定选择与失效

- `colonists`：当前地图殖民者。
- `map`：当前地图活着且有心情需求的 Pawn。
- `selected`：首次需要此目标的应用时捕获选中的 Pawn，之后固定；空选择也会明确捕获。要换人使用“重新捕获选择”。

死亡或没有心情需求的对象不获得效果，运行检查会说明原因。切换地图、Pawn 离开目标范围、条件变假会移除持续效果；已获得的限时记忆自然过期，不因条件变假立即消失。

删除规则、改变规则类型或停用脚本会清理对应归属的持续效果和记忆。相同标签、不同 ID 的规则互相独立，不按显示名称合并或删除。

## 动态心情热更新和存档

应用先解析、分析、验证注册与候选条件，再替换有效规则。失败保留上一份有效主脚本与玩法。

同一 ID 的普通修改不重置记忆年龄和冷却截止时间。改 duration 以原触发时间计算；改数值、标签、描述更新现有效果。缩小目标范围清理不再匹配的持续效果；降低叠加上限或改成刷新立即保留最新允许数量。

保存包括有效主脚本、启用状态、固定选择、冷却和效果身份／记忆年龄。顶层 let、模块全局值和任意闭包不持久化，应用或读档会重新初始化。读档重新读取模块；依赖缺失或编译失败时停用脚本并显示错误，不能假定旧模块源码仍在。

默认每 150 tick 评估一次，可在界面调整正整数间隔。应用／立即评估各执行一次，不在每 Pawn 每 tick 重解析脚本。运行错误会记录位置、规则或目标，不把不完整结果当作可靠的批量删除依据。

## Pawn 与游戏上下文

动态心情条件可读取：

- `pawn.id`、`pawn.name`。
- `pawn.mood`、`pawn.food`、`pawn.rest`：需求缺失时 null，先检查再做比较。
- `pawn.drafted`、`pawn.downed`、`pawn.colonist`。
- `game.tick`：当前 tick；`game.hour`：当前目标所在地的小时。

游戏原生事实宿主还提供：

- 特质查询：`pawn.has_trait(defName)`、`pawn.has_trait(defName, degree)`、`pawn.trait_degree(defName)`、`pawn.trait_suppressed(defName)`。
- 身份：`faction_id`、`faction_def`、`prisoner`、`slave`、`stage`。
- 需求／疾病：`hunger`、`rest_category`、`sick`、`young_hungry`、`young_sick`、`group_sick`。
- RHAH 来源：`origin`、`source`、`role`、`lifecycle`、`active_visitor`、`carries_plague`、`has_been_fed`。

特质查询用原生 defName，不用翻译后的 label。trait_degree 没有该特质时也可能为 0，应先 has_trait，不能仅凭 degree 0 判断存在。类别字符串来自原生枚举；`stage` 可含 `|` 组合，缺失 food／rest 的类别为空字符串。群体基于所在 Lord，否则是自由殖民者集合，不是附近半径扫描。

无 RHAH 时来源标志为 false，来源文本为空。消费 Mod 专用成员（例如 original_name、grade）不是框架通用 Pawn 字段，必须查看其宿主文档。

所有这些对象是只读宿主表面。`pawn.food = 1` 不合法，传进 any、包装进 struct 或使用别名也不会授予写入权。

## 原生内容的加载入口

递归读取所有活动 Mod 的 `Oxygen/**/*.oxy`，不要求 ODF 依赖声明。含原生声明的文件在传统 XML Patch 之后、Def 解析／引用解析／缓存构建之前执行；运行规则与设置页在 Def 完成后生效。同一相对路径按 Mod 加载文件夹优先级覆盖，其余文件按稳定路径顺序执行，提供者按活动 Mod 顺序处理。

内容候选 XML 验证成功后才提交该提供者的计划，失败不提交半份定义。启动时没有旧内容可以恢复。所有 define／patch 修改需要重启；不要把“文件已保存”误认为已热改原生 Def。

本批内置内容类型为 `TraitDef`、`ThoughtDef`。其它类型必须由可信宿主扩展 schema，不能因为字符串可写就假定已经支持 ThingDef、Job 或任意 CLR 类型。

## template、define、def_ref 与 native_type

- `template("name", type: "TraitDef", fields: {...})`：作者模板，不注册抽象 Def。
- `define("TraitDef", "StableDefName", fields: {...})`：注册真实原生 Def。
- `def_ref("TraitDef", "Industriousness", package: "ludeon.rimworld")`：带类型和可选包来源限制的原生引用，不是裸字符串。
- `native_type("RimWorld.ThoughtWorker_AlwaysActive")`：解析真实 CLR 类型并校验要求的基类；只用作声明值，不授予调用任意方法的权限。

模板可用 extends 和 mixins。按 mixins 顺序合并，再合并 extends 与本项字段；对象递归合并，列表替换，展开后容器独立。`extends: "template_name"` 指作者模板，`extends: def_ref(...)` 指原生基项的有效内容，不生成 XML ParentName。跨提供者基项必须先加载；同一计划可引用自己的其他定义；循环继承拒绝。

### 示例 03-B：原生特质和情境思想

完整 `Oxygen/content.oxy`。需要原生内容宿主、RimWorld 的 WorkSpeedGlobal StatDef 和正确 About／加载目录包装。不会自动把特质授予 Pawn；测试时须用正常游戏方式或开发者操作给予特质。文本可通过 DefInjected 翻译。

```oxygen
template("manual_rare_trait", type: "TraitDef", fields: {
    commonality: 0
})

define("TraitDef", "Manual_HardWorker", extends: "manual_rare_trait", fields: {
    degreeDatas: [{
        degree: 0,
        label: "勤于工作",
        description: "{PAWN_nameDef}有活可做时就会继续工作。{PAWN_pronoun}的工作速度提高15%。",
        statFactors: [{stat: def_ref("StatDef", "WorkSpeedGlobal"), value: 1.15}]
    }]
})

define("ThoughtDef", "Manual_FedThought", fields: {
    requiredTraits: [def_ref("TraitDef", "Manual_HardWorker")],
    stages: [{
        label: "吃饱了",
        description: "我吃饱了。可以接着做事。",
        baseMoodEffect: 3
    }]
})
```

### 示例 03-C：绑定上述思想

完整 `Oxygen/rules.oxy`，与示例 03-B 同一消费 Mod；需要已经加载的 Manual_FedThought 和原生规则宿主，不能单独在无该 Def 的游戏中绑定。

```oxygen
fn fed_stage(pawn): int {
    if pawn.food == null { return -1 }
    if pawn.food > 0.5 { return 0 }
    return -1
}

thought_rule("manual_fed_stage",
    thought: def_ref("ThoughtDef", "Manual_FedThought"),
    evaluate: fed_stage)
```

evaluate 接收一个 Pawn，返回 -1 表示未激活，或 `[0, stages.Count)` 的整数阶段。原版仍检查 requiredTraits、抵消特质、等级、基因、Hediff 和发育阶段；函数返回 0 不代表可绕过资格。没有显式 worker／memory class 的新情境思想使用框架 Worker。

固定特质数值由原生属性机制合成，不另用脚本逐 Pawn 周期加成。内容字段名沿用原生名称：特质的 degreeDatas、statOffsets、statFactors、conflictingTraits；思想的 stages、requiredTraits、durationDays、stackLimit 等。

嵌套内容可以由 object 或 struct 字段提供，提交时变成独立内容值。未知字段、错误字段类型、错误 Def 引用类型、非整数 int 字段、空等级／阶段和重复等级拒绝。属性项使用 `{stat: def_ref("StatDef", "WorkSpeedGlobal"), value: 1.15}`；技能项使用 `{skill: def_ref("SkillDef", "Plants"), amount: 2}`。

## patch 的定点修改

patch 明确指定一个目标。目标零项或多项报错；requires 中缺少任一活动包则跳过。身份字段不能改名。

### 示例 03-D：修改已有原生特质

独立完整 `Oxygen/content.oxy`。需要原版 Industriousness；它会改变原版特质生成权重，勿与示例 03-B 拼接当作没有副作用的练习。

```oxygen
patch("TraitDef", "Industriousness",
    set: {commonality: 1.5},
    requires: ["ludeon.rimworld"])
```

set 可设置整项字段，也可用字符串点路径如 `"degreeDatas.0.label"` 修改已有项。索引零基，越界报错。append／remove 的值为列表，路径必须指向原生列表；追加按引用键／标量值去重，移除没有匹配任何项时报错。修改 XML 继承字段会先展开该字段，避免父列表合并抵消删除。

作者应明确列出修改对象与 Mod 依赖，不依赖脚本自动猜测兼容路径。完整安装示例在发布仓库的 `Examples/NativeTraits`。

## RHAH 三态行为闸门

`gate_rule(id, gate: "Beg", evaluate: function)` 绑定一个单 Pawn 参数函数，返回严格 `bool?`：

- true：允许。
- false：拒绝。
- null：不参与，保留其他覆盖／默认规则。

### 示例 03-E：离开中的来客不再乞讨

完整 `Oxygen/rules.oxy`。需要 RHAH 闸门适配器与原生事实宿主；没有 RHAH 不能据此宣称产生乞讨行为。

```oxygen
fn allow_begging(pawn): bool? {
    if !pawn.origin || !pawn.active_visitor || pawn.colonist { return null }
    if pawn.lifecycle == "Leaving" { return false }
    return pawn.hunger == "Hungry"
}

gate_rule("manual_begging", gate: "Beg", evaluate: allow_begging)
```

支持的闸门名：Beg、Steal、Fight、LeaveAfterFed、EatOutsideRelief、FeedFromRelief、Gnaw、TailBite、Leash、Carry、JoinColony、Hire、Transfer、Imprison、DropOffChild、ExitMap。大小写必须一致。

适配器稳定注册一次，角色自身覆盖优先；脚本没有参与时沿用 RHAH 默认。闸门不写永久 SetGate，也不直接启动 Job；允许某行为不等于当前 Pawn 必定执行它。

## 原生规则重载的边界

规则逐提供者替换；失败保留该提供者上一份有效快照，不是所有 Mod 之间的全局事务。重复 ThoughtDef 绑定或同闸门绑定拒绝；停用的提供者不占活动绑定。

内容源码或导入内容变化会阻止相关规则半份热更新，内容变化应重启。规则停用状态与原生等级／阶段布局签名随存档保存；布局不兼容时停用规则，不自动重新解释已有 Pawn 等级与阶段。

普通变量仍是临时脚本实例状态。需要持久玩法状态、权限外的操作、任意方法拦截或自定义 Job 时，必须由消费端明确实现；当前语言不会自动补齐这些能力。

停用 Mod 规则不会删除原生 Def、已经授予的特质或已获取的原生记忆；它不是安全卸载内容包的通用操作。原生状态的删除或迁移仍按消费包契约处理。

## 持久事件链

事件链不是动态心情函数。消费 Mod 在 C# 注册 provider，框架保存它的实例、阶段、期限和动作回执，并仅在到期时求值。实际生成、信件和其它不可逆动作仍由消费端的白名单实现执行。

事件宿主提供 `use events.ctx@event as ctx`。决议函数返回 `ctx.wait(next_tick)`、`ctx.action(action_id, next_tick: ..., stage: ...)` 或 `ctx.finish("completed")`；普通心情窗口和自动内容编译器不提供这个模块。事实字段与动作名必须查消费端接口，不能凭字符串调用任意 C#。

Paused、Disabled、Cancelled 与 owner-unavailable 有不同语义：暂停／停用保留实例并停止回调，取消写终态，缺失 owner 保留原始数据等待恢复或导出。恢复不会重放已确认动作；未知版本或未确认副作用会显示原因并停摆，不回退旧引擎。

RHAH 使用这一宿主接管已有短链、八种长链和捕食到达延迟；NarrativeState 与现有世界对象仍由 RHAH 持有。移除 RHAH 须使用它的卸载副本导出；单独停用 ODF 心情脚本不足以卸掉框架或依赖它的 Mod。

## 原生文本的 DefInjected 翻译

原生内容加载期不提供 lang 宿主。把翻译放在 `Languages/<语言>/DefInjected/<真实Def类型>/`，例如 TraitDef 和 ThoughtDef；目录按真实类型，不按用途或 XML 文件夹猜名。

示例 03-B 的思想可在 `Languages/English/DefInjected/ThoughtDef/Manual.xml` 中写：

```xml
<LanguageData>
  <Manual_FedThought.stages.0.label>well fed</Manual_FedThought.stages.0.label>
  <Manual_FedThought.stages.0.description>I've eaten enough. I can get back to work.</Manual_FedThought.stages.0.description>
</LanguageData>
```

索引对应实际 stages 顺序，DefName 不翻译。特质等级文本使用 degreeDatas 的原生翻译路径；不要把脚本注册参数名当成 DefInjected 路径。自定义 Def 类型目录使用完整命名空间；不依赖原版的目录名兼容猜测。

check／preview 可发现已知的类型目录或 Def 归属错误，缺少外部目录／程序集时标为 Unknown；不保证全部字段路径正确、翻译完整或已注入。最终在主菜单选择非英语，启用开发者模式，用 Translation → Save Translation Report 查看 General load errors 和 Def-injected translations load errors。报告通常写入桌面的 TranslationReport.txt，桌面不可用时使用游戏保存数据目录。

## 检查后仍要观察什么

- 条件是否针对正确目标，需求缺失是否被处理。
- 原生 Def 是否真实加载，标签／描述是否按预期翻译。
- ThoughtDef 阶段索引是否在范围内，原版资格是否满足。
- 同 ID 重载是否保持冷却与年龄，删除／停用是否只清理本归属。
- 保存、读档和依赖移除后的状态是否符合消费端契约。

移除框架前先停用动态脚本并另存；不要在唯一玩家存档上试卸载带活动效果的框架。下一篇介绍[模块、翻译与设置](04-modules-and-settings.md)。
