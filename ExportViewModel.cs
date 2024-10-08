﻿using DynamicData;
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
        private readonly MainViewModel _mainViewModel;
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

        private bool _ignoreDisabled;
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

        public ObservableCollection<Mod> ModList { get; set; } = new ObservableCollection<Mod>();

        public ReactiveCommand<Unit, Unit> SelectSourceCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportSelectedCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseFolderCommand { get; }

        public ExportViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;

            IsPleaseWaitVisible = false;

            Profiles = new ObservableCollection<string>();
            ModList = new ObservableCollection<Mod>();

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

            ExportSelectedCommand = ReactiveCommand.Create(ExportSelected, canExport);

            BrowseFolderCommand = ReactiveCommand.CreateFromTask(BrowseFolder);
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

            if (Profiles.Any())
            {
                SelectedProfile = Profiles.First();
            }
        }

        private void LoadModList()
        {
            if (string.IsNullOrEmpty(_selectedProfile)) return;

            IsPleaseWaitVisible = true;
            System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render); // when this value becomes true, render the associated texblock right away. Without this code, rendering lags until time-consuming listbox updates are done.

            ModList.Clear();
            var modlistPath = Path.Combine(_mo2Directory, "profiles", _selectedProfile, "modlist.txt");
            if (File.Exists(modlistPath))
            {
                var mods = File.ReadAllLines(modlistPath)
                       .Where(line => !line.StartsWith("#")) // Exclude comment lines
                       .Select(line =>
                       {
                           return new Mod(line);
                       })
                       .Reverse(); // Invert the order

                foreach (var mod in mods)
                {
                    ModList.Add(mod);
                }
            }

            _filteredModList = new ObservableCollection<Mod>(ModList);

            IsLoadingList = true;
            foreach (var mod in _filteredModList)
            {
                if (mod.ListName == _filteredModList.Last().ListName)
                {
                    IsLoadingList = false;
                }
                mod.SelectedInUI = true;
            }

            this.WhenAnyValue(x => x.FilterText)
                .Subscribe(_ => ApplyFilter());

            UpdateSelectedCount();
        }

        private void ExportSelected()
        {
            var selectedModsToExport = ModList
                .Where(mod => mod.SelectedInUI && 
                    (!IgnoreDisabled || mod.EnabledInMO2) &&
                    (!IgnoreSeparators || !mod.IsSeparator))
                .ToList();
            // Create and display the ExportPopupView
            var exportPopupView = new ExportPopupView();
            var exportPopupViewModel = new ExportPopupViewModel(exportPopupView, this, _mo2Directory, selectedModsToExport, ExportDestinationFolder, _selectedProfile, _mainViewModel.ProgramVersion);
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
    }
}
