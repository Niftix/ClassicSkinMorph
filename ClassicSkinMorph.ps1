param([string]$Config = (Join-Path $PSScriptRoot 'config.json'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ClassicSkinMorph.psm1') -Force
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $Config)) {
    $configTemplate = Join-Path $PSScriptRoot 'config.example.json'
    if (-not (Test-Path -LiteralPath $configTemplate)) {
        throw "Configuration et modele introuvables."
    }
    Copy-Item -LiteralPath $configTemplate -Destination $Config
}
$settings = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$script:archiveIndex = if ([string]::IsNullOrWhiteSpace([string]$settings.pbeChampionsDirectory)) {
    @{}
} else {
    Get-ClassicArchiveIndex -ChampionsDirectory $settings.pbeChampionsDirectory
}
$manifestPath = Join-Path $PSScriptRoot $settings.selectionManifest
$modLibrary = Join-Path $PSScriptRoot $settings.modLibrary
New-Item -ItemType Directory -Path $modLibrary -Force | Out-Null

$form = New-Object Windows.Forms.Form
$form.Text = 'ClassicSkinMorph'
$form.Size = New-Object Drawing.Size(760, 470)
$form.MinimumSize = New-Object Drawing.Size(620, 360)
$form.StartPosition = 'CenterScreen'
$form.BackColor = [Drawing.Color]::FromArgb(16, 24, 34)
$form.ForeColor = [Drawing.Color]::WhiteSmoke

$title = New-Object Windows.Forms.Label
$title.Text = 'CLASSICSKINMORPH'
$title.Font = New-Object Drawing.Font('Segoe UI Semibold', 18)
$title.Location = New-Object Drawing.Point(22, 18)
$title.AutoSize = $true
$form.Controls.Add($title)

$status = New-Object Windows.Forms.Label
$status.Text = "En attente d'une partie..."
$status.Font = New-Object Drawing.Font('Segoe UI', 10)
$status.Location = New-Object Drawing.Point(25, 58)
$status.AutoSize = $true
$status.ForeColor = [Drawing.Color]::FromArgb(116, 192, 252)
$form.Controls.Add($status)

$grid = New-Object Windows.Forms.DataGridView
$grid.Location = New-Object Drawing.Point(24, 92)
$grid.Size = New-Object Drawing.Size(695, 245)
$grid.Anchor = 'Top,Bottom,Left,Right'
$grid.ReadOnly = $true
$grid.AllowUserToAddRows = $false
$grid.AllowUserToDeleteRows = $false
$grid.AutoSizeColumnsMode = 'Fill'
$grid.BackgroundColor = [Drawing.Color]::FromArgb(24, 34, 46)
$grid.BorderStyle = 'None'
$grid.RowHeadersVisible = $false
$grid.EnableHeadersVisualStyles = $false
$grid.GridColor = [Drawing.Color]::FromArgb(55, 68, 82)
$grid.ColumnHeadersHeight = 32
$grid.ColumnHeadersDefaultCellStyle.BackColor = [Drawing.Color]::FromArgb(31, 44, 58)
$grid.ColumnHeadersDefaultCellStyle.ForeColor = [Drawing.Color]::WhiteSmoke
$grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = [Drawing.Color]::FromArgb(31, 44, 58)
$grid.ColumnHeadersDefaultCellStyle.Font = New-Object Drawing.Font('Segoe UI Semibold', 9)
$grid.DefaultCellStyle.BackColor = [Drawing.Color]::FromArgb(24, 34, 46)
$grid.DefaultCellStyle.ForeColor = [Drawing.Color]::FromArgb(226, 232, 240)
$grid.DefaultCellStyle.SelectionBackColor = [Drawing.Color]::FromArgb(14, 116, 190)
$grid.DefaultCellStyle.SelectionForeColor = [Drawing.Color]::White
$grid.DefaultCellStyle.Font = New-Object Drawing.Font('Segoe UI', 9)
$grid.DefaultCellStyle.Padding = New-Object Windows.Forms.Padding(4, 2, 4, 2)
$grid.AlternatingRowsDefaultCellStyle.BackColor = [Drawing.Color]::FromArgb(29, 41, 54)
$grid.RowTemplate.Height = 28
[void]$grid.Columns.Add('Champion', 'Champion')
[void]$grid.Columns.Add('Archive', 'Type de skin PBE')
[void]$grid.Columns.Add('Package', 'Paquet LTK')
$form.Controls.Add($grid)

$footer = New-Object Windows.Forms.Label
$footer.Text = 'Version 0.1'
$footer.Location = New-Object Drawing.Point(25, 397)
$footer.Anchor = 'Bottom,Left'
$footer.AutoSize = $true
$footer.ForeColor = [Drawing.Color]::FromArgb(148, 163, 184)
$form.Controls.Add($footer)

$ltkButton = New-Object Windows.Forms.Button
$ltkButton.Text = 'Ouvrir LTK Manager'
$ltkButton.Location = New-Object Drawing.Point(24, 350)
$ltkButton.Size = New-Object Drawing.Size(160, 32)
$ltkButton.Anchor = 'Bottom,Left'
$ltkButton.Add_Click({
    $bundledLtk = Join-Path $PSScriptRoot 'LTK Manager\ltk-manager.exe'
    $installedLtk = Join-Path $env:LOCALAPPDATA 'LTK Manager\ltk-manager.exe'
    $ltk = @($bundledLtk, $installedLtk) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

    if ($ltk) { Start-Process -FilePath $ltk -WorkingDirectory (Split-Path -Parent $ltk) | Out-Null }
    else { [Windows.Forms.MessageBox]::Show('LTK Manager est introuvable dans le dossier du logiciel.', 'ClassicSkinMorph') | Out-Null }
})
$form.Controls.Add($ltkButton)

$packageButton = New-Object Windows.Forms.Button
$packageButton.Text = 'Afficher le paquet'
$packageButton.Location = New-Object Drawing.Point(194, 350)
$packageButton.Size = New-Object Drawing.Size(190, 32)
$packageButton.Anchor = 'Bottom,Left'
$packageButton.Add_Click({
    Start-Process explorer.exe -ArgumentList "`"$modLibrary`"" | Out-Null
})
$form.Controls.Add($packageButton)

$pathButton = New-Object Windows.Forms.Button
$pathButton.Text = 'Choisir dossier PBE'
$pathButton.Location = New-Object Drawing.Point(394, 350)
$pathButton.Size = New-Object Drawing.Size(170, 32)
$pathButton.Anchor = 'Bottom,Left'
$pathButton.Add_Click({
    $dialog = New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Choisir le dossier League of Legends PBE ou le dossier Champions'
    $dialog.ShowNewFolderButton = $false
    if (-not [string]::IsNullOrWhiteSpace([string]$settings.pbeChampionsDirectory) -and
        (Test-Path -LiteralPath $settings.pbeChampionsDirectory)) {
        $dialog.SelectedPath = $settings.pbeChampionsDirectory
    }
    if ($dialog.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { return }

    $selected = $dialog.SelectedPath
    $candidates = @(
        $selected
        (Join-Path $selected 'Game\DATA\FINAL\Champions')
        (Join-Path $selected 'DATA\FINAL\Champions')
    )
    $championsPath = $candidates | Where-Object {
        (Test-Path -LiteralPath $_ -PathType Container) -and
        @(Get-ChildItem -LiteralPath $_ -Filter '*.wad.client' -File -ErrorAction SilentlyContinue).Count -gt 0
    } | Select-Object -First 1

    if (-not $championsPath) {
        [Windows.Forms.MessageBox]::Show('Aucune archive de champion trouvee dans ce dossier.', 'ClassicSkinMorph') | Out-Null
        return
    }

    $settings.pbeChampionsDirectory = [IO.Path]::GetFullPath($championsPath)
    $script:archiveIndex = Get-ClassicArchiveIndex -ChampionsDirectory $settings.pbeChampionsDirectory
    $json = $settings | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($Config, $json, [Text.UTF8Encoding]::new($false))
    $script:lastFingerprint = ''
    $grid.Rows.Clear()
    $status.Text = "Dossier PBE configure - en attente d'une partie..."
})
$form.Controls.Add($pathButton)

$script:lastFingerprint = ''
$timer = New-Object Windows.Forms.Timer
$timer.Interval = [Math]::Max(1000, [int]$settings.pollIntervalMs)
$timer.Add_Tick({
    try {
        $players = @(Get-LivePlayers -Endpoint $settings.liveClientEndpoint)
        if ($players.Count -eq 0) {
            $status.Text = "En attente d'une partie..."
            $grid.Rows.Clear()
            $script:lastFingerprint = ''
            return
        }

        $selection = @(New-ClassicSelection -Players $players -ArchiveIndex $script:archiveIndex -ModLibrary $modLibrary)
        if ($selection.Count -eq 0) {
            $status.Text = 'Transition de partie - donnees joueur incompletes'
            return
        }
        $ready = @($selection | Where-Object ready)
        $fingerprint = ($selection | ForEach-Object { "$($_.key):$($_.ready)" }) -join '|'
        if ($fingerprint -eq $script:lastFingerprint) { return }
        $script:lastFingerprint = $fingerprint

        $grid.Rows.Clear()
        foreach ($item in $selection) {
            $archiveState = $item.variant
            $packageState = if ($item.ready) { Split-Path -Leaf $item.package } else { 'Absent - aucune action' }
            [void]$grid.Rows.Add($item.champion, $archiveState, $packageState)
        }
        Write-SelectionManifest -Selection $selection -Path $manifestPath
        $launched = Invoke-ConfiguredAdapter -Adapter $settings.adapter -ManifestPath $manifestPath -ReadySelection $ready
        $status.Text = if ($ready.Count -eq 0) {
            "Partie detectee - aucun paquet Classic pret, aucune action"
        } elseif ($launched) {
            "$($ready.Count) paquet(s) Classic transmis a l'adaptateur"
        } else {
            "$($ready.Count) paquet(s) pret(s) - manifeste genere"
        }
    } catch {
        $status.Text = 'Reponse Live Client temporairement invalide - nouvelle tentative...'
        $script:lastFingerprint = ''
    }
})
$form.Add_Shown({ $timer.Start() })
$form.Add_FormClosed({ $timer.Stop() })
[void]$form.ShowDialog()
