# RimworldMods

RimWorld 模组、框架与开发工具的统一源码仓库。原独立仓库停止维护，后续代码、Issue 和发布统一在 [Nanaloveyuki/RimworldMods](https://github.com/Nanaloveyuki/RimworldMods) 进行。

## 项目

| 目录 | 内容 |
| --- | --- |
| [AdvancedRimTalk](AdvancedRimTalk/) | RimTalk 提示词、Arti 语言与编辑器扩展 |
| [IrisMenus](IrisMenus/) | 模组菜单、设置与渲染接口 |
| [KeepYourIdentity](KeepYourIdentity/) | 仅 1.7：囚犯、奴隶新生儿身份继承与限时身份选择 |
| [Lead-Your-Pets](Lead-Your-Pets/) | 牵着你的宠物续作；原作者授权范围见项目 NOTICE |
| [MultiFloor-fork](MultiFloor-fork/) | MultiFloors 机制与兼容性调查，目前没有源码 fork |
| [ODF-Mods](ODF-Mods/) | Oxygen DSL 消费端模组，各自独立打包 |
| [OhYouAreSoFast](OhYouAreSoFast/) | 寻路与 AI 优化，保留 1.6 发布内容并适配 1.7 |
| [Oxygen-Dsl-Framework](Oxygen-Dsl-Framework/) | Oxygen DSL 框架、适配器、编辑器和离线工具 |
| [RadiusUI-MissionControl-Fixes](RadiusUI-MissionControl-Fixes/) | RadiusUI Mission Control 修复 |
| [Ratkin-Great-Famine-Year-Continued](Ratkin-Great-Famine-Year-Continued/) | 旧鼠灾续作归档；不适用根目录双许可 |
| [Ratkin_Hunger_and_Havoc](Ratkin_Hunger_and_Havoc/) | 鼠族：饥与祸 |
| [RimCarbon14](RimCarbon14/) | 游戏运行时检查、调试与自动化桥接工具 |
| [RimIrisShaders](RimIrisShaders/) | RimIris 着色器与控制界面 |
| [rimworld-washingmachine-play](rimworld-washingmachine-play/) | 自动洗衣机模组 |
| [SaveYourStorage-and-Tick-Every-Minute](SaveYourStorage-and-Tick-Every-Minute/) | 仓储与低频 Tick 调度优化，保留 1.6 发布内容并适配 1.7 |

`Digital-Storage-fork` 不在迁移范围内，本地副本已按要求删除；其源码与提交历史不纳入本仓库。

## 开发与发布

```sh
git clone https://github.com/Nanaloveyuki/RimworldMods.git
cd RimworldMods

# 从 monorepo 根目录仅构建，不部署到游戏
bash IrisMenus/scripts/deploy.sh --build-only
bash AdvancedRimTalk/scripts/deploy.sh --build-only
bash Oxygen-Dsl-Framework/scripts/deploy.sh --build-only

# 各项目仍保留自己的命令入口
cd Ratkin_Hunger_and_Havoc
python3 scripts/rhah.py --help
```

- 所有项目都是普通子目录，不使用 submodule，也不需要再克隆旧仓库。项目保持同级布局，ODF、IrisMenus、RHAH 等现有相对引用继续有效。
- 项目 README 中的相对命令默认在对应项目目录执行；根目录调用脚本时使用上例的目录前缀。游戏程序集、SDK、Harmony 与外部依赖要求仍以各项目说明为准。
- 各项目保留自己的目标框架、游戏版本和构建／部署脚本；归并仓库不代表所有旧项目已迁移至 RimWorld 1.7。不要使用一套全局 MSBuild 配置覆盖版本差异。
- 发布包仍以单个 Mod 为单位，保留原 packageId、加载目录及依赖关系；不要将整个仓库直接放进游戏 Mods 目录。
- GitHub Actions 统一位于根目录 [`.github/workflows/`](.github/workflows/)。RHAH 的受保护本机构建与发布需要在新仓库配置 runner 和 environment；参见 [自动化说明](Ratkin_Hunger_and_Havoc/docs/automation.md)。
- 新 Issue 标题注明项目目录；新 tag 使用 `<项目>/v<版本>` 避免跨项目重名。现有 RHAH 受保护发布入口仍接受 `v<版本>`，并要求该 tag 指向审查后的 monorepo 提交。

### KeepYourIdentity（仅 RimWorld 1.7）

显示名称为「保持你的身份 KeepYourIdentity」，packageId 为 `nanaloveyuki.keepyouridentity`。需要 Harmony、Biotech 和 Ideology。

殖民地囚犯、奴隶分娩后，活产婴儿立即继承分娩母亲当时的身份，并收到一封可选择殖民者、囚犯或奴隶身份的信。选择期限为出生后 12 个游戏小时（30,000 ticks）；暂停游戏不计时，存读档不会重置期限，超时自动确认出生时记录的默认身份。母亲随后被招募、释放、奴役或死亡不会改变默认身份。殖民者母亲、培养舱、死产和非殖民地管理的生育保持原版逻辑；幼儿成长后的原版身份事件仍然保留。

多胎兼容：在一次 `ApplyBirthOutcome` 调用及其后置补丁期间收集 `PawnGenerator.GeneratePawn` 生成的新生儿，在所有后置补丁结束后统一继承身份并逐婴儿发信，首胎去重。覆盖 RHAH「待产流民」的额外婴儿；不会回溯处理修复前已经出生的孩子。

从 monorepo 根目录构建：

```sh
dotnet build KeepYourIdentity/Source/KeepYourIdentity.csproj -c Release
```

默认引用本机 `E:/Apps/Steam/steamapps/common/RimWorld` 和 Workshop Harmony 的 `1.7/Assemblies/0Harmony.dll`（WSL 使用 `/mnt/e/`）。其他安装位置通过 MSBuild 的 `RimWorldDir`、`HarmonyAssemblyPath` 属性指定。分发内容为 `KeepYourIdentity/About/`、`LoadFolders.xml`、`LICENSE` 和 `1.7/`；编译后的 DLL 位于 `1.7/Assemblies/KeepYourIdentity.dll`。

验证（2026-10-16）：Release 构建 0 警告、0 错误；独立宿主加载实际 1.7 游戏程序集并成功安装 Harmony 补丁，验证了母亲身份快照、殖民者与培养舱旁路、三种选择、实际身份切换、重复动作保护、30,000 ticks 超时边界及信件存读档。未运行完整游戏中的分娩流程和信件界面。

多胎回归：`dotnet run --project KeepYourIdentity/Tests/BirthHarness/BirthHarness.csproj -c Release`。宿主加载实际游戏/Harmony 程序集，在引擎生成边界注入婴儿，重放 RHAH 首胎返回、后置补丁追加多胎的调用顺序。旧版复现 1/12；修复后囚犯 12/12、5/5，奴隶 5/5，殖民者不干预；验证了前后两种后置补丁优先级、逐婴儿单封信、单胎和生育外生成隔离。未运行完整 RHAH 事件及游戏界面。


## 迁移与历史

迁移保留各项目原有已提交历史，并以合并提交接入本仓库；工作区中的未提交修改、暂存修改、未跟踪文件和本地忽略文件一并迁入。原仓库的 Git 元数据保存在本地 `.git/migration-backups/`，不属于源码，也不会上传。原分支与 tag 的本地备份引用位于 `refs/archive/<项目>/heads/` 和 `refs/archive/<项目>/tags/`。

后续开发只使用本仓库的根 `.git`。旧仓库的归档由维护者在 GitHub 上操作；本地迁移不自动发布、推送或更改远端设置。

`SaveYourStorage-and-Tick-Every-Minute` 与 `OhYouAreSoFast` 也已从原 GitHub 仓库迁入，保留完整提交历史及 `refs/archive/<项目>/heads/` 备份引用；原仓库归档后，源码与 Issue 统一在本仓库维护。这两个项目保留原 MIT 许可。其 `1.6/Assemblies/` 保全已部署二进制，1.7 构建与部署使用独立的 `1.7/` 目录，不重编译或覆盖 1.6。

两个项目的 1.7 Release 构建及独立游戏 CLR/Harmony 烟雾验证通过，已部署至现有游戏 Mods 目录。部署文件逐项 SHA-256 校验一致，原 1.6 的 7 个文件及两个 PublishedFileId 均未变化；版本加载清单各只选择对应版本的一个程序集。完整存档、世界路径及性能压力回归未执行，具体范围见各项目验证说明。

### 迁移验证（2026-10-15）

- AdvancedRimTalk、IrisMenus 与 Oxygen 完整构建入口通过，均为 0 警告、0 错误；未执行游戏部署。
- 根目录 Actions 的 actionlint、RHAH 工具测试与结构检查通过；17 个字面量项目引用均能解析至迁移后的实际文件。
- Oxygen 离线 `RhahPolicy` 预览输出 `begging_policy Beg: allow`；ODF 囚犯代号临时发布包逐文件 SHA-256 校验通过，包内 LICENSE 包含 MIT 与 Apache-2.0 完整文本。
- Carbon 从任意工作目录能定位同级源码，但完整 doctor 未通过：仍检测 1.6 的 `RimWorldWin64.exe`，本机 1.7 使用 `RimWorld.exe`。未在本次仓库迁移中改动其游戏版本兼容性。
- 囚犯代号默认规则的离线预览暴露既有缺口：`pawn.has_interaction` 未由预览宿主提供。相关源码迁移前后 SHA-256 一致；不计为通过，也未为迁移顺带修改玩法或宿主 API。


## 许可证

有权重新授权的自有贡献采用 **MIT OR Apache-2.0** 双许可，使用者可任选其一。完整文本见 [LICENSE-MIT](LICENSE-MIT) 和 [LICENSE-APACHE](LICENSE-APACHE)，适用范围与例外见 [LICENSE](LICENSE)。

第三方源码、素材、依赖与 fork 保留原许可证、署名及 NOTICE；根目录双许可不覆盖它们。尤其 `Ratkin-Great-Famine-Year-Continued` 不获得新的使用授权，`Lead-Your-Pets` 的上游内容仍受原作者授权范围约束。分发单个项目时，应携带其 LICENSE、NOTICE 和适用的第三方声明。
