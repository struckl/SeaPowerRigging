# The steps every R.A.T.S. Sea Power mod's tools\package.ps1 shares: build both games' DLLs, check them against both games,
# stage the common files, install. A mod's script keeps its parameters and its own extra files; README.md next to the shared
# folder shows a script that uses this module.

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

function Build-RatsMod {
    # The globe build, then (unless -NoFlat) the flat-map build. --no-incremental on both, or the second build may reuse the
    # first one's output. Copies the DLLs into $Dist (made fresh) and returns their names.
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Dist,
        [Parameter(Mandatory)][string]$GameDir,
        [Parameter(Mandatory)][string]$GlobeManagedDir,
        [string]$FlatManagedDir,
        [string]$Workspace,
        [switch]$NoFlat,
        [switch]$SingleBuild
    )
    $name = [IO.Path]::GetFileNameWithoutExtension($Project)
    $out = Join-Path (Split-Path -Parent $Project) 'bin\Release\netstandard2.1'
    $common = @('-c', 'Release', '--no-incremental', '-p:Deploy=false', "-p:GameDir=$GameDir")
    if ($Workspace) { $common += "-p:RatsWorkspace=$Workspace" }
    dotnet build $Project @common "-p:ManagedDir=$GlobeManagedDir" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Globe build failed (with the game on 0.8.7, pass -GlobeManagedDir with the globe version''s assemblies).' }
    if (Test-Path $Dist) { Remove-Item -Recurse -Force $Dist }
    New-Item -ItemType Directory -Force $Dist | Out-Null
    Copy-Item (Join-Path $out "$name.dll") $Dist
    $dlls = @("$name.dll")
    if ($SingleBuild -or $NoFlat) { return $dlls }
    if (-not (Test-Path (Join-Path $FlatManagedDir 'Seapower-Scripts.dll'))) { throw "No 0.8.7 assemblies in $FlatManagedDir (a copy of its Sea Power_Data\Managed, or pass -NoFlat)." }
    dotnet build $Project @common -p:GameBuild=Flat "-p:FlatManagedDir=$FlatManagedDir" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Flat-map build failed.' }
    Copy-Item (Join-Path $out "${name}Flat.dll") $Dist
    return $dlls + "${name}Flat.dll"
}

function Test-RatsCompat {
    # Each DLL against both games' assemblies (the workspace's tools\compat-check): nothing missing on its own game, and on the
    # other game its entry point nothing missing and every type loadable (--classload: Unity's ECS start-up loads every type of
    # every loaded assembly, so one field of a type the other game lacks stops all missions there, although the DLL stays off).
    # A single-build DLL must fit both games whole.
    param(
        [Parameter(Mandatory)][string]$Workspace,
        [Parameter(Mandatory)][string]$Dist,
        [Parameter(Mandatory)][string]$RootNamespace,
        [Parameter(Mandatory)][string]$GlobeManagedDir,
        [Parameter(Mandatory)][string]$FlatManagedDir,
        [switch]$SingleBuild
    )
    $tool = Join-Path $Workspace 'tools\compat-check'
    if (-not (Test-Path $tool)) { throw "tools\compat-check not found in $Workspace - the DLLs cannot be checked against both games." }
    dotnet build $tool -c Release -v q | Out-Null
    $gates = @('--gate', "$RootNamespace.Plugin::TriggerEntryPoint", '--gate', "$RootNamespace.Plugin::.ctor")
    foreach ($dll in Get-ChildItem $Dist -Filter "$RootNamespace*.dll") {
        $flat = $dll.BaseName.EndsWith('Flat')
        $own = if ($flat) { $FlatManagedDir } else { $GlobeManagedDir }
        $other = if ($flat) { $GlobeManagedDir } else { $FlatManagedDir }
        dotnet run --project $tool -c Release --no-build -- $dll.FullName --against $own --quiet
        if ($LASTEXITCODE -ne 0) { throw "$($dll.Name) references things its own game does not have." }
        if ($SingleBuild) {
            dotnet run --project $tool -c Release --no-build -- $dll.FullName --against $other --quiet
            if ($LASTEXITCODE -ne 0) { throw "$($dll.Name) references things the other game does not have." }
            continue
        }
        dotnet run --project $tool -c Release --no-build -- $dll.FullName --against $other --quiet --classload @gates
        if ($LASTEXITCODE -ne 0) { throw "$($dll.Name): the entry point or a type's fields would fail on the other game." }
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

Export-ModuleMember -Function Find-RatsWorkspace, Build-RatsMod, Test-RatsCompat, Copy-RatsCommon, Get-RatsModVersion, Show-RatsStage, New-RatsZip, Install-RatsMod
