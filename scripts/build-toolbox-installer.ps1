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

$payloadDir = Join-Path $repoRoot "src\Avln.ToolBox.Setup\Payload"
$payloadZip = Join-Path $payloadDir "toolbox-payload.zip"

Write-Host "=== AVLN ToolBox installer build ==="
Write-Host "Configuration: $Configuration"
Write-Host "Runtime:       $Runtime"

if (Test-Path $toolBoxPublishDir) { Remove-Item $toolBoxPublishDir -Recurse -Force }
if (Test-Path $setupPublishDir) { Remove-Item $setupPublishDir -Recurse -Force }
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }

New-Item -ItemType Directory -Force -Path $toolBoxPublishDir, $setupPublishDir, $outputDir, $payloadDir | Out-Null

Write-Host "=== Publish ToolBox payload ==="
dotnet publish $toolBoxProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=x64 `
    -p:PublishSingleFile=false `
    -p:PublishDir="$toolBoxPublishDir\"

$toolBoxExe = Join-Path $toolBoxPublishDir "AVLN.ToolBox.exe"
if (!(Test-Path $toolBoxExe)) {
    throw "ToolBox publish failed: AVLN.ToolBox.exe not found in $toolBoxPublishDir"
}

Write-Host "=== Pack embedded payload ==="
Compress-Archive -Path (Join-Path $toolBoxPublishDir "*") -DestinationPath $payloadZip -CompressionLevel Optimal -Force

if (!(Test-Path $payloadZip)) {
    throw "Payload zip was not created: $payloadZip"
}

Write-Host "=== Publish one-file setup ==="
dotnet publish $setupProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=x64 `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishDir="$setupPublishDir\"

$setupExe = Join-Path $setupPublishDir "AVLN.ToolBox.Setup.exe"
if (!(Test-Path $setupExe)) {
    throw "Setup publish failed: AVLN.ToolBox.Setup.exe not found in $setupPublishDir"
}

Copy-Item $setupExe $outputExe -Force

Write-Host "=== Done ==="
Write-Host "Installer: $outputExe"
