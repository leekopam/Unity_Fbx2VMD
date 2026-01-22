using UnityEngine;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// [Unit Engine] 단일 뼈에 대한 델타 리타겟팅을 수행합니다.
    /// 수학적 상대값(Delta)을 계산하여 적용하므로 초기 자세(A-Pose/T-Pose) 차이를 무시합니다.
    /// </summary>
    public class BoneLinker
    {
        private Transform _ghostBone;
        private Transform _targetBone;

        // [핵심 변경] 로컬이 아닌 '월드 회전'을 기준점으로 잡습니다.
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

            // [초기화] 현재(T-Pose)의 '월드 회전값'을 저장합니다.
            // 이때 Ghost와 Target은 반드시 T-Pose 상태여야 합니다 (PoseSpaceRetargeter가 보장함)
            _ghostRestRot = ghost.rotation;
            _targetRestRot = target.rotation;

            // 위치는 월드 포지션 기준으로 델타를 계산합니다.
            if (_mapPosition)
            {
                _ghostRestPos = ghost.position;
                _targetRestPos = target.position;
            }
        }

        public void Tick()
        {
            if (_ghostBone == null || _targetBone == null) return;

            // ---------------------------------------------------------
            // [알고리즘 교체] World Space Delta Copy
            // ---------------------------------------------------------
            
            // 1. Ghost가 Rest Pose에서 월드 기준으로 얼마나 회전했는가? (Delta)
            // 수식: Current = Delta * Rest  =>  Delta = Current * Inverse(Rest)
            Quaternion worldDelta = _ghostBone.rotation * Quaternion.Inverse(_ghostRestRot);

            // 2. Target의 Rest Pose에 그 Delta를 월드 기준으로 적용한다.
            // TargetCurrent = Delta * TargetRest
            _targetBone.rotation = worldDelta * _targetRestRot;

            // 3. 위치 리타겟팅 (Hips 전용)
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
