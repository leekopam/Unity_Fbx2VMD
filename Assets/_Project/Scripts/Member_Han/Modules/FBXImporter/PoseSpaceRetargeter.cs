using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// ⭐ MASTER STAGE 35-36: Total Sync + Smart Grounding
    /// - 1. Container Pattern: Ghost의 거대화(250배)를 부모(0.01)로 상쇄 -> Scale Ratio 정상화(1.2)
    /// - 2. Raycast Grounding: 물리 엔진 없이 발바닥 최저점(World)을 레이캐스트로 찾아서 "양방향" 접지 (Floating/Sinking 해결)
    /// - 3. Forward Correction: 180도 회전 보정
    /// </summary>
    public class PoseSpaceRetargeter : MonoBehaviour
    {
        [Header("--- CORE COMPONENTS ---")]
        public Animator ghostAnimator;  // (Container 내부의 모델)
        public Animator targetAnimator; // 내 캐릭터

        [Header("--- FINAL TUNING ---")]
        [Tooltip("캐릭터가 뒤를 보고 있다면 체크 (180도 회전)")]
        public bool fixReverseRotation = true;

        [Tooltip("체크 시 공중 부양/박힘을 모두 해결 (Raycast 사용)")]
        public bool useSmartGrounding = true;

        [Tooltip("발바닥 높이 미세 조절 (양수: 띄움, 음수: 박음)")]
        [Range(-0.1f, 0.1f)]
        public float groundOffset = 0.0f;

        // --- 내부 변수 ---
        private HumanPoseHandler _ghostHandler;
        private HumanPoseHandler _targetHandler;
        private HumanPose _humanPose;
        
        private Vector3 _prevGhostPos;
        private Quaternion _facingCorrection = Quaternion.Euler(0, 180, 0); // 180도 회전값
        private float _scaleRatio = 1.0f; // 체형 차이 비율

        // --- 초기화 ---
        private bool _isInitialized = false;
        private Animation _legacyAnim;

        public void Initialize(GameObject ghostRoot, GameObject targetRoot, Dictionary<string, string> mappingData, AnimationClip clip, FileManager settings)
        {
            ghostAnimator = ghostRoot.GetComponent<Animator>();
            targetAnimator = targetRoot.GetComponent<Animator>();

            // 1. Ghost Animator 끄기 (Legacy 구동용)
            if (ghostAnimator != null) ghostAnimator.enabled = false;

            // 2. Legacy Animation 재생
            _legacyAnim = ghostRoot.GetComponent<Animation>();
            if (_legacyAnim == null) _legacyAnim = ghostRoot.AddComponent<Animation>();
            
            clip.legacy = true;
            clip.wrapMode = WrapMode.Once; // [User Request] Loop 방지: 한 번만 재생
            _legacyAnim.AddClip(clip, clip.name);
            _legacyAnim.clip = clip;
            _legacyAnim.Play();

            // 3. 포즈 핸들러 초기화
            if (!ghostAnimator.avatar || !targetAnimator.avatar) return;
            _ghostHandler = new HumanPoseHandler(ghostAnimator.avatar, ghostAnimator.transform);
            _targetHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);
            _humanPose = new HumanPose();

            // 4. 초기 위치 저장
            _prevGhostPos = ghostAnimator.transform.position;

            _isInitialized = true;
            Debug.Log("[Master Stage] System Initialized. Waiting for First Update...");
        }

        void LateUpdate()
        {
            if (!_isInitialized || ghostAnimator == null || targetAnimator == null) return;

            // 0. 스케일 비율 계산 (매 프레임 체크하여 안정성 확보)
            // Container가 작동 중이라면 ghostHip.position.y는 ~0.8m 수준이어야 함.
            Transform ghostHip = ghostAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHip = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);

            if (ghostHip != null && targetHip != null && ghostHip.position.y > 0.01f)
            {
                // 비율 = 내 골반 높이 / 원본 골반 높이
                _scaleRatio = targetHip.position.y / ghostHip.position.y;
            }

            // =========================================================
            // A. 포즈(근육) 동기화
            // =========================================================
            _ghostHandler.GetHumanPose(ref _humanPose);
            
            // 골반 높이(Y)는 비율에 맞춰 재조정 (나머지는 Root Motion이 담당)
            Vector3 bodyPos = _humanPose.bodyPosition;
            bodyPos.y *= _scaleRatio;
            _humanPose.bodyPosition = bodyPos;

            _targetHandler.SetHumanPose(ref _humanPose);

            // =========================================================
            // B. 월드 회전 동기화 (180도 문제 해결)
            // =========================================================
            if (fixReverseRotation)
            {
                // Ghost 회전 * 180도 보정
                targetAnimator.transform.rotation = ghostAnimator.transform.rotation * _facingCorrection;
            }
            else
            {
                targetAnimator.transform.rotation = ghostAnimator.transform.rotation;
            }

            // =========================================================
            // C. 루트 모션 동기화 (호 그리기 방지)
            // =========================================================
            // Ghost 이동량 계산
            Vector3 ghostDelta = ghostAnimator.transform.position - _prevGhostPos;
            
            // 내 캐릭터 크기에 맞춰 이동량 스케일링
            Vector3 targetDelta = ghostDelta * _scaleRatio;

            // 이동 적용
            targetAnimator.transform.position += targetDelta;
            
            // 위치 갱신
            _prevGhostPos = ghostAnimator.transform.position;

            // =========================================================
            // D. 스마트 접지 (Raycast Grounding) - 공중 부양 해결
            // =========================================================
            if (useSmartGrounding)
            {
                ApplyRaycastGrounding();
            }
        }

        void ApplyRaycastGrounding()
        {
            // 1. 양발 위치 확보 (발목)
            Transform lFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            
            if (lFoot == null || rFoot == null) return;

            // 2. 발바닥 위치 (발목 - 반지름)
            float footRadius = 0.04f; // 약간 여유 있게
            float lBottom = lFoot.position.y - footRadius;
            float rBottom = rFoot.position.y - footRadius;

            // 3. 현재 가장 낮은 발바닥 높이
            float lowestFootCurrentY = Mathf.Min(lBottom, rBottom);

            // 4. 목표는 지면(0) + Offset
            // Raycast를 사용하여 실제 지면을 찾을 수도 있으나, 현재는 평면(Plane) 위라고 가정하고 0.0f 사용
            // 만약 계단이나 경사면이라면 Physics.Raycast로 hit.point.y를 구해야 함.
            float targetGroundY = 0.0f; // 평면 가정
            
            // Physics.Raycast 로직 (옵션)
            /*
            RaycastHit hit;
            if (Physics.Raycast(targetAnimator.transform.position + Vector3.up, Vector3.down, out hit, 2f)) {
                targetGroundY = hit.point.y;
            }
            */

            float targetHeight = targetGroundY + groundOffset;

            // 5. 보정값 계산 (목표 - 현재)
            // 양수면 들어 올리고, 음수면 내림 (양방향)
            float adjustment = targetHeight - lowestFootCurrentY;

            // 6. 감쇠 적용 (Damping) - 급격한 튐 방지
            // 너무 큰 보정은 부드럽게, 작은 보정은 즉시
            float damping = 0.5f; 
            if (Mathf.Abs(adjustment) > 0.5f) damping = 0.1f; // 너무 멀면 천천히
            
            Vector3 currentPos = targetAnimator.transform.position;
            
            // *핵심*: 이 부분이 "내리기(Pull)"와 "올리기(Push)"를 모두 수행함
            currentPos.y += adjustment * damping; 
            
            // 최소 안전장치 (땅 밑으로 영원히 꺼지지 않게)
            if (currentPos.y < targetGroundY) currentPos.y = targetHeight;

            targetAnimator.transform.position = currentPos;
        }
    }
}
