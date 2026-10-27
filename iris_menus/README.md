# IrisMenus

Opt-in RimWorld settings UI library. The current 1.7 source uses a single full-screen
host; the existing 1.6 release binary and resources remain archived and unchanged.
Requires Harmony; no dependency on RimIris. Original settings entries for opted-in
owners open IrisMenus. Unselected mods and unrelated game settings remain untouched.

## Integration

See [integration guide](docs/references/guide.md) for the Chinese integration walkthrough and
[Agent integration contract](docs/agents/integration.md) for exact API contracts and troubleshooting.
Markdown help and documentation content is covered in
[docs/references/markdown.md](docs/references/markdown.md).
Reusable native controls, containers, overlays and their IMGUI/animation contracts are documented in
[Native UI components](docs/references/native-ui-components.md). The full-screen host
is the only settings presentation in the current 1.7 source: `MenuRegistry.Open()`,
the bottom-bar button and opted-in settings-entry routing all open that host.
There is no old/new interface switch or resizable legacy window. Existing
`RegisterListing`, `Register` and adapter drawing callbacks remain supported inside
the same host; using it does not require replacing every traditional control.
Retained native page instances keep their scroll positions and edit buffers until
the page is replaced or the window is disposed.
The current 1.7 source follows the local Endfield-like prototype's component styling
and timing; see the component guide's 2026-10-25 fidelity notes for screenshot evidence
and remaining acceptance limits. It retains game fonts rather than bundling web fonts.
Pages are grouped by Mod; only the selected group expands. Existing single-page
registrations still open directly. Search supports global or owner-wide metadata
results, pagination, and explicit anchors without drawing unselected pages.
The native header search exits management and searches the configured global or
current-Mod scope, rather than leaving matching results behind the management page.

Reference `IrisMenus.dll` with `Private=false`, declare a mod dependency on
`Nanaloveyuki.IrisMenus`, and register on the game thread, usually in your Mod
constructor:

```csharp
MenuRegistry.RegisterListing(this, "general", () => "My Mod", DrawSettings);
```

`RegisterListing` receives `Action<Listing_Standard>` and manages scrolling
using measured content height. Use `MenuControls.Section`, `Checkbox`, `Slider`,
`Number` (int/float), and `Select` (dropdown) to build a page. Keep numeric edit
buffers in your Mod or page instance, not local variables created each frame.

For existing custom rendering, `MenuRegistry.Register` receives `Action<Rect>`.
That page owns its controls and scrolling; `MenuScrollView` is also available
for composing a custom page. Both APIs use the same optional save callback.

The page ID is stable and unique within the owning Mod class. Titles are
evaluated when drawn and may use `Translate()`. Drawing uses a clipped local
coordinate space. Balance all GUI groups and restore any GUI state you change.
Do not ship another copy of IrisMenus.dll inside your consumer mod.

The optional fifth argument is a save callback; the default is the owner's
`WriteSettings`. Saving runs when leaving a page and when closing the window,
even if drawing failed. Changes are live; closing does not cancel them. The host
does not implement a separate settings store for consumers. Call
`MenuRegistry.Open()` to open the shared window. Save failures keep the current
page/window open for retry through the game's `OnCloseRequest` contract.
The window pauses
the simulation, like the original mod settings dialog; Enter does not close it.

See `tests/ApiConsumer/ExampleMod.cs` for a compile-checked consumer.

## Manual Hosting

Users can select settings pages in the add-mod-settings view. Only loaded Mod
instances with a nonempty `SettingsCategory()` are listed. Selection persists
by package ID and full Mod type name; unloaded selections remain stored.
Explicitly registered owners take precedence over adapters.

The full-screen host uses a dedicated desktop library page with
`About/Preview.png` thumbnails, current-language aliases (or the About name),
and hosted-page visibility controls. These controls do not enable or disable game mods.

### Language-specific Mod names

From 1.1.1, mods and translation packs can register names for themselves or
another package without changing `About.xml` or patching `Content.Name`:

```csharp
MenuRegistry.RegisterModNameAlias(this, "English", "My Mod");
MenuRegistry.RegisterModNameAlias("author.targetmod", "English", "Hunger and Havoc");
```

Use RimWorld language folder names (`English`, `ChineseSimplified`, etc.).
`ModContentPack` is also accepted. Same-package/language registrations are
last-wins and case-insensitive. Unregistered languages retain the original
name; explicit category titles and persistent IDs do not change. Navigation,
manual-hosting titles and search paths use the current alias; searches also
match the original name. `MenuRegistry.GetModDisplayName(Mod|ModContentPack)`
exposes the same resolution for consumers. See both guides for full contracts.

A narrow Harmony prefix on `WindowStack.Add` redirects an exact
`Dialog_ModSettings` instance only when its owner registered a page or the
user selected that owner for hosting. Existing IrisMenus windows are reused.
Unselecting the owner restores its original entry immediately; custom dialog
subclasses are not redirected. No mod's drawing method is patched.

Ordinary adapters call the original `DoSettingsWindowContents(Rect)` directly,
without an extra outer scroll view, and use the original `WriteSettings()`.
It does not emulate `Dialog_ModSettings`, its lifecycle, or mod-specific patches
to that dialog. Drawing failures are logged and the page exposes a retry action.
Unbalanced third-party IMGUI groups cannot be reliably recovered by the host.
In the 1.7 full-screen host, common backgrounds, buttons, sliders, default checkboxes,
toggles and native input chrome follow Light/Dark without relocating controls.
Standard labeled checkboxes/toggles use switch visuals; custom checkbox textures
and custom style backgrounds remain native. Plain/rich text receives final-draw
contrast adjustment without rewriting strings, tags or native editor buffers.
Caller and rich-text opacity are preserved, not forced opaque. Adaptation is scoped
to the selected callback; unrelated windows retain their original appearance.
Custom images/shaders and arbitrary consumer background colors are not covered.

Character Editor (`CharacterEditor.CharacterEditorMod`, Workshop 1874644848)
uses its drawing callback to open a separate configuration window and close
the vanilla settings dialog. Its hosted page therefore provides an **Open
original settings window** button instead of invoking that callback every
IMGUI event. Clicking opens the original window, or focuses it if already open;
closing it never automatically reopens it. Save changes with Character Editor's
own Save button. Existing manual selections and settings entry routing still
work; explicit page registrations retain precedence. No Character Editor
methods or private settings are patched or reflected.

## Build

Install the .NET 10 SDK and a local RimWorld 1.7 installation. The existing
engine-double checks also require the .NET 9 runtime. Builds and deployment
default to 1.7 and use the installed game's DLLs, not a 1.7 NuGet package.
The existing 1.6 DLL and shader bundle are preserved unchanged: deployment
scripts neither rebuild nor copy any 1.6 files. Harmony remains an external
runtime dependency; its reference package is used only to compile the mod.

From the monorepo root:

```bash
bash scripts/deploy.sh iris_menus --build-only
bash scripts/deploy.sh iris_menus --rimworld-dir /path/to/RimWorld
```

Windows uses `python scripts/python/deploy.py iris_menus` with the same options. `--property RimWorldManagedDir=PATH` overrides the 1.7 DLL directory; decompiled source cannot be used as binary references. The module's separate verification entry remains `iris_menus/scripts/validate.ps1`.

For DLL-only iteration:

```bash
# Default: 1.7, net10.0, 1.7/Assemblies/IrisMenus.dll
dotnet build Source/IrisMenus.csproj -c Release
# Explicit 1.7 selection is also accepted.
dotnet build Source/IrisMenus.csproj -c Release -p:RimWorldVersion=1.7
```

Consumers must reference the DLL matching their game version and target
`net48` for 1.6 or `net10.0` for 1.7. `tests/ApiConsumer` accepts the same
`RimWorldVersion` and `RimWorldManagedDir` properties. The preserved 1.6 binary retains
its existing APIs; native presentation annotations are available in the current 1.7 build.

The 1.6 release is an archive, not a target of this source cutover. Retain its
existing DLL and resources rather than rebuilding them from the current source;
new full-screen-only behavior and settings migration are promised only for 1.7.

The shared deployment CLI accepts `--configuration Debug|Release`, `--rimworld-dir`, `--target`, and repeated `--property NAME=VALUE` overrides. Build-only stages `artifacts/iris_menus/release` without modifying the game. Deployment refuses a running game, validates package identity, rejects symlink redirects, and verifies copied hashes.

`LoadFolders.xml` selects `[1.6]` or `[1.7, Common]`. The unchanged legacy
resources remain in `1.6`; 1.7 uses `Common` and loads neither the 1.6 DLL nor its Unity
AssetBundle. The current 1.7 host uses a gradient, not a Frost shader or scene capture.
Deployment copies only current 1.7 output, shared resources, metadata,
README and `docs/`, never Harmony or game dependencies. It leaves
the destination's entire existing `1.6/` directory untouched and does not
remove files or edit ModsConfig. A fresh 1.7 staging package intentionally has
no 1.6 DLL/bundle; retain the existing 1.6 artifacts separately for legacy use.
`--skip-build` stages existing 1.7 output without rebuilding it.

`validate.ps1` builds the 1.7 API consumer and provider, runs
engine-double checks, validates XML/translations and load-folder isolation,
and stages a hash-verified 1.7 package without deploying to the game. Staging
explicitly rejects 1.6 files. The verification script and shared deployment do not
invoke Unity or modify the preserved 1.6 bundle. GPU/UI acceptance remains manual.
The retired Unity 2022.3.35/D3D11 fixtures covered only the historical 1.6 renderer.
Full-screen rendering and UI interactions require verification in the actual 1.7 game.

## Background and memory

The current 1.7 full-screen host uses the reference's opaque Light/Dark three-stop
gradient with independently configurable panel opacity (94% by default). It does
not capture the scene or expose frost, blur, transparency or legacy window geometry.
The preserved 1.6 binary retains its original Unity camera/AssetBundle background
implementation and settings; this source cutover does not rebuild or alter it.
RimWorld 1.7 requires the Harmony mod's .NET 10-compatible Harmony 3 runtime.

**Remember last page** restores the page, global/owner search, result page,
management filter, host scroll positions and registered Listing-page scrolling
across reopen/restart. Explicitly opening a Mod's settings takes precedence over
the saved view. Missing pages fall back to an available category of the same Mod,
or the global catalog. Disabling memory clears host navigation state, not Mod
settings, adapter selections, sidebar configuration or appearance preferences.
Custom pages/adapters remain responsible for their own internal UI state.

### Settings migration (current 1.7)

The host keeps `LastPage`, `LastSubItems`, `HostView`, adapter choices, sidebar
configuration, bottom-bar visibility and item-value colors. Appearance and behavior
preferences remain configurable; consumer settings and save callbacks are unchanged.

| Previous persisted key | Current persisted key |
|---|---|
| `nativeTheme` | `theme` |
| `nativeScale` | `scale` |
| `nativeDensity` | `density` |
| `nativePanelOpacity` | `panelOpacity` |

Legacy appearance keys are read only during `LoadingVars`, and only when the
corresponding new key is absent. New keys take precedence, and saving writes only
the new keys. Internal settings fields `NativeScale` and `NativePanelOpacity`
become `Scale` and `PanelOpacity`; `Theme` and `Density` retain their names.
Within `HostView`, `navigationScroll` migrates to `railScroll` and `adapterScroll`
to `managementScroll`, with existing destination keys taking precedence.
The retired `nativeMode`, `width`, `height`, `x`, `y`, `frostEnabled`, `blur` and
`transparency` settings are no longer used or written. The browse-state `HostView`
mode remains navigation state; it is not the removed old/new presentation mode.

**Show IrisMenus button in the bottom bar** is enabled by default. Toggling it
immediately hides/restores the button and persists across restarts. When hidden,
use **Options → Mod settings → IrisMenus → Open IrisMenus**; registered/hosted Mod
entry routing is unaffected.

In 1.7, the toolbar immediately left of the header search opens **Bottom-bar
buttons**, a dedicated full-screen IrisMenus page rather than a separate dialog.
The same page is available from ordinary IrisMenus mod settings, including when its
own bottom-bar entry is hidden. Back/Escape returns to the previous settings page.
Toggle entries in the list; changes save immediately. **Restore all** also clears
entries belonging to currently unloaded mods.
Filtering runs before the standard bottom-bar layout; remaining buttons fill the
space. It does not modify Defs or keyboard shortcuts. Only `MainButtonDef` entries
are covered, not independent mod-drawn bars. A worker's own visibility conditions
still apply.

## Release notes

### Unreleased — chapter navigation (1.7)

- Preserve adjacent TOC pointer events when disabled scroll content temporarily
  suppresses input; capture the event type before entering its hit clip.
- Publish the final native scroll offset through `MenuScrollView.VisibleRect` after
  drawing, including scrollbar movement in the same pass.
- Verification: provider/API consumer and affected RHAH consumer build with zero
  warnings/errors; 247 engine-double runtime assertions pass. Installed-assembly
  probes cover forward/reverse jumps, rapid retargeting, wheel interruption and
  recovery, adjacent TOC clicks, and final-chapter selection at the scroll limit.
  No new in-game visual acceptance or deployment: the installed companion launcher
  still targets the missing `RimWorldWin64.exe`.

### 2.0.0 — full-screen-only cutover (1.7)

- Keep the accepted full-screen interface as the only host; remove the old window,
  old/new switch, geometry and Frost controls without removing third-party drawers,
  Listing pages, adapters or their save ownership.
- Migrate host appearance and scroll keys as documented above; retain browse memory,
  sidebar choices, configuration and bottom-bar controls.
- Preserve existing 1.6 release binaries, language files and resources unchanged.
- Verification: provider/API consumer build with no warnings or errors; 247 runtime
  assertions pass with engine doubles. Isolated RimWorld 1.7.4985 checks cover entry
  reuse, migration, close/save and remembered-page reopening; see the
  [cutover acceptance record](docs/references/native-ui-components.md#唯一全屏宿主与设置迁移验收--2026-10-27).

The entries below are historical records from before the full-screen-only cutover.
Their old-window controls, presentation switching and renderer behavior do not
describe the current 1.7 interface or add capabilities to the archived 1.6 binary.

### Unreleased — sidebar menu and localization (1.7)

- Align the sidebar context menu with EndfieldUI's plain Light/Dark menu:
  scoped colors, icon actions, inset keyboard focus, separated removal action,
  top yellow marker, 200ms fade and 300ms entrance motion.
- Replace implementation-oriented player copy with usage descriptions; remove
  ordinary bottom-bar Def names and management package IDs from player labels.
- Complete Simplified Chinese, Traditional Chinese and English keyed strings,
  including document-link failures and color-picker labels. Icon provenance and
  licenses remain in `NOTICE`.
- Record the [visible-region performance plan](docs/references/native-ui-components.md#可见区域性能规划未实施)
  and [independent UI consumer plan](docs/references/native-ui-components.md#独立-ui-消费者开放规划未实施).
  Both are proposals, not implemented capabilities or measured improvements.
- Verification: provider/API consumer build with no warnings or errors; 261
  existing runtime assertions pass with engine doubles. Language XML, key sets,
  placeholders and required assets checked. Menu visuals and player copy remain
  assigned to user acceptance; no new game rendering verification or deployment.

### Unreleased — toolbar and bottom-bar visibility (1.7)

- Add reusable horizontal icon-action `UiToolbar`, following upstream EndfieldUI's
  sunken styling, hover/press transitions, tooltips and single-Tab-stop navigation.
- Add a dedicated bottom-bar visibility page with persistent Def-name choices,
  recovery from missing mods and Restore all. Screen picking has been removed;
  visibility is managed exclusively through the list.
- Verification: 1.7 provider/API consumer builds and runtime checks are described
  in the component guide. Full game visual/input and restart-persistence acceptance
  remain manual.

### Unreleased — item controls and inspectable registry (1.7)

- Adapt common hosted controls and plain/rich text to Light/Dark without requiring
  consumer rewrites; retain native input editors, values and callback ownership.
  Align switch enabled/disabled colors and geometry with upstream switch states.
  See the component guide's hosted-theme verification notes for exercised limits.
- Add reusable item slots, virtualized item rows, grouped hosted lists, ChoiceSort,
  real-progress loaders and an opaque RGB color-picker dialog.
- Add shared single-item market-value colors: gray below 50, green from 50,
  blue from 200, purple from 500 and gold from 1500. IrisMenus settings expose
  threshold sliders/numeric fields, color editing, add/remove and reset.
- Index loaded ThingDefs at startup in batches of 256 using the game's categories.
  Expose processed/total progress, lookup, direct category membership, uncategorized
  definitions and diagnostics; provide a searchable registry page and log export.
- RHAH consumes the shared item lists and grouping choices without changing its
  whitelist settings or bulk-selection behavior. Legacy settings remain available.
- Verification and limitations: see the [component guide's 2026-10-26 notes](docs/references/native-ui-components.md#物品组件与注册表验收--2026-10-26).

### Unreleased — native full-screen interface (historical pre-cutover, 1.7)

The following records the implementation and acceptance limits at that stage,
before the old presentation was retired; it is not current entry-point guidance.

- Add an explicit full-screen entry in the old footer without increasing footer height;
  keep the old main-button/settings routing and restore old geometry when returning.
- Share registration, selection, save gates, search/back and adapters between presentations.
  Add optional `RegisterPresentation` annotations for native pages, chapters, notes and reset.
- Match the reference's Light/Dark three-stop gradient, rectangular panels, navigation,
  tabs, context drawers, settings preview and rotating close control. Native panel opacity
  defaults to 94% independently of legacy frost/blur/transparency.
- Suppress underlying HUD/window drawing only while the full-screen host covers it;
  preserve higher dialogs, input ownership, pause and normal restoration after exit.
- Add working appearance/behavior settings in both presentations; persist scale, density,
  theme, reduced motion, tips, search preference and native panel opacity.
- Add a 720ms, column-by-column square-scale window transition on 1.7, using a
  frozen GPU frame rather than repeated hosted callbacks; reduced motion skips it.
- Add a low-contrast geometric background print and editable lower-left marquee,
  plus About.xml name, author and version metadata in the footer with ellipsis.
- Add reusable `UiToast` notifications and `MenuRegistry.ShowToast`; automatic edit
  hints appear three seconds after the last actual edit, not ordinary navigation.
  Honor the notification switch and configurable 1–10 second display duration;
  save timing remains unchanged.
- Keep factory/draw failures retryable, save failures blocking, confirmation targets stable,
  and consumer disposal errors unable to trap the window.
- Correct overlay input ownership, search blur/Escape behavior, navigation animation
  state and screen-scaled popup/drag coordinates. Preserve SubItem tabs with
  upper-right pagination and middle-ellipsized long labels, including scale changes.
- Replace the add-mod drawer with a desktop library page showing Preview images,
  localized names and hosted-settings visibility controls. Remove the page's
  automatic-save hint while retaining its reset action and existing save gates.
- Persist the selected presentation across reopen/restart independently of page memory.
  Keep failed saves from committing a presentation change; retain the legacy first-use default.
- Show native header search results after leaving management; retain configured search scope.
- Give light buttons an opaque gray base with distinct hover/pressed shading.
- Theme native hosted tabs and scrollbar paint without replacing their input/callbacks.
  Include narrowly scoped RimTalk/RimChat custom navigation painters; RimChat 1.7
  compatibility is not established by these hooks.
- Use one native owner-action strip for hosted and native pages, with text measurement
  matching the drawn font, scale and button padding.
- ODF documentation uses native topic/chapter navigation and theme-correct prose,
  literal source and code. Advanced RimTalk retains its document links and syntax colors.

### 1.1.2

- Restore native 1.7 main-menu artwork blur instead of explicitly disabling frost
  outside a running game; retain the existing map capture and saved preferences.
- Bind 1.7 scene color at material recording so optimized forwarding calls cannot
  bypass the frost hook in modded profiles. Preserve the rounded translucent
  fallback, existing frost and transparency controls, and stored preferences.
- Add a default-on, persistent bottom-bar IrisMenus button visibility setting
  shared by the original Mod settings dialog and the IrisMenus window footer;
  changes apply and save immediately, and the settings-page launcher remains available.
- Place the bottom-bar button visibility toggle to the right of the appearance
  controls, aligned with the frosted-background toggle; keep a separate lower
  row when the window or translated label cannot fit.
- Reduce the footer from 120 to 72 UI units when its controls fit on two rows,
  returning 48 units to settings and navigation; narrow windows and long
  translations retain the space required for wrapped controls.
- Fix the startup `MenuActionOwner` constructor error by storing action-only
  launcher content directly instead of creating an automatically discovered Mod subclass.
- Add black-on-white line-art Preview and ModIcon PNGs. Regenerate with
  `uv run --no-project --with pillow scripts/generate-about-images.py`.

### 1.1.1

- Add language-specific Mod name aliases, including translation-pack registration
  for other package IDs, and a shared display-name lookup.
- Show cached ModIcons in the add-mod-settings list; reserve icon/checkbox space
  when measuring wrapped names. Search matches localized and original names.
- Publish atomic dynamic SubItem listing replacement and per-content Mod actions,
  including action-only launcher pages.


## Current Verification Boundary

The current 1.7 contract is a single full-screen host with registered/native pages,
traditional drawers, opt-in adapters, search, browse memory, theme and configuration.
The dated component and release-note evidence above predates this cutover; it does
not establish a new build, migration regression result or game acceptance for it.

Compile and engine-double checks are not in-game acceptance. Before release,
verify registered/native/adapted pages, original opted-in settings entry points,
save/reopen/restart and migrated settings, draw/save failure paths, long translated
labels, scrolling, and UI scale/resolution changes in RimWorld 1.7. Include both
ordinary and self-scrolling adapter pages. Existing 1.6 outputs remain unchanged.
