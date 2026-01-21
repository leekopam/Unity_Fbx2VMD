using UnityEngine;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// 모델 임포터 서비스 인터페이스
    /// 에디터/런타임 환경에 따라 다른 구현체를 사용하기 위한 추상화
    /// </summary>
    public interface IModelImporterService
    {
        System.Threading.Tasks.Task<GameObject> ImportAsync(string path);
    }
}
