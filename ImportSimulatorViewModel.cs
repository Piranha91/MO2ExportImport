using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace MO2ExportImport;

public class ImportSimulatorViewModel : ReactiveObject
{
    public bool CancelImport { get; set; } = true;
    public ObservableCollection<ModSimulatorNode> Mods { get; set; } = new ObservableCollection<ModSimulatorNode>();
    public ObservableCollection<PluginSimulatorNode> Plugins { get; set; } = new ObservableCollection<PluginSimulatorNode>();
    
    private ImportSimulatorWindow _window { get; }
    
    public ReactiveCommand<Unit, Unit> CloseWindowCommand { get; }
    public ReactiveCommand<Unit, Unit> ProceedCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ImportSimulatorViewModel()
    {
        _window = new ImportSimulatorWindow();
        
        CloseWindowCommand = ReactiveCommand.Create(() => _window.Close());
        ProceedCommand = ReactiveCommand.Create(Proceed);
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public void ShowWindow()
    {
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
    
    public void LogModEvent(ModListing modListing, string modEvent)
    {
        var modNode = Mods.FirstOrDefault(x => x.Label == modListing.Name) ?? new ModSimulatorNode(modListing.Name, Mods);
        
        modNode.EventLog.Add(modEvent);
    }
    
    public void LogPluginEvent(PluginListing pluginListing, string modEvent)
    {
        var pluginNode = Plugins.FirstOrDefault(x => x.Label == pluginListing.Name) ?? new PluginSimulatorNode(pluginListing.Name, Plugins);
        
        pluginNode.EventLog.Add(modEvent);
    }
}

public interface ISimulatorNode
{
    public ObservableCollection<ISimulatorNode> Children { get; }
    public string Label { get; set; }
    public ObservableCollection<string> EventLog {get;set;}
}

public class ModSimulatorNode : ISimulatorNode
{
    public ObservableCollection<ISimulatorNode> Children { get; }
    public string Label { get; set; }
    public ObservableCollection<string> EventLog { get; set; } = new();
    public ObservableCollection<ModSimulatorNode> ParentCollection { get; set; }

    public ModSimulatorNode(string label, ObservableCollection<ModSimulatorNode> parentCollection)
    {
        Label = label;
        ParentCollection = parentCollection;
        ParentCollection.Add(this);
    }
}

public class PluginSimulatorNode : ISimulatorNode
{
    public ObservableCollection<ISimulatorNode> Children { get; }
    public string Label { get; set; }
    public ObservableCollection<string> EventLog { get; set; } = new();
    public ObservableCollection<PluginSimulatorNode> ParentCollection { get; set; }

    public PluginSimulatorNode(string label, ObservableCollection<PluginSimulatorNode> parentCollection)
    {
        Label = label;
        ParentCollection = parentCollection;
        ParentCollection.Add(this);
    }
}