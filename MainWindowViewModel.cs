using Microsoft.Win32;
using MO2ExportImport.Models;
using ReactiveUI;
using System.IO;
using System.Reactive;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace MO2ExportImport.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        public string ProgramVersion { get; } = "1.6";

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

        private static SolidColorBrush _activeColor = new(Colors.DarkGreen);
        private static SolidColorBrush _inactiveColor = new(Colors.Transparent);

        private SolidColorBrush _exportBorderColor = _activeColor;
        public SolidColorBrush ExportBorderColor
        {
            get => _exportBorderColor;
            set => this.RaiseAndSetIfChanged(ref _exportBorderColor, value);
        }

        private SolidColorBrush _importBorderColor = _inactiveColor;
        public SolidColorBrush ImportBorderColor
        {
            get => _importBorderColor;
            set => this.RaiseAndSetIfChanged(ref _importBorderColor, value);
        }

        private SolidColorBrush _undoBorderColor = _inactiveColor;
        public SolidColorBrush UndoBorderColor
        {
            get => _undoBorderColor;
            set => this.RaiseAndSetIfChanged(ref _undoBorderColor, value);
        }

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
            
            // run personal functions if needed
            PersonalFunctions.LaunchPersonalFunction();
        }

        private void NavigateToExport()
        {
            CurrentView = _exportViewModel;
            ResetColors();
            ExportBorderColor = _activeColor;
        }

        private void NavigateToImport()
        {
            CurrentView = _importViewModel;
            ResetColors();
            ImportBorderColor = _activeColor;
        }

        private void NavigateToUndo()
        {
            CurrentView = _undoOperationMenuViewModel;
            ResetColors();
            UndoBorderColor = _activeColor;
        }

        private void ResetColors()
        {
            ExportBorderColor = _inactiveColor;
            UndoBorderColor = _inactiveColor;
            ImportBorderColor = _inactiveColor;
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
                _exportViewModel.AutoCalculateSpace = settings?.ExportAutoCalculateSpace ?? true;
                _importViewModel.Mo2Directory = settings?.ImportTargetMO2Dir ?? string.Empty;
                _importViewModel.SelectedImportMode = settings?.ImportMode ?? ImportMode.Spliced;
                if (settings?.ImportMode == ImportMode.Before || settings?.ImportMode == ImportMode.After)
                {
                    if (!string.IsNullOrEmpty(settings.AnchorModName) && 
                        _importViewModel.AvailableAnchorMods != null)
                    {
                        _importViewModel.SelectedAnchorMod = _importViewModel.AvailableAnchorMods
                            .FirstOrDefault(m => m.DisplayName == settings.AnchorModName);
                    }
                }
                _importViewModel.IgnoreDisabled = settings?.ImportIgnoreDisabled ?? true; // Default to true if not set
                _importViewModel.IgnoreSeparators = settings?.ImportIgnoreSeparators ?? false; // Default to false if not set
                _importViewModel.AddNoDeleteFlags = settings?.ImportAddNoDeleteFlags ?? false;
                _importViewModel.StripNoDelete = settings?.ImportStripNoDelete ?? false;
                _importViewModel.SkipExisting = settings?.ImportSkipExistingMods ?? true;
                _importViewModel.MatchModActivationState = settings?.ImportMatchModActivationState ?? true;
                _importViewModel.MatchPluginActivationState = settings?.ImportMatchPluginActivationState ?? true;
                _importViewModel.AutoCalculateSpace = settings?.ImportAutoCalculateSpace ?? true;
                _importViewModel.ImportPrefix = settings?.ImportPrefix ?? string.Empty;
                _importViewModel.ImportPrefixSeparators = settings?.ImportPrefixSeparators ?? string.Empty;
                _importViewModel.ImportPrefixMods = settings?.ImportPrefixMods ?? string.Empty;
                _importViewModel.InterpolateMissingPluginGroups = settings?.ImportInterpolateMissingPluginGroups ?? false;
                _importViewModel.TransferDownloads = settings?.ImportTransferDownloads ?? false;
                _importViewModel.IgnoreMatchedModsForOrdering = settings?.ImportIgnoreMatchedModsForOrdering ?? true;

                _importViewModel.StartingImportSource = _exportViewModel.ExportDestinationFolder;
            }
        }

        public void SaveSettings()
        {
            if (_exportViewModel == null || _importViewModel == null || _undoOperationMenuViewModel == null)
            {
                return;
            }

            var settings = new Settings
            {
                ExportDestinationFolder = _exportViewModel.ExportDestinationFolder,
                ExportIgnoreDisabled = _exportViewModel.IgnoreDisabled,
                ExportIgnoreSeparators = _exportViewModel.IgnoreSeparators,
                ExportAutoCalculateSpace = _exportViewModel.AutoCalculateSpace,
                ImportTargetMO2Dir = _importViewModel.Mo2Directory,
                ImportMode = _importViewModel.SelectedImportMode,
                AnchorModName = _importViewModel.SelectedAnchorMod?.DisplayName,
                ImportIgnoreDisabled = _importViewModel.IgnoreDisabled,
                ImportIgnoreSeparators = _importViewModel.IgnoreSeparators,
                ImportAddNoDeleteFlags = _importViewModel.AddNoDeleteFlags,
                ImportSkipExistingMods = _importViewModel.SkipExisting,
                ImportMatchModActivationState = _importViewModel.MatchModActivationState,
                ImportMatchPluginActivationState = _importViewModel.MatchPluginActivationState,
                ImportStripNoDelete = _importViewModel.StripNoDelete,
                ImportAutoCalculateSpace = _importViewModel.AutoCalculateSpace,
                ImportPrefix = _importViewModel.ImportPrefix,
                ImportPrefixSeparators = _importViewModel.ImportPrefixSeparators,
                ImportPrefixMods = _importViewModel.ImportPrefixMods,
                ImportInterpolateMissingPluginGroups = _importViewModel.InterpolateMissingPluginGroups,
                ImportTransferDownloads = _importViewModel.TransferDownloads,
                ImportIgnoreMatchedModsForOrdering = _importViewModel.IgnoreMatchedModsForOrdering
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
                ScrollableMessageBox.Show($"An error occurred while checking or creating the Backups folder: {ExceptionHelper.GetFullExceptionMessage(ex)}", "Error");
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
                try
                {
                    _logWriter.Close();
                    _logWriter = null;
                }
                catch (Exception e)
                {

                }

            }
        }
    }
}
