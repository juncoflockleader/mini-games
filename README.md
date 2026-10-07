# Mini Games

A growing Unity arcade collection. The first game is **Breakout**: three brick patterns, three lives, bounce streaks, and a saved personal best.

## Open and play

Requires **Unity 6000.6.3f1** (the version installed on this host). The Unity project is in [`Arcade/`](Arcade/), not the repository root.

```sh
unity open ./Arcade
```

Open `Assets/MiniGames/Scenes/Launcher.unity` and press Play. Choose **Play Breakout**, or press Space. You can also open the Breakout scene directly.

| Action | Control |
| --- | --- |
| Move paddle | Mouse over the playfield, A / D, or left / right arrows |
| Serve / continue | Space or the on-screen button |
| Pause / resume | Escape or the Pause button |
| Restart round | R or Restart |
| Toggle sound | M or Sound |
| Return to collection | All Games |

Moving away from the game pauses an active round. Use Resume when you return. Aim with the edge of the paddle for sharper angles. Consecutive brick hits increase your score multiplier up to 5×; a paddle hit resets the streak. Clear all three patterns to win.

## Project layout

```text
Arcade/
  Assets/MiniGames/
    Runtime/
      Shared/                 Shared UI and game catalog
      Launcher/               Collection menu
      Games/Breakout/         Game controller and independent simulation
    Scenes/                   Launcher and Breakout
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

Tests cover swept collisions, scoring, paddle bounds, pause/resume, lives, restarting, and the complete three-level progression.

## Desktop builds

In Unity, use **Mini Games → Build → macOS**. The output is `Arcade/Builds/macOS/Mini Games.app`. Windows has a matching menu action and requires the Windows build module.

For a batch build, save and close the project's Editor first:

```sh
unity build ./Arcade --target StandaloneOSX --execute-method MiniGames.Editor.ProjectSetup.BuildMac
```

The scenes are committed. **Mini Games → Create Missing Scenes** regenerates missing starter scenes and restores the initial two-scene build list; it leaves existing scene files intact. Extend that setup utility as you add games.

Commit `Assets` (including `.meta` files), `Packages`, and `ProjectSettings`. Unity caches, local preferences, screenshots, test results, and builds are ignored.
