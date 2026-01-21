#if UNITY_EDITOR
using UnityEditor;

namespace Member_Han.Modules.FileSystem
{
    public class EditorFileBrowserService : IFileBrowserService
    {
        public string[] OpenFilePanel(string title, string directory, string extension, bool multiselect)
        {
            string path = EditorUtility.OpenFilePanel(title, directory, extension);
            if (string.IsNullOrEmpty(path))
            {
                return new string[0];
            }
            return new string[] { path };
        }
    }
}
#endif
