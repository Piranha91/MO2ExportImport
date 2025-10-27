using System;
using MO2ExportImport.ViewModels;

namespace MO2ExportImport.Models
{
    public class Settings
    {
        public string ExportDestinationFolder { get; set; }
        public bool ExportIgnoreDisabled { get; set; } = true; // Default value
        public bool ExportIgnoreSeparators { get; set; } = false; // Default value
        public bool ImportStripNoDelete { get; set; } = false;
        public bool ExportAutoCalculateSpace { get; set; } = true;
        public string ImportTargetMO2Dir { get; set; }
        public ImportMode ImportMode { get; set; } = ImportMode.Spliced;
        public string AnchorModName { get; set; }
        public bool ImportIgnoreDisabled { get; set; } = true; // Default value
        public bool ImportIgnoreSeparators { get; set; } = false; // Default value
        public bool ImportAddNoDeleteFlags { get; set; } = false; // Default value
        public bool ImportSkipExistingMods { get; set; } = false; // Default value
        public bool ImportIgnoreMatchedModsForOrdering { get; set; } = true;
        public bool ImportMatchModActivationState { get; set; } = true;
        public bool ImportMatchPluginActivationState { get; set; } = true;
        public bool ImportInterpolateMissingPluginGroups { get; set; } = false;
        public bool ImportTransferDownloads { get; set; } = false;
        public bool ImportAutoCalculateSpace { get; set; } = true;
        public string ImportPrefix { get; set; } = string.Empty;
        public ImportViewModel.MultiPluginMode ImportMultiPluginMode { get; set; } = ImportViewModel.MultiPluginMode.WinnerOnly;
        public ImportViewModel.PluginSelectionMode ImportPluginSelectionMode { get; set; } = ImportViewModel.PluginSelectionMode.EnabledOnly;
    }
}
