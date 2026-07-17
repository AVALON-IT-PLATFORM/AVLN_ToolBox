param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$SignToolPath = "\\Nas\WORK\16_Scripts\Infrastructure\08_Tools\signtool.exe",
    [string]$CertPath = "\\Nas\WORK\16_Scripts\Infrastructure\05_Config\Certificate\AVLN.pfx",
    [string]$TimeStampServer = "http://timestamp.digicert.com/",
    [switch]$SkipSigning
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolBoxProject = Join-Path $repoRoot "src\Avln.ToolBox\Avln.ToolBox.csproj"
$setupProject = Join-Path $repoRoot "src\Avln.ToolBox.Setup\Avln.ToolBox.Setup.csproj"

$buildRoot = Join-Path $repoRoot "artifacts\build"
$toolBoxPublishDir = Join-Path $buildRoot "toolbox-publish"
$setupPublishDir = Join-Path $buildRoot "setup-publish"
$outputDir = Join-Path $repoRoot "artifacts\toolbox"
$outputExe = Join-Path $outputDir "AVLN.ToolBox.Setup.exe"
$outputAppZip = Join-Path $outputDir "AVLN.ToolBox.App.win-x64.zip"

function Assert-SigningInputs {
    if ($SkipSigning) {
        Write-Host "=== Code signing skipped by -SkipSigning ==="
        return
    }

    if (!(Test-Path $SignToolPath)) {
        throw "signtool.exe not found: $SignToolPath"
    }

    if (!(Test-Path $CertPath)) {
        throw "Code signing certificate not found: $CertPath"
    }
}

function Sign-ReleaseFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ($SkipSigning) {
        return
    }

    if (!(Test-Path $Path)) {
        throw "File to sign not found: $Path"
    }

    Write-Host "Signing: $Path"
    & $SignToolPath sign /fd SHA256 /f $CertPath $Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed for $Path"
    }

    & $SignToolPath timestamp /td sha256 /tr $TimeStampServer $Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool timestamp failed for $Path"
    }

    $signature = Get-AuthenticodeSignature $Path
    if ($signature.Status -ne "Valid") {
        throw "Invalid Authenticode signature for $Path. Status: $($signature.Status)"
    }
}

function Sign-ToolBoxPayload {
    if ($SkipSigning) {
        return
    }

    Write-Host "=== Sign ToolBox app payload ==="

    $signTargets = @()
    $signTargets += Get-Item (Join-Path $toolBoxPublishDir "AVLN.ToolBox.exe")
    $signTargets += Get-ChildItem $toolBoxPublishDir -File -Filter "*.dll"

    foreach ($target in $signTargets | Sort-Object FullName -Unique) {
        Sign-ReleaseFile -Path $target.FullName
    }
}

Write-Host "=== AVLN ToolBox web-installer build ==="
Write-Host "Configuration: $Configuration"
Write-Host "Runtime:       $Runtime"
Write-Host "Signing:       $(-not $SkipSigning)"

Assert-SigningInputs

if (Test-Path $toolBoxPublishDir) { Remove-Item $toolBoxPublishDir -Recurse -Force }
if (Test-Path $setupPublishDir) { Remove-Item $setupPublishDir -Recurse -Force }
if (Test-Path $outputExe) { Remove-Item $outputExe -Force }
if (Test-Path $outputAppZip) { Remove-Item $outputAppZip -Force }

New-Item -ItemType Directory -Force -Path $toolBoxPublishDir, $setupPublishDir, $outputDir | Out-Null

Write-Host "=== Publish ToolBox app payload ==="
dotnet publish $toolBoxProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=x64 `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:PublishDir="$toolBoxPublishDir\"

$toolBoxExe = Join-Path $toolBoxPublishDir "AVLN.ToolBox.exe"
if (!(Test-Path $toolBoxExe)) {
    throw "ToolBox publish failed: AVLN.ToolBox.exe not found in $toolBoxPublishDir"
}

Sign-ToolBoxPayload

Write-Host "=== Pack ToolBox app release asset ==="
Compress-Archive -Path (Join-Path $toolBoxPublishDir "*") -DestinationPath $outputAppZip -CompressionLevel Optimal -Force

if (!(Test-Path $outputAppZip)) {
    throw "ToolBox app zip was not created: $outputAppZip"
}

Write-Host "=== Publish one-file setup downloader ==="
dotnet publish $setupProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=x64 `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:PublishDir="$setupPublishDir\"

$setupExe = Join-Path $setupPublishDir "AVLN.ToolBox.Setup.exe"
if (!(Test-Path $setupExe)) {
    throw "Setup publish failed: AVLN.ToolBox.Setup.exe not found in $setupPublishDir"
}

Copy-Item $setupExe $outputExe -Force
Sign-ReleaseFile -Path $outputExe

Write-Host "=== Done ==="
Write-Host "Installer: $outputExe"
Write-Host "App zip:   $outputAppZip"
