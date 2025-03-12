using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using System.Reactive;
using GongSolutions.Wpf.DragDrop;

namespace MO2ExportImport
{
    public class ImportSimulatorViewModel : ReactiveObject
    {
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
        public IDropTarget DropHandler { get; } = new CustomDropHandler();
        
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
                    // This node becomes the current header.
                    currentHeader = node;
                    // Clear existing children for re-arrangement.
                    node.Children.Clear();
                    newTopLevel.Add(node);
                }
                else
                {
                    if (currentHeader != null)
                    {
                        // Move node into current header's Children.
                        currentHeader.Children.Add(node);
                    }
                    else
                    {
                        // No header encountered yet; keep at top level.
                        newTopLevel.Add(node);
                    }
                }
            }
            
            // Replace ModList with the newly arranged top-level nodes.
            ModList.Clear();
            foreach (var node in newTopLevel)
            {
                ModList.Add(node);
            }
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
                // Try to locate an existing ModSimulatorNode that matches this listing.
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
            
            // Update the observable collections.
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
        
        /// <summary>
        /// Initializes the Simulator by sorting entries and arranging mod separators.
        /// </summary>
        /// <param name="sourceLoadOrder">Desired order for plugin listings.</param>
        /// <param name="sourceModList">Desired order for mod listings.</param>
        /// <param name="addMissingItems">If true, missing items are created.</param>
        public void Initialize(IEnumerable<PluginListing> sourceLoadOrder, IEnumerable<ModListing> sourceModList, bool addMissingItems)
        {
            SortEntries(sourceLoadOrder, sourceModList, addMissingItems);
            ArrangeModSeparators();
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
        
        // New property for the source listing.
        public ModListing SourceListing { get; set; }
        
        // Constructor accepts a ModListing and sets IsSectionHeader based on SourceListing.IsSeparator.
        public ModSimulatorNode(ModListing sourceListing, ObservableCollection<ISimulatorNode> parentCollection)
        {
            SourceListing = sourceListing;
            Label = sourceListing.Name;
            if (SourceListing.IsSeparator)
            {
                Label = StringExtensions.RemoveAtEnd(Label, ModListing._separatorSuffix).Trim();
            }
            ParentCollection = parentCollection;
            IsSectionHeader = sourceListing.IsSeparator; // automatically set based on the source listing.
            ParentCollection.Add(this);
        }
    }
    
    // PluginSimulatorNode remains unchanged.
    public class PluginSimulatorNode : ISimulatorNode
    {
        public ObservableCollection<ISimulatorNode> Children { get; } = new ObservableCollection<ISimulatorNode>();
        public string Label { get; set; }
        public ObservableCollection<string> EventLog { get; set; } = new ObservableCollection<string>();
        public bool IsSectionHeader { get; set; }
        public ObservableCollection<ISimulatorNode> ParentCollection { get; set; }
        
        // New property for the source listing.
        public PluginListing SourceListing { get; set; }
        
        // Constructor accepts a PluginListing.
        public PluginSimulatorNode(PluginListing sourceListing, ObservableCollection<ISimulatorNode> parentCollection, bool isSectionHeader = false)
        {
            SourceListing = sourceListing;
            Label = sourceListing.Name;
            ParentCollection = parentCollection;
            IsSectionHeader = isSectionHeader;
            ParentCollection.Add(this);
        }
    }
    
    // Custom drop handler that allows drops only on nodes where IsSectionHeader is true.
    public class CustomDropHandler : GongSolutions.Wpf.DragDrop.DefaultDropHandler
    {
        public override void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ISimulatorNode draggedNode)
            {
                // If a section header is being dragged,
                // allow drop only if the target collection is the same as the source collection.
                if (draggedNode.IsSectionHeader)
                {
                    if (!object.ReferenceEquals(dropInfo.TargetCollection, dropInfo.DragInfo.SourceCollection))
                    {
                        dropInfo.Effects = System.Windows.DragDropEffects.None;
                        return;
                    }
                }
        
                // For all nodes: if a target item exists, it must be a section header.
                if (dropInfo.TargetItem != null)
                {
                    if (dropInfo.TargetItem is ISimulatorNode targetNode)
                    {
                        if (!targetNode.IsSectionHeader)
                        {
                            dropInfo.Effects = System.Windows.DragDropEffects.None;
                            return;
                        }
                    }
                }
        
                // If no valid insert position is provided, disallow the drop.
                if (dropInfo.InsertIndex < 0)
                {
                    dropInfo.Effects = System.Windows.DragDropEffects.None;
                    return;
                }
            }
            base.DragOver(dropInfo);
        }
        
        public override void Drop(IDropInfo dropInfo)
        {
            base.Drop(dropInfo);
        }
    }
}
