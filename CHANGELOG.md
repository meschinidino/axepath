# Changelog

All notable changes to Axe Path. Format follows Keep a Changelog;
releases are `vMAJOR.MINOR.PATCH` tags, built by the release workflow.

## [Unreleased]

### Fixed

- CI actions bumped off deprecated Node 20 (checkout v7, cache v6,
  upload-artifact v7, gh-release v3).
- macOS builds and both test suites retry once in-job on failure
  (net for transient runner OOMs; genuine breaks still fail twice
  and loudly).
- Editor MCP bridge boot falls back to stdio transport when the HTTP
  relay is unreachable, so agent sessions connect without UI clicks.

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
