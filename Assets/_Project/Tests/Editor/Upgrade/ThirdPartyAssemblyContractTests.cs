using NUnit.Framework;
using System;
using System.Text;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 서드파티 어셈블리/타입 해상 계약. 리플렉션으로만 검사하므로
    /// 어셈블리가 깨져도 테스트 어셈블리 자체는 컴파일되어 결과가 보고된다.
    /// 업그레이드 후 어셈블리명이 바뀌면 여기서 즉시 빨간불.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class ThirdPartyAssemblyContractTests
    {
        private static readonly string[] RequiredAssemblies =
        {
            "MagicaClothV2", "MagicaClothV2.Editor",
            "Assembly-CSharp-firstpass",              // Final IK, StandaloneFileBrowser
            "uLipSync.Runtime", "uLipSync.Editor",
            "MMD4Mecanim", "MMD4MecanimEditor",
            "AssimpNet",
            "NAudio.Core", "NAudio.Wasapi", "NAudio.WinMM",
            "Ookii.Dialogs",
            "Unity.Recorder", "Unity.Recorder.Editor", "Unity.Recorder.Base",
            "Unity.Postprocessing.Runtime",           // PPv2 — 제거 결정 시 이 항목도 삭제
            "Unity.RenderPipelines.Universal.Runtime",
            "Unity.Animation.Rigging",
        };

        private static readonly (string AssemblyName, string TypeName)[] RequiredTypes =
        {
            ("MagicaClothV2", "MagicaCloth2.MagicaManager"),
            ("MagicaClothV2", "MagicaCloth2.MagicaCloth"),
            ("Assembly-CSharp-firstpass", "RootMotion.FinalIK.BipedIK"),
            ("Assembly-CSharp-firstpass", "RootMotion.FinalIK.GrounderBipedIK"),
            ("Assembly-CSharp-firstpass", "RootMotion.FinalIK.LimbIK"),
            ("Assembly-CSharp-firstpass", "RootMotion.FinalIK.IKSolverTrigonometric"),
            ("uLipSync.Runtime", "uLipSync.uLipSync"),
            ("uLipSync.Runtime", "uLipSync.Profile"),
            ("uLipSync.Runtime", "uLipSync.uLipSyncBlendShape"),
            ("Assembly-CSharp-firstpass", "SFB.StandaloneFileBrowser"),
            ("MMD4Mecanim", "MMD4MecanimData"),
            ("MMD4Mecanim", "MMD4MecanimAnim"),
            ("MMD4MecanimEditor", "MMD4MecanimAssetPostprocessor"),
            ("AssimpNet", "Assimp.AssimpContext"),
            ("Unity.Recorder.Editor", "UnityEditor.Recorder.RecorderController"),
            ("Unity.Recorder.Editor", "UnityEditor.Recorder.MovieRecorderSettings"),
            ("Unity.Animation.Rigging", "UnityEngine.Animations.Rigging.RigBuilder"),
            ("Unity.Postprocessing.Runtime", "UnityEngine.Rendering.PostProcessing.PostProcessProfile"),
            ("Unity.RenderPipelines.Universal.Runtime", "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"),
        };

        [Test]
        public void RequiredAssemblies_AreLoaded()
        {
            // 대다수 대상이 gitignored 자산 소속 — 미프로비저닝 환경에서는 Ignore
            UpgradeReadinessReport.RequireProvisionedAssets();

            var failures = new StringBuilder();

            foreach (string assemblyName in RequiredAssemblies)
            {
                try
                {
                    var assembly = System.Reflection.Assembly.Load(assemblyName);
                    if (assembly == null)
                    {
                        failures.AppendLine($"{assemblyName}: 로드 실패");
                    }
                }
                catch (Exception exception)
                {
                    failures.AppendLine($"{assemblyName}: {exception.GetType().Name}");
                }
            }

            Assert.That(failures.ToString(), Is.Empty, "로드되지 않는 서드파티 어셈블리가 있음");
        }

        [Test]
        public void RequiredTypes_Resolve()
        {
            UpgradeReadinessReport.RequireProvisionedAssets();

            var failures = new StringBuilder();

            foreach (var (assemblyName, typeName) in RequiredTypes)
            {
                Type type = null;
                try
                {
                    type = Type.GetType($"{typeName}, {assemblyName}");
                }
                catch (Exception exception)
                {
                    failures.AppendLine($"{typeName}, {assemblyName}: {exception.GetType().Name}");
                    continue;
                }

                if (type == null)
                {
                    failures.AppendLine($"{typeName}, {assemblyName}: 타입 없음");
                }
            }

            Assert.That(failures.ToString(), Is.Empty,
                "해상되지 않는 서드파티 타입이 있음 — 어셈블리명/네임스페이스 변경 가능성");
        }
    }
}
