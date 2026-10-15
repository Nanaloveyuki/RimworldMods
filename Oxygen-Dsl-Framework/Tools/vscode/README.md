# Oxygen Language Support

RimWorld Oxygen DSL 的 VSCode 插件。识别 `.oxy`，提供：

- 当前 Oxygen 语法高亮，包括后置类型、`struct`、`trait`、`impl`、泛型函数、模块与插值字符串。
- Markdown 中 `oxygen`／`oxy` 围栏高亮。
- 函数、组合、内容定义、思想规则、IrisMenus 设置页等代码片段。
- 当前文件的语法／静态语义诊断，显示在 Problems 和编辑器中；支持未保存修改。
- Outline、面包屑与当前文件符号导航，包括字段、类型方法与嵌套函数。
- 内嵌离线 Blockly 积木编辑器，独立 `.oxyblocks` 文档、源码预览、检查与 `.oxy` 导出。

诊断进程复用 Oxygendsl 的 C# parser/analyzer，不执行源代码，不读取导入模块，不依赖游戏程序集。它不是 LSP，不提供补全、跨文件跳转、重命名或游戏 Def 校验。当前 analyzer 不能静态证明所有动态成员、类型和宿主行为；检查成功仍需内容 CLI 或游戏验收。

## 安装

在 VSCode 的 Extensions 菜单选择 **Install from VSIX…**，安装 `oxygen-language-support-0.2.0.vsix`。也可以执行：

```sh
code --install-extension /path/to/oxygen-language-support-0.2.0.vsix
```

诊断与 Outline 要求插件所在机器安装 **.NET 9 或更新版本的运行时**。WSL、SSH 和容器窗口在远程插件宿主上运行，需要在对应环境安装 .NET。仅使用高亮／片段时无需 .NET。设置 `oxygen.dotnetPath` 可指定 `dotnet` 可执行文件，参数由插件直接传递，不经 shell。

## 使用

打开 `.oxy` 后自动检查，编辑停顿 300ms 或保存时更新。输入 `fn`、`struct`、`trait`、`mood`、`define TraitDef`、`irismenus` 或 `lang` 使用片段。`Ctrl+Shift+O`／`Cmd+Shift+O` 显示当前文件声明。`lang` 是运行时翻译函数，检查器只做静态分析；执行缺少翻译宿主时才报错，不在 IDE 内猜玩家语言或补造翻译。

命令面板提供：

- **Oxygen: Check Current Document**：立即检查未保存文本，即使自动诊断关闭。
- **Oxygen: Restart Analyzer**：重启进程并重新检查打开的文档。
- **Oxygen: Show Analyzer Output**：查看进程错误，例如找不到运行时。
- **Oxygen: New Block Program**：选择模板并创建 `.oxyblocks`，自动打开积木编辑器。

`oxygen.analysis.mode` 默认 `auto`：根据语法区分纯函数／类型／绑定库和可执行脚本，不按路径或文件名识别。`config.oxy` 可以声明设置、原生内容或规则；纯库中的 `pub` 仍按模块语义检查。需要强制检查模式时可在文件夹设置中覆盖为 `module`／`script`。

`oxygen.diagnostics.language` 默认跟随 VSCode 界面，也可选 `en` 或 `zh-CN`。关闭 `oxygen.diagnostics.enabled` 只关闭自动诊断，Outline 仍可请求检查器。未受信任的工作区保留高亮与片段，不启动检查进程；授予工作区信任后自动检查已打开的 Oxygen 文件。文本不会发送到网络。

### 积木编辑

1. 执行 **Oxygen: New Block Program**，选择饱食心情、原生特质与思想、IrisMenus 设置分页、囚犯动态命名或空白模板，保存 `.oxyblocks`。囚犯模板生成可被 PrisonerNames 实际执行的 `codename`；不从已有源码反解画布。
2. 从左侧工具箱拖拽；或搜索积木后点击「添加积木」。右侧「定位画布积木」可直接选择已有块，用字段表单和「键盘连接」编辑，无需精确拖拽。
3. 「撤销／重做」处理画布操作；`Ctrl+S` 保存积木 JSON。保存不自动生成或覆盖 `.oxy`。
4. 填写相对于积木文件的输出路径，例如 `Oxygen/rules.oxy`，选择入口脚本或库模块，点击「检查积木」。错误文字可定位对应块。
5. 「导出 .oxy」先检查再写入；「另存源码副本」允许改为独立手写。已有目标被手工修改时要求明确覆盖，未保存目标或确认期间发生修改则拒绝写入。

工具箱覆盖条件、Pawn 查询、函数与分支、集合／对象、模块、心情／思想／RHAH 规则、原生内容和 IrisMenus 设置页。复杂 `struct`／`trait`／`impl`／泛型等继续手写；不支持任意 `.oxy` 反向还原。生成内容仍需对应宿主接入，设置模板不替消费 Mod 创建 C# 设置 host。

Blockly 与资源随 VSIX 打包，不访问 CDN，也不需要另开网站。积木编辑和保存不要求 .NET 或工作区信任；检查／导出需要信任及 .NET 9+。当前文件检查不替代内容 CLI 或游戏实测。坏 JSON 或不支持的版本不会自动覆盖，可通过「重新打开编辑器方式 → 文本编辑器」修复。

## 构建与打包

仓库开发环境需要 Node.js 22+ 与 .NET SDK 9+：

```sh
cd Tools/vscode
npm ci
npm run build
npm run package:vsix
```

VSIX 输出到仓库 `tmp/vscode/`。打包会自动重新发布检查器；发布的是跨平台、依赖运行时的 DLL，携带 Core 与许可文件，不携带游戏／Unity 程序集。开发打包脚本和 `node_modules` 不进入 VSIX。

高亮／发布结构参考 AdvancedRimTalk 的 Arti 插件，按 Oxygen 当前语法重新适配；许可证见 `LICENSE`。检查器及语言核心许可见 `server/LICENSE` 与 `server/THIRD_PARTY_NOTICES.md`。Blockly 使用 Apache-2.0，许可随包附于 `media/Blockly-LICENSE`。图形编程契约见仓库 `docs/ide.md`。
