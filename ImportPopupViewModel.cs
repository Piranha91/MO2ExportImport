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
        private readonly string _importSourceFolder;
        private readonly ObservableCollection<Mod> _selectedModList;
        private bool _isImportEnabled;
        private readonly ImportPopupView _view;
        private string _selectedProfile;
        private ImportMode _importMode;
        private string _anchorModName;
        private bool _addNoDeleteFlags;
        private bool _removeNoDeleteFlags;
        private bool _matchModActivationState;
        private bool _matchPluginActivationState;
        private StreamWriter _logWriter;
        private List<string> _importEvents = new();
        private string _programVersion;
        private string _importPrefix;
        private List<Mod> _removedModsMatchingExisting = new();
        private bool _ignoreMatchedModsForOrdering;
        private bool _interpolateMissingPluginGroups;
        private bool _transferDownloads;
        private bool _isSourceMo2Directory;

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

        private bool _showModListPreview;
        public bool ShowModListPreview
        {
            get => _showModListPreview;
            set => this.RaiseAndSetIfChanged(ref _showModListPreview, value);
        }

        public ReactiveCommand<Unit, Unit> CalculateSpaceCommand { get; }
        public ReactiveCommand<Unit, Unit> ImportCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public ImportPopupViewModel(ImportPopupView view, string mo2Directory, string modSourceDirectory, 
            string importProfileSourceDirectory, string selectedProfile, ObservableCollection<Mod> modList, 
            ImportMode importMode, bool addNoDeleteFlags, bool removeNoDeleteFlags, bool matchModActivationState, 
            bool matchPluginActivationState, StreamWriter logWriter, string programVersion, bool autoCalculateSpace, 
            string importPrefix, List<Mod> removedMods_Matching_Existing, bool IgnoreMatchedModsForOrdering, 
            bool interpolateMissingPluginGroups, bool transferDownloads, bool isSourceMo2Directory,
            string importSourceFolder, string anchorModName) 
        {
            _view = view;
            _mo2Directory = mo2Directory;
            _modSourceDirectory = modSourceDirectory;
            _importProfileSourceDirectory = importProfileSourceDirectory;
            _importSourceFolder = importSourceFolder;
            _selectedModList = modList;
            _selectedProfile = selectedProfile;
            _importMode = importMode;
            _anchorModName = anchorModName;
            _addNoDeleteFlags = addNoDeleteFlags;
            _removeNoDeleteFlags = removeNoDeleteFlags;
            _matchModActivationState = matchModActivationState;
            _matchPluginActivationState = matchPluginActivationState;
            _logWriter = logWriter;
            _programVersion = programVersion;
            _importPrefix = importPrefix;
            _removedModsMatchingExisting = removedMods_Matching_Existing;
            _ignoreMatchedModsForOrdering = IgnoreMatchedModsForOrdering;
            _transferDownloads = transferDownloads;
            _isSourceMo2Directory = isSourceMo2Directory;
            _interpolateMissingPluginGroups = interpolateMissingPluginGroups;

            CalculateSpaceCommand = ReactiveCommand.Create(CalculateSpace);
            ImportCommand = ReactiveCommand.Create(ImportMods, this.WhenAnyValue(x => x.IsImportEnabled));
            CancelCommand = ReactiveCommand.Create(ClosePopup);

            IsImportEnabled = false;

            if (autoCalculateSpace)
            {
                CalculateSpace();
            }
            
            ShowModListPreview = true; 
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

            var manifest = new ImportOperation(_modSourceDirectory, _mo2Directory, DateTime.Now, _programVersion, _importSourceFolder);

            try
            {
                var modsOutputDir = CommonFuncs.GetModsDirectory(_mo2Directory);  // CHANGED
                
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
                
                foreach (var profile in ProfilesToImport())
                {
                    var simulator = new ImportSimulatorViewModel();
                    
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
                    var sourcePluginsList = CommonFuncs.LoadPluginListFromLoadOrder(_importProfileSourceDirectory).Cast<IListing>().ToList();
                    
                    // Define and log mods whose position is ignored due to matched name
                    var spliceModeIgnoredModListings = new List<IListing>();
                    if (_ignoreMatchedModsForOrdering && _importMode == ImportMode.Spliced)
                    {
                        var candidateSpliceModeIgnoredModListings = _removedModsMatchingExisting
                            .Select(x => x.SourceListing).Cast<IListing>().ToList();
                        foreach (var candidate in candidateSpliceModeIgnoredModListings)
                        {
                            if (!HasSameRelativePosition(candidate, sourceModList, profileModList))
                            {
                                spliceModeIgnoredModListings.Add(candidate);
                                simulator.LogModEvent(candidate as ModListing, 
                                    "The position of this mod will be ignored during splicing because it is already in the destination mod list, potentially at a different position than in the source list.");
                            }
                        }
                    }

                    // Define and log plugins whose position is ignored due to matched parent mod name
                    var spliceModeIgnoredPluginListings= new List<IListing>();
                    if (_ignoreMatchedModsForOrdering && _importMode == ImportMode.Spliced)
                    {
                        var pluginsFromRemovedAlreadyExistingsMods = _removedModsMatchingExisting.SelectMany(x =>
                                CommonFuncs.GetPluginNamesInDir(Path.Combine(_modSourceDirectory, x.SourceDirectoryName)))
                            .ToList();
                        var candidateSpliceModeIgnoredPluginListings = sourcePluginsList.Where(x => pluginsFromRemovedAlreadyExistingsMods.Contains(x.Name)).ToList();
                        foreach (var candidate in candidateSpliceModeIgnoredPluginListings)
                        {
                            if (!HasSameRelativePosition(candidate, sourcePluginsList, profilePluginsList))
                            {
                                Log($"Plugin ordering: the position of {candidate.Name} will be disregarded when importing other plugins because it is already present in the destination load order");
                                simulator.LogPluginEvent(candidate as PluginListing, $"Position of this plugin will be disregarded for determining load order of other plugins");
                                var matchedPlugin = profilePluginsList.FirstOrDefault(x => x.Name == candidate.Name);
                                if (matchedPlugin != null)
                                {
                                    simulator.ReplacePluginListing(matchedPlugin as PluginListing);
                                }
                                
                                spliceModeIgnoredPluginListings.Add(candidate); // this plugin is not where the source mod list expects it to be in the load order, so don't use it to anchor spliced-in plugins.
                            }
                        }
                    }
                    
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
                                simulator.LogModEvent(mod, "Enabled because this mod is enabled in the imported mod list");
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

                            foreach (var mod in disabledMods)
                            {
                                simulator.LogModEvent(mod, "Disabled because this mod is disabled in the imported mod list");
                            }
                        }

                        profileManifest.DeletedPlugins = DeletePluginsFromUncheckedMods(disabledMods,
                            profilePluginsList.Cast<PluginListing>().ToList(), modsOutputDir);
                        if (profileManifest.DeletedPlugins.Any())
                        {
                            string deletedPlugins = "- Deleted the following plugins in profile " + profile + " because they were from mods that are disabled in the mod list being imported" + Environment.NewLine + string.Join(Environment.NewLine, profileManifest.DeletedPlugins.Select(x => "-- " + x.Name).ToArray());
                            Log(deletedPlugins);
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
                                    Log($"Plugin Import: {pluginFileName} from {mod.DisplayName} is already present in the destination load order so it will not be added as a new plugin.");
                                    simulator.LogPluginEvent(existingPluginListing as PluginListing, $"Detected as a member of {mod.DisplayName} but already present in destination load order");
                                    simulator.ReplacePluginListing(existingPluginListing as PluginListing);
                                    if (_ignoreMatchedModsForOrdering && _importMode == ImportMode.Spliced && !HasSameRelativePosition(existingPluginListing, sourcePluginsList, profilePluginsList))
                                    {
                                        Log($"Plugin ordering: the position of {pluginFileName} will be disregarded when importing other plugins because it is already present in the destination load order");
                                        simulator.LogPluginEvent(existingPluginListing as PluginListing, $"Position of this plugin will be disregarded for determining load order of other plugins");
                                        spliceModeIgnoredPluginListings.Add(existingPluginListing); // this plugin is not where the source mod list expects it to be in the load order, so don't use it to anchor spliced-in plugins.
                                    }
                                    continue;
                                }

                                var matchedSourcePlugin = sourcePluginsList.FirstOrDefault(x => x.Name == pluginFileName);
                                if (matchedSourcePlugin is PluginListing match)
                                {
                                    var alreadyAddedPlugin = validPlugins.FirstOrDefault(x => x.Equals(match));
                                    if (alreadyAddedPlugin is null) // don't add the same plugin multiple times (e.g. from override mods)
                                    {
                                        validPlugins.Add(match);
                                        simulator.LogPluginEvent(match, "Importing from mod: " + mod.DisplayName);
                                    }
                                    else
                                    {
                                        Log($"Plugin Import: {pluginFileName} from {mod.DisplayName} is being skipped for import because another imported mod is already supplying this plugin.");
                                        simulator.LogPluginEvent(match, "Is also present in mod: " + mod.DisplayName);
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
                            simulator.LogPluginEvent(plugin, "Enabled because this plugin is enabled in the imported mod list");
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

                    foreach (var plugin in validPlugins)
                    {
                        var sourceListing = sourcePluginsList.FirstOrDefault(x => x.Equals(plugin));
                        if (sourceListing is null)
                        {
                            continue;
                            
                        }
                        
                        var index = sourcePluginsList.IndexOf(sourceListing);
                        
                        if (index > 0)
                        {
                            var precedingPlugin = sourcePluginsList[index - 1];
                            simulator.LogPluginEvent(sourceListing as PluginListing, "The preceding plugin in the source load order is: " + precedingPlugin.Name);
                        }
                        else
                        {
                            simulator.LogPluginEvent(sourceListing as PluginListing, "This is the first plugin in the import source load order");
                        }
                        
                        if (index < sourcePluginsList.Count - 1)
                        {
                            var subsequentPlugin = sourcePluginsList[index + 1];
                            simulator.LogPluginEvent(sourceListing as PluginListing, "The subsequent plugin in the source load order is: " + subsequentPlugin.Name);
                        }
                        else
                        {
                            simulator.LogPluginEvent(sourceListing as PluginListing, "This is the last plugin in the import source load order");
                        }
                    }
                    
                    // Handle ImportMode for modlist.txt
                    Log("Importing mods into modlist.txt");
                    
                    // Track insertion index for Beginning, Before, and After modes
                    int insertionIndex = 0;
                    bool useInsertionIndex = false;
                    
                    // Determine initial insertion index based on mode
                    if (_importMode == ImportMode.Beginning)
                    {
                        insertionIndex = 0;
                        useInsertionIndex = true;
                        Log("Beginning mode: Will insert mods at the beginning of modlist.txt");
                    }
                    else if (_importMode == ImportMode.Before && !string.IsNullOrEmpty(_anchorModName))
                    {
                        insertionIndex = profileModList.FindIndex(m => 
                            FormatHandler.TrimModActivationStatus(m.Name).Equals(_anchorModName, StringComparison.OrdinalIgnoreCase));
    
                        if (insertionIndex >= 0)
                        {
                            useInsertionIndex = true;
                            Log($"Before mode: Will insert mods before '{_anchorModName}' (index {insertionIndex})");
                        }
                        else
                        {
                            Log($"Warning: Anchor mod '{_anchorModName}' not found. Falling back to End mode.");
                        }
                    }
                    else if (_importMode == ImportMode.After && !string.IsNullOrEmpty(_anchorModName))
                    {
                        insertionIndex = profileModList.FindIndex(m => 
                            FormatHandler.TrimModActivationStatus(m.Name).Equals(_anchorModName, StringComparison.OrdinalIgnoreCase));
    
                        if (insertionIndex >= 0)
                        {
                            insertionIndex++; // Insert after means one position later
                            useInsertionIndex = true;
                            Log($"After mode: Will insert mods after '{_anchorModName}' (index {insertionIndex})");
                        }
                        else
                        {
                            Log($"Warning: Anchor mod '{_anchorModName}' not found. Falling back to End mode.");
                        }
                    }
                    
                    foreach (var currentMod in validSourceMods)
                    {
                        var sourceListing = sourceModList.FirstOrDefault(x => x.Equals(currentMod.SourceListing));
                        if (sourceListing is null)
                        {
                            continue;
                        }
                        
                        var index = sourceModList.IndexOf(sourceListing);
                        
                        if (index > 0)
                        {
                            var precedingMod = sourceModList[index - 1];
                            simulator.LogModEvent(sourceListing as ModListing, "The preceding mod in the source load order is: " + precedingMod.Name);
                        }
                        else
                        {
                            simulator.LogModEvent(sourceListing as ModListing, "This is the first mod in the import source load order");
                        }
                        
                        if (index < sourceModList.Count - 1)
                        {
                            var subsequentMod = sourceModList[index + 1];
                            simulator.LogModEvent(sourceListing as ModListing, "The subsequent mod in the source load order is: " + subsequentMod.Name);
                        }
                        else
                        {
                            simulator.LogModEvent(sourceListing as ModListing, "This is the last mod in the import source load order");
                        }
                        
                        // INSERT THE MOD BASED ON MODE
                        if (_importMode == ImportMode.End)
                        {
                            var previousItem = profileModList.LastOrDefault()?.Name ?? "start";
                            profileModList.Add(currentMod.SourceListing);
                            simulator.LogModEvent(currentMod.SourceListing, "Added to mod list after " + previousItem + ".");
                            Log($"- Added {FormatHandler.TrimModActivationStatus(currentMod.DisplayName)} to end of modlist.txt after {previousItem}");
                        }
                        else if (useInsertionIndex) // Beginning, Before, or After mode
                        {
                            string modeDescription = _importMode == ImportMode.Beginning ? "at beginning" :
                                _importMode == ImportMode.Before ? $"before {_anchorModName}" :
                                $"after {_anchorModName}";
        
                            profileModList.Insert(insertionIndex, currentMod.SourceListing);
                            simulator.LogModEvent(currentMod.SourceListing, $"Added to mod list {modeDescription} (index {insertionIndex}).");
                            Log($"- Added {FormatHandler.TrimModActivationStatus(currentMod.DisplayName)} {modeDescription} at index {insertionIndex}");
        
                            // INCREMENT the insertion index for the next mod
                            insertionIndex++;
                        }
                        else // Spliced mode (or fallback)
                        {
                            var spliceLog = new List<string>();
                            var previousItem = CommonFuncs.AddEntryInSplicedMode(profileModList, sourceModList, currentMod.SourceListing, spliceModeIgnoredModListings, StringType.Mod, spliceLog);
                            foreach (var entry in spliceLog)
                            {
                                simulator.LogModEvent(currentMod.SourceListing, entry);
                            }
                            Log(string.Join(Environment.NewLine, spliceLog.Select(x => "-- " + x).ToArray()));
                            Log($"- Spliced {FormatHandler.TrimModActivationStatus(currentMod.DisplayName)} into modlist.txt after {previousItem}");
                            simulator.LogModEvent(currentMod.SourceListing, "Added to mod list after " + previousItem + "."); 
                        }
                        
                        simulator.LogModEvent(currentMod.SourceListing, Environment.NewLine + "The current mod order is: " + Environment.NewLine + string.Join(Environment.NewLine, profileModList.Select(x => x.Name)));
                    }

                    // Handle ImportMode for plugins.txt
                    Log("Importing plugins into plugins.txt");
                    
                    // Track insertion index for Beginning, Before, and After modes
                    int pluginInsertionIndex = 0;
                    bool usePluginInsertionIndex = false;

                    // Determine initial insertion index based on mode
                    if (_importMode == ImportMode.Beginning)
                    {
                        pluginInsertionIndex = 0;
                        usePluginInsertionIndex = true;
                        Log("Beginning mode: Will insert plugins at the beginning of plugins.txt");
                    }
                    else if (_importMode == ImportMode.Before && !string.IsNullOrEmpty(_anchorModName))
                    {
                        var result = FindPluginAnchorForBeforeMode(profilePluginsList, profileModList, _anchorModName);
    
                        if (result.Index >= 0)
                        {
                            pluginInsertionIndex = result.Index;
                            usePluginInsertionIndex = true;
                            Log($"Before mode: Will insert plugins before first plugin from '{result.ModName}' (index {pluginInsertionIndex})");
                        }
                        else
                        {
                            Log($"Warning: No suitable anchor mod with plugins found. Falling back to Beginning mode.");
                            pluginInsertionIndex = 0;
                            usePluginInsertionIndex = true;
                        }
                    }
                    else if (_importMode == ImportMode.After && !string.IsNullOrEmpty(_anchorModName))
                    {
                        var result = FindPluginAnchorForAfterMode(profilePluginsList, profileModList, _anchorModName);
    
                        if (result.Index >= 0)
                        {
                            pluginInsertionIndex = result.Index;
                            usePluginInsertionIndex = true;
                            Log($"After mode: Will insert plugins after last plugin from '{result.ModName}' (index {pluginInsertionIndex})");
                        }
                        else
                        {
                            Log($"Warning: No suitable anchor mod with plugins found. Falling back to End mode.");
                            // Keep default behavior (append to end)
                        }
                    }
                    
                    // Import each plugin
                    foreach (var currentPlugin in validPlugins)
                    {
                        if (_importMode == ImportMode.End)
                        {
                            var previousItem = profilePluginsList.LastOrDefault()?.Name ?? "start";
                            profilePluginsList.Add(currentPlugin);
                            simulator.LogPluginEvent(currentPlugin, "Added to plugin list after " + previousItem + ".");
                            Log($"- Added {currentPlugin.Name} to end of plugins.txt after {previousItem}");
                        }
                        else if (usePluginInsertionIndex) // Beginning, Before, or After mode
                        {
                            string modeDescription = _importMode == ImportMode.Beginning ? "at beginning" :
                                _importMode == ImportMode.Before ? $"before plugins from {_anchorModName}" :
                                $"after plugins from {_anchorModName}";

                            profilePluginsList.Insert(pluginInsertionIndex, currentPlugin);
                            simulator.LogPluginEvent(currentPlugin,
                                $"Added to plugin list {modeDescription} (index {pluginInsertionIndex}).");
                            Log($"- Added {currentPlugin.Name} {modeDescription} at index {pluginInsertionIndex}");

                            // INCREMENT the insertion index for the next plugin
                            pluginInsertionIndex++;
                        }
                        else // Spliced mode (or fallback)
                        {
                            var spliceLog = new List<string>();
                            var previousItem = CommonFuncs.AddEntryInSplicedMode(profilePluginsList, sourcePluginsList,
                                currentPlugin, spliceModeIgnoredPluginListings, StringType.Plugin, spliceLog);
                            foreach (var entry in spliceLog)
                            {
                                simulator.LogPluginEvent(currentPlugin, entry);
                            }

                            Log(string.Join(Environment.NewLine, spliceLog.Select(x => "-- " + x).ToArray()));
                            Log($"- Spliced {currentPlugin.Name} into plugins.txt after {previousItem}");
                            simulator.LogPluginEvent(currentPlugin, "Added to plugin list after " + previousItem + ".");
                        }

                        simulator.LogPluginEvent(currentPlugin,
                            Environment.NewLine + "The current load order is: " + Environment.NewLine +
                            string.Join(Environment.NewLine, profilePluginsList.Select(x => x.Name)));
                    }

                    if (_matchPluginActivationState)
                    {
                        var (enabledPlugins, disabledPlugins) = MatchPluginActivationStatus(profilePluginsList.Cast<PluginListing>().ToList(), sourcePluginsList.Cast<PluginListing>().ToList(), simulator);
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
                        InterpolateMissingPluginGroups(profilePluginsList.Cast<PluginListing>().ToList(), validPlugins, simulator);
                    }

                    if (ShowModListPreview)
                    {
                        simulator.SetModSourceDirectory(_modSourceDirectory);
                        simulator.Initialize(profilePluginsList.Cast<PluginListing>(), validPlugins, profileModList.Cast<ModListing>(), true, profile);
                        simulator.ShowWindow();
                        if (simulator.CancelImport)
                        {
                            return;
                        }
                        else
                        {
                            profileModList = simulator.GetModListings().ToList();
                            profilePluginsList = simulator.GetPluginListings().ToList();
                        }
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
                    
                    profileManifest.AddedModNames.AddRange(validSourceMods.Select(x => x.GetDestinationName()));  
                    manifest.ProfileImports.Add(profileManifest);
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
                
                if (_transferDownloads)
                {
                    try
                    {
                        Log("Beginning download transfer process...");
        
                        var sourceDownloadDir = DownloadTransferHelper.ResolveSourceDownloadDirectory(
                            _importProfileSourceDirectory, _modSourceDirectory, _isSourceMo2Directory);
        
                        if (string.IsNullOrEmpty(sourceDownloadDir))
                        {
                            Log("Download transfer cancelled - could not determine source downloads directory");
                        }
                        else
                        {
                            var destDownloadDir = DownloadTransferHelper.ResolveDestinationDownloadDirectory(_mo2Directory);
            
                            if (string.IsNullOrEmpty(destDownloadDir))
                            {
                                Log("Download transfer cancelled - could not determine destination downloads directory");
                            }
                            else if (string.Equals(sourceDownloadDir, destDownloadDir, StringComparison.OrdinalIgnoreCase))
                            {
                                Log("Source and destination downloads directories are the same - skipping download transfer");
                            }
                            else
                            {
                                Log($"Transferring downloads from: {sourceDownloadDir}");
                                Log($"Transferring downloads to: {destDownloadDir}");
                
                                await TransferDownloads(validSourceMods, sourceDownloadDir, destDownloadDir, manifest);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error during download transfer: {ex.Message}");
                        ScrollableMessageBox.Show($"Error transferring downloads: {ExceptionHelper.GetFilteredStackTrace(ex)}", "Warning");
                    }
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
        
        private (int Index, string ModName) FindPluginAnchorForBeforeMode(List<IListing> pluginsList, List<IListing> modList, string anchorModName)
        {
            // Find the anchor mod's position in the mod list
            int anchorModIndex = modList.FindIndex(m => 
                FormatHandler.TrimModActivationStatus(m.Name).Equals(anchorModName, StringComparison.OrdinalIgnoreCase));
            
            if (anchorModIndex < 0)
                return (-1, null);

            // Check if anchor mod has plugins
            int firstPluginIndex = FindFirstPluginFromMod(pluginsList, anchorModName);
            if (firstPluginIndex >= 0)
            {
                return (firstPluginIndex, anchorModName);
            }

            // Anchor mod has no plugins - search backwards for a mod with plugins
            for (int i = anchorModIndex - 1; i >= 0; i--)
            {
                string candidateModName = FormatHandler.TrimModActivationStatus(modList[i].Name);
                int candidateFirstPlugin = FindFirstPluginFromMod(pluginsList, candidateModName);
                
                if (candidateFirstPlugin >= 0)
                {
                    // Found a mod with plugins - use its first plugin as anchor
                    Log($"-- Anchor mod '{anchorModName}' has no plugins. Using first plugin from preceding mod '{candidateModName}'");
                    return (candidateFirstPlugin, candidateModName);
                }
            }

            // No suitable anchor found - return -1 to trigger fallback to Beginning
            return (-1, null);
        }

        private (int Index, string ModName) FindPluginAnchorForAfterMode(List<IListing> pluginsList, List<IListing> modList, string anchorModName)
        {
            // Find the anchor mod's position in the mod list
            int anchorModIndex = modList.FindIndex(m => 
                FormatHandler.TrimModActivationStatus(m.Name).Equals(anchorModName, StringComparison.OrdinalIgnoreCase));
            
            if (anchorModIndex < 0)
                return (-1, null);

            // Check if anchor mod has plugins
            int lastPluginIndex = FindLastPluginFromMod(pluginsList, anchorModName);
            if (lastPluginIndex >= 0)
            {
                return (lastPluginIndex + 1, anchorModName);
            }

            // Anchor mod has no plugins - search forwards for a mod with plugins
            for (int i = anchorModIndex + 1; i < modList.Count; i++)
            {
                string candidateModName = FormatHandler.TrimModActivationStatus(modList[i].Name);
                int candidateLastPlugin = FindLastPluginFromMod(pluginsList, candidateModName);
                
                if (candidateLastPlugin >= 0)
                {
                    // Found a mod with plugins - use position after its last plugin
                    Log($"-- Anchor mod '{anchorModName}' has no plugins. Using last plugin from subsequent mod '{candidateModName}'");
                    return (candidateLastPlugin + 1, candidateModName);
                }
            }

            // No suitable anchor found - return -1 to trigger fallback to End
            return (-1, null);
        }

        private int FindFirstPluginFromMod(List<IListing> pluginsList, string modName)
        {
            // Get the mod directory path using _mo2Directory which is available in ImportPopupViewModel
            var modPath = Path.Combine(_mo2Directory, "mods", modName);

            if (!Directory.Exists(modPath))
                return -1;

            // Get all plugin files from the mod
            var modPlugins = Directory.GetFiles(modPath, "*.esp", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(modPath, "*.esm", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(modPath, "*.esl", SearchOption.TopDirectoryOnly))
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find the first plugin in the load order that belongs to this mod
            for (int i = 0; i < pluginsList.Count; i++)
            {
                if (modPlugins.Contains(pluginsList[i].Name))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindLastPluginFromMod(List<IListing> pluginsList, string modName)
        {
            // Get the mod directory path using _mo2Directory which is available in ImportPopupViewModel
            var modPath = Path.Combine(_mo2Directory, "mods", modName);

            if (!Directory.Exists(modPath))
                return -1;

            // Get all plugin files from the mod
            var modPlugins = Directory.GetFiles(modPath, "*.esp", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(modPath, "*.esm", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(modPath, "*.esl", SearchOption.TopDirectoryOnly))
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find the last plugin in the load order that belongs to this mod
            int lastIndex = -1;
            for (int i = 0; i < pluginsList.Count; i++)
            {
                if (modPlugins.Contains(pluginsList[i].Name))
                {
                    lastIndex = i;
                }
            }

            return lastIndex;
        }

        private async Task TransferDownloads(List<Mod> mods, string sourceDownloadDir, string destDownloadDir,
            ImportOperation manifest)
        {
            // First, collect all expected downloads and check which ones are missing
            var missingDownloads = new List<string>();
            var downloadsToTransfer =
                new List<(Mod mod, string sourceFilePath, string destFilePath, string installationFile, bool hasDownload
                    , bool hasMeta)>();

            foreach (var mod in mods)
            {
                var modDirectory = Path.Combine(_modSourceDirectory, mod.SourceDirectoryName);
                var installationFile = DownloadTransferHelper.GetInstallationFileFromMeta(modDirectory);

                if (string.IsNullOrEmpty(installationFile))
                {
                    Log($"-- No installation file found for {mod.DisplayName}");
                    continue;
                }

                var sourceFilePath = Path.Combine(sourceDownloadDir, installationFile);
                var sourceMetaPath = sourceFilePath + ".meta";
                var destFilePath = Path.Combine(destDownloadDir, installationFile);
                var destMetaPath = destFilePath + ".meta";

                bool downloadExists = File.Exists(sourceFilePath);
                bool metaExists = File.Exists(sourceMetaPath);

                // Check what's missing
                if (!downloadExists && !metaExists)
                {
                    missingDownloads.Add($"{mod.DisplayName}: {sourceFilePath} (and .meta)");
                    Log($"-- Download and meta not found: {installationFile}");
                    continue;
                }
                else if (!downloadExists)
                {
                    missingDownloads.Add($"{mod.DisplayName}: {sourceFilePath}");
                }
                else if (!metaExists)
                {
                    missingDownloads.Add($"{mod.DisplayName}: {sourceMetaPath}");
                }

                // Check if already exists in destination
                bool destDownloadExists = File.Exists(destFilePath);
                bool destMetaExists = File.Exists(destMetaPath);

                if (destDownloadExists && destMetaExists)
                {
                    Log($"-- Download and meta already exist: {installationFile}");
                    continue;
                }
                else if (destDownloadExists && !metaExists)
                {
                    Log($"-- Download already exists: {installationFile}");
                    continue;
                }
                else if (destMetaExists && !downloadExists)
                {
                    Log($"-- Meta already exists: {installationFile}.meta");
                    continue;
                }

                downloadsToTransfer.Add((mod, sourceFilePath, destFilePath, installationFile, downloadExists,
                    metaExists));
            }

            // If there are missing downloads, show them to the user
            if (missingDownloads.Any())
            {
                var message = new List<string>
                {
                    "The following download files could not be found:",
                    ""
                };
                message.AddRange(missingDownloads);
                message.Add("");
                message.Add($"Available downloads will still be transferred ({downloadsToTransfer.Count} mod(s)).");

                ScrollableMessageBox.Show(message, "Missing Downloads");
            }

            // Now transfer the downloads that were found
            var downloadTasks = new List<Task>();

            foreach (var (mod, sourceFilePath, destFilePath, installationFile, hasDownload, hasMeta) in
                     downloadsToTransfer)
            {
                downloadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Transfer the download file if it exists and destination doesn't have it
                        if (hasDownload && !File.Exists(destFilePath))
                        {
                            await FileOperation.CopyFileWithUIAsync(sourceFilePath, destFilePath);
                            Log($"-- Transferred download: {installationFile}");

                            manifest.TransferredDownloads.Add(new TransferredDownload
                            {
                                FileName = installationFile,
                                DestinationPath = destFilePath
                            });
                        }

                        // Transfer the .meta file if it exists and destination doesn't have it
                        if (hasMeta && !File.Exists(destFilePath + ".meta"))
                        {
                            await FileOperation.CopyFileWithUIAsync(sourceFilePath + ".meta", destFilePath + ".meta");
                            Log($"-- Transferred meta: {installationFile}.meta");

                            manifest.TransferredDownloads.Add(new TransferredDownload
                            {
                                FileName = installationFile + ".meta",
                                DestinationPath = destFilePath + ".meta"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"-- Failed to transfer {installationFile}: {ex.Message}");
                    }
                }));
            }

            await Task.WhenAll(downloadTasks);
            Log($"Download transfer complete. Transferred {manifest.TransferredDownloads.Count} files.");
        }

        private bool HasSameRelativePosition(IListing listing, List<IListing> list1, List<IListing> list2)
        {
            var index1 = list1.IndexOf(listing);
            var index2 = list2.IndexOf(listing);

            if (index1 < 0 || index2 < 0)
            {
                return false;
            }

            string precedingListingName1;
            string precedingListingName2;

            string firstStr = "!!first!!";
                        
            if (index1 > 0)
            {
                precedingListingName1 = list1[index1 - 1].Name;
            }
            else
            {
                precedingListingName1 = firstStr;
            }
                        
            if (index2 > 0)
            {
                precedingListingName2 = list2[index2 - 1].Name;
            }
            else
            {
                precedingListingName2 = firstStr;
            }
            
            return precedingListingName1 == precedingListingName2;
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
            List<PluginListing> sourcePluginList, ImportSimulatorViewModel simulator)
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
                        plugin.Enabled = true;
                        simulator.LogPluginEvent(plugin, "Enabled because this plugin is enabled in the imported load order");
                        enabledPlugins.Add(plugin);
                    }
                    else
                    {
                        plugin.Enabled = false;
                        simulator.LogPluginEvent(plugin, "Disabled because this plugin is disabled in the imported load order");
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

        public void InterpolateMissingPluginGroups(List<PluginListing> profilePluginList, List<PluginListing> addedPlugins, ImportSimulatorViewModel simulator)
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
                    simulator.LogPluginEvent(profilePlugin, "Interpolated plugin group to: " + profilePlugin.PluginGroup);
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
                var profilesPath = CommonFuncs.GetProfilesDirectory(_mo2Directory);  // ADDED
        
                if (_selectedProfile == "All")
                {
                    profilesToBackup.AddRange(Directory.GetDirectories(profilesPath).Select(Path.GetFileName));  // CHANGED
                }
                else
                {
                    profilesToBackup.Add(_selectedProfile);
                }

                foreach (var profile in profilesToBackup)
                {
                    string profileDir = Path.Combine(profilesPath, profile);  // CHANGED
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
            var profilesPath = CommonFuncs.GetProfilesDirectory(_mo2Directory);  // ADDED
    
            if (_selectedProfile == "All")
            {
                return Directory.GetDirectories(profilesPath).Select(Path.GetFileName);  // CHANGED
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
