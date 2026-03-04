// U.cs
// (1/28/26)

#region Namespaces
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
#endregion

namespace AG
{
    /// <summary>
    /// Misc Revit API software utilities by www.LetsBIMtogether.com
    /// </summary>
    public static class U
    {
        /// <summary>
        /// Same as Assembly.GetExecutingAssembly()
        /// </summary>
        public static Assembly exeAsm = Assembly.GetExecutingAssembly();

        /// <summary>
        /// Gets icon for WinForms.
        /// </summary>
        /// <param name="resourceName">Path to resource. Example: "MY_NAMESPACE.Resources.icon.ico"</param>
        /// <returns></returns>
        public static Icon GetWinFormsIcon(string resourceName)
        {
            using (var stream = exeAsm.GetManifestResourceStream(resourceName))
            {
                return new Icon(stream);
            }
        }

        /// <summary>
        /// Gets icon for Revit ribbon buttons. (Embedded Resource)
        /// </summary>
        /// <param name="resourceName">Path to resource. Example: "MY_NAMESPACE.Resources.icon.ico"</param>
        /// <returns></returns>
        public static BitmapImage GetRibbonBitmapER(string resourceName)
        {
            // Create a Stream object (implements IDisposable)
            using (var stream = exeAsm.GetManifestResourceStream(resourceName))
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = stream;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                return img;
            }
            // ^-- when execution leaves the block, stream.Dispose() is called
        }

        /// <summary>
        /// Gets icon for Revit ribbon buttons. (Resource)
        /// </summary>
        /// <param name="resourceName">Path to resource. Example: "Images/bookmark-ui-32.ico"</param>
        /// <returns></returns>
        public static BitmapImage GetRibbonBitmapR(string relativePath)
        {
            // Build pack URI using the executing assembly name
            string asmName = exeAsm.GetName().Name;

            var uri = new Uri($"pack://application:,,,/{asmName};component/{relativePath}", UriKind.Absolute);

            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = uri;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        /// <summary>
        /// Cancellation flag for stopping batch operations.
        /// Used by <see cref="DeleteElements"/>.
        /// </summary>
        public class CancelFlagInfo
        {
            /// <summary>
            /// Stop operation if this property is true.
            /// </summary>
            public bool Stop { get; set; } = false;
        }

        /// <summary>
        /// Deletes the given element IDs in one transaction.
        /// Can not be inside a Transaction, can be inside a TransactionGroup.
        /// Returns (success, failed). Stops early if cancel flag is set.
        /// </summary>
        /// <param name="doc">Revit document.</param>
        /// <param name="ids">ElementIds to delete.</param>
        /// <param name="cancel">Cancellation flag.</param>
        /// <param name="reportProgress">Optional progress callback (current count).</param>
        public static (List<ElementId> success, List<ElementId> failed) DeleteElements(Document doc, IEnumerable<ElementId> ids, CancelFlagInfo cancel, Action<int> reportProgress = null)
        {
            var success = new List<ElementId>();
            var failed = new List<ElementId>();
            var items = ids?.ToList() ?? new List<ElementId>();

            if (items.Count == 0)
                return (success, failed);

            int total = items.Count;
            int current = 0;

            using (var t = new Transaction(doc, "AGU-DeleteElements"))
            {
                t.Start();

                foreach (ElementId id in items)
                {
                    if (cancel.Stop)
                    {
                        t.RollBack();

                        return (success, items);
                    }

                    try
                    {
                        /*
                        ^ IEnumerable<T>    <-- foreach                     // Interface
                        ^ ICollection<T>    <-- Add, Remove, Count          // Interface
                        ^ IList<T>          <-- indexing, insert at index   // Interface (rules / capabilities)
                        ^ List<T>           <-- concrete implementation     // Class (actual object with data)
                        */

                        ICollection<ElementId> deleted = doc.Delete(id);

                        if (deleted != null && deleted.Count > 0)
                            success.Add(id);

                        else
                            failed.Add(id);
                    }
                    catch
                    {
                        // Revit sometimes throws even if the element got deleted...
                        if (doc.GetElement(id) == null)
                            success.Add(id);

                        else
                            failed.Add(id);
                    }

                    current++;
                    reportProgress?.Invoke(current);

                    /*
                    if(Globals.Debug)
                        System.Threading.Thread.Sleep(100);*/
                }

                t.Commit();
            }

            return (success, failed);
        }

        /// <summary>
        /// Collects all schedule views in the document, with optional filtering
        /// for template schedules and revision (titleblock) schedules.
        /// </summary>
        /// <param name="doc">Revit document to search.</param>
        /// <param name="includeTemplates">If true, includes schedule templates in the results.</param>
        /// <param name="includeRevisionSchedules">If true, includes titleblock revision schedules.</param>
        public static List<ViewSchedule> GetAllSchedules(Document doc, bool includeTemplates = false, bool includeRevisionSchedules = false)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(vs =>
                    (includeTemplates || !vs.IsTemplate) &&
                    (includeRevisionSchedules || !vs.IsTitleblockRevisionSchedule))
                // true || anything = true ... flag ...
                .ToList();
        }

        /// <summary>
        /// Collects all sheets in the document, with optional filtering for template sheets.
        /// Optionally excludes view templates.
        /// </summary>
        /// <param name="doc">Revit document to search.</param>
        /// <param name="includeTemplates">If true, includes sheet templates.</param>
        public static List<ViewSheet> GetAllSheets(Document doc, bool includeTemplates = false)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(vs => includeTemplates || !vs.IsTemplate)
                .ToList();
        }

        /// <summary>
        /// Collects all views in the document (NOT sheets, NOT schedules).
        /// Optionally excludes view templates.
        /// </summary>
        /// <param name="doc">Revit document to search.</param>
        /// <param name="includeTemplates">If true, includes view templates.</param>
        public static List<View> GetAllViews(Document doc, bool includeTemplates = false)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v =>
                    !(v is ViewSheet) &&
                    !(v is ViewSchedule) &&
                    (includeTemplates || !v.IsTemplate))
                .ToList();
        }

        /// <summary>
        /// Returns ElementId's of all Views, Sheets, Legends, and Schedules.
        /// Has an option to Exclude active view.
        /// </summary>
        /// <param name="doc">Revit document to search.</param>
        /// <param name="excludeActiveView">If true, excludes active view</param>
        public static List<ElementId> GetAllDrawingsEId(Document doc, bool excludeActiveView = true)
        {
            List<ElementId> ids = U.GetAllSchedules(doc).Select(s => s.Id)
                .Concat(U.GetAllViews(doc).Select(sh => sh.Id))
                .Concat(U.GetAllSheets(doc).Select(sh => sh.Id))
                .ToList();

            if (ids.Count == 0)
                return null;

            // Filter out the system views
            ids = ids.Where(id =>
            {
                var v = doc.GetElement(id) as View;

                if (v == null)
                    return true;  // keep non-views

                return v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser;
            }).ToList();

            return ids;
        }

        /// <summary>
        /// User-friendly display name for a Revit view based on its view type.
        /// </summary>
        public static string GetViewTypeName(View v)
        {
            switch (v.ViewType)
            {
                case ViewType.FloorPlan: return "Floor Plan";
                case ViewType.CeilingPlan: return "Ceiling Plan";
                case ViewType.EngineeringPlan: return "Engineering Plan";
                case ViewType.AreaPlan: return "Area Plan";
                case ViewType.Elevation: return "Elevation";
                case ViewType.Section: return "Section";
                case ViewType.Detail: return "Detail";
                case ViewType.ThreeD: return "3D Drawing";
                case ViewType.DraftingView: return "Drafted Sketch";
                case ViewType.Legend: return "Legend";
                case ViewType.Rendering: return "Rendering";
                case ViewType.Walkthrough: return "Walkthrough";
                case ViewType.Schedule: return "Schedule";

                default: return v.ViewType.ToString();
            }
        }

        /// <summary>
        /// Architectural drawing type for the given view element:
        /// "Floor Plan", "Sheet", "Legend", or "Schedule".
        /// Anything else returns "<< Unknown >>".
        /// </summary>
        /// <param name="doc">Revit document to search.</param>
        /// <param name="id">View's ElementId.</param>
        public static string GetDrawingKind(Document doc, ElementId id)
        {
            Element e = doc.GetElement(id);

            if (e == null)
                return "<< Null Element >>";

            if (e is ViewSheet)
                return "Sheet";

            if (e is ViewSchedule)
                return "Schedule";

            if (e is View v)
                return "View (" + GetViewTypeName(v) + ")";

            return "<< Unknown >>";
        }

        /// <summary>
        /// Determines the file path of the project. If worksharing is enabled, returns the central model path.
        /// Otherwise, returns the local file path.
        /// </summary>
        /// <param name="doc">The Revit document.</param>
        public static string ProjectFilePath(Document doc)
        {
            // Check if worksharing is enabled
            if (doc.IsWorkshared)
            {
                // Get the central model path if worksharing is enabled
                ModelPath centralModelPath = doc.GetWorksharingCentralModelPath();

                if (centralModelPath != null)
                {
                    // Return the central model's full path
                    return ModelPathUtils.ConvertModelPathToUserVisiblePath(centralModelPath);
                }
            }

            // If not workshared, return the local file path
            return doc.PathName;
        }

    }
}
