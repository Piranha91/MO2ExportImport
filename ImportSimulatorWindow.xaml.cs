using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MO2ExportImport
{
    public partial class ImportSimulatorWindow : Window
    {
        public ImportSimulatorWindow()
        {
            InitializeComponent();
        }
        
        // When the TreeView selection changes, update the ViewModel.
        private void SimulatorTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ImportSimulatorViewModel vm)
            {
                vm.SelectedSimulatorNode = e.NewValue as ISimulatorNode;
            }
        }
    }
    
    /// <summary>
    /// Converts an IEnumerable&lt;string&gt; (such as an ObservableCollection&lt;string&gt;) 
    /// to a single string with each element separated by a newline.
    /// </summary>
    public class CollectionToStringConverter : IValueConverter
    {
        // Convert collection to string.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> collection)
            {
                return string.Join(Environment.NewLine, collection);
            }
            return value;
        }
        
        // One-way converter; ConvertBack is not implemented.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}