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
    public class HumanoidLowerBodyPlaybackQualityTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const float TransformTolerance = 0.0001f;
        private const float MaximumSegmentStepDegrees = 45f;
        private const float MaximumAdditionalContactDriftMeters = 0.01f;
        private const float ContactHeightMarginMeters = 0.03f;
        private const float ContactSpeedLimitMetersPerSecond = 0.15f;
        private const int MinimumContactFrameCount = 6;

        private static readonly HumanBodyBones[] LowerBodyBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes
        };

        private static readonly LowerBodySegment[] Segments =
        {
            new LowerBodySegment(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
            new LowerBodySegment(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            new LowerBodySegment(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
            new LowerBodySegment(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
            new LowerBodySegment(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            new LowerBodySegment(HumanBodyBones.RightFoot, HumanBodyBones.RightToes)
        };

        private delegate bool SeekDelegate(float timeSeconds);

        [Test]
        public void Given_SatisfactionClip_When_EvaluatingAllFrames_Then_LowerBodyPlaybackRemainsStable()
        {
            RequireLocalFixture();
            EnsureHumanoidClipImport();

            GameObject sourceTarget = InstantiateAsset(ClipAssetPath, "Lower Body Source");
            GameObject directTarget = InstantiateAsset(TargetAssetPath, "Lower Body Direct");
            GameObject correctedTarget = InstantiateAsset(TargetAssetPath, "Lower Body Corrected");
            object sourceController = CreateController();
            object directController = CreateController();
            object correctedController = CreateController();

            try
            {
                AnimationClip clip = LoadHumanoidClip();
                GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath);
                var sourceRig = new LowerBodyRig(RequireHumanoidAnimator(sourceTarget));
                var directRig = new LowerBodyRig(RequireHumanoidAnimator(directTarget));
                var correctedRig = new LowerBodyRig(RequireHumanoidAnimator(correctedTarget));

                Invoke(sourceController, "Prepare", sourceRig.Animator, clip);
                Invoke(directController, "Prepare", directRig.Animator, clip);
                Invoke(
                    correctedController,
                    "PrepareWithArmDirectionReference",
                    correctedRig.Animator,
                    clip,
                    sourceAsset);

                SeekDelegate sourceSeek = CreateSeekDelegate(sourceController);
                SeekDelegate directSeek = CreateSeekDelegate(directController);
                SeekDelegate correctedSeek = CreateSeekDelegate(correctedController);
                float frameRate = clip.frameRate > 0f ? clip.frameRate : 60f;
                int lastFrameIndex = Mathf.CeilToInt(clip.length * frameRate);
                var sourceLeftContacts = new List<Vector3>(lastFrameIndex + 1);
                var sourceRightContacts = new List<Vector3>(lastFrameIndex + 1);
                var targetLeftContacts = new List<Vector3>(lastFrameIndex + 1);
                var targetRightContacts = new List<Vector3>(lastFrameIndex + 1);
                Vector3[] previousDirections = null;
                float maximumRootPathError = 0f;
                float maximumBoneLengthDelta = 0f;
                float maximumScaleDelta = 0f;
                float maximumSegmentStep = 0f;
                int excessiveSegmentStepCount = 0;
                int nonFiniteValueCount = 0;
                float maximumPostProcessPosition = 0f;
                float maximumPostProcessScale = 0f;
                float maximumPostProcessRootPosition = 0f;
                float maximumPostProcessHipsPosition = 0f;

                for (int frameIndex = 0; frameIndex <= lastFrameIndex; frameIndex++)
                {
                    float timeSeconds = Mathf.Min(frameIndex / frameRate, clip.length);
                    Assert.That(sourceSeek(timeSeconds), Is.True);
                    Assert.That(directSeek(timeSeconds), Is.True);
                    Assert.That(correctedSeek(timeSeconds), Is.True);

                    maximumRootPathError = Mathf.Max(
                        maximumRootPathError,
                        Vector2.Distance(
                            sourceRig.CaptureAnchorSpaceRootOffsetXZ(),
                            correctedRig.CaptureAnchorSpaceRootOffsetXZ()));
                    correctedRig.AccumulateDimensionDeltas(
                        ref maximumBoneLengthDelta,
                        ref maximumScaleDelta,
                        ref nonFiniteValueCount);

                    Vector3[] currentDirections = correctedRig.CaptureSegmentDirections();
                    if (previousDirections != null)
                    {
                        for (int segmentIndex = 0;
                             segmentIndex < currentDirections.Length;
                             segmentIndex++)
                        {
                            float step = Vector3.Angle(
                                previousDirections[segmentIndex],
                                currentDirections[segmentIndex]);
                            maximumSegmentStep = Mathf.Max(maximumSegmentStep, step);
                            if (step > MaximumSegmentStepDegrees)
                            {
                                excessiveSegmentStepCount++;
                            }
                        }
                    }

                    previousDirections = currentDirections;
                    AccumulateGeometryDeltas(
                        directRig,
                        correctedRig,
                        ref maximumPostProcessPosition,
                        ref maximumPostProcessScale,
                        ref maximumPostProcessRootPosition,
                        ref maximumPostProcessHipsPosition);
                    sourceLeftContacts.Add(sourceRig.CaptureFootContactPoint(isLeft: true));
                    sourceRightContacts.Add(sourceRig.CaptureFootContactPoint(isLeft: false));
                    targetLeftContacts.Add(correctedRig.CaptureFootContactPoint(isLeft: true));
                    targetRightContacts.Add(correctedRig.CaptureFootContactPoint(isLeft: false));
                }

                FootContactMetrics leftContact = FootContactMetrics.Calculate(
                    sourceLeftContacts,
                    targetLeftContacts,
                    frameRate);
                FootContactMetrics rightContact = FootContactMetrics.Calculate(
                    sourceRightContacts,
                    targetRightContacts,
                    frameRate);
                UpperBodyCorrectionMetrics armCorrection =
                    MeasureArmDirectionCorrectionIsolation(
                        directController,
                        directRig,
                        directSeek,
                        sourceAsset,
                        clip,
                        frameRate,
                        lastFrameIndex);
                UpperBodyCorrectionMetrics frameCorrection =
                    MeasureUpperBodyFrameCorrectionIsolation(
                        correctedController,
                        correctedRig,
                        correctedSeek,
                        frameRate,
                        lastFrameIndex);

                Debug.Log(
                    "[HumanoidLowerBodyQuality] " +
                    $"frames={lastFrameIndex + 1}, " +
                    $"rootPathErrorMax={maximumRootPathError:F9}, " +
                    $"boneLengthDeltaMax={maximumBoneLengthDelta:F9}, " +
                    $"scaleDeltaMax={maximumScaleDelta:F9}, " +
                    $"segmentStepMax={maximumSegmentStep:F6}, " +
                    $"segmentStepOver45={excessiveSegmentStepCount}, " +
                    $"leftContactRuns={leftContact.RunCount}, " +
                    $"leftAdditionalDriftMax={leftContact.MaximumAdditionalDriftMeters:F9}, " +
                    $"rightContactRuns={rightContact.RunCount}, " +
                    $"rightAdditionalDriftMax={rightContact.MaximumAdditionalDriftMeters:F9}, " +
                    $"armIsolationFrames={armCorrection.SampleCount}, " +
                    $"armCorrectionLowerRotationMax={armCorrection.LowerRotation:F9}, " +
                    $"frameIsolationFrames={frameCorrection.SampleCount}, " +
                    $"frameCorrectionLowerRotationMax={frameCorrection.LowerRotation:F9}, " +
                    $"frameCorrectionLowerRotationFrame={frameCorrection.LowerRotationFrame}, " +
                    $"frameCorrectionLowerRotationBone={frameCorrection.LowerRotationBone}, " +
                    $"nonFinite={nonFiniteValueCount}");

                Assert.That(nonFiniteValueCount, Is.Zero);
                Assert.That(maximumRootPathError, Is.LessThanOrEqualTo(TransformTolerance));
                Assert.That(maximumBoneLengthDelta, Is.LessThanOrEqualTo(TransformTolerance));
                Assert.That(maximumScaleDelta, Is.LessThanOrEqualTo(TransformTolerance));
                Assert.That(excessiveSegmentStepCount, Is.Zero,
                    "하체 분절 방향이 한 프레임에 45도 넘게 변하면 안 됩니다.");
                Assert.That(leftContact.RunCount, Is.GreaterThan(0));
                Assert.That(rightContact.RunCount, Is.GreaterThan(0));
                Assert.That(
                    leftContact.MaximumAdditionalDriftMeters,
                    Is.LessThanOrEqualTo(MaximumAdditionalContactDriftMeters));
                Assert.That(
                    rightContact.MaximumAdditionalDriftMeters,
                    Is.LessThanOrEqualTo(MaximumAdditionalContactDriftMeters));
                AssertGeometryWithinTolerance(
                    maximumPostProcessPosition,
                    maximumPostProcessScale,
                    maximumPostProcessRootPosition,
                    maximumPostProcessHipsPosition,
                    "발 접촉 후처리");
                AssertIsolationWithinTolerance(
                    armCorrection.LowerRotation,
                    armCorrection.LowerPosition,
                    armCorrection.LowerScale,
                    armCorrection.RootPosition,
                    armCorrection.HipsPosition,
                    $"팔 방향 보정(frame={armCorrection.LowerRotationFrame}, " +
                    $"bone={armCorrection.LowerRotationBone})");
                AssertIsolationWithinTolerance(
                    frameCorrection.LowerRotation,
                    frameCorrection.LowerPosition,
                    frameCorrection.LowerScale,
                    frameCorrection.RootPosition,
                    frameCorrection.HipsPosition,
                    $"상체 프레임 보정(frame={frameCorrection.LowerRotationFrame}, " +
                    $"bone={frameCorrection.LowerRotationBone})");
                Assert.That(frameCorrection.ChangedMuscleCount, Is.GreaterThan(0));
            }
            finally
            {
                DisposeController(sourceController);
                DisposeController(directController);
                DisposeController(correctedController);
                UnityEngine.Object.DestroyImmediate(sourceTarget);
                UnityEngine.Object.DestroyImmediate(directTarget);
                UnityEngine.Object.DestroyImmediate(correctedTarget);
            }
        }

        private static UpperBodyCorrectionMetrics MeasureArmDirectionCorrectionIsolation(
            object controller,
            LowerBodyRig rig,
            SeekDelegate seek,
            GameObject sourceAsset,
            AnimationClip clip,
            float frameRate,
            int lastFrameIndex)
        {
            int[] sampleFrames = BuildIsolationSampleFrames(lastFrameIndex);
            var nativeSnapshots = new Dictionary<int, LowerBodySnapshot>();
            foreach (int frameIndex in sampleFrames)
            {
                Assert.That(seek(frameIndex / frameRate), Is.True);
                nativeSnapshots.Add(frameIndex, rig.CaptureSnapshot());
            }

            FieldInfo referencePlayerField = controller.GetType().GetField(
                "_poseReferencePlayer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(referencePlayerField, Is.Not.Null);
            object referencePlayer = referencePlayerField.GetValue(controller);
            Invoke(referencePlayer, "InitializeFromSourceModel", sourceAsset, clip);

            var result = new UpperBodyCorrectionMetrics();
            foreach (int frameIndex in sampleFrames)
            {
                Assert.That(seek(frameIndex / frameRate), Is.True);
                result.Accumulate(
                    frameIndex,
                    nativeSnapshots[frameIndex],
                    rig.CaptureSnapshot());
            }

            return result;
        }

        private static UpperBodyCorrectionMetrics MeasureUpperBodyFrameCorrectionIsolation(
            object controller,
            LowerBodyRig rig,
            SeekDelegate seek,
            float frameRate,
            int lastFrameIndex)
        {
            int[] sampleFrames = BuildIsolationSampleFrames(lastFrameIndex);
            int muscleIndex = FindUpperBodyMuscleIndex();
            string muscleName = HumanTrait.MuscleName[muscleIndex];
            object document = CreateDocument("satisfaction_2", frameRate);
            var snapshots = new Dictionary<int, LowerBodySnapshot>();
            var muscleValues = new Dictionary<int, float>();

            foreach (int frameIndex in sampleFrames)
            {
                Assert.That(seek(frameIndex / frameRate), Is.True);
                snapshots.Add(frameIndex, rig.CaptureSnapshot());
                Assert.That(TryCapturePose(controller, out HumanPose pose), Is.True);
                muscleValues.Add(frameIndex, pose.muscles[muscleIndex]);
                float delta = pose.muscles[muscleIndex] >= 0f ? -0.05f : 0.05f;
                Assert.That(
                    (bool)Invoke(
                        document,
                        "TrySetMuscleDelta",
                        frameIndex,
                        muscleName,
                        delta),
                    Is.True);
            }

            Assert.That((bool)Invoke(controller, "TryPreviewPoseCorrection", document), Is.True);
            var result = new UpperBodyCorrectionMetrics();
            foreach (int frameIndex in sampleFrames)
            {
                Assert.That(seek(frameIndex / frameRate), Is.True);
                LowerBodySnapshot after = rig.CaptureSnapshot();
                result.Accumulate(frameIndex, snapshots[frameIndex], after);
                if (TryCapturePose(controller, out HumanPose pose) &&
                    Mathf.Abs(
                        pose.muscles[muscleIndex] - muscleValues[frameIndex]) >
                    TransformTolerance)
                {
                    result.ChangedMuscleCount++;
                }
            }

            return result;
        }

        private static int[] BuildIsolationSampleFrames(int lastFrameIndex)
        {
            return Enumerable.Range(0, lastFrameIndex + 1).ToArray();
        }

        private static void AccumulateGeometryDeltas(
            LowerBodyRig direct,
            LowerBodyRig corrected,
            ref float maximumPosition,
            ref float maximumScale,
            ref float maximumRootPosition,
            ref float maximumHipsPosition)
        {
            for (int index = 0; index < direct.Bones.Length; index++)
            {
                maximumPosition = Mathf.Max(
                    maximumPosition,
                    Vector3.Distance(
                        direct.Bones[index].localPosition,
                        corrected.Bones[index].localPosition));
                maximumScale = Mathf.Max(
                    maximumScale,
                    Vector3.Distance(
                        direct.Bones[index].localScale,
                        corrected.Bones[index].localScale));
            }

            maximumRootPosition = Mathf.Max(
                maximumRootPosition,
                Vector2.Distance(
                    direct.CaptureAnchorSpaceRootOffsetXZ(),
                    corrected.CaptureAnchorSpaceRootOffsetXZ()));
            maximumHipsPosition = Mathf.Max(
                maximumHipsPosition,
                Vector3.Distance(
                    direct.CaptureNormalizedHipsPosition(),
                    corrected.CaptureNormalizedHipsPosition()));
        }

        private static void AssertGeometryWithinTolerance(
            float position,
            float scale,
            float rootPosition,
            float hipsPosition,
            string source)
        {
            Assert.That(position, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 하체 localPosition을 바꾸면 안 됩니다.");
            Assert.That(scale, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 하체 localScale을 바꾸면 안 됩니다.");
            Assert.That(rootPosition, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 FBX XZ 루트 궤적을 바꾸면 안 됩니다.");
            Assert.That(hipsPosition, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 골반 위치를 바꾸면 안 됩니다.");
        }

        private static void AssertIsolationWithinTolerance(
            float rotation,
            float position,
            float scale,
            float rootPosition,
            float hipsPosition,
            string source)
        {
            Assert.That(rotation, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 하체 localRotation을 바꾸면 안 됩니다.");
            Assert.That(position, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 하체 localPosition을 바꾸면 안 됩니다.");
            Assert.That(scale, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 하체 localScale을 바꾸면 안 됩니다.");
            Assert.That(rootPosition, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 FBX XZ 루트 궤적을 바꾸면 안 됩니다.");
            Assert.That(hipsPosition, Is.LessThanOrEqualTo(TransformTolerance),
                $"{source}이 골반 위치를 바꾸면 안 됩니다.");
        }

        private static int FindUpperBodyMuscleIndex()
        {
            for (int index = 0; index < HumanTrait.MuscleCount; index++)
            {
                string name = HumanTrait.MuscleName[index];
                if (name.IndexOf("Left Arm", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.IndexOf("Twist", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return index;
                }
            }

            Assert.Fail("상체 격리 검증에 사용할 왼팔 canonical muscle이 필요합니다.");
            return -1;
        }

        private static bool TryCapturePose(object controller, out HumanPose pose)
        {
            object[] arguments = { null };
            bool captured = (bool)Invoke(controller, "TryCaptureCurrentPose", arguments);
            pose = captured ? (HumanPose)arguments[0] : default;
            return captured;
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
            Assert.That(method, Is.Not.Null,
                "로컬 FBX fixture를 Humanoid로 준비하는 설정기가 필요합니다.");
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        private static GameObject InstantiateAsset(string path, string instanceName)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, $"모델 asset을 찾을 수 없습니다: {path}");
            GameObject instance = UnityEngine.Object.Instantiate(asset);
            instance.name = instanceName;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);
            return instance;
        }

        private static Animator RequireHumanoidAnimator(GameObject root)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, $"{root.name}에 Animator가 필요합니다.");
            Assert.That(animator.avatar, Is.Not.Null, $"{root.name}에 Avatar가 필요합니다.");
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True,
                $"{root.name}은 유효한 Humanoid Avatar를 사용해야 합니다.");
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null,
                $"Humanoid clip을 찾을 수 없습니다: {ClipAssetPath}");
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

        private static object CreateDocument(string motionName, float frameRate)
        {
            Type documentType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionDocument",
                throwOnError: false);
            Assert.That(documentType, Is.Not.Null);
            return Activator.CreateInstance(
                documentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { motionName, frameRate },
                culture: null);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static SeekDelegate CreateSeekDelegate(object controller)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Seek",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (SeekDelegate)method.CreateDelegate(typeof(SeekDelegate), controller);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct LowerBodySegment
        {
            internal LowerBodySegment(HumanBodyBones start, HumanBodyBones end)
            {
                Start = start;
                End = end;
            }

            internal HumanBodyBones Start { get; }
            internal HumanBodyBones End { get; }
        }

        private sealed class LowerBodyRig
        {
            private readonly Transform _root;
            private readonly Transform _hips;
            private readonly Vector3 _rootAnchorPosition;
            private readonly Quaternion _rootAnchorRotation;
            private readonly float[] _referenceBoneLengths;
            private readonly Vector3[] _referenceLocalScales;
            private readonly Transform[] _segmentStarts;
            private readonly Transform[] _segmentEnds;
            private readonly Transform _leftFoot;
            private readonly Transform _leftToes;
            private readonly Transform _rightFoot;
            private readonly Transform _rightToes;

            internal LowerBodyRig(Animator animator)
            {
                Animator = animator;
                _root = animator.transform;
                _hips = RequireBone(animator, HumanBodyBones.Hips);
                _rootAnchorPosition = _root.position;
                _rootAnchorRotation = _root.rotation;
                Bones = LowerBodyBones.Select(bone => RequireBone(animator, bone)).ToArray();
                _referenceBoneLengths = Bones
                    .Select(bone => bone.localPosition.magnitude)
                    .ToArray();
                _referenceLocalScales = Bones.Select(bone => bone.localScale).ToArray();
                _segmentStarts = Segments
                    .Select(segment => RequireBone(animator, segment.Start))
                    .ToArray();
                _segmentEnds = Segments
                    .Select(segment => RequireBone(animator, segment.End))
                    .ToArray();
                _leftFoot = RequireBone(animator, HumanBodyBones.LeftFoot);
                _leftToes = RequireBone(animator, HumanBodyBones.LeftToes);
                _rightFoot = RequireBone(animator, HumanBodyBones.RightFoot);
                _rightToes = RequireBone(animator, HumanBodyBones.RightToes);
            }

            internal Animator Animator { get; }
            internal Transform[] Bones { get; }

            internal Vector2 CaptureAnchorSpaceRootOffsetXZ()
            {
                Vector3 offset = Quaternion.Inverse(_rootAnchorRotation) *
                    (_root.position - _rootAnchorPosition);
                return new Vector2(offset.x, offset.z);
            }

            internal Vector3 CaptureNormalizedHipsPosition()
            {
                return _root.InverseTransformPoint(_hips.position) /
                    Mathf.Max(Animator.humanScale, 0.0001f);
            }

            internal Vector3[] CaptureSegmentDirections()
            {
                var directions = new Vector3[Segments.Length];
                for (int index = 0; index < Segments.Length; index++)
                {
                    directions[index] = _root.InverseTransformDirection(
                        _segmentEnds[index].position - _segmentStarts[index].position).normalized;
                }

                return directions;
            }

            internal Vector3 CaptureFootContactPoint(bool isLeft)
            {
                Transform foot = isLeft ? _leftFoot : _rightFoot;
                Transform toes = isLeft ? _leftToes : _rightToes;
                return (foot.position + toes.position) * 0.5f;
            }

            internal void AccumulateDimensionDeltas(
                ref float maximumBoneLengthDelta,
                ref float maximumScaleDelta,
                ref int nonFiniteValueCount)
            {
                for (int index = 0; index < Bones.Length; index++)
                {
                    Transform bone = Bones[index];
                    if (index > 0)
                    {
                        maximumBoneLengthDelta = Mathf.Max(
                            maximumBoneLengthDelta,
                            Mathf.Abs(
                                _referenceBoneLengths[index] -
                                bone.localPosition.magnitude));
                    }
                    maximumScaleDelta = Mathf.Max(
                        maximumScaleDelta,
                        Vector3.Distance(_referenceLocalScales[index], bone.localScale));
                    if (!IsFinite(bone.localPosition.x) ||
                        !IsFinite(bone.localPosition.y) ||
                        !IsFinite(bone.localPosition.z) ||
                        !IsFinite(bone.localRotation.x) ||
                        !IsFinite(bone.localRotation.y) ||
                        !IsFinite(bone.localRotation.z) ||
                        !IsFinite(bone.localRotation.w) ||
                        !IsFinite(bone.localScale.x) ||
                        !IsFinite(bone.localScale.y) ||
                        !IsFinite(bone.localScale.z))
                    {
                        nonFiniteValueCount++;
                    }
                }
            }

            internal LowerBodySnapshot CaptureSnapshot()
            {
                return new LowerBodySnapshot(
                    Bones.Select(bone => bone.localRotation).ToArray(),
                    Bones.Select(bone => bone.localPosition).ToArray(),
                    Bones.Select(bone => bone.localScale).ToArray(),
                    CaptureAnchorSpaceRootOffsetXZ(),
                    CaptureNormalizedHipsPosition());
            }

            private static Transform RequireBone(Animator animator, HumanBodyBones bone)
            {
                Transform transform = animator.GetBoneTransform(bone);
                Assert.That(transform, Is.Not.Null, $"{bone} Humanoid 본이 필요합니다.");
                return transform;
            }
        }

        private readonly struct LowerBodySnapshot
        {
            internal LowerBodySnapshot(
                Quaternion[] rotations,
                Vector3[] positions,
                Vector3[] scales,
                Vector2 rootPosition,
                Vector3 hipsPosition)
            {
                Rotations = rotations;
                Positions = positions;
                Scales = scales;
                RootPosition = rootPosition;
                HipsPosition = hipsPosition;
            }

            internal Quaternion[] Rotations { get; }
            internal Vector3[] Positions { get; }
            internal Vector3[] Scales { get; }
            internal Vector2 RootPosition { get; }
            internal Vector3 HipsPosition { get; }
        }

        private sealed class UpperBodyCorrectionMetrics
        {
            internal float LowerRotation { get; private set; }
            internal int LowerRotationFrame { get; private set; } = -1;
            internal HumanBodyBones LowerRotationBone { get; private set; } =
                HumanBodyBones.LastBone;
            internal float LowerPosition { get; private set; }
            internal float LowerScale { get; private set; }
            internal float RootPosition { get; private set; }
            internal float HipsPosition { get; private set; }
            internal int ChangedMuscleCount { get; set; }
            internal int SampleCount { get; private set; }

            internal void Accumulate(
                int frameIndex,
                LowerBodySnapshot before,
                LowerBodySnapshot after)
            {
                SampleCount++;
                for (int index = 0; index < before.Rotations.Length; index++)
                {
                    float lowerRotation = Quaternion.Angle(
                        before.Rotations[index],
                        after.Rotations[index]);
                    if (lowerRotation > LowerRotation)
                    {
                        LowerRotation = lowerRotation;
                        LowerRotationFrame = frameIndex;
                        LowerRotationBone = LowerBodyBones[index];
                    }
                    LowerPosition = Mathf.Max(
                        LowerPosition,
                        Vector3.Distance(before.Positions[index], after.Positions[index]));
                    LowerScale = Mathf.Max(
                        LowerScale,
                        Vector3.Distance(before.Scales[index], after.Scales[index]));
                }

                RootPosition = Mathf.Max(
                    RootPosition,
                    Vector2.Distance(before.RootPosition, after.RootPosition));
                HipsPosition = Mathf.Max(
                    HipsPosition,
                    Vector3.Distance(before.HipsPosition, after.HipsPosition));
            }
        }

        private sealed class FootContactMetrics
        {
            private FootContactMetrics(int runCount, float maximumAdditionalDriftMeters)
            {
                RunCount = runCount;
                MaximumAdditionalDriftMeters = maximumAdditionalDriftMeters;
            }

            internal int RunCount { get; }
            internal float MaximumAdditionalDriftMeters { get; }

            internal static FootContactMetrics Calculate(
                IReadOnlyList<Vector3> sourcePoints,
                IReadOnlyList<Vector3> targetPoints,
                float frameRate)
            {
                float[] sortedHeights = sourcePoints
                    .Select(point => point.y)
                    .OrderBy(value => value)
                    .ToArray();
                int percentileIndex = Mathf.Clamp(
                    Mathf.CeilToInt(sortedHeights.Length * 0.02f) - 1,
                    0,
                    sortedHeights.Length - 1);
                float contactHeight = sortedHeights[percentileIndex] +
                    ContactHeightMarginMeters;
                bool[] contactFrames = DetectStableContactFrames(
                    sourcePoints,
                    frameRate,
                    contactHeight);

                int runCount = 0;
                float maximumAdditionalDrift = 0f;
                int startIndex = -1;
                for (int index = 0; index <= contactFrames.Length; index++)
                {
                    bool isContact = index < contactFrames.Length && contactFrames[index];
                    if (isContact && startIndex < 0)
                    {
                        startIndex = index;
                        continue;
                    }

                    if (isContact || startIndex < 0)
                    {
                        continue;
                    }

                    int endIndex = index - 1;
                    if (endIndex - startIndex + 1 >= MinimumContactFrameCount)
                    {
                        float sourceDrift = HorizontalDistance(
                            sourcePoints[startIndex],
                            sourcePoints[endIndex]);
                        float targetDrift = HorizontalDistance(
                            targetPoints[startIndex],
                            targetPoints[endIndex]);
                        maximumAdditionalDrift = Mathf.Max(
                            maximumAdditionalDrift,
                            targetDrift - sourceDrift);
                        runCount++;
                    }

                    startIndex = -1;
                }

                return new FootContactMetrics(runCount, maximumAdditionalDrift);
            }

            private static bool[] DetectStableContactFrames(
                IReadOnlyList<Vector3> points,
                float frameRate,
                float contactHeight)
            {
                var candidates = new bool[points.Count];
                var stableFrames = new bool[points.Count];
                for (int index = 0; index < points.Count; index++)
                {
                    float horizontalSpeed = index == 0
                        ? 0f
                        : HorizontalDistance(points[index - 1], points[index]) *
                            frameRate;
                    candidates[index] = points[index].y <= contactHeight &&
                        horizontalSpeed <= ContactSpeedLimitMetersPerSecond;
                    stableFrames[index] = candidates[index] &&
                        CalculateCenteredSpeed(points, index, frameRate) <=
                        ContactSpeedLimitMetersPerSecond;
                }

                var result = new bool[points.Count];
                int candidateStart = -1;
                for (int index = 0; index <= candidates.Length; index++)
                {
                    bool isCandidate = index < candidates.Length && candidates[index];
                    if (isCandidate && candidateStart < 0)
                    {
                        candidateStart = index;
                        continue;
                    }

                    if (isCandidate || candidateStart < 0)
                    {
                        continue;
                    }

                    MarkStableSpan(
                        stableFrames,
                        result,
                        candidateStart,
                        index - 1);
                    candidateStart = -1;
                }

                return result;
            }

            private static void MarkStableSpan(
                IReadOnlyList<bool> stableFrames,
                bool[] contactFrames,
                int candidateStart,
                int candidateEnd)
            {
                int firstStable = -1;
                int lastStable = -1;
                for (int index = candidateStart; index <= candidateEnd; index++)
                {
                    if (!stableFrames[index])
                    {
                        continue;
                    }

                    if (firstStable < 0)
                    {
                        firstStable = index;
                    }

                    lastStable = index;
                }

                if (firstStable < 0)
                {
                    return;
                }

                int contactStart = Mathf.Max(candidateStart, firstStable - 1);
                int contactEnd = Mathf.Min(candidateEnd, lastStable + 1);
                for (int index = contactStart; index <= contactEnd; index++)
                {
                    contactFrames[index] = true;
                }
            }

            private static float CalculateCenteredSpeed(
                IReadOnlyList<Vector3> points,
                int index,
                float frameRate)
            {
                if (points.Count <= 1)
                {
                    return 0f;
                }

                if (index == 0)
                {
                    return Vector3.Distance(points[0], points[1]) * frameRate;
                }

                if (index == points.Count - 1)
                {
                    return Vector3.Distance(points[index - 1], points[index]) *
                        frameRate;
                }

                return Vector3.Distance(points[index - 1], points[index + 1]) *
                    frameRate * 0.5f;
            }

            private static float HorizontalDistance(Vector3 first, Vector3 second)
            {
                return Vector2.Distance(
                    new Vector2(first.x, first.z),
                    new Vector2(second.x, second.z));
            }
        }
    }
}
