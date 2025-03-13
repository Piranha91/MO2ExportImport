using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using System.Reactive;
using GongSolutions.Wpf.DragDrop;

namespace MO2ExportImport;

public class ImportSimulatorViewModel : ReactiveObject
{
    private string _currentProfileName = string.Empty;

    public string CurrentProfileName
    {
        get => _currentProfileName;
        set => this.RaiseAndSetIfChanged(ref _currentProfileName, value);
    }
    
    private bool _cancelImport = true;
    public bool CancelImport 
    { 
        get => _cancelImport;
        set => this.RaiseAndSetIfChanged(ref _cancelImport, value);
    }
    
    // Collections for Mods and Plugins.
    public ObservableCollection<ISimulatorNode> ModList { get; set; } = new ObservableCollection<ISimulatorNode>();
    public ObservableCollection<ISimulatorNode> PluginList { get; set; } = new ObservableCollection<ISimulatorNode>();

    // Toggle properties for switching views.
    private bool _isModListSelected = true;
    public bool IsModListSelected
    {
        get => _isModListSelected;
        set 
        {
            this.RaiseAndSetIfChanged(ref _isModListSelected, value);
            if (value)
                IsPluginListSelected = false;
            this.RaisePropertyChanged(nameof(SelectedSimulatorNodes));
        }
    }
    
    private bool _isPluginListSelected;
    public bool IsPluginListSelected
    {
        get => _isPluginListSelected;
        set
        {
            this.RaiseAndSetIfChanged(ref _isPluginListSelected, value);
            if (value)
                IsModListSelected = false;
            this.RaisePropertyChanged(nameof(SelectedSimulatorNodes));
        }
    }
    
    // Computed property: returns the active list.
    public ObservableCollection<ISimulatorNode> SelectedSimulatorNodes =>
        IsModListSelected ? ModList : PluginList;
    
    // The currently selected node (for EventLog display).
    private ISimulatorNode _selectedSimulatorNode;
    public ISimulatorNode SelectedSimulatorNode
    {
        get => _selectedSimulatorNode;
        set => this.RaiseAndSetIfChanged(ref _selectedSimulatorNode, value);
    }
    
    // Custom drop handler.
    public CustomDropHandler DropHandler { get; }
    
    // Commands for the buttons.
    public ReactiveCommand<Unit, Unit> CloseWindowCommand { get; }
    public ReactiveCommand<Unit, Unit> ProceedCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    
    // Reference to the window.
    private ImportSimulatorWindow _window { get; }
    
    public ImportSimulatorViewModel()
    {
        _window = new ImportSimulatorWindow();
        
        CloseWindowCommand = ReactiveCommand.Create(() => _window.Close());
        ProceedCommand = ReactiveCommand.Create(Proceed);
        CancelCommand = ReactiveCommand.Create(Cancel);

        DropHandler = new CustomDropHandler(ModList, PluginList);
    }
    
    public void ShowWindow()
    {
        _window.DataContext = this;
        _window.ShowDialog();
    }
    
    public void Proceed()
    {
        CancelImport = false;
        _window.Close();
    }
    public void Cancel()
    {
        CancelImport = true;
        _window.Close();
    }
    
    // Logs a mod event by locating or creating a new ModSimulatorNode.
    public void LogModEvent(ModListing modListing, string modEvent)
    {
        var modNode = ModList.FirstOrDefault(x => x.Label == modListing.Name)
            ?? new ModSimulatorNode(modListing, ModList);
        modNode.EventLog.Add(modEvent);
    }
    
    // Logs a plugin event by locating or creating a new PluginSimulatorNode.
    public void LogPluginEvent(PluginListing pluginListing, string modEvent)
    {
        var pluginNode = PluginList.FirstOrDefault(x => x.Label == pluginListing.Name)
            ?? new PluginSimulatorNode(pluginListing, PluginList, isSectionHeader: false);
        pluginNode.EventLog.Add(modEvent);
    }
    
    /// <summary>
    /// Rearranges the ModList so that all ModSimulatorNodes following a header (IsSectionHeader == true)
    /// are moved into that header's Children collection until the next header is encountered.
    /// Nodes that appear before the first header remain at top level.
    /// </summary>
    public void ArrangeModSeparators()
    {
        var newTopLevel = new List<ISimulatorNode>();
        ISimulatorNode currentHeader = null;
        
        // Create a copy since we are modifying the collection.
        foreach (var node in ModList.ToList())
        {
            if (node.IsSectionHeader)
            {
                currentHeader = node;
                node.Children.Clear();
                newTopLevel.Add(node);
            }
            else
            {
                if (currentHeader != null)
                {
                    currentHeader.Children.Add(node);
                }
                else
                {
                    newTopLevel.Add(node);
                }
            }
        }
        
        ModList.Clear();
        foreach (var node in newTopLevel)
        {
            ModList.Add(node);
        }
    }
    
    /// <summary>
    /// Rearranges the PluginList based on each PluginSimulatorNode's SourceListing.PluginGroup.
    /// For each node:
    ///   - If PluginGroup is null, the node remains at the top level.
    ///   - If not null, check if the immediate preceding top-level node is a header with that group name.
    ///       - If yes, move the node into that header's Children.
    ///       - If no, create a new header node with Label equal to the PluginGroup and add the node into its Children.
    /// </summary>
    public void ArrangePluginSeparators()
    {
        var newTopLevel = new List<ISimulatorNode>();
        
        // Iterate over a copy of PluginList.
        foreach (var node in PluginList.ToList())
        {
            if (node is PluginSimulatorNode pluginNode)
            {
                string? group = pluginNode.SourceListing.PluginGroup;
                if (string.IsNullOrEmpty(group))
                {
                    // No group defined; leave at top level.
                    newTopLevel.Add(pluginNode);
                }
                else
                {
                    // Check if the immediate preceding top-level node is a header with a matching label.
                    if (newTopLevel.Any())
                    {
                        var lastTop = newTopLevel.Last();
                        if (lastTop.IsSectionHeader && lastTop.Label == group)
                        {
                            lastTop.Children.Add(pluginNode);
                        }
                        else
                        {
                            // Create a new header node.
                            var headerListing = new PluginListing();
                            headerListing.Name = group;
                            headerListing.PluginGroup = group;
                            var headerNode = new PluginSimulatorNode(headerListing, PluginList, isSectionHeader: true);
                            newTopLevel.Add(headerNode);
                            headerNode.Children.Add(pluginNode);
                        }
                    }
                    else
                    {
                        // No top-level nodes yet; create a header.
                        var headerListing = new PluginListing();
                        headerListing.Name = group;
                        headerListing.PluginGroup = group;
                        var headerNode = new PluginSimulatorNode(headerListing, PluginList, isSectionHeader: true);
                        newTopLevel.Add(headerNode);
                        headerNode.Children.Add(pluginNode);
                    }
                }
            }
        }
        
        PluginList.Clear();
        foreach (var node in newTopLevel)
        {
            PluginList.Add(node);
        }
    }
    
    /// <summary>
    /// Rearranges the ModList and PluginList to match the order of the provided source lists,
    /// then arranges the mod and plugin separators.
    /// </summary>
    public void Initialize(IEnumerable<PluginListing> sourceLoadOrder, IEnumerable<ModListing> sourceModList, bool addMissingItems, string currentProfileName)
    {
        CurrentProfileName = currentProfileName;
        SortEntries(sourceLoadOrder, sourceModList, addMissingItems);
        ArrangeModSeparators();
        ArrangePluginSeparators();
    }
    
    /// <summary>
    /// Rearranges the ModList and PluginList to match the order of the provided source lists.
    /// For each listing, if a corresponding SimulatorNode exists (via SourceListing.Equals(...)),
    /// it is placed into the new ordering. If not found and addMissing is true, a new SimulatorNode is created.
    /// Additionally, when a new node is created, a message is added to its EventLog.
    /// </summary>
    public void SortEntries(IEnumerable<PluginListing> sourceLoadOrder, IEnumerable<ModListing> sourceModList, bool addMissing)
    {
        // Reorder the ModList.
        var newModNodes = new List<ISimulatorNode>();
        foreach (var modListing in sourceModList)
        {
            var node = ModList.OfType<ModSimulatorNode>().FirstOrDefault(n => n.SourceListing.Equals(modListing));
            if (node == null && addMissing)
            {
                node = new ModSimulatorNode(modListing, ModList);
                node.EventLog.Add("Unmodified. This item is from the Import Destination");
            }
            if (node != null)
            {
                newModNodes.Add(node);
            }
        }
        
        // Reorder the PluginList.
        var newPluginNodes = new List<ISimulatorNode>();
        foreach (var pluginListing in sourceLoadOrder)
        {
            var node = PluginList.OfType<PluginSimulatorNode>().FirstOrDefault(n => n.SourceListing.Equals(pluginListing));
            if (node == null && addMissing)
            {
                node = new PluginSimulatorNode(pluginListing, PluginList, isSectionHeader: false);
                node.EventLog.Add("Unmodified. This item is from the Import Destination");
            }
            if (node != null)
            {
                newPluginNodes.Add(node);
            }
        }
        
        ModList.Clear();
        foreach (var node in newModNodes)
        {
            ModList.Add(node);
        }
        
        PluginList.Clear();
        foreach (var node in newPluginNodes)
        {
            PluginList.Add(node);
        }
    }

    public IEnumerable<IListing> GetModListings()
    {
        var modNodes = ModList.Cast<ModSimulatorNode>().ToList();
        List<ModListing> modListings = new List<ModListing>();

        foreach (var modNode in modNodes)
        {
            modListings.Add(modNode.SourceListing); // add the separator
            foreach (var childNode in modNode.Children.Cast<ModSimulatorNode>().ToList())
            {
                modListings.Add(childNode.SourceListing); // add the mod within the separator
            }
        }
        
        return modListings;
    }

    public IEnumerable<IListing> GetPluginListings()
    {
        var pluginNodes = PluginList.Cast<PluginSimulatorNode>().ToList();
        List<PluginListing> pluginListings = new List<PluginListing>();
        foreach (var pluginNode in pluginNodes)
        {
            if (pluginNode.Children.Any())
            {
                // this is a "fake" plugin node acting as a separator
                foreach (var childNode in pluginNode.Children.Cast<PluginSimulatorNode>().ToList())
                {
                    childNode.SourceListing.PluginGroup = pluginNode.Label; // set explicitly in case the plugin was moved to a different separator
                    pluginListings.Add(childNode.SourceListing); // add the plugin within the separator
                }
            }
            else if (!pluginNode.IsSectionHeader)
            {
                // this is a "real" plugin node that's outside of a separator. Add it directly.
                pluginNode.SourceListing.PluginGroup = string.Empty; // set explicitly in case the plugin was moved outside of a separator
                pluginListings.Add(pluginNode.SourceListing);
            }
        }

        return pluginListings;
    }
}

// The common interface now includes an IsSectionHeader property.
public interface ISimulatorNode
{
    ObservableCollection<ISimulatorNode> Children { get; }
    string Label { get; set; }
    ObservableCollection<string> EventLog { get; set; }
    bool IsSectionHeader { get; set; }
}

// ModSimulatorNode now accepts a ModListing.
// Its IsSectionHeader property is set based on SourceListing.IsSeparator.
public class ModSimulatorNode : ISimulatorNode
{
    public ObservableCollection<ISimulatorNode> Children { get; } = new ObservableCollection<ISimulatorNode>();
    public string Label { get; set; }
    public ObservableCollection<string> EventLog { get; set; } = new ObservableCollection<string>();
    public bool IsSectionHeader { get; set; }
    public ObservableCollection<ISimulatorNode> ParentCollection { get; set; }
    
    public ModListing SourceListing { get; set; }
    
    // Constructor accepts a ModListing, sets Label and IsSectionHeader accordingly.
    public ModSimulatorNode(ModListing sourceListing, ObservableCollection<ISimulatorNode> parentCollection)
    {
        SourceListing = sourceListing;
        Label = sourceListing.Name;
        if (SourceListing.IsSeparator)
        {
            // Remove the separator suffix and trim the result.
            Label = StringExtensions.RemoveAtEnd(Label, ModListing._separatorSuffix).Trim();
        }
        ParentCollection = parentCollection;
        IsSectionHeader = sourceListing.IsSeparator;
        ParentCollection.Add(this);
    }
}

// PluginSimulatorNode remains unchanged except for supporting grouping in ArrangePluginSeparators.
public class PluginSimulatorNode : ISimulatorNode
{
    public ObservableCollection<ISimulatorNode> Children { get; } = new ObservableCollection<ISimulatorNode>();
    public string Label { get; set; }
    public ObservableCollection<string> EventLog { get; set; } = new ObservableCollection<string>();
    public bool IsSectionHeader { get; set; }
    public ObservableCollection<ISimulatorNode> ParentCollection { get; set; }

    public PluginListing SourceListing { get; set; }

    // Constructor accepts a PluginListing.
    public PluginSimulatorNode(PluginListing sourceListing, ObservableCollection<ISimulatorNode> parentCollection,
        bool isSectionHeader = false)
    {
        SourceListing = sourceListing;
        Label = sourceListing.Name;
        ParentCollection = parentCollection;
        IsSectionHeader = isSectionHeader;
        ParentCollection.Add(this);
    }
}

public class CustomDropHandler : GongSolutions.Wpf.DragDrop.DefaultDropHandler
{
    public ObservableCollection<ISimulatorNode> TopLevelModList { get; set; }
    public ObservableCollection<ISimulatorNode> TopLevelPluginList { get; set; }

    public CustomDropHandler(ObservableCollection<ISimulatorNode> topLevelModList,
        ObservableCollection<ISimulatorNode> topLevelPluginList)
    {
        TopLevelModList = topLevelModList;
        TopLevelPluginList = topLevelPluginList;
    }

    public override void DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.Data is ISimulatorNode draggedNode)
        {
            // MOD DRAGGING
            if (draggedNode is ModSimulatorNode)
            {
                if (draggedNode.IsSectionHeader)
                {
                    // The dragged item is a section header
                    if (dropInfo.InsertPosition == RelativeInsertPosition.BeforeTargetItem || dropInfo.InsertPosition == RelativeInsertPosition.AfterTargetItem)
                    {
                        // The pointer is between items (reordering)
                        if (ReferenceEquals(dropInfo.TargetCollection, TopLevelModList))
                        {
                            // We're in the top-level ModList.
                        }
                        else /* dropInfo.TargetCollection is one of the Children collections */
                        {
                            // We're in a nested collection.
                            dropInfo.Effects = System.Windows.DragDropEffects.None;
                            return;
                        }
                    }
                    else if (dropInfo.TargetItem is ModSimulatorNode targetNode)
                    {
                        // The pointer is over an item (dropping onto an item)
                        dropInfo.Effects = System.Windows.DragDropEffects.None;
                        return;
                    }
                }
                else
                {
                    // The dragged item is a mod
                    if (dropInfo.InsertPosition == RelativeInsertPosition.BeforeTargetItem || dropInfo.InsertPosition == RelativeInsertPosition.AfterTargetItem)
                    {
                        // The pointer is between items (reordering)
                        if (ReferenceEquals(dropInfo.TargetCollection, TopLevelModList))
                        {
                            // We're in the top-level ModList.
                            dropInfo.Effects = System.Windows.DragDropEffects.None;
                            return;
                        }
                        else /* dropInfo.TargetCollection is one of the Children collections */
                        {
                            // We're in a nested collection.
                        }
                    }
                    else if (dropInfo.TargetItem is ModSimulatorNode targetNode)
                    {
                        // The pointer is over an item (dropping onto an item)
                        if (!targetNode.IsSectionHeader)
                        {
                            dropInfo.Effects = System.Windows.DragDropEffects.None;
                            return;
                        }
                    }
                }
            }
            // PLUGIN DRAGGING
            else if (draggedNode is PluginSimulatorNode)
            {
                if (draggedNode.IsSectionHeader)
                {
                    // The dragged item is a section header
                    if (dropInfo.InsertPosition == RelativeInsertPosition.BeforeTargetItem || dropInfo.InsertPosition == RelativeInsertPosition.AfterTargetItem)
                    {
                        // The pointer is between items (reordering)
                        if (ReferenceEquals(dropInfo.TargetCollection, TopLevelPluginList))
                        {
                            // We're in the top-level ModList.
                        }
                        else /* dropInfo.TargetCollection is one of the Children collections */
                        {
                            // We're in a nested collection.
                            dropInfo.Effects = System.Windows.DragDropEffects.None;
                            return;
                        }
                    }
                    else if (dropInfo.TargetItem is PluginSimulatorNode targetNode)
                    {
                        // The pointer is over an item (dropping onto an item)
                        dropInfo.Effects = System.Windows.DragDropEffects.None;
                        return;
                    }
                }
                else
                {
                    // The dragged item is a plugin
                    if (dropInfo.InsertPosition == RelativeInsertPosition.BeforeTargetItem || dropInfo.InsertPosition == RelativeInsertPosition.AfterTargetItem)
                    {
                        // The pointer is between items (reordering)
                        if (ReferenceEquals(dropInfo.TargetCollection, TopLevelPluginList))
                        {
                            // We're in the top-level ModList.
                            // This is allowed for plugin nodes, but not for mod nodes
                        }
                        else /* dropInfo.TargetCollection is one of the Children collections */
                        {
                            // We're in a nested collection.
                        }
                    }
                    else if (dropInfo.TargetItem is PluginSimulatorNode targetNode)
                    {
                        // The pointer is over an item (dropping onto an item)
                        if (!targetNode.IsSectionHeader)
                        {
                            dropInfo.Effects = System.Windows.DragDropEffects.None;
                            return;
                        }
                    }
                }
            }

            base.DragOver(dropInfo);
        }
    }
    
    public override void Drop(IDropInfo dropInfo)
    {
        base.Drop(dropInfo);
    }
}

