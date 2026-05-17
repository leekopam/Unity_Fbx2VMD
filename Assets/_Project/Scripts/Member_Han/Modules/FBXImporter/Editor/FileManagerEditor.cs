using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
            DrawExternalMmdAutomationSettings();
            DrawDebugSettings();

            serializedObject.ApplyModifiedProperties();
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
                catch
                {
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
                catch
                {
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

            string pyLauncher = @"C:\Windows\py.exe";
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
            catch
            {
                // ignored
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
            catch
            {
                // ignored
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
                        Debug.LogWarning($"Failed to stop process: {ex.Message}");
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
                Debug.LogError("External MMD: Project(.pmm) and Motion(.vmd) are required.");
                return;
            }

            if (!skipRenderAvi && string.IsNullOrEmpty(outputAvi))
            {
                Debug.LogError("External MMD: Output(.avi) is required unless Skip render AVI is enabled.");
                return;
            }

            string projectRoot = ResolveProjectRoot();
            string cliScript = Path.Combine(
                projectRoot,
                "Docs",
                "Machine_Spirit",
                "Tools",
                "Local",
                "mmd_qa_mcp",
                "mmd_qa_automation_cli.py");

            if (!File.Exists(cliScript))
            {
                Debug.LogError($"External MMD: CLI script not found: {cliScript}");
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
                catch
                {
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
                Debug.Log($"External MMD: started automation (PID={process.Id}). {outputLabel}");
            }
            catch (Exception ex)
            {
                mmdAutomationProcess = null;
                Debug.LogError($"External MMD: failed to start process: {ex.Message}");
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
                DrawProperty("useEditorHumanoidRootTranslationReference", "Editor Humanoid RootT 이동 기준 사용");
                DrawProperty("useManualAnimatorFingerPoseReference", "수동 Animator 손가락 기준 사용");
                DrawProperty("useManualAnimatorBodyRotationReference", "수동 Animator bodyRotation 기준 사용");
                DrawProperty("useManualAnimatorHipsLocalPositionReference", "수동 Animator Hips localPosition 기준 사용");
                if (GetBool("useManualAnimatorHipsLocalPositionReference"))
                {
                    EditorGUILayout.HelpBox(
                        "Sub_Manual/testPrefab Animator의 Hips localPosition 경로를 Main_Auto target Hips에 선택 적용합니다. visual_body_arc_jitter A/B 검증 전용으로 사용합니다.",
                        MessageType.Info);
                    DrawProperty("manualAnimatorHipsLocalPositionWeight", "Hips localPosition 보정 강도");
                    DrawProperty("manualAnimatorHipsLocalPositionMaxOffset", "Hips localPosition 최대 보정");
                }
                if (GetBool("useManualAnimatorFingerPoseReference"))
                {
                    EditorGUILayout.HelpBox(
                        "손가락은 Sub_Manual/testPrefab Animator가 같은 FBX clip을 평가한 HumanPose 값을 기준으로 덮어씁니다. 비워두면 기본 testPrefab과 TestAnimator1_Manual을 자동으로 찾습니다.",
                        MessageType.Info);
                    DrawProperty("useManualAnimatorThumbLocalRotationReference", "엄지 localRotation도 수동 기준 적용");
                    DrawProperty("useManualAnimatorHandLocalRotationReference", "손목 localRotation도 수동 기준 적용");
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
            DrawProperty("RetargetPrewarmFrameCount", "시작 포즈 prewarm 프레임");
            DrawFolderProperty("additionalVmdCopyFolder", "VMD 추가 복사 폴더", "생성된 VMD를 추가로 복사할 폴더(선택)");
            DrawProperty("enableRecordingDiagnostics", "녹화 진단/캡처 사용");
            DrawProperty("clampRetargetVisualClipStep", "Ghost clip time step 제한");
            if (GetBool("clampRetargetVisualClipStep"))
            {
                EditorGUILayout.HelpBox("이 옵션은 테스트 전용입니다. 실제 clip time을 제한하므로 프레임 드랍 때 재생이 느려질 수 있습니다.", MessageType.Warning);
                EditorGUI.indentLevel++;
                DrawProperty("RetargetVisualClipFrameRate", "시각 재생 기준 FPS");
                EditorGUI.indentLevel--;
            }

            DrawProperty("smoothRetargetPoseOnVisualStepSpike", "pose spike smoothing");
            if (GetBool("smoothRetargetPoseOnVisualStepSpike"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("RetargetPoseVisualSpikeCurrentWeight", "현재 pose 반영 비율");
                DrawProperty("RetargetPoseVisualMuscleDeltaThreshold", "muscle delta 기준");
                EditorGUI.indentLevel--;
            }

            if (GetBool("enableRecordingDiagnostics"))
            {
                EditorGUILayout.HelpBox(
                    "CSV/프레임 캡처와 결정론 녹화는 회귀 테스트용입니다. 일반 변환에서 켜면 GameView가 살짝 멈추거나 배속처럼 보일 수 있습니다.",
                    MessageType.Warning);
                EditorGUI.indentLevel++;
                DrawProperty("useDeterministicCaptureFramerateForDiagnostics", "테스트용 30fps 시간 고정");
                DrawProperty("enableDiagnosticFingerCloseups", "손 close-up 캡처");
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

            DrawProperty("smoothRetargetGrounding", "접지 보정 안정화");
            if (GetBool("smoothRetargetGrounding"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("MaxGroundingVerticalStepPerFrame", "프레임당 접지 보정 제한");
                DrawProperty("GroundingSmoothing", "접지 보정 반영 비율");
                DrawProperty("GroundingDeadZone", "접지 보정 데드존");
                DrawProperty("FreezeRootYAfterInitialGrounding", "초기 접지 뒤 root Y 고정");
                DrawProperty("rejectRendererGroundingOutliers", "renderer 접지 outlier 제외");
                if (GetBool("rejectRendererGroundingOutliers"))
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("MaxRendererFootGroundingSeparation", "renderer-foot 허용 거리");
                    EditorGUI.indentLevel--;
                }
                DrawProperty("smoothLateVisualGroundingCorrection", "최종 접지 미세 떨림 완화");
                if (GetBool("smoothLateVisualGroundingCorrection"))
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("LateVisualGroundingSnapThreshold", "큰 오차 즉시 보정 기준");
                    DrawProperty("LateVisualGroundingSmoothing", "최종 접지 smoothing");
                    DrawProperty("MaxLateVisualGroundingStepPerFrame", "최종 접지 프레임당 제한");
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
                catch
                {
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

        private bool GetBool(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
        }
    }
}
