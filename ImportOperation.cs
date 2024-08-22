using MO2ExportImport.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public List<string> AddedPluginNames => ProfileImports.SelectMany(x => x.AddedPluginNames).Distinct().ToList();
    }

    public class ProfileImportOperation
    {
        public ProfileImportOperation(string profileName)
        {
            ProfileName = profileName;
        }

        public string ProfileName { get; set; }
        public List<string> AddedModNames { get; set; } = new();
        public List<string> AddedPluginNames { get; set; } = new();
    }
}
