using System.Windows.Controls;
using MO2ExportImport.ViewModels;

namespace MO2ExportImport.Views
{
    public partial class ImportView : UserControl
    {
        public ImportView()
        {
            InitializeComponent();
            this.DataContext = new ImportViewModel();
        }
    }
}
