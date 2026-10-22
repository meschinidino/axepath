# Changelog

All notable changes to Axe Path. Format follows Keep a Changelog;
releases are `vMAJOR.MINOR.PATCH` tags, built by the release workflow.

## [Unreleased]

### Added

- Tuning-as-data: every balance knob (wolf senses/HP/speed, dulling,
  swing, hit-stop/shake, chop FX) lives in `Assets/Resources/tuning.json`
  and loads at session start; `TuningSet` is the plain-data shape,
  `Logic` reads it, and a corrupt or missing file falls back to the
  previous in-code defaults. 8 new EditMode tests pin the loader and
  the formulas over arbitrary data.

### Fixed

- CI actions bumped off deprecated Node 20 (checkout v7, cache v6,
  upload-artifact v7, gh-release v3).
- macOS builds and both test suites retry once in-job on failure
  (net for transient runner OOMs; genuine breaks still fail twice
  and loudly).

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
