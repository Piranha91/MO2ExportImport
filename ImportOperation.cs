using MO2ExportImport.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Accessibility;

namespace MO2ExportImport
{
    public class ImportOperation
    {
        public ImportOperation(string modSourceRootDir, string destinationMO2Dir, DateTime importTime, string programVersion)
        {
            ModSourceRootDir = modSourceRootDir;
            DestinationMO2Dir = destinationMO2Dir;
            ImportTime = importTime;
            ProgramVersion = programVersion;
        }

        public string ModSourceRootDir { get; set; }
        public string ModSourceDirName => new DirectoryInfo(ModSourceRootDir)?.Name ?? string.Empty;
        public string DestinationMO2Dir { get; set; }
        public DateTime ImportTime { get; set; }
        public string ProgramVersion { get; set; }
        public List<ProfileImportOperation> ProfileImports { get; set; } = new();
        public List<string> AddedModNames => ProfileImports.SelectMany(x => x.AddedModNames).Distinct().ToList();
        public List<ImportOwnedPlugin> AddedPlugins => ProfileImports.SelectMany(x => x.AddedPluginNames).Distinct().ToList();
        public List<TransferredDownload> TransferredDownloads { get; set; } = new();

        [JsonIgnore]
        public string ThisFilePath { get; set; } = string.Empty;
    }
    
    public class TransferredDownload
    {
        public string FileName { get; set; }
        public string DestinationPath { get; set; }
    }

    public class ProfileImportOperation
    {
        public ProfileImportOperation(string profileName)
        {
            ProfileName = profileName;
        }

        public string ProfileName { get; set; }
        public List<string> AddedModNames { get; set; } = new();
        public List<ImportOwnedPlugin> AddedPluginNames { get; set; } = new();
        public List<string> EnabledMods { get; set; } = new();
        public List<string> DisabledMods { get; set; } = new();
        public List<PluginListing> DeletedPlugins { get; set; } = new();
        public List<string> EnabledPlugins { get; set; } = new();
        public List<string> DisabledPlugins { get; set; } = new();
        public List<string> OriginalModList { get; set; } = new();
        public List<string> OriginalPluginList { get; set; }
    }

    public class ImportOwnedPlugin
    {
        public ImportOwnedPlugin(string pluginName, string parentModName)
        {
            PluginName = pluginName;
            ParentModName = parentModName;
        }

        public string PluginName { get; set; }
        public string ParentModName { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var other = (ImportOwnedPlugin)obj;
            return PluginName.Equals(other.PluginName, StringComparison.OrdinalIgnoreCase) &&
                   ParentModName.Equals(other.ParentModName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            int hashPluginName = PluginName?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;
            int hashParentMod = ParentModName?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;

            return hashPluginName ^ hashParentMod;
        }
    }
}
