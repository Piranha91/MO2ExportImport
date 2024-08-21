using System.Windows;
using System.Windows.Controls;
using MO2ExportImport.ViewModels;
using MO2ExportImport.Views;

namespace MO2ExportImport
{
    public partial class MainWindow : Window
    {
        private MainViewModel _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            _mainViewModel = new MainViewModel();
            DataContext = _mainViewModel;

            // Subscribe to the Closed event
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Call CloseLog method from the ViewModel
            _mainViewModel?.CloseLog();
        }
    }
}
