using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 표면 보정 디스크 캐시의 수동 관리 메뉴.
    /// 재계산 요청은 다음 재생 준비 때 캐시 로드를 건너뛰고 새로 계산한다.
    /// </summary>
    internal static class NativeSkinningCorrectionCacheMenu
    {
        private const string DriverTypeName =
            "Fbx2Vmd.FBXImporter.NativeSkinningCorrectionPlaybackDriver";
        private const string StoreTypeName =
            "Fbx2Vmd.FBXImporter.NativeSkinningCorrectionCacheFileStore";
        private const string ForceRecalculatePath =
            "Tools/FBXImporter/표면 보정 캐시/다음 재생 시 강제 재계산";
        private const string DeleteAllPath =
            "Tools/FBXImporter/표면 보정 캐시/캐시 파일 전체 삭제";

        [MenuItem(ForceRecalculatePath)]
        private static void ForceRecalculate()
        {
            System.Type driverType = RequireType(DriverTypeName);
            if (driverType == null)
            {
                return;
            }
            PropertyInfo property = driverType.GetProperty(
                "ForceCacheRecalculate",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (property == null)
            {
                Debug.LogWarning(
                    "[NativePrepCache] 재계산 플래그를 찾지 못했습니다.");
                return;
            }
            property.SetValue(null, true);
            Debug.Log(
                "[NativePrepCache] 다음 재생 준비에서 캐시를 무시하고 " +
                "표면 보정을 새로 계산합니다.");
        }

        [MenuItem(DeleteAllPath)]
        private static void DeleteAll()
        {
            System.Type storeType = RequireType(StoreTypeName);
            if (storeType == null)
            {
                return;
            }
            MethodInfo method = storeType.GetMethod(
                "GetCacheDirectory",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogWarning(
                    "[NativePrepCache] 캐시 경로를 찾지 못했습니다.");
                return;
            }
            string directory = (string)method.Invoke(null, null);
            if (!Directory.Exists(directory))
            {
                Debug.Log("[NativePrepCache] 삭제할 캐시 파일이 없습니다.");
                return;
            }
            Directory.Delete(directory, recursive: true);
            Debug.Log(
                $"[NativePrepCache] 캐시를 전부 삭제했습니다: {directory}");
        }

        private static System.Type RequireType(string typeName)
        {
            System.Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                typeName,
                throwOnError: false);
            if (type == null)
            {
                Debug.LogWarning(
                    $"[NativePrepCache] 타입을 찾지 못했습니다: {typeName}");
            }
            return type;
        }
    }
}
