using System;

namespace MO2ExportImport.Models
{
    public class Settings
    {
        public string ExportDestinationFolder { get; set; }
        public bool ExportIgnoreDisabled { get; set; } = true; // Default value
        public bool ExportIgnoreSeparators { get; set; } = false; // Default value
        public bool ExportAutoCalculateSpace { get; set; } = true;
        public string ImportTargetMO2Dir { get; set; }
        public ImportMode ImportMode { get; set; } = ImportMode.Spliced;
        public bool ImportIgnoreDisabled { get; set; } = true; // Default value
        public bool ImportIgnoreSeparators { get; set; } = false; // Default value
        public bool ImportAddNoDeleteFlags { get; set; } = false; // Default value
        public bool ImportSkipExistingMods { get; set; } = true; // Default value
        public bool ImportDisableUncheckedMods { get; set; } = false;
        public bool ImportAutoCalculateSpace { get; set; } = true;
    }
}
