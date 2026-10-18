# Event behavior and performance

## Attitudes

Each incident in the settings catalog has its own attitude selector. Missing settings
default to neutral, including assault incidents. An occurrence snapshots its setting;
changing the selector only affects future occurrences. Existing untracked visitors
retain their legacy behavior.

| Setting | Initial relation | Player damage / forced expulsion |
| --- | --- | --- |
| Hostile | Hostile | Remains hostile |
| Leaning hostile | Neutral | That occurrence becomes hostile |
| Neutral | Neutral | No group-wide damage reaction; forced expulsion becomes hostile |
| Leaning friendly | Neutral | Group leaves without becoming hostile |
| Friendly | Allied | Group leaves without becoming hostile |

Damage must have a player-faction instigator and cause external-violence damage.
Unattributed environmental damage does not trigger the group reaction. Recruitment
and player affiliation exclude pawns from visitor control. Held children and later
members of staged generation retain occurrence ownership.

Three fixed hidden role factions provide vanilla hostility checks without creating
one faction per occurrence or patching global hostility queries. Friendly visitors
do not take food outside the designated relief area. Incident choices, rewards and
narrative text remain incident-specific; attitude is not a rewrite of those scripts.

## Implemented Hotspot Changes

- Visitor records use a pawn index for lookup and swap removal. Registering or
  removing each of N visitors no longer repeatedly scans the entire record list.
- Event self-feeding caches a selected food source. Compatible requests can reuse
  a shared positive target; failures are per pawn and expire after a short retry.
- Targets are validated against location, food quantity, freshness, diet,
  reservations, reachability, request flags and relief restrictions. A cache hit
  avoids selecting from all food sources, not all reservation or path checks.
- Empty relief areas return immediately. Area searches reject lower-scoring
  candidates before doing expensive reservation and reachability checks.
- Staged generation registers only newly added members, rather than re-enumerating
  every earlier member after each spawn.
- Thief combat fear is a reusable behavior flag. Neutral/hostile thieves can keep
  eating under combat pressure; burning, downed and non-combat escape paths are
  not globally disabled. Core health and needs updates remain intact.

## Event Display IDs and Narrative Attitudes

Display IDs are explicit catalog data, not list indexes: the 14 entries already
classified as original use O-001 through O-014. N-001 through N-010 remain reserved
for the existing narrative chain; the other 36 incident entries use N-011 through
N-046. Settings and debug menus share the catalog labels. Def names, saved flags,
toggle keys and incident counts are unchanged. S-series outcome notifications and
E-series endings retain their own IDs. Return branches display N-004-R and N-007-R
while retaining their existing internal keys.

N-004 shares the ShatteredMother attitude, N-005 shares ChildExchange, and N-007
shares its nine plague-visitor source attitudes. A mixed N-007 selection is shown
explicitly; selecting an attitude there updates all nine source settings. Changing
a source separately is reflected immediately. These controls only affect future
cohorts, not active story records, existing cohorts or quarantine/release decisions.

N-008 creates its own behavior cohort with the saved N008 settings key. It uses
the same five attitudes and attack/expulsion reactions as incidents. Nonhostile
envoys retain their meeting duty and do not leave merely because feeding completes.
Expulsion settles the story without replacing the configured hostility/flee reaction
with an unconditional departure. Notifications, grain-burrow tasks and endings do
not expose ineffective attitude controls. Return branches retain their existing
story behavior rather than silently reassigning an existing pawn to a new cohort.

## Feeding Completion and Visit Summaries

Food-cache requests without `calculateWantedStackCount` require only one available
unit, matching the original selection contract. Explicit quantity requests retain
their reservation and stack checks.

A mouse-disaster food-seeking visitor at 82% food is marked as fed once in the game
component, using the pawn's integer ID rather than a health effect or pawn reference.
This stops additional begging, theft and gnawing; ordinary vanilla eating remains
available. Existing map-stay requirements are preserved. Old hidden fed-once marks
are migrated on access and removed. Editing or clearing health effects does not
erase the independent completion record.

Guanyin Tu satiety temporarily suppresses those extra food-seeking jobs only while
its hediff exists. It neither records permanent feeding completion nor resets the
visitor's food-seeking mental state. After removal, a hungry visitor can seek food
again; a full visitor completes normally. Temporary satiety alone is not counted as
full nutrition.

At 82% food and malnutrition severity at least 0.4, refeeding syndrome can be applied
once per visitor when new content is enabled. The check does not require a minimum
meal size, actual ingestion or a particular carbohydrate source. A separate saved
integer-ID record prevents refreshing the condition or forcing it back after a
health editor removes it. Malnutrition is not removed. Digestion is reduced by 30
percentage points, blood filtration by 20, and consciousness by 25 for 2-5 days.
These penalties can compound with existing illness.

Ordinary setters are observed immediately. The existing 150-tick food-need cadence
also checks current values before hunger decay, covering loaded values, direct
field edits and MaxNutrition changes without a full-map scan. NaN and infinite
food percentages do not complete feeding. Mods replacing or skipping these game
methods can still bypass the checks and need individual compatibility testing.

Completed general visits become summaries of scene, ID, creation/completion times,
delivery/expulsion decisions and their times, aid credit and outcome counts. They
contain no pawn or map references. Only active visits participate in observation
scans. Old completed records migrate without replaying aid/trust rewards; unknown
historical timestamps remain -1. The envoy verification and debug totals include
summaries. Specialized unfinished story records retain references required for
their follow-up events. Summary storage still grows with event count, but no longer
retains per-person observation objects or participates in periodic visit scanning.

`scripts/verify-feeding-summary.ps1` checks feeding thresholds, exclusions, the
new-content gate, summary migration and scalar serialization against controlled
types, plus the syndrome XML. It is not a live-game save/load test.

## Remaining Scaling Costs

The vanilla needs tracker already gates normal needs updates at a 150-tick interval;
it is not a full food search per pawn per tick. Blanket needs throttling was not used.

`HasPendingPrisonerBabyCare` can search food for multiple caregivers for each baby.
This remains a potential babies-by-caregivers-by-food-sources cost. Caregiver searches
are intentionally excluded from the self-feeding cache to preserve feeding priority.

`ProcessN004PredatorHunt` scans spawned pawns per pending narrative record on its
2,500-tick cadence. Many simultaneous pending records can multiply that work.
Plague spread has a much longer actual spread interval than its component polling
interval. Neither was presented as a proven per-tick bottleneck.

Pawn generation, pathfinding and reservation contention remain native costs. Different
diets, moving food, depleted stacks and blocked routes can invalidate many targets
at once. No exponential runtime was established, and no constant-time total event
runtime is claimed.

## Verification Boundary

Run `scripts/verify-event-behavior.ps1` with PowerShell 7. It compiles production policy,
cohort and food-cache code against controlled game types, and tests production visitor
index methods. The 1/10/100-pawn selection counters use stable food, compatible diets,
and successful reservations/reachability. They are not Unity TPS measurements.

Build and run the existing regression scripts as well. Actual save round trips,
Harmony integration, mixed-event combat, interrupted generation, caregiver feeding,
and same-save 100-pawn A/B profiling still require in-game validation. Uninstall-export
XML checks do not establish that a game can load the exported save without the mod.
