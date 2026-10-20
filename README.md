# Axe Path (Unity slice)

First-person survival-ascent slice: spawn a nobody, take a lumber axe,
climb the hill forest, find the dwarf's unmarked cave, earn sharpening.
No tutorials, no markers, no quest log. Permadeath with legacy.

Built in Unity 6000.6.6f1 (built-in RP, C#). The saved scene is an empty
boot container — the entire world is constructed in code on load, which
keeps every placement seeded, testable, and reviewable as a diff.

## Play it

Open `Builds/AxePath.app` (built locally, not committed).

| Input | Action |
|---|---|
| W A S D / arrows | Walk (Shift = run, forward only) |
| Mouse | Look |
| Left click | Swing the axe |
| E | Interact (take, talk, read) |
| Esc | Title / close panels |

Fixed keys in v1, always shown on the title controls card.

## Build it

Open the project in Unity 6000.6.6f1, then run the menu item:

**AxePath → Build OSX Player**

This builds `Builds/AxePath.app` from the open editor (a headless
`-batchmode` build cannot open a project the GUI editor already holds).
After building on macOS, strip quarantine once:
`xattr -dr com.apple.quarantine Builds/AxePath.app`.

## Test it

- **EditMode (23):** pure game rules in `GameLogic.cs` — trust ladder,
  dulling, wolf brain, saves. No Unity dependencies.
- **PlayMode (22):** `SessionFlow` drives the real session end to end
  (title → axe → book → chop → wolf → thaw → ironwood → sharpened →
  death → reclaim); `WorldShots` asserts world structure and captures
  viewpoint screenshots to `Assets/Screenshots/` (gitignored, regenerable).

Run either suite from the Unity Test Runner, or ask the agent bridge
(`run_tests` / `get_test_job` over the MCP connection).

## Layout

- `Assets/Scripts/AxePath/` — game code. `GameLogic.cs` is pure rules;
  `WorldBuilder.cs` builds the valley; `GameSession.cs` runs the loop;
  `PlayerController.cs` is the first-person body; `ActorViews.cs`
  animates wolf/NPCs; `Hud.cs` and `AudioSynth.cs` present and sound it.
- `Assets/Scenes/Main.unity` — boot container. Everything visible is
  built by `Game.Boot()` from the scripts above.
- `Assets/Editor/` — `BuildScript.cs` (player builds), `McpBoot.cs`
  (agent bridge autostart), `ShaderPreload.cs` (keeps player builds
  from stripping shaders the code looks up at runtime).
- `Assets/Tests/` — EditMode + PlayMode suites.
- `Assets/Resources/` — shader preload materials.

## Docs

- `DESIGN.md` — the game: premise, loop, world, rules, boundaries.
- `ROADMAP.md` — where the slice stands and what comes next.
- `../games/DESIGN.md` — the original v1 design seed (three.js era).
