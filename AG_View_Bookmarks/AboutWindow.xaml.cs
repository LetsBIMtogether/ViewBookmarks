// AboutWindow.cs

#region Namespaces
using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Navigation;
#endregion

namespace AG_View_Bookmarks
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); 
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the left mouse button is pressed
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    this.DragMove();
                }
                catch (InvalidOperationException ex)
                {
                    TaskDialog.Show("Error", $"Drag operation failed: {ex.Message}");
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Open the hyperlink in the default web browser
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                // Required for .NET Framework to open URLs
                UseShellExecute = true 
            });

            // Prevent further navigation handling
            e.Handled = true;
        }

    }
}
