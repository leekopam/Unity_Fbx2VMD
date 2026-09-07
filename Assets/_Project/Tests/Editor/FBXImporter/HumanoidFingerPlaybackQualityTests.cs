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
    public class HumanoidFingerPlaybackQualityTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const float GeometryTolerance = 0.0001f;
        private const float MuscleTolerance = 0.0001f;
        private const float DirectionErrorLimitDegrees = 20f;
        private const float BendErrorLimitDegrees = 10f;
        private const float InducedStepLimitDegrees = 2f;
        private const int MinimumAnimatedFingerMuscleCount = 30;

        private static readonly FingerChain[] FingerChains =
        {
            new FingerChain("left_thumb", false, true,
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate,
                HumanBodyBones.LeftThumbDistal),
            new FingerChain("left_index", false, false,
                HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal),
            new FingerChain("left_middle", false, false,
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal),
            new FingerChain("left_ring", false, false,
                HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal),
            new FingerChain("left_little", false, false,
                HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal),
            new FingerChain("right_thumb", true, true,
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate,
                HumanBodyBones.RightThumbDistal),
            new FingerChain("right_index", true, false,
                HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal),
            new FingerChain("right_middle", true, false,
                HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal),
            new FingerChain("right_ring", true, false,
                HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal),
            new FingerChain("right_little", true, false,
                HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal)
        };

        private static readonly string[] FingerMuscleTokens =
        {
            "Thumb",
            "Index",
            "Middle",
            "Ring",
            "Little"
        };

        private delegate bool SeekDelegate(float timeSeconds);

        private delegate bool PoseCaptureDelegate(out HumanPose pose);

        [Test]
        public void Given_SatisfactionClip_When_EvaluatingAllFrames_Then_FingerRetargetingRemainsStable()
        {
            RequireLocalFixture();
            EnsureHumanoidClipImport();

            GameObject sourceTarget = InstantiateAsset(ClipAssetPath, "Finger Quality Source");
            GameObject directTarget = InstantiateAsset(TargetAssetPath, "Finger Quality Direct");
            GameObject correctedTarget = InstantiateAsset(TargetAssetPath, "Finger Quality Corrected");
            object sourceController = CreateController();
            object directController = CreateController();
            object correctedController = CreateController();

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

                var sourceRig = new FingerRig(sourceAnimator);
                var directRig = new FingerRig(directAnimator);
                var correctedRig = new FingerRig(correctedAnimator);
                int[] fingerMuscleIndices = ResolveFingerMuscleIndices();
                Assert.That(fingerMuscleIndices.Length, Is.EqualTo(40),
                    "Unity Humanoid finger muscle 40개를 모두 식별해야 합니다.");

                SeekDelegate sourceSeek = CreateDelegate<SeekDelegate>(sourceController, "Seek");
                SeekDelegate directSeek = CreateDelegate<SeekDelegate>(directController, "Seek");
                SeekDelegate correctedSeek = CreateDelegate<SeekDelegate>(correctedController, "Seek");
                PoseCaptureDelegate sourceCapture =
                    CreateDelegate<PoseCaptureDelegate>(sourceController, "TryCaptureCurrentPose");
                PoseCaptureDelegate directCapture =
                    CreateDelegate<PoseCaptureDelegate>(directController, "TryCaptureCurrentPose");
                PoseCaptureDelegate correctedCapture =
                    CreateDelegate<PoseCaptureDelegate>(correctedController, "TryCaptureCurrentPose");

                float frameRate = clip.frameRate > 0f ? clip.frameRate : 30f;
                int lastFrameIndex = Mathf.CeilToInt(clip.length * frameRate);
                var sourceMuscleMinimum = Enumerable.Repeat(float.PositiveInfinity, 40).ToArray();
                var sourceMuscleMaximum = Enumerable.Repeat(float.NegativeInfinity, 40).ToArray();
                Vector3[] previousSourceDirections = null;
                Vector3[] previousCorrectedDirections = null;
                float maxSourceDirectMuscleDelta = 0f;
                float maxSourceCorrectedMuscleDelta = 0f;
                float maxFingerRotationDelta = 0f;
                float maxLocalPositionDelta = 0f;
                float maxLocalScaleDelta = 0f;
                float maxThumbDirectionError = 0f;
                float maxOtherFingerDirectionError = 0f;
                float maxThumbBendError = 0f;
                float maxOtherFingerBendError = 0f;
                float maxInducedStep = 0f;
                int inducedStepOverLimitCount = 0;
                int inversionCount = 0;
                int nonFiniteCount = 0;
                QualityFrame maxDirectionErrorFrame = default;
                QualityFrame maxBendErrorFrame = default;

                for (int frameIndex = 0; frameIndex <= lastFrameIndex; frameIndex++)
                {
                    float timeSeconds = Mathf.Min(frameIndex / frameRate, clip.length);
                    Assert.That(sourceSeek(timeSeconds), Is.True);
                    Assert.That(directSeek(timeSeconds), Is.True);
                    Assert.That(correctedSeek(timeSeconds), Is.True);
                    Assert.That(sourceCapture(out HumanPose sourcePose), Is.True);
                    Assert.That(directCapture(out HumanPose directPose), Is.True);
                    Assert.That(correctedCapture(out HumanPose correctedPose), Is.True);

                    for (int muscleIndex = 0; muscleIndex < fingerMuscleIndices.Length; muscleIndex++)
                    {
                        int canonicalIndex = fingerMuscleIndices[muscleIndex];
                        float sourceValue = sourcePose.muscles[canonicalIndex];
                        float directValue = directPose.muscles[canonicalIndex];
                        float correctedValue = correctedPose.muscles[canonicalIndex];
                        if (!IsFinite(sourceValue) ||
                            !IsFinite(directValue) ||
                            !IsFinite(correctedValue))
                        {
                            nonFiniteCount++;
                            continue;
                        }

                        sourceMuscleMinimum[muscleIndex] = Mathf.Min(
                            sourceMuscleMinimum[muscleIndex],
                            sourceValue);
                        sourceMuscleMaximum[muscleIndex] = Mathf.Max(
                            sourceMuscleMaximum[muscleIndex],
                            sourceValue);
                        maxSourceDirectMuscleDelta = Mathf.Max(
                            maxSourceDirectMuscleDelta,
                            Mathf.Abs(sourceValue - directValue));
                        maxSourceCorrectedMuscleDelta = Mathf.Max(
                            maxSourceCorrectedMuscleDelta,
                            Mathf.Abs(sourceValue - correctedValue));
                    }

                    CalculateDirectCorrectionDeltas(
                        directRig,
                        correctedRig,
                        ref maxFingerRotationDelta,
                        ref maxLocalPositionDelta,
                        ref maxLocalScaleDelta);

                    Vector3[] sourceDirections = sourceRig.CapturePalmFrameDirections();
                    Vector3[] correctedDirections = correctedRig.CapturePalmFrameDirections();
                    for (int directionIndex = 0;
                         directionIndex < sourceDirections.Length;
                         directionIndex++)
                    {
                        Vector3 sourceDirection = sourceDirections[directionIndex];
                        Vector3 correctedDirection = correctedDirections[directionIndex];
                        if (!IsFinite(sourceDirection) || !IsFinite(correctedDirection))
                        {
                            nonFiniteCount++;
                            continue;
                        }

                        int chainIndex = directionIndex / 2;
                        float directionError = Vector3.Angle(
                            sourceDirection,
                            correctedDirection);
                        if (FingerChains[chainIndex].IsThumb)
                        {
                            maxThumbDirectionError = Mathf.Max(
                                maxThumbDirectionError,
                                directionError);
                        }
                        else
                        {
                            maxOtherFingerDirectionError = Mathf.Max(
                                maxOtherFingerDirectionError,
                                directionError);
                        }

                        if (directionError > maxDirectionErrorFrame.Value)
                        {
                            maxDirectionErrorFrame = new QualityFrame(
                                frameIndex,
                                timeSeconds,
                                FingerChains[chainIndex].Name,
                                directionError);
                        }

                        if (Vector3.Dot(sourceDirection, correctedDirection) < 0f)
                        {
                            inversionCount++;
                        }

                        if (previousSourceDirections == null)
                        {
                            continue;
                        }

                        float sourceStep = Vector3.Angle(
                            previousSourceDirections[directionIndex],
                            sourceDirection);
                        float correctedStep = Vector3.Angle(
                            previousCorrectedDirections[directionIndex],
                            correctedDirection);
                        float inducedStep = correctedStep - sourceStep;
                        maxInducedStep = Mathf.Max(maxInducedStep, inducedStep);
                        if (inducedStep > InducedStepLimitDegrees)
                        {
                            inducedStepOverLimitCount++;
                        }
                    }

                    for (int chainIndex = 0; chainIndex < FingerChains.Length; chainIndex++)
                    {
                        int firstDirectionIndex = chainIndex * 2;
                        float sourceBend = Vector3.Angle(
                            sourceDirections[firstDirectionIndex],
                            sourceDirections[firstDirectionIndex + 1]);
                        float correctedBend = Vector3.Angle(
                            correctedDirections[firstDirectionIndex],
                            correctedDirections[firstDirectionIndex + 1]);
                        float bendError = Mathf.Abs(sourceBend - correctedBend);
                        if (FingerChains[chainIndex].IsThumb)
                        {
                            maxThumbBendError = Mathf.Max(maxThumbBendError, bendError);
                        }
                        else
                        {
                            maxOtherFingerBendError = Mathf.Max(
                                maxOtherFingerBendError,
                                bendError);
                        }

                        if (bendError > maxBendErrorFrame.Value)
                        {
                            maxBendErrorFrame = new QualityFrame(
                                frameIndex,
                                timeSeconds,
                                FingerChains[chainIndex].Name,
                                bendError);
                        }
                    }

                    previousSourceDirections = sourceDirections;
                    previousCorrectedDirections = correctedDirections;
                }

                int animatedMuscleCount = sourceMuscleMinimum
                    .Zip(sourceMuscleMaximum, (minimum, maximum) => maximum - minimum)
                    .Count(range => range > MuscleTolerance);
                string[] staticMuscles = fingerMuscleIndices
                    .Select((canonicalIndex, localIndex) => new
                    {
                        Name = HumanTrait.MuscleName[canonicalIndex],
                        Range = sourceMuscleMaximum[localIndex] -
                            sourceMuscleMinimum[localIndex]
                    })
                    .Where(item => item.Range <= MuscleTolerance)
                    .Select(item => item.Name)
                    .ToArray();
                Debug.Log(
                    "[HumanoidFingerQuality] " +
                    $"frames={lastFrameIndex + 1}, mappedBones={correctedRig.Bones.Length}, " +
                    $"fingerMuscles={fingerMuscleIndices.Length}, animatedMuscles={animatedMuscleCount}, " +
                    $"sourceDirectMuscleDeltaMax={maxSourceDirectMuscleDelta:F9}, " +
                    $"sourceCorrectedMuscleDeltaMax={maxSourceCorrectedMuscleDelta:F9}, " +
                    $"fingerRotationDeltaMax={maxFingerRotationDelta:F9}, " +
                    $"localPositionDeltaMax={maxLocalPositionDelta:F9}, " +
                    $"localScaleDeltaMax={maxLocalScaleDelta:F9}, " +
                    $"thumbDirectionErrorMax={maxThumbDirectionError:F9}, " +
                    $"otherDirectionErrorMax={maxOtherFingerDirectionError:F9}, " +
                    $"thumbBendErrorMax={maxThumbBendError:F9}, " +
                    $"otherBendErrorMax={maxOtherFingerBendError:F9}, " +
                    $"inducedStepMax={maxInducedStep:F9}, " +
                    $"inducedStepOver2={inducedStepOverLimitCount}, " +
                    $"inversions={inversionCount}, nonFinite={nonFiniteCount}");
                Debug.Log(
                    $"[HumanoidFingerQualityCandidates] direction={maxDirectionErrorFrame};" +
                    $"bend={maxBendErrorFrame};" +
                    $"staticMuscles={string.Join(",", staticMuscles)}");

                Assert.That(nonFiniteCount, Is.Zero);
                Assert.That(correctedRig.Bones.Length, Is.EqualTo(30),
                    "양손의 Humanoid 손가락 본 30개가 모두 매핑되어야 합니다.");
                Assert.That(
                    animatedMuscleCount,
                    Is.GreaterThanOrEqualTo(MinimumAnimatedFingerMuscleCount),
                    "손가락 움직임 커버리지가 부족하면 전 프레임 품질 검증으로 인정하지 않습니다.");
                Assert.That(maxSourceDirectMuscleDelta, Is.LessThanOrEqualTo(MuscleTolerance));
                Assert.That(maxSourceCorrectedMuscleDelta, Is.LessThanOrEqualTo(MuscleTolerance));
                Assert.That(maxFingerRotationDelta, Is.LessThanOrEqualTo(GeometryTolerance),
                    "팔 방향 보정은 손가락 localRotation을 직접 바꾸면 안 됩니다.");
                Assert.That(maxLocalPositionDelta, Is.LessThanOrEqualTo(GeometryTolerance));
                Assert.That(maxLocalScaleDelta, Is.LessThanOrEqualTo(GeometryTolerance));
                Assert.That(inversionCount, Is.Zero,
                    "원본과 반대 방향으로 뒤집힌 손가락 구간이 없어야 합니다.");
                Assert.That(inducedStepOverLimitCount, Is.Zero,
                    "대상 손가락의 프레임 이동이 원본보다 2도 넘게 급격해지면 안 됩니다.");
                Assert.That(maxThumbDirectionError, Is.LessThanOrEqualTo(DirectionErrorLimitDegrees));
                Assert.That(maxOtherFingerDirectionError, Is.LessThanOrEqualTo(DirectionErrorLimitDegrees));
                Assert.That(maxThumbBendError, Is.LessThanOrEqualTo(BendErrorLimitDegrees));
                Assert.That(maxOtherFingerBendError, Is.LessThanOrEqualTo(BendErrorLimitDegrees));
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

        private static void CalculateDirectCorrectionDeltas(
            FingerRig directRig,
            FingerRig correctedRig,
            ref float maxRotationDelta,
            ref float maxLocalPositionDelta,
            ref float maxLocalScaleDelta)
        {
            Assert.That(correctedRig.Bones.Length, Is.EqualTo(directRig.Bones.Length));
            for (int index = 0; index < directRig.Bones.Length; index++)
            {
                Transform direct = directRig.Bones[index];
                Transform corrected = correctedRig.Bones[index];
                maxRotationDelta = Mathf.Max(
                    maxRotationDelta,
                    Quaternion.Angle(direct.localRotation, corrected.localRotation));
                maxLocalPositionDelta = Mathf.Max(
                    maxLocalPositionDelta,
                    Vector3.Distance(direct.localPosition, corrected.localPosition));
                maxLocalScaleDelta = Mathf.Max(
                    maxLocalScaleDelta,
                    Vector3.Distance(direct.localScale, corrected.localScale));
            }
        }

        private static int[] ResolveFingerMuscleIndices()
        {
            return HumanTrait.MuscleName
                .Select((name, index) => new { Name = name, Index = index })
                .Where(item => FingerMuscleTokens.Any(token =>
                    item.Name.IndexOf(token, StringComparison.Ordinal) >= 0))
                .Select(item => item.Index)
                .ToArray();
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

        private static T CreateDelegate<T>(object target, string methodName)
            where T : Delegate
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return (T)method.CreateDelegate(typeof(T), target);
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

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private readonly struct FingerChain
        {
            internal FingerChain(
                string name,
                bool isRight,
                bool isThumb,
                HumanBodyBones proximal,
                HumanBodyBones intermediate,
                HumanBodyBones distal)
            {
                Name = name;
                IsRight = isRight;
                IsThumb = isThumb;
                Proximal = proximal;
                Intermediate = intermediate;
                Distal = distal;
            }

            internal string Name { get; }

            internal bool IsRight { get; }

            internal bool IsThumb { get; }

            internal HumanBodyBones Proximal { get; }

            internal HumanBodyBones Intermediate { get; }

            internal HumanBodyBones Distal { get; }
        }

        private readonly struct PalmFrame
        {
            internal PalmFrame(Vector3 lateral, Vector3 normal, Vector3 forward)
            {
                Lateral = lateral;
                Normal = normal;
                Forward = forward;
            }

            private Vector3 Lateral { get; }

            private Vector3 Normal { get; }

            private Vector3 Forward { get; }

            internal Vector3 TransformDirection(Vector3 worldDirection)
            {
                Vector3 direction = worldDirection.normalized;
                return new Vector3(
                    Vector3.Dot(direction, Lateral),
                    Vector3.Dot(direction, Normal),
                    Vector3.Dot(direction, Forward)).normalized;
            }
        }

        private sealed class FingerRig
        {
            private readonly Transform[] _proximalBones;
            private readonly Transform[] _intermediateBones;
            private readonly Transform[] _distalBones;
            private readonly Transform _leftHand;
            private readonly Transform _rightHand;
            private readonly Transform _leftIndex;
            private readonly Transform _leftMiddle;
            private readonly Transform _leftLittle;
            private readonly Transform _rightIndex;
            private readonly Transform _rightMiddle;
            private readonly Transform _rightLittle;

            internal FingerRig(Animator animator)
            {
                _proximalBones = FingerChains
                    .Select(chain => RequireBone(animator, chain.Proximal))
                    .ToArray();
                _intermediateBones = FingerChains
                    .Select(chain => RequireBone(animator, chain.Intermediate))
                    .ToArray();
                _distalBones = FingerChains
                    .Select(chain => RequireBone(animator, chain.Distal))
                    .ToArray();
                Bones = _proximalBones
                    .Concat(_intermediateBones)
                    .Concat(_distalBones)
                    .ToArray();
                _leftHand = RequireBone(animator, HumanBodyBones.LeftHand);
                _rightHand = RequireBone(animator, HumanBodyBones.RightHand);
                _leftIndex = RequireBone(animator, HumanBodyBones.LeftIndexProximal);
                _leftMiddle = RequireBone(animator, HumanBodyBones.LeftMiddleProximal);
                _leftLittle = RequireBone(animator, HumanBodyBones.LeftLittleProximal);
                _rightIndex = RequireBone(animator, HumanBodyBones.RightIndexProximal);
                _rightMiddle = RequireBone(animator, HumanBodyBones.RightMiddleProximal);
                _rightLittle = RequireBone(animator, HumanBodyBones.RightLittleProximal);
            }

            internal Transform[] Bones { get; }

            internal Vector3[] CapturePalmFrameDirections()
            {
                PalmFrame leftFrame = CreatePalmFrame(
                    _leftHand,
                    _leftIndex,
                    _leftMiddle,
                    _leftLittle);
                PalmFrame rightFrame = CreatePalmFrame(
                    _rightHand,
                    _rightIndex,
                    _rightMiddle,
                    _rightLittle);
                var directions = new Vector3[FingerChains.Length * 2];
                for (int chainIndex = 0; chainIndex < FingerChains.Length; chainIndex++)
                {
                    PalmFrame frame = FingerChains[chainIndex].IsRight
                        ? rightFrame
                        : leftFrame;
                    directions[chainIndex * 2] = frame.TransformDirection(
                        _intermediateBones[chainIndex].position -
                        _proximalBones[chainIndex].position);
                    directions[chainIndex * 2 + 1] = frame.TransformDirection(
                        _distalBones[chainIndex].position -
                        _intermediateBones[chainIndex].position);
                }

                return directions;
            }

            private static PalmFrame CreatePalmFrame(
                Transform hand,
                Transform index,
                Transform middle,
                Transform little)
            {
                Vector3 forward = (middle.position - hand.position).normalized;
                Vector3 lateral = (index.position - little.position).normalized;
                Vector3 normal = Vector3.Cross(lateral, forward).normalized;
                Assert.That(forward.sqrMagnitude, Is.GreaterThan(0.999f));
                Assert.That(lateral.sqrMagnitude, Is.GreaterThan(0.999f));
                Assert.That(normal.sqrMagnitude, Is.GreaterThan(0.999f));
                lateral = Vector3.Cross(forward, normal).normalized;
                return new PalmFrame(lateral, normal, forward);
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

        private readonly struct QualityFrame
        {
            internal QualityFrame(
                int frameIndex,
                float timeSeconds,
                string chainName,
                float value)
            {
                FrameIndex = frameIndex;
                TimeSeconds = timeSeconds;
                ChainName = chainName;
                Value = value;
            }

            private int FrameIndex { get; }

            private float TimeSeconds { get; }

            private string ChainName { get; }

            internal float Value { get; }

            public override string ToString()
            {
                return $"frame={FrameIndex},time={TimeSeconds:F3},chain={ChainName},value={Value:F6}";
            }
        }
    }
}
