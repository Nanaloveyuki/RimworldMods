# K4-E3：保护眼前的人

> 2026-10-13 定位调整：本稿保留为早期行动候选素材，正文暂不回填。保护书记不锁定最终政治路线；旧立场与终局属性不再是实现依据，按 [新版跨幕流程](../workflow.md) 审阅。

## 剧本

[背景图:茶铺后的房间，书记把旧证件放在包里，门外有等候问话的人影]
[手动阅读]
[发话:书记，通过独立信使送来的求助]

```text
他们又来问我签收的事。我说了几遍，还是让我明天再去。

我想先离开这里。证件和声明我都带着，不想什么也没留下就走。
```

[画卷转场:0.4秒]
[背景图:穗音寝室，桌面是一次离开许可和书记本人确认的目的地]
[发话:穗音，委托信]
[手动阅读]

```text
我替他要到了离开许可。要接他，还得有人走完这一段路。

若你们愿意，我会暂时放下问询，把接应的事办完。
```

[显示:同一书记的身份、本人意愿、接应点、实际路线与接应期限；等待答复]

```text
接应并护送书记
```

```text
回去考虑其他办法
```

[条件:本人仍同意且接应开始]
[背景图:书记持许可与护送人员会合；其他角色以通用背影表达]
[等待1.5秒自动切换]
[发话:书记]
[文案停留4秒，可暂停]

```text
就是这条路。我想去那边，不回配给厅了。
```

[条件:抵达本人同意的安全目的地]
[背景图:书记在接纳他的住处门口，打开包取出证件]
[等待2秒，随后手动阅读]
[发话:书记，回信]

```text
我到了。门能从里面关上，今晚想睡个整觉。
```

[条件:穗音收到确认]
[背景图:寝室，穗音将确认函夹进日记]
[发话:穗音]
[手动阅读]

```text
我把他的信夹进去了。今天先不追着他问数字。
```

[条件:撤回、失联或死亡；按实际状态分别使用]
[背景图:未完成的接应许可，不能画成已经入住]
[手动阅读]

```text
他改变了主意，请我们停下接应。我把许可收起来了。
```

```text
我们没能接到他。约好的地方没有人，也没有新的回信。
```

```text
他死了。我看着办好的许可，不知道该把它放在哪里。
```

[条件:接应取消、超时或途中受阻，但书记仍可联系]
[背景图:尚未完成的接应路线与许可；不画成失联或死亡]
[手动阅读]

```text
这次接应没能走完。他仍能收到我们的信，我会把停下来的地方告诉他。
```

## 流程与分支

仅使用第二幕同一书记；被撤职本身不等于被抓捕，本稿压力来自本篇后续问话。离开许可不保证路途安全，接受前明示实际风险。目的地由书记同意，玩家收留也需双方明确同意，不自动加入殖民地。接应期限建议七个游戏日；抵达安全目的地或同意收留并实际入住才成功。只拿到许可、给过食物或路上活着不能计成成功保护。撤回、失联、死亡允许结束受挫路线或返回第四幕。

## 事件属性

```yaml
id: K4-E3
kind: witness_transfer
entry: 第四幕立场未定，同一书记仍在且重新同意转移
action_days: 7
branches:
  safe_arrival: {routeOutcome: success, witnessState: safe, suiyinPosition: protect, kingdomAct: 5, next: K5-E3}
  failed_close: {routeOutcome: failed, suiyinPosition: protect, kingdomAct: 5, next: K5-E3}
  reconsider: {routeOutcome: none, routeFailureReason: none, suiyinPosition: undecided, next: K4-E4}
on_withdrawal: {witnessState: withdrawn, routeFailureReason: withdrawn}
on_lost_contact: {witnessState: absent, routeFailureReason: lost_contact}
on_death: {witnessState: dead, routeFailureReason: death}
on_cancel_or_obstruction: {witnessState: 按实际联系情况保留, routeFailureReason: 按实际原因记录cancelled、expired或obstructed}
```

## 插图

房内收拾、许可、接应会合、目的地门口、确认函五组。去向安全的画面仅用于实际抵达；尸体不作为失败默认插图。保护后暂停直接调查，已有文书、公开副本与核验事实继续保留。

底图采用厚涂重描边，放在 `background/`。可读文书内容由界面绘制，不烘焙进图。

| 文件 | 画面与使用条件 |
| --- | --- |
| [K4-E3-protect-1.png](background/K4-E3-protect-1.png) | 同一书记在茶铺后收拾 |
| [K4-E3-protect-2.png](background/K4-E3-protect-2.png) | 离开许可与本人同意路线 |
| [K4-E3-protect-3.png](background/K4-E3-protect-3.png) | 书记与通用背影护送者会合 |
| [K4-E3-protect-4.png](background/K4-E3-protect-4.png) | 到达同意的住处门口；实际安全抵达 |
| [K4-E3-protect-5.png](background/K4-E3-protect-5.png) | 穗音把到达确认夹入日记；实际收到安全到达确认 |
| [K4-E3-protect-6.png](background/K4-E3-protect-6.png) | 未完成接应许可；撤回、失联或死亡 |
| [K4-E3-protect-7.png](background/K4-E3-protect-7.png) | 受阻但仍可联系的路线；取消、超时或途中受阻，仍可联系 |
