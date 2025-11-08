#if TOOLS
using System.IO;
using Godot;
using GodotFMODSharp.Editor;
using FileAccess = Godot.FileAccess;

namespace GodotFMODSharp;

[Tool]
public partial class FmodExporter : EditorExportPlugin
{
    private string _fmodLibsPath = ProjectSettings.GlobalizePath("res://addons/GodotFMODSharp/fmod/libs");
    
    public override void _ExportBegin(string[] features, bool isDebug, string path, uint flags)
    { 
        string outputDirectoryPathAbs = Path.GetFullPath(path).GetBaseDir();
        CopyFmodDlls(outputDirectoryPathAbs);
        AddBankFiles();
    }

    /// <summary>
    /// Adds fmod .bank files to the exported .pck file
    /// </summary>
    private void AddBankFiles()
    {
        string projectRoot = ProjectSettings.GlobalizePath("res://").GetBaseDir();
        string[] bankFilePaths = Directory.GetFiles(projectRoot, "*.bank", SearchOption.AllDirectories);
        foreach (var bankFilePath in bankFilePaths)
        {
            var finalPathToBank = ProjectSettings.LocalizePath(bankFilePath);
            AddFile(finalPathToBank, FileAccess.GetFileAsBytes(finalPathToBank), false);
        }
    }

    public override string _GetName()
    {
        return FmodPluginSettings.PluginName + "ExportPlugin";
    }

    /// <summary>
    /// Copies the fmod .dll files directly from the addon folder into the export target directory
    /// </summary>
    /// <param name="targetDir"></param>
    private void CopyFmodDlls(string targetDir)
    {
        if (!Directory.Exists(targetDir))
        {
            GD.PrintErr($"[FMOD] Directory {targetDir} does not exist");
            return;   
        }

        foreach (var file in Directory.GetFiles(_fmodLibsPath, "*.dll"))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
    }
}
#endif