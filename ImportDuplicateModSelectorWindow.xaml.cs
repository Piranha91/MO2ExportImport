using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MO2ExportImport
{
    public partial class ImportDuplicateModSelectorWindow : Window
    {
        // Import necessary functions from user32.dll
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        // Constants for the menu command to remove (the close button)
        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;

        public ImportDuplicateModSelectorWindow()
        {
            InitializeComponent();
            Loaded += ImportDuplicateModSelectorWindow_Loaded;
        }

        private void ImportDuplicateModSelectorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the window handle
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            // Get the system menu handle
            IntPtr hMenu = GetSystemMenu(hWnd, false);
            // Remove the close menu item (which also disables the X button)
            RemoveMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
        }
    }
}