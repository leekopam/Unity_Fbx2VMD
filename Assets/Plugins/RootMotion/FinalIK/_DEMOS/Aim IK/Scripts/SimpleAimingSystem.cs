using RootMotion.Demos;
using UnityEngine;
using System.Collections;
using RootMotion.FinalIK;

namespace RootMotion.Demos
{
    /// <summary>
    /// 6개의 정적 조준 포즈와 AimIK를 사용한 360도 조준 시스템 데모
    /// </summary>
    public class SimpleAimingSystem : MonoBehaviour
    {
        #region 인스펙터 노출 필드
        [Tooltip("AimPoser는 방향에 따라 애니메이션 이름을 반환하는 도구입니다.")]
        public AimPoser aimPoser;
        
        [Tooltip("AimIK 컴포넌트 참조")]
        public AimIK aim;
        
        [Tooltip("LookAt 컴포넌트 참조 (이 인스턴스에서는 머리에만 사용)")]
        public LookAtIK lookAt;
        
        [Tooltip("Animator 컴포넌트 참조")]
        public Animator animator;
        
        [Tooltip("포즈 간 크로스페이드 시간")]
        public float crossfadeTime = 0.2f;

        [Tooltip("조준 타겟을 일정 거리에 유지")]
        public float minAimDistance = 0.5f;
        #endregion
        
        #region Private 필드
        private AimPoser.Pose _aimPose;
        private AimPoser.Pose _lastPose;
        #endregion

        #region Unity 생명주기
        void Start()
        {
            // IK 컴포넌트를 비활성화하여 업데이트 순서 관리
            aim.enabled = false;
            lookAt.enabled = false;
        }
        
        // LateUpdate는 프레임당 한 번 호출됨
        void LateUpdate()
        {
            // 조준 포즈 전환 (레거시 애니메이션)
            Pose();

            // IK 솔버 업데이트
            aim.solver.Update();
            if (lookAt != null) lookAt.solver.Update();
        }
        #endregion
        
        #region 에임 로직
        private void Pose()
        {
            // 조준 타겟이 너무 가깝지 않도록 제한
            LimitAimTarget();

            // 조준 방향 가져오기
            Vector3 direction = (aim.solver.IKPosition - aim.solver.bones[0].transform.position);
            // 루트 트랜스폼 기준 상대 방향으로 변환
            Vector3 localDirection = transform.InverseTransformDirection(direction);
            
            // AimPoser에서 포즈 가져오기
            _aimPose = aimPoser.GetPose(localDirection);
            
            // 포즈가 변경되었을 때
            if (_aimPose != _lastPose)
            {
                // 너무 빨리 다시 전환하지 않도록 포즈의 각도 버퍼 증가
                aimPoser.SetPoseActive(_aimPose);
                
                // 변경 여부를 알 수 있도록 포즈 저장
                _lastPose = _aimPose;
            }
            
            // 직접 블렌딩
            foreach (AimPoser.Pose pose in aimPoser.poses)
            {
                if (pose == _aimPose)
                {
                    DirectCrossFade(pose.name, 1f);
                }
                else
                {
                    DirectCrossFade(pose.name, 0f);
                }
            }
        }
        #endregion
        
        #region 유틸리티
        // 조준 타겟이 너무 가깝지 않도록 제한 (첫 번째 본보다 타겟이 더 가까우면 솔버가 불안정해질 수 있음)
        void LimitAimTarget()
        {
            Vector3 aimFrom = aim.solver.bones[0].transform.position;
            Vector3 direction = (aim.solver.IKPosition - aimFrom);
            direction = direction.normalized * Mathf.Max(direction.magnitude, minAimDistance);
            
            aim.solver.IKPosition = aimFrom + direction;
        }
        
        // Mecanim의 Direct 블렌드 트리를 사용한 크로스페이드
        private void DirectCrossFade(string state, float target)
        {
            float f = Mathf.MoveTowards(animator.GetFloat(state), target, Time.deltaTime * (1f / crossfadeTime));
            animator.SetFloat(state, f);
        }
        #endregion
    }
}
