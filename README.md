# Sea Power: R.A.T.S. Rigging

The part every R.A.T.S. mod shares, as one library: start, settings, hooks, mission events, map layers and the Options menu tab.

| | |
| --- | --- |
| Questions | [R.A.T.S. Discord](https://discord.gg/CzuHd4f8Yf): help, ideas, talk; the first place to ask |
| Version | `1.0.1` |
| Game | Sea Power 0.9.3 (build 261116), the current main version |
| Needs | R.A.T.S. Keel, the mod loader |
| Get it | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3843688859) |
| Settings | none to set; `BepInEx\config\dev.sewerlabs.seapowerrigging.cfg` holds the tick of its notice |
| Bug reports | [R.A.T.S. feedback board](https://hub.sewerlabs.dev/s/rigging) |
| Source | R.A.T.S. Source License, see `LICENSE` |

## Features

- One object per mod (`RigMod`): its log, settings, hooks, mission events, Options tab and host.
- Settings in `BepInEx\config\<mod id>.cfg` with "Enable mod" and "Verbose log", written a moment after the last change.
- Hooks by feature: a missing game method switches one feature off with one log line, not the mod.
- Mission start and end for every mod, from one patch each.
- A tab in the game's Options menu, and an entry in the map's Layers menu.
- A mod stays off when it is unticked in the Mod Manager.
- A notice on the main menu for players who run the mods on a built-in copy and have not subscribed to this item.

Details: [docs/api.md](docs/api.md).

## Install

1. Players do nothing: the R.A.T.S. mods name this item as a required item and carry a built-in copy for a while.
2. Subscribe to the item to replace the built-in copies; the notice offers it.

## Building

- Needs the .NET SDK (8+) and a local Sea Power install.
- A mod builds against this repo: its project imports `build\Rats.Mod.props` (see [docs/building.md](docs/building.md)).

```bash
dotnet build src/SeaPowerRigging/SeaPowerRigging.csproj -c Release -p:Deploy=false
```

## Credits and license

- R.A.T.S. Source License, see `LICENSE`.
- `ref\AnchorChain.keel.bin` is Anchor Chain's library by the SeaPower-Modders (MIT), compile time only: `ref\THIRD-PARTY.txt`.
