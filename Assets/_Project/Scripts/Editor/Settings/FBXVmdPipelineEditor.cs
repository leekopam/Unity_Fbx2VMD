using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Fbx2Vmd.Settings;
using Fbx2Vmd.Settings.EditorTools;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Fbx2Vmd.FBXImporter
{
    [CustomEditor(typeof(FBXVmdPipeline))]
    public class FBXVmdPipelineEditor : UnityEditor.Editor
    {
        private const string EditorPrefsPrefix = "MemberHan.FBXImporter.FBXVmdPipelineEditor";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawRequiredSettings();
            DrawImportSettings();
            DrawRetargetSettings();
            DrawRetargetGuardSettings();
            DrawIdlePoseGuardSettings();
            DrawRecordingSettings();
            DrawFinalTuningSettings();
            DrawHandCorrectionSettings();
            DrawExternalMmdAutomationSettings();
            DrawDebugSettings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRequiredSettings()
        {
            bool expanded = DrawFoldout("Required", "필수 설정");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_targetCharacter", "대상 캐릭터", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_shouldRecordVmdAfterImport", "FBX 임포트 후 VMD 자동 녹화", showWarningOnNull: true);
            DrawRequiredSettingsValidation();

            EndFoldout(true);
        }

        private void DrawRequiredSettingsValidation()
        {
            if (target is FBXVmdPipeline pipeline && !pipeline.gameObject.activeInHierarchy)
            {
                EditorGUILayout.HelpBox(
                    "FBXVmdPipeline GameObject가 비활성 상태입니다. FBX 임포트와 녹화를 사용하려면 활성화해야 합니다.",
                    MessageType.Error);
            }

            SerializedProperty targetProperty = serializedObject.FindProperty("_targetCharacter");
            if (targetProperty?.objectReferenceValue is not GameObject targetCharacter)
            {
                EditorGUILayout.HelpBox("대상 캐릭터를 연결해야 FBX 리타게팅과 녹화를 실행할 수 있습니다.", MessageType.Error);
                return;
            }

            Animator animator = targetCharacter.GetComponent<Animator>();
            if (animator == null)
            {
                EditorGUILayout.HelpBox("대상 캐릭터 루트에 Animator가 필요합니다.", MessageType.Error);
                return;
            }

            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                EditorGUILayout.HelpBox("대상 캐릭터에 유효한 Humanoid Avatar를 연결해야 합니다.", MessageType.Error);
            }
        }

        private const string ExternalMmdPrefsPrefix = EditorPrefsPrefix + ".ExternalMMD";

        private static Process mmdAutomationProcess;
        private static readonly StringBuilder mmdAutomationStdout = new StringBuilder(16_384);
        private static readonly StringBuilder mmdAutomationStderr = new StringBuilder(16_384);
        private static DateTime mmdAutomationStartedAt;
        private static string mmdAutomationLastResultJson;

        private static string PrefKey(string suffix) => $"{ExternalMmdPrefsPrefix}.{suffix}";

        private static string GetPrefString(string suffix, string fallback = "")
        {
            return EditorPrefs.GetString(PrefKey(suffix), fallback) ?? fallback;
        }

        private static void SetPrefString(string suffix, string value)
        {
            EditorPrefs.SetString(PrefKey(suffix), value ?? string.Empty);
        }

        private static string DrawPathField(
            string label,
            string value,
            string browseTitle,
            string extensionFilter,
            bool saveFile = false,
            string defaultDirectory = "")
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string directory = "";
                try
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        directory = Directory.Exists(value) ? value : Path.GetDirectoryName(value) ?? "";
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FBXVmdPipelineEditor] 디렉터리 확인 실패: {ex.Message}");
                    directory = "";
                }

                if (string.IsNullOrEmpty(directory))
                {
                    directory = defaultDirectory ?? "";
                }

                string next = saveFile
                    ? EditorUtility.SaveFilePanel(browseTitle, directory, Path.GetFileName(value), extensionFilter)
                    : EditorUtility.OpenFilePanel(browseTitle, directory, extensionFilter);

                if (!string.IsNullOrEmpty(next))
                {
                    value = next;
                }
            }
            EditorGUILayout.EndHorizontal();
            return value;
        }

        private static string DrawFolderField(string label, string value, string browseTitle)
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string directory = "";
                try
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        directory = Directory.Exists(value) ? value : Path.GetDirectoryName(value) ?? "";
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FBXVmdPipelineEditor] 디렉터리 확인 실패: {ex.Message}");
                    directory = "";
                }

                string next = EditorUtility.OpenFolderPanel(browseTitle, directory, "");
                if (!string.IsNullOrEmpty(next))
                {
                    value = next;
                }
            }
            EditorGUILayout.EndHorizontal();
            return value;
        }

        private static string ResolvePythonExecutable(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            string pyLauncher = @"py.exe";
            if (File.Exists(pyLauncher))
            {
                return pyLauncher;
            }

            return "py";
        }

        private static string ResolveProjectRoot()
        {
            // Assets/.. => Unity project root
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private static string ResolveDefaultMotionVmdBrowseFolder(string projectPmm)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(projectPmm) && File.Exists(projectPmm))
                {
                    string directory = Path.GetDirectoryName(projectPmm) ?? "";
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        return directory;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FBXVmdPipelineEditor] 프로젝트 PMM에서 디렉터리를 확인하지 못했습니다: {ex.Message}");
            }

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) ?? "";
                if (!string.IsNullOrWhiteSpace(desktop))
                {
                    string candidate = Path.Combine(desktop, "MMD", "MikuMikuDance_v932x64", "SaveFile");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    return desktop;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FBXVmdPipelineEditor] MMD SaveFile 디렉터리를 확인하지 못했습니다: {ex.Message}");
            }

            return "";
        }

        private static void AppendProcessLine(StringBuilder buffer, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            const int maxChars = 32_000;
            if (buffer.Length > maxChars)
            {
                buffer.Remove(0, Math.Max(0, buffer.Length - maxChars));
            }

            buffer.AppendLine(line);
        }

        private void DrawExternalMmdAutomationSettings()
        {
            bool expanded = DrawFoldout("ExternalMMD", "External MMD (Experimental)");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            EditorGUILayout.HelpBox(
                "A-version (PMM only): File(F)->Open(O) PMM, File(F)->Load Motion(M) VMD, File(F)->Render AVI(V).\n" +
                "Direct .pmx/.pmd loading is not supported yet. For stability, prefer using a pre-made .pmm that already contains the target model.\n" +
                "If the automation keeps targeting Null_* / camera-light-accessory, set 'Target model name contains' to match the model name shown in MMD.",
                MessageType.Info);

            string mmdExeOverride = GetPrefString("mmdExeOverride", "");
            string pythonExeOverride = GetPrefString("pythonExeOverride", "");
            string projectPmm = GetPrefString("projectPmm", "");
            string motionVmd = GetPrefString("motionVmd", "");
            string motionVmdBrowseFolder = GetPrefString("motionVmdBrowseFolder", ResolveDefaultMotionVmdBrowseFolder(projectPmm));
            string outputAvi = GetPrefString("outputAvi", "");
            string windowTitle = GetPrefString("windowTitle", "MikuMikuDance");
            int selectModelTabs = EditorPrefs.GetInt(PrefKey("selectModelTabs"), 1);
            string targetModelNameContains = GetPrefString("targetModelNameContains", "");
            int targetModelIndex = EditorPrefs.GetInt(PrefKey("targetModelIndex"), -1);
            bool skipRenderAvi = EditorPrefs.GetBool(PrefKey("skipRenderAvi"), false);
            bool playAfterLoad = EditorPrefs.GetBool(PrefKey("playAfterLoad"), false);
            float playSeconds = EditorPrefs.GetFloat(PrefKey("playSeconds"), 2f);

            mmdExeOverride = DrawPathField("MMD exe (override)", mmdExeOverride, "Select MikuMikuDance.exe", "exe");
            pythonExeOverride = DrawPathField("Python exe (override)", pythonExeOverride, "Select python/py.exe", "exe");
            projectPmm = DrawPathField("Project (.pmm)", projectPmm, "Select .pmm project", "pmm");
            motionVmdBrowseFolder = DrawFolderField("VMD folder (for browse)", motionVmdBrowseFolder, "Select folder that contains .vmd files");
            motionVmd = DrawPathField("Motion (.vmd)", motionVmd, "Select .vmd motion", "vmd", defaultDirectory: motionVmdBrowseFolder);
            outputAvi = DrawPathField("Output (.avi)", outputAvi, "Select output .avi path", "avi", saveFile: true);
            windowTitle = EditorGUILayout.TextField("Window title contains", windowTitle);
            selectModelTabs = EditorGUILayout.IntSlider("Select model TAB presses", selectModelTabs, 0, 8);
            targetModelNameContains = EditorGUILayout.TextField("Target model name contains", targetModelNameContains);
            targetModelIndex = EditorGUILayout.IntField("Target model index (fallback, 0-based)", targetModelIndex);
            skipRenderAvi = EditorGUILayout.ToggleLeft("Skip render AVI (load motion only)", skipRenderAvi);
            playAfterLoad = EditorGUILayout.ToggleLeft("Play after load (Space)", playAfterLoad);
            if (playAfterLoad)
            {
                playSeconds = EditorGUILayout.Slider("Play seconds", playSeconds, 0f, 15f);
            }

            SetPrefString("mmdExeOverride", mmdExeOverride);
            SetPrefString("pythonExeOverride", pythonExeOverride);
            SetPrefString("projectPmm", projectPmm);
            SetPrefString("motionVmd", motionVmd);
            SetPrefString("motionVmdBrowseFolder", motionVmdBrowseFolder);
            SetPrefString("outputAvi", outputAvi);
            SetPrefString("windowTitle", windowTitle);
            EditorPrefs.SetInt(PrefKey("selectModelTabs"), selectModelTabs);
            SetPrefString("targetModelNameContains", targetModelNameContains);
            EditorPrefs.SetInt(PrefKey("targetModelIndex"), targetModelIndex);
            EditorPrefs.SetBool(PrefKey("skipRenderAvi"), skipRenderAvi);
            EditorPrefs.SetBool(PrefKey("playAfterLoad"), playAfterLoad);
            EditorPrefs.SetFloat(PrefKey("playSeconds"), playSeconds);

            EditorGUILayout.Space(4f);

            bool isRunning = mmdAutomationProcess != null && !mmdAutomationProcess.HasExited;
            using (new EditorGUI.DisabledScope(isRunning))
            {
                if (GUILayout.Button("Run Full (A)"))
                {
                    StartMmdAutomationProcess(
                        pythonExeOverride,
                        mmdExeOverride,
                        projectPmm,
                        motionVmd,
                        outputAvi,
                        windowTitle,
                        selectModelTabs,
                        targetModelNameContains,
                        targetModelIndex,
                        skipRenderAvi,
                        playAfterLoad,
                        playSeconds);
                 }
              }

            using (new EditorGUI.DisabledScope(!isRunning))
            {
                if (GUILayout.Button("Stop Running"))
                {
                    try
                    {
                        mmdAutomationProcess?.Kill();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"프로세스 중지 실패: {ex.Message}");
                    }
                }
            }

            DrawMmdAutomationStatus();
            EndFoldout(true);
        }

        private static void StartMmdAutomationProcess(
            string pythonExeOverride,
            string mmdExeOverride,
            string projectPmm,
             string motionVmd,
              string outputAvi,
              string windowTitle,
            int selectModelTabs,
            string targetModelNameContains,
            int targetModelIndex,
            bool skipRenderAvi,
            bool playAfterLoad,
            float playSeconds)
          {
            if (string.IsNullOrEmpty(projectPmm) || string.IsNullOrEmpty(motionVmd))
            {
                Debug.LogError("External MMD: Project(.pmm)와 Motion(.vmd)이 필요합니다.");
                return;
            }

            if (!skipRenderAvi && string.IsNullOrEmpty(outputAvi))
            {
                Debug.LogError("External MMD: Skip render AVI가 활성화되지 않은 경우 Output(.avi)이 필요합니다.");
                return;
            }

            string projectRoot = ResolveProjectRoot();
            string cliScript = Path.Combine(
                projectRoot,
                "Docs",
                "Workflow",
                "Tools",
                "Local",
                "mmd_qa_mcp",
                "mmd_qa_automation_cli.py");

            if (!File.Exists(cliScript))
            {
                Debug.LogError($"External MMD: CLI 스크립트를 찾을 수 없습니다: {cliScript}");
                return;
            }

            string pythonExe = ResolvePythonExecutable(pythonExeOverride);
             string args =
                  $"\"{cliScript}\" " +
                  $"--pmm \"{projectPmm}\" " +
                  $"--vmd \"{motionVmd}\" " +
                  $"--window-title \"{windowTitle}\" " +
                 $"--select-model-tabs {Mathf.Clamp(selectModelTabs, 0, 32)}";

             if (!string.IsNullOrEmpty(outputAvi))
             {
                 args += $" --avi \"{outputAvi}\"";
             }

             if (skipRenderAvi)
             {
                 args += " --skip-render";
             }

             if (playAfterLoad)
             {
                 args += $" --play-after-load --play-seconds {Mathf.Clamp(playSeconds, 0f, 60f):0.###}";
             }

            if (!string.IsNullOrWhiteSpace(targetModelNameContains))
            {
                args += $" --select-model-name \"{targetModelNameContains}\"";
            }

            if (targetModelIndex >= 0)
            {
                args += $" --select-model-index {targetModelIndex}";
            }

             if (!string.IsNullOrEmpty(mmdExeOverride))
             {
                 args += $" --mmd-exe \"{mmdExeOverride}\"";
             }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = args,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (_, e) => AppendProcessLine(mmdAutomationStdout, e.Data);
            process.ErrorDataReceived += (_, e) => AppendProcessLine(mmdAutomationStderr, e.Data);
            process.Exited += (_, _) =>
            {
                try
                {
                    mmdAutomationLastResultJson = mmdAutomationStdout.ToString().Trim();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FBXVmdPipelineEditor] MMD 자동화 stdout 읽기 실패: {ex.Message}");
                    mmdAutomationLastResultJson = "";
                }
            };

            try
            {
                mmdAutomationStdout.Clear();
                mmdAutomationStderr.Clear();
                mmdAutomationLastResultJson = "";
                mmdAutomationStartedAt = DateTime.Now;
                mmdAutomationProcess = process;

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                string outputLabel = skipRenderAvi ? "skip-render" : $"Output AVI: {outputAvi}";
                Debug.Log($"External MMD: 자동화 시작됨 (PID={process.Id}). {outputLabel}");
            }
            catch (Exception ex)
            {
                mmdAutomationProcess = null;
                Debug.LogError($"External MMD: 프로세스 시작 실패: {ex.Message}");
            }
        }

        private static void DrawMmdAutomationStatus()
        {
            if (mmdAutomationProcess == null)
            {
                if (!string.IsNullOrEmpty(mmdAutomationLastResultJson))
                {
                    EditorGUILayout.LabelField("Last result (stdout JSON):");
                    EditorGUILayout.TextArea(mmdAutomationLastResultJson, GUILayout.MinHeight(48));
                }
                return;
            }

            bool isRunning = !mmdAutomationProcess.HasExited;
            string status = isRunning ? "Running" : $"Exited (code {mmdAutomationProcess.ExitCode})";
            TimeSpan elapsed = DateTime.Now - mmdAutomationStartedAt;

            EditorGUILayout.LabelField("Status", status);
            EditorGUILayout.LabelField("Elapsed", $"{elapsed:hh\\:mm\\:ss}");

            if (!isRunning && !string.IsNullOrEmpty(mmdAutomationLastResultJson))
            {
                EditorGUILayout.LabelField("Result (stdout JSON):");
                EditorGUILayout.TextArea(mmdAutomationLastResultJson, GUILayout.MinHeight(48));
            }

            if (mmdAutomationStdout.Length > 0)
            {
                EditorGUILayout.LabelField("Stdout (tail):");
                EditorGUILayout.TextArea(mmdAutomationStdout.ToString(), GUILayout.MinHeight(64));
            }

            if (mmdAutomationStderr.Length > 0)
            {
                EditorGUILayout.LabelField("Stderr (tail):");
                EditorGUILayout.TextArea(mmdAutomationStderr.ToString(), GUILayout.MinHeight(64));
            }
        }

        private void DrawImportSettings()
        {
            bool expanded = DrawFoldout("Import", "FBX 임포트");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_shouldSaveToImportFolder", "Import_FBX 폴더에 저장", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_shouldSaveToImportFolder"))
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

            EditorDrawUtility.DrawProperty(serializedObject, "_showGhostModel", "Ghost 모델 표시", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_showGhostSkeletonWhenNoRenderers", "Renderer 없는 Ghost skeleton 표시", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_showGhostModel"))
            {
                EditorGUILayout.HelpBox("디버그용 표시입니다. 녹화 기준을 확인한 뒤에는 꺼두는 편이 좋습니다.", MessageType.Info);
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseLegacyPoseSpaceFacingCorrection", "Legacy PoseSpace 방향 보정", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_shouldUseLegacyPoseSpaceFacingCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "이전 수동 프로젝트와 같은 180도 방향 보정입니다. 현재 자동 경로의 카메라 정면 기준을 깨뜨릴 수 있어 비교/롤백용으로만 사용합니다.",
                    MessageType.Warning);
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_shouldPreserveFbxRootRotation", "FBX Root 회전 보존", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_shouldPreserveFbxRootRotation"))
            {
                EditorGUILayout.HelpBox("Main_Auto가 Sub_Manual 직접 Animator 재생처럼 FBX body/root yaw를 그대로 따릅니다.", MessageType.Info);
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseEditorHumanoidClipMuscleReference", "Editor Humanoid Muscle 기준 사용", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_shouldUseEditorHumanoidClipMuscleReference"))
            {
                EditorGUILayout.HelpBox(
                    "Editor 자동 경로에서는 Unity가 FBX에서 임포트한 Humanoid muscle curve를 기준으로 사용합니다. Runtime Assimp Ghost의 회전 curve가 수동 기준과 다르게 해석될 때 팔/상체 포즈 차이를 줄이는 경로입니다.",
                    MessageType.Info);

                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseEditorHumanoidRootTranslationReference", "Editor Humanoid RootT 이동 기준 사용", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseManualAnimatorFingerPoseReference", "수동 Animator 손가락 기준 사용", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseManualAnimatorBodyRotationReference", "수동 Animator bodyRotation 기준 사용", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseManualAnimatorHipsLocalPositionReference", "수동 Animator Hips localPosition 기준 사용", showWarningOnNull: true);
                if (EditorDrawUtility.GetBool(serializedObject, "_shouldUseManualAnimatorHipsLocalPositionReference"))
                {
                    EditorGUILayout.HelpBox(
                        "Sub_Manual/testPrefab Animator의 Hips localPosition 경로를 Main_Auto target Hips에 선택 적용합니다. visual_body_arc_jitter A/B 검증 전용으로 사용합니다.",
                        MessageType.Info);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorHipsLocalPositionWeight", "Hips localPosition 보정 강도", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorHipsLocalPositionMaxOffset", "Hips localPosition 최대 보정", showWarningOnNull: true);
                }
                EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseManualAnimatorFootLocalRotationReference", "Lower-body localRotation runtime reference", showWarningOnNull: true);
                if (EditorDrawUtility.GetBool(serializedObject, "_shouldUseManualAnimatorFootLocalRotationReference"))
                {
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorFootLocalRotationReferenceWeight", "Lower-body localRotation reference weight", showWarningOnNull: true);
                }
                EditorDrawUtility.DrawProperty(serializedObject, "_shouldUseManualAnimatorLowerBodySegmentDirectionReference", "Lower-body segment direction runtime reference", showWarningOnNull: true);
                if (EditorDrawUtility.GetBool(serializedObject, "_shouldUseManualAnimatorLowerBodySegmentDirectionReference"))
                {
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorLowerBodySegmentDirectionReferenceWeight", "Lower-body segment direction weight", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle", "Lower-body segment direction max angle", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_shouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference", "Disable UpperLegToLowerLeg segment direction", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle", "UpperLegToLowerLeg segment direction max angle override", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_shouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference", "Disable LowerLegToFoot segment direction", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle", "LowerLegToFoot segment direction max angle override", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_shouldDisableManualAnimatorFootToToesSegmentDirectionReference", "Disable FootToToes segment direction", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle", "FootToToes segment direction max angle override", showWarningOnNull: true);
                }
                EditorDrawUtility.DrawProperty(serializedObject, "_useManualAnimatorBipedIkFootPositionReference", "BipedIK foot position runtime reference", showWarningOnNull: true);
                if (EditorDrawUtility.GetBool(serializedObject, "_useManualAnimatorBipedIkFootPositionReference"))
                {
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorBipedIkFootPositionReferenceWeight", "BipedIK foot position reference weight", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorBipedIkFootPositionReferenceMaxOffset", "BipedIK foot position max offset", showWarningOnNull: true);
                }
                if (EditorDrawUtility.GetBool(serializedObject, "_shouldUseManualAnimatorFingerPoseReference"))
                {
                    EditorGUILayout.HelpBox(
                        "손가락은 Sub_Manual/testPrefab Animator가 같은 FBX clip을 평가한 HumanPose 값을 기준으로 덮어씁니다. 비워두면 기본 testPrefab과 TestAnimator1_Manual을 자동으로 찾습니다.",
                        MessageType.Info);
                    EditorDrawUtility.DrawProperty(serializedObject, "_useManualAnimatorThumbLocalRotationReference", "엄지 localRotation도 수동 기준 적용", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_useManualAnimatorHandLocalRotationReference", "손목 localRotation도 수동 기준 적용", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_useManualAnimatorThumbSegmentDirectionReference", "엄지 세그먼트 방향도 수동 기준 적용", showWarningOnNull: true);
                    if (EditorDrawUtility.GetBool(serializedObject, "_useManualAnimatorThumbSegmentDirectionReference"))
                    {
                        EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorThumbSegmentDirectionWeight", "엄지 세그먼트 방향 보정 강도", showWarningOnNull: true);
                    }
                    EditorDrawUtility.DrawProperty(serializedObject, "_useManualAnimatorThumbHandDirectionReference", "엄지 시작 방향도 수동 기준 적용", showWarningOnNull: true);
                    if (EditorDrawUtility.GetBool(serializedObject, "_useManualAnimatorThumbHandDirectionReference"))
                    {
                        EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorThumbHandDirectionWeight", "엄지 시작 방향 보정 강도", showWarningOnNull: true);
                    }
                    EditorDrawUtility.DrawProperty(serializedObject, "_useManualAnimatorHandPalmFrameReference", "손바닥 프레임도 수동 기준 적용", showWarningOnNull: true);
                    if (EditorDrawUtility.GetBool(serializedObject, "_useManualAnimatorHandPalmFrameReference"))
                    {
                        EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorHandPalmFrameWeight", "손바닥 프레임 보정 강도", showWarningOnNull: true);
                    }
                    EditorDrawUtility.DrawProperty(serializedObject, "_useManualAnimatorThumbBasePositionReference", "엄지 시작 위치도 수동 기준 적용", showWarningOnNull: true);
                    if (EditorDrawUtility.GetBool(serializedObject, "_useManualAnimatorThumbBasePositionReference"))
                    {
                        EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorThumbBasePositionWeight", "엄지 시작 위치 보정 강도", showWarningOnNull: true);
                        EditorDrawUtility.DrawProperty(serializedObject, "_manualAnimatorThumbBasePositionMaxOffset", "엄지 시작 위치 최대 보정", showWarningOnNull: true);
                    }
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualFingerReferencePrefab", "손가락 기준 프리팹", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_manualFingerReferenceController", "손가락 기준 컨트롤러", showWarningOnNull: true);
                }
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_disableMmdShoulderPostPoseDuringRetarget", "Retarget 중 MMD 어깨 PPH 끄기", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_disableMmdShoulderPostPoseDuringRetarget"))
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

            EditorDrawUtility.DrawProperty(serializedObject, "_clampRetargetMusclesToHumanRange", "Muscle 기본 범위 제한", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_shouldLockTargetHumanoidBonePositions", "Humanoid 본 길이 잠금", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_lockTargetLimbChildLocalPositions", "Limb 보조본 위치 잠금", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_lockTargetLimbChildLocalRotations", "Limb 보조본 회전 잠금", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_attachTargetArmDeformationGuard", "Target 팔 변형 가드 자동 부착", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_attachTargetArmDeformationGuard"))
            {
                EditorGUILayout.HelpBox("Retarget 외의 YYB 직접 재생 경로에도 붙일 수 있는 HumanoidArmDeformationGuard와 같은 규칙을 자동 경로 Target에 적용합니다.", MessageType.Info);
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_targetGuardClampAnatomicalArmMuscles", "Target 가드 Muscle 제한", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_targetGuardClampArmStretchMuscles", "Target 가드 Stretch 제한", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logArmDeformationGuardCorrections", "가드 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            EditorDrawUtility.DrawProperty(serializedObject, "_enableAnimationRiggingArmTwistCorrection", "Animation Rigging 팔 Twist 보정", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableAnimationRiggingArmTwistCorrection"))
            {
                EditorGUILayout.HelpBox("고스트 리타게팅 결과 위에 YYB 팔 twist 보조본만 보정합니다. 기존 Limb 보조본 회전 잠금은 rig가 제어하는 twist 본을 예외 처리합니다.", MessageType.Info);
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_AnimationRiggingArmTwistRigWeight", "전체 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_AnimationRiggingUpperArmTwistWeight", "상완 Twist 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_AnimationRiggingForearmTwistWeight", "전완 Twist 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logAnimationRiggingArmTwistCorrection", "Rigging 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_enableYybArmDirectionRetargetCorrection", "YYB 팔 방향 Retarget 보정", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableYybArmDirectionRetargetCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "실험 옵션입니다. 일부 정상 프레임까지 크게 바꿀 수 있어 기본값은 끕니다. 켤 때는 반드시 Sub_Manual/testPrefab 기준과 같은 시간대 스크린샷을 비교하세요.",
                    MessageType.Warning);
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmDirectionUpperArmWeight", "상완 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmDirectionForearmWeight", "전완 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmDirectionUpperArmMaxDegrees", "상완 최대 각도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmDirectionForearmMaxDegrees", "전완 최대 각도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmDirectionLeftSideWeightScale", "왼쪽 팔 영향도 배율", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmDirectionRightSideWeightScale", "오른쪽 팔 영향도 배율", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logYybArmDirectionRetargetCorrection", "팔 방향 보정 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_enableYybArmVisualTwistCorrection", "YYB 팔 시각 Twist 보정", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableYybArmVisualTwistCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "Animation Rigging 없이 전완-손목 회전 변화를 YYB 팔/소매 보조본에 직접 분배합니다. 빨간 박스처럼 소매가 얇게 찌그러지는 시각 변형을 줄이기 위한 보정입니다.",
                    MessageType.Info);
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmVisualUpperArmInfluence", "상완 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmVisualForearmInfluence", "전완/손목 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmVisualUpperArmMaxDegrees", "상완 최대 각도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmVisualForearmMaxDegrees", "전완/손목 최대 각도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logYybArmVisualTwistCorrection", "시각 보정 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_enableYybArmSwingLimitCorrection", "YYB 상완 하강 제한", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableYybArmSwingLimitCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "손이 완전히 아래로 내려간 자연 포즈는 제외하고, 소매가 어깨 아래로 말려 내려오는 상완 방향만 제한합니다.",
                    MessageType.Info);
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingLimitWeight", "보정 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingMaxDownDot", "상완 하강 허용치", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingMinHandHorizontalRatio", "손 수평 거리 최소값", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingMaxHandBelowShoulderRatio", "자연 하강 제외 기준", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingHorizontalReachLimitWeight", "수평 reach 제한 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingMaxHandHorizontalReachRatio", "손 수평 reach 최대값", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingRaisedPoseHorizontalReachLimitWeight", "Raised pose reach 제한 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingRaisedPoseMinUpperArmDownDot", "Raised pose 최소 하강 dot", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingRaisedPoseMaxHandBelowShoulderRatio", "Raised pose 아래 위치 제외 기준", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSwingRaisedPoseMaxHandHorizontalReachRatio", "Raised pose 수평 reach 최대값", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logYybArmSwingLimitCorrection", "상완 하강 제한 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_enableYybArmSleeveAnchorCorrection", "YYB 소매 Anchor 보정", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableYybArmSleeveAnchorCorrection"))
            {
                EditorGUILayout.HelpBox(
                    "상완 회전을 YYB 소매/어깨 캡 보조본에 제한적으로 전달해 어깨 부근 소매가 따로 무너지는 현상을 줄입니다.",
                    MessageType.Info);
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSleeveAnchorInfluence", "소매 상단 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmShoulderCapAnchorInfluence", "어깨 캡 영향도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_YybArmSleeveAnchorMaxDegrees", "최대 따라가기 각도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logYybArmSleeveAnchorCorrection", "소매 Anchor 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            if (EditorDrawUtility.GetBool(serializedObject, "_shouldLockTargetHumanoidBonePositions"))
            {
                EditorGUILayout.HelpBox("SetHumanPose 이후 팔/다리 본 localPosition을 초기값으로 복구해 모델이 길게 늘어나거나 가늘어지는 변형을 막습니다.", MessageType.Info);
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_enableAnatomicalArmGuard", "팔 해부학적 안전장치", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableAnatomicalArmGuard"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_ArmStretchMuscleLimit", "팔 Stretch 허용치", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_clampRetargetArmStretchMuscles", "Retarget 팔꿈치 Stretch 제한", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_UpperArmTwistMuscleLimit", "상완 Twist 허용치", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_LowerArmTwistMuscleLimit", "전완 Twist 허용치", showWarningOnNull: true);
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

            EditorGUILayout.HelpBox("Target Idle Pose Guard 설정은 TargetIdlePoseGuard 컴포넌트에서 직접 편집합니다.", MessageType.Info);

            EndFoldout(true);
        }

        private void DrawRecordingSettings()
        {
            bool expanded = DrawFoldout("Recording", "VMD 녹화");
            if (!expanded)
            {
                EndFoldout(false);
                return;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_startDelay", "VMD 녹화 시작 대기 시간", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_vmdRecordingPlaybackSpeed", "VMD 녹화 배속", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_useKnownMmdReferenceTiming", "satisfaction_2 reference timing 사용", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_RetargetPrewarmFrameCount", "시작 포즈 prewarm 프레임", showWarningOnNull: true);
            DrawFolderProperty("_additionalVmdCopyFolder", "VMD 추가 복사 폴더", "생성된 VMD를 추가로 복사할 폴더(선택)");
            EditorDrawUtility.DrawProperty(serializedObject, "_clampRetargetVisualClipStep", "Ghost clip time step 제한", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_clampRetargetVisualClipStep"))
            {
                EditorGUILayout.HelpBox("이 옵션은 테스트 전용입니다. 실제 clip time을 제한하므로 프레임 드랍 때 재생이 느려질 수 있습니다.", MessageType.Warning);
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_RetargetVisualClipFrameRate", "시각 재생 기준 FPS", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_smoothRetargetPoseOnVisualStepSpike", "pose spike smoothing", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_smoothRetargetPoseOnVisualStepSpike"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_RetargetPoseVisualSpikeCurrentWeight", "현재 pose 반영 비율", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_RetargetPoseVisualMuscleDeltaThreshold", "muscle delta 기준", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

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

            EditorDrawUtility.DrawProperty(serializedObject, "_HeightOffset", "높이 보정", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_MovementScaleMultiplier", "보폭 비율", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_clampRetargetRootDeltaSpikes", "Root 순간이동 방지", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_clampRetargetRootDeltaSpikes"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_MaxRetargetRootDeltaPerFrame", "프레임당 Root 이동 제한", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logRetargetRootDeltaSpikes", "Root 튐 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_smoothRetargetGrounding", "접지 보정 안정화", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_smoothRetargetGrounding"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_MaxGroundingVerticalStepPerFrame", "프레임당 접지 보정 제한", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_GroundingSmoothing", "접지 보정 반영 비율", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_GroundingDeadZone", "접지 보정 데드존", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_FreezeRootYAfterInitialGrounding", "초기 접지 뒤 root Y 고정", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_rejectRendererGroundingOutliers", "renderer 접지 outlier 제외", showWarningOnNull: true);
                if (EditorDrawUtility.GetBool(serializedObject, "_rejectRendererGroundingOutliers"))
                {
                    EditorGUI.indentLevel++;
                    EditorDrawUtility.DrawProperty(serializedObject, "_MaxRendererFootGroundingSeparation", "renderer-foot 허용 거리", showWarningOnNull: true);
                    EditorGUI.indentLevel--;
                }
                EditorDrawUtility.DrawProperty(serializedObject, "_smoothLateVisualGroundingCorrection", "최종 접지 미세 떨림 완화", showWarningOnNull: true);
                if (EditorDrawUtility.GetBool(serializedObject, "_smoothLateVisualGroundingCorrection"))
                {
                    EditorGUI.indentLevel++;
                    EditorDrawUtility.DrawProperty(serializedObject, "_LateVisualGroundingSnapThreshold", "큰 오차 즉시 보정 기준", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_LateVisualGroundingSmoothing", "최종 접지 smoothing", showWarningOnNull: true);
                    EditorDrawUtility.DrawProperty(serializedObject, "_MaxLateVisualGroundingStepPerFrame", "최종 접지 프레임당 제한", showWarningOnNull: true);
                    EditorGUI.indentLevel--;
                }
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
            EditorDrawUtility.DrawProperty(serializedObject, "_FingerStretchScale", "손가락 굽힘 스케일", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_FingerSpreadScale", "손가락 벌림 스케일", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_ThumbStretchScale", "엄지 굽힘 스케일", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_ThumbSpreadScale", "엄지 벌림 스케일", showWarningOnNull: true);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("엄지 해부학적 제한", EditorStyles.boldLabel);
            EditorDrawUtility.DrawProperty(serializedObject, "_enableThumbAnatomicalGuard", "엄지 해부학적 안전장치", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableThumbAnatomicalGuard"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbStretchMin", "엄지 굽힘 최소", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbStretchMax", "엄지 굽힘 최대", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbSpreadMin", "엄지 벌림 최소", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbSpreadMax", "엄지 벌림 최대", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_preserveManualFingerReferenceThumbMuscles", "Manual 기준 엄지 muscle 보존", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logThumbAnatomicalGuardCorrections", "엄지 가드 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_enableThumbLocalRotationGuard", "엄지 본 회전 안전장치", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableThumbLocalRotationGuard"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_disableThumbLocalRotationGuardWithManualFingerReference", "Manual 기준 사용 시 localRotation 가드 끄기", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbProximalMaxLocalAngle", "엄지 첫 관절 허용각", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbIntermediateMaxLocalAngle", "엄지 둘째 관절 허용각", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbDistalMaxLocalAngle", "엄지 끝 관절 허용각", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_logThumbLocalRotationGuardCorrections", "엄지 본 회전 진단 로그", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("엄지 Offset", EditorStyles.boldLabel);
            EditorDrawUtility.DrawProperty(serializedObject, "_ThumbRotationOffset", "엄지 회전 Offset", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_mirrorRightThumbRotationOffset", "오른손 Offset Mirror", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_LeftThumbRotationOffset", "왼손 추가 회전 Offset", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_RightThumbRotationOffset", "오른손 추가 회전 Offset", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_useDefaultThumbStretchOffsetWhenUnset", "0값이면 기본 굽힘 Offset 사용", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_ThumbStretchOffset", "엄지 굽힘 Muscle Offset", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_syncDetachedThumbBaseHelpers", "Thumb0 보조본 회전 동기화", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_syncDetachedThumbBaseHelpers"))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("YYB는 실제 엄지 구동본과 스킨용 Thumb0 보조본이 분리되어 있어, 위치를 완전히 고정하면 엄지 뿌리가 탈골처럼 보일 수 있습니다. 기본값은 제한 추종입니다.", MessageType.Info);
                EditorDrawUtility.DrawProperty(serializedObject, "_detachedThumbBaseHelperSyncWeight", "Thumb0 보조본 동기화 비율", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_detachedThumbBaseHelperMaxLocalAngle", "Thumb0 보조본 최대 회전각", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_syncDetachedThumbBaseHelperPositions", "Thumb0 보조본 위치 동기화", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }
            EditorDrawUtility.DrawProperty(serializedObject, "_stabilizeDetachedThumbBasePalm", "손꿈치 Thumb0 안정화", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_stabilizeDetachedThumbBasePalm"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_detachedThumbBasePalmStabilizeWeight", "손꿈치 안정화 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_detachedThumbBasePalmMaxLocalAngle", "손꿈치 허용 회전각", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_stabilizeThumbWebbingCrease", "엄지 웹빙 라인 안정화", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_stabilizeThumbWebbingCrease"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_thumbWebbingCreaseStabilizeWeight", "웹빙 안정화 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_thumbWebbingCreaseMaxLocalAngle", "웹빙 허용 회전각", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_thumbWebbingCreaseMaxPositionOffset", "웹빙 허용 위치 이동", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_enableThumbVisualLengthGuard", "엄지 시각 길이 보정", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_enableThumbVisualLengthGuard"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbProjectionMinPalmNormal", "엄지 손바닥 앞쪽 최소 성분", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbProjectionMaxPalmNormal", "엄지 손바닥 돌출 허용치", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbProjectionGuardWeight", "엄지 돌출 보정 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbIndexMaxSpreadAngle", "엄지-검지 최대 벌어짐", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbIndexSpreadGuardWeight", "엄지 벌어짐 보정 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbMaxSegmentBendAngle", "엄지 마디 최대 굽힘각", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbSegmentStraightenWeight", "엄지 마디 펴기 강도", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            EditorDrawUtility.DrawProperty(serializedObject, "_EnableSmartCurve", "손가락 Smart Curve", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_EnableSmartCurve"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_SmartCurveStrength", "손가락 감쇠 강도", showWarningOnNull: true);
                EditorDrawUtility.DrawProperty(serializedObject, "_StretchThreshold", "굽힘 임계값", showWarningOnNull: true);
                EditorGUI.indentLevel--;
            }

            EditorDrawUtility.DrawProperty(serializedObject, "_EnableThumbSmartCurve", "엄지 Smart Curve", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_EnableThumbSmartCurve"))
            {
                EditorGUI.indentLevel++;
                EditorDrawUtility.DrawProperty(serializedObject, "_ThumbSmartCurveStrength", "엄지 감쇠 강도", showWarningOnNull: true);
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

            EditorDrawUtility.DrawProperty(serializedObject, "_showBoneMappingLog", "본 매핑 로그", showWarningOnNull: true);
            EditorDrawUtility.DrawProperty(serializedObject, "_showRuntimeAnimationLog", "런타임 애니메이션 로그", showWarningOnNull: true);
            if (EditorDrawUtility.GetBool(serializedObject, "_showBoneMappingLog") || EditorDrawUtility.GetBool(serializedObject, "_showRuntimeAnimationLog"))
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

        private void DrawFolderProperty(string propertyName, string label, string browseTitle)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"Inspector 필드를 찾을 수 없습니다: {propertyName}", MessageType.Warning);
                return;
            }

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
                return;
            }

            EditorGUILayout.BeginHorizontal();
            property.stringValue = EditorGUILayout.TextField(new GUIContent(label, property.tooltip), property.stringValue);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string directory = "";
                try
                {
                    if (!string.IsNullOrEmpty(property.stringValue))
                    {
                        directory = Directory.Exists(property.stringValue)
                            ? property.stringValue
                            : Path.GetDirectoryName(property.stringValue) ?? "";
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FBXVmdPipelineEditor] 디렉터리 확인 실패: {ex.Message}");
                    directory = "";
                }

                string next = EditorUtility.OpenFolderPanel(browseTitle, directory, "");
                if (!string.IsNullOrEmpty(next))
                {
                    property.stringValue = next;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
