using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Rats.Rigging;

/// <summary>
/// The loader starts every mod folder it finds, ticked in the game's Mod Manager or not (Anchor Chain did; Keel starts only the
/// switched-on ones, but a mod must stay out in either case). The Mod Manager keeps its choice in usersettings.ini
/// (ModNDirectory=folder,True|False,workshop id), so the mod's entry there is read and the mod stays out when it is unticked.
/// </summary>
internal static class ModState
{
    // The same line Keel's ModList reads (digits in the key), the switch read leniently.
    private static readonly Regex ModLine = new Regex(@"^Mod\d+Directory=([^,\r\n]+),\s*([^,\r\n]*)", RegexOptions.IgnoreCase);

    /// <summary>False only when the Mod Manager lists the folder of the mod's DLL as unticked; anything unclear counts as on.</summary>
    public static bool TickedInModManager(RigInfo info, RigLog log)
    {
        try
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(info.ModAssembly.Location) ?? "");
            string file = Path.Combine(Application.persistentDataPath, "usersettings.ini");
            if (folder.Length == 0 || !File.Exists(file))
                return true;
            foreach (string line in File.ReadAllLines(file))
            {
                Match m = ModLine.Match(line);
                if (m.Success && m.Groups[1].Value.Trim().Equals(folder, StringComparison.OrdinalIgnoreCase))
                    return !m.Groups[2].Value.Trim().Equals("False", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            log.Debug($"Mod Manager state unreadable: {ex.Message}");
        }
        return true;
    }
}
