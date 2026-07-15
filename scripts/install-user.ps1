[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\Avln.ToolBox\Avln.ToolBox.csproj"
$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\AVLN ToolBox"
$publishDirectory = Join-Path $env:TEMP ("avln-toolbox-publish-" + [Guid]::NewGuid().ToString("N"))
$desktopDirectory = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktopDirectory "AVLN ToolBox.lnk"

try {
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    & dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:Platform=x64 `
        -o $publishDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $publishedExecutable = Join-Path $publishDirectory "AVLN.ToolBox.exe"
    $publishedIcon = Join-Path $publishDirectory "AVLN.ToolBox.ico"

    if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
        throw "Published executable was not found: $publishedExecutable"
    }

    if (-not (Test-Path -LiteralPath $publishedIcon -PathType Leaf)) {
        throw "Published canonical icon was not found: $publishedIcon"
    }

    Get-Process -Name "AVLN.ToolBox" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction Stop

    if (Test-Path -LiteralPath $installDirectory) {
        Remove-Item -LiteralPath $installDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $installDirectory -Recurse -Force

    $installedExecutable = Join-Path $installDirectory "AVLN.ToolBox.exe"
    $installedIcon = Join-Path $installDirectory "AVLN.ToolBox.ico"

    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExecutable
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$installedIcon,0"
    $shortcut.Description = "AVLN ToolBox"
    $shortcut.Save()

    # Ask Explorer to refresh the icon after replacing an existing shortcut.
    $iconRefresh = Join-Path $env:WINDIR "System32\ie4uinit.exe"
    if (Test-Path -LiteralPath $iconRefresh) {
        Start-Process -FilePath $iconRefresh -ArgumentList "-show" -WindowStyle Hidden -Wait
    }

    Start-Process -FilePath $installedExecutable -WorkingDirectory $installDirectory

    Write-Host "AVLN ToolBox installed successfully."
    Write-Host "Executable: $installedExecutable"
    Write-Host "Shortcut:   $shortcutPath"
    Write-Host "Icon:       $installedIcon"
}
finally {
    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
