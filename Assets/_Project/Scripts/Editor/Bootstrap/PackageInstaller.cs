using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Fbx2Vmd.EditorTools
{
    /// <summary>
    /// 헤드리스 UPM 패키지 설치기.
    /// -batchmode에서 -executeMethod로 호출하며 -quit 없이 에디터가 살아 있어야
    /// 비동기 AddAndRemove 요청이 완료된다.
    /// </summary>
    public static class PackageInstaller
    {
        static readonly string[] PackagesToAdd =
        {
            "https://github.com/lilxyzw/lilToon.git?path=Assets/lilToon#2.3.4",
        };

        static readonly string[] PackagesToRemove = { };

        const double TimeoutSeconds = 600;

        static AddAndRemoveRequest _request;
        static double _deadline;

        public static void Install()
        {
            Debug.Log($"[PackageInstaller] 추가: {string.Join(", ", PackagesToAdd)}");
            _request = Client.AddAndRemove(
                packagesToAdd: PackagesToAdd,
                packagesToRemove: PackagesToRemove);
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null)
            {
                return;
            }

            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] UPM 응답 시간 초과.");
                    EditorApplication.Exit(2);
                }
                return;
            }

            EditorApplication.update -= Poll;

            if (_request.Status == StatusCode.Success)
            {
                var names = _request.Result.Select(p => $"{p.name}@{p.version}");
                Debug.Log($"[PackageInstaller] 해결 완료: {string.Join(", ", names)}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PackageInstaller] 실패: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
