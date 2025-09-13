// Command.cs

#region Namespaces
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace AG_View_Bookmarks
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        // private static myWPF mainWindow = null;
        public static double sliderValue = 2;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements
        )
        {
            // Access the centralized instance in the App class
            if (App.myWPFInstance == null || !App.myWPFInstance.IsVisible)
            {
                App.myWPFInstance = new myWPF(commandData.Application);
                App.myWPFInstance.RefreshBookmarks_Labels();
                App.myWPFInstance.RefreshBookmarks_LEDs();
                App.myWPFInstance.Slider_Count.Value = sliderValue;
                App.myWPFInstance.Show();
            }
            else
            {
                App.myWPFInstance.Hide();
                sliderValue = App.myWPFInstance.Slider_Count.Value;
            }

            return Result.Succeeded;
        }
    }
}