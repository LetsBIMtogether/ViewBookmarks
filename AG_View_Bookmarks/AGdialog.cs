// AGdialog.cs

#region Namespaces
using System.Windows;
using System;
#endregion

namespace AG_View_Bookmarks
{
    public static class AGdialog
   {
        public static void Show(string message)
        {
            try
            {
                var dialog = new agDialogWindow(message);
                dialog.ShowDialog();
                // ^-- This ensures it is shown modally (instead of .Show())
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to show dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool ShowYesNo(string message)
        {
            try
            {
                var dialog = new agDialogWindowYesNo(message);
                dialog.ShowDialog(); 
                return dialog.UserResponse; 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to show Yes/No dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false; 
            }
        }
    }
}
