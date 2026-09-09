# RimIris Moonlight

A single-pass, cool, bright nighttime filter loaded as a RimIris bundle addon.
No runtime DLL, Harmony patches, or framework modifications.

## Install

Place this folder in RimWorld's `Mods` directory (or link it there), enable
RimIris Beta and RimIris Moonlight, and load Moonlight after the framework.
Keep the RimIris master switch enabled. In the Iris pack screen, enable the
`Moonlight` coexisting addon. Adjust strength, night brightness and cool tint
in its options. It can remain enabled alongside the selected base pack.

The runtime files are `About/` and `ShaderPacks/`, including `moonlight_win`.
The checked-in bundle targets Windows; other platforms require a matching build.

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

Unity Editor 2022.3.35f1c1 is used by the local toolchain. A licensed editor and
a working graphics device are required; do not pass `-nographics`.

```powershell
./scripts/build.ps1 -UnityPath 'E:\app\Unity 2022.3.35f1c1\Editor\Unity.exe'
```

Build output is reloaded as an AssetBundle and GPU-tested for day/off identity,
night brightness and tint, twilight interpolation, alpha, black/white preservation
and bounded grayscale output before publication. Logs go to `artifacts/build.log`.

Game acceptance remains separate: compare addon off/on at noon, dusk and midnight;
check interiors, lamps, snow, fog of war, UI, map/world switching and save/restart.
No in-game visual or FPS claim is implied by the build tests.
