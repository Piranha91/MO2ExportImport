using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;

namespace MO2ExportImport
{
    public class DownloadTransferHelper
    {
        public static string GetInstallationFileFromMeta(string modDirectory)
        {
            var metaPath = Path.Combine(modDirectory, "meta.ini");
            
            if (!File.Exists(metaPath))
            {
                return null;
            }

            try
            {
                var lines = File.ReadAllLines(metaPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("installationFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring("installationFile=".Length).Trim();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        public static string GetSourceMO2DirectoryFromExportLog(string exportSourceFolder)
        {
            var exportLogPath = Path.Combine(exportSourceFolder, "ExportLog.json");
            
            if (!File.Exists(exportLogPath))
            {
                return null;
            }

            try
            {
                var jsonString = File.ReadAllText(exportLogPath);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                
                if (jsonDoc.RootElement.TryGetProperty("SourceMO2Directory", out var sourceDir))
                {
                    return sourceDir.GetString();
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        public static string GetSourceMO2DirectoryFromModlistJson(string importSourceFolder)
        {
            var modlistJsonPath = Path.Combine(importSourceFolder, "modlist.json");
            
            if (!File.Exists(modlistJsonPath))
            {
                return null;
            }

            try
            {
                var jsonString = File.ReadAllText(modlistJsonPath);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                
                if (jsonDoc.RootElement.TryGetProperty("ModsRootPath", out var modsRootPath))
                {
                    var modsPath = modsRootPath.GetString();
                    if (!string.IsNullOrEmpty(modsPath) && modsPath.EndsWith("\\mods", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove "\mods" from the end to get MO2 root
                        return modsPath.Substring(0, modsPath.Length - 5);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        public static string PromptForDownloadDirectory(string title)
        {
            var dialog = new OpenFolderDialog
            {
                Title = title
            };
            
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                return dialog.FolderName;
            }

            return null;
        }

        public static string ResolveSourceDownloadDirectory(string importSourceFolder, string modsRootPath, bool isSourceMo2Directory)
        {
            string sourceMO2Dir = null;

            if (isSourceMo2Directory)
            {
                // Source is an MO2 directory
                sourceMO2Dir = importSourceFolder;
            }
            else
            {
                // Check if it's a list-only export (has modlist.json)
                var modlistJsonPath = Path.Combine(importSourceFolder, "modlist.json");
                if (File.Exists(modlistJsonPath))
                {
                    sourceMO2Dir = GetSourceMO2DirectoryFromModlistJson(importSourceFolder);
                }
                else
                {
                    // Check if it's a mod export directory (has ExportLog.json)
                    var exportLogPath = Path.Combine(importSourceFolder, "ExportLog.json");
                    if (File.Exists(exportLogPath))
                    {
                        sourceMO2Dir = GetSourceMO2DirectoryFromExportLog(importSourceFolder);
                    }
                }
            }

            // Try to get download directory from MO2 directory
            if (!string.IsNullOrEmpty(sourceMO2Dir) && Directory.Exists(sourceMO2Dir))
            {
                var downloadDir = CommonFuncs.GetDownloadsDirectory(sourceMO2Dir);  // CHANGED
                if (!string.IsNullOrEmpty(downloadDir) && Directory.Exists(downloadDir))
                {
                    return downloadDir;
                }
            }

            // If we couldn't find it, prompt user
            return PromptForDownloadDirectory("Select Source Downloads Directory");
        }

        public static string ResolveDestinationDownloadDirectory(string mo2Directory)
        {
            var downloadDir = CommonFuncs.GetDownloadsDirectory(mo2Directory);  // CHANGED
    
            if (!string.IsNullOrEmpty(downloadDir) && Directory.Exists(downloadDir))
            {
                return downloadDir;
            }

            // If directory doesn't exist, prompt user
            return PromptForDownloadDirectory("Select Destination Downloads Directory");
        }
    }
}