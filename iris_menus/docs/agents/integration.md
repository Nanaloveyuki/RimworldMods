# IrisMenus Agent Integration Contract

## Scope and ownership

- Target RimWorld 1.6 (`net48`) and 1.7 (`net10.0`), namespace `IrisMenus`. Require Harmony and package `Nanaloveyuki.IrisMenus`; no RimIris dependency.
- Reference the matching `1.6/Assemblies/IrisMenus.dll` or `1.7/Assemblies/IrisMenus.dll` with `Private=false`. Never ship a duplicate provider DLL. The preserved 1.6 binary does not contain the new native presentation API; the current 1.7 build does.
- Register pages, metadata, actions and presentations on the game thread, normally after `GetSettings<T>()` in the Mod constructor. Never register during rendering.
- Consumer owns settings, persistence, defaults and internal scrolling. The host neither serializes consumer values nor supports rollback. Preserve existing settings keys and save semantics.
- Do not patch other mods, scan private fields, or invoke drawing to discover settings. Manual adapter opt-in remains a user decision.

## Ordinary pages and metadata

```csharp
MenuRegistry.Register(Mod owner, string pageId, Func<string> title,
    Action<Rect> draw, Action save = null);
MenuRegistry.RegisterListing(Mod owner, string pageId, Func<string> title,
    Action<Listing_Standard> draw, Action save = null);
MenuRegistry.RegisterSubItem(Mod owner, string pageId, Func<string> title,
    Action<Rect> draw, Action save = null);
MenuRegistry.RegisterSubItemListing(Mod owner, string pageId, Func<string> title,
    Action<Listing_Standard> draw, Action save = null);
MenuRegistry.ReplaceSubItemListings(Mod owner, string registrationId,
    IEnumerable<MenuSubItemListing> listings);
MenuRegistry.RegisterSearchProvider(Mod owner, string pageId,
    Func<IEnumerable<MenuSearchEntry>> provider, Action<string> focus = null);
MenuRegistry.RegisterAdapterSearch(Mod owner,
    Func<IEnumerable<MenuSearchEntry>> provider, Action<string> focus = null);
MenuRegistry.InvalidateSearchIndex();
MenuRegistry.Open();
```

- Owner identity is `Content.PackageIdPlayerFacing + ":" + GetType().FullName`; page identity appends `":" + pageId`. Keep IDs and owner types stable. Null callbacks/owners and blank IDs are rejected; duplicate page IDs throw.
- Save defaults to `owner.WriteSettings`. Leaving a different page, returning from a search hit, and normal close save; errors block navigation/close. Same-page selection and switching presentation do not save. Forced removal can attempt saving but cannot veto removal.
- `RegisterListing` and `RegisterSubItemListing` own a persistent `MenuScrollView`. A custom `Action<Rect>` owns its scrolling. There is no extra host scroll wrapper.
- Dynamic replacement validates the complete set before mutation. Same-ID pages keep identity and Listing scrolling; removed pages rebind to a nearby category or catalog without running removed callbacks for a transition. Empty listings remove only that registration.
- Existing `RegisterListing` single pages open directly. Explicit SubItems retain an owner heading. All categories of the selected owner share search scope; native library groups by content pack.

```csharp
new MenuSubItemListing(string pageId, Func<string> title, Action<Listing_Standard> draw,
    Action save = null, Func<IEnumerable<MenuSearchEntry>> searchProvider = null);
new MenuSearchEntry(string id, Func<string> title,
    Func<string> keywords = null, Func<string> context = null);
```

Metadata ID is the anchor supplied to focus. Matching uses all whitespace-separated terms with `CurrentCultureIgnoreCase` against title, context, keywords, owner and page names. No fuzzy matching or private-control introspection. Indexing advances at most 32 provider steps and 32 matches per frame; results paginate by 20. This limits work count, not callback latency or total memory. Providers must be cheap and re-enumerable. They never draw pages. Language/registry/adapter changes rebuild the catalog; invalidate explicitly only when other metadata changes. Enumerator errors dispose that provider and preserve other results.

## Native full-screen presentation (current 1.7)

The old footer's **Open full-screen interface** enters the native host. `MenuRegistry.Open()`, the main button and original settings routing still default to the old host. One `MenuWindow`, session, catalog and browse state own both presentations. Returning restores old geometry, not full-screen bounds. Theme/scale/density/tips/panel opacity persist separately from old frost/blur settings.

```csharp
MenuRegistry.RegisterPresentation(this, "general",
    new MenuPresentation(context => new ExampleNativePage(context, settings, WriteSettings, help)));
```

Register the ordinary page first, then annotate the same owner/page ID. The ordinary drawer remains necessary for the old host. `MenuPresentation.Create` receives the host-owned `IrisMenus.UI.UiContext` and must return a fresh retained `MenuNativePage`. The host owns its lifetime. See `tests/ApiConsumer/ExampleMod.cs` for a compile-checked consumer and [native components](../references/native-ui-components.md) for controls.

```csharp
public abstract class MenuNativePage : IDisposable
{
    public abstract void Draw(Rect rect);
    public virtual void Prepare();
    public virtual void DrawOverlay(Rect viewport);
    public virtual void Focus(string anchor);
    public virtual IReadOnlyList<MenuChapter> Chapters;
    public virtual IReadOnlyList<MenuNote> Notes;
    public virtual bool HasPreview;
    public virtual void DrawPreview(Rect rect);
    public virtual bool CanReset;
    public virtual void Reset();
    public virtual void Dispose();
}
new MenuChapter(string id, Func<string> title);
new MenuNote(string id, Func<string> title, Func<string> text);
```

These are signature summaries, not a compilable class definition.

- Create controls once; no per-draw IDs or delegates. Do not call `BeginFrame`/`EndFrame` or dispose the borrowed context. The host runs them inside the actual IMGUI callback.
- `Draw` receives a clipped local viewport beginning at zero. Own exactly one content scroll view. Host does not infer chapters, fields, defaults, changes or save status from a callback.
- `Prepare` runs before host interactions. Prepare open popups here. `DrawOverlay` runs at root after content clips, before host drawers/dialogs. Keep input layering consistent with drawing.
- Chapter and metadata IDs feed `Focus`; retain pending anchors until layout exists. `Notes` are text with localized callback titles and session-local accordion state. `HasPreview` enables a bounded explicit preview region; it is not a game-scene capture.
- `CanReset` is false by default. Opt in only when `Reset` implements real defaults. Confirmation is tied to the originating page; do not reset another page after registry changes. Save remains the ordinary page's save callback.
- Factory, prepare, draw and overlay exceptions enter a retry view; do not silently replace a failed native page with its legacy drawer. Disposal is attempted once, reported on error, and cannot trap the window. Balance all GUI groups; the host cannot repair arbitrary consumer imbalance.
- Without a presentation, the original drawer renders in an isolated dark compatibility panel even under Light. It retains native text colors and one scrolling owner, rather than pretending to be light-theme aware.
- The native background is an opaque three-stop gradient with Light/Dark colors matching the reference. Panel opacity defaults to 94%. No scene capture or blur is used in this presentation. Closing fades the host only after both save gates succeed, blocks content input through exit, and releases retained resources after removal. Reduced motion snaps transitions.
- Underlying HUD/window drawing is gated while full-screen coverage is active. Windows above the host still render and receive input. Returning/closing restores normal drawing without replacing or deleting underlying windows.

## Navigation and memory

- The global catalog is a directory, not a combined editor. Opening hits saves/selects first, retains query/scope/page/scroll, then focuses an available anchor. Back saves before returning. Navigation clears search; editing search does not become a page-selection operation.
- `RememberState`, `LastPage`, `LastSubItems` and `HostView` retain navigation, searches, host scrolling and registered Listing scrolling. Native self-settings retain their own scroll offsets there. Custom pages/adapters own internal state. Drawers, notes and confirmations are session-local.
- Disabling memory clears remembered navigation, not consumer values, appearance, geometry or adapter choices. Explicit Mod routing takes precedence over remembered browsing.
- Save failures keep the selected page/window open; draw failures preserve save paths and offer retry. `ExitGUIException` propagates. Title failures log once and fall back to owner name.

## Names and owner actions

```csharp
MenuRegistry.RegisterModNameAlias(Mod owner, string language, string alias);
MenuRegistry.RegisterModNameAlias(ModContentPack content, string language, string alias);
MenuRegistry.RegisterModNameAlias(string packageId, string language, string alias);
string name = MenuRegistry.GetModDisplayName(Mod owner); // also ModContentPack
MenuOwnerActions.RegisterOwnerAction(Mod owner, string id, Func<string> label, Action execute,
    Func<bool> visible = null, Func<bool> enabled = null, Func<string> confirmation = null);
// ModContentPack overload also available.
```

Use language folder names (`English`, `ChineseSimplified`), not locale codes. Names/package IDs are trimmed; aliases are case-insensitive per package/language and last-wins. Resolution uses the current alias or original content name, never another language. It changes neither metadata nor stable IDs; original and localized names remain searchable.

Actions share content identity `PackageIdPlayerFacing + ":" + RootDir`. Re-registration replaces the same action ID. Drawing and execution recheck visibility/enabled. Nonblank confirmation uses a game dialog and rechecks on accept. Errors are reported without closing the host. A pack with actions but no settings gets an action-only launcher, not a synthetic Mod subclass. Launchers are excluded from settings tabs/search and adopt real handles when available.

## Anchors, sections and Markdown

```csharp
MenuControls.Anchor(Listing_Standard list, string id, float height = 30f);
new MenuScrollView(); // Draw, Focus, Reset, VisibleRect
new MenuSection(string id, Func<float, float> measure, Action<Rect> draw);
new MenuSection(string id, Func<float, float> measure, Action<Rect> draw, Func<string> layoutKey);
new MenuSectionView(IEnumerable<MenuSection> sections); // Draw, Focus, InvalidateLayout
IMarkdown live = Markdown.Dynamic(Func<string> getMarkdown, IMarkdownParser parser = null);
IMarkdown file = Markdown.StaticFile(string filePath, IMarkdownParser parser = null);
MenuControls.Markdown(Listing_Standard list, IMarkdown markdown);
```

Listing anchors are declared immediately before controls and do not reserve height. IDs must be unique per draw; calling outside an active host Listing throws. Focus stores an ID, resolves on the next layout and highlights for four real-time seconds. Missing conditional anchors warn and consume focus; explicitly reveal conditional content first. A custom provider focus replaces default Listing focus; null retains it. Listing clipping is not virtualization.

`MenuSectionView` materializes a fixed unique-ID section set. Measure is finite, nonnegative, side-effect-free, width-aware. Heights cache until width/language/layout key changes or explicit invalidation. Only visible positive-height sections draw, although traversal still visits all sections. Draw is clipped/local and must balance GUI state. Focus reuses measured geometry. Use `VisibleRect` only inside draw to cull consumer content.

Markdown supports its documented subset, not HTML. It preserves valid color tags and b/i rich text, strips other complete tag-shaped markup, and reparses dynamic sources only when text changes. Static files are UTF-8, read once and never watched/written. See [Markdown contract](../references/markdown.md).

## Adapters and routing

Manual adapters require a loaded Mod with nonempty `SettingsCategory()` and explicit user selection. Registered pages win; metadata alone does not opt in. Original drawers and `WriteSettings` execute directly, without extra scrolling or emulated `Dialog_ModSettings` lifecycle. The router replaces only exact `Dialog_ModSettings`, not custom subclasses or unrelated game settings.

Character Editor (`CharacterEditor.CharacterEditorMod`, Workshop 1874644848) is an explicit launcher adapter. Invoke its original configuration dialog only on a click; focus an existing dialog instead of opening duplicates. Closing it does not reopen it; use its own Save. Never patch or reflect its private settings/draw methods.

`ShowMainButton` defaults true. Both self-settings and old footer control the same value. `MainButtonWorker_IrisMenus.Visible` still includes `base.Visible`; hiding it preserves other entry points.

## Build, packaging and verification

Use .NET 10 SDK, .NET 9 runtime for engine-double checks, installed RimWorld 1.7 DLLs and the Harmony mod's .NET 10-compatible Harmony 3 runtime. Default references are the local game root; `RimWorldManagedDir` overrides it. Decompiled source is not a binary reference.

- Build `Source/IrisMenus.csproj` or `tests/ApiConsumer/ApiConsumer.csproj` for 1.7. Preserve existing 1.6 DLL/bundle unchanged; no upgrade/deploy step rebuilds or copies them.
- `scripts/validate.ps1 -SkipGpu` builds, runs engine-double contracts, validates localization/XML/load folders and stages a hash-verified package. These checks do not execute `MenuWindow` or prove GPU/input behavior.
- Shared deployment: `bash scripts/deploy.sh iris_menus [--build-only]`; `--skip-build` stages existing output. Deployment requires a stopped game, rejects redirecting targets and verifies copied hashes. Preserve ModsConfig, Workshop ID and installed 1.6 content.
- `LoadFolders.xml` selects `[1.6]` or `[1.7, Common]`, not subdirectories such as `Common/Defs`. Ship only the provider DLL, native shader source, shared resources, metadata and documentation; never game/Harmony assemblies.

Old-window renderer: 1.7 borrows scene color inside `ImGuiMaterialRenderer.TryRecord(frameSlot)` and finalizer-clears it. JIT-inlined forwarding hooks are not proof of execution. Draw rounded fill once; the material uses ClearTex as ordinary fallback, overlaying border or blurred scene. Entry frost samples main-menu artwork directly and maps physical bounds; no per-frame GUI bake. The full-screen gradient bypasses this path.

Real-game acceptance must distinguish compiled contracts from rendered behavior. Exercise supported game versions, map/world/menu, native/legacy/mixed/adapter pages, search/back, save/draw/factory failure, dynamic registrations, keyboard/pointer input, translations, scaling, reopen/restart and resource restoration before making corresponding claims.
