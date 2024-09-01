using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using DynamicData;
using MO2ExportImport.Models;
using MO2ExportImport.Views;
using ReactiveUI;
using static MO2ExportImport.FormatHandler;

namespace MO2ExportImport.ViewModels
{
    public class ImportPopupViewModel : ReactiveObject
    {
        private readonly string _mo2Directory;
        private string _modSourceDirectory;
        private string _importProfileSourceDirectory;
        private readonly ObservableCollection<Mod> _selectedModList;
        private bool _isImportEnabled;
        private readonly ImportPopupView _view;
        private string _selectedProfile;
        private ImportMode _importMode;
        private StreamWriter _logWriter;
        private string _programVersion;

        private const string _manifestRelativePath = "ImportManifests";

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

        public ImportPopupViewModel(ImportPopupView view, string mo2Directory, string modSourceDirectory, string importProfileSourceDirectory, string selectedProfile, ObservableCollection<Mod> modList, ImportMode importMode, StreamWriter logWriter, string programVersion)
        {
            _view = view;
            _mo2Directory = mo2Directory;
            _modSourceDirectory = modSourceDirectory;
            _importProfileSourceDirectory = importProfileSourceDirectory;
            _selectedModList = modList;
            _selectedProfile = selectedProfile;
            _importMode = importMode;
            _logWriter = logWriter;
            _programVersion = programVersion;

            CalculateSpaceCommand = ReactiveCommand.Create(CalculateSpace);
            ImportCommand = ReactiveCommand.Create(ImportMods, this.WhenAnyValue(x => x.IsImportEnabled));
            CancelCommand = ReactiveCommand.Create(ClosePopup);

            IsImportEnabled = false;
        }

        private void CalculateSpace()
        {
            try
            {
                var totalSize = _selectedModList.Where(x => x.SelectedInUI)
                                        .Sum(mod => GetDirectorySize(Path.Combine(_modSourceDirectory, mod.DirectoryName)));

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

            var manifest = new ImportOperation(_modSourceDirectory, _mo2Directory, DateTime.Now, _programVersion);

            try
            {
                foreach (var profile in ProfilesToImport())
                {
                    string profileDir = Path.Combine(_mo2Directory, "profiles", profile);
                    Log($"Importing to profile {profile}:");

                    if (!Directory.Exists(profileDir))
                    {
                        Log($"Error: Cannot find {profileDir}:");
                        continue; // Skip if profile directory does not exist
                    }

                    var profileManifest = new ProfileImportOperation(profile);

                    var modsOutputDir = Path.Combine(_mo2Directory, "mods");

                    // Load and reverse the ProfileModList and ProfilePluginsList for correct processing
                    var profileModListPath = Path.Combine(profileDir, "modlist.txt");
                    var profileModList = CommonFuncs.LoadModList(profileModListPath);

                    var profilePluginsListPath = Path.Combine(profileDir, "plugins.txt");
                    var profilePluginsList = CommonFuncs.LoadPluginList(profilePluginsListPath);

                    // Load the SourceModList and SourcePluginsList
                    var sourceModListPath = Path.Combine(_importProfileSourceDirectory, "modlist.txt");
                    var sourceModList = CommonFuncs.LoadModList(sourceModListPath);

                    var sourcePluginsListPath = Path.Combine(_importProfileSourceDirectory, "plugins.txt");
                    var sourcePluginsList = CommonFuncs.LoadPluginList(sourcePluginsListPath);

                    // Filter SourceModList to include only mods with corresponding directories
                    var validSourceMods = sourceModList
                        .Where(mod => _selectedModList.Select(x => x.DirectoryName).Contains(FormatHandler.TrimModActivationStatus(mod)))
                        .Where(mod => Directory.Exists(Path.Combine(_modSourceDirectory, FormatHandler.TrimModActivationStatus(mod))))
                        .ToList();

                    // Collect valid plugins based on validSourceMods
                    var profilePluginsSearchList = FormatHandler.TrimPluginActivationStatus(profilePluginsList).ToArray();

                    var validPlugins = new List<string>();
                    foreach (var mod in validSourceMods)
                    {
                        var modName = FormatHandler.TrimModActivationStatus(mod);
                        var modDirectory = Path.Combine(_modSourceDirectory, modName);
                        if (Directory.Exists(modDirectory))
                        {
                            var pluginFilesInMod = Directory.GetFiles(modDirectory, "*.*", SearchOption.TopDirectoryOnly)
                                .Where(f => f.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                                .Select(Path.GetFileName)
                                .ToList();

                            foreach (var pluginFileName in pluginFilesInMod)
                            {
                                // Ignore plugins that already exist in destination load order
                                if (profilePluginsSearchList.Contains(pluginFileName))
                                {
                                    Log($"Skipped {pluginFileName} because it is already present in the destination load order");
                                    continue;
                                }

                                // Match ignoring the leading asterisk in SourcePluginsList
                                var matchingPlugin = sourcePluginsList.FirstOrDefault(x => FormatHandler.TrimPluginActivationStatus(x) == pluginFileName);

                                if (matchingPlugin != null)
                                {
                                    validPlugins.Add(matchingPlugin); // Retain the original activation status
                                    profileManifest.AddedPluginNames.Add(new(FormatHandler.TrimPluginActivationStatus(matchingPlugin), modName));
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
                            Log($"Added { FormatHandler.TrimModActivationStatus(currentMod)} to end of modlist.txt after {previousItem}");
                        }
                        else // Spliced
                        {
                            var previousItem = AddEntryInSplicedMode(profileModList, sourceModList, currentMod, ignorePositions, StringType.Mod);
                            Log($"Spliced {FormatHandler.TrimModActivationStatus(currentMod)} into modlist.txt after {previousItem}");
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
                            Log($"Added {FormatHandler.TrimPluginActivationStatus(currentPlugin)} to end of plugins.txt after {previousItem}");
                        }
                        else // Spliced
                        {
                            var previousItem = AddEntryInSplicedMode(profilePluginsList, sourcePluginsList, currentPlugin, ignorePositions, StringType.Plugin);
                            Log($"Spliced {FormatHandler.TrimPluginActivationStatus(currentPlugin)} into plugins.txt after {previousItem}");
                        }
                    }

                    if (!CommonFuncs.SaveModList(profileModListPath, profileModList, out var modExStr))
                    {
                        Log(modExStr);
                    }
                    if (!CommonFuncs.SavePluginList(profilePluginsListPath, profilePluginsList, out var pluginExStr))
                    {
                        Log(pluginExStr);
                    }

                    // Now copy the valid mods into the mods folder

                    var copyTasks = new List<Task>();


                    foreach (var mod in validSourceMods)
                    {
                        var sourceModPath = Path.Combine(_modSourceDirectory, FormatHandler.TrimModActivationStatus(mod));
                        var destinationModPath = Path.Combine(modsOutputDir, FormatHandler.TrimModActivationStatus(mod));

                        if (Directory.Exists(sourceModPath) && !Directory.Exists(destinationModPath))
                        {
                            // Add the async copy task to the list
                            copyTasks.Add(Task.Run(async () =>
                            {
                                bool success = await FileOperation.CopyFolderWithUIAsync(sourceModPath, destinationModPath);
                                if (success)
                                {
                                    Log($"Copied mod {FormatHandler.TrimModActivationStatus(mod)} to {destinationModPath}");
                                }
                                else
                                {
                                    Log($"Failed to copy mod {FormatHandler.TrimModActivationStatus(mod)} to {destinationModPath}");
                                }
                            }));
                        }
                    }

                    // Wait for all copy tasks to complete
                    await Task.WhenAll(copyTasks);

                    profileManifest.AddedModNames.AddRange(FormatHandler.TrimModActivationStatus(validSourceMods));  
                    manifest.ProfileImports.Add(profileManifest);
                }

                SaveManifest(manifest);
                MessageBox.Show("Import completed successfully.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                ClosePopup();
            }
            catch (Exception ex)
            {
                Log($"An error occurred during the import process: {ex.Message}");
                MessageBox.Show($"An error occurred during the import process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string AddEntryInSplicedMode(List<string> profileList, List<string> sourceList, string currentEntry, List<string> ignoredEntries, StringType stringType)
        {
            var searchEntry = FormatHandler.TrimActivationStatus(currentEntry, stringType);
            var profileSearchList = FormatHandler.TrimActivationStatus(profileList, stringType).ToArray();

            for (int i = sourceList.IndexOf(currentEntry) - 1; i >= 0; i--)
            {
                string precedingSearchEntry = FormatHandler.TrimActivationStatus(sourceList[i], stringType);
                if (ignoredEntries.Contains(precedingSearchEntry))
                {
                    continue;
                }

                int indexInProfile = profileSearchList.IndexOf(precedingSearchEntry);
                if (indexInProfile != -1)
                {
                    profileList.Insert(indexInProfile + 1, currentEntry);
                    return precedingSearchEntry;
                }
            }

            // If no precedingEntry is found or inserted, add to end
            profileList.Add(currentEntry);
            //ignoredEntries.Add(currentEntry); Commented out for now. Double checking my logic, I don't think this makes sense to include.
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

        private void SaveManifest(ImportOperation manifest)
        {
            if (!Directory.Exists(_manifestRelativePath))
            {
                Directory.CreateDirectory(_manifestRelativePath);
            }

            var destinationPath = Path.Combine(_manifestRelativePath, manifest.ImportTime.ToString("yyyy MM dd HH mm"));

            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(destinationPath, "ImportManifest.json"), manifestJson);
        }
    }
}
