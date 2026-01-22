using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// [v14] Hybrid Retargeting: Animation Rigging + Custom Hips Position
    /// - 회전(Rotation): Animation Rigging의 MultiParentConstraint 사용 (MaintainOffset으로 T-Pose/A-Pose 차이 자동 보정)
    /// - 골반 위치(Hips Position): Custom Code로 스케일 비율 보정 (58배 차이 문제 해결)
    /// </summary>
    public class RuntimeRetargeter : MonoBehaviour
    {
        #region Public 필드
        [Header("Ghost (애니메이션 소스)")]
        public AnimationClip ghostClip;
        public GameObject ghostRootBone;
        
        [Header("Target (적용 대상)")]
        public Animator targetAnimator;

        [Header("Debug Settings (FileManager에서 주입됨)")]
        public bool showMappingDebug = true;
        public bool showRuntimeDebug = false;
        #endregion

        #region Private 필드
        // Bone Mapping
        private readonly Dictionary<HumanBodyBones, Transform> _ghostBoneMap = new();
        private readonly Dictionary<HumanBodyBones, Transform> _targetBoneMap = new();

        // Initial Poses (for Hybrid: Hips Position only)
        private Vector3 _ghostHipsInitPos;
        private Vector3 _targetHipsInitPos;
        private float _hipsHeightRatio = 1.0f;

        // Animation Rigging
        private RigBuilder _rigBuilder;
        private Rig _rig;
        private bool _rigBuilt = false;

        private float _currentTime = 0f;
        #endregion

        #region Unity Lifecycle
        void Start()
        {
            InitializeRetargeting();
        }

        void LateUpdate()
        {
            if (ghostClip == null || ghostRootBone == null || !_rigBuilt) return;

            // 1. 시간 업데이트 (루프)
            _currentTime += Time.deltaTime;
            if (_currentTime > ghostClip.length) _currentTime = 0f;

            // 2. Ghost 애니메이션 샘플링 (Ghost 뼈들이 움직임 -> Constraint가 자동으로 Target에 적용)
            ghostClip.SampleAnimation(ghostRootBone, _currentTime);

            // 3. [Hybrid] Hips Position: Custom Code (스케일 보정)
            ApplyScaledHipsPosition();
        }
        #endregion

        #region Initialization
        private void InitializeRetargeting()
        {
            if (ghostClip == null || ghostRootBone == null)
            {
                Debug.LogError("[RuntimeRetargeter] Ghost Clip or RootBone is missing.");
                return;
            }

            _currentTime = 0f;
            _targetBoneMap.Clear();
            _ghostBoneMap.Clear();

            // 1. Target Bones 매핑 (Animator 기준)
            if (targetAnimator == null) targetAnimator = GetComponent<Animator>();
            if (targetAnimator == null)
            {
                Debug.LogError("[RuntimeRetargeter] Target Animator missing.");
                return;
            }

            for (HumanBodyBones bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
            {
                Transform t = targetAnimator.GetBoneTransform(bone);
                if (t != null) _targetBoneMap[bone] = t;
            }

            // 2. Ghost Bones 매핑 (Heuristic)
            MapGhostBones();

            // 3. Hips 비율 계산 (Hybrid용)
            CalculateHipsRatio();

            // 4. Animation Rigging 빌드
            BuildRig();

            Debug.Log($"[RuntimeRetargeter] Initialized. Target: {_targetBoneMap.Count}, Ghost: {_ghostBoneMap.Count}");
        }

        private void CalculateHipsRatio()
        {
            if (!_ghostBoneMap.ContainsKey(HumanBodyBones.Hips) || !_targetBoneMap.ContainsKey(HumanBodyBones.Hips))
            {
                _hipsHeightRatio = 1.0f;
                return;
            }

            _ghostHipsInitPos = _ghostBoneMap[HumanBodyBones.Hips].localPosition;
            _targetHipsInitPos = _targetBoneMap[HumanBodyBones.Hips].localPosition;

            // Head - Hips 높이로 비율 계산
            float ghostHeight = GetBoneWorldY(HumanBodyBones.Head, _ghostBoneMap) - GetBoneWorldY(HumanBodyBones.Hips, _ghostBoneMap);
            float targetHeight = GetBoneWorldY(HumanBodyBones.Head, _targetBoneMap) - GetBoneWorldY(HumanBodyBones.Hips, _targetBoneMap);

            // Fallback: Neck 사용
            if (ghostHeight <= 0.01f) ghostHeight = GetBoneWorldY(HumanBodyBones.Neck, _ghostBoneMap) - GetBoneWorldY(HumanBodyBones.Hips, _ghostBoneMap);
            if (targetHeight <= 0.01f) targetHeight = GetBoneWorldY(HumanBodyBones.Neck, _targetBoneMap) - GetBoneWorldY(HumanBodyBones.Hips, _targetBoneMap);

            // 최종 Fallback
            if (ghostHeight <= 0.01f) ghostHeight = 1f;
            if (targetHeight <= 0.01f) targetHeight = 1f;

            _hipsHeightRatio = targetHeight / ghostHeight;

            if (showMappingDebug)
            {
                Debug.Log($"[RuntimeRetargeter] Scale Ratio: {_hipsHeightRatio:F4} (Ghost: {ghostHeight:F2}, Target: {targetHeight:F2})");
            }
        }

        private float GetBoneWorldY(HumanBodyBones bone, Dictionary<HumanBodyBones, Transform> map)
        {
            return map.TryGetValue(bone, out Transform t) ? t.position.y : 0f;
        }
        #endregion

        #region Ghost Bone Mapping
        private void MapGhostBones()
        {
            _ghostBoneMap.Clear();
            Transform[] allBones = ghostRootBone.GetComponentsInChildren<Transform>();
            List<string> unmappedBones = new();

            foreach (Transform bone in allBones)
            {
                HumanBodyBones guessedBone = GuessHumanBodyBone(bone.name);

                if (guessedBone != HumanBodyBones.LastBone && !_ghostBoneMap.ContainsKey(guessedBone))
                {
                    _ghostBoneMap[guessedBone] = bone;
                }
                else if (guessedBone == HumanBodyBones.LastBone && bone.name != ghostRootBone.name)
                {
                    unmappedBones.Add(bone.name);
                }
            }

            // Fallback: Hips 강제 검색
            if (!_ghostBoneMap.ContainsKey(HumanBodyBones.Hips))
            {
                var hipBone = FindDeepChild(ghostRootBone.transform, "Hips")
                           ?? FindDeepChild(ghostRootBone.transform, "Hip")
                           ?? FindDeepChild(ghostRootBone.transform, "Pelvis");
                if (hipBone != null) _ghostBoneMap[HumanBodyBones.Hips] = hipBone;
            }

            // 진단 로그 (showMappingDebug가 켜져 있을 때만)
            if (showMappingDebug && unmappedBones.Count > 0)
            {
                Debug.LogWarning($"[RuntimeRetargeter] ⚠️ 매핑 실패한 Ghost 뼈 ({unmappedBones.Count}개):\n{string.Join(", ", unmappedBones)}");
            }
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>())
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return child;
            }
            return null;
        }

        private HumanBodyBones GuessHumanBodyBone(string boneName)
        {
            string lower = boneName.ToLower().Replace(" ", "").Replace("_", "").Replace("-", "").Replace(":", "");

            // Hips
            if (lower.Contains("hips") || lower.Contains("pelvis")) return HumanBodyBones.Hips;

            // Spine
            if (lower.Contains("spine2") || lower.Contains("chest")) return HumanBodyBones.Chest;
            if (lower.Contains("spine1")) return HumanBodyBones.Spine;
            if (lower.Contains("spine")) return HumanBodyBones.Spine;

            // Head & Neck
            if (lower.Contains("neck")) return HumanBodyBones.Neck;
            if (lower.Contains("head")) return HumanBodyBones.Head;

            // Legs (Mixamo: UpLeg/Leg)
            if (lower.Contains("left"))
            {
                if (lower.Contains("foot") || lower.Contains("ankle")) return HumanBodyBones.LeftFoot;
                if (lower.Contains("toe")) return HumanBodyBones.LeftToes;
                if (lower.Contains("upleg") || lower.Contains("thigh") || lower.Contains("upperleg")) return HumanBodyBones.LeftUpperLeg;
                if (lower.Contains("leg") || lower.Contains("calf") || lower.Contains("shin")) return HumanBodyBones.LeftLowerLeg;
            }
            if (lower.Contains("right"))
            {
                if (lower.Contains("foot") || lower.Contains("ankle")) return HumanBodyBones.RightFoot;
                if (lower.Contains("toe")) return HumanBodyBones.RightToes;
                if (lower.Contains("upleg") || lower.Contains("thigh") || lower.Contains("upperleg")) return HumanBodyBones.RightUpperLeg;
                if (lower.Contains("leg") || lower.Contains("calf") || lower.Contains("shin")) return HumanBodyBones.RightLowerLeg;
            }

            // Arms (Mixamo: Arm/ForeArm)
            if (lower.Contains("left"))
            {
                if (lower.Contains("hand") && !lower.Contains("thumb") && !lower.Contains("index") && !lower.Contains("middle") && !lower.Contains("ring") && !lower.Contains("pinky")) return HumanBodyBones.LeftHand;
                if (lower.Contains("shoulder") || lower.Contains("clavicle")) return HumanBodyBones.LeftShoulder;
                if (lower.Contains("forearm") || lower.Contains("lowerarm")) return HumanBodyBones.LeftLowerArm;
                if (lower.Contains("arm")) return HumanBodyBones.LeftUpperArm;
            }
            if (lower.Contains("right"))
            {
                if (lower.Contains("hand") && !lower.Contains("thumb") && !lower.Contains("index") && !lower.Contains("middle") && !lower.Contains("ring") && !lower.Contains("pinky")) return HumanBodyBones.RightHand;
                if (lower.Contains("shoulder") || lower.Contains("clavicle")) return HumanBodyBones.RightShoulder;
                if (lower.Contains("forearm") || lower.Contains("lowerarm")) return HumanBodyBones.RightLowerArm;
                if (lower.Contains("arm")) return HumanBodyBones.RightUpperArm;
            }

            return HumanBodyBones.LastBone;
        }
        #endregion

        #region Animation Rigging
        private void BuildRig()
        {
            // 1. RigBuilder 추가 (Target Root에)
            _rigBuilder = targetAnimator.gameObject.GetComponent<RigBuilder>();
            if (_rigBuilder == null)
            {
                _rigBuilder = targetAnimator.gameObject.AddComponent<RigBuilder>();
            }

            // 2. Rig GameObject 생성
            GameObject rigGO = new GameObject("RuntimeRig");
            rigGO.transform.SetParent(targetAnimator.transform, false);
            _rig = rigGO.AddComponent<Rig>();
            _rig.weight = 1f;

            // 3. 각 매핑된 뼈에 대해 Constraint 생성
            int constraintCount = 0;
            foreach (var kvp in _ghostBoneMap)
            {
                HumanBodyBones bone = kvp.Key;
                Transform ghostBone = kvp.Value;

                if (!_targetBoneMap.TryGetValue(bone, out Transform targetBone)) continue;

                // Hips Position은 Custom Code가 담당하므로 Rotation만 Constraint
                // 다른 뼈들은 모두 Rotation만 적용 (Position은 팔다리 늘어남 방지)
                CreateRotationConstraint(rigGO.transform, bone, ghostBone, targetBone);
                constraintCount++;
            }

            // 4. RigBuilder에 Rig 등록 및 빌드
            _rigBuilder.layers.Clear();
            _rigBuilder.layers.Add(new RigLayer(_rig, true));
            _rigBuilder.Build();

            _rigBuilt = true;

            if (showMappingDebug)
            {
                Debug.Log($"[RuntimeRetargeter] ✅ Animation Rigging 빌드 완료: {constraintCount}개 Constraint 생성");
            }
        }

        private void CreateRotationConstraint(Transform rigParent, HumanBodyBones bone, Transform ghostBone, Transform targetBone)
        {
            // Constraint용 빈 오브젝트 생성
            GameObject constraintGO = new GameObject($"Constraint_{bone}");
            constraintGO.transform.SetParent(rigParent, false);

            // MultiRotationConstraint 사용 (Rotation만 따라가기)
            var constraint = constraintGO.AddComponent<MultiRotationConstraint>();
            constraint.weight = 1f;

            // Constrained Object (Target 뼈)
            constraint.data.constrainedObject = targetBone;

            // Source (Ghost 뼈)
            var sourceObjects = new WeightedTransformArray(1);
            sourceObjects[0] = new WeightedTransform(ghostBone, 1f);
            constraint.data.sourceObjects = sourceObjects;

            // MaintainOffset: T-Pose/A-Pose 차이를 자동 보정
            constraint.data.maintainOffset = true;
        }
        #endregion

        #region Hybrid: Custom Hips Position
        private void ApplyScaledHipsPosition()
        {
            if (!_ghostBoneMap.TryGetValue(HumanBodyBones.Hips, out Transform ghostHips)) return;
            if (!_targetBoneMap.TryGetValue(HumanBodyBones.Hips, out Transform targetHips)) return;

            // Ghost Hips의 현재 위치와 초기 위치 차이 계산
            Vector3 ghostCurrentPos = ghostHips.localPosition;
            Vector3 positionDelta = ghostCurrentPos - _ghostHipsInitPos;

            // 비율 적용하여 Target Hips 위치 설정
            targetHips.localPosition = _targetHipsInitPos + (positionDelta * _hipsHeightRatio);

            if (showRuntimeDebug && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[Hips] Delta: {positionDelta}, Ratio: {_hipsHeightRatio:F4}, Result: {targetHips.localPosition}");
            }
        }
        #endregion
    }
}
