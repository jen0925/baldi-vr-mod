#!/bin/bash
# Baldi VR mod installer for Mac.
# Double-click this file. It turns YOUR OWN copy of Baldi's Basics Classic
# Remastered into a Quest 2 app with the VR mod, and puts it on your Quest.
# Nothing of the game is downloaded: it all comes from your copy.

set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="${BALDIVR_WORK:-$HOME/BaldiVR-Install}"
UNITY="/Applications/Unity/Hub/Editor/2020.3.48f1/Unity.app/Contents/MacOS/Unity"
ANDROID="/Applications/Unity/Hub/Editor/2020.3.48f1/PlaybackEngines/AndroidPlayer"
RIPPER_VERSION="2.0.0"
PORT=45678

step() { echo; echo "=== $1 ==="; }
fail() { echo; echo "PROBLEM: $1"; echo; read -n 1 -s -r -p "Press any key to close."; exit 1; }

clear 2>/dev/null || true
echo "Baldi VR mod installer"
echo "This takes about 30 to 60 minutes the first time. Keep this window open."
mkdir -p "$WORK"

step "1 of 7: Checking Unity"
if [ ! -x "$UNITY" ] || [ ! -d "$ANDROID" ]; then
    echo "You need Unity 2020.3.48f1 with Android Build Support."
    echo "Unity Hub will open now. Install that version and tick"
    echo "'Android Build Support' (with OpenJDK and Android SDK & NDK)."
    echo "Then double-click this installer again."
    open "unityhub://2020.3.48f1/b805b124c6b7" || open "https://unity.com/download"
    fail "Unity 2020.3.48f1 with Android Build Support is not installed yet."
fi
echo "Unity is ready."

step "2 of 7: Finding your copy of the game"
GAME=""
for place in "$HOME/Library/Application Support/Steam/steamapps/common"/*; do
    for app in "$place"/*.app; do
        [ -d "$app/Contents/Resources/Data" ] && grep -qi "baldi" <<< "$place$app" && GAME="$app"
    done
done
# Also anywhere usual: Applications, Downloads, Desktop, Documents.
if [ -z "$GAME" ]; then
    for app in /Applications/*.app "$HOME/Applications"/*.app "$HOME/Downloads"/*.app "$HOME/Desktop"/*.app "$HOME/Documents"/*.app; do
        [ -f "$app/Contents/Resources/Data/globalgamemanagers" ] || continue
        grep -qi "Classic Remastered" <<< "$(defaults read "$app/Contents/Info" CFBundleName 2>/dev/null)" && { GAME="$app"; break; }
    done
fi
if [ -z "$GAME" ]; then
    echo "I couldn't find it on my own."
    echo "Drag your Baldi's Basics Classic Remastered app (or its game folder)"
    echo "into this window, then press Return:"
    read -r GAME
    # A dragged-in path: no spaces or quotes round it, and "\ " back to " ".
    GAME="$(printf '%s' "$GAME" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e "s/^'//" -e "s/'$//" -e 's/\\\(.\)/\1/g')"
fi
[ -e "$GAME" ] || fail "That game wasn't found: $GAME"
echo "Using: $GAME"

step "3 of 7: Unpacking your copy (about 5 minutes)"
RIPPER_DIR="$WORK/AssetRipper"
if [ ! -x "$RIPPER_DIR/AssetRipper.GUI.Free" ]; then
    ARCH=$( [ "$(uname -m)" = "arm64" ] && echo arm64 || echo x64 )
    mkdir -p "$RIPPER_DIR"
    curl -L --fail -o "$WORK/ripper.tar.xz" "https://github.com/AssetRipper/AssetRipper/releases/download/$RIPPER_VERSION/AssetRipper_mac_$ARCH.tar.xz" || fail "Couldn't download AssetRipper. Check the internet."
    tar -xf "$WORK/ripper.tar.xz" -C "$RIPPER_DIR"
    xattr -dr com.apple.quarantine "$RIPPER_DIR" 2>/dev/null || true
    chmod +x "$RIPPER_DIR/AssetRipper.GUI.Free"
fi
RIP="$WORK/Unpacked"
PROJECT="$RIP/ExportedProject"
if [ ! -d "$PROJECT/Assets" ]; then
    "$RIPPER_DIR/AssetRipper.GUI.Free" --headless --port $PORT > "$WORK/ripper.txt" 2>&1 &
    RIPPER_PID=$!
    for i in $(seq 1 60); do curl -s -o /dev/null "http://localhost:$PORT/" && break; sleep 1; done
    curl -s -o /dev/null --data-urlencode "path=$GAME" "http://localhost:$PORT/LoadFolder" || fail "AssetRipper couldn't open the game."
    curl -s -o /dev/null --data-urlencode "path=$RIP" "http://localhost:$PORT/Export/UnityProject" || fail "AssetRipper couldn't unpack the game."
    kill $RIPPER_PID 2>/dev/null || true
fi
[ -d "$PROJECT/Assets" ] || fail "Unpacking didn't work. See $WORK/ripper.txt"
echo "Unpacked."

step "4 of 7: Adding the mod"
if [ ! -f "$PROJECT/.mod-added" ]; then
    # The same file IDs as the copy the mod was made on.
    perl "$HERE/patches/match-ids.pl" "$PROJECT" "$HERE/patches/original-ids.txt" "$HERE/patches/inner-ids.txt"
    while IFS= read -r gone; do
        [ -n "$gone" ] && rm -rf "${PROJECT:?}/$gone"
    done < "$HERE/patches/files-to-delete.txt"
    cp -R "$HERE/mod-files/." "$PROJECT/"
    # A few of the game's own files the mod uses in other places, copied from your copy.
    while IFS=$'\t' read -r from to; do
        [ -n "$from" ] && mkdir -p "$(dirname "$PROJECT/$to")" && cp "$PROJECT/$from" "$PROJECT/$to"
    done < "$HERE/patches/copy-from-game.txt"
    # Each copy's .meta files have their own timestamps; without them the changes fit any copy.
    find "$PROJECT/Assets" -name '*.meta' -exec perl -ni -e 'print unless /^timeCreated: \d+$/' {} +
    # The mod's changes inside the game's scripts, prefabs, scenes and settings.
    ( cd "$PROJECT" && patch -p1 --forward --no-backup-if-mismatch -r - < "$HERE/patches/game-changes.patch" ) > "$WORK/patch.txt" 2>&1 || echo "(Some changes didn't fit exactly; see $WORK/patch.txt)"
    cp "$HERE/patches/manifest.json" "$PROJECT/Packages/manifest.json"
    # Meta's VR system for the Quest, from Unity's own package site.
    curl -L --fail -o "$WORK/oculus.tgz" "https://download.packages.unity.com/com.unity.xr.oculus/-/com.unity.xr.oculus-1.13.1.tgz" || fail "Couldn't download the Oculus package."
    mkdir -p "$WORK/oculus" && tar -xzf "$WORK/oculus.tgz" -C "$WORK/oculus"
    rm -rf "${PROJECT:?}/Packages/com.unity.xr.oculus"
    mv "$WORK/oculus/package" "$PROJECT/Packages/com.unity.xr.oculus"
    # The game brings its own copy of Unity's UI code, so the package must not ask for it too.
    perl -0pi -e 's/,\s*"Unity\.ugui"//; s/"Unity\.ugui",\s*//' "$PROJECT/Packages/com.unity.xr.oculus/Runtime/Unity.XR.Oculus.asmdef"
    perl -0pi -e 's/,\s*"com\.unity\.ugui":\s*"1\.0\.0"//' "$PROJECT/Packages/com.unity.xr.oculus/package.json"
    touch "$PROJECT/.mod-added"
fi
echo "Mod added."

step "5 of 7: Building the Quest app (20 to 40 minutes)"
echo "Unity is working in the background. It's slow the first time, that's normal."
rm -f "$PROJECT/Temp/UnityLockfile"
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -buildTarget Android -executeMethod ModSetup.SetUpAndBuild -logFile "$WORK/build.txt" || true
APK="$WORK/Build/apk/BaldiVR.apk"
[ -f "$APK" ] || fail "The build didn't finish. The details are in $WORK/build.txt"
echo "Built."

[ -n "$BALDIVR_NOINSTALL" ] && { echo "Built: $APK"; exit 0; }
step "6 of 7: Getting the Quest installer tool"
ADB="$WORK/platform-tools/adb"
if [ ! -x "$ADB" ]; then
    curl -L --fail -o "$WORK/platform-tools.zip" "https://dl.google.com/android/repository/platform-tools-latest-darwin.zip" || fail "Couldn't download Google's platform tools."
    unzip -q -o "$WORK/platform-tools.zip" -d "$WORK"
fi

step "7 of 7: Putting it on your Quest"
echo "Plug your Quest into this Mac with a USB-C cable and put it on."
echo "If it asks 'Allow USB debugging?', tick 'Always allow' and press Allow."
"$ADB" wait-for-device
"$ADB" install -r "$APK" || fail "Installing on the Quest didn't work. Is Developer Mode on?"

echo
echo "Done! On your Quest: App Library > filter 'Unknown Sources' > Baldi's Basics Private."
read -n 1 -s -r -p "Press any key to close."
