using UnityEngine;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// 스텁 모델 임포터 서비스
    /// 실제 런타임 임포터가 구현되지 않았을 때 사용
    /// </summary>
    public class StubModelImporterService : IModelImporterService
    {
        #region IModelImporterService 구현
        public System.Threading.Tasks.Task<GameObject> ImportAsync(string path)
        {
            Debug.LogError("Runtime Model Importer가 구현되지 않았습니다. FBXImporter 플러그인이 누락되었습니다.");
            return System.Threading.Tasks.Task.FromResult<GameObject>(null);
        }
        #endregion
    }
}
