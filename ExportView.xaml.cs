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

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = DataContext as ExportViewModel;
            if (viewModel == null) return;

            // Add newly selected items
            foreach (var item in e.AddedItems)
            {
                if (!viewModel.SelectedMods.Contains(item.ToString()))
                {
                    viewModel.SelectedMods.Add(item.ToString());
                }
            }

            // Remove deselected items
            foreach (var item in e.RemovedItems)
            {
                viewModel.SelectedMods.Remove(item.ToString());
            }
        }
    }
}
