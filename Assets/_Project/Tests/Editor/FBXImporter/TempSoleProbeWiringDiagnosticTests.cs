using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    // Phase 6 임시 진단 — 실제 모델 경로에서 밑창 프로브 배선과 lift 발화를 실측함.
    // 진단 종료 후 이 파일은 삭제함.
    public class TempSoleProbeWiringDiagnosticTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Report_SoleProbe_Wiring_And_Lift_On_Real_Path()
        {
            const string clipPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            const string modelPath =
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(clipPath);
            if (source == null)
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 진단을 생략함");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath)
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            GameObject model = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            var floor = new GameObject("프로브 배선 진단 바닥") { hideFlags = HideFlags.HideAndDontSave };
            // 캡처와 동일하게 발 아래에 둠 — 발 위의 콜라이더는 지면 레이캐스트가 맞지 않음.
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(40f, 0.1f, 40f);
            object controller = Activator.CreateInstance(typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController", true), true);
            try
            {
                model.hideFlags = HideFlags.HideAndDontSave;
                // 캡처와 동일하게 원점에 둠 — lift 발화 여부가 모델 높이에 의존함.
                model.transform.position = Vector3.zero;
                foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
                    script.enabled = false;
                Animator animator = model.GetComponentInChildren<Animator>(true);
                animator.enabled = true;
                Invoke(controller, "PrepareWithArmDirectionReference", animator, clip, source);
                object grounding = Field(controller, "_footGrounding");
                Debug.Log($"[probe-diag] grounding={(grounding == null ? "null" : "ok")} " +
                    $"IsPrepared={(grounding == null ? "-" : Property(grounding, "IsPrepared"))}");
                if (grounding != null)
                {
                    object[] args = { true, null };
                    bool leftOk = (bool)Invoke(grounding, "TryGetSoleProbeLocalOffsets", args);
                    Debug.Log($"[probe-diag] leftProbe ok={leftOk} count={(leftOk ? ((Vector3[])args[1]).Length : -1)}");
                    args = new object[] { false, null };
                    bool rightOk = (bool)Invoke(grounding, "TryGetSoleProbeLocalOffsets", args);
                    Debug.Log($"[probe-diag] rightProbe ok={rightOk} count={(rightOk ? ((Vector3[])args[1]).Length : -1)}");
                }
                object driver = Field(controller, "_footGoalDriver");
                Debug.Log($"[probe-diag] driver init={Property(driver, "IsInitialized")} " +
                    $"probes L={Property(driver, "DebugLeftSoleProbeCount")} " +
                    $"R={Property(driver, "DebugRightSoleProbeCount")}");
                Invoke(controller, "SetGroundResponseEnabled", true);
                Invoke(controller, "SetIkGoalFootDrive", true, 1.0f);
                // 소스 골반 복원 진단 — goal이 원본 월드 발 위치와 일치하는지 검증.
                object stabilizer = Field(controller, "_footContactStabilizer");
                float srcScale = (float)Property(stabilizer, "SourceHumanScale");
                var srcHips = (System.Collections.Generic.IReadOnlyList<Vector3>)
                    Property(stabilizer, "SourceHipsPositions");
                Debug.Log($"[probe-diag] scale targetHS={animator.humanScale:F4} " +
                    $"sourceHS={srcScale:F4}");
                Transform hipsRef = animator.GetBoneTransform(HumanBodyBones.Hips);
                Invoke(controller, "Seek", 0f);
                float tgtHipsY = hipsRef.position.y;
                Debug.Log($"[probe-diag] hips tgt={tgtHipsY:F4} " +
                    $"src[f451]={(srcHips != null && srcHips.Count > 451 ? srcHips[451].y : float.NaN):F4} " +
                    $"src[f463]={(srcHips != null && srcHips.Count > 463 ? srcHips[463].y : float.NaN):F4} " +
                    $"src[f934]={(srcHips != null && srcHips.Count > 934 ? srcHips[934].y : float.NaN):F4}");
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                Transform rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                bool[] pinL = (bool[])Property(driver, "DebugLeftPinArray");
                bool[] pinR = (bool[])Property(driver, "DebugRightPinArray");
                float[] floorL = (float[])Property(driver, "DebugLeftFloorWorldY");
                float[] floorR = (float[])Property(driver, "DebugRightFloorWorldY");
                Debug.Log($"[probe-diag] floorL[10519]={(floorL != null && floorL.Length > 10519 ? floorL[10519] : float.NaN):F4} " +
                    $"floorR[10519]={(floorR != null && floorR.Length > 10519 ? floorR[10519] : float.NaN):F4} " +
                    $"floorL[5908]={(floorL != null && floorL.Length > 5908 ? floorL[5908] : float.NaN):F4}");
                Debug.Log($"[probe-diag] pinL_total={(pinL == null ? -1 : pinL.Length)} " +
                    $"true={CountTrue(pinL)} pinR_total={(pinR == null ? -1 : pinR.Length)} " +
                    $"true={CountTrue(pinR)}");
                // 재캡처 잔여 미러 구간(좌측 f≈3079-3119) 앞부터 순차 Seek해
                // 단계별 무릎 부호를 실측함 — 크기 동일·부호 반전 패턴 재현 여부.
                int pinStart = -1;
                if (pinL != null)
                    for (int i = Mathf.Min(3070, pinL.Length - 1); i >= 0; i--)
                        if (pinL[i]) { pinStart = i; break; }
                Debug.Log($"[probe-diag] pinL_last_before_3070={pinStart} " +
                    $"pinL[3075..3125]={(pinL == null ? "-" : string.Join("", Enumerable.Range(3075, 51).Select(i => i < pinL.Length && pinL[i] ? "1" : "0")))}");
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var srcSamples = (System.Collections.Generic.IReadOnlyList<HumanoidFootContactSample>)
                    Property(stabilizer, "SourceSamples");
                // 목표 복원 검증 — 타겟 골반 복원 시 발이 +20cm 떴던 구간을 소스 골반
                // 복원으로 바꾼 뒤 실측 발이 소스 궤적에 맞는지 직접 대비함.
                int[][] spans =
                {
                    new[] { 430, 470 },
                    new[] { 920, 950 },
                    new[] { 3070, 3125 },
                };
                foreach (int[] span in spans)
                for (int frame = span[0]; frame <= span[1]; frame++)
                {
                    Fbx2Vmd.FBXImporter.HumanoidGroundResponse.DebugSolveCount = 0;
                    Invoke(controller, "Seek", frame / clip.frameRate);
                    object gate = Property(controller, "LastGroundingGate");
                    float wL_val = (float)Property(driver, "DebugLeftWeight");
                    Vector3 thighDir = (leftKnee.position - leftUpper.position).normalized;
                    Vector3 shinDir = (leftFoot.position - leftKnee.position).normalized;
                    float kneeFlex = Vector3.SignedAngle(thighDir, shinDir, hips.right);
                    Vector3 preU = (Vector3)Property(driver, "DebugLeftPreUpper");
                    Vector3 preL = (Vector3)Property(driver, "DebugLeftPreLower");
                    Vector3 preF = (Vector3)Property(driver, "DebugLeftPreFoot");
                    float preKnee = Vector3.SignedAngle(
                        (preL - preU).normalized, (preF - preL).normalized, hips.right);
                    Vector3 pbL = (Vector3)Property(driver, "DebugLeftPostBlendLower");
                    Vector3 pbF = (Vector3)Property(driver, "DebugLeftPostBlendFoot");
                    float postBlendKnee = wL_val > 0f
                        ? Vector3.SignedAngle(
                            (pbL - preU).normalized, (pbF - pbL).normalized, hips.right)
                        : 0f;
                    Debug.Log($"[probe-diag] f={frame} " +
                        $"kL={kneeFlex:F1} " +
                        $"preK={preKnee:F1} " +
                        $"pbK={postBlendKnee:F1} " +
                        $"stage={Field(gate, "evaluation_stage")} " +
                        $"wL={wL_val:F3} " +
                        $"pinL={(pinL != null && frame < pinL.Length && pinL[frame])} " +
                        $"nL={(Vector3)Property(driver, "LastUsedLeftBendNormal"):F3} " +
                        $"refW={(float)Property(driver, "DebugLeftReferenceWeight"):F2} " +
                        $"refL={(Vector3)Property(driver, "DebugLeftProjectedReference"):F3} " +
                        $"dkL={(Vector3)Property(driver, "DebugLeftDesiredKnee"):F3} " +
                        $"terrL={(float)Property(driver, "DebugLeftTargetError"):F4} " +
                        $"grN={Fbx2Vmd.FBXImporter.HumanoidGroundResponse.DebugSolveCount} " +
                        $"footY={leftFoot.position.y:F3} " +
                        $"srcY={(srcSamples != null && frame < srcSamples.Count ? srcSamples[frame].LeftFoot.y : float.NaN):F3} " +
                        $"goalY={(Vector3)Property(driver, "DebugLeftSmoothedGoal"):F3}");
                }
            }
            finally
            {
                ((IDisposable)controller).Dispose();
                UnityEngine.Object.DestroyImmediate(model);
                UnityEngine.Object.DestroyImmediate(floor);
            }
        }

        private static int CountTrue(bool[] values)
        {
            if (values == null) return -1;
            int count = 0;
            foreach (bool v in values) if (v) count++;
            return count;
        }

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Flags).Invoke(target, args);

        private static object Property(object target, string name) =>
            target.GetType().GetProperty(name, Flags).GetValue(target);

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Flags).GetValue(target);
    }
}
