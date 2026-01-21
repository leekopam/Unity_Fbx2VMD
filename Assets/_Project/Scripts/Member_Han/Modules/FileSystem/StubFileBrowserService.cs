
using UnityEngine;

namespace Member_Han.Modules.FileSystem
{
    public class StubFileBrowserService : IFileBrowserService
    {
        public string[] OpenFilePanel(string title, string directory, string extension, bool multiselect)
        {
            Debug.LogError("Runtime File Browser not implemented. Missing StandaloneFileBrowser plugin.");
            return new string[0];
        }
    }
}
