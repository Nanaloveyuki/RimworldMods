# Architecture.md — Bird's-Eye View

> **Living map of "The Children Need To Play"** (`drati.ToddlersPlayExpanded.fork`).
> This file is the fast-orientation map: folder layout, design patterns, data
> flows, and the "where do I touch this?" index. Update it whenever a system
> is added/changed materially. It intentionally does NOT restate full XML —
> that lives in the Defs themselves (and the Schematic). This is about
> **shape**, not content.
>
> **Last full code-vs-doc audit:** after feature #23 (power outage / toy trading).
> Every count, file location, priority and class name below was re-checked against
> the source in that pass; what changed is listed in §6 "Full-scan audit".
>
> **Latest fork change (2026-10-29):** shared scheduling/rest eligibility now gates normal,
> urgent, idle and crib play. `PlaySchedulingPatches.cs` replaces four literal transpilers
> with narrow prefixes and one crib fallback chooser; running play rechecks every 50 ticks.
> `CryingPatches.cs` handles child cry hooks; parental decisions live in saved mental states.
> See §4 for dispatch and §5 for subsystem ownership. The audit history in §6 describes
> upstream versions, not the current fork's implementation.
>
> **Earlier (v1.2.0):** stale-DLL guard (a build-time source fingerprint plus a dev-mode startup
> check, §3a) and upload cleanup (§1, §6). Fixes the v1.1.x Workshop upload whose DLL predated its
> own Source/ + Defs/ ("Failed to find ThoughtDef TPE_Event_SolarFlare").

---

## 0. What this mod IS

A large **Harmony + XML-patch expansion** of Cyanobot's **Toddlers** mod. It
does three structurally different things that share one codebase:

1. **Extends the base mod's toddler play system** (new `ToddlerPlayGiver` /
   `JobDriver` pairs plugged into Toddlers' own dispatch loop).
2. **Bolts on an entirely new, parallel need system for children 3–13**
   ("Play" need, desires, think-tree node) that has no equivalent in the base
   mod — modeled after vanilla Biotech's Learning system.
3. **A pile of independent side-systems** (crib lock + crib gate, toy box,
   event moods, random moods, child crying/tantrums, Bubble Machine + mood
   thoughts, the "Learning" schedule assignment + growth-point blending,
   auto-dressing toddlers, Homely compat, god-mode gizmo) that hang off the
   same Harmony patch file and Defs folder but don't depend on each other.

Hard dependency: **Toddlers.dll** (Cyanobot) is referenced directly by the
csproj — many of our types subclass or patch its types (`ToddlerPlayGiver`,
`ToddlerPlayUtility`, `BabyPlayGiver_*`, `AnimationWorker_Toddler`, `MentalState_BabyFit`,
`BabyMoveUtility`). This is NOT a standalone mod — read Toddlers' shape first
when confused about a base type.

**Game-version support: 1.6 + 1.7 from ONE DLL** (no version folders / no LoadFolders.xml —
everything loads from the mod root on both versions).
- RimWorld 1.7 is not Unity/Mono any more: it runs on a .NET 10 "WaylithClr" runtime that
  redirects `UnityEngine*` assembly refs to `UnityStubs.dll` and keeps Assembly-CSharp
  Harmony-patchable. Its own DLLs are net10 (no `RimWorldWin64_Data/Managed/` folder), so the
  csproj compiles against the **RimWorld 1.6 reference assemblies** (`Krafs.Rimworld.Ref`
  1.6.4871 NuGet — Assembly-CSharp + every UnityEngine module). Never re-add game HintPaths.
- Toddlers (Cyanobot) has no 1.7 folder; on 1.7 the game falls back to its `1.6/` folder, so
  we keep referencing `…/2903359152/1.6/Assemblies/Toddlers.dll`.
- 1.7 audit (done): our sources compile cleanly against 1.7's real Assembly-CSharp; all 52
  attribute-targeted + 7 vanilla `TargetMethod()` Harmony patches resolve unambiguously with
  matching parameter/`___field` names (the other 26 `TargetMethod()` patches hit Toddlers.dll,
  which is unchanged); every Patches/ XPath matches, every ParentName/Class ref and every
  string `GetNamed(...)` lookup resolves. `harmony.PatchAll()` aborts at the first bad
  target, so re-run this kind of audit after any future game update.

**Mod version:** About.xml `<modVersion>` (1.2.1 as of the toddler sleep-loop fix). Bump it and add a
CHANGELOG entry with every Workshop upload, so bug reports can name the exact build (§7 "Publishing").

---

## 1. Folder structure

```
children_need_to_play_fork/        (mod root — packageId drati.ToddlersPlayExpanded.fork)
├── About/
│   ├── About.xml                  Mod metadata (incl. upstream <modVersion>), deps (Harmony, Toddlers),
│   │                                loadAfter (+ soft Homely hint)
│   ├── Preview.png                Workshop preview image
│   └── UserBG.png                 not read by the game (likely the source background for Preview.png)
│                                 PublishedFileId.txt removed; this fork must not be published
├── Assemblies/                    BUILD OUTPUT — ToddlersPlayExpanded.dll (+ .pdb). Don't hand-edit. It carries the
│                                    fingerprint of the source it was built from (§3a). Rebuild after source changes; this fork must not be published
├── Defs/                          XML def declarations (new defs; NOT patches to existing ones — see Patches/)
│   ├── AnimationDefs/             AnimationDef for active-play animations (spin/jump/snail-crawl)
│   ├── JobDefs/                   JobDef for crib-lock jobs (give toy, return to crib)
│   ├── MentalStateDefs/           Child crying + child tantrum mental states, the crying MentalFitDef + tear EffecterDef
│   ├── MoteDefs/                  Bubble mote def
│   ├── PlayDesireDefs/            All PlayDesireDef entries (the 3–13 desire roster)
│   ├── SoundDefs/                 Child-crying + speech-practice sound defs
│   ├── ThingDefs/                 ThingDefs_DirtCastle.xml (castle) + ThingDefs_Toys.xml (⚠️ holds ONLY the Bubble
│   │                                Machine — same filename as the root file below, different content)
│   ├── ThoughtDefs/               5 random-mood sets (+ homesick), event moods, self-soothing, child crying
│   ├── ThingDefs_Toys.xml         "Toys" ThingCategoryDef, Toy Workbench, 6 plushes (abstract TPE_PlushToyBase),
│   │                                toy sword, magnifying glass — prices/trade tags live here (§5.4)
│   └── (other root xml files)     Hediffs, Jobs, Need, Recipes, RoomRole, ToddlerPlayDefs, WorkGivers, toy/crib
│                                    thoughts (ThoughtDefs_Toys.xml), child play thoughts, Learning TimeAssignment
│   NOTE: the Toy Box is NOT one of our ThingDefs — it is Biotech's vanilla `ToyBox`, converted by Patches/.
├── Patches/                       PatchOperation XML — surgical edits to VANILLA/Toddlers defs (not new defs)
│   ├── ThinkTree_ChildPlay.xml         inserts JobGiver_ChildPlay into MainColonistBehaviorCore
│   ├── ThinkTree_ChildPlayWhenIdle.xml inserts JobGiver_ChildPlayWhenIdle into Humanlike's idle branch
│   ├── ToyBox_Storage.xml              turns Biotech's vanilla `ToyBox` into `Building_ToyBox` storage (Toys filter,
│   │                                     25 items/cell, Standable, empty/full texture swap). Conditional — no-op without Biotech
│   └── TraderKinds_BulkGoods_BuyToys.xml  bulk goods traders (only) buy anything tagged "Toys" (§5.4)
├── Source/                        C# project (net472, Harmony 2.4.1, Krafs.Rimworld.Ref 1.6 refs + Toddlers.dll)
│   ├── ToddlersPlayExpanded.csproj   also defines TPE_MOD_BUILD + the TpeEmbedSourceFingerprint build step (§3a)
│   ├── SourceFingerprint.cs       stale-DLL guard part 1: the fingerprint algorithm. Compiled TWICE (into the mod AND
│   │                                as the MSBuild task); BCL-only, never add game types (§3a)
│   ├── BuildFingerprintCheck.cs   stale-DLL guard part 2: dev-mode-only startup comparison, called from TPE_Mod (§3a)
│   ├── Mod.cs                     legacy empty `Verse.Mod` subclass — inert (no settings, never shown); real entry is TPE_Mod
│   ├── HarmonyPatches.cs          ⭐ THE HUB — ~4800 lines, ~84 active patch classes (+3 commented-out Pillow Nesting
│   │                                ones), each a TOP-LEVEL `public static class Patch_*` in the namespace
│   ├── ModSettings.cs             TPE_Mod host + TPE_Settings persistence and standalone category navigation
│   ├── SettingsPages.cs           seven shared category drawers, no gameplay state or duplicate settings store
│   ├── IrisMenusIntegration.cs    optional reflection registration and hosted adult-mood choice
│   ├── DefOfs.cs                  all `[DefOf]` static holder classes (TPE_JobDefOf, TPE_ThoughtDefOf, ...)
│   ├── DebugActions.cs            Dev-mode "General" debug actions: Force play activity on toddler, Force play desire
│   │                                on child, Test plush thought, Spawn 3/7 children, Spawn 7 toddlers, Spawn toy set,
│   │                                Toggle God Mode: Max Learning / Max Play, Fix child play need duplicates, …
│   ├── (JobDriver_*.cs, JobDrivers.cs)     toil-based job implementations — one file per feature cluster
│   ├── (ToddlerPlayGiver*.cs)               ToddlerPlayGiver subclasses (base-mod dispatch contract)
│   ├── (PlayDesireDef.cs, PlayDesireGiver.cs) new-need-system desire roster + selection strategy
│   ├── (MapComponent_*.cs, GameComponent_PlayTracker.cs)  polling/persistent state owners (8 MapComponents, §2.5)
│   ├── GameComponent_KnownColonists.cs      save-wide (cross-map) "ever seen as a free
│   │                                          colonist" registry — backs the NEW COLONIST
│   │                                          event-mood edge detection (see §2.3b)
│   ├── (Comp*.cs, Mote_Bubble.cs, Building_*.cs)          Bubble Machine + Toy Box + Dirt Castle
│   ├── DirtCastleUtility.cs       shared "where may a dirt castle go + is it enabled" rules (see §4.2a)
│   ├── PlayAnimationUtility.cs    the two "disable play animations" settings reads + start/keep/stop
│   │                                helpers every animating play driver uses (see §5.3)
│   ├── (Hediff_TPE_*.cs, MentalState_*.cs)                crib-lock restlessness, child-cry mental state
│   ├── (ThoughtWorker_*.cs, Thought_SelfSoothing.cs)      dynamic mood conditionals
│   ├── (WorkGiver_*.cs, JobGiver_*.cs)                    colonist-facing work (give toy to locked toddler, dress
│   │                                                        toddler) + child-facing think nodes. ⚠️
│   │                                                        WorkGiver_TPE_LetToddlerOutOfCrib + JobGiver_SeekPlushBeforeSleep
│   │                                                        are DEAD (no def/think-tree references them — §6)
│   ├── FloatMenuOptionProvider_ToddlerToys.cs  right-click a toy with a walking toddler selected → Play with / Carry
│   ├── (*Utility.cs)              stateless static helper libraries shared across the above
│   ├── HomelyCompat.cs            reflection-only soft-compat shim for "Intimacy - Homely"
│   ├── JobDriver_StorySpinning.cs / LearningGiver_StorySpinning.cs   shelved feature (§6) — compiles, never used
│   └── obj/                       build intermediates (ignore). Holds the generated AssemblyInfo.cs that CONTAINS the
│                                    fingerprint, so it is excluded from the fingerprint (circular otherwise, §3a)
├── Textures/
│   ├── Things/{Building,Item,Misc,Mote,Toys}/   ThingDef art (toy box empty-state, workbench, toys, dirt castle,
│   │                                              bubble mote; Things/Toys/ = Bubble Machine)
│   ├── Toys/                      ⚠️ 5 placeholder sprites for roadmap toys (Pinwheel, Rattle, SensoryBall,
│   │                                Watergun, wand) — referenced by nothing yet
│   └── UI/{Icons/, LockPlayNeed.png, ToddlerIcon.png}   gizmo + desire icons (ToddlerIcon + Icons/Learning/
│                                    StorySpinning are currently unreferenced)
├── Sounds/{Pawn/, ToddlerSpeech/, TPE_DinoRoar.ogg}      child-crying loops, babble grains, monster-hunt roar
├── Languages/English/Keyed/       translation keys for all C#-emitted strings (gizmos, messages, settings)
├── CHANGELOG.md / README.md       user-facing docs (CHANGELOG: v1.1.0, then v1.2.0; the gap between them is noted there)
├── .codegraph/                    external code-index tool folder (only a .gitignore). Not mod content, harmless
└── .modmixer/                     Modmixer bookkeeping, NOT uploaded by Modmixer's Publish (schematic.json,
                                     cs-assets.json, stubs.json, prefs.json; backup/ = Modmixer's own pre-edit snapshots)
                                     archive/ = kept-but-unshipped material moved out of the mod in v1.2.0:
                                       Plans/ (design docs: crib gate, play-need rollout, prisoner system),
                                       _Disabled_StorySpinning/ (SHELVED feature XML, see §6),
                                       backups/Hediffs.xml.backup_20260805_100849 (stale Defs snapshot)
```

**Rule of thumb:** `Defs/` = things that don't exist yet and we're adding.
`Patches/` = surgical edits to a def that vanilla/Toddlers *already* defines
(ThinkTreeDef insertions, storage filter tweaks). If you need to change how
an *existing* ThinkTreeDef/def behaves structurally, it's a `Patches/`
PatchOperation; if you're declaring a brand new Def, it's under `Defs/`.

---

## 2. Design patterns in play

### 2.1 Harmony patch organization — "one static class per patch"
`HarmonyPatches.cs` opens with a `[StaticConstructorOnStartup] public static class
HarmonyPatches` whose static constructor runs `new Harmony("drati.toddlersplayexpanded")`
(all lowercase) `.PatchAll()` once, then `RegisterRetiredDefs()` (§5.2), the plush
`developmentalStageFilter` fix + sleeping-plush graphic cache (§5.1) and
`HomelyCompat.Initialize()`. `PatchAll()` scans the WHOLE assembly, so a
`[HarmonyPatch]` class in any file is applied. Every individual patch is its **own
small top-level `public static class Patch_<Target>_<Member>_<Purpose>`** (siblings in
the namespace — NOT nested inside `HarmonyPatches`) decorated with
`[HarmonyPatch(typeof(X), "Method")]` (or `[HarmonyPatch]` + `TargetMethod()` for
Toddlers-internal / overloaded targets) and a `Prefix`/`Postfix`/`Finalizer` method.
Feature groups remain top-level patch classes. Scheduling patches live in
`PlaySchedulingPatches.cs`; crying hooks live in `CryingPatches.cs`; remaining groups
stay in `HarmonyPatches.cs`. The former four fullness-constant transpilers are removed.
Use shared eligibility before dispatch, rather than interpreting a null result as permission.

### 2.2 Two parallel "play dispatch" strategies (toddlers vs. children)
These are the two most important extension points and they are **not the
same pattern** — don't confuse them:

- **Toddlers (~1–3, `ToddlerUtility.IsToddler`; base-mod driven):** Toddlers itself defines an abstract
  `ToddlerPlayGiver` (Strategy pattern) and two JobGivers. `JobGiver_ToddlerPlay` iterates
  all registered `ToddlerPlayDef`s in random order, calling `CanDo` on each giver and then
  `TryGiveJob` on the first eligible one. `JobGiver_ToddlerPlayInCrib` does NOT iterate: it
  hard-codes the base mod's TV + Skydreaming (`CanDoFromCrib`/`TryGiveJobFromCrib`), and
  our four in-crib activities use one gated fallback chooser (§4.1).
  This mod registers **17 `ToddlerPlayDef`s**
  (`Defs/ToddlerPlayDefs.xml`) backed by **16 active `ToddlerPlayGiver` subclasses**
  (14 in `ToddlerPlayGivers.cs` — `_Spinning`, `_WorkWatching`, `_Jumping`, `_Running`,
  `_MonsterHunt`, `_Dinosaur`, `_LookThroughStorage`, `_SelfSoothing`, `_CribJumping`,
  `_CeilingWatching`, `_SpeechPractice` (serves both SpeechPractice defs), `_HeaterSnuggling`,
  `_PoppingBubbles`, `_ChasingBubbles` — plus `_PlayWithRealToy`/`_CarryToy` in
  `ToddlerPlayGiver_Toys.cs`; `_PillowNesting` is commented out, §6). The 18 toddler
  `JobDriver`s all live in `JobDrivers.cs` (incl. `JobDriver_TPE_PlayWithToy`, `_CarryToy`,
  `_BugwatchingWithMagnifyingGlass`, which the base mod's Bug Watching is upgraded to by a
  `JobGiver_ToddlerPlay.TryGiveJob` postfix). We do NOT reimplement
  the dispatch loop — we plug into it. Harmony patches on `ToddlerPlayUtility`
  (`ToddlerPlayTickCheckEnd`, `IsToddlerPlaying`) adjust *when a job ends* and
  *what counts as "playing"* for animation/render purposes, since the base
  mod's own logic doesn't know about our new activities.

- **Children (3–13, this-mod-invented):** `Patches/ThinkTree_ChildPlay.xml`
  inserts `ThinkNode_Priority_ChildPlay { JobGiver_ChildPlay }` directly into
  vanilla's `MainColonistBehaviorCore` `ThinkNode_PrioritySorter`, just before
  `ThinkNode_Priority_GetJoy` (normal 8.5, urgent 9.15, both gated by schedule, assigned work,
  sleep and medical-rest policy — §4.2). `JobGiver_ChildPlay.TryGiveJob` reads the
  pawn's **currently active 2 desires** off `Pawn_PlayTracker` (see §2.3) and
  tries each via its `PlayDesireGiver` (Strategy pattern, mirrors
  `PlayDesireGiver_PlayWithToy`, `_WatchTelevision`, `_BubblePopping`, etc. in
  `PlayDesireGiver.cs`), producing a `JobDriver_Child*` (in `JobDriver_ChildPlay.cs`
  / `JobDriver_ChildActivePlay.cs` / `JobDriver_ChildBubblePopping.cs` / etc.).
  A second, independent think-node insertion
  (`Patches/ThinkTree_ChildPlayWhenIdle.xml` → `JobGiver_ChildPlayWhenIdle`)
  hooks vanilla's *idle/wander* branch of `Humanlike`, not the colonist work
  core — see §4.3 for why these two entry points behave differently.

### 2.3b Per-save state: `GameComponent` for cross-map identity tracking (distinct from §2.3 below)
`GameComponent_KnownColonists` is a SECOND, unrelated reason this codebase reaches for a
`GameComponent` instead of `MapComponent` state — not object-store-per-pawn (that's §2.3's
job), but "a HashSet that must be scoped to the whole save, not one map", because a Pawn
can move between the player's maps and a per-map set can't tell "new to this map" apart
from "new to the colony". It pre-seeds itself in `FinalizeInit()` (iterating `Find.Maps`,
which are guaranteed fully spawned by the time any GameComponent's `FinalizeInit` runs —
verified against `Game.LoadGame()`/`InitNewGame()` call order) so existing colonists at
game start/load never retroactively look "new". Since event-moods audit #2 (§5.2) the
seed ALSO covers every player-faction humanlike in `PawnsFinder.AllMapsWorldAndTemporary_Alive`
(caravans, transporters, quest-lent pawns, gravship passengers), and the per-map
`RegisterColonistsOn(map)` it exposes reads the cheap `SpawnedPawnsInFaction(player)` list
with `FreeColonists`' own filter (slaves included) — never `FreeColonists`/`AllPawns`,
which walk every thing-holder on the map. If you need more save-wide (not
per-map, not per-pawn-object) identity/dedup state in the future, follow this pattern
rather than smuggling a `static` field into a MapComponent.

### 2.3 Per-pawn state: `GameComponent` as an object-store, not `ThingComp`
Children don't get a custom `ThingComp` for play state. Instead:
`GameComponent_PlayTracker : GameComponent` owns a `Dictionary<Pawn, Pawn_PlayTracker>`,
lazily created via `GetTracker(pawn)`, and persists it with a manual
key/value Scribe pass (`scribeTrackerValues` intermediate list — the classic
RimWorld "you can't Scribe a Dictionary directly" workaround). `Pawn_PlayTracker`
itself is a plain `IExposable` — NOT a `ThingComp`/`HediffComp` — holding
`ActivePlayDesires`, `lockPlayNeed` (the god-mode gizmo flag), and desire-reroll
timestamps. **Anywhere you need "does this pawn have play tracker state", go
through `Current.Game.GetComponent<GameComponent_PlayTracker>().GetTracker(pawn)`
— never construct `Pawn_PlayTracker` directly outside that file.** Exception:
if you're reading FROM OUTSIDE the play-need system (a caller that has no
other reason to touch play trackers, e.g. the Learning/Play mutual-exclusion
Harmony patch in §4.2b) and the pawn plausibly has none, use
`TryGetTracker(pawn)` instead — it returns null instead of lazily creating,
so you don't spuriously allocate+save an empty tracker for a pawn that would
otherwise never have one (e.g. every child in a game with the whole
play-need system disabled).

### 2.4 `Comp` pattern for building/hediff behavior
Where a *Thing* needs its own local behavior + inspect string + gizmos, this
mod uses vanilla's intended extension points instead of MapComponent polling:
- `CompBubbleZone : ThingComp` on the Bubble Machine (zone caching, spawn
  timer, bidirectional bubble ownership).
- `CompToyCategory : ThingComp` (on every toy ThingDef, `categoryLabel` in XML) shows a
  "Category: X" inspect line, and `TPE_ToyUtility.IsPlushCategory` (label "Plush toys")
  is how the child Play-With-Toy desire finds plushes without hard-coding defNames.
  (The right-click "Play with / Carry" menu is `FloatMenuOptionProvider_ToddlerToys`,
  which uses its own `IsToy` check, not this comp.)
- `HediffComp_DinosaurStompSafety : HediffComp` guards the "Being a Dinosaur"
  activity.
- `Hediff_TPE_CribConfinement` / `Hediff_TPE_CribLocked` / `Hediff_TPE_WantsOutOfCrib`
  are full custom `Hediff` subclasses (not comps) — restlessness accrues and
  ticks inside the hediff itself.

### 2.5 `MapComponent` = "polling watcher", one per concern
Eight separate `MapComponent`s each own exactly one narrow job, ticked from their own
`MapComponentTick`/interval check — deliberately NOT merged into one god-component:

| MapComponent | Job |
|---|---|
| `_CribGate` | per-crib "gate open" state (saved) for the Open Crib Gate gizmo |
| `_GodModeNeeds` | the dev "Toggle God Mode: Max Learning / Max Play" debug actions (self-gated: play-need system on, player home, a toggle on) |
| `_ToddlerCribRescue` | lets toddlers with the WantsOutOfCrib hediff out (dispatches colonists directly, ignoring work settings) |
| `_ToddlerCribReturn` | returns crib-LOCKED toddlers who are out of the crib (with its own crib reservation table) |
| `_ToddlerEventMoods` | event-triggered moods (§5.2) |
| `_ToddlerPostWakeToyDrop` | makes toddlers drop the plush they slept with after waking |
| `_ToddlerRandomMoods` | hourly random moods |
| `_ToddlerSleepTracker` | adds the crib-confinement hediff to toddlers in cribs; removes the crib lock on age-up |

**Registration:** vanilla `Map.FillComponents()` instantiates EVERY non-abstract
`MapComponent` subclass on EVERY map, both when a map is created and when it is loaded,
before `Map.FinalizeInit`. The three `Map.FinalizeInit` postfixes that "add the component
if missing" (`Map_FinalizeInit_Patch` in MapComponent_ToddlerSleepTracker.cs,
`Patch_Map_FinalizeInit`, `Patch_Map_FinalizeInit_ToyDrop`) therefore never add anything,
and their `IsPlayerHome` / setting conditions do NOT limit where those components exist —
any such gate must live inside `MapComponentTick` (GodModeNeeds does it there). Harmless;
don't copy the pattern. When adding a new "periodically scan the map for X" feature, add a
new `MapComponent` (no registration needed) rather than extending an existing one.

### 2.6 Feature-scale escape hatches from the "everything in HarmonyPatches.cs" rule
Two features earned their own file/class instead of patch classes in HarmonyPatches.cs,
because they're substantial standalone subsystems, not a 5-line prefix:
- **Bubble Machine** (`CompBubbleZone.cs`, `Mote_Bubble.cs`, `BubbleMoodUtility.cs`,
  `BubbleSettings.cs`) — full building behavior, not a patch at all.
- **Homely compat** (`HomelyCompat.cs`) — reflection-gated, see §5.
- Minor, accidental: `MapComponent_ToddlerSleepTracker.cs` also holds a `[HarmonyPatch]`
  (`Map_FinalizeInit_Patch`, the redundant registration patch from §2.5).

### 2.7 `DefOf` caching convention
`DefOfs.cs` holds 12 small static `[DefOf]` classes (`TPE_JobDefOf`,
`TPE_AnimationDefOf`, `TPE_HediffDefOf`, `TPE_MentalStateDefOf`,
`TPE_ThoughtDefOf`, `TPE_SoundDefOf`, `TPE_ThingDefOf`, `TPE_ChildPlayDefOf`,
`TPE_TimeAssignmentDefOf`, `TPE_RoomRoleDefOf`, `TPE_PlayDesireDefOf`,
`TPE_ToddlerPlayDefOf`) — one per def-type family, not one giant class. Follow this
split when adding a defOf for a new def type.
`TPE_ToddlerPlayDefOf` (v1.2.1) binds the base mod's `ToddlerPlayDef` type (hence
`using Toddlers;` in DefOfs.cs) for the four in-crib activities, replacing per-think
`DefDatabase<ToddlerPlayDef>.GetNamed(...)` calls in the shared in-crib chooser. Field name =
defName. Code that still looks a ToddlerPlayDef up by string (DebugActions, the commented-out
Pillow Nesting block) is deliberately left alone.

### 2.8 Utility-class convention
Stateless cross-cutting logic lives in `public static class *Utility`
(`PlayUtility`, `TPE_ToyUtility`, `CribLockUtility`, `BubbleMoodUtility`,
`ChildCryTracker`, `DirtCastleUtility`, `PlayAnimationUtility`, `TPE_PlayUtility` inside
`ToddlerPlayGivers.cs`). These are
the correct place for shared eligibility checks — don't duplicate a check across
multiple `JobDriver`/`PlayDesireGiver` files. Exception that is still "shared": the
building gate `ColonyHasBuilding(ThingDef | List<ThingDef>)` is a `protected static` on
the `PlayDesireGiver` base class (only givers need it).

### 2.9 Settings: shared categories and optional IrisMenus

`ModSettings.cs` defines `TPE_SettingsTab`, `TPE_Settings` persistence and the `TPE_Mod` entry point. `SettingsPages.cs` owns seven category drawers: toddler moods, toddler activities, child moods, child play, plush sleep, presentation and debug. Every drawer edits the same `TPE_Mod.Settings` fields used by gameplay. Save keys, defaults and slider ranges are unchanged.

The standalone window lays out category buttons in width-aware rows and draws the selected category in one measured `Listing_Standard` scroll view. Switching categories resets its scroll. `SettingsCategory()` matches the mod name, "The Children Need To Play".

`IrisMenusIntegration.Register` runs after mod construction and checks the running package list for `Nanaloveyuki.IrisMenus`. Only its public API is resolved by reflection; there is no assembly reference, shipped provider DLL or hard mod dependency. Each stable page ID is registered with `RegisterSubItemListing` and `owner.WriteSettings`. Where available, `MenuPresentation.Listing` supplies native scrolling, themed controls and help overlays. Older providers keep ordinary categorized listings. The full standalone window remains usable without the provider.

The hosted plush-sleep page uses public `MenuItemControls.ChoiceSort` for adult mood selection. Its labels are refreshed on language changes; outside the native host the same setting uses the ordinary FloatMenu. No extra category navigation or nested scroll view is drawn inside IrisMenus.

Category titles and descriptions have English/Simplified Chinese keys. Upstream hardcoded English control labels remain English; this integration is not a complete translation.

---

## 3. Data flow — startup / load order

```
Game boot
  └─ Mod load order (About.xml loadAfter): brrainz.harmony → cyanobot.toddlers →
       (Homely.PillowTalk.WithEuterpe, if installed) → drati.ToddlersPlayExpanded.fork
  └─ Assemblies loaded; every Verse.Mod subclass constructed:
       TPE_Mod → GetSettings<TPE_Settings>() (settings live from here on)
               → BuildFingerprintCheck.RunIfDevMode(content)  (dev mode only: stale-DLL check, §3a.
                 Runs on the loading thread: file IO + hashing + Log only, no Unity API)
       Mod (legacy, Mod.cs) → does nothing
  └─ XML def loading:
       Defs/**/*.xml            → new ThingDef/JobDef/ThoughtDef/etc. definitions parsed
       Patches/**/*.xml         → applied to the combined XML BEFORE defs are built:
                                   ThinkTree_ChildPlay / ThinkTree_ChildPlayWhenIdle (vanilla
                                   ThinkTreeDefs), ToyBox_Storage (Biotech ToyBox),
                                   TraderKinds_BulkGoods_BuyToys (bulk goods traders)
  └─ Defs resolved, [DefOf] classes bound (12 TPE_*DefOf classes, §2.7)
  └─ [StaticConstructorOnStartup] pass (main thread, AFTER defs exist):
       HarmonyPatches cctor:
         new Harmony("drati.toddlersplayexpanded").PatchAll()
           → every [HarmonyPatch] class in the assembly (~85) applied to
             Assembly-CSharp + Toddlers.dll methods
         RegisterRetiredDefs()  → retired ThoughtDef names into vanilla's
             BackCompatibility.RemovedDefs (§5.2) — before any save can load
         plush developmentalStageFilter overwrite + PlushDecorativeGraphics cache (§5.1)
         HomelyCompat.Initialize()  → inert unless Homely is active
       ToddlerWobbleAccess cctor → binds Toddlers' private wobble fields (§5.3)
  └─ New game / load → GameComponent_PlayTracker + GameComponent_KnownColonists and all 8
       MapComponents instantiate (vanilla FillComponents, §2.5)
```
(Because patching happens in a `[StaticConstructorOnStartup]`, nothing this mod patches
runs during def loading — a patch on def-loading code would never fire.)

**Corollary:** because ThinkTreeDef patches are applied to the *parsed def
tree*, not runtime state, `JobGiver_ChildPlay` / `JobGiver_ChildPlayWhenIdle`
are permanently wired into every game's think tree the moment the mod is
active — the `TPE_Mod.Settings.enableChildrenPlayNeed` toggle is checked
**inside** `TryGiveJob`, not by conditionally patching. Same pattern for
every other feature flag: **the patch/insert is unconditional; the settings
check is the first line of the runtime method.** This is why toggling a
setting takes effect immediately without restarting — don't "fix" a
disabled feature by making the patch conditional; check inside the method
body instead, matching everything else in this codebase.

### 3a. Build ↔ source fingerprint (stale-DLL guard, v1.2.0)

**Why it exists:** the v1.1.x Workshop upload shipped an `Assemblies/` DLL built *before* the
Solar Flare retirement, next to newer `Source/` + `Defs/`. Every player got "Failed to find
RimWorld.ThoughtDef named TPE_Event_SolarFlare" (a DefOf field with no def). On top of that, the
power-outage rework, the held-toy tilt and the retired-def save cleanup never shipped at all.
Neither Modmixer's Publish nor RimWorld's uploader compiles anything:
`SteamUGC.SetItemContent` uploads the folder exactly as it is.

**How it works (two halves, one shared algorithm):**
```
build (build_mod / dotnet build)
  └─ csproj target TpeEmbedSourceFingerprint    BeforeTargets="CreateGeneratedAssemblyInfoInputsCacheFile"
       └─ inline task TpeComputeSourceFingerprint  RoslynCodeTaskFactory; its code IS Source/SourceFingerprint.cs
            │                                      (TPE_MOD_BUILD undefined there, so the task class compiles in)
            └─ SourceFingerprint.Compute(Source/) → [assembly: AssemblyMetadata("TPE.SourceFingerprint", "<hex>")]
game start (mod constructors, RimWorld's loading thread)
  └─ TPE_Mod ctor → BuildFingerprintCheck.RunIfDevMode(content)
       ├─ dev mode off / no Source/ (or no .cs in it) / no embedded value → return (silent)
       ├─ SourceFingerprint.Compute(RootDir/Source) == embedded  → return (silent)
       └─ otherwise → ONE Log.Error "[TPE] Out-of-date DLL: …"  (IO failure → ONE Log.Warning; never throws)
```
- **Fingerprint** = SHA-256 over the sorted list of (lower-cased relative path, SHA-256 of the file's
  bytes with every CR removed) for every `*.cs` under `Source/`, skipping `obj/`, `bin/` and
  dot-folders. Dropping CRs makes CRLF and LF copies hash the same, so line-ending conversion (git,
  ZIP downloads, editors) can't cause a false alarm, while any real code change still counts. `obj/` MUST stay
  excluded: it holds the generated AssemblyInfo.cs that *contains* the fingerprint, so including it
  would be circular. Contents and names both count, so edits, additions, deletions and renames all
  change the fingerprint. XML/Defs/csproj changes don't, because they aren't compiled into the DLL.
- **One file, compiled twice:** `SourceFingerprint.cs` is both the MSBuild task's source and part of
  the mod. `#if !TPE_MOD_BUILD` gates the task class (the csproj defines `TPE_MOD_BUILD` only for the
  mod compile). Keep that file BCL-only (no Verse/Unity/Harmony): the task compile only references
  netstandard + MSBuild, so a game type would break the build.
- **Incremental builds are safe.** The hook runs before the SDK hashes `@(AssemblyAttribute)`
  (including `%(_Parameter2)`) into its AssemblyInfo cache. So a changed fingerprint always
  regenerates AssemblyInfo, and an unchanged one leaves the build up to date. Verified offline:
  - edit a .cs + rebuild → new embedded value
  - revert + rebuild → the original value comes back (deterministic)
  - no-change rebuild → DLL not rewritten
  - an independent bash/sha256sum reimplementation gives the same hex
- **Cost:**
  - Build: a few ms of hashing, plus RoslynCodeTaskFactory compiling the task (~0.5 s per build).
  - Game: zero unless dev mode is on. Then it runs once at startup, about 5–50 ms for ~1.3 MB (a
    cold JIT/crypto init dominates), on the loading thread. It never runs per tick or per frame.
- **Who sees the error:**
  - The author, in every Modmixer test run (dev mode is on), if code was edited but not rebuilt.
  - Dev-mode players, if a stale build is ever published again.
  - Not the v1.1.x DLL already on the Workshop: it predates the guard, with neither the check code
    nor a fingerprint, so it can't flag itself. Re-uploading v1.2.0 is what fixes that copy.
- **Inspecting a shipped DLL:** the value is plain assembly metadata (`decompile_dll`, or PowerShell
  `[Reflection.Assembly]::LoadFile(dll).GetCustomAttributesData()`). Compare it with
  `SourceFingerprint.Compute(<Source dir>)`, invoked by reflection (the class is `internal`;
  `Assembly.GetType("ToddlersPlayExpanded.SourceFingerprint")` still finds it).
- **Don't:**
  - Move the hook later than `CreateGeneratedAssemblyInfoInputsCacheFile`. An incremental build would
    then keep a STALE fingerprint.
  - Change the metadata key on one side only (`SourceFingerprint.MetadataKey` ↔ the csproj
    `_Parameter1`).
  - Set `GenerateAssemblyInfo=false`. The attribute would silently vanish, and the check would
    become a silent no-op.

---

## 4. Data flow — the three job-dispatch loops

### 4.1 Toddler activity loop (base-mod-owned dispatch)
```
Toddlers' JobGiver_ToddlerPlay.TryGiveJob(toddler)
  → iterates DefDatabase<ToddlerPlayDef> in some priority/random order
  → for each: giver.CanDo(pawn) / CanDoFromCrib(pawn)   [OUR overrides live/gate here]
  → first eligible → giver.TryGiveJob(pawn) / TryGiveJobFromCrib(pawn)
  → returns a Job whose driver is one of OUR JobDriver_Toddler* classes
       (JobDrivers.cs — toils, e.g. JobDriver_ToddlerSpinning, _Jumping, _SelfSoothing...)
  → JobDriver runs its toils; per-tick play-need gain via tickAction on a toil
  → PlaySchedulingPatches gates schedule, assigned work, rest and health before selection
  → normal below-fullness requests keep the base implementation and its other patches
  → narrow full-bar prefixes preserve optional toddler play without rewriting IL literals
  → ToddlerPlayUtility.ToddlerPlayTickCheckEnd (HARMONY-PATCHED by us)
       decides whether to end the job — patched so job ends by DURATION
       for our activities, not "need already full" (base mod's default logic)
  → visuals: Harmony postfix on PawnRenderer.GetBodyPos (jump / crib-jump hop),
       AnimationDefs + AnimationWorkers (spin; reused base-mod Bugwatch tilt / crib
       wiggle via Enabled prefixes) — ALL gated by "Disable toddler play animations"
       (§5.3). (IsToddlerPlaying postfixes are NOT visual — they only make the base
       mod count our activities as "playing".)
```
**In-crib dispatch (and the sleep gate, v1.2.1).** The base mod's think tree reaches
`JobGiver_ToddlerPlayInCrib` from TWO places:
```
HumanlikeToddler think tree (Toddlers ThinkTrees.xml)
  ├─ ThinkNode_ConditionalMustKeepLyingDown   ← true while ASLEEP in bed/crib (or downed / medical rest)
  │    ├─ ThinkNode_QueuedJob (inBedOnly)
  │    ├─ JobGiver_ToddlerPlayInCrib          ← evaluated for SLEEPING toddlers, every 211 ticks
  │    │                                         (vanilla LayDown toil → CheckForJobOverride)
  │    └─ JobGiver_KeepLyingDown              ← "keep sleeping"
  └─ ThinkNode_ConditionalInCrib → PrioritySorter { GetRest, LeaveCrib, ToddlerPlayInCrib (8 / 2), IdleInCrib }

JobGiver_ToddlerPlayInCrib.TryGiveJob
  → shared prefix checks CanStartInCribPlay and the presence of a play need
  → disqualified: null, with fallback disabled
  → eligible and below 95%: original TV / Skydreaming implementation
  → eligible and full: same two workers through cached crib-job delegates
  → eligible but still null: one chooser tries SelfSoothing, CribJumping,
       CeilingWatching and SpeechPractice, retaining each worker's settings/cooldowns
```
The trap: base "null" also means "this toddler is asleep". Before v1.2.1 the postfixes read every
null as "base found nothing" and woke sleepers. The only thing still stopping them was the base
`CanDoFromCrib`'s `PawnUtility.WillSoonHaveBasicNeed(pawn, 0f)`, which is exactly `rest < 0.28`. So
a napping toddler was woken (mostly for Ceiling Watching: no cooldown, no crib requirement) within
211 ticks of rest passing 28%. It gained no rest while playing, so rest sank a little. When the
activity ended, `KeepLyingDown` put it straight back to sleep (below 60% a toddler falls asleep at
once), and the next re-think above 28% woke it again: stuck at ~28% forever.
Now a sleeping toddler sleeps until vanilla wakes it: the toddler life stage's
`naturalWakeThresholdOverride` is 1.0 (100%), or hunger / schedule / disturbance / danger. Vanilla's identical branch (`JobGiver_GetJoyInBed` in Humanlike's `MustKeepLyingDown`)
opens with the same `!pawn.Awake()` check.

Awake-but-tired and injured toddlers use the same policy as all other play selectors:
`HealthAIUtility.ShouldSeekMedicalRest` and `JobGiver_GetRest.GetPriority` must both
allow leisure. An autonomous running play job rechecks on staggered 50-tick intervals,
including travel; forced orders keep player precedence. In-crib drivers register
`KeepLyingDown` at driver level so saved jobs resume with the same finalizer.

Debug entry point: `TPE_DebugActions` → "Force play activity on toddler" (General
debug menu) lists EVERY registered `ToddlerPlayDef` (base mod's too) and starts the chosen
one on a clicked toddler. It bypasses the base mod's weighted pick and the play-need gate,
but it still REQUIRES `CanDo` (or `CanDoFromCrib` when in a crib) — an ineligible toddler
gets a "CanDo returned false" message instead of the job. It prefers `TryGiveJobFromCrib`
(found by reflection) in a crib and replicates the magnifying-glass upgrade for Bug
Watching (the normal path does that in a `JobGiver_ToddlerPlay` postfix).

### 4.2 Child play-desire loop (this-mod-owned dispatch, "normal" path)
```
Every 8 in-game hours (Pawn_PlayTracker.PlayDesireIntervalTicks = 20000; counted down
150 ticks per Need_ChildPlay.NeedInterval, so it pauses while the need is frozen or the
play-need system is off; first roll on the first interval after the tracker is created):
  Pawn_PlayTracker.RefreshDesires()
    → IF Settings.guaranteeActivePlayDesire: force-add TPE_ActivePlay as slot 1
         (if CanGiveDesire + age-eligible; else falls through to a normal roll
         for that slot too). Default OFF — original fully-random 2-slot roll.
    → remaining slot(s) via Pawn_PlayTracker.AddNewPlayDesire(), weighted by
         selectionWeight from DefDatabase<PlayDesireDef>, filtered by:
        - per-desire minAge/maxAge (biological years; only if Settings.ageGatedPlayDesires)
        - PlayDesireGiver.CanGiveDesire(pawn)  [pawn-aware building-gate check —
              TV/Radiotalking/BubblePopping require ColonyHasBuilding(...) to have
              found the relevant built ThingDef somewhere in the colony first]
        - PlayDesireDef.conflictingLearningDesire NOT in pawn.learning.ActiveLearningDesires
              [Learning/Play mutual exclusion — see §4.2b]
    → picks up to 2 → stored in Pawn_PlayTracker.ActivePlayDesires

Per child think-tree evaluation (MainColonistBehaviorCore's PrioritySorter):
  ThinkNode_Priority_ChildPlay.GetPriority is 8.5 normally, or 9.15 when play < 30%
    and below Learning. Both tiers use PlayScheduleUtility.CanSelectChildPlay:
    setting enabled, Biotech active, tick >= 5000, Child stage, play need below 95%,
    and shared schedule/work/medical/rest eligibility. Anything and Recreation are
    the only allowed assignments; Work, Sleep, Learning and custom schedules return 0.
    Existing assigned labor is protected under Anything, including prisoners without
    a timetable. Recreation may supersede ordinary work; player-forced jobs stay protected.
    If no desire can produce a job, the sorter falls through normally.
  ThinkNode_Priority_ChildPlay → JobGiver_ChildPlay.TryGiveJob(pawn)
    → TPE_Mod.Settings.enableChildrenPlayNeed gate
    → PlayScheduleUtility.CanSelectChildPlay(pawn)
    → reads tracker.ActivePlayDesires, shuffles, tries each:
         desire.Giver.CanDo(pawn) → desire.Giver.TryGiveJob(pawn)
    → returns Job → JobDriver_Child* (JobDriver_ChildPlay.cs / _ChildActivePlay.cs /
         _ChildBubblePopping.cs / _ChildBuildDirtCastles.cs / _ChildWatchTelevision.cs)
    → PlayUtility.PlayTickCheckEnd ends satisfied/expired sessions
    → JobDriver.DriverTick checks running/traveling registered play jobs every 50 ticks;
         idle play yields to any prioritized job, ordinary play to higher priorities
```
**Reading (`PlayDesireGiver_ReadBook` → `JobDriver_ChildReadBook`) specifics:**
the book is picked by vanilla `BookUtility.TryGetRandomBookToRead`, which
returns loose/stockpiled books AND books held (UNSPAWNED) inside a
`Building_Bookcase`. The driver therefore mirrors vanilla `JobDriver_Reading`:
fixed 5-toil layout `JumpIf(already carrying) → GotoThing(A, Touch,
canGotoSpawnedParent:true) → StartCarryThing(canTakeFromInventory:true) →
CarryToReadingSpot (initAction picks chair/stand spot) → read (Wait,
tickIntervalAction, Book.IsOpen while reading)`, plus a `SetFinalizerJob`
that hauls the book back to storage on success (drop + unforbid otherwise).
The giver hands CanDo's chosen book to TryGiveJob via a one-shot
same-pawn/same-tick static cache (one vanilla book scan per request, not
two). See §6 for the bug this replaced.

Debug entry point: `TPE_DebugActions` → "Force play desire on child" REPLACES the clicked
child's active desire list with just the chosen desire, bypassing `CanGiveDesire`, age
gating and the Learning-conflict check (explicit design decision — QA-only). The job is
still handed out by the normal `JobGiver_ChildPlay` path, so `CanDo` and the schedule /
play-need gates above still apply. The forced list lasts until the next 8 h refresh.

### 4.2a Dirt castles (`TPE_BuildDirtCastles`) — setting, "only on full completion", "never override buildings/crops"
Files: `PlayDesireGiver_BuildDirtCastles` (PlayDesireGiver.cs, picks the cell) →
`JobDriver_ChildBuildDirtCastles` (walk → ~3h play toil → castle spawned by a
DRIVER-level finish action) → `Building_DirtCastle` (the Thing; label "<Name>'s Castle!",
3-day `CompProperties_Lifespan`). Shared rules live in **`DirtCastleUtility`** — both the
giver and the driver call it, so there is exactly one definition of "a cell a castle may
occupy" (§2.8: don't duplicate a check across giver + driver).

- **Setting `showDirtCastles`** (`TPE_Settings`, default ON, Presentation category;
  keys `TPE_Settings_ShowDirtCastles_*`).
  Read via `DirtCastleUtility.CastlesEnabled` (null-safe, defaults ON). OFF ⇒ the child
  still walks out, plays the full session and gains Play, but `SpawnDirtCastle` returns
  on its first line — **no castle Thing is ever created**, so OFF costs nothing (no
  ticker, no comp, no map-mesh print). **Only affects castles built AFTER the toggle**
  (user-confirmed): castles already on the map are left alone and expire on their own
  lifespan. Runtime check, never a conditional patch (§3).
- **A castle appears ONLY when the session fully completes (user-confirmed).** The spawn
  is registered with `JobDriver.AddFinishAction(Action<JobCondition>)` at the top of
  `MakeNewToils` — a *driver*-level action that is told HOW the job ended (a toil-level
  `Toil.AddFinishAction` is not, which is why the old code spawned a castle on every
  interruption) — and only acts on `JobCondition.Succeeded`. Verified against vanilla,
  `Succeeded` covers exactly: (1) the play toil's timer ran out (all toils done);
  (2) `PlayUtility.PlayTickCheckEnd` ended the job because the child's Play need is
  satisfied (child is done playing); (3) the engine's own `expiryInterval` expiry
  (`Pawn_JobTracker`, `EndCurrentJob(JobCondition.Succeeded)`). Everything that
  interrupts — hunger/sleep/bedtime taking over, being downed, dying or despawning
  (`Pawn.DeSpawn` calls `jobs.StopAll()` ⇒ `InterruptForced`), mental breaks, player
  orders — leaves NO castle. Known, accepted quirk of (3): the giver sets
  `expiryInterval = 8000` and the expiry only fires on the pawn's 8000-tick hash cadence at
  or after start+8000; a long walk (>500 ticks) can therefore, rarely (<≈6%), end the job
  up to ~500 ticks short of the full 7500-tick session — still reported (and counted) as
  `Succeeded`. Not worth special-casing; if it ever matters, track completion with an
  explicit flag instead of the JobCondition. The job itself is registered once per driver
  instance (`SetupToils` runs `MakeNewToils` once; it clears `toils` but not
  `globalFinishActions`, so never call it twice on one instance).
- **"Can't override buildings or crops" (user-confirmed scope) — why it was needed:** the
  castle def is an edifice-style Building and `GenSpawn.Spawn(…)` defaults to
  `WipeMode.Vanish`; per vanilla `GenSpawn.SpawningWipes` that DESTROYS plants in the
  cell and any other edifice. The old code only checked `Standable`, which lets sown
  crops (`Plant_*` default to `Standable`), growing-zone land, and non-blocking buildings
  through. Now:
  - `DirtCastleUtility.IsNaturalOutdoorCell` — bounds, unroofed, `Standable`, natural
    terrain (not water / bridge / `layerable` flooring). The rules the giver always had.
  - `DirtCastleUtility.IsCellFreeOfBuildingsAndCrops` — a cell is rejected if it is a
    `Zone_Growing` cell (crop land, planted or fallow — a castle would block sowing for
    its lifespan), holds a colony-sown plant (`Plant.sown`, the same flag vanilla's
    `DropCellFinder.CellHasCrops` uses; kept even outside a zone), or holds ANY
    `ThingCategory.Building` thing (buildings, construction frames, traps, conduits,
    other castles, …). Self-protecting against out-of-bounds/null map (it uses the
    unchecked fast grid accessors).
  - **Deliberately NOT protected (user: "grass and stuff like that is fine"):** wild
    plants (`sown == false`), items, filth, blueprints, non-growing zones (stockpiles),
    pawns, motes. Consequence to remember: the castle spawn WILL wipe wild plants and
    filth in its cell (default `WipeMode.Vanish`, same as any vanilla building placed on
    grass) — intended. If stockpile cells / items ever need protecting, add them in
    `IsCellFreeOfBuildingsAndCrops` (a castle blocks storage in its cell for 3 days).
  - `DirtCastleUtility.CanSpawnCastleAt` = both. Used as the **final authority at spawn
    time** (the world changes during the 3h session — floor laid, building placed, cell
    sown / zoned for growing, roof built) and it runs BEFORE `ThingMaker.MakeThing`, so a
    refused spawn allocates nothing. If refused the castle is simply skipped (no
    relocation). It also makes the spawn idempotent: a second castle would hit the first
    (a PassThroughOnly Building ⇒ `Standable` fails).
  - The giver applies the "no building/crop" half **only when castles are ON**
    (`protectBuildingsAndCrops`, read once per search): OFF ⇒ nothing is spawned ⇒
    nothing to override ⇒ children keep the full pool of play spots. The giver also runs
    the cheap grid checks BEFORE the expensive `CanReserveAndReach` (old order did
    reachability first).
- **Vanilla parity check:** compared with `GenSpawn.CanSpawnAt` (Walkable ✓, terrain
  affordance, interaction cells) and the closest vanilla analogue, `JoyGiver_BuildSnowman`
  / `JobDriver_BuildSnowman` (validates its cell only when picked — no edifice, standable
  3×3, unreserved — and spawns with default wipe; no spawn-time re-check, so it too can
  wipe plants). Terrain affordance and interaction cells are n/a: `TPE_DirtCastle`
  declares no `terrainAffordanceNeeded` and has no interaction cell, so vanilla's
  `CanBuildOnTerrain` is a no-op for it.
- **Perf trap worth knowing:** `CellFinder.TryFindRandomCellNear(…, maxTries: -1)` does
  20 random probes and then falls back to an EXHAUSTIVE shuffled sweep of the whole
  square (radius 20 ≈ 1.7k cells, radius 60 ≈ 14.6k) calling the validator on each — so
  validator cost and check ORDER matter (cheapest first, reachability last), and a failed
  `CanDo` is the expensive case. `CanDo` and `TryGiveJob` each run the search (no cache
  like the Reading giver has) — a possible future optimization, not done here.

### 4.2b Play Desire ↔ Learning Desire mutual exclusion (user-requested, cuts across 4.2)
A child must never simultaneously hold the same ACTIVITY as both an active Play
desire and an active vanilla Learning desire (e.g. "floor drawing" as both).
Data side: `PlayDesireDef.conflictingLearningDesire` (optional `LearningDesireDef`
reference, XML-only via `<conflictingLearningDesire MayRequire="Ludeon.RimWorld.Biotech">`
since the field type only exists with Biotech active) — set on exactly 5 of the
10 Play Desires that share an activity identity with a vanilla `LearningDesireDef`
(confirmed independently by both matching defNames AND matching `iconPath`s —
these 5 Play Desires already reused the vanilla Learning icon texture paths
before this feature existed): FloorDrawing, NatureRunning, Radiotalking,
ReadBook↔Reading, Skydreaming. The other 5 Play Desires (PlayWithToy,
BuildDirtCastles, WatchTelevision, ActivePlay, BubblePopping) and the other 2
Learning Desires (Lessontaking, Workwatching) have no counterpart and are never
restricted.

Enforced from BOTH directions, independently, each checking the OTHER
tracker's LIVE state at its own selection instant:
  - **Play excludes Learning**: `Pawn_PlayTracker.AddNewPlayDesire`'s own filter
    reads `pawn.learning.ActiveLearningDesires` directly (vanilla field on
    `Pawn`, not `Pawn.needs.learning` — easy to get wrong).
  - **Learning excludes Play**: `Patch_Pawn_LearningTracker_AddNewLearningDesire_ExcludeActivePlayDesires`
    in HarmonyPatches.cs, a full-body Prefix replacement of vanilla
    `Pawn_LearningTracker.AddNewLearningDesire` (small method, one LINQ
    statement + 2-line FIFO update — reimplementing verbatim plus one extra
    `Where()` clause beats transpiling the LINQ predicate's IL). Reads the
    Play tracker via `GameComponent_PlayTracker.TryGetTracker` (NON-creating
    peek — `GetTracker`, which lazily creates, would spuriously allocate + save
    an empty tracker for every child the moment their Learning desire happens
    to roll, even with the whole play-need system OFF).

**Same-tick race safety**: both directions are correct even if a pawn's Play
refresh (`Need_ChildPlay.NeedInterval`, every 150 ticks) and Learning refresh
(`Pawn.TickRare`, every 250 ticks) expire on the exact same absolute game
tick, because RimWorld ticks are processed strictly sequentially — never
concurrently. Whichever refresh runs second on that tick reads the other's
already-applied, fully-current result, never a stale snapshot. No locking,
ordering, or "pending pick" bookkeeping is needed beyond "always read live
state, never cache it across the check".

### 4.3 Child "Play when Idle" loop (opt-in, different think-tree branch — DO NOT conflate with 4.2)
```
Patches/ThinkTree_ChildPlayWhenIdle.xml inserts JobGiver_ChildPlayWhenIdle
as a sibling BEFORE JobGiver_WanderColony, inside Humanlike's
"idle colonist" ThinkNode_Tagger — this branch is only reached when
NOTHING else in the entire think tree produced a job (no food/rest/chemical
need, no work, no learning desire, no normal play-desire job from §4.2).

JobGiver_ChildPlayWhenIdle.TryGiveJob(pawn)
  → TPE_Mod.Settings.playWhenIdle gate (default OFF)
  → schedule guard: refuses during TPE_Learning and Work-scheduled hours
  → tries pawn's 2 active rolled desires first (already building-gated at roll time)
  → falls back to EVERY registered PlayDesireDef (re-checking CanGiveDesire
       per-desire — fallback pool can never exceed what could've been rolled)
  → if truly nothing satisfiable → returns null → tree falls through to
       JobGiver_WanderColony (wandering is the genuine fallback, never replaced)

Companion mechanisms REQUIRED for this loop specifically (do not rip out
without checking all four together):
  1. PlayUtility.PlayTickCheckEnd checks `pawn.CurJob?.jobGiver is
     JobGiver_ChildPlayWhenIdle` (engine-native Job.jobGiver field, set by
     Pawn_JobTracker.StartJob) to treat idle-triggered jobs like `forced`
     calls — otherwise a job handed to an already-100%-satisfied child
     ends on its first tick.
  2. Duration caps added to jobs that previously relied ENTIRELY on
     PlayTickCheckEnd to ever terminate (JobDriver_ChildNatureRunning's
     find→walk→wait Toils_Jump loop had no cap of its own).
  3. PlayUtility.CheckForBetterJobWhileIdlePlaying — ~50-tick interrupt
     check via Pawn_JobTracker.CheckForJobOverride(minPriority: 0.01f),
     called from inside PlayTickCheckEnd, so every play JobDriver gets it
     for free. Lets hunger/tiredness/learning/work reclaim the pawn quickly
     without idle-tier nodes (WanderColony/IdleJoy/ChildPlayWhenIdle itself)
     ever self-interrupting.
  4. BUGFIX (caught auditing the §4.2b mutual-exclusion feature): the
     "try EVERY registered PlayDesireDef" fallback pool above bypasses the
     tracked ActivePlayDesires list entirely and hands out a ONE-OFF job
     directly — it must independently re-check
     `PlayUtility.ConflictsWithActiveLearningDesire(desire, pawn)` (gated
     behind the same `checkCanGiveDesire` flag as the `CanGiveDesire`
     re-check, since the "active desires" pool was already exclusion-
     checked at roll time). Without this, an idle child could still be
     sent to literally DO an activity (e.g. Floor Drawing) as a one-off
     idle-play job while ALSO currently holding it as an active vanilla
     Learning desire — exactly what §4.2b exists to prevent — even though
     that one-off job is never added to the tracked list itself. Vanilla's
     own `JobGiver_Learn` has no equivalent "try everything" fallback (only
     ever tries the tracked 2), so there's no symmetric gap on the
     Learning side needing the same treatment.
```

### 4.4 Recreation vs. TPE_Learning schedule exclusivity (cuts across 4.1–4.3)
`HarmonyPatches.cs`'s "TPE_LEARNING ASSIGNMENT BEHAVIOR" patch group is the
single choke point enforcing: Recreation time → Play always wins over vanilla
Learning (Learning's `GetPriority` forced to 0 during Recreation when the
play-need system is on); `TPE_Learning` time → Work forced to priority 0 for
enrolled children (hard block, not an out-priority), Play explicitly
suppressed. `Patch_JobGiver_Learn_TryGiveJob_BlockDuringRecreation` is a
defense-in-depth backstop, not the primary mechanism — if schedule bleed-through
bugs show up, check the priority-forcing patches FIRST, the backstop SECOND.

---

## 5. Cross-cutting / independent subsystems (no shared dispatch loop)

These don't participate in §4's loops — they're their own tick/event chains.
Listed so you know where to look without re-deriving from scratch:

| System | Owner(s) | Trigger |
|---|---|---|
| Crib Lock | `Patch_Pawn_GetGizmos_CribLock` ("Lock in Crib" gizmo on the toddler), `Hediff_TPE_CribLocked`, `ThoughtWorker_LockedInCrib`, `MapComponent_ToddlerCribReturn` + `JobDriver_TPE_ReturnToddlerToCrib`, `WorkGiver_TPE_GiveToyToLockedToddler` + `JobDriver_TPE_GiveToyToLockedToddler`, `Patch_JobGiver_LeaveCrib_*`, `Patch_BabyMoveUtility_ShouldBeInBed`, `Patch_BabyPlayGiver_PlayWalking/PlayToys_CanDo`, `Patch_FloatMenuOptionProvider_LetOutOfCrib_*`, the "COMPREHENSIVE TV BLOCKING" patches | Player toggles the lock on a toddler. Locked toddlers can't self-escape, can't be carried off by adult play givers or the "Let out of crib" order, never get TV; if out (feeding, rescue) a MapComponent sends a colonist to return them; a Childcare WorkGiver (priority 82) brings them a plush. Lock auto-removed on age-up (`MapComponent_ToddlerSleepTracker`). Feeding is vanilla/Toddlers. |
| Crib confinement ("wants out") | `MapComponent_ToddlerSleepTracker` (adds `Hediff_TPE_CribConfinement`), `Hediff_TPE_WantsOutOfCrib`, `MentalState_WantsOutOfCrib`, `MapComponent_ToddlerCribRescue`, crib-restless thoughts in `ThoughtDefs_Toys.xml`, setting `enableConfinementPriorityScaling` | Toddlers in cribs accrue confinement → restless/frustrated → "wants out" mental state; `MapComponent_ToddlerCribRescue` dispatches a colonist to let them out (bypasses work settings). NOT `WorkGiver_TPE_LetToddlerOutOfCrib` — that class has no def (dead, §6). |
| Crib Gate | `Patch_Building_Bed_GetGizmos_CribGate` ("Open Crib Gate" gizmo on cribs), `MapComponent_CribGate` (saved per-crib state), `Patch_JobGiver_LeaveCrib_GetPriority_CrawlerGateOverride` | Open gate ⇒ crawlers may leave the crib like walkers (base mod blocks crawlers); sleeping or locked toddlers still can't |
| Toy Box / Toy Workbench | `Patches/ToyBox_Storage.xml` + `Building_ToyBox`, `TPE_ToyWorkbench` + `TPE_DoBillsMakeToys` (Crafting WorkGiver) + `Recipes_Toys.xml` (8 recipes), `CompToyCategory`, `FloatMenuOptionProvider_ToddlerToys`, `Patch_Pawn_ApparelTracker_TryDrop_UnforbidPlush` / `Patch_Pawn_CarryTracker_TryDropCarriedThing_UnforbidToy` | Crafting bill at the workbench → hauled into Biotech's vanilla Toy Box, which the patch turns into a `Building_Storage` subclass (Toys only, 25/cell, swaps to an "empty" texture when nothing is stored) → toddlers fetch toys for activities and plushes at bedtime; dropped toys are always unforbidden. Prices, "only bulk goods traders buy toys", and the 45° held sword/glass: **§5.4** |
| Plush-to-bed sleep system | `JobDriver_SeekPlushToy`, 4 sibling `Patch_JobDriver_LayDown_SetupToils*` redirect patches, `Patch_Toils_LayDown_ApplyBedThoughts`, `TPE_ToyUtility` | See §5.1 below — four independent age brackets (toddler / child 3–6 / child 7–12 / adult 13+) share one seek-and-equip job but each has its own Harmony redirect patch, own settings toggle, and (adults only) a 4-way mood-mode dial |
| Event-Triggered Moods | `MapComponent_ToddlerEventMoods` + `GameComponent_KnownColonists` + Harmony patches on `IncidentWorker.TryExecute`, `Pawn.Kill` (prefix+postfix), `Pawn_AgeTracker.BirthdayBiological`, `InteractionWorker_RecruitAttempt.DoRecruit` (6-param overload) | **Full rules + per-mood trigger table: §5.2.** Four categories, never mixed for one ThoughtDef: ONGOING (polled; refreshed on awake toddlers, removed from ALL spawned toddlers when it ends), LINGERING (Burial + the three "witnessed" moods — refreshed while active, never force-removed), ONE-TIME polled edges (RaidWon, NewColonist, injury, rescue, corpse/blood sightings, homesick), ONE-TIME Harmony sources (incidents incl. disease outbreak, colonist death, birthday, animal tamed). |
| Random hourly moods | `MapComponent_ToddlerRandomMoods` | Hourly roll per awake toddler (`randomMoodFrequency`, cap `randomMoodMaxActive`), 100 moods in 5 weighted sets (20 each) |
| Child crying / tantrums | `ChildCryTracker`, `GameComponent_ChildCryTracker`, `MentalState_ChildCry`, `CryingPatches.cs` | Ages 3–6 use the original toggle; ages 7–13 additionally require Child stage and the default-off older toggle. Older bouts last 600/300 ticks and apply half-strength aura thoughts. Each parent's response is rolled once per bout and saved in the state; ignored parents skip this cry's clamor/mood and comfort targeting, and the child receives one −5 mood memory for 0.5 days. Severe accepted/renewed memories trigger directly; situational thoughts are scanned every 600 ticks with reused buffers. |
| Bubble Machine + mood | `CompBubbleZone`, `Mote_Bubble`, `BubbleMoodUtility`, `BubbleSettings`, `ThoughtWorker_Bubble*` | Building needs power (20 W) AND chemfuel (`CompRefuelable`, 2.25/day, burns only while powered) and must be switched on (`CompBubbleZone.IsMachineActive`) → spawn timer → motes spawn/drift/despawn inside a cached zone (global cap `maxBubblesPerMap`) → ThoughtWorkers check `BubbleMoodUtility.IsInActiveBubbleZone` per pawn (babies/children joy, adults annoyed). Toddler activities Popping/Chasing Bubbles + child desire Bubble Popping use the zone. |
| Homely compat | `HomelyCompat.cs` (reflection only, no compile-time ref) | Both Homely active + plush-to-bed setting on → tags plush apparel + steps aside for `JobDriver_UndressBeforeBed` |
| God-mode "Lock Play Need at 50%" | `Patch_Pawn_GetGizmos_LockPlayNeed`, `Patch_Need_Play_NeedInterval_LockAt50`, `Need_ChildPlay` direct checks, `Pawn_PlayTracker.lockPlayNeed` | Gizmo visible only when `DebugSettings.godMode`; toggling clamps the relevant Need every interval tick. (Separate feature: the "Toggle God Mode: Max Learning / Max Play" debug actions, enforced by `MapComponent_GodModeNeeds` every 60 ticks.) |
| Room Role: Playground + classroom buff | `RoomRoleWorker_Playground`, `RoomRole_Playground.xml`, `TPE_PlayedInPlayground` (child play drivers, room role check), `TPE_LearnedInClassroom` (`Patch_LearningUtility_LearningTickCheckEnd` + `Patch_JobDriver_Lessontaking_MakeNewToils` backup) | Vanilla room-role scoring; finishing play in a Playground / a lesson in a Classroom gives a mood |
| "Learning" schedule + growth blending | `TimeAssignment_Learning.xml` (`TPE_Learning`), `Patch_TimeAssignmentSelector_DrawGrid_ExtraButton`, the "TPE_LEARNING ASSIGNMENT BEHAVIOR" priority patches (§4.4), `Patch_Pawn_AgeTracker_GrowthPointsPerDay`, `Patch_Need_Learning_NeedInterval`, `Patch_LearningUtility_LearningTickCheckEnd_SuppressDuringPlay`, `Patch_Gizmo_GrowthTier_*`, `Patch_JobGiver_IdleJoy_TryGiveJob` | Adds a 6th schedule button. Growth points = blend of Learning and Play (`playWeightInGrowth`); Learning decays at the play-decay rate; play jobs don't fill Learning; the growth-tier gizmo gets a Play bar + tooltip |
| Child Play need lifecycle | `Patch_Pawn_NeedsTracker_AddOrRemoveNeedsAsAppropriate`, `Need_ChildPlay`, `ThoughtWorker_NeedChildPlay` / `_ChildPlayHigh`, `Patch_ThoughtUtility_NullifyingTrait_PlayMood` | The NeedDef has no stage filter; the postfix adds it to child colonists only while `enableChildrenPlayNeed` is on (and de-duplicates — resurrection bug), removes it when off. Psychopath nullification of play moods is optional (`keepPlayMoodForPsychopaths`) |
| Auto-dress toddlers | `WorkGiver_DressToddler` (`TPE_DressToddler`, Childcare priority 75) | Adults put baby onesie / tribal babywear on toddlers wearing none (vanilla `ForceTargetWear`) |
| Toddler age-up cleanup | `Patch_LifeStageWorker_HumanlikeChild_Notify_LifeStageStarted` | On Baby→Child, cancels/dequeues toddler-only play jobs |
| Play animation toggles | `PlayAnimationUtility`, `TPE_Settings.disableToddlerPlayAnimations` / `disableChildPlayAnimations`, the animating job drivers, the animation workers, `Patch_PawnRenderer_GetBodyPos`, the Bugwatch/WiggleInCrib `Enabled` prefixes | See §5.3 — two cosmetic settings (default OFF); gated where an animation is STARTED (drivers) and where it is RENDERED (workers / bounce patch) |

### 5.1 Plush-to-bed sleep system — four parallel age brackets, one shared job

Four age brackets can autonomously fetch and wear a plush toy before sleeping,
each independently toggleable:

| Bracket | Age | Setting | Rest-give-up threshold | Mood ThoughtDefs |
|---|---|---|---|---|
| Toddler | ~1–3 (`ToddlerUtility.IsToddler`) | always on | ≤15% | `TPE_SleptWithPlushToy` / `TPE_NoSoftCuddleBuddy` (+2/−4, unconditional) |
| Child Group 1 | 3–6 (`DevelopmentalStage.Child`) | `enableChildPlushSleep` (default ON) | ≤25% | `TPE_ChildSleptWithPlushToy` / `TPE_ChildNoPlushToy` (+2/−4) |
| Child Group 2 | 7–12 (`DevelopmentalStage.Child`) | `enableOlderChildPlushSleep` (default ON) | ≤25% (see BUGFIX below — was 30%, functionally dead) | same ThoughtDefs as Group 1 (reused, not duplicated) |
| Adult | 13+ (`DevelopmentalStage.Adult` — includes the Teenager life stage; see below) | `enableAdultPlushSleep` (default OFF) | ≤25% (see BUGFIX below — was 35%, functionally dead) | `TPE_AdultSleptWithPlushToy` / `TPE_AdultNoPlushToy` (+2/−4), each independently gate-able by `adultPlushieMoodMode` (`None`/`NegativeOnly`/`PositiveOnly`/`Both`) |

**BUGFIX (user-reported: adults/7–12 got the mood but never attempted to fetch a
plush at all):** the Child Group 2 and Adult rest-give-up thresholds originally
shipped at 30%/35%, chosen to express "more mature brackets give up on fetching
a plush sooner." This was wrong in a way that silently disabled the redirect
entirely for both brackets. Verified against decompiled vanilla source:
`JobGiver_GetRest.GetPriority` only assigns a LayDown job outside a scheduled
Sleep block once `rest.CurLevel < 0.3f` to begin with (`Need_Rest.CurCategory`
doesn't even leave `RestCategory.Tired` for `Rested` until 0.28) — so by the
time `SetupToils` runs, a non-scheduled bedtime's rest is already guaranteed to
sit just under 0.30. A 0.30/0.35 "give up if rest ≤ threshold" check therefore
evaluated true on almost every real bedtime, and the redirect never fired. The
toddler (0.15) and Child Group 1 (0.25) thresholds happened to sit safely below
this ~0.30 ceiling, which is why only those two brackets worked. Fix: both new
brackets now share the proven-working 0.25 value. A meaningful "increasingly
relaxed with age" gradient genuinely does not fit in the sliver of safe numeric
space between 0.25 and the ~0.28–0.30 ceiling — don't reintroduce higher
values here without re-deriving this exact math first.

**NEW SETTING (`alwaysSeekPlushRegardlessOfRest`, default OFF):** lets the
player bypass all four brackets' rest-give-up checks above entirely —
`TPE_Mod.Settings?.alwaysSeekPlushRegardlessOfRest != true &&` is prepended
to each of the four `if (pawn.needs?.rest?.CurLevel <= threshold) return
true;` lines. When ON, a pawn will always detour to fetch a plush before
sleeping no matter how low their rest already is, at the cost of a slightly
later bedtime every time. Deliberately does NOT touch the other guards in
these same patches ("already in bed", "already has a plush", redirect
cooldown) — only the rest-percentage safety valve is skippable. Lives next
to `renderPlushWhileSleeping` on the Toddlers/Newborns settings tab and
shares its "applies to all four brackets uniformly" scope.

**BUGFIX (developmentalStageFilter startup override silently blocked adult
equip — found immediately after the threshold fix above, same live-test
session):** even after the rest-threshold fix made the Adult redirect fire
correctly (pawn walks to a plush, picks it up), the equip toil in
`JobDriver_SeekPlushToy` still rejected it every time — logged as `"cannot
wear TPE_Plush* (filter: Baby, Child)"` — and dropped it on the floor. Root
cause: a `[StaticConstructorOnStartup]` block at the very top of
`HarmonyPatches.cs` (predates this feature — originally added to fix a
different, older bug where the plush ThingDefs' XML `developmentalStageFilter`
was "somehow resolving to just `Child`" at runtime) unconditionally
OVERWRITES `def.apparel.developmentalStageFilter` on every `TPE_Plush*`
ThingDef at startup, AFTER Defs finish loading. That hardcoded value was
still the old two-stage `Baby | Child` — it had never been updated when this
feature widened the actual XML (`ThingDefs_Toys.xml`) to `Baby, Child, Adult`,
so the startup hook silently stomped the XML edit back to the pre-feature
value on every single game launch, making the XML change completely inert.
Fix: updated the hardcoded `correctFilter` constant in that static
constructor to `DevelopmentalStage.Baby | DevelopmentalStage.Child |
DevelopmentalStage.Adult`, matching the XML. **Both the XML and this
hardcoded fallback must be kept in sync going forward** — this class of bug
(one source of truth silently overriding another with a stale value) has now
bitten this exact field twice; if a future dev ever widens the filter again
(e.g. adding a 5th bracket), grep for `TPE_Plush` in `HarmonyPatches.cs`'s
static constructor as well as the XML, not just the XML. Also fixed in the
same pass: the constructor's own debug-log line was reading
`def.apparel.developmentalStageFilter` for its `"(was X)"` diagnostic
AFTER already overwriting the field earlier in the same statement block,
so it could never actually report what the value used to be — it now
captures the pre-overwrite value into a local first.

**Why 13 is "adult":** `LifeStageDef.developmentalStage` defaults to `Adult`
in the base class, and vanilla's `HumanlikeTeenager` LifeStageDef (min age 13,
from `Races_Humanlike.xml`'s `lifeStageAges`) never overrides it away from
that default — only the *label* changes from "teenager" to "adult" at 18, not
the underlying `DevelopmentalStage` bucket. So `pawn.DevelopmentalStage ==
DevelopmentalStage.Adult` is true starting at 13, which is exactly the
bracket boundary this feature (and the user request that drove it) intends.

**Mechanism, mirrored per bracket:** each bracket gets its OWN Harmony Prefix
on `JobDriver.SetupToils` (`Patch_JobDriver_LayDown_SetupToils` for toddler,
`..._Child` for Group 1, `..._OlderChild` for Group 2, `..._Adult` for adults)
rather than one shared parameterized patch — this deliberately follows the
codebase's existing toddler/child duplication precedent (see §2.1) instead of
branching inside a single method, so touching one bracket's thresholds/guards
can never regress another's. All four redirect to the same
`TPE_JobDefOf.TPE_SeekPlushToy` job (`JobDriver_SeekPlushToy.cs`) with the
original bed as `targetB`; the job walks to the nearest reachable plush,
picks it up, equips it as apparel, then re-queues the original sleep job.
Mood application is centralized (unlike the redirects): a single
`Patch_Toils_LayDown_ApplyBedThoughts` postfix resolves which bracket a
sleeping pawn belongs to via `TryResolvePlushMoodBracket` (`internal`, also
reused by the "Test plush thought" debug action so the tool can never drift
out of sync with production behavior) and grants the matching thought pair.

**BUGFIX (save-load restore guard, applies to ALL FIVE SetupToils patches —
the 4 redirect prefixes + `Patch_JobDriver_SetupToils_AddPlush`):** vanilla
calls `SetupToils` not only from `StartJob` but also from
`JobDriver.ExposeData` during `PostLoadInit`, to rebuild the toil list of a
job that was in progress at save time — BEFORE the pawn is spawned. User
report: loading a save with a sleeping 3–6 child (no plush) logged "Could not
do PostLoadInit on RimWorld.JobDriver_LayDown: NullReferenceException …
GenClosest.EarlyOutSearch … FindNearestPlush", then "tried to get CurToil
with curToilIndex=2 but only has 0 toils", and the child woke up. (`InBed`
reads false for an unspawned pawn, so the redirect fell through to the plush
search on a null map; the exception aborted SetupToils.) Every patch now
bails first via `TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned(driver)`
(`Scribe.mode != Inactive` OR pawn unspawned/map-less) — placed right after
the cheap type check (toddler/AddPlush) or the setting check (3 other
brackets). `TPE_ToyUtility.FindNearestPlush` also returns null for an
unspawned pawn as a second line of defense.
Related: the in-crib toddler equip used to INSERT an extra toil into LayDown,
which made a crib-sleeping toddler's saved `curToilIndex` (3) out of range
after load (rebuilt list = 3 vanilla toils) → same error, toddler wakes. It is
now a `Toil.AddPreInitAction` on the existing toil (toil COUNT always equals
vanilla's). `ClampLegacyRestoredToilIndex` (same patch, PostLoadInit only,
via cached `AccessTools.FieldRef`s) maps an out-of-range restored LayDown
index back onto the last toil (LayDown) so saves made BEFORE this fix also
load cleanly. Never re-introduce `toils.Insert` into a vanilla driver.

**BUGFIX (apparel-layer conflict guard, applies to ALL brackets):**
`Pawn_ApparelTracker.Wear(dropReplacedApparel:false)` — used by every plush
equip site in this mod, specifically to avoid dropping the pawn's real gear
on the floor — silently calls `Remove()` on any apparel already worn on a
conflicting layer. `Remove()` only detaches the item from `WornApparel`; it
is never spawned on the map or placed in any container, i.e. it is silently
deleted from the save. Toddlers/children essentially never wear anything
else on the plush's "Belt" layer, so this never surfaced before, but an
adult wearing a shield belt (also Belt-layer) hits it constantly.
`TPE_ToyUtility.HasConflictingWornApparel(pawn, candidateDef)` (wraps vanilla
`ApparelUtility.CanWearTogether`) is now checked in THREE places: before
queueing the Adult/OlderChild redirect (avoids a doomed round-trip walk,
repeated forever every sleep cycle), and inside both plush-equip toils
(`JobDriver_SeekPlushToy`'s `equipPlush`, and the in-crib toddler
equip pre-init action in `Patch_JobDriver_SetupToils_AddPlush`) as the
final safety net. On conflict, the pawn simply sleeps without a plush that
cycle — exactly as if none had been found.

**BUGFIX (vanilla auto-dress AI):** `Patch_JobGiver_OptimizeApparel_ExcludePlush`
(postfix on `JobGiver_OptimizeApparel.TryGiveJob`) nulls out any `Wear` job
targeting one of our plush defs, so vanilla's autonomous "optimize my outfit"
AI can never equip a plush as everyday wear outside our own controlled
sleep-time job. Mirrors `Patch_JobGiver_OptimizeBabyApparel` above, which
guards the same risk in the opposite direction (stripping). Applies
universally (all ages), not just adults — cheap and strictly protective.

**Sleeping-plush rendering (BUGFIX — was invisible; now universal + repositioned + Yayo's-Animations-safe):**
plush toys are Belt-layer apparel, so by default the game would try to draw
them flat on the pawn's body while lying in bed. In practice this rendering
path is unreliable/inconsistent-looking for a laying pawn (the user's own
report, backed by a reference screenshot, was that the plush wasn't visible
at all while a pawn slept), and it was ALREADY known to be broken for one
bracket: Child Group 1 (3–6) had a `PawnRenderNode_Apparel.GraphicsFor`
postfix that suppressed the on-body plush draw so a separate "redraw next to
the head" patch could take over — but that replacement patch had been
permanently disabled (`return;` no-op) after a historical "concurrent
collection" crash, so Group 1's plush was unconditionally invisible while
asleep, by our own code, the whole time. The other three brackets (toddler,
Group 2, adult) were never suppressed, so they showed the (unreliable) flat
on-body graphic instead.

This has been replaced with ONE universal system, covering all four
brackets identically:

1. `Patch_PawnRenderNode_Apparel_GraphicsFor_SuppressSleepingPlush` —
   unconditionally suppresses the on-body plush draw for ANY sleeping pawn
   wearing one (via the new shared `TPE_ToyUtility.IsAsleepInBed(pawn)`
   helper: humanlike, spawned, not dead, `!Awake()`, `RestUtility.InBed`).
   Deliberately NOT gated on any bracket's settings toggle — those toggles
   only control whether a pawn could ever have *equipped* a plush in the
   first place; once one is physically worn, sleep-rendering behavior no
   longer needs to re-derive which bracket the wearer belongs to.
2. `Patch_PawnRenderer_RenderPawnAt_DrawSleepingPlush` (postfix on
   `PawnRenderer.RenderPawnAt`) draws a properly positioned replacement —
   or nothing, if the new `TPE_Settings.renderPlushWhileSleeping` toggle
   (default ON) is off, in which case the plush is simply hidden while
   asleep rather than falling back to the old flat on-body look.

**Position (matches a user-supplied reference screenshot — up and to the
right of the head, roughly pillow height):** computed as
`drawLoc + headDir * HeadOffsetForward + sideDir * HeadOffsetSide`, where
`sideDir` is `headDir` rotated 90° clockwise in the XZ plane
(`new Vector3(headDir.z, 0f, -headDir.x)`) — giving a consistent "beside the
head" position for all 4 possible bed rotations without needing a
per-rotation lookup table. `HeadOffsetForward` (0.12) / `HeadOffsetSide`
(0.32) are tunable constants on the patch class.

**BUGFIX (caught during the mandated review pass, never live-tested before
this fix):** `headDir` is derived from `bed.GetSleepingSlotPos(slot) -
bed.GetFootSlotPos(slot)` (both authoritative `Building_Bed` methods,
using the actual sleeping slot index returned by
`RestUtility.CurrentBed(pawn, out int? sleepingSlot)`), NOT from
`bed.Rotation.FacingCell` directly. A first draft assumed "`FacingCell`
points from foot toward head", carried forward from the original
(disabled-before-it-could-ever-be-tested) predecessor patch's own comment.
Cross-checking against `BedUtility.GetSlotPos`'s actual switch statement
showed this was backwards for every one of the 4 `Rot4` values — the head
slot always sits on the `FacingCell.Opposite` side of the bed's footprint.
Had this shipped uncorrected, the plush would have been drawn beside the
pawn's FEET, not their head, for every sleeping pawn. Deriving the direction
from the two authoritative slot positions instead of re-deriving it from
Rot4 semantics means this is correct by construction and can't silently
regress if a future dev ever "simplifies" it back to a `FacingCell` read.

**BUGFIX (user-reported, screenshot-confirmed — plush rendered upside-down):**
the final `graphic.Draw(plushPos, rot, plush)` call was originally passed
`bed.Rotation` as its `rot` argument, on the (wrong) assumption that this
simply picks "which way the decorative sprite should visually face" the way
a multi-directional `Graphic_Multi` apparel graphic would. For a
`Graphic_Single` (what the plush's plain map/dropped graphic actually is),
`ShouldDrawRotated` defaults to true, which makes `Graphic.DrawWorker`
actually SPIN the flat sprite mesh around the vertical axis by
`rot.AsAngle` degrees (North=0°, East=90°, South=180°, West=270°) — verified
by reading `Graphic.DrawWorker`/`AngleFromRot`/`Rot4.AsAngle` directly.
Beds default-place at `Rot4.South` (`BedBase`'s
`<defaultPlacingRot>South</defaultPlacingRot>`), so every plush was being
rendered rotated a full 180° — i.e. upside-down — for the single most common
bed orientation in the game, exactly matching the reported bug. Fixed by
passing `Rot4.North` (`AsAngle == 0`, i.e. no rotation applied) instead of
`bed.Rotation`: the plush's own visual orientation is now a constant
"upright as authored" regardless of which way the bed is placed, while
WHERE it sits beside the head is still fully handled by the independent
`headDir`/`sideDir` math above. Also verified `GraphicData.DrawOffsetForRot`
returns `Vector3.zero` for any rotation on this ThingDef (no `drawOffset*`
fields are set), so this change is a pure rotation fix with zero positional
side effects.

**THREAD-SAFETY FIX (why the old "draw by head" patch used to crash):** the
disabled predecessor resolved the plush's WORN, body-type-specific graphic
on demand via `ApparelGraphicRecordGetter.TryGetGraphicApparel`, which
internally calls `GraphicDatabase.Get<Graphic_Multi>(...)` — the exact same
shared, mutable cache vanilla's own `PawnRenderNode_Apparel.GraphicsFor`
populates for every pawn's apparel. (Verified via decompiled source that
`DynamicDrawManager.DrawDynamicThings` DOES fully `Schedule(...).Complete()`
its `ParallelPreDraw` job before the sequential `Draw` phase that calls
`RenderPawnAt` even starts — so `RenderPawnAt` itself is not literally
racing another thread within one frame's draw call — but calling back into
that same cache-populating API from a hook the parallel apparel pipeline
also feeds from is still the highest-risk thing this render hook could do,
and matches the developer's own contemporaneous description of the crash.)
The fix avoids that whole API: the plush's plain, non-body-type "dropped on
the ground" graphic (`ThingDef.graphicData.Graphic`, a `Graphic_Single` from
`texPath`) is pre-resolved for all 6 plush defs ONCE, at startup, on the
main thread, into `TPE_ToyUtility.PlushDecorativeGraphics` (a
`Dictionary<ThingDef, Graphic>` populated by the SAME startup loop in
`HarmonyPatches`'s static constructor that also fixes `developmentalStageFilter`
— see §6's dev-lore entry on that constructor). The render hook itself then
only ever does a dictionary read — nothing it does at runtime can mutate or
race with any shared cache, on any thread, ever. The hook's one remaining
external call (`graphic.Draw(...)`) is additionally wrapped in a try/catch
with a one-time (not per-frame) warning log, specifically because this call
site has a track record of surprising failures.

**Iterator-safety hardening:** the redraw hook finds the worn plush with a
plain INDEX-based `for` loop over `pawn.apparel.WornApparel`, not a
`foreach`/enumerator — this runs on every visible sleeping pawn every
frame, so it must be immune to an `InvalidOperationException` ("Collection
was modified; enumeration operation may not execute") even in the
ever-technically-possible case that something else mutates `WornApparel`
reentrantly on the same thread mid-frame. A stale read (missing or extra
item for one frame) is harmless here; a thrown exception every frame is not.

**Yayo's Animations (`com.yayo.yayoAni.continued`) compatibility — by
construction, not by testing against the mod directly:** decompiled
`yayoAni.dll` to confirm exactly what it touches. It patches
`PawnRenderer.GetBodyPos` (adds a per-frame walk/aim position offset),
`PawnRenderer.BodyAngle` (adds a per-frame tilt), and `PawnRenderer.LayingFacing`
(can force a fixed rotation during certain animations) — all of which feed
the pawn's own BODY MESH transform inside the render tree. Our position
calculation reads none of these: it uses `RenderPawnAt`'s raw `drawLoc`
PARAMETER (the pawn's pre-offset draw-phase position) and the BED's fixed,
discrete `Rot4` rotation — never `GetBodyPos`'s result, `BodyAngle`'s
result, or the head node's actual rendered transform. The plush's position
is therefore structurally incapable of moving in response to anything Yayo
(or any other mod patching those same three methods) does — which also
happens to be exactly what "stays in the same position regardless of head
position" means: it was never coupled to the head's position to begin with.

**Known dead code, not part of this system:** `JobGiver_SeekPlushBeforeSleep`
(a `ThinkNode_JobGiver` subclass) looks like it should be the toddler
dispatch mechanism but is never referenced by any `Patches/*.xml` — it's
orphaned, superseded by the Harmony-redirect approach above. Don't extend it;
extend the redirect patches instead. (Full dead-code list: §6.)

### 5.2 Event-Triggered Moods — rules, sources, and the second correctness audit

Files: `MapComponent_ToddlerEventMoods.cs` (all polled moods + the shared helpers),
`GameComponent_KnownColonists.cs` (save-wide "ever a colonist" registry, §2.3b), the
"EVENT-TRIGGERED TODDLER MOODS" block in `HarmonyPatches.cs` (one-time sources),
`Defs/ThoughtDefs/ThoughtDefs_ToddlerEventMoods.xml` (+ `TPE_AdoptedToddlerHomesick`
in `ThoughtDefs_ToddlerRandomMoods.xml`). Gate: `TPE_Settings.enableEventTriggeredMoods`.

**Categories (a ThoughtDef belongs to exactly ONE — mixing them was a shipped bug twice):**

| Category | Behaviour | Members |
|---|---|---|
| ONGOING (polled) | Active → `RefreshMemory` on every AWAKE toddler each 150-tick check. Inactive → removed from EVERY spawned toddler, asleep ones included. Asleep + still active → left alone (may expire; re-applied on waking). | Rain, Snow, Aurora, ToxicFallout, HeatWave, ColdSnap, Eclipse, VolcanicWinter, Thunderstorm Fascinating/Scary, Flashstorm, DryThunderstorm, Blight, Tornado, RaidActive, ManhunterPack, MadAnimal, Wedding, Party, PrisonBreak, MentalBreak, FoodShortage; per-toddler: FireOnMap, ScaryPersonNearby, PowerOutage (tech-gated). *(SolarFlare: REMOVED — see PowerOutage below.)* |
| LINGERING (polled) | Refreshed while active, NEVER force-removed — decays over its full duration after the last refresh. | Burial (map-wide, awake toddlers); MedicalOperation, ButcheringHumanlike, SocialFight (per toddler, **only if the toddler witnesses it**) |
| ONE-TIME, polled edge | Applied once to toddlers awake at that moment (no "catch-up" on waking — user-confirmed). | RaidWon, NewColonist, ToddlerInjured, ToddlerRescued (applied even if unconscious), CorpseObserved, ObservedBlood, AdoptedToddlerHomesick |
| ONE-TIME, Harmony | Applied by the patch via `ApplyToAwakeToddlers` (awake, living, spawned toddlers on that map). | ColonistDied (`Pawn.Kill`), Birthday (`BirthdayBiological`, applied even if asleep), AnimalTamed (`DoRecruit` 6-param), DiseaseOutbreak, Infestation, MechanoidRaid, WandererJoins, ResourcePodCrash, MeteoriteImpact, ThrumbosPassing, VisitorsArrived, TraderCaravan (`IncidentWorker.TryExecute`) |

**Witness rule (user-confirmed):** toddler awake AND (same indoor room — `Room.ProperRoom`,
so "outdoors" never counts as one room — OR within 10 cells with line of sight). Event
cells: surgery = patient's cell; butchering = butcher's cell; social fight = each fighter.

**Per-mood triggers (the non-obvious ones):**
- *Game conditions* use `map.gameConditionManager` (map list + world parent) — never
  `Find.World.gameConditionManager` alone: Toxic Fallout / Volcanic Winter / Heat Wave /
  Cold Snap / Flashstorm are MAP-registered. Type-based (`GetActiveCondition<T>`) where the
  original was type-based, so Toxic Spewer, Giant Smoke Cloud and Odyssey Deep Freeze count.
- *Rain/Snow* read `curWeather.rainRate/snowRate`, not the lerped `RainRate` (which made
  "It's raining!" appear AFTER a thunderstorm ended).
- *RaidActive / RaidWon / ScaryPersonNearby*: hostile **pawns with a faction** from
  `attackTargetsCache.TargetsHostileToFaction(player)` that pass
  `GenHostility.IsActiveThreatToPlayer` (excludes downed, asleep, dormant, prisoners,
  hive guards, fogged, fleeing). ScaryPersonNearby: within 8 cells + LOS, awake only.
- *Fire* (user-confirmed): any `Fire` in the home area, or within 15 cells of the toddler.
- *Surgery*: doctor's `DoBill` + `Bill_Medical` + `recipe.anesthetize` (vanilla generates
  every "Administer X" recipe with `anesthetize=false`; transfusion/hemogen too) + doctor in
  the work toil (`JobDriver_DoBill.ticksSpentDoingRecipeWork > 0`, only incremented inside
  `Toils_Recipe.DoRecipeWork`) + witnessed. The old map-wide rule (incl. walking to fetch
  medicine, carrying the patient, drug administration, animals, no witness) is why the
  user saw it "almost always".
- *Butchering*: recipe `specialProducts` contains `Butchery` (cremation doesn't), work
  toil, humanlike corpse in `job.placedThings` (fallback `targetB`), witnessed.
- *Burial* (user-confirmed): `HaulToContainer` of a corpse to a `Building_Grave` whose
  inner pawn is a COLONY MEMBER — `Faction == player` (colonists AND slaves; a pawn keeps
  its faction after death, the same test vanilla `Alert_ColonistLeftUnburied` uses),
  excluding quest lodgers and subhumans.
- *PowerOutage* (`EvaluatePowerOutage`, user-confirmed "power ACTUALLY went out" rule,
  replaces audit #2's version): on while at least one PLAYER-owned consumer that was
  RUNNING has been switched off by the game because its grid can't keep up — i.e. it is
  now off, still `FlickUtility.WantsToBeOn`, not broken down, and either
  `GameConditionManager.ElectricityDisabled(map)` (solar flare etc.) or vanilla would not
  restart it right now. "Would restart" mirrors `PowerNet.PowerNetTick` exactly: net not in
  deficit AND `(stored − 5 Wd if batteries hold ≥ 0.1, else stored) + gain ≥ 0` AND the
  part's draw fits `gain + stored` (`CanPowerNow`). Consequences: batteries covering a
  deficit = lights on = no mood; a machine that NEVER ran (oversized drill, grid without a
  generator) is ignored; losing the only generator IS caught; consumers severed from every
  grid (destroyed conduit) aren't on any net and aren't a generation problem → ignored.
  State: `runningConsumers` (seen ON last evaluation, runtime) + `cutOffConsumers` (saved as
  Thing refs in `<powerCutOffThings>`), both double-buffered `HashSet<CompPowerTrader>`
  swapped each evaluation (no allocation). History only advances while an eligible toddler
  is on the map; a stale history can only report a genuine ongoing shortage. Known,
  accepted gap: a consumer cut off within the first check after loading isn't recognised
  (no "was running" history yet) — vanilla sheds 5% per 20 ticks, so the rest of the
  shed is still caught.
  **Tech gate (user-requested):** never applied to toddlers whose OWN faction def is
  `Animal`/`Neolithic`/`Medieval` techLevel (`IsLowTechToddler`); Undefined/Industrial+ are
  not excluded. A tribal start (PlayerTribe = Neolithic) therefore never gets it, even with
  electricity researched (user-confirmed). If no toddler on the map is eligible, the power
  scan is skipped entirely.
  **Solar Flare mood removed (user request):** it duplicated this mood and misfired with
  mods that keep power on during flares. A flare now only counts when it really switches
  running consumers off; mods that keep them running produce no mood.
  **Verified compatible with Vanilla Furniture Expanded – Power** (user-requested check;
  decompiled VanillaPowerExpanded.dll, VEF.dll, PipeSystem.dll — no code change needed,
  details in `EvaluatePowerOutage`'s doc comment): VFE Power has no Harmony/XML patches
  to power; all its generators (incl. the nuclear reactor, a CompPowerTrader whose output
  is only ever ≥ 0) are skipped by the `PowerOutput < 0` consumer filter; its batteries are
  vanilla `CompPowerBattery`; generator loss (meltdown, the reactor's once-per-solar-flare
  breakdown, solar at night, fuel running out) flows through vanilla's shutdown → mood.
  Framework pieces: VEF powered floors use a real floor-wire consumer
  (`CompPowerTraderFloor`), `CompScheduleExtended` subclasses `CompSchedule` (so
  `WantsToBeOn` sees it). VFE's `VPE_RadioactiveFog` weather has no rain/snow rate.
  **If you change the consumer filter (`PowerOutput < 0`), the faction test, or the
  `WantsToBeOn`/`IsBrokenDown` exclusions, re-check these mods.**
- *FoodShortage*: `map.IsPlayerHome` only (like vanilla `Alert_LowFood`), stored
  human-edible nutrition < 2 × spawned free colonists.
- *NewColonist*: `GameComponent_KnownColonists.RegisterColonistsOn(map)` every check, even
  with no toddlers (spawned player-faction humanlikes, slaves included). The WandererJoin /
  StrangerInBlackJoin incident patch pre-registers the map so the wanderer gets ONLY
  "WandererJoins". Re-enabling the setting silently re-registers (no burst of "new" moods).
- *ToddlerRescued*: downed-outside-a-bed → later downed-IN-a-bed (`InBed()`; cribs count).
  Tracked in a runtime `HashSet<Pawn>` so a toddler carried (unspawned) to bed is still
  recognised; recovering on the floor just forgets the entry.
- *CorpseObserved / ObservedBlood*: every 1500 ticks (elapsed-time schedule), awake
  toddlers, LOS. Seen-state is per (toddler, Thing) pair (`HashSet<long>`, toddler ID high
  32 bits / target ID low 32), pruned when the corpse/filth is gone. Corpse = humanlike only.
  Blood = filth defs whose defName contains "blood" (cached once).
- *DiseaseOutbreak*: `IncidentWorker_DiseaseHuman` succeeded AND a colonist/prisoner on the
  map actually has `def.diseaseIncident` (TryExecute also returns true when all were immune).
- *Birthday*: postfix on private `Pawn_AgeTracker.BirthdayBiological(int)` for
  `birthdayAge` in `[ToddlerMinAge, ToddlerEndAge)`, `DevelopmentalStage.Baby`, on a map or
  in a caravan, not inside a growth vat.
- *Homesick*: "adopted" = no parent whose `Faction == player` (alive OR dead — orphans born
  in the colony are not homesick); 2%/check roll, 20-day cooldown.
- *Setting turned OFF*: every event mood is removed once from every toddler on the map.

**Performance contract:** one pass over `AllPawnsSpawned` for all job/mental-state checks;
no LINQ; static scratch buffers cleared on every use; `RefreshMemory` renews an existing
memory instead of letting `TryGainMemory` allocate a throw-away `Thought_Memory`;
`RemoveMemory` pre-checks with `GetFirstMemoryOfDef` because vanilla `RemoveMemoriesOfDef`
allocates a closure per call; early-out after colonist bookkeeping when the map has no
toddlers. **Never call `map.mapPawns.FreeColonists`/`AllPawns` on the per-check path** —
`AllPawns` does a recursive `ThingOwnerUtility.GetAllThingsRecursively` walk of the map.
(And `FreeColonistsSpawned` is NOT a cheap substitute: it silently drops slaves.)

**Harmony notes:** `Pawn.Kill` despawns the pawn before any postfix runs (`__instance.Map`
is always null there) → map captured in a Prefix via `__state` (same class as the
Postfix). Real taming calls the **6-param** `DoRecruit` directly (the 3-param overload is
only a forwarder used by debug tools) → `TargetMethod()` with `MakeByRefType()`. Both the
birthday and taming patches have a `Prepare()` that disables just that patch (with a
warning) if the target ever disappears, instead of failing `PatchAll` for the whole mod.
The old `PrisonBreakUtility.StartPrisonBreak` postfix was **removed** (double source).
**Retired ThoughtDefs:** `HarmonyPatches`' static constructor calls `RegisterRetiredDefs()`,
which appends `HarmonyPatches.RetiredThoughtDefNames` (currently `TPE_Event_SolarFlare`)
to vanilla's private `BackCompatibility.RemovedDefs` list (reflection, once, at startup,
before any save loads). A save whose pawn still holds such a memory then loads silently —
the def resolves to null and `MemoryThoughtHandler` drops it in PostLoadInit — instead of
logging "Could not load reference to ThoughtDef". **When you delete a ThoughtDef this mod
shipped, add its defName there.**

**Save compatibility:** new scribed nodes `seenCorpsePairs`/`seenBloodPairs`,
`powerCutOffThings` (Thing references — only spawned, non-destroyed things on this map are
written, so every reference resolves; converted back to comps in PostLoadInit). Old
`lastBiologicalAge`, `diseasedPawnIds`, `seenCorpseIds`, `seenBloodCells`,
`lastColonistCount` nodes are simply never read (ignored silently; old "seen" data was
wrong by design and is intentionally not migrated).

### 5.3 Play animation toggles — "Disable toddler play animations" / "Disable child play animations"

Two purely cosmetic settings, both **default OFF (animations play)**:
`TPE_Settings.disableToddlerPlayAnimations` (Toddlers/Newborns tab, under "Mute baby /
toddler crying sounds") and `TPE_Settings.disableChildPlayAnimations` (Children's Play
Need tab, under "Always include Active Play", inside the `enableChildrenPlayNeed` block).
Keys `TPE_Settings_DisableToddlerPlayAnimations_*` / `TPE_Settings_DisableChildPlayAnimations_*`.
Read ONLY through `PlayAnimationUtility.ToddlerAnimationsDisabled` / `ChildAnimationsDisabled`
(null-safe, one static read + one bool — safe on render worker threads).

**Scope (user-confirmed):**

| Toggle | Activity | Animation | With the toggle ON |
|---|---|---|---|
| Toddler | Spinning Around | `TPE_ToddlerSpinning` (Root spin) | lies still on the spot; the mid-session relocation is ALSO skipped (it would be a lying-pawn slide); an in-progress slide is `StopDead`-ed |
| Toddler | Jumping Up and Down, Crib Jumping | hop in `Patch_PawnRenderer_GetBodyPos` (no AnimationDef) | stands still (Jumping still walks between spots; Crib Jumping stands in the crib facing south, name tag still hidden) |
| Toddler | Looking Through Storage, Bug Watching w/ Magnifying Glass | base mod's `Bugwatch` tilt | stands upright facing the target |
| Toddler | Self Soothing | base mod's `WiggleInCrib` | lies still in the crib |
| Child | Active Play → Spinning / Jumping / Snail Crawling | `TPE_ChildSpinning` / `TPE_ChildJumping` (marker; hop in GetBodyPos) / `TPE_ChildSnailCrawling` (lean) | stands still facing south / stands still / walks the same slow path upright |

NOT in scope (deliberately): the base Toddlers mod's OWN animations (its bug watching, crib
wiggle/lay-angle, crawl/wobble locomotion), and non-animation visuals — Play-With-Toy toy
motes, facing changes (Monster Hunt look-around, Heater Snuggling, Work Watching), speed
hediffs (running, dinosaur, snail), lying poses (Ceiling Watching). Activities themselves are
never removed from the pools and Play gain/durations are unchanged ("Option A").

**Two-level gating (both are required — don't remove either):**
1. **Start site** — drivers call `PlayAnimationUtility.MaintainToddlerAnimation` /
   `MaintainChildAnimation(pawn, def)` instead of `renderer.SetAnimation(def)`, in the toil's
   init AND its per-tick action, and `ClearIfCurrent(pawn, def)` instead of
   `SetAnimation(null)` when stopping. Maintain = "disabled ⇒ clear it only if it's ours;
   allowed ⇒ set it only if it isn't already" (idempotent, never resets the start tick, never
   rebuilds the render tree needlessly — `SetAnimation` always calls `renderTree.SetDirty()`).
   While disabled nothing is ever set, so the pawn keeps vanilla's cached render path (skipped
   whenever `renderTree.currentAnimation != null`) and the base mod's crawl/wobble locomotion
   animation is never displaced. The per-tick call makes a mid-activity flip take effect next
   tick in BOTH directions, and also restores the animation after a save load (render-tree
   animations aren't saved). All defs here use `loopMode` Loop (default), so a per-tick
   maintain can never "restart" a finished one-shot animation.
2. **Render site** — `AnimationWorker_ToddlerSpinning.Enabled`, the three child workers via
   `ChildAnimationWorkerUtility.Enabled`, the `Patch_AnimationWorker_Bugwatch_Enabled` /
   `Patch_AnimationWorker_WiggleInCrib_Enabled` prefixes (fall through to the original, which
   is false for our jobs and true for the base mod's own), and `Patch_PawnRenderer_GetBodyPos`
   (the ONLY gate for the toddler hop). Evaluated every frame ⇒ a flip is visible on the very
   next frame even while PAUSED (ticks — and so the maintain helpers — don't run while paused).

`PlayAnimationUtility.BugwatchAnimation` / `WiggleInCribAnimation` resolve the base mod's
AnimationDefs by name ONCE (its `Toddlers_AnimationDefOf` is `internal`; "searched" flag so a
missing def isn't re-looked-up). They replaced per-driver caches and two `DefDatabase` lookups
per TICK in Bug Watching with a Magnifying Glass.

**Performance work done alongside (user-requested "most performance friendly"):**
- `Patch_PawnRenderer_GetBodyPos` runs for every pawn every frame on the PARALLEL pre-draw
  threads. Cheapest rejections first (posture param → two driver type tests → one
  `__instance.CurAnimation` compare); the child-hop sine is only computed for child jumpers.
- The toddler hop used to walk every hop since tick 0 (`while(true)` over
  `TicksGame + id*7` — ~75k iterations per frame per jumping toddler after one in-game year,
  growing forever). The per-hop pause `10 + (c*31 + id*17) % 16` repeats every 16 hops
  (= 760 ticks, each pause 10..25 used once), so `ApplyToddlerHop` takes `t mod 760` and walks
  ≤ 16 hops: O(1), bit-identical (verified against the old loop over 2.46M (tick, id) pairs,
  incl. ~5-year tick values). 64-bit sum, so no overflow.
- The airborne wobble read `Hediff_LearningToWalk.WobbleMagnitude/WobblePeriod` with
  `Traverse` (reflection) twice per frame, plus a `DefDatabase` lookup per frame. Now:
  `Toddlers_DefOf.LearningToWalk` (public) + `ToddlerWobbleAccess` (in HarmonyPatches.cs next
  to its only user): `[StaticConstructorOnStartup]`, binds `AccessTools.FieldRefAccess<Hediff, …>`
  delegates to the private `cachedWobbleMagnitude`/`cachedWobblePeriod` fields once on the main
  thread (the hediff class is `internal`, so no direct cast), never throws (logs one warning
  and disables only the sway if Toddlers renames them), guards the cast with `IsInstanceOfType`.
- `Patch_PawnUIOverlay_DrawPawnGUIOverlay_CribJumping` (every pawn, every OnGUI pass) used
  `Traverse.Create(__instance).Field<Pawn>("pawn")`; now Harmony's `___pawn` injection.
- `JobDriver_ChildActivePlay.CleanupAnimation` only clears one of its OWN three animations
  (was an unconditional `SetAnimation(null)` — a render-tree rebuild on every sub-activity
  switch and a wipe of any other mod's animation); a redundant clear in `InitSpinning` was removed.
- BUGFIX found on the way: the child workers' downed check was `!playWhenDowned || !Downed`
  (always true — never suppressed anything); now `playWhenDowned || !Downed`, matching the
  base mod's `AnimationWorker_Toddler`.

### 5.4 Toys — prices, who buys them, and the 45° held sword / magnifying glass

**Prices (user-set, `Defs/ThingDefs_Toys.xml`):** every plush = **30** (set ONCE on the
abstract `TPE_PlushToyBase`; the six plush defs no longer carry their own `statBases`),
magnifying glass = **30**, toy sword = **10**. Deliberately at or below vanilla's own
crafted-value formula (Σ ingredient value + workAmount × 0.0036 ≈ 38–51 for the plushes,
≈ 45 for the 20-steel glass, ≈ 11 for the sword) to discourage crafting toys for profit.
Keep them EXPLICIT: without a `MarketValue`, vanilla derives one through
`StatWorker_MarketValue.CalculableRecipe`, which scans every RecipeDef on each lookup.
The Bubble Machine is a building with a `costList`, so vanilla derives its value (≈259) —
intentionally untouched.

**Only bulk goods traders buy toys (user-requested):**
`Patches/TraderKinds_BulkGoods_BuyToys.xml` adds vanilla's buy-only
`StockGenerator_BuyTradeTag` (tag `Toys`) to every `TraderKindDef` whose defName contains
`BulkGoods` (vanilla: `Caravan_Outlander_BulkGoods`, `Caravan_Neolithic_BulkGoods`,
`Orbital_BulkGoods`; modded ones following that naming too). They never STOCK toys. Every
toy has `tradeTags: Toys` and `genericMarketSellable=false`. Verified nobody else buys:
no vanilla trader has a Category generator for `Toys` or an ancestor, or a `Toys` tag
generator, and the only catch-all buyer (`StockGenerator_BuyExpensiveSimple`, exotic
traders) skips `genericMarketSellable=false` (plushes are Apparel, which it skips anyway).
If you add a new toy, give it BOTH the `Toys` trade tag and `genericMarketSellable=false`.

**45° held tilt (user-requested):** `Patch_PawnRenderUtility_DrawCarriedThing_TiltHeldToys`
(HarmonyPatches.cs, next to the sleeping-plush renderer), a Prefix on
`PawnRenderUtility.DrawCarriedThing` — the single path vanilla uses to draw ANY carried
thing (from `PawnRenderNodeWorker_Carried`, present in the core Humanlike render tree for
all life stages, and `PawnFlyer`). Every toddler/child activity holds these two items via
`Pawn_CarryTracker` (`Toils_Haul.StartCarryThing`), and none puts them down mid-play, so
the tilt covers Monster Hunt, Carry/Play With Toy, bug watching and child toy play. For
`TPE_ToySword`/`TPE_MagnifyingGlass` carried by a `DevelopmentalStage.Baby` (toddler) or
`Child` pawn only (adults keep vanilla — user-confirmed), it runs vanilla's
`CalculateCarriedDrawPos` then `Graphic.Draw(loc, Rot4.North, thing, ±45°)` (+ the
silhouette call `Thing.DrawAt` would make) and skips the original. Direction: +45
(clockwise on screen — leans right) except facing West = −45. Both sprites are authored
vertically and are left/right symmetric, so no mirrored mesh is needed. Hot path: first
lines are a null check + two static-field compares. On any exception the tilt disables
itself for the session (one warning) and vanilla draws upright — never a throw per frame.
Ignores vanilla's `flip` (only Ingest/SocialRelax drivers set it, and they carry food).

---

## 6. Known dead weight / traps (don't waste time rediscovering these)

- **Publishing never builds (the v1.1.x stale-DLL incident).** Modmixer's Publish and RimWorld's
  uploader send the mod folder exactly as it is on disk. On 11 Oct, a publish two hours after a
  source edit shipped a DLL that predated that edit, and players got "Failed to find RimWorld.ThoughtDef
  named TPE_Event_SolarFlare". ALWAYS build right before publishing. In a dev-mode run, the §3a guard
  (`[TPE] Out-of-date DLL`) now catches this.
- **Stale backup files: REMOVED in v1.2.0.** The five `Source/*.cs.backup*` snapshots and the empty
  `README.md~` were deleted. `Defs/Hediffs.xml.backup_20260805_100849` was moved to
  `.modmixer/archive/backups/`. Don't create backup copies inside the mod folder (`Source/`,
  `Defs/`, root): Publish uploads them to every player. Modmixer's own pre-edit snapshots already
  live in `.modmixer/backup/`, which isn't uploaded.
- **Dead code that still compiles (verified unreferenced — don't extend, don't be fooled):**
  - `WorkGiver_TPE_LetToddlerOutOfCrib` — its WorkGiverDef was removed in v1.1.0
    (CHANGELOG "Removed … WorkGiver_TPE_LetToddlerOutOfCrib") but the class stayed. Crib
    rescue is `MapComponent_ToddlerCribRescue`.
  - `JobGiver_SeekPlushBeforeSleep` — never inserted into any think tree (§5.1).
  - `JobGiver_ChildPlay.GetPriority` (+ its `PriorityJoyAssignment`/`PriorityAnythingLow`
    constants) — never consulted; the real priority is `ThinkNode_Priority_ChildPlay` (§4.2).
  - `Need_ChildPlay.DefaultFallPerInterval` — old fixed 10-day rate; decay now comes from
    `playNeedDecayDays` (default **6** days, slider 3–14).
  - The three `Map.FinalizeInit` MapComponent-registration postfixes (§2.5).
  - Story Spinning and Pillow Nesting (shelved, below).
  - Unreferenced assets: `Textures/Toys/*` (5 roadmap toy sprites),
    `Textures/Things/Building/ToyBox/ToyBox_Full.png` (the "full" state uses vanilla's
    `Things/Building/Childcare/ToyBox`), `Textures/UI/ToddlerIcon.png`,
    `Textures/UI/Icons/Learning/StorySpinning.png` (shelved), and the `*_back`/`*_front`/
    `*_side` plush variants (the game only reads `_north/_south/_east/_west`).
- **Full-scan audit (code-vs-doc pass after feature #23)** — what was found and done:
  - **BUGFIX (code):** `Pawn_PlayTracker.PlayDesireIntervalTicks` was `48000` with the
    comment "8 hours = 48000 ticks" — but 1 in-game hour is 2500 ticks, so desires
    rerolled every **19.2 h**, while this doc, every code comment, the Schematic and the
    Workshop description all say 8 h. Now `20000` (= 8 h). Existing saves finish their
    current countdown, then use 8 h. Vanilla's Learning reroll is 20000–40000 for comparison.
  - Stale "~10 days by default" decay comments corrected to 6 days (`Need_ChildPlay`,
    the Learning-decay patch in `HarmonyPatches.cs`).
  - README.md rewritten (it still had the old "Toddlers Play Expanded" title and three
    duplicated `# TheChildrenNeedToPlay` headers).
  - Doc corrections: Harmony id is lowercase; patch classes are top-level, ~84 not ~90,
    file is ~4800 lines; startup order (§3 — patching happens AFTER def loading; Homely loads
    BEFORE us); 8 MapComponents not 9 + vanilla auto-registration (§2.5); the Toy Box is
    Biotech's `ToyBox` patched, not our ThingDef; the root vs `ThingDefs/` `ThingDefs_Toys.xml`
    split; 16 active toddler givers / 17 ToddlerPlayDefs; child-play priority applies under
    every allowJoy assignment, not only Recreation (§4.2); both debug actions' real bypass
    rules (§4.1, §4.2); crib release is a MapComponent, not a WorkGiver; CompToyCategory's
    real purpose; Pillow Nesting C# is commented out too; added the previously undocumented
    subsystems to the §5 table (crib gate, confinement, Learning schedule + growth blending,
    play-need lifecycle, dress toddler, age-up cleanup, tantrums, classroom buff) and the
    four play-gate transpilers behind feature #1 (§4.1).
  - Found but deliberately NOT changed (player-facing / user-owned): About.xml description
    (see the Schematic notes), `SettingsCategory()` still "Toddlers Play Expanded", hardcoded
    English in older settings/gizmos (§2.9), CHANGELOG stopped at v1.1.0 (a v1.2.0 entry has
    been added since; the gap is noted there).
- **Event-Triggered Moods bugfix audit #1** (superseded in places by audit #2 —
  see §5.2, which is now the authoritative description; bullets below marked
  *[audit #2]* were changed again): a full pass fixed
  several checks in `MapComponent_ToddlerEventMoods.cs`/`HarmonyPatches.cs`
  that looked plausible but were silently dead or double-firing. If you're
  touching this file, know that these are now the *correct*, intentional
  forms — don't "simplify" them back:
  - Thunderstorm/DryThunderstorm are detected via `weather.curWeather`
    (WeatherDefs `RainyThunderstorm`/`DryThunderstorm`, now compared by cached
    def reference), NOT
    `gameConditionManager` — there is no GameCondition by either name, so the
    old check was permanently false.
  - The Fascinating/Scary thunderstorm mood is rolled ONCE per toddler per
    storm (cached in `thunderstormMoodChoice`, cleared when the storm ends),
    not re-rolled every check — re-rolling without clearing the loser stacked
    both the +5 and -6 thought simultaneously for the whole storm.
  - Blight is detected via `listerThings.ThingsOfDef` (it's a spawned `Thing`,
    same pattern as Tornado right next to it), NOT `gameConditionManager`.
  - Burial requires `JobDefOf.HaulToContainer` with `targetA is Corpse` and
    `targetB is Building_Grave` — there is no `"Bury"` JobDef in vanilla.
    *[audit #2]* …and now only for colony-member corpses (§5.2).
  - *[audit #2 — REPLACED]* MedicalOperation used to match `TakeToBedToOperate`
    or any `DoBill`+`Bill_Medical` job map-wide; that was the "surgery mood is
    almost always on" bug. Now: anesthetizing recipe + doctor in the work toil +
    toddler witnesses it, LINGERING (§5.2). Don't re-add TakeToBedToOperate.
  - *[audit #2 — REPLACED]* `isHostile` / ScaryPersonNearby used to hand-filter
    `!p.IsPrisoner`; they now use `GenHostility.IsActiveThreatToPlayer` over the
    attack-target cache, which covers prisoners AND downed/asleep/dormant/hive/
    fogged hostiles (§5.2).
  - TPE_Event_ThrumbosPassing/TraderCaravan/VisitorsArrived are one-time-only
    now (see the §5 table entry) — do NOT add a presence-based
    `MaintainOrRemove` for them back into the tick loop; the incident-patch
    in HarmonyPatches.cs is their sole source of truth.
  - NEW COLONIST detection uses `GameComponent_KnownColonists` (§2.3b), not a
    per-map headcount comparison — a headcount-based check can't distinguish
    an existing colonist arriving on a map from a genuinely new join.
    *[audit #2]* It still double-fired with WandererJoins (a wanderer IS a
    never-seen colonist) — fixed by pre-registering in the incident patch; and
    registration now runs every check even with no awake toddlers (§5.2).
  - `HarmonyPatches.cs`'s incident-name switch had a typo
    (`"WanderersSkylanterns"` vs the real `"WanderersSkylantern"`, no trailing
    s) that made that case permanently unreachable; also broadened to cover
    `DeepDrillInfestation`/`WastepackInfestation`/`MechCluster` (real threat
    incidents that were previously uncovered — `Infestation_Jelly` was
    deliberately left OUT, it's a friendly Ideology gift incident despite the
    name).
  - The corpse-observed/observed-blood rate limiters use elapsed-time
    scheduling (`nextCorpseCheckTick`/`nextBloodCheckTick`, scribed), not
    `TicksGame % 1500` — modulo checks go silent after time-skips/loads.
- **Burial's LINGERING duration + Play↔Learning mutual exclusion + guarantee-
  ActivePlay setting** (three separate user requests landed together): Burial
  switched from `MaintainOrRemove` to LINGERING (originally a `MaintainLingering`
  helper; since audit #2 it is `ApplyToAwake` on the burial flag, same semantics) and its
  `durationDays` bumped 0.125→0.208333 (3h→5h) — don't "fix" it back to
  MaintainOrRemove, the whole point is that it no longer vanishes the instant
  hauling stops (see §5's Event-Triggered Moods row). The Play↔Learning
  mutual exclusion (§4.2b) is enforced from BOTH directions independently
  (`Pawn_PlayTracker.AddNewPlayDesire` + a full-body Harmony Prefix on
  `Pawn_LearningTracker.AddNewLearningDesire`) — if you add a new Play Desire
  that shares an activity with a vanilla Learning Desire, set its
  `conflictingLearningDesire` in XML (with the `MayRequire="Ludeon.RimWorld.Biotech"`
  guard, since `LearningDesireDef` only resolves with Biotech active) rather
  than hand-rolling a new exclusion check — both directions already read it
  generically. `guaranteeActivePlayDesire` (default OFF) only ever force-fills
  ONE of the 2 desire slots in `Pawn_PlayTracker.RefreshDesires`; the other
  slot always still rolls normally. A THIRD enforcement point exists
  alongside the two directions above: `JobGiver_ChildPlayWhenIdle`'s
  "try every registered PlayDesireDef" fallback pool (§4.3) independently
  re-checks `PlayUtility.ConflictsWithActiveLearningDesire` per-candidate —
  without it, an idle-triggered one-off job could still perform an activity
  the child already holds as an active Learning desire, even though that
  same activity could never have been rolled into their tracked
  `ActivePlayDesires` list. All three checks call the same shared
  `PlayUtility.ConflictsWithActiveLearningDesire` helper (two overloads —
  `List<LearningDesireDef>` and a `Pawn` convenience wrapper) rather than
  each rolling their own — keep it that way; a future 4th call site should
  reuse the helper too, not copy-paste the `Contains` check.
- **Child "Reading" infinite job loop (user-reported, fixed)** — symptom:
  "<child> started 10 jobs in one tick. newJob=TPE_ChildReadBook …", child
  frozen in place; manual "read X" order (vanilla job) worked. Cause: vanilla
  `TryGetRandomBookToRead` returns bookcase-held books, which are NOT
  spawned; the old driver used plain `Toils_Goto.GotoThing(A, Touch)`, whose
  built-in `FailOnDespawnedOrNull` failed the job on its first tick, and the
  think tree instantly re-issued it. Rules for any future driver that targets
  something that can live in a container (books, anything in a
  `ThingOwner`-based storage building): use `GotoThing(…, canGotoSpawnedParent: true)`
  (or `GotoCell(thing.PositionHeld)`), `StartCarryThing(…, canTakeFromInventory: true)`,
  and never `FailOnDespawnedOrNull` on that target. Same rewrite also: made the
  toil list fixed-shape (the old one yielded toils conditionally on live state
  at `MakeNewToils` time — that made its "return book to toy box" step dead code
  and could shift `curToilIndex` across save/load; never `yield` conditionally
  on runtime state — use `Toils_Jump.JumpIf` or decide inside an initAction),
  and replaced "drop the book on the floor" with vanilla's finalizer-job haul
  back to storage. NOTE: finalizer jobs are requested BEFORE finish actions
  run, so a finish action must not drop the carried item the finalizer wants
  to haul.
- **Dirt castles: show/hide setting, "only on full completion", "never override buildings/crops"
  (user-requested)** — see §4.2a for the full design. Short version for grep-ability: castles
  used to be spawned from a toil-level finish action (fires on EVERY end, so interrupted
  sessions left castles) with default `WipeMode.Vanish` on any `Standable` cell (destroying
  crops, sitting on non-blocking buildings and growing-zone land). Now: a driver-level,
  `JobCondition`-aware finish action spawns only on `Succeeded`; `DirtCastleUtility`
  refuses cells with a building, a colony-sown plant, or growing-zone land (wild grass,
  items, filth etc. are intentionally NOT protected); the whole visual can be turned off
  with `TPE_Settings.showDirtCastles` (new castles only). If you add a new "spawn a
  decorative building after an activity" feature, copy that shape: setting gate → driver
  finish action filtered on `Succeeded` → placement guard → only then `ThingMaker.MakeThing`.
- **INTENDED BEHAVIOR — DO NOT FIX: children leave toys out after Play With
  Toy.** The toy is always dropped (and unforbidden) beside the child by
  `JobDriver_ChildPlayBase`'s finish action. The user explicitly reviewed this
  and PREFERS it ("more realistic — kids leave their toys out"). The old,
  never-reachable "Step 3: return toy to toy box" (`ReturnToyToToyBox` /
  `FindNearestToyBox`) has been DELETED — it was not only dead on fresh jobs
  but crashed on save-load (see the next bullet). Do not re-add a toy-box return.
- **RULE: `MakeNewToils` also runs at SAVE LOAD, before the pawn is spawned.**
  `JobDriver.ExposeData` calls `SetupToils()` in `PostLoadInit` to rebuild an
  in-progress job's toils; `pawn.Map` is null and the saved `curToilIndex` must
  line up with the rebuilt list. Therefore, in ANY `MakeNewToils` (and any
  `SetupToils` patch): (1) never do map searches (GenClosest/CellFinder/
  `pawn.Map` access) eagerly — do them inside a toil `initAction`; (2) never
  `yield` conditionally on live runtime state (position, carried thing, search
  results) — always yield the same toils and decide at run time
  (`Toils_Jump.JumpIf`, initAction, or a goto-to-own-cell which arrives
  instantly); conditions on SAVED job data (targets, `FromBed`, `HasToy`) are
  fine; (3) never `toils.Insert` into a vanilla driver from a patch — use
  `AddPreInitAction`/`AddFinishAction` on an existing toil. Violations found and
  fixed: `JobDriver_ChildActivePlay` (eager `FindStartCell` + conditional goto →
  now `PickActivePlayStartCell` initAction toil + unconditional goto),
  `JobDriver_ChildPlayBase` (conditional toy-box return → deleted), the LayDown
  plush patches (see §5.1), earlier `JobDriver_ChildReadBook`, and (v1.2.1)
  `JobDriver_ToddlerCeilingWatching`. It registered `KeepLyingDown` inside a toil `initAction`, which
  is NOT re-run on restore, so the finish action was lost after a load. Register driver-level
  finish actions such as `KeepLyingDown` in `MakeNewToils` itself, like vanilla `JobDriver_RelaxAlone`.
  Known
  remaining instance, deliberately untouched: `JobDriver_StorySpinning`'s
  conditional "go to sit spot" yield — that feature is shelved (defs in
  `.modmixer/archive/_Disabled_StorySpinning/`, never loaded), fix it before re-enabling.
- **`Pillow Nesting`** is fully written but SHELVED ("animation caching issues") — and
  EVERY piece is commented out, not just the registration: the `ToddlerPlayDef`
  (ToddlerPlayDefs.xml) and `JobDef` (JobDefs.xml) are in XML comments; in C# the
  `/* … */` blocks cover `ToddlerPlayGiver_PillowNesting` (ToddlerPlayGivers.cs),
  `JobDriver_ToddlerPillowNesting` (JobDrivers.cs), `AnimationWorker_ToddlerPillowNesting`
  (AnimationWorkers.cs), the three integration patches (IsToddlerPlaying, TryGiveJob,
  GetBodyPos — HarmonyPatches.cs) and the two DefOf fields. To restore, uncomment ALL of
  them together (a JobDef whose driverClass doesn't exist errors at load). Parked, don't
  delete; don't be surprised it doesn't fire in-game.
- **`.modmixer/archive/_Disabled_StorySpinning/`** is a whole feature (Learning-time
  "Story Spinning" activity: `JobDriver_StorySpinning` and `LearningGiver_StorySpinning` in
  `Source/`, two def XMLs in this folder). It was moved OUTSIDE `Defs/` so RimWorld's def
  loader never sees it, and in v1.2.0 out of the mod root as well, so it no longer ships. The C#
  classes still compile (harmless, just unreferenced); the kill switch is "XML never loaded,"
  not "C# removed." To re-enable: fix the driver (above), then move both XMLs back under
  `Defs/` and restore the `TPE_StorySpinning` DefOf field.
- **`Mod.cs`'s `Mod` class** is legacy/inert (RimWorld constructs every `Verse.Mod`
  subclass; this one has no settings, so it never shows up anywhere — its "kept for save
  compatibility" comment is not accurate, saves don't reference Mod classes, but removing it
  gains nothing). The real settings/entry point is `TPE_Mod` in `ModSettings.cs`. Don't add
  logic to `Mod.cs`.
- **Two different "child idle/idle-adjacent" think-tree hooks exist** (§4.2's
  Recreation-schedule desire loop vs §4.3's true-idle wander-preempt loop) —
  they look similar (both end up calling into `PlayDesireGiver`s) but have
  different gating, different duration-safety requirements, and different
  failure modes. Changes to one almost never apply to the other; check which
  loop a bug report is actually describing before patching.
- **`PawnRenderer.GetBodyPos` / `AnimationWorker.Enabled` are hot, PARALLEL paths.** They
  run per pawn per frame on render worker threads. Never put time-proportional loops,
  reflection (`Traverse`, `AccessTools` lookups), `DefDatabase` lookups or allocations in
  them; bail on the cheapest checks first; only read immutable/main-thread-written state.
  (The pre-§5.3 toddler hop loop is the cautionary tale — cost grew with game age.)
- **`JobGiver_ToddlerPlayInCrib` returning null does NOT mean "base found nothing" (v1.2.1
  sleep-loop bug).** The base mod evaluates it for SLEEPING toddlers (its "must keep lying down"
  branch, every 211 ticks) and returns null for them. Any postfix that adds an in-crib activity
  must call `TPE_PlayUtility.CanStartInCribPlay(pawn)` right after its `__result != null` check,
  or it wakes sleepers. The player-visible symptom was "stuck at 28% rest, repeatedly sleeping and
  looking at the ceiling" (the 28% is the base `WillSoonHaveBasicNeed(pawn, 0f)` = `rest < 0.28`).
  Full flow: §4.1 "In-crib dispatch". Same rule for any other postfix on a giver that the base mod
  (or vanilla) evaluates in a `MustKeepLyingDown` branch: mirror its early-outs, don't treat null as
  "nothing found". Done alongside: the Ceiling Watching postfix called `TryGiveJobFromCrib` through
  per-think reflection (`GetMethod` + `Invoke` + an argument array), and all four looked their def up
  by string every think. Now a direct call and `TPE_ToddlerPlayDefOf`.
- **A child-need node below 9.1 starves while vanilla Learning catches up (v1.2.2 "refusing to
  play" bug).** Vanilla `ThinkNode_Priority_Learn` stays at 9.1 until Learning ≥ 99.9%, and every
  new need starts at 50%, so a child who just aged up spends ~2 in-game days doing only learning
  activities while a node at 8.5 is never even asked for a job. Nothing errors, and debug logging
  inside `JobGiver_ChildPlay` stays silent because it is never called. The player-visible symptom
  was a 3-year-old "refusing to play" with play stuck draining (save showed 0.200000286, exactly
  0.5 minus 720 decay steps, i.e. zero play since aging up), while older siblings played fine.
  Fixed by the urgent tier in §4.2 (9.15 when play < 30% and below Learning). Third-party
  "Learning Threshold for Learning Job Fix" (Skye) makes the base problem worse by raising vanilla's
  90%/95% learning cut-offs to 99%. Don't drop the tier back to a flat 8.5, and keep it below 9.25
  (chemical needs, Dubs Bad Hygiene washing) so survival/hygiene still come first.
- **XML comment gotcha:** RimWorld's def loader hard-fails on a literal `--`
  (double hyphen) inside an XML comment. Use an em-dash (`—`) instead — this
  codebase does this consistently, don't "fix" it back to `--`.

---

## 7. Where to add something new (quick decision guide)

- **New toddler (~1–3) activity** → new `ToddlerPlayGiver_X : ToddlerPlayGiver`
  in `ToddlerPlayGivers.cs` (or a new file if large) + `JobDriver_ToddlerX` in
  `JobDrivers.cs` + its `JobDef` in `Defs/JobDefs.xml` (+ a `TPE_JobDefOf` field if code
  references it) + a `ToddlerPlayDef` entry in `Defs/ToddlerPlayDefs.xml`. If the base mod
  must treat it as "playing" (render/animation/ending rules), add an
  `IsToddlerPlaying` postfix like the existing ones (CribJumping, CeilingWatching,
  SpeechPractice, HeaterSnuggling). Debug-menu support comes for free: "Force play
  activity on toddler" enumerates every registered `ToddlerPlayDef`.
  **In-crib activity?** Add its `TPE_ToddlerPlayDefOf` field and register it in
  `Patch_ToddlerCribPlaySelection`'s single gated chooser, rather than appending a
  result-only postfix. Keep settings/cooldowns in the worker and end the driver
  with `KeepLyingDown` like the others.
- **New child (3–13) play desire** → new `PlayDesireGiver_X : PlayDesireGiver`
  in `PlayDesireGiver.cs` + `JobDriver_ChildX` + `PlayDesireDef` entry in
  `Defs/PlayDesireDefs/PlayDesires.xml`. If it depends on a player-built
  structure, override `CanGiveDesire(Pawn)` using the base class's protected
  `ColonyHasBuilding` helper (see §4.2) — don't gate portable-item desires
  (toys/books) this way, that's an explicit design exception. Give it
  `minAge`/`maxAge`, an `iconPath`, and — if it is the same activity as a vanilla
  Learning desire — `conflictingLearningDesire` with `MayRequire` (§4.2b). Add a
  `TPE_PlayDesireDefOf` field only if code needs to reference it.
  Idle-play fallback pool (§4.3) picks this up automatically — no separate
  registration needed, just make sure `CanGiveDesire` is correct.
- **New Harmony patch** → top-level `public static class` in `HarmonyPatches.cs`
  following the `Patch_<Target>_<Member>_<Purpose>` naming convention, placed
  near its feature's comment banner. Only break out to a new file if the
  feature is Bubble-Machine-scale (its own Comp/Mote/multi-file subsystem).
- **New setting** → add a field to `TPE_Settings` (`ModSettings.cs`) AND its
  `Scribe_Values.Look` line in `ExposeData` with the same default; draw it in the
  appropriate `SettingsPages.cs` category with localized label/tooltip keys.
  Standalone and IrisMenus share that drawer; gameplay reads `TPE_Mod.Settings`.
  Never make the XML patch conditional.
- **New MapComponent-scale polling feature** → new dedicated
  `MapComponent_YourThing` file, not a bolt-on to an existing one (§2.5). Vanilla
  instantiates it on every map automatically — no `FinalizeInit` registration patch; put
  any "only on player home maps" gate inside `MapComponentTick`.
- **New toy** → ThingDef in the root `Defs/ThingDefs_Toys.xml` with `thingCategories: Toys`,
  `tradeTags: Toys`, `genericMarketSellable=false`, an explicit `MarketValue` (§5.4), a
  `CompProperties_ToyCategory` label, and a recipe in `Recipes_Toys.xml`. A new PLUSH should
  inherit `TPE_PlushToyBase` and its defName must start with `TPE_Plush` (the startup
  filter fix + sleeping-plush graphic cache key off that prefix, §5.1).
- **New plush-sleep age bracket** (see §5.1) → duplicate the pattern of an
  existing sibling redirect patch (`Patch_JobDriver_LayDown_SetupToils_Adult`
  is the newest/cleanest template), give it its own settings toggle + own
  cooldown dictionary, and add one branch to
  `Patch_Toils_LayDown_ApplyBedThoughts.TryResolvePlushMoodBracket` for mood.
  Do NOT try to merge brackets into one parameterized patch — this codebase
  deliberately duplicates per-bracket (matching the pre-existing
  toddler/child split) so one bracket's tuning can never regress another's.
- **New ANIMATED play activity** → never call `pawn.Drawer.renderer.SetAnimation` directly:
  use `PlayAnimationUtility.MaintainToddlerAnimation`/`MaintainChildAnimation` in the toil's
  init AND per-tick action, `ClearIfCurrent` when it stops, and put the matching
  `ToddlerAnimationsDisabled`/`ChildAnimationsDisabled` check in its AnimationWorker's
  `Enabled` (or in `Patch_PawnRenderer_GetBodyPos` for a position offset). Add it to the
  §5.3 scope table and the setting's tooltip.
- **User-facing string from C#** → add a key to
  `Languages/English/Keyed/<file>.xml` and call `"Key".Translate()` — never
  hardcode a literal string that reaches a Gizmo label, Message, or inspect
  string.
- **Publishing an update to the Workshop:**
  1. Bump `<modVersion>` in About.xml and add a CHANGELOG entry.
  2. Build (`build_mod`) and confirm 0 errors.
  3. Ideally do a dev-mode test run. No `[TPE] Out-of-date DLL` error means the DLL matches
     `Source/` (§3a).
  4. Only then press Publish.

  Never leave backups or scratch files in the mod folder; everything outside `.modmixer/` ships.
