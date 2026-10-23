# Changelog

All notable changes to Axe Path. Format follows Keep a Changelog;
releases are `vMAJOR.MINOR.PATCH` tags, built by the release workflow.

## [Unreleased]

### Added

- Procedural PBR detail: every surface gains generated grain, bark,
  rock, and ground textures with bump relief (`TextureFactory.cs`);
  materials move from flat 0.05 gloss to lit, textured finishes.
- Zone-graded atmosphere: fog color/density and ambient shift between
  village, forest, and cave (`CameraFx.cs`); 2048px night sky with
  nebula tint and 2600 stars; warm lantern posts light the village.
- World dressing stage: 2600 grass tufts, ferns, scatter rocks,
  mushrooms, stumps, fallen logs, fences, crates, and barrels
  (`DetailBuilder.cs`), all merged into the static draw budget.
- Living ecology: grazing/fleeing deer, darting rabbits, and circling
  birds (`Wildlife.cs`); detailed dwarf (beard, buckle, hammer),
  librarian (hood, sash), and wolves (ears, hackles, paws).
- Survival gameplay: stamina (sprint/swing/guard costs), right-click
  guard that turns wolf bites, Ctrl/C sneak that cuts hearing, wolf
  pack alerting, wolf loot (pelts + meat), berry foraging, and eating
  to heal (F meat, R berries). Saved with the world; old saves load
  with empty stores.
- Generative score: sparse D-minor pentatonic plucks over a fifth
  drone, darker and slower inside the cave (`AudioSynth.TickMusic`).
- HUD: engraved serif type, breath (stamina) bar with guard/sneak
  states, loot readout, and a permanent soft vignette.

- Shaped world art: tiered pines, timber-framed pitched-roof huts,
  a stone chapel with bell tower and graveyard, and a two-floor inn
  with porch and hanging sign. Jointed wolves (hip, head, and tail
  pivots) trot and gallop with a diagonal gait; the dwarf works the
  anvil with a looping forge arm.

### Fixed

- Losing cursor lock mid-swing (unfocused window) no longer strands
  the axe in the air: the in-flight swing settles and lands while
  the world stays paused.
- Grass no longer grows over stone or trodden ground (path, cave
  apron, clearings, dirt patches excluded), and mountain and cave
  wall colliders now match their visuals: no walking through
  mountainsides or slipping between cave-wall discs.
- The ground no longer washes white and flutters while walking:
  the Blender kit's studio render floor (a ~300m white plane) was
  being exported into the game and z-fought the terrain, and the
  dirt discs shared a pale wood material that blew out to cream
  under moonlight. The exporter and the runtime now both strip
  the floor, and dirt has its own dark earth texture/material.

## [0.2.0] - 2026-10-22

### Added

- Pause menu gains a volume slider + mute toggle, persisted across
  runs in PlayerPrefs.
- Boot shows a loading overlay naming each world stage while the
  valley builds one stage per frame (same order, same world).
- Save-slot labels via a pure formatter: dateless saves show no
  dangling separator; handhelds get a desktop-only notice on title.
- Space hop: a small jump whose apex clears wolf lunges (lunges
  check height; a hopped lunge snaps at air with a prompt).
- Crosshair warms gold while a wolf or pine is inside axe reach,
  so the hitbox reads before the swing.
- Thematic impact bursts: wolves bleed (more on a kill), rock and
  metal throw glowing sparks with a clang, wooden walls chip like
  trees but don't shudder. Walls and boulders are targets now —
  the axe connects with anything it can reach.
- Windows player builds: releases now ship AxePath-Windows.zip next
  to AxePath-macOS.zip, with an INSTALL.md covering both.
- Gated perf sampler: launching with -perfLog logs fps, draw calls,
  and memory to the player log every 120 frames.

### Changed

- Axe impact resolves at animation contact (k=0.62) instead of on
  click: the whoosh answers the click, and damage, hit-stop, flash,
  knockback and prompts land when the axe visibly arrives. Non-killing
  wolf hits now flinch the chase for 0.3s, and the yelp trails the hit
  crack by 80ms so it reads as pain.
- Static valley geometry merges per material at boot (4237 statics
  into 36 meshes): draw calls fall from ~10.5k to ~1.6k on the title
  view (Very High, 3600x2260) with the same world and look. Animated,
  toggled, shaken, and landmark pieces stay live.
- Motes no longer cast shadows, and the far plane pulls in 1500→900
  (still past the farthest peak).

### Fixed

- Pause menu mute button no longer hides behind Resume (the panel
  grows to 480 to fit the volume row above the bottom-anchored
  buttons).
- The game no longer stalls when launched in the background: it
  runs unfocused now, and losing focus mid-play opens the pause
  menu instead of freezing mid-frame.
- Respawning no longer inherits the old body's mid-swing cooldown,
  camera trauma, or falling hop — dying mid-swing used to jam the
  axe for the first moments of the new life.
- Impact bursts now erupt at the struck face (a confirmation ray
  from the eye finds it), not at collider centers and rims — no
  more sparks floating before fat rock walls or blood inside the
  wolf's heart.
- Wolf corpses keel over and linger 2.5s before clearing instead
  of vanishing on the killing frame.
- Burst origins now aim along the look-line instead of the closest
  rim point: no more sparks at the player's flank on wide rocks or
  chips lost inside/behind thin trunks. Flesh bursts aim at body
  centers so blood starts at hide, and bursts sit a breath off the
  face so grazing angles can't embed them.
- The chop now sweeps sideways through center to the crosshair at
  contact (and lunges deeper) instead of chopping down the right
  edge short of the target.
- Blood has presence: brighter arterial red, bigger and more
  numerous drops, harder spread, longer ground splatter. Chips
  mix bark-brown with pale fresh-split wood and fly slightly
  larger. All motes shrink out over their last 0.2s instead of
  popping.
- CI actions bumped off deprecated Node 20 (checkout v7, cache v6,
  upload-artifact v7, gh-release v3).
- macOS builds and both test suites retry once in-job on failure
  (net for transient runner OOMs; genuine breaks still fail twice
  and loudly).
- The build cleanup between attempts retries with sudo when a
  killed attempt leaves root-owned builder byproducts (the builder
  runs as root in docker) that plain git clean can't remove.

## [0.1.0] - 2026-10-21

First numbered cut: the playable valley slice with the combat feel pass.

### Added

- Combat feel: 3-beat axe swing with whoosh; hit-stop and camera kick
  on connects; kill confirms; wolf lunge telegraphs (dodgeable) and
  death reads; chops with trunk shudder and chip bursts.
- Pause menu with mouse-sensitivity slider; Esc resumes.
- Waystone rune and cave-side note readable in range.
- Release machinery: CI (EditMode + PlayMode + macOS build), VERSION,
  changelog, tagged releases with attached builds.

### Fixed

- Cave corner colliders no longer wall off the note approach.
- Mill seated on the bank; wheel over water, turning with the current.
