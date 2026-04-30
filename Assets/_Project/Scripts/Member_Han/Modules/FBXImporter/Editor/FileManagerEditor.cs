using UnityEditor;
using UnityEngine;

namespace Member_Han.Modules.FBXImporter.EditorTools
{
    [CustomEditor(typeof(FileManager))]
    public class FileManagerEditor : UnityEditor.Editor
    {
        private const string EditorPrefsPrefix = "MemberHan.FBXImporter.FileManagerEditor";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawImportSettings();
            DrawRetargetSettings();
            DrawRetargetGuardSettings();
            DrawIdlePoseGuardSettings();
            DrawRecordingSettings();
            DrawFinalTuningSettings();
            DrawHandCorrectionSettings();
            DrawDebugSettings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawImportSettings()
        {
            bool expanded = DrawFoldout("Import", "FBX 임포트");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            DrawProperty("saveToImportFolder", "Import_FBX 폴더에 저장");
            if (GetBool("saveToImportFolder"))
            {
                EditorGUILayout.HelpBox(
                    "Editor에서는 Assets/Resources/Import_FBX에 복사하고, 빌드에서는 persistentDataPath 아래 Import_FBX를 사용합니다.",
                    MessageType.Info);
            }

            EndFoldout(true);
        }

        private void DrawRetargetSettings()
        {
            bool expanded = DrawFoldout("Retarget", "대상 / Ghost Retargeting");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            DrawProperty("targetCharacter", "대상 캐릭터");
            DrawProperty("showGhostModel", "Ghost 모델 표시");
            if (GetBool("showGhostModel"))
            {
                EditorGUILayout.HelpBox("디버그용 표시입니다. 녹화 기준을 확인한 뒤에는 꺼두는 편이 좋습니다.", MessageType.Info);
            }

            DrawProperty("useLegacyPoseSpaceFacingCorrection", "Legacy PoseSpace 방향 보정");
            if (GetBool("useLegacyPoseSpaceFacingCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "이전 수동 프로젝트와 같은 180도 방향 보정입니다. 현재 자동 경로의 카메라 정면 기준을 깨뜨릴 수 있어 비교/롤백용으로만 사용합니다.",
                    MessageType.Warning);
            }

            DrawProperty("preserveFbxRootRotation", "FBX Root 회전 보존");
            if (GetBool("preserveFbxRootRotation"))
            {
                EditorGUILayout.HelpBox("Main_Auto가 Sub_Manual 직접 Animator 재생처럼 FBX body/root yaw를 그대로 따릅니다.", MessageType.Info);
            }

            DrawProperty("useEditorHumanoidClipMuscleReference", "Editor Humanoid Muscle 기준 사용");
            if (GetBool("useEditorHumanoidClipMuscleReference"))
            {
                EditorGUILayout.HelpBox(
                    "Editor 자동 경로에서는 Unity가 FBX에서 임포트한 Humanoid muscle curve를 기준으로 사용합니다. Runtime Assimp Ghost의 회전 curve가 수동 기준과 다르게 해석될 때 팔/상체 포즈 차이를 줄이는 경로입니다.",
                    MessageType.Info);

                EditorGUI.indentLevel++;
                DrawProperty("useManualAnimatorFingerPoseReference", "수동 Animator 손가락 기준 사용");
                if (GetBool("useManualAnimatorFingerPoseReference"))
                {
                    EditorGUILayout.HelpBox(
                        "손가락은 Sub_Manual/testPrefab Animator가 같은 FBX clip을 평가한 HumanPose 값을 기준으로 덮어씁니다. 비워두면 기본 testPrefab과 TestAnimator1_Manual을 자동으로 찾습니다.",
                        MessageType.Info);
                    DrawProperty("useManualAnimatorThumbLocalRotationReference", "엄지 localRotation도 수동 기준 적용");
                    DrawProperty("useManualAnimatorThumbSegmentDirectionReference", "엄지 세그먼트 방향도 수동 기준 적용");
                    if (GetBool("useManualAnimatorThumbSegmentDirectionReference"))
                    {
                        DrawProperty("manualAnimatorThumbSegmentDirectionWeight", "엄지 세그먼트 방향 보정 강도");
                    }
                    DrawProperty("useManualAnimatorThumbHandDirectionReference", "엄지 시작 방향도 수동 기준 적용");
                    if (GetBool("useManualAnimatorThumbHandDirectionReference"))
                    {
                        DrawProperty("manualAnimatorThumbHandDirectionWeight", "엄지 시작 방향 보정 강도");
                    }
                    DrawProperty("useManualAnimatorHandPalmFrameReference", "손바닥 프레임도 수동 기준 적용");
                    if (GetBool("useManualAnimatorHandPalmFrameReference"))
                    {
                        DrawProperty("manualAnimatorHandPalmFrameWeight", "손바닥 프레임 보정 강도");
                    }
                    DrawProperty("useManualAnimatorThumbBasePositionReference", "엄지 시작 위치도 수동 기준 적용");
                    if (GetBool("useManualAnimatorThumbBasePositionReference"))
                    {
                        DrawProperty("manualAnimatorThumbBasePositionWeight", "엄지 시작 위치 보정 강도");
                        DrawProperty("manualAnimatorThumbBasePositionMaxOffset", "엄지 시작 위치 최대 보정");
                    }
                    DrawProperty("manualFingerReferencePrefab", "손가락 기준 프리팹");
                    DrawProperty("manualFingerReferenceController", "손가락 기준 컨트롤러");
                }
                EditorGUI.indentLevel--;
            }

            DrawProperty("disableMmdShoulderPostPoseDuringRetarget", "Retarget 중 MMD 어깨 PPH 끄기");
            if (GetBool("disableMmdShoulderPostPoseDuringRetarget"))
            {
                EditorGUILayout.HelpBox(
                    "Retarget/녹화 중 MMD4Mecanim 어깨 Post Pose 보정을 일시적으로 꺼서 FBX 팔 회전과 중복 적용되는 상황을 줄입니다.",
                    MessageType.Info);
            }

            EndFoldout(true);
        }

        private void DrawRetargetGuardSettings()
        {
            bool expanded = DrawFoldout("RetargetGuard", "Retarget 안전장치");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            DrawProperty("clampRetargetMusclesToHumanRange", "Muscle 기본 범위 제한");
            DrawProperty("lockTargetHumanoidBonePositions", "Humanoid 본 길이 잠금");
            DrawProperty("lockTargetLimbChildLocalPositions", "Limb 보조본 위치 잠금");
            DrawProperty("lockTargetLimbChildLocalRotations", "Limb 보조본 회전 잠금");
            DrawProperty("attachTargetArmDeformationGuard", "Target 팔 변형 가드 자동 부착");
            if (GetBool("attachTargetArmDeformationGuard"))
            {
                EditorGUILayout.HelpBox("Retarget 외의 YYB 직접 재생 경로에도 붙일 수 있는 HumanoidArmDeformationGuard와 같은 규칙을 자동 경로 Target에 적용합니다.", MessageType.Info);
                EditorGUI.indentLevel++;
                DrawProperty("targetGuardClampAnatomicalArmMuscles", "Target 가드 Muscle 제한");
                DrawProperty("targetGuardClampArmStretchMuscles", "Target 가드 Stretch 제한");
                DrawProperty("logArmDeformationGuardCorrections", "가드 진단 로그");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            DrawProperty("enableAnimationRiggingArmTwistCorrection", "Animation Rigging 팔 Twist 보정");
            if (GetBool("enableAnimationRiggingArmTwistCorrection"))
            {
                EditorGUILayout.HelpBox("고스트 리타게팅 결과 위에 YYB 팔 twist 보조본만 보정합니다. 기존 Limb 보조본 회전 잠금은 rig가 제어하는 twist 본을 예외 처리합니다.", MessageType.Info);
                EditorGUI.indentLevel++;
                DrawProperty("AnimationRiggingArmTwistRigWeight", "전체 영향도");
                DrawProperty("AnimationRiggingUpperArmTwistWeight", "상완 Twist 영향도");
                DrawProperty("AnimationRiggingForearmTwistWeight", "전완 Twist 영향도");
                DrawProperty("logAnimationRiggingArmTwistCorrection", "Rigging 진단 로그");
                EditorGUI.indentLevel--;
            }

            DrawProperty("enableYybArmDirectionRetargetCorrection", "YYB 팔 방향 Retarget 보정");
            if (GetBool("enableYybArmDirectionRetargetCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "실험 옵션입니다. 일부 정상 프레임까지 크게 바꿀 수 있어 기본값은 끕니다. 켤 때는 반드시 Sub_Manual/testPrefab 기준과 같은 시간대 스크린샷을 비교하세요.",
                    MessageType.Warning);
                EditorGUI.indentLevel++;
                DrawProperty("YybArmDirectionUpperArmWeight", "상완 영향도");
                DrawProperty("YybArmDirectionForearmWeight", "전완 영향도");
                DrawProperty("YybArmDirectionUpperArmMaxDegrees", "상완 최대 각도");
                DrawProperty("YybArmDirectionForearmMaxDegrees", "전완 최대 각도");
                DrawProperty("logYybArmDirectionRetargetCorrection", "팔 방향 보정 진단 로그");
                EditorGUI.indentLevel--;
            }

            DrawProperty("enableYybArmVisualTwistCorrection", "YYB 팔 시각 Twist 보정");
            if (GetBool("enableYybArmVisualTwistCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "Animation Rigging 없이 전완-손목 회전 변화를 YYB 팔/소매 보조본에 직접 분배합니다. 빨간 박스처럼 소매가 얇게 찌그러지는 시각 변형을 줄이기 위한 보정입니다.",
                    MessageType.Info);
                EditorGUI.indentLevel++;
                DrawProperty("YybArmVisualUpperArmInfluence", "상완 영향도");
                DrawProperty("YybArmVisualForearmInfluence", "전완/손목 영향도");
                DrawProperty("YybArmVisualUpperArmMaxDegrees", "상완 최대 각도");
                DrawProperty("YybArmVisualForearmMaxDegrees", "전완/손목 최대 각도");
                DrawProperty("logYybArmVisualTwistCorrection", "시각 보정 진단 로그");
                EditorGUI.indentLevel--;
            }

            DrawProperty("enableYybArmSwingLimitCorrection", "YYB 상완 하강 제한");
            if (GetBool("enableYybArmSwingLimitCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "손이 완전히 아래로 내려간 자연 포즈는 제외하고, 소매가 어깨 아래로 말려 내려오는 상완 방향만 제한합니다.",
                    MessageType.Info);
                EditorGUI.indentLevel++;
                DrawProperty("YybArmSwingLimitWeight", "보정 강도");
                DrawProperty("YybArmSwingMaxDownDot", "상완 하강 허용치");
                DrawProperty("YybArmSwingMinHandHorizontalRatio", "손 수평 거리 최소값");
                DrawProperty("YybArmSwingMaxHandBelowShoulderRatio", "자연 하강 제외 기준");
                DrawProperty("logYybArmSwingLimitCorrection", "상완 하강 제한 진단 로그");
                EditorGUI.indentLevel--;
            }

            DrawProperty("enableYybArmSleeveAnchorCorrection", "YYB 소매 Anchor 보정");
            if (GetBool("enableYybArmSleeveAnchorCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "상완 회전을 YYB 소매/어깨 캡 보조본에 제한적으로 전달해 어깨 부근 소매가 따로 무너지는 현상을 줄입니다.",
                    MessageType.Info);
                EditorGUI.indentLevel++;
                DrawProperty("YybArmSleeveAnchorInfluence", "소매 상단 영향도");
                DrawProperty("YybArmShoulderCapAnchorInfluence", "어깨 캡 영향도");
                DrawProperty("YybArmSleeveAnchorMaxDegrees", "최대 따라가기 각도");
                DrawProperty("logYybArmSleeveAnchorCorrection", "소매 Anchor 진단 로그");
                EditorGUI.indentLevel--;
            }

            if (GetBool("lockTargetHumanoidBonePositions"))
            {
                EditorGUILayout.HelpBox("SetHumanPose 이후 팔/다리 본 localPosition을 초기값으로 복구해 모델이 길게 늘어나거나 가늘어지는 변형을 막습니다.", MessageType.Info);
            }

            DrawProperty("enableAnatomicalArmGuard", "팔 해부학적 안전장치");
            if (GetBool("enableAnatomicalArmGuard"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("ArmStretchMuscleLimit", "팔 Stretch 허용치");
                DrawProperty("clampRetargetArmStretchMuscles", "Retarget 팔꿈치 Stretch 제한");
                DrawProperty("UpperArmTwistMuscleLimit", "상완 Twist 허용치");
                DrawProperty("LowerArmTwistMuscleLimit", "전완 Twist 허용치");
                EditorGUI.indentLevel--;
            }

            EndFoldout(true);
        }

        private void DrawIdlePoseGuardSettings()
        {
            bool expanded = DrawFoldout("IdlePoseGuard", "대기 자세 보호");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            DrawProperty("faceTargetToCameraOnIdle", "대기 중 카메라 바라보기");
            if (GetBool("faceTargetToCameraOnIdle"))
            {
                EditorGUILayout.HelpBox("Play 진입과 FBX 선택 전 기준 방향을 카메라 정면으로 고정합니다.", MessageType.None);
            }

            DrawProperty("detachTargetAnimatorControllerOnIdle", "대기 중 Animator Controller 분리");
            if (GetBool("detachTargetAnimatorControllerOnIdle"))
            {
                EditorGUILayout.HelpBox("FBX 선택 전 기본 Animator 모션이 재생되어 기준 포즈가 오염되는 것을 막습니다.", MessageType.None);
            }

            DrawProperty("lockTargetPoseUntilImport", "임포트 전 시작 자세 잠금");
            if (GetBool("lockTargetPoseUntilImport"))
            {
                EditorGUILayout.HelpBox("처음 캡처한 타깃 캐릭터 포즈를 FBX 처리 시작 전까지 LateUpdate에서 복구합니다.", MessageType.None);
            }

            EndFoldout(true);
        }

        private void DrawRecordingSettings()
        {
            bool expanded = DrawFoldout("Recording", "녹화");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            DrawProperty("startDelay", "녹화 시작 대기 시간");

            EndFoldout(true);
        }

        private void DrawFinalTuningSettings()
        {
            bool expanded = DrawFoldout("FinalTuning", "최종 튜닝");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            DrawProperty("HeightOffset", "높이 보정");
            DrawProperty("MovementScaleMultiplier", "보폭 비율");
            DrawProperty("clampRetargetRootDeltaSpikes", "Root 순간이동 방지");
            if (GetBool("clampRetargetRootDeltaSpikes"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("MaxRetargetRootDeltaPerFrame", "프레임당 Root 이동 제한");
                DrawProperty("logRetargetRootDeltaSpikes", "Root 튐 진단 로그");
                EditorGUI.indentLevel--;
            }

            EndFoldout(true);
        }

        private void DrawHandCorrectionSettings()
        {
            bool expanded = DrawFoldout("HandCorrection", "손가락 / 엄지 보정");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            EditorGUILayout.LabelField("Golden Hand", EditorStyles.boldLabel);
            DrawProperty("FingerStretchScale", "손가락 굽힘 스케일");
            DrawProperty("FingerSpreadScale", "손가락 벌림 스케일");
            DrawProperty("ThumbStretchScale", "엄지 굽힘 스케일");
            DrawProperty("ThumbSpreadScale", "엄지 벌림 스케일");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("엄지 해부학적 제한", EditorStyles.boldLabel);
            DrawProperty("enableThumbAnatomicalGuard", "엄지 해부학적 안전장치");
            if (GetBool("enableThumbAnatomicalGuard"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("ThumbStretchMin", "엄지 굽힘 최소");
                DrawProperty("ThumbStretchMax", "엄지 굽힘 최대");
                DrawProperty("ThumbSpreadMin", "엄지 벌림 최소");
                DrawProperty("ThumbSpreadMax", "엄지 벌림 최대");
                DrawProperty("preserveManualFingerReferenceThumbMuscles", "Manual 기준 엄지 muscle 보존");
                DrawProperty("logThumbAnatomicalGuardCorrections", "엄지 가드 진단 로그");
                EditorGUI.indentLevel--;
            }

            DrawProperty("enableThumbLocalRotationGuard", "엄지 본 회전 안전장치");
            if (GetBool("enableThumbLocalRotationGuard"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("disableThumbLocalRotationGuardWithManualFingerReference", "Manual 기준 사용 시 localRotation 가드 끄기");
                DrawProperty("ThumbProximalMaxLocalAngle", "엄지 첫 관절 허용각");
                DrawProperty("ThumbIntermediateMaxLocalAngle", "엄지 둘째 관절 허용각");
                DrawProperty("ThumbDistalMaxLocalAngle", "엄지 끝 관절 허용각");
                DrawProperty("logThumbLocalRotationGuardCorrections", "엄지 본 회전 진단 로그");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("엄지 Offset", EditorStyles.boldLabel);
            DrawProperty("ThumbRotationOffset", "엄지 회전 Offset");
            DrawProperty("mirrorRightThumbRotationOffset", "오른손 Offset Mirror");
            DrawProperty("LeftThumbRotationOffset", "왼손 추가 회전 Offset");
            DrawProperty("RightThumbRotationOffset", "오른손 추가 회전 Offset");
            DrawProperty("useDefaultThumbStretchOffsetWhenUnset", "0값이면 기본 굽힘 Offset 사용");
            DrawProperty("ThumbStretchOffset", "엄지 굽힘 Muscle Offset");
            DrawProperty("syncDetachedThumbBaseHelpers", "Thumb0 보조본 회전 동기화");
            if (GetBool("syncDetachedThumbBaseHelpers"))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("YYB는 실제 엄지 구동본과 스킨용 Thumb0 보조본이 분리되어 있어, 위치를 완전히 고정하면 엄지 뿌리가 탈골처럼 보일 수 있습니다. 기본값은 제한 추종입니다.", MessageType.Info);
                DrawProperty("detachedThumbBaseHelperSyncWeight", "Thumb0 보조본 동기화 비율");
                DrawProperty("detachedThumbBaseHelperMaxLocalAngle", "Thumb0 보조본 최대 회전각");
                DrawProperty("syncDetachedThumbBaseHelperPositions", "Thumb0 보조본 위치 동기화");
                EditorGUI.indentLevel--;
            }
            DrawProperty("stabilizeDetachedThumbBasePalm", "손꿈치 Thumb0 안정화");
            if (GetBool("stabilizeDetachedThumbBasePalm"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("detachedThumbBasePalmStabilizeWeight", "손꿈치 안정화 강도");
                DrawProperty("detachedThumbBasePalmMaxLocalAngle", "손꿈치 허용 회전각");
                EditorGUI.indentLevel--;
            }

            DrawProperty("stabilizeThumbWebbingCrease", "엄지 웹빙 라인 안정화");
            if (GetBool("stabilizeThumbWebbingCrease"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("thumbWebbingCreaseStabilizeWeight", "웹빙 안정화 강도");
                DrawProperty("thumbWebbingCreaseMaxLocalAngle", "웹빙 허용 회전각");
                DrawProperty("thumbWebbingCreaseMaxPositionOffset", "웹빙 허용 위치 이동");
                EditorGUI.indentLevel--;
            }

            DrawProperty("enableThumbVisualLengthGuard", "엄지 시각 길이 보정");
            if (GetBool("enableThumbVisualLengthGuard"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("ThumbProjectionMinPalmNormal", "엄지 손바닥 앞쪽 최소 성분");
                DrawProperty("ThumbProjectionMaxPalmNormal", "엄지 손바닥 돌출 허용치");
                DrawProperty("ThumbProjectionGuardWeight", "엄지 돌출 보정 강도");
                DrawProperty("ThumbIndexMaxSpreadAngle", "엄지-검지 최대 벌어짐");
                DrawProperty("ThumbIndexSpreadGuardWeight", "엄지 벌어짐 보정 강도");
                DrawProperty("ThumbMaxSegmentBendAngle", "엄지 마디 최대 굽힘각");
                DrawProperty("ThumbSegmentStraightenWeight", "엄지 마디 펴기 강도");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            DrawProperty("EnableSmartCurve", "손가락 Smart Curve");
            if (GetBool("EnableSmartCurve"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("SmartCurveStrength", "손가락 감쇠 강도");
                DrawProperty("StretchThreshold", "굽힘 임계값");
                EditorGUI.indentLevel--;
            }

            DrawProperty("EnableThumbSmartCurve", "엄지 Smart Curve");
            if (GetBool("EnableThumbSmartCurve"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("ThumbSmartCurveStrength", "엄지 감쇠 강도");
                EditorGUI.indentLevel--;
            }

            EndFoldout(true);
        }

        private void DrawDebugSettings()
        {
            bool expanded = DrawFoldout("Debug", "디버그");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            DrawProperty("showBoneMappingLog", "본 매핑 로그");
            DrawProperty("showRuntimeAnimationLog", "런타임 애니메이션 로그");
            if (GetBool("showBoneMappingLog") || GetBool("showRuntimeAnimationLog"))
            {
                EditorGUILayout.HelpBox("로그가 많아질 수 있으므로 캡처/녹화 QA가 끝나면 끄는 편이 좋습니다.", MessageType.Info);
            }

            EndFoldout(true);
        }

        private bool DrawFoldout(string key, string title)
        {
            EditorGUILayout.Space(4f);

            string editorPrefsKey = $"{EditorPrefsPrefix}.{key}";
            bool expanded = EditorPrefs.GetBool(editorPrefsKey, true);
            bool nextExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
            if (nextExpanded != expanded)
            {
                EditorPrefs.SetBool(editorPrefsKey, nextExpanded);
            }

            if (nextExpanded)
            {
                EditorGUI.indentLevel++;
            }

            return nextExpanded;
        }

        private static void EndFoldout(bool expanded)
        {
            if (expanded)
            {
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"Inspector 필드를 찾을 수 없습니다: {propertyName}", MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }

        private bool GetBool(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
        }
    }
}
