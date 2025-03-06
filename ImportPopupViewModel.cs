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
        private bool _addNoDeleteFlags;
        private bool _removeNoDeleteFlags;
        private bool _matchModActivationState;
        private bool _matchPluginActivationState;
        private StreamWriter _logWriter;
        private List<string> _importEvents = new();
        private string _programVersion;
        private string _importPrefix;
        private List<Mod> _removedMatchingMods = new();
        private bool _ignoreMatchedModsForOrdering;
        private bool _interpolateMissingPluginGroups;

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

        public ImportPopupViewModel(ImportPopupView view, string mo2Directory, string modSourceDirectory, string importProfileSourceDirectory, string selectedProfile, ObservableCollection<Mod> modList, ImportMode importMode, bool addNoDeleteFlags, bool removeNoDeleteFlags, bool matchModActivationState, bool matchPluginActivationState, StreamWriter logWriter, string programVersion, bool autoCalculateSpace, string importPrefix, List<Mod> removedMatchingMods, bool IgnoreMatchedModsForOrdering, bool interpolateMissingPluginGroups)
        {
            _view = view;
            _mo2Directory = mo2Directory;
            _modSourceDirectory = modSourceDirectory;
            _importProfileSourceDirectory = importProfileSourceDirectory;
            _selectedModList = modList;
            _selectedProfile = selectedProfile;
            _importMode = importMode;
            _addNoDeleteFlags = addNoDeleteFlags;
            _removeNoDeleteFlags = removeNoDeleteFlags;
            _matchModActivationState = matchModActivationState;
            _matchPluginActivationState = matchPluginActivationState;
            _logWriter = logWriter;
            _programVersion = programVersion;
            _importPrefix = importPrefix;
            _removedMatchingMods = removedMatchingMods;
            _ignoreMatchedModsForOrdering = IgnoreMatchedModsForOrdering;
            _interpolateMissingPluginGroups = interpolateMissingPluginGroups;

            CalculateSpaceCommand = ReactiveCommand.Create(CalculateSpace);
            ImportCommand = ReactiveCommand.Create(ImportMods, this.WhenAnyValue(x => x.IsImportEnabled));
            CancelCommand = ReactiveCommand.Create(ClosePopup);

            IsImportEnabled = false;

            if (autoCalculateSpace)
            {
                CalculateSpace();
            }
        }

        private void CalculateSpace()
        {
            try
            {
                var totalSize = _selectedModList.Where(x => x.SelectedInUI)
                                        .Sum(mod => GetDirectorySize(Path.Combine(_modSourceDirectory, mod.SourceDirectoryName)));

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
                ScrollableMessageBox.Show($"An error occurred during space calculation: {ExceptionHelper.GetFullExceptionMessage(ex)}", "Error");
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
            _importEvents = new();

            BackupSelectedProfiles(); // Start logging

            var manifest = new ImportOperation(_modSourceDirectory, _mo2Directory, DateTime.Now, _programVersion);

            try
            {
                var removedPluginNames = _removedMatchingMods.SelectMany(x =>
                        CommonFuncs.GetPluginNamesInDir(Path.Combine(_modSourceDirectory, x.SourceDirectoryName)))
                    .ToList();
                
                var removedModListings = _removedMatchingMods.Select(x => x.SourceListing).Cast<IListing>().ToList();
                
                var modsOutputDir = Path.Combine(_mo2Directory, "mods");
                
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

                    // Load and reverse the ProfileModList and ProfilePluginsList for correct processing
                    var profileModListPath = Path.Combine(profileDir, "modlist.txt");
                    var profileModList = CommonFuncs.LoadModList(profileModListPath).Cast<IListing>().ToList();
                    profileManifest.OriginalModList = profileModList.Cast<ModListing>().Select(x => x.GetCurrentEntryString()).ToList();

                    var profilePluginsListPath = Path.Combine(profileDir, "plugins.txt");
                    var profilePluginsList = CommonFuncs.LoadPluginListFromLoadOrder(profileDir).Cast<IListing>().ToList();;
                    profileManifest.OriginalPluginList = profilePluginsList.Cast<PluginListing>().Select(x => x.GetCurrentEntryString()).ToList();

                    // Load the SourceModList and SourcePluginsList
                    var sourceModListPath = Path.Combine(_importProfileSourceDirectory, "modlist.txt");
                    var sourceModList = CommonFuncs.LoadModList(sourceModListPath).Cast<IListing>().ToList();;

                    //var sourcePluginsListPath = Path.Combine(_importProfileSourceDirectory, "plugins.txt");
                    var sourcePluginsList = CommonFuncs.LoadPluginListFromLoadOrder(_importProfileSourceDirectory).Cast<IListing>().ToList();;
                    
                    var spliceModeIgnoredPluginListings = sourcePluginsList.Where(x => removedPluginNames.Contains(x.Name)).ToList();
                                        
                    // Match the enabled/disabled status of plugins from the source mod list
                    List<PluginListing> pluginsFromExistingButNewlyActivatedMods = new();
                    if (_matchModActivationState)
                    {
                        var (enabledMods, disabledMods) = MatchModActivationStatus(profileModList.Cast<ModListing>().ToList(), sourceModList.Cast<ModListing>().ToList());
                        
                        profileManifest.EnabledMods = enabledMods.Select(x => x.Name).ToList();
                        if (profileManifest.EnabledMods.Any())
                        {
                            string enabledRecord = "- Enabled the following mods in profile " + profile + " because they are enabled in the mod list being imported" + Environment.NewLine + string.Join(Environment.NewLine, profileManifest.EnabledMods.Select(x => "-- " + x).ToArray());
                            Log(enabledRecord);

                            foreach (var mod in enabledMods)
                            {
                                var modFolder = Path.Combine(_mo2Directory, "mods", mod.GetCurrentFolderName());
                                {
                                    var plugins = CommonFuncs.GetPluginNamesInDir(modFolder);
                                    foreach (var pluginName in plugins)
                                    {
                                        var existingPlugin = sourcePluginsList.FirstOrDefault(x => x.Name == pluginName);
                                        if (existingPlugin is PluginListing plugin)
                                        {
                                            pluginsFromExistingButNewlyActivatedMods.Add(plugin);
                                        }
                                    }
                                }
                            }
                        }
                        
                        profileManifest.DisabledMods = disabledMods.Select(x => x.Name).ToList();
                        if (profileManifest.DisabledMods.Any())
                        {
                            string disabledRecord = "- Disabled the following mods in profile " + profile + " because they are disabled in the mod list being imported" + Environment.NewLine + string.Join(Environment.NewLine, profileManifest.DisabledMods.Select(x => "-- " + x).ToArray());
                            Log(disabledRecord);
                        }

                        profileManifest.DeletedPlugins = DeletePluginsFromUncheckedMods(disabledMods,
                            profilePluginsList.Cast<PluginListing>().ToList(), modsOutputDir);
                        if (profileManifest.DeletedPlugins.Any())
                        {
                            string deletedPlugins = "- Deleted the following plugins in profile " + profile + " because they were from mods that are disabled in the mod list being imported" + Environment.NewLine + string.Join(Environment.NewLine, profileManifest.DeletedPlugins.Select(x => "-- " + x.Name).ToArray());
                            Log(deletedPlugins);
                        }
                    }

                    // Filter SourceModList to include only mods with corresponding directories
                    
                    var validSourceMods = _selectedModList
                        .Where(x => x.SelectedInUI) // don't import mods that have been manually or automatically deselected
                        .Where(mod => Directory.Exists(Path.Combine(_modSourceDirectory, mod.SourceDirectoryName)))
                        .ToList();

                    if (_addNoDeleteFlags)
                    {
                        Log("Adding NoDelete flags to mods where required");
                        foreach (var mod in validSourceMods)
                        {
                            mod.MakeNoDelete();
                        }
                    }
                    else if (_removeNoDeleteFlags)
                    {
                        Log("Removing NoDelete flags from mods where required");
                        foreach(var mod in validSourceMods)
                        {
                            mod.RemoveNoDelete();
                        }
                    }

                    if (_importPrefix != null && _importPrefix.Length > 0)
                    {
                        Log("Adding prefix \"" + _importPrefix + "\" to each mod name");

                        foreach (var mod in validSourceMods)
                        {
                            mod.SetPrefix(_importPrefix);
                        }
                    }

                    // Collect valid plugins based on validSourceMods
                    Log("Collecting plugin names for import");
                    var validPlugins = new List<PluginListing>();
                    
                    foreach (var mod in validSourceMods)
                    {
                        var modDirectory = Path.Combine(_modSourceDirectory, mod.SourceDirectoryName);
                        if (Directory.Exists(modDirectory))
                        {
                            var pluginFilesInMod = CommonFuncs.GetPluginPathsInDir(modDirectory)
                                .Select(Path.GetFileName)
                                .ToList();

                            foreach (var pluginFileName in pluginFilesInMod)
                            {
                                // Ignore plugins that already exist in destination load order
                                var existingPluginListing = profilePluginsList.FirstOrDefault(x => x.Name == pluginFileName);
                                if (existingPluginListing is not null)
                                {
                                    if (_ignoreMatchedModsForOrdering && _importMode == ImportMode.Spliced)
                                    {
                                        Log($"Plugin ordering: the position of {pluginFileName} will be disregarded when importing other plugin because it is already present in the destination load order");
                                        spliceModeIgnoredPluginListings.Add(existingPluginListing); // this plugin is not where the source mod list expects it to be in the load order, so don't use it to anchor spliced-in plugins.
                                    }
                                    Log($"Plugin Import: {pluginFileName} from {mod.DisplayName} is already present in the destination load order so it will not be added.");
                                    continue;
                                }

                                var matchedSourcePlugin = sourcePluginsList.FirstOrDefault(x => x.Name == pluginFileName);
                                if (matchedSourcePlugin is PluginListing match)
                                {
                                    var alreadyAddedPlugin = validPlugins.FirstOrDefault(x => x.Equals(match));
                                    if (alreadyAddedPlugin is null) // don't add the same plugin multiple times (e.g. from override mods)
                                    {
                                        validPlugins.Add(match);
                                    }
                                    else
                                    {
                                        Log($"Plugin Import: {pluginFileName} from {mod.DisplayName} is being skipped for import because another imported mod is already supplying this plugin.");
                                    }

                                    profileManifest.AddedPluginNames.Add(new(pluginFileName, mod.GetDestinationName())); // register the plugin regardless of whether it's an override or not.
                                }
                            }
                        }
                    }
                    
                    // add plugins activated from latent mods
                    foreach (var plugin in pluginsFromExistingButNewlyActivatedMods)
                    {
                        var matchedPlugin = validPlugins.FirstOrDefault(x => x.Equals(plugin));
                        if (matchedPlugin is null)
                        {
                            validPlugins.Add(plugin);
                        }
                    }
                    
                    // Create a dictionary to map each PluginListing in sourcePluginsList to its index
                    var sourcePluginIndexMap = sourcePluginsList
                        .Select((listing, index) => new { listing, index })
                        .ToDictionary(x => x.listing, x => x.index);

                    // Sort validPlugins in-place based on their order in sourcePluginsList
                    validPlugins.Sort((plugin1, plugin2) =>
                    {
                        var index1 = sourcePluginIndexMap.TryGetValue(plugin1, out var idx1) ? idx1 : int.MaxValue;
                        var index2 = sourcePluginIndexMap.TryGetValue(plugin2, out var idx2) ? idx2 : int.MaxValue;
                        return index1.CompareTo(index2);
                    });
                    
                    // Handle ImportMode for modlist.txt
                    Log("Importing mods into modlist.txt");
                    foreach (var currentMod in validSourceMods)
                    {
                        if (_importMode == ImportMode.End)
                        {
                            var previousItem = profileModList.LastOrDefault()?.Name ?? "start";
                            profileModList.Add(currentMod.SourceListing);
                            Log($"- Added { FormatHandler.TrimModActivationStatus(currentMod.DisplayName)} to end of modlist.txt after {previousItem}");
                        }
                        else // Spliced
                        {
                            var spliceLog = new List<string>();
                            var previousItem = CommonFuncs.AddEntryInSplicedMode(profileModList, sourceModList, currentMod.SourceListing, removedModListings, StringType.Mod, spliceLog);
                            Log(string.Join(Environment.NewLine, spliceLog.Select(x => "-- " + x).ToArray()));
                            Log($"- Spliced {FormatHandler.TrimModActivationStatus(currentMod.DisplayName)} into modlist.txt after {previousItem}");
                        }
                    }

                    // Handle ImportMode for plugins.txt
                    Log("Importing plugins into plugins.txt");
                    foreach (var currentPlugin in validPlugins)
                    {
                        if (_importMode == ImportMode.End)
                        {
                            var previousItem = profilePluginsList.LastOrDefault()?.Name ?? "start";
                            profilePluginsList.Add(currentPlugin);
                            Log($"- Added {currentPlugin.Name} to end of plugins.txt after {previousItem}");
                        }
                        else // Spliced
                        {
                            var spliceLog = new List<string>();
                            var previousItem = CommonFuncs.AddEntryInSplicedMode(profilePluginsList, sourcePluginsList, currentPlugin, spliceModeIgnoredPluginListings, StringType.Plugin, spliceLog);
                            Log(string.Join(Environment.NewLine, spliceLog.Select(x => "-- " + x).ToArray()));
                            Log($"- Spliced {currentPlugin.Name} into plugins.txt after {previousItem}");
                        }
                    }

                    if (_matchPluginActivationState)
                    {
                        var (enabledPlugins, disabledPlugins) = MatchPluginActivationStatus(profilePluginsList.Cast<PluginListing>().ToList(), sourcePluginsList.Cast<PluginListing>().ToList());
                        profileManifest.EnabledPlugins = enabledPlugins.Select(x => x.Name).ToList();
                        profileManifest.DisabledPlugins = disabledPlugins.Select(x => x.Name).ToList();

                        if (profileManifest.EnabledPlugins.Any())
                        {
                            Log("- Enabled the following plugins in the destination load order because they were enabled in the source load order:");
                            Log(string.Join(Environment.NewLine, profileManifest.EnabledPlugins.Select(x => "-- " + x).ToArray()));
                        }
                        if (profileManifest.DisabledPlugins.Any())
                        {
                            Log("- Disabled the following plugins in the destination load order because they were disabled in the source load order:");
                            Log(string.Join(Environment.NewLine, profileManifest.DisabledPlugins.Select(x => "-- " + x).ToArray()));
                        }
                    }
                    
                    // set missing plugin groups if needed
                    if (_interpolateMissingPluginGroups)
                    {
                        InterpolateMissingPluginGroups(profilePluginsList.Cast<PluginListing>().ToList(), validPlugins);
                    }

                    if (!CommonFuncs.SaveModList(profileModListPath, profileModList.Cast<ModListing>().ToList(), out var modExStr))
                    {
                        Log(modExStr);
                    }
                    if (!CommonFuncs.SavePluginList(profilePluginsListPath, profilePluginsList.Cast<PluginListing>().ToList(), false, out var pluginExStr))
                    {
                        Log(pluginExStr);
                    }

                    // Make the LoadOrder.txt file based on the new Plugins.txt file
                    var profileLoadOrderPath = Path.Combine(profileDir, "loadorder.txt");
                    if (!CommonFuncs.SavePluginList(profileLoadOrderPath, profilePluginsList.Cast<PluginListing>().ToList(), true, out var loadorderExStr))
                    {
                        Log(loadorderExStr);
                    }

                    // Now copy the valid mods into the mods folder

                    var copyTasks = new List<Task>();

                    foreach (var mod in validSourceMods)
                    {
                        var sourceModPath = Path.Combine(_modSourceDirectory, mod.SourceDirectoryName);
                        var destinationModPath = Path.Combine(modsOutputDir, mod.GetDestinationName());

                        if (Directory.Exists(sourceModPath) && !Directory.Exists(destinationModPath))
                        {
                            // Add the async copy task to the list
                            copyTasks.Add(Task.Run(async () =>
                            {
                                bool success = await FileOperation.CopyFolderWithUIAsync(sourceModPath, destinationModPath);
                                if (success)
                                {
                                    Log($"-- Copied mod {FormatHandler.TrimModActivationStatus(mod.DisplayName)} to {destinationModPath}");
                                }
                                else
                                {
                                    Log($"-- Failed to copy mod {FormatHandler.TrimModActivationStatus(mod.DisplayName)} to {destinationModPath}");
                                }
                            }));
                        }
                    }

                    // Wait for all copy tasks to complete
                    await Task.WhenAll(copyTasks);

                    profileManifest.AddedModNames.AddRange(validSourceMods.Select(x => x.GetDestinationName()));  
                    manifest.ProfileImports.Add(profileManifest);
                }

                SaveManifest(manifest);
                MessageBox.Show("Import completed successfully.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                ClosePopup();
            }
            catch (Exception ex)
            {
                Log($"An error occurred during the import process: {ex.Message}");
                ScrollableMessageBox.Show($"An error occurred during the import process: {ExceptionHelper.GetFilteredStackTrace(ex)}", "Error");
            }
        }

        private (List<ModListing>, List<ModListing>) MatchModActivationStatus(List<ModListing> profileModList, List<ModListing> sourceModList)
        {
            List<ModListing> disabledMods = new();
            List<ModListing> enabledMods = new();

            foreach (var mod in profileModList)
            {
                var sourceMod = sourceModList.FirstOrDefault(x => x.Equals(mod));
                if (sourceMod is not null && mod.Enabled != sourceMod.Enabled)
                {
                    if (sourceMod.Enabled.HasValue && sourceMod.Enabled.Value == true)
                    {
                        enabledMods.Add(mod);
                    }
                    else
                    {
                        disabledMods.Add(mod);
                    }
                    
                    mod.Enabled = sourceMod.Enabled;
                }
            }
            
            /* Deprecated
            foreach (var sourceMod in sourceModList.Where(x => x.Enabled.HasValue && x.Enabled == false))
            {
                var matchedMod = profileModList.FirstOrDefault(x => x.Name == sourceMod.Name && x.Enabled.HasValue && x.Enabled.Value == true);
                if (matchedMod != null)
                {
                    matchedMod.Disable();
                    disabledMods.Add(matchedMod);
                }
            }
            */
            return (enabledMods, disabledMods);
        }

        private (List<PluginListing>, List<PluginListing>) MatchPluginActivationStatus(List<PluginListing> profilePluginsList,
            List<PluginListing> sourcePluginList)
        {
            List<PluginListing> enabledPlugins = new();
            List<PluginListing> disabledPlugins = new();
            
            foreach (var plugin in profilePluginsList)
            {
                var sourcePlugin = sourcePluginList.FirstOrDefault(x => x.Equals(plugin));
                if (sourcePlugin is not null && plugin.Enabled != sourcePlugin.Enabled)
                {
                    if (sourcePlugin.Enabled.HasValue && sourcePlugin.Enabled.Value == true)
                    {
                        enabledPlugins.Add(plugin);
                    }
                    else
                    {
                        disabledPlugins.Add(plugin);
                    }
                }
            }
            
            return (enabledPlugins, disabledPlugins);
        }

        private List<PluginListing> DeletePluginsFromUncheckedMods(List<ModListing> disabledMods, List<PluginListing> pluginList, string modFolderPath)
        {
            List<PluginListing> deletedPlugins = new();

            foreach (var mod in disabledMods)
            {
                var modDir = Path.Combine(modFolderPath, mod.GetCurrentFolderName());
                if (Directory.Exists(modDir))
                {
                    var pluginPaths = CommonFuncs.GetPluginPathsInDir(modDir);
                    var pluginNames = pluginPaths.Select(x => Path.GetFileName(x) ?? string.Empty).ToList();

                    foreach (var pluginName in pluginNames)
                    {
                        var matchedPlugin = pluginList.FirstOrDefault(x => x.Name == pluginName);
                        if (matchedPlugin is not null)
                        {
                            deletedPlugins.Add(matchedPlugin);
                            pluginList.Remove(matchedPlugin);
                        }
                    }
                }
            }
            
            return deletedPlugins;
        }

        public void InterpolateMissingPluginGroups(List<PluginListing> profilePluginList, List<PluginListing> addedPlugins)
        {
            for (int i = 0; i < profilePluginList.Count; i++)
            {
                if (i == 0 || i == profilePluginList.Count - 1)
                {
                    continue;
                }
                
                var profilePlugin = profilePluginList[i];
                var previousPlugin = profilePluginList[i - 1];
                var nextPlugin = profilePluginList[i + 1];

                if (!addedPlugins.Any(x => x.Equals(profilePlugin))) // only modify plugins that were added
                {
                    continue;
                }

                if (string.IsNullOrEmpty(profilePlugin.PluginGroup) && 
                    !string.IsNullOrEmpty(previousPlugin.PluginGroup) && 
                    !string.IsNullOrEmpty(nextPlugin.PluginGroup) && 
                    previousPlugin.PluginGroup == nextPlugin.PluginGroup
                    )
                {
                    profilePlugin.PluginGroup = previousPlugin.PluginGroup;
                    Log($"-- Group Interpolation: Set {profilePlugin.Name} to: {previousPlugin.PluginGroup}");
                }
            }
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
                LogOperationEvent("Created backup directory at " +  currentBackupDir);

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
                        
                        // Backup plugingroups.txt
                        string pluginsGroupsFilePath = Path.Combine(profileDir, "plugingroups.txt");
                        if (File.Exists(pluginsGroupsFilePath))
                        {
                            File.Copy(pluginsGroupsFilePath, Path.Combine(profileBackupDir, "plugingroups.txt"), overwrite: true);
                        }

                        // Backup loadorder.txt
                        string loadOrderPath = Path.Combine(profileDir, "loadorder.txt");
                        if (File.Exists(loadOrderPath))
                        {
                            File.Copy(loadOrderPath, Path.Combine(profileBackupDir, "loadorder.txt"), overwrite: true);
                        }

                        // Backup modlist.txt
                        string modlistFilePath = Path.Combine(profileDir, "modlist.txt");
                        if (File.Exists(modlistFilePath))
                        {
                            File.Copy(modlistFilePath, Path.Combine(profileBackupDir, "modlist.txt"), overwrite: true);
                        }

                        LogOperationEvent("Backing up " + profileDir);
                    }
                }
            }
            catch (Exception ex)
            {
                ScrollableMessageBox.Show($"An error occurred during the backup process: {ExceptionHelper.GetFilteredStackTrace(ex)}", "Error");
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
            try
            {
                _logWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            }
            catch (Exception e)
            {
                
            }
            try
            {
                _importEvents.Add(message);
            }
            catch (Exception e)
            {
                
            }
        }

        private void CloseLog()
        {
            _logWriter?.Close();
        }

        private void LogOperationEvent(string message)
        {
            _importEvents.Add(message);
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

            var eventLogPath = Path.Combine(destinationPath, "ImportLog.txt");
            File.WriteAllText(eventLogPath, string.Join(Environment.NewLine, _importEvents));
        }
    }
}
