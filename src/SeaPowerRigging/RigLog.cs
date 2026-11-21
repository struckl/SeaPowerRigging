using System;
using BepInEx.Logging;

namespace Rats.Rigging;

/// <summary>A mod's lines in BepInEx\LogOutput.log, under the mod's log source. Debug lines only with "Verbose log" on in the mod's config.</summary>
public sealed class RigLog
{
    private readonly ManualLogSource source;
    private readonly Func<bool> verbose;

    internal RigLog(string sourceName, Func<bool> verbose = null)
    {
        source = Logger.CreateLogSource(sourceName);
        this.verbose = verbose;
    }

    public void Info(string message) => source.LogInfo(message);

    public void Warn(string message) => source.LogWarning(message);

    public void Error(string message) => source.LogError(message);

    public void Debug(string message)
    {
        if (verbose != null && verbose())
            source.LogInfo("[dbg] " + message);
    }
}
