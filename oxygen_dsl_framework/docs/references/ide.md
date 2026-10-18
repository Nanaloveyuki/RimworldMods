# Oxygen 编辑器与图形编程

## 已实现的文本入口

`Tools/vscode` 是可独立打包的 VSCode 插件，提供 `.oxy`／Markdown 高亮、代码片段、当前文件诊断与声明导航。安装与构建见 [插件 README](../../Tools/vscode/README.md)。不额外维护独立 IDE：编辑、查找、文件管理、差异比较由 VSCode 提供，Oxygen 提供自己的语言能力。

`Source/Editor` 仅引用 netstandard2.0 的 Core，发布为 net9.0 运行时依赖 DLL。插件经 stdin/stdout 的逐行 JSON 请求它：

```json
{"id":1,"source":"fn add(a: int, b: int): int { return a + b }","isModule":false,"language":"zh-CN"}
```

响应含同一 `id`、`diagnostics` 和嵌套 `symbols`；诊断使用 `code/message/severity/start/length`，符号使用声明范围与 `selectionStart/selectionLength`。位置均为 UTF-16 偏移，与 VSCode 文本 API 一致。单次请求异常返回 `error`，进程继续接受下一行。协议不执行脚本、不读取模块和游戏文件，也不是 LSP。

编辑停顿 300ms 后检查，保存时立即检查，未保存文本直接发送给进程。响应仅用于相同文件版本，关闭文件时撤销诊断。Outline 和 Problems 共用相同版本的检查结果。命令可立即检查当前文件或重启检查器。

检查范围是当前 parser/analyzer 能判断的语法、作用域、模块导出位置与局部类型约束。导入模块只检查声明，不解析导出类型和依赖；Def 引用、内容字段和动态宿主行为继续通过已有 `check`／`preview` 或游戏验收。以后需要跨文件语义时，再把此进程升级为复用相同 Core 的 LSP，避免维护第二套语法或类型系统。

## 已实现的积木入口

Blockly 13.3.0 已集成在 VSCode 自定义编辑器的 Webview 内；不需要外部网站、独立 Web 服务或 CDN。命令 **Oxygen: New Block Program** 创建 `.oxyblocks`，打开该文件直接进入画布。内置饱食心情、原生特质与思想、IrisMenus 设置分页、囚犯动态命名和空白五个模板。囚犯模板生成 `fn codename(pawn): string?`，可修改种族分支、编号、等级和原名拼接。

| 积木类别 | 已映射的 Oxygen | 编辑方式 |
| --- | --- | --- |
| 值与集合 | 数值、文本、bool、null、引用、list、字段对象 | 类型连接口与列表／字段条目 |
| 条件 | 比较、`&&`／`||`／`!`、基本算术 | 嵌套表达式，生成时显式保留括号 |
| Pawn 查询 | 常用字段、成员与函数查询 | 字段选择与参数积木，不暴露任意 C# |
| 函数与流程 | `fn`、`if`／`else`、`return`、变量定义／赋值、调用 | 函数容器与顺序语句 |
| 模块 | `use`、`optional use`、别名 | 模块名与 packageId 字段 |
| 心情与规则 | `mood`、`memory`、`thought_rule`、`gate_rule` | 注册参数与条件函数引用 |
| 原生内容 | `template`、`define`、`patch`、`def_ref` | 字段对象与 Def 引用 |
| 玩家设置 | 可选 IrisMenus registry、分页、说明、值控件、按钮 | 页面容器与条目顺序 |

`struct`、`trait`、`impl`、复杂泛型和循环等未提供专用积木，继续使用文本入口。连接类型是编辑约束，不改变 Oxygen 的动态绑定或后置类型语义。空连接、未知积木、无效标识符、重复字段与脱离语句的表达式阻止导出，不生成假默认值掩盖错误。

### 存储与生成契约

`.oxyblocks` JSON 根结构为 `schemaVersion`、`generatorVersion`、`target`、`workspace`；当前两个版本均为 `1`。`workspace` 使用 Blockly 序列化数据，`target` 记录相对 `.oxy` 路径与 script/module 模式。块类型 ID 和字段 ID 与显示标题分开；未知版本或坏文件报错并保留原文件，可用 VSCode「重新打开编辑器方式 → 文本编辑器」修复。

积木修改通过版本校验提交至 VSCode 文本文档；`Ctrl+S` 保存 JSON，**不会自动覆盖生成源码**。画布按钮撤销／重做 Blockly 操作，VSCode 文档撤销／重做恢复已提交的 JSON。外部文本修改会重新载入画布，过期提交不覆盖新文档。

生成器由 Webview 和扩展宿主共用，根据结构化积木生成固定缩进与转义文本；连接链按顺序输出，独立顶层块按画布 y、x 和 ID 排序。同一输入得到相同源码。每块记录 UTF-16 源码范围；同一 C# parser/analyzer 检查后将错误映射到最小包含块，点击错误定位画布，选中块高亮对应的只读源码。

`.oxy` 才是游戏入口，不承诺任意源码反向还原积木。导出前检查生成结果，记录目标 URI 与 SHA-256；目标存在但不匹配上次生成摘要时要求明确覆盖或另选文件。未保存的目标修改、确认期间文件变化、过期文档版本都会拒绝写入。可以另存源码副本继续手写，不再同步回积木。

检查与导出要求工作区信任和扩展宿主上的 .NET 9+；未信任时仍可编辑、保存积木。检查只覆盖当前文件语法与静态语义，不执行玩法，不解析依赖和游戏 Def，也不替消费 Mod 提供宿主 API。IrisMenus 模板仍需消费端已有的设置 host。

### 界面与素材

左侧分类工具箱、中间可缩放画布、右侧选中块字段、连接、错误和源码预览。可拖拽，也可通过搜索添加、定位积木、字段表单、连接目标、断开连接、上下移动与删除完成键盘编辑。颜色之外保留名称、字段标签和错误文字；界面跟随 VSCode 主题，窄窗口纵向排列。

积木使用本地 Blockly SVG/CSS，不把关键控件做成位图；本轮未生成图片素材。验证记录见 [validation.md](validation.md#本机网页编辑器与囚犯玩家配置2026-10-10)。

## 本机网页编辑器

ODF 的可选 IrisMenus 适配层为有 Oxygen 目录或已注册文档的活动 Mod 注册编辑／退出操作，IrisMenus 本身不引用 ODF。编辑按钮先确认；服务由游戏程序集提供，浏览器资源完整打包于 `WebEditor/`，不是 VSCode Server，也不需要玩家安装开发运行时。

编辑资格来自 Mod 启用状态及 Oxygen 目录／注册文档，不再要求 Dependencies 声明 ODF；文件权限仍限定该 Mod 的活动加载目录。About 依赖用于游戏加载顺序与缺依赖提示，非官方依赖仍应提供 packageId、displayName 和有效 downloadUrl／steamWorkshopUrl，避免被游戏元数据校验移除。

1. 打开对应 Mod 的编辑入口，左栏选择物理文件或消费端提供的 `current/naming`、`current/events` 草稿。
2. 文本采用 CodeMirror；检查把未保存的文本交给同一 C# parser/analyzer，不执行脚本或验证游戏 Def。检查成功不能证明运行时正确。
3. 「保存」只写文件／存档草稿。「应用到本局」要求先保存；安装 `.oxy` 文件统一重载所属 Mod 的全部 Oxygen 文件，设置和运行规则失败保留上一有效计划，含原生声明的文件修改要求重启。消费端文档仍使用其 Apply 回调；存档草稿须再保存游戏才落盘。
4. 创建 `.oxyblocks` 可从囚犯模板起步；编辑积木、保存 JSON、检查生成源码是独立动作。导出可选择现有文档（包括 `current/naming`）或 `Oxygen/` 内的新 `.oxy` 路径；已有目标先确认并用确认前的 SHA-256 拒绝期间发生的外部修改。导出到当前命名草稿后再打开它并应用。
5. 显式 JSON 试算由消费端 `Preview(source, facts)` 提供。PrisonerNames 使用实际 `NamingRules` 和格式化器，仅使用输入事实及当前命名配置，不读取或修改真实 Pawn；未知事实报错，不伪造游戏行为。

文件版本冲突返回 409，保留本地编辑，玩家选择继续保留或重新载入。文档身份包含当前游戏；切换存档后旧文档拒绝保存／应用，物理文件仍在目录。浏览器关闭和主菜单不回收服务；IrisMenus「退出编辑器」或游戏退出释放监听器和排队请求，页面显示断连，不显示假成功。

服务器仅监听 `127.0.0.1` 随机端口；API 要求会话 token 和同源 Host／Origin，不提供 CORS、任意文件访问或远程执行。文件限定活动 Mod 加载目录下的 `.oxy`／`.oxyblocks`，拒绝路径逃逸、文件覆盖创建和重定向路径。源文本最多 1,048,576 个 UTF-16 code units，JSON 请求体最多 8 MiB，连接／排队均有上限。回调在 `Pump` 游戏主线程运行；作者应保持回调短小，退出不能中断已经开始的任意 C# 回调。

开发构建：`npm --prefix Tools/vscode ci && node Tools/web-editor/build.mjs`；部署脚本先打包本地 CodeMirror、Blockly、媒体及许可证。玩家发布目录必须携带完整 `WebEditor/`。

