using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string AnimatorControllerAssetPath =
            "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller";
        private const float TransformTolerance = 0.0001f;
        private static readonly HumanBodyBones[] ArmBones =
        {
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm
        };

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
        public void Given_CompleteHumanoidCurves_When_ConfiguringDefaultReference_Then_UsesSelectivePolicy()
        {
            var retargeterObject = new GameObject("Selective Humanoid Reference Policy Test");

            try
            {
                var retargeter = retargeterObject.AddComponent<Fbx2Vmd.FBXImporter.PoseSpaceRetargeter>();
                retargeter.ConfigureEditorHumanoidMuscleReference(LoadHumanoidClip());

                Assert.That(
                    ReadField<bool>(retargeter, "_useEditorHumanoidMuscleReference"),
                    Is.True,
                    "Humanoid 근육 곡선 기준은 활성화되어야 합니다.");
                Assert.That(
                    ReadField<bool>(retargeter, "_useCompleteEditorHumanoidMuscleReference"),
                    Is.False,
                    "기본 경로가 전체 Native 포즈 덮어쓰기로 자동 승격되면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(retargeterObject);
            }
        }

        [Test]
        public void Given_CompleteHumanoidCurves_When_RequestingCompleteReference_Then_UsesCompletePolicy()
        {
            var retargeterObject = new GameObject("Complete Humanoid Reference Policy Test");

            try
            {
                var retargeter = retargeterObject.AddComponent<Fbx2Vmd.FBXImporter.PoseSpaceRetargeter>();
                retargeter.ConfigureEditorHumanoidMuscleReference(
                    LoadHumanoidClip(),
                    shouldUseCompletePoseReference: true);

                Assert.That(
                    ReadField<bool>(retargeter, "_useCompleteEditorHumanoidMuscleReference"),
                    Is.True,
                    "명시적으로 요청한 진단 경로는 전체 Native 포즈 기준을 사용할 수 있어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(retargeterObject);
            }
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
                RuntimeAnimatorController originalController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        AnimatorControllerAssetPath);
                Assert.That(originalController, Is.Not.Null,
                    $"AnimatorController를 찾을 수 없습니다: {AnimatorControllerAssetPath}");
                animator.runtimeAnimatorController = originalController;

                Invoke(player, "Initialize", animator, clip);
                Assert.That(animator.runtimeAnimatorController, Is.Null,
                    "Native Playable과 AnimatorController가 같은 본을 동시에 쓰면 안 됩니다.");
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

        [Test]
        public void Given_TargetAnimatorAndHumanoidClip_When_SamplingEditorReference_Then_MatchesNativeBodyRotation()
        {
            GameObject target = InstantiateTarget();
            GameObject baselineTarget = InstantiateTarget();
            object referencePlayer = CreateEditorReferencePlayer();
            object baselinePlayer = CreatePlayer();

            try
            {
                Animator targetAnimator = RequireHumanoidAnimator(target);
                Animator baselineAnimator = RequireHumanoidAnimator(baselineTarget);
                AnimationClip clip = LoadHumanoidClip();
                const float sampleTime = 31.1675f;

                Invoke(referencePlayer, "Initialize", targetAnimator, clip);
                HumanPose referencePose = SampleReferencePose(referencePlayer, sampleTime);

                Invoke(baselinePlayer, "Initialize", baselineAnimator, clip);
                Invoke(baselinePlayer, "EvaluateAt", sampleTime);
                using (var baselineHandler = new HumanPoseHandler(
                    baselineAnimator.avatar,
                    baselineAnimator.transform))
                {
                    var baselinePose = new HumanPose();
                    baselineHandler.GetHumanPose(ref baselinePose);

                    Assert.That(
                        Quaternion.Angle(referencePose.bodyRotation, baselinePose.bodyRotation),
                        Is.LessThanOrEqualTo(TransformTolerance),
                        "Editor reference는 RootQ raw curve가 아니라 Native Humanoid가 해석한 bodyRotation을 제공해야 합니다.");
                    Assert.That(
                        CalculateAverageMuscleDelta(referencePose, baselinePose),
                        Is.LessThanOrEqualTo(TransformTolerance));
                }
            }
            finally
            {
                DisposePlayer(referencePlayer);
                DisposePlayer(baselinePlayer);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(baselineTarget);
            }
        }

        [Test]
        public void Given_RareHumanoidArmFrames_When_ApplyingEditorReferenceRotations_Then_MatchesNativeContinuityWithoutDimensionChanges()
        {
            GameObject target = InstantiateTarget();
            GameObject baselineTarget = InstantiateTarget();
            object referencePlayer = CreateEditorReferencePlayer();
            object baselinePlayer = CreatePlayer();

            try
            {
                Animator targetAnimator = RequireHumanoidAnimator(target);
                Animator baselineAnimator = RequireHumanoidAnimator(baselineTarget);
                AnimationClip clip = LoadHumanoidClip();
                int[][] criticalFramePairs =
                {
                    new[] { 1164, 1165 },
                    new[] { 1283, 1284 },
                    new[] { 2887, 2888 },
                    new[] { 2934, 2935 },
                    new[] { 3357, 3358 }
                };

                Invoke(referencePlayer, "Initialize", targetAnimator, clip);
                Invoke(baselinePlayer, "Initialize", baselineAnimator, clip);

                MethodInfo applyRotationsMethod = referencePlayer.GetType().GetMethod(
                    "TryApplyHumanoidBoneLocalRotationsTo",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyRotationsMethod, Is.Not.Null,
                    "Native reference가 HumanPose 재해석 뒤 Humanoid 본 회전을 직접 복구해야 합니다.");

                using (var targetHandler = new HumanPoseHandler(
                    targetAnimator.avatar,
                    targetAnimator.transform))
                {
                    foreach (int[] framePair in criticalFramePairs)
                    {
                        Dictionary<HumanBodyBones, Vector3> previousTargetDirections = null;
                        Dictionary<HumanBodyBones, Vector3> previousBaselineDirections = null;

                        foreach (int frame in framePair)
                        {
                            float sampleTime = frame / 30f;
                            HumanPose referencePose = SampleReferencePose(referencePlayer, sampleTime);
                            Invoke(baselinePlayer, "EvaluateAt", sampleTime);

                            targetHandler.SetHumanPose(ref referencePose);
                            Dictionary<HumanBodyBones, BoneSnapshot> beforeApply =
                                CaptureHumanoidBoneSnapshots(targetAnimator);

                            bool applied = (bool)applyRotationsMethod.Invoke(
                                referencePlayer,
                                new object[] { targetAnimator });

                            Assert.That(applied, Is.True,
                                $"frame {frame}에서 Native Humanoid 본 회전 복구가 적용되어야 합니다.");
                            Dictionary<HumanBodyBones, BoneSnapshot> afterApply =
                                CaptureHumanoidBoneSnapshots(targetAnimator);
                            Dictionary<HumanBodyBones, BoneSnapshot> baseline =
                                CaptureHumanoidBoneSnapshots(baselineAnimator);
                            AssertBoneDimensionsUnchanged(beforeApply, afterApply);

                            foreach (HumanBodyBones bone in ArmBones)
                            {
                                Assert.That(afterApply.ContainsKey(bone), Is.True);
                                Assert.That(baseline.ContainsKey(bone), Is.True);
                                Assert.That(
                                    Quaternion.Angle(
                                        afterApply[bone].LocalRotation,
                                        baseline[bone].LocalRotation),
                                    Is.LessThanOrEqualTo(0.01f),
                                    $"frame {frame}의 {bone} 회전은 Native 기준과 같아야 합니다.");
                            }

                            Dictionary<HumanBodyBones, Vector3> currentTargetDirections =
                                CaptureArmSegmentDirections(targetAnimator);
                            Dictionary<HumanBodyBones, Vector3> currentBaselineDirections =
                                CaptureArmSegmentDirections(baselineAnimator);
                            if (previousTargetDirections != null)
                            {
                                foreach (HumanBodyBones bone in ArmBones)
                                {
                                    float targetStep = Vector3.Angle(
                                        previousTargetDirections[bone],
                                        currentTargetDirections[bone]);
                                    float baselineStep = Vector3.Angle(
                                        previousBaselineDirections[bone],
                                        currentBaselineDirections[bone]);
                                    Assert.That(targetStep, Is.EqualTo(baselineStep).Within(0.02f),
                                        $"frame {framePair[0]}->{framePair[1]}의 {bone} 분절 방향 변화량은 Native 기준과 같아야 합니다.");
                                    Assert.That(targetStep, Is.LessThan(90f),
                                        $"frame {framePair[0]}->{framePair[1]}에서 {bone}이 한 프레임에 반전되면 안 됩니다.");
                                }
                            }

                            previousTargetDirections = currentTargetDirections;
                            previousBaselineDirections = currentBaselineDirections;
                        }
                    }
                }
            }
            finally
            {
                DisposePlayer(referencePlayer);
                DisposePlayer(baselinePlayer);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(baselineTarget);
            }
        }

        [Test]
        public void Given_CompleteEditorReferenceFrame_When_RunningArmDeformationGuard_Then_PreservesNativeArmRotations()
        {
            GameObject target = InstantiateTarget();
            var retargeterObject = new GameObject("Editor reference arm guard test");
            object referencePlayer = CreateEditorReferencePlayer();

            try
            {
                Animator targetAnimator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                Invoke(referencePlayer, "Initialize", targetAnimator, clip);

                HumanPose referencePose = SampleReferencePose(referencePlayer, 1164 / 30f);
                using (var targetHandler = new HumanPoseHandler(
                    targetAnimator.avatar,
                    targetAnimator.transform))
                {
                    targetHandler.SetHumanPose(ref referencePose);
                }

                Invoke(
                    referencePlayer,
                    "TryApplyHumanoidBoneLocalRotationsTo",
                    targetAnimator);

                var retargeter = retargeterObject.AddComponent<Fbx2Vmd.FBXImporter.PoseSpaceRetargeter>();
                retargeter.targetAnimator = targetAnimator;
                WriteField(retargeter, "_useCompleteEditorHumanoidMuscleReference", true);
                WriteField(retargeter, "_hasEditorHumanoidPoseReferenceForFrame", true);
                WriteField(retargeter, "_editorHumanoidPoseReferencePlayer", referencePlayer);
                Assert.That(retargeter.IsCompleteEditorHumanoidPoseReferenceActive, Is.True);

                var guard = target.AddComponent<Fbx2Vmd.FBXImporter.HumanoidArmDeformationGuard>();
                guard.Configure(new Fbx2Vmd.FBXImporter.ArmDeformationSettings(
                    clampMusclesToHumanRange: false,
                    enableAnatomicalArmGuard: true,
                    stretchMuscleLimit: 0f,
                    upperArmTwistMuscleLimit: 0.75f,
                    lowerArmTwistMuscleLimit: 0.65f,
                    lockHumanoidBonePositions: true,
                    logCorrections: false,
                    clampArmStretchMuscles: false,
                    lockLimbChildLocalPositions: true,
                    lockLimbChildLocalRotations: false));

                MethodInfo bindMethod = guard.GetType().GetMethod(
                    "BindRetargeter",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(bindMethod, Is.Not.Null,
                    "대상 arm guard가 현재 세션 retargeter를 명시적으로 연결받아야 합니다.");
                bindMethod.Invoke(guard, new object[] { retargeter });

                Dictionary<HumanBodyBones, BoneSnapshot> beforeGuard =
                    CaptureHumanoidBoneSnapshots(targetAnimator);
                Invoke(guard, "LateUpdate");
                Dictionary<HumanBodyBones, BoneSnapshot> afterGuard =
                    CaptureHumanoidBoneSnapshots(targetAnimator);

                foreach (HumanBodyBones bone in ArmBones)
                {
                    Assert.That(
                        Quaternion.Angle(
                            beforeGuard[bone].LocalRotation,
                            afterGuard[bone].LocalRotation),
                        Is.LessThanOrEqualTo(0.01f),
                        $"완전한 Editor Native 기준이 적용된 프레임에서 {bone} 회전을 guard가 다시 바꾸면 안 됩니다.");
                }

                AssertBoneDimensionsUnchanged(beforeGuard, afterGuard);
            }
            finally
            {
                DisposePlayer(referencePlayer);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(retargeterObject);
            }
        }

        [Test]
        public void Given_CompleteEditorReferenceFrame_When_RunningArmSwingLimitGuard_Then_PreservesNativeArmRotations()
        {
            GameObject target = InstantiateTarget();
            var retargeterObject = new GameObject("Editor reference arm swing guard test");
            object referencePlayer = CreateEditorReferencePlayer();

            try
            {
                Animator targetAnimator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                Invoke(referencePlayer, "Initialize", targetAnimator, clip);

                HumanPose referencePose = SampleReferencePose(referencePlayer, 2935 / 30f);
                using (var targetHandler = new HumanPoseHandler(
                    targetAnimator.avatar,
                    targetAnimator.transform))
                {
                    targetHandler.SetHumanPose(ref referencePose);
                }

                Invoke(
                    referencePlayer,
                    "TryApplyHumanoidBoneLocalRotationsTo",
                    targetAnimator);

                var retargeter = retargeterObject.AddComponent<Fbx2Vmd.FBXImporter.PoseSpaceRetargeter>();
                retargeter.targetAnimator = targetAnimator;
                WriteField(retargeter, "_useCompleteEditorHumanoidMuscleReference", true);
                WriteField(retargeter, "_hasEditorHumanoidPoseReferenceForFrame", true);
                WriteField(retargeter, "_editorHumanoidPoseReferencePlayer", referencePlayer);
                Assert.That(retargeter.IsCompleteEditorHumanoidPoseReferenceActive, Is.True);

                var guard = target.AddComponent<Fbx2Vmd.FBXImporter.HumanoidArmSwingLimitGuard>();
                guard.Configure(
                    targetAnimator,
                    enabled: true,
                    weight: 0.6f,
                    upperArmDownDot: 0.75f,
                    handHorizontalRatio: 0.05f,
                    handBelowShoulderRatio: 1.5f,
                    reachLimitWeight: 1f,
                    handHorizontalReachRatio: 0.06f,
                    reachMaxHandBelowShoulderRatio: 0f,
                    reachMinElbowAngleAfterApply: 0f,
                    raisedReachLimitWeight: 0f,
                    raisedMinUpperArmDownDot: 0.55f,
                    raisedMaxHandBelowShoulderRatio: 0.05f,
                    raisedMaxHandHorizontalReachRatio: 0f,
                    logCorrectionMessages: false);

                MethodInfo bindMethod = guard.GetType().GetMethod(
                    "BindRetargeter",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(bindMethod, Is.Not.Null,
                    "팔 스윙 guard가 현재 세션 retargeter를 명시적으로 연결받아야 합니다.");
                bindMethod.Invoke(guard, new object[] { retargeter });

                Dictionary<HumanBodyBones, BoneSnapshot> beforeBoundGuard =
                    CaptureHumanoidBoneSnapshots(targetAnimator);
                Invoke(guard, "LateUpdate");
                Dictionary<HumanBodyBones, BoneSnapshot> afterBoundGuard =
                    CaptureHumanoidBoneSnapshots(targetAnimator);

                Assert.That(guard.LastLeftApplied, Is.Zero);
                foreach (HumanBodyBones bone in ArmBones)
                {
                    Assert.That(
                        Quaternion.Angle(
                            beforeBoundGuard[bone].LocalRotation,
                            afterBoundGuard[bone].LocalRotation),
                        Is.LessThanOrEqualTo(0.01f),
                        $"완전한 Editor Native 기준이 적용된 프레임에서 {bone} 회전을 swing guard가 다시 바꾸면 안 됩니다.");
                }
            }
            finally
            {
                DisposePlayer(referencePlayer);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(retargeterObject);
            }
        }

        [Test]
        public void Given_VisibleTargetRenderers_When_InitializingEditorReference_Then_DisablesReferenceRenderers()
        {
            GameObject target = InstantiateTarget();
            object referencePlayer = CreateEditorReferencePlayer();

            try
            {
                Invoke(
                    referencePlayer,
                    "Initialize",
                    RequireHumanoidAnimator(target),
                    LoadHumanoidClip());

                GameObject referenceInstance = ReadField<GameObject>(
                    referencePlayer,
                    "_referenceInstance");
                Renderer[] renderers =
                    referenceInstance.GetComponentsInChildren<Renderer>(true);

                Assert.That(renderers, Is.Not.Empty,
                    "Editor pose reference가 실제 Renderer를 가진 모델로 검증되어야 합니다.");
                Assert.That(renderers.All(renderer => !renderer.enabled), Is.True,
                    "숨김 pose reference의 Renderer는 캡처에 섞이지 않도록 모두 비활성화되어야 합니다.");
            }
            finally
            {
                DisposePlayer(referencePlayer);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_NativeBaselineTemporaryTarget_When_CleaningUp_Then_DestroysObjectImmediately()
        {
            var target = new GameObject("Native Humanoid Baseline Target");

            try
            {
                Type runnerType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.NativeHumanoidPlaybackBaselineRunner",
                    throwOnError: false);
                Assert.That(runnerType, Is.Not.Null,
                    "Native Humanoid 기준선 runner가 필요합니다.");

                MethodInfo cleanupMethod = runnerType.GetMethod(
                    "DestroyTemporaryTarget",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(cleanupMethod, Is.Not.Null,
                    "기준선 임시 대상을 명시적으로 정리하는 메서드가 필요합니다.");

                cleanupMethod.Invoke(null, new object[] { target });

                Assert.That(target == null, Is.True,
                    "기준선 임시 대상은 실행 종료 시 즉시 파괴되어야 합니다.");
            }
            finally
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
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

        private static object CreateEditorReferencePlayer()
        {
            Type playerType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidPoseReferencePlayer",
                throwOnError: false);
            Assert.That(playerType, Is.Not.Null,
                "기존 Native Humanoid 재생기를 재사용하는 Editor pose reference player가 필요합니다.");
            return Activator.CreateInstance(playerType, nonPublic: true);
        }

        private static HumanPose SampleReferencePose(object player, float timeSeconds)
        {
            MethodInfo method = player.GetType().GetMethod(
                "TryEvaluateAt",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "TryEvaluateAt 메서드가 필요합니다.");

            object[] arguments = { timeSeconds, new HumanPose() };
            bool sampled = (bool)method.Invoke(player, arguments);
            Assert.That(sampled, Is.True);
            return (HumanPose)arguments[1];
        }

        private static float CalculateAverageMuscleDelta(HumanPose left, HumanPose right)
        {
            Assert.That(left.muscles, Is.Not.Null);
            Assert.That(right.muscles, Is.Not.Null);
            Assert.That(left.muscles.Length, Is.EqualTo(right.muscles.Length));

            float sum = 0f;
            for (int i = 0; i < left.muscles.Length; i++)
            {
                sum += Mathf.Abs(left.muscles[i] - right.muscles[i]);
            }

            return left.muscles.Length > 0 ? sum / left.muscles.Length : 0f;
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

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            return (T)field.GetValue(target);
        }

        private static void WriteField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
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

        private static Dictionary<HumanBodyBones, Vector3> CaptureArmSegmentDirections(
            Animator animator)
        {
            var directions = new Dictionary<HumanBodyBones, Vector3>
            {
                [HumanBodyBones.LeftUpperArm] = ResolveSegmentDirection(
                    animator,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm),
                [HumanBodyBones.LeftLowerArm] = ResolveSegmentDirection(
                    animator,
                    HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand),
                [HumanBodyBones.RightUpperArm] = ResolveSegmentDirection(
                    animator,
                    HumanBodyBones.RightUpperArm,
                    HumanBodyBones.RightLowerArm),
                [HumanBodyBones.RightLowerArm] = ResolveSegmentDirection(
                    animator,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand)
            };

            return directions;
        }

        private static Vector3 ResolveSegmentDirection(
            Animator animator,
            HumanBodyBones startBone,
            HumanBodyBones endBone)
        {
            Transform start = animator.GetBoneTransform(startBone);
            Transform end = animator.GetBoneTransform(endBone);
            Assert.That(start, Is.Not.Null);
            Assert.That(end, Is.Not.Null);

            Vector3 direction = end.position - start.position;
            Assert.That(direction.sqrMagnitude, Is.GreaterThan(0.000001f));
            return direction.normalized;
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
