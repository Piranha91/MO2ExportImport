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
        private readonly IEnumerable<string> _selectedMods;
        private readonly string _selectedProfile;
        private readonly string _exportDestinationFolder;
        private string _spaceInfo;
        private Brush _spaceInfoColor;
        private bool _isExportEnabled;
        private readonly ExportPopupView _window;
        private readonly MainViewModel _mainViewModel;

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
        public ReactiveCommand<Unit, Unit> ExportCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public ExportPopupViewModel(ExportPopupView window, MainViewModel mainViewModel, string mo2Directory, IEnumerable<string> selectedMods, string exportDestinationFolder, string selectedProfile)
        {
            _window = window;
            _mainViewModel = mainViewModel;

            _mo2Directory = mo2Directory;
            _selectedMods = selectedMods;
            _selectedProfile = selectedProfile;
            _exportDestinationFolder = exportDestinationFolder;

            CalculateSpaceCommand = ReactiveCommand.Create(CalculateSpace);
            ExportCommand = ReactiveCommand.CreateFromTask(Export);
            CancelCommand = ReactiveCommand.Create(Cancel);

            // Initially disable the Export button
            IsExportEnabled = false;
        }

        private void CalculateSpace()
        {
            long totalSize = 0;

            // Calculate the total size of the selected mod folders
            foreach (var mod in _selectedMods)
            {
                var modPath = System.IO.Path.Combine(_mo2Directory, "mods", mod);
                if (System.IO.Directory.Exists(modPath))
                {
                    totalSize += System.IO.Directory.EnumerateFiles(modPath, "*", SearchOption.AllDirectories)
                                          .Sum(f => new System.IO.FileInfo(f).Length);
                }
            }

            // Determine the root drive of the export destination folder
            var exportRoot = System.IO.Path.GetPathRoot(_exportDestinationFolder);
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

        private async Task Export()
        {
            var pluginsSourcePath = System.IO.Path.Combine(_mo2Directory, "profiles", _selectedProfile, "plugins.txt");
            var modlistSourcePath = System.IO.Path.Combine(_mo2Directory, "profiles", _selectedProfile, "modlist.txt");

            bool isMergeOperation = System.IO.File.Exists(System.IO.Path.Combine(_mainViewModel.ExportDestinationFolder, "ExportLog.json"));
            string exportFolderPath = _mainViewModel.ExportDestinationFolder;

            if (!isMergeOperation)
            {
                exportFolderPath = System.IO.Path.Combine(_mainViewModel.ExportDestinationFolder, DateTime.Now.ToString("yyyy MM dd (HH mm)"));
                Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(exportFolderPath);
            }

            var exportLog = new
            {
                DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ProgramVersion = _mainViewModel.ProgramVersion
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
                var modSourcePath = System.IO.Path.Combine(_mo2Directory, "mods", mod);
                var modDestinationPath = System.IO.Path.Combine(exportFolderPath, mod);

                if (!Alphaleonis.Win32.Filesystem.Directory.Exists(modDestinationPath))
                {
                    copyTasks.Add(Task.Run(() => DirectoryCopy(modSourcePath, modDestinationPath)));
                }
            }

            // Wait for all copy tasks to complete
            await Task.WhenAll(copyTasks);

            string renameMessage = string.Empty;

            // Attempt to rename the directory
            if (isMergeOperation)
            {
                var newExportFolderPath = System.IO.Path.Combine(_mainViewModel.ExportDestinationFolder, DateTime.Now.ToString("yyyy MM dd (HH mm)"));

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
            }

            MessageBox.Show($"Export completed successfully.{renameMessage}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
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
