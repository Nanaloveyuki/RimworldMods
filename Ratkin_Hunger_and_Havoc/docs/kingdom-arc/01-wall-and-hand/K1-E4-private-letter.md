# K1-E4：第一封私人来信

> 作者最新来信修稿见 [人物校准稿](../../../tmp/text/kingdom-arc/K1-E4-private-letter.md)，本页旧正文尚未回填。2026-10-13 起 K1～K5 只铺陈背景与初步合作，旧初始属性中的 `kingdomEnding` 不再是有效设计，见 [新版跨幕流程](../workflow.md)。

## 剧本

[背景图:沿用如今寝室的机位与布局，穗音桌边有揉皱的纸和两份尚未寄出的抄件]
[手动阅读]
[发话:穗音，私人来信]

```text
我写了又撕，已经弄坏好几张纸了。
```

[等待:玩家点击下一段，沿用同图]

```text
宫里有两份回执，写的是同一批粮食。一份说已经送到了，另一份记着还没有发出。我问过保管它们的人，他让我去问另一个人。
```

[等待:玩家点击下一段]

```text
我不敢再把原件拿出来，抄了两份。你们愿意替我看看吗？
```

[等待:玩家点击下一段]

```text
我现在只想知道，究竟是哪一份写错了。
——穗音
```

[显示:行动条件与答复按钮，停止演出]
[界面说明:接受后接收副本；当前无需交付物资]

```text
愿意看看
```

```text
以后再说
```

```text
不接这件事
```

[条件:接受]
[背景图:穗音把抄件封进信封，交到信使手中]
[等待1.5秒自动切换]
[发话:穗音，简短回信]
[手动阅读]

```text
谢谢。我让送信的人把抄件带过去。
```

[条件:拒绝]
[背景图:穗音把抄件收进抽屉]
[等待1.5秒，随后手动阅读]

```text
好，我先收起来。
```

[条件:暂缓；停留原信，无新画面或催促]

## 流程与分支

接受只同意接收副本；下一幕副本实际寄达后才记录持有。拒绝关闭本次邀请，章节入口保留主动联系；重新联系重开同一委托。回看或跳过回忆回到当前信件，不重复接受或寄送。

## 事件属性

```yaml
id: K1-E4
kind: illustrated_letter
entry: K1-E3结束或跳过整幕
branches:
  accept: {kingdomAct: 2, next: K2-E1}
  defer: {kingdomAct: 1, invitation: pending}
  refuse: {kingdomAct: 1, invitation: declined}
initial: {suiyinPosition: undecided, kingdomEnding: none, recordCustody: none, witnessState: unknown}
```

## 插图与归属

寝室、封缄递信、收件入抽屉三种动作均沿用 `K1-E1-garden-1.png` 的机位与家具位置；暂缓复用原信。床、桌、椅、窗、电灯和茶具不随分支重新摆放，穗音在原座位递信，拒绝时打开同一张桌子的前侧抽屉。当前画面不能画成副本已在玩家桌上。本篇往来由独立信使承担，不发普通资助路费，不改原有穗音信任或案件。

底图位于 `background/`；暂缓分支复用 `-1`。

| 文件 | 画面 |
| --- | --- |
| [K1-E4-private-letter-1.png](background/K1-E4-private-letter-1.png) | 原寝室机位、揉皱的纸与两份抄件 |
| [K1-E4-private-letter-2.png](background/K1-E4-private-letter-2.png) | 接受后，封缄递给独立信使 |
| [K1-E4-private-letter-3.png](background/K1-E4-private-letter-3.png) | 拒绝后，两份抄件收入抽屉 |
