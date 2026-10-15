# 自动化与低噪声调试

统一入口：`python3 scripts/rhah.py`，快捷入口：`make`。Python 只使用标准库；.NET SDK 由根目录 `global.json` 固定。Windows 游戏目录默认 `E:\Apps\Steam\steamapps\common\RimWorld`，WSL 使用 `/mnt/e/Apps/Steam/steamapps/common/RimWorld`。

## 常用命令

| 命令 | 行为 |
| --- | --- |
| `make doctor` | 查看工具链、游戏、ODF 与发布版本前置条件 |
| `make check` | 结构、Def、语言键、API 与存档归属静态门禁 |
| `make tools-test` | 离线运行自动化工具的行为测试 |
| `make build` | Release 构建 ODF、主体及其 API、Guard |
| `make test` | 构建并运行现有 net48 测试 |
| `make ci` | 工具测试、静态检查、完整构建和游戏契约测试 |
| `make package` | 完整 CI 通过后制作发布 zip 和 SHA-256 清单 |
| `make deploy` | 完整 CI 通过后安全同步到本机 Mods |
| `make report` | 最近一次流水线的状态、阶段和证据路径 |
| `make logs` | 增量查看 Player.log 中与本项目有关的内容 |
| `make carbon-status` | 独立测试进程、桥接与主线程状态 |
| `make carbon-tools` | 按需发现运行时工具，不展开全部 schema |
| `make carbon-events` | 增量事件、重复聚合及过滤计数 |
| `make carbon-smoke` | 对已经打开的测试地图运行只读项目场景 |
| `make mcp-config` | 生成项目适配器的 MCP 客户端配置 |

需要参数时直接调用 Python，例如：

```bash
python3 scripts/rhah.py ci --rimworld-dir /mnt/e/Apps/Steam/steamapps/common/RimWorld
python3 scripts/rhah.py test --tools-only
python3 scripts/rhah.py package --release-tag v1.1.0
python3 scripts/rhah.py logs --match 'HungerAndHavoc.*Exception|RHAH_' --limit 5
```

`build` 不是完整门禁；只有完整 CI 成功才允许部署或打包。没有绕过检查的发布参数。发布版要求 `About/About.xml` 的 `modVersion` 与主体项目 `Version` 一致，tag 必须是对应的 `v<版本>`；API 的独立契约版本、稳定 `AssemblyVersion` 不随内容版本强制改变。版本不一致时报告失败，不自动修改版本号。

环境覆盖：`RIMWORLD_DIR`（也接受 `RimWorldDir`）、`OXYGEN_DSL_DIR`、`CONFIGURATION`、`HARMONY_PATH`。ODF 默认使用同级 `Oxygen-Dsl-Framework` 仓库；完整 CI 先构建该仓库的 `Source/Mod/Oxygendsl.Mod.csproj`。不要将未经审核的外部 checkout 作为受保护 runner 的依赖。

## CI/CD

公共 GitHub Actions 在 push 和 PR 上执行 Actions schema 检查、离线工具测试与静态门禁，不接触游戏安装、不下载或上传游戏 DLL、不启动游戏。分支保护应要求公共检查通过。

完整流水线通过 `workflow_dispatch` 手动触发，运行在标有 `rhah` 的 Linux x64 self-hosted runner 上，使用受保护的 `rhah-local` environment。它只 checkout 当前仓库默认分支，不接受任意 ref，也不在 PR 或 `pull_request_target` 上执行。runner 的工具链版本与安装要求以 `global.json`、工作流配置和 `doctor` 报告为准；游戏程序集、Harmony、审核过的 ODF 及可选 IrisMenus 需在本机准备。

工作流文件为 `.github/workflows/static.yml` 和 `local.yml`。在 GitHub 仓库设置中登记 runner，为 `rhah-local` 和发布用的 `rhah-release` environment 设置审批人及允许的默认分支。`rhah-local` 变量使用 `RIMWORLD_DIR`、`OXYGEN_DSL_DIR`、`HARMONY_PATH`；运行时烟雾另设 `CARBON_PROFILE` 为已准备的独立 profile。源码中的工作流不会自动注册 runner 或创建远端审批规则。WSL runner 必须能够执行 `powershell.exe`，部署时用它检查 Windows 游戏进程

完整工作流可选择 `ci`、`package`、`deploy`、`release`。`runtime_smoke` 只允许与 `deploy` 一起启用，确保游戏观察的是本次部署；它启动独立 profile、创建新测试地图、执行只读场景，最后只停止该 profile 拥有的进程。已有测试游戏仍运行时拒绝接管。未启用时不声称通过运行时验收。`release` 要求既有 `v<版本>` tag 正好指向默认分支本次 checkout 的 HEAD，完整门禁成功后才经 `rhah-release` 发布 GitHub Release

发布与部署共用本地门禁。发布产物只包含 `About`、语言、Guard、`1.6`、Biotech、纹理、加载配置与许可证等白名单内容；三个本模组 DLL 必须存在。第三方程序集、PDB、源码、存档、发现令牌和原始测试日志不得入包或公共 artifact。私有游戏文件、登录凭据及玩家存档只留在本机，不上传 CI 或 Release。需要 Steam Workshop 发布时，仍通过游戏的 Workshop 上传流程，不在 CI 中保存 Steam 登录凭据。

语言发布内容包括 XML 和 `Languages/<语言>/Strings/**/*.txt` 姓名库；其它语言目录中的文本文件不入包。漏发 `Strings` 文本会导致姓名规则找不到词库，不能只同步语言 XML。

部署只写 `Mods/RatkinHungerAndHavoc` 对应的目标，拒绝游戏运行中、进程检查失败、链接目标、未知身份或其它模组身份。同步前再次检查进程，逐文件核对 SHA-256；清理范围限于本模组的白名单发布内容。不会自动退出游戏或写入旧鼠灾目录。原来的 `scripts/deploy.sh` 与 `scripts/build-and-deploy.ps1` 转发到同一实现，不维护第二套复制清单。

### 王国篇部署图片压缩

本机部署还要求 `ffmpeg` 在执行 Python 的环境 `PATH` 中可用，`make doctor` 会检查。WSL 安装 Linux 版（例如 `sudo apt install ffmpeg`）；使用 PowerShell 入口时安装 Windows 版并加入 Windows `PATH`。不随模组发布 FFmpeg，不自动下载工具。

PNG 的压缩等级不是 JPEG 式画质参数，单靠无损重编码不能保证指定体积。部署使用临时产物，必要时通过 Lanczos 等比例缩小来控制体积；这会损失细节，不承诺原分辨率。保留彩色 PNG，不采用调色板减色。小图不放大或填充到目标下限，源文件、其它纹理和 `package` 打包不变；具体目标与编码参数以 `scripts/rhah_pipeline.py` 为准。

所有图片处理成功后才再次检查游戏进程和目标身份，再同步并按实际压缩产物校验 SHA-256。缺少 FFmpeg 或图片处理失败时拒绝部署，不用未压缩原图静默回退；临时文件自动清理。部署阶段证据中的 `kingdom_textures` 提供原图与部署图总字节数。

参数依据：[FFmpeg PNG 编码器源码](https://github.com/FFmpeg/FFmpeg/blob/master/libavcodec/pngenc.c)（压缩等级 0–9、`pred=mixed`），[scale 滤镜文档](https://ffmpeg.org/ffmpeg-filters.html#scale)与 [Lanczos 缩放文档](https://ffmpeg.org/ffmpeg-scaler.html)。

## RimCarbon14

调用已安装的 `Mods/RimCarbon14/Tools/win-x64/RimCarbon14.Agent.exe`，不为 RHAH 增加运行时依赖。

工具发现通过认证的本地接口进行。WSL 使用 `scripts/carbon/read-tools.ps1` 在 Windows 本地请求 `127.0.0.1`，不改用宿主 IP 或外网地址。Discovery 的协议、端口和认证必须通过校验，禁止代理与重定向；token 由 helper 本地读取，不进入命令行。调用、场景和进程控制交给真实 Agent。

默认测试 profile：`E:\RHAHAutomation\RatkinHungerAndHavoc\profile`，WSL 对应 `/mnt/e/RHAHAutomation/RatkinHungerAndHavoc/profile`。这是独立存档目录，不是 `.rws` 文件。`prepare` 不覆盖已有配置，不修改玩家日常 profile；进程控制只允许适配器标记的独立 profile，Agent 再按 PID、启动时间和 executable 校验所有权。

第一次准备与实测：

```bash
python3 scripts/rhah.py carbon doctor
python3 scripts/rhah.py carbon prepare --minimal
python3 scripts/rhah.py carbon start
python3 scripts/rhah.py carbon run scripts/carbon/rhah-new-test.json
python3 scripts/rhah.py carbon run
python3 scripts/rhah.py carbon stop
```

`rhah-new-test.json` 显式创建测试殖民地并捕图；默认 `rhah-smoke.json` 只读检查已有地图、本模组及其 API/依赖加载。两者不是所有事件、王国篇、卸载或第三方兼容性的完整验收。改玩法时另写有结果断言的实际场景，不能以 `accepted`、方法没抛异常或截图文件非空代替玩法结果。

进程启动与配置复制属于显式操作，不随查询自动执行。游戏需要真实渲染窗口才能捕图；不要在玩家用鼠标时执行 desktop input。存档和实例切换后重新获取对象 handle。

```bash
python3 scripts/rhah.py carbon tools --query objects
python3 scripts/rhah.py carbon tools --name objects_types
python3 scripts/rhah.py carbon call game_state --select result.data.status
python3 scripts/rhah.py carbon events --all --limit 10
python3 scripts/rhah.py carbon operation <operation-id> --instance <original-instance-id>
python3 scripts/rhah.py carbon config
```

工具必须先查真实 schema，再提供精确参数。长调用显式设置超时；写调用失败或 acknowledgement 丢失后查 operation，不自动重放。事件 cursor 按实例隔离；`lost` 或截断计数意味着观察不完整，不能据此声称没有错误。

适配器接受 `CARBON_AGENT`、`CARBON_PROFILE`、`CARBON_SOURCE_DIR`，以及命令行 `--agent`、`--profile`、`--game-root`、`--evidence-dir`。WSL 自动转换传给 Windows companion 的 profile、游戏、参数文件与场景路径。`carbon config` 输出标准 MCP 配置；客户端只接入这个精简适配器即可，避免再同时加载上游全部工具定义。

## 输出与证据

默认只输出阶段摘要、有限错误片段、操作状态和证据路径；结果省略部分带计数。需要某个值时用 `--select` 精确取 JSON 路径，需要工具参数时只查一个 schema。截图和完整结果保存在文件，不向文本摘要塞 base64。

流水线原始输出与 `run.json` 在 `tmp/automation/`；`latest.json` 指向最近的报告，不抹掉较早失败。Carbon 的原始响应和场景证据也留在本地，token 做脱敏。不要提交或批量打开这些目录，不将原始日志上传公共 CI artifact。

Player.log 首次只扫描近期片段，此后按 cursor 读取新内容；日志轮转或覆盖会重置 cursor。摘要记录未扫描历史与剩余字节，聚合重复堆栈，默认只保留本项目及 ODF 相关诊断。需追查其它内容时使用 `--all` 或 `--match`。过滤后的证据仍可能包含存档内容，按本地敏感数据处理。

按任务需要使用自动化，不为了完善报告追加不必要的 smoke 或测试。需要验证时先看 `doctor` 或上次 `report`，只运行受影响门禁一次；失败后按报告里的具体文件和行范围读证据。不要为了找一句错误反复展开整个 Player.log、所有运行时类型、所有 tool schema 或全部 trace。自动化摘要不等于完整验收证明，最终交付只报告实际执行的检查与场景。

