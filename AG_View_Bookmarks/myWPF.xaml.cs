// myWPF.cs

#region Namespaces
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using System.Linq;
using CommonGuiWpf.Controls;
using System.Collections.Concurrent;
#endregion

namespace AG_View_Bookmarks
{
    public class agButton : Button
    {
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(agButton));

        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }
    }

    public partial class myWPF : Window
    {
        private readonly Brush _defaultBackground = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFAFAFAF"));  // Light Grey
        private readonly Brush _clickedBackground = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FF444444"));  // Dark Grey
        private readonly Dictionary<Label, CancellationTokenSource> _cancellationTokenSources = new Dictionary<Label, CancellationTokenSource>();

        private AboutWindow _aboutWindow;
        private readonly UIApplication _uiapp;

        private readonly System.Windows.Controls.TextBox _consoleTextBox;                           // Reference to the console's TextBox
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();     // A thread-safe queue to store strings for processing
        private Task _consoleTask = Task.CompletedTask;                                             // Tracks the active task
        private readonly object _queueLock = new object();                                          // Prevent overlapping tasks

        public myWPF(UIApplication uiapp)
        {
            InitializeComponent();
            _uiapp = uiapp;
            RefreshBookmarks_Labels();
            RefreshBookmarks_LEDs();

            // Find the TextBox by its name
            _consoleTextBox = this.FindName("ConsoleTextBox") as System.Windows.Controls.TextBox;
            ConsoleCurrentDoc();
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

        public void ConsoleCurrentDoc()
        {
            if (_consoleTextBox == null) return;

            // Get the Revit document
            Document doc = _uiapp.ActiveUIDocument?.Document;

            // Set the TextBox content to the Revit project name
            if (doc != null)
                _consoleTextBox.Text = $"Project: {doc.Title}";

            else
                _consoleTextBox.Text = "No active document.";
        }

        public void ConsoleShow(string message)
        {
            // Enqueue the message
            _messageQueue.Enqueue(message);

            lock (_queueLock)
            {
                // If no task is currently running, start processing
                if (_consoleTask.IsCompleted)
                    _consoleTask = ProcessQueueAsync();
            }
        }

        private async Task ProcessQueueAsync()
        {
            while (_messageQueue.TryDequeue(out string message))
            {
                if (_consoleTextBox != null)
                {
                    _consoleTextBox.Text = message;

                    // Wait before processing the next message
                    await Task.Delay(1250);

                    ConsoleCurrentDoc();
                }
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if the AboutWindow is already open
            if (_aboutWindow == null || !_aboutWindow.IsVisible)
            {
                // Create a new instance if it's not already open
                _aboutWindow = new AboutWindow();

                // Handle the Closed event to nullify the reference when the window is closed
                _aboutWindow.Closed += (s, args) => _aboutWindow = null;

                // Show the AboutWindow
                _aboutWindow.Show();
            }

            // Bring the already open window to the foreground
            else
                _aboutWindow.Activate();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshBookmarks_Labels();
            RefreshBookmarks_LEDs();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open a file dialog to select the import XML file
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "XML files (*.xml)|*.xml",
                    Title = "Select Import File"
                };

                if (openFileDialog.ShowDialog() != true)
                {
                    AGdialog.Show("Import cancelled.");
                    return;
                }

                string importFilePath = openFileDialog.FileName;

                // Load the import XML file
                if (!File.Exists(importFilePath))
                {
                    AGdialog.Show("ERROR: Selected file not found.");
                    return;
                }

                XDocument importDoc = XDocument.Load(importFilePath);

                // Define paths for the application data XML file
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string agvbDirectory = Path.Combine(appDataPath, "AG View Bookmarks");
                string agvbFilePath = Path.Combine(agvbDirectory, "AGVB.xml");

                // Load or create the application data XML file
                XDocument existingDoc = File.Exists(agvbFilePath) ? XDocument.Load(agvbFilePath) : new XDocument(new XElement("Data"));

                // Get the current Revit project path
                UIDocument uiDoc = _uiapp.ActiveUIDocument;
                Document doc = uiDoc?.Document;

                if (doc == null)
                {
                    AGdialog.Show("ERROR: No active Revit document found.");
                    return;
                }

                string currentProjectPath = AG.U.ProjectFilePath(doc);

                // Find or create the <Project> element for the current project
                XElement currentProjectElement = existingDoc.Root.Elements("Project")
                    .FirstOrDefault(p => p.Attribute("Path")?.Value == currentProjectPath);

                if (currentProjectElement == null)
                {
                    currentProjectElement = new XElement("Project", new XAttribute("Path", currentProjectPath));
                    existingDoc.Root.Add(currentProjectElement);
                }

                // Import bookmarks into the current project
                foreach (XElement projectElement in importDoc.Root.Elements("Project"))
                {
                    foreach (XElement bookmarkElement in projectElement.Elements("Bookmark"))
                    {
                        string importedIndex = bookmarkElement.Element("Index")?.Value;
                        string importedViewName = bookmarkElement.Element("ViewName")?.Value;
                        string importedUniqueId = bookmarkElement.Element("UniqueId")?.Value;

                        // Skip invalid bookmarks
                        if (string.IsNullOrEmpty(importedIndex) || string.IsNullOrEmpty(importedUniqueId))
                            continue; 

                        // Check if the bookmark already exists in the current project
                        XElement existingBookmark = currentProjectElement.Elements("Bookmark")
                            .FirstOrDefault(b => b.Element("Index")?.Value == importedIndex);

                        // Update the existing bookmark
                        if (existingBookmark != null)
                        {
                            existingBookmark.SetElementValue("ViewName", importedViewName);
                            existingBookmark.SetElementValue("UniqueId", importedUniqueId);
                        }

                        // Add the new bookmark
                        else
                        {
                            XElement newBookmark = new XElement("Bookmark",
                                new XElement("Index", importedIndex),
                                new XElement("ViewName", importedViewName),
                                new XElement("UniqueId", importedUniqueId));
                            currentProjectElement.Add(newBookmark);
                        }
                    }
                }

                // Save the updated application data XML file
                if (!Directory.Exists(agvbDirectory))
                    Directory.CreateDirectory(agvbDirectory);

                existingDoc.Save(agvbFilePath);

                // Refresh the UI to reflect the new bookmarks
                RefreshBookmarks_Labels();
                RefreshBookmarks_LEDs();

                AGdialog.Show("Import successful! Bookmarks have been added to the current project.");
            }
            catch (Exception ex)
            {
                AGdialog.Show($"ERROR: Failed to import file: {ex.Message}");
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshBookmarks_LEDs();
            RefreshBookmarks_Labels();

            try
            {
                // Define the paths
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string agvbDirectory = Path.Combine(appDataPath, "AG View Bookmarks");
                string agvbFilePath = Path.Combine(agvbDirectory, "AGVB.xml");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string exportFilePath = Path.Combine(desktopPath, "AGVB_Export.xml");

                // Check if the XML file exists
                if (!File.Exists(agvbFilePath))
                {
                    AGdialog.Show("ERROR: Bookmark file not found.");
                    return;
                }

                // Load the XML file
                XDocument xmlDoc = XDocument.Load(agvbFilePath);

                // Get the active Revit document
                UIDocument uiDoc = _uiapp.ActiveUIDocument;
                Document doc = uiDoc?.Document;

                if (doc == null)
                {
                    AGdialog.Show("ERROR: No active Revit document found.");
                    return;
                }

                // Get the current project path
                string currentProjectPath = AG.U.ProjectFilePath(doc);

                // Find the <Project> element for the current project
                XElement currentProjectElement = xmlDoc.Root.Elements("Project")
                    .FirstOrDefault(p => p.Attribute("Path")?.Value == currentProjectPath);

                if (currentProjectElement == null)
                {
                    AGdialog.Show("ERROR: No bookmarks found for the current project.");
                    return;
                }

                // Create a new XML document with the current project's bookmarks ONLY
                XDocument exportDoc = new XDocument(
                    new XElement("Data",
                        new XElement("Project",
                            currentProjectElement.Elements("Bookmark") 
                        )
                    )
                );

                // Save the new XML to the export file
                exportDoc.Save(exportFilePath);

                // Show success message
                AGdialog.Show("AGVB_Export.xml has been saved to desktop.");
            }
            catch (Exception ex)
            {
                AGdialog.Show($"ERROR: Failed to export file: {ex.Message}");
            }
        }

        private void Slider_Count_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int visibleCount = (int)e.NewValue;

            // Show or hide each StackPanel based on the slider value
            for (int i = 1; i <= 20; i++)
            {
                StackPanel stackPanel = (StackPanel)this.FindName($"StackPanel_P{i}");
                if (stackPanel != null)
                    stackPanel.Visibility = i <= visibleCount ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }

            // Adjust window height dynamically
            double panelHeight = 16;                                                        // LED height
            double panelMargin = 9.5;                                                       // Top and bottom margin of each panel
            double stackPanelTotalHeight = panelHeight + 2 * panelMargin;
            double baseHeight = 220;                                                        // Static content height
            double newHeight = baseHeight + (visibleCount - 2) * stackPanelTotalHeight;
            this.Height = newHeight;
        }

        private void BookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Define file and directory paths
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string agvbDirectory = Path.Combine(appDataPath, "AG View Bookmarks");
                string agvbFilePath = Path.Combine(agvbDirectory, "AGVB.xml");

                // Ensure directory exists
                if (!Directory.Exists(agvbDirectory))
                    Directory.CreateDirectory(agvbDirectory);

                // Load or create XML document
                XDocument xmlDoc;

                if (File.Exists(agvbFilePath))
                    xmlDoc = XDocument.Load(agvbFilePath);

                else
                    xmlDoc = new XDocument(new XElement("Data"));

                // Get the active Revit document and view
                UIDocument uiDoc = _uiapp.ActiveUIDocument;

                if (uiDoc == null)
                {
                    AGdialog.Show("ERROR: No active Revit document found.");
                    ConsoleCurrentDoc();

                    // Refresh the bookmarks in the form
                    RefreshBookmarks_Labels();
                    RefreshBookmarks_LEDs();

                    return;
                }

                Document doc = uiDoc.Document;

                if (doc == null)
                {
                    AGdialog.Show("ERROR: Active document is null.");
                    ConsoleCurrentDoc();

                    // Refresh the bookmarks in the form
                    RefreshBookmarks_Labels();
                    RefreshBookmarks_LEDs();

                    return;
                }

                // Try to get the active view
                View activeView = doc.ActiveView;
                if (activeView == null)
                {
                    AGdialog.Show("ERROR: No active view found. Please activate a view and try again.");
                    ConsoleCurrentDoc();

                    // Refresh the bookmarks in the form
                    RefreshBookmarks_Labels();
                    RefreshBookmarks_LEDs();

                    return;
                }

                // Get project path
                string projectPath = AG.U.ProjectFilePath(doc);
                if (string.IsNullOrEmpty(projectPath))
                {
                    AGdialog.Show("ERROR: Project path is empty. Save the project before adding bookmarks.");
                    ConsoleCurrentDoc();

                    // Refresh the bookmarks in the form
                    RefreshBookmarks_Labels();
                    RefreshBookmarks_LEDs();

                    return;
                }

                // Find or create the <Project> element
                XElement projectElement = xmlDoc.Root.Elements("Project")
                    .FirstOrDefault(p => p.Attribute("Path")?.Value == projectPath);

                if (projectElement == null)
                {
                    projectElement = new XElement("Project", new XAttribute("Path", projectPath));
                    xmlDoc.Root.Add(projectElement);
                }

                // Retrieve the button's tag to set the bookmark index
                Button clickedButton = sender as Button;
                if (clickedButton == null || clickedButton.Tag == null)
                {
                    AGdialog.Show("ERROR: Bookmark button is invalid or missing a tag.");
                    ConsoleCurrentDoc();

                    // Refresh the bookmarks in the form
                    RefreshBookmarks_Labels();
                    RefreshBookmarks_LEDs();

                    return;
                }

                string index = clickedButton.Tag.ToString();

                // Create or update the <Bookmark> element
                XElement bookmarkElement = projectElement.Elements("Bookmark")
                    .FirstOrDefault(b => b.Element("Index")?.Value == index);

                if (bookmarkElement == null)
                {
                    bookmarkElement = new XElement("Bookmark");
                    projectElement.Add(bookmarkElement);
                }

                bookmarkElement.SetElementValue("Index", index);
                bookmarkElement.SetElementValue("ViewName", activeView.Name);
                bookmarkElement.SetElementValue("UniqueId", activeView.UniqueId);

                // Save the updated XML document
                xmlDoc.Save(agvbFilePath);

                // Refresh the bookmarks in the form
                RefreshBookmarks_Labels();
                RefreshBookmarks_LEDs();

                ConsoleShow("Bookmark saved.");
            }
            catch (Exception ex)
            {
                // Refresh the bookmarks in the form
                RefreshBookmarks_Labels();
                RefreshBookmarks_LEDs();

                AGdialog.Show($"ERROR: Failed to save bookmark: {ex.Message}");
            }
        }

        public void RefreshBookmarks_LEDs()
        {
            try
            {
                // Define file and directory paths
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string agvbDirectory = Path.Combine(appDataPath, "AG View Bookmarks");
                string agvbFilePath = Path.Combine(agvbDirectory, "AGVB.xml");

                // Load the XML file
                if (!File.Exists(agvbFilePath))
                {
                    FlipRedAllLED();
                    ConsoleShow("ERROR: Bookmark file not found.");
                    return;
                }

                XDocument xmlDoc = XDocument.Load(agvbFilePath);

                // Get the active Revit document
                UIDocument uiDoc = _uiapp.ActiveUIDocument;
                if (uiDoc == null)
                {
                    FlipRedAllLED();
                    ConsoleShow("ERROR: No active Revit document found.");
                    return;
                }

                Document doc = uiDoc.Document;
                if (doc == null)
                {
                    FlipRedAllLED();
                    ConsoleShow("ERROR: Active document is null.");
                    return;
                }

                // Get project path
                string projectPath = AG.U.ProjectFilePath(doc);

                // Find the <Project> element for the current project
                XElement projectElement = xmlDoc.Root.Elements("Project")
                    .FirstOrDefault(p => p.Attribute("Path")?.Value == projectPath);

                // Iterate through all child elements in the StackPanel to find LEDs
                foreach (var child in DynamicContainer.Children)
                {
                    if (child is StackPanel stackPanel)
                    {
                        // Find the LED control within the stack panel
                        var ledControl = stackPanel.Children.OfType<LEDControl>().FirstOrDefault();
                        if (ledControl != null && ledControl.Tag != null)
                        {
                            string index = ledControl.Tag.ToString();

                            // Determine the state of the LED based on the bookmark's presence

                            // No project found
                            if (projectElement == null)
                            {
                                ledControl.State = LEDState.gray; 
                                continue;
                            }

                            XElement bookmarkElement = projectElement.Elements("Bookmark")
                                .FirstOrDefault(b => b.Element("Index")?.Value == index);

                            if (bookmarkElement != null)
                            {
                                string uniqueId = bookmarkElement.Element("UniqueId")?.Value;

                                if (!string.IsNullOrEmpty(uniqueId))
                                {
                                    // Attempt to resolve the element by UniqueId
                                    Element viewElement = doc.GetElement(uniqueId);

                                    // If the element is found and is a valid view, set the LED state to green
                                    if (viewElement != null)
                                        ledControl.State = LEDState.green;

                                    // If the element is not found, set the LED state to red
                                    else
                                        ledControl.State = LEDState.red;
                                }

                                // If the UniqueId is missing or invalid, set the LED state to gray
                                else
                                    ledControl.State = LEDState.gray;
                            }

                            // If no bookmark exists for this index, set the LED state to gray
                            else
                                ledControl.State = LEDState.gray;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AGdialog.Show($"ERROR: Failed to refresh LEDs: {ex.Message}");
            }
        }

        private void FlipRedAllLED()
        {
            try
            {
                // Iterate through all child elements in the DynamicContainer
                foreach (var child in DynamicContainer.Children)
                {
                    if (child is StackPanel stackPanel)
                    {
                        // Find the LED control within the stack panel
                        var ledControl = stackPanel.Children.OfType<LEDControl>().FirstOrDefault();

                        // Set the LED state to red
                        if (ledControl != null)
                            ledControl.State = LEDState.red;
                    }
                }
            }
            catch (Exception ex)
            {
                AGdialog.Show($"ERROR: Failed to turn all LEDs red: {ex.Message}");
            }
        }

        public void FlipLabelsToEmpty()
        {
            try
            {
                // Iterate through all child elements in the DynamicContainer
                foreach (var child in DynamicContainer.Children)
                {
                    if (child is StackPanel stackPanel)
                    {
                        // Find the Label control within the StackPanel
                        var label = stackPanel.Children.OfType<Label>().FirstOrDefault();

                        // Set the Label content to "<<<No bookmark saved>>>"
                        if (label != null)
                            label.Content = "\tNo bookmark saved-._.-`~,-._.-`~,";
                    }
                }
            }
            catch (Exception ex)
            {
                AGdialog.Show($"ERROR: Failed to reset labels: {ex.Message}");
            }
        }

        public void RefreshBookmarks_Labels()
        {
            try
            {
                FlipLabelsToEmpty();

                // Define file and directory paths
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string agvbDirectory = Path.Combine(appDataPath, "AG View Bookmarks");
                string agvbFilePath = Path.Combine(agvbDirectory, "AGVB.xml");

                // Load the XML file
                if (!File.Exists(agvbFilePath))
                {
                    ConsoleShow("ERROR: Bookmark file not found.");
                    return;
                }

                XDocument xmlDoc = XDocument.Load(agvbFilePath);

                // Get the active Revit document
                UIDocument uiDoc = _uiapp.ActiveUIDocument;
                if (uiDoc == null)
                {
                    ConsoleShow("ERROR: No active Revit document found.");
                    return;
                }

                Document doc = uiDoc.Document;
                if (doc == null)
                {
                    ConsoleShow("ERROR: Active document is null.");
                    return;
                }

                // Get project path
                string projectPath = AG.U.ProjectFilePath(doc);

                // Find the <Project> element for the current project
                XElement projectElement = xmlDoc.Root.Elements("Project")
                    .FirstOrDefault(p => p.Attribute("Path")?.Value == projectPath);

                if (projectElement == null)
                {
                    ConsoleShow("No bookmarks found for the current project.");
                    return;
                }

                // Iterate through all child elements in the StackPanel to find labels
                foreach (var child in DynamicContainer.Children)
                {
                    if (child is StackPanel stackPanel)
                    {
                        // Find the label within the stack panel
                        var label = stackPanel.Children.OfType<Label>().FirstOrDefault();

                        if (label != null && label.Tag != null)
                        {
                            string index = label.Tag.ToString();

                            // Find the corresponding <Bookmark> in XML
                            XElement bookmarkElement = projectElement.Elements("Bookmark")
                                .FirstOrDefault(b => b.Element("Index")?.Value == index);

                            if (bookmarkElement != null)
                            {
                                string uniqueId = bookmarkElement.Element("UniqueId")?.Value;

                                if (!string.IsNullOrEmpty(uniqueId))
                                {
                                    // Get the view using UniqueId
                                    Element viewElement = doc.GetElement(uniqueId);

                                    if (viewElement is View view)
                                    {
                                        // Update the view name in XML if it has changed
                                        string currentViewName = view.Name;
                                        string savedViewName = bookmarkElement.Element("ViewName")?.Value;

                                        if (currentViewName != savedViewName)
                                            bookmarkElement.SetElementValue("ViewName", currentViewName);

                                        // Update the label content
#if REVIT2024UP
                                        label.Content = $"{currentViewName} (ElementId: {view.Id.Value})";
#else
                                        label.Content = $"{currentViewName} (ElementId: {view.Id.IntegerValue})";
#endif

                                    }

                                    // Mark as "Deleted View" if UniqueId is not found
                                    else
                                        label.Content = "\tDeleted View-._.-`~,-._.-`~,";
                                }

                                // Reset content if UniqueId is invalid
                                else
                                    label.Content = "\tNo bookmark saved-._.-`~,-._.-`~,";
                            }

                            // Reset content if no bookmark exists
                            else
                                label.Content = "\tNo bookmark saved-._.-`~,-._.-`~,";
                        }
                    }
                }

                // Save the updated XML document
                xmlDoc.Save(agvbFilePath);

                ConsoleShow("Bookmarks refreshed.");
            }
            catch (Exception ex)
            {
                AGdialog.Show($"ERROR: Failed to refresh bookmarks: {ex.Message}");
            }
        }

        private async void LabelResetBookmarks_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Label label)
            {
                RefreshBookmarks_Labels();
                RefreshBookmarks_LEDs();

                // Cancel any ongoing task for this label
                if (_cancellationTokenSources.TryGetValue(label, out var cts))
                    cts.Cancel();

                var newCts = new CancellationTokenSource();
                _cancellationTokenSources[label] = newCts;
                var token = newCts.Token;

                label.Background = _clickedBackground;

                try
                {
                    await Task.Delay(250, token); // Wait briefly for user feedback
                }
                catch (TaskCanceledException)
                {
                    // Ignore the exception if the task is canceled
                }

                // Restore original background
                if (!token.IsCancellationRequested)
                    label.Background = _defaultBackground;

                _cancellationTokenSources.Remove(label);

                bool result = AGdialog.ShowYesNo("This will remove all bookmarks for the current project. Do you want to continue?");

                if (result)
                {
                    try
                    {
                        // Define file and directory paths
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string agvbDirectory = Path.Combine(appDataPath, "AG View Bookmarks");
                        string agvbFilePath = Path.Combine(agvbDirectory, "AGVB.xml");

                        // Check if the file exists
                        if (File.Exists(agvbFilePath))
                        {
                            // Load the XML file
                            XDocument xmlDoc = XDocument.Load(agvbFilePath);

                            // Get the current project path
                            UIDocument uiDoc = _uiapp.ActiveUIDocument;
                            Document doc = uiDoc?.Document;
                            if (doc == null)
                            {
                                ConsoleShow("ERROR: No active Revit document found.");
                                return;
                            }

                            string currentProjectPath = AG.U.ProjectFilePath(doc);

                            // Find the <Project> element for the current project
                            XElement projectElement = xmlDoc.Root.Elements("Project")
                                .FirstOrDefault(p => p.Attribute("Path")?.Value == currentProjectPath);

                            if (projectElement != null)
                            {
                                // Remove all bookmarks for this project
                                projectElement.RemoveAll();

                                // Save the updated XML file
                                xmlDoc.Save(agvbFilePath);

                                ConsoleShow("All bookmarks for the current project have been removed.");
                            }

                            else
                                ConsoleShow("No bookmarks found for the current project.");
                        }

                        else
                            ConsoleShow("ERROR: Bookmark file not found.");
                    }
                    catch (Exception ex)
                    {
                        ConsoleShow($"ERROR: Failed to reset bookmarks: {ex.Message}");
                    }

                    // Refresh the UI after deletion
                    RefreshBookmarks_Labels();
                    RefreshBookmarks_LEDs();
                }

                else
                    ConsoleShow("Bookmark reset cancelled.");
            }
        }

        private async void LabelClose_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Label label)
            {
                RefreshBookmarks_Labels();
                RefreshBookmarks_LEDs();

                // Cancel any ongoing task for this label
                if (_cancellationTokenSources.TryGetValue(label, out var cts))
                    cts.Cancel();

                var newCts = new CancellationTokenSource();
                _cancellationTokenSources[label] = newCts;
                var token = newCts.Token;

                label.Background = _clickedBackground;

                try
                {
                    await Task.Delay(250, token); // Wait briefly for user feedback
                }
                catch (TaskCanceledException)
                {
                    // Ignore the exception if the task is canceled
                }

                // Restore original background
                if (!token.IsCancellationRequested)
                    label.Background = _defaultBackground;

                _cancellationTokenSources.Remove(label);

                this.Hide();
                Command.sliderValue = this.Slider_Count.Value;
            }
        }

        private async void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Label label)
            {
                RefreshBookmarks_Labels();
                RefreshBookmarks_LEDs();

                // Cancel any ongoing task for this label
                if (_cancellationTokenSources.TryGetValue(label, out var cts))
                    cts.Cancel();

                var newCts = new CancellationTokenSource();
                _cancellationTokenSources[label] = newCts;
                var token = newCts.Token;

                label.Background = _clickedBackground;

                try
                {
                    await Task.Delay(250, token); // Wait briefly for user feedback
                }
                catch (TaskCanceledException)
                {
                    // Ignore the exception if the task is canceled
                }

                // Restore original background
                if (!token.IsCancellationRequested)
                    label.Background = _defaultBackground;

                _cancellationTokenSources.Remove(label);

                if (label?.Tag != null)
                {
                    string index = label.Tag.ToString();

                    // Get the Revit document
                    UIDocument uiDoc = _uiapp.ActiveUIDocument;
                    Document doc = uiDoc?.Document;

                    if (doc != null)
                    {
                        // Retrieve UniqueId from XML file
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string agvbDirectory = Path.Combine(appDataPath, "AG View Bookmarks");
                        string agvbFilePath = Path.Combine(agvbDirectory, "AGVB.xml");

                        if (!File.Exists(agvbFilePath))
                        {
                            ConsoleShow("ERROR: Bookmark file not found.");
                            return;
                        }

                        XDocument xmlDoc = XDocument.Load(agvbFilePath);

                        // Get project path
                        string projectPath = AG.U.ProjectFilePath(doc);

                        // Find the <Project> element
                        XElement projectElement = xmlDoc.Root.Elements("Project")
                            .FirstOrDefault(p => p.Attribute("Path")?.Value == projectPath);

                        if (projectElement == null)
                        {
                            ConsoleShow("ERROR: No bookmarks found for the current project.");
                            return;
                        }

                        // Find the <Bookmark> element matching the label index
                        XElement bookmarkElement = projectElement.Elements("Bookmark")
                            .FirstOrDefault(b => b.Element("Index")?.Value == index);

                        if (bookmarkElement == null)
                        {
                            ConsoleShow($"ERROR: No bookmark found for index: {index}");
                            return;
                        }

                        // Retrieve the UniqueId from the XML
                        string uniqueId = bookmarkElement.Element("UniqueId")?.Value;

                        if (!string.IsNullOrEmpty(uniqueId))
                        {
                            // Resolve the UniqueId to an ElementId
                            Element viewElement = doc.GetElement(uniqueId);
                            if (viewElement is View view)
                            {
                                try
                                {
                                    // Set the ActiveView using the resolved view
                                    uiDoc.ActiveView = view;
#if REVIT2024UP
                                    ConsoleShow($"Navigated to view: {view.Name} (ID: {view.Id.Value})");
#else
                                    ConsoleShow($"Navigated to view: {view.Name} (ID: {view.Id.IntegerValue})");
#endif
                                }
                                catch (Exception ex)
                                {
                                    ConsoleShow($"ERROR: Could not activate the view: {ex.Message}");
                                }
                            }

                            else
                                ConsoleShow($"ERROR: View not found or invalid for UniqueId: {uniqueId}");
                        }

                        else
                            ConsoleShow("ERROR: UniqueId is missing or invalid in XML.");
                    }

                    else
                        ConsoleShow("ERROR: Active document is null.");
                }

                else
                    ConsoleShow("ERROR: Label Tag does not contain an index.");
            }
        }

        private int ExtractTagInt(string input, string delimiter)
        {
            // Return default value if input or delimiter is invalid
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(delimiter))
                return 0; 

            // Split the string by the delimiter
            string[] parts = input.Split(new[] { delimiter }, StringSplitOptions.None);

            // Return the integer part after the delimiter
            if (parts.Length > 1 && int.TryParse(parts[1], out int result))
                return result;

            // Return default value if parsing fails
            return 0; 
        }

    }
}
