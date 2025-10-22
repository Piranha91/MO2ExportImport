using DynamicData.Binding;
using Microsoft.Win32;
using MO2ExportImport.Models;
using MO2ExportImport.Views;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace MO2ExportImport.ViewModels
{
    public class ImportViewModel : ReactiveObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly SelectionHistoryManager _selectionHistory = new();
        private string _mo2Directory;
        private string _importSourceFolder;
        private string _selectedProfile;
        private string _modsRootPath;
        private bool _isImportEnabled;
        private bool _modsLoaded;
        private StreamWriter _logWriter;
        private string _importButtonLabel;
        private List<Mod> _removedMods_Matching_Existing = new();
        public string StartingImportSource { get; set; } = string.Empty;
        private ImportView _view;

        public string Mo2Directory
        {
            get => _mo2Directory;
            set
            {
                this.RaiseAndSetIfChanged(ref _mo2Directory, value);
                _mainViewModel.SaveSettings(); // Assuming there's a Save method in Settings to persist the changes
                LoadProfiles(); // Load profiles whenever Mo2Directory is set
                UpdateImportEnabled();
            }
        }

        public string ImportSourceFolder
        {
            get => _importSourceFolder;
            set
            {
                this.RaiseAndSetIfChanged(ref _importSourceFolder, value);
                UpdateImportEnabled();
            }
        }

        private bool _isSourceMo2Directory;

        public bool IsSourceMo2Directory
        {
            get => _isSourceMo2Directory;
            set => this.RaiseAndSetIfChanged(ref _isSourceMo2Directory, value);
        }

        public ObservableCollection<string> SourceProfiles { get; } = new();

        private string _selectedSourceProfile;

        public string SelectedSourceProfile
        {
            get => _selectedSourceProfile;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSourceProfile, value);
                if (value != null)
                {
                    LoadModsFromSourceProfile();
                }
            }
        }

        public string SelectedProfile
        {
            get => _selectedProfile;
            set => this.RaiseAndSetIfChanged(ref _selectedProfile, value);
        }

        public bool IsImportEnabled
        {
            get => _isImportEnabled;
            set => this.RaiseAndSetIfChanged(ref _isImportEnabled, value);
        }

        public bool ModsLoaded
        {
            get => _modsLoaded;
            set => this.RaiseAndSetIfChanged(ref _modsLoaded, value);
        }

        private ImportMode _selectedImportMode = ImportMode.Spliced;

        public ObservableCollection<ImportMode> ImportModes { get; } = new ObservableCollection<ImportMode>
        {
            ImportMode.End,
            ImportMode.Spliced
        };

        public ImportMode SelectedImportMode
        {
            get => _selectedImportMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedImportMode, value);
                _mainViewModel.SaveSettings(); // Save the selected import mode to settings
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

        private bool _addNoDeleteFlags;

        public bool AddNoDeleteFlags
        {
            get => _addNoDeleteFlags;
            set
            {
                this.RaiseAndSetIfChanged(ref _addNoDeleteFlags, value);
                if (value)
                {
                    StripNoDelete = false;
                }

                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        private bool _stripNoDelete;

        public bool StripNoDelete
        {
            get => _stripNoDelete;
            set
            {
                this.RaiseAndSetIfChanged(ref _stripNoDelete, value);
                if (value)
                {
                    AddNoDeleteFlags = false;
                }

                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        private bool _matchModActivationState;

        public bool MatchModActivationState
        {
            get => _matchModActivationState;
            set
            {
                this.RaiseAndSetIfChanged(ref _matchModActivationState, value);
                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        private bool _matchPluginActivationState;

        public bool MatchPluginActivationState
        {
            get => _matchPluginActivationState;
            set
            {
                this.RaiseAndSetIfChanged(ref _matchPluginActivationState, value);
                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        private bool _skipExisting = false;

        public bool SkipExisting
        {
            get => _skipExisting;
            set
            {
                this.RaiseAndSetIfChanged(ref _skipExisting, value);
                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        private bool _ignoreMatchedModsForOrdering = true;

        public bool IgnoreMatchedModsForOrdering
        {
            get => _ignoreMatchedModsForOrdering;
            set
            {
                this.RaiseAndSetIfChanged(ref _ignoreMatchedModsForOrdering, value);
                _mainViewModel.SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        private Visibility _showFilteringNotification = Visibility.Hidden;

        public Visibility ShowFilteringNotification
        {
            get => _showFilteringNotification;
            set
            {
                this.RaiseAndSetIfChanged(ref _showFilteringNotification, value);
                System.Windows.Application.Current.Dispatcher.Invoke(() => { },
                    System.Windows.Threading.DispatcherPriority
                        .Render); // when this value becomes true, render the associated texblock right away. Without this code, rendering lags until time-consuming listbox updates are done.
            }
        }

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

        public string ImportButtonLabel
        {
            get => _importButtonLabel;
            set => this.RaiseAndSetIfChanged(ref _importButtonLabel, value);
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
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { },
                        System.Windows.Threading.DispatcherPriority
                            .Render); // when this value becomes true, render the associated texblock right away. Without this code, rendering lags until time-consuming listbox updates are done.
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

        private string _importPrefix;

        public string ImportPrefix
        {
            get => _importPrefix;
            set
            {
                this.RaiseAndSetIfChanged(ref _importPrefix, value);
                _mainViewModel.SaveSettings();
            }
        }

        private bool _interpolateMissingPluginGroups;

        public bool InterpolateMissingPluginGroups
        {
            get => _interpolateMissingPluginGroups;
            set
            {
                this.RaiseAndSetIfChanged(ref _interpolateMissingPluginGroups, value);
                _mainViewModel.SaveSettings();
            }
        }
        
        private bool _transferDownloads;
        public bool TransferDownloads
        {
            get => _transferDownloads;
            set
            {
                this.RaiseAndSetIfChanged(ref _transferDownloads, value);
                _mainViewModel.SaveSettings();
            }
        }

        public enum MultiPluginMode
        {
            All,
            WinnerOnly
        }

        public enum PluginSelectionMode
        {
            EnabledOnly,
            All
        }

        public ObservableCollection<MultiPluginMode> MultiPluginModes { get; } =
            new ObservableCollection<MultiPluginMode>
            {
                MultiPluginMode.All,
                MultiPluginMode.WinnerOnly
            };

        public ObservableCollection<PluginSelectionMode> PluginSelectionModes { get; } =
            new ObservableCollection<PluginSelectionMode>
            {
                PluginSelectionMode.EnabledOnly,
                PluginSelectionMode.All
            };

        private MultiPluginMode _selectedMultiPluginMode = MultiPluginMode.WinnerOnly;

        public MultiPluginMode SelectedMultiPluginMode
        {
            get => _selectedMultiPluginMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedMultiPluginMode, value);
                _mainViewModel.SaveSettings();
            }
        }

        private PluginSelectionMode _selectedPluginSelectionMode = PluginSelectionMode.EnabledOnly;

        public PluginSelectionMode SelectedPluginSelectionMode
        {
            get => _selectedPluginSelectionMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedPluginSelectionMode, value);
                _mainViewModel.SaveSettings();
            }
        }

        private bool _showProgressDetails = false;

        public bool ShowProgressDetails
        {
            get => _showProgressDetails;
            set => this.RaiseAndSetIfChanged(ref _showProgressDetails, value);
        }

        private string _progressStatusText = string.Empty;

        public string ProgressStatusText
        {
            get => _progressStatusText;
            set => this.RaiseAndSetIfChanged(ref _progressStatusText, value);
        }

        private string _progressDetailText = string.Empty;

        public string ProgressDetailText
        {
            get => _progressDetailText;
            set => this.RaiseAndSetIfChanged(ref _progressDetailText, value);
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

        public ObservableCollection<string> Profiles { get; } = new ObservableCollection<string>();
        public BulkObservableCollection<Mod> ModList { get; } = new BulkObservableCollection<Mod>();

        public ReactiveCommand<Unit, Unit> SelectMo2DirectoryCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectImportSourceFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> SetSelectedAsOverrideCommand { get; }
        public ReactiveCommand<Unit, Unit> UnsetSelectedAsOverrideCommand { get; }
        public ReactiveCommand<Unit, Unit> LaunchImportPopupCommand { get; }
        public ReactiveCommand<Unit, Unit> AddMasterDependenciesCommand { get; }
        public ReactiveCommand<Mod, Unit> SelectAllInGroupCommand { get; }
        public ReactiveCommand<Mod, Unit> DeselectAllInGroupCommand { get; }

        public ImportViewModel(MainViewModel mainViewModel, StreamWriter logWriter)
        {
            _mainViewModel = mainViewModel;
            _logWriter = logWriter;
            SelectMo2DirectoryCommand = ReactiveCommand.Create(SelectMo2Directory);
            SelectImportSourceFolderCommand = ReactiveCommand.Create(SelectImportSourceFolder);
            SetSelectedAsOverrideCommand = ReactiveCommand.Create(SetSelectedAsOverrideMods);
            UnsetSelectedAsOverrideCommand = ReactiveCommand.Create(UnsetSelectedAsOverrideMods);
            LaunchImportPopupCommand =
                ReactiveCommand.Create(LaunchImportPopup, this.WhenAnyValue(x => x.IsImportEnabled));
            AddMasterDependenciesCommand = ReactiveCommand.Create(AddMasterDependencies);
            SelectAllInGroupCommand = ReactiveCommand.Create<Mod>(SelectAllInGroup);
            DeselectAllInGroupCommand = ReactiveCommand.Create<Mod>(DeselectAllInGroup);
            UndoSelectionCommand = ReactiveCommand.Create(UndoSelection, this.WhenAnyValue(x => x.CanUndo));
            RedoSelectionCommand = ReactiveCommand.Create(RedoSelection, this.WhenAnyValue(x => x.CanRedo));

            Profiles.Add("All");
            SelectedProfile = "All";
            IsImportEnabled = false; // Initially disable import until both directories are selected

            UpdateSelectedCount();

            ModList.ToObservableChangeSet().Subscribe(x =>
            {
                var sw = Stopwatch.StartNew();
                Debug.WriteLine($"=== ModList.ToObservableChangeSet event fired ({x.Count()} changes) ===");

                if (x.Any())
                {
                    _modsLoaded = true;
                    _filteredModList = new ObservableCollection<Mod>(ModList);
                    Debug.WriteLine($"  Set _filteredModList: {sw.ElapsedMilliseconds}ms");

                    sw.Restart();
                    ApplyFilter();
                    Debug.WriteLine($"  ApplyFilter completed: {sw.ElapsedMilliseconds}ms");
                }
                else
                {
                    _modsLoaded = false;
                }

                sw.Stop();
                Debug.WriteLine($"=== ModList.ToObservableChangeSet TOTAL: {sw.ElapsedMilliseconds}ms ===");
            });

            this.WhenAnyValue(x => x.FilterText)
                .Subscribe(_ => ApplyFilter());
        }
        
        public void OnViewLoaded(ImportView view)
        {
            _view = view; // Store the view reference
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


        public void UpdateImportEnabled()
        {
            bool sourceReady = false;
            if (IsSourceMo2Directory)
            {
                sourceReady = !string.IsNullOrEmpty(SelectedSourceProfile);
            }
            else
            {
                sourceReady = !string.IsNullOrEmpty(ImportSourceFolder) && Directory.Exists(ImportSourceFolder);
            }

            IsImportEnabled =
                !string.IsNullOrEmpty(Mo2Directory) && Directory.Exists(Mo2Directory) &&
                sourceReady &&
                ModList.Any(x => x.SelectedInUI);
        }


        private void SelectMo2Directory()
        {
            var dialog = new OpenFolderDialog();
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                Mo2Directory = dialog.FolderName;
                LoadProfiles();
            }
        }

        private void LoadProfiles()
        {
            Profiles.Clear();
            Profiles.Add("All");

            if (Directory.Exists(Mo2Directory))
            {
                var profilesPath = Path.Combine(Mo2Directory, "profiles");
                if (Directory.Exists(profilesPath))
                {
                    var profileDirs = Directory.GetDirectories(profilesPath);
                    foreach (var dir in profileDirs)
                    {
                        Profiles.Add(Path.GetFileName(dir));
                    }
                }
            }

            // Set default selection to "All"
            SelectedProfile = "All";
        }

        private void SelectImportSourceFolder()
        {
            var dialog = new OpenFolderDialog();
            dialog.FolderName = StartingImportSource;
            dialog.InitialDirectory = StartingImportSource;
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                ImportSourceFolder = dialog.FolderName;
                AnalyzeImportSourceFolder();
            }
        }

        private void AnalyzeImportSourceFolder()
{
    var totalStopwatch = Stopwatch.StartNew();
    Debug.WriteLine("=== AnalyzeImportSourceFolder START ===");
    
    IsPleaseWaitVisible = true;
    ModList.Clear();
    _modsRootPath = string.Empty;
    IsSourceMo2Directory = false;
    SelectedSourceProfile = null;
    SourceProfiles.Clear();

    var moIniPath = Path.Combine(ImportSourceFolder, "ModOrganizer.ini");
    if (File.Exists(moIniPath))
    {
        Debug.WriteLine("Detected MO2 directory");
        IsSourceMo2Directory = true;
        _modsRootPath = Path.Combine(ImportSourceFolder, "mods");
        LoadSourceProfiles();
        // Don't load any mods yet. Wait for user to select a profile.
        IsPleaseWaitVisible = false;
    }
    else
    {
        var modlistJsonPath = Path.Combine(ImportSourceFolder, "modlist.json");
        if (File.Exists(modlistJsonPath))
        {
            Debug.WriteLine("Loading from modlist.json");
            var sw = Stopwatch.StartNew();
            var jsonString = File.ReadAllText(modlistJsonPath);
            Debug.WriteLine($"  Read JSON file: {sw.ElapsedMilliseconds}ms");
            
            sw.Restart();
            var modlistData = JsonSerializer.Deserialize<ModlistJson>(jsonString);
            Debug.WriteLine($"  Deserialize JSON: {sw.ElapsedMilliseconds}ms");
            
            _modsRootPath = modlistData?.ModsRootPath ?? string.Empty;

            sw.Restart();
            var modsToAdd = new List<Mod>();
            foreach (var mod in modlistData?.SelectedMods ?? new())
            {
                var modItem = new Mod(mod) { SelectedInUI = true };
                modsToAdd.Add(modItem);
            }
            Debug.WriteLine($"  Create {modsToAdd.Count} Mod objects: {sw.ElapsedMilliseconds}ms");
            
            sw.Restart();
            ModList.AddRange(modsToAdd);
            Debug.WriteLine($"  Add mods to ModList: {sw.ElapsedMilliseconds}ms");
        }
        else
        {
            Debug.WriteLine("Loading from modlist.txt");
            _modsRootPath = ImportSourceFolder;
            var modListPath = Path.Combine(ImportSourceFolder, "modlist.txt");
            
            var sw = Stopwatch.StartNew();
            var modList = CommonFuncs.LoadModList(modListPath);
            Debug.WriteLine($"  Load modlist.txt: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            var modDirs = Directory.GetDirectories(ImportSourceFolder);
            Debug.WriteLine($"  Get directories: {sw.ElapsedMilliseconds}ms");
            
            sw.Restart();
            var modsToAdd = new List<Mod>();
            foreach (var modListEntry in modList)
            {
                var matchingDir = modDirs.FirstOrDefault(x =>
                    Path.GetFileName(x) == modListEntry.GetCurrentFolderName());
                if (matchingDir != null)
                {
                    var mod = new Mod(modListEntry) { SelectedInUI = true };
                    modsToAdd.Add(mod);
                }
            }
            Debug.WriteLine($"  Create {modsToAdd.Count} Mod objects: {sw.ElapsedMilliseconds}ms");
            
            sw.Restart();
            ModList.AddRange(modsToAdd);
            Debug.WriteLine($"  Add mods to ModList: {sw.ElapsedMilliseconds}ms");
        }
    }

    UpdateImportEnabled();
    // It is important to hide the please wait indicator here if not an mo2 source.
    // If it is an MO2 source, it will be hidden inside LoadModsFromSourceProfile after mods are loaded.
    if (!IsSourceMo2Directory)
    {
        IsPleaseWaitVisible = false;
        // Select all items in the ListBox for non-MO2 sources
        SelectAllItemsInListBox();
    }
    
    totalStopwatch.Stop();
    Debug.WriteLine($"=== AnalyzeImportSourceFolder TOTAL: {totalStopwatch.ElapsedMilliseconds}ms ===");
}

        private void LoadSourceProfiles()
        {
            SourceProfiles.Clear();

            if (Directory.Exists(ImportSourceFolder))
            {
                var profilesPath = Path.Combine(ImportSourceFolder, "profiles");
                if (Directory.Exists(profilesPath))
                {
                    var profileDirs = Directory.GetDirectories(profilesPath);
                    foreach (var dir in profileDirs)
                    {
                        SourceProfiles.Add(Path.GetFileName(dir));
                    }
                }
            }

            // Auto-select the profile from ModOrganizer.ini or fall back to first profile
            if (SourceProfiles.Any())
            {
                var selectedProfile = CommonFuncs.GetSelectedProfileFromIni(ImportSourceFolder);

                if (!string.IsNullOrEmpty(selectedProfile) && SourceProfiles.Contains(selectedProfile))
                {
                    SelectedSourceProfile = selectedProfile;
                }
                else
                {
                    // Fall back to first profile
                    SelectedSourceProfile = SourceProfiles.First();
                }
            }
        }

        private void LoadModsFromSourceProfile()
        {
            var totalStopwatch = Stopwatch.StartNew();
            Debug.WriteLine("=== LoadModsFromSourceProfile START ===");

            IsPleaseWaitVisible = true;

            var sw = Stopwatch.StartNew();
            ModList.Clear();
            Debug.WriteLine($"  Clear ModList: {sw.ElapsedMilliseconds}ms");

            if (string.IsNullOrEmpty(SelectedSourceProfile) || !IsSourceMo2Directory)
            {
                IsPleaseWaitVisible = false;
                Debug.WriteLine("=== LoadModsFromSourceProfile EARLY EXIT ===");
                return;
            }

            var modListPath = Path.Combine(ImportSourceFolder, "profiles", SelectedSourceProfile, "modlist.txt");

            sw.Restart();
            var modList = CommonFuncs.LoadModList(modListPath);
            Debug.WriteLine($"  Load modlist.txt ({modList.Count} entries): {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            var modDirs = Directory.GetDirectories(_modsRootPath);
            Debug.WriteLine($"  Get directories ({modDirs.Length} dirs): {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            var modsToAdd = new List<Mod>();
            int matchCount = 0;
            foreach (var modListEntry in modList)
            {
                var matchingDir =
                    modDirs.FirstOrDefault(x => Path.GetFileName(x) == modListEntry.GetCurrentFolderName());
                if (matchingDir != null)
                {
                    var mod = new Mod(modListEntry) { SelectedInUI = true };
                    modsToAdd.Add(mod);
                    matchCount++;
                }
            }

            Debug.WriteLine($"  Create {matchCount} Mod objects: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            ModList.AddRange(modsToAdd); // Add all at once!
            Debug.WriteLine($"  Add {modsToAdd.Count} mods to ModList: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            UpdateImportEnabled();
            Debug.WriteLine($"  UpdateImportEnabled: {sw.ElapsedMilliseconds}ms");

            IsPleaseWaitVisible = false;
            
            SelectAllItemsInListBox();

            totalStopwatch.Stop();
            Debug.WriteLine($"=== LoadModsFromSourceProfile TOTAL: {totalStopwatch.ElapsedMilliseconds}ms ===");
        }

        public void FilterModsForImport()
        {
            ShowFilteringNotification = Visibility.Visible;
            var modsToRemoveLog = new List<string>();
            _removedMods_Matching_Existing = new List<Mod>();
            var modsWithPluginsToRemove = new List<string>();
            var duplicateModViewModels = new List<DuplicateModItem>();

            var selectedModsToExport = ModList
                .Where(mod => mod.SelectedInUI &&
                              (!IgnoreDisabled || mod.IsEnabled()) &&
                              (!IgnoreSeparators || !mod.SourceListing.IsSeparator))
                .ToList();

            var modPathsInDestination = Directory.GetDirectories(Path.Combine(Mo2Directory, "mods"))
                                        ?? Array.Empty<string>();

            foreach (var mod in selectedModsToExport)
            {
                var literalModPathInMO2 = Path.Combine(Mo2Directory, "mods", mod.DisplayName);
                var simplifiedModPathInMO2 = Path.Combine(Mo2Directory, "mods", mod.SourceListing.Name);

                if (Directory.Exists(literalModPathInMO2) || Directory.Exists(simplifiedModPathInMO2) ||
                    ContainsNoDeleteFolder(modPathsInDestination, mod.SourceListing.Name))
                {
                    // Log and remove mod if a directory with the same name already exists in MO2
                    if (SkipExisting || !mod.OverWriteExistingDuringImport)
                    {
                        modsToRemoveLog.Add(mod.DisplayName + " - Matched existing directory name.");
                        //mod.SelectedInUI = false;

                        DuplicateModItem duplicateModItem = new DuplicateModItem(mod, DuplicateModItem.MatchMethodName);
                        if (Directory.Exists(literalModPathInMO2))
                        {
                            duplicateModItem.MatchedDestinationModPath = literalModPathInMO2;
                        }
                        else if (Directory.Exists(simplifiedModPathInMO2))
                        {
                            duplicateModItem.MatchedDestinationModPath = simplifiedModPathInMO2;
                            duplicateModItem.Label += " (as " + Path.GetFileName(simplifiedModPathInMO2) + ")";
                        }

                        duplicateModViewModels.Add(duplicateModItem);
                    }

                    continue;
                }

                // Determine the correct path to search for plugin files
                var searchPath = string.IsNullOrEmpty(_modsRootPath)
                    ? Path.Combine(ImportSourceFolder, mod.SourceDirectoryName)
                    : Path.Combine(_modsRootPath, mod.SourceDirectoryName);

                var pluginFiles = CommonFuncs.GetPluginPathsInDir(searchPath);

                if (pluginFiles.Any())
                {
                    foreach (var existingModDir in Directory.GetDirectories(Path.Combine(Mo2Directory, "mods")))
                    {
                        var existingModPlugins = CommonFuncs.GetPluginPathsInDir(existingModDir);

                        if (pluginFiles.All(pf => existingModPlugins.Any(ep =>
                                Path.GetFileName(pf).Equals(Path.GetFileName(ep), StringComparison.OrdinalIgnoreCase))))
                        {
                            if (SkipExisting || !mod.OverWriteExistingDuringImport)
                            {
                                // Log and remove mod if all plugin files match an existing mod in MO2
                                modsWithPluginsToRemove.Add(mod.DisplayName +
                                                            " - All plugins matched with an existing mod.");
                                //mod.SelectedInUI = false;

                                DuplicateModItem duplicateModItem =
                                    new DuplicateModItem(mod, DuplicateModItem.MatchMethodPlugin)
                                        { MatchedDestinationModPath = existingModDir };
                                duplicateModItem.Label += " (as " + Path.GetFileName(existingModDir) + ")";
                                duplicateModViewModels.Add(duplicateModItem);
                            }

                            break;
                        }
                    }
                }
            }

            ShowFilteringNotification = Visibility.Hidden;

            if (duplicateModViewModels.Any())
            {
                ImportDuplicateModSelectorViewModel duplicateModSelector = new(duplicateModViewModels, _modsRootPath);
                foreach (var toPreserve in duplicateModSelector.UnselectedModsForOverwrite)
                {
                    toPreserve.SelectedInUI = false;

                    if (IgnoreMatchedModsForOrdering)
                    {
                        _removedMods_Matching_Existing.Add(toPreserve);
                    }
                }
            }

            /*
            if (modsToRemoveLog.Any() || modsWithPluginsToRemove.Any())
            {
                ShowRemovalSummaryPopup(modsToRemoveLog, modsWithPluginsToRemove);
            }*/

            UpdateImportEnabled();
        }

        public static bool ContainsNoDeleteFolder(string[] subdirectoryPaths, string inputString)
        {
            if (subdirectoryPaths == null || subdirectoryPaths.Length == 0 || string.IsNullOrWhiteSpace(inputString))
                return false;

            // Regex pattern for matching "[NoDelete]" prefix variations
            string pattern = @"^\[NoDelete.*?\]\s*" + Regex.Escape(inputString) + @"$";

            try
            {
                // Check if any folder names match the pattern
                foreach (var folderPath in subdirectoryPaths)
                {
                    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                        continue; // Skip invalid paths

                    string folderName = Path.GetFileName(folderPath);
                    if (Regex.IsMatch(folderName, pattern, RegexOptions.IgnoreCase))
                    {
                        return true; // Match found
                    }
                }
            }
            catch (Exception ex)
            {
                ScrollableMessageBox.Show(
                    $"Error processing directories: {ExceptionHelper.GetFullExceptionMessage(ex)}", "Error");
            }

            return false; // No match found
        }

        private void ShowRemovalSummaryPopup(List<string> modsToRemove, List<string> modsWithPluginsToRemove)
        {
            var sb = new StringBuilder();

            if (modsToRemove.Any())
            {
                sb.AppendLine("Mods removed due to name conflicts:");
                foreach (var mod in modsToRemove)
                {
                    sb.AppendLine(mod);
                }
            }

            if (modsWithPluginsToRemove.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Mods removed due to matching plugins:");
                foreach (var mod in modsWithPluginsToRemove)
                {
                    sb.AppendLine(mod);
                }
            }

            MessageBox.Show(sb.ToString(), "Mods Removed from Import", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void UpdateSelectedCount()
        {
            int selectedCount = FilteredModList?.Where(x => x.SelectedInUI).Count() ?? 0;
            ImportButtonLabel =
                "Import " + selectedCount.ToString() + " Selected Mod" + (selectedCount != 1 ? "s" : "");
        }

        private void LaunchImportPopup()
        {
            // Filter the mods before launching the import popup
            FilterModsForImport();
            // If no mods are selected after filtering, don't open the popup
            if (ModList.Any(x => x.SelectedInUI))
            {
                var importPopup = new ImportPopupView();
                string profileSourceDir;
                if (IsSourceMo2Directory)
                {
                    profileSourceDir = Path.Combine(ImportSourceFolder, "profiles", SelectedSourceProfile);
                }
                else
                {
                    profileSourceDir = ImportSourceFolder;
                }

                var viewModel = new ImportPopupViewModel(importPopup, Mo2Directory, _modsRootPath, profileSourceDir,
                    SelectedProfile, ModList, SelectedImportMode, AddNoDeleteFlags, StripNoDelete,
                    MatchModActivationState, MatchPluginActivationState, _logWriter, _mainViewModel.ProgramVersion,
                    _autoCalculateSpace, ImportPrefix, _removedMods_Matching_Existing, IgnoreMatchedModsForOrdering,
                    InterpolateMissingPluginGroups, TransferDownloads, IsSourceMo2Directory, 
                    ImportSourceFolder);
                importPopup.DataContext = viewModel;
                importPopup.ShowDialog();
            }
            else
            {
                MessageBox.Show("No mods are available for import after filtering.", "No Mods to Import",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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

        private class ModlistJson
        {
            public string ModsRootPath { get; set; }
            public List<Mod> SelectedMods { get; set; }
        }

        private void ApplyFilter()
        {
            var sw = Stopwatch.StartNew();
            Debug.WriteLine("=== ApplyFilter START ===");

            if (string.IsNullOrEmpty(FilterText))
            {
                // If the filter is empty, show all mods
                FilteredModList = new ObservableCollection<Mod>(ModList);
                Debug.WriteLine($"  No filter - showing all {ModList.Count} mods: {sw.ElapsedMilliseconds}ms");
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
                Debug.WriteLine(
                    $"  Filtered to {matchedMods.Count} mods from {ModList.Count}: {sw.ElapsedMilliseconds}ms");
            }

            sw.Stop();
            Debug.WriteLine($"=== ApplyFilter TOTAL: {sw.ElapsedMilliseconds}ms ===");
        }

        private void AddMasterDependencies()
        {
            if (string.IsNullOrEmpty(_modsRootPath))
            {
                MessageBox.Show("Please select an import source folder first.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsPleaseWaitVisible = true;
                ShowProgressDetails = true;
                UpdateProgress("Analyzing Master Dependencies...", "Loading source plugin list");

                // Load source plugins.txt to check enabled status
                HashSet<string> enabledPlugins = new(StringComparer.OrdinalIgnoreCase);
                string sourcePluginsPath = IsSourceMo2Directory
                    ? Path.Combine(ImportSourceFolder, "profiles", SelectedSourceProfile, "plugins.txt")
                    : Path.Combine(ImportSourceFolder, "plugins.txt");

                if (File.Exists(sourcePluginsPath))
                {
                    var pluginListings = CommonFuncs.LoadPluginListRaw(sourcePluginsPath);
                    enabledPlugins = pluginListings
                        .Where(p => p.Enabled.HasValue && p.Enabled.Value)
                        .Select(p => p.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }

                var selectedMods = ModList.Where(m => m.SelectedInUI).ToList();
                var unselectedMods = ModList.Where(m => !m.SelectedInUI).ToList();

                // Track by full plugin path to handle duplicate plugin names across mods
                var analyzedPluginPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var modsToAdd = new HashSet<Mod>();
                var pluginsToAnalyze =
                    new Queue<(string pluginPath, string pluginName, string sourceMod, bool isFromNewlyAddedMod)>();

                // Track dependency information for report
                var dependencyReport = new Dictionary<string, DependencyInfo>(StringComparer.OrdinalIgnoreCase);

                int pluginsAnalyzed = 0;
                int modsAdded = 0;

                UpdateProgress("Analyzing Master Dependencies...", "Collecting plugins from selected mods");

                // Initial population: add all plugins from selected mods to analysis queue
                foreach (var mod in selectedMods)
                {
                    var modPath = Path.Combine(_modsRootPath, mod.SourceDirectoryName);
                    var pluginPaths = CommonFuncs.GetPluginPathsInDir(modPath);

                    foreach (var pluginPath in pluginPaths)
                    {
                        string pluginName = Path.GetFileName(pluginPath);

                        // Check if plugin should be analyzed based on enabled status
                        if (SelectedPluginSelectionMode == PluginSelectionMode.EnabledOnly)
                        {
                            if (!enabledPlugins.Contains(pluginName))
                            {
                                continue;
                            }
                        }

                        if (!analyzedPluginPaths.Contains(pluginPath))
                        {
                            pluginsToAnalyze.Enqueue((pluginPath, pluginName, mod.DisplayName, false));
                        }
                    }
                }

                UpdateProgress("Analyzing Master Dependencies...",
                    $"Found {pluginsToAnalyze.Count} plugin(s) to analyze");

                // Process queue
                while (pluginsToAnalyze.Count > 0)
                {
                    var (currentPluginPath, currentPluginName, sourceMod, isFromNewlyAddedMod) =
                        pluginsToAnalyze.Dequeue();

                    if (analyzedPluginPaths.Contains(currentPluginPath))
                        continue;

                    analyzedPluginPaths.Add(currentPluginPath);
                    pluginsAnalyzed++;

                    UpdateProgress($"Analyzing: {currentPluginName}",
                        $"From: {sourceMod} | Analyzed: {pluginsAnalyzed} plugins | Added: {modsAdded} mods");

                    if (!File.Exists(currentPluginPath))
                        continue;

                    // Create dependency info for this plugin
                    var depInfo = new DependencyInfo
                    {
                        PluginName = currentPluginName,
                        PluginPath = currentPluginPath,
                        SourceMod = sourceMod,
                        IsNewlyAdded = isFromNewlyAddedMod
                    };

                    // Get masters using Mutagen
                    var masterNames = GetPluginMasters(currentPluginPath);

                    // For each master, check if we need to add mods
                    foreach (var masterName in masterNames)
                    {
                        // Check if master is enabled (if using Enabled-Only mode)
                        if (SelectedPluginSelectionMode == PluginSelectionMode.EnabledOnly)
                        {
                            if (!enabledPlugins.Contains(masterName))
                            {
                                continue;
                            }
                        }

                        // Check if this master already exists in selected mods or mods being added
                        var selectedAndPendingMods = selectedMods.Concat(modsToAdd);
                        List<string> existingMasterPaths = FindPluginInMods(masterName, selectedAndPendingMods);

                        if (existingMasterPaths.Any())
                        {
                            // Master already covered
                            foreach (var existingPath in existingMasterPaths)
                            {
                                var parentMod = FindModByPluginPath(existingPath, selectedAndPendingMods);
                                var masterDep = new MasterDependency
                                {
                                    MasterName = masterName,
                                    SourceMod = parentMod?.DisplayName ?? "Unknown",
                                    IsNewlyAdded = modsToAdd.Contains(parentMod)
                                };
                                depInfo.Masters.Add(masterDep);

                                // Queue for analysis if not yet analyzed to get sub-dependencies
                                if (!analyzedPluginPaths.Contains(existingPath))
                                {
                                    pluginsToAnalyze.Enqueue((existingPath, masterName, masterDep.SourceMod,
                                        masterDep.IsNewlyAdded));
                                }
                                else if (dependencyReport.ContainsKey(existingPath))
                                {
                                    // Already analyzed, copy its sub-dependencies
                                    masterDep.SubMasters = CopySubMasters(dependencyReport[existingPath]);
                                }
                            }

                            continue;
                        }

                        // Master not found in selected mods, search unselected mods
                        List<Mod> modsWithMaster = new();

                        for (int i = unselectedMods.Count - 1; i >= 0; i--)
                        {
                            var mod = unselectedMods[i];
                            var modPath = Path.Combine(_modsRootPath, mod.SourceDirectoryName);
                            var masterPath = Path.Combine(modPath, masterName);

                            if (File.Exists(masterPath))
                            {
                                modsWithMaster.Add(mod);

                                if (SelectedMultiPluginMode == MultiPluginMode.WinnerOnly)
                                {
                                    break;
                                }
                            }
                        }

                        // Add found mods and queue their plugins for analysis
                        foreach (var mod in modsWithMaster)
                        {
                            var modPath = Path.Combine(_modsRootPath, mod.SourceDirectoryName); // Declare once here
    
                            bool isNewlyAdded = modsToAdd.Add(mod);
                            if (isNewlyAdded)
                            {
                                modsAdded++;
        
                                // NEW CODE: Analyze ALL plugins in this newly-added mod
                                var allPluginsInMod = CommonFuncs.GetPluginPathsInDir(modPath);
        
                                foreach (var pluginPath in allPluginsInMod)
                                {
                                    string pluginName = Path.GetFileName(pluginPath);
            
                                    // Check if plugin should be analyzed based on enabled status
                                    if (SelectedPluginSelectionMode == PluginSelectionMode.EnabledOnly)
                                    {
                                        if (!enabledPlugins.Contains(pluginName))
                                        {
                                            continue;
                                        }
                                    }
            
                                    if (!analyzedPluginPaths.Contains(pluginPath))
                                    {
                                        pluginsToAnalyze.Enqueue((pluginPath, pluginName, mod.DisplayName, true));
                                    }
                                }
                            }

                            // Existing code continues here - handles the specific master that triggered adding this mod
                            var masterPath = Path.Combine(modPath, masterName);

                            var masterDep = new MasterDependency
                            {
                                MasterName = masterName,
                                SourceMod = mod.DisplayName,
                                IsNewlyAdded = true
                            };
                            depInfo.Masters.Add(masterDep);

                            if (!analyzedPluginPaths.Contains(masterPath))
                            {
                                pluginsToAnalyze.Enqueue((masterPath, masterName, mod.DisplayName, true));
                            }
                            else if (dependencyReport.ContainsKey(masterPath))
                            {
                                // Already analyzed, copy its sub-dependencies
                                masterDep.SubMasters = CopySubMasters(dependencyReport[masterPath]);
                            }
                        }
                    }

                    dependencyReport[currentPluginPath] = depInfo;
                }

                // Apply selections
                foreach (var mod in modsToAdd)
                {
                    mod.SelectedInUI = true;
                }
                
                // Update the actual ListBox selection
                if (_view != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var mod in modsToAdd)
                        {
                            if (!_view.ModsListBox.SelectedItems.Contains(mod))
                            {
                                _view.ModsListBox.SelectedItems.Add(mod);
                            }
                        }
                    });
                }

                IsPleaseWaitVisible = false;
                ShowProgressDetails = false;
                UpdateImportEnabled();

                // Build and show report
                string report = BuildDependencyReport(dependencyReport, modsAdded, pluginsAnalyzed);
                ScrollableMessageBox.Show(report, "Master Dependencies Analysis");
            }
            catch (Exception ex)
            {
                IsPleaseWaitVisible = false;
                ShowProgressDetails = false;
                ScrollableMessageBox.Show(
                    $"Error analyzing master dependencies: {ExceptionHelper.GetFilteredStackTrace(ex)}", "Error");
            }
        }

        private List<MasterDependency> CopySubMasters(DependencyInfo depInfo)
        {
            var subMasters = new List<MasterDependency>();
            foreach (var master in depInfo.Masters)
            {
                subMasters.Add(new MasterDependency
                {
                    MasterName = master.MasterName,
                    SourceMod = master.SourceMod,
                    IsNewlyAdded = master.IsNewlyAdded,
                    SubMasters = new List<MasterDependency>(master.SubMasters)
                });
            }

            return subMasters;
        }

        private string BuildDependencyReport(Dictionary<string, DependencyInfo> dependencyReport, int modsAdded,
            int pluginsAnalyzed)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("MASTER DEPENDENCIES ANALYSIS REPORT");
            sb.AppendLine("═══════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Plugins Analyzed: {pluginsAnalyzed}");
            sb.AppendLine($"Mods Added: {modsAdded}");
            sb.AppendLine();

            if (modsAdded == 0)
            {
                sb.AppendLine("✓ All master dependencies are satisfied.");
                sb.AppendLine("  No additional mods needed.");
                return sb.ToString();
            }

            sb.AppendLine("Legend:");
            sb.AppendLine("  [+] = Mod was newly added to satisfy dependencies");
            sb.AppendLine("  [ ] = Mod was already selected");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────");
            sb.AppendLine();

            // Group by top-level plugins (those from originally selected or newly added mods)
            var topLevelPlugins = dependencyReport.Values
                .Where(d => d.Masters.Any()) // Only show plugins that have masters
                .OrderBy(d => d.PluginName)
                .ToList();

            foreach (var plugin in topLevelPlugins)
            {
                string marker = plugin.IsNewlyAdded ? "[+]" : "[ ]";
                sb.AppendLine($"{marker} {plugin.PluginName} (from: {plugin.SourceMod})");

                foreach (var master in plugin.Masters)
                {
                    AppendMasterDependency(sb, master, 1);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void AppendMasterDependency(StringBuilder sb, MasterDependency master, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 3);
            string marker = master.IsNewlyAdded ? "[+]" : "[ ]";

            sb.AppendLine($"{indent}→ {marker} {master.MasterName} (from: {master.SourceMod})");

            foreach (var subMaster in master.SubMasters)
            {
                AppendMasterDependency(sb, subMaster, indentLevel + 1);
            }
        }

// Keep the existing helper methods...
        private List<string> FindPluginInMods(string pluginName, IEnumerable<Mod> mods)
        {
            var paths = new List<string>();

            foreach (var mod in mods)
            {
                var modPath = Path.Combine(_modsRootPath, mod.SourceDirectoryName);
                var pluginPath = Path.Combine(modPath, pluginName);

                if (File.Exists(pluginPath))
                {
                    paths.Add(pluginPath);
                }
            }

            return paths;
        }

        private Mod FindModByPluginPath(string pluginPath, IEnumerable<Mod> mods)
        {
            foreach (var mod in mods)
            {
                var modPath = Path.Combine(_modsRootPath, mod.SourceDirectoryName);
                if (pluginPath.StartsWith(modPath, StringComparison.OrdinalIgnoreCase))
                {
                    return mod;
                }
            }

            return null;
        }

        private List<string> GetPluginMasters(string pluginPath)
        {
            try
            {
                using var plugin = Mutagen.Bethesda.Skyrim.SkyrimMod.CreateFromBinaryOverlay(
                    pluginPath,
                    Mutagen.Bethesda.Skyrim.SkyrimRelease.SkyrimSE);

                return plugin.ModHeader.MasterReferences
                    .Select(m => m.Master.FileName.String)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void UpdateProgress(string status, string detail = "")
        {
            ShowProgressDetails = true;
            ProgressStatusText = status;
            ProgressDetailText = detail;
            // Force UI update
            System.Windows.Application.Current.Dispatcher.Invoke(() => { },
                System.Windows.Threading.DispatcherPriority.Render);
        }

        private class DependencyInfo
        {
            public string PluginName { get; set; }
            public string PluginPath { get; set; }
            public string SourceMod { get; set; }
            public List<MasterDependency> Masters { get; set; } = new();
            public bool IsNewlyAdded { get; set; } // Was the mod containing this plugin newly added?
        }

        private class MasterDependency
        {
            public string MasterName { get; set; }
            public string SourceMod { get; set; }
            public bool IsNewlyAdded { get; set; }
            public List<MasterDependency> SubMasters { get; set; } = new(); // For nested dependencies
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
