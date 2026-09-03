#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class NativeHumanoidPlaybackBaselineResult
    {
        internal NativeHumanoidPlaybackBaselineResult(
            bool captured,
            string reportPath,
            string startImagePath,
            string sampleImagePath)
        {
            Captured = captured;
            ReportPath = reportPath;
            StartImagePath = startImagePath;
            SampleImagePath = sampleImagePath;
        }

        internal bool Captured { get; }
        internal string ReportPath { get; }
        internal string StartImagePath { get; }
        internal string SampleImagePath { get; }
    }

    /// <summary>
    /// 저장되지 않는 additive 씬에서 Native Humanoid 직접 재생 기준선을 생성함.
    /// </summary>
    internal static class NativeHumanoidPlaybackBaselineRunner
    {
        private const string DefaultTargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string DefaultClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string MenuPath =
            "Tools/FBX2VMD/검증/Native Humanoid 기준선 생성";
        private const float TransformTolerance = 0.0001f;
        private const float MinimumPixelChangeRatio = 0.005f;

        [MenuItem(MenuPath)]
        private static void RunDefaultFixture()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Unity 프로젝트 루트를 찾을 수 없습니다.");
            }

            string outputRoot = Path.Combine(
                projectRoot,
                "Docs",
                "Workflow",
                "Local",
                "evidence",
                "native-humanoid-baseline");
            NativeHumanoidPlaybackBaselineResult result = Run(
                DefaultTargetAssetPath,
                DefaultClipAssetPath,
                outputRoot);

            if (!result.Captured)
            {
                throw new InvalidOperationException(
                    $"Native Humanoid 기준선 캡처에 실패했습니다: {result.ReportPath}");
            }

            Debug.Log(
                $"[NativeHumanoidBaseline] CAPTURED report={result.ReportPath} " +
                $"start={result.StartImagePath} sample={result.SampleImagePath}");
        }

        internal static NativeHumanoidPlaybackBaselineResult Run(
            string targetAssetPath,
            string clipAssetPath,
            string outputRoot)
        {
            ValidatePath(targetAssetPath, nameof(targetAssetPath));
            ValidatePath(clipAssetPath, nameof(clipAssetPath));
            ValidatePath(outputRoot, nameof(outputRoot));

            EditorHumanoidClipImportConfigurator.EnsureHumanoid(clipAssetPath);
            GameObject targetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(targetAssetPath);
            if (targetAsset == null)
            {
                throw new InvalidOperationException(
                    $"Humanoid 대상 모델을 찾을 수 없습니다: {targetAssetPath}");
            }

            AnimationClip clip = EditorAnimationClipAssetLoader.LoadFirst(clipAssetPath);
            if (clip == null || !clip.humanMotion)
            {
                throw new InvalidOperationException(
                    $"Humanoid AnimationClip을 찾을 수 없습니다: {clipAssetPath}");
            }

            string generatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sessionName = $"{sessionId}_native_humanoid_baseline";
            string sessionDirectory = Path.Combine(outputRoot, sessionName);
            Directory.CreateDirectory(sessionDirectory);

            string targetToken = Sanitize(targetAsset.name, 14);
            string clipToken = Sanitize(clip.name, 14);
            string startImagePath = Path.Combine(
                sessionDirectory,
                $"{sessionId}_temp-scene_{targetToken}_{clipToken}_start_" +
                "native_no-ghost-ik-correction.png");
            string sampleImagePath = Path.Combine(
                sessionDirectory,
                $"{sessionId}_temp-scene_{targetToken}_{clipToken}_sample_" +
                "native_no-ghost-ik-correction.png");
            string reportPath = Path.Combine(sessionDirectory, "index.json");

            Scene originalActiveScene = SceneManager.GetActiveScene();
            Scene baselineScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            GameObject target = null;
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            try
            {
                target = UnityEngine.Object.Instantiate(targetAsset);
                target.name = "Native Humanoid Baseline Target";
                target.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(target, baselineScene);
                target.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                NativeHumanoidBaselineImageCapture.PrepareTarget(target);
                target.SetActive(true);

                Animator animator = RequireHumanoidAnimator(target);
                int disabledMonoBehaviourCount = DisableMonoBehaviours(target);
                int disabledLegacyAnimationCount = DisableLegacyAnimation(target);
                animator.runtimeAnimatorController = null;
                animator.enabled = true;

                Vector3 initialRootScale = target.transform.localScale;
                float sampleTime = 0f;
                NativeHumanoidBaselinePoseMetrics metrics;

                using (var player = new NativeHumanoidAnimationPlayer())
                {
                    player.Initialize(animator, clip);
                    player.EvaluateAt(0f);
                    var firstPose = NativeHumanoidBaselinePoseAnalyzer.Capture(animator);
                    sampleTime = NativeHumanoidBaselinePoseAnalyzer.SelectComparisonTime(
                        player,
                        animator,
                        firstPose,
                        clip.length);

                    player.EvaluateAt(0f);
                    Bounds framingBounds =
                        NativeHumanoidBaselineImageCapture.CalculateRendererBounds(target);
                    Camera camera = NativeHumanoidBaselineImageCapture.CreateCamera(
                        baselineScene,
                        framingBounds);
                    NativeHumanoidBaselineImageCapture.CreateDirectionalLight(
                        baselineScene,
                        camera.transform.rotation);
                    Color32[] startPixels = NativeHumanoidBaselineImageCapture.Render(
                        camera,
                        startImagePath);

                    player.EvaluateAt(sampleTime);
                    var samplePose = NativeHumanoidBaselinePoseAnalyzer.Capture(animator);
                    Color32[] samplePixels = NativeHumanoidBaselineImageCapture.Render(
                        camera,
                        sampleImagePath);

                    metrics = NativeHumanoidBaselinePoseAnalyzer.Calculate(
                        firstPose,
                        samplePose);
                    NativeHumanoidBaselineImageMetrics imageMetrics =
                        NativeHumanoidBaselineImageCapture.CalculateDifference(
                            startPixels,
                            samplePixels);
                    metrics.ChangedPixelCount = imageMetrics.ChangedPixelCount;
                    metrics.PixelChangeRatio = imageMetrics.PixelChangeRatio;
                    metrics.RootScaleDelta =
                        Vector3.Distance(initialRootScale, target.transform.localScale);
                    metrics.IsFootIkEnabled = player.IsFootIkEnabled;
                    metrics.IsPlayableIkEnabled = player.IsPlayableIkEnabled;
                    metrics.HasBoundPlayables = animator.hasBoundPlayables;
                }

                int activeMonoBehaviourCount = CountEnabledMonoBehaviours(target);
                int activeLegacyAnimationCount = CountEnabledLegacyAnimation(target);
                bool baselineCaptured =
                    metrics.HasBoundPlayables &&
                    !metrics.IsFootIkEnabled &&
                    !metrics.IsPlayableIkEnabled &&
                    activeMonoBehaviourCount == 0 &&
                    activeLegacyAnimationCount == 0 &&
                    metrics.MovingBoneCount >= 3 &&
                    metrics.PixelChangeRatio >= MinimumPixelChangeRatio &&
                    metrics.RootScaleDelta <= TransformTolerance &&
                    metrics.MaxBoneScaleDelta <= TransformTolerance &&
                    metrics.MaxBoneLengthDelta <= TransformTolerance;

                var report = new BaselineReport
                {
                    schemaVersion = "native-humanoid-playback-baseline@1",
                    generatedAt = generatedAt,
                    baselineCaptured = baselineCaptured,
                    targetAssetPath = targetAssetPath,
                    targetName = targetAsset.name,
                    clipAssetPath = clipAssetPath,
                    clipName = clip.name,
                    clipLengthSeconds = clip.length,
                    sampleTimeSeconds = sampleTime,
                    isHumanoidClip = clip.humanMotion,
                    isValidHumanAvatar = animator.avatar != null &&
                        animator.avatar.isValid && animator.avatar.isHuman,
                    hasBoundPlayables = metrics.HasBoundPlayables,
                    isFootIkEnabled = metrics.IsFootIkEnabled,
                    isPlayableIkEnabled = metrics.IsPlayableIkEnabled,
                    disabledMonoBehaviourCount = disabledMonoBehaviourCount,
                    activeMonoBehaviourCount = activeMonoBehaviourCount,
                    disabledLegacyAnimationCount = disabledLegacyAnimationCount,
                    activeLegacyAnimationCount = activeLegacyAnimationCount,
                    mappedBoneCount = metrics.MappedBoneCount,
                    movingBoneCount = metrics.MovingBoneCount,
                    rootScaleDelta = metrics.RootScaleDelta,
                    maxBoneScaleDelta = metrics.MaxBoneScaleDelta,
                    maxBoneLengthDelta = metrics.MaxBoneLengthDelta,
                    maxBoneRotationDeltaDegrees = metrics.MaxBoneRotationDeltaDegrees,
                    bodyMotionScoreDegrees = metrics.BodyMotionScoreDegrees,
                    changedPixelCount = metrics.ChangedPixelCount,
                    pixelChangeRatio = metrics.PixelChangeRatio,
                    startImagePath = startImagePath,
                    sampleImagePath = sampleImagePath,
                    playbackPath = "AnimationClipPlayable -> AnimationPlayableOutput -> Animator",
                    correctionPolicy = "Ghost, Legacy Animation, IK, MonoBehaviour 보정 비활성",
                    sceneIsolation = "저장하지 않는 additive 임시 씬과 전용 layer 30 사용",
                    sampleSelection = "20개 균등 후보 중 주요 Humanoid 본 회전 변화 합 최대 시점",
                    visualQualityStatus = "REQUIRES_HUMAN_REVIEW",
                };
                File.WriteAllText(
                    reportPath,
                    JsonUtility.ToJson(report, prettyPrint: true),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                return new NativeHumanoidPlaybackBaselineResult(
                    baselineCaptured,
                    reportPath,
                    startImagePath,
                    sampleImagePath);
            }
            finally
            {
                DestroyTemporaryTarget(target);

                if (baselineScene.IsValid() && baselineScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(baselineScene, removeScene: true);
                }

                if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(originalActiveScene);
                }
            }
        }

        private static void DestroyTemporaryTarget(GameObject target)
        {
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                throw new InvalidOperationException(
                    "대상 모델에는 유효한 Humanoid Animator가 필요합니다.");
            }

            return animator;
        }

        private static int DisableMonoBehaviours(GameObject target)
        {
            int count = 0;
            foreach (MonoBehaviour behaviour in
                target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour.enabled)
                {
                    continue;
                }

                behaviour.enabled = false;
                count++;
            }

            return count;
        }

        private static int DisableLegacyAnimation(GameObject target)
        {
            int count = 0;
            foreach (Animation animation in target.GetComponentsInChildren<Animation>(true))
            {
                if (!animation.enabled)
                {
                    continue;
                }

                animation.enabled = false;
                count++;
            }

            return count;
        }

        private static int CountEnabledMonoBehaviours(GameObject target)
        {
            int count = 0;
            foreach (MonoBehaviour behaviour in
                target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour.enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountEnabledLegacyAnimation(GameObject target)
        {
            int count = 0;
            foreach (Animation animation in target.GetComponentsInChildren<Animation>(true))
            {
                if (animation.enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static void ValidatePath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("경로가 필요합니다.", parameterName);
            }
        }

        private static string Sanitize(string value, int maxLength)
        {
            var builder = new StringBuilder(value ?? string.Empty);
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                builder.Replace(invalidCharacter, '_');
            }

            string sanitized = builder.ToString().Replace(' ', '_');
            return sanitized.Length <= maxLength
                ? sanitized
                : sanitized.Substring(0, maxLength);
        }

        [Serializable]
        private sealed class BaselineReport
        {
            public string schemaVersion;
            public string generatedAt;
            public bool baselineCaptured;
            public string targetAssetPath;
            public string targetName;
            public string clipAssetPath;
            public string clipName;
            public float clipLengthSeconds;
            public float sampleTimeSeconds;
            public bool isHumanoidClip;
            public bool isValidHumanAvatar;
            public bool hasBoundPlayables;
            public bool isFootIkEnabled;
            public bool isPlayableIkEnabled;
            public int disabledMonoBehaviourCount;
            public int activeMonoBehaviourCount;
            public int disabledLegacyAnimationCount;
            public int activeLegacyAnimationCount;
            public int mappedBoneCount;
            public int movingBoneCount;
            public float rootScaleDelta;
            public float maxBoneScaleDelta;
            public float maxBoneLengthDelta;
            public float maxBoneRotationDeltaDegrees;
            public float bodyMotionScoreDegrees;
            public int changedPixelCount;
            public float pixelChangeRatio;
            public string startImagePath;
            public string sampleImagePath;
            public string playbackPath;
            public string correctionPolicy;
            public string sceneIsolation;
            public string sampleSelection;
            public string visualQualityStatus;
        }
    }
}
#endif
