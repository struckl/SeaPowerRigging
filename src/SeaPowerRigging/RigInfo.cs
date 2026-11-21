using System.Reflection;

namespace Rats.Rigging;

/// <summary>What Rigging needs to know about a mod. A mod makes one in its start and hands it to <see cref="RigMod.Create"/>.</summary>
public sealed class RigInfo
{
    public RigInfo(string guid, string name, string version, string logSource, Assembly modAssembly)
    {
        Guid = guid;
        Name = name;
        Version = version;
        LogSource = logSource;
        ModAssembly = modAssembly;
    }

    /// <summary>The mod's plugin id: the config file name (BepInEx\config\(Guid).cfg), the Harmony id and the loaded-once marker.</summary>
    public string Guid { get; }

    /// <summary>The name shown in the game, "R.A.T.S. (mod)".</summary>
    public string Name { get; }

    public string Version { get; }

    /// <summary>The BepInEx log source and the name of the host object: the mod's assembly base name.</summary>
    public string LogSource { get; }

    /// <summary>The mod's own DLL: its folder is how the Mod Manager's tick for the mod is found.</summary>
    public Assembly ModAssembly { get; }
}
