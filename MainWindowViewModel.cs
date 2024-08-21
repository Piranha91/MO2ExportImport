using Microsoft.Win32;
using MO2ExportImport.Models;
using ReactiveUI;
using System.IO;
using System.Reactive;
using System.Text.Json;
using System.Windows;

namespace MO2ExportImport.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        public string ProgramVersion { get; } = "1.0";

        private const string SettingsFilePath = "settings.json";

        private readonly ExportViewModel _exportViewModel;
        private readonly ImportViewModel _importViewModel;

        private ReactiveObject _currentView;

        public ReactiveObject CurrentView
        {
            get => _currentView;
            set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }

        public ReactiveCommand<Unit, Unit> NavigateToExportCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateToImportCommand { get; }

        public MainViewModel()
        {
            _exportViewModel = new(this);
            _importViewModel = new(this);
            LoadSettings(); // Load settings on startup

            // Ensure Backups folder exists on startup
            EnsureBackupsFolderExists();

            NavigateToExportCommand = ReactiveCommand.Create(NavigateToExport);
            NavigateToImportCommand = ReactiveCommand.Create(NavigateToImport);

            // Set initial view
            CurrentView = _exportViewModel;
        }

        private void NavigateToExport()
        {
            CurrentView = _exportViewModel;
        }

        private void NavigateToImport()
        {
            CurrentView = _importViewModel;
        }


        private void LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var settingsJson = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<Settings>(settingsJson);
                _exportViewModel.ExportDestinationFolder = settings?.ExportDestinationFolder ?? string.Empty;
                _exportViewModel.IgnoreDisabled = settings?.IgnoreDisabled ?? true; // Default to true if not set
                _exportViewModel.IgnoreSeparators = settings?.IgnoreSeparators ?? false; // Default to false if not set
                _importViewModel.Mo2Directory = settings?.ImportTargetMO2Dir ?? string.Empty;
            }
        }

        public void SaveSettings()
        {
            var settings = new Settings
            {
                ExportDestinationFolder = _exportViewModel.ExportDestinationFolder,
                IgnoreDisabled = _exportViewModel.IgnoreDisabled,
                IgnoreSeparators = _exportViewModel.IgnoreSeparators,
                ImportTargetMO2Dir = _importViewModel.Mo2Directory
            };

            var settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, settingsJson);
        }
        private void EnsureBackupsFolderExists()
        {
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string backupsFolderPath = Path.Combine(exeDirectory, "Backups");

                if (!Directory.Exists(backupsFolderPath))
                {
                    Directory.CreateDirectory(backupsFolderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while checking or creating the Backups folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
