#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// 에디터 환경용 모델 임포터 서비스
    /// AssetDatabase를 사용하여 프로젝트 내 파일을 로드
    /// </summary>
    public class EditorModelImporterService : IModelImporterService
    {
        #region IModelImporterService 구현
        public System.Threading.Tasks.Task<GameObject> ImportAsync(string path)
        {
            return System.Threading.Tasks.Task.FromResult(ImportInternal(path));
        }
        #endregion

        #region 내부 로직
        private GameObject ImportInternal(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // 절대 경로를 Assets 상대 경로로 변환
            if (Path.IsPathRooted(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    Debug.LogWarning("EditorModelImporterService: 파일이 프로젝트 Assets 폴더 내에 있어야 합니다.");
                    return null;
                }
            }

            GameObject loadedObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (loadedObject != null)
            {
                // Prefab 참조가 아닌 새로운 인스턴스 생성
                return Object.Instantiate(loadedObject); 
            }
            return null;
        }
        #endregion
    }
}
#endif
