using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MO2ExportImport
{
    public class Mod : ReactiveObject
    {
        private bool _selected;

        public string Name { get; set; }
        public bool Enabled { get; set; }
        public bool IsSeparator { get; set; }

        public bool Selected
        {
            get => _selected;
            set => this.RaiseAndSetIfChanged(ref _selected, value);
        }

        public Mod(string name, bool enabled)
        {
            Name = name;
            IsSeparator = name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase);

            // If it's a separator, ensure it's always enabled
            Enabled = IsSeparator || enabled;
        }

        /*
        public Mod()
        {
            Name = string.Empty;
            this.WhenAnyValue(x => x.Selected).Skip(1).Subscribe(isSelected =>
            {
                MessageBox.Show($"{Name} is {(isSelected ? "selected" : "deselected")}", "Selection Changed", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }*/
    }
}
