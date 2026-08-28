using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Assimp 네이티브 DLL의 탐색과 로드를 담당함.
    /// </summary>
    public static class AssimpLibraryLoader
    {
        private const string ASSIMP_DLL_NAME = "assimp.dll";
        private const string ASSIMP_PLUGIN_FOLDER = "Assimp-net";

        public static bool IsLoaded = false;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern System.IntPtr LoadLibrary(string lpFileName);

        public static void LoadLibrary()
        {
            if (IsLoaded) return;

            // 빌드 환경 및 에디터 환경을 모두 고려한 검색 경로 목록
            string[] possiblePaths = new string[]
            {
                // 에디터 기본 경로 (Assets/Plugins/Assimp-net/assimp.dll)
                Path.Combine(Application.dataPath, "Plugins", ASSIMP_PLUGIN_FOLDER, ASSIMP_DLL_NAME),

                // 빌드: 실행 파일 옆 Plugins 폴더
                Path.Combine(Application.dataPath, "Plugins", ASSIMP_DLL_NAME),

                // 빌드: x86_64 서브폴더
                Path.Combine(Application.dataPath, "Plugins", "x86_64", ASSIMP_DLL_NAME),

                // 빌드: Assimp-net 서브폴더 보존 시
                Path.Combine(Application.dataPath, "Plugins", ASSIMP_PLUGIN_FOLDER, ASSIMP_DLL_NAME)
            };

            string validPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    validPath = path;
                    break;
                }
            }

            if (validPath == null)
            {
                Debug.LogError($"[FBXImport] assimp.dll을 찾을 수 없음. 검색 경로:\n{string.Join("\n", possiblePaths)}");
                return;
            }

            Debug.Log($"[FBXImport] 네이티브 라이브러리 찾음. 경로={validPath}");
            System.IntPtr handle = LoadLibrary(validPath);

            if (handle == System.IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.LogError($"[FBXImport] 네이티브 라이브러리 불러오기 실패함. 오류 코드={errorCode}, 경로={validPath}");
            }
            else
            {
                Debug.Log($"[FBXImport] 네이티브 라이브러리 불러오기 완료됨. 핸들={handle}");
                IsLoaded = true;
            }
        }
    }
}
