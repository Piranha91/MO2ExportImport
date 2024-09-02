using DynamicData;
using ReactiveUI;
using Splat.ModeDetection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace MO2ExportImport.ViewModels
{
    public class UndoOperationMenuViewModel : ReactiveObject
    {
        public ObservableCollection<ImportOperation> ImportOperations { get; } = new ObservableCollection<ImportOperation>();
        private string _currentOperationInfo;
        public string CurrentOperationInfo
        {
            get => _currentOperationInfo;
            set => this.RaiseAndSetIfChanged(ref _currentOperationInfo, value);
        }
        public ObservableCollection<string> AddedModNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SelectedMods { get; } = new ObservableCollection<string>();

        private ImportOperation _selectedOperation;
        private List<string> _operationNotes = new();

        public ReactiveCommand<ImportOperation, Unit> DeleteOperationCommand { get; }

        public ImportOperation SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedOperation, value);
                LoadModsForSelectedOperation();
            }
        }

        public ReactiveCommand<Unit, Unit> UndoCommand { get; }

        public bool IsUndoEnabled => SelectedMods.Any();

        public UndoOperationMenuViewModel()
        {
            LoadOperations();

            UndoCommand = ReactiveCommand.Create(UndoSelectedMods, this.WhenAnyValue(x => x.IsUndoEnabled));
            DeleteOperationCommand = ReactiveCommand.Create<ImportOperation>(DeleteOperation);
        }

        public void OnViewLoaded()
        {
            LoadOperations();
        }

        private void LoadOperations()
        {
            ImportOperations.Clear();
            // Get all subdirectories in the ImportManifests folder
            var importManifestDirs = Directory.GetDirectories("ImportManifests");

            foreach (var dir in importManifestDirs)
            {
                var manifestFilePath = Path.Combine(dir, "ImportManifest.json");

                // Check if the manifest file exists in the subdirectory
                if (File.Exists(manifestFilePath))
                {
                    var jsonString = File.ReadAllText(manifestFilePath);
                    var operation = JsonSerializer.Deserialize<ImportOperation>(jsonString);

                    if (operation != null)
                    {
                        operation.ThisFilePath = manifestFilePath;
                        ImportOperations.Add(operation);
                    }
                }
            }
        }

        private void DeleteOperation(ImportOperation operation)
        {
            if (operation == null)
                return;

            var Result = MessageBox.Show("This is NOT the Undo button. Only delete Import Logs after you've pressed the Undo button and have verified that the import has been reversed.", "Delete Import Log?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (Result == MessageBoxResult.No)
            {
                return;
            }

            List<string> errors = new();

            // Delete the file
            if (File.Exists(operation.ThisFilePath))
            {
                try
                {
                    File.Delete(operation.ThisFilePath);
                }
                catch (Exception e)
                {
                    errors.Add("Could not delete Operation Log at " + operation.ThisFilePath + Environment.NewLine + e.Message);                  
                }

                var parentFolder = Directory.GetParent(operation.ThisFilePath)?.FullName;
                if (parentFolder != null && Directory.Exists(parentFolder) && !Directory.GetFiles(parentFolder).Any())
                {
                    try
                    {
                        Directory.Delete(parentFolder);
                    }
                    catch (Exception e)
                    {
                        errors.Add("Could not delete folder " + parentFolder + Environment.NewLine + e.Message);
                    }
                }
            }

            // Remove the operation from the list
            ImportOperations.Remove(operation);

            if (SelectedOperation is null)
            {
                SelectedMods.Clear();
                AddedModNames.Clear();
            }

            if (errors.Any())
            {
                ScrollableMessageBox.Show(errors);
            }
        }

        private void LoadModsForSelectedOperation()
        {
            AddedModNames.Clear();
            CurrentOperationInfo = string.Empty;

            if (SelectedOperation != null)
            {
                CurrentOperationInfo =
                    "From: " + SelectedOperation.ModSourceDirName + Environment.NewLine +
                    "To: " + SelectedOperation.DestinationMO2Dir;

                foreach (var mod in SelectedOperation.AddedModNames)
                {
                    AddedModNames.Add(mod);
                }

                // By default, select all mods
                SelectedMods.Clear();
                foreach (var mod in AddedModNames)
                {
                    SelectedMods.Add(mod);
                }

                this.RaisePropertyChanged(nameof(IsUndoEnabled));
            }
        }

        private void UndoSelectedMods()
        {
            // Implement the logic to undo the selected mods
            _operationNotes.Clear();

            // First remove files
            foreach (var mod in SelectedMods)
            {
                var modDirPath = Path.Combine(SelectedOperation.DestinationMO2Dir, "mods", mod);

                if (Directory.Exists(modDirPath))
                {
                    try
                    {
                        Directory.Delete(modDirPath, true);
                    }
                    catch (Exception e)
                    {
                        _operationNotes.Add("Could not delete " + mod + " at: " + modDirPath + "\n" + e.Message);
                    }
                }
                else
                {
                    _operationNotes.Add("Could not remove " + mod + ". The path does not exist: " + modDirPath);
                }
            }

            // Then remove the reference to each plugin from each profile

            foreach (var profile in SelectedOperation.ProfileImports)
            {
                var profileName = profile.ProfileName;
                var profileDir = Path.Combine(SelectedOperation.DestinationMO2Dir, "profiles", profileName);
                if (Directory.Exists(profileDir))
                {
                    var profileModListPath = Path.Combine(profileDir, "modlist.txt");
                    var profileModList = CommonFuncs.LoadModList(profileModListPath);

                    var profilePluginsListPath = Path.Combine(profileDir, "plugins.txt");
                    var profilePluginsList = CommonFuncs.LoadPluginList(profilePluginsListPath);

                    foreach (var mod in profile.AddedModNames)
                    {
                        var associatedPlugins = profile.AddedPluginNames.Where(x => x.ParentMod == mod).Select(x => x.PluginName).ToList();

                        profileModList = profileModList.Where(x => FormatHandler.TrimModActivationStatus(x) != mod).ToList();
                        profilePluginsList = profilePluginsList.Where(x => !associatedPlugins.Contains(FormatHandler.TrimPluginActivationStatus(x))).ToList();
                    }

                    if (!CommonFuncs.SaveModList(profileModListPath, profileModList, out var modExStr))
                    {
                        _operationNotes.Add(modExStr);
                    }
                    if (!CommonFuncs.SavePluginList(profilePluginsListPath, profilePluginsList, out var pluginExStr))
                    {
                        _operationNotes.Add(pluginExStr);
                    }
                }
                else
                {
                    _operationNotes.Add("Could not edit profile " + profileName + ". Profile does not exist.");
                }
            }

            string caption = "Warning";
            if (!_operationNotes.Any())
            {
                _operationNotes.Add("Import has been undone");
                caption = "Success";
            }

            ScrollableMessageBox.Show(_operationNotes, caption);
        }
    }
}