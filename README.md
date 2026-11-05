# Baldi VR mod (Baldi's Basics Classic Remastered, Meta Quest 2)

A fan-made VR mod for **Baldi's Basics Classic Remastered** that runs on the
Meta Quest 2 by itself, with no PC needed while you play.

**This mod does not include the game.** It has none of the game's pictures,
sounds, music, levels or code. You need your own copy of Baldi's Basics Classic
Remastered. All credit for the game goes to **Basically Games**.

## What it adds

- Full 360 VR: you're inside the school, and the menus fill the view all round.
- Your hands: point and touch to press doors, notebooks and buttons.
- The You Can Think Pad in your left hand, with your items, notebooks and stamina.
- Arm-swing running and smooth controller movement.
- Lighting styles in Settings: **Dynamic**, **Filmmaker mode**, **Normal**
  (the game's own light) and **Plus style** (the clean, bright look of Baldi's
  Basics Plus).
- The full Remastered learning song as soon as the You Can Think Pad opens.
- B-soda splashes on the walls, Baldi glitching on the style picker, and more.

## Install (Mac)

You need your own copy of the game, and **Unity 2020.3.48f1 with Android Build
Support** (the installer opens Unity Hub for you if it's missing).

1. Click the green **Code** button above, then **Download ZIP**, and unzip it.
2. Double-click **Install Baldi VR (Mac).command**. It finds your game and builds
   the app.
3. Plug in your Quest when it asks, and press **Allow**.

Then play **Baldi's Basics Private** from your Quest's App Library (filter:
**Unknown Sources**).

## How it works

Nothing of the game is downloaded. The installer uses your own copy:

- `patches/match-ids.pl` with `original-ids.txt` and `inner-ids.txt` give your
  unpacked copy the same file IDs the mod was made with. These lists hold
  only file names and IDs.
- `mod-files/` holds the mod's own code, shaders and settings.
- `patches/game-changes.patch` holds only the changed lines inside the game's
  scripts, prefabs and settings.
- `patches/files-to-delete.txt` lists game files the mod doesn't use (mostly
  an old controller plug-in), and `patches/copy-from-game.txt` lists a few of
  your copy's own files it reuses elsewhere.

## Not included

A few VR pictures were made from the game's own art, so they are not here: the
whole clipboard, the hand and cursor, the You Can Think Pad's shape, the upright
stamina meter and the 3D title screen layers. Without them those parts use the
game's plain look. If you make your own, put them in `Assets/Resources/VR/`
with these names: `ClipboardWhole.png`, `YCTP_Shape.png`, `HandCursor.png`,
`HandOpen.png`, `StaminaUpright.png`, `StaminaUprightBar.png`,
`StaminaUprightNeedle.png`, and in `Cover3D/`: `back.png`, `baldi.png`,
`banners.png`, `board.png`, `ribbon.png`, `title.png`.

## Please don't share the app

The Quest app the installer builds has the whole game inside it, so keep it to
yourself. Share this page instead.

The web page template in `mod-files/Assets/WebGLTemplates/` is from the free
WebXR Export project by De-Panther (Apache 2.0 licence), with changes.
