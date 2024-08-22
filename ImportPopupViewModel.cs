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
        private ImportMode _importMode;
        private StreamWriter _logWriter;

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

        public ImportPopupViewModel(ImportPopupView view, string mo2Directory, string modSourceDirectory, string selectedProfile, ObservableCollection<Mod> modList, ImportMode importMode, StreamWriter logWriter)
        {
            _view = view;
            _mo2Directory = mo2Directory;
            _modSourceDirectory = modSourceDirectory;
            _modList = modList;
            _selectedProfile = selectedProfile;
            _importMode = importMode;
            _logWriter = logWriter;

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

        private async void ImportMods()
        {
            BackupSelectedProfiles(); // Start logging

            try
            {
                foreach (var profile in ProfilesToImport())
                {
                    string profileDir = Path.Combine(_mo2Directory, "profiles", profile);
                    if (!Directory.Exists(profileDir))
                    {
                        continue; // Skip if profile directory does not exist
                    }

                    // Load and reverse the ProfileModList and ProfilePluginsList for correct processing
                    var profileModListPath = Path.Combine(profileDir, "modlist.txt");
                    var profileModList = LoadModList(profileModListPath);

                    var profilePluginsListPath = Path.Combine(profileDir, "plugins.txt");
                    var profilePluginsList = LoadModList(profilePluginsListPath, reverseOrder: false);

                    // Load the SourceModList and SourcePluginsList
                    var sourceModListPath = Path.Combine(_modSourceDirectory, "modlist.txt");
                    var sourceModList = LoadModList(sourceModListPath, reverseOrder: false);

                    var sourcePluginsListPath = Path.Combine(_modSourceDirectory, "plugins.txt");
                    var sourcePluginsList = LoadModList(sourcePluginsListPath, reverseOrder: false);

                    // Filter SourceModList to include only mods with corresponding directories
                    var validSourceMods = sourceModList
                        .Where(mod => Directory.Exists(Path.Combine(_modSourceDirectory, mod.TrimStart('+', '-'))))
                        .ToList();

                    // Collect valid plugins based on validSourceMods
                    var validPlugins = new List<string>();
                    foreach (var mod in validSourceMods)
                    {
                        var modDirectory = Path.Combine(_modSourceDirectory, mod.TrimStart('+', '-'));
                        if (Directory.Exists(modDirectory))
                        {
                            var pluginFiles = Directory.GetFiles(modDirectory, "*.*", SearchOption.TopDirectoryOnly)
                                .Where(f => f.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                                .Select(Path.GetFileName)
                                .ToList();

                            foreach (var plugin in pluginFiles)
                            {
                                // Match ignoring the leading asterisk in SourcePluginsList
                                var matchingPlugin = sourcePluginsList.FirstOrDefault(sp =>
                                    sp.TrimStart('*').Equals(plugin, StringComparison.OrdinalIgnoreCase));

                                if (matchingPlugin != null)
                                {
                                    validPlugins.Add(matchingPlugin); // Retain the original activation status
                                }
                            }
                        }
                    }

                    // Handle ImportMode for modlist.txt
                    var ignorePositions = new List<string>();
                    foreach (var currentMod in validSourceMods)
                    {
                        if (_importMode == ImportMode.End)
                        {
                            var previousItem = profileModList.LastOrDefault() ?? "start";
                            profileModList.Add(currentMod);
                            Log($"Added {currentMod.TrimStart('+', '-')} to end of modlist.txt after {previousItem}");
                        }
                        else // Spliced
                        {
                            var previousItem = AddModInSplicedMode(profileModList, validSourceMods, currentMod, ignorePositions);
                            Log($"Spliced {currentMod.TrimStart('+', '-')} into modlist.txt after {previousItem}");
                        }
                    }

                    // Handle ImportMode for plugins.txt
                    ignorePositions.Clear();
                    foreach (var currentPlugin in validPlugins)
                    {
                        if (_importMode == ImportMode.End)
                        {
                            var previousItem = profilePluginsList.LastOrDefault() ?? "start";
                            profilePluginsList.Add(currentPlugin);
                            Log($"Added {currentPlugin.TrimStart('*')} to end of plugins.txt after {previousItem}");
                        }
                        else // Spliced
                        {
                            var previousItem = AddModInSplicedMode(profilePluginsList, validPlugins, currentPlugin, ignorePositions);
                            Log($"Spliced {currentPlugin.TrimStart('*')} into plugins.txt after {previousItem}");
                        }
                    }

                    // Reverse the ProfileModList back to original order before saving
                    profileModList.Reverse();

                    // Reinsert the special comment line at the beginning
                    profileModList.Insert(0, "# This file was automatically generated by Mod Organizer.");
                    profilePluginsList.Insert(0, "# This file was automatically generated by Mod Organizer.");

                    // For debugging: Save to DebugOutput instead of overwriting the files
                    string debugOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DebugOutput", DateTime.Now.ToString("yyyy-MM-dd_HH-mm"), profile);
                    Directory.CreateDirectory(debugOutputDir);

                    File.WriteAllLines(Path.Combine(debugOutputDir, "modlist.txt"), profileModList);
                    File.WriteAllLines(Path.Combine(debugOutputDir, "plugins.txt"), profilePluginsList);

                    // Now copy the valid mods into debugOutputDir\mods
                    string modsOutputDir = Path.Combine(debugOutputDir, "mods");
                    Directory.CreateDirectory(modsOutputDir);

                    var copyTasks = new List<Task>();

                    foreach (var mod in validSourceMods)
                    {
                        var sourceModPath = Path.Combine(_modSourceDirectory, mod.TrimStart('+', '-'));
                        var destinationModPath = Path.Combine(modsOutputDir, mod.TrimStart('+', '-'));

                        if (Directory.Exists(sourceModPath) && !Directory.Exists(destinationModPath))
                        {
                            // Add the async copy task to the list
                            copyTasks.Add(Task.Run(async () =>
                            {
                                bool success = await FileOperation.CopyFolderWithUIAsync(sourceModPath, destinationModPath);
                                if (success)
                                {
                                    Log($"Copied mod {mod.TrimStart('+', '-')} to {destinationModPath}");
                                }
                                else
                                {
                                    Log($"Failed to copy mod {mod.TrimStart('+', '-')} to {destinationModPath}");
                                }
                            }));
                        }
                    }

                    // Wait for all copy tasks to complete
                    await Task.WhenAll(copyTasks);
                }

                MessageBox.Show("Import completed successfully. Check DebugOutput for results.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"An error occurred during the import process: {ex.Message}");
                MessageBox.Show($"An error occurred during the import process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> LoadModList(string filePath, bool reverseOrder = true)
        {
            if (!File.Exists(filePath))
            {
                return new List<string>();
            }

            var lines = File.ReadAllLines(filePath).Where(line => !line.StartsWith("#")).ToList();

            if (reverseOrder)
            {
                lines.Reverse(); // Reverse the list in place if needed
            }

            return lines;
        }

        private string AddModInSplicedMode(List<string> profileList, List<string> sourceList, string currentMod, List<string> ignorePositions)
        {
            for (int i = sourceList.IndexOf(currentMod) - 1; i >= 0; i--)
            {
                string precedingMod = sourceList[i];
                if (ignorePositions.Contains(precedingMod))
                {
                    continue;
                }

                int indexInProfile = profileList.IndexOf(precedingMod);
                if (indexInProfile != -1)
                {
                    profileList.Insert(indexInProfile + 1, currentMod);
                    return precedingMod;
                }
            }

            // If no precedingMod is found or inserted, add to end
            profileList.Add(currentMod);
            ignorePositions.Add(currentMod);
            return "end";
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

                //MessageBox.Show("Backup completed successfully.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during the backup process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private IEnumerable<string> ProfilesToImport()
        {
            if (_selectedProfile == "All")
            {
                return Directory.GetDirectories(Path.Combine(_mo2Directory, "profiles")).Select(Path.GetFileName);
            }
            else
            {
                return new List<string> { _selectedProfile };
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }


        private void Log(string message)
        {
            _logWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        private void CloseLog()
        {
            _logWriter?.Close();
        }
    }
}
