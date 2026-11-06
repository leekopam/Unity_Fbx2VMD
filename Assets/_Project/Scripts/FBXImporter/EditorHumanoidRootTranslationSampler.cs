#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 에디터 Humanoid 클립의 RootT XZ 궤적을 절대 시간 기준으로 샘플링함.
    /// </summary>
    internal sealed class EditorHumanoidRootTranslationSampler
    {
        private const string RootTranslationXProperty = "RootT.x";
        private const string RootTranslationZProperty = "RootT.z";

        private AnimationCurve _rootTranslationX;
        private AnimationCurve _rootTranslationZ;
        private Vector3 _clipSpaceOrigin;
        private float _clipLengthSeconds;
        private float _sourceHumanScale = 1f;

        internal bool IsAvailable =>
            _rootTranslationX != null &&
            _rootTranslationZ != null;

        internal void Initialize(AnimationClip clip)
        {
            Clear();
            if (clip == null || !clip.humanMotion)
            {
                return;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type != typeof(Animator) || !string.IsNullOrEmpty(binding.path))
                {
                    continue;
                }

                if (binding.propertyName == RootTranslationXProperty)
                {
                    _rootTranslationX = AnimationUtility.GetEditorCurve(clip, binding);
                }
                else if (binding.propertyName == RootTranslationZProperty)
                {
                    _rootTranslationZ = AnimationUtility.GetEditorCurve(clip, binding);
                }
            }

            if (!IsAvailable)
            {
                Clear();
                return;
            }

            _clipLengthSeconds = Mathf.Max(0f, clip.length);
            _clipSpaceOrigin = SampleClipSpacePosition(0f);
            _sourceHumanScale = ResolveSourceHumanScale(clip);
        }

        internal bool TryEvaluateClipSpaceOffset(
            float timeSeconds,
            out Vector3 clipSpaceOffset)
        {
            clipSpaceOffset = Vector3.zero;
            if (!IsAvailable || !IsFinite(timeSeconds))
            {
                return false;
            }

            float evaluationTime = Mathf.Clamp(timeSeconds, 0f, _clipLengthSeconds);
            // RootT는 Humanoid 정규화 좌표이므로 대상 체형과 무관한 원본 미터 이동량으로 복원함.
            clipSpaceOffset = (SampleClipSpacePosition(evaluationTime) - _clipSpaceOrigin) * _sourceHumanScale;
            clipSpaceOffset.y = 0f;
            return IsFinite(clipSpaceOffset);
        }

        internal void Clear()
        {
            _rootTranslationX = null;
            _rootTranslationZ = null;
            _clipSpaceOrigin = Vector3.zero;
            _clipLengthSeconds = 0f;
            _sourceHumanScale = 1f;
        }

        private static float ResolveSourceHumanScale(AnimationClip clip)
        {
            string sourcePath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return 1f;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                return 1f;
            }

            Animator animator = source.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                float humanScale = animator.humanScale;
                if (IsFinite(humanScale) && humanScale > 0f)
                {
                    return humanScale;
                }
            }

            Debug.LogWarning($"원본 Avatar 스케일을 확인할 수 없어 기존 RootT 배율을 유지합니다: {sourcePath}");
            return 1f;
        }

        private Vector3 SampleClipSpacePosition(float timeSeconds)
        {
            return new Vector3(
                _rootTranslationX.Evaluate(timeSeconds),
                0f,
                _rootTranslationZ.Evaluate(timeSeconds));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif
