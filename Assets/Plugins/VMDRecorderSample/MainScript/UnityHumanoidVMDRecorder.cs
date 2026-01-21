using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Serialization;

// ============================================
// [실행 순서 2] VMD 레코딩 메인 엔진
// 역할: FBX 애니메이션 → VMD 파일 변환 핵심 로직
// 실행 시점: Start에서 초기화, FixedUpdate에서 레코딩
// ============================================

//初期ポーズ(T,Aポーズ)の時点でアタッチ、有効化されている必要がある
// [한글] 초기 포즈(T포즈, A포즈) 상태에서 컴포넌트가 부착되고 활성화되어 있어야 함
public class UnityHumanoidVMDRecorder : MonoBehaviour
{
    // === VMD 변환 설정 옵션 ===
    public bool UseParentOfAll = true;                    // "全ての親" 본 사용 여부
    public bool UseCenterAsParentOfAll = true;            // 센터를 "全ての親"으로 사용
    
    /// <summary>
    /// 全ての親の座標・回転を絶対座標系で計算する
    /// UseParentOfAllがTrueでないと意味がない
    /// </summary>
    /// [한글] "全ての親"의 좌표와 회전을 절대 좌표계로 계산 (UseParentOfAll이 true여야 의미 있음)
    public bool UseAbsoluteCoordinateSystem = true;
    
    public bool IgnoreInitialPosition = false;            // 초기 위치 무시 여부
    public bool IgnoreInitialRotation = false;            // 초기 회전 무시 여부
    
    /// <summary>
    /// 一部のモデルではMMD上ではセンターが足元にある
    /// Start前に設定されている必要がある
    /// </summary>
    /// [한글] 일부 모델은 MMD에서 센터가 발 위치에 있음 (Start 전에 설정 필요)
    public bool UseBottomCenter = false;
    
    /// <summary>
    /// Unity上のモーフ名に1.まばたきなど番号が振られている場合、番号を除去する
    /// </summary>
    /// [한글] Unity 모프 이름에 "1.まばたき" 같은 번호가 있으면 제거
    public bool TrimMorphNumber = true;
    
    public int KeyReductionLevel = 3;                     // 키 리덕션 레벨 (파일 크기 감소)
    // === 레코딩 상태 변수 ===
    public bool IsRecording { get; private set; } = false;  // 현재 레코딩 중인지 여부
    public int FrameNumber { get; private set; } = 0;       // 현재 레코딩 중인 프레임 번호
    int frameNumberSaved = 0;                                // 저장된 총 프레임 수
    const float FPSs = 0.03333f;                             // 30 FPS (1/30 = 0.03333초)
    const string CenterNameString = "センター";              // VMD 센터 본 이름
    const string GrooveNameString = "グルーブ";              // VMD 그루브 본 이름

    public Transform ForceLeftToeEnd;
    public Transform ForceRightToeEnd;
    
    public enum BoneNames
    {
        全ての親,
        センター,
        左足ＩＫ,
        右足ＩＫ,
        // Added toe IK bones:
        左つま先ＩＫ,
        右つま先ＩＫ,
        上半身,
        上半身2,
        首,
        頭,
        左肩,
        左腕,
        左ひじ,
        左手首,
        右肩,
        右腕,
        右ひじ,
        右手首,
        左親指１,
        左親指２,
        左人指１,
        左人指２,
        左人指３,
        左中指１,
        左中指２,
        左中指３,
        左薬指１,
        左薬指２,
        左薬指３,
        左小指１,
        左小指２,
        左小指３,
        右親指１,
        右親指２,
        右人指１,
        右人指２,
        右人指３,
        右中指１,
        右中指２,
        右中指３,
        右薬指１,
        右薬指２,
        右薬指３,
        右小指１,
        右小指２,
        右小指３,
        左足,
        右足,
        左ひざ,
        右ひざ,
        左足首,
        右足首,
        None
        // 左つま先, 右つま先は情報付けると足首の回転、位置との矛盾が生じかねない（今回はIKとして記録します）
    }
    // === 본 매핑 데이터 (Unity HumanBodyBones → VMD BoneNames) ===
    //コンストラクタにて初期化
    //全てのボーンを名前で引く辞書
    // [한글] 생성자에서 초기화 - 모든 본을 이름으로 검색하는 딕셔너리
    Dictionary<string, Transform> transformDictionary = new Dictionary<string, Transform>();
    public Dictionary<BoneNames, Transform> BoneDictionary { get; private set; }
    Vector3 parentInitialPosition = Vector3.zero;        // 초기 위치 저장용
    Quaternion parentInitialRotation = Quaternion.identity;  // 초기 회전 저장용
    Dictionary<BoneNames, List<Vector3>> positionDictionary = new Dictionary<BoneNames, List<Vector3>>();
    Dictionary<BoneNames, List<Vector3>> positionDictionarySaved = new Dictionary<BoneNames, List<Vector3>>();
    Dictionary<BoneNames, List<Quaternion>> rotationDictionary = new Dictionary<BoneNames, List<Quaternion>>();
    Dictionary<BoneNames, List<Quaternion>> rotationDictionarySaved = new Dictionary<BoneNames, List<Quaternion>>();
    //ボーン移動量の補正係数
    //この値は大体の値、正確ではない
    // [한글] 본 이동량 보정 계수 (대략적인 값, 정확하지 않음)
    const float DefaultBoneAmplifier = 12.5f;

    public Vector3 ParentOfAllOffset = new Vector3(0, 0, 0);  // "全ての親" 오프셋
    public Vector3 LeftFootIKOffset = Vector3.zero;           // 왼발 IK 오프셋
    public Vector3 RightFootIKOffset = Vector3.zero;          // 오른발 IK 오프셋
    // New: Toe IK Offset
    public Vector3 LeftToeIKOffset = Vector3.zero;            // 왼발끝 IK 오프셋
    public Vector3 RightToeIKOffset = Vector3.zero;           // 오른발끝 IK 오프셋

    Animator animator;             // 애니메이터 참조
    BoneGhost boneGhost;           // 정규화된 본 구조 (가상 스켈레톤)
    MorphRecorder morphRecorder;   // 모프 레코더 (현재 레코딩용)
    MorphRecorder morphRecorderSaved;  // 모프 레코더 (저장용)

    // [Start is called before the first frame update]
    // [한글] [실행 순서 2-1] 첫 프레임 전에 호출 - 본 매핑 및 초기화
    void Start()
    {
        // FPS 설정 (30fps = 0.03333초마다 FixedUpdate 호출)
        Time.fixedDeltaTime = FPSs;
        
        // Animator 컴포넌트 가져오기
        animator = GetComponent<Animator>();
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

        EnforceInitialPose(animator, true);

        SetInitialPositionAndRotation();

        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null) { continue; }

            positionDictionary.Add(boneName, new List<Vector3>());
            rotationDictionary.Add(boneName, new List<Quaternion>());
        }

        // Set offsets for foot IK
        if (BoneDictionary[BoneNames.左足ＩＫ] != null)
        {
            LeftFootIKOffset = Quaternion.Inverse(transform.rotation) * (BoneDictionary[BoneNames.左足ＩＫ].position - transform.position);
        }
        if (BoneDictionary[BoneNames.右足ＩＫ] != null)
        {
            RightFootIKOffset = Quaternion.Inverse(transform.rotation) * (BoneDictionary[BoneNames.右足ＩＫ].position - transform.position);
        }
        // Set offsets for toe IK
        if (BoneDictionary.ContainsKey(BoneNames.左つま先ＩＫ) && BoneDictionary[BoneNames.左つま先ＩＫ] != null)
        {
            LeftToeIKOffset = Quaternion.Inverse(transform.rotation) * (BoneDictionary[BoneNames.左つま先ＩＫ].position - BoneDictionary[BoneNames.左足ＩＫ].position);
        }
        if (BoneDictionary.ContainsKey(BoneNames.右つま先ＩＫ) && BoneDictionary[BoneNames.右つま先ＩＫ] != null)
        {
            RightToeIKOffset = Quaternion.Inverse(transform.rotation) * (BoneDictionary[BoneNames.右つま先ＩＫ].position - BoneDictionary[BoneNames.右足ＩＫ].position);
        }

        boneGhost = new BoneGhost(animator, BoneDictionary, UseBottomCenter);
        morphRecorder = new MorphRecorder(transform);
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


    // [한글] [실행 순서 2-2] 30fps로 매 프레임 실행 - 레코딩 중일 때만 동작
    private void FixedUpdate()
    {
        if (IsRecording)  // 레코딩 중일 때만
        {
            SaveFrame();      // 현재 프레임 데이터 저장
            FrameNumber++;    // 프레임 번호 증가
        }
    }


    // [한글] [실행 순서 2-3] 현재 프레임의 본/IK/모프 데이터 저장
    void SaveFrame()
    {
        // BoneGhost: 정규화된 본 구조 업데이트 (Unity 본 → VMD 본 변환)
        if (boneGhost != null) { boneGhost.GhostAll(); }
        
        // MorphRecorder: 모든 BlendShape(모프) 값 기록
        if (morphRecorder != null) { morphRecorder.RecrodAllMorph(); }

        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null)
            {
                continue;  // 본이 없으면 건너뛔
            }

            // Process foot IK and toe IK together.
            // [한글] 발 IK 처리 (왼발, 오른발)
            if (boneName == BoneNames.右足ＩＫ ||
                boneName == BoneNames.左足ＩＫ )
            {
                Vector3 targetVector = Vector3.zero;
                if (UseCenterAsParentOfAll)
                {
                    if ((!UseAbsoluteCoordinateSystem && transform.parent != null) && IgnoreInitialPosition )
                    {
                        targetVector = Quaternion.Inverse(transform.parent.rotation)
                            * (BoneDictionary[boneName].position - transform.parent.position)
                            - parentInitialPosition;
                    }
                    else if ((!UseAbsoluteCoordinateSystem && transform.parent != null) && !IgnoreInitialPosition)
                    {
                        targetVector = Quaternion.Inverse(transform.parent.rotation)
                            * (BoneDictionary[boneName].position - transform.parent.position);
                    }
                    else if ((UseAbsoluteCoordinateSystem || transform.parent == null) && IgnoreInitialPosition)
                    {
                        targetVector = BoneDictionary[boneName].position - parentInitialPosition;
                    }
                    else if ((UseAbsoluteCoordinateSystem || transform.parent == null) && transform.parent && !IgnoreInitialPosition)
                    {
                        targetVector = BoneDictionary[boneName].position;
                    }
                    else
                    {
                        // Make IK bone get global position data
                        targetVector = BoneDictionary[boneName].position;
                        // Cancel IK bone default position data wrt right and left
                        if (boneName == BoneNames.右足ＩＫ)
                            targetVector -= new Vector3(0.05238038f, 0.115296f, -0.02825557f);
                        else
                            targetVector -= new Vector3(-0.05238038f, 0.115296f, -0.02825557f);
                    }
                }
                else
                {
                    targetVector = BoneDictionary[boneName].position - transform.position;
                    targetVector = Quaternion.Inverse(transform.rotation) * targetVector;
                }
                // Now subtract the appropriate IK offset
                //if (boneName == BoneNames.左足ＩＫ)
                //{
                //    targetVector -= LeftFootIKOffset;
                //}
                //else if (boneName == BoneNames.右足ＩＫ)
                //{
                //    targetVector -= RightFootIKOffset;
                //}


                // Unity 좌표계 → VMD 좌표계 변환 (X축, Z축 반전)
                Vector3 ikPosition = new Vector3(-targetVector.x, targetVector.y, -targetVector.z);
                
                // 스케일 보정 및 저장
                positionDictionary[boneName].Add(ikPosition * DefaultBoneAmplifier);
                
                //回転は全部足首／つま先に持たせる（今回はidentity）
                // [한글] 회전은 모두 발목/발끝에 맡김 (지금은 identity)
                Quaternion ikRotation = Quaternion.identity;
                rotationDictionary[boneName].Add(ikRotation);
                continue;
            }

            // 발끝 IK
            // [한글] 발끝 IK 처리 (왼발끝, 오른발끝)
            if (boneName == BoneNames.左つま先ＩＫ ||
                boneName == BoneNames.右つま先ＩＫ)
            {
                Vector3 targetVector = Vector3.zero;

                if (boneName == BoneNames.左つま先ＩＫ)
                {
                    targetVector = BoneDictionary[boneName].position - BoneDictionary[BoneNames.左足ＩＫ].position;
                    //targetVector = Quaternion.Inverse(transform.rotation) * targetVector;
                    //targetVector -= LeftToeIKOffset;
                    targetVector -= new Vector3(-0.001641536f, -0.07096878f, 0.1238693f);
                }
                else if (boneName == BoneNames.右つま先ＩＫ)
                {
                    targetVector = BoneDictionary[boneName].position - BoneDictionary[BoneNames.右足ＩＫ].position;
                    //targetVector = Quaternion.Inverse(transform.rotation) * targetVector;
                    //targetVector -= RightToeIKOffset;
                    targetVector -= new Vector3(0.001641536f, -0.07096878f, 0.1238693f);
                }
                Vector3 ikPosition = new Vector3(-targetVector.x, targetVector.y, -targetVector.z);
                positionDictionary[boneName].Add(ikPosition * DefaultBoneAmplifier);
                //回転は全部足首／つま先に持たせる（今回はidentity）
                Quaternion ikRotation = Quaternion.identity;
                rotationDictionary[boneName].Add(ikRotation);
                continue;
            }

            if (boneGhost != null && boneGhost.GhostDictionary.Keys.Contains(boneName))
            {
                if (boneGhost.GhostDictionary[boneName].ghost == null || !boneGhost.GhostDictionary[boneName].enabled)
                {
                    rotationDictionary[boneName].Add(Quaternion.identity);
                    positionDictionary[boneName].Add(Vector3.zero);
                    continue;
                }

                Vector3 boneVector = boneGhost.GhostDictionary[boneName].ghost.localPosition;
                Quaternion boneQuatenion = boneGhost.GhostDictionary[boneName].ghost.localRotation;
                rotationDictionary[boneName].Add(new Quaternion(-boneQuatenion.x, boneQuatenion.y, -boneQuatenion.z, boneQuatenion.w));

                boneVector -= boneGhost.GhostOriginalLocalPositionDictionary[boneName];

                positionDictionary[boneName].Add(new Vector3(-boneVector.x, boneVector.y, -boneVector.z) * DefaultBoneAmplifier);
                continue;
            }

            Quaternion fixedQuatenion = Quaternion.identity;
            Quaternion vmdRotation = Quaternion.identity;

            if (boneName == BoneNames.全ての親 && UseAbsoluteCoordinateSystem)
            {
                fixedQuatenion = BoneDictionary[boneName].rotation;
            }
            else
            {
                fixedQuatenion = BoneDictionary[boneName].localRotation;
            }

            if (boneName == BoneNames.全ての親 && IgnoreInitialRotation)
            {
                fixedQuatenion = BoneDictionary[boneName].localRotation.MinusRotation(parentInitialRotation);
            }

            vmdRotation = new Quaternion(-fixedQuatenion.x, fixedQuatenion.y, -fixedQuatenion.z, fixedQuatenion.w);

            rotationDictionary[boneName].Add(vmdRotation);

            Vector3 fixedPosition = Vector3.zero;
            Vector3 vmdPosition = Vector3.zero;

            if (boneName == BoneNames.全ての親 && UseAbsoluteCoordinateSystem)
            {
                fixedPosition = BoneDictionary[boneName].position;
            }
            else
            {
                fixedPosition = BoneDictionary[boneName].localPosition;
            }

            if (boneName == BoneNames.全ての親 && IgnoreInitialPosition)
            {
                fixedPosition -= parentInitialPosition;
            }

            vmdPosition = new Vector3(-fixedPosition.x, fixedPosition.y, -fixedPosition.z);

            if (boneName == BoneNames.全ての親)
            {
                positionDictionary[boneName].Add(vmdPosition * DefaultBoneAmplifier + ParentOfAllOffset);
            }
            else
            {
                positionDictionary[boneName].Add(vmdPosition * DefaultBoneAmplifier);
            }
        }
    }

    // [한글] 초기 위치/회전 저장 (레코딩 시작 시 기준점 설정)
    void SetInitialPositionAndRotation()
    {
        if (UseAbsoluteCoordinateSystem)  // 절대 좌표계 사용 시
        {
            parentInitialPosition = transform.position;       // 글로벌 위치
            parentInitialRotation = transform.rotation;       // 글로벌 회전
        }
        else  // 상대 좌표계 사용 시
        {
            parentInitialPosition = transform.localPosition;  // 로컬 위치
            parentInitialRotation = transform.localRotation;  // 로컬 회전
        }
    }


    // [한글] FPS 설정 (정적 메서드)
    public static void SetFPS(int fps)
    {
        Time.fixedDeltaTime = 1 / (float)fps;  // FixedUpdate 호출 간격 설정
    }


    /// <summary>
    /// レコーディングを開始または再開
    /// </summary>
    /// [한글] 레코딩 시작 또는 재개
    // [한글] [실행 순서 2-4] HumanoidSampleCode에서 호출 - 레코딩 활성화
    public void StartRecording()
    {
        SetInitialPositionAndRotation();  // 현재 위치를 기준점으로 설정
        IsRecording = true;                // 레코딩 플래그 활성화
    }


    /// <summary>
    /// レコーディングを一時停止
    /// </summary>
    /// [한글] 레코딩 일시정지
    public void PauseRecording() { IsRecording = false; }  // 레코딩 플래그만 비활성화


    /// <summary>
    /// レコーディングを終了
    /// </summary>
    /// [한글] 레코딩 종료 (데이터 백업 및 초기화)
    // [한글] [실행 순서 2-5] HumanoidSampleCode에서 호출 - 레코딩 중지 및 데이터 백업
    public void StopRecording()
    {
        IsRecording = false;  // 레코딩 중지
        
        // 현재 레코딩 데이터를 "Saved" 버전으로 백업
        frameNumberSaved = FrameNumber;
        morphRecorderSaved = morphRecorder;
        FrameNumber = 0;
        positionDictionarySaved = positionDictionary;
        positionDictionary = new Dictionary<BoneNames, List<Vector3>>();
        rotationDictionarySaved = rotationDictionary;
        rotationDictionary = new Dictionary<BoneNames, List<Quaternion>>();
        
        // 다음 레코딩을 위해 딕셔너리 초기화
        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null) { continue; }

            positionDictionary.Add(boneName, new List<Vector3>());
            rotationDictionary.Add(boneName, new List<Quaternion>());
        }
        morphRecorder = new MorphRecorder(transform);
    }


    /// <summary>
    /// VMDを作成する
    /// 呼び出す際は先にStopRecordingを呼び出すこと
    /// </summary>
    /// <param name="modelName">VMDファイルに記載される専用モデル名</param>
    /// <param name="filePath">保存先の絶対ファイルパス</param>
    /// [한글] VMD 파일 생성 (호출 전 StopRecording 필수)
    /// [한글] [실행 순서 2-6] HumanoidSampleCode에서 호출 - VMD 파일 생성
    public async void SaveVMD(string modelName, string filePath)
    {
        if (IsRecording)
        {
            Debug.Log(transform.name + "VMD保存前にレコーディングをストップしてください。");
            // [한글] VMD 저장 전에 레코딩을 중지해주세요
            return;
        }

        if (KeyReductionLevel <= 0) { KeyReductionLevel = 1; }  // 최소값 1

        Debug.Log(transform.name + "VMDファイル作成開始");  // [한글] VMD 파일 생성 시작
        await Task.Run(() =>  // 백그라운드 스레드에서 실행 (Unity 메인 스레드 블록킹 방지)
        {
            //ファイルの書き込み  // [한글] 파일 쓰기
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
            {
                try
                {
                    const string ShiftJIS = "shift_jis";  // 일본어 인코딩
                    const int intByteLength = 4;          // int = 4바이트

                    //ファイルタイプの書き込み  // [한글] 파일 타입 쓰기
                    const int fileTypeLength = 30;
                    const string RightFileType = "Vocaloid Motion Data 0002";  // VMD 헤더
                    byte[] fileTypeBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(RightFileType);
                    binaryWriter.Write(fileTypeBytes, 0, fileTypeBytes.Length);
                    binaryWriter.Write(new byte[fileTypeLength - fileTypeBytes.Length], 0, fileTypeLength - fileTypeBytes.Length);


                    //モデル名の書き込み、Shift_JISで保存  // [한글] 모델명 쓰기 (Shift-JIS 저장)
                    const int modelNameLength = 20;
                    byte[] modelNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(modelName);
                    //モデル名が長すぎたとき  // [한글] 모델명이 너무 길 때 (20바이트 제한)
                    modelNameBytes = modelNameBytes.Take(Mathf.Min(modelNameLength, modelNameBytes.Length)).ToArray();
                    binaryWriter.Write(modelNameBytes, 0, modelNameBytes.Length);
                    binaryWriter.Write(new byte[modelNameLength - modelNameBytes.Length], 0, modelNameLength - modelNameBytes.Length);


                    //全ボーンフレーム数の書き込み  // [한글] 전체 본 프레임 개수 쓰기
                    void LoopWithBoneCondition(Action<BoneNames, int> action)
                    {
                        for (int i = 0; i < frameNumberSaved; i++)
                        {
                            if ((i % KeyReductionLevel) != 0) { continue; }  // 키 리덕션 적용

                            foreach (BoneNames boneName in Enum.GetValues(typeof(BoneNames)))
                            {
                                if (!BoneDictionary.Keys.Contains(boneName)) { continue; }
                                if (BoneDictionary[boneName] == null) { continue; }
                                if (!UseParentOfAll && boneName == BoneNames.全ての親) { continue; }

                                action(boneName, i);
                            }
                        }
                    }
                    uint allKeyFrameNumber = 0;
                    LoopWithBoneCondition((a, b) => { allKeyFrameNumber++; });
                    byte[] allKeyFrameNumberByte = BitConverter.GetBytes(allKeyFrameNumber);
                    binaryWriter.Write(allKeyFrameNumberByte, 0, intByteLength);

                    //人ボーンの書き込み
                    LoopWithBoneCondition((boneName, i) =>
                    {
                        const int boneNameLength = 15;
                        string boneNameString = boneName.ToString();
                        if (boneName == BoneNames.全ての親 && UseCenterAsParentOfAll)
                        {
                            boneNameString = CenterNameString;
                        }
                        if (boneName == BoneNames.センター && UseCenterAsParentOfAll)
                        {
                            boneNameString = GrooveNameString;
                        }

                        byte[] boneNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(boneNameString);
                        binaryWriter.Write(boneNameBytes, 0, boneNameBytes.Length);
                        binaryWriter.Write(new byte[boneNameLength - boneNameBytes.Length], 0, boneNameLength - boneNameBytes.Length);

                        byte[] frameNumberByte = BitConverter.GetBytes((ulong)i);
                        binaryWriter.Write(frameNumberByte, 0, intByteLength);

                        Vector3 position = positionDictionarySaved[boneName][i];
                        byte[] positionX = BitConverter.GetBytes(position.x);
                        binaryWriter.Write(positionX, 0, intByteLength);
                        byte[] positionY = BitConverter.GetBytes(position.y);
                        binaryWriter.Write(positionY, 0, intByteLength);
                        byte[] positionZ = BitConverter.GetBytes(position.z);
                        binaryWriter.Write(positionZ, 0, intByteLength);
                        Quaternion rotation = rotationDictionarySaved[boneName][i];
                        byte[] rotationX = BitConverter.GetBytes(rotation.x);
                        binaryWriter.Write(rotationX, 0, intByteLength);
                        byte[] rotationY = BitConverter.GetBytes(rotation.y);
                        binaryWriter.Write(rotationY, 0, intByteLength);
                        byte[] rotationZ = BitConverter.GetBytes(rotation.z);
                        binaryWriter.Write(rotationZ, 0, intByteLength);
                        byte[] rotationW = BitConverter.GetBytes(rotation.w);
                        binaryWriter.Write(rotationW, 0, intByteLength);

                        byte[] interpolateBytes = new byte[64];
                        binaryWriter.Write(interpolateBytes, 0, 64);
                    });

                    //全モーフフレーム数の書き込み
                    morphRecorderSaved.DisableIntron();
                    if (TrimMorphNumber) { morphRecorderSaved.TrimMorphNumber(); }
                    void LoopWithMorphCondition(Action<string, int> action)
                    {
                        for (int i = 0; i < frameNumberSaved; i++)
                        {
                            foreach (string morphName in morphRecorderSaved.MorphDrivers.Keys)
                            {
                                if (morphRecorderSaved.MorphDrivers[morphName].ValueList.Count == 0) { continue; }
                                if (i > morphRecorderSaved.MorphDrivers[morphName].ValueList.Count) { continue; }
                                //変化のない部分は省く
                                if (!morphRecorderSaved.MorphDrivers[morphName].ValueList[i].enabled) { continue; }
                                const int boneNameLength = 15;
                                string morphNameString = morphName.ToString();
                                byte[] morphNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(morphNameString);
                                //名前が長過ぎた場合書き込まない
                                if (boneNameLength - morphNameBytes.Length < 0) { continue; }

                                action(morphName, i);
                            }
                        }
                    }
                    uint allMorphNumber = 0;
                    LoopWithMorphCondition((a, b) => { allMorphNumber++; });
                    byte[] faceFrameCount = BitConverter.GetBytes(allMorphNumber);
                    binaryWriter.Write(faceFrameCount, 0, intByteLength);

                    //モーフの書き込み
                    LoopWithMorphCondition((morphName, i) =>
                    {
                        const int boneNameLength = 15;
                        string morphNameString = morphName.ToString();
                        byte[] morphNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(morphNameString);

                        binaryWriter.Write(morphNameBytes, 0, morphNameBytes.Length);
                        binaryWriter.Write(new byte[boneNameLength - morphNameBytes.Length], 0, boneNameLength - morphNameBytes.Length);

                        byte[] frameNumberByte = BitConverter.GetBytes((ulong)i);
                        binaryWriter.Write(frameNumberByte, 0, intByteLength);

                        byte[] valueByte = BitConverter.GetBytes(morphRecorderSaved.MorphDrivers[morphName].ValueList[i].value);
                        binaryWriter.Write(valueByte, 0, intByteLength);
                    });

                    //カメラの書き込み
                    byte[] cameraFrameCount = BitConverter.GetBytes(0);
                    binaryWriter.Write(cameraFrameCount, 0, intByteLength);

                    //照明の書き込み
                    byte[] lightFrameCount = BitConverter.GetBytes(0);
                    binaryWriter.Write(lightFrameCount, 0, intByteLength);

                    //セルフシャドウの書き込み
                    byte[] selfShadowCount = BitConverter.GetBytes(0);
                    binaryWriter.Write(selfShadowCount, 0, intByteLength);

                    //IKの書き込み
                    //0フレームにキーフレーム一つだけ置く
                    byte[] ikCount = BitConverter.GetBytes(1);
                    byte[] ikFrameNumber = BitConverter.GetBytes(0);
                    byte modelDisplay = Convert.ToByte(1);
                    //右足IKと左足IKと右足つま先IKと左足つま先IKの4つ
                    byte[] ikNumber = BitConverter.GetBytes(4);
                    const int IKNameLength = 20;
                    byte[] leftIKName = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes("左足ＩＫ");
                    byte[] rightIKName = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes("右足ＩＫ");
                    byte[] leftToeIKName = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes("左つま先ＩＫ");
                    byte[] rightToeIKName = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes("右つま先ＩＫ");
                    byte ikOn = Convert.ToByte(1);
                    byte ikOff = Convert.ToByte(0);
                    binaryWriter.Write(ikCount, 0, intByteLength);
                    binaryWriter.Write(ikFrameNumber, 0, intByteLength);
                    binaryWriter.Write(modelDisplay);
                    binaryWriter.Write(ikNumber, 0, intByteLength);
                    binaryWriter.Write(leftIKName, 0, leftIKName.Length);
                    binaryWriter.Write(new byte[IKNameLength - leftIKName.Length], 0, IKNameLength - leftIKName.Length);
                    binaryWriter.Write(ikOn);
                    binaryWriter.Write(leftToeIKName, 0, leftToeIKName.Length);
                    binaryWriter.Write(new byte[IKNameLength - leftToeIKName.Length], 0, IKNameLength - leftToeIKName.Length);
                    binaryWriter.Write(ikOn);
                    binaryWriter.Write(rightIKName, 0, rightIKName.Length);
                    binaryWriter.Write(new byte[IKNameLength - rightIKName.Length], 0, IKNameLength - rightIKName.Length);
                    binaryWriter.Write(ikOn);
                    binaryWriter.Write(rightToeIKName, 0, rightToeIKName.Length);
                    binaryWriter.Write(new byte[IKNameLength - rightToeIKName.Length], 0, IKNameLength - rightToeIKName.Length);
                    binaryWriter.Write(ikOn);
                }
                catch (Exception ex)
                {
                    Debug.Log("VMD書き込みエラー" + ex.Message);
                }
                finally
                {
                    binaryWriter.Close();
                }
            }
        });
        Debug.Log(transform.name + "VMDファイル作成終了");
    }

    /// <summary>
    /// VMDを作成する
    /// 呼び出す際は先にStopRecordingを呼び出すこと
    /// </summary>
    /// <param name="modelName">VMDファイルに記載される専用モデル名</param>
    /// <param name="filePath">保存先の絶対ファイルパス</param>
    /// <param name="keyReductionLevel">キーの書き込み頻度を減らして容量を減らす</param>
    public void SaveVMD(string modelName, string filePath, int keyReductionLevel)
    {
        KeyReductionLevel = keyReductionLevel;
        SaveVMD(modelName, filePath);
    }

    //裏で正規化されたモデル
    //(初期ポーズで各ボーンのlocalRotationがQuaternion.identityのモデル)を疑似的にアニメーションさせる
    class BoneGhost
    {
        public Dictionary<BoneNames, (Transform ghost, bool enabled)> GhostDictionary { get; private set; } = new Dictionary<BoneNames, (Transform ghost, bool enabled)>();
        public Dictionary<BoneNames, Vector3> GhostOriginalLocalPositionDictionary { get; private set; } = new Dictionary<BoneNames, Vector3>();
        public Dictionary<BoneNames, Quaternion> GhostOriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();
        public Dictionary<BoneNames, Quaternion> OriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();

        public bool UseBottomCenter { get; private set; } = false;

        const string GhostSalt = "Ghost";
        private Dictionary<BoneNames, Transform> boneDictionary = new Dictionary<BoneNames, Transform>();
        float centerOffsetLength = 0;

        public BoneGhost(Animator animator, Dictionary<BoneNames, Transform> boneDictionary, bool useBottomCenter)
        {
            this.boneDictionary = boneDictionary;
            UseBottomCenter = useBottomCenter;

            Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)> boneParentDictionary
                = new Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)>()
            {
                { BoneNames.センター, (BoneNames.None, BoneNames.None, BoneNames.全ての親) },
                { BoneNames.左足,     (BoneNames.None, BoneNames.None, BoneNames.センター) },
                { BoneNames.左ひざ,   (BoneNames.None, BoneNames.None, BoneNames.左足) },
                { BoneNames.左足首,   (BoneNames.None, BoneNames.None, BoneNames.左ひざ) },
                { BoneNames.右足,     (BoneNames.None, BoneNames.None, BoneNames.センター) },
                { BoneNames.右ひざ,   (BoneNames.None, BoneNames.None, BoneNames.右足) },
                { BoneNames.右足首,   (BoneNames.None, BoneNames.None, BoneNames.右ひざ) },
                { BoneNames.上半身,   (BoneNames.None, BoneNames.None, BoneNames.センター) },
                { BoneNames.上半身2,  (BoneNames.None, BoneNames.None, BoneNames.上半身) },
                { BoneNames.首,       (BoneNames.上半身2, BoneNames.None, BoneNames.上半身) },
                { BoneNames.頭,       (BoneNames.首, BoneNames.上半身2, BoneNames.上半身) },
                { BoneNames.左肩,     (BoneNames.上半身2, BoneNames.None, BoneNames.上半身) },
                { BoneNames.左腕,     (BoneNames.左肩, BoneNames.上半身2, BoneNames.上半身) },
                { BoneNames.左ひじ,   (BoneNames.None, BoneNames.None, BoneNames.左腕) },
                { BoneNames.左手首,   (BoneNames.None, BoneNames.None, BoneNames.左ひじ) },
                { BoneNames.左親指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
                { BoneNames.左親指２, (BoneNames.左親指１, BoneNames.None, BoneNames.None) },
                { BoneNames.左人指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
                { BoneNames.左人指２, (BoneNames.左人指１, BoneNames.None, BoneNames.None) },
                { BoneNames.左人指３, (BoneNames.左人指２, BoneNames.None, BoneNames.None) },
                { BoneNames.左中指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
                { BoneNames.左中指２, (BoneNames.左中指１, BoneNames.None, BoneNames.None) },
                { BoneNames.左中指３, (BoneNames.左中指２, BoneNames.None, BoneNames.None) },
                { BoneNames.左薬指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
                { BoneNames.左薬指２, (BoneNames.左薬指１, BoneNames.None, BoneNames.None) },
                { BoneNames.左薬指３, (BoneNames.左薬指２, BoneNames.None, BoneNames.None) },
                { BoneNames.左小指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
                { BoneNames.左小指２, (BoneNames.左小指１, BoneNames.None, BoneNames.None) },
                { BoneNames.左小指３, (BoneNames.左小指２, BoneNames.None, BoneNames.None) },
                { BoneNames.右肩,     (BoneNames.上半身2, BoneNames.None, BoneNames.上半身) },
                { BoneNames.右腕,     (BoneNames.右肩, BoneNames.上半身2, BoneNames.上半身) },
                { BoneNames.右ひじ,   (BoneNames.None, BoneNames.None, BoneNames.右腕) },
                { BoneNames.右手首,   (BoneNames.None, BoneNames.None, BoneNames.右ひじ) },
                { BoneNames.右親指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
                { BoneNames.右親指２, (BoneNames.右親指１, BoneNames.None, BoneNames.None) },
                { BoneNames.右人指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
                { BoneNames.右人指２, (BoneNames.右人指１, BoneNames.None, BoneNames.None) },
                { BoneNames.右人指３, (BoneNames.右人指２, BoneNames.None, BoneNames.None) },
                { BoneNames.右中指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
                { BoneNames.右中指２, (BoneNames.右中指１, BoneNames.None, BoneNames.None) },
                { BoneNames.右中指３, (BoneNames.右中指２, BoneNames.None, BoneNames.None) },
                { BoneNames.右薬指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
                { BoneNames.右薬指２, (BoneNames.右薬指１, BoneNames.None, BoneNames.None) },
                { BoneNames.右薬指３, (BoneNames.右薬指２, BoneNames.None, BoneNames.None) },
                { BoneNames.右小指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
                { BoneNames.右小指２, (BoneNames.右小指１, BoneNames.None, BoneNames.None) },
                { BoneNames.右小指３, (BoneNames.右小指２, BoneNames.None, BoneNames.None) },
            };

            // Ghostの生成
            foreach (BoneNames boneName in boneDictionary.Keys)
            {
                // Ignore 全ての親, 足IK, toe IK
                if (boneName == BoneNames.全ての親 ||
                    boneName == BoneNames.左足ＩＫ ||
                    boneName == BoneNames.右足ＩＫ ||
                    boneName == BoneNames.左つま先ＩＫ ||
                    boneName == BoneNames.右つま先ＩＫ)
                {
                    continue;
                }

                if (boneDictionary[boneName] == null)
                {
                    GhostDictionary.Add(boneName, (null, false));
                    continue;
                }

                Transform ghost = new GameObject(boneDictionary[boneName].name + GhostSalt).transform;
                if (boneName == BoneNames.センター && UseBottomCenter)
                {
                    ghost.position = boneDictionary[BoneNames.全ての親].position;
                }
                else
                {
                    ghost.position = boneDictionary[boneName].position;
                }
                ghost.rotation = animator.transform.rotation;
                GhostDictionary.Add(boneName, (ghost, true));
            }

            // Ghostの親子構造を設定
            foreach (BoneNames boneName in boneDictionary.Keys)
            {
                if (boneName == BoneNames.全ての親 ||
                    boneName == BoneNames.左足ＩＫ ||
                    boneName == BoneNames.右足ＩＫ ||
                    boneName == BoneNames.左つま先ＩＫ ||
                    boneName == BoneNames.右つま先ＩＫ)
                {
                    continue;
                }

                if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled)
                {
                    continue;
                }

                if (boneName == BoneNames.センター)
                {
                    GhostDictionary[boneName].ghost.SetParent(animator.transform);
                    continue;
                }

                if (boneParentDictionary[boneName].optionParent1 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent1] != null)
                {
                    GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].optionParent1].ghost);
                }
                else if (boneParentDictionary[boneName].optionParent2 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent2] != null)
                {
                    GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].optionParent2].ghost);
                }
                else if (boneParentDictionary[boneName].necessaryParent != BoneNames.None && boneDictionary[boneParentDictionary[boneName].necessaryParent] != null)
                {
                    GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].necessaryParent].ghost);
                }
                else
                {
                    GhostDictionary[boneName] = (GhostDictionary[boneName].ghost, false);
                }
            }

            // 初期状態を保存
            foreach (BoneNames boneName in GhostDictionary.Keys)
            {
                if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled)
                {
                    GhostOriginalLocalPositionDictionary.Add(boneName, Vector3.zero);
                    GhostOriginalRotationDictionary.Add(boneName, Quaternion.identity);
                    OriginalRotationDictionary.Add(boneName, Quaternion.identity);
                }
                else
                {
                    GhostOriginalRotationDictionary.Add(boneName, GhostDictionary[boneName].ghost.rotation);
                    OriginalRotationDictionary.Add(boneName, boneDictionary[boneName].rotation);
                    if (boneName == BoneNames.センター && UseBottomCenter)
                    {
                        GhostOriginalLocalPositionDictionary.Add(boneName, Vector3.zero);
                        continue;
                    }
                    GhostOriginalLocalPositionDictionary.Add(boneName, GhostDictionary[boneName].ghost.localPosition);
                }
            }

            centerOffsetLength = Vector3.Distance(boneDictionary[BoneNames.全ての親].position, boneDictionary[BoneNames.センター].position);
        }

        public void GhostAll()
        {
            foreach (BoneNames boneName in GhostDictionary.Keys)
            {
                if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled) { continue; }
                Quaternion transQuaternion = boneDictionary[boneName].rotation * Quaternion.Inverse(OriginalRotationDictionary[boneName]);
                GhostDictionary[boneName].ghost.rotation = transQuaternion * GhostOriginalRotationDictionary[boneName];
                if (boneName == BoneNames.センター && UseBottomCenter)
                {
                    GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position - centerOffsetLength * GhostDictionary[boneName].ghost.up;
                    continue;
                }
                GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position;
            }
        }
    }

    class MorphRecorder
    {
        List<SkinnedMeshRenderer> skinnedMeshRendererList;
        //キーはunity上のモーフ名
        public Dictionary<string, MorphDriver> MorphDrivers { get; private set; } = new Dictionary<string, MorphDriver>();

        public MorphRecorder(Transform model)
        {
            List<SkinnedMeshRenderer> searchBlendShapeSkins(Transform t)
            {
                List<SkinnedMeshRenderer> skinnedMeshRendererList = new List<SkinnedMeshRenderer>();
                Queue queue = new Queue();
                queue.Enqueue(t);
                while (queue.Count != 0)
                {
                    SkinnedMeshRenderer skinnedMeshRenderer = (queue.Peek() as Transform).GetComponent<SkinnedMeshRenderer>();

                    if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh.blendShapeCount != 0)
                    {
                        skinnedMeshRendererList.Add(skinnedMeshRenderer);
                    }

                    foreach (Transform childT in (queue.Dequeue() as Transform))
                    {
                        queue.Enqueue(childT);
                    }
                }

                return skinnedMeshRendererList;
            }
            skinnedMeshRendererList = searchBlendShapeSkins(model);

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRendererList)
            {
                int morphCount = skinnedMeshRenderer.sharedMesh.blendShapeCount;
                for (int i = 0; i < morphCount; i++)
                {
                    string morphName = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(i);
                    ////モーフ名に重複があれば2コ目以降は無視
                    if (MorphDrivers.Keys.Contains(morphName)) { continue; }
                    MorphDrivers.Add(morphName, new MorphDriver(skinnedMeshRenderer, i));
                }
            }
        }

        public void RecrodAllMorph()
        {
            foreach (MorphDriver morphDriver in MorphDrivers.Values)
            {
                morphDriver.RecordMorph();
            }
        }

        public void TrimMorphNumber()
        {
            string dot = ".";
            Dictionary<string, MorphDriver> morphDriversTemp = new Dictionary<string, MorphDriver>();
            foreach (string morphName in MorphDrivers.Keys)
            {
                //正規表現使うより、dot探して整数か見る
                if (morphName.Contains(dot) && int.TryParse(morphName.Substring(0, morphName.IndexOf(dot)), out int dummy))
                {
                    morphDriversTemp.Add(morphName.Substring(morphName.IndexOf(dot) + 1), MorphDrivers[morphName]);
                    continue;
                }
                morphDriversTemp.Add(morphName, MorphDrivers[morphName]);
            }
            MorphDrivers = morphDriversTemp;
        }

        public void DisableIntron()
        {
            foreach (string morphName in MorphDrivers.Keys)
            {
                for (int i = 0; i < MorphDrivers[morphName].ValueList.Count; i++)
                {
                    //情報がなければ次へ
                    if (MorphDrivers[morphName].ValueList.Count == 0) { continue; }
                    //今、前、後が同じなら不必要なので無効化
                    if (i > 0
                        && i < MorphDrivers[morphName].ValueList.Count - 1
                        && MorphDrivers[morphName].ValueList[i].value == MorphDrivers[morphName].ValueList[i - 1].value
                        && MorphDrivers[morphName].ValueList[i].value == MorphDrivers[morphName].ValueList[i + 1].value)
                    {
                        MorphDrivers[morphName].ValueList[i] = (MorphDrivers[morphName].ValueList[i].value, false);
                    }
                }
            }
        }

        public class MorphDriver
        {
            const float MorphAmplifier = 0.01f;
            public SkinnedMeshRenderer SkinnedMeshRenderer { get; private set; } = new SkinnedMeshRenderer();
            public int MorphIndex { get; private set; }

            public List<(float value, bool enabled)> ValueList { get; private set; } = new List<(float value, bool enabled)>();

            public MorphDriver(SkinnedMeshRenderer skinnedMeshRenderer, int morphIndex)
            {
                SkinnedMeshRenderer = skinnedMeshRenderer;
                MorphIndex = morphIndex;
            }

            public void RecordMorph()
            {
                ValueList.Add((SkinnedMeshRenderer.GetBlendShapeWeight(MorphIndex) * MorphAmplifier, true));
            }
        }
    }
}
