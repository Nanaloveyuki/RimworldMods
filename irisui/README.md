# IrisUI

RimWorld 1.7 · version 0.1.0 · Nanaloveyuki.

## Status: design and integration draft

This is an early working draft, not a finished UI replacement or a release-ready implementation. The current code explores Framework layout/input coordination and Inspector native-tab hosting; the visual design and interaction model are still being revised. Version 0.1.0 identifies this draft only.

The latest Normal/Minimum direction is not implemented: Normal should open by default with Needs, skill levels and passion visible, bottom tabs, and expand/collapse/settings controls. Prolonged inactivity without hover or interaction should collapse it to Minimum, showing only the name and body condition. The timeout and restoration behavior remain undecided. Current code behavior is described in [Inspector's notes](irisui_inspector/README.md), separately from this intended design.

Local frontend/image concepts and the reusable image-generation script remain under ignored `tmp/`; they are design material, not shipped UI. The solution requires the accompanying IrisMenus independent-surface APIs, not only the earlier released binary. Historical smoke checks below cover specific exercised paths, not completion of the design.

## Modules

- **IrisUI Framework** (`Nanaloveyuki.IrisUI.Framework`): shared light/dark theme, interface scale, reduced motion and layout support. Can be enabled without Inspector.
- **IrisUI Inspector** (`Nanaloveyuki.IrisUI.Inspector`): single-pawn inspector with Minimum, Normal and Fullscreen views and the pawn's native inspection tabs. Requires Framework.

Colonist Bar is deferred and is not part of this draft. The original colonist bar remains in use; no replacement geometry or docking is provided.

## Install

Place `irisui_framework` and, if wanted, `irisui_inspector` in RimWorld's `Mods` directory. Each mod folder must include its `About`, `Common`, `1.7` and `LoadFolders.xml` contents. Build the assemblies first when installing from source.

Load order:

1. [Harmony](steam://url/CommunityFilePage/2009463077)
2. [IrisMenus](https://steamcommunity.com/sharedfiles/filedetails/?id=3798502474)
3. IrisUI Framework
4. IrisUI Inspector (optional)

Supported combinations are Framework alone or Framework + Inspector, in both cases with Harmony and IrisMenus. Framework is currently a local dependency; Inspector intentionally has no Workshop URL for it.

## Build

Requires the .NET 10 SDK, RimWorld 1.7 game assemblies and the sibling `iris_menus` source project, which builds through Framework's project reference. From the repository root:

```sh
dotnet build irisui/IrisUI.slnx -c Release -p:RimWorldVersion=1.7
```

Set `-p:RimWorldManagedDir=/path/to/RimWorld` if the shared build properties do not match your installation. Outputs go to each module's `1.7/Assemblies` directory.

## Integration checks

On 2026-10-27, the Release solution build completed with zero warnings and zero errors. A temporary runner using the installed game assemblies passed 66 input and layout checks, including pointer-only/keyboard-only eligibility, disabled scopes, exact scale 0.9375, and non-overlapping Minimum/Normal/Fullscreen regions.

The existing IrisMenus API consumer also built with zero warnings and zero errors, and its engine-double suite passed 247 assertions (no rendering coverage). Both modules' metadata/load XML, 960×540 PNG previews, and English/Simplified Chinese/Traditional Chinese key sets and placeholders passed validation. Framework's Workshop URL remains omitted by agreement while it is a local dependency.

An isolated RimWorld 1.7.4990 instance loaded the same paused test save with Framework alone and then Framework + Inspector. Runtime checks exercised native health/needs page scrolling, unchanged natural tab size, Minimum body removal, Fullscreen-to-new-Pawn compact-mode restoration, hidden colonist-bar hit suppression/restoration, higher-dialog input priority, two-stage Escape, deselection, screenshot fallback/restoration and reduced motion. Captures were inspected at 1280×720; commands and reflection exercised the original game callbacks and window stack, not a complete physical mouse/keyboard acceptance run.

Third-party dynamic tabs, IME, resize-during-edit, tutorial gates and performance remain manual acceptance cases. See [Inspector's checks](irisui_inspector/README.md#integration-checks). This is a bounded smoke record, not a general compatibility claim.
