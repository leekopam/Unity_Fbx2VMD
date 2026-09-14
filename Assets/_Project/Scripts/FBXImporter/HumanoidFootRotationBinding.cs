using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 모델별 발 본 축을 고정된 전방·상방 기준으로 연결하고 적용 회전을 복원함.
    /// </summary>
    internal sealed class HumanoidFootRotationBinding
    {
        private readonly Transform _foot;
        private readonly Quaternion _localFrame;
        private Quaternion _originalLocalRotation;
        private bool _hasAppliedRotation;

        private HumanoidFootRotationBinding(Transform foot, Quaternion localFrame)
        {
            _foot = foot;
            _localFrame = localFrame;
        }

        internal static bool TryCreate(Transform foot, Transform toes, Vector3 up,
            out HumanoidFootRotationBinding binding)
        {
            binding = null;
            if (!HasSupportedTransform(foot) || toes == null || !toes.IsChildOf(foot) ||
                !IsFinite(up.sqrMagnitude) || Mathf.Abs(up.sqrMagnitude - 1f) > 0.0001f)
                return false;
            Vector3 forward = Vector3.ProjectOnPlane(toes.position - foot.position, up);
            if (!IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < 0.00000001f)
                return false;
            Quaternion localFrame = Quaternion.Inverse(foot.rotation) *
                Quaternion.LookRotation(forward.normalized, up);
            if (!IsUnitRotation(localFrame))
                return false;
            binding = new HumanoidFootRotationBinding(foot, localFrame.normalized);
            return true;
        }

        internal bool TryCaptureWorldFrame(out Quaternion frame)
        {
            frame = Quaternion.identity;
            if (!HasSupportedTransform(_foot))
                return false;
            frame = (_foot.rotation * _localFrame).normalized;
            return IsUnitRotation(frame);
        }

        internal bool TryApplyWorldFrame(Quaternion frame)
        {
            if (!IsUnitRotation(frame) || !HasSupportedTransform(_foot))
                return false;
            // 같은 평가 안에서 여러 번 적용해도 최초 원본 자세만 복원 대상으로 유지함.
            if (!_hasAppliedRotation)
                _originalLocalRotation = _foot.localRotation;
            _foot.rotation = (frame * Quaternion.Inverse(_localFrame)).normalized;
            _hasAppliedRotation = true;
            return true;
        }

        internal void RestoreAppliedRotation()
        {
            if (_hasAppliedRotation && _foot != null)
                _foot.localRotation = _originalLocalRotation;
            _hasAppliedRotation = false;
        }

        private static bool HasSupportedTransform(Transform foot)
        {
            if (foot == null || !IsUnitRotation(foot.rotation))
                return false;
            float scale = foot.lossyScale.x;
            if (!IsFinite(scale) || scale <= 0f)
                return false;
            Matrix4x4 matrix = foot.localToWorldMatrix;
            // 반사·비균일 배율·전단은 회전만으로 기준축을 전달할 수 없으므로 거부함.
            return Vector3.Distance(matrix.MultiplyVector(Vector3.right) / scale, foot.rotation * Vector3.right) < 0.0001f &&
                Vector3.Distance(matrix.MultiplyVector(Vector3.up) / scale, foot.rotation * Vector3.up) < 0.0001f &&
                Vector3.Distance(matrix.MultiplyVector(Vector3.forward) / scale, foot.rotation * Vector3.forward) < 0.0001f;
        }

        private static bool IsUnitRotation(Quaternion value)
        {
            float length = Quaternion.Dot(value, value);
            return IsFinite(length) && Mathf.Abs(length - 1f) < 0.0001f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
