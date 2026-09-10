using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public sealed class HumanoidNativeSkinningQualityTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string TargetRendererName = "U_Char_2";
        private const float SharpFoldLimitDegrees = 150f;

        private static readonly NativeSkinningCase[] Cases =
        {
            new NativeSkinningCase(
                900,
                new[] { 17178, 16836, 16834, 16829 },
                new[,] { { 2, 3, 1 }, { 2, 1, 0 } }),
            new NativeSkinningCase(
                9974,
                new[] { 17307, 17309, 17310, 17314 },
                new[,] { { 2, 1, 0 }, { 2, 3, 1 } })
        };

        [TestCaseSource(nameof(Cases))]
        public void Given_KnownNativeSkinningPose_When_EvaluatingPlayback_Then_ElbowSurfaceDoesNotSharpFold(
            NativeSkinningCase testCase)
        {
            RequireLocalFixture();
            EnsureHumanoidClipImport();
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                    ClipAssetPath);
                Invoke(
                    controller,
                    "PrepareWithArmDirectionReference",
                    animator,
                    clip,
                    sourceModel);
                SkinnedMeshRenderer renderer = animator
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(candidate => candidate.name == TargetRendererName);

                float timeSeconds = testCase.FrameIndex / clip.frameRate;
                Assert.That((bool)Invoke(controller, "Seek", timeSeconds), Is.True);
                float angleDegrees = MeasureFoldAngle(renderer, testCase);

                Assert.That(
                    angleDegrees,
                    Is.LessThan(SharpFoldLimitDegrees),
                    $"프레임 {testCase.FrameIndex}의 Native 스키닝 결과에 " +
                    "팔꿈치 급접힘이 남으면 안 됩니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static float MeasureFoldAngle(
            SkinnedMeshRenderer renderer,
            NativeSkinningCase testCase)
        {
            var bakedMesh = new Mesh();
            try
            {
                renderer.BakeMesh(bakedMesh, false);
                Vector3[] vertices = testCase.VertexIndices
                    .Select(vertexIndex => bakedMesh.vertices[vertexIndex])
                    .ToArray();
                Vector3 firstNormal = CalculateFaceNormal(
                    vertices,
                    testCase.Triangles,
                    0);
                Vector3 secondNormal = CalculateFaceNormal(
                    vertices,
                    testCase.Triangles,
                    1);
                return Vector3.Angle(firstNormal, secondNormal);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bakedMesh);
            }
        }

        private static Vector3 CalculateFaceNormal(
            Vector3[] vertices,
            int[,] triangles,
            int faceIndex)
        {
            Vector3 first = vertices[triangles[faceIndex, 0]];
            Vector3 second = vertices[triangles[faceIndex, 1]];
            Vector3 third = vertices[triangles[faceIndex, 2]];
            Vector3 normal = Vector3.Cross(second - first, third - first);
            Assert.That(
                normal.sqrMagnitude,
                Is.GreaterThan(0.0000000000000001f),
                "팔꿈치 회귀 정점의 삼각형 면적이 퇴화하면 안 됩니다.");
            return normal.normalized;
        }

        private static void RequireLocalFixture()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath) == null)
            {
                Assert.Ignore($"로컬 FBX fixture를 찾을 수 없습니다: {ClipAssetPath}");
            }
        }

        private static void EnsureHumanoidClipImport()
        {
            Type configuratorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator",
                throwOnError: false);
            MethodInfo method = configuratorType?.GetMethod(
                "EnsureHumanoid",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        private static GameObject InstantiateTarget()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(
                asset,
                Is.Not.Null,
                $"대상 모델을 찾을 수 없습니다: {TargetAssetPath}");
            GameObject target = UnityEngine.Object.Instantiate(asset);
            target.name = "Native Skinning Quality Target";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null);
            return clip;
        }

        private static object CreateController()
        {
            Type controllerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController",
                throwOnError: false);
            Assert.That(controllerType, Is.Not.Null);
            return Activator.CreateInstance(controllerType, nonPublic: true);
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }

        public sealed class NativeSkinningCase
        {
            internal NativeSkinningCase(
                int frameIndex,
                int[] vertexIndices,
                int[,] triangles)
            {
                FrameIndex = frameIndex;
                VertexIndices = vertexIndices;
                Triangles = triangles;
            }

            internal int FrameIndex { get; }

            internal int[] VertexIndices { get; }

            internal int[,] Triangles { get; }

            public override string ToString()
            {
                return $"frame_{FrameIndex}";
            }
        }
    }
}
