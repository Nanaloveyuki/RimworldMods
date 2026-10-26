# Markdown 渲染

IrisMenus 提供只读 Markdown 内容对象。它们把 Markdown 转换为 RimWorld/Unity IMGUI 使用的富文本，并提供测量、绘制、文档链接和行定位；解析器不依赖 HTML，也不会创建 DOM。

## 快速接入

引用 `IrisMenus.dll` 后使用以下公开类型：

```csharp
using IrisMenus;
using System.IO;
using Verse;

public sealed class ExampleMod : Mod
{
    private readonly IMarkdown liveText;
    private readonly IMarkdown readmeText;

    public ExampleMod(ModContentPack content) : base(content)
    {
        liveText = Markdown.Dynamic(() =>
            "**即时状态**\n\n当前值: " + GetCurrentValue());
        readmeText = Markdown.StaticFile(
            Path.Combine(content.RootDir, "docs", "readme.md"));

        MenuRegistry.RegisterSubItemListing(this, "help", () => "帮助", DrawHelp);
    }

    private void DrawHelp(Listing_Standard listing)
    {
        MenuControls.Markdown(listing, liveText);
        MenuControls.Markdown(listing, readmeText);
    }

    private string GetCurrentValue() => "由 Mod 自己的状态计算";
}
```

`MenuControls.Markdown` 会先调用 `Measure`，再取得列表区域并调用 `Draw`。自定义页面也可以直接注册 `IMarkdown.Draw`：

```csharp
var document = Markdown.Document(() => settings.Description);
MenuRegistry.RegisterSubItem(this, "description", () => "说明", document.Draw);
```

`Markdown.Document(Func<string>)` 创建可复用的 `MarkdownDocument`。对象会缓存上一次的源字符串、解析结果和按宽度计算的行位置；源字符串不变时，`Measure` 和 `Draw` 不会再次解析。源回调应只读取主线程状态，不应在每帧执行文件 I/O。静态文本也可使用 `Markdown.Document("...")`。

## 动态内容与静态文件

`Markdown.Dynamic(Func<string>)` 每次访问时取得当前输入，只有字符串变化时才重新解析。若动态内容可能改变高度，把 `() => document.MarkdownText` 作为 `MenuSection` 的 `layoutKey`，让页面在内容变化时自动重新测量。

`Markdown.StaticFile(path)` 在构造时以 UTF-8 读取并解析一次，不监听文件，也不会写回文件。文件变化后由消费方重新创建对象；不要在每帧重建，否则会重复文件 I/O。

## 支持的语法

内置解析器支持设置页面和帮助文本常用子集：

- `# 标题` 到 `###### 标题`：转换为加粗文本。
- `- 项目`、`* 项目`、`+ 项目`：无序列表。
- `1. 项目`：有序列表并保留编号。
- `> 引用`：保留引用前缀。
- `**加粗**`：转换为 Unity 的 `<b>...</b>`。
- `*斜体*`、`_斜体_`：转换为 Unity 的 `<i>...</i>`。
- `[文字](package://...)`：注册后可点击的内部文档链接。
- 反斜杠可以转义 Markdown 分隔符，例如 `\**文本**` 会显示为 `**文本**`。
- 三个或更多反引号或波浪号包围的代码围栏会逐行保留字面内容；围栏中的 `**`、链接和富文本标签不会被解析。
- 输入换行会保留；CRLF 会统一为 LF。
- `MarkdownDocument` 的原生表面支持常见管道表格；普通解析器会保留表格文本。

原文中的 HTML 不会被解析成 Markdown 或 HTML DOM；不在受支持富文本标签白名单内的类 XML 标签会被移除，使 `Measure` 与 `Draw` 保持一致。

## `<color>` 兼容

RimWorld 原版 `ColoredText.Colorize(...)` 生成的 `<color=#RRGGBB>...</color>` 或 `<color=#RRGGBBAA>...</color>` 会先校验格式，合法标签才作为富文本标签保留：

```text
<color=#ff0000ff>**危险**</color>
```

结果相当于：

```text
<color=#ff0000ff><b>危险</b></color>
```

引号、颜色名称、错误位数或非十六进制字符会使整个 `<color>` 标签按原文处理；解析器只保留合法的 RimWorld `<color>`、`<b>` 和 `<i>` 标签。

## `**` 分隔符边界

加粗分隔符遵循项目既有边界约定：开始的 `**` 前面必须是文本/字母数字、空白或内容开头。若前面紧邻逗号等其他符号，则不把它当作加粗标记：

```text
**加粗**        -> <b>加粗</b>
文字 **加粗**   -> 文字 <b>加粗</b>
文字,**加粗**   -> 文字,**加粗**
```

不要为了强制解析标记而删除前面的标点；需要加粗时，用空格将标点和 `**` 分开。

## 文档链接与 URI

文档链接不是外部网页链接。目标 URI 必须通过 `DocumentUri.TryParse` 验证，并且目标 Mod 必须已启用、拥有 IrisMenus 页面、且已注册对应文档或 SubItem。解析失败、目标未启用、目标未注册或行号不存在时，链接不会打开。

URI 格式如下：

```text
package://SubItem/page
package://DocumentItem/parent[:line]
package://DocumentItem/parent/child[:line]
```

`package` 是 Mod 的 package ID；`SubItem` URI 使用 `PageId`。`DocumentItem` URI 使用父文档 `PageId`，可选 `ChildId`，最后可选一个从 1 开始的源文档行号。行号用于打开文档后定位，不是字符偏移。URI 不包含 `im://` 前缀或 `#anchor` 锚点。

Mod 通过注册接口建立显式 allow-list：

```csharp
var parent = Markdown.Document(() => "# 总览\n\n参见 [高级设置](package://DocumentItem/help/advanced:12)。");
var advanced = Markdown.Document(() => "# 高级设置\n\n内容");
MenuRegistry.RegisterDocument(this, new DocumentItem("help", () => "帮助", parent));
MenuRegistry.RegisterDocument(this, new DocumentItem("advanced", () => "高级设置", advanced), "help");
```

`MenuRegistry.RegisterDocument(owner, item, parentId)` 为文档创建菜单页面；父文档必须先注册，`parentId` 表示父 `DocumentItem.Id`。`MenuRegistry.OpenUri(uri)` 负责验证 package、页面、文档层级和行号，然后导航到目标页面。`MarkdownDocument.LineOffset`/`TryGetLinePosition` 为宿主提供同一文档的 1-based 行定位。

`IInteractiveMarkdown` 返回已经通过 URI 验证的 `Links`，并提供 `Draw(Rect, Action<DocumentUri>)`。原生 IrisMenus 页面使用 `UiDocument` 绘制带样式的文本和可点击的包裹链接；浅色和深色主题都使用当前 `UiPalette` 的可读 ink/accent 颜色。

## 低层接口

```csharp
public interface IMarkdown
{
    string MarkdownText { get; }
    string RenderedText { get; }
    float Measure(float width);
    void Draw(Rect rect);
}
```

`RenderedText` 是面向 RimWorld `Widgets.Label` 的富文本字符串，不是 HTML。若只需要转换结果，可以调用 `Markdown.Parse(source)` 或 `new MarkdownParser().Parse(source)`；需要绘制和自动测量时，应长期持有 `IMarkdown` 对象。
