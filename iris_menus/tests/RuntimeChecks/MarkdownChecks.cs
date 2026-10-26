using System;
using System.IO;
using IrisMenus;
using UnityEngine;
using Verse;

internal static class MarkdownChecks
{
    public static void Run(Action<bool, string> check)
    {
        var parser = new MarkdownParser();
        check(parser.Parse("**bold**") == "<b>bold</b>", "Bold Markdown becomes Unity rich text");
        check(parser.Parse("text **bold**") == "text <b>bold</b>", "Bold after whitespace is parsed");
        check(parser.Parse("text,**bold**") == "text,**bold**", "Punctuation before bold keeps original Markdown");
        check(parser.Parse("text, **bold**") == "text, <b>bold</b>", "Whitespace separates punctuation from bold");
        check(parser.Parse("<color=#ff0000ff>**red**</color>") ==
            "<color=#ff0000ff><b>red</b></color>", "RimWorld color tags remain intact around Markdown");
        check(parser.Parse("<color=#ffffff>white</color>") ==
            "<color=#ffffff>white</color>", "Six-digit vanilla color tags remain intact");
        check(parser.Parse("<color=\"#ffffff\">**white**</color>") ==
            "<color=\"#ffffff\"><b>white</b></color>", "Invalid quoted color tags remain literal");
        check(parser.Parse("<color=red>**red**</color>") ==
            "<color=red><b>red</b></color>", "Invalid named color tags remain literal");
        check(parser.Parse("<color=#fff>**short**</color>") ==
            "<color=#fff><b>short</b></color>", "Invalid short color tags remain literal");
        check(parser.Parse("**<color=#ff0000ff>red</color>**") ==
            "<b><color=#ff0000ff>red</color></b>", "RimWorld color tags remain intact inside Markdown");
        check(parser.Parse("<color=#ff0000ff># Heading</color>") ==
            "<b><color=#ff0000ff>Heading</color></b>", "Color-wrapped headings keep valid rich-text nesting");
        check(parser.Parse("<color=#ff0000ff>- **item**</color>") ==
            "<color=#ff0000ff>- <b>item</b></color>", "Color-wrapped lists keep block parsing");
        check(parser.Parse("<size=200>large</size>") == "large", "Unsupported rich-text tags are removed consistently");
        check(parser.Parse("<size=200>- item</size>") == "- item", "Unsupported tags are removed from block prefixes");
        check(parser.Parse("<i>italic</i>") == "<i>italic</i>", "Supported RimWorld style tags remain intact");
        check(parser.Parse("# Heading\n- **item**\n1. *next*") ==
            "<b>Heading</b>\n- <b>item</b>\n1. <i>next</i>", "Common settings-page blocks are parsed");
        check(parser.Parse(@"\**bold**") == "**bold**", "Escaped Markdown delimiter stays literal");
        check(parser.Parse("line\r\nnext") == "line\nnext", "Windows line endings are normalized");
        check(parser.Parse("See [the guide](test.mod://DocumentItem/help/start:2) now") ==
            "See <color=#66b3ffff>the guide</color> now", "Internal document links render with link color");
        var linked = new DynamicMarkdown(() => "See [guide](test.mod://DocumentItem/help/start:2)");
        check(linked.Links.Count == 1 && linked.Links[0].Uri.ChildId == "start" && linked.Links[0].Uri.Line == 2,
            "Markdown preserves package, child document and line targets");
        check(new DynamicMarkdown(() => "See [web](https://example.com)").Links.Count == 0,
            "External Markdown links are not interactive document links");
        check(DocumentUri.TryParse("Test.Mod://DocumentItem/Guide%20page/Intro:12", out DocumentUri address) &&
            address.PackageId == "test.mod" && address.PageId == "Guide page" && address.ChildId == "Intro" && address.Line == 12 &&
            address.ToString() == "test.mod://DocumentItem/Guide%20page/Intro:12", "Package URI roundtrips encoded IDs and one-based lines");
        foreach (string invalid in new[] { "im://help", "test.mod://SubItem/page:1", "test.mod://DocumentItem/parent/child:0",
            "test.mod://DocumentItem/../child", "test.mod://DocumentItem/a%2Fb", "test.mod://DocumentItem/a%ZZ", "test.mod://DocumentItem/a?x=1",
            "test.mod://DocumentItem/a:2147483648", "test.mod://DocumentItem/a/b/c" })
            check(!DocumentUri.TryParse(invalid, out _), "Invalid document target rejected: " + invalid);
         check(parser.Parse("See [the guide](guide/start.md) now") ==
             "See <color=#66b3ffff>the guide</color> now", "Markdown links render with link color");

        var multiple = Markdown.Interactive(() => "[first](test.mod://DocumentItem/first) / [second](test.mod://DocumentItem/second)");
        string opened = null;
        Event.current = new Event { type = EventType.MouseUp, mousePosition = new Vector2 { x = 9f, y = 1f } };
        multiple.Draw(new Rect(0f, 0f, 100f, 20f), uri => opened = uri.PageId);
        check(opened == "second", "Clicking the second link on a line opens its own target");
        Event.current = new Event { type = EventType.MouseUp, mousePosition = new Vector2 { x = 6f, y = 1f } };
        opened = null;
        multiple.Draw(new Rect(0f, 0f, 100f, 20f), uri => opened = uri.PageId);
        check(opened == null && Event.current.type == EventType.MouseUp, "Text between links does not navigate or consume input");
        var wrapped = Markdown.Interactive(() => "# **[abcdefgh](test.mod://DocumentItem/wrapped)**\r\n\\[literal](test.mod://DocumentItem/ignored)");
        Event.current = new Event { type = EventType.MouseUp, mousePosition = new Vector2 { x = 12f, y = 23f } };
        opened = null;
        wrapped.Draw(new Rect(10f, 20f, 4f, 12f), uri => opened = uri.PageId);
        check(opened == "wrapped" && Event.current.type == EventType.Used,
            "Wrapped formatted links navigate from their continuation line at an offset origin");
        check(wrapped.Links.Count == 1, "Escaped link syntax stays noninteractive");
        Event.current = new Event { type = EventType.MouseUp, button = 1, mousePosition = new Vector2 { x = 12f, y = 21f } };
        opened = null;
        wrapped.Draw(new Rect(10f, 20f, 4f, 12f), uri => opened = uri.PageId);
        check(opened == null, "Right clicks do not activate document links");
        GUI.enabled = false;
        Event.current.button = 0;
        wrapped.Draw(new Rect(10f, 20f, 4f, 12f), uri => opened = uri.PageId);
        check(opened == null, "Disabled Markdown does not navigate");
        GUI.enabled = true;
        Event.current = new Event();

        string source = "**one**";
        var dynamicParser = new CountingParser();
        var dynamic = new DynamicMarkdown(() => source, dynamicParser);
        check(dynamic.RenderedText == "<b>one</b>", "Dynamic Markdown renders its initial source");
        dynamic.Measure(100f);
        dynamic.Draw(new Rect());
        check(dynamicParser.Calls == 1 && Widgets.LastLabel == "<b>one</b>", "Dynamic Markdown caches and draws unchanged input");
        source = "**two**";
        check(dynamic.RenderedText == "<b>two</b>" && dynamicParser.Calls == 2, "Dynamic Markdown reparses changed input");

        string sectionSource = "one";
        var sectionMarkdown = new DynamicMarkdown(() => sectionSource);
        var sectionView = new MenuSectionView(new[]
        {
            new MenuSection("markdown", sectionMarkdown.Measure, sectionMarkdown.Draw,
                () => sectionMarkdown.MarkdownText)
        });
        sectionView.Draw(new Rect(0f, 0f, 120f, 10f));
        float firstContentHeight = Widgets.LastContent.height;
        sectionSource = "one\nline whose height changes";
        sectionView.Draw(new Rect(0f, 0f, 120f, 10f));
        check(Widgets.LastContent.height > firstContentHeight,
            "Dynamic Markdown layout key remeasures changed content");

        Widgets.LastLabel = null;
        var listing = new Listing_Standard
        {
            ColumnWidth = 120f,
            BoundingRectCached = new Rect(0f, 200f, 120f, 20f)
        };
        MenuControls.Markdown(listing, sectionMarkdown);
        check(Widgets.LastLabel == null, "Markdown control skips drawing outside listing bounds");
        listing = new Listing_Standard
        {
            ColumnWidth = 120f,
            BoundingRectCached = new Rect(0f, 0f, 120f, 40f)
        };
        MenuControls.Markdown(listing, sectionMarkdown);
        check(Widgets.LastLabel == sectionMarkdown.RenderedText, "Markdown control draws rows inside listing bounds");

        string path = Path.Combine(Path.GetTempPath(), "irismenues-markdown-" + Guid.NewGuid().ToString("N") + ".md");
        try
        {
            File.WriteAllText(path, "**file**");
            var staticParser = new CountingParser();
            var document = new StaticMarkdown(path, staticParser);
            File.WriteAllText(path, "**changed**");
            check(document.RenderedText == "<b>file</b>" && staticParser.Calls == 1, "Static Markdown keeps its initial file snapshot");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class CountingParser : IMarkdownParser
    {
        private readonly MarkdownParser inner = new MarkdownParser();
        public int Calls { get; private set; }
        public string Parse(string markdown) { Calls++; return inner.Parse(markdown); }
    }
}
