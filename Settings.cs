using System;

namespace MO2ExportImport.Models
{
    public class Settings
    {
        public string ExportDestinationFolder { get; set; }
        public bool ExportIgnoreDisabled { get; set; } = true; // Default value
        public bool ExportIgnoreSeparators { get; set; } = false; // Default value
        public string ImportTargetMO2Dir { get; set; }
        public ImportMode ImportMode { get; set; } = ImportMode.Spliced;
        public bool ImportIgnoreDisabled { get; set; } = true; // Default value
        public bool ImportIgnoreSeparators { get; set; } = false; // Default value

    }
}
