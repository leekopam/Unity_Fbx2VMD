using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Member_Han.Modules.FBXImporter
{
    public class PoseSpaceRetargeter : MonoBehaviour
    {
        // 유니티 네이티브 핸들러 사용
        private HumanPoseHandler _srcHandler; // Ghost
        private HumanPoseHandler _destHandler; // Target
        private HumanPose _humanPose;
        private bool _isInitialized = false;

        public void Initialize(GameObject ghostRoot, GameObject targetRoot, Dictionary<string, string> mappingData, AnimationClip clipToPlay)
        {
            StartCoroutine(InitializeRoutine(ghostRoot, targetRoot, mappingData, clipToPlay));
        }

        private IEnumerator InitializeRoutine(GameObject ghostRoot, GameObject targetRoot, Dictionary<string, string> mappingData, AnimationClip clip)
        {
            Debug.Log("[PoseSpaceRetargeter] ⏳ 네이티브 리타겟팅 시퀀스 시작...");

            // 1. Target 초기화 (앉은 자세 방지 - 필수)
            var targetAnimator = targetRoot.GetComponent<Animator>();
            if (targetAnimator != null)
            {
                targetAnimator.runtimeAnimatorController = null;
                targetAnimator.Rebind();
                targetAnimator.Update(0f);
            }

            // 2. Ghost 초기화 및 Animator 확인
            var ghostAnimator = ghostRoot.GetComponent<Animator>();
            if (ghostAnimator == null) ghostAnimator = ghostRoot.AddComponent<Animator>();
            
            // Ghost의 Legacy Animation 컴포넌트 (재생용)
            // Importer에서 이미 Animation 컴포넌트를 붙이고 클립을 넣어뒀으므로 가져오기만 하면 됨
            var ghostLegacy = ghostRoot.GetComponent<Animation>();
            if (ghostLegacy == null) ghostLegacy = ghostRoot.AddComponent<Animation>();
            ghostLegacy.Stop();

            // 3. [복원] Ghost Root 정렬 (Alignment)
            // 180도 돌린 Ghost와 Target의 방향을 맞춥니다.
            ghostRoot.transform.position = targetRoot.transform.position;
            ghostRoot.transform.rotation = targetRoot.transform.rotation; 

            // T-Pose 안정화를 위한 대기 (필수)
            yield return new WaitForEndOfFrame();

            // 4. 네이티브 핸들러 연결
            // HumanoidAvatarBuilder가 만든 Avatar를 믿고 사용합니다.
            if (ghostAnimator.avatar == null || !ghostAnimator.avatar.isValid || targetAnimator.avatar == null)
            {
                Debug.LogError("❌ Avatar 설정 오류! HumanoidAvatarBuilder를 확인하세요.");
                yield break;
            }

            _srcHandler = new HumanPoseHandler(ghostAnimator.avatar, ghostRoot.transform);
            _destHandler = new HumanPoseHandler(targetAnimator.avatar, targetRoot.transform);
            _humanPose = new HumanPose();

            _isInitialized = true;
            Debug.Log($"[PoseSpaceRetargeter] ✅ 네이티브 엔진 가동 완료.");

            // 5. 애니메이션 재생 (시간 보정된 클립)
            if (clip != null)
            {
                clip.legacy = true;
                clip.wrapMode = WrapMode.Loop;
                // 이미 Importer에서 AddClip을 했지만 안전하게 다시 확인
                if (ghostLegacy.GetClip(clip.name) == null)
                    ghostLegacy.AddClip(clip, clip.name);
                    
                ghostLegacy.clip = clip;
                ghostLegacy.Play(clip.name);
                Debug.Log($"[PoseSpaceRetargeter] 🎬 Action! Ghost 재생: {clip.name} ({clip.length:F2}s)");
            }
        }

        void LateUpdate()
        {
            if (!_isInitialized) return;

            // [핵심] 유니티 엔진이 알아서 축 변환 및 리타겟팅 수행
            _srcHandler.GetHumanPose(ref _humanPose);
            
            // [FIX A] 방향 전환 (뒤로 돌아 -> 앞으로 봐)
            // 현재 월드 회전에 180도(Y축)를 곱해서 반대로 돌림
            Quaternion turnAround = Quaternion.Euler(0, 180f, 0);
            _humanPose.bodyRotation = turnAround * _humanPose.bodyRotation;

            // [FIX B] 높이 복원 (가라앉음 해결)
            // 기존에 있던 '_humanPose.bodyPosition = Vector3.zero;' 삭제함!
            // 이제 원본 애니메이션의 높이(Y)와 이동(XZ)이 그대로 적용됩니다.
            
            _destHandler.SetHumanPose(ref _humanPose);
        }
    }
}
