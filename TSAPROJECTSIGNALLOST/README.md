# Signal Lost: Static Sweeper

A deep-space incremental game built in Unity 2022.3 LTS (URP). It is played in short days. Each shift on
Relay Station Omega runs one production line: **Pop** the glowing Static, **Sweep** up the Data bits it
drops, **Sort** them into the chute of their color, and the chutes feed the Compressor, which
**Crunches** them into Data. At the end of each day you read a shift report, answer mail (every reply is a
trade-off), and spend Data on one central skill tree. Mail steers the story to one of three endings.
Beating the game unlocks a permanent rebirth tree. No combat anywhere.

## Getting started

1. Open `TSAPROJECTSIGNALLOST` in Unity **2022.3.43f1**. Unity installs the packages in
   `Packages/manifest.json` (URP, Input System, Newtonsoft Json).
2. If Unity asks to enable the new Input System backends, click **Yes**. The game works with either
   input setting.
3. On first open, the editor runs **Tools > Signal Lost > Setup Everything** by itself. It imports the
   TMP Essentials, creates and assigns the URP asset (HDR on), builds every placeholder prefab, and
   writes `Assets/Scenes/Main.unity`. You can rerun it from the menu at any time.
4. Open `Assets/Scenes/Main.unity` and press **Play**. The game starts on the main menu.

When the generated content changes (scene, prefabs, mixer, quality presets), `ProjectSetup.SetupVersion`
goes up and the editor rebuilds it once on the next open. `Assets/Settings/SL_SetupVersion.txt` records
the installed version. This version (2.0.0, setup v3) rebuilds the scene once.

To make a Windows build, use **Tools > Signal Lost > Build Windows Player**. It writes to
`Builds/Windows/`.

## Controls

| Action | Mouse | Keys |
|---|---|---|
| Pop Static | Left-click it | |
| Suck (sweep) bits | Hold the right mouse button over loose bits | |
| Sort | Let go of the right button over the chute of the bits' color | |
| Suck (toggle mode) | Right-click to start, right-click again to drop | Settings > Gameplay > Vacuum |
| Debug menu | | ~ (backquote) or F1 |
| Mail (end of day / finale) | Mail step button | M |
| Pause | Top-right button | Esc or P (gamepad Start) |
| Menus and the tree | Hover / click | Arrow keys or stick to move, Enter / Space to choose, Esc to go back |

## How a day works

1. **Title card**: "DAY 3. Signal at 30%." plus a line from Hale, Dr. Sol or the Static. Click to skip.
2. **Shift**: a countdown (60 s on Day 1, longer with Overtime). Static only spawns while it runs.
   Puzzle events pause the clock.
3. **Last call**: 5 s to get your bits into a chute. Whatever is inside the Compressor is crunched.
   Bits left on the floor are lost (Night Storage keeps them).
4. **End of day**: Shift Report, then Mail (replies change tomorrow: Data, shift length, Gold Static,
   events), then the Skill Tree. Undo tonight refunds tonight's purchases. Start Day N+1.

The game saves at the start of every day and on the end-of-day screen. Quitting mid-shift resumes at the
start of that day.

**Skill tree** (`tree.json`): four branches grow from the center. Navy = Pop (your clicks), Pink = Sweep
(the vacuum), Orange = Station (automation), Purple = Signal (economy and story). The branch colors are
only labels, separate from Static colors. Every node is a single purchase: what used to be ranks (Pop Power
1 to 5, Antenna 1 to 10) are separate nodes further down the branch. Fog of war: a node and its connector
stay invisible until you own a node next to it, so you cannot see how big the tree is. Drag to pan, scroll
(or the + / - buttons) to zoom, FIT to re-frame. Big unlocks include Universal Chute (one chute for every
color), Drone Bay, Auto-Pop Tower and the Antenna chain (Antenna 5 plays the midpoint cutscene, Antenna 10
starts the finale). Costs climb steeply down each branch.

**Rebirth**: every ending pays Echoes (more for a new ending and for finishing under 17 days). The ending
screen shows which ending you got (1/3, 2/3, 3/3) and prompts you to spend Echoes. Rebroadcast resets the
run and opens the Deep Signal tree, whose nodes last forever. The main menu and end-of-day screen show
"Endings found X/3".

**Debug menu**: press ~ (backquote) or F1, or click the faint DBG tab in the bottom-right corner. Skip Day,
Open Skill Tree, add Data / Echoes, trigger any puzzle event (QTE), spawn Static (including GOLD and Prism),
spawn bits, Reveal All nodes, Own All nodes. `DebugMenu.Enabled` (a constant) turns it off for a release build.

## Menus, settings and tutorial

- **Main menu**: a crowd of Static in every color drifts behind it, leans toward the cursor and flinches
  when a neighbor is popped (no Data). Continue shows the day and Data. New Game plays the intro cutscene.
- **Pause** freezes the game, blurs and dims the world, and autosaves before going back to the main menu.
- **Settings** has Audio, Graphics, Gameplay, Accessibility and Data tabs, saved to `settings.json`.
- **Colorblind mode** puts a shape on every Static, bit and chute: red = triangle, blue = circle,
  green = square, gold = star. Sorting works in grayscale.
- **Tutorial** (`tutorial.json`): on Day 1 Hale teaches Pop, Sweep, Sort, Crunch while the clock waits.
  On the first night he walks through the report, mail and the tree. One-time tips cover later firsts.
- **Cutscenes** (`cutscenes.json`): the intro (New Game) and the midpoint (Antenna 5), built in-engine
  from camera shots and comms lines. Skip with the button, Esc or Enter.

## Where things live

| What | Where |
|---|---|
| Every tunable number | `Assets/StreamingAssets/Data/balance.json` (days, bits, spawn, stat bases) |
| Skill tree and Deep Signal nodes | `tree.json` |
| Story mail, briefings, endings | `mail.json` |
| Events, tutorial, cutscenes | `events.json`, `tutorial.json`, `cutscenes.json` |
| Scripts | `Assets/Scripts/{Core,Gameplay,Systems,Juice,UI,Data}` (day loop: `Systems/DayManager.cs`) |
| Prefab + material builder | `Assets/Editor/PlaceholderModelBuilder.cs` |
| Scene builder / project setup | `Assets/Editor/SceneBuilder.cs`, `Assets/Editor/ProjectSetup.cs` |
| Save / settings | `Application.persistentDataPath/save.json`, `settings.json` |

To add a tree node, add an entry to `tree.json` whose effects target an existing stat. No code is needed.
Saves from before 2.0.0 cannot carry over (the old upgrades and levels no longer exist) and start a fresh
run; settings are kept.
