# RimworldMods

RimWorld 模组、框架与开发工具的统一源码仓库。原独立仓库停止维护，后续代码、Issue 和发布统一在 [Nanaloveyuki/RimworldMods](https://github.com/Nanaloveyuki/RimworldMods) 进行。

## 项目

| 目录 | 内容 |
| --- | --- |
| [AdvancedRimTalk](AdvancedRimTalk/) | RimTalk 提示词、Arti 语言与编辑器扩展 |
| [IrisMenus](IrisMenus/) | 模组菜单、设置与渲染接口 |
| [Lead-Your-Pets](Lead-Your-Pets/) | 牵着你的宠物续作；原作者授权范围见项目 NOTICE |
| [MultiFloor-fork](MultiFloor-fork/) | MultiFloors 机制与兼容性调查，目前没有源码 fork |
| [ODF-Mods](ODF-Mods/) | Oxygen DSL 消费端模组，各自独立打包 |
| [Oxygen-Dsl-Framework](Oxygen-Dsl-Framework/) | Oxygen DSL 框架、适配器、编辑器和离线工具 |
| [RadiusUI-MissionControl-Fixes](RadiusUI-MissionControl-Fixes/) | RadiusUI Mission Control 修复 |
| [Ratkin-Great-Famine-Year-Continued](Ratkin-Great-Famine-Year-Continued/) | 旧鼠灾续作归档；不适用根目录双许可 |
| [Ratkin_Hunger_and_Havoc](Ratkin_Hunger_and_Havoc/) | 鼠族：饥与祸 |
| [RimCarbon14](RimCarbon14/) | 游戏运行时检查、调试与自动化桥接工具 |
| [RimIrisShaders](RimIrisShaders/) | RimIris 着色器与控制界面 |
| [rimworld-washingmachine-play](rimworld-washingmachine-play/) | 自动洗衣机模组 |

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

## 迁移与历史

迁移保留各项目原有已提交历史，并以合并提交接入本仓库；工作区中的未提交修改、暂存修改、未跟踪文件和本地忽略文件一并迁入。原仓库的 Git 元数据保存在本地 `.git/migration-backups/`，不属于源码，也不会上传。原分支与 tag 的本地备份引用位于 `refs/archive/<项目>/heads/` 和 `refs/archive/<项目>/tags/`。

后续开发只使用本仓库的根 `.git`。旧仓库的归档由维护者在 GitHub 上操作；本地迁移不自动发布、推送或更改远端设置。

### 迁移验证（2026-10-15）

- AdvancedRimTalk、IrisMenus 与 Oxygen 完整构建入口通过，均为 0 警告、0 错误；未执行游戏部署。
- 根目录 Actions 的 actionlint、RHAH 工具测试与结构检查通过；17 个字面量项目引用均能解析至迁移后的实际文件。
- Oxygen 离线 `RhahPolicy` 预览输出 `begging_policy Beg: allow`；ODF 囚犯代号临时发布包逐文件 SHA-256 校验通过，包内 LICENSE 包含 MIT 与 Apache-2.0 完整文本。
- Carbon 从任意工作目录能定位同级源码，但完整 doctor 未通过：仍检测 1.6 的 `RimWorldWin64.exe`，本机 1.7 使用 `RimWorld.exe`。未在本次仓库迁移中改动其游戏版本兼容性。
- 囚犯代号默认规则的离线预览暴露既有缺口：`pawn.has_interaction` 未由预览宿主提供。相关源码迁移前后 SHA-256 一致；不计为通过，也未为迁移顺带修改玩法或宿主 API。


## 许可证

有权重新授权的自有贡献采用 **MIT OR Apache-2.0** 双许可，使用者可任选其一。完整文本见 [LICENSE-MIT](LICENSE-MIT) 和 [LICENSE-APACHE](LICENSE-APACHE)，适用范围与例外见 [LICENSE](LICENSE)。

第三方源码、素材、依赖与 fork 保留原许可证、署名及 NOTICE；根目录双许可不覆盖它们。尤其 `Ratkin-Great-Famine-Year-Continued` 不获得新的使用授权，`Lead-Your-Pets` 的上游内容仍受原作者授权范围约束。分发单个项目时，应携带其 LICENSE、NOTICE 和适用的第三方声明。
