using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 프로젝트 설정 계약: Linear 색공간, Mono 백엔드, .NET Standard 2.1,
    /// 레거시 입력, Built-in 렌더 파이프라인(PPv2 전제) — 업그레이드 마이그레이션으로
    /// 값이 조용히 바뀌면 렌더링/동작이 깨진다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class ProjectSettingsContractTests
    {
        [Test]
        public void RenderingAndRuntimeSettings_KeepContract()
        {
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear),
                "색공간이 Linear가 아님 — 툰/PPv2 룩이 깨짐");

            // 실제 상태: 파이프라인 에셋 미지정(Built-in RP) — URP는 패키지만 설치돼 있고
            // GraphicsSettings/QualitySettings 어디에도 에셋이 할당되지 않는다.
            // Built-in이어야 PostProcessing v2(ToonStageLook)가 실제로 동작한다.
            // URP로 전환하려는 의도가 생기면 이 어서트가 선행 신호가 된다.
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Null,
                "렌더 파이프라인 에셋이 할당됨 — Built-in 전제(PPv2)와 달라짐");
            // 비활성 품질레벨의 오버라이드도 잠복 경로 — 전 레벨이 미지정이어야 한다
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(i), Is.Null,
                    $"품질레벨 {QualitySettings.names[i]}에 파이프라인 할당 — 레벨 전환 시 SRP 활성화");
            }

            var buildTarget = NamedBuildTarget.Standalone;
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(buildTarget);
            ApiCompatibilityLevel apiLevel = PlayerSettings.GetApiCompatibilityLevel(buildTarget);
#if UNITY_6000_0_OR_NEWER
            ApiCompatibilityLevel expectedApi = ApiCompatibilityLevel.NET_Standard;
#else
            ApiCompatibilityLevel expectedApi = ApiCompatibilityLevel.NET_Standard_2_0;
#endif
            Assert.That(backend, Is.EqualTo(ScriptingImplementation.Mono2x),
                "스크립팅 백엔드가 Mono가 아님 — WinForms/NAudio/수동 LoadLibrary 경로가 깨짐");
            Assert.That(apiLevel, Is.EqualTo(expectedApi),
                "API 호환 수준이 .NET Standard 2.1 계열이 아님");

            string settingsText = File.ReadAllText(
                Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset"));
            Assert.That(settingsText, Does.Contain("activeInputHandler: 0"),
                "레거시 Input Manager가 아님 — Input.* 호출 경로가 깨짐");
        }
    }
}
