using SFB;

namespace Member_Han.Modules.FileSystem
{
    /// <summary>
    /// StandaloneFileBrowser를 사용하는 런타임 파일 브라우저 서비스
    /// 에디터 및 빌드 환경 모두에서 작동
    /// </summary>
    public class RuntimeFileBrowserService : IFileBrowserService
    {
        #region IFileBrowserService 구현
        public string[] OpenFilePanel(string title, string directory, string extension, bool multiselect)
        {
            return StandaloneFileBrowser.OpenFilePanel(title, directory, extension, multiselect);
        }
        #endregion
    }
}
