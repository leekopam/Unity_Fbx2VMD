using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public class BoneLinker
    {
        private Transform _ghostBone;
        private Transform _targetBone;

        private Quaternion _ghostRestRot; 
        private Quaternion _targetRestRot; 
        
        private Vector3 _ghostRestPos;
        private Vector3 _targetRestPos;
        private bool _mapPosition;

        public BoneLinker(Transform ghost, Transform target, bool mapPosition = false)
        {
            _ghostBone = ghost;
            _targetBone = target;
            _mapPosition = mapPosition;

            // 현재(T-Pose)의 '월드 회전값'을 저장
            // 이때 Ghost와 Target은 반드시 T-Pose 상태여야 함 (PoseSpaceRetargeter가 보장함)
            _ghostRestRot = ghost.rotation;
            _targetRestRot = target.rotation;

            // 위치는 월드 포지션 기준으로 델타를 계산
            if (_mapPosition)
            {
                _ghostRestPos = ghost.position;
                _targetRestPos = target.position;
            }
        }

        public void Tick()
        {
            if (_ghostBone == null || _targetBone == null) return;
            
            // Ghost가 Rest Pose에서 월드 기준으로 얼마나 회전했는가? (Delta)
            Quaternion worldDelta = _ghostBone.rotation * Quaternion.Inverse(_ghostRestRot);

            // Target의 Rest Pose에 그 Delta를 월드 기준으로 적용
            _targetBone.rotation = worldDelta * _targetRestRot;

            // 위치 리타겟팅 (Hips 전용)
            if (_mapPosition)
            {
                // Ghost가 이동한 월드 벡터 계산
                Vector3 moveDelta = _ghostBone.position - _ghostRestPos;
                
                // Target의 원래 위치에 이동량만 더함
                _targetBone.position = _targetRestPos + moveDelta;
            }
        }
    }
}
