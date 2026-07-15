param([string]$Config = (Join-Path $PSScriptRoot 'config.json'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ClassicSkinMorph.Ltk.psm1') -Force
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $Config)) {
    $template = Join-Path $PSScriptRoot 'config.example.json'
    if (-not (Test-Path -LiteralPath $template)) { throw 'Modele de configuration introuvable.' }
    Copy-Item -LiteralPath $template -Destination $Config
}
$settings = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$modLibrary = Join-Path $PSScriptRoot $settings.modLibrary
$sessionPath = Join-Path $PSScriptRoot 'state\ltk-session.json'
$ltkExe = Join-Path $PSScriptRoot 'LTK Manager\ltk-manager.exe'
$script:ltkSessionStarted = $false
$script:ltkReady = $false
$script:loadingTicks = 0

$form = New-Object Windows.Forms.Form
$form.Text = 'ClassicSkinMorph'
$form.ClientSize = New-Object Drawing.Size(470, 170)
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.StartPosition = 'CenterScreen'
$form.BackColor = [Drawing.Color]::FromArgb(16, 24, 34)
$form.ForeColor = [Drawing.Color]::WhiteSmoke

$title = New-Object Windows.Forms.Label
$title.Text = 'CLASSICSKINMORPH'
$title.Font = New-Object Drawing.Font('Segoe UI Semibold', 18)
$title.Location = New-Object Drawing.Point(22, 18)
$title.AutoSize = $true
$form.Controls.Add($title)

$indicator = New-Object Windows.Forms.Label
$indicator.Text = [char]0x25CF
$indicator.Font = New-Object Drawing.Font('Segoe UI Symbol', 18)
$indicator.Location = New-Object Drawing.Point(23, 66)
$indicator.AutoSize = $true
$indicator.ForeColor = [Drawing.Color]::FromArgb(245, 158, 11)
$form.Controls.Add($indicator)

$status = New-Object Windows.Forms.Label
$status.Text = 'Chargement des skins Classic...'
$status.Font = New-Object Drawing.Font('Segoe UI Semibold', 11)
$status.Location = New-Object Drawing.Point(52, 72)
$status.Size = New-Object Drawing.Size(390, 24)
$status.ForeColor = [Drawing.Color]::FromArgb(116, 192, 252)
$form.Controls.Add($status)

$progress = New-Object Windows.Forms.ProgressBar
$progress.Location = New-Object Drawing.Point(27, 108)
$progress.Size = New-Object Drawing.Size(416, 6)
$progress.Style = 'Marquee'
$progress.MarqueeAnimationSpeed = 22
$form.Controls.Add($progress)

$footer = New-Object Windows.Forms.Label
$footer.Text = 'Version 0.2'
$footer.Location = New-Object Drawing.Point(27, 137)
$footer.AutoSize = $true
$footer.ForeColor = [Drawing.Color]::FromArgb(100, 116, 139)
$form.Controls.Add($footer)

function Test-PbePath {
    -not [string]::IsNullOrWhiteSpace([string]$settings.pbeChampionsDirectory) -and
    (Test-Path -LiteralPath $settings.pbeChampionsDirectory -PathType Container) -and
    @(Get-ChildItem -LiteralPath $settings.pbeChampionsDirectory -Filter '*.wad.client' -File -ErrorAction SilentlyContinue).Count -gt 0
}

function Initialize-PbePath {
    if (Test-PbePath) { return $true }
    $dialog = New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Premiere configuration : choisissez League of Legends PBE'
    $dialog.ShowNewFolderButton = $false
    if ($dialog.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { return $false }
    $selected = $dialog.SelectedPath
    $candidates = @($selected,(Join-Path $selected 'Game\DATA\FINAL\Champions'),(Join-Path $selected 'DATA\FINAL\Champions'))
    $championsPath = $candidates | Where-Object {
        (Test-Path -LiteralPath $_ -PathType Container) -and
        @(Get-ChildItem -LiteralPath $_ -Filter '*.wad.client' -File -ErrorAction SilentlyContinue).Count -gt 0
    } | Select-Object -First 1
    if (-not $championsPath) {
        [Windows.Forms.MessageBox]::Show('Dossier PBE invalide.', 'ClassicSkinMorph') | Out-Null
        return $false
    }
    $settings.pbeChampionsDirectory = [IO.Path]::GetFullPath($championsPath)
    [IO.File]::WriteAllText($Config, ($settings | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    return $true
}

function Start-SkinEngine {
    try {
        $indicator.ForeColor = [Drawing.Color]::FromArgb(245, 158, 11)
        $status.ForeColor = [Drawing.Color]::FromArgb(116, 192, 252)
        $status.Text = 'Chargement des skins Classic...'
        $progress.Visible = $true
        [Windows.Forms.Application]::DoEvents()
        $session = Start-ClassicLtkSession -LtkExe $ltkExe -ModLibrary $modLibrary -ChampionsDirectory $settings.pbeChampionsDirectory -SessionPath $sessionPath
        $script:ltkSessionStarted = $true
        $script:loadingTicks = 0
        $status.Text = "Preparation de $($session.packageCount) skins..."
    } catch {
        $progress.Visible = $false
        $indicator.ForeColor = [Drawing.Color]::FromArgb(239, 68, 68)
        $status.ForeColor = [Drawing.Color]::FromArgb(248, 113, 113)
        $status.Text = $_.Exception.Message
    }
}

$engineTimer = New-Object Windows.Forms.Timer
$engineTimer.Interval = 500
$engineTimer.Add_Tick({
    if (-not $script:ltkSessionStarted) { return }
    Hide-ClassicLtkWindow
    if ($script:ltkReady -and -not (Test-ClassicLtkReady)) {
        $script:ltkReady = $false
        $progress.Visible = $false
        $indicator.ForeColor = [Drawing.Color]::FromArgb(239, 68, 68)
        $status.ForeColor = [Drawing.Color]::FromArgb(248, 113, 113)
        $status.Text = 'Patcher interrompu - relancez ClassicSkinMorph'
        return
    }
    if ($script:ltkReady) { return }
    $script:loadingTicks++
    if (Test-ClassicLtkReady) {
        $script:ltkReady = $true
        $progress.Visible = $false
        $indicator.ForeColor = [Drawing.Color]::FromArgb(34, 197, 94)
        $status.ForeColor = [Drawing.Color]::FromArgb(74, 222, 128)
        $status.Text = 'Vous pouvez a present jouer - SKIN CLASSIC ACTIF'
    } elseif ($script:loadingTicks -ge 240) {
        $progress.Visible = $false
        $indicator.ForeColor = [Drawing.Color]::FromArgb(239, 68, 68)
        $status.ForeColor = [Drawing.Color]::FromArgb(248, 113, 113)
        $status.Text = 'Le patcher LTK ne repond pas'
    }
})

$form.Add_Shown({
    if (-not (Initialize-PbePath)) {
        $form.Close()
        return
    }
    $engineTimer.Start()
    Start-SkinEngine
})

$form.Add_FormClosing({
    $engineTimer.Stop()
    if ($script:ltkSessionStarted -or (Test-Path -LiteralPath $sessionPath)) {
        $progress.Visible = $true
        $indicator.ForeColor = [Drawing.Color]::FromArgb(245, 158, 11)
        $status.ForeColor = [Drawing.Color]::FromArgb(251, 191, 36)
        $status.Text = 'Arret et nettoyage des skins...'
        [Windows.Forms.Application]::DoEvents()
        try { Stop-ClassicLtkSession -SessionPath $sessionPath } catch { }
    }
})

[void]$form.ShowDialog()
