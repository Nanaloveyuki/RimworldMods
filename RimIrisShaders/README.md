# RimIris Moonlight

A single-pass, cool, bright nighttime filter for RimIris on RimWorld 1.6 and 1.7.
RimWorld 1.6 retains the original Unity AssetBundle and UI DLL. RimWorld 1.7 uses
a native activation bridge and `1.7/Rendering/Moonlight.frag.glsl` instead of
rebuilding or loading the legacy Unity bundle. Both paths use RimIris's existing
pack screen, options and processing stage; the addon does not patch Harmony or
replace the framework's pane.

## Install

Place this folder in RimWorld's `Mods` directory (or link it there), enable
RimIris Beta and RimIris Moonlight, and load Moonlight after the framework.
Keep the RimIris master switch enabled. In the Iris pack screen, enable the
`Moonlight` coexisting addon. Its strength, night brightness and cool tint are
the framework's own sliders. It can remain enabled alongside the selected base pack.

Keep the complete mod folder, including `LoadFolders.xml`:

- **1.6:** loads the root `Assemblies/RimIrisShaders.UI.dll` and `Languages/`,
  with the original Windows bundle at `ShaderPacks/Moonlight/moonlight_win`.
  Other platforms require a matching legacy bundle build.
- **1.7:** loads only `1.7/`, including `Assemblies/RimIrisShaders.UI.dll`,
  `Rendering/Moonlight.frag.glsl` and physical copies of the keyed translations
  in `Languages/`. The native bridge uses the game's default scene vertex shader
  and the existing RimIris stage/options pipeline.

RimIris scans the root `ShaderPacks/` directly, independently of load folders, so
that shared pack metadata stays in place. Do not add the root folder to the 1.7
load list: the game would discover both copies of the same DLL. The original
1.6 DLL, bundle files and root translations are retained unchanged by the 1.7
build; no symlinks are required for translations.

## Effect

- Uses the framework's `_RimIris_SunGlow`, not screen brightness or wall-clock time.
- Full effect at sun glow <= 0.05; smooth fade to no effect at >= 0.55.
- Default shadow gain is 2.6 before highlight compression, with a blue-cyan tint.
- Preserves pure black, bright whites and source alpha; no blur or extra bloom.
- Runs at `post_composite`, after the framework's grading and darkness.

This is a screen-space filter, not physical moon lighting: it also affects indoor
pixels and cannot recover details already crushed to black by another effect.
It does not change gameplay illumination. Daylight is unchanged by this addon,
but the underlying RimIris/base-pack effects still apply.

## Build And Verify

### RimWorld 1.7 — .NET 10 and native rendering

Install the .NET 10 SDK. Build against the installed 1.7 binaries, not decompiled
source. The project and script default to 1.7 and the local installation at
`E:\Apps\Steam\steamapps\common\RimWorld` on Windows, or
`/mnt/e/Apps/Steam/steamapps/common/RimWorld` under WSL. RimIris defaults to
`Mods/3796735623/Assemblies/RimIris.dll` in that installation.

From this mod's directory, the exact default build command is:

```sh
dotnet build Source/Moonlight.UI.csproj -c Release -p:RimWorldVersion=1.7
```

The PowerShell script validates the version-specific game DLLs and RimIris path
before invoking the same build. Use PowerShell 7 (`pwsh`) under WSL:

```powershell
./scripts/build-ui.ps1 -RimWorldVersion 1.7
# Override either installation path when needed:
./scripts/build-ui.ps1 -RimWorldVersion 1.7 -GameDir 'E:\Apps\Steam\steamapps\common\RimWorld' -RimIrisAssemblyPath 'E:\Apps\Steam\steamapps\common\RimWorld\Mods\3796735623\Assemblies\RimIris.dll'
```

Output is `1.7/Assemblies/RimIrisShaders.UI.dll` (`net10.0`, with
`RIMWORLD_1_7` defined). All game/framework references are non-copying; dependent
DLLs must come from the installed game and RimIris. This build does not run Unity,
rebuild `moonlight_win` or overwrite `Assemblies/RimIrisShaders.UI.dll`.

### RimWorld 1.6 — legacy Unity path

The checked-in 1.6 runtime remains usable without rebuilding. An explicit legacy
UI rebuild targets `net48`, requires .NET Framework 4.8 reference assemblies and
the matching 1.6 game/RimIris installation, and **replaces the root deployed DLL**:

```powershell
./scripts/build-ui.ps1 -RimWorldVersion 1.6 -GameDir 'D:\Games\RimWorld-1.6' -RimIrisAssemblyPath 'D:\Games\RimWorld-1.6\Mods\3796735623\Assemblies\RimIris.dll'
```

For 1.6, `-GameDir` accepts the game root (using `RimWorldWin64_Data/Managed`) or
the Managed directory itself. A current 1.7 installation is not a source of
legacy Unity reference assemblies. Unsupported versions or missing required
DLLs are rejected.

Rebuilding the **legacy bundle** separately uses Unity Editor 2022.3.35f1c1.
A licensed editor and a working graphics device are required; do not pass
`-nographics`. This command replaces the root 1.6 bundle, not the 1.7 native
shader:

```powershell
./scripts/build.ps1 -UnityPath 'E:\app\Unity 2022.3.35f1c1\Editor\Unity.exe'
./scripts/verify-localization.ps1
```

The legacy bundle script requests bundle reload/GPU checks and requires the
completion marker in `artifacts/build.log` before reporting success. Those
checks do not exercise the 1.7 native path; the localization script checks the
original root language files. Neither command implies an in-game or FPS result.

### Verified 1.7 behavior

The installed addon was exercised with the game's own CLR, RimIris and Waylith
Vulkan renderer on an NVIDIA GeForce RTX 3060 Ti. The smoke run verified:

- Exactly one UI DLL selected for each version by the game's file loader.
- Native pack activation/deactivation across three cycles, option bounds,
  global-to-material uniform binding and the post-composite stage gate.
- GPU pixel readback for daylight/off identity, nighttime brightness and cool
  tint, twilight interpolation, neutral sliders, asymmetric image orientation,
  alpha and pure black/white preservation.
- English and Simplified Chinese root translation keys; unsupported build
  versions and missing 1.7 references are rejected.

The original six deployed 1.6 DLL/bundle/metadata/translation files retained
their pre-upgrade SHA-256 hashes. The 1.6 game runtime was not exercised.

The throwaway GPU host required a consistent Vulkan loader and an explicit
load of the game's `slang-glslang.dll`; these are host setup steps, not mod
patches. This isolated rendering check is not a full game-session or FPS test.

### In-game acceptance

Full game-session acceptance remains separate for each version: compare addon
off/on at noon, dusk and midnight; check interiors, lamps, snow, fog of war, UI,
map/world switching and save/restart. Also check pack and slider translations
in English and Simplified Chinese. No full-session visual or performance
result is claimed here.
