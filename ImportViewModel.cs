using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using MO2ExportImport.Views;

namespace MO2ExportImport.ViewModels
{
    public class ImportViewModel : ReactiveObject
    {
        private string _mo2Directory;
        private string _exportDirectory;
        private string _selectedProfile;

        public string Mo2Directory
        {
            get => _mo2Directory;
            set => this.RaiseAndSetIfChanged(ref _mo2Directory, value);
        }

        public string ExportDirectory
        {
            get => _exportDirectory;
            set => this.RaiseAndSetIfChanged(ref _exportDirectory, value);
        }

        public string SelectedProfile
        {
            get => _selectedProfile;
            set => this.RaiseAndSetIfChanged(ref _selectedProfile, value);
        }

        public ObservableCollection<string> Profiles { get; } = new ObservableCollection<string>();

        public ReactiveCommand<Unit, Unit> SelectMo2DirectoryCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectExportDirectoryCommand { get; }
        public ReactiveCommand<Unit, Unit> LaunchImportPopupCommand { get; }

        public ImportViewModel()
        {
            SelectMo2DirectoryCommand = ReactiveCommand.Create(SelectMo2Directory);
            SelectExportDirectoryCommand = ReactiveCommand.Create(SelectExportDirectory);
            LaunchImportPopupCommand = ReactiveCommand.Create(LaunchImportPopup);

            // Initialize with default profile selection
            Profiles.Add("All");
            SelectedProfile = "All";
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

        private void SelectExportDirectory()
        {
            var dialog = new OpenFolderDialog();
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                ExportDirectory = dialog.FolderName;
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

        private void LaunchImportPopup()
        {
            // Code to launch the import popup window
            var importPopup = new ImportPopupView(); // Assuming ImportPopupView exists
            var viewModel = new ImportPopupViewModel(importPopup, Mo2Directory, SelectedProfile, ExportDirectory);
            importPopup.DataContext = viewModel;
            importPopup.ShowDialog();
        }
    }
}
