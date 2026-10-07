# Mini Games

A growing Unity arcade collection for desktop:

- **Breakout**: three brick patterns, three lives, bounce streaks, and a saved personal best.
- **Sprout Run**: an automatic runner with four growth sizes, breakable crates, three stages, and an in-game level editor. See the [design and research notes](docs/sprout-run-design.md).

## Open and play

Requires **Unity 6000.6.3f1** (the version installed on this host). The Unity project is in [`Arcade/`](Arcade/), not the repository root.

```sh
unity open ./Arcade
```

Open `Assets/MiniGames/Scenes/Launcher.unity` and press Play. Choose either game; Space opens Breakout. You can also open either game's scene directly.

### Breakout

| Action | Control |
| --- | --- |
| Move paddle | Mouse over the playfield, A / D, or left / right arrows |
| Serve / continue | Space or the on-screen button |
| Pause / resume | Escape or the Pause button |
| Restart round | R or Restart |
| Toggle sound | M or Sound |
| Return to collection | All Games |

Moving away from the game pauses an active round. Use Resume when you return. Aim with the edge of the paddle for sharper angles. Consecutive brick hits increase your score multiplier up to 5×; a paddle hit resets the streak. Clear all three patterns to win.

### Sprout Run

Your sprout runs right automatically. Gold seeds grow it through four sizes; size two and above can break crates. Blue dew shrinks it one size. Spikes and small-sprout beetle collisions shrink it, or end the attempt at the smallest size. Bigger sprouts squash beetles. Solid ground remains solid at every size. Reach the finish post to advance.

| Action | Control |
| --- | --- |
| Start / retry / next stage | Space or the on-screen button |
| Jump | Space, W, Up, or left-click in the playfield; hold for height |
| Pause / resume | Escape |
| Retry from the start | R |
| Open editor / test draft / return from playtest | E |
| Toggle sound | M |
| Choose stage | Left / right buttons beside the stage name |

In the editor, choose a palette item and paint with the left mouse button. Right-click erases. Pan with A / D, left / right arrows, or the mouse wheel over the grid. Place supported start and finish markers, then choose **Test**. Playtesting uses a copy; E returns to the unchanged draft. **Play** returns to the built-in stages while keeping the draft in memory.

**Save** and **Load** use one local draft slot. **JSON** supports clipboard import/export and writes `export.json`. Imported levels are validated before replacing the draft; arbitrary imported stages are not guaranteed solvable. The initial canvas is 80 × 12 tiles. Unsaved changes are marked and protected before replacement or returning to the collection; save before closing the application.

Files are stored in `Application.persistentDataPath/SproutRun/` (normally `~/Library/Application Support/Junco/Mini Games/SproutRun/` on macOS). `draft.json.bak` retains the previous save. To recover it, paste its contents into the JSON dialog and import. Mobile layouts, online sharing, undo/redo, and multiple named save slots are future work.

## Project layout

```text
Arcade/
  Assets/MiniGames/
    Runtime/
      Shared/                 Shared UI and game catalog
      Launcher/               Collection menu
      Games/Breakout/         Game controller and independent simulation
      Games/SproutRun/        Runner simulation, levels, runtime editor, and persistence
    Scenes/                   Launcher, Breakout, and Sprout Run
    Editor/                   Scene setup and desktop builds
    Tests/EditMode/           Collision and session tests
  Packages/                   Pinned Unity package manifests
  ProjectSettings/            Shared Editor and player settings
```

The presentation uses Unity UI with generated geometric artwork and synthesized sound effects. There are no downloaded art packs or paid assets. The project uses URP and the Input System. `com.unity.pipeline` enables live Editor development through the Unity CLI.

## Add a game

1. Add code under `Runtime/Games/<GameName>/`, in a game-specific namespace.
2. Create a scene under `Scenes/` with the game's controller and camera.
3. Register its title, scene name, and description in `GameCatalog.cs`.
4. Add the scene to Build Profiles and the build scene list in `Editor/ProjectSetup.cs`.
5. Return to `GameCatalog.LauncherScene` from the game's menu.

Keep rules independent from MonoBehaviours where practical; the Breakout simulation is an example. Each game owns and cleans up its scene objects. Share UI and navigation, and use separate PlayerPrefs keys for game-specific records. The initial launcher supports a featured game and a short list of additional games; add scrolling/pagination when the collection grows.

## Tests

Use **Window → General → Test Runner → EditMode → Run All** in the Editor. To run in batch mode, first save and close the project's Editor:

```sh
unity test ./Arcade --mode EditMode --output ./Arcade/TestResults/editmode.xml
```

Tests cover Breakout collisions and progression, plus runner growth clearance, short/held jumps, crate collisions, damage protection, pits, pause, all three authored stages, frame-rate independence, JSON validation, and save/load backups.

## Desktop builds

In Unity, use **Mini Games → Build → macOS**. The output is `Arcade/Builds/macOS/Mini Games.app`. Windows has a matching menu action and requires the Windows build module.

For a batch build, save and close the project's Editor first:

```sh
unity build ./Arcade --target StandaloneOSX --execute-method MiniGames.Editor.ProjectSetup.BuildMac
```

The scenes are committed. **Mini Games → Create Missing Scenes** regenerates missing scenes and restores the three-scene build list; it leaves existing scene files intact. Extend that setup utility as you add games.

Commit `Assets` (including `.meta` files), `Packages`, and `ProjectSettings`. Unity caches, local preferences, screenshots, test results, and builds are ignored.
