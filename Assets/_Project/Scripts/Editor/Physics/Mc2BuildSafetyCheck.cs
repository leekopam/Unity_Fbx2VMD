using System.IO;
using System.Text;
using Fbx2Vmd.ClothPhysics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fbx2Vmd.EditorPhysics
{
    /// <summary>
    /// MC2/Burst 배포 안전 점검.
    /// - Burst AOT 설정 존재 여부 (없으면 빌드에서 Burst가 꺼져 수십 배 느려지는 사례 방지)
    /// - Managed Stripping Level High 금지 (Burst/리플렉션 파괴 실보고)
    /// - 스크립팅 백엔드(IL2CPP) 확인
    /// - 빌드 후 클로스 스모크 마커([MC2_SMOKE])가 Player.log에 남는지 안내
    /// </summary>
    public static class Mc2BuildSafetyCheck
    {
        [MenuItem("Fbx2Vmd/MC2 QA/빌드 안전 점검")]
        public static void Run()
        {
            var sb = new StringBuilder("[MC2 빌드 안전 점검]\n");
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;

            // 1) 스크립팅 백엔드
            var backend = PlayerSettings.GetScriptingBackend(target);
            sb.AppendLine($"스크립팅 백엔드: {backend}");

            // 2) Managed Stripping — High는 Burst/리플렉션 기반 에셋과 비호환 사례 다수
            var strip = PlayerSettings.GetManagedStrippingLevel(target);
            sb.AppendLine($"Managed Stripping: {strip} " +
                (strip >= ManagedStrippingLevel.Medium
                    ? "— ⚠ Medium 이상은 MC2/Burst에서 실패 사례 있음. 빌드 스모크 필수"
                    : "— OK"));

            // 3) Burst AOT 설정 파일
            string burstAot = $"ProjectSettings/BurstAotSettings_{EditorUserBuildSettings.activeBuildTarget}.json";
            bool hasBurstAot = File.Exists(burstAot);
            sb.AppendLine($"Burst AOT 설정: {(hasBurstAot ? burstAot : "없음 — Burst 기본값 사용 (보통 AOT 켜짐, 명시 생성 권장)")}");

            // 4) MC2 PreBuild 사용 여부 — 생성물 의존이라 지금은 기본 OFF
            var setups = Object.FindObjectsOfType<CharacterPhysicsSetup>(true);
            int prebuildOn = 0, total = 0;
            foreach (var s in setups)
            {
                total++;
                if (s.usePreBuild) prebuildOn++;
            }
            sb.AppendLine($"PreBuild 사용 셋업: {prebuildOn}/{total} " +
                "(런타임 생성 클로스는 PreBuild 미지원 권장 — 메시/본 구조 변경 시 무효화 위험)");

            // 5) 빌드 스모크 안내
            sb.AppendLine("빌드 검증: 플레이어 실행 후 Player.log에서 '[MC2_SMOKE]' 라인을 확인하세요 " +
                "(valid=클로스 수가 0이면 MC2 런타임 실패).");

            Debug.Log(sb.ToString());
        }

        /// <summary>빌드 직후 로그에 요약을 남기는 파이프라인 훅.</summary>
        class PostBuildReporter : IPostprocessBuildWithReport
        {
            public int callbackOrder => 9999;
            public void OnPostprocessBuild(BuildReport report)
            {
                var target = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup);
                var strip = PlayerSettings.GetManagedStrippingLevel(target);
                Debug.Log($"[MC2 빌드] {report.summary.platform} 완료 " +
                    $"(크기 {report.summary.totalSize / (1024 * 1024)}MB) — " +
                    $"ManagedStripping={strip}. 플레이어 로그에서 [MC2_SMOKE] 마커를 확인하세요.");
            }
        }
    }
}
