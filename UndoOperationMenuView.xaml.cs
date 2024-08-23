using MO2ExportImport.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MO2ExportImport.Views
{
    /// <summary>
    /// Interaction logic for UndoOperationMenuView.xaml
    /// </summary>
    public partial class UndoOperationMenuView : UserControl
    {
        public UndoOperationMenuView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Assuming your DataContext is your ViewModel
            var viewModel = DataContext as UndoOperationMenuViewModel;
            viewModel?.OnViewLoaded(); // Call your ViewModel function
        }
    }
}
