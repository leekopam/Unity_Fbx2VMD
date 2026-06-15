using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Tests.Editor.VMDRecorderSample
{
    public class MotionComparisonProbeReportWriterTests
    {
        [Test]
        public void Given_ScreenshotIndexRow_When_BuildCsvLine_Then_EscapesQuotesAndCommas()
        {
            MotionComparisonProbeScreenshotIndexRow row = new MotionComparisonProbeScreenshotIndexRow(
                comparisonLabel: "hello, \"world\"",
                sceneName: "scene",
                reason: "reason",
                recorderFrame: 12,
                viewName: "front",
                relativePath: "Local/ComparisonFrames/a.png");

            string line = MotionComparisonProbeReportWriter.BuildScreenshotIndexRowCsvLine(row);

            Assert.That(line, Is.EqualTo("\"hello, \"\"world\"\"\",scene,reason,12,front,Local/ComparisonFrames/a.png"));
        }

        [Test]
        public void Given_NewMotionComparisonProbe_When_InspectingFullBodyScreenshotPadding_Then_MatchesReferenceMp4LongShotFraming()
        {
            var probeObject = new GameObject("MotionComparisonProbe padding test");
            try
            {
                var probe = probeObject.AddComponent<MotionComparisonProbe>();
                FieldInfo field = typeof(MotionComparisonProbe).GetField(
                    "screenshotPadding",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(field, Is.Not.Null, "MotionComparisonProbe must expose its full-body screenshot padding as a serialized field.");
                Assert.That((float)field.GetValue(probe), Is.EqualTo(1.8f).Within(0.0001f),
                    "Full-body comparison captures must use the measured MP4 long-shot scale instead of zooming the model to the frame.");

                FieldInfo anchorField = typeof(MotionComparisonProbe).GetField(
                    "screenshotVerticalViewportCenter",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(anchorField, Is.Not.Null, "MotionComparisonProbe must expose its full-body vertical viewport anchor as a serialized field.");
                Assert.That((float)anchorField.GetValue(probe), Is.EqualTo(0.28f).Within(0.0001f),
                    "Full-body comparison captures must use the measured MP4 lower-stage anchor instead of centering the model vertically.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probeObject);
            }
        }

        [Test]
        public void Given_MetricsCsvValues_When_FormattersRun_Then_UseInvariantCsvAndBlankInvalidNumbers()
        {
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvText("hello, \"world\""), Is.EqualTo("\"hello, \"\"world\"\"\""));
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvFloat(1.2345678f), Is.EqualTo("1.234568"));
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvFloat(float.NaN), Is.Empty);
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvFloat(float.PositiveInfinity), Is.Empty);
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvVector(new Vector3(1.25f, float.NaN, -2.5f)), Is.EqualTo("1.25||-2.5"));
        }

        [Test]
        public void Given_CsvInteger_When_FormatCsvInt_Then_UsesInvariantDigits()
        {
            Assert.That(MotionComparisonProbeReportWriter.FormatCsvInt(0), Is.EqualTo("0"));
            Assert.That(MotionComparisonProbeReportWriter.FormatCsvInt(-12), Is.EqualTo("-12"));
            Assert.That(MotionComparisonProbeReportWriter.FormatCsvInt(123456), Is.EqualTo("123456"));
        }

        [Test]
        public void Given_TransformInstanceIds_When_BuildTransformPairKey_Then_UsesStableDelimitedInvariantIds()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildTransformPairKey("left-thumb", 42, -7),
                Is.EqualTo("left-thumb:42:-7"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildTransformPairKey("", 0, 0),
                Is.EqualTo(":0:0"));
        }

        [Test]
        public void Given_Transforms_When_BuildTransformPairKey_Then_UsesInstanceIdsAndNullFallback()
        {
            GameObject first = new GameObject("first");
            GameObject second = new GameObject("second");

            try
            {
                Assert.That(
                    MotionComparisonProbeReportWriter.BuildTransformPairKey("left-thumb", first.transform, second.transform),
                    Is.EqualTo("left-thumb:" + first.transform.GetInstanceID() + ":" + second.transform.GetInstanceID()));
                Assert.That(
                    MotionComparisonProbeReportWriter.BuildTransformPairKey(null, first.transform, null),
                    Is.EqualTo(":" + first.transform.GetInstanceID() + ":0"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Given_ThumbHelperPairKeySide_When_BuildLabels_Then_UsesStableDiagnosticLabels()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbHelperDistancePairKeyLabel(isRightSide: false),
                Is.EqualTo("thumb-helper-distance-left"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbHelperDistancePairKeyLabel(isRightSide: true),
                Is.EqualTo("thumb-helper-distance-right"));

            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbHelperRotationPairKeyLabel(isRightSide: false),
                Is.EqualTo("thumb-helper-rotation-left"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbHelperRotationPairKeyLabel(isRightSide: true),
                Is.EqualTo("thumb-helper-rotation-right"));
        }

        [Test]
        public void Given_ThumbDiagnosticTransformSide_When_BuildCacheKeys_Then_UsesStableLookupLabels()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildExplicitThumbBaseSourceCacheKey(isRightSide: false),
                Is.EqualTo("thumb-explicit-source-left"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildExplicitThumbBaseSourceCacheKey(isRightSide: true),
                Is.EqualTo("thumb-explicit-source-right"));

            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbBaseHelperCacheKey(isRightSide: false),
                Is.EqualTo("thumb-helper-left"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbBaseHelperCacheKey(isRightSide: true),
                Is.EqualTo("thumb-helper-right"));

            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbBaseSourceCacheKey(isRightSide: false),
                Is.EqualTo("thumb-source-left"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildThumbBaseSourceCacheKey(isRightSide: true),
                Is.EqualTo("thumb-source-right"));
        }

        [Test]
        public void Given_DiagnosticTransformSide_When_BuildSideToken_Then_UsesStableNameLookupToken()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildDiagnosticTransformSideToken(isRightSide: false),
                Is.EqualTo("left"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildDiagnosticTransformSideToken(isRightSide: true),
                Is.EqualTo("right"));
        }

        [Test]
        public void Given_DiagnosticTransformNameAndSide_When_MatchesSideToken_Then_UsesStableLeftRightContainment()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDiagnosticTransformSide("YYB_Left_Thumb0M", isRightSide: false),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDiagnosticTransformSide("YYB_Right_Thumb0M", isRightSide: true),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDiagnosticTransformSide("YYB_Left_Thumb0M", isRightSide: true),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDiagnosticTransformSide(null, isRightSide: false),
                Is.False);
        }

        [Test]
        public void Given_DiagnosticTransformName_When_NormalizeName_Then_UsesInvariantLowercaseAndEmptyFallback()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.NormalizeDiagnosticTransformName("YYB Thumb_Left!"),
                Is.EqualTo("yyb thumb_left!"));
            Assert.That(
                MotionComparisonProbeReportWriter.NormalizeDiagnosticTransformName(null),
                Is.EqualTo(""));
            Assert.That(
                MotionComparisonProbeReportWriter.NormalizeDiagnosticTransformName(""),
                Is.EqualTo(""));
        }

        [Test]
        public void Given_ModelName_When_MatchesYybModelName_Then_UsesNormalizedYybToken()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesYybModelName("YYB Hatsune Miku"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesYybModelName("yyb_miku_variant"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesYybModelName("Tda Hatsune Miku"),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesYybModelName(null),
                Is.False);
        }

        [Test]
        public void Given_ThumbDiagnosticTransformName_When_MatchNamePredicates_Then_UsesStableLookupRules()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesThumbBaseName("Left_Thumb0_Base"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesThumbBaseName("left_thumb1"),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesThumbBaseName("left_thumbtip"),
                Is.False);

            Assert.That(
                MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceName("Left_Thumb0M"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceName("left_thumb0m_ghost"),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceName("left_thumb0"),
                Is.False);

            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperName("left_thumb0_helper"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperName("left_thumb0m"),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperName("left_thumb0!"),
                Is.False);

            Assert.That(
                MotionComparisonProbeReportWriter.MatchesAmbiguousThumbExtraTransformCandidateName("left_thumb_extra"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesAmbiguousThumbExtraTransformCandidateName("left_thumb3"),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesAmbiguousThumbExtraTransformCandidateName("left_proximal_thumb"),
                Is.False);
        }

        [Test]
        public void Given_ThumbDiagnosticTransformNameAndSide_When_MatchTransformPredicates_Then_RequiresSideAndNameRules()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceTransformName("YYB_Left_Thumb0M", isRightSide: false),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceTransformName("YYB_Left_Thumb0M", isRightSide: true),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceTransformName("YYB_Left_Thumb0M_Ghost", isRightSide: false),
                Is.False);

            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperTransformName("YYB_Right_Thumb0_Helper", isRightSide: true),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperTransformName("YYB_Right_Thumb0_Helper", isRightSide: false),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperTransformName("YYB_Right_Thumb0M", isRightSide: true),
                Is.False);
        }

        [Test]
        public void Given_SleeveAnchorPairKeySide_When_BuildLabel_Then_UsesStableDiagnosticLabel()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildSleeveAnchorRotationPairKeyLabel(isRightSide: false),
                Is.EqualTo("sleeve-anchor-rotation-left"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildSleeveAnchorRotationPairKeyLabel(isRightSide: true),
                Is.EqualTo("sleeve-anchor-rotation-right"));
        }

        [Test]
        public void Given_SleeveAnchorLookupSide_When_BuildLabels_Then_UsesStableTransformLookupLabels()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildSleeveAnchorTransformNameSuffix(isRightSide: false),
                Is.EqualTo("joint_LeftArmM"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildSleeveAnchorTransformNameSuffix(isRightSide: true),
                Is.EqualTo("joint_RightArmM"));

            Assert.That(
                MotionComparisonProbeReportWriter.BuildSleeveAnchorTransformCacheKey(isRightSide: false),
                Is.EqualTo("sleeve-anchor-joint_LeftArmM"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildSleeveAnchorTransformCacheKey(isRightSide: true),
                Is.EqualTo("sleeve-anchor-joint_RightArmM"));
        }

        [Test]
        public void Given_TransformNameSuffix_When_MatchesSuffix_Then_AllowsExactAndDottedUnityNames()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesTransformNameSuffix("joint_LeftArmM", "joint_LeftArmM"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesTransformNameSuffix("root.spine.joint_LeftArmM", "joint_LeftArmM"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesTransformNameSuffix("prefix_joint_LeftArmM", "joint_LeftArmM"),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesTransformNameSuffix("joint_LeftArmM_child", "joint_LeftArmM"),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesTransformNameSuffix("", "joint_LeftArmM"),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesTransformNameSuffix("joint_LeftArmM", ""),
                Is.False);
        }

        [Test]
        public void Given_SleeveAnchorTransformNameAndSide_When_MatchesSleeveAnchorTransformName_Then_UsesStableSideSuffixRules()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesSleeveAnchorTransformName("root.spine.joint_LeftArmM", isRightSide: false),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesSleeveAnchorTransformName("root.spine.joint_LeftArmM", isRightSide: true),
                Is.False);

            Assert.That(
                MotionComparisonProbeReportWriter.MatchesSleeveAnchorTransformName("prefix_joint_RightArmM", isRightSide: true),
                Is.True);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesSleeveAnchorTransformName("joint_RightArmM_child", isRightSide: true),
                Is.False);
            Assert.That(
                MotionComparisonProbeReportWriter.MatchesSleeveAnchorTransformName("", isRightSide: false),
                Is.False);
        }

        [Test]
        public void Given_SamplingStartupWarnings_When_BuildMessages_Then_UsesStableDiagnosticText()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildAnimatorMissingWarningMessage(),
                Is.EqualTo("[MotionComparisonProbe] Animator is missing; comparison sampling cannot start."));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildNonZeroRecorderFrameStartWarningMessage(12),
                Is.EqualTo("[MotionComparisonProbe] comparison sampling started at recorderFrame=12. Use only sessions that start at frame 0 for Main/Sub motion comparison."));
        }

        [Test]
        public void Given_HumanoidArmMuscleWarning_When_BuildMessage_Then_UsesStableDiagnosticText()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildMissingHumanoidArmMusclesWarningMessage(),
                Is.EqualTo("[MotionComparisonProbe] some Humanoid arm muscle indices were not found; matching CSV values will be blank."));
        }

        [Test]
        public void Given_MetricsCsvInteger_When_FormatMetricsCsvInt_Then_UsesInvariantDigits()
        {
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvInt(0), Is.EqualTo("0"));
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvInt(-12), Is.EqualTo("-12"));
            Assert.That(MotionComparisonProbeReportWriter.FormatMetricsCsvInt(123456), Is.EqualTo("123456"));
        }

        [Test]
        public void Given_FormattedMetricsCsvValues_When_BuildMetricsCsvLine_Then_JoinsColumnsInOrder()
        {
            string line = MotionComparisonProbeReportWriter.BuildMetricsCsvLine(
                MotionComparisonProbeReportWriter.FormatMetricsCsvText("label, one"),
                MotionComparisonProbeReportWriter.FormatMetricsCsvFloat(1.25f),
                MotionComparisonProbeReportWriter.FormatMetricsCsvInt(-3),
                MotionComparisonProbeReportWriter.FormatMetricsCsvVector(new Vector3(1f, 2f, 3f)));

            Assert.That(line, Is.EqualTo("\"label, one\",1.25,-3,1|2|3"));
        }

        [Test]
        public void Given_LabelInputs_When_BuildComparisonLabel_Then_PrefersOverrideAndSanitizesFallbacks()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildComparisonLabel("existing label", "", "Game Object"),
                Is.EqualTo("existing_label"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildComparisonLabel("existing label", "override label:1", "Game Object"),
                Is.EqualTo("override_label_1"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildComparisonLabel("", "", "Game Object"),
                Is.EqualTo("Game_Object"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildComparisonLabel("", "", ""),
                Is.EqualTo("motion_comparison"));
        }

        [Test]
        public void Given_CaptureCameraLabel_When_BuildObjectName_Then_UsesStablePrefixAndFallback()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildCaptureCameraObjectName("Main/Sub"),
                Is.EqualTo("MotionComparisonCapture_Main/Sub"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildCaptureCameraObjectName(""),
                Is.EqualTo("MotionComparisonCapture_motion_comparison"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildCaptureCameraObjectName(null),
                Is.EqualTo("MotionComparisonCapture_motion_comparison"));
        }

        [Test]
        public void Given_AnimationTimeSources_When_BuildLabels_Then_UsesStableMetricsValues()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildRetargeterLegacyAnimationTimeSourceLabel(),
                Is.EqualTo("retargeterLegacy"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildRetargeterLegacyRecorderFrameAnimationTimeSourceLabel(),
                Is.EqualTo("retargeterLegacyRecorderFrame"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildAnimatorStateAnimationTimeSourceLabel(),
                Is.EqualTo("animatorState"));
            Assert.That(
                MotionComparisonProbeReportWriter.BuildUnknownAnimationTimeSourceLabel(),
                Is.Empty);
        }

        [Test]
        public void Given_ScreenshotIndexRow_When_AppendRow_Then_WritesCsvRow()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string indexPath = Path.Combine(projectRoot, "index.csv");
            Directory.CreateDirectory(projectRoot);

            try
            {
                MotionComparisonProbeReportWriter.AppendScreenshotIndexRow(
                    indexPath,
                    new MotionComparisonProbeScreenshotIndexRow(
                        "label",
                        "scene",
                        "reason",
                        42,
                        "front",
                        "Docs/Machine_Spirit/Local/ComparisonFrames/frame.png"));

                string content = File.ReadAllText(indexPath);

                Assert.That(content, Is.EqualTo("label,scene,reason,42,front,Docs/Machine_Spirit/Local/ComparisonFrames/frame.png" + Environment.NewLine));
            }
            finally
            {
                if (Directory.Exists(projectRoot))
                {
                    Directory.Delete(projectRoot, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ScreenshotIndexRowPathWithMissingParent_When_AppendRow_Then_CreatesParentAndWritesRow()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string indexPath = Path.Combine(projectRoot, "nested", "index.csv");

            try
            {
                MotionComparisonProbeReportWriter.AppendScreenshotIndexRow(
                    indexPath,
                    new MotionComparisonProbeScreenshotIndexRow(
                        "label",
                        "scene",
                        "reason",
                        42,
                        "front",
                        "Docs/Machine_Spirit/Local/ComparisonFrames/frame.png"));

                Assert.That(Directory.Exists(Path.GetDirectoryName(indexPath)), Is.True);
                string content = File.ReadAllText(indexPath);
                Assert.That(content, Is.EqualTo("label,scene,reason,42,front,Docs/Machine_Spirit/Local/ComparisonFrames/frame.png" + Environment.NewLine));
            }
            finally
            {
                if (Directory.Exists(projectRoot))
                {
                    Directory.Delete(projectRoot, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ReportWriterBoundary_When_CheckedForOutputPathHelpers_Then_DoesNotExposePathNamingMethods()
        {
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            string[] pathHelperNames =
            {
                "BuildScreenshotPngFileName",
                "BuildScreenshotFrameName",
                "BuildSampleScreenshotViewName",
                "BuildFingerCloseupViewName",
                "BuildEvidenceBaseName",
                "BuildMetricsEvidenceBaseName",
                "BuildComparisonSessionIdBaseName",
                "BuildSessionManifestFileName",
                "BuildFrameSessionIndexFileName",
                "BuildMetricsCsvExtension",
                "BuildMetricsCsvFileName"
            };

            foreach (string methodName in pathHelperNames)
            {
                Assert.That(
                    typeof(MotionComparisonProbeReportWriter).GetMethod(methodName, flags),
                    Is.Null,
                    $"{methodName} belongs in {nameof(MotionComparisonProbeOutputPaths)}.");
            }
        }

        [Test]
        public void Given_FrameSessionIndexData_When_BuildMarkdown_Then_EscapesBackticksAndPipes()
        {
            MotionComparisonProbeFrameSessionIndexData data = new MotionComparisonProbeFrameSessionIndexData(
                sessionId: "a`b|c",
                sessionManifestRelativePath: "Local/Logs/idx.md",
                metricsCsvRelativePath: "Local/Logs/metrics.csv",
                frameIndexCsvRelativePath: "Local/Frames/index.csv");

            string markdown = MotionComparisonProbeReportWriter.BuildFrameSessionIndexMarkdown(data);

            Assert.That(markdown, Does.Contain("- session id: `a'b\\|c`"));
            Assert.That(markdown, Does.Contain("Local/Logs/metrics.csv"));
        }

        [Test]
        public void Given_ScreenshotIndexCsvHeader_When_BuildHeader_Then_KeepsColumnOrderAndCount()
        {
            string header = MotionComparisonProbeReportWriter.BuildScreenshotIndexCsvHeader();
            string[] columns = header.Split(',');

            Assert.That(columns.Length, Is.EqualTo(6));
            Assert.That(columns[0], Is.EqualTo("label"));
            Assert.That(columns[1], Is.EqualTo("scene"));
            Assert.That(columns[2], Is.EqualTo("reason"));
            Assert.That(columns[3], Is.EqualTo("recorderFrame"));
            Assert.That(columns[4], Is.EqualTo("view"));
            Assert.That(columns[5], Is.EqualTo("path"));
        }

        [Test]
        public void Given_ScreenshotIndexCsvPath_When_WriteHeader_Then_WritesHeaderLine()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                MotionComparisonProbeReportWriter.WriteScreenshotIndexCsvHeader(path);

                string content = File.ReadAllText(path);

                Assert.That(content, Is.EqualTo("label,scene,reason,recorderFrame,view,path" + Environment.NewLine));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void Given_ScreenshotSessionFilePaths_When_WriteSessionFiles_Then_WritesCsvHeaderAndMarkdownIndex()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string indexPath = Path.Combine(root, "index.csv");
            string sessionIndexPath = Path.Combine(root, "session_index.md");
            Directory.CreateDirectory(root);

            try
            {
                MotionComparisonProbeReportWriter.WriteScreenshotSessionFiles(
                    indexPath,
                    sessionIndexPath,
                    new MotionComparisonProbeFrameSessionIndexData(
                        sessionId: "session-a",
                        sessionManifestRelativePath: "Local/Sessions/index.md",
                        metricsCsvRelativePath: "Local/Logs/metrics.csv",
                        frameIndexCsvRelativePath: "Local/Frames/index.csv"));

                Assert.That(File.ReadAllText(indexPath), Is.EqualTo("label,scene,reason,recorderFrame,view,path" + Environment.NewLine));
                string markdown = File.ReadAllText(sessionIndexPath);
                Assert.That(markdown, Does.Contain("- session id: `session-a`"));
                Assert.That(markdown, Does.Contain("- frame index: `Local/Frames/index.csv`"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ScreenshotSessionFilePathsWithMissingParent_When_WriteSessionFiles_Then_CreatesParentAndWritesFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string indexPath = Path.Combine(root, "nested", "index.csv");
            string sessionIndexPath = Path.Combine(root, "nested", "session_index.md");

            try
            {
                MotionComparisonProbeReportWriter.WriteScreenshotSessionFiles(
                    indexPath,
                    sessionIndexPath,
                    new MotionComparisonProbeFrameSessionIndexData(
                        sessionId: "session-b",
                        sessionManifestRelativePath: "Local/Sessions/index.md",
                        metricsCsvRelativePath: "Local/Logs/metrics.csv",
                        frameIndexCsvRelativePath: "Local/Frames/index.csv"));

                Assert.That(Directory.Exists(Path.GetDirectoryName(indexPath)), Is.True);
                Assert.That(File.ReadAllText(indexPath), Is.EqualTo("label,scene,reason,recorderFrame,view,path" + Environment.NewLine));
                string markdown = File.ReadAllText(sessionIndexPath);
                Assert.That(markdown, Does.Contain("- session id: `session-b`"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SampleTimes_When_FormatSampleTimes_Then_UsesInvariantCompactValues()
        {
            string sampleTimes = MotionComparisonProbeReportWriter.FormatSampleTimes(new[] { 0f, 1.25f, 2.5f });

            Assert.That(sampleTimes, Is.EqualTo("0, 1.25, 2.5"));
        }

        [Test]
        public void Given_SampleTime_When_BuildSampleTimeReason_Then_UsesCompactReasonLabel()
        {
            Assert.That(MotionComparisonProbeReportWriter.BuildSampleTimeReason(0f), Is.EqualTo("t0"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSampleTimeReason(1.25f), Is.EqualTo("t1.25"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSampleTimeReason(13.2345f), Is.EqualTo("t13.235"));
        }

        [Test]
        public void Given_SamplingLifecycle_When_BuildReasonLabels_Then_UsesStableManifestAndSampleReasons()
        {
            Assert.That(MotionComparisonProbeReportWriter.BuildSessionStartedReason(), Is.EqualTo("started"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSamplingStartReason(), Is.EqualTo("start"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSamplingStopReason(), Is.EqualTo("stop"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSamplingDefaultReason(), Is.EqualTo("sample"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSamplingDisabledReason(), Is.EqualTo("disabled"));
        }

        [Test]
        public void Given_RealtimeRiskEvaluation_When_BuildReasonLabel_Then_UsesStableValue()
        {
            Assert.That(MotionComparisonProbeReportWriter.BuildRealtimeRiskEvaluationReason(), Is.EqualTo("realtime"));
        }

        [Test]
        public void Given_SampleLogValues_When_BuildSampleLogMessage_Then_UsesInvariantFixedPrecision()
        {
            string message = MotionComparisonProbeReportWriter.BuildSampleLogMessage(
                comparisonLabel: "yyb label",
                reason: "t1.25",
                elapsed: 1.234f,
                animationClipTime: 2.3456f,
                recorderFrame: 12,
                hipsY: -0.1254f,
                cameraFacingDot: 0.9876f,
                maxScaleDelta: 0.00123f,
                yybRisk: float.NaN);

            Assert.That(
                message,
                Is.EqualTo("[MotionComparisonProbe] yyb label t1.25 t=1.23s clip=2.346s frame=12 hipsY=-0.125 facing=0.988 scaleDelta=0.0012 yybRisk=NaN"));
        }

        [Test]
        public void Given_ScreenshotWarningValues_When_BuildWarningMessages_Then_UsesStableDiagnosticText()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildScreenshotBoundsUnavailableWarningMessage(
                    comparisonLabel: "yyb label",
                    reason: "t1.25"),
                Is.EqualTo("[MotionComparisonProbe] screenshot skipped: render bounds unavailable label=yyb label reason=t1.25"));

            Assert.That(
                MotionComparisonProbeReportWriter.BuildScreenshotBlankWarningMessage(
                    path: "C:/tmp/pose.png"),
                Is.EqualTo("[MotionComparisonProbe] screenshot render produced blank/no evidence: C:/tmp/pose.png"));
        }

        [Test]
        public void Given_NoSampleTimes_When_FormatSampleTimes_Then_ReturnsEmptyString()
        {
            Assert.That(MotionComparisonProbeReportWriter.FormatSampleTimes(null), Is.Empty);
            Assert.That(MotionComparisonProbeReportWriter.FormatSampleTimes(new float[0]), Is.Empty);
        }

        [Test]
        public void Given_SampleClockMode_When_BuildSampleClockLabel_Then_UsesManifestLabels()
        {
            Assert.That(MotionComparisonProbeReportWriter.BuildSampleClockLabel(sampleByAnimationClipTime: true), Is.EqualTo("animationClipTime"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSampleClockLabel(sampleByAnimationClipTime: false), Is.EqualTo("elapsed"));
        }

        [Test]
        public void Given_SessionTimestamp_When_BuildSessionTimeLabels_Then_UsesInvariantManifestFormats()
        {
            DateTime timestamp = new DateTime(2026, 5, 25, 7, 8, 9, DateTimeKind.Local);

            Assert.That(MotionComparisonProbeReportWriter.BuildSessionStamp(timestamp), Is.EqualTo("20260525-070809"));
            Assert.That(MotionComparisonProbeReportWriter.BuildSessionUpdatedAt(timestamp), Is.EqualTo("2026-05-25 07:08:09"));
        }

        [Test]
        public void Given_MetricsCsvHeader_When_BuildHeader_Then_KeepsColumnOrderAndCount()
        {
            string[] columns = MotionComparisonProbeReportWriter.BuildMetricsCsvHeader().Split(',');

            Assert.That(columns.Length, Is.EqualTo(241));
            Assert.That(columns[0], Is.EqualTo("label"));
            Assert.That(columns[1], Is.EqualTo("scene"));
            Assert.That(columns[2], Is.EqualTo("reason"));
            Assert.That(columns[140], Is.EqualTo("rightLittleProximalLocalEuler"));
            Assert.That(columns[141], Is.EqualTo("leftThumbIndexSpreadAngle"));
            Assert.That(columns[columns.Length - 1], Is.EqualTo("thumbGuardWebbingMaxPositionOffset"));
        }

        [Test]
        public void Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSleeveThicknessDiagnostics()
        {
            string[] columns = MotionComparisonProbeReportWriter.BuildMetricsCsvHeader().Split(',');
            int sleeveAnchorIndex = Array.IndexOf(columns, "rightSleeveAnchorRisk");

            Assert.That(sleeveAnchorIndex, Is.GreaterThan(0));
            Assert.That(columns[sleeveAnchorIndex + 1], Is.EqualTo("leftSleeveAnchorDistance"));
            Assert.That(columns[sleeveAnchorIndex + 2], Is.EqualTo("rightSleeveAnchorDistance"));
            Assert.That(columns[sleeveAnchorIndex + 3], Is.EqualTo("leftSleeveThicknessRatio"));
            Assert.That(columns[sleeveAnchorIndex + 4], Is.EqualTo("rightSleeveThicknessRatio"));
            Assert.That(columns[sleeveAnchorIndex + 5], Is.EqualTo("leftSleeveThicknessRisk"));
            Assert.That(columns[sleeveAnchorIndex + 6], Is.EqualTo("rightSleeveThicknessRisk"));
            Assert.That(columns[sleeveAnchorIndex + 7], Is.EqualTo("leftYybDeformationRisk"));
        }

        [Test]
        public void Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesGroundingStepLimitDiagnostics()
        {
            string[] columns = MotionComparisonProbeReportWriter.BuildMetricsCsvHeader().Split(',');
            int maxStepIndex = Array.IndexOf(columns, "retargetGroundingMaxStepPerFrame");

            Assert.That(maxStepIndex, Is.GreaterThan(0));
            Assert.That(columns[maxStepIndex + 1], Is.EqualTo("retargetGroundingLastStepToMaxStepRatio"));
            Assert.That(columns[maxStepIndex + 2], Is.EqualTo("retargetGroundingLastStepAtMaxStep"));
            Assert.That(Array.IndexOf(columns, "leftThumbIndexSpreadAngle"), Is.GreaterThan(maxStepIndex));
        }

        [Test]
        public void Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesHipsYContributionDiagnostics()
        {
            string[] columns = MotionComparisonProbeReportWriter.BuildMetricsCsvHeader().Split(',');
            int hipsIndex = Array.IndexOf(columns, "hipsY");

            Assert.That(hipsIndex, Is.GreaterThan(0));
            Assert.That(columns[hipsIndex - 3], Is.EqualTo("bodyPositionY"));
            Assert.That(columns[hipsIndex - 2], Is.EqualTo("hipsLocalY"));
            Assert.That(columns[hipsIndex - 1], Is.EqualTo("retargetFootHeightReferenceLift"));
        }

        [Test]
        public void Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRecordingStartHipsBaselineDiagnostics()
        {
            string[] columns = MotionComparisonProbeReportWriter.BuildMetricsCsvHeader().Split(',');
            int bodyIndex = Array.IndexOf(columns, "bodyPositionY");

            Assert.That(bodyIndex, Is.GreaterThan(0));
            Assert.That(columns[bodyIndex - 9], Is.EqualTo("retargetRecordingStartRootY"));
            Assert.That(columns[bodyIndex - 8], Is.EqualTo("retargetRecordingStartBodyPositionY"));
            Assert.That(columns[bodyIndex - 7], Is.EqualTo("retargetRecordingStartHipsLocalY"));
            Assert.That(columns[bodyIndex - 6], Is.EqualTo("retargetRecordingStartHipsY"));
            Assert.That(columns[bodyIndex - 5], Is.EqualTo("retargetRecordingStartHipsReferenceBeforeLocalY"));
            Assert.That(columns[bodyIndex - 4], Is.EqualTo("retargetRecordingStartHipsReferenceAfterLocalY"));
            Assert.That(columns[bodyIndex - 3], Is.EqualTo("retargetRecordingStartHipsReferenceDeltaY"));
            Assert.That(columns[bodyIndex - 2], Is.EqualTo("retargetRecordingStartHipsReferenceFlipDetected"));
            Assert.That(columns[bodyIndex - 1], Is.EqualTo("retargetRecordingStartHipsReferenceStage"));
        }

        [Test]
        public void Given_MetricsCsvPath_When_AppendLine_Then_WritesLineWithNewline()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                MotionComparisonProbeReportWriter.AppendMetricsCsvLine(path, "label,scene,reason");

                string content = File.ReadAllText(path);

                Assert.That(content, Is.EqualTo("label,scene,reason" + Environment.NewLine));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void Given_MetricsCsvPathWithMissingParent_When_WriteHeaderAndAppendLine_Then_CreatesParentAndWritesLines()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "nested", "metrics.csv");

            try
            {
                MotionComparisonProbeReportWriter.WriteMetricsCsvHeader(path);
                MotionComparisonProbeReportWriter.AppendMetricsCsvLine(path, "label,scene,reason");

                Assert.That(Directory.Exists(Path.GetDirectoryName(path)), Is.True);
                string[] lines = File.ReadAllLines(path);
                Assert.That(lines.Length, Is.EqualTo(2));
                Assert.That(lines[0], Does.StartWith("label,scene,reason,elapsed"));
                Assert.That(lines[1], Is.EqualTo("label,scene,reason"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ScreenshotPngBytes_When_WriteBytes_Then_WritesFileAndReturnsTrue()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N") + ".png");
            byte[] bytes = { 0x89, 0x50, 0x4E, 0x47 };

            try
            {
                bool wrote = MotionComparisonProbeReportWriter.WriteScreenshotPngBytes(path, bytes);

                Assert.That(wrote, Is.True);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void Given_ScreenshotPngPathWithMissingParent_When_WriteBytes_Then_CreatesParentAndWritesFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "nested", "sample.png");
            byte[] bytes = { 0x89, 0x50, 0x4E, 0x47 };

            try
            {
                bool wrote = MotionComparisonProbeReportWriter.WriteScreenshotPngBytes(path, bytes);

                Assert.That(wrote, Is.True);
                Assert.That(Directory.Exists(Path.GetDirectoryName(path)), Is.True);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_TextureSamples_When_IsScreenshotTextureBlank_Then_DetectsUniformAndVariedContent()
        {
            Texture2D blank = CreateFilledTexture(24, 24, new Color32(12, 12, 12, 255));
            Texture2D varied = CreateFilledTexture(24, 24, new Color32(12, 12, 12, 255));

            try
            {
                varied.SetPixel(12, 12, new Color32(40, 12, 12, 255));
                varied.Apply();

                Assert.That(MotionComparisonProbeReportWriter.IsScreenshotTextureBlank(blank), Is.True);
                Assert.That(MotionComparisonProbeReportWriter.IsScreenshotTextureBlank(varied), Is.False);
                Assert.That(MotionComparisonProbeReportWriter.IsScreenshotTextureBlank(null), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blank);
                UnityEngine.Object.DestroyImmediate(varied);
            }
        }

        [Test]
        public void Given_TextureAndPath_When_WriteNonBlankScreenshotPng_Then_WritesOnlyNonBlankImage()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string blankPath = Path.Combine(root, "blank.png");
            string variedPath = Path.Combine(root, "varied.png");
            Directory.CreateDirectory(root);
            Texture2D blank = CreateFilledTexture(24, 24, new Color32(12, 12, 12, 255));
            Texture2D varied = CreateFilledTexture(24, 24, new Color32(12, 12, 12, 255));

            try
            {
                varied.SetPixel(12, 12, new Color32(40, 12, 12, 255));
                varied.Apply();

                bool wroteBlank = MotionComparisonProbeReportWriter.WriteNonBlankScreenshotPng(blankPath, blank);
                bool wroteVaried = MotionComparisonProbeReportWriter.WriteNonBlankScreenshotPng(variedPath, varied);

                Assert.That(wroteBlank, Is.False);
                Assert.That(File.Exists(blankPath), Is.False);
                Assert.That(wroteVaried, Is.True);
                Assert.That(new FileInfo(variedPath).Length, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blank);
                UnityEngine.Object.DestroyImmediate(varied);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SessionManifestData_When_BuildMarkdown_Then_ContainsOutputsTable()
        {
            MotionComparisonProbeSessionManifestData data = new MotionComparisonProbeSessionManifestData(
                sessionId: "s1",
                comparisonLabel: "label",
                sceneName: "scene",
                stateReason: "started",
                createdAt: "2026-05-15 00:00:00",
                updatedAt: "2026-05-15 00:00:01",
                screenshotsEnabled: true,
                sampleClock: "elapsed",
                sampleTimes: "0, 1",
                yybDiagnosticOnlyMetrics: false,
                riskEvaluationFrameCount: 10,
                leftThumbCoreCoverageFrameCount: 1,
                rightThumbCoreCoverageFrameCount: 2,
                leftThumbHelperCoverageRequired: true,
                rightThumbHelperCoverageRequired: false,
                leftThumbHelperCoverageFrameCount: 3,
                rightThumbHelperCoverageFrameCount: 4,
                maxGenericThumbAnatomyRisk: 0.1f,
                maxGenericThumbAnatomyRiskReason: "r",
                maxGenericThumbAnatomyRiskClipTime: 0.2f,
                maxGenericThumbAnatomyRiskRecorderFrame: 5,
                maxThumbSpreadRisk: 0.3f,
                maxThumbProjectionRisk: 0.4f,
                maxThumbHelperSeparationRisk: 0.5f,
                maxThumbWebbingRisk: 0.6f,
                maxYybDeformationRisk: 0.7f,
                maxYybDeformationRiskReason: "yyb",
                maxYybDeformationRiskClipTime: 0.8f,
                maxYybDeformationRiskRecorderFrame: 9,
                leftThumbProjectionGuardWeight: 0.11f,
                rightThumbProjectionGuardWeight: 0.12f,
                leftThumbIndexSpreadGuardWeight: 0.13f,
                rightThumbIndexSpreadGuardWeight: 0.14f,
                leftThumbSegmentStraightenGuardWeight: 0.15f,
                rightThumbSegmentStraightenGuardWeight: 0.16f,
                metricsCsvRelativePath: "Local/ComparisonLogs/metrics.csv",
                frameFolderRelativePath: "Local/ComparisonFrames",
                frameIndexCsvRelativePath: "Local/ComparisonFrames/index.csv",
                frameSessionIndexRelativePath: "Local/ComparisonFrames/session_index.md");

            string markdown = MotionComparisonProbeReportWriter.BuildSessionManifestMarkdown(data);

            Assert.That(markdown, Does.Contain("## 산출물"));
            Assert.That(markdown, Does.Contain("| metrics csv |"));
            Assert.That(markdown, Does.Contain("Local/ComparisonFrames/session_index.md"));
        }

        [Test]
        public void Given_SessionManifestPathWithMissingParent_When_WriteMarkdown_Then_CreatesParentAndWritesFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string manifestPath = Path.Combine(root, "nested", "index.md");
            MotionComparisonProbeSessionManifestData data = new MotionComparisonProbeSessionManifestData(
                sessionId: "s2",
                comparisonLabel: "label",
                sceneName: "scene",
                stateReason: "started",
                createdAt: "2026-05-15 00:00:00",
                updatedAt: "2026-05-15 00:00:01",
                screenshotsEnabled: true,
                sampleClock: "elapsed",
                sampleTimes: "0, 1",
                yybDiagnosticOnlyMetrics: false,
                riskEvaluationFrameCount: 10,
                leftThumbCoreCoverageFrameCount: 1,
                rightThumbCoreCoverageFrameCount: 2,
                leftThumbHelperCoverageRequired: true,
                rightThumbHelperCoverageRequired: false,
                leftThumbHelperCoverageFrameCount: 3,
                rightThumbHelperCoverageFrameCount: 4,
                maxGenericThumbAnatomyRisk: 0.1f,
                maxGenericThumbAnatomyRiskReason: "r",
                maxGenericThumbAnatomyRiskClipTime: 0.2f,
                maxGenericThumbAnatomyRiskRecorderFrame: 5,
                maxThumbSpreadRisk: 0.3f,
                maxThumbProjectionRisk: 0.4f,
                maxThumbHelperSeparationRisk: 0.5f,
                maxThumbWebbingRisk: 0.6f,
                maxYybDeformationRisk: 0.7f,
                maxYybDeformationRiskReason: "yyb",
                maxYybDeformationRiskClipTime: 0.8f,
                maxYybDeformationRiskRecorderFrame: 9,
                leftThumbProjectionGuardWeight: 0.11f,
                rightThumbProjectionGuardWeight: 0.12f,
                leftThumbIndexSpreadGuardWeight: 0.13f,
                rightThumbIndexSpreadGuardWeight: 0.14f,
                leftThumbSegmentStraightenGuardWeight: 0.15f,
                rightThumbSegmentStraightenGuardWeight: 0.16f,
                metricsCsvRelativePath: "Local/ComparisonLogs/metrics.csv",
                frameFolderRelativePath: "Local/ComparisonFrames",
                frameIndexCsvRelativePath: "Local/ComparisonFrames/index.csv",
                frameSessionIndexRelativePath: "Local/ComparisonFrames/session_index.md");

            try
            {
                MotionComparisonProbeReportWriter.WriteSessionManifestMarkdown(manifestPath, data);

                Assert.That(Directory.Exists(Path.GetDirectoryName(manifestPath)), Is.True);
                string markdown = File.ReadAllText(manifestPath);
                Assert.That(markdown, Does.Contain("s2"));
                Assert.That(markdown, Does.Contain("| metrics csv |"));
                Assert.That(markdown, Does.Contain("Local/ComparisonFrames/session_index.md"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SessionManifestArtifactsHeadingWithoutTable_When_AppendExportedVmd_Then_RecreatesArtifactsTableUnderHeading()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string manifestPath = Path.Combine(root, "index.md");
            Directory.CreateDirectory(root);
            string artifactsHeading = FindSessionManifestArtifactsHeading();

            try
            {
                File.WriteAllText(
                    manifestPath,
                    "# Session" + Environment.NewLine + Environment.NewLine +
                    artifactsHeading + Environment.NewLine + Environment.NewLine +
                    "## Next" + Environment.NewLine +
                    "body" + Environment.NewLine,
                    Encoding.UTF8);

                MotionComparisonProbeReportWriter.TryAppendExportedVmdToSessionManifest(
                    manifestPath,
                    "Assets/VMDRecorderSample/out.vmd",
                    frameCount: 12,
                    fileSizeBytes: 34);

                string content = File.ReadAllText(manifestPath);
                string exportedRow = "| exported vmd | `Assets/VMDRecorderSample/out.vmd` (frames=12, bytes=34) |";
                int headingIndex = content.IndexOf(artifactsHeading, StringComparison.Ordinal);
                int separatorIndex = content.IndexOf("|---|---|", StringComparison.Ordinal);
                int rowIndex = content.IndexOf(exportedRow, StringComparison.Ordinal);
                int nextHeadingIndex = content.IndexOf("## Next", StringComparison.Ordinal);

                Assert.That(separatorIndex, Is.GreaterThan(headingIndex));
                Assert.That(rowIndex, Is.GreaterThan(separatorIndex));
                Assert.That(rowIndex, Is.LessThan(nextHeadingIndex));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ExportedVmdArtifactInputs_When_BuildRow_Then_EscapesPathAndIncludesOptionalStats()
        {
            Assert.That(
                MotionComparisonProbeReportWriter.BuildExportedVmdArtifactRow(
                    "Assets/VMDRecorderSample/out`a|b.vmd",
                    frameCount: 12,
                    fileSizeBytes: 34),
                Is.EqualTo("| exported vmd | `Assets/VMDRecorderSample/out'a\\|b.vmd` (frames=12, bytes=34) |"));

            Assert.That(
                MotionComparisonProbeReportWriter.BuildExportedVmdArtifactRow(
                    "Assets/VMDRecorderSample/out.vmd",
                    frameCount: 0,
                    fileSizeBytes: 0),
                Is.EqualTo("| exported vmd | `Assets/VMDRecorderSample/out.vmd` |"));
        }

        [Test]
        public void Given_TwoMetricsCsvs_When_BuildFrameQualitySummary_Then_ComparesSameRecorderFramesAndReportsSpikes()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 0f, 1f, 0f, 1.2f, 0.02f, 0.02f, 0.01f, 0.01f, 0.003f),
                    Row("manual", 1, 0.1f, 1f, 0f, 1.22f, 0.03f, 0.03f, 0.02f, 0.02f, 0.004f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 0.02f, 1f, 0f, 1.19f, 0.01f, 0.01f, 0.05f, 0.06f, 0.006f),
                    Row("main", 1, 0.42f, 1f, 0f, 1.08f, -0.04f, -0.04f, 0.31f, 0.29f, 0.04f),
                    Row("main", 2, 1.0f, 1f, 0f, 1.1f, -0.02f, -0.02f, 0.31f, 0.29f, 0.04f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        "",
                        baselineRecordedFrameCount: 11,
                        candidateRecordedFrameCount: 21,
                        targetFrameCount: 20);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.baseline_metric_frames, Is.EqualTo(2));
                Assert.That(summary.candidate_metric_frames, Is.EqualTo(3));
                Assert.That(summary.compared_frames, Is.EqualTo(2));
                Assert.That(summary.missing_baseline_frames, Is.EqualTo(1));
                Assert.That(summary.candidate_below_floor_metric_frames, Is.EqualTo(2));
                Assert.That(summary.min_candidate_foot_bottom_y, Is.EqualTo(-0.04f).Within(0.0001f));
                Assert.That(summary.min_candidate_foot_bottom_ground_gap, Is.EqualTo(-0.04f).Within(0.0001f));
                Assert.That(summary.max_same_frame_root_position_delta, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(summary.max_same_frame_hips_y_delta, Is.EqualTo(0.13f).Within(0.0001f));
                Assert.That(summary.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.06f).Within(0.0001f));
                Assert.That(summary.max_candidate_root_step, Is.EqualTo(0.58f).Within(0.0001f));
                Assert.That(summary.candidate_retarget_root_delta_max, Is.EqualTo(0.31f).Within(0.0001f));
                Assert.That(summary.candidate_retarget_pose_delta_max, Is.EqualTo(0.29f).Within(0.0001f));
                Assert.That(summary.candidate_grounding_vertical_step_max, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(summary.candidate_frame_count_delta_from_target, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SparseMetricsCsvs_When_BuildFrameQualitySummary_Then_DoesNotTreatSampleGapAsOneFrameTeleport()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("manual", 300, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("main", 300, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0.01f, 0f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("pass"));
                Assert.That(summary.compared_frames, Is.EqualTo(2));
                Assert.That(summary.candidate_root_step_spike_frames, Is.EqualTo(0));
                Assert.That(summary.max_candidate_root_step, Is.NaN);
                Assert.That(summary.max_same_frame_root_position_delta, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MetricSamplesDriftByOneRecorderFrame_When_BuildFrameQualitySummary_Then_ComparesNearestFrame()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 0f, 0f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("manual", 900, 0f, 0f, 0f, 1.01f, 0.03f, 0.03f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 0f, 0f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("main", 901, 0.01f, 0f, 0f, 1.011f, 0.031f, 0.031f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0.01f, 0f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 902,
                        candidateRecordedFrameCount: 902,
                        targetFrameCount: 902);

                Assert.That(summary.status, Is.EqualTo("pass"));
                Assert.That(summary.compared_frames, Is.EqualTo(2));
                Assert.That(summary.missing_baseline_frames, Is.EqualTo(0));
                Assert.That(summary.missing_candidate_frames, Is.EqualTo(0));
                Assert.That(summary.max_same_frame_root_position_delta, Is.EqualTo(0.01f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SparseMetricsCsvsWithSameFrameRootDelta_When_BuildFrameQualitySummary_Then_FailsGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("manual", 300, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("main", 300, 3f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0.01f, 0f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("same-frame root position delta threshold exceeded"));
                Assert.That(summary.compared_frames, Is.EqualTo(2));
                Assert.That(summary.candidate_root_step_spike_frames, Is.EqualTo(0));
                Assert.That(summary.max_candidate_root_step, Is.NaN);
                Assert.That(summary.max_same_frame_root_position_delta, Is.EqualTo(3f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_YybCandidateRiskColumnWithoutFiniteValues_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-yyb.csv");
            string vmdPath = Path.Combine(root, "main-yyb.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithYybRisk(
                    baselinePath,
                    YybRiskRow("Sub_Manual testPrefab", 0, "0"),
                    YybRiskRow("Sub_Manual testPrefab", 300, "0"));
                WriteMetricsCsvWithYybRisk(
                    candidatePath,
                    YybRiskRow("Main_Recoding YYB", 0, ""),
                    YybRiskRow("Main_Recoding YYB", 300, ""));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual testPrefab",
                        baselinePath,
                        "Main_Recoding YYB",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("YYB deformation risk diagnostic missing"));
                Assert.That(GetSummaryField<bool>(summary, "candidate_yyb_deformation_risk_column_present"), Is.True);
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_deformation_risk_frame_count"), Is.EqualTo(0));
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_deformation_risk_missing_frames"), Is.EqualTo(2));
                Assert.That(GetSummaryField<float>(summary, "candidate_yyb_max_deformation_risk"), Is.NaN);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_YybCandidateWithoutRiskColumn_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-yyb.csv");
            string vmdPath = Path.Combine(root, "main-yyb.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("Sub_Manual testPrefab", 0, 0f, 1f, 0f, 1f, 0.1f, 0.1f, 0f, 0f, 0f),
                    Row("Sub_Manual testPrefab", 300, 0f, 1f, 0f, 1f, 0.1f, 0.1f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("Main_Recoding YYB", 0, 0f, 1f, 0f, 1f, 0.1f, 0.1f, 0f, 0f, 0f),
                    Row("Main_Recoding YYB", 300, 0f, 1f, 0f, 1f, 0.1f, 0.1f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual testPrefab",
                        baselinePath,
                        "Main_Recoding YYB",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("YYB deformation risk diagnostic missing"));
                Assert.That(GetSummaryField<bool>(summary, "candidate_yyb_deformation_risk_column_present"), Is.False);
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_deformation_risk_missing_frames"), Is.EqualTo(2));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_YybCandidateSleeveThicknessRiskWithoutFiniteValues_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-yyb.csv");
            string vmdPath = Path.Combine(root, "main-yyb.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    baselinePath,
                    YybRiskAndSleeveThicknessRow("Sub_Manual YYB", 0, "0", "0", "0"),
                    YybRiskAndSleeveThicknessRow("Sub_Manual YYB", 300, "0", "0", "0"));
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    candidatePath,
                    YybRiskAndSleeveThicknessRow("Main_Auto YYB", 0, "0.1", "", ""),
                    YybRiskAndSleeveThicknessRow("Main_Auto YYB", 300, "0.1", "", ""));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual YYB",
                        baselinePath,
                        "Main_Auto YYB",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("YYB sleeve thickness diagnostic missing"));
                Assert.That(GetSummaryField<bool>(summary, "candidate_yyb_sleeve_thickness_risk_column_present"), Is.True);
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_sleeve_thickness_risk_frame_count"), Is.EqualTo(0));
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_sleeve_thickness_risk_missing_frames"), Is.EqualTo(2));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_YybCandidateSleeveThicknessRiskExceedsThreshold_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-yyb.csv");
            string vmdPath = Path.Combine(root, "main-yyb.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    baselinePath,
                    YybRiskAndSleeveThicknessRow("Sub_Manual YYB", 0, "0", "0", "0"),
                    YybRiskAndSleeveThicknessRow("Sub_Manual YYB", 300, "0", "0", "0"));
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    candidatePath,
                    YybRiskAndSleeveThicknessRow("Main_Auto YYB", 0, "0.1", "0.2", "0.1"),
                    YybRiskAndSleeveThicknessRow("Main_Auto YYB", 300, "0.1", "0.4", "0.37"));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual YYB",
                        baselinePath,
                        "Main_Auto YYB",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("YYB sleeve thickness risk threshold exceeded"));
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_sleeve_thickness_risk_frame_count"), Is.EqualTo(2));
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_sleeve_thickness_risk_missing_frames"), Is.EqualTo(0));
                Assert.That(GetSummaryField<float>(summary, "candidate_yyb_max_sleeve_thickness_risk"), Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_YybCandidateRiskExceedsThreshold_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-yyb.csv");
            string vmdPath = Path.Combine(root, "main-yyb.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithYybRisk(
                    baselinePath,
                    YybRiskRow("Sub_Manual YYB", 0, "0"),
                    YybRiskRow("Sub_Manual YYB", 300, "0"));
                WriteMetricsCsvWithYybRisk(
                    candidatePath,
                    YybRiskRow("Main_Auto YYB", 0, "0.1"),
                    YybRiskRow("Main_Auto YYB", 300, "0.42"));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual YYB",
                        baselinePath,
                        "Main_Auto YYB",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("YYB deformation risk threshold exceeded"));
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_deformation_risk_frame_count"), Is.EqualTo(2));
                Assert.That(GetSummaryField<int>(summary, "candidate_yyb_deformation_risk_missing_frames"), Is.EqualTo(0));
                Assert.That(GetSummaryField<float>(summary, "candidate_yyb_max_deformation_risk"), Is.EqualTo(0.42f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MainRecordingMovingRootStageMotion_When_MarkingIntentionalRootPath_Then_AllowsRootPathDeltaOnly()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-recording.csv");
            string vmdPath = Path.Combine(root, "main-recording.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    baselinePath,
                    RowWithYybAndSleeve("manual", 0, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"),
                    RowWithYybAndSleeve("manual", 300, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"));
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    candidatePath,
                    RowWithYybAndSleeve("main-recording", 0, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"),
                    RowWithYybAndSleeve("main-recording", 300, 0.75f, 1f, 0f, 1f, 0.146f, 0.146f, 0.08f, 0f, 0f, "0", "0", "0"));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual testPrefab",
                        baselinePath,
                        "Main_Recoding YYB 자동 경로",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("same-frame root position delta threshold exceeded"));
                Assert.That(summary.max_same_frame_root_position_delta, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(summary.candidate_retarget_root_delta_max, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(summary.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.066f).Within(0.0001f));

                MotionComparisonProbeReportWriter.MarkIntentionalMovingRootStageMotion(summary);

                Assert.That(summary.status, Is.EqualTo("pass"));
                Assert.That(summary.status_reason, Does.Contain("intentional moving-root stage path"));
                Assert.That(summary.frame_quality_evaluation_role, Is.EqualTo("main_recording_moving_root_metrics"));
                Assert.That(summary.max_same_frame_root_position_delta, Is.EqualTo(0.75f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MainRecordingMovingRootStageMotionWithRetargetRootSpike_When_MarkingIntentionalRootPath_Then_StillFails()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-recording.csv");
            string vmdPath = Path.Combine(root, "main-recording.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    baselinePath,
                    RowWithYybAndSleeve("manual", 0, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"),
                    RowWithYybAndSleeve("manual", 300, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"));
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    candidatePath,
                    RowWithYybAndSleeve("main-recording", 0, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"),
                    RowWithYybAndSleeve("main-recording", 300, 0.75f, 1f, 0f, 1f, 0.146f, 0.146f, 0.152133f, 0f, 0f, "0", "0", "0"));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual testPrefab",
                        baselinePath,
                        "Main_Recoding YYB 자동 경로",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                MotionComparisonProbeReportWriter.MarkIntentionalMovingRootStageMotion(summary);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("one-frame root/center/IK teleport threshold exceeded"));
                Assert.That(summary.status_reason, Does.Not.Contain("moving-root retarget root delta"));
                Assert.That(summary.candidate_retarget_root_delta_max, Is.EqualTo(0.152133f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MainRecordingMovingRootStageMotionWithFloorFailure_When_MarkingIntentionalRootPath_Then_StillFails()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main-recording.csv");
            string vmdPath = Path.Combine(root, "main-recording.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    baselinePath,
                    RowWithYybAndSleeve("manual", 0, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"),
                    RowWithYybAndSleeve("manual", 300, 0f, 1f, 0f, 1f, 0.08f, 0.08f, 0f, 0f, 0f, "0", "0", "0"));
                WriteMetricsCsvWithYybRiskAndSleeveThickness(
                    candidatePath,
                    RowWithYybAndSleeve("main-recording", 0, 0f, 1f, 0f, 1f, -0.03f, -0.03f, 0f, 0f, 0f, "0", "0", "0"),
                    RowWithYybAndSleeve("main-recording", 300, 0.75f, 1f, 0f, 1f, -0.02f, -0.02f, 0f, 0f, 0f, "0", "0", "0"));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 300, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, -0.03f, 0f),
                    VmdFrame("LeftFootIK", 300, 0.01f, -0.03f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "Sub_Manual testPrefab",
                        baselinePath,
                        "Main_Recoding YYB 자동 경로",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                MotionComparisonProbeReportWriter.MarkIntentionalMovingRootStageMotion(summary);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.status_reason, Does.Contain("below-floor foot/IK sample detected"));
                Assert.That(summary.status_reason, Does.Not.Contain("same-frame root position delta threshold exceeded"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ConstantSceneRootOffset_When_BuildFrameQualitySummary_Then_DoesNotFailRootDeltaGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 1f, 0f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("manual", 300, 1f, 0f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 5f, 0f, -2f, 1f, 0.02f, 0.02f, 0f, 0f, 0f),
                    Row("main", 300, 5f, 0f, -2f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0.01f, 0f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("pass"));
                Assert.That(summary.max_same_frame_root_position_delta, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ConstantVerticalModelOffset_When_BuildFrameQualitySummary_Then_DoesNotWarnHipsOrFootGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 0f, 0f, 0f, 1.075f, 0.072f, 0.072f, 0f, 0f, 0f),
                    Row("manual", 300, 0f, 0f, 0f, 1.055f, 0.056f, 0.056f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 0f, 0f, 0f, 1.025f, 0.064f, 0.064f, 0f, 0f, 0f),
                    Row("main", 300, 0f, 0f, 0f, 1.005f, 0.048f, 0.048f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0.01f, 0f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("pass"));
                Assert.That(summary.max_same_frame_hips_y_delta, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(summary.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_RelativeVerticalDriftExceedsTolerance_When_BuildFrameQualitySummary_Then_WarnsGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 0f, 0f, 0f, 1.075f, 0.072f, 0.072f, 0f, 0f, 0f),
                    Row("manual", 300, 0f, 0f, 0f, 1.055f, 0.056f, 0.056f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 0f, 0f, 0f, 1.025f, 0.064f, 0.064f, 0f, 0f, 0f),
                    Row("main", 300, 0f, 0f, 0f, 0.955f, 0.008f, 0.008f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0.01f, 0f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 301,
                        candidateRecordedFrameCount: 301,
                        targetFrameCount: 301);

                Assert.That(summary.status, Is.EqualTo("warn"));
                Assert.That(summary.status_reason, Does.Contain("same-frame hips Y delta warning threshold exceeded"));
                Assert.That(summary.status_reason, Does.Contain("same-frame foot bottom Y delta warning threshold exceeded"));
                Assert.That(summary.max_same_frame_hips_y_delta, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(summary.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.04f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SameFrameHipsAndFootDeltasExceedWarnThreshold_When_BuildFrameQualitySummary_Then_WarnsGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(
                    baselinePath,
                    Row("manual", 0, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMetricsCsv(
                    candidatePath,
                    Row("main", 0, 0f, 1f, 0f, 1.05f, 0.06f, 0.06f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0.01f, 0f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 1,
                        candidateRecordedFrameCount: 1,
                        targetFrameCount: 1);

                Assert.That(summary.status, Is.EqualTo("warn"));
                Assert.That(summary.status_reason, Does.Contain("same-frame hips Y delta warning threshold exceeded"));
                Assert.That(summary.status_reason, Does.Contain("same-frame foot bottom Y delta warning threshold exceeded"));
                Assert.That(summary.max_same_frame_hips_y_delta, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(summary.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.04f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_HipsContributionColumns_When_BuildFrameQualitySummary_Then_ReportsOffsetNormalizedHipsYContributors()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1f, 0f, 0.50f, 0.20f, 0.010f, 0.000f, 1.20f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 30, 0f, 1f, 0f, 0.55f, 0.25f, 0.020f, 0.030f, 1.25f, 0.21f, 0.21f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.10f, 0f, 0.60f, 0.30f, 0.015f, 0.010f, 1.40f, 0.25f, 0.25f),
                    HipsContributionRow("main", 30, 0f, 1.12f, 0f, 0.66f, 0.38f, 0.040f, 0.050f, 1.57f, 0.27f, 0.27f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        candidateVmdPath: "",
                        baselineRecordedFrameCount: 31,
                        candidateRecordedFrameCount: 31,
                        targetFrameCount: 31);

                Assert.That(summary.max_same_frame_hips_y_delta, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(GetSummaryField<int>(summary, "max_same_frame_hips_y_delta_recorder_frame"), Is.EqualTo(30));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_root_y_delta"), Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_body_position_y_delta"), Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_hips_local_y_delta"), Is.EqualTo(0.03f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_grounding_vertical_step_delta"), Is.EqualTo(0.015f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_foot_height_reference_lift_delta"), Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_hips_y_delta_root_y_component"), Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_hips_y_delta_body_position_y_component"), Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_hips_y_delta_hips_local_y_component"), Is.EqualTo(0.03f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "max_same_frame_hips_y_delta_foot_bottom_y_delta_at_frame"), Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(GetSummaryField<int>(summary, "max_same_frame_foot_bottom_y_delta_recorder_frame"), Is.EqualTo(30));
                Assert.That(GetSummaryField<int>(summary, "max_same_frame_foot_bottom_y_delta_candidate_recorder_frame"), Is.EqualTo(30));
                Assert.That(GetSummaryField<string>(summary, "same_frame_hips_y_contribution_basis"), Does.Contain("offset-normalized"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_RecordingStartHipsBaselineColumns_When_BuildFrameQualitySummary_Then_ReportsCandidateStartAndFlip()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithRecordingStartHipsBaseline(
                    baselinePath,
                    RecordingStartHipsBaselineRow(
                        "manual", 0, "start",
                        rootY: 1.0f,
                        bodyPositionY: 0.50f,
                        hipsLocalY: 0.90f,
                        hipsY: 1.20f,
                        recordingStartRootY: float.NaN,
                        recordingStartBodyPositionY: float.NaN,
                        recordingStartHipsLocalY: float.NaN,
                        recordingStartHipsY: float.NaN,
                        referenceBeforeLocalY: float.NaN,
                        referenceAfterLocalY: float.NaN,
                        referenceDeltaY: float.NaN,
                        flipDetected: -1,
                        stage: ""));
                WriteMetricsCsvWithRecordingStartHipsBaseline(
                    candidatePath,
                    RecordingStartHipsBaselineRow(
                        "main", 0, "start",
                        rootY: 1.1f,
                        bodyPositionY: 0.60f,
                        hipsLocalY: 0.80f,
                        hipsY: 1.40f,
                        recordingStartRootY: 1.1f,
                        recordingStartBodyPositionY: 0.60f,
                        recordingStartHipsLocalY: 0.80f,
                        recordingStartHipsY: 1.40f,
                        referenceBeforeLocalY: 0.829f,
                        referenceAfterLocalY: 0.800f,
                        referenceDeltaY: -0.029f,
                        flipDetected: 1,
                        stage: "prewarm-complete"));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        candidateVmdPath: "",
                        baselineRecordedFrameCount: 1,
                        candidateRecordedFrameCount: 1,
                        targetFrameCount: 1);

                Assert.That(GetSummaryField<int>(summary, "candidate_first_recorded_recorder_frame"), Is.EqualTo(0));
                Assert.That(GetSummaryField<float>(summary, "candidate_first_recorded_root_y"), Is.EqualTo(1.1f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_first_recorded_body_position_y"), Is.EqualTo(0.60f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_first_recorded_hips_local_y"), Is.EqualTo(0.80f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_first_recorded_hips_y"), Is.EqualTo(1.40f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_recording_start_root_y"), Is.EqualTo(1.1f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_recording_start_body_position_y"), Is.EqualTo(0.60f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_recording_start_hips_local_y"), Is.EqualTo(0.80f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_recording_start_hips_y"), Is.EqualTo(1.40f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_recording_start_hips_reference_before_local_y"), Is.EqualTo(0.829f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_recording_start_hips_reference_after_local_y"), Is.EqualTo(0.800f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "candidate_recording_start_hips_reference_delta_y"), Is.EqualTo(-0.029f).Within(0.0001f));
                Assert.That(GetSummaryField<int>(summary, "candidate_recording_start_hips_reference_flip_detected"), Is.EqualTo(1));
                Assert.That(GetSummaryField<string>(summary, "candidate_recording_start_hips_reference_stage"), Is.EqualTo("prewarm-complete"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_FrameSpecificVerticalSolveCandidate_When_BuildFrameQualitySummary_Then_ReportsBoundedPrototypeWithoutChangingActualGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 1800, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.12f, 0.302f, 0.302f),
                    HipsContributionRow("main", 1802, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                Assert.That(summary.status, Is.EqualTo("fail"), "The prototype must not hide the current measured frame_quality gate.");
                Assert.That(summary.status_reason, Does.Contain("same-frame foot bottom Y delta fail threshold exceeded"));
                Assert.That(summary.missing_baseline_frames, Is.EqualTo(1));
                Assert.That(summary.missing_candidate_frames, Is.EqualTo(1));
                Assert.That(GetSummaryField<string>(summary, "vertical_solve_prototype_status"), Is.EqualTo("pass"));
                Assert.That(
                    GetSummaryField<string>(summary, "vertical_solve_prototype_status_reason"),
                    Does.Contain("projected frame-specific vertical solve stayed within thresholds"));
                Assert.That(GetSummaryField<float>(summary, "vertical_solve_prototype_max_same_frame_hips_y_delta"), Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "vertical_solve_prototype_max_same_frame_foot_bottom_y_delta"), Is.EqualTo(0.035f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "vertical_solve_prototype_max_same_frame_root_position_delta"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetSummaryField<int>(summary, "vertical_solve_prototype_below_floor_metric_frames"), Is.EqualTo(0));
                Assert.That(GetSummaryField<int>(summary, "vertical_solve_prototype_target_frame_count"), Is.EqualTo(901));
                Assert.That(GetSummaryField<int>(summary, "vertical_solve_prototype_candidate_recorded_frame_count"), Is.EqualTo(901));
                Assert.That(GetSummaryField<int>(summary, "vertical_solve_prototype_hips_correction_recorder_frame"), Is.EqualTo(900));
                Assert.That(GetSummaryField<int>(summary, "vertical_solve_prototype_foot_correction_recorder_frame"), Is.EqualTo(900));
                Assert.That(GetSummaryField<float>(summary, "vertical_solve_prototype_hips_correction_y"), Is.EqualTo(-0.03f).Within(0.0001f));
                Assert.That(GetSummaryField<float>(summary, "vertical_solve_prototype_foot_correction_y"), Is.EqualTo(-0.017f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_FrameSpecificVerticalSolveCandidate_When_BuildFrameQualitySummary_Then_WritesPostprocessedMetricsWithoutHidingActualGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string expectedPostprocessPath = Path.Combine(root, "main.vertical_solve_postprocess.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 1800, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.12f, 0.302f, 0.302f),
                    HipsContributionRow("main", 1800, 0f, 1.05f, 0f, 0.62f, 0.58f, 0.000f, 0.000f, 1.12f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("Center", 1800, 0.02f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 1800, 0.02f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                Assert.That(summary.status, Is.EqualTo("fail"), "The postprocess artifact must not hide the current measured gate.");
                Assert.That(summary.status_reason, Does.Contain("same-frame foot bottom Y delta fail threshold exceeded"));
                Assert.That(GetSummaryField<string>(summary, "vertical_solve_postprocess_status"), Is.EqualTo("pass"));
                Assert.That(
                    GetSummaryField<string>(summary, "vertical_solve_postprocess_status_reason"),
                    Does.Contain("postprocessed frame-specific vertical solve stayed within thresholds"));
                Assert.That(GetSummaryField<string>(summary, "vertical_solve_postprocess_metrics_csv"), Is.EqualTo(expectedPostprocessPath));
                Assert.That(File.Exists(expectedPostprocessPath), Is.True);
                MotionComparisonFrameQualitySummary postprocessed =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        expectedPostprocessPath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);
                Assert.That(postprocessed.status, Is.EqualTo("pass"));
                Assert.That(postprocessed.max_same_frame_hips_y_delta, Is.EqualTo(0.0395f).Within(0.0001f));
                Assert.That(postprocessed.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.0345f).Within(0.0001f));
                Assert.That(postprocessed.candidate_below_floor_metric_frames, Is.EqualTo(0));
                Assert.That(postprocessed.target_frame_count, Is.EqualTo(901));
                Assert.That(postprocessed.candidate_recorded_frame_count, Is.EqualTo(901));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_FinishSamplesUseDifferentFrameCounts_When_BuildFrameQualitySummary_Then_PostprocessHasFullEvidence()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithRecordingStartHipsBaseline(
                    baselinePath,
                    RecordingStartHipsBaselineRow("manual", 0, "start", 0f, 0.50f, 0.50f, 1.00f, 0f, 0.50f, 0.50f, 1.00f, 0f, 0f, 0f, 0, ""),
                    RecordingStartHipsBaselineRow("manual", 1800, "t60", 0f, 0.50f, 0.50f, 1.00f, 0f, 0.50f, 0.50f, 1.00f, 0f, 0f, 0f, 0, ""),
                    RecordingStartHipsBaselineRow("manual", 6234, "finish", 0f, 0.50f, 0.50f, 1.00f, 0f, 0.50f, 0.50f, 1.00f, 0f, 0f, 0f, 0, ""));
                WriteMetricsCsvWithRecordingStartHipsBaseline(
                    candidatePath,
                    RecordingStartHipsBaselineRow("main", 0, "start", 0f, 0.55f, 0.55f, 1.05f, 0f, 0.55f, 0.55f, 1.05f, 0f, 0f, 0f, 0, ""),
                    RecordingStartHipsBaselineRow("main", 1801, "t60", 0f, 0.55f, 0.55f, 1.12f, 0f, 0.55f, 0.55f, 1.05f, 0f, 0f, 0f, 0, ""),
                    RecordingStartHipsBaselineRow("main", 6001, "finish", 0f, 0.55f, 0.55f, 1.05f, 0f, 0.55f, 0.55f, 1.05f, 0f, 0f, 0f, 0, ""));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 6234,
                        candidateRecordedFrameCount: 6001,
                        targetFrameCount: 6001);

                Assert.That(summary.status, Is.EqualTo("warn"));
                Assert.That(summary.status_reason, Does.Contain("same-frame hips Y delta warning threshold exceeded"));
                Assert.That(summary.missing_baseline_frames, Is.EqualTo(0));
                Assert.That(summary.missing_candidate_frames, Is.EqualTo(0));
                Assert.That(GetSummaryField<string>(summary, "vertical_solve_postprocess_status"), Is.EqualTo("pass"));
                Assert.That(
                    GetSummaryField<string>(summary, "vertical_solve_postprocess_status_reason"),
                    Does.Contain("postprocessed frame-specific vertical solve stayed within thresholds"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_PostprocessMetricsArtifact_When_BuildingFrameQualitySummary_Then_ReturnsSeparatePassingEvaluationEntry()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 1800, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.12f, 0.302f, 0.302f),
                    HipsContributionRow("main", 1800, 0f, 1.05f, 0f, 0.62f, 0.58f, 0.000f, 0.000f, 1.12f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("Center", 1800, 0.02f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 1800, 0.02f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                Assert.That(raw.status, Is.EqualTo("fail"));
                Assert.That(
                    MotionComparisonProbeReportWriter.TryBuildVerticalSolvePostprocessFrameQualitySummary(
                        raw,
                        out MotionComparisonFrameQualitySummary postprocessed),
                    Is.True);

                Assert.That(postprocessed.status, Is.EqualTo("pass"));
                Assert.That(postprocessed.candidate_label, Is.EqualTo("main vertical_solve_postprocess"));
                Assert.That(
                    GetSummaryField<string>(postprocessed, "frame_quality_evaluation_role"),
                    Is.EqualTo("vertical_solve_postprocess_metrics"));
                Assert.That(
                    GetSummaryField<string>(raw, "frame_quality_evaluation_role"),
                    Is.EqualTo("raw_candidate_metrics"));
                Assert.That(postprocessed.candidate_metrics_csv, Is.EqualTo(GetSummaryField<string>(raw, "vertical_solve_postprocess_metrics_csv")));
                Assert.That(postprocessed.max_same_frame_hips_y_delta, Is.EqualTo(0.0395f).Within(0.0001f));
                Assert.That(postprocessed.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.0345f).Within(0.0001f));
                Assert.That(postprocessed.max_same_frame_root_position_delta, Is.EqualTo(raw.max_same_frame_root_position_delta).Within(0.0001f));
                Assert.That(postprocessed.candidate_below_floor_metric_frames, Is.EqualTo(0));
                Assert.That(postprocessed.target_frame_count, Is.EqualTo(901));
                Assert.That(postprocessed.candidate_recorded_frame_count, Is.EqualTo(901));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_PostprocessMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_PrimaryEntryKeepsRawCandidateAndPostprocessRemainsSecondary()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 1800, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.12f, 0.302f, 0.302f),
                    HipsContributionRow("main", 1800, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                MotionComparisonFrameQualitySummary[] entries =
                    MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(raw);

                Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0].status, Is.EqualTo("fail"));
                Assert.That(
                    GetSummaryField<string>(entries[0], "frame_quality_evaluation_role"),
                    Is.EqualTo("evaluation_candidate_metrics"));
                Assert.That(entries[0].candidate_metrics_csv, Is.EqualTo(candidatePath));
                Assert.That(entries[1].status, Is.EqualTo("pass"));
                Assert.That(
                    GetSummaryField<string>(entries[1], "frame_quality_evaluation_role"),
                    Is.EqualTo("corrected_candidate_metrics"));
                Assert.That(entries[1].candidate_metrics_csv, Is.EqualTo(GetSummaryField<string>(raw, "vertical_solve_corrected_candidate_metrics_csv")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CorrectedArtifactFootDeltaNeedsMoreThanPrototypeCap_When_BuildingEvaluationEntries_Then_PostprocessUsesArtifactCap()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.366f, 0.366f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                MotionComparisonFrameQualitySummary[] entries =
                    MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(raw);

                Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0].status, Is.EqualTo("fail"));
                Assert.That(entries[1].status, Is.EqualTo("pass"));
                Assert.That(entries[1].max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.0345f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CorrectedMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_EvaluatesCorrectedArtifactSeparatelyFromRawCandidate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 1800, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.12f, 0.302f, 0.302f),
                    HipsContributionRow("main", 1800, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                MotionComparisonFrameQualitySummary[] entries =
                    MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(raw);

                Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0].status, Is.EqualTo("fail"));
                Assert.That(
                    GetSummaryField<string>(entries[0], "frame_quality_evaluation_role"),
                    Is.EqualTo("evaluation_candidate_metrics"));
                Assert.That(entries[0].candidate_metrics_csv, Is.EqualTo(candidatePath));
                Assert.That(entries[1].status, Is.EqualTo("pass"));
                Assert.That(
                    GetSummaryField<string>(entries[1], "frame_quality_evaluation_role"),
                    Is.EqualTo("corrected_candidate_metrics"));
                Assert.That(
                    GetSummaryField<string>(entries[1], "frame_quality_evaluation_basis"),
                    Does.Contain("same raw frame_quality evaluator"));
                Assert.That(entries[1].candidate_label, Is.EqualTo("main corrected_vertical_solve_candidate"));
                Assert.That(entries[1].candidate_metrics_csv, Is.EqualTo(GetSummaryField<string>(raw, "vertical_solve_corrected_candidate_metrics_csv")));
                Assert.That(File.Exists(entries[1].candidate_metrics_csv), Is.True);
                Assert.That(entries[1].max_same_frame_hips_y_delta, Is.EqualTo(0.0395f).Within(0.0001f));
                Assert.That(entries[1].max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.0345f).Within(0.0001f));
                Assert.That(entries[1].candidate_below_floor_metric_frames, Is.EqualTo(0));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CorrectedMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_CorrectedEntryUsesExplicitVmdArtifactAndManifest()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 1800, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.12f, 0.302f, 0.302f),
                    HipsContributionRow("main", 1800, 0f, 1.05f, 0f, 0.62f, 0.58f, 0.000f, 0.000f, 1.12f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("Center", 1800, 0.02f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 1800, 0.02f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                MotionComparisonFrameQualitySummary[] entries =
                    MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(raw);

                string correctedVmdPath = GetSummaryField<string>(raw, "vertical_solve_corrected_candidate_vmd_path");
                string manifestPath = GetSummaryField<string>(raw, "vertical_solve_corrected_candidate_manifest_path");
                Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0].candidate_vmd_path, Is.EqualTo(vmdPath), "Raw primary entry must keep the unmodified VMD.");
                Assert.That(entries[1].candidate_vmd_path, Is.EqualTo(correctedVmdPath), "Corrected entry must point at the explicit corrected VMD artifact.");
                Assert.That(File.Exists(correctedVmdPath), Is.True, "Corrected candidate VMD artifact must exist on disk.");
                Assert.That(File.Exists(manifestPath), Is.True, "Corrected candidate manifest must exist on disk.");
                Assert.That(
                    Convert.ToBase64String(File.ReadAllBytes(correctedVmdPath)),
                    Is.Not.EqualTo(Convert.ToBase64String(File.ReadAllBytes(vmdPath))),
                    "Corrected candidate VMD must contain rewritten bone keyframe data, not a raw file copy.");
                Assert.That(
                    ReadMinimalVmdY(correctedVmdPath, "Center", 1800),
                    Is.LessThan(ReadMinimalVmdY(vmdPath, "Center", 1800)),
                    "Hips vertical solve must be reflected in the corrected VMD center carrier frame.");
                Assert.That(
                    ReadMinimalVmdY(correctedVmdPath, "LeftFootIK", 900),
                    Is.LessThan(ReadMinimalVmdY(vmdPath, "LeftFootIK", 900)),
                    "Foot vertical solve must be reflected in the corrected VMD foot IK frame.");

                string manifest = File.ReadAllText(manifestPath);
                Assert.That(manifest, Does.Contain("\"artifact_role\":\"corrected_vertical_solve_candidate\""));
                Assert.That(manifest, Does.Contain(EscapeJsonForAssertion(candidatePath)));
                Assert.That(manifest, Does.Contain(EscapeJsonForAssertion(vmdPath)));
                Assert.That(manifest, Does.Contain(EscapeJsonForAssertion(entries[1].candidate_metrics_csv)));
                Assert.That(manifest, Does.Contain(EscapeJsonForAssertion(entries[1].candidate_vmd_path)));
                Assert.That(manifest, Does.Contain("\"corrected_vmd_changed_frames\":"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CorrectedCandidatePasses_When_PromotingToPrimaryExport_Then_RewritesMainAutoPathsAndPreservesRawDiagnostics()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 1800, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.12f, 0.302f, 0.302f),
                    HipsContributionRow("main", 1800, 0f, 1.05f, 0f, 0.62f, 0.58f, 0.000f, 0.000f, 1.12f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 900, 0.01f, 0f, 0f),
                    VmdFrame("Center", 1800, 0.02f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0.01f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 1800, 0.02f, 0.05f, 0f));

                string rawCandidateCsv = File.ReadAllText(candidatePath);
                string rawCandidateVmd = Convert.ToBase64String(File.ReadAllBytes(vmdPath));
                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                Assert.That(raw.status, Is.EqualTo("fail"));
                Assert.That(raw.vertical_solve_corrected_candidate_status, Is.EqualTo("pass"));

                bool promoted = MotionComparisonProbeReportWriter.TryPromoteVerticalSolveCorrectedCandidateToPrimaryExport(
                    raw,
                    out VerticalSolvePrimaryExportPromotion promotion);

                Assert.That(promoted, Is.True);
                Assert.That(promotion, Is.Not.Null);
                Assert.That(File.Exists(promotion.raw_diagnostic_metrics_csv), Is.True);
                Assert.That(File.Exists(promotion.raw_diagnostic_vmd_path), Is.True);
                Assert.That(File.ReadAllText(promotion.raw_diagnostic_metrics_csv), Is.EqualTo(rawCandidateCsv));
                Assert.That(Convert.ToBase64String(File.ReadAllBytes(promotion.raw_diagnostic_vmd_path)), Is.EqualTo(rawCandidateVmd));
                Assert.That(File.ReadAllText(candidatePath), Is.Not.EqualTo(rawCandidateCsv));
                Assert.That(Convert.ToBase64String(File.ReadAllBytes(vmdPath)), Is.Not.EqualTo(rawCandidateVmd));

                MotionComparisonFrameQualitySummary promotedPrimary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);
                Assert.That(promotedPrimary.status, Is.EqualTo("pass"));
                Assert.That(promotedPrimary.max_same_frame_hips_y_delta, Is.EqualTo(0.0395f).Within(0.0001f));
                Assert.That(promotedPrimary.max_same_frame_foot_bottom_y_delta, Is.EqualTo(0.0345f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_IntegratedPrimarySummary_When_BuildingEvaluationEntries_Then_KeepsPrimaryAsOnlyAcceptanceEntry()
        {
            var integrated = new MotionComparisonFrameQualitySummary
            {
                frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics",
                frame_quality_evaluation_basis = "primary Main_Auto result paths after bounded vertical solve promotion",
                status = "pass",
                candidate_metrics_csv = "main.csv",
                candidate_vmd_path = "main.vmd",
                vertical_solve_corrected_candidate_metrics_csv = "main.corrected.csv",
                vertical_solve_corrected_candidate_vmd_path = "main.corrected.vmd"
            };

            MotionComparisonFrameQualitySummary[] entries =
                MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(integrated);

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0], Is.SameAs(integrated));
            Assert.That(entries[0].frame_quality_evaluation_role, Is.EqualTo("main_auto_integrated_vertical_solve_metrics"));
            Assert.That(entries[0].status, Is.EqualTo("pass"));
        }

        [Test]
        public void Given_VerticalSolveWouldCreateUnsafeVmdCarrierStep_When_BuildingEvaluationEntries_Then_CorrectedVmdStaysWithinSafetyGates()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.1695f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 899, 0f, 0.09f, 0f),
                    VmdFrame("Center", 900, 0f, 0.05f, 0f),
                    VmdFrame("Center", 901, 0f, 0.09f, 0f),
                    VmdFrame("LeftFootIK", 899, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 900, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 901, 0f, 0f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                MotionComparisonFrameQualitySummary[] entries =
                    MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(raw);

                Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0].status, Is.Not.EqualTo("pass"), "Raw primary must remain the unmodified non-passing gate.");
                Assert.That(entries[1].status, Is.EqualTo("pass"), "Corrected VMD artifact must not introduce floor or teleport safety failures.");
                Assert.That(entries[1].candidate_vmd_center_spike_frames, Is.EqualTo(0));
                Assert.That(entries[1].candidate_vmd_foot_ik_spike_frames, Is.EqualTo(0));
                Assert.That(entries[1].min_candidate_vmd_effective_foot_ik_y, Is.GreaterThanOrEqualTo(-0.001f));
                Assert.That(
                    File.ReadAllText(GetSummaryField<string>(raw, "vertical_solve_corrected_candidate_manifest_path")),
                    Does.Contain("\"corrected_vmd_safety_limited_frames\":"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_FootVerticalSolveWouldSinkEffectiveVmdIk_When_BuildingEvaluationEntries_Then_CorrectedVmdPreservesFloorMargin()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.57f, 0.54f, 0.000f, 0.000f, 1.06f, 0.33f, 0.33f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 899, 0f, 0.0f, 0f),
                    VmdFrame("Center", 900, 0f, 0.0f, 0f),
                    VmdFrame("Center", 901, 0f, 0.0f, 0f),
                    VmdFrame("LeftFootIK", 899, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 900, 0f, 0.02f, 0f),
                    VmdFrame("LeftFootIK", 901, 0f, 0.05f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                MotionComparisonFrameQualitySummary[] entries =
                    MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(raw);

                Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0].status, Is.Not.EqualTo("pass"), "Raw primary must remain the unmodified non-passing gate.");
                Assert.That(entries[1].status, Is.EqualTo("pass"), "Corrected VMD artifact must not sink an already-low effective foot IK sample below floor.");
                Assert.That(entries[1].candidate_vmd_foot_ik_spike_frames, Is.EqualTo(0));
                Assert.That(entries[1].min_candidate_vmd_effective_foot_ik_y, Is.GreaterThanOrEqualTo(-0.001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CenterVerticalSolveWouldSinkToeIkEffectiveY_When_BuildingEvaluationEntries_Then_CorrectedVmdPreservesToeFloorMargin()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsvWithHipsContributors(
                    baselinePath,
                    HipsContributionRow("manual", 0, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f),
                    HipsContributionRow("manual", 900, 0f, 1.00f, 0f, 0.50f, 0.50f, 0.000f, 0.000f, 1.00f, 0.20f, 0.20f));
                WriteMetricsCsvWithHipsContributors(
                    candidatePath,
                    HipsContributionRow("main", 0, 0f, 1.05f, 0f, 0.55f, 0.55f, 0.000f, 0.000f, 1.05f, 0.25f, 0.25f),
                    HipsContributionRow("main", 900, 0f, 1.05f, 0f, 0.64f, 0.61f, 0.000f, 0.000f, 1.1695f, 0.25f, 0.25f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 899, 0f, 0.09f, 0f),
                    VmdFrame("Center", 900, 0f, 0.05f, 0f),
                    VmdFrame("Center", 901, 0f, 0.09f, 0f),
                    VmdFrame("LeftFootIK", 899, 0f, 0.10f, 0f),
                    VmdFrame("LeftFootIK", 900, 0f, 0.10f, 0f),
                    VmdFrame("LeftFootIK", 901, 0f, 0.10f, 0f),
                    VmdFrame("LeftToeIK", 899, 0f, -0.099f, 0f),
                    VmdFrame("LeftToeIK", 900, 0f, -0.099f, 0f),
                    VmdFrame("LeftToeIK", 901, 0f, -0.099f, 0f));

                MotionComparisonFrameQualitySummary raw =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 901,
                        candidateRecordedFrameCount: 901,
                        targetFrameCount: 901);

                MotionComparisonFrameQualitySummary[] entries =
                    MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(raw);

                Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0].status, Is.Not.EqualTo("pass"), "Raw primary must remain the unmodified non-passing gate.");
                Assert.That(entries[1].status, Is.EqualTo("pass"), "Corrected VMD center solve must include toe IK when guarding effective floor.");
                Assert.That(entries[1].candidate_vmd_center_spike_frames, Is.EqualTo(0));
                Assert.That(entries[1].candidate_vmd_foot_ik_spike_frames, Is.EqualTo(0));
                Assert.That(entries[1].min_candidate_vmd_effective_foot_ik_y, Is.GreaterThanOrEqualTo(-0.001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CenterLiftKeepsEffectiveFootIkAboveFloor_When_BuildFrameQualitySummary_Then_DoesNotFailBelowFloorGate()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(baselinePath, Row("manual", 0, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMetricsCsv(candidatePath, Row("main", 0, 0f, 1f, 0f, 1f, 0.02f, 0.02f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0.05f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, -0.03f, 0f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 1,
                        candidateRecordedFrameCount: 1,
                        targetFrameCount: 1);

                Assert.That(summary.status, Is.EqualTo("pass"));
                Assert.That(summary.status_reason, Does.Not.Contain("below-floor foot/IK sample detected"));
                Assert.That(summary.min_candidate_vmd_foot_ik_y, Is.EqualTo(-0.03f).Within(0.0001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_VmdFrames_When_BuildFrameQualitySummary_Then_ReportsCenterAndFootIkExportDelta()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string baselinePath = Path.Combine(root, "manual.csv");
            string candidatePath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            Directory.CreateDirectory(root);

            try
            {
                WriteMetricsCsv(baselinePath, Row("manual", 0, 0f, 1f, 0f, 1f, 0f, 0f, 0f, 0f, 0f));
                WriteMetricsCsv(candidatePath, Row("main", 0, 0f, 1f, 0f, 1f, 0f, 0f, 0f, 0f, 0f));
                WriteMinimalVmd(
                    vmdPath,
                    VmdFrame("Center", 0, 0f, 0f, 0f),
                    VmdFrame("Center", 1, 0.2f, 0f, 0f),
                    VmdFrame("LeftFootIK", 0, 0f, 0f, 0f),
                    VmdFrame("LeftFootIK", 1, 0f, -0.03f, 0.4f));

                MotionComparisonFrameQualitySummary summary =
                    MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                        "manual",
                        baselinePath,
                        "main",
                        candidatePath,
                        vmdPath,
                        baselineRecordedFrameCount: 1,
                        candidateRecordedFrameCount: 1,
                        targetFrameCount: 1);

                Assert.That(summary.status, Is.EqualTo("fail"));
                Assert.That(summary.candidate_vmd_bone_frames, Is.EqualTo(4));
                Assert.That(summary.max_candidate_vmd_center_step, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(summary.max_candidate_vmd_foot_ik_step, Is.EqualTo(Math.Sqrt(0.0009d + 0.16d)).Within(0.0001f));
                Assert.That(summary.min_candidate_vmd_foot_ik_y, Is.EqualTo(-0.03f).Within(0.0001f));
                Assert.That(summary.candidate_vmd_center_spike_frames, Is.EqualTo(1));
                Assert.That(summary.candidate_vmd_foot_ik_spike_frames, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MmdAutomationEvidence_When_AttachLatestMmdAutomationEvidence_Then_UpdatesStatusAndRelativePaths()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string assetsPath = Path.Combine(root, "Assets");
            string candidateVmdPath = Path.Combine(root, "Assets", "VMDRecorderSample", "smoke_satisfaction_2_31s.vmd");
            string runDir = Path.Combine(root, "Docs", "Machine_Spirit", "Local", "MMDQASessions", "automation_runs", "run-a");
            string screenshotsDir = Path.Combine(runDir, "screenshots");
            string reportPath = Path.Combine(runDir, "report.json");
            string playScreenshotPath = Path.Combine(screenshotsDir, "06_after_play.png");
            string modelScreenshotPath = Path.Combine(screenshotsDir, "06_after_play_model.png");
            Directory.CreateDirectory(Path.GetDirectoryName(candidateVmdPath));
            Directory.CreateDirectory(screenshotsDir);

            try
            {
                File.WriteAllBytes(candidateVmdPath, Array.Empty<byte>());
                File.WriteAllBytes(playScreenshotPath, new byte[] { 1 });
                File.WriteAllBytes(modelScreenshotPath, new byte[] { 2 });
                File.WriteAllText(
                    reportPath,
                    "{\n" +
                    "  \"status\": \"ok\",\n" +
                    "  \"finished_at\": \"2026-05-25T04:49:46\",\n" +
                    "  \"config\": { \"motion_vmd\": \"" + JsonPath(candidateVmdPath) + "\" },\n" +
                    "  \"artifacts\": { \"report_path\": \"" + JsonPath(reportPath) + "\", \"run_dir\": \"" + JsonPath(runDir) + "\", \"screenshots_dir\": \"" + JsonPath(screenshotsDir) + "\" },\n" +
                    "  \"steps\": [ { \"name\": \"play\", \"status\": \"ok\", \"play_state_screenshot\": \"" + JsonPath(playScreenshotPath) + "\" } ]\n" +
                    "}\n");
                MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
                {
                    candidate_vmd_path = candidateVmdPath,
                    mmd_result_status = "not_run"
                };

                MotionComparisonProbeReportWriter.AttachLatestMmdAutomationEvidence(
                    summary,
                    projectRoot: root,
                    automationRunsRoot: Path.Combine(root, "Docs", "Machine_Spirit", "Local", "MMDQASessions", "automation_runs"));

                Assert.That(summary.mmd_result_status, Is.EqualTo("ok"));
                Assert.That(summary.mmd_report_path, Is.EqualTo("Docs/Machine_Spirit/Local/MMDQASessions/automation_runs/run-a/report.json"));
                Assert.That(summary.mmd_run_dir, Is.EqualTo("Docs/Machine_Spirit/Local/MMDQASessions/automation_runs/run-a"));
                Assert.That(summary.mmd_after_play_screenshot_path, Is.EqualTo("Docs/Machine_Spirit/Local/MMDQASessions/automation_runs/run-a/screenshots/06_after_play_model.png"));
                Assert.That(summary.mmd_finished_at, Is.EqualTo("2026-05-25T04:49:46"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MmdAutomationEvidenceWithProjectRelativeArtifactPaths_When_AttachLatestMmdAutomationEvidence_Then_ResolvesScreenshotPath()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string assetsPath = Path.Combine(root, "Assets");
            string relativeCandidateVmdPath = "Assets/VMDRecorderSample/smoke_satisfaction_2_31s.vmd";
            string candidateVmdPath = Path.Combine(root, "Assets", "VMDRecorderSample", "smoke_satisfaction_2_31s.vmd");
            string relativeRunDir = "Docs/Machine_Spirit/Local/MMDQASessions/automation_runs/run-relative";
            string relativeScreenshotsDir = relativeRunDir + "/screenshots";
            string relativeReportPath = relativeRunDir + "/report.json";
            string relativePlayScreenshotPath = relativeScreenshotsDir + "/06_after_play.png";
            string relativeModelScreenshotPath = relativeScreenshotsDir + "/06_after_play_model.png";
            string runDir = Path.Combine(root, relativeRunDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string screenshotsDir = Path.Combine(root, relativeScreenshotsDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string reportPath = Path.Combine(root, relativeReportPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string playScreenshotPath = Path.Combine(root, relativePlayScreenshotPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string modelScreenshotPath = Path.Combine(root, relativeModelScreenshotPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            Directory.CreateDirectory(Path.GetDirectoryName(candidateVmdPath));
            Directory.CreateDirectory(screenshotsDir);

            try
            {
                File.WriteAllBytes(candidateVmdPath, Array.Empty<byte>());
                File.WriteAllBytes(playScreenshotPath, new byte[] { 1 });
                File.WriteAllBytes(modelScreenshotPath, new byte[] { 2 });
                File.WriteAllText(
                    reportPath,
                    "{\n" +
                    "  \"status\": \"ok\",\n" +
                    "  \"finished_at\": \"2026-05-25T05:01:11\",\n" +
                    "  \"config\": { \"motion_vmd\": \"" + JsonPath(relativeCandidateVmdPath) + "\" },\n" +
                    "  \"artifacts\": { \"report_path\": \"" + JsonPath(relativeReportPath) + "\", \"run_dir\": \"" + JsonPath(relativeRunDir) + "\", \"screenshots_dir\": \"" + JsonPath(relativeScreenshotsDir) + "\" },\n" +
                    "  \"steps\": [ { \"name\": \"play\", \"status\": \"ok\", \"play_state_screenshot\": \"" + JsonPath(relativePlayScreenshotPath) + "\" } ]\n" +
                    "}\n");
                MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
                {
                    candidate_vmd_path = candidateVmdPath,
                    mmd_result_status = "not_run"
                };

                MotionComparisonProbeReportWriter.AttachLatestMmdAutomationEvidence(
                    summary,
                    projectRoot: root,
                    automationRunsRoot: Path.Combine(root, "Docs", "Machine_Spirit", "Local", "MMDQASessions", "automation_runs"));

                Assert.That(summary.mmd_result_status, Is.EqualTo("ok"));
                Assert.That(summary.mmd_report_path, Is.EqualTo(relativeReportPath));
                Assert.That(summary.mmd_run_dir, Is.EqualTo(relativeRunDir));
                Assert.That(summary.mmd_after_play_screenshot_path, Is.EqualTo(relativeModelScreenshotPath));
                Assert.That(summary.mmd_finished_at, Is.EqualTo("2026-05-25T05:01:11"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MmdAutomationEvidenceWithReportRelativeRunDir_When_AttachLatestMmdAutomationEvidence_Then_ResolvesRunDirPath()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string relativeCandidateVmdPath = "Assets/VMDRecorderSample/smoke_satisfaction_2_31s.vmd";
            string candidateVmdPath = Path.Combine(root, "Assets", "VMDRecorderSample", "smoke_satisfaction_2_31s.vmd");
            string relativeRunDir = "Docs/Machine_Spirit/Local/MMDQASessions/automation_runs/run-report-relative";
            string relativeReportPath = relativeRunDir + "/report.json";
            string relativeModelScreenshotPath = relativeRunDir + "/screenshots/06_after_play_model.png";
            string runDir = Path.Combine(root, relativeRunDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string screenshotsDir = Path.Combine(runDir, "screenshots");
            string reportPath = Path.Combine(root, relativeReportPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string playScreenshotPath = Path.Combine(screenshotsDir, "06_after_play.png");
            string modelScreenshotPath = Path.Combine(screenshotsDir, "06_after_play_model.png");
            Directory.CreateDirectory(Path.GetDirectoryName(candidateVmdPath));
            Directory.CreateDirectory(screenshotsDir);

            try
            {
                File.WriteAllBytes(candidateVmdPath, Array.Empty<byte>());
                File.WriteAllBytes(playScreenshotPath, new byte[] { 1 });
                File.WriteAllBytes(modelScreenshotPath, new byte[] { 2 });
                File.WriteAllText(
                    reportPath,
                    "{\n" +
                    "  \"status\": \"ok\",\n" +
                    "  \"finished_at\": \"2026-05-25T05:19:18\",\n" +
                    "  \"config\": { \"motion_vmd\": \"" + JsonPath(relativeCandidateVmdPath) + "\" },\n" +
                    "  \"artifacts\": { \"report_path\": \"" + JsonPath(relativeReportPath) + "\", \"run_dir\": \".\", \"screenshots_dir\": \"screenshots\" },\n" +
                    "  \"steps\": [ { \"name\": \"play\", \"status\": \"ok\", \"play_state_screenshot\": \"screenshots/06_after_play.png\" } ]\n" +
                    "}\n");
                MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
                {
                    candidate_vmd_path = candidateVmdPath,
                    mmd_result_status = "not_run"
                };

                MotionComparisonProbeReportWriter.AttachLatestMmdAutomationEvidence(
                    summary,
                    projectRoot: root,
                    automationRunsRoot: Path.Combine(root, "Docs", "Machine_Spirit", "Local", "MMDQASessions", "automation_runs"));

                Assert.That(summary.mmd_result_status, Is.EqualTo("ok"));
                Assert.That(summary.mmd_report_path, Is.EqualTo(relativeReportPath));
                Assert.That(summary.mmd_run_dir, Is.EqualTo(relativeRunDir));
                Assert.That(summary.mmd_after_play_screenshot_path, Is.EqualTo(relativeModelScreenshotPath));
                Assert.That(summary.mmd_finished_at, Is.EqualTo("2026-05-25T05:19:18"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ReportRelativeScreenshotAlsoExistsAtProjectRoot_When_AttachLatestMmdAutomationEvidence_Then_PrefersReportDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "MotionComparisonProbeReportWriterTests_" + Guid.NewGuid().ToString("N"));
            string relativeCandidateVmdPath = "Assets/VMDRecorderSample/smoke_satisfaction_2_31s.vmd";
            string candidateVmdPath = Path.Combine(root, "Assets", "VMDRecorderSample", "smoke_satisfaction_2_31s.vmd");
            string relativeRunDir = "Docs/Machine_Spirit/Local/MMDQASessions/automation_runs/run-report-screenshot";
            string relativeReportPath = relativeRunDir + "/report.json";
            string relativeModelScreenshotPath = relativeRunDir + "/screenshots/06_after_play_model.png";
            string runDir = Path.Combine(root, relativeRunDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string reportScreenshotsDir = Path.Combine(runDir, "screenshots");
            string projectScreenshotsDir = Path.Combine(root, "screenshots");
            string reportPath = Path.Combine(root, relativeReportPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string reportPlayScreenshotPath = Path.Combine(reportScreenshotsDir, "06_after_play.png");
            string reportModelScreenshotPath = Path.Combine(reportScreenshotsDir, "06_after_play_model.png");
            string staleProjectPlayScreenshotPath = Path.Combine(projectScreenshotsDir, "06_after_play.png");
            string staleProjectModelScreenshotPath = Path.Combine(projectScreenshotsDir, "06_after_play_model.png");
            Directory.CreateDirectory(Path.GetDirectoryName(candidateVmdPath));
            Directory.CreateDirectory(reportScreenshotsDir);
            Directory.CreateDirectory(projectScreenshotsDir);

            try
            {
                File.WriteAllBytes(candidateVmdPath, Array.Empty<byte>());
                File.WriteAllBytes(reportPlayScreenshotPath, new byte[] { 1 });
                File.WriteAllBytes(reportModelScreenshotPath, new byte[] { 2 });
                File.WriteAllBytes(staleProjectPlayScreenshotPath, new byte[] { 3 });
                File.WriteAllBytes(staleProjectModelScreenshotPath, new byte[] { 3 });
                File.WriteAllText(
                    reportPath,
                    "{\n" +
                    "  \"status\": \"ok\",\n" +
                    "  \"finished_at\": \"2026-05-25T06:11:07\",\n" +
                    "  \"config\": { \"motion_vmd\": \"" + JsonPath(relativeCandidateVmdPath) + "\" },\n" +
                    "  \"artifacts\": { \"report_path\": \"" + JsonPath(relativeReportPath) + "\", \"run_dir\": \".\", \"screenshots_dir\": \"screenshots\" },\n" +
                    "  \"steps\": [ { \"name\": \"play\", \"status\": \"ok\", \"play_state_screenshot\": \"screenshots/06_after_play.png\" } ]\n" +
                    "}\n");
                MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
                {
                    candidate_vmd_path = candidateVmdPath,
                    mmd_result_status = "not_run"
                };

                MotionComparisonProbeReportWriter.AttachLatestMmdAutomationEvidence(
                    summary,
                    projectRoot: root,
                    automationRunsRoot: Path.Combine(root, "Docs", "Machine_Spirit", "Local", "MMDQASessions", "automation_runs"));

                Assert.That(summary.mmd_result_status, Is.EqualTo("ok"));
                Assert.That(summary.mmd_after_play_screenshot_path, Is.EqualTo(relativeModelScreenshotPath));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        private static string[] Row(
            string label,
            int recorderFrame,
            float rootX,
            float rootY,
            float rootZ,
            float hipsY,
            float lowestFootBottomY,
            float footBottomGroundGap,
            float retargetRootDeltaMax,
            float retargetPoseRootDeltaMax,
            float retargetGroundingVerticalStepMax)
        {
            return new[]
            {
                label,
                recorderFrame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rootX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rootY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rootZ.ToString(System.Globalization.CultureInfo.InvariantCulture),
                hipsY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                lowestFootBottomY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                footBottomGroundGap.ToString(System.Globalization.CultureInfo.InvariantCulture),
                retargetRootDeltaMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
                retargetPoseRootDeltaMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
                retargetGroundingVerticalStepMax.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        private static string[] RowWithYybAndSleeve(
            string label,
            int recorderFrame,
            float rootX,
            float rootY,
            float rootZ,
            float hipsY,
            float lowestFootBottomY,
            float footBottomGroundGap,
            float retargetRootDeltaMax,
            float retargetPoseDeltaMax,
            float retargetGroundingVerticalStepMax,
            string yybMaxDeformationRisk,
            string leftSleeveThicknessRisk,
            string rightSleeveThicknessRisk)
        {
            string[] baseRow = Row(
                label,
                recorderFrame,
                rootX,
                rootY,
                rootZ,
                hipsY,
                lowestFootBottomY,
                footBottomGroundGap,
                retargetRootDeltaMax,
                retargetPoseDeltaMax,
                retargetGroundingVerticalStepMax);
            var row = new List<string>(baseRow)
            {
                yybMaxDeformationRisk ?? "",
                leftSleeveThicknessRisk ?? "",
                rightSleeveThicknessRisk ?? ""
            };
            return row.ToArray();
        }

        private static string[] HipsContributionRow(
            string label,
            int recorderFrame,
            float rootX,
            float rootY,
            float rootZ,
            float bodyPositionY,
            float hipsLocalY,
            float groundingVerticalStepLast,
            float footHeightReferenceLift,
            float hipsY,
            float lowestFootBottomY,
            float footBottomGroundGap)
        {
            return new[]
            {
                label,
                recorderFrame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rootX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rootY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rootZ.ToString(System.Globalization.CultureInfo.InvariantCulture),
                bodyPositionY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                hipsLocalY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                groundingVerticalStepLast.ToString(System.Globalization.CultureInfo.InvariantCulture),
                footHeightReferenceLift.ToString(System.Globalization.CultureInfo.InvariantCulture),
                hipsY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                lowestFootBottomY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                footBottomGroundGap.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "0",
                "0",
                "0"
            };
        }

        private static string[] RecordingStartHipsBaselineRow(
            string label,
            int recorderFrame,
            string reason,
            float rootY,
            float bodyPositionY,
            float hipsLocalY,
            float hipsY,
            float recordingStartRootY,
            float recordingStartBodyPositionY,
            float recordingStartHipsLocalY,
            float recordingStartHipsY,
            float referenceBeforeLocalY,
            float referenceAfterLocalY,
            float referenceDeltaY,
            int flipDetected,
            string stage)
        {
            return new[]
            {
                label,
                reason,
                recorderFrame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "0",
                rootY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "0",
                FormatTestFloat(recordingStartRootY),
                FormatTestFloat(recordingStartBodyPositionY),
                FormatTestFloat(recordingStartHipsLocalY),
                FormatTestFloat(recordingStartHipsY),
                FormatTestFloat(referenceBeforeLocalY),
                FormatTestFloat(referenceAfterLocalY),
                FormatTestFloat(referenceDeltaY),
                flipDetected.ToString(System.Globalization.CultureInfo.InvariantCulture),
                stage,
                bodyPositionY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                hipsLocalY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "0",
                hipsY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "0.2",
                "0.2",
                "0",
                "0",
                "0"
            };
        }

        private static string FormatTestFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? ""
                : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static T GetSummaryField<T>(MotionComparisonFrameQualitySummary summary, string fieldName)
        {
            FieldInfo field = typeof(MotionComparisonFrameQualitySummary).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected summary field '{fieldName}' to exist.");
            return (T)field.GetValue(summary);
        }

        private static string FindSessionManifestArtifactsHeading()
        {
            string markdown = MotionComparisonProbeReportWriter.BuildSessionManifestMarkdown(
                new MotionComparisonProbeSessionManifestData(
                    sessionId: "s1",
                    comparisonLabel: "label",
                    sceneName: "scene",
                    stateReason: "started",
                    createdAt: "2026-05-15 00:00:00",
                    updatedAt: "2026-05-15 00:00:01",
                    screenshotsEnabled: true,
                    sampleClock: "elapsed",
                    sampleTimes: "0, 1",
                    yybDiagnosticOnlyMetrics: false,
                    riskEvaluationFrameCount: 10,
                    leftThumbCoreCoverageFrameCount: 1,
                    rightThumbCoreCoverageFrameCount: 2,
                    leftThumbHelperCoverageRequired: true,
                    rightThumbHelperCoverageRequired: false,
                    leftThumbHelperCoverageFrameCount: 3,
                    rightThumbHelperCoverageFrameCount: 4,
                    maxGenericThumbAnatomyRisk: 0.1f,
                    maxGenericThumbAnatomyRiskReason: "r",
                    maxGenericThumbAnatomyRiskClipTime: 0.2f,
                    maxGenericThumbAnatomyRiskRecorderFrame: 5,
                    maxThumbSpreadRisk: 0.3f,
                    maxThumbProjectionRisk: 0.4f,
                    maxThumbHelperSeparationRisk: 0.5f,
                    maxThumbWebbingRisk: 0.6f,
                    maxYybDeformationRisk: 0.7f,
                    maxYybDeformationRiskReason: "yyb",
                    maxYybDeformationRiskClipTime: 0.8f,
                    maxYybDeformationRiskRecorderFrame: 9,
                    leftThumbProjectionGuardWeight: 0.11f,
                    rightThumbProjectionGuardWeight: 0.12f,
                    leftThumbIndexSpreadGuardWeight: 0.13f,
                    rightThumbIndexSpreadGuardWeight: 0.14f,
                    leftThumbSegmentStraightenGuardWeight: 0.15f,
                    rightThumbSegmentStraightenGuardWeight: 0.16f,
                    metricsCsvRelativePath: "Local/ComparisonLogs/metrics.csv",
                    frameFolderRelativePath: "Local/ComparisonFrames",
                    frameIndexCsvRelativePath: "Local/ComparisonFrames/index.csv",
                    frameSessionIndexRelativePath: "Local/ComparisonFrames/session_index.md"));
            string[] lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int metricsRowIndex = Array.FindIndex(lines, line => line.Contains("| metrics csv |"));
            for (int i = metricsRowIndex; i >= 0; i--)
            {
                if (lines[i].StartsWith("## ", StringComparison.Ordinal))
                {
                    return lines[i];
                }
            }

            throw new InvalidOperationException("Session manifest artifacts heading was not found.");
        }

        private static string JsonPath(string path)
        {
            return path.Replace("\\", "\\\\");
        }

        private static string EscapeJsonForAssertion(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteMetricsCsv(string path, params string[][] rows)
        {
            var lines = new List<string>
            {
                "label,recorderFrame,rootX,rootY,rootZ,hipsY,lowestFootBottomY,footBottomGroundGap,retargetRootDeltaMax,retargetPoseRootDeltaMax,retargetGroundingVerticalStepMax"
            };
            foreach (string[] row in rows)
            {
                lines.Add(string.Join(",", row));
            }

            File.WriteAllLines(path, lines);
        }

        private static void WriteMetricsCsvWithYybRisk(string path, params string[][] rows)
        {
            var lines = new List<string>
            {
                "label,recorderFrame,rootX,rootY,rootZ,hipsY,lowestFootBottomY,footBottomGroundGap,retargetRootDeltaMax,retargetPoseRootDeltaMax,retargetGroundingVerticalStepMax,yybMaxDeformationRisk"
            };
            foreach (string[] row in rows)
            {
                lines.Add(string.Join(",", row));
            }

            File.WriteAllLines(path, lines);
        }

        private static void WriteMetricsCsvWithYybRiskAndSleeveThickness(string path, params string[][] rows)
        {
            var lines = new List<string>
            {
                "label,recorderFrame,rootX,rootY,rootZ,hipsY,lowestFootBottomY,footBottomGroundGap,retargetRootDeltaMax,retargetPoseRootDeltaMax,retargetGroundingVerticalStepMax,yybMaxDeformationRisk,leftSleeveThicknessRisk,rightSleeveThicknessRisk"
            };
            foreach (string[] row in rows)
            {
                lines.Add(string.Join(",", row));
            }

            File.WriteAllLines(path, lines);
        }

        private static void WriteMetricsCsvWithHipsContributors(string path, params string[][] rows)
        {
            var lines = new List<string>
            {
                "label,recorderFrame,rootX,rootY,rootZ,bodyPositionY,hipsLocalY,retargetGroundingVerticalStepLast,retargetFootHeightReferenceLift,hipsY,lowestFootBottomY,footBottomGroundGap,retargetRootDeltaMax,retargetPoseRootDeltaMax,retargetGroundingVerticalStepMax"
            };
            foreach (string[] row in rows)
            {
                lines.Add(string.Join(",", row));
            }

            File.WriteAllLines(path, lines);
        }

        private static string[] YybRiskAndSleeveThicknessRow(
            string label,
            int recorderFrame,
            string yybMaxDeformationRisk,
            string leftSleeveThicknessRisk,
            string rightSleeveThicknessRisk)
        {
            return new[]
            {
                label,
                recorderFrame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "0",
                "1",
                "0",
                "1",
                "0.1",
                "0.1",
                "0",
                "0",
                "0",
                yybMaxDeformationRisk ?? "",
                leftSleeveThicknessRisk ?? "",
                rightSleeveThicknessRisk ?? ""
            };
        }

        private static string[] YybRiskRow(string label, int recorderFrame, string yybMaxDeformationRisk)
        {
            return new[]
            {
                label,
                recorderFrame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "0",
                "1",
                "0",
                "1",
                "0.1",
                "0.1",
                "0",
                "0",
                "0",
                yybMaxDeformationRisk ?? ""
            };
        }

        private static void WriteMetricsCsvWithRecordingStartHipsBaseline(string path, params string[][] rows)
        {
            var lines = new List<string>
            {
                "label,reason,recorderFrame,rootX,rootY,rootZ,retargetRecordingStartRootY,retargetRecordingStartBodyPositionY,retargetRecordingStartHipsLocalY,retargetRecordingStartHipsY,retargetRecordingStartHipsReferenceBeforeLocalY,retargetRecordingStartHipsReferenceAfterLocalY,retargetRecordingStartHipsReferenceDeltaY,retargetRecordingStartHipsReferenceFlipDetected,retargetRecordingStartHipsReferenceStage,bodyPositionY,hipsLocalY,retargetFootHeightReferenceLift,hipsY,lowestFootBottomY,footBottomGroundGap,retargetRootDeltaMax,retargetPoseRootDeltaMax,retargetGroundingVerticalStepMax"
            };
            foreach (string[] row in rows)
            {
                lines.Add(string.Join(",", row));
            }

            File.WriteAllLines(path, lines);
        }

        private static VmdTestFrame VmdFrame(string boneName, uint frame, float x, float y, float z)
        {
            return new VmdTestFrame(boneName, frame, x, y, z);
        }

        private static void WriteMinimalVmd(string path, params VmdTestFrame[] frames)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                WritePaddedShiftJis(writer, "Vocaloid Motion Data 0002", 30);
                WritePaddedShiftJis(writer, "test", 20);
                writer.Write((uint)frames.Length);
                foreach (VmdTestFrame frame in frames)
                {
                    WritePaddedShiftJis(writer, frame.BoneName, 15);
                    writer.Write(frame.Frame);
                    writer.Write(frame.X);
                    writer.Write(frame.Y);
                    writer.Write(frame.Z);
                    writer.Write(0f);
                    writer.Write(0f);
                    writer.Write(0f);
                    writer.Write(1f);
                    writer.Write(new byte[64]);
                }
            }
        }

        private static void WritePaddedShiftJis(BinaryWriter writer, string value, int byteLength)
        {
            byte[] bytes = Encoding.GetEncoding("shift_jis").GetBytes(value);
            writer.Write(bytes, 0, Math.Min(bytes.Length, byteLength));
            if (bytes.Length < byteLength)
            {
                writer.Write(new byte[byteLength - bytes.Length]);
            }
        }

        private static float ReadMinimalVmdY(string path, string boneName, uint frame)
        {
            byte[] bytes = File.ReadAllBytes(path);
            const int headerLength = 50;
            const int countLength = 4;
            const int boneFrameSize = 111;
            if (bytes.Length < headerLength + countLength)
            {
                Assert.Fail("VMD file is too short.");
            }

            Encoding shiftJis = Encoding.GetEncoding("shift_jis");
            uint boneFrameCount = BitConverter.ToUInt32(bytes, headerLength);
            int offset = headerLength + countLength;
            for (uint index = 0; index < boneFrameCount && offset + boneFrameSize <= bytes.Length; index++, offset += boneFrameSize)
            {
                int end = offset;
                int maxEnd = Math.Min(bytes.Length, offset + 15);
                while (end < maxEnd && bytes[end] != 0)
                {
                    end++;
                }

                string currentBoneName = end > offset ? shiftJis.GetString(bytes, offset, end - offset) : "";
                uint currentFrame = BitConverter.ToUInt32(bytes, offset + 15);
                if (string.Equals(currentBoneName, boneName, StringComparison.Ordinal) && currentFrame == frame)
                {
                    return BitConverter.ToSingle(bytes, offset + 23);
                }
            }

            Assert.Fail($"VMD frame not found: {boneName} frame {frame}.");
            return float.NaN;
        }

        private static Texture2D CreateFilledTexture(int width, int height, Color32 color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private readonly struct VmdTestFrame
        {
            public VmdTestFrame(string boneName, uint frame, float x, float y, float z)
            {
                BoneName = boneName;
                Frame = frame;
                X = x;
                Y = y;
                Z = z;
            }

            public string BoneName { get; }
            public uint Frame { get; }
            public float X { get; }
            public float Y { get; }
            public float Z { get; }
        }
    }
}
