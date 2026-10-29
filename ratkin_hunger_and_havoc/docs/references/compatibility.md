# 外部模组兼容登记

本页保留外部接口边界和失败策略，不维护菜单数量、设置字段或日期验收记录。适配细节以 [兼容源码](../../Source/Pawn/Compat/) 和目标模组公开 API 为准。

跨模组代码只引用 `HungerAndHavoc.Api.dll`；不引用目标模组的私有类型、字段或方法。

| 目标 | 加载顺序 | 依赖 | 支持范围 | 失败策略 | 验证范围 |
| --- | --- | --- | --- | --- | --- |
| Oxygen DSL Framework | ODF 主体先于 RHAH；行为适配器在全部静态启动完成后注册 | 必需事件框架；缺少时仅加载独立 Guard | ODF 主体不引用 RHAH API；行为适配器延后加载，不复制 API DLL | 未启用 RHAH 不加载适配器；ODF 未启用、程序集未加载或未安装时，Guard 在主界面排队一次可关闭提示，LoadFolders 跳过 RHAH 主体、Def 和 Biotech 内容。API、适配 DLL 或公开入口不匹配时明确报错，不伪装已注册 | 正常加载顺序、三态策略与 Pawn 显式覆盖优先级；缺少 ODF 时主界面可用且提示可关闭 |
| NewRatkinPlus | `loadAfter` | 必需 | 1.6，Ratkin 种族匹配与生成 | Ratkin 匹配器未注册时，来源标记请求安全失败；主体不创建无来源 Pawn | Ratkin 判定、`TryMarkOrigin`、交易带入幼年鼠族、Def 加载 |
| Humanoid Alien Races (HAR) | 随 NewRatkinPlus 前置加载 | 必需框架，运行时公开方法查询 | 1.6，`AlienRace.RaceRestrictionSettings.CanUseXenotype(XenotypeDef, ThingDef)`，按实际请求种族筛选，不新增 Harmony | 公开类型/签名缺失或查询抛异常时停止异种生成，不绕过白名单、不引用私有字段 | 实际种族许可与异种安装边界 |
| Ratkin Gene Expanded (RGE) | `loadAfter` `EoralMilk.RatkinGeneExpanded` | 可选内容 | 官方 `1.6` 和 `1.6_unofficial`；保留上游白名单和异种权重，不硬编码 RGE 候选，不扩种族许可 | 无合法异种、显式禁止/缺失、生成种族变化或异种/基因不全时清理新 Pawn 并失败；不修改已有伪鼠，不改 HAR/RGE 文件 | 两分支 XML 白名单修补、候选保留与幂等；默认池、全零、关闭优化及玩家权重 |
| Lead Your Pet | `loadAfter` | 可选 | 1.6，运行时查找 `LeadYourPet.LeadYourPetApi` 的公开方法。生成后牵幼年角色：`I-004` 亲子走母亲绳并记来源 1，其它原作亲属走普通绳，`I-013` 亲子且非出售囚犯走母亲绳并记来源 2。`I-012` 与 `I-038` 用公开 `TryStartChildLeash` 绑定同来源、同亲属组内已有的幼年随行者，不调用只支持原版 Lord 的旅行分配，不额外生成人物。加入、俘虏、成交、收留、雇佣和整批离场清理链接。弃婴投放不接。食物替代、商人不卖食物、NPC 交孩子不改 | 未启用、类型缺失、公开签名不对或 `Available` 为 false 时跳过，记一条日志，访客照常生成。不引用 `LeadYourPet.dll`，不反射 `LeadYourPetGameComponent`。清理失败不阻断离场 | 无 LYP 时主体仍可运行。年龄、距离和链接许可仍由 LYP 公开 API 判定。发布目录不出现 `LeadYourPet.dll` |
| IrisMenus | `loadAfter` | 可选 | RimWorld 1.6 公开菜单、搜索和滚动 API；兼容判断使用 `supportedVersions`，`modVersion` 只显示发布版本。裁剪读取可选公开属性 `MenuScrollView.VisibleRect`，缺失时按完整内容绘制 | 未安装、程序集缺失或不支持 `1.6` 时不注册菜单，主体和原版设置窗口仍可用。属性缺失或读取异常只记一条警告，不裁剪，页面仍可滚动和定位 | 缺失依赖、版本识别、长列表裁剪、搜索定位和 UI 缩放 |
| 金鸢尾兰鼠族 `Ratkin_OA` | 无专用加载顺序 | 可选 | 1.6，异种 `Ratkin_OA` 挂在 NewRatkinPlus 的 `Ratkin` 白名单上。本模组不引用其程序集 | Def 不存在时不进权重表，不报错。玩家在基因页加入后才参与抽取，默认权重 0 | 生物科技开启且该异种已加载时，基因页可加入并保存权重 |
| 鼠鼠退化 | 无专用加载顺序 | 可选 | 不提供新人形种族或异种。手术目标仍回到 `Ratkin` | 不扫描其动物种族，不把肉鼠、松鼠蛋、仓鼠蛋加入异种表 | 退化后的动物不进入饥与祸基因抽取 |
| 鼠蛋佳肴拓展 `DtrndG.RatEggRecipe` | 无专用加载顺序 | 可选 | 1.6。只按 defName 查原料 `RatEgg_Meat`、`RatEgg_Ear`、`RatEgg_Tail`、`RatEgg_Brain`、`RatEgg_Viscera`、`RatEgg_SilkSkin`、`RatEgg_RoundHead`，以及 `Meal_RatEgg` 前缀的菜。`I-012` 与 `I-038` 把货放进最年幼随行的背包。交易 Lord 下未满 14 岁的随行标成驮夫，菜可以卖，其它营养食物仍不卖。咬掉天然尾后地上放 1 个 `RatEgg_Tail`。本模组来源、未满 14 岁的人吃到 `Meal_RatEgg` 时记 `RHAH_Thought_AteRatEggMeal` | Def 缺失、是尸体、没有市价或商人种类拒绝时跳过该件，不报错。不引用 `RatEggRecipe.dll`，不扫全库标签 | 未安装时商队不出现这些 defName，咬尾不掉东西，吃普通饭不记这条心情 |
| Toddlers `cyanobot.toddlers` | 可选公开 Def 补丁，不新增 Harmony | 可选 | 1.6，保留原衣物白名单，仅幂等追加本模组温度衣；实际穿戴继续尊重年龄与 HAR 许可 | 目标 Def 缺失时不补；模组设置或种族限制拒绝穿戴时，不恢复许可、不强穿 | XML 补丁幂等、衣物设置及实际穿戴许可 |
| Save Our Ship 2 `kentington.saveourship2` | 无专用加载顺序 | 可选 | 1.6，`OuterSpaceBiome` 按太空家园处理，不引用 SOS2 私有类型 | 未安装时地面家园不受影响；不改 SOS2 穿梭机起飞 | 太空家园目标筛选 |
| Layered Atmosphere and Orbit `MrHydralisk.LayeredAtmosphereOrbit` | 无专用加载顺序 | 可选，不引用其程序集 | 1.7。使用原版 `OrbitLayer` 几何类型判断，包含 `isSpace=false` 的大气层；边缘无法步行入场时，从可站立、非迷雾、非危险、可空投且可达殖民地的实体地面入场。月面 `SurfaceLayer` 保留普通边缘路径 | 没有合法落点时保留原目标等待，不生成到空气中；温度、新内容、空岛与太空家园设置保持原有语义。高层室外温度超出配置时仍等待 | 普通边缘优先、非太空大气层与高轨空投选择、无地面及不可达/危险地面拒绝；实际落舱与整套 Mod 游戏环境未验收 |
| Combat Extended `CETeam.CombatExtended` | 无专用加载顺序，角色生成时解析已加载程序集 | 可选，不编译引用 CE | RHAH 更换主武器后，调用公开 `LoadoutPropertiesExtension.GenerateLoadoutFor(Pawn, float)`；独立配置只装填当前武器并按 CE 容量限制生成一匣备用弹，不重跑副武器或护盾生成。弹种和关闭弹药系统的行为交给 CE | 公开类型、构造器、`primaryMagazineCount` 字段或方法签名不匹配，或调用失败时只报一次错误并跳过适配，访客照常生成；不修改 CE 文件或添加 Harmony 补丁 | 提供的 CE 16.7.4.0 声明支持 1.6，公开签名已核对；本机只有 1.7 游戏，不视为 CE 1.7 装填与战斗验收 |

兼容策略统一使用 `RHAH_Api` 的查询、闸门、行为策略、事件和 `TryMarkOrigin`。经历和特质用 `IsOwnedHistory`、`IsOwnedTrait`、`TryGetHistory`、`TryGetTrait`、`CopyHistoryIds`、`CopyTraitIds` 查询显示 ID 与 defName，不扫描 Backstory 或 TraitDef 前缀。兼容适配实现只能放在 `Source/Pawn/Compat`。需要补原版或种族框架缺口时，必须另行登记 Harmony 目标、版本范围和失败策略。

不兼容的旧鼠灾包由 Guard 提示并阻止主体加载；不自动停用旧包，也不迁移旧存档。
