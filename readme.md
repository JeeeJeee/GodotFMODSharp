# GodotFMODSharp
#### A Godot addon that integrates FMOD; written in C#
_The editor UI was taken from the [fmod-gdextension by Utopia-Rise](https://github.com/utopia-rise/fmod-gdextension). If you are using a gdscript project I would recommend that project over this_

## Version

---
Tested with Fmod 2.03.09 and Godot 4.4

---
## Installation

---
1. Download the plugin from here and drop it into your projects `addon` folder
   Make sure to name the addon folder "GodotFMODSharp"
2. Download the necessary FMOD libraries from the official website: https://www.fmod.com/ _(I can't redistribute them here)_
   3. Copy the fmod `.cs` files into `GodotFMODSharp\fmod\`
   4. Copy the fmod `.dll` files into `GodotFMODSharp\fmod\libs`
2. Enable the addon in your Godot project `Project -> Project settings -> Plugins`
3. Set your master bank path in `Project -> General -> (GodotFMODSharp) Banks`

## Using the plugin

---
- `FmodServer` is the main singleton that can be used to interact with Fmod functions.  
- The Master banks are loaded automatically based on the Banks path you set in the plugin settings
- The other banks have to be loaded by you, the user. I recommend loading them early in some autoload.

Most of the fmod api functions are simply wrapped by this addon so read the [official docs here](https://www.fmod.com/docs/2.03/api/welcome.html).  


## Notes

---
- Currently only Windows is supported.
    - (Adding support for other platforms shouldn't be hard but I can't test it)
- Plugins, DPSs and the Performance monitor are currently not yet in
- Since I use this for my own projects I will add new features and bug fixes as I need them; not by request.
    - _Though, do feel free to open a_ **pull request**!
