using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MO2ExportImport
{
    public interface IContainsMods
    {
        ObservableCollection<Mod> ModList { get; }
        public void RefreshCanExport();
    }
}
