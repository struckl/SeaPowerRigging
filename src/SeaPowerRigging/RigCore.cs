using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnchorChain;
using Rats.Rigging.Notice;

namespace Rats.Rigging;

/// <summary>R.A.T.S. Rigging itself: its plugin id, its version and the lines it writes under its own log source.</summary>
public static class RigCore
{
    public const string Guid = "dev.sewerlabs.seapowerrigging";
    public const string Name = "R.A.T.S. Rigging";
    public const string Version = "1.0.0";

    internal static RigLog Log { get; } = new RigLog("SeaPowerRigging");

    /// <summary>Rigging as a mod of its own for the registry of hooks it installs once for every mod.</summary>
    internal static RigInfo Info { get; } = new RigInfo(Guid, Name, Version, "SeaPowerRigging", typeof(RigCore).Assembly);

    /// <summary>The Workshop item the notice offers, from the build (RigItem in the csproj); 0 when there is none yet.</summary>
    internal static ulong WorkshopItem
    {
        get
        {
            string text = typeof(RigCore).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "Rigging.WorkshopItem")?.Value;
            return ulong.TryParse(text, out ulong id) ? id : 0;
        }
    }

    /// <summary>The folder name of the copy that runs: the Workshop item's folder, or the mod that carries a built-in copy.</summary>
    internal static string RunsFrom() => Path.GetFileName(Path.GetDirectoryName(typeof(RigCore).Assembly.Location) ?? "");

    internal static void Start()
    {
        Log.Info($"{Name} {Version} loaded (from {RunsFrom()}).");
        RiggingNotice.Begin();
    }
}

/// <summary>
/// The entry point of the Rigging plugin. It only logs the version and, when needed, starts the notice: what the mods need from
/// Rigging is not set up here, so the order in which plugins start does not matter.
/// </summary>
[ACPlugin(RigCore.Guid, RigCore.Name, RigCore.Version)]
public class RiggingPlugin : IAnchorChainMod
{
    public void TriggerEntryPoint() => RigCore.Start();
}
