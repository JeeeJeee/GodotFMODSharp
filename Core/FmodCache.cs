using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace GodotFMODSharp;

/// <summary>
/// Caches some FMOD things to prevent multiple loads
/// </summary>
[Tool]
public static class FmodCache
{
    /// <summary>
    /// Key: BankName
    /// Value: The wrapped bank class
    /// </summary>
    private static Dictionary<string, Bank> _loadedBanks = new();

    public static bool IsBankLoaded(string path, out Bank loadedBank)
    {
        path = Path.GetFileNameWithoutExtension(path);
        return _loadedBanks.TryGetValue(path, out loadedBank);;
    }

    public static Bank[] GetLoadedBanks()
    {
        return _loadedBanks.Values.ToArray();
    }

    public static void AddBank(string path, Bank bank)
    {
        string bankName = Path.GetFileNameWithoutExtension(path);
        _loadedBanks.TryAdd(bankName, bank);
    }
}