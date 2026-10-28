# IrisUI Inspector

RimWorld 1.7 / .NET 10 · version 0.1.0 · Nanaloveyuki. Requires Harmony, IrisMenus and IrisUI Framework, loaded in that order before Inspector. Framework can also be used without Inspector; Inspector cannot be used alone. The assembly entry point is `IrisUI.Inspector.InspectorMod`.

This is an early integration draft, not the completed Inspector design. The behavior below describes current code. The intended Normal overview, bottom tabs, visible Needs and skills/passions, and inactivity-driven name/condition-only Minimum are not implemented; see [draft status](../README.md#status-design-and-integration-draft).

## Behavior

- Replaces the shell only while the map's original Inspect main tab is open with exactly one Pawn selected. Multiple selection, non-Pawn selection, world view and screenshot mode use the original UI.
- Minimum shows identity, the native condition label, current job and summary health bar without the full summary filler. Normal adds the complete, scrollable vanilla summary and dynamic native tab navigation. Fullscreen places a rectangular navigation rail at the right edge, native content on the left and a `PortraitsCache` portrait with an echo and moving perspective floor. No invented equipment categories or character anatomy are added.
- The display mode is saved in mod settings. Switching modes does not reopen the active tab. Selecting another Pawn exits Fullscreen and restores the last Minimum/Normal preference; Minimum and Normal retain their respective modes. `MainTabWindow_Inspect.OpenTabType` remains the active-page state, with the native validity checks governing which pages are available.
- Fullscreen Escape/back restores the last Minimum/Normal preference. Normal/Minimum close clears selection and closes the original main tab, matching the native Escape route and preventing automatic selection-driven reopening. Original selection shortcuts, inspect buttons, filler and gizmos remain native.

## Native tab contract

The original `MainTabWindow_Inspect`, `WindowUpdate`, `CurTabs`, `OpenTabType`, `InspectTabBase.DoTabGUI`, immediate window ID `235086`, validity checks, close button, exception guard, `ExtraOnGUI` and outside-click callback are retained. No private `FillTab` is called directly.

`TabRect` still runs the original `UpdateSize`; only its origin is moved to the framework body. The original tab's natural `size` is never rewritten. The original immediate-window callback is carried inside a native two-axis scroll view whenever its natural size exceeds the available body. Drawing and hit testing therefore share the same scroll transform, including after resolution changes. Third-party content is not assumed to be responsive; all natural content remains scroll-accessible.

In Normal and Fullscreen, the native filler retains a dark inset even in the light shell: vanilla and third-party native labels are not rewritten or globally recolored. Its summary text keeps its native scrolling inside a natural-width outer viewport, with status and inspection buttons reachable through two-axis scrolling.

The existing immediate-window hit rectangle is updated before high-priority input and drawing. Minimum and page/selection transitions remove obsolete native windows instead of leaving an old hit area until repaint. The framework owns fullscreen cover/native-window ordering and lower-layer input suppression. Header and navigation share one IrisMenus context; native body remains in its original window.

Fullscreen staggers the portrait entrance before the native content, with the header and navigation remaining unfaded. Animation respects the framework's reduced-motion setting without moving input coordinates. UI controls and navigation arrays are persistent and rebuilt only when the visible tab instances or language change.

Native opacity is applied around the original callbacks. Vanilla or third-party controls that explicitly reset their drawing color can bypass fractional opacity; preserving their rendering and business logic takes priority over enforcing a uniform fade. No offscreen texture or global native recoloring hook is used.

## Integration checks

From the repository root, build with `dotnet build irisui/IrisUI.slnx -c Release -p:RimWorldVersion=1.7`. In game, exercise all three modes, rapid Pawn selection, Fullscreen-to-new-Pawn transitions, hidden/dynamically added third-party tabs, tutorial gates, close/reopen, Escape with a higher dialog, screenshot/world/non-Pawn fallback, both scroll axes and resizing while a native control is focused. Check reduced motion and that header/navigation remain usable during the portrait entrance. At 1280×720 with framework scale 0.9375, confirm the last natural-content controls remain reachable. Build and runtime outcomes are reported separately by the integration owner.
