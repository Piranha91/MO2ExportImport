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

        public ObservableCollection<string> ModList
        {
            get => _modList;
            set => this.RaiseAndSetIfChanged(ref _modList, value);
        }

        public ObservableCollection<string> SelectedMods { get; }

        public ReactiveCommand<Unit, Unit> SelectSourceCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportSelectedCommand { get; }

        public ExportViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;

            SelectSourceCommand = ReactiveCommand.CreateFromTask(SelectSource);

            // Ensure that the ExportSelectedCommand is only enabled when a folder is selected and mods are selected
            var canExport = this.WhenAnyValue(
                x => x._mainViewModel.ExportDestinationFolder,
                x => x.SelectedMods.Count,
                (folder, mods) => !string.IsNullOrEmpty(folder) && mods > 0
            );

            ExportSelectedCommand = ReactiveCommand.Create(ExportSelected, canExport);

            Profiles = new ObservableCollection<string>();
            ModList = new ObservableCollection<string>();
            SelectedMods = new ObservableCollection<string>();

            // Subscribe to changes in the SelectedMods collection to trigger re-evaluation
            SelectedMods.CollectionChanged += (s, e) => this.RaisePropertyChanged(nameof(SelectedMods));
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
            ModList.Clear();
            var modlistPath = Path.Combine(_mo2Directory, "profiles", SelectedProfile, "modlist.txt");
            if (File.Exists(modlistPath))
            {
                var mods = File.ReadAllLines(modlistPath)
                               .Where(line => !line.StartsWith("#"))     // Exclude comment lines
                               .Where(line => !line.Contains("DLC:"))    // Exclude lines starting with "DLC:"
                               .Select(line => line.Substring(1).Trim()) // Remove the first character
                               .Reverse();                               // Invert the order

                foreach (var mod in mods)
                {
                    ModList.Add(mod);
                }
            }
        }

        private void ExportSelected()
        {
            // Create and display the ExportPopupView
            var exportPopupView = new ExportPopupView();
            var exportPopupViewModel = new ExportPopupViewModel(exportPopupView, _mainViewModel, _mo2Directory, SelectedMods, _mainViewModel.ExportDestinationFolder, _selectedProfile);
            exportPopupView.DataContext = exportPopupViewModel;
            exportPopupView.ShowDialog();
        }
    }
}
