# Baldi VR mod (for Baldi's Basics Classic Remastered, Meta Quest 2)

This folder has **only the mod's own files**. It has none of the game's pictures,
sounds, music, levels or code. To use it, you need **your own copy** of
Baldi's Basics Classic Remastered (it's on Steam). All credit for the game goes
to Basically Games.

**Never share the finished app (.apk) or the web version.** Those have the whole
game built into them. Only share this folder.

## What's in here

- `mod-files/Assets/VR/`: all the VR code (the floating screen, full 360 VR,
  hands, the pad, menus, loading screen, speed-ups, BSODA splash, and so on).
  It also has the scripts that build the Quest app and the web version.
- `mod-files/Assets/Shader/` and `mod-files/Assets/Resources/VR/`: the
  hand-written shaders that replace the game's (theirs don't survive being
  unpacked), including the lighting.
- `mod-files/Assets/Material/WoodDesk.mat`: the desks' and chairs' colour.
- `mod-files/Assets/Scripts/Assembly-CSharp/LightingPage.cs`: the Lighting pages
  in Settings (Dynamic, Filmmaker mode, Normal, and 14 more looks from Snow day
  to Eclipse).
- `mod-files/Assets/Scripts/Assembly-CSharp/GlitchOnHover.cs`: Baldi's picture on
  the style picker glitches while the cursor is on it.
- `mod-files/Assets/WebGLTemplates/`: the web page, from the free WebXR Export
  project by De-Panther (Apache 2.0 licence), with changes.
- `patches/game-scripts.patch`: the few lines changed inside the game's own
  scripts (only the changed lines, not whole files).
- `patches/files-to-delete.txt`: game files to remove (mostly the Rewired
  controller plug-in, which doesn't survive unpacking and is replaced by
  `VR/RewiredShim.cs`).

## Not included

A few VR pictures were made from the game's own art (the whole clipboard, the
You Can Think Pad, the hands, the cursor and the 3D title screen pieces), so
they are not in this folder. Without them the mod still runs, with those parts
plain or missing. If you make your own pictures, put them in
`Assets/Resources/VR/` with these names: `ClipboardWhole.png`, `YCTP_Base.png`,
`YCTP_Shape.png`, `HandCursor.png`, `HandOpen.png`, `StaminaUpright.png`,
`StaminaUprightBar.png`, `StaminaUprightNeedle.png`, and in `Cover3D/`:
`back.png`, `baldi.png`, `banners.png`, `board.png`, `ribbon.png`, `title.png`.

## What you need

1. A PC or Mac.
2. Your own copy of **Baldi's Basics Classic Remastered** (Steam).
3. **AssetRipper** (free) to unpack your copy into a Unity project.
4. **Unity Hub** with **Unity 2020.3.48f1**, plus the **Android Build Support**
   (with OpenJDK, Android SDK and NDK) and **WebGL Build Support** modules.
5. **Git** (to apply the patch).
6. A **Meta Quest 2** with **Developer Mode** on, a USB-C cable, and
   **SideQuest** (free) to install the app.

## Steps

1. **Unpack your game.** Open AssetRipper and load your game's folder (in Steam,
   right-click the game, then Manage, then Browse local files). Export it as a
   Unity project. You'll get a folder called `ExportedProject`.
2. **Copy the mod in.** Copy everything inside `mod-files/` into
   `ExportedProject/`. Say yes to replacing files.
3. **Delete the listed files.** Delete every file listed in
   `patches/files-to-delete.txt` from `ExportedProject/`.
4. **Apply the script changes.** Open a terminal in `ExportedProject/` and run:
   `git apply --reject /path/to/patches/game-scripts.patch`
   If a few parts don't fit (your copy is a different game version), Git leaves
   `.rej` files showing what didn't apply. Make those few changes by hand.
5. **Add the VR packages.** In `ExportedProject/Packages/manifest.json`, add:
   - `"com.de-panther.webxr": "0.25.0"`
   - `"com.unity.xr.management": "4.2.1"`
   - `"com.unity.xr.oculus": "1.13.1"`
   - `"com.unity.xr.legacyinputhelpers": "2.1.10"`
6. **A few settings the mod changes** (do these in Unity):
   - Desks and chairs: in the prefabs `BigDesk`, `BigDesk_NoSpawn`,
     `Chairs_Desk_Base`, `Chairs_Desk_Perfect`, `NullBlocker_SmallDesk`,
     `NullProjectile_Desk`, `BillboardChair`, `Chair_Test`,
     `NullProjectile_Chair`, `RoundTable_Chairs_1` and `RoundTable_Chairs_2`,
     swap the `WoodSimple` material for `WoodDesk`.
   - Playtime's jump rope: in the `Jumprope` prefab, set `Rope Delay` to `1.7`
     (each jump is a second slower).
   - Oculus settings (Project Settings, XR Plug-in Management, Oculus): turn on
     `Phase Sync`.
   - App picture (optional): put a square PNG at `Assets/VR/Icon/AppIcon.png` and
     set it as the Oculus `System Splash Screen`.
7. **Open the project** in Unity 2020.3.48f1. The first time takes a while.
8. **Build the Quest app.** In Unity's top menu, choose
   **Baldi VR > Set up for Quest APK**, then **Baldi VR > Build Quest APK**.
   The app comes out at `Build/apk/BaldiVR.apk`.
9. **Put it on your Quest.** Plug the Quest into the computer with the USB-C
   cable, put it on, and allow USB debugging. Open SideQuest, then drag
   `BaldiVR.apk` onto it (or use its "Install APK" button).
10. **Play.** On the Quest, open your App Library, choose **Unknown Sources** from
    the filter, and open **Baldi's Basics Private**.

## Before you share it

Check Basically Games' rules on fan mods before you post this anywhere public.
Share only this folder, never the built app or web version. That is also why
this mod is not on SideQuest: SideQuest only takes finished apps, and a
finished app has the whole game inside it.
