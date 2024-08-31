using MO2ExportImport.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Alphaleonis.Win32.Filesystem;

namespace MO2ExportImport.ViewModels
{
    public class ExportPopupViewModel : ReactiveObject
    {
        private readonly string _mo2Directory;
        private readonly IEnumerable<Mod> _selectedMods;
        private readonly string _selectedProfile;
        private string _exportDestinationRootFolder;
        private string _spaceInfo;
        private Brush _spaceInfoColor;
        private bool _isExportEnabled;
        private readonly ExportPopupView _window;
        private readonly ExportViewModel _exportViewModel;
        private readonly string _programVersion;

        private string _exportDestinationFolderName;
        public string ExportDestinationFolderName
        {
            get => _exportDestinationFolderName;
            set => this.RaiseAndSetIfChanged(ref _exportDestinationFolderName, value);
        }

        public string SpaceInfo
        {
            get => _spaceInfo;
            set => this.RaiseAndSetIfChanged(ref _spaceInfo, value);
        }

        public Brush SpaceInfoColor
        {
            get => _spaceInfoColor;
            set => this.RaiseAndSetIfChanged(ref _spaceInfoColor, value);
        }

        public bool IsExportEnabled
        {
            get => _isExportEnabled;
            set => this.RaiseAndSetIfChanged(ref _isExportEnabled, value);
        }

        public ReactiveCommand<Unit, Unit> CalculateSpaceCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportModsAndListCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportListCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public ExportPopupViewModel(ExportPopupView window, ExportViewModel exportViewModel, string mo2Directory, IEnumerable<Mod> selectedMods, string exportDestinationFolder, string selectedProfile, string programVersion)
        {
            _window = window;
            _exportViewModel = exportViewModel;

            _mo2Directory = mo2Directory;
            _selectedMods = selectedMods;
            _selectedProfile = selectedProfile;
            _exportDestinationRootFolder = exportDestinationFolder;
            _programVersion = programVersion;

            CalculateSpaceCommand = ReactiveCommand.Create(CalculateSpace);
            ExportModsAndListCommand = ReactiveCommand.CreateFromTask(ExportModsAndList);
            ExportListCommand = ReactiveCommand.CreateFromTask(ExportList);
            CancelCommand = ReactiveCommand.Create(Cancel);

            // Initially disable the Export button
            IsExportEnabled = false;

            ExportDestinationFolderName = DateTime.Now.ToString("yyyy MM dd (HH mm)");
        }

        private void CalculateSpace()
        {
            long totalSize = 0;

            // Calculate the total size of the selected mod folders
            foreach (var mod in _selectedMods)
            {
                var modPath = System.IO.Path.Combine(_mo2Directory, "mods", mod.ListName);
                if (System.IO.Directory.Exists(modPath))
                {
                    totalSize += System.IO.Directory.EnumerateFiles(modPath, "*", SearchOption.AllDirectories)
                                          .Sum(f => new System.IO.FileInfo(f).Length);
                }
            }

            // Determine the root drive of the export destination folder
            var exportRoot = System.IO.Path.GetPathRoot(_exportDestinationRootFolder);
            var drive = new System.IO.DriveInfo(exportRoot);

            // Convert sizes to GB or MB
            double totalSizeGB = totalSize / (1024.0 * 1024.0 * 1024.0);
            double availableSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

            string totalSizeDisplay = totalSizeGB >= 1 ? $"{totalSizeGB:F2} GB" : $"{totalSize / (1024.0 * 1024.0):F2} MB";
            string availableSpaceDisplay = availableSpaceGB >= 1 ? $"{availableSpaceGB:F2} GB" : $"{drive.AvailableFreeSpace / (1024.0 * 1024.0):F2} MB";

            if (totalSizeGB > availableSpaceGB)
            {
                SpaceInfo = $"Total size: {totalSizeDisplay}\nAvailable space: {availableSpaceDisplay}\nThere is not enough space on the drive for export.";
                SpaceInfoColor = Brushes.Red;
                IsExportEnabled = false;
            }
            else
            {
                SpaceInfo = $"Total size: {totalSizeDisplay}\nAvailable space: {availableSpaceDisplay}\nThere is enough space on the drive for export.";
                SpaceInfoColor = Brushes.Green;
                IsExportEnabled = true;
            }
        }

        private async Task ExportModsAndList()
        {
            var pluginsSourcePath = System.IO.Path.Combine(_mo2Directory, "profiles", _selectedProfile, "plugins.txt");
            var modlistSourcePath = System.IO.Path.Combine(_mo2Directory, "profiles", _selectedProfile, "modlist.txt");

            (string exportFolderPath, bool isMergeOperation) = CreateExportFolder();

            var exportLog = new
            {
                DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ProgramVersion = _programVersion
            };

            var exportLogJson = JsonSerializer.Serialize(exportLog, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(System.IO.Path.Combine(exportFolderPath, "ExportLog.json"), exportLogJson);

            var copyTasks = new List<Task>();

            // Overwrite plugins.txt and modlist.txt
            copyTasks.Add(Task.Run(() => System.IO.File.Copy(pluginsSourcePath, System.IO.Path.Combine(exportFolderPath, "plugins.txt"), true)));
            copyTasks.Add(Task.Run(() => System.IO.File.Copy(modlistSourcePath, System.IO.Path.Combine(exportFolderPath, "modlist.txt"), true)));

            // Copy other files
            foreach (var mod in _selectedMods)
            {
                var modSourcePath = System.IO.Path.Combine(_mo2Directory, "mods", mod.ListName);
                var modDestinationPath = System.IO.Path.Combine(exportFolderPath, mod.ListName);

                if (!Alphaleonis.Win32.Filesystem.Directory.Exists(modDestinationPath))
                {
                    copyTasks.Add(FileOperation.CopyFolderWithUIAsync(modSourcePath, modDestinationPath));
                }
            }

            // Wait for all copy tasks to complete
            try
            {
                await Task.WhenAll(copyTasks);
            }
            catch (IOException ex)
            {
                // Log or display error message
                MessageBox.Show($"Error during copy operation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            string renameMessage = string.Empty;

            // Attempt to rename the directory
            if (isMergeOperation)
            {
                renameMessage = TryRenameDirectory(exportFolderPath);
            }

            MessageBox.Show($"Export completed successfully.{renameMessage}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            _window.Close();
        }


        private async Task ExportList()
        {
            var selectedModsToExport = _selectedMods
                .Where(mod => mod.SelectedInUI && (!_exportViewModel.IgnoreDisabled || mod.EnabledInMO2) && (!_exportViewModel.IgnoreSeparators || !mod.IsSeparator))
                .ToList();

            (string exportFolderPath, bool isMergeOperation) = CreateExportFolder();

            // Create the JSON file with mod paths
            var exportData = new
            {
                ModsRootPath = System.IO.Path.Combine(_mo2Directory, "mods"),
                SelectedMods = selectedModsToExport
            };

            var jsonFilePath = System.IO.Path.Combine(exportFolderPath, "modlist.json");
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(jsonFilePath, json);

            await CopyModAndPluginListFiles(exportFolderPath);

            MessageBox.Show($"Export completed successfully.", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);

            _window.Close();
        }

        private (string, bool) CreateExportFolder()
        {
            bool isMergeOperation = System.IO.File.Exists(System.IO.Path.Combine(_exportDestinationRootFolder, "ExportLog.json"));
            string exportFolderPath = _exportDestinationRootFolder;

            if (!isMergeOperation)
            {
                exportFolderPath = System.IO.Path.Combine(_exportDestinationRootFolder, ExportDestinationFolderName);
                Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(exportFolderPath);
            }

            return (exportFolderPath, isMergeOperation);
        }

        private async Task CopyModAndPluginListFiles(string exportFolderPath)
        {
            var profilePath = System.IO.Path.Combine(_mo2Directory, "profiles", _selectedProfile);
            var modlistSourcePath = System.IO.Path.Combine(profilePath, "modlist.txt");
            var pluginsSourcePath = System.IO.Path.Combine(profilePath, "plugins.txt");

            var copyTasks = new List<Task>();

            // Overwrite plugins.txt and modlist.txt
            copyTasks.Add(FileOperation.CopyFileWithUIAsync(pluginsSourcePath, System.IO.Path.Combine(exportFolderPath, "plugins.txt")));
            copyTasks.Add(FileOperation.CopyFileWithUIAsync(modlistSourcePath, System.IO.Path.Combine(exportFolderPath, "modlist.txt")));

            // Wait for all copy tasks to complete
            try
            {
                await Task.WhenAll(copyTasks);
            }
            catch (IOException ex)
            {
                // Log or display error message
                MessageBox.Show($"Error during copy operation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string TryRenameDirectory(string exportFolderPath)
        {
            var newExportFolderPath = System.IO.Path.Combine(_exportDestinationRootFolder, ExportDestinationFolderName);
            string renameMessage = string.Empty;
            try
            {
                Alphaleonis.Win32.Filesystem.Directory.Move(exportFolderPath, newExportFolderPath);
                exportFolderPath = newExportFolderPath;
                renameMessage = "\n\nDirectory renamed successfully.";
            }
            catch (IOException ex)
            {
                renameMessage = $"\n\nThe directory could not be renamed due to a file access error: {ex.Message}";
                // Log the error or take other appropriate action here if needed
            }

            return renameMessage;
        }

        private void DirectoryCopy(string sourceDirName, string destDirName)
        {
            var dir = new Alphaleonis.Win32.Filesystem.DirectoryInfo(sourceDirName);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDirName}");
            }

            Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(destDirName);

            foreach (var file in dir.GetFiles())
            {
                var tempPath = System.IO.Path.Combine(destDirName, file.Name);
                if (!System.IO.File.Exists(tempPath))
                {
                    file.CopyTo(tempPath);
                }
            }

            foreach (var subdir in dir.GetDirectories())
            {
                var tempPath = System.IO.Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, tempPath);
            }
        }


        private void Cancel()
        {
           _window.Close();
        }
    }
}
