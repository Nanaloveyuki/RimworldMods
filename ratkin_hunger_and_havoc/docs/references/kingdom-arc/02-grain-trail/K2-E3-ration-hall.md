# K2-E3：配给厅闭门之后

## 剧本

[背景图:配给厅门外，信使拿着穗音取得的持函核验许可]
[手动阅读]
[发话:穗音，邀约信]

```text
他们同意在闭门后让你们看原簿。

请带上抄件。若书记愿意同行，也把他的旧证件带着。
```

[显示:地点、往返路程、实际赴约期限与随行者；等待答复]

```text
准备赴约
```

```text
放弃这次核验
```

[条件:抵达且许可有效]
[背景图:配给厅室内，桌面摊着原簿，管理人站在另一侧；书记仅在实际同行时入画]
[等待1.5秒，随后手动阅读]
[发话:配给厅管理人]

```text
原簿留在这里。你们可以核对，也可以抄走；请把看过的页码写在函上。
```

[动作:翻到对应货号，0.6秒；原簿的两次改写由界面叠字显示]
[发话:核验者的现场记录]
[手动阅读]

```text
出库数量改过两次。第一次旁边有签名，第二次只有日期。领粮签收的位置空着。
```

[条件:书记声明已实际送达；附加段]

```text
“送到”栏的字迹与书记的声明相同。
```

[条件:书记实际同行]
[背景图:书记指着“送到”栏；管理员低头看函]
[发话:书记]
[文案停留4秒，可暂停]

```text
就是这里。签收栏空着的时候，我就写了。
```

[条件:核验结束]
[背景图:玩家持有的核验抄件被装进信封，原簿留在桌上]
[发话:现场记录]
[手动阅读]

```text
页码与改写处已经抄下。原簿仍由配给厅保管。
```

[条件:入厅遭拒]
[背景图:门内只伸出一只退还函件的手，原簿不可见]
[发话:信使，结果通知]
[手动阅读]

```text
配给厅退回了函件，没有让我们看原簿。他们要求另行申请。
```

[条件:未赴约或许可过期]
[背景图:配给厅闭门，门边公告已换下]
[手动阅读]

```text
这次核验的时段已经过去。我们没有见到原簿。
```

[条件:主动放弃核验]
[背景图:未使用的许可放回信封]
[手动阅读]

```text
这次不去配给厅了。许可收起来，原簿还没有看过。
```

[条件:无法安排可达地点]
[背景图:许可与路线图并列，不展示抵达画面]
[手动阅读]

```text
这次没能安排可通行的赴约路线，核验没有开始。
```

## 流程与分支

准备赴约不等于抵达。期限采用赴约窗口七个游戏日的建议值；地点须选在该窗口可通行的范围，无法创建时明确报告未安排成功并收束，不丢入无限等待。现场核验产生带页码的独立抄件，不偷取原簿、不奖励白银。拒绝、错过和主动放弃均接未核验收束。

## 事件属性

```yaml
id: K2-E3
kind: hall_verification
entry: K2-E2继续调查
appointment_days: 7
branches:
  verified: {hallResult: verified, evidenceLevel: verified, verifiedKnowledge: true, next: K2-E4}
  denied: {hallResult: denied, next: K2-E4}
  expired: {hallResult: expired, next: K2-E4}
  declined: {hallResult: declined, next: K2-E4}
  unavailable: {hallResult: unavailable, next: K2-E4}
source_original: 配给厅保管
```

## 插图

门外、室内核验、原簿近景、封件、退函、闭门六组；书记另作人物叠层，缺席不改变玩家是否看过原簿。正常 Letter 手动阅读，入厅与翻簿动作分别1.5秒、0.6秒。

底图采用厚涂重描边，放在 `background/`。可读文书内容由界面绘制，不烘焙进图。

| 文件 | 画面与使用条件 |
| --- | --- |
| [K2-E3-ration-hall-1.png](background/K2-E3-ration-hall-1.png) | 信使持许可在配给厅门外；开场邀约 |
| [K2-E3-ration-hall-2.png](background/K2-E3-ration-hall-2.png) | 室内核验、不带书记；抵达且许可有效，书记缺席 |
| [K2-E3-ration-hall-3-revised.png](background/K2-E3-ration-hall-3-revised.png) | 原簿栏位近景；入厅后翻簿核验 |
| [K2-E3-ration-hall-4-revised.png](background/K2-E3-ration-hall-4-revised.png) | 同一书记指向原簿；书记实际同行 |
| [K2-E3-ration-hall-5.png](background/K2-E3-ration-hall-5.png) | 装走核验副本、原簿留下；核验完成 |
| [K2-E3-ration-hall-6.png](background/K2-E3-ration-hall-6.png) | 闭门退还许可；入厅遭拒 |
| [K2-E3-ration-hall-7.png](background/K2-E3-ration-hall-7.png) | 错过核验后的闭门；未赴约或许可过期 |
| [K2-E3-ration-hall-8-revised.png](background/K2-E3-ration-hall-8-revised.png) | 未使用许可收回信封；主动放弃核验 |
| [K2-E3-ration-hall-9.png](background/K2-E3-ration-hall-9.png) | 许可与无法安排的路线；未安排成功 |

