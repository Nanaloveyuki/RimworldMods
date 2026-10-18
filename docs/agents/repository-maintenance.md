# 仓库维护入口

本文说明 monorepo 的文档分类、统一部署入口和本次整理后仍需补齐的发布资料。工程约束以根 [AGENTS.md](../../AGENTS.md) 为准；模块行为以源码和各自文档为准。路径或发布状态变化时可继续更新本页。

## 文档导航

- 根 [References.md](../References.md)：用户提供的本机参考目录，含 WSL 链接及 Windows 对照。
- `docs/adr/`：设计决策及历史取舍；模块已有 ADR 继续保留在本模块。
- `docs/agents/`：协作约定、问题跟踪及必要操作说明。
- `docs/references/`：引用、接口手册、设计资料及历史证据。Arti 与 Oxygen 手册整体迁入此类别，内部目录和相对链接保持一致。
- 临时共享文档需明确删除条件。现存用户 `tmp/`、王国篇作者 `tmp.md` 原稿未删除；原稿归在王国篇参考目录，不作为自动清理目标。Oxygen 根 `.tmp.md` 已归为 `docs/references/architecture-notes.md`，保留原文并注明历史性质。
- MultiFloors 项目只有调查报告，没有源码 fork 或可部署 Mod。

## 统一部署

以下命令从 monorepo 根目录运行：

```bash
bash scripts/deploy.sh --list
bash scripts/deploy.sh <module> --build-only
bash scripts/deploy.sh <module> --rimworld-dir /path/to/RimWorld
bash scripts/deploy.sh odf_mods --mod PrisonerNames --build-only
```

Windows PowerShell 使用 `python scripts/python/deploy.py` 及相同参数。共享实现位于 `scripts/python/`，模块差异配置在 `scripts/deploy/<module>.json`；不再维护模块部署脚本入口。

- `--build-only`：构建并正式暂存，不检查游戏进程、不写入游戏 `Mods`。
- `--skip-build`：使用已有构建输出重新暂存；默认构建配置为 Release，可用 `--configuration Debug` 覆盖。
- `--rimworld-dir` 指定游戏安装，`--target` 指定 Mod 目标；重复的 `--property NAME=VALUE` 传入 MSBuild 属性。`--version` 与具体模块支持的游戏版本保持一致，不能把版本参数当作完成适配。
- 正式暂存位于根 `artifacts/<module>/release`；ODF 消费端按 Mod 放在 `artifacts/odf_mods/<Mod>/release`。RimCarbon14 专用工具包位于 `artifacts/rim_carbon14/package`，zip 也在该模块产物目录中。
- Carbon 可复用 PowerShell 工具在根 `scripts/carbon/`；专用构建、fixture 与 smoke 仍在各模块。RHAH 专用 CI、打包和发布门禁继续由模块流水线维护。

部署必须使用模块支持的真实游戏安装，并遵守当前脚本的进程、路径、目标身份和内容清单要求。本次已验证统一 CLI 的 `--help`、`--list`、`--preflight`，并完成 `keep_your_identity`、`advanced_rimtalk` 的 Release build-only 及 `odf_mods/PrisonerNames` 的跨项目构建与正式暂存；未运行完整测试套件、格式化、游戏内验收或实际写入游戏 Mods。

## 发布资料状态

14 个正式 Mod 包均已有 `About/Preview.png`。本轮为 `oh_you_are_so_fast`、`radius_ui_mission_control_fixes`、`rim_carbon14`、`rim_iris_shaders` 补齐 960×540 黑底白字中英双标题图，既有预览图保持不变。Oxygen 示例与 Carbon Fixture 属于示例／测试，不混作正式发布项目。

Oxygen DSL Framework 的用户确认工坊链接为 <https://steamcommunity.com/sharedfiles/filedetails/?id=3817603245>，已写入本体 `url` 和四处依赖声明。RhahPolicy 示例的饥与祸依赖链接使用仓库现有 `PublishedFileId.txt`（3807233439），不修改发布 ID。

其他依赖工坊链接依据本机已安装 Mod 的 `packageId` 与 `About/PublishedFileId.txt` 补齐：RimTalk（3551203752）、RadiusUI Framework（3786107692）、RadiusUI Mission Control（3788256848）、RimIris Beta（3796735623）、IrisMenus（3798502474）。AdvancedRimTalk 的本体 URL 曾误指向 IrisMenus，现按本机发布 ID 修正为 3801974557。全部非 DLC 依赖声明均已有 `steamWorkshopUrl`；未进行网页搜索，未改动任何发布 ID 文件。

本轮已打开四张新图检查排版，验证 17 份 About XML 可解析及全部非 DLC 依赖链接完整；七个相关发布配置使用真实暂存逻辑完成临时打包，预览图和元数据 SHA-256 与源文件一致，临时目录已删除。RHAH 源码契约检查通过；未写入游戏 Mods、未上传工坊或进行游戏内验收。

## 链接边界

本机 WSL 链接只能在具有对应挂载的环境访问，GitHub 网页不能访问本机磁盘。旧验收记录中的进程、截图、临时回执和已经归档的 bug-card 路径属于历史证据，不保证仍可打开；现行缺陷跟踪转到各模块的 GitHub Issues 入口。文档迁移没有重跑历史验收，也没有把旧证据改写为新路径下的新验收。
