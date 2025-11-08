using System.Collections.Generic;
using FMOD;
using Godot;
using Godot.Collections;

namespace GodotFMODSharp.Editor;

[Tool]
public static class FmodPluginSettings
{
    public const string PluginName = "GodotFMODSharp";
    private const string _banks = "Banks";
    private const string _sound = "Sound";
        
    /// <summary>
    /// Paths to properties used by the editor and their default values
    /// </summary>
    public static (string propertyPath, Variant defaultValue) BanksPath = (PluginName + "/" + _banks + "/" + "BanksPaths", "");
    public static (string propertyPath, Variant defaultValue) MaxChannels = (PluginName+ "/" + _sound + "/" + "MaxChannels", 512);
    public static (string propertyPath, Variant defaultValue) SpeakerMode = (PluginName + "/" + _sound + "/" + "SpeakerMode", (int)SPEAKERMODE.STEREO);
    public static (string propertyPath, Variant defaultValue) SampleRate = (PluginName + "/" + _sound + "/" + "SampleRate", 48000);
    public static (string propertyPath, Variant defaultValue) RawSpeakerCount = (PluginName + "/" + _sound + "/" + "RawSpeakerCount", 0);
    public static (string propertyPath, Variant defaultValue) DefaultListenerCount = (PluginName + "/" + _sound + "/" + "DefaultListenerCount", 1);


    /// <summary>
    /// Settings that have default values but are not overriden are not saved by godot and are therefore not available at runtime
    /// So this static function exists so the runtime GDFSFmodServer autoload can register these settings and actually use them
    /// See: https://github.com/godotengine/godot/issues/56598 (which by the way, was just closed with a little fucking docs change that explains less than the bug ticket)
    /// </summary>
    public static void RegisterFmodSettings()
    {
        AddEditorSetting(BanksPath.propertyPath, BanksPath.defaultValue, Variant.Type.String, PropertyHint.Dir, "Path to the folder containing the FMOD banks");
        AddEditorSetting(MaxChannels.propertyPath, MaxChannels.defaultValue, Variant.Type.Int, PropertyHint.Range, "2, 2048");
        AddEditorSetting(SpeakerMode.propertyPath, SpeakerMode.defaultValue, Variant.Type.Int, PropertyHint.Enum, "DEFAULT,RAW,MONO,STEREO,QUAD,SURROUND,5POINT1,7POINT1,7POINT1POINT4");
        AddEditorSetting(SampleRate.propertyPath, SampleRate.defaultValue, Variant.Type.Int, PropertyHint.Range, "48000, 96000, 1");
        AddEditorSetting(RawSpeakerCount.propertyPath, RawSpeakerCount.defaultValue, Variant.Type.Int, PropertyHint.Range, "0, 8");
        AddEditorSetting(DefaultListenerCount.propertyPath, DefaultListenerCount.defaultValue, Variant.Type.Int, PropertyHint.Range, "1, 8");
    }
        
    private static void AddEditorSetting(string settingName, Variant defaultValue, Variant.Type type, PropertyHint hint, string hintString)
    {
        Dictionary propertyInfo = new();
        propertyInfo["name"] = settingName;
        propertyInfo["type"] = (int)type;
        propertyInfo["hint"] = (int)hint;
        propertyInfo["hint_string"] = hintString;
        propertyInfo["default"] = defaultValue;

        Variant setValue;
        if (ProjectSettings.HasSetting(settingName))
        {
            setValue = ProjectSettings.GetSetting(settingName);
        }
        else
        {
            setValue = defaultValue;
        }
		
        ProjectSettings.SetSetting(settingName, setValue);
        ProjectSettings.SetAsBasic(settingName, true);
        ProjectSettings.SetInitialValue(settingName, defaultValue);
        ProjectSettings.AddPropertyInfo(propertyInfo);
    }
}

public static class FmodEditorHelpers
{
    /// <summary>
    /// Returns all files with given extension in godot filesystem paths
    /// res:// | user://
    /// </summary>
    /// <param name="path"></param>
    /// <param name="fileExtension"></param>
    /// <param name="recursive"></param>
    /// <returns></returns>
    public static List<string> GetAllFilesAtPath(string path, string fileExtension, bool recursive = true)
    {
        List<string> filePaths = new List<string>();
        var dir = DirAccess.Open(path);

        // Add files in current directory
        foreach (var fileName in dir.GetFiles())
        {
            // filter every file that doesn't have the correct file extension
            if (!fileName.EndsWith(fileExtension))
            {
                continue;
            }

            filePaths.Add(dir.GetCurrentDir(false) + "/" + fileName);
        }

        if (recursive)
        {
            foreach (var subDir in dir.GetDirectories())
            {
                filePaths.AddRange(GetAllFilesAtPath(dir.GetCurrentDir(false) + "/" + subDir, fileExtension));
            }   
        }

        return filePaths;
    }
}