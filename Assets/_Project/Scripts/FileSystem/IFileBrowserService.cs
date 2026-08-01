namespace Fbx2Vmd.Modules.FileSystem
{
    public interface IFileBrowserService
    {
        string[] OpenFilePanel(string title, string directory, string extension, bool multiselect);
    }
}
