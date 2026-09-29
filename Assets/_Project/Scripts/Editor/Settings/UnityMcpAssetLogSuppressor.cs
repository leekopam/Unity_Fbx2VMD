using System;
using System.Reflection;
using UnityEditor;

namespace Fbx2Vmd.Settings.EditorTools
{
    // MCP 플러그인(McpLogRecord)이 Assets/UnityMCP/Log 아래에 호출 로그를 기록하면
    // 에셋 감시·리임포트가 반복돼 무거운 작업 중 브리지 무응답으로 이어질 수 있다.
    // 플러그인은 도메인 로드 시 EditorPrefs 값을 정적 캐시하므로
    // 프리퍼런스와 캐시를 함께 끈다(IsEnabled setter가 둘 다 갱신함).
    [InitializeOnLoad]
    internal static class UnityMcpAssetLogSuppressor
    {
        static UnityMcpAssetLogSuppressor()
        {
            try
            {
                Type recordType = Type.GetType(
                    "MCPForUnity.Editor.Helpers.McpLogRecord, MCPForUnity.Editor");
                PropertyInfo isEnabled = recordType?.GetProperty("IsEnabled",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (isEnabled != null && isEnabled.CanWrite)
                    isEnabled.SetValue(null, false);
            }
            catch (Exception)
            {
                // 패키지 구조가 달라도 프리퍼런스 설정으로 다음 로드부터 비활성화된다.
            }
            EditorPrefs.SetBool("MCPForUnity.LogRecordEnabled", false);
        }
    }
}
