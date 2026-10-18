# 灾鼠 Pawn 组件与扩展

## 当前边界

- `MouseDisasterGenerationPolicy`：新生成 Pawn 的特质、普通衣装准入。缓存 Def 候选，避免每名角色重新扫描所有特质。
- `MouseDisasterPawnGroupUtility.HoldForDropoff`：解除旧队伍和乞讨状态，设置投放点职责，取消独立离场计时。
- `MouseDisasterPawnGroupUtility.SendFamilyAway`：切换队伍，交给可存档的 `LordJob_MouseDisasterFamilyExit`。事件负责传入真实商人、同行者和应带走的幼儿。
- `LordJob_MouseDisasterFamilyExit`：商人逐个走到幼儿身边，调用原版 `TakeInventory` 收入携行容器，收齐后才离场。多名幼儿使用原版库存保存，并非每人单独占用手抱槽。无法接近时等待；携带者倒地时允许同行成年人接手。玩家已接纳或拘押的幼儿不再收走。
- 托孤状态保存 `deliveredChildIds`：到达投放点即记录，不会因孩子之后走动反复搬回去；母亲死亡或离场后结算仍在场的孩子。玩家收留的孩子不再转回野生身份。

组件只抽取当前重复的生成与转移职责，未拆分全部 `MouseDisasterUtility`。后续其他事件可以组合调用这些入口。

## 特质准入

默认允许官方 Core/DLC 的特质。其他 Mod 的特质需要在对应 `TraitDef` 中加入：

```xml
<modExtensions>
  <li Class="MouseDisaster.MouseDisasterGenerationExtension">
    <allowTrait>true</allowTrait>
  </li>
</modExtensions>
```

如果定义已有 `modExtensions`，在现有列表中追加 `li`，不要重复创建列表。通过 Patch 授权外部 Def 时，先确认目标存在，并补齐缺失的 `modExtensions` 节点。扩展 Mod 应在本 Mod 之后加载。

准入包含生成请求的 `prohibitedTraits`、幼儿额外特质池、负面特质补齐池和最终复核。授权允许进入候选池，不保证必定生成，也不绕过冲突、年龄和数量规则。生成请求强制新 Pawn，不拿已存在的世界角色做特质清洗。

自有 `MouseDisaster_Trait_*` 特质在经历/事件应用之后额外赋予至多 1 个：优先匹配当前经历，其次匹配当前事件或事件类别，最后按幼年/成年槽位回退。`commonality` 为 0，不进入全游戏随机人类生成，也不进入幼年额外池和负面补齐池。
名称颜色写在 `MouseDisasterTraitDegreeData.color`，数值取自原版 `ColorLibrary`（与 Core `ColorDefs` 相同的字节三元组）。角色卡与设置页都通过 `ColoredText.Colorize` 着色。
可在设置「特质」页开关每一条并调整权重；总开关关闭、未勾选或权重为 0 时不会赋予。

## 衣装准入

默认池包括部落服、普通衬衫、裤子、防寒服和既有婴幼儿衣帽。成年人的生成衣装会重配，避免把随机生成的盔甲仅降品质后继续穿着。使用既有布料/人皮材料池；品质为极差、差、一般，耐久保持 10%–60%，25% 带死者衣物标记。武器逻辑未全局重写。

兼容衣服可在其 `ThingDef.modExtensions` 中添加：

```xml
<li Class="MouseDisaster.MouseDisasterGenerationExtension">
  <allowRefugeeApparel>true</allowRefugeeApparel>
</li>
```

该授权用于成年灾鼠衣装池，仍要求物品为可制材、能遮蔽裸露、适合身体且支持现有材料的衣服。幼儿继续使用年龄限定池。鼠族麻布袋等外部衣服需以实际 Def 授权，当前未用模糊名称匹配自动放行。

规则仅作用于新生成角色；不会清洗现存殖民者、囚犯或世界 Pawn 的衣装与特质。

## 验证与读档

运行 `scripts/verify-pawn-components.ps1` 和 `scripts/verify-narrative.ps1 -Build`。前者执行真实组件代码的受控对象检查，后者检查项目、XML、本地化与叙事状态。

读档会恢复进行中的托孤职责，并升级仍在场、仍由旧离场 Lord 持有的易子而食家庭。已经离开的商人不会重新生成；早已失去原关联的落单幼儿不会被自动交给其他商人。

仍需游戏内确认：拒绝/成交/超时离场、受阻与倒地、携带中存档读档、原版与牵引 Mod 开关下的托孤投放，以及大量事件下的实际耗时。
