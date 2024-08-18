using System.Windows.Controls;
using MO2ExportImport.ViewModels;

namespace MO2ExportImport.Views
{
    public partial class ExportView : UserControl
    {
        public ExportView(MainViewModel mainViewModel)
        {
            InitializeComponent();
            DataContext = new ExportViewModel(mainViewModel);
        }
    }
}
