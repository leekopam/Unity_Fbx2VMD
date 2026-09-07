using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidForearmDirectionDeadbandRegressionTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string TargetRendererName = "U_Char_2";
        private const float SharpFoldLimitDegrees = 150f;

        private static readonly int[] SampleFrameIndices = { 1326, 1327, 1328 };
        private static readonly int[] HingeVertexIndices = { 17307, 17309, 17310, 17314 };
        private static readonly int[,] HingeTriangles =
        {
            { 2, 1, 0 },
            { 2, 3, 1 }
        };

        [TestCase(0f, false)]
        [TestCase(2f, false)]
        [TestCase(2.001f, true)]
        [TestCase(float.NaN, false)]
        public void Given_ForearmDirectionError_When_ResolvingCorrection_Then_UsesBoundedDeadband(
            float errorDegrees,
            bool expected)
        {
            Assert.That(InvokeShouldApplyForearmCorrection(errorDegrees), Is.EqualTo(expected));
        }

        [Test]
        public void Given_SensitiveElbowFrames_When_ApplyingDirectionCorrection_Then_DoesNotCreateSharpFold()
        {
            RequireLocalFixture();
            EnsureHumanoidClipImport();
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath);
                Invoke(controller, "PrepareWithArmDirectionReference", animator, clip, sourceModel);
                SkinnedMeshRenderer renderer = animator
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(candidate => candidate.name == TargetRendererName);
                var measuredAngles = new List<float>(SampleFrameIndices.Length);

                foreach (int frameIndex in SampleFrameIndices)
                {
                    float timeSeconds = frameIndex / clip.frameRate;
                    Assert.That((bool)Invoke(controller, "Seek", timeSeconds), Is.True);
                    float angleDegrees = MeasureFoldAngle(renderer);
                    measuredAngles.Add(angleDegrees);
                    Assert.That(angleDegrees, Is.LessThan(SharpFoldLimitDegrees),
                        $"프레임 {frameIndex}의 하완 방향 보정이 팔꿈치 급접힘을 만들면 안 됩니다.");
                }

                Debug.Log(
                    "[HumanoidForearmDeadband] " +
                    string.Join(",", SampleFrameIndices.Zip(
                        measuredAngles,
                        (frame, angle) => $"{frame}:{angle:F6}")));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static bool InvokeShouldApplyForearmCorrection(float errorDegrees)
        {
            Type policyType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidArmDirectionCorrectionPolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null,
                "작은 하완 방향 오차를 보존하는 모델 중립 정책이 필요합니다.");
            MethodInfo method = policyType.GetMethod(
                "ShouldApplyForearmCorrection",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] { errorDegrees });
        }

        private static float MeasureFoldAngle(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, false);
                Vector3[] vertices = HingeVertexIndices
                    .Select(index => mesh.vertices[index])
                    .ToArray();
                Vector3 firstNormal = CalculateFaceNormal(vertices, 0);
                Vector3 secondNormal = CalculateFaceNormal(vertices, 1);
                return Vector3.Angle(firstNormal, secondNormal);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static Vector3 CalculateFaceNormal(IReadOnlyList<Vector3> vertices, int faceIndex)
        {
            Vector3 first = vertices[HingeTriangles[faceIndex, 0]];
            Vector3 second = vertices[HingeTriangles[faceIndex, 1]];
            Vector3 third = vertices[HingeTriangles[faceIndex, 2]];
            Vector3 normal = Vector3.Cross(second - first, third - first);
            Assert.That(normal.sqrMagnitude, Is.GreaterThan(0.0000000000000001f),
                "팔꿈치 회귀 정점의 삼각형 면적이 퇴화하면 안 됩니다.");
            return normal / Mathf.Sqrt(normal.sqrMagnitude);
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
            Assert.That(asset, Is.Not.Null, $"대상 모델을 찾을 수 없습니다: {TargetAssetPath}");
            GameObject target = UnityEngine.Object.Instantiate(asset);
            target.name = "Forearm Direction Deadband Regression Target";
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
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) && candidate.humanMotion);
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

        private static object Invoke(object target, string methodName, params object[] arguments)
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
    }
}
