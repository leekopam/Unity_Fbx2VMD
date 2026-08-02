namespace Fbx2Vmd.FileSystem
{
    public interface IFileBrowserService
    {
        string[] OpenFilePanel(string title, string directory, string extension, bool multiselect);
    }
}
