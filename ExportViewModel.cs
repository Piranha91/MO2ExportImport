using DynamicData;
using DynamicData.Binding;
using Microsoft.Win32;
using MO2ExportImport.Views;
using ReactiveUI;
using Splat.ModeDetection;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;


namespace MO2ExportImport.ViewModels
{
    public class ExportViewModel : ReactiveObject
    {
        private ExportView _view;
        private readonly MainViewModel _mainViewModel;
        private readonly SelectionHistoryManager _selectionHistory = new();
        private string _mo2Directory;
        private string _selectedProfile;
        private ObservableCollection<string> _profiles;
        private ObservableCollection<string> _modList;
        private string _filterText;
        public string FilterText
        {
            get => _filterText;
            set => this.RaiseAndSetIfChanged(ref _filterText, value);
        }

        private ObservableCollection<Mod> _filteredModList;
        public ObservableCollection<Mod> FilteredModList
        {
            get => _filteredModList;
            private set => this.RaiseAndSetIfChanged(ref _filteredModList, value);
        }

        private string _exportDestinationFolder;
        public string ExportDestinationFolder
        {
            get => _exportDestinationFolder;
            set
            {
                this.RaiseAndSetIfChanged(ref _exportDestinationFolder, value);
                _mainViewModel.SaveSettings(); // Save settings whenever ExportDestinationFolder changes
            }
        }

        public ObservableCollection<string> Profiles
        {
            get => _profiles;
            set => this.RaiseAndSetIfChanged(ref _profiles, value);
        }

        public string SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedProfile, value);
                LoadModList();
            }
        }

        private bool _ignoreDisabled = true;
        public bool IgnoreDisabled
        {
            get => _ignoreDisabled;
            set
            {
                this.RaiseAndSetIfChanged(ref _ignoreDisabled, value);
                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreDisabled changes
            }
        }

        private bool _ignoreSeparators;
        public bool IgnoreSeparators
        {
            get => _ignoreSeparators;
            set
            {
                this.RaiseAndSetIfChanged(ref _ignoreSeparators, value);
                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        private string _exportButtonLabel;
        public string ExportButtonLabel
        {
            get => _exportButtonLabel;
            set => this.RaiseAndSetIfChanged(ref _exportButtonLabel, value);
        }

        private bool _isLoadingList;
        public bool IsLoadingList
        {
            get => _isLoadingList;
            set
            {
                this.RaiseAndSetIfChanged(ref _isLoadingList, value);
            }
        }

        private bool _isPleaseWaitVisible;
        public bool IsPleaseWaitVisible
        {
            get => _isPleaseWaitVisible;
            set
            {
                this.RaiseAndSetIfChanged(ref _isPleaseWaitVisible, value);
                if (value)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render); // when this value becomes true, render the associated texblock right away. Without this code, rendering lags until time-consuming listbox updates are done.
                }
            }
        }

        private bool _autoCalculateSpace = true;
        public bool AutoCalculateSpace
        {
            get => _autoCalculateSpace;
            set
            {
                this.RaiseAndSetIfChanged(ref _autoCalculateSpace, value);
                _mainViewModel.SaveSettings(); 
            }
        }
        
        private bool _canUndo;
        public bool CanUndo
        {
            get => _canUndo;
            set => this.RaiseAndSetIfChanged(ref _canUndo, value);
        }

        private bool _canRedo;
        public bool CanRedo
        {
            get => _canRedo;
            set => this.RaiseAndSetIfChanged(ref _canRedo, value);
        }

        public ReactiveCommand<Unit, Unit> UndoSelectionCommand { get; }
        public ReactiveCommand<Unit, Unit> RedoSelectionCommand { get; }

        public BulkObservableCollection<Mod> ModList { get; set; } = new BulkObservableCollection<Mod>();

        public ReactiveCommand<Unit, Unit> SelectSourceCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportSelectedCommand { get; }
        public ReactiveCommand<Unit, Unit> SetSelectedAsOverrideCommand { get; }
        public ReactiveCommand<Unit, Unit> UnsetSelectedAsOverrideCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseFolderCommand { get; }
        public ReactiveCommand<Mod, Unit> SelectAllInGroupCommand { get; }
        public ReactiveCommand<Mod, Unit> DeselectAllInGroupCommand { get; }

        public ExportViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;

            IsPleaseWaitVisible = false;

            Profiles = new ObservableCollection<string>();
            ModList = new BulkObservableCollection<Mod>();

            string exePath = Assembly.GetExecutingAssembly()?.Location ?? string.Empty;
            string dirPath = Path.GetDirectoryName(exePath) ?? string.Empty;

            if (dirPath != string.Empty)
            {
                ExportDestinationFolder = Path.Combine(dirPath, "Exports");
                if (!Directory.Exists(ExportDestinationFolder))
                {
                    Directory.CreateDirectory(ExportDestinationFolder);
                }
            }

            UpdateSelectedCount();

            _filteredModList = new ObservableCollection<Mod>(ModList);

            this.WhenAnyValue(x => x.FilterText)
                .Subscribe(_ => ApplyFilter());

            SelectSourceCommand = ReactiveCommand.CreateFromTask(SelectSource);

            // Ensure that the ExportSelectedCommand is only enabled when a folder is selected and mods are selected

            // Set up the canExport observable
            var canExport = this.WhenAnyValue(
                x => x.ExportDestinationFolder)
                .CombineLatest(
                    ModList.ToObservableChangeSet()
                           .AutoRefresh(mod => mod.SelectedInUI)
                           .ToCollection(),
                    (folder, mods) => !string.IsNullOrEmpty(folder) && mods.Any(mod => mod.SelectedInUI)
                );

            SetSelectedAsOverrideCommand = ReactiveCommand.Create(SetSelectedAsOverrideMods);
            UnsetSelectedAsOverrideCommand = ReactiveCommand.Create(UnsetSelectedAsOverrideMods);
            
            ExportSelectedCommand = ReactiveCommand.Create(ExportSelected, canExport);

            BrowseFolderCommand = ReactiveCommand.CreateFromTask(BrowseFolder);
            
            SelectAllInGroupCommand = ReactiveCommand.Create<Mod>(SelectAllInGroup);
            DeselectAllInGroupCommand = ReactiveCommand.Create<Mod>(DeselectAllInGroup);
            
            UndoSelectionCommand = ReactiveCommand.Create(UndoSelection, this.WhenAnyValue(x => x.CanUndo));
            RedoSelectionCommand = ReactiveCommand.Create(RedoSelection, this.WhenAnyValue(x => x.CanRedo));
        }

        private async Task SelectSource()
        {
            var dialog = new OpenFolderDialog();
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                _mo2Directory = dialog.FolderName;
                ValidateMo2Directory();
            }
        }

        private void SetSelectedAsOverrideMods()
        {
            foreach (var mod in ModList.Where(x => x.SelectedInUI))
            {
                mod.OverWriteExistingDuringImport = true;
            }
        }
        
        private void UnsetSelectedAsOverrideMods()
        {
            foreach (var mod in ModList.Where(x => x.SelectedInUI))
            {
                mod.OverWriteExistingDuringImport = false;
            }
        }

        private void ValidateMo2Directory()
        {
            if (Directory.Exists(Path.Combine(_mo2Directory, "mods")) &&
                Directory.Exists(Path.Combine(_mo2Directory, "profiles")))
            {
                LoadProfiles();
            }
            else
            {
                System.Windows.MessageBox.Show("Invalid MO2 directory. Please select a valid directory.");
            }
        }

        private void LoadProfiles()
        {
            Profiles.Clear();
            var profilesDir = Path.Combine(_mo2Directory, "profiles");
            var profiles = Directory.GetDirectories(profilesDir).Select(Path.GetFileName);
            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }

            // Auto-select the profile from ModOrganizer.ini or fall back to first profile
            if (Profiles.Any())
            {
                var selectedProfile = CommonFuncs.GetSelectedProfileFromIni(_mo2Directory);
        
                if (!string.IsNullOrEmpty(selectedProfile) && Profiles.Contains(selectedProfile))
                {
                    SelectedProfile = selectedProfile;
                }
                else
                {
                    // Fall back to first profile
                    SelectedProfile = Profiles.First();
                }
            }
        }

        private void LoadModList()
        {
            if (string.IsNullOrEmpty(_selectedProfile)) return;

            IsLoadingList = true;
            IsPleaseWaitVisible = true;
            System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            ModList.Clear();
            var modlistPath = Path.Combine(_mo2Directory, "profiles", _selectedProfile, "modlist.txt");
            if (File.Exists(modlistPath))
            {
                var mods = File.ReadAllLines(modlistPath)
                    .Where(line => !line.StartsWith("#"))
                    .Select(line => new Mod(line))
                    .Reverse()
                    .ToList();

                ModList.AddRange(mods);
            }

            _filteredModList = new ObservableCollection<Mod>(ModList);

            this.WhenAnyValue(x => x.FilterText)
                .Subscribe(_ => ApplyFilter());

            UpdateSelectedCount();
    
            IsLoadingList = false;
            IsPleaseWaitVisible = false;
    
            // Select all items in the ListBox
            SelectAllItemsInListBox();
        }

        private void ExportSelected()
        {
            var selectedModsToExport = ModList
                .Where(mod => mod.SelectedInUI && 
                    (!IgnoreDisabled || mod.IsEnabled() || mod.SourceListing.IsSeparator) && // treat separators as enabled
                    (!IgnoreSeparators || !mod.SourceListing.IsSeparator))
                .ToList();
            // Create and display the ExportPopupView
            var exportPopupView = new ExportPopupView();
            var exportPopupViewModel = new ExportPopupViewModel(exportPopupView, this, _mo2Directory, selectedModsToExport, ExportDestinationFolder, _selectedProfile, _mainViewModel.ProgramVersion, _autoCalculateSpace);
            exportPopupView.DataContext = exportPopupViewModel;
            exportPopupView.ShowDialog();
        }

        private async Task BrowseFolder()
        {
            var dialog = new OpenFolderDialog();
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                ExportDestinationFolder = dialog.FolderName;
            }
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrEmpty(FilterText))
            {
                // If the filter is empty, show all mods
                FilteredModList = new ObservableCollection<Mod>(ModList);
            }
            else
            {
                // Apply the filter, but maintain the current selections
                var matchedMods = ModList
                    .Where(mod => mod.DisplayName.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                // Keep the previously selected mods in the filtered list
                foreach (var mod in ModList.Where(mod => mod.SelectedInUI && !matchedMods.Contains(mod)))
                {
                    matchedMods.Add(mod);
                }

                FilteredModList = new ObservableCollection<Mod>(matchedMods);
            }
        }

        public void UpdateSelectedCount()
        {
            int selectedCount = FilteredModList?.Where(x => x.SelectedInUI).Count() ?? 0;
            ExportButtonLabel = "Export " + selectedCount.ToString() + " Selected Mod" + (selectedCount != 1 ? "s" : "");
        }
        
        public void OnViewLoaded(ExportView view)
        {
            _view = view;
        }

        private void SelectAllItemsInListBox()
        {
            if (_view != null)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _view.ModsListBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        
        private void SelectAllInGroup(Mod separatorMod)
        {
            if (separatorMod?.SourceListing?.IsSeparator != true)
                return;

            var modsInGroup = GetModsInGroup(separatorMod);
    
            // Update the SelectedInUI property
            foreach (var mod in modsInGroup)
            {
                mod.SelectedInUI = true;
            }
    
            // Update the actual ListBox selection
            if (_view != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var mod in modsInGroup)
                    {
                        if (!_view.ModsListBox.SelectedItems.Contains(mod))
                        {
                            _view.ModsListBox.SelectedItems.Add(mod);
                        }
                    }
                });
            }
    
            UpdateSelectedCount();
        }

        private void DeselectAllInGroup(Mod separatorMod)
        {
            if (separatorMod?.SourceListing?.IsSeparator != true)
                return;

            var modsInGroup = GetModsInGroup(separatorMod);
    
            // Update the SelectedInUI property
            foreach (var mod in modsInGroup)
            {
                mod.SelectedInUI = false;
            }
    
            // Update the actual ListBox selection
            if (_view != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var mod in modsInGroup)
                    {
                        if (_view.ModsListBox.SelectedItems.Contains(mod))
                        {
                            _view.ModsListBox.SelectedItems.Remove(mod);
                        }
                    }
                });
            }
    
            UpdateSelectedCount();
        }

        private List<Mod> GetModsInGroup(Mod separatorMod)
        {
            var modsInGroup = new List<Mod>();
    
            // Use FilteredModList if filtering is active, otherwise use ModList
            var sourceList = string.IsNullOrEmpty(FilterText) ? ModList : FilteredModList;
    
            int separatorIndex = sourceList.IndexOf(separatorMod);
            if (separatorIndex == -1)
                return modsInGroup;

            // Get all mods after this separator until the next separator or end of list
            for (int i = separatorIndex + 1; i < sourceList.Count; i++)
            {
                var currentMod = sourceList[i];
        
                // Stop if we hit another separator
                if (currentMod.SourceListing.IsSeparator)
                    break;
            
                modsInGroup.Add(currentMod);
            }

            return modsInGroup;
        }
        
        public void SaveSelectionState()
        {
            if (!_selectionHistory.IsUndoRedoOperation)
            {
                var selectedMods = ModList.Where(m => m.SelectedInUI).ToList();
                _selectionHistory.SaveState(selectedMods);
                UpdateUndoRedoButtons();
            }
        }

        private void UndoSelection()
        {
            _selectionHistory.SetUndoRedoOperation(true);
    
            var previousState = _selectionHistory.Undo();
    
            if (previousState != null)
            {
                ApplySelectionState(previousState);
            }
    
            _selectionHistory.SetUndoRedoOperation(false);
            UpdateUndoRedoButtons();
        }

        private void RedoSelection()
        {
            _selectionHistory.SetUndoRedoOperation(true);
    
            var nextState = _selectionHistory.Redo();
    
            if (nextState != null)
            {
                ApplySelectionState(nextState);
            }
    
            _selectionHistory.SetUndoRedoOperation(false);
            UpdateUndoRedoButtons();
        }

        private void ApplySelectionState(HashSet<string> selectedModNames)
        {
            if (_view == null)
                return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _view.ModsListBox.SelectedItems.Clear();
        
                foreach (var mod in ModList)
                {
                    bool shouldBeSelected = selectedModNames.Contains(mod.DisplayName);
                    mod.SelectedInUI = shouldBeSelected;
            
                    if (shouldBeSelected)
                    {
                        _view.ModsListBox.SelectedItems.Add(mod);
                    }
                }
            });
    
            UpdateSelectedCount();
        }

        private void UpdateUndoRedoButtons()
        {
            CanUndo = _selectionHistory.CanUndo;
            CanRedo = _selectionHistory.CanRedo;
        }
    }
}
