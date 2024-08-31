using Microsoft.Win32;
using MO2ExportImport.Models;
using ReactiveUI;
using System.IO;
using System.Reactive;
using System.Reflection.Metadata;
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
        private readonly UndoOperationMenuViewModel _undoOperationMenuViewModel;

        private StreamWriter _logWriter;
        private ReactiveObject _currentView;

        public ReactiveObject CurrentView
        {
            get => _currentView;
            set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }

        public ReactiveCommand<Unit, Unit> NavigateToExportCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateToImportCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateToUndoCommand { get; }

        public MainViewModel()
        {
            InitializeLog();

            _exportViewModel = new(this);
            _importViewModel = new(this, _logWriter);
            _undoOperationMenuViewModel = new();
            LoadSettings(); // Load settings on startup

            // Ensure Backups folder exists on startup
            EnsureBackupsFolderExists();

            NavigateToExportCommand = ReactiveCommand.Create(NavigateToExport);
            NavigateToImportCommand = ReactiveCommand.Create(NavigateToImport);
            NavigateToUndoCommand = ReactiveCommand.Create(NavigateToUndo);

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

        private void NavigateToUndo()
        {
            CurrentView = _undoOperationMenuViewModel;
        }


        private void LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var settingsJson = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<Settings>(settingsJson);

                _exportViewModel.ExportDestinationFolder = settings?.ExportDestinationFolder ?? string.Empty;
                _exportViewModel.IgnoreDisabled = settings?.ExportIgnoreDisabled ?? true; // Default to true if not set
                _exportViewModel.IgnoreSeparators = settings?.ExportIgnoreSeparators ?? false; // Default to false if not set
                _importViewModel.Mo2Directory = settings?.ImportTargetMO2Dir ?? string.Empty;
                _importViewModel.SelectedImportMode = settings?.ImportMode ?? ImportMode.Spliced;
                _importViewModel.IgnoreDisabled = settings?.ImportIgnoreDisabled ?? true; // Default to true if not set
                _importViewModel.IgnoreSeparators = settings?.ImportIgnoreSeparators ?? false; // Default to false if not set
            }
        }

        public void SaveSettings()
        {
            var settings = new Settings
            {
                ExportDestinationFolder = _exportViewModel.ExportDestinationFolder,
                ExportIgnoreDisabled = _exportViewModel.IgnoreDisabled,
                ExportIgnoreSeparators = _exportViewModel.IgnoreSeparators,
                ImportTargetMO2Dir = _importViewModel.Mo2Directory,
                ImportMode = _importViewModel.SelectedImportMode,
                ImportIgnoreDisabled = _importViewModel.IgnoreDisabled,
                ImportIgnoreSeparators = _importViewModel.IgnoreSeparators
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

        private void InitializeLog()
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log.txt");
            _logWriter = new StreamWriter(logFilePath, append: false); // Overwrite log on each start
        }

        public void CloseLog()
        {
            if (_logWriter != null)
            {
                _logWriter.Close();
                _logWriter = null;
            }
        }
    }
}
