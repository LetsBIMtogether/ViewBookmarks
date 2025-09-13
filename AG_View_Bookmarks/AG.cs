// AG.cs

#region Namespaces
using Autodesk.Revit.DB;
#endregion

namespace AG
{
    class U
    {
        /// <summary>
        /// Determines the file path of the project. If worksharing is enabled, returns the central model path.
        /// Otherwise, returns the local file path.
        /// </summary>
        /// <param name="doc">The Revit document.</param>
        /// <returns>The file path of the project or central model.</returns>
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
