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
        private const string _separatorSuffix = "_separator";
        private const string _separatorDispString = "-----";
        private const string _noDeleteString = "[NoDelete]";

        public ModListing SourceListing { get; set; }
        public string DirectoryName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsSeparator { get; set; } = false;
        public bool IsNoDelete { get; set; } = false;
        public string? NoDeleteIndex { get; set; } = null;
        public bool EnabledInExportedModList { get; set; } = false;

        public bool SelectedInUI
        {
            get => _selectedInUI;
            set => this.RaiseAndSetIfChanged(ref _selectedInUI, value);
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
            Initialize();
        }

        private void Initialize()
        {
            DisplayName = SourceListing.Name;
            DirectoryName = DisplayName;

            IsSeparator = SourceListing.Name.EndsWith(_separatorSuffix, StringComparison.OrdinalIgnoreCase);
            if (IsSeparator)
            {
                DisplayName = StringExtensions.RemoveAtEnd(DisplayName, _separatorSuffix).Trim();
                DisplayName = _separatorDispString + DisplayName + _separatorDispString;
            }

            IsNoDelete = DisplayName.StartsWith(_noDeleteString);
            if (IsNoDelete)
            {
                DisplayName = StringExtensions.RemoveAtBeginning(DisplayName, _noDeleteString).Trim();

                // Extract the NoDeleteIndex if it exists
                if (DisplayName.StartsWith("[") && DisplayName.Contains("]"))
                {
                    int endIndex = DisplayName.IndexOf("]");
                    NoDeleteIndex = DisplayName.Substring(1, endIndex - 1); // Get the string between the brackets
                    DisplayName = DisplayName.Substring(endIndex + 1).Trim(); // Remove the NoDeleteIndex from DisplayName
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

        public void MakeNoDelete()
        {
            if (!IsNoDelete)
            {
                SourceListing.MakeNoDelete();
                IsNoDelete = true;
            }
        }

        public string GetDestinationName()
        {
            return FormatHandler.TrimModActivationStatus(SourceListing.GetCurrentEntryString());
        }

        public bool IsEnabled()
        {
            return SourceListing.Enabled.HasValue && SourceListing.Enabled.Value;
        }
    }
}
