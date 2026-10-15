# K2-E2：被撤职的书记

## 剧本

[背景图:配给厅外的小茶铺，书记穿着旧制服，面前只放着一杯水]
[手动阅读]
[发话:书记，经本篇信使转录的谈话]

```text
那两个字是我写的。“送到”。

车还没出门，主管让我先填。我问签在哪一栏，他说先把数字写齐。
```

[等待:玩家点击下一段；保持茶铺画面]

```text
后来有人来找领粮的签字，我拿不出来。他们就让我收拾东西了。
```

[画卷转场:0.4秒]
[背景图:如今的穗音寝室，她把书记的声明夹在两份抄件旁]
[发话:穗音，来信]
[手动阅读]

```text
这是他托人带来的话。他愿意陪你们看原簿，但不想让自己的名字出现在公告上。

他说车还没出门，主管就让他填“送到”。我听得出他怕再惹上麻烦，可光凭这段话，我还不能知道整批粮食后来去了哪里。

我可以替你们要一封入厅的函。到了那里，原簿也许能对上他的话。
```

[显示:是否邀请书记同行；出行条件须在确认前列出]

```text
请他同行，暂不公开姓名
```

```text
只保留声明，独自核验
```

```text
停止本轮调查
```

[条件:邀请且书记仍可联系]
[背景图:书记拿着自己的旧证件，信使等在门外]
[等待1.5秒，随后手动阅读]
[发话:书记]

```text
我陪你们去。但我的名字只给核验的人看，好吗？
```

[条件:不邀请]
[背景图:署名声明留在桌面，证件被书记收回]
[手动阅读]

```text
那我把当时的事写下来。你们带着去问。
```

[条件:书记撤回、离开或死亡，按事实只显示对应一段]
[背景图:茶铺空座，不再显示会谈人物]
[发话:本篇信使，结果通知]
[手动阅读]

```text
书记撤回了同行的答应。他请我们别再去找他。
```

```text
书记已经离开，没能等到这次核验。
```

```text
书记死了。已经寄出的声明还在，他不能再来作证。
```

[条件:停止；返回收束信，不追加新会谈]

## 流程与分支

书记是本篇单独创建并追踪的角色，不取代普通问路来客。他的陈述是证词而非事实认证；允许不带证人赴约。邀请成立后在实际出行前再次核对身份与同意，撤回者不能被强行加入队伍。声明已送达才保存，未送达时不能使用相应文本。

## 事件属性

```yaml
id: K2-E2
kind: witness_interview
entry: K2-E1正式或私下问询
on_interview: {witnessState: available}
on_statement_received: {clerkStatement: true}
branches:
  accompany: {witnessConsent: private, witnessState: available, next: K2-E3}
  statement_only: {witnessConsent: private, next: K2-E3}
  stop: {next: K2-E4}
on_withdrawal: {witnessState: withdrawn}
on_departure: {witnessState: absent}
on_death: {witnessState: dead}
```

## 插图与节拍

茶铺全景、寝室文书、证件近景、空座四组底图；谈话全部手动阅读。空座变体只报告真实结果，死亡不附加惩罚。玩家原有仓库、救济计数与信任不参与此委托。

书记沿用灰棕短发、旧深蓝制服、赭色证件与棕皮包；信使沿用棕色短发与绿色披肩。寝室家具位置与 K1 相同。

| 文件 | 画面与使用条件 |
| --- | --- |
| [K2-E2-dismissed-clerk-1.png](background/K2-E2-dismissed-clerk-1.png) | 茶铺会谈，一杯清水 |
| [K2-E2-dismissed-clerk-2.png](background/K2-E2-dismissed-clerk-2.png) | 寝室摆放声明与两份抄件 |
| [K2-E2-dismissed-clerk-3.png](background/K2-E2-dismissed-clerk-3.png) | 书记同意同行后出示旧证件 |
| [K2-E2-dismissed-clerk-4.png](background/K2-E2-dismissed-clerk-4.png) | 不邀请时留下声明、收回证件 |
| [K2-E2-dismissed-clerk-5.png](background/K2-E2-dismissed-clerk-5.png) | 同一茶铺空座；撤回、离开与死亡共用，不画默认死因 |

