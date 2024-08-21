using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Windows;
using System.Windows.Media;
using MO2ExportImport.Views;
using ReactiveUI;

namespace MO2ExportImport.ViewModels
{
    public class ImportPopupViewModel : ReactiveObject
    {
        private readonly string _mo2Directory;
        private string _modSourceDirectory;
        private readonly ObservableCollection<Mod> _modList;
        private bool _isImportEnabled;
        private readonly ImportPopupView _view;
        private string _selectedProfile;

        public bool IsImportEnabled
        {
            get => _isImportEnabled;
            set => this.RaiseAndSetIfChanged(ref _isImportEnabled, value);
        }

        private string _requiredSpaceText;
        public string RequiredSpaceText
        {
            get => _requiredSpaceText;
            set => this.RaiseAndSetIfChanged(ref _requiredSpaceText, value);
        }

        private string _availableSpaceText;
        public string AvailableSpaceText
        {
            get => _availableSpaceText;
            set => this.RaiseAndSetIfChanged(ref _availableSpaceText, value);
        }

        private string _spaceStatusText;
        public string SpaceStatusText
        {
            get => _spaceStatusText;
            set => this.RaiseAndSetIfChanged(ref _spaceStatusText, value);
        }

        private SolidColorBrush _spaceStatusColor;
        public SolidColorBrush SpaceStatusColor
        {
            get => _spaceStatusColor;
            set => this.RaiseAndSetIfChanged(ref _spaceStatusColor, value);
        }

        public ReactiveCommand<Unit, Unit> CalculateSpaceCommand { get; }
        public ReactiveCommand<Unit, Unit> ImportCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public ImportPopupViewModel(ImportPopupView view, string mo2Directory, string modSourceDirectory, string selectedProfile, ObservableCollection<Mod> modList)
        {
            _view = view;
            _mo2Directory = mo2Directory;
            _modSourceDirectory = modSourceDirectory;
            _modList = modList;
            _selectedProfile = selectedProfile;

            CalculateSpaceCommand = ReactiveCommand.Create(CalculateSpace);
            ImportCommand = ReactiveCommand.Create(ImportMods, this.WhenAnyValue(x => x.IsImportEnabled));
            CancelCommand = ReactiveCommand.Create(ClosePopup);

            IsImportEnabled = false;
        }

        private void CalculateSpace()
        {
            try
            {
                var totalSize = _modList.Where(x => x.Selected)
                                        .Sum(mod => GetDirectorySize(Path.Combine(_modSourceDirectory, mod.Name)));

                var requiredSpaceInGB = ConvertBytesToGB(totalSize);
                RequiredSpaceText = $"Total size: {requiredSpaceInGB:F2} GB";

                var driveInfo = new DriveInfo(Path.GetPathRoot(_mo2Directory));
                var availableSpaceInGB = ConvertBytesToGB(driveInfo.AvailableFreeSpace);
                AvailableSpaceText = $"Available space: {availableSpaceInGB:F2} GB";

                if (availableSpaceInGB >= requiredSpaceInGB)
                {
                    SpaceStatusText = "There is enough space on the drive for import.";
                    SpaceStatusColor = Brushes.Green;
                    IsImportEnabled = true;
                }
                else
                {
                    SpaceStatusText = "There is not enough space on the drive for import.";
                    SpaceStatusColor = Brushes.Red;
                    IsImportEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during space calculation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;

            var dirInfo = new DirectoryInfo(path);
            return dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }

        private static double ConvertBytesToGB(long bytes)
        {
            return bytes / (1024.0 * 1024.0 * 1024.0);
        }

        private void ImportMods()
        {
            // Logic for importing mods

            // Step 1: Backup the selected profiles
            BackupSelectedProfiles();

            // Step 2: Proceed with the import logic
        }

        private void ClosePopup()
        {
            // Logic to close the popup window
            _view.Close();
        }

        private void BackupSelectedProfiles()
        {
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string backupsFolderPath = Path.Combine(exeDirectory, "Backups");

                // Ensure the Backups directory exists
                if (!Directory.Exists(backupsFolderPath))
                {
                    Directory.CreateDirectory(backupsFolderPath);
                }

                // Create a new backup directory with a timestamp
                string currentBackupDir = Path.Combine(backupsFolderPath, DateTime.Now.ToString("yyyy MM dd HH mm"));
                Directory.CreateDirectory(currentBackupDir);

                // Determine which profiles to back up
                var profilesToBackup = new List<string>();
                if (_selectedProfile == "All")
                {
                    profilesToBackup.AddRange(Directory.GetDirectories(Path.Combine(_mo2Directory, "profiles")).Select(Path.GetFileName));
                }
                else
                {
                    profilesToBackup.Add(_selectedProfile);
                }

                foreach (var profile in profilesToBackup)
                {
                    string profileDir = Path.Combine(_mo2Directory, "profiles", profile);
                    string profileBackupDir = Path.Combine(currentBackupDir, profile);

                    if (Directory.Exists(profileDir))
                    {
                        Directory.CreateDirectory(profileBackupDir);

                        // Backup plugins.txt
                        string pluginsFilePath = Path.Combine(profileDir, "plugins.txt");
                        if (File.Exists(pluginsFilePath))
                        {
                            File.Copy(pluginsFilePath, Path.Combine(profileBackupDir, "plugins.txt"), overwrite: true);
                        }

                        // Backup modlist.txt
                        string modlistFilePath = Path.Combine(profileDir, "modlist.txt");
                        if (File.Exists(modlistFilePath))
                        {
                            File.Copy(modlistFilePath, Path.Combine(profileBackupDir, "modlist.txt"), overwrite: true);
                        }
                    }
                }

                MessageBox.Show("Backup completed successfully.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during the backup process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
