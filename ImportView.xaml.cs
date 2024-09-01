using System.Windows.Controls;
using System.Linq;
using MO2ExportImport.ViewModels;
using DynamicData.Binding;
using ReactiveUI;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MO2ExportImport.Views
{
    public partial class ImportView : UserControl
    {
        private bool isScrolling = false;
        private DispatcherTimer textChangedTimer;

        public ImportView()
        {
            InitializeComponent();
            Loaded += ImportView_Loaded;

            // Initialize the DispatcherTimer
            textChangedTimer = new DispatcherTimer();
            textChangedTimer.Interval = TimeSpan.FromSeconds(0.05); // Set the interval
            textChangedTimer.Tick += TextChangedTimer_Tick;
        }

        private void ImportView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ImportViewModel viewModel)
            {
                viewModel.OnViewLoaded(this);
            }
        }

        private void TextChangedTimer_Tick(object sender, EventArgs e)
        {
            // Stop the timer
            textChangedTimer.Stop();

            // Place your logic here to handle the text change
            if (string.IsNullOrEmpty(FilterTextBox.Text))
            {
                // FilterText is empty, re-add rectangles for all selected items
                UpdateHighlightPositions(false);
            }
            else
            {
                // FilterText is not empty, remove all rectangles
                HighlightCanvas.Children.Clear();
            }
        }

        private void ModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ImportViewModel viewModel)
            {
                // Refresh the selection status of each mod
                //foreach (Mod mod in viewModel.ModList)
                //{
                //    mod.Selected = viewModel.ModList.Contains(mod);
                //}

                // prevent erroneous selection of ListBox items in large lists due to Virtualization
                if (isScrolling)
                {
                    return;
                }

                // Determine if the selection was modified (e.g., Ctrl+click or Shift+click)
                bool isModifiedSelection = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
                                           Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ||
                                           Mouse.LeftButton == MouseButtonState.Pressed && Keyboard.Modifiers != ModifierKeys.None;

                // If the selection is modified, do not clear existing rectangles
                if (e.AddedItems.Count > 0 || e.RemovedItems.Count > 0)
                {
                    UpdateHighlightPositions(!isModifiedSelection);
                }

                // Trigger the update for the Import button's enabled status
                viewModel.UpdateImportEnabled();

                // Update the selected item count for the view model
                viewModel.UpdateSelectedCount();
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Reset the timer whenever the text changes
            textChangedTimer.Stop();
            textChangedTimer.Start();

            if (string.IsNullOrEmpty(FilterTextBox.Text))
            {
                // FilterText is empty, re-add rectangles for all selected items
                UpdateHighlightPositions(false);
            }
            else
            {
                // FilterText is not empty, remove all rectangles
                HighlightCanvas.Children.Clear();
            }
        }

        private void UpdateHighlightPositions(bool clearExisting = true)
        {
            // Check if FilterTextBox.Text is empty; if not, do not place marks
            if (!string.IsNullOrEmpty(FilterTextBox.Text))
            {
                return;
            }

            if (clearExisting)
            {
                HighlightCanvas.Children.Clear();
            }

            // Get the ScrollViewer inside the ListBox
            var scrollViewer = GetScrollViewer(ModsListBox);

            if (scrollViewer == null)
                return;

            // Calculate the scrollbar button height (same as scrollbar width)
            double scrollbarButtonHeight = SystemParameters.VerticalScrollBarWidth;

            // Calculate the adjusted scrollable height
            double adjustedScrollableHeight = scrollViewer.ScrollableHeight + (2 * scrollbarButtonHeight);

            // Calculate the average height of visible items
            double totalVisibleHeight = 0;
            int visibleItemCount = 0;

            foreach (var item in ModsListBox.Items)
            {
                var container = ModsListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    totalVisibleHeight += container.ActualHeight;
                    visibleItemCount++;
                }
            }

            // Calculate the average item height
            // Fallback to an estimated height if no items are visible
            double averageItemHeight;
            if (visibleItemCount > 0)
            {
                averageItemHeight = totalVisibleHeight / visibleItemCount;
            }
            else
            {
                // Create a virtual StackPanel to mimic the DataTemplate
                var virtualStackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var virtualCheckBox = new CheckBox { IsChecked = true, IsEnabled = false, Margin = new Thickness(0, 0, 10, 0) };
                var virtualTextBlock = new TextBlock { Text = "Sample Text", VerticalAlignment = VerticalAlignment.Center };

                virtualStackPanel.Children.Add(virtualCheckBox);
                virtualStackPanel.Children.Add(virtualTextBlock);

                virtualStackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                averageItemHeight = virtualStackPanel.DesiredSize.Height;
            }

            // Estimate the total height of all items
            double totalHeight = averageItemHeight * ModsListBox.Items.Count;

            // Iterate over each SelectedItem in the ListBox
            foreach (var item in ModsListBox.SelectedItems)
            {
                // Calculate the cumulative height up to this item using the average height
                int itemIndex = ModsListBox.Items.IndexOf(item);
                double cumulativeHeight = averageItemHeight * itemIndex;

                // Calculate the ratio of the selected item's position relative to the total theoretical height
                double selectedPositionRatio = cumulativeHeight / totalHeight;

                // Calculate the position of the rectangle relative to the adjusted scrollable height
                double rectanglePosition = selectedPositionRatio * (HighlightCanvas.ActualHeight - (2 * scrollbarButtonHeight)) + scrollbarButtonHeight;

                // Create and place the rectangle
                double rectangleHeight = 6; // Thickness of the rectangle
                double scrollbarWidth = scrollbarButtonHeight; // Use the scrollbar width for the rectangle width
                double canvasWidth = HighlightCanvas.ActualWidth;
                double lineLeft = canvasWidth - scrollbarWidth;

                var line = new Rectangle
                {
                    Width = scrollbarWidth,
                    Height = rectangleHeight,
                    Fill = Brushes.Blue
                };

                Canvas.SetLeft(line, lineLeft);
                Canvas.SetTop(line, rectanglePosition - (rectangleHeight / 2)); // Adjust for centering the rectangle

                HighlightCanvas.Children.Add(line);
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer)
            {
                return (ScrollViewer)o;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
