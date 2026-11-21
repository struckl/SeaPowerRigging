# R.A.T.S. Rigging: building, and how a mod uses it

## Building Rigging

Requires the .NET SDK (8+) and a local Sea Power install (`-p:GameDir=...`, or `-p:ManagedDir=...` at a copy of its Managed folder).

```bash
dotnet build src/SeaPowerRigging/SeaPowerRigging.csproj -c Release -p:Deploy=false
powershell -File tools/package.ps1 -Install -Zip    # stage dist\SeaPowerRigging, check it, install it, zip it
```

- One DLL, `SeaPowerRigging.dll`, for the game's current main version. Assembly version stays 1.0.0.0; the real version is
  in `RigCore.Version`, the plugin attribute and the file version.
- `-p:RigItem=<id>` names the Workshop item the notice offers (a test build may name another; 0 means none, no notice).
- Anchor Chain's library for the plugin attribute is in `ref\` (MIT, compile time only).

## How a mod uses it

1. The mod's project sets its own properties, finds the workspace and imports Rigging's props:

   ```xml
   <PropertyGroup>
     <AssemblyName>SeaPowerX</AssemblyName>
     <RootNamespace>SeaPowerX</RootNamespace>
     <Product>...</Product>
     <Version>1.0.0</Version>
     <RatsWorkspace Condition="'$(RatsWorkspace)' == ''">$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), mods.json))</RatsWorkspace>
     <RatsRigging Condition="'$(RatsRigging)' == ''">$([MSBuild]::EnsureTrailingSlash('$(RatsWorkspace)'))SeaPowerRigging\</RatsRigging>
   </PropertyGroup>
   <Import Project="$(RatsRigging)build\Rats.Mod.props" />
   ```

   - the props reference the Rigging project (built on demand, never copied into the mod's output) and the game's assemblies
   - `-p:RatsRigging=<folder>\` builds against another copy of this repo (a worktree)
2. `Plugin` keeps the `[ACPlugin]` attribute and names Rigging as a dependency; it holds no Rigging type:

   ```csharp
   [ACPlugin(Guid, Name, Version)]
   [ACDependency("dev.sewerlabs.seapowerrigging", "1.0.0", null)]
   public class Plugin : IAnchorChainMod
   {
       public void TriggerEntryPoint() => Start();
       [MethodImpl(MethodImplOptions.NoInlining)]
       private static void Start() => Rig.Start<Host>(Settings.Bind, Hooks.Declare);
   }
   ```

3. The mod's one small file `Rig.cs` makes its `RigMod` and keeps it for the code that is not handed one (see `docs/api.md`).
4. The host derives from `RigHost`; hooks are declared with `Rig.Hooks.Add(...).Hook(...)` and `Rig.Mission.Declare()`; settings are
   bound in `Settings.Bind(ConfigFile)`.
5. `tools\package.ps1` imports `build\RatsPackage.psm1` from this repo and adds the built-in copy with `Copy-RatsRiggingCopy`.

## The minimum version

A mod names the oldest Rigging it works with in `[ACDependency]`. The loader keeps a mod out, with the reason on its card, when
the Rigging it finds is older or missing. The public parts of the library only grow, so a mod built against an older Rigging
runs on a newer one.
