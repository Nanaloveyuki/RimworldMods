# RimworldMods

<!-- 该文件只能用来放有哪些项目, 不应该放具体实现和提示词要求 -->

RimWorld 模组、框架与开发工具的统一源码仓库。原独立仓库停止维护，后续代码、Issue 和发布统一在 [Nanaloveyuki/RimworldMods](https://github.com/Nanaloveyuki/RimworldMods) 进行。

## 框架与开发工具

| 项目 | 内容与文档入口 |
| --- | --- |
| [AdvancedRimTalk](advanced_rimtalk/) | RimTalk 提示词与编辑器扩展；[Arti 手册](advanced_rimtalk/docs/references/Arti/zh_cn/index.md) |
| [IrisMenus](iris_menus/) | 模组菜单、设置与渲染接口；[接入指南](iris_menus/docs/references/guide.md)、[Agent 合约](iris_menus/docs/agents/integration.md) |
| [Oxygen DSL Framework](oxygen_dsl_framework/) | Oxygen 语言、适配器、编辑器与离线工具；[语言手册](oxygen_dsl_framework/docs/references/Oxygen/zh_cn/00-overview.md) |
| [RimCarbon14](rim_carbon14/) | 游戏运行时检查、调试与自动化桥接；[协议](rim_carbon14/docs/references/protocol.md)、[场景操作](rim_carbon14/docs/references/testing.md) |

## 玩法与视觉内容

| 项目 | 内容 |
| --- | --- |
| [鼠族：饥与祸](ratkin_hunger_and_havoc/) | 独立鼠族事件与剧情；[事件参考](ratkin_hunger_and_havoc/docs/references/incidents.md)、[王国篇设计稿](ratkin_hunger_and_havoc/docs/references/kingdom-arc/README.md) |
| [KeepYourIdentity](keep_your_identity/) | 仅 1.7：囚犯、奴隶新生儿身份继承与限时身份选择 |
| [Lead-Your-Pets](lead_your_pets/) | 牵着你的宠物续作；原作者授权范围见项目 NOTICE |
| [Prisoners Pay To Eat 2 fork](prisoner_pay_to_eat_fork/) | 囚犯饭票经济与劳工系统；vendor 自 PPTE2，保留上游 MIT 许可证及 1.6 内容 |
| [鼠族回收行为 RatkinRecycle](ratkin_recycle/) | 原作者授权接手，保留 1.6 并独立适配 1.7；包名 `nanaloveyuki.ratkinrecycle`，MIT OR Apache-2.0 |
| [ODF-Mods](odf_mods/) | Oxygen DSL 消费端内容，各自独立打包；[PrisonerNames](odf_mods/Mods/PrisonerNames/) |
| [RimIrisShaders](rim_iris_shaders/) | RimIris 着色器与控制界面 |
| [rimworld-washingmachine-play](rimworld_washingmachine_play/) | 自动洗衣机模组 |

## 性能优化与兼容修复

| 项目 | 内容 |
| --- | --- |
| [OhYouAreSoFast](oh_you_are_so_fast/) | 寻路与 AI 优化，保留 1.6 发布内容并适配 1.7 |
| [Save Your Storage and Tick Every Minute](save_your_storage_and_tick_every_minute/) | 仓储与低频 Tick 调度优化，保留 1.6 发布内容并适配 1.7；[开发与测试](save_your_storage_and_tick_every_minute/docs/agents/development.md) |
| [RadiusUI-MissionControl-Fixes](radius_ui_mission_control_fixes/) | RadiusUI Mission Control 兼容修复 |

## 调查与归档

| 项目 | 边界 |
| --- | --- |
| [MultiFloor-fork](multi_floor_fork/) | 仅保留 [MultiFloors 机制与兼容性调查报告](multi_floor_fork/docs/references/MultiFloors-机制与兼容性调查.md)，没有源码 fork 或可部署 Mod |

## 共享入口

- [仓库规则](AGENTS.md)
- [本机引用资料](docs/References.md)
- [仓库维护、部署入口与发布资料缺项](docs/agents/repository-maintenance.md)

## 许可证

有权重新授权的自有贡献采用 **MIT OR Apache-2.0** 双许可，使用者可任选其一。完整文本见 [LICENSE-MIT](LICENSE-MIT) 和 [LICENSE-APACHE](LICENSE-APACHE)，适用范围与例外见 [LICENSE](LICENSE)。

第三方源码、素材、依赖与 fork 保留原许可证、署名及 NOTICE；根目录双许可不覆盖它们。`Lead-Your-Pets` 的上游内容仍受原作者授权范围约束。分发单个项目时，应携带其 LICENSE、NOTICE 和适用的第三方声明。
