using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Animator))] // Animator 컴포넌트 필수

// ============================================
// [실행 순서 3] IK 보정 시스템
// 역할: 발이 지면에 정확히 붙도록 IK 보정
// 실행 시점: Animator 업데이트 후 (OnAnimatorIK)
// ============================================
public class IKControl : MonoBehaviour
{
    public Animator animator;          // 애니메이터 참조
    public LayerMask groundLayer;      // 지면 레이어 (레이캐스트 대상)

    // [실행 순서 3-1] Animator IK Pass 진입 (매 프레임, Animator 업데이트 직후)
    void OnAnimatorIK(int layerIndex)
    {
        if (animator)
        {
            // 양쪽 발 IK 가중치를 100%로 설정 (완전히 IK 제어)
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);   // 왼발 위치 가중치
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1);   // 왼발 회전 가중치
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);  // 오른발 위치 가중치
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1);  // 오른발 회전 가중치

            // 각 발의 지면 접촉 보정
            AdjustFoot(AvatarIKGoal.LeftFoot);   // 왼발 보정
            AdjustFoot(AvatarIKGoal.RightFoot);  // 오른발 보정
        }
    }

    // [실행 순서 3-2] 발 위치/회전 조정 (Raycast로 지면 감지)
    void AdjustFoot(AvatarIKGoal foot)
    {
        // 현재 발 위치 가져오기
        Vector3 footPos = animator.GetIKPosition(foot);
        RaycastHit hit;

        // 발 위치에서 위로 1유닛, 아래로 1.5유닛 Raycast 발사
        if (Physics.Raycast(footPos + Vector3.up, Vector3.down, out hit, 1.5f, groundLayer))
        {
            // 지면 충돌 지점으로 발 위치 이동
            animator.SetIKPosition(foot, hit.point);
            
            // 지면 법선 방향에 맞춰 발 회전 (자연스러운 접지)
            animator.SetIKRotation(foot, Quaternion.LookRotation(transform.forward, hit.normal));
        }
    }
}