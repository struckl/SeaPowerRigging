# The steps every R.A.T.S. Sea Power mod's tools\package.ps1 shares: build the DLL, check it against the game and against Rigging,
# stage the common files and Rigging's built-in copy, install. A mod's script keeps its parameters and its own extra files;
# README.md in the Rigging repo shows a script that uses this module.

$ErrorActionPreference = 'Stop'

function Find-RatsWorkspace {
    # The folder holding mods.json above $From: works from the repo, a worktree under it, or with -Workspace from anywhere.
    param([Parameter(Mandatory)][string]$From)
    $dir = [IO.Path]::GetFullPath($From)
    while ($dir) {
        if (Test-Path (Join-Path $dir 'mods.json')) { return $dir }
        $dir = Split-Path -Parent $dir
    }
    throw "No workspace (a folder with mods.json) above $From - pass -Workspace."
}

function Get-RatsRiggingDll {
    # Rigging's built DLL: what a mod compiles against and what a package carries as its built-in copy.
    param([Parameter(Mandatory)][string]$Workspace, [string]$RiggingDir)
    if (-not $RiggingDir) { $RiggingDir = Join-Path $Workspace 'SeaPowerRigging' }
    return Join-Path $RiggingDir 'src\SeaPowerRigging\bin\Release\netstandard2.1\SeaPowerRigging.dll'
}

function Build-RatsMod {
    # One build against the game's current assemblies. --no-incremental, or a stale DLL may be reused. Copies the DLL into $Dist
    # (made fresh) and returns its name. Rigging is built first as the project's reference.
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Dist,
        [Parameter(Mandatory)][string]$GameDir,
        [Parameter(Mandatory)][string]$ManagedDir,
        [string]$Workspace,
        [string]$RiggingDir
    )
    $name = [IO.Path]::GetFileNameWithoutExtension($Project)
    $out = Join-Path (Split-Path -Parent $Project) 'bin\Release\netstandard2.1'
    $buildArgs = @('-c', 'Release', '--no-incremental', '-p:Deploy=false', "-p:GameDir=$GameDir", "-p:ManagedDir=$ManagedDir")
    if ($Workspace) { $buildArgs += "-p:RatsWorkspace=$Workspace" }
    # The project imports "$(RatsRigging)build\Rats.Mod.props", so the folder needs its closing slash; forward slashes, because a
    # trailing backslash before the closing quote of a path with a space ("Sea Power") would swallow the quote.
    if ($RiggingDir) { $buildArgs += ('-p:RatsRigging=' + ($RiggingDir.TrimEnd('\', '/') -replace '\\', '/') + '/') }
    dotnet build $Project @buildArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (Test-Path $Dist) { Remove-Item -Recurse -Force $Dist }
    New-Item -ItemType Directory -Force $Dist | Out-Null
    Copy-Item (Join-Path $out "$name.dll") $Dist
    return @("$name.dll")
}

function Test-RatsCompat {
    # Each DLL against the game's assemblies (the workspace's tools\compat-check): nothing missing, every type loadable
    # (--classload: Unity's ECS start-up loads every type of every loaded assembly) and every member of Rigging it uses there
    # (--also: the folder holding SeaPowerRigging.dll).
    param(
        [Parameter(Mandatory)][string]$Workspace,
        [Parameter(Mandatory)][string]$Dist,
        [Parameter(Mandatory)][string]$RootNamespace,
        [Parameter(Mandatory)][string]$ManagedDir,
        [string]$RiggingDir
    )
    $tool = Join-Path $Workspace 'tools\compat-check'
    if (-not (Test-Path $tool)) { throw "tools\compat-check not found in $Workspace - the DLL cannot be checked against the game." }
    $rigging = Split-Path -Parent (Get-RatsRiggingDll -Workspace $Workspace -RiggingDir $RiggingDir)
    dotnet build $tool -c Release -v q | Out-Null
    foreach ($dll in Get-ChildItem $Dist -Filter "$RootNamespace*.dll") {
        dotnet run --project $tool -c Release --no-build -- $dll.FullName --against $ManagedDir --also $rigging --classload --quiet
        if ($LASTEXITCODE -ne 0) { throw "$($dll.Name) references things the game or Rigging does not have." }
    }
}

function Copy-RatsCommon {
    # _info.ini, LICENSE as LICENSE.txt, CHANGELOG.md (Keel's What's new shows its newest lines when there are no Workshop
    # notes: local copies, GitHub zips) and the Workshop preview.
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Dist,
        [Parameter(Mandatory)][string]$InfoIni
    )
    Copy-Item $InfoIni $Dist
    Copy-Item (Join-Path $Root 'LICENSE') (Join-Path $Dist 'LICENSE.txt')
    Copy-Item (Join-Path $Root 'CHANGELOG.md') $Dist
    $preview = Join-Path $Root 'workshop\preview.jpg'
    if (Test-Path $preview) { Copy-Item $preview $Dist } else { Write-Warning 'workshop\preview.jpg missing - nothing to pick as the Workshop preview.' }
}

function Copy-RatsRiggingCopy {
    # The built-in copy of Rigging every code mod's package carries for a while: players who have not subscribed to the item
    # yet still run the mod (the loader runs the newest copy of one plugin id), and Rigging's notice offers them the item.
    param(
        [Parameter(Mandatory)][string]$Workspace,
        [Parameter(Mandatory)][string]$Dist,
        [string]$RiggingDir
    )
    $dll = Get-RatsRiggingDll -Workspace $Workspace -RiggingDir $RiggingDir
    if (-not (Test-Path $dll)) { throw "Rigging is not built ($dll)." }
    Copy-Item $dll $Dist
    Write-Host "Built-in copy of Rigging $((Get-Item $dll).VersionInfo.FileVersion) added."
}

function Get-RatsModVersion {
    # The Version constant of the mod's Plugin.cs (modtool reads the same one).
    param([Parameter(Mandatory)][string]$PluginFile)
    return (Select-String -Path $PluginFile -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
}

function Show-RatsStage {
    param([Parameter(Mandatory)][string]$Dist)
    Get-ChildItem -Recurse $Dist | Where-Object { -not $_.PSIsContainer } |
        ForEach-Object { Write-Host ("  {0,10:N0}  {1}" -f $_.Length, $_.FullName.Substring($Dist.Length + 1)) }
}

function New-RatsZip {
    # dist\<Name>-<version>.zip for a GitHub release.
    param(
        [Parameter(Mandatory)][string]$Dist,
        [Parameter(Mandatory)][string]$Version
    )
    $archive = Join-Path (Split-Path -Parent $Dist) ("{0}-{1}.zip" -f (Split-Path -Leaf $Dist), $Version)
    if (Test-Path $archive) { Remove-Item -Force $archive }
    Compress-Archive -Path $Dist -DestinationPath $archive -CompressionLevel Fastest
    Write-Host "Release zip: $archive"
}

function Install-RatsMod {
    # Mirrors the staged folder into the game's StreamingAssets (the folder to pick in Mods > Create Mod). The game must be closed.
    param(
        [Parameter(Mandatory)][string]$Dist,
        [Parameter(Mandatory)][string]$GameDir
    )
    if (Get-Process -Name 'Sea Power' -ErrorAction SilentlyContinue) { throw 'Sea Power is running - close it first.' }
    $target = Join-Path $GameDir ('Sea Power_Data\StreamingAssets\' + (Split-Path -Leaf $Dist))
    robocopy $Dist $target /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Installing failed (robocopy $LASTEXITCODE)." }
    Write-Host "Installed to $target - pick this folder in Mods > Create Mod."
}

Export-ModuleMember -Function Find-RatsWorkspace, Get-RatsRiggingDll, Build-RatsMod, Test-RatsCompat, Copy-RatsCommon, Copy-RatsRiggingCopy, Get-RatsModVersion, Show-RatsStage, New-RatsZip, Install-RatsMod
