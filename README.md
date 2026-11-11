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
- Hold a quarter up to a B-soda machine and it turns into a B-soda.
- B-soda splashes on the walls, Baldi glitching on the style picker, and more.

## Install

You need your own copy of Baldi's Basics Classic Remastered.

1. Click the green **Code** button above, then **Download ZIP**, and unzip it.
   On Windows, right-click the ZIP and choose **Extract All...**, then **Extract**.
2. Double-click the installer:
   - **Mac:** `Install Baldi VR (Mac).command`
   - **Windows:** `Install Baldi VR (Windows).bat`
3. Plug in your Quest when it asks, and press **Allow**.

Then play **Baldi's Basics Private** from your Quest's App Library (filter:
**Unknown Sources**).

The first time, the installer downloads and installs Unity (free, about 5 GB)
and builds the app from your copy, so it can take 30 to 60 minutes. If Unity
Hub pops up asking you to sign in, sign in or make a free Unity account, then
run the installer again. On Windows, if a blue "Windows protected your PC" box
appears, click **More info**, then **Run anyway**.

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

## The VR pictures

A few VR pictures (the hand, the whole clipboard, the You Can Think Pad's
shape, the upright stamina meter, the 3D title screen and the app icon) were
made from the game's own art, so the files aren't here. The installer makes
them again from your copy, the same way they were first made, so they come out
exactly the same. The mod only carries their outlines and touch-ups
(`mod-files/Assets/VR/Shapes~/`).

## Please don't share the app

The Quest app the installer builds has the whole game inside it, so keep it to
yourself. Share this page instead.

The web page template in `mod-files/Assets/WebGLTemplates/` is from the free
WebXR Export project by De-Panther (Apache 2.0 licence), with changes.
