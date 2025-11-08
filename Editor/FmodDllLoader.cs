using System;
using System.IO;
using System.Runtime.InteropServices;
using Godot;

namespace GodotFMODSharp.Editor;

public static class FmodDllLoader
{
    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    public static void LoadFmodDllsForEditor()
    {
        string fmodLibsPath = ProjectSettings.GlobalizePath("res://addons/GodotFMODSharp/fmod/libs/");
        foreach (string file in Directory.GetFiles(fmodLibsPath, "*.dll"))
        {
            LoadLibrary(file);
        }
    }
}