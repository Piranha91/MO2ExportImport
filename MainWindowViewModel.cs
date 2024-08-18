using Microsoft.Win32;
using MO2ExportImport.Models;
using ReactiveUI;
using System.IO;
using System.Reactive;
using System.Text.Json;

namespace MO2ExportImport.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        public string ProgramVersion { get; } = "1.0";

        private const string SettingsFilePath = "settings.json";
        private string _exportDestinationFolder;
        private ReactiveObject _currentView;
        private bool _ignoreDisabled;
        public bool IgnoreDisabled
        {
            get => _ignoreDisabled;
            set
            {
                this.RaiseAndSetIfChanged(ref _ignoreDisabled, value);
                SaveSettings(); // Save settings whenever IgnoreDisabled changes
            }
        }

        private bool _ignoreSeparators;
        public bool IgnoreSeparators
        {
            get => _ignoreSeparators;
            set
            {
                this.RaiseAndSetIfChanged(ref _ignoreSeparators, value);
                SaveSettings(); // Save settings whenever IgnoreSeparators changes
            }
        }

        public string ExportDestinationFolder
        {
            get => _exportDestinationFolder;
            set {
                this.RaiseAndSetIfChanged(ref _exportDestinationFolder, value);
                SaveSettings(); // Save settings whenever ExportDestinationFolder changes
            }
        }

        public ReactiveObject CurrentView
        {
            get => _currentView;
            set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }

        public ReactiveCommand<Unit, Unit> NavigateToExportCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateToImportCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseFolderCommand { get; }

        public MainViewModel()
        {
            LoadSettings(); // Load settings on startup

            NavigateToExportCommand = ReactiveCommand.Create(NavigateToExport);
            NavigateToImportCommand = ReactiveCommand.Create(NavigateToImport);
            BrowseFolderCommand = ReactiveCommand.CreateFromTask(BrowseFolder);

            // Set initial view
            CurrentView = new ExportViewModel(this);
        }

        private void NavigateToExport()
        {
            CurrentView = new ExportViewModel(this);
        }

        private void NavigateToImport()
        {
            CurrentView = new ImportViewModel(); // Implement ImportViewModel similarly to ExportViewModel
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

        private void LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var settingsJson = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<Settings>(settingsJson);
                ExportDestinationFolder = settings?.ExportDestinationFolder;
                IgnoreDisabled = settings?.IgnoreDisabled ?? true; // Default to true if not set
                IgnoreSeparators = settings?.IgnoreSeparators ?? false; // Default to false if not set
            }
        }

        private void SaveSettings()
        {
            var settings = new Settings
            {
                ExportDestinationFolder = ExportDestinationFolder,
                IgnoreDisabled = IgnoreDisabled,
                IgnoreSeparators = IgnoreSeparators
            };

            var settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, settingsJson);
        }
    }
}
