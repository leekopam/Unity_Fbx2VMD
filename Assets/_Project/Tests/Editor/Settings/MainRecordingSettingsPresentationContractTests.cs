using System.Collections.Generic;
using System.IO;
using Fbx2Vmd.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Settings
{
    public class MainRecordingSettingsPresentationContractTests
    {
        private const string GuiPackRoot = "Assets/UI/GUIPack-Clean&Minimalist";
        private static readonly string[] RequiredGuiPackAssetPaths =
        {
            GuiPackRoot + "/Demo/Resources/Popups/Light/Light - Settings.prefab",
            GuiPackRoot + "/Demo/Prefabs/UI Elements/Background/Background - Rounded.prefab",
            GuiPackRoot + "/Demo/Prefabs/UI Elements/Button/Button/Rounded/Basic/Filled/Button Rounded - Filled - White.prefab",
            GuiPackRoot + "/Demo/Prefabs/UI Elements/Input Field/Light/Single/InputField.prefab",
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Settings/Settings.png",
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Navigation/Home.png",
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Media/Video Cam.png",
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Basic/Info.png",
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Navigation/List - Menu.png",
        };

        [Test]
        public void Given_SettingsDeliveryPolicy_When_InspectingSurfacePolicy_Then_UsesCompanionAndSharedFile()
        {
            Assert.That(MainRecordingSettingsSurfacePolicy.ProductionSurface, Is.EqualTo("electron web companion"));
            Assert.That(MainRecordingSettingsSurfacePolicy.FallbackSurface, Does.Contain("popup"));

            string policy = MainRecordingSettingsSurfacePolicy.DeliveryPolicy;
            Assert.That(policy, Does.Contain("companion"));
            Assert.That(policy, Does.Contain("Player"));
            Assert.That(policy, Does.Contain("shared settings file"));
            Assert.That(policy, Does.Contain("HTTP"));
            Assert.That(policy, Does.Contain("WebSocket"));
        }

        [Test]
        public void Given_EditorSettingsPolicy_When_InspectingSurfacePolicy_Then_EditorUsesElectronWebLauncherNotGameViewPopup()
        {
            Assert.That(MainRecordingSettingsSurfacePolicy.EditorSurface, Is.EqualTo("electron web launcher"));
            Assert.That(MainRecordingSettingsSurfacePolicy.EditorSurfacePolicy, Does.Contain("Electron"));
            Assert.That(MainRecordingSettingsSurfacePolicy.EditorSurfacePolicy, Does.Contain("Web UI"));
            Assert.That(MainRecordingSettingsSurfacePolicy.EditorSurfacePolicy, Does.Not.Contain("EditorWindow"));
            Assert.That(
                MainRecordingSettingsSurfacePolicy.ShouldAutoOpenRuntimePopup(
                    requestedOpen: true,
                    isEditor: true),
                Is.False);
            Assert.That(
                MainRecordingSettingsSurfacePolicy.ShouldAutoOpenRuntimePopup(
                    requestedOpen: true,
                    isEditor: false),
                Is.True);
            Assert.That(
                MainRecordingSettingsSurfacePolicy.ShouldAutoOpenRuntimePopup(
                    requestedOpen: true,
                    isEditor: false,
                    isBatchMode: true),
                Is.False);
        }

        [Test]
        public void Given_LocalGuiPackFixture_When_InspectingAvailability_Then_IsAbsentOrComplete()
        {
            int availableAssetCount = 0;
            foreach (string assetPath in RequiredGuiPackAssetPaths)
            {
                Assert.That(assetPath, Does.StartWith(GuiPackRoot), assetPath);
                if (File.Exists(assetPath))
                {
                    availableAssetCount++;
                }
            }

            Assert.That(
                availableAssetCount,
                Is.EqualTo(0).Or.EqualTo(RequiredGuiPackAssetPaths.Length),
                "The local-only GUI pack can be absent in a clean workspace, but a partial install should not pass.");
        }

        [Test]
        public void Given_SettingsCards_When_InspectingState_Then_KeepsAtLeastTwoDisabledCards()
        {
            int disabledCardCount = 0;
            foreach (MainRecordingSettingsCardSpec card in MainRecordingSettingsLayoutSpec.Cards)
            {
                if (!card.Enabled)
                {
                    disabledCardCount++;
                }
            }

            Assert.That(disabledCardCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Given_SettingsCards_When_InspectingVisibleText_Then_GuardsAgainstBrokenKoreanText()
        {
            var visibleText = new List<string>();
            foreach (MainRecordingSettingsCardSpec card in MainRecordingSettingsLayoutSpec.Cards)
            {
                visibleText.Add(card.Title);
                visibleText.Add(card.Body);
                visibleText.Add(card.ButtonLabel);
            }

            Assert.That(visibleText, Is.Not.Empty);
            foreach (string text in visibleText)
            {
                Assert.That(text, Does.Match("[가-힣]"));
                Assert.That(text.IndexOf('\uFFFD'), Is.EqualTo(-1));
                Assert.That(text, Does.Not.Contain("??"));
            }
        }

        [Test]
        public void Given_RuntimePopup_When_InspectingDirectLabels_Then_KeepsReadableKoreanText()
        {
            var popupObject = new GameObject(
                "Runtime Popup Presentation Contract",
                typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                string[] visibleText = popup.GetVisibleTextForTests();

                Assert.That(visibleText, Does.Contain("시네마토그래피"));
                Assert.That(visibleText, Does.Contain("환경"));
                foreach (string text in visibleText)
                {
                    Assert.That(text.IndexOf('\uFFFD'), Is.EqualTo(-1));
                    Assert.That(text, Does.Not.Contain("??"));
                }
            }
            finally
            {
                Object.DestroyImmediate(popupObject);
            }
        }
    }
}
