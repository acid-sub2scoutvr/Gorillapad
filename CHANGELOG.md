# Changelog

## 3.1.0

### New

- **Gravity zones.** Walking, jumping, wall climbing, sliding and flying follow the zone's gravity, so you can walk on walls and ceilings. The camera stays upright and behind you.
- **Swim-in-air zones** (like the planet room). Walk on the floor, then float and swim through the air once you leave it.
- **Hoverboards.** Hold a mouse button near a board in a hover area to ride it with the game's own hoverboard physics. Steer with the mouse, carve with A/D, push with W and hop with Space.
- **MIT license.**

### Changed

- **Removed the classic walk.** F6 and the "Classic Walk" setting are gone.

### Fixed

- **Getting stuck at a computer** after pressing E right at the edge of its range while moving away.
- **The third-person camera** no longer flips upside down when looking far up or down.
- **Lava** no longer makes you swim.

## 3.0.3

- **Works on the Oculus/Meta PC version of the game.** The mod didn't start there before.
- **Interact mode taps land the fingertip on the cursor**, instead of the hand.
- **Keyboards in Interact mode:** finished the fix for keys your hand passes over on the way to the one you clicked. 3.0.2 still registered them in some cases.

## 3.0.2

- **Fixed a black screen in first person** for players who use the alarm clock. First person and the computer view now look through the game's own camera.
- **Virtual Stump touchscreens** can be pressed in Interact mode.
- **Keyboards in Interact mode** no longer register other keys your hand passes over on the way to the one you aimed at.
- **Look further up and down**, so low buttons close to you are easier to reach in Interact mode.
- **Fly speed** now changes properly with the scroll wheel, never drops to zero, and is shown in the controls overlay.
- **Walking away from a computer** while using it now puts you back in your previous mode.

## 3.0.1

- **VR and desktop switching.** With a VR headset connected you now start in VR mode again, and without one you start walking. Press **V** at any time to switch between VR and desktop controls, or hold **Tab** to pick a desktop mode.
- **Fixed:** switching back to VR now gives your hands back to your controllers and releases any buttons the mod was holding.

## 3.0.0

Gorillapad is back for the current version of Gorilla Tag, with a rebuilt walk and a lot of new ways to get around.

### Requirements

- **BepInEx 5.4.23.x** or newer. 5.4.22 crashes on launch with the current game.
- **A current Utilla build.** Utilla 1.6.27 and older no longer load.
- **No VR headset needed.** SteamVR no longer has to be running.

### New

- **New walk animation.** Hands plant on the ground and stay put while you move over them, the step rhythm follows your speed, and the body bobs and sways with each step. Turning in place makes your hands step around. Press **F6** to switch back to the classic walk.
- **Wall climbing.** Hold a direction into a wall or steep slope and press **Space** to bounce off it. Keep jumping to climb, and your hands slap the wall on each bounce.
- **Air control.** Steer while in the air, without losing the momentum you already have.
- **Sliding.** Icy surfaces work like the real game: you keep your speed, slopes speed you up, and steering turns your slide. Hands stop walking while you slide, and the game's slide sound plays.
- **Swimming.** Walk or fall into deep water to swim toward where you're looking. **Space** / **Ctrl** go up and down, and **Space** at the surface leaps out. You float at the surface when idle, and flowing water carries you.
- **Ziplines.** Hold a mouse button near a cable to ride it, and release or press **Space** to jump off.
- **Ropes.** Hold a mouse button near a rope to swing on it. **WASD** pumps the swing, and you launch off with its momentum when you let go.
- **Gliders.** Hold a mouse button near a glider to pick it up. **A/D** roll, **W/S** dive and climb, and the glider keeps itself level otherwise. You can walk and jump while carrying it.
- **Broomsticks.** Hold a mouse button when a flying broomstick passes to hang on and get carried along.
- **Sliders.** In Interact mode, hold a mouse button on a slider and move the mouse up or down to drag it.
- **On-screen controls.** The controls for your current mode are listed in the corner. Press **H** to show or hide them.
- **Flying animations.** Hands tread water while hovering and spread into airplane arms that bank as you turn.
- **Settings file** with options for the classic walk and the controls overlay.

### Improved

- **Start walking right away.** Walk mode is selected as soon as the game loads, so you no longer need to pick it from the Tab menu first.
- **Sprint while flying** by holding **Shift**.
- **Stop emotes** by moving or pressing the same number again.
- **Interact mode** presses buttons instantly, including elevator buttons, and only presses the one you're aiming at. The hand tap is just for show.
- **Pose mode** scroll is much faster.
- **Computers and terminals** only activate when you're standing in front of one, and the camera lines up with the screen.
- **The Tab mode menu** is smaller.
- **Closing the Tab menu** without choosing keeps your current mode.
- **Arms keep up with you** when falling fast.

### Fixed

- Mouse-look and the camera work again.
- Entering zones like the city no longer drops you into a public lobby when a modded mode is selected.
- Jumping no longer stops working after joining or leaving a room.
- Leaving noclip in fly mode no longer leaves your body passing through things.
