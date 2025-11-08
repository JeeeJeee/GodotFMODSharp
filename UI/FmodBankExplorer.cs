#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

using GodotFMODSharp;

[Flags]
public enum FmodExplorerDisplayFlags
{
    Banks,
    Busses,
    VCAs,
    Events
}

[Tool]
public partial class FmodBankExplorer : Window
{
    [Export] private Button _closeButton;
    [Export] private Button _refreshBanksButton;
    [Export] private Tree _tree;
    [Export] private Label _guidLabel;
    [Export] private Label _pathLabel;
    [Export] private PanelContainer _pathBg;
    [Export] private Button _copyGuidButton;
    [Export] private Button _copyPathButton;
    [Export] private PanelContainer _eventPlayControls;
    [Export] private Button _playButton;
    [Export] private Button _stopButton;

    private static Texture2D _fmodIcon = GD.Load<Texture2D>("res://addons/GodotFMODSharp/icons/fmod_icon.svg");
    private static Texture2D _vcaIcon = GD.Load<Texture2D>("res://addons/GodotFMODSharp/icons/vca_icon.svg");
    private static Texture2D _bankIcon = GD.Load<Texture2D>("res://addons/GodotFMODSharp/icons/bank_icon.svg");
    private static Texture2D _eventIcon = GD.Load<Texture2D>("res://addons/GodotFMODSharp/icons/event_icon.svg");
    private static Texture2D _busIcon = GD.Load<Texture2D>("res://addons/GodotFMODSharp/icons/bus_icon.svg");
    private static Texture2D _snapshotIcon = GD.Load<Texture2D>("res://addons/GodotFMODSharp/icons/snapshot_icon.svg");

    private EventDescription _currentlySelectedEvent;
    private EventInstance _currentEventInstance;
    
    public override void _Ready()
    {
        CloseRequested += OnWindowClose;
        _closeButton.Pressed += OnWindowClose;
        _refreshBanksButton.Pressed += OnRefreshBanksButtonPressed;
        _tree.ItemSelected += OnTreeItemSelected;
        
        Texture2D copyIcon = EditorInterface.Singleton.GetEditorTheme().GetIcon("ActionCopy", "EditorIcons");
        _copyPathButton.Icon = copyIcon;
        _copyPathButton.Pressed += () =>
        {
            DisplayServer.ClipboardSet(_pathLabel.Text);
        };
        _copyGuidButton.Icon = copyIcon;
        _copyGuidButton.Pressed += () =>
        {
            DisplayServer.ClipboardSet(_guidLabel.Text);
        };

        _playButton.Pressed += () =>
        {
            if (!_currentlySelectedEvent.IsValid())
            {
                GD.PrintErr("EventDescription is invalid");
                return;
            }
            if(_currentEventInstance != null) { _currentEventInstance.Stop(); }
            _currentEventInstance = _currentlySelectedEvent.CreateInstance();
            _currentEventInstance.Start();
        };
        _stopButton.Pressed += () =>
        {
            if(_currentEventInstance == null) { return; }
            _currentEventInstance.Stop();
            _currentEventInstance.Release();
        };
    }

    private void OnTreeItemSelected()
    {
        string metaDataFmodPath = (string)_tree.GetSelected().GetMetadata(0);
        if (metaDataFmodPath.Contains("bus:") || string.IsNullOrEmpty(metaDataFmodPath)|| !FmodServer.FmodStudioSystem.GetEventByPath(metaDataFmodPath, out EventDescription eventDescription))
        {
            _pathBg.Visible = false;
            _eventPlayControls.Visible = false;
            _currentlySelectedEvent = null;
            return;
        }
        _pathBg.Visible = true;
        _guidLabel.Text = eventDescription.GetGuid().ToStringExact();
        _pathLabel.Text = metaDataFmodPath;
        _copyGuidButton.Visible = true;
        _copyPathButton.Visible = true;
        _eventPlayControls.Visible = true;
        _currentlySelectedEvent = eventDescription;
    }

    private List<T> GetSortedByPath<T>(T[] items) where T : IFmodPath
    {
        return items.OrderBy(x => x.GetPath()).ToList();
    }
    
    public void RegenerateTree()
    {
        _tree.Clear();
        
        TreeItem rootItem = _tree.CreateItem();
        rootItem.SetText(0, "Fmod objects");
        rootItem.SetIcon(0, _fmodIcon);
        
        foreach (Bank bank in FmodServer.FmodStudioSystem.GetBanks())
        {
            TreeItem bankItem = _tree.CreateItem(rootItem);
            bankItem.SetText(0, ProjectSettings.LocalizePath(bank.GetPath()));
            bankItem.SetIcon(0, _bankIcon);
            
            var busItem = _tree.CreateItem(bankItem);
            busItem.SetText(0, "Busses");
            busItem.SetIcon(0, _busIcon);
            AddItemsToTree(busItem, GetSortedByPath(bank.GetBusses()));
            
            var vcaItem = _tree.CreateItem(bankItem);
            vcaItem.SetText(0, "VCAs");
            vcaItem.SetIcon(0, _vcaIcon);
            AddItemsToTree(vcaItem, GetSortedByPath(bank.GetVCAs()));
            
            var eventItem = _tree.CreateItem(bankItem);
            eventItem.SetText(0, "Events");
            eventItem.SetIcon(0, _eventIcon);
            AddItemsToTree(eventItem, GetSortedByPath(bank.GetEvents()));
        }
    }
    
    private void AddItemsToTree<T>(TreeItem topMostItem, List<T> items)
    {
        foreach(T inObject in items)
        {
            string fmodPath = ((IFmodPath)inObject).GetPath();
            string[] pathParts = fmodPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToArray();
            TreeItem currentItem = topMostItem;
            for (int i = 0; i < pathParts.Length; i++)
            {
                string pathPart = pathParts[i] == "bus:" ? "Master" : pathParts[i];
                if (pathPart == "event:") continue;
                
                // find existing nodes
                TreeItem foundItem = null;
                foreach (TreeItem item in topMostItem.GetChildren())
                {
                    if (item.GetText(0) == pathPart)
                    {
                        foundItem = item;
                        break;
                    }
                }

                if (foundItem == null)
                {
                    foundItem = currentItem.CreateChild();
                    
                    foundItem.SetText(0, pathPart);
                    foundItem.SetIcon(0, _GetIconForFmodPath(fmodPath));
                    foundItem.SetMetadata(0, fmodPath);
                }

                currentItem = foundItem;
            }
        }
    }

    /// <summary>
    /// TreeItems, being godot native can't hold C# types inside their metadata.
    /// So we clone the displayed tree here so we can query any information we want from the results.
    /// </summary>
    private void ConstructInternalTree()
    {
        
    }
    
    private Texture2D _GetIconForFmodPath(string path)
    {
        Texture2D icon = null;
        if (path.EndsWith(".snapshot")) { return _snapshotIcon; }
        if (path.EndsWith(".vca")) { return _vcaIcon; }
        if (path.EndsWith(".bank")) { return _bankIcon; }
        return icon;
    }
    
    private void OnRefreshBanksButtonPressed()
    {
        // @TODO: Reload the banks here then regenerate the UI tree
        RegenerateTree();
    }

    private void OnWindowClose()
    {
        _currentEventInstance?.Stop();
        _currentEventInstance?.Release();
        Visible = false;
    }
}
#endif