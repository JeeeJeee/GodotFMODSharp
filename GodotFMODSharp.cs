#if TOOLS
using Godot;
using GodotFMODSharp.Editor;

namespace GodotFMODSharp;

[Tool]
public partial class GodotFMODSharp : EditorPlugin
{
	private FmodExporter _fmodExport = new();

	public override void _EnablePlugin()
	{
		AddAutoloadSingleton("FmodServer", "res://addons/GodotFMODSharp/Core/FmodServer.cs");
		ProjectSettings.Save();
	}

	public override void _DisablePlugin()
	{
		RemoveAutoloadSingleton("FmodServer");
		RemoveEditorUI();
		if(_fmodExport != null) { RemoveExportPlugin(_fmodExport); }
		ProjectSettings.Save();
	}

	public override void _EnterTree()
	{
		AddEditorUI();
		AddExportPlugin(_fmodExport);
	}

	private Button _fmodBankExplorerButton;
	private FmodBankExplorer _fmodBankExplorer;
	private void AddEditorUI()
	{
		// Add the plugin project settings
		FmodPluginSettings.RegisterFmodSettings();
		
		// Add the FMOD explorer button in the top right
		_fmodBankExplorerButton = new Button();
		_fmodBankExplorerButton.Icon = GD.Load<Texture2D>("res://addons/GodotFMODSharp/icons/fmod_icon.svg");
		_fmodBankExplorerButton.Text = "Fmod Explorer";
		_fmodBankExplorerButton.Pressed += OnFmodExplorerButtonPressed; 
		_fmodBankExplorer = GD.Load<PackedScene>("res://addons/GodotFMODSharp/UI/FmodBankExplorer.tscn").Instantiate<FmodBankExplorer>();
		_fmodBankExplorer.Theme = EditorInterface.Singleton.GetBaseControl().Theme;
		_fmodBankExplorer.Visible = false;
		AddChild(_fmodBankExplorer);
		AddControlToContainer(CustomControlContainer.Toolbar, _fmodBankExplorerButton);	
	}

	private void OnFmodExplorerButtonPressed()
	{
		_fmodBankExplorer.Visible = !_fmodBankExplorer.Visible;
		if (_fmodBankExplorer.Visible)
		{
			_fmodBankExplorer.RegenerateTree();
			_fmodBankExplorer.PopupCentered();
		}
	}

	public override void _ExitTree()
	{
		RemoveExportPlugin(_fmodExport);
		RemoveEditorUI();
	}

	private void RemoveEditorUI()
	{
		if (_fmodBankExplorerButton != null)
		{
			RemoveControlFromContainer(CustomControlContainer.Toolbar, _fmodBankExplorerButton);	
		}
		_fmodBankExplorer?.QueueFree();
	}

	public override string _GetPluginName()
	{
		return FmodPluginSettings.PluginName;
	}
}
#endif
