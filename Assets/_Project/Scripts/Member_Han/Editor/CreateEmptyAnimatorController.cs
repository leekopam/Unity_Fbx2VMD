using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace Member_Han.Modules.FBXImporter.Editor
{
    /// <summary>
    /// EmptyAnimatorController와 EmptyClip을 자동으로 생성하는 Editor 스크립트
    /// Ghost Retargeting 시스템에서 사용하는 기본 AnimatorController를 생성합니다.
    /// </summary>
    public static class CreateEmptyAnimatorController
    {
        private const string RESOURCES_PATH = "Assets/Resources";
        private const string CONTROLLER_PATH = "Assets/Resources/EmptyAnimatorController.controller";
        private const string CLIP_PATH = "Assets/Resources/EmptyClip.anim";
        
        [MenuItem("Tools/Ghost Retargeting/Create Empty Animator Controller")]
        public static void CreateController()
        {
            // 1. Resources 폴더 확인 및 생성
            if (!AssetDatabase.IsValidFolder(RESOURCES_PATH))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
                Debug.Log("[CreateEmptyAnimatorController] Resources 폴더 생성됨");
            }
            
            // 2. 기존 파일 확인
            bool controllerExists = File.Exists(CONTROLLER_PATH);
            bool clipExists = File.Exists(CLIP_PATH);
            
            if (controllerExists && clipExists)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "EmptyAnimatorController 생성",
                    "EmptyAnimatorController와 EmptyClip이 이미 존재합니다.\n덮어쓰시겠습니까?",
                    "덮어쓰기",
                    "취소"
                );
                
                if (!overwrite)
                {
                    Debug.Log("[CreateEmptyAnimatorController] 사용자가 취소했습니다.");
                    return;
                }
                
                // 기존 파일 삭제
                AssetDatabase.DeleteAsset(CONTROLLER_PATH);
                AssetDatabase.DeleteAsset(CLIP_PATH);
            }
            
            // 3. EmptyClip 생성
            AnimationClip emptyClip = new AnimationClip();
            emptyClip.name = "EmptyClip";
            emptyClip.wrapMode = WrapMode.Once;
            emptyClip.legacy = false;
            
            AssetDatabase.CreateAsset(emptyClip, CLIP_PATH);
            Debug.Log($"[CreateEmptyAnimatorController] ✅ EmptyClip 생성: {CLIP_PATH}");
            
            // 4. AnimatorController 생성
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
            
            // 5. Base Layer 가져오기
            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorStateMachine stateMachine = layer.stateMachine;
            
            // 6. EmptyState 생성
            AnimatorState emptyState = stateMachine.AddState("EmptyState");
            emptyState.motion = emptyClip;
            
            // 7. Default State로 설정
            stateMachine.defaultState = emptyState;
            
            // 8. 저장
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("[CreateEmptyAnimatorController] ========================================");
            Debug.Log("[CreateEmptyAnimatorController] ✅ EmptyAnimatorController 생성 완료!");
            Debug.Log($"[CreateEmptyAnimatorController]    경로: {CONTROLLER_PATH}");
            Debug.Log("[CreateEmptyAnimatorController]    State: EmptyState (Default)");
            Debug.Log($"[CreateEmptyAnimatorController]    Motion: EmptyClip");
            Debug.Log("[CreateEmptyAnimatorController] ========================================");
            
            // 9. Animator Window 자동 열기
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = controller;
            EditorApplication.ExecuteMenuItem("Window/Animation/Animator");
        }
        
        [MenuItem("Tools/Ghost Retargeting/Verify Empty Animator Controller")]
        public static void VerifyController()
        {
            // Resources에서 로드 시도
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("EmptyAnimatorController");
            
            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "검증 실패",
                    "EmptyAnimatorController를 찾을 수 없습니다!\n\n'Tools > Ghost Retargeting > Create Empty Animator Controller'를 실행하세요.",
                    "확인"
                );
                Debug.LogError("[VerifyController] ❌ EmptyAnimatorController를 찾을 수 없습니다!");
                return;
            }
            
            // AnimatorController로 캐스팅
            AnimatorController animController = controller as AnimatorController;
            if (animController == null)
            {
                EditorUtility.DisplayDialog(
                    "검증 실패",
                    "로드된 Controller가 AnimatorController가 아닙니다.",
                    "확인"
                );
                Debug.LogError("[VerifyController] ❌ AnimatorController 타입 오류!");
                return;
            }
            
            // State 확인
            AnimatorStateMachine stateMachine = animController.layers[0].stateMachine;
            bool hasEmptyState = false;
            
            foreach (var state in stateMachine.states)
            {
                if (state.state.name == "EmptyState")
                {
                    hasEmptyState = true;
                    
                    // Motion 확인
                    if (state.state.motion == null)
                    {
                        EditorUtility.DisplayDialog(
                            "검증 경고",
                            "EmptyState가 있지만 Motion이 할당되지 않았습니다.\n\nEmptyClip을 할당해야 합니다.",
                            "확인"
                        );
                        Debug.LogWarning("[VerifyController] ⚠️ EmptyState의 Motion이 null입니다!");
                        return;
                    }
                    
                    if (state.state.motion.name != "EmptyClip")
                    {
                        EditorUtility.DisplayDialog(
                            "검증 경고",
                            $"EmptyState의 Motion이 EmptyClip이 아닙니다.\n현재 Motion: {state.state.motion.name}",
                            "확인"
                        );
                        Debug.LogWarning($"[VerifyController] ⚠️ Motion 이름 불일치: {state.state.motion.name}");
                        return;
                    }
                    
                    // Default State 확인
                    if (stateMachine.defaultState != state.state)
                    {
                        EditorUtility.DisplayDialog(
                            "검증 경고",
                            "EmptyState가 Default State로 설정되지 않았습니다.",
                            "확인"
                        );
                        Debug.LogWarning("[VerifyController] ⚠️ EmptyState가 Default State가 아닙니다!");
                        return;
                    }
                    
                    break;
                }
            }
            
            if (!hasEmptyState)
            {
                EditorUtility.DisplayDialog(
                    "검증 실패",
                    "EmptyState를 찾을 수 없습니다!\n\n'Tools > Ghost Retargeting > Create Empty Animator Controller'를 실행하세요.",
                    "확인"
                );
                Debug.LogError("[VerifyController] ❌ EmptyState를 찾을 수 없습니다!");
                return;
            }
            
            // 모든 검증 통과
            EditorUtility.DisplayDialog(
                "검증 성공",
                "EmptyAnimatorController가 올바르게 설정되었습니다!\n\n✅ EmptyState 존재\n✅ Motion: EmptyClip\n✅ Default State 설정됨",
                "확인"
            );
            
            Debug.Log("[VerifyController] ========================================");
            Debug.Log("[VerifyController] ✅ EmptyAnimatorController 검증 성공!");
            Debug.Log("[VerifyController] ========================================");
        }
    }
}
