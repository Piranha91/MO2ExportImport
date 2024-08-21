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

        private ImportMode _selectedImportMode;

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

        public ObservableCollection<string> Profiles { get; } = new ObservableCollection<string>();
        public ObservableCollection<Mod> ModList { get; } = new ObservableCollection<Mod>();

        public ReactiveCommand<Unit, Unit> SelectMo2DirectoryCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectImportSourceFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> LaunchImportPopupCommand { get; }

        public ImportViewModel(MainViewModel mainViewModel, StreamWriter logWriter)
        {
            _mainViewModel = mainViewModel;
            _logWriter = logWriter;
            SelectMo2DirectoryCommand = ReactiveCommand.Create(SelectMo2Directory);
            SelectImportSourceFolderCommand = ReactiveCommand.Create(SelectImportSourceFolder);
            LaunchImportPopupCommand = ReactiveCommand.Create(LaunchImportPopup, this.WhenAnyValue(x => x.IsImportEnabled));

            Profiles.Add("All");
            SelectedProfile = "All";
            IsImportEnabled = false; // Initially disable import until both directories are selected


            ModList.ToObservableChangeSet().Subscribe(x =>
            {
                if (x.Any())
                {
                    _modsLoaded = true;
                }
                else
                {
                    _modsLoaded = false;
                }
            });      
        }

        public void OnViewLoaded(ImportView view)
        {
            this.WhenAnyValue(x => x.ModsLoaded).Subscribe(x =>
            {
                if (x)
                {
                    view.ModListBox.SelectedItems.Clear();
                    foreach (var item in view.ModListBox.Items)
                    {
                        view.ModListBox.SelectedItems.Add(item);
                    }
                }
            });
        }


        public void UpdateImportEnabled()
        {
            IsImportEnabled =
                !string.IsNullOrEmpty(Mo2Directory) && Directory.Exists(Mo2Directory) &&
                !string.IsNullOrEmpty(ImportSourceFolder) && Directory.Exists(ImportSourceFolder) &&
                ModList.Any(x => x.Selected);
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
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                ImportSourceFolder = dialog.FolderName;
                AnalyzeImportSourceFolder();
            }
        }

        private void AnalyzeImportSourceFolder()
        {
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
                    var modItem = new Mod(mod.Name, true) { Selected = true }; // Always select the mod
                    ModList.Add(modItem);
                }
            }
            else
            {
                _modsRootPath = ImportSourceFolder;
                var modDirs = Directory.GetDirectories(ImportSourceFolder);
                foreach (var dir in modDirs)
                {
                    var modName = Path.GetFileName(dir);
                    var mod = new Mod(modName, true) { Selected = true }; // Enabled and Selected by default
                    ModList.Add(mod);
                }
            }

            UpdateImportEnabled(); // Update import enabled status based on all conditions
        }

        public void FilterModsForImport()
        {
            var modsToRemove = new List<string>();
            var modsWithPluginsToRemove = new List<string>();

            foreach (var mod in ModList.Where(x => x.Selected).ToList())
            {
                var modPathInMo2 = Path.Combine(Mo2Directory, "mods", mod.Name);
                if (Directory.Exists(modPathInMo2))
                {
                    // Log and remove mod if a directory with the same name already exists in MO2
                    modsToRemove.Add(mod.Name + " - Matched existing directory name.");
                    mod.Selected = false;
                    continue;
                }

                // Determine the correct path to search for plugin files
                var searchPath = string.IsNullOrEmpty(_modsRootPath) ? Path.Combine(ImportSourceFolder, mod.Name) : Path.Combine(_modsRootPath, mod.Name);

                var pluginFiles = Directory.GetFiles(searchPath, "*.*", SearchOption.TopDirectoryOnly)
                                           .Where(f => f.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) ||
                                                       f.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ||
                                                       f.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                                           .ToList();

                if (pluginFiles.Any())
                {
                    foreach (var existingModDir in Directory.GetDirectories(Path.Combine(Mo2Directory, "mods")))
                    {
                        var existingModPlugins = Directory.GetFiles(existingModDir, "*.*", SearchOption.TopDirectoryOnly)
                                                          .Where(f => f.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) ||
                                                                      f.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ||
                                                                      f.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                                                          .ToList();

                        if (pluginFiles.All(pf => existingModPlugins.Any(ep => Path.GetFileName(pf).Equals(Path.GetFileName(ep), StringComparison.OrdinalIgnoreCase))))
                        {
                            // Log and remove mod if all plugin files match an existing mod in MO2
                            modsWithPluginsToRemove.Add(mod.Name + " - All plugins matched with an existing mod.");
                            mod.Selected = false;
                            break;
                        }
                    }
                }
            }

            if (modsToRemove.Any() || modsWithPluginsToRemove.Any())
            {
                ShowRemovalSummaryPopup(modsToRemove, modsWithPluginsToRemove);
            }

            UpdateImportEnabled();
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

        private void LaunchImportPopup()
        {
            // Filter the mods before launching the import popup
            FilterModsForImport();

            // If no mods are selected after filtering, don't open the popup
            if (ModList.Any(x => x.Selected))
            {
                var importPopup = new ImportPopupView();
                var viewModel = new ImportPopupViewModel(importPopup, Mo2Directory, _modsRootPath, SelectedProfile, ModList, SelectedImportMode, _logWriter);
                importPopup.DataContext = viewModel;
                importPopup.ShowDialog();
            }
            else
            {
                MessageBox.Show("No mods are available for import after filtering.", "No Mods to Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private class ModlistJson
        {
            public string ModsRootPath { get; set; }
            public List<Mod> SelectedMods { get; set; }
        }
    }

}
