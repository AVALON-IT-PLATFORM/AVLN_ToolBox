param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
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

Write-Host "=== AVLN ToolBox web-installer build ==="
Write-Host "Configuration: $Configuration"
Write-Host "Runtime:       $Runtime"

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

Write-Host "=== Done ==="
Write-Host "Installer: $outputExe"
Write-Host "App zip:   $outputAppZip"
