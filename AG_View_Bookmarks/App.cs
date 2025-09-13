// App.cs

#region Namespaces
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Reflection;
using System.Windows.Media.Imaging;
#endregion

namespace AG_View_Bookmarks
{
    internal class App : IExternalApplication
    {
        public static myWPF myWPFInstance = null;

        public Result OnStartup(UIControlledApplication a)
        {
            try
            {
                RibbonPanel rPanel = a.CreateRibbonPanel("AG View Bookmarks");

                string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

                PushButtonData buttonData = new PushButtonData(
                    "AG_View_Bookmarks",
                    "Show Window /\nHide Window",
                    thisAssemblyPath,
                    "AG_View_Bookmarks.Command"
                    );

                PushButton pButton = rPanel.AddItem(buttonData) as PushButton;

                pButton.ToolTip = "Shows or hides AG View Bookmarks window.";

                // Set the icon from the resource path
                BitmapImage bLargeImage = new BitmapImage();
                bLargeImage.BeginInit();
                bLargeImage.UriSource = new Uri("pack://application:,,,/AG_View_Bookmarks;component/Images/bookmark-ui-32.ico");
                bLargeImage.EndInit();
                pButton.LargeImage = bLargeImage;

                // Use https://convertio.co to convert PDF to HTML :)
                ContextualHelp contextHelp = new ContextualHelp(ContextualHelpType.Url, "https://letsbimtogether.com/__revit/VB/F1.html");
                pButton.SetContextualHelp(contextHelp);

                // Attach the ViewActivated event
                a.ViewActivated += OnViewActivated;
            }
            catch (Exception ex)
            {
                a.ViewActivated -= OnViewActivated;
                TaskDialog.Show("Error", ex.Message);
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            // Detach event and cleanup
            a.ViewActivated -= OnViewActivated;

            if (myWPFInstance != null)
            {
                myWPFInstance.Close();
                myWPFInstance = null;
            }

            return Result.Succeeded;
        }

        private string _lastDocumentPath = string.Empty;

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            var uiApp = sender as UIApplication;
            if (uiApp?.ActiveUIDocument?.Document == null)
                return;

            var doc = uiApp.ActiveUIDocument.Document;
            string currentDocumentPath = AG.U.ProjectFilePath(doc);

            if (_lastDocumentPath != currentDocumentPath)
            {
                _lastDocumentPath = currentDocumentPath;

                if (myWPFInstance == null)
                {
                    myWPFInstance = new myWPF(uiApp);
                }

                myWPFInstance.RefreshBookmarks_Labels();
                myWPFInstance.RefreshBookmarks_LEDs();
                myWPFInstance.ConsoleCurrentDoc();
            }
        }

    }
}
