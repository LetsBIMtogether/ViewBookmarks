// AGdialogWindowYesNo.xaml

#region Namespaces
using System;
using System.Windows;
using System.Windows.Input;
#endregion

namespace AG_View_Bookmarks
{
    public partial class agDialogWindowYesNo : Window
    {
        public bool UserResponse { get; private set; } 

        public agDialogWindowYesNo(string message)
        {
            try
            {
                InitializeComponent();

                // Ensure the message is not null and set it
                MessageTextBlock.Text = message ?? "No message provided.";

                // Default response to false (e.g., Cancel)
                UserResponse = false;

                // Measure the size of the text and adjust the window height if necessary
                MeasureWindowHeight();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void MeasureWindowHeight()
        {
            // Measure the size of the TextBlock after setting the message
            MessageTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredHeight = MessageTextBlock.DesiredSize.Height;

            // Add padding for buttons and borders and adjust window height
            const double padding = 50;
            this.Height = desiredHeight + padding;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            // Set response to true (Yes)
            UserResponse = true;    
            this.Close(); 
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Set response to false (No)
            UserResponse = false; 
            this.Close(); 
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow the window to be dragged
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    this.DragMove();
                }
                catch
                {
                    // Ignore drag errors
                }
            }
        }
    }
}
