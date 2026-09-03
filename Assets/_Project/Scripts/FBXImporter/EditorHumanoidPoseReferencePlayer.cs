#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 대상 모델의 원본 프리팹에서 Native Humanoid 기준 포즈를 샘플링함.
    /// </summary>
    internal sealed class EditorHumanoidPoseReferencePlayer : IDisposable
    {
        private readonly NativeHumanoidAnimationPlayer _animationPlayer =
            new NativeHumanoidAnimationPlayer();

        private GameObject _referenceInstance;
        private HumanPoseHandler _poseHandler;

        internal bool IsInitialized =>
            _referenceInstance != null &&
            _poseHandler != null &&
            _animationPlayer.IsInitialized;

        internal void Initialize(Animator targetAnimator, AnimationClip clip)
        {
            if (targetAnimator == null)
            {
                throw new ArgumentNullException(nameof(targetAnimator));
            }

            Dispose();

            GameObject referenceSource =
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(targetAnimator.gameObject) ??
                targetAnimator.gameObject;
            _referenceInstance = UnityEngine.Object.Instantiate(referenceSource);
            _referenceInstance.name = $"EditorHumanoidPoseReference_{referenceSource.name}";
            _referenceInstance.hideFlags = HideFlags.HideAndDontSave;
            _referenceInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            DisableRuntimeComponents(_referenceInstance);
            _referenceInstance.SetActive(true);

            Animator referenceAnimator =
                _referenceInstance.GetComponent<Animator>() ??
                _referenceInstance.GetComponentInChildren<Animator>(true);
            if (referenceAnimator == null)
            {
                throw new InvalidOperationException(
                    "Native Humanoid 기준 모델에 Animator가 없습니다.");
            }

            referenceAnimator.runtimeAnimatorController = null;
            referenceAnimator.enabled = true;
            _animationPlayer.Initialize(referenceAnimator, clip);
            _poseHandler = new HumanPoseHandler(
                referenceAnimator.avatar,
                referenceAnimator.transform);
        }

        internal bool TryEvaluateAt(float timeSeconds, ref HumanPose pose)
        {
            if (!IsInitialized)
            {
                return false;
            }

            _animationPlayer.EvaluateAt(timeSeconds);
            _poseHandler.GetHumanPose(ref pose);
            return IsFinite(pose);
        }

        public void Dispose()
        {
            _animationPlayer.Dispose();
            _poseHandler?.Dispose();
            _poseHandler = null;

            if (_referenceInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_referenceInstance);
                _referenceInstance = null;
            }
        }

        private static void DisableRuntimeComponents(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Animation animation in root.GetComponentsInChildren<Animation>(true))
            {
                animation.enabled = false;
            }
        }

        private static bool IsFinite(HumanPose pose)
        {
            if (!IsFinite(pose.bodyPosition) || !IsFinite(pose.bodyRotation) || pose.muscles == null)
            {
                return false;
            }

            for (int i = 0; i < pose.muscles.Length; i++)
            {
                if (!IsFinite(pose.muscles[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif
