using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidPoseFrameEditingTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const float ValueTolerance = 0.0001f;

        [OneTimeSetUp]
        public void EnsureHumanoidClipImport()
        {
            Type configuratorType = RequireType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator");
            InvokeStatic(configuratorType, "EnsureHumanoid", ClipAssetPath);
        }

        [Test]
        public void Given_ClipTiming_When_ConvertingFrameAndTime_Then_UsesStableRoundedFrame()
        {
            Type calculatorType = RequireType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionFrameCalculator");

            int lastFrameIndex = (int)InvokeStatic(
                calculatorType,
                "CalculateLastFrameIndex",
                2.05f,
                30f);
            int currentFrameIndex = (int)InvokeStatic(
                calculatorType,
                "CalculateFrameIndex",
                1.02f,
                2.05f,
                30f);
            float timeSeconds = (float)InvokeStatic(
                calculatorType,
                "CalculateTimeSeconds",
                currentFrameIndex,
                2.05f,
                30f);

            Assert.That(lastFrameIndex, Is.EqualTo(62));
            Assert.That(currentFrameIndex, Is.EqualTo(31));
            Assert.That(timeSeconds, Is.EqualTo(31f / 30f).Within(ValueTolerance));
        }

        [Test]
        public void Given_MuscleDelta_When_SerializingAndRestoring_Then_PreservesCanonicalData()
        {
            Type documentType = RequireType(
                "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionDocument");
            object document = Activator.CreateInstance(
                documentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { "motion", 30f },
                culture: null);
            string muscleName = HumanTrait.MuscleName[0];

            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", 45, muscleName, 0.2f),
                Is.True);

            string json = JsonUtility.ToJson(document, prettyPrint: true);
            object restored = JsonUtility.FromJson(json, documentType);
            object[] arguments = { 45, muscleName, 0f };

            Assert.That((int)ReadProperty(restored, "SchemaVersion"), Is.EqualTo(1));
            Assert.That((string)ReadProperty(restored, "MotionName"), Is.EqualTo("motion"));
            Assert.That((float)ReadProperty(restored, "SourceFrameRate"),
                Is.EqualTo(30f).Within(ValueTolerance));
            Assert.That((int)ReadProperty(restored, "FrameCount"), Is.EqualTo(1));
            Assert.That((bool)Invoke(restored, "TryGetMuscleDelta", arguments), Is.True);
            Assert.That((float)arguments[2], Is.EqualTo(0.2f).Within(ValueTolerance));
            Assert.That(json, Does.Contain(muscleName),
                "보정 데이터는 모델 본 이름이 아니라 Unity Humanoid muscle 이름을 저장해야 합니다.");
        }

        [Test]
        public void Given_InvalidPoseCorrection_When_Storing_Then_RejectsWithoutMutation()
        {
            Type documentType = RequireType(
                "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionDocument");
            object document = Activator.CreateInstance(
                documentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { "motion", 60f },
                culture: null);

            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", -1, HumanTrait.MuscleName[0], 0.1f),
                Is.False);
            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", 1, "Unknown Bone Name", 0.1f),
                Is.False);
            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", 1, HumanTrait.MuscleName[0], float.NaN),
                Is.False);
            Assert.That((int)ReadProperty(document, "FrameCount"), Is.Zero);
        }

        [Test]
        public void Given_FrameMuscleDelta_When_ApplyingAndRemoving_Then_ChangesOnlyRequestedFrame()
        {
            object document = CreateDocument("motion", 30f);
            string muscleName = HumanTrait.MuscleName[0];
            float[] muscles = new float[HumanTrait.MuscleCount];
            muscles[0] = 0.9f;

            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", 45, muscleName, 0.2f),
                Is.True);
            Assert.That(
                (bool)Invoke(document, "HasFrameCorrection", 44),
                Is.False);
            Assert.That(
                (bool)Invoke(document, "HasFrameCorrection", 45),
                Is.True);
            Assert.That(
                (bool)Invoke(document, "TryApplyMuscleDeltas", 44, muscles),
                Is.False);
            Assert.That(muscles[0], Is.EqualTo(0.9f).Within(ValueTolerance));
            Assert.That(
                (bool)Invoke(document, "TryApplyMuscleDeltas", 45, muscles),
                Is.True);
            Assert.That(muscles[0], Is.EqualTo(1f).Within(ValueTolerance),
                "보정된 Humanoid muscle 값은 유효 범위 안에 있어야 합니다.");
            Assert.That((bool)Invoke(document, "TryRemoveFrame", 45), Is.True);
            Assert.That(
                (bool)Invoke(document, "HasFrameCorrection", 45),
                Is.False);
            Assert.That((int)ReadProperty(document, "FrameCount"), Is.Zero);
        }

        [Test]
        public void Given_SelectedFrame_When_ApplyingSameDeltaTwiceAndRestoring_Then_DoesNotAccumulateOrChangeGeometry()
        {
            GameObject target = InstantiateTarget();
            var pipelineObject = new GameObject("Humanoid Pose Frame Editing Pipeline");
            pipelineObject.SetActive(false);
            var pipeline = pipelineObject.AddComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
            object controller = CreatePlaybackController();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                Invoke(controller, "Prepare", animator, LoadHumanoidClip());
                SetField(pipeline, "_humanoidMotionPlaybackController", controller);
                SetField(pipeline, "_preparedMotionName", "motion");
                Assert.That(pipeline.TrySeekImportedMotionFrame(30), Is.True);
                Assert.That(pipeline.TryCaptureImportedMotionPose(out HumanPose originalPose),
                    Is.True);

                int muscleIndex = FindEditableMuscleIndex(originalPose.muscles);
                string muscleName = HumanTrait.MuscleName[muscleIndex];
                float delta = originalPose.muscles[muscleIndex] >= 0f ? -0.1f : 0.1f;
                Transform[] bones = CaptureHumanoidBones(animator);
                Vector3[] originalLocalPositions = bones
                    .Select(bone => bone.localPosition)
                    .ToArray();
                Vector3[] originalLocalScales = bones
                    .Select(bone => bone.localScale)
                    .ToArray();

                Assert.That(
                    pipeline.TryApplyImportedMotionMuscleDelta(muscleName, delta),
                    Is.True);
                Assert.That(pipeline.TryCaptureImportedMotionPose(out HumanPose firstPose),
                    Is.True);
                AssertGeometryUnchanged(
                    bones,
                    originalLocalPositions,
                    originalLocalScales);

                Assert.That(
                    pipeline.TryApplyImportedMotionMuscleDelta(muscleName, delta),
                    Is.True);
                Assert.That(pipeline.TryCaptureImportedMotionPose(out HumanPose secondPose),
                    Is.True);
                float expectedValue = Mathf.Clamp(
                    originalPose.muscles[muscleIndex] + delta,
                    -1f,
                    1f);
                Assert.That(firstPose.muscles[muscleIndex],
                    Is.EqualTo(expectedValue).Within(ValueTolerance));
                Assert.That(secondPose.muscles[muscleIndex],
                    Is.EqualTo(firstPose.muscles[muscleIndex]).Within(ValueTolerance),
                    "같은 delta를 다시 미리보기해도 누적 적용되면 안 됩니다.");
                Assert.That(pipeline.ImportedMotionPoseCorrectionFrameCount, Is.EqualTo(1));

                Assert.That(pipeline.TryRestoreImportedMotionPoseFrame(), Is.True);
                Assert.That(pipeline.TryCaptureImportedMotionPose(out HumanPose restoredPose),
                    Is.True);
                Assert.That(restoredPose.muscles[muscleIndex],
                    Is.EqualTo(originalPose.muscles[muscleIndex]).Within(ValueTolerance));
                Assert.That(pipeline.ImportedMotionPoseCorrectionFrameCount, Is.Zero);
                AssertGeometryUnchanged(
                    bones,
                    originalLocalPositions,
                    originalLocalScales);
            }
            finally
            {
                SetField(pipeline, "_humanoidMotionPlaybackController", null);
                Invoke(controller, "Dispose");
                UnityEngine.Object.DestroyImmediate(pipelineObject);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_FrameCorrections_When_PlayingToNextFrame_Then_AppliesCurrentFrameOnce()
        {
            GameObject target = InstantiateTarget();
            object controller = CreatePlaybackController();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                Invoke(controller, "Prepare", animator, LoadHumanoidClip());
                float frameRate = (float)ReadProperty(controller, "ClipFrameRate");
                int correctedFrameIndex = Math.Min(
                    31,
                    (int)ReadProperty(controller, "LastFrameIndex"));
                int previousFrameIndex = Math.Max(0, correctedFrameIndex - 1);

                Assert.That(
                    (bool)Invoke(controller, "SeekFrame", correctedFrameIndex),
                    Is.True);
                object[] originalPoseArguments = { null };
                Assert.That(
                    (bool)Invoke(controller, "TryCaptureCurrentPose", originalPoseArguments),
                    Is.True);
                HumanPose originalPose = (HumanPose)originalPoseArguments[0];
                int muscleIndex = FindEditableMuscleIndex(originalPose.muscles);
                string muscleName = HumanTrait.MuscleName[muscleIndex];
                float delta = originalPose.muscles[muscleIndex] >= 0f ? -0.1f : 0.1f;
                Transform[] bones = CaptureHumanoidBones(animator);
                Vector3[] originalLocalPositions = bones
                    .Select(bone => bone.localPosition)
                    .ToArray();
                Vector3[] originalLocalScales = bones
                    .Select(bone => bone.localScale)
                    .ToArray();
                object document = CreateDocument("motion", frameRate);

                Assert.That(
                    (bool)Invoke(
                        document,
                        "TrySetMuscleDelta",
                        previousFrameIndex,
                        muscleName,
                        delta),
                    Is.True);
                Assert.That(
                    (bool)Invoke(
                        document,
                        "TrySetMuscleDelta",
                        correctedFrameIndex,
                        muscleName,
                        delta),
                    Is.True);
                Assert.That(
                    (bool)Invoke(controller, "SeekFrame", previousFrameIndex),
                    Is.True);
                Assert.That(
                    (bool)Invoke(controller, "TryPreviewPoseCorrection", document),
                    Is.True);
                Assert.That((bool)Invoke(controller, "Play"), Is.True);

                Invoke(controller, "Tick", 1f / frameRate);
                object[] correctedPoseArguments = { null };
                Assert.That(
                    (bool)Invoke(controller, "TryCaptureCurrentPose", correctedPoseArguments),
                    Is.True);
                HumanPose correctedPose = (HumanPose)correctedPoseArguments[0];
                float expectedValue = Mathf.Clamp(
                    originalPose.muscles[muscleIndex] + delta,
                    -1f,
                    1f);

                Assert.That(
                    (int)ReadProperty(controller, "CurrentFrameIndex"),
                    Is.EqualTo(correctedFrameIndex));
                Assert.That(
                    correctedPose.muscles[muscleIndex],
                    Is.EqualTo(expectedValue).Within(ValueTolerance),
                    "연속 재생도 현재 프레임의 저장된 muscle delta를 적용해야 합니다.");

                Invoke(controller, "Tick", 0f);
                object[] repeatedPoseArguments = { null };
                Assert.That(
                    (bool)Invoke(controller, "TryCaptureCurrentPose", repeatedPoseArguments),
                    Is.True);
                HumanPose repeatedPose = (HumanPose)repeatedPoseArguments[0];
                Assert.That(
                    repeatedPose.muscles[muscleIndex],
                    Is.EqualTo(expectedValue).Within(ValueTolerance),
                    "동일 프레임 재평가에서 muscle delta가 누적되면 안 됩니다.");
                AssertGeometryUnchanged(
                    bones,
                    originalLocalPositions,
                    originalLocalScales);
            }
            finally
            {
                Invoke(controller, "Dispose");
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_FirstFrameCorrection_When_StoppingAndPlaying_Then_AppliesBeforeFirstTick()
        {
            GameObject target = InstantiateTarget();
            object controller = CreatePlaybackController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                object[] originalPoseArguments = { null };
                Assert.That(
                    (bool)Invoke(controller, "TryCaptureCurrentPose", originalPoseArguments),
                    Is.True);
                HumanPose originalPose = (HumanPose)originalPoseArguments[0];
                int muscleIndex = FindEditableMuscleIndex(originalPose.muscles);
                string muscleName = HumanTrait.MuscleName[muscleIndex];
                float delta = originalPose.muscles[muscleIndex] >= 0f ? -0.1f : 0.1f;
                object document = CreateDocument(
                    "motion",
                    (float)ReadProperty(controller, "ClipFrameRate"));

                Assert.That(
                    (bool)Invoke(document, "TrySetMuscleDelta", 0, muscleName, delta),
                    Is.True);
                Assert.That(
                    (bool)Invoke(controller, "TryPreviewPoseCorrection", document),
                    Is.True);
                Assert.That((bool)Invoke(controller, "Stop"), Is.True);
                Assert.That((bool)Invoke(controller, "Play"), Is.True);

                object[] correctedPoseArguments = { null };
                Assert.That(
                    (bool)Invoke(controller, "TryCaptureCurrentPose", correctedPoseArguments),
                    Is.True);
                HumanPose correctedPose = (HumanPose)correctedPoseArguments[0];
                float expectedValue = Mathf.Clamp(
                    originalPose.muscles[muscleIndex] + delta,
                    -1f,
                    1f);

                Assert.That(
                    correctedPose.muscles[muscleIndex],
                    Is.EqualTo(expectedValue).Within(ValueTolerance),
                    "녹화 시작의 Stop→Play 경계에서도 0프레임 보정이 빠지면 안 됩니다.");
            }
            finally
            {
                Invoke(controller, "Dispose");
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_SavedCorrections_When_LoadingMatchingMotion_Then_AppliesAndRejectsMismatch()
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "Fbx2VmdPoseCorrectionPipelineTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            GameObject target = InstantiateTarget();
            var pipelineObject = new GameObject("Pose Correction Persistence Pipeline");
            pipelineObject.SetActive(false);
            var pipeline = pipelineObject.AddComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                Invoke(
                    pipeline,
                    "PrepareEditorHumanoidPlayback",
                    animator,
                    clip,
                    "motion",
                    null);
                int frameIndex = Math.Min(30, pipeline.ImportedMotionLastFrameIndex);
                Assert.That(pipeline.TrySeekImportedMotionFrame(frameIndex), Is.True);
                Assert.That(pipeline.TryCaptureImportedMotionPose(out HumanPose originalPose),
                    Is.True);
                int muscleIndex = FindEditableMuscleIndex(originalPose.muscles);
                string muscleName = HumanTrait.MuscleName[muscleIndex];
                float delta = originalPose.muscles[muscleIndex] >= 0f ? -0.1f : 0.1f;
                Assert.That(
                    pipeline.TryApplyImportedMotionMuscleDelta(muscleName, delta),
                    Is.True);

                string matchingPath = Path.Combine(
                    directoryPath,
                    "motion.pose-corrections.json");
                object[] saveArguments = { matchingPath, null };
                Assert.That(
                    (bool)Invoke(
                        pipeline,
                        "TrySaveImportedMotionPoseCorrections",
                        saveArguments),
                    Is.True);
                Assert.That(saveArguments[1], Is.EqualTo(string.Empty));
                Assert.That(pipeline.TryRestoreImportedMotionPoseFrame(), Is.True);
                Assert.That(pipeline.ImportedMotionPoseCorrectionFrameCount, Is.Zero);

                object[] loadArguments = { matchingPath, null };
                Assert.That(
                    (bool)Invoke(
                        pipeline,
                        "TryLoadImportedMotionPoseCorrections",
                        loadArguments),
                    Is.True);
                Assert.That(loadArguments[1], Is.EqualTo(string.Empty));
                Assert.That(pipeline.ImportedMotionPoseCorrectionFrameCount, Is.EqualTo(1));
                Assert.That(pipeline.TryCaptureImportedMotionPose(out HumanPose loadedPose),
                    Is.True);
                Assert.That(
                    loadedPose.muscles[muscleIndex],
                    Is.EqualTo(Mathf.Clamp(
                        originalPose.muscles[muscleIndex] + delta,
                        -1f,
                        1f)).Within(ValueTolerance));

                object mismatchedDocument = CreateDocument("different-motion", clip.frameRate);
                Assert.That(
                    (bool)Invoke(
                        mismatchedDocument,
                        "TrySetMuscleDelta",
                        frameIndex,
                        muscleName,
                        delta),
                    Is.True);
                string mismatchedPath = Path.Combine(
                    directoryPath,
                    "different.pose-corrections.json");
                object[] mismatchedSaveArguments =
                {
                    mismatchedPath,
                    mismatchedDocument,
                    null
                };
                Assert.That(
                    (bool)InvokeStatic(
                        RequireType("Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionFileStore"),
                        "TrySave",
                        mismatchedSaveArguments),
                    Is.True);

                object[] mismatchedLoadArguments = { mismatchedPath, null };
                Assert.That(
                    (bool)Invoke(
                        pipeline,
                        "TryLoadImportedMotionPoseCorrections",
                        mismatchedLoadArguments),
                    Is.False);
                Assert.That((string)mismatchedLoadArguments[1], Does.Contain("모션"));
                Assert.That(pipeline.ImportedMotionPoseCorrectionFrameCount, Is.EqualTo(1),
                    "호환되지 않는 문서가 현재 보정 데이터를 덮어쓰면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
                UnityEngine.Object.DestroyImmediate(target);
                Directory.Delete(directoryPath, recursive: true);
            }
        }

        private static object CreateDocument(string motionName, float frameRate)
        {
            Type documentType = RequireType(
                "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionDocument");
            return Activator.CreateInstance(
                documentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { motionName, frameRate },
                culture: null);
        }

        private static object CreatePlaybackController()
        {
            return Activator.CreateInstance(
                RequireType("Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController"),
                nonPublic: true);
        }

        private static GameObject InstantiateTarget()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(source, Is.Not.Null, $"기준 모델을 찾을 수 없습니다: {TargetAssetPath}");
            GameObject target = UnityEngine.Object.Instantiate(source);
            target.name = "Humanoid Pose Frame Editing Target";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                Is.True);
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase
                .LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null, $"Humanoid 클립을 찾을 수 없습니다: {ClipAssetPath}");
            return clip;
        }

        private static int FindEditableMuscleIndex(float[] muscles)
        {
            Assert.That(muscles, Is.Not.Null);
            for (int index = 0; index < muscles.Length; index++)
            {
                if (!float.IsNaN(muscles[index]) &&
                    !float.IsInfinity(muscles[index]) &&
                    Mathf.Abs(muscles[index]) < 0.75f)
                {
                    return index;
                }
            }

            Assert.Fail("안전하게 delta를 적용할 Humanoid muscle을 찾지 못했습니다.");
            return -1;
        }

        private static Transform[] CaptureHumanoidBones(Animator animator)
        {
            return Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                .Select(index => animator.GetBoneTransform((HumanBodyBones)index))
                .Where(bone => bone != null)
                .Distinct()
                .ToArray();
        }

        private static void AssertGeometryUnchanged(
            Transform[] bones,
            Vector3[] expectedLocalPositions,
            Vector3[] expectedLocalScales)
        {
            for (int index = 0; index < bones.Length; index++)
            {
                Assert.That(
                    Vector3.Distance(bones[index].localPosition, expectedLocalPositions[index]),
                    Is.LessThanOrEqualTo(ValueTolerance),
                    $"{bones[index].name} localPosition이 pose 수정으로 바뀌면 안 됩니다.");
                Assert.That(
                    Vector3.Distance(bones[index].localScale, expectedLocalScales[index]),
                    Is.LessThanOrEqualTo(ValueTolerance),
                    $"{bones[index].name} localScale이 pose 수정으로 바뀌면 안 됩니다.");
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                fullName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{fullName} 타입이 필요합니다.");
            return type;
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(null, arguments);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static object ReadProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return property.GetValue(target);
        }
    }
}
