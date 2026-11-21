using BepInEx.Logging;

namespace Rats.Shared;

/// <summary>The mod's lines in BepInEx\LogOutput.log, under the mod's log source (ModInfo.LogSource, the assembly's base name).</summary>
internal static class ModLog
{
    private static readonly ManualLogSource Source = Logger.CreateLogSource(ModInfo.LogSource);

    public static void Info(string message) => Source.LogInfo(message);

    public static void Warn(string message) => Source.LogWarning(message);

    public static void Error(string message) => Source.LogError(message);

    /// <summary>Only with "Verbose log" on in the mod's config.</summary>
    public static void Debug(string message)
    {
        if (ModSettings.Verbose)
            Source.LogInfo("[dbg] " + message);
    }
}
