# ADR 0002: Pawn 身份与其它模组可改行为

- Status: Implemented
- Date: 2026-09-20
- Superseded in part by: [0003](0003-engineering-standards.md)（对外身份是 `IRHAH_Pawn`，不是 `CompRHAH_Pawn`）

## Context

旧模组用童年/成年经历判定“灾鼠”，再用二十多个空 Hediff 标角色。其它模组（牵着你的宠物、Toddlers、囚犯扩张、IrisMenus）只能 Harmony 私有方法或猜 Backstory。种族 `ThingDef` 上挂 ThingComp 会和 NewRatkinPlus / HAR 的种族补丁抢 Def。

需要：身份随 pawn 存档、不依赖经历、其它模组能查询和改行为而不打我们的内部补丁。

## Decision

1. 身份是 Hediff `RHAH_HungerMark` + `CompRHAH_Pawn`（HediffComp），不打种族 ThingDef 补丁。`Hediff_RHAH_Mark.ShouldRemove` 恒为 false。
2. 对外只保证 `HungerAndHavoc.Api.RHAH_Api` 与 `IRHAH_PawnBehavior`。查询、标记来源、释放到殖民地、闸门覆盖都走这里。
3. 行为不在 JobGiver 写死。询问顺序：单 pawn `Allow:`/`Deny:` 标签 → 后注册的 `IRHAH_PawnBehavior` → 本模组默认。闸门枚举第一期就列齐，未实现的 AI 也先占位。
4. `extraData`（`packageId:key` → string）给其它模组存私有状态，避免它们再挂一个容易被清掉的 Hediff。
5. `TryMarkOrigin` 允许其它模组生成的鼠族计入来源（例如牵引/交易带进的幼年鼠族）。

## Consequences

- 健康面板可以显示身份句，但清 Hediff 的模组理论上仍能拆标记；自定义 `ShouldRemove` 降低误伤，不能防恶意移除。API 对“没有标记”必须当成非来源。
- 其它模组 `loadAfter` 本包后注册策略即可。我们 `loadAfter` Lead Your Pet 以便可选检测，但不在内部写死对方类型名以外的反射契约。
- Harmony 补丁只用于原版/种族框架缺口；模组间协作不走私有方法名。
