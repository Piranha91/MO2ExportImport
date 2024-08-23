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
using System.Windows.Shapes;

namespace MO2ExportImport
{
    /// <summary>
    /// Interaction logic for ScrollableMessageBox.xaml
    /// </summary>
    public partial class ScrollableMessageBox : Window
    {
        public ScrollableMessageBox(string message)
        {
            InitializeComponent();
            MessageTextBox.Text = message;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public static void Show(string message, string title = "Message")
        {
            var messageBox = new ScrollableMessageBox(message)
            {
                Title = title
            };
            messageBox.ShowDialog();
        }

        public static void Show(IEnumerable<string> messages, string title = "Message")
        {
            var messageBox = new ScrollableMessageBox(string.Join(Environment.NewLine, messages))
            {
                Title = title
            };
            messageBox.ShowDialog();
        }
    }
}
