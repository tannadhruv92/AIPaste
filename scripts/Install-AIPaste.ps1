[CmdletBinding()]
param(
    [string]$Version,
    [string]$DownloadUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = 'tannadhruv92/AIPaste'
$assetPattern = 'AIPaste-v*.zip'
$headers = @{ 'User-Agent' = 'AIPaste installer' }
$dotnetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/9.0/runtime'
$tempRoot = $null

if ($PSVersionTable.PSEdition -eq 'Desktop') {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

function Test-DotNet9DesktopRuntime {
    $dotnetCommand = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    $dotnetPath = if ($dotnetCommand) {
        $dotnetCommand.Source
    }
    else {
        Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    }

    if (-not (Test-Path -LiteralPath $dotnetPath -PathType Leaf)) {
        return $false
    }

    try {
        $runtimes = @(& $dotnetPath --list-runtimes 2>$null)
        return [bool]($runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 9\.' } | Select-Object -First 1)
    }
    catch {
        return $false
    }
}

function Ensure-DotNet9DesktopRuntime {
    if (Test-DotNet9DesktopRuntime) {
        Write-Host '.NET 9 Desktop Runtime is installed.'
        return
    }

    Write-Host ''
    Write-Host 'AIPaste requires the .NET 9 Desktop Runtime.'
    $winget = Get-Command 'winget' -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "Windows Package Manager (winget) was not found. Install the .NET 9 Desktop Runtime from $dotnetDownloadUrl, then rerun this installer."
    }

    $answer = Read-Host 'Install the .NET 9 Desktop Runtime now using winget? [Y/n]'
    if ($answer -and $answer -notmatch '^(?i:y|yes)$') {
        throw "Installation cancelled. Install the .NET 9 Desktop Runtime from $dotnetDownloadUrl before installing AIPaste."
    }

    Write-Host 'Installing the .NET 9 Desktop Runtime...'
    & $winget.Source install --id Microsoft.DotNet.DesktopRuntime.9 --exact --source winget --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "winget could not install the .NET 9 Desktop Runtime. Install it from $dotnetDownloadUrl, then rerun this installer."
    }

    if (-not (Test-DotNet9DesktopRuntime)) {
        throw "The .NET 9 Desktop Runtime installation could not be verified. Restart PowerShell and rerun this installer. Download: $dotnetDownloadUrl"
    }
}

function Resolve-AIPasteReleaseAssetUrl {
    param(
        [string]$Repo,
        [string]$ReleaseVersion,
        [string]$Pattern,
        [hashtable]$RequestHeaders
    )

    if ($ReleaseVersion) {
        $tag = if ($ReleaseVersion.StartsWith('v', [StringComparison]::OrdinalIgnoreCase)) { $ReleaseVersion } else { "v$ReleaseVersion" }
        $releaseUri = "https://api.github.com/repos/$Repo/releases/tags/$tag"
    }
    else {
        $releaseUri = "https://api.github.com/repos/$Repo/releases/latest"
    }

    Write-Host "Looking up AIPaste release asset..."
    $release = Invoke-RestMethod -Uri $releaseUri -Headers $RequestHeaders
    $asset = $release.assets | Where-Object { $_.name -like $Pattern } | Select-Object -First 1

    if (-not $asset) {
        throw "Could not find a release asset matching '$Pattern' in $($release.html_url)."
    }

    return $asset.browser_download_url
}

function Resolve-AIPasteInstallPath {
    param(
        [string]$DefaultPath
    )

    Write-Host ''
    Write-Host 'Choose install location.'
    Write-Host "Default: $DefaultPath"
    $pathInput = Read-Host 'Press Enter to use default, or type a custom folder path'

    if ([string]::IsNullOrWhiteSpace($pathInput)) {
        return $DefaultPath
    }

    $path = [Environment]::ExpandEnvironmentVariables($pathInput.Trim().Trim('"').Trim("'"))

    if ($path -eq '~') {
        $path = $HOME
    }
    elseif ($path.StartsWith('~\', [StringComparison]::Ordinal) -or $path.StartsWith('~/', [StringComparison]::Ordinal)) {
        $path = Join-Path $HOME $path.Substring(2)
    }

    if (-not [IO.Path]::IsPathRooted($path)) {
        $path = Join-Path (Get-Location).ProviderPath $path
    }

    return [IO.Path]::GetFullPath($path)
}

function Confirm-AIPasteInstallPathReplacement {
    param(
        [string]$Path
    )

    $rootPath = [IO.Path]::GetPathRoot($Path).TrimEnd('\')
    if ($Path.TrimEnd('\') -eq $rootPath) {
        throw 'Choose a folder for AIPaste, not a drive root.'
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $items = @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)
    if ($items.Count -eq 0 -or (Test-Path -LiteralPath (Join-Path $Path 'AIPaste.exe'))) {
        return
    }

    Write-Host ''
    Write-Host 'The selected folder already exists and does not look like an AIPaste install:'
    Write-Host "  $Path"
    $answer = Read-Host 'Type YES to replace this folder, or press Enter to cancel'
    if ($answer -cne 'YES') {
        throw 'Installation cancelled.'
    }
}

try {
    $defaultInstallPath = Join-Path $env:LOCALAPPDATA 'AIPaste'
    $InstallPath = Resolve-AIPasteInstallPath -DefaultPath $defaultInstallPath
    Confirm-AIPasteInstallPathReplacement -Path $InstallPath
    Ensure-DotNet9DesktopRuntime

    if (-not $DownloadUrl) {
        $DownloadUrl = Resolve-AIPasteReleaseAssetUrl -Repo $repo -ReleaseVersion $Version -Pattern $assetPattern -RequestHeaders $headers
    }

    $runningProcess = Get-Process -Name 'AIPaste' -ErrorAction SilentlyContinue
    if ($runningProcess) {
        throw 'AIPaste is currently running. Exit it from the system tray, then rerun this script.'
    }

    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('AIPaste-' + [Guid]::NewGuid().ToString('N'))
    $zipPath = Join-Path $tempRoot 'AIPaste.zip'
    $expandedPath = Join-Path $tempRoot 'expanded'

    New-Item -ItemType Directory -Path $tempRoot, $expandedPath -Force | Out-Null

    Write-Host "Downloading $DownloadUrl"
    $downloadParams = @{
        Uri = $DownloadUrl
        OutFile = $zipPath
        Headers = $headers
    }
    if ((Get-Command Invoke-WebRequest).Parameters.ContainsKey('UseBasicParsing')) {
        $downloadParams.UseBasicParsing = $true
    }
    Invoke-WebRequest @downloadParams

    Unblock-File -Path $zipPath -ErrorAction SilentlyContinue

    Write-Host 'Extracting release zip...'
    Expand-Archive -LiteralPath $zipPath -DestinationPath $expandedPath -Force

    $sourcePath = $expandedPath
    if (-not (Test-Path -LiteralPath (Join-Path $sourcePath 'AIPaste.exe'))) {
        $childDirectories = @(Get-ChildItem -LiteralPath $expandedPath -Directory)
        if ($childDirectories.Count -eq 1 -and (Test-Path -LiteralPath (Join-Path $childDirectories[0].FullName 'AIPaste.exe'))) {
            $sourcePath = $childDirectories[0].FullName
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $sourcePath 'AIPaste.exe'))) {
        throw 'The downloaded zip did not contain AIPaste.exe at the expected location.'
    }

    if (Test-Path -LiteralPath $InstallPath) {
        Remove-Item -LiteralPath $InstallPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

    Get-ChildItem -LiteralPath $sourcePath -Force | Copy-Item -Destination $InstallPath -Recurse -Force
    Get-ChildItem -LiteralPath $InstallPath -Recurse -File -Force | Unblock-File -ErrorAction SilentlyContinue

    Write-Host "AIPaste installed to $InstallPath"
    Write-Host 'Starting AIPaste and opening the install folder...'
    Write-Host 'To pin AIPaste, right-click its running taskbar icon and choose Pin to taskbar.'
    Start-Process -FilePath (Join-Path $InstallPath 'AIPaste.exe') -WorkingDirectory $InstallPath
    Invoke-Item -LiteralPath $InstallPath

    Write-Host ''
    [void](Read-Host 'Installation complete. Press Enter to close this installer')
}
finally {
    if ($tempRoot -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}