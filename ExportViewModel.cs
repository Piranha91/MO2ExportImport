﻿using DynamicData;
using DynamicData.Binding;
using Microsoft.Win32;
using MO2ExportImport.Views;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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

        public ObservableCollection<Mod> ModList { get; set; } = new ObservableCollection<Mod>();

        public ReactiveCommand<Unit, Unit> SelectSourceCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportSelectedCommand { get; }

        public ExportViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;

            Profiles = new ObservableCollection<string>();
            ModList = new ObservableCollection<Mod>();

            SelectSourceCommand = ReactiveCommand.CreateFromTask(SelectSource);

            // Ensure that the ExportSelectedCommand is only enabled when a folder is selected and mods are selected

            // Set up the canExport observable
            var canExport = this.WhenAnyValue(
                x => x._mainViewModel.ExportDestinationFolder)
                .CombineLatest(
                    ModList.ToObservableChangeSet()
                           .AutoRefresh(mod => mod.Selected)
                           .ToCollection(),
                    (folder, mods) => !string.IsNullOrEmpty(folder) && mods.Any(mod => mod.Selected)
                );

            ExportSelectedCommand = ReactiveCommand.Create(ExportSelected, canExport);
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

            ModList.Clear();
            var modlistPath = Path.Combine(_mo2Directory, "profiles", _selectedProfile, "modlist.txt");
            if (File.Exists(modlistPath))
            {
                var mods = File.ReadAllLines(modlistPath)
                       .Where(line => !line.StartsWith("#")) // Exclude comment lines
                       .Select(line =>
                       {
                           var enabled = line.StartsWith("+");
                           var name = line.Substring(1).Trim();
                           return new Mod(name, enabled);
                       })
                       .Reverse(); // Invert the order

                foreach (var mod in mods)
                {
                    ModList.Add(mod);
                }
            }
        }

        private void ExportSelected()
        {
            var selectedModsToExport = ModList
                .Where(mod => mod.Selected && 
                    (!_mainViewModel.IgnoreDisabled || mod.Enabled) &&
                    (!_mainViewModel.IgnoreSeparators || !mod.IsSeparator))
                .Select(x => x.Name)
                .ToList();
            // Create and display the ExportPopupView
            var exportPopupView = new ExportPopupView();
            var exportPopupViewModel = new ExportPopupViewModel(exportPopupView, _mainViewModel, _mo2Directory, selectedModsToExport, _mainViewModel.ExportDestinationFolder, _selectedProfile);
            exportPopupView.DataContext = exportPopupViewModel;
            exportPopupView.ShowDialog();
        }
    }
}