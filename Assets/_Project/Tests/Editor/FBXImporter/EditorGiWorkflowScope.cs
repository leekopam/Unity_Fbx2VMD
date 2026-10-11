using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// 씬의 Auto Generate(Iterative) 상태에서 FBX 렌더러 instantiate가 Enlighten 수집을
    /// 유발해 에디터가 장시간 무응답하므로, 테스트 동안만 수동 베이크 모드로 전환하는 헬퍼.
    /// 이전 모드는 nullable로 들고 있다가 값이 있을 때만 복원한다 — SetUp이 중간에
    /// 실패해 기본값이 덮어쓰이는 경우를 막기 위함이다.
    /// </summary>
    internal static class EditorGiWorkflowScope
    {
        public static Lightmapping.GIWorkflowMode Push()
        {
            Lightmapping.GIWorkflowMode previous = Lightmapping.giWorkflowMode;
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
            return previous;
        }

        public static void Pop(Lightmapping.GIWorkflowMode? previous)
        {
            if (previous.HasValue)
            {
                Lightmapping.giWorkflowMode = previous.Value;
            }
        }
    }
}
