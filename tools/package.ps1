<#
.SYNOPSIS
    Builds a release of Sea Power: R.A.T.S. Rigging and stages the folder that goes to the Steam Workshop.

.DESCRIPTION
    Release build without the automatic deploy, then dist\SeaPowerRigging\ with exactly what players get: SeaPowerRigging.dll,
    _info.ini, LICENSE.txt, CHANGELOG.md and preview.jpg (workshop\preview.jpg, if present).
    The DLL is checked against the game's assemblies with the workspace's tools\compat-check (-ManagedDir: a copy of the game's
    Managed folder when the installed game is not the current main version; default: the installed game).
    -RiggingItem <id> builds the notice for another Workshop item than the one in the project file (a test build).
    With -Install the staged folder also replaces <GameDir>\Sea Power_Data\StreamingAssets\SeaPowerRigging\, the folder to pick in
    the game's Create Mod screen. Sea Power must be closed for that. -Zip also writes dist\SeaPowerRigging-<version>.zip.
    The steps are the shared ones in build\RatsPackage.psm1; the workspace is found above this repo, -Workspace names it for a
    clone elsewhere.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
#>
param(
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { 'E:\Games\steamapps\common\Sea Power' }),
    [string]$Workspace = $env:RatsWorkspace,
    [string]$ManagedDir = $env:ManagedDir,
    [string]$RiggingItem,
    [switch]$Install,
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $root 'build\RatsPackage.psm1') -Force
if (-not $Workspace) { $Workspace = Find-RatsWorkspace -From $root }
if (-not $ManagedDir) { $ManagedDir = Join-Path $GameDir 'Sea Power_Data\Managed' }

$name = 'SeaPowerRigging'
$project = Join-Path $root "src\$name\$name.csproj"
$dist = Join-Path $root "dist\$name"
if ($RiggingItem) { $env:RigItem = $RiggingItem }

# Rigging is built through the same step as a mod; the item id reaches the project as a property.
$build = @('-c', 'Release', '--no-incremental', '-p:Deploy=false', "-p:GameDir=$GameDir", "-p:ManagedDir=$ManagedDir")
if ($RiggingItem) { $build += "-p:RigItem=$RiggingItem" }
dotnet build $project @build | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $root "src\$name\bin\Release\netstandard2.1\$name.dll") $dist

Test-RatsCompat -Workspace $Workspace -Dist $dist -RootNamespace $name -ManagedDir $ManagedDir -RiggingDir $root
Copy-RatsCommon -Root $root -Dist $dist -InfoIni (Join-Path $root 'mod\_info.ini')
$version = (Select-String -Path (Join-Path $root "src\$name\RigCore.cs") -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
Write-Host "Staged Sea Power: R.A.T.S. Rigging $version in $dist"
Show-RatsStage -Dist $dist
if ($Zip) { New-RatsZip -Dist $dist -Version $version }
if ($Install) { Install-RatsMod -Dist $dist -GameDir $GameDir }
