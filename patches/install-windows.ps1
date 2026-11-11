# Baldi VR mod installer for Windows (run it with "Install Baldi VR (Windows).bat").
# It turns YOUR OWN copy of Baldi's Basics Classic Remastered into a Quest 2
# app with the VR mod, and puts it on your Quest. Nothing of the game is
# downloaded: it all comes from your copy. Works in Windows PowerShell 5.1.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Here = Split-Path -Parent $PSScriptRoot
$Work = if ($env:BALDIVR_WORK) { $env:BALDIVR_WORK } else { Join-Path $env:LOCALAPPDATA 'BaldiVR-Install' }
$EditorDir = 'C:\Program Files\Unity\Hub\Editor\2020.3.48f1\Editor'
$Unity = Join-Path $EditorDir 'Unity.exe'
$Android = Join-Path $EditorDir 'Data\PlaybackEngines\AndroidPlayer'
$Hub = 'C:\Program Files\Unity Hub\Unity Hub.exe'
$RipperVersion = '2.0.0'
$Port = 45678
# Bytes in, bytes out: files are read and written byte for byte.
$Bytes = [Text.Encoding]::GetEncoding(28591)

function Step($text) { Write-Host ''; Write-Host "=== $text ===" -ForegroundColor Cyan }
function Fail($text) {
    Write-Host ''
    Write-Host "PROBLEM: $text" -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}
function Download($url, $to) {
    try { Invoke-WebRequest -Uri $url -OutFile $to -UseBasicParsing }
    catch { Fail "Couldn't download $url. Check the internet." }
}

Clear-Host
Write-Host 'Baldi VR mod installer'
Write-Host 'This takes about 30 to 60 minutes the first time. Keep this window open.'
New-Item -ItemType Directory -Force -Path $Work | Out-Null

Step '1 of 7: Getting Unity (free; the first time this is a big download)'
if (-not (Test-Path $Unity) -or -not (Test-Path $Android)) {
    if (-not (Test-Path $Hub)) {
        Write-Host 'Downloading Unity Hub...'
        $setup = Join-Path $Work 'UnityHubSetup.exe'
        Download 'https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe' $setup
        Write-Host 'Installing Unity Hub. Windows may ask you to allow it.'
        Start-Process -FilePath $setup -ArgumentList '/S' -Wait -Verb RunAs
    }
    if (-not (Test-Path $Hub)) { Fail 'Unity Hub did not install.' }
    Write-Host 'Installing Unity 2020.3.48f1 with Android support. This takes a while...'
    if (-not (Test-Path $Unity)) {
        & $Hub -- --headless install --version 2020.3.48f1 --changeset b805b124c6b7 --module android android-sdk-ndk-tools android-open-jdk --childModules | Out-Host
    } else {
        & $Hub -- --headless install-modules --version 2020.3.48f1 --module android android-sdk-ndk-tools android-open-jdk --childModules | Out-Host
    }
    if (-not (Test-Path $Unity) -or -not (Test-Path $Android)) {
        Fail "Unity didn't finish installing. Open Unity Hub, install 2020.3.48f1 with Android Build Support, then run this again."
    }
}
Write-Host 'Unity is ready.'

Step '2 of 7: Finding your copy of the game'
function Is-Game($folder) {
    foreach ($data in (Get-ChildItem -Path $folder -Directory -Filter '*_Data' -ErrorAction SilentlyContinue)) {
        $info = Join-Path $data.FullName 'app.info'
        if ((Test-Path (Join-Path $data.FullName 'globalgamemanagers')) -and (Test-Path $info) -and ((Get-Content $info -Raw) -match 'Classic Remastered')) { return $true }
    }
    return $false
}
$Game = $null
$libraries = @('C:\Program Files (x86)\Steam', 'C:\Program Files\Steam')
$vdf = 'C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf'
if (Test-Path $vdf) {
    foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) { $libraries += $m.Groups[1].Value -replace '\\\\', '\' }
}
foreach ($library in $libraries) {
    $common = Join-Path $library 'steamapps\common'
    if (-not (Test-Path $common)) { continue }
    foreach ($folder in (Get-ChildItem -Path $common -Directory)) {
        if (Is-Game $folder.FullName) { $Game = $folder.FullName; break }
    }
    if ($Game) { break }
}
if (-not $Game) {
    Write-Host "I couldn't find it on my own."
    Write-Host "Drag your Baldi's Basics Classic Remastered game folder into this window, then press Enter:"
    $Game = (Read-Host).Trim().Trim('"')
}
if (-not (Test-Path $Game)) { Fail "That game wasn't found: $Game" }
Write-Host "Using: $Game"

Step '3 of 7: Unpacking your copy (about 5 minutes)'
$RipperDir = Join-Path $Work 'AssetRipper'
$Ripper = Join-Path $RipperDir 'AssetRipper.GUI.Free.exe'
if (-not (Test-Path $Ripper)) {
    $zip = Join-Path $Work 'ripper.zip'
    Download "https://github.com/AssetRipper/AssetRipper/releases/download/$RipperVersion/AssetRipper_win_x64.zip" $zip
    Expand-Archive -Path $zip -DestinationPath $RipperDir -Force
}
$Rip = Join-Path $Work 'Unpacked'
$Project = Join-Path $Rip 'ExportedProject'
if (-not (Test-Path (Join-Path $Project 'Assets'))) {
    $process = Start-Process -FilePath $Ripper -ArgumentList '--headless', '--port', $Port -PassThru -WindowStyle Hidden
    for ($i = 0; $i -lt 60; $i++) {
        try { Invoke-WebRequest -Uri "http://localhost:$Port/" -UseBasicParsing | Out-Null; break } catch { Start-Sleep -Seconds 1 }
    }
    try {
        Invoke-WebRequest -Uri "http://localhost:$Port/LoadFolder" -Method Post -Body @{ path = $Game } -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue | Out-Null
        Invoke-WebRequest -Uri "http://localhost:$Port/Export/UnityProject" -Method Post -Body @{ path = $Rip } -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue | Out-Null
    } catch { }
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}
if (-not (Test-Path (Join-Path $Project 'Assets'))) { Fail 'Unpacking did not work.' }
Write-Host 'Unpacked.'

# The same file IDs as the copy the mod was made on (match-ids.pl, in PowerShell).
function Match-Ids($project, $list, $inner) {
    $original = @{}
    foreach ($line in [IO.File]::ReadAllLines($list)) {
        $parts = $line -split "`t"
        if ($parts.Count -ge 2) { $original[$parts[0]] = $parts[1] }
    }
    $swap = @{}
    $root = (Resolve-Path $project).Path
    foreach ($meta in (Get-ChildItem -Path (Join-Path $root 'Assets') -Recurse -File -Filter '*.meta')) {
        $rel = $meta.FullName.Substring($root.Length + 1).Replace('\', '/')
        $rel = $rel.Substring(0, $rel.Length - 5)
        if (-not $original.ContainsKey($rel)) { continue }
        foreach ($line in [IO.File]::ReadLines($meta.FullName)) {
            if ($line -match '^guid: ([0-9a-f]{32})') {
                if ($Matches[1] -ne $original[$rel]) { $swap[$Matches[1]] = $original[$rel] }
                break
            }
        }
    }
    $evaluator = [Text.RegularExpressions.MatchEvaluator] { param($m) if ($swap.ContainsKey($m.Value)) { $swap[$m.Value] } else { $m.Value } }
    $guid = [regex]'\b[0-9a-f]{32}\b'
    foreach ($folder in @('Assets', 'ProjectSettings')) {
        foreach ($file in (Get-ChildItem -Path (Join-Path $root $folder) -Recurse -File)) {
            $isMeta = $file.Name.EndsWith('.meta')
            if (-not $isMeta) {
                $stream = [IO.File]::OpenRead($file.FullName)
                $head = New-Object byte[] 5
                $read = $stream.Read($head, 0, 5)
                $stream.Close()
                if ($read -lt 5 -or $Bytes.GetString($head) -ne '%YAML') { continue }
            }
            $text = [IO.File]::ReadAllText($file.FullName, $Bytes)
            $new = $guid.Replace($text, $evaluator)
            if ($new -ne $text) { [IO.File]::WriteAllText($file.FullName, $new, $Bytes) }
        }
    }
    # IDs inside a file (the sound mixers), by line.
    $byFile = @{}
    foreach ($line in [IO.File]::ReadAllLines($inner)) {
        $parts = $line -split "`t"
        if ($parts.Count -lt 3) { continue }
        if (-not $byFile.ContainsKey($parts[0])) { $byFile[$parts[0]] = @() }
        $byFile[$parts[0]] += , @([int]$parts[1], $parts[2])
    }
    foreach ($file in $byFile.Keys) {
        $path = Join-Path $root $file
        if (-not (Test-Path $path)) { continue }
        $text = [IO.File]::ReadAllText($path, $Bytes)
        $lines = $text -split "`n"
        $local = @{}
        foreach ($pair in $byFile[$file]) {
            $n = $pair[0]
            if ($n -le $lines.Count -and $lines[$n - 1] -match '\b([0-9a-f]{32})\b') { $local[$Matches[1]] = $pair[1] }
        }
        $inside = [Text.RegularExpressions.MatchEvaluator] { param($m) if ($local.ContainsKey($m.Value)) { $local[$m.Value] } else { $m.Value } }
        [IO.File]::WriteAllText($path, $guid.Replace($text, $inside), $Bytes)
    }
    Write-Host "$($swap.Count) IDs matched"
}

# Applies a unified diff (the mod's changes) exactly, the way "patch" does.
function Apply-Patch($project, $patchFile) {
    $root = (Resolve-Path $project).Path
    $all = [IO.File]::ReadAllText($patchFile, $Bytes) -split "`n"
    $failed = 0
    $i = 0
    while ($i -lt $all.Count) {
        if (-not $all[$i].StartsWith('--- a/')) { $i++; continue }
        $rel = ($all[$i].Substring(6) -split "`t")[0]
        $i += 2
        $path = Join-Path $root $rel
        if (-not (Test-Path $path)) { Write-Host "  missing: $rel"; $failed++; continue }
        $text = [IO.File]::ReadAllText($path, $Bytes)
        $finalNewline = $text.EndsWith("`n")
        $lines = New-Object System.Collections.Generic.List[string]
        $lines.AddRange([string[]]($text -split "`n"))
        if ($finalNewline) { $lines.RemoveAt($lines.Count - 1) }
        $offset = 0
        while ($i -lt $all.Count -and $all[$i].StartsWith('@@')) {
            $m = [regex]::Match($all[$i], '^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@')
            $oldStart = [int]$m.Groups[1].Value
            $i++
            $old = New-Object System.Collections.Generic.List[string]
            $new = New-Object System.Collections.Generic.List[string]
            $last = ''
            while ($i -lt $all.Count -and -not $all[$i].StartsWith('@@') -and -not $all[$i].StartsWith('--- a/')) {
                $l = $all[$i]
                if ($l.StartsWith('\')) {
                    # "No newline at end of file": after a removed line only the
                    # old file lacked it; otherwise the new one does too.
                    $finalNewline = ($last -eq '-')
                } elseif ($l.Length -gt 0 -and ($l[0] -eq ' ' -or $l[0] -eq '-' -or $l[0] -eq '+')) {
                    $body = $l.Substring(1)
                    if ($l[0] -ne '+') { $old.Add($body) }
                    if ($l[0] -ne '-') { $new.Add($body) }
                    $last = [string]$l[0]
                } elseif ($l.Length -eq 0 -and $i -eq $all.Count - 1) {
                } else { break }
                $i++
            }
            $start = if ($old.Count -eq 0) { $oldStart + $offset } else { $oldStart - 1 + $offset }
            $at = -1
            for ($d = 0; $d -le 200 -and $at -lt 0; $d++) {
                foreach ($try in @(($start + $d), ($start - $d))) {
                    if ($try -lt 0 -or $try + $old.Count -gt $lines.Count) { continue }
                    $same = $true
                    for ($k = 0; $k -lt $old.Count; $k++) { if ($lines[$try + $k] -cne $old[$k]) { $same = $false; break } }
                    if ($same) { $at = $try; break }
                }
            }
            if ($at -lt 0) { Write-Host "  didn't fit: $rel"; $failed++; continue }
            $lines.RemoveRange($at, $old.Count)
            $lines.InsertRange($at, $new)
            $offset += $new.Count - $old.Count + ($at - $start)
        }
        $out = [string]::Join("`n", $lines)
        if ($finalNewline) { $out += "`n" }
        [IO.File]::WriteAllText($path, $out, $Bytes)
    }
    return $failed
}

Step '4 of 7: Adding the mod'
$Done = Join-Path $Project '.mod-added'
if (-not (Test-Path $Done)) {
    Match-Ids $Project (Join-Path $Here 'patches\original-ids.txt') (Join-Path $Here 'patches\inner-ids.txt')
    foreach ($gone in [IO.File]::ReadAllLines((Join-Path $Here 'patches\files-to-delete.txt'))) {
        if ($gone) { Remove-Item -LiteralPath (Join-Path $Project $gone) -Recurse -Force -ErrorAction SilentlyContinue }
    }
    # The old controller plug-in's Windows pieces too (the mod replaces it).
    Get-ChildItem -Path (Join-Path $Project 'Assets\Plugins') -Filter 'Rewired*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $Here 'mod-files\*') -Destination $Project -Recurse -Force
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $Here 'patches\copy-from-game.txt'))) {
        $parts = $line -split "`t"
        if ($parts.Count -lt 2) { continue }
        $to = Join-Path $Project $parts[1]
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $to) | Out-Null
        Copy-Item -LiteralPath (Join-Path $Project $parts[0]) -Destination $to -Force
    }
    # Each copy's .meta files have their own timestamps; without them the changes fit any copy.
    foreach ($meta in (Get-ChildItem -Path (Join-Path $Project 'Assets') -Recurse -File -Filter '*.meta')) {
        $text = [IO.File]::ReadAllText($meta.FullName, $Bytes)
        $new = [regex]::Replace($text, '(?m)^timeCreated: \d+\n', '')
        if ($new -ne $text) { [IO.File]::WriteAllText($meta.FullName, $new, $Bytes) }
    }
    $failed = Apply-Patch $Project (Join-Path $Here 'patches\game-changes.patch')
    if ($failed -gt 0) { Write-Host "($failed changes didn't fit exactly.)" }
    Copy-Item -LiteralPath (Join-Path $Here 'patches\manifest.json') -Destination (Join-Path $Project 'Packages\manifest.json') -Force
    # Meta's VR system for the Quest, from Unity's own package site.
    $tgz = Join-Path $Work 'oculus.tgz'
    Download 'https://download.packages.unity.com/com.unity.xr.oculus/-/com.unity.xr.oculus-1.13.1.tgz' $tgz
    $unpack = Join-Path $Work 'oculus'
    New-Item -ItemType Directory -Force -Path $unpack | Out-Null
    tar -xzf $tgz -C $unpack
    $package = Join-Path $Project 'Packages\com.unity.xr.oculus'
    Remove-Item -LiteralPath $package -Recurse -Force -ErrorAction SilentlyContinue
    Move-Item -LiteralPath (Join-Path $unpack 'package') -Destination $package
    # The game brings its own copy of Unity's UI code, so the package must not ask for it too.
    $asmdef = Join-Path $package 'Runtime\Unity.XR.Oculus.asmdef'
    [IO.File]::WriteAllText($asmdef, ([IO.File]::ReadAllText($asmdef) -replace ',\s*"Unity\.ugui"', '' -replace '"Unity\.ugui",\s*', ''))
    $json = Join-Path $package 'package.json'
    [IO.File]::WriteAllText($json, ([IO.File]::ReadAllText($json) -replace ',\s*"com\.unity\.ugui":\s*"1\.0\.0"', ''))
    New-Item -ItemType File -Force -Path $Done | Out-Null
}
Write-Host 'Mod added.'

Step '5 of 7: Building the Quest app (20 to 40 minutes)'
Write-Host "Unity is working in the background. It's slow the first time, that's normal."
Remove-Item -LiteralPath (Join-Path $Project 'Temp\UnityLockfile') -Force -ErrorAction SilentlyContinue
$log = Join-Path $Work 'build.txt'
Start-Process -FilePath $Unity -ArgumentList '-batchmode', '-quit', '-projectPath', "`"$Project`"", '-buildTarget', 'Android', '-executeMethod', 'ModSetup.SetUpAndBuild', '-logFile', "`"$log`"" -Wait -WindowStyle Hidden
$Apk = Join-Path $Work 'Build\apk\BaldiVR.apk'
if (-not (Test-Path $Apk)) {
    if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'license' -Quiet)) {
        Write-Host 'Unity needs you signed in to a free Unity account (one time only).'
        Write-Host 'Unity Hub is opening: sign in or make a free account, then run this again.'
        Start-Process -FilePath $Hub
        Fail 'Sign in to Unity Hub first.'
    }
    Fail "The build didn't finish. The details are in $log"
}
Write-Host 'Built.'
if ($env:BALDIVR_NOINSTALL) { Write-Host "Built: $Apk"; exit 0 }

Step '6 of 7: Getting the Quest installer tool'
$Adb = Join-Path $Work 'platform-tools\adb.exe'
if (-not (Test-Path $Adb)) {
    $zip = Join-Path $Work 'platform-tools.zip'
    Download 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' $zip
    Expand-Archive -Path $zip -DestinationPath $Work -Force
}

Step '7 of 7: Putting it on your Quest'
Write-Host 'Plug your Quest into this PC with a USB-C cable (one that carries data) and put it on.'
Write-Host "If it asks 'Allow USB debugging?', tick 'Always allow' and press Allow."
& $Adb wait-for-device
& $Adb install -r $Apk
if ($LASTEXITCODE -ne 0) { Fail "Installing on the Quest didn't work. Is the cable a data cable?" }

Write-Host ''
Write-Host "Done! On your Quest: App Library > filter 'Unknown Sources' > Baldi's Basics Private."
Read-Host 'Press Enter to close'
