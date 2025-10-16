using System.Collections.ObjectModel;

namespace MO2ExportImport;

// Add this helper class to your project (in a new file or at the bottom of ImportViewModel.cs):
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    public void AddRange(IEnumerable<T> items)
    {
        _suppressNotification = true;
        foreach (var item in items)
        {
            Add(item);
        }
        _suppressNotification = false;
        OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
            System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }
}