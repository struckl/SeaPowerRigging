using System;
using SeaPower;

namespace Rats.Shared;

/// <summary>Which game the mod runs in, and whether the game's UI (NoesisGUI) is up.</summary>
internal static class GameBuild
{
    /// <summary>
    /// Only the 3D-globe build has MapDisplayState. Looked up by name, so this runs on every game build: the entry point
    /// asks it before the mod decides whether this build is the one it is made for.
    /// </summary>
    public static readonly bool IsGlobe = Type.GetType("SeapowerUI.MapDisplayState, Seapower-Scripts") != null;

    /// <summary>
    /// A WindowHost registers itself with WindowHostManager when it is built, which only happens while NoesisGUI runs (the
    /// main menu view has one). From then on Noesis types are safe to patch: patching a class that registers dependency
    /// properties before Noesis exists is a native crash.
    /// </summary>
    public static bool NoesisUp() => Singleton<SeapowerUI.WindowHostManager>.InstanceExists();
}
