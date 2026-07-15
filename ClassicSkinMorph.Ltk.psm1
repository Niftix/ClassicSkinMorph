Set-StrictMode -Version Latest

function Write-Utf8Json {
    param([Parameter(Mandatory)]$Value, [Parameter(Mandatory)][string]$Path, [int]$Depth = 10)
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $json = $Value | ConvertTo-Json -Depth $Depth
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Get-FantomeMetadata {
    param([Parameter(Mandatory)][string]$Path)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.GetEntry('META/info.json')
        if (-not $entry) { throw "META/info.json is missing from $(Split-Path -Leaf $Path)" }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { return ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() }
    } finally { $archive.Dispose() }
}

function Get-DefaultLtkSettings {
    param([string]$LeaguePath)
    [ordered]@{
        leaguePath = $LeaguePath; modStoragePath = $null; workshopPath = $null
        firstRunComplete = $true; theme = 'system'; accentColor = [ordered]@{ preset=$null; customHue=$null }
        backdropImage=$null; backdropBlur=$null; libraryViewMode=$null; patchTft=$false
        minimizeToTray=$true; startInTray=$true; autoRun=$false; startInTrayUnlessUpdate=$false
        alwaysStartPatcher=$true; migrationDismissed=$false; reloadModsHotkey=$null
        killLeagueHotkey=$null; killLeagueStopsPatcher=$true; trustedDomains=@('runeforge.dev','divineskins.gg')
        watcherEnabled=$false; blockScriptsWad=$true; linkedBinCheckEnabled=$true; wadBlocklist=@()
        authorProfiles=@(); defaultAuthorProfileId=$null; hasSeenHddWarning=$true; elevateInjector=$false
        autoCategorizationEnabled=$true; enforceSkinhackScan=$true; applyStringOverridesToAllLocales=$false
    }
}

function Restore-ClassicLtkSession {
    param([Parameter(Mandatory)][string]$SessionPath, [switch]$SkipProcessControl)
    if (-not (Test-Path -LiteralPath $SessionPath)) { return }
    $session = Get-Content -LiteralPath $SessionPath -Raw | ConvertFrom-Json

    if (-not $SkipProcessControl) {
        Get-Process -Name 'ltk-manager','ltk_patcher_host','cslol-host' -ErrorAction SilentlyContinue |
            Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }

    foreach ($item in @($session.backups)) {
        if ([bool]$item.existed -and (Test-Path -LiteralPath $item.backup)) {
            Copy-Item -LiteralPath $item.backup -Destination $item.path -Force
        } elseif (-not [bool]$item.existed -and (Test-Path -LiteralPath $item.path)) {
            Remove-Item -LiteralPath $item.path -Force -ErrorAction SilentlyContinue
        }
    }
    $runtimeItems = if ($session.PSObject.Properties['runtimeBackups']) { @($session.runtimeBackups) } else { @() }
    foreach ($item in $runtimeItems) {
        if ([bool]$item.existed -and (Test-Path -LiteralPath $item.backup)) {
            Copy-Item -LiteralPath $item.backup -Destination $item.path -Force
        } elseif (-not [bool]$item.existed -and (Test-Path -LiteralPath $item.path)) {
            Remove-Item -LiteralPath $item.path -Force -ErrorAction SilentlyContinue
        }
    }
    foreach ($id in @($session.modIds)) {
        $archive = Join-Path $session.dataRoot "archives\$id.fantome"
        $mod = Join-Path $session.dataRoot "mods\$id"
        if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $mod) { Remove-Item -LiteralPath $mod -Recurse -Force -ErrorAction SilentlyContinue }
    }
    $backupRoot = Split-Path -Parent $SessionPath
    Remove-Item -LiteralPath $SessionPath -Force -ErrorAction SilentlyContinue
    $backupDirectory = Join-Path $backupRoot 'ltk-backup'
    if (Test-Path -LiteralPath $backupDirectory) {
        Remove-Item -LiteralPath $backupDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Start-ClassicLtkSession {
    param(
        [Parameter(Mandatory)][string]$LtkExe,
        [Parameter(Mandatory)][string]$ModLibrary,
        [Parameter(Mandatory)][string]$ChampionsDirectory,
        [Parameter(Mandatory)][string]$SessionPath,
        [scriptblock]$ProgressCallback,
        [switch]$SkipProcessCheck
    )
    Restore-ClassicLtkSession -SessionPath $SessionPath -SkipProcessControl:$SkipProcessCheck
    $activeLtkProcesses = @(Get-Process -Name 'ltk-manager','ltk_patcher_host','cslol-host' -ErrorAction SilentlyContinue |
        Where-Object { -not $_.HasExited })
    if (-not $SkipProcessCheck -and $activeLtkProcesses.Count -gt 0) {
        throw 'Close LTK Manager before starting Classic Skin Morph.'
    }
    # CIM also catches half-terminated League processes left behind by a crash.
    # The patcher enumerates those processes at a lower level and may otherwise
    # try to attach to an invalid instance.
    $leagueProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'League of Legends.exe'" -ErrorAction SilentlyContinue)
    if (-not $SkipProcessCheck -and $leagueProcesses.Count -gt 0) {
        throw 'League processes are still running or stuck after a crash. Restart Windows, then start Classic Skin Morph before League.'
    }

    $packages = @(Get-ChildItem -LiteralPath $ModLibrary -Filter '*.fantome' -File | Sort-Object Name)
    if ($packages.Count -eq 0) { throw 'No .fantome packages were found.' }
    if (-not (Test-Path -LiteralPath $LtkExe -PathType Leaf)) { throw 'LTK engine not found.' }

    $dataRoot = Join-Path $env:APPDATA 'dev.leaguetoolkit.manager'
    $profileRoot = Join-Path $dataRoot 'profiles\default'
    $backupRoot = Join-Path (Split-Path -Parent $SessionPath) 'ltk-backup'
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    foreach ($directory in @('archives','mods','profiles\default')) {
        New-Item -ItemType Directory -Path (Join-Path $dataRoot $directory) -Force | Out-Null
    }

    $settingsPath = Join-Path $dataRoot 'settings.json'
    $libraryPath = Join-Path $dataRoot 'library.json'
    $overlayPath = Join-Path $profileRoot 'overlay.json'
    $backups = @()
    foreach ($path in @($settingsPath,$libraryPath,$overlayPath)) {
        $name = [IO.Path]::GetFileName($path)
        $backup = Join-Path $backupRoot $name
        $exists = Test-Path -LiteralPath $path -PathType Leaf
        if ($exists) { Copy-Item -LiteralPath $path -Destination $backup -Force }
        $backups += [pscustomobject]@{ path=$path; backup=$backup; existed=$exists }
    }

    $runtimeRoot = Split-Path -Parent $LtkExe
    $session = [ordered]@{ dataRoot=$dataRoot; backups=$backups; modIds=@() }
    Write-Utf8Json -Value $session -Path $SessionPath
    try {
        $library = if (Test-Path -LiteralPath $libraryPath) {
            Get-Content -LiteralPath $libraryPath -Raw | ConvertFrom-Json
        } else { [pscustomobject]@{ version=1; mods=@() } }
        $entries = @($library.mods)
        $ids = @()
        foreach ($package in $packages) {
            $metadata = Get-FantomeMetadata -Path $package.FullName
            $id = [guid]::NewGuid().ToString()
            $ids += $id
            $session.modIds = @($ids)
            Write-Utf8Json -Value $session -Path $SessionPath
            Copy-Item -LiteralPath $package.FullName -Destination (Join-Path $dataRoot "archives\$id.fantome")
            $configDirectory = Join-Path $dataRoot "mods\$id"
            New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
            $safeName = (([string]$metadata.Name).ToLowerInvariant() -replace '[^a-z0-9]+','-').Trim('-')
            $config = [ordered]@{
                name=$safeName; display_name=[string]$metadata.Name; version=[string]$metadata.Version
                description=[string]$metadata.Description; authors=@([string]$metadata.Author)
                layers=@([ordered]@{name='base';priority=0;description='Base layer of the mod'})
            }
            Write-Utf8Json -Value $config -Path (Join-Path $configDirectory 'mod.config.json')
            $entries += [pscustomobject]@{ id=$id; installedAt=(Get-Date).ToUniversalTime().ToString('o'); format='fantome' }
            if ($ProgressCallback) { & $ProgressCallback $ids.Count $packages.Count }
        }
        $library.mods = @($entries)
        Write-Utf8Json -Value $library -Path $libraryPath
        $temporaryOverlay = [ordered]@{
            version=5
            enabledMods=@($ids)
            modFingerprints=[ordered]@{}
            gameFingerprint=0
            blockedWads=@('map22.wad.client','scripts.wad.client')
            stringOverrideLocales=@()
            wadFingerprints=[ordered]@{}
            linkedBinOffenders=@()
        }
        Write-Utf8Json -Value $temporaryOverlay -Path $overlayPath

        $leaguePath = [IO.Path]::GetFullPath((Join-Path $ChampionsDirectory '..\..\..\..'))
        $settings = if (Test-Path -LiteralPath $settingsPath) {
            Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        } else { Get-DefaultLtkSettings -LeaguePath $leaguePath }
        $settings.leaguePath = $leaguePath.Replace('\','/')
        $settings.firstRunComplete = $true
        $settings.minimizeToTray = $true
        $settings.startInTray = $true
        # LTK 1.11 reveals its window when an update exists if this is enabled.
        # Keep it disabled so the embedded engine starts silently in the tray.
        $settings.startInTrayUnlessUpdate = $false
        $settings.alwaysStartPatcher = $true
        Write-Utf8Json -Value $settings -Path $settingsPath

        $session.modIds = @($ids)
        Write-Utf8Json -Value $session -Path $SessionPath
        $process = Start-Process -FilePath $LtkExe -WorkingDirectory $runtimeRoot -WindowStyle Hidden -PassThru
        return [pscustomobject]@{ processId=$process.Id; packageCount=$ids.Count; sessionPath=$SessionPath }
    } catch {
        Restore-ClassicLtkSession -SessionPath $SessionPath -SkipProcessControl:$SkipProcessCheck
        throw
    }
}

function Test-ClassicLtkReady {
    [bool](@(Get-Process -Name 'ltk_patcher_host','cslol-host' -ErrorAction SilentlyContinue |
        Where-Object { -not $_.HasExited }).Count -gt 0)
}

function Hide-ClassicLtkWindow {
    if (-not ('ClassicSkinMorph.NativeWindow' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace ClassicSkinMorph {
    public static class NativeWindow {
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    }
}
'@
    }
    Get-Process -Name 'ltk-manager' -ErrorAction SilentlyContinue | ForEach-Object {
        $_.Refresh()
        if ($_.MainWindowHandle -ne [IntPtr]::Zero) {
            [ClassicSkinMorph.NativeWindow]::ShowWindowAsync($_.MainWindowHandle, 0) | Out-Null
        }
    }
}

function Stop-ClassicLtkSession {
    param([Parameter(Mandatory)][string]$SessionPath, [switch]$SkipProcessControl)
    Restore-ClassicLtkSession -SessionPath $SessionPath -SkipProcessControl:$SkipProcessControl
}

Export-ModuleMember -Function Start-ClassicLtkSession,Stop-ClassicLtkSession,Test-ClassicLtkReady,Hide-ClassicLtkWindow,Restore-ClassicLtkSession
