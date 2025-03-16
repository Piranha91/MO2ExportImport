using DynamicData.Binding;
using Microsoft.Win32;
using MO2ExportImport.Models;
using MO2ExportImport.Views;
using ReactiveUI;
using System.Collections.ObjectModel;
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

        public string Mo2Directory
        {
            get => _mo2Directory;
            set
            {
                this.RaiseAndSetIfChanged(ref _mo2Directory, value);
                _mainViewModel.SaveSettings();  // Assuming there's a Save method in Settings to persist the changes
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
                System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render); // when this value becomes true, render the associated texblock right away. Without this code, rendering lags until time-consuming listbox updates are done.
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

        public ObservableCollection<string> Profiles { get; } = new ObservableCollection<string>();
        public ObservableCollection<Mod> ModList { get; } = new ObservableCollection<Mod>();

        public ReactiveCommand<Unit, Unit> SelectMo2DirectoryCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectImportSourceFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> SetSelectedAsOverrideCommand { get; }
        public ReactiveCommand<Unit, Unit> UnsetSelectedAsOverrideCommand { get; }
        public ReactiveCommand<Unit, Unit> LaunchImportPopupCommand { get; }

        public ImportViewModel(MainViewModel mainViewModel, StreamWriter logWriter)
        {
            _mainViewModel = mainViewModel;
            _logWriter = logWriter;
            SelectMo2DirectoryCommand = ReactiveCommand.Create(SelectMo2Directory);
            SelectImportSourceFolderCommand = ReactiveCommand.Create(SelectImportSourceFolder);
            SetSelectedAsOverrideCommand = ReactiveCommand.Create(SetSelectedAsOverrideMods);
            UnsetSelectedAsOverrideCommand = ReactiveCommand.Create(UnsetSelectedAsOverrideMods);
            LaunchImportPopupCommand = ReactiveCommand.Create(LaunchImportPopup, this.WhenAnyValue(x => x.IsImportEnabled));

            Profiles.Add("All");
            SelectedProfile = "All";
            IsImportEnabled = false; // Initially disable import until both directories are selected

            UpdateSelectedCount();

            ModList.ToObservableChangeSet().Subscribe(x =>
            {
                if (x.Any())
                {
                    _modsLoaded = true;
                    _filteredModList = new ObservableCollection<Mod>(ModList);
                    ApplyFilter();
                }
                else
                {
                    _modsLoaded = false;
                }
            });

            this.WhenAnyValue(x => x.FilterText)
                .Subscribe(_ => ApplyFilter());
        }

        public void OnViewLoaded(ImportView view)
        {
            this.WhenAnyValue(x => x.ModsLoaded).Subscribe(x =>
            {
                if (x)
                {
                    view.ModsListBox.SelectedItems.Clear();
                    foreach (var item in view.ModsListBox.Items)
                    {
                        view.ModsListBox.SelectedItems.Add(item);
                    }
                }
            });
        }


        public void UpdateImportEnabled()
        {
            IsImportEnabled =
                !string.IsNullOrEmpty(Mo2Directory) && Directory.Exists(Mo2Directory) &&
                !string.IsNullOrEmpty(ImportSourceFolder) && Directory.Exists(ImportSourceFolder) &&
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
            IsPleaseWaitVisible = true;

            ModList.Clear();
            _modsRootPath = string.Empty;

            var modlistJsonPath = Path.Combine(ImportSourceFolder, "modlist.json");
            if (File.Exists(modlistJsonPath))
            {
                var jsonString = File.ReadAllText(modlistJsonPath);
                var modlistData = JsonSerializer.Deserialize<ModlistJson>(jsonString);
                _modsRootPath = modlistData?.ModsRootPath ?? string.Empty;

                foreach (var mod in modlistData?.SelectedMods ?? new())
                {
                    var modItem = new Mod(mod) { SelectedInUI = true }; // Always select the mod
                    ModList.Add(modItem);
                }
            }
            else
            {
                _modsRootPath = ImportSourceFolder;

                var modListPath = Path.Combine(ImportSourceFolder, "modlist.txt");
                var modList = CommonFuncs.LoadModList(modListPath);

                var modDirs = Directory.GetDirectories(ImportSourceFolder);

                foreach (var modListEntry in modList)
                {
                    var matchingDir = modDirs.FirstOrDefault(x => Path.GetFileName(x) == modListEntry.GetCurrentFolderName());
                    if (matchingDir != null)
                    {
                        var mod = new Mod(modListEntry) { SelectedInUI = true }; // Selected by default | If for some reason the mod doesn't exist in the modlist.txt, build the Mod entry from the mod name (starts disabled).
                        ModList.Add(mod);
                    }
                }
            }

            UpdateImportEnabled(); // Update import enabled status based on all conditions
        }

        public void FilterModsForImport()
        {
            ShowFilteringNotification = Visibility.Visible;
            var modsToRemoveLog = new List<string>();
            _removedMods_Matching_Existing = new List<Mod>();
            var modsWithPluginsToRemove = new List<string>();

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

                if (Directory.Exists(literalModPathInMO2) || Directory.Exists(simplifiedModPathInMO2) || ContainsNoDeleteFolder(modPathsInDestination, mod.SourceListing.Name))
                {
                    // Log and remove mod if a directory with the same name already exists in MO2
                    if (SkipExisting || !mod.OverWriteExistingDuringImport)
                    {
                        modsToRemoveLog.Add(mod.DisplayName + " - Matched existing directory name.");
                        mod.SelectedInUI = false;
                    }

                    if (IgnoreMatchedModsForOrdering)
                    {
                        _removedMods_Matching_Existing.Add(mod);
                    }
                    
                    continue;
                }

                // Determine the correct path to search for plugin files
                var searchPath = string.IsNullOrEmpty(_modsRootPath) ? Path.Combine(ImportSourceFolder, mod.SourceDirectoryName) : Path.Combine(_modsRootPath, mod.SourceDirectoryName);

                var pluginFiles = CommonFuncs.GetPluginPathsInDir(searchPath);

                if (pluginFiles.Any())
                {
                    foreach (var existingModDir in Directory.GetDirectories(Path.Combine(Mo2Directory, "mods")))
                    {
                        var existingModPlugins = CommonFuncs.GetPluginPathsInDir(existingModDir);

                        if (pluginFiles.All(pf => existingModPlugins.Any(ep => Path.GetFileName(pf).Equals(Path.GetFileName(ep), StringComparison.OrdinalIgnoreCase))))
                        {
                            if (SkipExisting || !mod.OverWriteExistingDuringImport)
                            {
                                // Log and remove mod if all plugin files match an existing mod in MO2
                                modsWithPluginsToRemove.Add(mod.DisplayName +
                                                            " - All plugins matched with an existing mod.");
                                mod.SelectedInUI = false;
                            }
                            
                            break;
                        }
                    }
                }
            }
            
            ShowFilteringNotification = Visibility.Hidden;

            if (modsToRemoveLog.Any() || modsWithPluginsToRemove.Any())
            {
                ShowRemovalSummaryPopup(modsToRemoveLog, modsWithPluginsToRemove);
            }

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
                ScrollableMessageBox.Show($"Error processing directories: {ExceptionHelper.GetFullExceptionMessage(ex)}", "Error");
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

            MessageBox.Show(sb.ToString(), "Mods Removed from Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void UpdateSelectedCount()
        {
            int selectedCount = FilteredModList?.Where(x => x.SelectedInUI).Count() ?? 0;
            ImportButtonLabel = "Import " + selectedCount.ToString() + " Selected Mod" + (selectedCount != 1 ? "s" : "");
        }

        private void LaunchImportPopup()
        {
            // Filter the mods before launching the import popup
            FilterModsForImport();

            // If no mods are selected after filtering, don't open the popup
            if (ModList.Any(x => x.SelectedInUI))
            {
                var importPopup = new ImportPopupView();
                var viewModel = new ImportPopupViewModel(importPopup, Mo2Directory, _modsRootPath, ImportSourceFolder, SelectedProfile, ModList, SelectedImportMode, AddNoDeleteFlags, StripNoDelete, MatchModActivationState, MatchPluginActivationState, _logWriter, _mainViewModel.ProgramVersion, _autoCalculateSpace, ImportPrefix, _removedMods_Matching_Existing, IgnoreMatchedModsForOrdering, InterpolateMissingPluginGroups);
                importPopup.DataContext = viewModel;
                importPopup.ShowDialog();
            }
            else
            {
                MessageBox.Show("No mods are available for import after filtering.", "No Mods to Import", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
