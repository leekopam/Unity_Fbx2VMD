using UnityEngine;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Play 진입과 FBX 선택 전 대기 상태에서 타깃 캐릭터의 Idle 자세를 유지하는 Guard.
    /// FBXVmdPipeline에서 추출 (Phase A-1).
    /// </summary>
    public class TargetIdlePoseGuard : MonoBehaviour
    {
        [Header("Target Idle Pose Guard")]
        [Tooltip("Play 진입과 FBX 선택 전 대기 상태에서 타깃 캐릭터가 카메라를 바라보도록 고정합니다.")]
        [SerializeField] private bool _faceTargetToCameraOnIdle = true;
        public bool ShouldFaceTargetToCameraOnIdle => _faceTargetToCameraOnIdle;

        [Tooltip("FBX가 들어오기 전 타깃 Animator Controller를 분리해 기본 모션이 재생되지 않도록 합니다.")]
        [SerializeField] private bool _detachTargetAnimatorControllerOnIdle = true;

        [Tooltip("FBX가 들어오기 전 타깃 캐릭터의 시작 자세를 매 프레임 복구합니다.")]
        [SerializeField] private bool _lockTargetPoseUntilImport = true;

        [Tooltip("Idle 자세를 유지할 대상 캐릭터")]
        [SerializeField] private GameObject _targetCharacter;

        private readonly List<TransformSnapshot> _targetIdlePose = new List<TransformSnapshot>();
        private RuntimeAnimatorController _cachedTargetController;
        private bool _hasCachedTargetController;
        private bool _idlePoseInitialized;

        public void SetTargetCharacter(GameObject target)
        {
            _targetCharacter = target;
        }

        /// <summary>
        /// 대상 캐릭터의 초기 Idle 자세를 캡처하고 유지 모드로 진입한다.
        /// </summary>
        public void Initialize()
        {
            if (_targetCharacter == null)
            {
                return;
            }

            DetachAnimatorController();

            if (_faceTargetToCameraOnIdle)
            {
                FaceToCamera();
            }

            CaptureBaseline();
            Apply();
        }

        /// <summary>
        /// 매 프레임 호출. FBX 처리 중이 아니고 활성 Retargeter가 없으면 Idle 자세를 복구한다.
        /// </summary>
        public bool TryApply(bool isProcessing, bool hasActiveRetargeter)
        {
            if (isProcessing || hasActiveRetargeter)
            {
                return false;
            }

            Apply();
            return true;
        }

        /// <summary>
        /// 컴포넌트 파괴 시 캐시된 Animator Controller를 복원한다.
        /// </summary>
        public void RestoreAnimatorController()
        {
            if (!_hasCachedTargetController || _targetCharacter == null)
            {
                return;
            }

            Animator targetAnimator = _targetCharacter.GetComponent<Animator>();
            if (targetAnimator != null && targetAnimator.runtimeAnimatorController == null)
            {
                targetAnimator.runtimeAnimatorController = _cachedTargetController;
            }
        }

        private void DetachAnimatorController()
        {
            if (!_detachTargetAnimatorControllerOnIdle || _targetCharacter == null)
            {
                return;
            }

            Animator targetAnimator = _targetCharacter.GetComponent<Animator>();
            if (targetAnimator == null)
            {
                return;
            }

            if (!_hasCachedTargetController)
            {
                _cachedTargetController = targetAnimator.runtimeAnimatorController;
                _hasCachedTargetController = true;
            }

            if (targetAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.runtimeAnimatorController = null;
            }

            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private void FaceToCamera()
        {
            if (_targetCharacter == null)
            {
                return;
            }

            Camera targetCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (targetCamera == null)
            {
                _targetCharacter.transform.rotation = Quaternion.identity;
                return;
            }

            Vector3 direction = targetCamera.transform.position - _targetCharacter.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                _targetCharacter.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void CaptureBaseline()
        {
            _targetIdlePose.Clear();

            if (_targetCharacter == null)
            {
                _idlePoseInitialized = false;
                return;
            }

            foreach (Transform targetTransform in _targetCharacter.GetComponentsInChildren<Transform>(true))
            {
                _targetIdlePose.Add(new TransformSnapshot(targetTransform));
            }

            _idlePoseInitialized = _targetIdlePose.Count > 0;
        }

        public void Apply()
        {
            if (!_lockTargetPoseUntilImport || !_idlePoseInitialized)
            {
                return;
            }

            DetachAnimatorController();

            foreach (TransformSnapshot snapshot in _targetIdlePose)
            {
                if (snapshot.Transform == null)
                {
                    continue;
                }

                snapshot.Transform.localPosition = snapshot.LocalPosition;
                snapshot.Transform.localRotation = snapshot.LocalRotation;
                snapshot.Transform.localScale = snapshot.LocalScale;
            }
        }

        private struct TransformSnapshot
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;

            public TransformSnapshot(Transform transform)
            {
                Transform = transform;
                LocalPosition = transform.localPosition;
                LocalRotation = transform.localRotation;
                LocalScale = transform.localScale;
            }
        }
    }
}
