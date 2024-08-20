using System.Windows.Controls;
using System.Linq;
using MO2ExportImport.ViewModels;
using DynamicData.Binding;
using ReactiveUI;
using System.Windows;

namespace MO2ExportImport.Views
{
    public partial class ImportView : UserControl
    {
        public ImportView()
        {
            InitializeComponent();
            Loaded += ImportView_Loaded;
        }

        private void ImportView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ImportViewModel viewModel)
            {
                viewModel.OnViewLoaded(this);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ImportViewModel viewModel)
            {
                // Refresh the selection status of each mod
                foreach (Mod mod in viewModel.ModList)
                {
                    mod.Selected = viewModel.ModList.Contains(mod);
                }

                // Trigger the update for the Import button's enabled status
                viewModel.UpdateImportEnabled();
            }
        }
    }
}
