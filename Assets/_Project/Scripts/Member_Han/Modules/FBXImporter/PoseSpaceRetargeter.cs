using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Member_Han.Modules.FBXImporter
{
    public class PoseSpaceRetargeter : MonoBehaviour
    {
        private HumanPoseHandler _srcHandler; 
        private HumanPoseHandler _destHandler; 
        private HumanPose _humanPose;
        private bool _isInitialized = false;
        
        // [수정] 설정값을 가진 매니저 참조
        private FileManager _settings; 

        // 캐싱용 인덱스
        private List<int> _fingerStretchIndices = new List<int>();
        private List<int> _fingerSpreadIndices = new List<int>();
        private List<int> _thumbStretchIndices = new List<int>();
        private List<int> _thumbSpreadIndices = new List<int>();
        
        // [New] Thumb Transforms for Post-Correction
        private Transform _leftThumbProximal;
        private Transform _rightThumbProximal;

        // [수정] Initialize에서 settings(FileManager)를 받음
        public void Initialize(GameObject ghostRoot, GameObject targetRoot, Dictionary<string, string> mappingData, AnimationClip clipToPlay, FileManager settings)
        {
            _settings = settings; // 참조 저장
            CacheFingerIndices();
            StartCoroutine(InitializeRoutine(ghostRoot, targetRoot, mappingData, clipToPlay));
        }

        private void CacheFingerIndices()
        {
            _fingerStretchIndices.Clear();
            _thumbStretchIndices.Clear();
            _fingerSpreadIndices.Clear();
            _thumbSpreadIndices.Clear();
            
            // [New] Cache Thumb Transforms from Target Animator
            if (_destHandler != null) { /* Moved to Initialize where Animator is available */ } 
            
            string[] muscleNames = HumanTrait.MuscleName;
            for (int i = 0; i < muscleNames.Length; i++)
            {
                string mName = muscleNames[i];
                // 손가락 관련만 필터링
                if (!mName.Contains("Left") && !mName.Contains("Right")) continue;
                bool isFinger = mName.Contains("Index") || mName.Contains("Middle") || mName.Contains("Ring") || mName.Contains("Little");
                bool isThumb = mName.Contains("Thumb");

                if (!isFinger && !isThumb) continue;

                if (mName.Contains("Spread"))
                {
                    if (isFinger) _fingerSpreadIndices.Add(i);
                    else if (isThumb) _thumbSpreadIndices.Add(i);
                }
                else if (mName.Contains("Stretched") || mName.Contains("Open"))
                {
                    if (isFinger) _fingerStretchIndices.Add(i); // 네 손가락
                    else if (isThumb) _thumbStretchIndices.Add(i); // 엄지
                }
            }
        }

        private IEnumerator InitializeRoutine(GameObject ghostRoot, GameObject targetRoot, Dictionary<string, string> mappingData, AnimationClip clip)
        {
            Debug.Log("[PoseSpaceRetargeter] ⏳ 초기화 및 정렬 시퀀스 시작...");
            
            var targetAnimator = targetRoot.GetComponent<Animator>();
            if (targetAnimator != null) 
            { 
                 targetAnimator.runtimeAnimatorController = null; 
                 targetAnimator.Rebind(); 
                 targetAnimator.Update(0f);
                 
                 // [New] Find Thumb Transforms
                 _leftThumbProximal = targetAnimator.GetBoneTransform(HumanBodyBones.LeftThumbProximal);
                 _rightThumbProximal = targetAnimator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
                 if (_leftThumbProximal == null) Debug.LogWarning("Left Thumb Proximal not found");
                 if (_rightThumbProximal == null) Debug.LogWarning("Right Thumb Proximal not found");
            }

            var ghostLegacy = ghostRoot.GetComponent<Animation>();
            if (ghostLegacy == null) ghostLegacy = ghostRoot.AddComponent<Animation>();
            ghostLegacy.Stop();

            var ghostAnimator = ghostRoot.GetComponent<Animator>();
            if (ghostAnimator == null) ghostAnimator = ghostRoot.AddComponent<Animator>();

            ghostRoot.transform.rotation = targetRoot.transform.rotation;
            ghostRoot.transform.position = targetRoot.transform.position;

            yield return new WaitForEndOfFrame();

            if (ghostAnimator.avatar == null || !ghostAnimator.avatar.isValid || targetAnimator.avatar == null) yield break;

            _srcHandler = new HumanPoseHandler(ghostAnimator.avatar, ghostRoot.transform);
            _destHandler = new HumanPoseHandler(targetAnimator.avatar, targetRoot.transform);
            _humanPose = new HumanPose();

            _isInitialized = true;
            Debug.Log($"[PoseSpaceRetargeter] ✅ 엔진 가동 완료.");

            if (clip != null)
            {
                clip.legacy = true;
                clip.wrapMode = WrapMode.Loop;
                ghostLegacy.AddClip(clip, clip.name);
                ghostLegacy.clip = clip;
                ghostLegacy.Play(clip.name);
            }
        }

        void LateUpdate()
        {
            if (!_isInitialized || _settings == null) return;

            _srcHandler.GetHumanPose(ref _humanPose);
            
            // -------------------------------------------------------------
            // [FIX] 스마트 핸드 로직 적용
            // -------------------------------------------------------------
            ApplySmartHand(ref _humanPose);

            if (_settings.FaceCamera)
            {
                Quaternion turnAround = Quaternion.Euler(0, 180f, 0);
                _humanPose.bodyRotation = turnAround * _humanPose.bodyRotation;
            }
            
            if (!_settings.ApplyRootMotion)
            {
                _humanPose.bodyPosition = new Vector3(0, _humanPose.bodyPosition.y, 0);
            }

            _destHandler.SetHumanPose(ref _humanPose);
            
            // [FIX] Post-Pose Correction (Digital Orthopedics)
            ApplyThumbPostCorrection();
        }

        private void ApplySmartHand(ref HumanPose pose)
        {
            // Golden Settings Mapping
            float threshold = _settings.StretchThreshold; 
            float dampen = _settings.EnableSmartCurve ? _settings.SmartCurveStrength : 1.0f; // 1.0 means no dampening if formulation is Overflow * Dampen? Wait.
            // If logic is: Threshold + (Overflow * Dampen), then Dampen=1.0 means full overflow pass-through (Linear). Correct.
            // If Enabled=False, we want Linear 1.0. If Enabled=True, we want Dampen (e.g. 0.5).
            
            float thumbDampen = _settings.EnableThumbSmartCurve ? _settings.ThumbSmartCurveStrength : 1.0f;

            // 1. 네 손가락 벌림
            foreach (int idx in _fingerSpreadIndices)
            {
                pose.muscles[idx] *= _settings.FingerSpreadScale; 
            }

            // 2. 네 손가락 굽힘 (Smart Curve)
            foreach (int idx in _fingerStretchIndices)
            {
                float inputVal = pose.muscles[idx];
                // Apply Scale First? Usually Scale is 1.0 in Golden Mode.
                inputVal *= _settings.FingerStretchScale;

                if (inputVal > threshold) 
                {
                    float overflow = inputVal - threshold;
                    pose.muscles[idx] = threshold + (overflow * dampen);
                }
                else
                {
                    pose.muscles[idx] = inputVal;
                }
            }

            // 3. 엄지 (Isolated)
           // Note: Spread is handled mostly by Rotation Offset now, but muscle scale helps dynamics.
            foreach (int idx in _thumbSpreadIndices)
            {
                float val = pose.muscles[idx];
                pose.muscles[idx] = val * _settings.ThumbSpreadScale;
            }

            foreach (int idx in _thumbStretchIndices)
            {
                float val = pose.muscles[idx];
                // Offset (Muscle)
                float offsetVal = val + _settings.ThumbStretchOffset;
                float scaledVal = offsetVal * _settings.ThumbStretchScale;
                
                // Smart Curve for Thumb
                if (scaledVal > threshold)
                {
                    float overflow = scaledVal - threshold;
                    pose.muscles[idx] = threshold + (overflow * thumbDampen);
                }
                else
                {
                     pose.muscles[idx] = scaledVal;
                }
                
                // Clamp Safety
                pose.muscles[idx] = Mathf.Clamp(pose.muscles[idx], -1.0f, 0.8f);
            }
        }

        // [New] Post-Pose Transform Correction
        private void ApplyThumbPostCorrection()
        {
            if (_settings == null) return;
            Vector3 offset = _settings.ThumbRotationOffset;
            
            // Left Thumb
            if (_leftThumbProximal != null)
            {
                // Local Rotation Accumulation
                // X: Flex correction, Y: Abduction correction, Z: Roll
                _leftThumbProximal.localRotation *= Quaternion.Euler(offset);
            }

            // Right Thumb (Mirroring)
            if (_rightThumbProximal != null)
            {
                // Symmetry: usually Y and Z need flipping for opposing hands depending on axis definition.
                // Standard Unity Humanoid: X is flex.
                // Trying symmetric offset:
                Vector3 mirrorOffset = new Vector3(offset.x, -offset.y, -offset.z); 
                _rightThumbProximal.localRotation *= Quaternion.Euler(mirrorOffset);
            }
        }
    }
}
