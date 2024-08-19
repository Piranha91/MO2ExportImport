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
            _importViewModel = new();
            LoadSettings(); // Load settings on startup

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
            }
        }

        public void SaveSettings()
        {
            var settings = new Settings
            {
                ExportDestinationFolder = _exportViewModel.ExportDestinationFolder,
                IgnoreDisabled = _exportViewModel.IgnoreDisabled,
                IgnoreSeparators = _exportViewModel.IgnoreSeparators
            };

            var settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, settingsJson);
        }
    }
}
