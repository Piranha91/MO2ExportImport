using System;

namespace MO2ExportImport.Models
{
    public class Settings
    {
        public string ExportDestinationFolder { get; set; }
        public string ImportTargetMO2Dir { get; set; }
        public bool IgnoreDisabled { get; set; } = true; // Default value
        public bool IgnoreSeparators { get; set; } = false; // Default value
        public ImportMode ImportMode { get; set; } = ImportMode.Spliced;
    }
}
