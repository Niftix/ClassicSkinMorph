Set-StrictMode -Version Latest

function Get-ClassicArchiveIndex {
    param([Parameter(Mandatory)][string]$ChampionsDirectory)

    $index = @{}
    if (-not (Test-Path -LiteralPath $ChampionsDirectory -PathType Container)) {
        return $index
    }

    Get-ChildItem -LiteralPath $ChampionsDirectory -Filter '*.wad.client' -File |
        Where-Object { $_.Name -notmatch '\.[a-z]{2}_[A-Z]{2}\.wad\.client$' } |
        ForEach-Object {
            $name = $_.Name -replace '\.wad\.client$', ''
            $index[$name.ToLowerInvariant()] = $_.FullName
        }
    return $index
}

function ConvertTo-ChampionKey {
    param([AllowNull()][string]$Name)
    if ([string]::IsNullOrWhiteSpace($Name)) { return '' }
    $key = ($Name -replace "[^a-zA-Z0-9]", '').ToLowerInvariant()
    if ($key -eq 'nunuwillump') { return 'nunu' }
    if ($key -eq 'monkeyking') { return 'wukong' }
    return $key
}

function Get-LivePlayers {
    param(
        [string]$Endpoint = 'https://127.0.0.1:2999/liveclientdata/playerlist',
        [int]$TimeoutSeconds = 2
    )

    try {
        # curl.exe gere de facon fiable le certificat local auto-signe, y compris
        # sous Windows PowerShell 5.1 ou Invoke-RestMethod refuse ce certificat.
        $curl = (Get-Command curl.exe -ErrorAction Stop).Source
        $json = & $curl --silent --show-error --insecure --max-time $TimeoutSeconds $Endpoint 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($json -join ''))) {
            return @()
        }
        $parsed = (($json -join "`n") | ConvertFrom-Json)
        # Windows PowerShell 5.1 peut conserver le tableau JSON comme un objet
        # imbrique unique. Le pipeline force une sortie joueur par joueur.
        return $parsed | ForEach-Object { $_ }
    } catch {
        return @()
    }
}

function New-ClassicSelection {
    param(
        [Parameter(Mandatory)][object[]]$Players,
        [Parameter(Mandatory)][hashtable]$ArchiveIndex,
        [Parameter(Mandatory)][string]$ModLibrary
    )

    $seen = @{}
    $flatPlayers = @($Players | ForEach-Object { $_ })
    $selected = foreach ($player in $flatPlayers) {
        if ($null -eq $player) { continue }
        $championProperty = $player.PSObject.Properties['championName']
        if ($null -eq $championProperty) { continue }
        $champion = [string]$championProperty.Value
        $key = ConvertTo-ChampionKey $champion
        if (-not $key -or $seen.ContainsKey($key)) { continue }
        $seen[$key] = $true

        $archiveKey = @($ArchiveIndex.Keys | Where-Object { (ConvertTo-ChampionKey $_) -eq $key } | Select-Object -First 1)
        if ($archiveKey.Count -eq 0) { continue }

        $packages = @(
            Join-Path $ModLibrary "$key-classic.fantome"
            Join-Path $ModLibrary "$key-base.fantome"
            Join-Path $ModLibrary "$key.fantome"
            Join-Path $ModLibrary "$key.zip"
            Join-Path $ModLibrary $key
        )
        $package = $packages | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        $variant = if ($package -and ([IO.Path]::GetFileName([string]$package) -like '*-classic.fantome')) {
            'Classic'
        } elseif ($package -and ([IO.Path]::GetFileName([string]$package) -like '*-base.fantome')) {
            'Basic'
        } else {
            'Indisponible'
        }

        [pscustomobject]@{
            champion = $champion
            key = $key
            pbeArchive = $ArchiveIndex[$archiveKey[0]]
            package = if ($package) { [string]$package } else { $null }
            ready = [bool]$package
            variant = $variant
        }
    }
    return @($selected)
}

function Write-SelectionManifest {
    param(
        [Parameter(Mandatory)][object[]]$Selection,
        [Parameter(Mandatory)][string]$Path
    )

    $directory = Split-Path -Parent $Path
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $document = [ordered]@{
        generatedAt = (Get-Date).ToUniversalTime().ToString('o')
        readyCount = @($Selection | Where-Object ready).Count
        champions = @($Selection)
    }
    $temporary = "$Path.tmp"
    $document | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporary -Encoding utf8
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}

function Invoke-ConfiguredAdapter {
    param(
        [Parameter(Mandatory)][pscustomobject]$Adapter,
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][object[]]$ReadySelection
    )

    if (-not $Adapter.enabled -or $ReadySelection.Count -eq 0) { return $false }
    $command = [string]$Adapter.command
    if ([string]::IsNullOrWhiteSpace($command) -or -not (Test-Path -LiteralPath $command -PathType Leaf)) {
        return $false
    }

    $packages = ($ReadySelection | ForEach-Object package) -join ';'
    $arguments = @($Adapter.arguments) | ForEach-Object {
        ([string]$_).Replace('{manifest}', $ManifestPath).Replace('{packages}', $packages)
    }
    Start-Process -FilePath $command -ArgumentList $arguments | Out-Null
    return $true
}

Export-ModuleMember -Function Get-ClassicArchiveIndex, ConvertTo-ChampionKey, Get-LivePlayers, New-ClassicSelection, Write-SelectionManifest, Invoke-ConfiguredAdapter
