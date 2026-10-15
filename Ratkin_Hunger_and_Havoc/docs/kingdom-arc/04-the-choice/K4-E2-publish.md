# K4-E2：公开记录

> 2026-10-13 定位调整：本稿保留为早期行动候选素材，正文暂不回填。公开材料不锁定最终政治路线；旧立场与终局属性不再是实现依据，按 [新版跨幕流程](../workflow.md) 审阅。

## 剧本

[背景图:穗音桌上有实际可用材料，空白封套分成两份]
[手动阅读]
[发话:穗音，委托信]

```text
我想把这些纸交给配给点的代表，也交给运输行的人。

他们各自留一份，能找我们问，也能互相问。尚未核对的地方，我会写清楚。
```

[条件:证据仅为初始副本]

```text
这次只能公开两份回执的差异。我们没有见过原簿，别把抄件写成已经查清的结果。
```

[条件:具备原簿核验或现场报告]

```text
看过的页码和现场记下的时间也附进去。书记的姓名仍遮住，除非他自己答应署名。
```

[显示:材料清单、两组独立收件人、公开后无法收回所有副本的风险；等待答复]

```text
分别送出材料
```

```text
回去考虑其他办法
```

[条件:第一份送达]
[背景图:配给点代表接过材料，收件确认留在信使手中]
[等待1.5秒自动切换]
[发话:配给点代表]
[手动阅读]

```text
我们收到了。我要拿给领过这批粮的人一起看。
```

[条件:第二份独立送达]
[背景图:运输行收件人在自己的桌边展开另一份材料]
[等待1.5秒自动切换]
[发话:运输行收件人]
[手动阅读]

```text
哪天出车，可以问搬货的人。这几处日期我们会再查。
```

[条件:两份均确认送达]
[背景图:穗音收到两封不同来源的确认函]
[发话:穗音，结果信]
[手动阅读]

```text
两边都收到了。他们问的问题还不一样。

我担心有人会把名字猜错，另外写了一封说明，把我们还不知道的部分又列了一遍。
```

[条件:只送达一份或全部未送达；分开使用]
[背景图:确认函只显示已收到的数量，未达封套仍关闭]
[手动阅读]

```text
只收到一边的回信。另一份没有确认送到。
```

```text
没有收到确认。这次寄出的材料，还不能说已经公开到他们手里。
```

## 流程与分支

进入要求至少有一种可实际复制、交付的本篇材料；可公开初始抄件、核验记录或现场报告，说明各自来源与未核对部分。两组收件人分属配给点代表与运输行，不能把同一机构的两个人计成两组。书记姓名默认遮蔽，署名需他仍可联系且明确同意。交付各自确认，七个游戏日后未确认形成部分或失败结果。公众得知材料可能引来追问，不能据此保证暴动、改革或粮食奖励。

## 事件属性

```yaml
id: K4-E2
kind: independent_publication
entry: 第四幕立场未定且存在可交付材料
reply_window_days: 7
recipient_groups: [配给点代表, 运输行]
branches:
  both_confirmed: {routeOutcome: success, recordCustody: shared, courtAttention: high, suiyinPosition: publish, kingdomAct: 5, next: K5-E2}
  partial_close: {routeOutcome: partial, suiyinPosition: publish, kingdomAct: 5, next: K5-E2}
  failed_close: {routeOutcome: failed, suiyinPosition: publish, kingdomAct: 5, next: K5-E2}
  reconsider: {routeOutcome: none, routeFailureReason: none, suiyinPosition: undecided, next: K4-E4}
```

## 插图

封套分件、两组不同收件场景、确认函四组；确认函数量随实际结果变。传播后仍保留玩家、王国和外部持有人的各份文书，不能用“公开”覆盖原件去向。

底图采用厚涂重描边，放在 `background/`。可读文书内容由界面绘制，不烘焙进图。

| 文件 | 画面与使用条件 |
| --- | --- |
| [K4-E2-publish-1.png](background/K4-E2-publish-1.png) | 两份封套与可公开材料 |
| [K4-E2-publish-2.png](background/K4-E2-publish-2.png) | 配给点代表接件；第一组实际收件 |
| [K4-E2-publish-3.png](background/K4-E2-publish-3.png) | 运输行独立收件；第二组独立收件 |
| [K4-E2-publish-4.png](background/K4-E2-publish-4.png) | 两封不同来源确认函；两组均确认 |
| [K4-E2-publish-5.png](background/K4-E2-publish-5.png) | 只有一封确认函；仅一组确认 |
| [K4-E2-publish-6.png](background/K4-E2-publish-6.png) | 寄件存根与空确认位置；全部未确认 |
