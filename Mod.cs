using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace MO2ExportImport
{
    public class Mod : ReactiveObject
    {
        private bool _selectedInUI;
        private bool _overWriteExistingDuringImport;
        private const string _separatorDispString = "-----";
        private const string _noDeleteString = "[NoDelete]";

        public ModListing SourceListing { get; set; }
        public string SourceDirectoryName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsNoDelete { get; set; } = false;
        public string? NoDeleteIndex { get; set; } = null;
        public bool EnabledInExportedModList { get; set; } = false;
        public string? Version { get; set; } = null;
        

        public bool SelectedInUI
        {
            get => _selectedInUI;
            set => this.RaiseAndSetIfChanged(ref _selectedInUI, value);
        }
        
        public bool OverWriteExistingDuringImport
        {
            get => _overWriteExistingDuringImport;
            set => this.RaiseAndSetIfChanged(ref _overWriteExistingDuringImport, value);
        }

        public Mod(string listingEntry)
        {
            SourceListing = new ModListing(listingEntry);
            Initialize();
        }

        public Mod(ModListing listingObject)
        {
            SourceListing = listingObject;
            Initialize();
        }

        public Mod(Mod copyTemplate)
        {
            SourceListing = new ModListing(copyTemplate.SourceListing.GetCurrentEntryString());
            SourceDirectoryName = copyTemplate.SourceDirectoryName;
            DisplayName = copyTemplate.DisplayName;
            IsNoDelete = copyTemplate.IsNoDelete;
            NoDeleteIndex = copyTemplate.NoDeleteIndex;
            EnabledInExportedModList = copyTemplate.EnabledInExportedModList;
            OverWriteExistingDuringImport = copyTemplate.OverWriteExistingDuringImport;
            Version = copyTemplate.Version;
        }

        private void Initialize()
        {
            DisplayName = SourceListing.GetCurrentFolderName();
            SourceDirectoryName = SourceListing.GetCurrentFolderName();
            
            if (SourceListing.IsSeparator)
            {
                DisplayName = StringExtensions.RemoveAtEnd(DisplayName, ModListing._separatorSuffix).Trim();
                DisplayName = _separatorDispString + DisplayName + _separatorDispString;
            }

            IsNoDelete = SourceListing.IsNoDelete;
            if (IsNoDelete)
            {
                // Extract the NoDeleteIndex if it exists
                if (SourceListing.NoDeletePrefix.StartsWith("[") && SourceListing.NoDeletePrefix.Contains("]"))
                {
                    int endIndex = SourceListing.NoDeletePrefix.IndexOf("]");
                    NoDeleteIndex = SourceListing.NoDeletePrefix.Substring(1, endIndex - 1); // Get the string between the brackets
                }
            }

            EnabledInExportedModList = SourceListing.Enabled ?? false;
        }

        public Mod() // for json deserialization
        {

        }

        public override string ToString()
        {
            return DisplayName;
        }

        public bool MakeNoDelete()
        {
            if (!IsNoDelete)
            {
                SourceListing.MakeNoDelete();
                IsNoDelete = true;
                return true;
            }
            return false;
        }

        public bool RemoveNoDelete()
        {
            if (IsNoDelete)
            {
                SourceListing.RemoveNoDelete();
                IsNoDelete = false;
                return true;
            }
            return false;
        }

        public string GetDestinationName()
        {
            return FormatHandler.TrimModActivationStatus(SourceListing.GetCurrentEntryString());
        }

        public bool IsEnabled()
        {
            return SourceListing.Enabled.HasValue && SourceListing.Enabled.Value;
        }

        public void SetPrefix(string prefix)
        {
            SourceListing.SetPrefix(prefix);
        }
    }
}
