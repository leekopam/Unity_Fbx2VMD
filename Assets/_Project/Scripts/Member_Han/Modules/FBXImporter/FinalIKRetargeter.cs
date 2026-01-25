using UnityEngine;
using RootMotion.FinalIK;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// [v21] Final IK 하이브리드 리타겟팅
    /// - Ghost에서 SampleAnimation() 재생
    /// - VRIK를 사용하여 Target이 Ghost를 따라가도록 설정
    /// - 이중 안전장치: GetBoneTransform() + 이름 기반 폴백
    /// </summary>
    public class FinalIKRetargeter : MonoBehaviour
    {
        #region Public Fields
        [Header("Target (애니메이션이 적용될 캐릭터)")]
        public Animator targetAnimator;
        
        [Header("Ghost (애니메이션 소스)")]
        public GameObject ghostObject;
        public Animator ghostAnimator;
        public AnimationClip ghostClip;
        
        [Header("Debug")]
        public bool showDebugLog = true;
        #endregion

        #region Private Fields
        private VRIK _vrik;
        private float _currentTime = 0f;
        private bool _isInitialized = false;
        
        // Ghost 뼈 캐싱
        private Transform _ghostHead;
        private Transform _ghostHips;
        private Transform _ghostLeftHand;
        private Transform _ghostRightHand;
        private Transform _ghostLeftFoot;
        private Transform _ghostRightFoot;
        #endregion

        #region Unity Lifecycle
        void Start()
        {
            Initialize();
        }

        void LateUpdate()
        {
            if (!_isInitialized || ghostClip == null || ghostObject == null) return;

            // 1. 애니메이션 시간 업데이트 (루프)
            _currentTime += Time.deltaTime;
            if (_currentTime > ghostClip.length)
            {
                _currentTime = 0f;
            }

            // 2. Ghost 애니메이션 샘플링 - Ghost 스켈레톤에 직접 적용
            ghostClip.SampleAnimation(ghostObject, _currentTime);
        }
        #endregion

        #region Initialization
        private void Initialize()
        {
            if (targetAnimator == null || ghostObject == null)
            {
                Debug.LogError("[FinalIKRetargeter] Target Animator 또는 Ghost Object가 할당되지 않았습니다.");
                return;
            }

            // Ghost Animator 확인 (없으면 이름 기반만 사용)
            if (ghostAnimator == null)
            {
                ghostAnimator = ghostObject.GetComponent<Animator>();
            }

            // 1. Ghost 스케일 정규화
            NormalizeGhostScale();

            // 2. Ghost 뼈 캐싱 (이중 안전장치)
            CacheGhostBones();

            // 3. VRIK 설정
            SetupVRIK();

            _isInitialized = true;
            
            if (showDebugLog)
            {
            }
        }

        /// <summary>
        /// Ghost 캐릭터를 Target 캐릭터와 동일한 크기로 스케일링
        /// </summary>
        private void NormalizeGhostScale()
        {
            float targetHeight = GetCharacterHeight(targetAnimator.gameObject);
            float ghostHeight = GetCharacterHeight(ghostObject);

            if (ghostHeight <= 0.001f)
            {
                Debug.LogWarning("[FinalIKRetargeter] Ghost 높이가 너무 작습니다. 스케일링 생략.");
                return;
            }

            float ratio = targetHeight / ghostHeight;
            ghostObject.transform.localScale = Vector3.one * ratio;

            if (showDebugLog)
            {
            }
        }

        /// <summary>
        /// 캐릭터의 높이를 측정
        /// </summary>
        private float GetCharacterHeight(GameObject character)
        {
            // 1차: Animator에서 Head 찾기
            Animator anim = character.GetComponent<Animator>();
            if (anim != null && anim.avatar != null && anim.avatar.isHuman)
            {
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    return head.position.y - character.transform.position.y;
                }
            }

            // 2차: 이름으로 Head 찾기
            Transform headByName = FindDeepChild(character.transform, "Head");
            if (headByName == null) headByName = FindDeepChild(character.transform, "mixamorig:Head");
            if (headByName != null)
            {
                return headByName.position.y - character.transform.position.y;
            }

            // 3차: Bounds 사용
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = new Bounds(character.transform.position, Vector3.zero);
                foreach (Renderer r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }
                return bounds.size.y;
            }

            Debug.LogWarning("[FinalIKRetargeter] 높이 측정 실패. 기본값 1.7 반환.");
            return 1.7f;
        }

        /// <summary>
        /// Ghost의 주요 뼈들을 캐싱 (이중 안전장치)
        /// </summary>
        private void CacheGhostBones()
        {
            _ghostHead = GetGhostBone(HumanBodyBones.Head, "Head", "mixamorig:Head");
            _ghostHips = GetGhostBone(HumanBodyBones.Hips, "Hips", "mixamorig:Hips");
            _ghostLeftHand = GetGhostBone(HumanBodyBones.LeftHand, "LeftHand", "mixamorig:LeftHand");
            _ghostRightHand = GetGhostBone(HumanBodyBones.RightHand, "RightHand", "mixamorig:RightHand");
            _ghostLeftFoot = GetGhostBone(HumanBodyBones.LeftFoot, "LeftFoot", "mixamorig:LeftFoot");
            _ghostRightFoot = GetGhostBone(HumanBodyBones.RightFoot, "RightFoot", "mixamorig:RightFoot");

            if (showDebugLog)
            {
                int mappedCount = 0;
                if (_ghostHead != null) mappedCount++;
                if (_ghostHips != null) mappedCount++;
                if (_ghostLeftHand != null) mappedCount++;
                if (_ghostRightHand != null) mappedCount++;
                if (_ghostLeftFoot != null) mappedCount++;
                if (_ghostRightFoot != null) mappedCount++;
            }
        }

        /// <summary>
        /// Ghost 뼈 찾기 (이중 안전장치)
        /// </summary>
        private Transform GetGhostBone(HumanBodyBones bone, params string[] fallbackNames)
        {
            // 1차: Humanoid Avatar (가장 정확)
            if (ghostAnimator != null && ghostAnimator.avatar != null && ghostAnimator.avatar.isHuman)
            {
                Transform t = ghostAnimator.GetBoneTransform(bone);
                if (t != null) return t;
            }
            
            // 2차: 이름 기반 폴백
            foreach (string name in fallbackNames)
            {
                Transform found = FindDeepChild(ghostObject.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// 계층 구조에서 이름으로 Transform 찾기
        /// </summary>
        private Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase) ||
                parent.name.EndsWith(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return parent;
            }

            foreach (Transform child in parent)
            {
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// VRIK 컴포넌트 설정 및 Solver Target 바인딩
        /// </summary>
        private void SetupVRIK()
        {
            // 1. VRIK 컴포넌트 확인 또는 추가
            _vrik = targetAnimator.GetComponent<VRIK>();
            if (_vrik == null)
            {
                _vrik = targetAnimator.gameObject.AddComponent<VRIK>();
                if (showDebugLog)
                {
                }
            }

            // 2. VRIK References 자동 감지
            _vrik.AutoDetectReferences();

            // 3. Solver Targets 할당
            // Head
            if (_ghostHead != null)
            {
                _vrik.solver.spine.headTarget = _ghostHead;
                _vrik.solver.spine.positionWeight = 1f;
                _vrik.solver.spine.rotationWeight = 1f;
            }

            // Pelvis (Hips)
            if (_ghostHips != null)
            {
                _vrik.solver.spine.pelvisTarget = _ghostHips;
                _vrik.solver.spine.pelvisPositionWeight = 1f;
                _vrik.solver.spine.pelvisRotationWeight = 1f;
            }

            // Left Arm
            if (_ghostLeftHand != null)
            {
                _vrik.solver.leftArm.target = _ghostLeftHand;
                _vrik.solver.leftArm.positionWeight = 1f;
                _vrik.solver.leftArm.rotationWeight = 1f;
            }

            // Right Arm
            if (_ghostRightHand != null)
            {
                _vrik.solver.rightArm.target = _ghostRightHand;
                _vrik.solver.rightArm.positionWeight = 1f;
                _vrik.solver.rightArm.rotationWeight = 1f;
            }

            // Left Leg
            if (_ghostLeftFoot != null)
            {
                _vrik.solver.leftLeg.target = _ghostLeftFoot;
                _vrik.solver.leftLeg.positionWeight = 1f;
                _vrik.solver.leftLeg.rotationWeight = 1f;
            }

            // Right Leg
            if (_ghostRightFoot != null)
            {
                _vrik.solver.rightLeg.target = _ghostRightFoot;
                _vrik.solver.rightLeg.positionWeight = 1f;
                _vrik.solver.rightLeg.rotationWeight = 1f;
            }

            if (showDebugLog)
            {
            }
        }
        #endregion
    }
}
