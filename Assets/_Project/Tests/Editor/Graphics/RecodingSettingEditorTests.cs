using System;
using System.IO;
using System.Reflection;
using Member_Han.Modules.FBXImporter;
using Member_Han.Modules.FileSystem;
using Member_Han.Modules.Graphics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor.Graphics
{
    public class RecodingSettingEditorTests
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string SettingsWindowTypeName =
            "Member_Han.Modules.Graphics.EditorTools.MainRecordingSettingsWindow, Assembly-CSharp-Editor";
        private const string SettingsWindowContextTypeName =
            "Member_Han.Modules.Graphics.EditorTools.MainRecordingSettingsWindowContext, Assembly-CSharp-Editor";
        private const string LayoutSpecTypeName =
            "Member_Han.Modules.Graphics.MainRecordingSettingsLayoutSpec, Assembly-CSharp";
        private const string RuntimePopupTypeName =
            "Member_Han.Modules.Graphics.MainRecordingSettingsPopup, Assembly-CSharp";
        private const string CompanionControllerTypeName =
            "Member_Han.Modules.Graphics.MainRecordingSettingsCompanionController, Assembly-CSharp";
        private const string EditorPlayModeGuardTypeName =
            "Member_Han.Modules.Graphics.EditorTools.MainRecordingEditorPlayModeGuard, Assembly-CSharp-Editor";

        [Test]
        public void Given_MissingFileManager_When_ApplyingSharedSettingsWithFbxPath_Then_ReturnsUserMessage()
        {
            var document = new MainRecordingSettingsDocument
            {
                fbxPath = "D:/motion/sample.fbx",
                captureWidth = 1920,
                captureHeight = 1080,
            };

            MainRecordingSettingsActionResult result =
                MainRecordingSettingsActions.ApplyForTests(document, null, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.UserMessage, Does.Contain("FBX"));
        }

        [Test]
        public void Given_RecodingSetting_When_LoadingSharedSettings_Then_AppliesCaptureAndPopupSettings()
        {
            string path = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(path);
            store.Save(new MainRecordingSettingsDocument
            {
                captureWidth = 1280,
                captureHeight = 720,
                openSettingsOnStart = false,
            });

            var fileManagerObject = new GameObject("Shared Settings FileManager Test");
            var settingObject = new GameObject("Shared Settings RecodingSetting Test");

            try
            {
                var fileManager = fileManagerObject.AddComponent<Member_Han.Modules.FBXImporter.FileManager>();
                var recodingSetting = settingObject.AddComponent<RecodingSetting>();
                SetField(recodingSetting, "recordingFileManager", fileManager);

                MainRecordingSettingsActionResult result =
                    recodingSetting.LoadSharedSettingsFromPathForTests(path);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(GetField<bool>(recodingSetting, "openSettingsPopupOnStart"), Is.False);
                Assert.That(GetField<object>(recodingSetting, "recordingCaptureQuality").ToString(), Is.EqualTo("Custom"));
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureWidth"), Is.EqualTo(1280));
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureHeight"), Is.EqualTo(720));
                Assert.That(fileManager.recordingCaptureQuality, Is.EqualTo(RecordingCaptureQualityPreset.Custom));
                Assert.That(fileManager.customRecordingCaptureWidth, Is.EqualTo(1280));
                Assert.That(fileManager.customRecordingCaptureHeight, Is.EqualTo(720));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_SettingsFileChanges_Then_PollingAppliesUpdatedDocument()
        {
            string path = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(path);
            store.Save(new MainRecordingSettingsDocument
            {
                captureWidth = 1280,
                captureHeight = 720,
                openSettingsOnStart = false,
            });

            var settingObject = new GameObject("Shared Settings Polling RecodingSetting Test");

            try
            {
                var recodingSetting = settingObject.AddComponent<RecodingSetting>();
                recodingSetting.LoadSharedSettingsFromPathForTests(path);

                store.Save(new MainRecordingSettingsDocument
                {
                    captureWidth = 2560,
                    captureHeight = 1440,
                    openSettingsOnStart = true,
                });
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

                MainRecordingSettingsActionResult result = recodingSetting.PollSharedSettingsForTests();

                Assert.That(result.Succeeded, Is.True);
                Assert.That(GetField<bool>(recodingSetting, "openSettingsPopupOnStart"), Is.True);
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureWidth"), Is.EqualTo(2560));
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureHeight"), Is.EqualTo(1440));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_CreateEditor_Then_UsesRecordingInspector()
        {
            var settingObject = new GameObject("Recoding Setting Editor Test");

            try
            {
                var recodingSetting = settingObject.AddComponent<RecodingSetting>();
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(recodingSetting);

                try
                {
                    Assert.That(editor.GetType().FullName,
                        Is.EqualTo("Member_Han.Modules.Graphics.EditorTools.RecodingSettingEditor"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_BackgroundColorSetting_When_CreateEditor_Then_UsesKoreanInspector()
        {
            var settingObject = new GameObject("Background Color Setting Editor Test");

            try
            {
                var backgroundColorSetting = settingObject.AddComponent<BackgroundColorSetting>();
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(backgroundColorSetting);

                try
                {
                    Assert.That(editor.GetType().FullName,
                        Is.EqualTo("Member_Han.Modules.Graphics.EditorTools.BackgroundColorSettingEditor"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_SettingFields_When_InspectingAttributes_Then_UsesKoreanLabels()
        {
            AssertHeader<BackgroundColorSetting>("targetCamera", "대상");
            AssertInspectorName<BackgroundColorSetting>("targetCamera", "대상 카메라");
            AssertHeader<BackgroundColorSetting>("applyOnAwake", "적용");
            AssertInspectorName<BackgroundColorSetting>("applyOnAwake", "실행 시작 시 자동 적용");
            AssertInspectorName<BackgroundColorSetting>("applyOnValidate", "Unity OnValidate 자동 적용");
            AssertHeader<BackgroundColorSetting>("applyBackgroundColor", "카메라 배경");
            AssertInspectorName<BackgroundColorSetting>("applyBackgroundColor", "배경색 적용");
            AssertInspectorName<BackgroundColorSetting>("backgroundColor", "배경색");

            AssertHeader<RecodingSetting>("recordingFileManager", "수동 녹화");
            AssertInspectorName<RecodingSetting>("recordingFileManager", "녹화 FileManager");
            AssertInspectorName<RecodingSetting>("manualRecordButton", "수동 녹화 버튼");
            AssertInspectorName<RecodingSetting>("recordingController", "녹화 대상");
            AssertHeader<RecodingSetting>("enableRecordingDiagnostics", "화면 녹화 진단");
            AssertInspectorName<RecodingSetting>("enableRecordingDiagnostics", "녹화 진단/캡처 사용");
            AssertInspectorName<RecodingSetting>(
                "useDeterministicCaptureFramerateForDiagnostics",
                "테스트용 30fps 시간 고정");
            AssertInspectorName<RecodingSetting>("enableDiagnosticFingerCloseups", "손 close-up 캡처");
            AssertInspectorName<RecodingSetting>("applyDiagnosticsToFileManagerOnAwake", "실행 시작 시 FileManager에 적용");
            AssertHeader<RecodingSetting>("settingsPopup", "설정 팝업");
            AssertInspectorName<RecodingSetting>("settingsPopup", "런타임 설정 팝업");
            AssertInspectorName<RecodingSetting>("openSettingsPopupOnStart", "시작 시 설정 팝업 열기");
        }

        [Test]
        public void Given_SettingsWindowType_When_InspectingMetadata_Then_UsesSeparateEditorWindowForMainRecording()
        {
            Type windowType = RequireType(SettingsWindowTypeName);

            Assert.That(typeof(EditorWindow).IsAssignableFrom(windowType), Is.True);
            Assert.That(InvokeStatic<string>(windowType, "GetWindowTitle"), Is.EqualTo("Onboarding Assistant"));
            Assert.That(
                InvokeStatic<Vector2>(windowType, "GetReferenceWindowSizeForTests"),
                Is.EqualTo(new Vector2(1265f, 675f)));
            Assert.That(
                InvokeStatic<float>(windowType, "GetDefaultDisplayScaleForTests"),
                Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(
                InvokeStatic<Vector2>(windowType, "GetDefaultDisplayWindowSizeForTests"),
                Is.EqualTo(new Vector2(1581.25f, 843.75f)));
            Assert.That(
                InvokeStatic<Vector2>(windowType, "GetReferenceCardSizeForTests"),
                Is.EqualTo(new Vector2(672f, 192f)));
            Assert.That(InvokeStatic<bool>(windowType, "UsesRuntimeSharedLayoutSpecForTests"), Is.True);
            Assert.That(
                InvokeStatic<string>(windowType, "GetMainRecordingScenePathForTests"),
                Is.EqualTo(MainRecordingScenePath));
            Assert.That(File.Exists(MainRecordingScenePath), Is.True);
            Assert.That(InvokeStatic<bool>(windowType, "ShouldOpenForScene", MainRecordingScenePath), Is.True);
            Assert.That(InvokeStatic<bool>(windowType, "ShouldOpenForScene", MainAutoScenePath), Is.False);
            Assert.That(
                InvokeStatic<string>(windowType, "GetEditorSurfacePolicyForTests"),
                Does.Contain("EditorWindow"));
            Assert.That(
                InvokeStatic<string>(windowType, "GetEditorSurfacePolicyForTests"),
                Does.Contain("outside GameView"));
            Assert.That(
                InvokeStatic<bool>(
                    windowType,
                    "ShouldAutoOpenEditorWindowForPlayModeForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.EnteredPlayMode),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(
                    windowType,
                    "ShouldAutoOpenEditorWindowForPlayModeForTests",
                    MainRecordingScenePath,
                    true,
                    PlayModeStateChange.EnteredPlayMode),
                Is.False);
        }

        [Test]
        public void Given_SettingsWindowType_When_ResolvingDefaultPosition_Then_UsesMovableFloatingWindowAwayFromOrigin()
        {
            Type windowType = RequireType(SettingsWindowTypeName);

            Rect mainEditorRect = new Rect(0f, 0f, 2560f, 1440f);
            Rect position = InvokeStatic<Rect>(
                windowType,
                "GetDefaultFloatingWindowPositionForTests",
                mainEditorRect);

            Assert.That(
                InvokeStatic<string>(windowType, "GetWindowPresentationModeForTests"),
                Is.EqualTo("utility-floating"));
            Assert.That(position.width, Is.EqualTo(1581.25f).Within(1f));
            Assert.That(position.height, Is.EqualTo(843.75f).Within(1f));
            Assert.That(position.x, Is.GreaterThan(0f),
                "The settings window must not open at the screen origin where its title bar is hard to grab.");
            Assert.That(position.y, Is.GreaterThan(0f),
                "The settings window must leave top margin so the separate OS window can be dragged.");
        }

        [Test]
        public void Given_MainRecordingScene_When_PreparingEditorPlayMode_Then_DisablesBurstDirectCallsAndNeutralizesEditorTint()
        {
            Type guardType = RequireType(EditorPlayModeGuardTypeName);

            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.ExitingEditMode),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainAutoScenePath,
                    false,
                    PlayModeStateChange.ExitingEditMode),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    true,
                    PlayModeStateChange.ExitingEditMode),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.EnteredPlayMode),
                Is.True,
                "The neutral Playmode tint must be re-applied after domain reload finishes and Play Mode is entered.");
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldMaintainEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    false,
                    false),
                Is.True,
                "Burst direct-call IL postprocessing must be disabled before the user presses Play, because the direct-call initializer can run during the Play transition.");
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldMaintainEditorPlayModeGuardForTests",
                    MainAutoScenePath,
                    false,
                    false),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldMaintainEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    true,
                    false),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldMaintainEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    false,
                    true),
                Is.True,
                "The guard must keep the neutral Playmode tint while Unity is playing or still changing Play Mode.");
            Assert.That(InvokeStatic<bool>(guardType, "CanReflectBurstCompilerOptionsForTests"), Is.True);
            Assert.That(
                InvokeStatic<string>(guardType, "GetBurstDisableEnvironmentVariableNameForTests"),
                Is.EqualTo("UNITY_BURST_DISABLE_COMPILATION"));
            Assert.That(
                InvokeStatic<bool>(guardType, "IsBurstDisableEnvironmentValueForTests", "1"),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(guardType, "IsBurstDisableEnvironmentValueForTests", "0"),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    null,
                    false,
                    false),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    "1",
                    false,
                    false),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    null,
                    true,
                    false),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    null,
                    false,
                    true),
                Is.False);
            Assert.That(InvokeStatic<bool>(guardType, "CanReflectPlayModeTintForTests"), Is.True);
            Assert.That(
                InvokeStatic<bool>(guardType, "IsNeutralPlayModeTintForTests", new Color(1f, 1f, 1f, 1f)),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(guardType, "IsNeutralPlayModeTintForTests", new Color(0.8f, 0.8f, 0.8f, 1f)),
                Is.False);
            Assert.That(
                InvokeStatic<string>(guardType, "GetEditorPlayModeGuardPolicyForTests"),
                Does.Contain("UNITY_BURST_DISABLE_COMPILATION"));
        }

        [Test]
        public void Given_MainRecordingScene_When_RestoringEditorPlayModeState_Then_RestoresBurstAndEnvironment()
        {
            Type guardType = RequireType(EditorPlayModeGuardTypeName);
            string environmentVariableName =
                InvokeStatic<string>(guardType, "GetBurstDisableEnvironmentVariableNameForTests");
            string originalEnvironmentValue =
                Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);
            bool originalBurstCompilation = InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests");

            try
            {
                InvokeStatic<object>(guardType, "RestoreEditorPlayModeState");
                EditorSceneManager.OpenScene(MainRecordingScenePath);
                Environment.SetEnvironmentVariable(environmentVariableName, null, EnvironmentVariableTarget.Process);
                InvokeStatic<bool>(guardType, "ApplyBurstCompilationForTests", true);
                bool savedBurstCompilation = InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests");

                InvokeStatic<object>(guardType, "ApplyBeforeMainRecordingPlayMode");

                Assert.That(InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests"), Is.False);
                Assert.That(
                    Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process),
                    Is.EqualTo("1"));

                InvokeStatic<object>(guardType, "RestoreEditorPlayModeState");

                Assert.That(
                    InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests"),
                    Is.EqualTo(savedBurstCompilation));
                Assert.That(
                    Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process),
                    Is.Null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    environmentVariableName,
                    originalEnvironmentValue,
                    EnvironmentVariableTarget.Process);
                InvokeStatic<bool>(guardType, "ApplyBurstCompilationForTests", originalBurstCompilation);
            }
        }

        [Test]
        public void Given_SettingsWindowType_When_InspectingOnboardingLayout_Then_MatchesReferenceFirstScreen()
        {
            Type windowType = RequireType(SettingsWindowTypeName);
            Type layoutSpecType = RequireType(LayoutSpecTypeName);

            Assert.That(GetStaticMemberValue<int>(layoutSpecType, "ReferenceWidth"), Is.EqualTo(1265));
            Assert.That(GetStaticMemberValue<int>(layoutSpecType, "ReferenceHeight"), Is.EqualTo(675));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "RailWidth"), Is.EqualTo(56f));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "SidebarWidth"), Is.EqualTo(249f));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "CardWidth"), Is.EqualTo(672f));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "CardHeight"), Is.EqualTo(192f));
            Assert.That(
                GetStaticMemberValue<float>(layoutSpecType, "CardButtonWidth"),
                Is.GreaterThanOrEqualTo(128f),
                "The 'FBX 파일 선택' button must be wide enough to avoid clipping at the default 1.25x display scale.");
            float fbxImportButtonPreferredWidth = InvokeStatic<float>(
                windowType,
                "GetCardButtonPreferredWidthForTests",
                "FBX 파일 선택");
            float fbxImportButtonAvailableWidth = InvokeStatic<float>(
                windowType,
                "GetCardButtonAvailableWidthForTests");
            Assert.That(
                fbxImportButtonPreferredWidth,
                Is.LessThanOrEqualTo(fbxImportButtonAvailableWidth),
                $"The 'FBX 파일 선택' button label preferred width ({fbxImportButtonPreferredWidth}) must fit in the available button width ({fbxImportButtonAvailableWidth}).");
            Assert.That(
                InvokeStatic<string>(windowType, "GetCardButtonClippingForTests"),
                Is.Not.EqualTo("Clip"),
                "The card button style must not clip the requested Korean label.");

            Assert.That(
                InvokeStatic<string[]>(windowType, "GetSidebarItemLabelsForTests"),
                Is.EqualTo(new[] { "Camera 1", "Environment", "Directional Light" }));
            Assert.That(
                InvokeStatic<string[]>(windowType, "GetOnboardingCardTitlesForTests"),
                Is.EqualTo(new[] { "FBX 파일 임포트", "기능 1", "기능 2" }));
            Assert.That(
                InvokeStatic<string[]>(windowType, "GetOnboardingCardBodiesForTests"),
                Is.EqualTo(new[]
                {
                    "FBX 파일을 선택해 프로젝트로 가져오고 모션 캡쳐 설정을 시작합니다",
                    "추후 업데이트 예정입니다.",
                    "추후 업데이트 예정입니다.",
                }));
            Assert.That(
                InvokeStatic<string[]>(windowType, "GetOnboardingCardButtonLabelsForTests"),
                Is.EqualTo(new[] { "FBX 파일 선택", "준비중", "준비중" }));
            Assert.That(
                InvokeStatic<string[]>(windowType, "GetOnboardingCardActionsForTests"),
                Is.EqualTo(new[] { "ImportFbx", "ComingSoon", "ComingSoon" }));
            Assert.That(
                InvokeStatic<bool[]>(windowType, "GetOnboardingCardEnabledStatesForTests"),
                Is.EqualTo(new[] { true, false, false }));
            Assert.That(
                InvokeStatic<string>(windowType, "GetVisualAssetPolicyForTests"),
                Does.Contain("Clean & Minimalist GUI Pack"));
            Assert.That(
                InvokeStatic<string>(windowType, "GetVisualAssetPolicyForTests"),
                Does.Contain("no external reference product assets"));
            string[] requiredGuiPackAssetPaths =
                InvokeStatic<string[]>(windowType, "GetRequiredGuiPackAssetPathsForTests");
            Assert.That(requiredGuiPackAssetPaths, Has.Length.EqualTo(9));
            foreach (string assetPath in requiredGuiPackAssetPaths)
            {
                Assert.That(assetPath, Does.StartWith("Assets/UI/GUIPack-Clean&Minimalist"));
            }
            Assert.That(
                InvokeStatic<int>(windowType, "CountRequiredGuiPackAssetsAvailableForTests"),
                Is.EqualTo(0).Or.EqualTo(requiredGuiPackAssetPaths.Length),
                "The local-only GUI pack can be absent in a clean workspace, but a partial install should not pass.");
            Assert.That(InvokeStatic<bool>(windowType, "HasReadableKoreanTextForTests"), Is.True);
        }

        [Test]
        public void Given_SettingsWindowCards_When_SharedFooterIsRemoved_Then_CardsUseFullWindowHeight()
        {
            Type windowType = RequireType(SettingsWindowTypeName);

            Rect viewport = InvokeStatic<Rect>(windowType, "GetOnboardingCardsViewportRectForTests");
            float contentHeight = InvokeStatic<float>(windowType, "GetOnboardingCardsContentHeightForTests");

            Assert.That(
                viewport.yMax,
                Is.EqualTo(675f).Within(0.001f),
                "The onboarding card viewport should use the full reference height after the shared settings footer is removed.");
            Assert.That(
                contentHeight,
                Is.GreaterThan(viewport.height),
                "Three onboarding cards remain scrollable even without the removed shared settings footer.");
            Assert.That(
                InvokeStatic<bool>(windowType, "HasSharedSettingsFooterForTests"),
                Is.False);
        }

        [Test]
        public void Given_SettingsWindowType_When_InspectingRedBoxRemovals_Then_RemovesSharedFooterAndCharacterPlaceholder()
        {
            Type windowType = RequireType(SettingsWindowTypeName);

            string[] visibleText = InvokeStatic<string[]>(windowType, "GetVisibleSettingsWindowTextForTests");

            Assert.That(visibleText, Does.Not.Contain("저장"));
            Assert.That(visibleText, Does.Not.Contain("공유 설정을 불러왔습니다."));
            Assert.That(visibleText, Does.Not.Contain("공유 설정을 저장했습니다."));
            Assert.That(visibleText, Does.Not.Contain("캐릭터"));
            Assert.That(visibleText, Does.Not.Contain("Character 1 (비활성화)"));
            Assert.That(visibleText, Does.Not.Contain("Character 1 (Inactive)"));
        }

        [Test]
        public void Given_SettingsWindowStyleCache_When_PartiallyInitialized_Then_RebuildsAllStyles()
        {
            Type windowType = RequireType(SettingsWindowTypeName);

            try
            {
                ResetSettingsWindowStyleCache(windowType);
                SetStaticField(windowType, "titleStyle", new GUIStyle(EditorStyles.label));

                Assert.DoesNotThrow(() =>
                    InvokeStatic<float>(windowType, "GetCardButtonPreferredWidthForTests", "FBX 파일 선택"));
                Assert.That(GetStaticMemberValue<object>(windowType, "titleStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "sidebarHeaderStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "sidebarItemStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "sidebarInactiveItemStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "cardTitleStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "cardBodyStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "cardButtonStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "iconStyle"), Is.Not.Null);
                Assert.That(GetStaticMemberValue<object>(windowType, "toolbarStyle"), Is.Not.Null);
            }
            finally
            {
                ResetSettingsWindowStyleCache(windowType);
            }
        }

        [Test]
        public void Given_SettingsWindowType_When_InspectingWindowText_Then_UsesReadableKoreanStrings()
        {
            Type windowType = RequireType(SettingsWindowTypeName);

            string[] footerText = InvokeStatic<string[]>(windowType, "GetVisibleSettingsWindowTextForTests");

            foreach (string text in footerText)
            {
                Assert.That(text.IndexOf('\uFFFD'), Is.EqualTo(-1), $"{text} must not contain replacement glyphs.");
                Assert.That(text, Does.Not.Contain("??"));
            }
        }

        [Test]
        public void Given_CompanionController_When_LoadingAndSaving_Then_UsesSharedSettingsStore()
        {
            Type controllerType = RequireType(CompanionControllerTypeName);
            string path = CreateTempSettingsPath();
            var gameObject = new GameObject("Main Recording Settings Companion Controller Test");

            try
            {
                var controller = gameObject.AddComponent(controllerType);

                MainRecordingSettingsDocument loaded =
                    InvokeInstance<MainRecordingSettingsDocument>(controller, "LoadFromPathForTests", path);

                Assert.That(loaded.schemaVersion, Is.EqualTo(1));
                Assert.That(loaded.captureWidth, Is.EqualTo(1920));
                Assert.That(InvokeInstance<bool>(controller, "IsSaveButtonEnabledForTests"), Is.True);

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = "D:/motions/editor-roundtrip.fbx",
                    characterModelPath = "D:/models/future-character.vrm",
                    captureWidth = 2560,
                    captureHeight = 1440,
                    openSettingsOnStart = false,
                };

                InvokeInstance<object>(controller, "SetDocumentForTests", document);
                Assert.That(InvokeInstance<bool>(controller, "SaveCurrentDocumentForTests"), Is.True);

                var store = new MainRecordingSettingsStore(path);
                MainRecordingSettingsDocument roundTrip = store.LoadOrCreateDefault();
                Assert.That(roundTrip.fbxPath, Is.EqualTo(document.fbxPath));
                Assert.That(roundTrip.characterModelPath, Is.EqualTo(document.characterModelPath));
                Assert.That(roundTrip.captureWidth, Is.EqualTo(2560));
                Assert.That(roundTrip.captureHeight, Is.EqualTo(1440));
                Assert.That(roundTrip.openSettingsOnStart, Is.False);
                Assert.That(InvokeInstance<string>(controller, "GetStatusMessageForTests"), Does.Contain("저장"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Given_MainRecordingScene_When_InspectingOnboardingActions_Then_BasicSetupCanReachFbxImporter()
        {
            Type windowType = RequireType(SettingsWindowTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            Assert.That(
                InvokeStatic<bool>(windowType, "CanHandleImportFbxActionForTests"),
                Is.True);
        }

        [Test]
        public void Given_MainRecordingScene_When_ExecutingImportFbxBeforeFileManagerAwake_Then_InitializesFileBrowserAndCancelsSafely()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);
            FileManager fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();
            Assert.That(fileManager, Is.Not.Null);

            var fakeFileBrowser = new CancelFileBrowserService();
            Func<IFileBrowserService> originalFactory =
                GetStaticMemberValue<Func<IFileBrowserService>>(typeof(FileManager), "fileBrowserServiceFactory");

            try
            {
                SetStaticField(
                    typeof(FileManager),
                    "fileBrowserServiceFactory",
                    new Func<IFileBrowserService>(() => fakeFileBrowser));
                SetField(fileManager, "_fileBrowserService", null);
                SetField(fileManager, "_fbxImporter", null);

                bool executed = false;
                Assert.DoesNotThrow(() =>
                {
                    executed = MainRecordingSettingsActions.Execute(
                        MainRecordingSettingsActionType.ImportFbx,
                        null,
                        fileManager);
                });

                Assert.That(executed, Is.True);
                Assert.That(fakeFileBrowser.OpenFilePanelCallCount, Is.EqualTo(1));
                Assert.That(fakeFileBrowser.LastTitle, Is.EqualTo("Import FBX"));
                Assert.That(fakeFileBrowser.LastExtension, Is.EqualTo("fbx"));
                Assert.That(fakeFileBrowser.LastMultiselect, Is.False);
            }
            finally
            {
                SetStaticField(typeof(FileManager), "fileBrowserServiceFactory", originalFactory);
            }
        }

        [Test]
        public void Given_MainRecordingScene_When_OpenSettingsWindow_Then_ResolvesSplitSettingComponents()
        {
            Type windowType = RequireType(SettingsWindowTypeName);
            Type contextType = RequireType(SettingsWindowContextTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            EditorWindow window = null;
            try
            {
                ExpectHeadlessWindowLogsIfNeeded();
                window = (EditorWindow)InvokeStatic<object>(windowType, "OpenForMainRecordingScene");
                Assert.That(window, Is.Not.Null);
                Assert.That(window.GetType(), Is.EqualTo(windowType));
                Assert.That(window.titleContent.text, Is.EqualTo("Onboarding Assistant"));
                Assert.That(window.minSize, Is.EqualTo(new Vector2(1265f, 675f)));
                Assert.That(window.maxSize.x, Is.GreaterThan(window.minSize.x));
                Assert.That(window.maxSize.y, Is.GreaterThan(window.minSize.y));
                Assert.That(window.position.width, Is.EqualTo(1581.25f).Within(1f));
                Assert.That(window.position.height, Is.EqualTo(843.75f).Within(1f));

                object context = InvokeStatic<object>(windowType, "ResolveContext");
                Assert.That(GetMemberValue<Component>(context, "GraphicSetting"), Is.Not.Null);
                Assert.That(GetMemberValue<BackgroundColorSetting>(context, "BackgroundColorSetting"), Is.Not.Null);
                Assert.That(GetMemberValue<RecodingSetting>(context, "RecodingSetting"), Is.Not.Null);
                Assert.That(GetMemberValue<Camera>(context, "TargetCamera"), Is.EqualTo(Camera.main));
                Assert.That(GetMemberValue<bool>(context, "IsComplete"), Is.True);
                Assert.That(context.GetType(), Is.EqualTo(contextType));
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        [Test]
        public void Given_MainRecordingScene_When_EnsuringRuntimeSettingsPopup_Then_CreatesPopupUnderUiCanvas()
        {
            Type popupType = RequireType(RuntimePopupTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            RecodingSetting recodingSetting = UnityEngine.Object.FindObjectOfType<RecodingSetting>();
            Assert.That(recodingSetting, Is.Not.Null, "Main_recoding must keep RecodingSetting on the Setting object.");
            Assert.That(GetField<bool>(recodingSetting, "openSettingsPopupOnStart"), Is.True);

            object popup = InvokeInstance<object>(recodingSetting, "EnsureSettingsPopup");
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.GetType(), Is.EqualTo(popupType));

            var popupComponent = (Component)popup;
            Assert.That(popupComponent.transform.parent, Is.Not.Null);
            Assert.That(popupComponent.transform.parent.name, Is.EqualTo("UI_Canvas"));
            Assert.That(GetField<Component>(recodingSetting, "settingsPopup"), Is.EqualTo(popupComponent));
            Assert.That(InvokeInstance<Vector2>(popup, "GetReferenceSizeForTests"), Is.EqualTo(new Vector2(1265f, 675f)));
            Assert.That(InvokeInstance<Vector2>(popup, "GetDisplayedSizeForTests"),
                Is.EqualTo(new Vector2(1581.25f, 843.75f)));
            Assert.That(InvokeInstance<bool>(popup, "SupportsPointerDragForTests"), Is.True);
            RectTransform popupRect = popupComponent.GetComponent<RectTransform>();
            Vector2 beforeDrag = popupRect.anchoredPosition;
            InvokeInstance<object>(popup, "ApplyDragDeltaForTests", new Vector2(120f, -48f));
            Assert.That(popupRect.anchoredPosition, Is.EqualTo(beforeDrag + new Vector2(120f, -48f)));
            Assert.That(InvokeInstance<int>(popup, "GetCardButtonCountForTests"), Is.EqualTo(3));
            Assert.That(InvokeInstance<bool>(popup, "CanResolveImportActionForTests"), Is.True);
            Assert.That(InvokeInstance<bool>(popup, "UsesCharacterVisualAssetForTests"), Is.False);
            Assert.That(InvokeInstance<bool>(popup, "IsProductionSurfaceForTests"), Is.False);
            Assert.That(InvokeInstance<bool>(popup, "HasReadableKoreanTextForTests"), Is.True);
            Assert.That(
                InvokeInstance<string[]>(popup, "GetSidebarItemLabelsForTests"),
                Is.EqualTo(new[] { "Camera 1", "Environment", "Directional Light" }));
            string[] visibleText = InvokeInstance<string[]>(popup, "GetVisibleTextForTests");
            Assert.That(visibleText, Does.Not.Contain("캐릭터"));
            Assert.That(visibleText, Does.Not.Contain("Character 1 (비활성화)"));
            Assert.That(visibleText, Does.Not.Contain("Character 1 (Inactive)"));
        }

        private static void ExpectHeadlessWindowLogsIfNeeded()
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
            LogAssert.Expect(LogType.Error, "No graphic device is available to show the window.");
            LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
        }

        private static void AssertHeader<T>(string fieldName, string expectedHeader)
        {
            FieldInfo field = RequireField<T>(fieldName);
            HeaderAttribute attribute = field.GetCustomAttribute<HeaderAttribute>();
            Assert.That(attribute, Is.Not.Null, $"{fieldName} must expose a Korean inspector section header.");
            Assert.That(attribute.header, Is.EqualTo(expectedHeader));
        }

        private static void AssertInspectorName<T>(string fieldName, string expectedName)
        {
            FieldInfo field = RequireField<T>(fieldName);
            InspectorNameAttribute attribute = field.GetCustomAttribute<InspectorNameAttribute>();
            Assert.That(attribute, Is.Not.Null, $"{fieldName} must expose a Korean inspector label.");
            Assert.That(attribute.displayName, Is.EqualTo(expectedName));
        }

        private static FieldInfo RequireField<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must exist.");
            return field;
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} must exist.");
            return type;
        }

        private static T InvokeStatic<T>(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} must exist.");
            return (T)method.Invoke(null, args);
        }

        private static T InvokeInstance<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().FullName}.{methodName} must exist.");
            return (T)method.Invoke(target, args);
        }

        private static T GetStaticMemberValue<T>(Type type, string memberName)
        {
            PropertyInfo property = type.GetProperty(
                memberName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return (T)property.GetValue(null);
            }

            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{type.FullName}.{memberName} must exist.");
            return (T)field.GetValue(null);
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{fieldName} must exist.");
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{fieldName} must exist.");
            field.SetValue(instance, value);
        }

        private static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{type.FullName}.{fieldName} must exist.");
            field.SetValue(null, value);
        }

        private static void ResetSettingsWindowStyleCache(Type windowType)
        {
            SetStaticField(windowType, "titleStyle", null);
            SetStaticField(windowType, "sidebarHeaderStyle", null);
            SetStaticField(windowType, "sidebarItemStyle", null);
            SetStaticField(windowType, "sidebarInactiveItemStyle", null);
            SetStaticField(windowType, "cardTitleStyle", null);
            SetStaticField(windowType, "cardBodyStyle", null);
            SetStaticField(windowType, "cardButtonStyle", null);
            SetStaticField(windowType, "iconStyle", null);
            SetStaticField(windowType, "toolbarStyle", null);
        }

        private static T GetMemberValue<T>(object instance, string memberName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return (T)property.GetValue(instance);
            }

            FieldInfo field = instance.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{memberName} must exist.");
            return (T)field.GetValue(instance);
        }

        private static string CreateTempSettingsPath()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "UnityFbx2Vmd-MainRecordingSettingsEditorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "main-recording-settings.json");
        }

        private sealed class CancelFileBrowserService : IFileBrowserService
        {
            public int OpenFilePanelCallCount { get; private set; }
            public string LastTitle { get; private set; }
            public string LastExtension { get; private set; }
            public bool LastMultiselect { get; private set; }

            public string[] OpenFilePanel(string title, string directory, string extension, bool multiselect)
            {
                OpenFilePanelCallCount++;
                LastTitle = title;
                LastExtension = extension;
                LastMultiselect = multiselect;
                return Array.Empty<string>();
            }
        }
    }
}
