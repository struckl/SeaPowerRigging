# R.A.T.S. shared mod skeleton

The parts every R.A.T.S. Sea Power mod has in common, kept once. Each mod compiles these files into its own DLL as linked
source: no mod references Keel at run time (`docs/loader.md` in the workspace).

## Files

| File | What it is |
|---|---|
| `Rats.Mod.props` | The common project part: netstandard2.1, BepInEx and HarmonyX packages, the game references every mod needs, the flat-map build (`-p:GameBuild=Flat`), the deploy target (DLL, pdb, mod files), and the `Compile` lines of the files below |
| `ModEntry.cs` | The start: game gate (globe / flat build), loaded-once marker, Mod Manager tick check, config, Enabled check, load-stage hooks, the mod's own start steps, the host, the "loaded" line |
| `ModLog.cs` | Log lines under the mod's log source; `Debug` only with Verbose log on |
| `ModHost.cs` | Base of the mod's per-frame object: first five errors logged, hooks that wait for NoesisGUI, mission start and end, debounced config save, teardown on quit while Noesis still lives |
| `HookRegistry.cs` | Every hook by feature: targets resolved first, constructors, `[HarmonyPriority]`, rollback when a patch throws, optional features, stages (`Load`, `AfterNoesis`), `IsActive`, `Summary` |
| `MissionHooks.cs` | Mission ready (`MainGameViewModel.OnStart`) and mission gone (`GameMain.OnDestroy`) as events, each subscriber guarded |
| `ModSettings.cs` | The config file with "1. General" (Enable mod, Verbose log), written a moment after the last change, one-time migrations, `ChangeCounter` per area |
| `ModState.cs` | Whether the game's Mod Manager has the mod ticked (Anchor Chain loads unticked folders too) |
| `GameBuild.cs` | Globe or flat game; whether NoesisGUI runs |
| `GameReflection.cs` | Globe build only: internal game members by reflection (`IsDark`, `NewLayer`, `Getter`), held as plain types so the DLL stays loadable on 0.8.7 |
| `LayerEntry.cs` | An entry in the map's Layers menu, kept in step with a config entry (both builds) |
| `OptionsPage.cs` | A tab in the game's Options menu (Audio-tab group as fallback) and `OptionsBuilder` for its rows (header, note, check, slider, choice); plain controls only |
| `tools/RatsPackage.psm1` | The package-script steps: build both DLLs, compat check against both games, common files, version, stage listing, zip, install |

## Linking a mod

1. The csproj sets its own properties, finds the workspace and imports the props:

   ```xml
   <PropertyGroup>
     <AssemblyName>SeaPowerX</AssemblyName>
     <RootNamespace>SeaPowerX</RootNamespace>
     <Product>...</Product>
     <Version>1.0.0</Version>
     <RatsUseLayers>true</RatsUseLayers>             <!-- optional: LayerEntry + GameReflection -->
     <RatsUseReflection>true</RatsUseReflection>     <!-- optional: GameReflection alone -->
     <RatsUseOptionsPage>true</RatsUseOptionsPage>   <!-- optional: OptionsPage -->
     <RatsSingleBuild>true</RatsSingleBuild>         <!-- optional: one DLL for both games, no flat build -->
     <RatsWorkspace Condition="'$(RatsWorkspace)' == ''">$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), mods.json))</RatsWorkspace>
     <RatsShared Condition="'$(RatsShared)' == ''">$([MSBuild]::EnsureTrailingSlash('$(RatsWorkspace)'))SeaPowerKeel\shared\</RatsShared>
   </PropertyGroup>
   <Import Project="$(RatsShared)Rats.Mod.props" />
   ```

   - The workspace is the folder with `mods.json` above the project, so builds from a worktree under the repo work too
   - `-p:RatsWorkspace=<folder>` for a clone outside the workspace, `-p:RatsShared=<folder>\` to build against another copy of this folder (a Keel worktree)
   - Extra references (Audio, Entities, ...) stay in the mod's csproj
2. The mod keeps one small file, `ModInfo.cs`, with what the shared code reads (namespace `Rats.Shared`):

   ```csharp
   global using Rats.Shared;
   namespace Rats.Shared;
   internal static class ModInfo
   {
       public const string Guid = SeaPowerX.Plugin.Guid;          // config file, Harmony id, loaded-once marker
       public const string Name = SeaPowerX.Plugin.Name;
       public const string Version = SeaPowerX.Plugin.Version;
       public const string LogSource = "SeaPowerX";               // log source and host object name
   }
   ```
3. `Plugin` keeps the `[ACPlugin]` attribute, its constants and `TriggerEntryPoint` (the compat check names it as the method that runs on every game):

   ```csharp
   public void TriggerEntryPoint() => ModEntry.Trigger(Start);
   [MethodImpl(MethodImplOptions.NoInlining)]
   private static void Start() => ModEntry.Start<Host>(Settings.Bind, Hooks.Declare);
   ```
4. The host derives from `ModHost` (`Tick`, `OnMissionReady`, `OnMissionEnded`, `Teardown`, `StageTwoReady`); hooks are declared with
   `HookRegistry.Add(...).Hook(...)` and `MissionHooks.Declare()`; settings are bound in `Settings.Bind(ConfigFile)`.
5. `tools\package.ps1` imports `tools\RatsPackage.psm1` from this folder (Sensor Views' script is the example).

## Rules for these files

- One namespace text for every mod (`Rats.Shared`): each mod's copy is a separate type in its own assembly. Nothing Noesis resolves
  by name (XAML types) may live here.
- No field of a game type that one of the two games lacks: Unity's ECS loads every type of every DLL (`docs/both-versions.md`).
- A change here changes every mod at its next build: build and `modtool compat` every mod that links the file.
