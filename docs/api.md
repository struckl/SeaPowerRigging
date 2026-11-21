# R.A.T.S. Rigging: the public API (`Rats.Rigging`)

The public parts only grow: one Rigging update reaches every R.A.T.S. mod of every player at once.

| Type | What it is |
|---|---|
| `RigInfo` | a mod's id, name, version, log source and its DLL, made once in the mod's start |
| `RigMod` | one mod's object: `Log`, `Settings`, `Hooks`, `Mission`, `Options`, `Host`, `Quitting`; `Create(info)` and `Start<THost>(bind, declare, steps)` |
| `RigLog` | `Info`, `Warn`, `Error`, `Debug` (Debug only with "Verbose log" on) under the mod's log source |
| `RigSettings` | the mod's config file (`File`, `Existed`, `Verbose`, `Enabled`); `Migrate(counter, steps)` for one-time changes |
| `ChangeCounter` | counts changes of a group of entries, `Of(file, ...)` and `AllBut(file, ...)` |
| `RigHooks`, `HookFeature`, `HookStage` | the mod's hooks by feature: `Add`, `IsActive`, `Summary`; `HookFeature.Hook` names a game method and a patch |
| `RigMission` | `Started` and `Ended` events; `Declare(optional)` stands for the two hooks |
| `RigOptions`, `OptionsBuilder` | a tab in the game's Options menu: `Declare(title, rows)`; header, note, check, choice, slider |
| `RigHost` | base of the mod's per-frame object: `Tick`, `OnMissionReady`, `OnMissionEnded`, `Teardown`, `StageTwoReady`, `ReportError` |
| `LayerEntry` | an entry in the map's Layers menu, kept in step with a config entry |
| `GameReflection`, `GameBuild` | facts about the game: `IsDark(palette)`, `NoesisUp()` |
| `RigCore`, `RiggingPlugin` | Rigging itself: its id and version, and its plugin entry |

## One object per mod

Each mod compiles nothing of this in any more. Its start makes one `RigMod`:

```csharp
Mod = RigMod.Create(new RigInfo(Plugin.Guid, Plugin.Name, Plugin.Version, "SeaPowerX", typeof(Plugin).Assembly));
Mod.Start<Host>(Settings.Bind, Hooks.Declare, steps);
```

- The mod keeps the object in a small static class (`Rig`), so code that is not handed it can reach `Rig.Log`, `Rig.Settings` and so on.
- Rigging holds no per-mod state in a static; the registry of mods and the facts about the game are the only statics.
- `RigInfo.ModAssembly` is how the Mod Manager's tick is found: the folder of the mod's DLL, not of Rigging's.

## What Rigging does once for every mod

- Mission start (`MainGameViewModel.OnStart`) and end (`GameMain.OnDestroy`) and the Options menu page's constructor are patched once,
  under Rigging's own Harmony id, and passed on to every mod's object. Each mod's own hooks stay under the mod's Harmony id.
- The notice (a game message box) shows on the main menu while the Rigging Workshop item is not there or not switched on.
