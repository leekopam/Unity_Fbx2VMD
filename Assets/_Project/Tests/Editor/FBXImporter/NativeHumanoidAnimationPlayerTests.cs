using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeHumanoidAnimationPlayerTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const float TransformTolerance = 0.0001f;

        [OneTimeSetUp]
        public void EnsureHumanoidClipImport()
        {
            Type configuratorType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator",
                throwOnError: false);
            Assert.That(configuratorType, Is.Not.Null,
                "FBX 기준 입력을 Humanoid로 준비하는 Editor 설정기가 필요합니다.");

            MethodInfo method = configuratorType.GetMethod(
                "EnsureHumanoid",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "EnsureHumanoid 메서드가 필요합니다.");
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        [Test]
        public void Given_MissingAnimator_When_Initializing_Then_RejectsRequest()
        {
            object player = CreatePlayer();
            AnimationClip clip = LoadHumanoidClip();

            try
            {
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => Invoke(player, "Initialize", null, clip));

                Assert.That(exception.InnerException, Is.TypeOf<ArgumentNullException>());
            }
            finally
            {
                DisposePlayer(player);
            }
        }

        [Test]
        public void Given_NonHumanoidAnimator_When_Initializing_Then_RejectsRequest()
        {
            var target = new GameObject("Native Humanoid Invalid Animator Test");
            object player = CreatePlayer();

            try
            {
                Animator animator = target.AddComponent<Animator>();
                AnimationClip clip = LoadHumanoidClip();

                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => Invoke(player, "Initialize", animator, clip));

                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            }
            finally
            {
                DisposePlayer(player);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_YybAndSatisfactionClip_When_EvaluatingNativeHumanoid_Then_MovesWithoutScaleOrBoneLengthChanges()
        {
            GameObject target = InstantiateTarget();
            object player = CreatePlayer();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                Vector3 rootScale = target.transform.localScale;

                Assert.That(target.GetComponentInChildren<Animation>(true), Is.Null,
                    "기준선 모델은 Legacy Animation/Ghost 재생 경로를 포함하면 안 됩니다.");
                Assert.That(target.GetComponentInChildren<Fbx2Vmd.FBXImporter.PoseSpaceRetargeter>(true), Is.Null,
                    "기준선 모델은 PoseSpaceRetargeter 보정을 포함하면 안 됩니다.");
                Assert.That(target.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty,
                    "원본 FBX 기준선에는 IK 또는 보정 MonoBehaviour가 없어야 합니다.");

                Invoke(player, "Initialize", animator, clip);
                Assert.That(ReadProperty<bool>(player, "IsInitialized"), Is.True);
                Assert.That(ReadProperty<bool>(player, "IsFootIkEnabled"), Is.False);
                Assert.That(ReadProperty<bool>(player, "IsPlayableIkEnabled"), Is.False);

                Invoke(player, "EvaluateAt", 0f);
                Dictionary<HumanBodyBones, BoneSnapshot> firstPose = CaptureHumanoidBoneSnapshots(animator);

                float comparisonTime = Mathf.Clamp(clip.length * 0.35f, 0f, clip.length);
                Invoke(player, "EvaluateAt", comparisonTime);
                Dictionary<HumanBodyBones, BoneSnapshot> comparisonPose = CaptureHumanoidBoneSnapshots(animator);

                int movingBoneCount = CountMovingBones(firstPose, comparisonPose);
                Assert.That(movingBoneCount, Is.GreaterThanOrEqualTo(3),
                    "satisfaction_2 클립이 YYB Humanoid 본에 직접 적용되어 실제 자세 변화가 있어야 합니다.");
                Assert.That(Vector3.Distance(target.transform.localScale, rootScale),
                    Is.LessThanOrEqualTo(TransformTolerance),
                    "Native Humanoid 재생 중 대상 루트 스케일이 바뀌면 안 됩니다.");
                AssertBoneDimensionsUnchanged(firstPose, comparisonPose);
            }
            finally
            {
                DisposePlayer(player);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_InitializedPlayer_When_Disposing_Then_RestoresAnimatorSettings()
        {
            GameObject target = InstantiateTarget();
            object player = CreatePlayer();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                RuntimeAnimatorController originalController = animator.runtimeAnimatorController;

                Invoke(player, "Initialize", animator, clip);
                DisposePlayer(player);

                Assert.That(ReadProperty<bool>(player, "IsInitialized"), Is.False);
                Assert.That(animator.applyRootMotion, Is.True);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullUpdateTransforms));
                Assert.That(animator.runtimeAnimatorController, Is.SameAs(originalController));
            }
            finally
            {
                DisposePlayer(player);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static object CreatePlayer()
        {
            Type playerType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.NativeHumanoidAnimationPlayer",
                throwOnError: false);
            Assert.That(playerType, Is.Not.Null,
                "모델 중립 Unity Native Humanoid 재생기 타입이 필요합니다.");
            return Activator.CreateInstance(playerType, nonPublic: true);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }

        private static void DisposePlayer(object player)
        {
            if (player == null)
            {
                return;
            }

            Invoke(player, "Dispose");
        }

        private static GameObject InstantiateTarget()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(source, Is.Not.Null, $"YYB 기준 모델을 찾을 수 없습니다: {TargetAssetPath}");

            GameObject target = UnityEngine.Object.Instantiate(source);
            target.name = "Native Humanoid Baseline Target";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, "기준 모델에 Animator가 필요합니다.");
            Assert.That(animator.avatar, Is.Not.Null, "기준 모델에 Avatar가 필요합니다.");
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True,
                "기준 모델은 유효한 Humanoid Avatar를 사용해야 합니다.");
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath);
            var diagnostics = new List<string>();
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip inspectedClip)
                {
                    diagnostics.Add(
                        $"{inspectedClip.name}: humanMotion={inspectedClip.humanMotion}, " +
                        $"legacy={inspectedClip.legacy}, length={inspectedClip.length:F3}");
                }
                else if (asset != null)
                {
                    diagnostics.Add($"{asset.name}: type={asset.GetType().FullName}");
                }

                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__", StringComparison.Ordinal) &&
                    clip.humanMotion)
                {
                    return clip;
                }
            }

            Assert.Fail(
                $"Humanoid AnimationClip을 찾을 수 없습니다: {ClipAssetPath}. " +
                $"하위 자산: {string.Join("; ", diagnostics)}");
            return null;
        }

        private static Dictionary<HumanBodyBones, BoneSnapshot> CaptureHumanoidBoneSnapshots(Animator animator)
        {
            var snapshots = new Dictionary<HumanBodyBones, BoneSnapshot>();
            for (HumanBodyBones bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
            {
                Transform transform = animator.GetBoneTransform(bone);
                if (transform == null)
                {
                    continue;
                }

                snapshots[bone] = new BoneSnapshot(
                    transform.localPosition.magnitude,
                    transform.localRotation,
                    transform.localScale);
            }

            return snapshots;
        }

        private static int CountMovingBones(
            IReadOnlyDictionary<HumanBodyBones, BoneSnapshot> firstPose,
            IReadOnlyDictionary<HumanBodyBones, BoneSnapshot> comparisonPose)
        {
            int count = 0;
            foreach (KeyValuePair<HumanBodyBones, BoneSnapshot> entry in firstPose)
            {
                if (comparisonPose.TryGetValue(entry.Key, out BoneSnapshot comparison) &&
                    Quaternion.Angle(entry.Value.LocalRotation, comparison.LocalRotation) > 0.5f)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertBoneDimensionsUnchanged(
            IReadOnlyDictionary<HumanBodyBones, BoneSnapshot> firstPose,
            IReadOnlyDictionary<HumanBodyBones, BoneSnapshot> comparisonPose)
        {
            foreach (KeyValuePair<HumanBodyBones, BoneSnapshot> entry in firstPose)
            {
                Assert.That(comparisonPose.ContainsKey(entry.Key), Is.True,
                    $"재생 후 {entry.Key} Humanoid 본이 사라지면 안 됩니다.");
                BoneSnapshot comparison = comparisonPose[entry.Key];

                Assert.That(Vector3.Distance(entry.Value.LocalScale, comparison.LocalScale),
                    Is.LessThanOrEqualTo(TransformTolerance),
                    $"{entry.Key} 본의 localScale이 바뀌면 안 됩니다.");

                if (entry.Key != HumanBodyBones.Hips)
                {
                    Assert.That(Mathf.Abs(entry.Value.LocalPositionMagnitude - comparison.LocalPositionMagnitude),
                        Is.LessThanOrEqualTo(TransformTolerance),
                        $"{entry.Key} 본 길이가 바뀌면 안 됩니다.");
                }
            }
        }

        private readonly struct BoneSnapshot
        {
            internal BoneSnapshot(
                float localPositionMagnitude,
                Quaternion localRotation,
                Vector3 localScale)
            {
                LocalPositionMagnitude = localPositionMagnitude;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            internal float LocalPositionMagnitude { get; }
            internal Quaternion LocalRotation { get; }
            internal Vector3 LocalScale { get; }
        }
    }
}
