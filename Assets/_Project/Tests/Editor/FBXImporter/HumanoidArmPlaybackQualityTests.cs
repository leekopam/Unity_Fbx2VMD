using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmPlaybackQualityTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string ReferenceObjectPrefix = "EditorHumanoidPoseReference_";
        private const float GeometryTolerance = 0.0001f;
        private const float DirectionErrorLimitDegrees = 0.1f;
        private const float InducedStepLimitDegrees = 2f;
        private const float ProximityCandidateLimit = 0.18f;
        private const float ProximityRegressionMargin = 0.02f;
        private const int PerformanceSampleCount = 600;

        private static readonly ArmSegment[] ArmSegments =
        {
            new ArmSegment(
                "left_upper_arm",
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm),
            new ArmSegment(
                "left_forearm",
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand),
            new ArmSegment(
                "right_upper_arm",
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm),
            new ArmSegment(
                "right_forearm",
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand)
        };

        private static readonly HumanBodyBones[] ScopeInvariantBones =
        {
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand
        };

        private delegate bool SeekDelegate(float timeSeconds);

        private delegate bool DirectionErrorDelegate(
            Vector3 expectedDirection,
            Vector3 actualDirection,
            out float errorDegrees);

        private delegate bool TwistAngleDelegate(
            Quaternion rotationDelta,
            Vector3 twistAxis,
            out float angleDegrees);

        private delegate bool SegmentDistanceDelegate(
            Vector3 firstStart,
            Vector3 firstEnd,
            Vector3 secondStart,
            Vector3 secondEnd,
            out float distance);

        [Test]
        public void Given_SatisfactionClip_When_EvaluatingAllFrames_Then_ArmCorrectionRemainsStable()
        {
            RequireLocalFixture();
            EnsureHumanoidClipImport();

            int referenceCountBefore = FindReferenceObjects().Length;
            GameObject sourceTarget = InstantiateAsset(ClipAssetPath, "Arm Quality Source");
            GameObject directTarget = InstantiateAsset(TargetAssetPath, "Arm Quality Direct");
            GameObject correctedTarget = InstantiateAsset(TargetAssetPath, "Arm Quality Corrected");
            object sourceController = CreateController();
            object directController = CreateController();
            object correctedController = CreateController();
            bool correctedControllerDisposed = false;

            try
            {
                AnimationClip clip = LoadHumanoidClip();
                GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath);
                Animator sourceAnimator = RequireHumanoidAnimator(sourceTarget);
                Animator directAnimator = RequireHumanoidAnimator(directTarget);
                Animator correctedAnimator = RequireHumanoidAnimator(correctedTarget);

                Invoke(sourceController, "Prepare", sourceAnimator, clip);
                Invoke(directController, "Prepare", directAnimator, clip);
                Invoke(
                    correctedController,
                    "PrepareWithArmDirectionReference",
                    correctedAnimator,
                    clip,
                    sourceAsset);

                GameObject[] references = FindReferenceObjects();
                Assert.That(references.Length, Is.EqualTo(referenceCountBefore + 1),
                    "팔 방향 기준 모델은 controller당 하나만 생성되어야 합니다.");
                Assert.That(
                    references.SelectMany(item =>
                            item.GetComponentsInChildren<Renderer>(true))
                        .All(renderer => !renderer.enabled),
                    Is.True,
                    "숨겨진 팔 방향 기준 모델의 Renderer는 모두 꺼져야 합니다.");

                SeekDelegate sourceSeek = CreateSeekDelegate(sourceController);
                SeekDelegate directSeek = CreateSeekDelegate(directController);
                SeekDelegate correctedSeek = CreateSeekDelegate(correctedController);
                DirectionErrorDelegate calculateDirectionError =
                    CreateMetricDelegate<DirectionErrorDelegate>(
                        "TryCalculateDirectionErrorDegrees");
                TwistAngleDelegate calculateTwist =
                    CreateMetricDelegate<TwistAngleDelegate>(
                        "TryCalculateTwistAngleDegrees");
                SegmentDistanceDelegate calculateDistance =
                    CreateMetricDelegate<SegmentDistanceDelegate>(
                        "TryCalculateSegmentDistance");

                var sourceRig = new ArmRig(sourceAnimator);
                var directRig = new ArmRig(directAnimator);
                var correctedRig = new ArmRig(correctedAnimator);
                float frameRate = clip.frameRate > 0f ? clip.frameRate : 30f;
                int lastFrameIndex = Mathf.CeilToInt(clip.length * frameRate);
                var directionErrors = new List<float>((lastFrameIndex + 1) * 4);
                var proximityCandidates = new List<ProximityFrame>();
                Vector3[] previousSourceDirections = null;
                Vector3[] previousCorrectedDirections = null;
                int nonFiniteMetricCount = 0;
                int directionStepRegressionCount = 0;
                int correctionTwistOverLimitCount = 0;
                float maxDirectionStepRegression = 0f;
                float maxCorrectionTwist = 0f;
                float maxElbowBendError = 0f;
                float maxScopeRotationDelta = 0f;
                float maxLocalPositionDelta = 0f;
                float maxLocalScaleDelta = 0f;
                float minimumNormalizedProximity = float.PositiveInfinity;

                for (int frameIndex = 0; frameIndex <= lastFrameIndex; frameIndex++)
                {
                    float timeSeconds = Mathf.Min(frameIndex / frameRate, clip.length);
                    Assert.That(sourceSeek(timeSeconds), Is.True);
                    Assert.That(directSeek(timeSeconds), Is.True);
                    Assert.That(correctedSeek(timeSeconds), Is.True);

                    Vector3[] sourceDirections = sourceRig.CaptureRootSpaceDirections();
                    Vector3[] directDirections = directRig.CaptureRootSpaceDirections();
                    Vector3[] correctedDirections = correctedRig.CaptureRootSpaceDirections();
                    Quaternion[] directRotations = directRig.CaptureSegmentWorldRotations();
                    Quaternion[] correctedRotations =
                        correctedRig.CaptureSegmentWorldRotations();
                    var inheritedCorrections = new Quaternion[ArmSegments.Length];

                    for (int segmentIndex = 0;
                         segmentIndex < ArmSegments.Length;
                         segmentIndex++)
                    {
                        if (!calculateDirectionError(
                                sourceDirections[segmentIndex],
                                correctedDirections[segmentIndex],
                                out float directionError))
                        {
                            nonFiniteMetricCount++;
                            continue;
                        }

                        directionErrors.Add(directionError);
                        Quaternion parentCorrection = segmentIndex == 1
                            ? inheritedCorrections[0]
                            : segmentIndex == 3
                                ? inheritedCorrections[2]
                                : Quaternion.identity;
                        Quaternion rotationBeforeSegmentCorrection =
                            parentCorrection * directRotations[segmentIndex];
                        Quaternion correctionDelta =
                            correctedRotations[segmentIndex] *
                            Quaternion.Inverse(rotationBeforeSegmentCorrection);
                        inheritedCorrections[segmentIndex] = correctionDelta;
                        Vector3 directAxis = directRig.TransformDirectionToWorld(
                            directDirections[segmentIndex]);
                        Vector3 axisBeforeSegmentCorrection =
                            parentCorrection * directAxis;
                        if (!calculateTwist(
                                correctionDelta,
                                axisBeforeSegmentCorrection,
                                out float correctionTwist))
                        {
                            nonFiniteMetricCount++;
                            continue;
                        }

                        maxCorrectionTwist = Mathf.Max(
                            maxCorrectionTwist,
                            correctionTwist);
                        if (correctionTwist > InducedStepLimitDegrees)
                        {
                            correctionTwistOverLimitCount++;
                        }

                        if (previousSourceDirections == null)
                        {
                            continue;
                        }

                        float sourceStep = Vector3.Angle(
                            previousSourceDirections[segmentIndex],
                            sourceDirections[segmentIndex]);
                        float correctedStep = Vector3.Angle(
                            previousCorrectedDirections[segmentIndex],
                            correctedDirections[segmentIndex]);
                        float directionStepRegression = correctedStep - sourceStep;
                        maxDirectionStepRegression = Mathf.Max(
                            maxDirectionStepRegression,
                            directionStepRegression);
                        if (directionStepRegression > InducedStepLimitDegrees)
                        {
                            directionStepRegressionCount++;
                        }
                    }

                    maxElbowBendError = Mathf.Max(
                        maxElbowBendError,
                        CalculateElbowBendError(sourceDirections, correctedDirections, 0));
                    maxElbowBendError = Mathf.Max(
                        maxElbowBendError,
                        CalculateElbowBendError(sourceDirections, correctedDirections, 2));
                    maxScopeRotationDelta = Mathf.Max(
                        maxScopeRotationDelta,
                        CalculateScopeRotationDelta(directRig, correctedRig));
                    CalculateGeometryDeltas(
                        directRig,
                        correctedRig,
                        ref maxLocalPositionDelta,
                        ref maxLocalScaleDelta);
                    CollectProximityCandidates(
                        calculateDistance,
                        frameIndex,
                        timeSeconds,
                        directRig,
                        correctedRig,
                        proximityCandidates,
                        ref minimumNormalizedProximity);

                    previousSourceDirections = sourceDirections;
                    previousCorrectedDirections = correctedDirections;
                }

                PerformanceResult performance = MeasurePerformance(
                    correctedSeek,
                    clip.length);
                ProximityFrame[] highestRiskFrames = proximityCandidates
                    .OrderBy(item => item.CorrectedNormalizedDistance)
                    .ThenBy(item => item.FrameIndex)
                    .Take(12)
                    .ToArray();

                Debug.Log(
                    "[HumanoidArmQuality] " +
                    $"frames={lastFrameIndex + 1}, " +
                    $"directionMean={directionErrors.Average():F9}, " +
                    $"directionP95={CalculatePercentile(directionErrors, 0.95f):F9}, " +
                    $"directionMax={directionErrors.Max():F9}, " +
                    $"directionStepOver2={directionStepRegressionCount}, " +
                    $"directionStepRegressionMax={maxDirectionStepRegression:F9}, " +
                    $"correctionTwistOver2={correctionTwistOverLimitCount}, " +
                    $"correctionTwistMax={maxCorrectionTwist:F9}, " +
                    $"elbowBendErrorMax={maxElbowBendError:F9}, " +
                    $"scopeRotationDeltaMax={maxScopeRotationDelta:F9}, " +
                    $"localPositionDeltaMax={maxLocalPositionDelta:F9}, " +
                    $"localScaleDeltaMax={maxLocalScaleDelta:F9}, " +
                    $"proximityMinimum={minimumNormalizedProximity:F9}, " +
                    $"proximityCandidateCount={proximityCandidates.Count}, " +
                    $"allocatedBytesPerSample={performance.AllocatedBytesPerSample:F3}, " +
                    $"millisecondsPerSample={performance.MillisecondsPerSample:F6}, " +
                    $"nonFinite={nonFiniteMetricCount}");
                Debug.Log(
                    "[HumanoidArmQualityRiskFrames] " +
                    string.Join(";", highestRiskFrames.Select(item => item.ToString())));

                Assert.That(nonFiniteMetricCount, Is.Zero);
                Assert.That(directionErrors.Max(), Is.LessThan(DirectionErrorLimitDegrees));
                Assert.That(directionStepRegressionCount, Is.Zero,
                    "팔 방향 보정이 원본보다 2도 넘는 frame-step을 만들면 안 됩니다.");
                Assert.That(correctionTwistOverLimitCount, Is.Zero,
                    "팔 방향 보정 회전은 팔 축 Twist를 2도 넘게 만들면 안 됩니다.");
                Assert.That(maxElbowBendError, Is.LessThan(DirectionErrorLimitDegrees));
                Assert.That(maxScopeRotationDelta, Is.LessThanOrEqualTo(GeometryTolerance),
                    "팔 보정은 쇄골과 손목 localRotation을 직접 바꾸면 안 됩니다.");
                Assert.That(maxLocalPositionDelta, Is.LessThanOrEqualTo(GeometryTolerance));
                Assert.That(maxLocalScaleDelta, Is.LessThanOrEqualTo(GeometryTolerance));

                DisposeController(correctedController);
                correctedControllerDisposed = true;
                Assert.That(FindReferenceObjects().Length, Is.EqualTo(referenceCountBefore),
                    "controller 종료 뒤 숨겨진 팔 방향 기준 모델이 남으면 안 됩니다.");
            }
            finally
            {
                DisposeController(sourceController);
                DisposeController(directController);
                if (!correctedControllerDisposed)
                {
                    DisposeController(correctedController);
                }

                UnityEngine.Object.DestroyImmediate(sourceTarget);
                UnityEngine.Object.DestroyImmediate(directTarget);
                UnityEngine.Object.DestroyImmediate(correctedTarget);
            }
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

        private static T CreateMetricDelegate<T>(string methodName)
            where T : Delegate
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidArmQualityMetricCalculator",
                throwOnError: false);
            MethodInfo method = calculatorType?.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return (T)method.CreateDelegate(typeof(T));
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }

        private static GameObject[] FindReferenceObjects()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.name.StartsWith(
                    ReferenceObjectPrefix,
                    StringComparison.Ordinal))
                .ToArray();
        }

        private static float CalculateElbowBendError(
            Vector3[] sourceDirections,
            Vector3[] correctedDirections,
            int upperArmIndex)
        {
            float sourceBend = Vector3.Angle(
                sourceDirections[upperArmIndex],
                sourceDirections[upperArmIndex + 1]);
            float correctedBend = Vector3.Angle(
                correctedDirections[upperArmIndex],
                correctedDirections[upperArmIndex + 1]);
            return Mathf.Abs(sourceBend - correctedBend);
        }

        private static float CalculateScopeRotationDelta(
            ArmRig directRig,
            ArmRig correctedRig)
        {
            float maximum = 0f;
            for (int index = 0; index < directRig.ScopeInvariantBones.Length; index++)
            {
                Transform direct = directRig.ScopeInvariantBones[index];
                Transform corrected = correctedRig.ScopeInvariantBones[index];
                if (direct == null && corrected == null)
                {
                    continue;
                }

                Assert.That(direct, Is.Not.Null);
                Assert.That(corrected, Is.Not.Null);
                maximum = Mathf.Max(
                    maximum,
                    Quaternion.Angle(direct.localRotation, corrected.localRotation));
            }

            return maximum;
        }

        private static void CalculateGeometryDeltas(
            ArmRig directRig,
            ArmRig correctedRig,
            ref float maxLocalPositionDelta,
            ref float maxLocalScaleDelta)
        {
            Assert.That(correctedRig.HumanoidBones.Length,
                Is.EqualTo(directRig.HumanoidBones.Length));
            for (int index = 0; index < directRig.HumanoidBones.Length; index++)
            {
                Transform direct = directRig.HumanoidBones[index];
                Transform corrected = correctedRig.HumanoidBones[index];
                maxLocalPositionDelta = Mathf.Max(
                    maxLocalPositionDelta,
                    Vector3.Distance(direct.localPosition, corrected.localPosition));
                maxLocalScaleDelta = Mathf.Max(
                    maxLocalScaleDelta,
                    Vector3.Distance(direct.localScale, corrected.localScale));
            }
        }

        private static void CollectProximityCandidates(
            SegmentDistanceDelegate calculateDistance,
            int frameIndex,
            float timeSeconds,
            ArmRig directRig,
            ArmRig correctedRig,
            ICollection<ProximityFrame> candidates,
            ref float minimumNormalizedProximity)
        {
            float directShoulderWidth = directRig.ShoulderWidth;
            float correctedShoulderWidth = correctedRig.ShoulderWidth;
            if (directShoulderWidth <= GeometryTolerance ||
                correctedShoulderWidth <= GeometryTolerance)
            {
                return;
            }

            for (int segmentIndex = 0; segmentIndex < ArmSegments.Length; segmentIndex++)
            {
                ArmSegment segment = ArmSegments[segmentIndex];
                Vector3 directStart = directRig.SegmentStarts[segmentIndex].position;
                Vector3 correctedStart = correctedRig.SegmentStarts[segmentIndex].position;
                if (segmentIndex == 0 || segmentIndex == 2)
                {
                    directStart = Vector3.Lerp(
                        directStart,
                        directRig.SegmentEnds[segmentIndex].position,
                        0.25f);
                    correctedStart = Vector3.Lerp(
                        correctedStart,
                        correctedRig.SegmentEnds[segmentIndex].position,
                        0.25f);
                }

                if (!calculateDistance(
                        directStart,
                        directRig.SegmentEnds[segmentIndex].position,
                        directRig.TorsoStart.position,
                        directRig.TorsoEnd.position,
                        out float directDistance) ||
                    !calculateDistance(
                        correctedStart,
                        correctedRig.SegmentEnds[segmentIndex].position,
                        correctedRig.TorsoStart.position,
                        correctedRig.TorsoEnd.position,
                        out float correctedDistance))
                {
                    continue;
                }

                float directNormalized = directDistance / directShoulderWidth;
                float correctedNormalized = correctedDistance / correctedShoulderWidth;
                minimumNormalizedProximity = Mathf.Min(
                    minimumNormalizedProximity,
                    correctedNormalized);
                if (correctedNormalized < ProximityCandidateLimit &&
                    correctedNormalized + ProximityRegressionMargin < directNormalized)
                {
                    candidates.Add(new ProximityFrame(
                        frameIndex,
                        timeSeconds,
                        segment.Name,
                        directNormalized,
                        correctedNormalized));
                }
            }
        }

        private static PerformanceResult MeasurePerformance(
            SeekDelegate correctedSeek,
            float clipLength)
        {
            for (int index = 0; index < 20; index++)
            {
                correctedSeek(clipLength * index / 19f);
            }

            GC.Collect();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            for (int index = 0; index < PerformanceSampleCount; index++)
            {
                correctedSeek(clipLength * index / (PerformanceSampleCount - 1f));
            }

            stopwatch.Stop();
            long allocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            return new PerformanceResult(
                allocatedBytes / (float)PerformanceSampleCount,
                stopwatch.Elapsed.TotalMilliseconds / PerformanceSampleCount);
        }

        private static float CalculatePercentile(ICollection<float> values, float percentile)
        {
            float[] sorted = values.OrderBy(value => value).ToArray();
            int index = Mathf.Clamp(
                Mathf.CeilToInt(sorted.Length * percentile) - 1,
                0,
                sorted.Length - 1);
            return sorted[index];
        }

        private readonly struct ArmSegment
        {
            internal ArmSegment(
                string name,
                HumanBodyBones startBone,
                HumanBodyBones endBone)
            {
                Name = name;
                StartBone = startBone;
                EndBone = endBone;
            }

            internal string Name { get; }

            internal HumanBodyBones StartBone { get; }

            internal HumanBodyBones EndBone { get; }
        }

        private sealed class ArmRig
        {
            private readonly Animator _animator;

            internal ArmRig(Animator animator)
            {
                _animator = animator;
                SegmentStarts = ArmSegments
                    .Select(segment => RequireBone(animator, segment.StartBone))
                    .ToArray();
                SegmentEnds = ArmSegments
                    .Select(segment => RequireBone(animator, segment.EndBone))
                    .ToArray();
                ScopeInvariantBones = HumanoidArmPlaybackQualityTests.ScopeInvariantBones
                    .Select(animator.GetBoneTransform)
                    .ToArray();
                TorsoStart = RequireBone(animator, HumanBodyBones.Spine);
                TorsoEnd = RequireBone(animator, HumanBodyBones.Neck);
                HumanoidBones = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                    .Select(index => animator.GetBoneTransform((HumanBodyBones)index))
                    .Where(bone => bone != null)
                    .ToArray();
            }

            internal Transform[] SegmentStarts { get; }

            internal Transform[] SegmentEnds { get; }

            internal Transform[] ScopeInvariantBones { get; }

            internal Transform TorsoStart { get; }

            internal Transform TorsoEnd { get; }

            internal Transform[] HumanoidBones { get; }

            internal float ShoulderWidth => Vector3.Distance(
                RequireBone(_animator, HumanBodyBones.LeftUpperArm).position,
                RequireBone(_animator, HumanBodyBones.RightUpperArm).position);

            internal Vector3[] CaptureRootSpaceDirections()
            {
                var directions = new Vector3[ArmSegments.Length];
                for (int index = 0; index < directions.Length; index++)
                {
                    directions[index] = _animator.transform.InverseTransformDirection(
                        SegmentEnds[index].position - SegmentStarts[index].position).normalized;
                }

                return directions;
            }

            internal Quaternion[] CaptureSegmentWorldRotations()
            {
                return SegmentStarts.Select(transform => transform.rotation).ToArray();
            }

            internal Vector3 TransformDirectionToWorld(Vector3 rootSpaceDirection)
            {
                return _animator.transform.TransformDirection(rootSpaceDirection).normalized;
            }

            private static Transform RequireBone(
                Animator animator,
                HumanBodyBones boneId)
            {
                Transform bone = animator.GetBoneTransform(boneId);
                Assert.That(bone, Is.Not.Null, $"{boneId} Humanoid 본이 필요합니다.");
                return bone;
            }
        }

        private readonly struct ProximityFrame
        {
            internal ProximityFrame(
                int frameIndex,
                float timeSeconds,
                string segmentName,
                float directNormalizedDistance,
                float correctedNormalizedDistance)
            {
                FrameIndex = frameIndex;
                TimeSeconds = timeSeconds;
                SegmentName = segmentName;
                DirectNormalizedDistance = directNormalizedDistance;
                CorrectedNormalizedDistance = correctedNormalizedDistance;
            }

            internal int FrameIndex { get; }

            internal float TimeSeconds { get; }

            internal string SegmentName { get; }

            internal float DirectNormalizedDistance { get; }

            internal float CorrectedNormalizedDistance { get; }

            public override string ToString()
            {
                return $"frame={FrameIndex},time={TimeSeconds:F3},segment={SegmentName}," +
                    $"direct={DirectNormalizedDistance:F6}," +
                    $"corrected={CorrectedNormalizedDistance:F6}";
            }
        }

        private readonly struct PerformanceResult
        {
            internal PerformanceResult(
                float allocatedBytesPerSample,
                double millisecondsPerSample)
            {
                AllocatedBytesPerSample = allocatedBytesPerSample;
                MillisecondsPerSample = millisecondsPerSample;
            }

            internal float AllocatedBytesPerSample { get; }

            internal double MillisecondsPerSample { get; }
        }
    }
}
