using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Serialization;

public partial class UnityHumanoidVMDRecorder
{
    // [Start is called before the first frame update]
    // [한글] [실행 순서 2-1] 첫 프레임 전에 호출 - 본 매핑 및 초기화
    void Start()
    {
        if (BoneDictionary != null && animator != null && positionDictionary != null && rotationDictionary != null)
        {
            return;
        }

        // FPS 설정 (30fps = 0.03333초마다 FixedUpdate 호출)
        Time.fixedDeltaTime = FPSs;
        
        // Animator 컴포넌트 가져오기
        animator = GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            Debug.LogError("[UnityHumanoidVMDRecorder] Humanoid Animator가 없어 VMD 녹화 본 매핑을 초기화할 수 없습니다.");
            return;
        }

        var wit = animator.GetBoneTransform(HumanBodyBones.LeftToes);
        
        // Unity HumanBodyBones → VMD BoneNames 매핑 (모든 본 등록)
        BoneDictionary = new Dictionary<BoneNames, Transform>()
            {
                //下半身などというものはUnityにはない
                // [한글] 하반신 같은 개념은 Unity에 없음 (루트 본으로 대체)
                { BoneNames.全ての親, (transform) },                            // 루트 Transform
                { BoneNames.センター, (animator.GetBoneTransform(HumanBodyBones.Hips))},  // 엉덩이(허리)
                { BoneNames.左足ＩＫ, (animator.GetBoneTransform(HumanBodyBones.LeftFoot))},   // 왼발 IK
                { BoneNames.右足ＩＫ, (animator.GetBoneTransform(HumanBodyBones.RightFoot))},  // 오른발 IK
                // Added toe IK bones using LeftToes and RightToes:
                // [한글] 발끝 IK 본 추가 (LeftToes, RightToes 사용)
                { BoneNames.左つま先ＩＫ,  ForceLeftToeEnd != null ? ForceLeftToeEnd : animator.GetBoneTransform(HumanBodyBones.LeftToes) },
                //                { BoneNames.左つま先ＩＫ,  (animator.GetBoneTransform(HumanBodyBones.LeftToes))},
                { BoneNames.右つま先ＩＫ, ForceRightToeEnd != null ? ForceRightToeEnd : animator.GetBoneTransform(HumanBodyBones.RightToes) },
                //                { BoneNames.右つま先ＩＫ, (animator.GetBoneTransform(HumanBodyBones.RightToes))},
                { BoneNames.上半身,   (animator.GetBoneTransform(HumanBodyBones.Spine))},
                { BoneNames.上半身2,  (animator.GetBoneTransform(HumanBodyBones.Chest))},
                { BoneNames.頭,       (animator.GetBoneTransform(HumanBodyBones.Head))},
                { BoneNames.首,       (animator.GetBoneTransform(HumanBodyBones.Neck))},
                { BoneNames.左肩,     (animator.GetBoneTransform(HumanBodyBones.LeftShoulder))},
                { BoneNames.右肩,     (animator.GetBoneTransform(HumanBodyBones.RightShoulder))},
                { BoneNames.左腕,     (animator.GetBoneTransform(HumanBodyBones.LeftUpperArm))},
                { BoneNames.右腕,     (animator.GetBoneTransform(HumanBodyBones.RightUpperArm))},
                { BoneNames.左ひじ,   (animator.GetBoneTransform(HumanBodyBones.LeftLowerArm))},
                { BoneNames.右ひじ,   (animator.GetBoneTransform(HumanBodyBones.RightLowerArm))},
                { BoneNames.左手首,   (animator.GetBoneTransform(HumanBodyBones.LeftHand))},
                { BoneNames.右手首,   (animator.GetBoneTransform(HumanBodyBones.RightHand))},
                { BoneNames.左親指１, (animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal))},
                { BoneNames.右親指１, (animator.GetBoneTransform(HumanBodyBones.RightThumbProximal))},
                { BoneNames.左親指２, (animator.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate))},
                { BoneNames.右親指２, (animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate))},
                { BoneNames.左人指１, (animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal))},
                { BoneNames.右人指１, (animator.GetBoneTransform(HumanBodyBones.RightIndexProximal))},
                { BoneNames.左人指２, (animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate))},
                { BoneNames.右人指２, (animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate))},
                { BoneNames.左人指３, (animator.GetBoneTransform(HumanBodyBones.LeftIndexDistal))},
                { BoneNames.右人指３, (animator.GetBoneTransform(HumanBodyBones.RightIndexDistal))},
                { BoneNames.左中指１, (animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal))},
                { BoneNames.右中指１, (animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal))},
                { BoneNames.左中指２, (animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate))},
                { BoneNames.右中指２, (animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate))},
                { BoneNames.左中指３, (animator.GetBoneTransform(HumanBodyBones.LeftMiddleDistal))},
                { BoneNames.右中指３, (animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal))},
                { BoneNames.左薬指１, (animator.GetBoneTransform(HumanBodyBones.LeftRingProximal))},
                { BoneNames.右薬指１, (animator.GetBoneTransform(HumanBodyBones.RightRingProximal))},
                { BoneNames.左薬指２, (animator.GetBoneTransform(HumanBodyBones.LeftRingIntermediate))},
                { BoneNames.右薬指２, (animator.GetBoneTransform(HumanBodyBones.RightRingIntermediate))},
                { BoneNames.左薬指３, (animator.GetBoneTransform(HumanBodyBones.LeftRingDistal))},
                { BoneNames.右薬指３, (animator.GetBoneTransform(HumanBodyBones.RightRingDistal))},
                { BoneNames.左小指１, (animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal))},
                { BoneNames.右小指１, (animator.GetBoneTransform(HumanBodyBones.RightLittleProximal))},
                { BoneNames.左小指２, (animator.GetBoneTransform(HumanBodyBones.LeftLittleIntermediate))},
                { BoneNames.右小指２, (animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate))},
                { BoneNames.左小指３, (animator.GetBoneTransform(HumanBodyBones.LeftLittleDistal))},
                { BoneNames.右小指３, (animator.GetBoneTransform(HumanBodyBones.RightLittleDistal))},
                { BoneNames.左足,     (animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg))},
                { BoneNames.右足,     (animator.GetBoneTransform(HumanBodyBones.RightUpperLeg))},
                { BoneNames.左ひざ,   (animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg))},
                { BoneNames.右ひざ,   (animator.GetBoneTransform(HumanBodyBones.RightLowerLeg))},
                { BoneNames.左足首,   (animator.GetBoneTransform(HumanBodyBones.LeftFoot))},
                { BoneNames.右足首,   (animator.GetBoneTransform(HumanBodyBones.RightFoot))}
                //左つま先, 右つま先は情報付けると足首の回転、位置との矛盾が生じかねない（今回はIKとして記録します）
            };

        makeTransformDictionary(transform, transformDictionary);

        void makeTransformDictionary(Transform rootBone, Dictionary<string, Transform> dictionary)
        {
            if (dictionary.ContainsKey(rootBone.name)) { return; }
            dictionary.Add(rootBone.name, rootBone);
            foreach (Transform childT in rootBone)
            {
                makeTransformDictionary(childT, dictionary);
            }
        }

        if (ApplyRecorderInitialPose)
        {
            EnforceInitialPose(animator, ApplyRecorderAPose);
        }

        SetInitialPositionAndRotation();

        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null) { continue; }

            positionDictionary.Add(boneName, new List<Vector3>());
            rotationDictionary.Add(boneName, new List<Quaternion>());
        }

        // Set offsets for foot IK
        Quaternion ikReferenceRotation = transform.rotation;
        Vector3 ikReferencePosition = transform.position;
        if (EnableParentFrameIkOffsetCompensationWhenCenterParented &&
            UseCenterAsParentOfAll &&
            !UseAbsoluteCoordinateSystem &&
            transform.parent != null)
        {
            ikReferenceRotation = transform.parent.rotation;
            ikReferencePosition = transform.parent.position;
        }
        if (BoneDictionary[BoneNames.左足ＩＫ] != null)
        {
            LeftFootIKOffset = Quaternion.Inverse(ikReferenceRotation) * (BoneDictionary[BoneNames.左足ＩＫ].position - ikReferencePosition);
        }
        if (BoneDictionary[BoneNames.右足ＩＫ] != null)
        {
            RightFootIKOffset = Quaternion.Inverse(ikReferenceRotation) * (BoneDictionary[BoneNames.右足ＩＫ].position - ikReferencePosition);
        }
        // Set offsets for toe IK
        if (BoneDictionary.ContainsKey(BoneNames.左つま先ＩＫ)
            && BoneDictionary[BoneNames.左つま先ＩＫ] != null
            && BoneDictionary[BoneNames.左足ＩＫ] != null)
        {
            LeftToeIKOffset = Quaternion.Inverse(ikReferenceRotation) * (BoneDictionary[BoneNames.左つま先ＩＫ].position - BoneDictionary[BoneNames.左足ＩＫ].position);
        }
        if (BoneDictionary.ContainsKey(BoneNames.右つま先ＩＫ)
            && BoneDictionary[BoneNames.右つま先ＩＫ] != null
            && BoneDictionary[BoneNames.右足ＩＫ] != null)
        {
            RightToeIKOffset = Quaternion.Inverse(ikReferenceRotation) * (BoneDictionary[BoneNames.右つま先ＩＫ].position - BoneDictionary[BoneNames.右足ＩＫ].position);
        }

        boneGhost = new VmdBoneGhost(animator, BoneDictionary, UseBottomCenter);
        morphRecorder = new VmdMorphRecorder(transform);
    }

    void EnforceInitialPose(Animator animator, bool aPose = false)
    {
        if (animator == null)
        {
            UnityEngine.Debug.Log("EnforceInitialPose");
            UnityEngine.Debug.Log("Animatorがnullです");
            return;
        }

        const int APoseDegree = 30;

        Vector3 position = animator.transform.position;
        Quaternion rotation = animator.transform.rotation;
        animator.transform.position = Vector3.zero;
        animator.transform.rotation = Quaternion.identity;

        int count = animator.avatar.humanDescription.skeleton.Length;
        for (int i = 0; i < count; i++)
        {
            if (!transformDictionary.ContainsKey(animator.avatar.humanDescription.skeleton[i].name))
            {
                continue;
            }

            transformDictionary[animator.avatar.humanDescription.skeleton[i].name].localPosition
                = animator.avatar.humanDescription.skeleton[i].position;
            transformDictionary[animator.avatar.humanDescription.skeleton[i].name].localRotation
                = animator.avatar.humanDescription.skeleton[i].rotation;
        }

        animator.transform.position = position;
        animator.transform.rotation = rotation;

        if (aPose && animator.isHuman)
        {
            Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (leftUpperArm == null || rightUpperArm == null) { return; }
            leftUpperArm.Rotate(animator.transform.forward, APoseDegree, Space.World);
            rightUpperArm.Rotate(animator.transform.forward, -APoseDegree, Space.World);
        }
    }


}
