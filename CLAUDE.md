# SeaPowerRigging

Part of the Sea Power mods workspace. **The meta folder is `E:\Project\Mods\Sea Power`**: its rules always apply to this
repo, in every session, also in worktrees and clones outside that folder. They are imported here, so every session sees them:

@E:/Project/Mods/Sea Power/CLAUDE.md
@E:/Project/Mods/CLAUDE.md

Rules for this one repo only go below; rules for all Sea Power mods go into the meta folder, never here.

- The public parts of `src/SeaPowerRigging` may only grow: one Rigging update reaches every R.A.T.S. mod of every player at once.
  A rename or removal needs a new major version and a release of every mod that uses it.
- Assembly version stays 1.0.0.0 so a mod built against an older Rigging binds to a newer one; the real version is the plugin
  version, the file version and `Rigging.Version`.
- No per-mod state in a static: one object per mod (`RigMod`) holds it. Statics are for facts about the game and the registry of mods.
