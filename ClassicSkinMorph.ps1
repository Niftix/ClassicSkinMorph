param([string]$Config = (Join-Path $PSScriptRoot 'config.json'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ClassicSkinMorph.Ltk.psm1') -Force
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $Config)) {
    $template = Join-Path $PSScriptRoot 'config.example.json'
    if (-not (Test-Path -LiteralPath $template)) { throw 'Configuration template not found.' }
    Copy-Item -LiteralPath $template -Destination $Config
}
$settings = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$modLibrary = Join-Path $PSScriptRoot $settings.modLibrary
$sessionPath = Join-Path $PSScriptRoot 'state\ltk-session.json'
$ltkExe = Join-Path $PSScriptRoot 'LTK Manager\ltk-manager.exe'
$script:ltkSessionStarted = $false
$script:ltkReady = $false
$script:loadingTicks = 0
$script:packageCount = 0
$script:logStartLine = 0
$script:ltkLog = Join-Path $env:APPDATA ("dev.leaguetoolkit.manager\logs\ltk-manager.{0}.log" -f (Get-Date).ToString('yyyy-MM-dd'))

$form = New-Object Windows.Forms.Form
$form.Text = 'Classic Skin Morph'
$form.ClientSize = New-Object Drawing.Size(500, 350)
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.StartPosition = 'CenterScreen'
$form.BackColor = [Drawing.Color]::White
$form.ForeColor = [Drawing.Color]::FromArgb(18, 42, 77)

$logoPath = Join-Path $PSScriptRoot 'assets\classic-skin-morph-logo.png'
$logoImage = [Drawing.Image]::FromFile($logoPath)
$logo = New-Object Windows.Forms.PictureBox
$logo.Location = New-Object Drawing.Point(70, 12)
$logo.Size = New-Object Drawing.Size(360, 153)
$logo.SizeMode = [Windows.Forms.PictureBoxSizeMode]::Zoom
$logo.BackColor = [Drawing.Color]::Transparent
$logo.Image = $logoImage
$form.Controls.Add($logo)

$indicator = New-Object Windows.Forms.Label
$indicator.Text = [char]0x25CF
$indicator.Font = New-Object Drawing.Font('Segoe UI Symbol', 18)
$indicator.Location = New-Object Drawing.Point(29, 174)
$indicator.AutoSize = $true
$indicator.ForeColor = [Drawing.Color]::FromArgb(245, 158, 11)
$form.Controls.Add($indicator)

$status = New-Object Windows.Forms.Label
$status.Text = 'Loading Classic skins...'
$status.Font = New-Object Drawing.Font('Segoe UI Semibold', 11)
$status.Location = New-Object Drawing.Point(57, 181)
$status.Size = New-Object Drawing.Size(413, 24)
$status.ForeColor = [Drawing.Color]::FromArgb(27, 57, 105)
$form.Controls.Add($status)

$script:progressValue = 0
$script:progressLive = $false
$progress = New-Object Windows.Forms.Panel
$progress.Location = New-Object Drawing.Point(30, 218)
$progress.Size = New-Object Drawing.Size(440, 24)
$progress.BackColor = [Drawing.Color]::FromArgb(229, 233, 239)
$progress.BorderStyle = [Windows.Forms.BorderStyle]::FixedSingle
$progress.Add_Paint({
    param($sender, $eventArgs)
    $width = [int](($sender.ClientSize.Width * $script:progressValue) / 100)
    $barColor = if ($script:progressLive) { [Drawing.Color]::FromArgb(22, 163, 74) } else { [Drawing.Color]::FromArgb(32, 92, 164) }
    if ($width -gt 0) {
        $brush = New-Object Drawing.SolidBrush($barColor)
        $eventArgs.Graphics.FillRectangle($brush, 0, 0, $width, $sender.ClientSize.Height)
        $brush.Dispose()
    }
    $text = "$($script:progressValue)%"
    $textColor = if ($script:progressValue -ge 48) { [Drawing.Color]::White } else { [Drawing.Color]::FromArgb(18, 42, 77) }
    $textBrush = New-Object Drawing.SolidBrush($textColor)
    $format = New-Object Drawing.StringFormat
    $format.Alignment = [Drawing.StringAlignment]::Center
    $format.LineAlignment = [Drawing.StringAlignment]::Center
    $textFont = New-Object Drawing.Font('Segoe UI Semibold', 9)
    $textBounds = New-Object Drawing.RectangleF(0, 0, $sender.ClientSize.Width, $sender.ClientSize.Height)
    $eventArgs.Graphics.DrawString($text, $textFont, $textBrush, $textBounds, $format)
    $textFont.Dispose()
    $format.Dispose()
    $textBrush.Dispose()
})
$form.Controls.Add($progress)

$patchTitle = New-Object Windows.Forms.Label
$patchTitle.Text = '------ Patch note V0.4 --------'
$patchTitle.Font = New-Object Drawing.Font('Segoe UI Semibold', 9, [Drawing.FontStyle]::Bold)
$patchTitle.Location = New-Object Drawing.Point(30, 258)
$patchTitle.Size = New-Object Drawing.Size(440, 20)
$patchTitle.TextAlign = [Drawing.ContentAlignment]::TopCenter
$patchTitle.ForeColor = [Drawing.Color]::FromArgb(18, 42, 77)
$form.Controls.Add($patchTitle)

$footer = New-Object Windows.Forms.Label
$footer.Text = "`r`n- Silent LTK integration`r`n- Automatic skin loading and cleanup`r`n- Improved stuck-process detection"
$footer.Font = New-Object Drawing.Font('Segoe UI', 9)
$footer.Location = New-Object Drawing.Point(30, 278)
$footer.Size = New-Object Drawing.Size(440, 64)
$footer.TextAlign = [Drawing.ContentAlignment]::TopCenter
$footer.ForeColor = [Drawing.Color]::FromArgb(79, 91, 110)
$form.Controls.Add($footer)

function Set-ProgressValue([int]$Value, [bool]$Live = $false) {
    $script:progressValue = [Math]::Min(100, [Math]::Max(0, $Value))
    $script:progressLive = $Live
    $progress.Invalidate()
    $progress.Update()
}

function Test-PbePath {
    -not [string]::IsNullOrWhiteSpace([string]$settings.pbeChampionsDirectory) -and
    (Test-Path -LiteralPath $settings.pbeChampionsDirectory -PathType Container) -and
    @(Get-ChildItem -LiteralPath $settings.pbeChampionsDirectory -Filter '*.wad.client' -File -ErrorAction SilentlyContinue).Count -gt 0
}

function Initialize-PbePath {
    if (Test-PbePath) { return $true }
    $dialog = New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'First-time setup: select your League of Legends PBE folder'
    $dialog.ShowNewFolderButton = $false
    if ($dialog.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { return $false }
    $selected = $dialog.SelectedPath
    $candidates = @($selected,(Join-Path $selected 'Game\DATA\FINAL\Champions'),(Join-Path $selected 'DATA\FINAL\Champions'))
    $championsPath = $candidates | Where-Object {
        (Test-Path -LiteralPath $_ -PathType Container) -and
        @(Get-ChildItem -LiteralPath $_ -Filter '*.wad.client' -File -ErrorAction SilentlyContinue).Count -gt 0
    } | Select-Object -First 1
    if (-not $championsPath) {
        [Windows.Forms.MessageBox]::Show('Invalid PBE folder.', 'Classic Skin Morph') | Out-Null
        return $false
    }
    $settings.pbeChampionsDirectory = [IO.Path]::GetFullPath($championsPath)
    [IO.File]::WriteAllText($Config, ($settings | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    return $true
}

function Start-SkinEngine {
    try {
        $indicator.ForeColor = [Drawing.Color]::FromArgb(245, 158, 11)
        $status.ForeColor = [Drawing.Color]::FromArgb(27, 57, 105)
        $status.Text = 'Loading Classic skins...'
        $progress.Visible = $true
        Set-ProgressValue 0
        $script:logStartLine = if (Test-Path -LiteralPath $script:ltkLog) { @(Get-Content -LiteralPath $script:ltkLog).Count } else { 0 }
        [Windows.Forms.Application]::DoEvents()
        $session = Start-ClassicLtkSession -LtkExe $ltkExe -ModLibrary $modLibrary -ChampionsDirectory $settings.pbeChampionsDirectory -SessionPath $sessionPath -ProgressCallback {
            param($completed, $total)
            Set-ProgressValue ([Math]::Min(30, [Math]::Max(1, [int](30 * $completed / $total))))
            $status.Text = "Loading skin packages: $completed / $total"
            [Windows.Forms.Application]::DoEvents()
        }
        $script:ltkSessionStarted = $true
        $script:loadingTicks = 0
        $script:packageCount = $session.packageCount
        $status.Text = "Building skin overlay: 0 / $($session.packageCount)"
    } catch {
        $progress.Visible = $false
        $indicator.ForeColor = [Drawing.Color]::FromArgb(239, 68, 68)
        $status.ForeColor = [Drawing.Color]::FromArgb(185, 28, 28)
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
        $status.ForeColor = [Drawing.Color]::FromArgb(185, 28, 28)
        $status.Text = 'Patcher stopped - restart Classic Skin Morph'
        return
    }
    if ($script:ltkReady) { return }
    $script:loadingTicks++
    if ($script:packageCount -gt 0 -and (Test-Path -LiteralPath $script:ltkLog)) {
        $newLogLines = @(Get-Content -LiteralPath $script:ltkLog | Select-Object -Skip $script:logStartLine)
        $builtCount = @($newLogLines | Where-Object { $_ -like '*Patched WAD complete*' }).Count
        $builtCount = [Math]::Min($script:packageCount, $builtCount)
        Set-ProgressValue ([Math]::Min(95, 30 + [int](65 * $builtCount / $script:packageCount)))
        $status.Text = "Building skin overlay: $builtCount / $($script:packageCount)"
    }
    if (Test-ClassicLtkReady) {
        $script:ltkReady = $true
        Set-ProgressValue 100 $true
        $indicator.ForeColor = [Drawing.Color]::FromArgb(34, 197, 94)
        $status.ForeColor = [Drawing.Color]::FromArgb(22, 130, 75)
        $status.Text = 'You can now play - CLASSIC SKINS ACTIVE'
    } elseif ($script:loadingTicks -ge 240) {
        $progress.Visible = $false
        $indicator.ForeColor = [Drawing.Color]::FromArgb(239, 68, 68)
        $status.ForeColor = [Drawing.Color]::FromArgb(185, 28, 28)
        $status.Text = 'The LTK patcher is not responding'
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
        $status.ForeColor = [Drawing.Color]::FromArgb(180, 125, 20)
        $status.Text = 'Stopping and cleaning up skins...'
        [Windows.Forms.Application]::DoEvents()
        try { Stop-ClassicLtkSession -SessionPath $sessionPath } catch { }
    }
    if ($logo.Image) { $logo.Image = $null }
    $logoImage.Dispose()
})

[void]$form.ShowDialog()
