param([switch]$ForceUpdate, [switch]$NoLaunch)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repository = 'Niftix/ClassicSkinMorph'
$branch = 'master'
$installDirectory = $PSScriptRoot
$versionFile = Join-Path $installDirectory '.update-version'
$application = Join-Path $installDirectory 'ClassicSkinMorph.ps1'
$developmentCheckout = Test-Path -LiteralPath (Join-Path $installDirectory '.git') -PathType Container
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$form = New-Object Windows.Forms.Form
$form.Text = 'Classic Skin Morph'
$form.Size = New-Object Drawing.Size(500, 210)
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.ControlBox = $false
$form.StartPosition = 'CenterScreen'
$form.BackColor = [Drawing.Color]::FromArgb(16, 24, 34)
$form.ForeColor = [Drawing.Color]::WhiteSmoke

$title = New-Object Windows.Forms.Label
$title.Text = 'CLASSIC SKIN MORPH'
$title.Font = New-Object Drawing.Font('Segoe UI Semibold', 18)
$title.Location = New-Object Drawing.Point(24, 22)
$title.AutoSize = $true
$form.Controls.Add($title)

$status = New-Object Windows.Forms.Label
$status.Text = 'Initializing...'
$status.Font = New-Object Drawing.Font('Segoe UI', 10)
$status.Location = New-Object Drawing.Point(27, 72)
$status.Size = New-Object Drawing.Size(440, 24)
$status.ForeColor = [Drawing.Color]::FromArgb(116, 192, 252)
$form.Controls.Add($status)

$progress = New-Object Windows.Forms.ProgressBar
$progress.Location = New-Object Drawing.Point(28, 108)
$progress.Size = New-Object Drawing.Size(428, 18)
$progress.Style = 'Marquee'
$form.Controls.Add($progress)

$detail = New-Object Windows.Forms.Label
$detail.Text = 'Connecting to GitHub...'
$detail.Location = New-Object Drawing.Point(28, 136)
$detail.Size = New-Object Drawing.Size(428, 20)
$detail.ForeColor = [Drawing.Color]::FromArgb(148, 163, 184)
$form.Controls.Add($detail)

function Set-LauncherStatus([string]$Message, [string]$Details = '') {
    $status.Text = $Message
    if ($Details) { $detail.Text = $Details }
    [Windows.Forms.Application]::DoEvents()
}

function Receive-Update([string]$Uri, [string]$Destination) {
    $client = New-Object Net.WebClient
    $client.Headers['User-Agent'] = 'ClassicSkinMorph-Updater'
    $client.add_DownloadProgressChanged({
        $progress.Style = 'Continuous'
        $progress.Value = [Math]::Min(100, [Math]::Max(0, $_.ProgressPercentage))
        $detail.Text = if ($_.TotalBytesToReceive -gt 0) {
            '{0:N1} / {1:N1} MB' -f ($_.BytesReceived / 1MB), ($_.TotalBytesToReceive / 1MB)
        } else { '{0:N1} MB received' -f ($_.BytesReceived / 1MB) }
    })
    try {
        $task = $client.DownloadFileTaskAsync([Uri]$Uri, $Destination)
        while (-not $task.IsCompleted) {
            [Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 40
        }
        if ($task.IsFaulted) { throw $task.Exception.GetBaseException() }
    } finally { $client.Dispose() }
}

function Install-Update([string]$CommitSha) {
    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('ClassicSkinMorph-' + [guid]::NewGuid().ToString('N'))
    $archive = Join-Path $temporaryRoot 'update.zip'
    $extract = Join-Path $temporaryRoot 'extract'
    New-Item -ItemType Directory -Path $extract -Force | Out-Null
    try {
        Set-LauncherStatus 'Downloading update...' 'Preparing download'
        Receive-Update "https://codeload.github.com/$repository/zip/$CommitSha" $archive
        $progress.Style = 'Marquee'
        Set-LauncherStatus 'Installing update...' 'Extracting files'
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $extract)
        $source = Get-ChildItem -LiteralPath $extract -Directory | Select-Object -First 1
        if (-not $source) { throw 'Invalid GitHub archive.' }
        foreach ($required in @('ClassicSkinMorph.ps1', 'ClassicSkinMorph.psm1', 'config.example.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $source.FullName $required) -PathType Leaf)) {
                throw "Required file is missing: $required"
            }
        }
        Set-LauncherStatus 'Installing update...' 'Copying files'
        Get-ChildItem -LiteralPath $source.FullName -Force | Where-Object {
            $_.Name -notin @('.git', 'config.json', 'state', '.update-version')
        } | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Recurse -Force
        }
        [IO.File]::WriteAllText($versionFile, $CommitSha, [Text.UTF8Encoding]::new($false))
    } finally {
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

$form.Add_Shown({
    try {
        if ($developmentCheckout -and -not $ForceUpdate) {
            Set-LauncherStatus 'Local development version.' 'Automatic updates are disabled in a Git repository'
            if (-not $NoLaunch) {
                Start-Process powershell.exe -WorkingDirectory $installDirectory -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$application`"") | Out-Null
            }
            $form.Close()
            return
        }
        Set-LauncherStatus 'Checking for updates...' 'Contacting the GitHub repository'
        $headers = @{ 'User-Agent' = 'ClassicSkinMorph-Updater'; 'Accept' = 'application/vnd.github+json' }
        $remote = Invoke-RestMethod -UseBasicParsing -Uri "https://api.github.com/repos/$repository/commits/$branch" -Headers $headers -TimeoutSec 15
        $remoteSha = [string]$remote.sha
        if ([string]::IsNullOrWhiteSpace($remoteSha)) { throw 'GitHub version could not be found.' }
        $localSha = if (Test-Path -LiteralPath $versionFile) { (Get-Content -LiteralPath $versionFile -Raw).Trim() } else { '' }
        if ($ForceUpdate -or -not (Test-Path -LiteralPath $application) -or $localSha -ne $remoteSha) {
            Install-Update $remoteSha
            Set-LauncherStatus 'Update completed.' 'Starting Classic Skin Morph'
        } else { Set-LauncherStatus 'Classic Skin Morph is up to date.' 'Starting application' }
    } catch {
        if (-not (Test-Path -LiteralPath $application)) {
            [Windows.Forms.MessageBox]::Show("Download failed.`r`n`r`n$($_.Exception.Message)", 'Classic Skin Morph', 'OK', 'Error') | Out-Null
            $form.Close()
            return
        }
        Set-LauncherStatus 'GitHub is unavailable.' 'Starting the local version'
    }
    if (-not $NoLaunch) {
        Start-Process powershell.exe -WorkingDirectory $installDirectory -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$application`"") | Out-Null
    }
    $form.Close()
})

[void]$form.ShowDialog()
