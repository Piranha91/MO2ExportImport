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

        public string ListName { get; set; }
        public string DirectoryName { get; set; }
        public string DisplayName { get; set; }
        public bool EnabledInMO2 { get; set; }
        public bool IsSeparator { get; set; }
        public bool IsNoDelete { get; set; }
        public string? NoDeleteIndex { get; set; } = null;
        [JsonIgnore] public string DestinationName { get; set; } // same as DirectoryName unless adding a [NoDelete] tag

        public bool SelectedInUI
        {
            get => _selectedInUI;
            set => this.RaiseAndSetIfChanged(ref _selectedInUI, value);
        }

        public Mod(string name)
        {
            ListName = name;
            DisplayName = name;

            #region Activation Status
            EnabledInMO2 = ListName.StartsWith("+") || ListName.StartsWith("*");
            if (EnabledInMO2)
            {
                DisplayName = StringExtensions.RemoveAtBeginning(DisplayName, "+").Trim();
            }
            else
            {
                DisplayName = StringExtensions.RemoveAtBeginning(DisplayName, "-").Trim();
            }
            DirectoryName = DisplayName;
            DestinationName = DirectoryName;
            #endregion

            IsSeparator = name.EndsWith(_separatorSuffix, StringComparison.OrdinalIgnoreCase);
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
        }
    }
}
