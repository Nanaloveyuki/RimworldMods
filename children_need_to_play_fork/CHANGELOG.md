# Changelog

## Fork v1.3.1 — 2026-10-29

- Renamed the displayed mod and settings category to The Children Need To Play Fork; retained Dratic95 and added Nanaloveyuki to the author metadata. Package ID and the no-publication restriction are unchanged.
- Added two independently saved, default-on controls for prisoner baby/toddler/child crying mood effects on colonists and non-colonists, including parents. Prisoners, slaves and visitors use the non-colonist option.
- Controls stop new crying mood memories and renewals; existing memories expire normally. Social opinion, waking and care behavior remain unchanged.
- Added complete Traditional Chinese localization: 18 XML files and 658 keys, matching Simplified Chinese keys, placeholders and literal newlines.
- Verified the installed Prison Security arc sentry already recognizes vanilla crying and all three fork crying states. Kept its master, crying and friendly-exclusion switches authoritative instead of adding a redundant patch; documented the required settings.
- Release build: 0 errors, 307 warnings. Actual 1.7 engine memory/settings and sentry predicate smoke: 161 checks passed, using disposable Unity-reference-adapted assembly copies. In-game visual and firing checks remain unexercised.

## Fork v1.3.0 — 2026-10-29

- Unified normal, urgent, idle and crib play scheduling. Work, Sleep, Learning, assigned labor and medical/sleep needs now take precedence; player-forced orders remain protected.
- Added staggered 50-tick running-play checks, including travel. Consolidated crib fallbacks and replaced four fullness-constant transpilers with narrow prefixes.
- Added saved per-parent, per-bout crying responses (80% ignore by default, configurable 0–100%) and the child's −5 mood / 12-hour ignored memory.
- Added independent, default-off older-child crying (ages 7–13 while still Child stage), with half-duration bouts and half-strength aura thoughts.
- Made dirt castles selectable, attackable and deconstructible while retaining their lifespan.
- Added complete Simplified Chinese player text and extracted normal player-visible C# strings; retained persisted identifiers and developer diagnostics.
- Reduced repeated mood candidate scans, pawn snapshots and temporary memory lists; play trackers now clean up on destruction rather than a full periodic sweep. Environmental observation retains bounded polling.
- Release build passed with 0 errors and 307 warnings. Actual-assembly smoke passed for shared scheduling rules, castle loading/deconstruction eligibility, tracker retention/removal, cry eligibility and saved parental decisions; all nine new patch groups attached with the game's installed Harmony. In-game visual/rest/aura and full-map profiling remain manual checks.

## Fork settings — 2026-10-29

- Added optional IrisMenus category registration without a compile-time or required mod dependency.
- Reorganized every setting into seven shared categories for standalone and hosted settings; existing save keys, defaults and ranges are unchanged.
- Current IrisMenus 1.7 uses themed native listing controls, scrolling, help overlays and adult plush-mood choices. Older providers retain categorized listings.
- Added English and Simplified Chinese category titles/descriptions; fixed local dependency and inline-task paths for Windows/WSL builds.

## Fork import — 2026-10-29

- Vendored upstream v1.2.2 with its MIT license and Dratic95 attribution intact.
- Changed package ID to `drati.ToddlersPlayExpanded.fork` and removed the Steam Workshop publishing ID.
- Added the author's no-release agreement and the required no-Workshop-upload warning to README.
- Added the Toddlers dependency's Workshop URL from the upstream project reference and fitted the preview to the repository's 960×540 format.
- Gameplay source and shipped assemblies are unchanged.

## v1.2.2 — Playtime Comes First

### 🐛 Fixed
- **Children who just turned 3 "refused to play" for about two in-game days.** When a toddler becomes a child, their Learning and Play needs both start at 50%. The base game always lets a child finish learning first, and only once Learning is almost full do they get a turn to play. So a new child spent all their free time on learning activities (floor drawing, nature running and so on) while Play drained to the bottom, even if you used the debug action to give them a play desire.
- Now, **when a child's Play need is low (below 30%) and lower than their Learning need, playing comes before learning.** Once a play session lifts Play back up, they return to their learning as usual. Eating, drinking, the toilet, washing and sleep when tired still come first, and children on a "Learning" schedule block still only learn.
- This mostly helps newly aged-up children, but any child who falls behind on play now catches up instead of waiting for learning to fill.

### 💡 Note
- If you also use "Learning Threshold for Learning Job Fix", children keep topping up Learning until it is 99% full, so they play a little less often than without it. With this fix they still play whenever their Play need runs low.

## v1.2.1 — Sweet Dreams Fix

### 🐛 Fixed
- **Toddlers stuck at 28% rest, repeatedly sleeping and "Gazing at the ceiling".** This mod's in-crib activities ("Gazing at the ceiling", "Self Soothing", "Jumping in crib" and "practicing talking in crib") were offered to toddlers who were already asleep. That woke them as soon as their rest passed 28%. Sleeping toddlers are now left alone and sleep until they wake up naturally: fully rested (100%), or for the usual reasons such as hunger, their schedule or danger. Thanks to the player who reported it!
- These activities are also no longer offered to a downed toddler resting in a crib (most of them would have stopped straight away).

### 🔧 Internal
- The in-crib activity checks no longer use reflection or look their activity up by name on every check, which makes them a little cheaper.

## v1.2.0 — Fresh Build Fix

### 🐛 Fixed
- **"Failed to find RimWorld.ThoughtDef named TPE_Event_SolarFlare" on startup.** The previous Workshop upload shipped an out-of-date `ToddlersPlayExpanded.dll`. It had not been rebuilt after the Solar Flare mood was retired, so it no longer matched the Defs and source shipped next to it. This release ships a DLL built fresh from the included source.
- These changes were in the previous upload's source but missing from its DLL, so players never got them. They are live now:
  - **Power Outage mood reworked**: it fires only when the power really went out (a running building was switched off for lack of power and can't restart yet). There's no outage mood for tribal/neolithic/medieval toddlers. The separate Solar Flare mood is gone; Power Outage covers it.
  - Old saves that still hold the retired Solar Flare mood now load silently.
  - Toddlers and children hold the toy sword and magnifying glass at a 45° angle.
- Child Play Desires now reroll every **8 in-game hours** as documented (they were rerolling every 19.2 hours).

### ✨ New
- **RimWorld 1.7 support**: one DLL runs on both 1.6 and 1.7.

### 🔧 Internal
- **Stale-build guard**: every build stamps a fingerprint of its source code into the DLL. With dev mode on, the mod logs one clear error at startup if the DLL doesn't match the `Source/` folder next to it, so an out-of-date build is caught before it is published.
- Upload cleanup: stale backup files were removed, and design notes and the shelved Story Spinning defs no longer ship with the mod.

> Versions between v1.1.0 and v1.2.0 were published without changelog entries.

## v1.1.0 — The Toys & Cribs Update

### ✨ New Features

#### Toy System
- **Toy Workbench** — New crafting station for making toys. Uses vanilla `Building_WorkTable` patterns with proper bills, work speed reporting, and facility support.
- **6 Craftable Toys** — Teddy Bear, Muffalo, Sheep, Dinosaur, and Monster plush toys, plus a Toy Sword for monster hunters.
- **Toy Categories** — Toys are sorted into 5 categories: Plush Toys, Imagination Toys, Sensory Toys, Technological Toys, and Pull Toys. Categories display in the inspect panel via `CompToyCategory`.
- **Toy Box Storage** — The vanilla Biotech Toy Box now functions as `Building_Storage` with `ITab_Storage`, holding up to 25 toys. Items remain visible inside. Texture swaps between empty and full states based on contents.
- **Right-Click Float Menu** — Right-click any toy to order a toddler to "Play with [toy]" or "Carry [toy]".

#### New Play Activities (Autonomous)
- **Play With Toy** — Toddlers pick up a real toy from storage, carry it to a clear spot, and play with it while orbiting toy motes spin around them.
- **Carry Toy** — Toddlers grab a toy and carry it around the colony.
- **Monster Hunt + Toy Sword** — Toddlers now pick up a toy sword before hunting monsters, carrying it for the entire hunt. Sword is dropped (unforbidden) when finished.
- **Being a Dinosaur + Plush** — Toddlers now find and equip a dinosaur plush before stomping around. The plush is worn as apparel during the entire activity.

#### "Wants to Be Let Out of Crib" System
- **Crib Confinement Hediff** — Crawling toddlers in cribs accumulate a "crib confinement" hediff over time. Stages: Restless → Frustrated → Wants Out (at 100% after ~4 hours awake).
- **Mental State** — At 100% confinement, toddlers enter a "Wants to be let out of crib" mental state. They cry intermittently with tear particle effects, suffer a -4 mood debuff while confined, and a -2 debuff for ~1 hour after release.
- **Auto-Rescue** — Colonists automatically receive a "Let toddler out of crib" job when a toddler enters the mental state. No work priority setup needed — a `MapComponent` handles it directly.
- **Timing** — Mental state lasts 25–42 minutes. Crying occurs in 8–14 second bursts with 4–10 second pauses. A 100-second cooldown prevents infinite re-trigger loops.

#### Plush Sleep Integration
- Toddlers automatically seek out a plush toy before going to sleep (injected into `JobDriver_LayDown` via Harmony).
- **"Slept with a soft toy!"** — +2 mood for 24 hours.
- **"No soft cuddle buddy..."** — -4 mood for 24 hours if no plush is available.
- Plush toys are converted to apparel (Belt layer, Baby developmental stage) so they persist through sleep without being dropped.
- Base mod's apparel optimizer is patched to prevent colonists from stripping plush toys off toddlers.

#### Custom Sound
- **Dinosaur Roar** — Replaces the vanilla warg-pitched sound with a custom baby dinosaur roar. Individual 15–25 second cooldown per toddler. Rare trigger chance (0.1%).
- **"Test dinosaur roar"** debug action for quick testing.

#### Debug Actions
- **Spawn 7 toddlers** — Spawns toddler-age colonists with Home area restriction.
- **Spawn toy set** — Spawns all 6 toys at the cursor.
- **Test plush thought** — Manually applies +2 or -4 sleep thought to selected colonist.
- **Test dinosaur roar** — Plays the custom roar sound.
- **Spawn baby food / muffalo plush** — Quick spawn helpers.

### 🐛 Bug Fixes
- Fixed plush apparel showing magenta checkerboard — all directional textures (`_north`, `_south`, `_east`) now use real art instead of stubs.
- Fixed "already in another container" `ThingOwner` conflict when equipping plush from carry tracker (sleep job, dinosaur job, and Harmony sleep injection).
- Fixed `DevelopmentalStageFilter` on plush toys showing "Child" instead of "Baby".
- Fixed debug spawner causing "non-downed Baby colonist" errors.
- Fixed infinite mental-state re-trigger loop after crib timeout with `lastTriggerTick` cooldown.
- Fixed `MentalStateDef` missing `moodThought` field by applying thoughts manually in `PostStart`/`PostEnd`.
- Fixed `Thought_Memory` without `durationDays` expiring instantly.
- Fixed `recoverFromSleep=true` causing rapid mental state flickering in cribs.
- Fixed immediate forced-out-of-crib bug — MapComponent now waits 10 seconds before rescuing.
- Fixed `CanReserve(crib)` silently blocking all crib rescues — base mod's `JobDriver_LetOutOfCrib` does not reserve the crib.
- Fixed sleep job injected toils crashing with hard `EndCurrentJob` failures — rewritten as soft-fail custom toils.
- Fixed `CribUtility.InCrib(pawn)` guard preventing toddlers from leaving cribs to fetch plush.

### 🔧 Technical
- Removed performance-killing `IEnumerable<Toil>` Harmony postfix on `MakeNewToils`.
- Removed defunct `TPE_SeekPlushToy` job and `WorkGiver_TPE_LetToddlerOutOfCrib`.
- Patched base mod `ToddlerSkydreaming` reservation spam to suppress log noise.
- All C# classes now have XML doc comments (`/// <summary>`).
- Added `drawData` to plush apparel for proper positioning and scaling (0.35x, side offset).

### 📝 Description Update
- Added "Wants to be let out of crib" system explanation.
- Added Toys & Toy Box section with categories and implemented toy list.
- Added Sleep With a Plush section.
- Restored Road Map with future plans.
