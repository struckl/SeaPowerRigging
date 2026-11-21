using SeaPower;

namespace Rats.Rigging;

/// <summary>Facts about the running game that every mod asks the same way.</summary>
public static class GameBuild
{
    /// <summary>
    /// Whether the game's UI (NoesisGUI) runs. A WindowHost registers itself with WindowHostManager when it is built, which only
    /// happens while NoesisGUI runs (the main menu view has one). From then on Noesis types are safe to patch: patching a class
    /// that registers dependency properties before Noesis exists is a native crash.
    /// </summary>
    public static bool NoesisUp() => Singleton<SeapowerUI.WindowHostManager>.InstanceExists();
}
