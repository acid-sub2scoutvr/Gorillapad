# GorillaPad

- Fork of [Gorillapad](https://github.com/KyleTheScientist/Gorillapad) with added gamepad/controller support!

Play Gorilla Tag using a gamepad or controller, no VR headset required! Walk, run, climb walls, swim, fly, and ride ziplines, ropes, gliders, broomsticks and hoverboards, all with procedurally animated gorilla arms.

GorillaPad only works in **modded lobbies**. In a public lobby it turns itself off.

> This product is not affiliated with Another Axiom Inc. or its videogames Gorilla Tag and Orion Drift and is not endorsed or otherwise sponsored by Another Axiom. Portions of the materials contained herein are property of Another Axiom. ©2021 Another Axiom Inc.

## Requirements

- Gorilla Tag (Steam)
- [BepInEx 5.4.23.x](https://github.com/BepInEx/BepInEx/releases) (x64). Older BepInEx 5.4.22 crashes on launch with the current game.
- A current [Utilla](https://github.com/sirkingbinx/Utilla) build for modded lobbies. Utilla 1.6.27 and older no longer load on the current game.
- An Xbox, PlayStation, or compatible PC game controller.

## Installation

1. Install BepInEx into your Gorilla Tag folder and launch the game once.
2. Install Utilla into `BepInEx/plugins`.
3. Extract the GorillaPad zip into your Gorilla Tag folder so the DLL ends up at `BepInEx/plugins/Gorillapad/Gorillapad.dll`.
4. Connect your controller.
5. Launch the game and join a modded lobby (or play offline).

## Controls

Press **Start** at any time to show or hide the controls for your current mode on screen.

### Gamepad and Controller

GorillaPad is designed around gamepad input. All desktop gameplay controls are intended to be accessible through a controller.

### Everywhere

| Controller | Action |
| --- | --- |
| Start | Show / hide controls |
| D-Pad | Navigate menus |
| A | Confirm / interact |
| B | Back / cancel |
| X | Switch between first and third person |
| Y | Switch gameplay mode |
| LB / RB | Cycle emotes |
| View / Select | Open mode menu |

### Walk

| Controller | Action |
| --- | --- |
| Left Stick | Move |
| Right Stick | Look |
| Left Stick Click | Sprint |
| A | Jump |
| A + direction toward a wall | Wall jump (repeat to climb) |
| B | Crouch |
| LT / RT | Grab with left / right hand |

Walking onto icy surfaces makes you slide. Walking into deep water switches to swimming automatically.

### Swim

| Controller | Action |
| --- | --- |
| Left Stick | Swim toward where you're looking |
| A | Swim upward |
| B | Swim downward |
| Left Stick Click | Swim faster |
| A at the surface | Leap out of the water |

Flowing water carries you along.

### Zipline, Rope and Broomstick

| Controller | Action |
| --- | --- |
| Left Stick | Pump the swing (ropes only) |
| A | Jump off |
| Release LT / RT | Let go and keep your momentum |

### Glider

| Controller | Action |
| --- | --- |
| Left Stick Left / Right | Roll |
| Left Stick Up / Down | Dive / climb |
| Right Stick | Look and turn |
| LT / RT | Hold the glider |
| Release LT / RT | Let go |

### Hoverboard

Grab a board inside a hover area to ride it with the game's own hoverboard physics.

| Controller | Action |
| --- | --- |
| Right Stick | Steer |
| Left Stick Left / Right | Carve |
| Left Stick Up | Paddle |
| A | Hop |
| Release LT / RT | Let go |

### Fly

| Controller | Action |
| --- | --- |
| Left Stick | Move |
| A / B | Up / down |
| Left Stick Click | Fly faster |
| D-Pad Up / Down | Adjust flight speed |
| Y | Noclip on / off |

### Pose

| Controller | Action |
| --- | --- |
| Right Stick | Move the hand |
| D-Pad Up / Down | Hand forward / back |
| LB / RB | Rotate the hand |
| Y | Switch hands |
| LT / RT | Trigger / grip |
| D-Pad Left / Right | Thumb buttons |

### Interact

| Controller | Action |
| --- | --- |
| LT / RT | Press the button under the crosshair with your left / right hand |
| Left Stick Up / Down | Drag a slider |

### Emote

Use the D-Pad to select an emote. Press the same direction again or move to stop.

### Computer

| Controller | Action |
| --- | --- |
| D-Pad | Navigate |
| A | Select / Enter |
| B | Delete / Back |
| LB / RB | Change screen |
| Y | Leave the computer |

## Configuration

Settings are in `BepInEx/config/com.scoutvr.gorillatag.gorillapad.cfg`, created after the first launch.

| Setting | Default | Description |
| --- | --- | --- |
| Interface › Show Controls | true | Show the controls overlay |
| Controller › Enable Controller | true | Enable gamepad input |
| Controller › Look Sensitivity | 1.0 | Adjust camera sensitivity |
| Controller › Invert Look | false | Invert vertical camera movement |

## Building

1. Set `GamePath` in `Directory.Build.props` to your Gorilla Tag install. BepInEx and Utilla need to be installed there, since the project references them.
2. Run `dotnet build`. The DLL should be copied into your game's `BepInEx/plugins/Gorillapad` folder once the project output settings are updated. Close the game first, or the copy fails because the DLL is in use.
3. To make a release, add a `## <version>` section to `CHANGELOG.md`, set the version in `PluginInfo.cs`, and run `MakeRelease.ps1`. Update it to produce `Gorillapad-v<version>.zip` and write that version's changelog section to `Release.md`, ready for `gh release create v<version> Gorillapad-v<version>.zip --notes-file Release.md`.

## License

MIT. See [LICENSE](LICENSE).
