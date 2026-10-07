# Sprout Run — design and first playable

Status: first desktop prototype implemented on 2026-10-06. Working title: **Sprout Run**. This is the second game in the Unity collection, alongside Breakout.

## Reference and direction

The reference is **Super Mega Runners** (Namicom; also known as Super Pico Pico Runners), the iOS game identified during research. Its [official page](https://namicom.cc/super-mega-runners/) describes automatic running, jumping, giant transformations, and creating/sharing stages. The [App Store listing](https://apps.apple.com/us/app/super-mega-runners/id868777274) documents its stage maker and community features. A [2014 hands-on review](https://www.appgaku.com/iphone-app/スーパーピコピコランナーズ.html) describes repeated strawberry growth, smashing blocks, and damage reducing size. These are documentary findings, not measurements from hands-on play; the current game's exact growth factors, duration, physics, and editor palette are unverified.

Our interpretation uses original characters, graphics, levels, and tuning. The central idea is that collecting power-ups changes the space the player occupies. Getting bigger opens some routes and closes others. The editor is part of the game, accessible in the desktop build.

## Player experience

A little green sprout runs right automatically. Tap for a short hop; hold for a higher jump. Collect golden seeds to grow through four sizes. At size two or above, run or jump through wooden crates. Blue dew drops shrink the player one size, allowing passage under low ceilings. Spikes and patrolling beetles reduce the player one size; a hit at the smallest size or falling into a pit ends the attempt. Retry immediately from the start. Reach the striped finish post to clear the stage.

The first release has three short stages:

1. **First shoots** teaches seeds, stacking growth, and breaking crates on safe ground.
2. **Mind the gap** adds pits, spikes, beetles, and optional jump routes.
3. **Room to grow** combines crate walls with a dew-drop corridor and a final climb back to giant size.

There is no life counter or waiting period. A complete run should take roughly 15–30 seconds, depending on jump timing and obstacles. That duration is a design target, not a measured result.

## Rules and starting values

World measurements use square tile units, with Y increasing upward. Physics runs in fixed 1/120-second steps with bounded catch-up. Rendering interpolates neither simulation state nor collisions in this initial version.

| Rule | Initial value |
| --- | --- |
| Automatic run speed | 5.5 tiles/second |
| Base body | 0.64 tiles wide, 0.9 tiles tall; position anchored at the feet |
| Growth scales | 1.00, 1.45, 2.10, 3.00 |
| Growth duration | Persistent until damage, dew, or retry |
| Jump speed / gravity | 10 / 26 tiles per second / per second squared |
| Short jump | Releasing jump increases gravity during ascent |
| Jump buffer / coyote time | 0.12 / 0.10 seconds |
| Crate destruction | Size two or larger; ground is always solid |
| Damage protection | 1.25 seconds after shrinking from a hit |
| Growth in a tight space | Defer the pickup if the expanded body intersects solid ground; never embed the player |
| Maximum size | Additional seeds are collected without growing further |
| Restart | Reset player, pickups, crates, and beetles from an untouched level definition |

Growth expands upward and sideways around the feet. The candidate body must fit before growth is applied. Crates may be cleared when the new size permits it. Damage shrinks without moving the feet. A wall blocks horizontal progress; the player can still jump or retry. A blocked runner is given a readable hint.

## Controls and feedback

Desktop first: Space / W / Up or hold the left mouse button over the playfield to jump. Escape pauses, R retries, E enters the editor or returns from playtest, and M toggles sound. Buttons expose the same main actions. Losing application focus pauses an active run.

The HUD shows stage name, four size indicators, and progress to the finish. Growth changes the actual collision bounds as well as the drawing. Crates have a visible cross brace; seeds are gold, dew blue, spikes coral, beetles purple, and solid ground teal. The runner has a face and leaves. Simple generated tones and short bursts communicate pickups, damage, destruction, and completion. The overall shell matches Breakout's dark background and lime accents.

## In-game level editor

The editor uses the same 12-row grid and viewport as play. Its palette contains ground, crates, seeds, dew, spikes, beetles, start, finish, and erase. Click and drag to paint; right-drag to erase. Start and finish are unique markers. Pan horizontally with A / D, arrow keys, or the mouse wheel. The first implementation uses a fixed 80-column canvas for newly created levels, with imported levels supporting 36–160 columns.

**Test** creates an independent play session from the current draft. **Back to editor** restores the draft and camera position; collecting seeds or breaking crates in a test must not change authored data. **Save** writes one local draft slot, **Load** restores it, and **New** makes a blank ground strip. Replacing a modified draft requires an in-game confirmation. Returning to the collection also protects unsaved edits.

**JSON** opens a text field containing the current level. Copy exports text to the clipboard and writes `export.json` beside the draft; paste/import validates a replacement before changing the draft. Validation checks schema version, title, dimensions, tile count and kinds, coordinates, duplicate tiles, and clear/supported start and finish markers. It cannot guarantee that an arbitrary stage is solvable. Invalid imports keep the previous draft intact and show an actionable message.

Files live under `Application.persistentDataPath/SproutRun/`. On this macOS build that is normally `~/Library/Application Support/Junco/Mini Games/SproutRun/`. Save through a temporary file and retain the prior draft as a backup. The schema contains `version`, `name`, `width`, `height`, `spawnX`, `spawnY`, `goalX`, `goalY`, and an array of `{x,y,kind}` cells. Positions are integer cells; the spawn feet are at `(spawnX + 0.5, spawnY)`. Stable numeric tile IDs: ground 1, crate 2, seed 3, dew 4, spikes 5, beetle 6.

## Unity structure

- `RunnerLevel`: serializable definition, validation, templates, and safe local persistence.
- `RunnerSession`: independent simulation, collisions, pickups, damage, moving enemies, pause, retry, and completion.
- `RunnerController`: Unity UI renderer, camera, input, feedback, and editor interactions.
- `SproutRun.unity`: controller and camera, registered in the shared catalog and desktop build.

Use the existing Input System and generated Unity UI graphics. No additional packages, downloaded art, external service, or online account is required. The runtime editor is separate from Unity's Editor tooling.

## Acceptance and next iterations

The first playable is ready when all three authored stages are completable; short and held jumps differ; stacked growth affects collisions and crate destruction; shrinking, pits, damage protection, pause, and retry behave predictably; and paint → test → return → save → load → JSON round-trip preserves a draft. Verify simulation and validation with EditMode tests, then exercise the actual scene/editor and build a macOS player. Breakout must still launch and its tests must still pass.

Later work: mobile touch layout and device builds, more characters, checkpoints, moving platforms, richer level art, undo/redo, multiple named local slots, controller support, accessibility options, and community discovery/sharing. Online accounts, rankings, monetization, and a replication of SMRun's full editor are outside this prototype. Game feel and level difficulty remain subject to playtesting.

## Verification record

- 29 EditMode tests passed: the existing 12 Breakout cases and 17 runner cases, including all three authored stages. The gap stage is completed by a scripted two-jump route; this proves a valid route, not balanced difficulty.
- Live scene checks: keyboard start, four-tier growth, stage completion, mouse painting, independent playtest, unchanged draft on return, save/load, clipboard/file export, valid import, and invalid-import rejection.
- macOS player checks: both game scenes launch, runner input responds, editor click/drag painting works, JSON text entry shows validation errors, and unsaved edits prompt before leaving. Fast clicks are handled by Input System callbacks as well as continuous drag updates.
- macOS builds succeed. The Unity CLI's Pipeline package is intentionally disabled in the player; its missing runtime-config warning is expected. Development uses the live Editor connection.
- The editor currently saves one draft and requires structurally valid start/finish placement before saving. Save before quitting the application; the in-game leave prompt does not intercept OS termination. Use JSON exports to keep multiple levels.
