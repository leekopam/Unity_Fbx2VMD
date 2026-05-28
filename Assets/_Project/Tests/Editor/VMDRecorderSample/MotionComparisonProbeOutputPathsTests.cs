using NUnit.Framework;
using System;
using System.IO;

namespace Tests.Editor.VMDRecorderSample
{
    public class MotionComparisonProbeOutputPathsTests
    {
        [Test]
        public void Given_DataPath_When_GetProjectRootFromDataPath_Then_ReturnsParent()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dataPath);

            try
            {
                Assert.That(MotionComparisonProbeOutputPaths.GetProjectRootFromDataPath(dataPath), Is.EqualTo(root));
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
        public void Given_DataPath_When_GetOrCreateFolderFromDataPath_Then_CreatesFolder()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dataPath);

            string created = string.Empty;

            try
            {
                created = MotionComparisonProbeOutputPaths.GetOrCreateFolderFromDataPath(
                    dataPath,
                    "Docs",
                    "Machine_Spirit",
                    "Local",
                    "ComparisonLogs");

                Assert.That(Directory.Exists(created), Is.True);
                Assert.That(created, Does.Contain(root));
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
        public void Given_ProjectPath_When_MakeProjectRelativePath_Then_UsesForwardSlashes()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            string sessionPath = Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonLogs", "metrics.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(sessionPath));

            try
            {
                string relativePath = MotionComparisonProbeOutputPaths.MakeProjectRelativePath(dataPath, sessionPath);

                Assert.That(relativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonLogs/metrics.csv"));
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
        public void Given_SiblingRootPrefixPath_When_MakeProjectRelativePath_Then_KeepsAbsolutePath()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string siblingRoot = root + "-sibling";
            string dataPath = Path.Combine(root, "Assets");
            string siblingPath = Path.Combine(siblingRoot, "Docs", "Machine_Spirit", "Local", "ComparisonLogs", "metrics.csv");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(siblingPath));

            try
            {
                string relativePath = MotionComparisonProbeOutputPaths.MakeProjectRelativePath(dataPath, siblingPath);

                Assert.That(relativePath, Is.EqualTo(siblingPath.Replace("\\", "/")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }

                if (Directory.Exists(siblingRoot))
                {
                    Directory.Delete(siblingRoot, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ProjectRootPath_When_MakeProjectRootRelativePath_Then_UsesForwardSlashes()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string reportPath = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "MMDQASessions",
                "automation_runs",
                "run-a",
                "report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            try
            {
                string relativePath = MotionComparisonProbeOutputPaths.MakeProjectRootRelativePath(root, reportPath);

                Assert.That(relativePath, Is.EqualTo("Docs/Machine_Spirit/Local/MMDQASessions/automation_runs/run-a/report.json"));
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
        public void Given_ExistingOutputPath_When_BuildUniqueOutputPath_Then_AddsNumericSuffix()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string outputFolder = Path.Combine(root, "Docs");
            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(Path.Combine(outputFolder, "metrics.csv"), "existing");

            try
            {
                string uniquePath = MotionComparisonProbeOutputPaths.BuildUniqueOutputPath(outputFolder, "metrics.csv");

                Assert.That(Path.GetFileName(uniquePath), Is.EqualTo("metrics_001.csv"));
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
        public void Given_EvidencePathLimits_When_ShortenEvidenceBaseName_Then_ReservesExtensionAndLeafFileName()
        {
            string longBaseName = new string('a', 80) + " spaced name";

            string fileBaseName = MotionComparisonProbeOutputPaths.ShortenEvidenceBaseNameToFitFile(
                new string('f', 199),
                longBaseName,
                ".csv");
            string folderBaseName = MotionComparisonProbeOutputPaths.ShortenEvidenceBaseNameToFitFolder(
                new string('r', 190),
                longBaseName,
                "index.md");

            Assert.That(fileBaseName.Length, Is.EqualTo(32));
            Assert.That(fileBaseName, Does.Match("^a+_[0-9a-f]{8}$"));
            Assert.That(folderBaseName.Length, Is.EqualTo(36));
            Assert.That(folderBaseName, Does.Match("^a+_[0-9a-f]{8}$"));
        }

        [Test]
        public void Given_DataPath_When_BuildComparisonOutputRoots_Then_ReturnsLogAndSessionRoots()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dataPath);

            try
            {
                MotionComparisonProbeOutputRoots roots =
                    MotionComparisonProbeOutputPaths.BuildComparisonOutputRoots(dataPath);

                Assert.That(roots.ComparisonOutputFolder, Is.EqualTo(Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonLogs")));
                Assert.That(roots.ComparisonSessionRootFolder, Is.EqualTo(Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonSessions")));
                Assert.That(Directory.Exists(roots.ComparisonOutputFolder), Is.True);
                Assert.That(Directory.Exists(roots.ComparisonSessionRootFolder), Is.True);
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
        public void Given_SessionId_When_BuildSessionOutputPaths_Then_ReturnsUniqueFolderAndManifestPath()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            string existingSessionFolder = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonSessions",
                "session_01");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(existingSessionFolder);

            try
            {
                MotionComparisonProbeSessionOutputPaths paths =
                    MotionComparisonProbeOutputPaths.BuildSessionOutputPaths(dataPath, "session 01", "session-manifest.md");

                string expectedSessionFolder = existingSessionFolder + "_001";
                Assert.That(paths.SessionFolder, Is.EqualTo(expectedSessionFolder));
                Assert.That(paths.SessionManifestPath, Is.EqualTo(Path.Combine(expectedSessionFolder, "session-manifest.md")));
                Assert.That(Directory.Exists(expectedSessionFolder), Is.True);
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
        public void Given_SessionIdWithoutManifestFileName_When_BuildSessionOutputPaths_Then_UsesDefaultSessionManifestName()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dataPath);

            try
            {
                MotionComparisonProbeSessionOutputPaths paths =
                    MotionComparisonProbeOutputPaths.BuildSessionOutputPaths(dataPath, "session 02", null);

                Assert.That(paths.SessionFolder, Does.EndWith(Path.Combine("ComparisonSessions", "session_02")));
                Assert.That(paths.SessionManifestPath, Is.EqualTo(Path.Combine(paths.SessionFolder, "index.md")));
                Assert.That(Directory.Exists(paths.SessionFolder), Is.True);
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
        public void Given_SessionStamp_When_BuildScreenshotOutputPaths_Then_ReturnsUniqueFolderAndIndexPaths()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            string existingScreenshotFolder = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonFrames",
                "when-stamp_01");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(existingScreenshotFolder);

            try
            {
                MotionComparisonProbeScreenshotOutputPaths paths =
                    MotionComparisonProbeOutputPaths.BuildScreenshotOutputPaths(dataPath, "stamp 01", "session_index.md");

                string expectedScreenshotFolder = existingScreenshotFolder + "_001";
                Assert.That(paths.ScreenshotFolder, Is.EqualTo(expectedScreenshotFolder));
                Assert.That(paths.ScreenshotIndexPath, Is.EqualTo(Path.Combine(expectedScreenshotFolder, "index.csv")));
                Assert.That(paths.ScreenshotSessionIndexPath, Is.EqualTo(Path.Combine(expectedScreenshotFolder, "session_index.md")));
                Assert.That(Directory.Exists(expectedScreenshotFolder), Is.True);
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
        public void Given_ScreenshotSessionInputs_When_BuildScreenshotSessionOutputPaths_Then_CentralizesFrameFolderAndSessionIndexData()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            string sessionManifestPath = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonSessions",
                "session-a",
                "index.md");
            string metricsCsvPath = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonLogs",
                "metrics.csv");
            string existingScreenshotFolder = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonFrames",
                "when-stamp_01");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(existingScreenshotFolder);

            try
            {
                System.Reflection.MethodInfo method = typeof(MotionComparisonProbeOutputPaths).GetMethod(
                    "BuildScreenshotSessionOutputPaths",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                Assert.That(method, Is.Not.Null);

                object paths = method.Invoke(
                    null,
                    new object[] { dataPath, "stamp 01", "session-a", sessionManifestPath, metricsCsvPath });

                Type pathsType = paths.GetType();
                string screenshotFolder = (string)pathsType.GetField("ScreenshotFolder").GetValue(paths);
                string screenshotIndexPath = (string)pathsType.GetField("ScreenshotIndexPath").GetValue(paths);
                string screenshotSessionIndexPath = (string)pathsType.GetField("ScreenshotSessionIndexPath").GetValue(paths);
                object frameSessionIndexData = pathsType.GetField("FrameSessionIndexData").GetValue(paths);
                Type frameSessionIndexDataType = frameSessionIndexData.GetType();
                string expectedScreenshotFolder = Path.Combine(
                    root,
                    "Docs",
                    "Machine_Spirit",
                    "Local",
                    "ComparisonFrames",
                    "when-stamp_01_001");

                Assert.That(screenshotFolder, Is.EqualTo(expectedScreenshotFolder));
                Assert.That(screenshotIndexPath, Is.EqualTo(Path.Combine(expectedScreenshotFolder, "index.csv")));
                Assert.That(screenshotSessionIndexPath, Is.EqualTo(Path.Combine(expectedScreenshotFolder, "session_index.md")));
                Assert.That(Directory.Exists(expectedScreenshotFolder), Is.True);
                Assert.That(frameSessionIndexDataType.GetField("SessionId").GetValue(frameSessionIndexData), Is.EqualTo("session-a"));
                Assert.That(
                    frameSessionIndexDataType.GetField("SessionManifestRelativePath").GetValue(frameSessionIndexData),
                    Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonSessions/session-a/index.md"));
                Assert.That(
                    frameSessionIndexDataType.GetField("MetricsCsvRelativePath").GetValue(frameSessionIndexData),
                    Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonLogs/metrics.csv"));
                Assert.That(
                    frameSessionIndexDataType.GetField("FrameIndexCsvRelativePath").GetValue(frameSessionIndexData),
                    Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames/when-stamp_01_001/index.csv"));
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
        public void Given_SessionArtifactInputs_When_BuildSessionArtifactOutputPaths_Then_CentralizesSessionAndScreenshotArtifacts()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            string metricsCsvPath = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonLogs",
                "metrics.csv");
            string existingSessionFolder = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonSessions",
                "session-a");
            string existingScreenshotFolder = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "ComparisonFrames",
                "when-stamp_01");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(existingSessionFolder);
            Directory.CreateDirectory(existingScreenshotFolder);

            try
            {
                System.Reflection.MethodInfo method = typeof(MotionComparisonProbeOutputPaths).GetMethod(
                    "BuildSessionArtifactOutputPaths",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                Assert.That(method, Is.Not.Null);

                object paths = method.Invoke(
                    null,
                    new object[] { dataPath, "stamp 01", "session-a", metricsCsvPath, true });

                Type pathsType = paths.GetType();
                string sessionFolder = (string)pathsType.GetField("SessionFolder").GetValue(paths);
                string sessionManifestPath = (string)pathsType.GetField("SessionManifestPath").GetValue(paths);
                string screenshotFolder = (string)pathsType.GetField("ScreenshotFolder").GetValue(paths);
                string screenshotIndexPath = (string)pathsType.GetField("ScreenshotIndexPath").GetValue(paths);
                string screenshotSessionIndexPath = (string)pathsType.GetField("ScreenshotSessionIndexPath").GetValue(paths);
                object frameSessionIndexData = pathsType.GetField("FrameSessionIndexData").GetValue(paths);
                Type frameSessionIndexDataType = frameSessionIndexData.GetType();
                string expectedSessionFolder = Path.Combine(
                    root,
                    "Docs",
                    "Machine_Spirit",
                    "Local",
                    "ComparisonSessions",
                    "session-a_001");
                string expectedScreenshotFolder = Path.Combine(
                    root,
                    "Docs",
                    "Machine_Spirit",
                    "Local",
                    "ComparisonFrames",
                    "when-stamp_01_001");

                Assert.That(sessionFolder, Is.EqualTo(expectedSessionFolder));
                Assert.That(sessionManifestPath, Is.EqualTo(Path.Combine(expectedSessionFolder, "index.md")));
                Assert.That(screenshotFolder, Is.EqualTo(expectedScreenshotFolder));
                Assert.That(screenshotIndexPath, Is.EqualTo(Path.Combine(expectedScreenshotFolder, "index.csv")));
                Assert.That(screenshotSessionIndexPath, Is.EqualTo(Path.Combine(expectedScreenshotFolder, "session_index.md")));
                Assert.That(Directory.Exists(expectedSessionFolder), Is.True);
                Assert.That(Directory.Exists(expectedScreenshotFolder), Is.True);
                Assert.That(frameSessionIndexDataType.GetField("SessionId").GetValue(frameSessionIndexData), Is.EqualTo("session-a"));
                Assert.That(
                    frameSessionIndexDataType.GetField("SessionManifestRelativePath").GetValue(frameSessionIndexData),
                    Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonSessions/session-a_001/index.md"));
                Assert.That(
                    frameSessionIndexDataType.GetField("MetricsCsvRelativePath").GetValue(frameSessionIndexData),
                    Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonLogs/metrics.csv"));
                Assert.That(
                    frameSessionIndexDataType.GetField("FrameIndexCsvRelativePath").GetValue(frameSessionIndexData),
                    Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames/when-stamp_01_001/index.csv"));
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
        public void Given_ScreenshotFolderAndFileName_When_BuildScreenshotPngPath_Then_ReturnsCombinedPathWithoutCreatingFolder()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string screenshotFolder = Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames", "when-stamp_01");

            string path = MotionComparisonProbeOutputPaths.BuildScreenshotPngPath(screenshotFolder, "pose_reason_rt-front_frame-000120.png");

            Assert.That(path, Is.EqualTo(Path.Combine(screenshotFolder, "pose_reason_rt-front_frame-000120.png")));
            Assert.That(Directory.Exists(screenshotFolder), Is.False);
        }

        [Test]
        public void Given_ScreenshotFileNameInputs_When_BuildScreenshotPngFileName_Then_SanitizesReasonAndKeepsFormat()
        {
            string fileName = MotionComparisonProbeOutputPaths.BuildScreenshotPngFileName(
                "thumb risk high",
                "left-hand-front",
                "000123");

            Assert.That(fileName, Is.EqualTo("pose_thumb_risk_high_rt-left-hand-front_frame-000123.png"));
        }

        [Test]
        public void Given_ScreenshotFileNamePartsWithInvalidChars_When_BuildScreenshotPngFileName_Then_SanitizesAllDynamicParts()
        {
            string fileName = MotionComparisonProbeOutputPaths.BuildScreenshotPngFileName(
                "thumb/risk high",
                "left/hand front",
                "00:12");

            Assert.That(fileName, Is.EqualTo("pose_thumb_risk_high_rt-left_hand_front_frame-00_12.png"));
        }

        [Test]
        public void Given_ScreenshotFrameNumbers_When_BuildScreenshotFrameName_Then_UsesRecorderFrameOrFallback()
        {
            Assert.That(MotionComparisonProbeOutputPaths.BuildScreenshotFrameName(12, 34), Is.EqualTo("000012"));
            Assert.That(MotionComparisonProbeOutputPaths.BuildScreenshotFrameName(-1, 34), Is.EqualTo("000034"));
        }

        [Test]
        public void Given_ScreenshotViewInputs_When_BuildScreenshotViewNames_Then_UsesStableTokens()
        {
            Assert.That(MotionComparisonProbeOutputPaths.BuildSampleScreenshotViewName(frontView: true), Is.EqualTo("front"));
            Assert.That(MotionComparisonProbeOutputPaths.BuildSampleScreenshotViewName(frontView: false), Is.EqualTo("right"));
            Assert.That(MotionComparisonProbeOutputPaths.BuildFingerCloseupViewName(leftHand: true, frontView: true), Is.EqualTo("left-hand-front"));
            Assert.That(MotionComparisonProbeOutputPaths.BuildFingerCloseupViewName(leftHand: true, frontView: false), Is.EqualTo("left-hand-right"));
            Assert.That(MotionComparisonProbeOutputPaths.BuildFingerCloseupViewName(leftHand: false, frontView: true), Is.EqualTo("right-hand-front"));
            Assert.That(MotionComparisonProbeOutputPaths.BuildFingerCloseupViewName(leftHand: false, frontView: false), Is.EqualTo("right-hand-right"));
        }

        [Test]
        public void Given_ScreenshotSampleNameInputs_When_BuildScreenshotCaptureNames_Then_CentralizesFrameAndViewTokens()
        {
            System.Reflection.MethodInfo method = typeof(MotionComparisonProbeOutputPaths).GetMethod(
                "BuildScreenshotCaptureNames",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            Assert.That(method, Is.Not.Null);

            object names = method.Invoke(null, new object[] { -1, 34 });

            Type namesType = names.GetType();
            Assert.That(namesType.GetField("FrameName").GetValue(names), Is.EqualTo("000034"));
            Assert.That(namesType.GetField("FrontViewName").GetValue(names), Is.EqualTo("front"));
            Assert.That(namesType.GetField("RightViewName").GetValue(names), Is.EqualTo("right"));
            Assert.That(namesType.GetField("LeftHandFrontViewName").GetValue(names), Is.EqualTo("left-hand-front"));
            Assert.That(namesType.GetField("LeftHandRightViewName").GetValue(names), Is.EqualTo("left-hand-right"));
            Assert.That(namesType.GetField("RightHandFrontViewName").GetValue(names), Is.EqualTo("right-hand-front"));
            Assert.That(namesType.GetField("RightHandRightViewName").GetValue(names), Is.EqualTo("right-hand-right"));
        }

        [Test]
        public void Given_EvidenceBaseNameInputs_When_BuildEvidenceBaseName_Then_SanitizesAndOrdersSegments()
        {
            string baseName = MotionComparisonProbeOutputPaths.BuildEvidenceBaseName(
                "20260521 143000",
                "Main Auto",
                "label with space",
                "metrics/csv",
                "session:probe",
                "probe run");

            Assert.That(baseName, Is.EqualTo("when-20260521_143000_where-Main_Auto_who-label_with_space_what-metrics_csv_why-session_probe_how-probe_run"));
        }

        [Test]
        public void Given_SessionIdentityInputs_When_BuildEvidenceNames_Then_UsesReportPurposeSegments()
        {
            string metricsBaseName = MotionComparisonProbeOutputPaths.BuildMetricsEvidenceBaseName(
                "20260521 143000",
                "Main Auto",
                "label with space");
            string sessionIdBaseName = MotionComparisonProbeOutputPaths.BuildComparisonSessionIdBaseName(
                "20260521 143000",
                "Main Auto",
                "label with space");

            Assert.That(metricsBaseName, Is.EqualTo("when-20260521_143000_where-Main_Auto_who-label_with_space_what-metrics_why-session_how-probe"));
            Assert.That(sessionIdBaseName, Is.EqualTo("when-20260521_143000_where-Main_Auto_who-label_with_space_what-comparison-session_why-motion-analysis_how-probe"));
        }

        [Test]
        public void Given_SamplingSessionInputs_When_BuildSamplingSessionOutputPaths_Then_CentralizesEvidenceCsvAndSessionNames()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dataPath);

            try
            {
                MotionComparisonProbeOutputRoots roots =
                    MotionComparisonProbeOutputPaths.BuildComparisonOutputRoots(dataPath);
                string expectedEvidenceBaseName = MotionComparisonProbeOutputPaths.ShortenEvidenceBaseNameToFitFile(
                    roots.ComparisonOutputFolder,
                    MotionComparisonProbeOutputPaths.BuildMetricsEvidenceBaseName(
                        "20260521-143000",
                        "Main Auto",
                        "label with space"),
                    MotionComparisonProbeOutputPaths.BuildMetricsCsvExtension());
                string expectedSessionId = MotionComparisonProbeOutputPaths.ShortenEvidenceBaseNameToFitFolder(
                    roots.ComparisonSessionRootFolder,
                    MotionComparisonProbeOutputPaths.BuildComparisonSessionIdBaseName(
                        "20260521-143000",
                        "Main Auto",
                        "label with space"),
                    MotionComparisonProbeOutputPaths.BuildSessionManifestFileName());
                string expectedMetricsCsvPath = MotionComparisonProbeOutputPaths.BuildMetricsCsvOutputPath(
                    roots.ComparisonOutputFolder,
                    MotionComparisonProbeOutputPaths.BuildMetricsCsvFileName(expectedEvidenceBaseName));

                System.Reflection.MethodInfo method = typeof(MotionComparisonProbeOutputPaths).GetMethod(
                    "BuildSamplingSessionOutputPaths",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                Assert.That(method, Is.Not.Null);

                object paths = method.Invoke(
                    null,
                    new object[] { dataPath, "20260521-143000", "Main Auto", "label with space" });

                Type pathsType = paths.GetType();
                string evidenceBaseName = (string)pathsType.GetField("EvidenceBaseName").GetValue(paths);
                string sessionId = (string)pathsType.GetField("SessionId").GetValue(paths);
                string metricsCsvPath = (string)pathsType.GetField("MetricsCsvPath").GetValue(paths);

                Assert.That(evidenceBaseName, Is.EqualTo(expectedEvidenceBaseName));
                Assert.That(sessionId, Is.EqualTo(expectedSessionId));
                Assert.That(metricsCsvPath, Is.EqualTo(expectedMetricsCsvPath));
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
        public void Given_ReportArtifactFileNames_When_BuildNames_Then_UsesStableSessionAndCsvNames()
        {
            Assert.That(
                MotionComparisonProbeOutputPaths.BuildSessionManifestFileName(),
                Is.EqualTo("index.md"));
            Assert.That(
                MotionComparisonProbeOutputPaths.BuildFrameSessionIndexFileName(),
                Is.EqualTo("session_index.md"));
            Assert.That(
                MotionComparisonProbeOutputPaths.BuildMetricsCsvExtension(),
                Is.EqualTo(".csv"));
            Assert.That(
                MotionComparisonProbeOutputPaths.BuildMetricsCsvFileName("when-demo"),
                Is.EqualTo("when-demo.csv"));
        }

        [Test]
        public void Given_ComparisonOutputFolderAndMetricsFileName_When_BuildMetricsCsvOutputPath_Then_ReturnsUniquePath()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string comparisonOutputFolder = Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonLogs");

            try
            {
                Directory.CreateDirectory(comparisonOutputFolder);
                File.WriteAllText(Path.Combine(comparisonOutputFolder, "metrics.csv"), "existing");

                string path = MotionComparisonProbeOutputPaths.BuildMetricsCsvOutputPath(
                    comparisonOutputFolder,
                    "metrics.csv");

                Assert.That(path, Is.EqualTo(Path.Combine(comparisonOutputFolder, "metrics_001.csv")));
                Assert.That(File.Exists(path), Is.False);
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
        public void Given_SessionManifestArtifactPaths_When_BuildSessionManifestOutputPaths_Then_UsesProjectRelativeForwardSlashPaths()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");

            MotionComparisonProbeSessionManifestOutputPaths paths = MotionComparisonProbeOutputPaths.BuildSessionManifestOutputPaths(
                dataPath,
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonLogs", "metrics.csv"),
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames"),
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames", "index.csv"),
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames", "session_index.md"));

            Assert.That(paths.MetricsCsvRelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonLogs/metrics.csv"));
            Assert.That(paths.FrameFolderRelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames"));
            Assert.That(paths.FrameIndexCsvRelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames/index.csv"));
            Assert.That(paths.FrameSessionIndexRelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames/session_index.md"));
        }

        [Test]
        public void Given_FrameSessionIndexPaths_When_BuildFrameSessionIndexData_Then_UsesProjectRelativeForwardSlashPaths()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");

            MotionComparisonProbeFrameSessionIndexData data = MotionComparisonProbeOutputPaths.BuildFrameSessionIndexData(
                dataPath,
                "session-a",
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonSessions", "index.md"),
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonLogs", "metrics.csv"),
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames", "index.csv"));

            Assert.That(data.SessionId, Is.EqualTo("session-a"));
            Assert.That(data.SessionManifestRelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonSessions/index.md"));
            Assert.That(data.MetricsCsvRelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonLogs/metrics.csv"));
            Assert.That(data.FrameIndexCsvRelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames/index.csv"));
        }

        [Test]
        public void Given_ScreenshotIndexInputs_When_BuildScreenshotIndexRow_Then_UsesProjectRelativeForwardSlashPath()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");

            MotionComparisonProbeScreenshotIndexRow row = MotionComparisonProbeOutputPaths.BuildScreenshotIndexRow(
                dataPath,
                "label",
                "scene",
                "reason",
                42,
                "front",
                Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames", "frame.png"));

            Assert.That(row.ComparisonLabel, Is.EqualTo("label"));
            Assert.That(row.SceneName, Is.EqualTo("scene"));
            Assert.That(row.Reason, Is.EqualTo("reason"));
            Assert.That(row.RecorderFrame, Is.EqualTo(42));
            Assert.That(row.ViewName, Is.EqualTo("front"));
            Assert.That(row.RelativePath, Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames/frame.png"));
        }

        [Test]
        public void Given_ScreenshotCaptureInputs_When_BuildScreenshotCaptureOutputPaths_Then_CentralizesFilePathAndIndexRow()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            string screenshotFolder = Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames", "when-stamp");

            System.Reflection.MethodInfo method = typeof(MotionComparisonProbeOutputPaths).GetMethod(
                "BuildScreenshotCaptureOutputPaths",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            Assert.That(method, Is.Not.Null);

            object paths = method.Invoke(
                null,
                new object[]
                {
                    dataPath,
                    screenshotFolder,
                    "label",
                    "scene",
                    "thumb/risk high",
                    42,
                    "left/hand front",
                    "00:12"
                });

            Type pathsType = paths.GetType();
            string screenshotFileName = (string)pathsType.GetField("ScreenshotFileName").GetValue(paths);
            string screenshotPath = (string)pathsType.GetField("ScreenshotPath").GetValue(paths);
            object indexRow = pathsType.GetField("IndexRow").GetValue(paths);
            Type indexRowType = indexRow.GetType();

            Assert.That(screenshotFileName, Is.EqualTo("pose_thumb_risk_high_rt-left_hand_front_frame-00_12.png"));
            Assert.That(screenshotPath, Is.EqualTo(Path.Combine(screenshotFolder, screenshotFileName)));
            Assert.That(indexRowType.GetField("ComparisonLabel").GetValue(indexRow), Is.EqualTo("label"));
            Assert.That(indexRowType.GetField("SceneName").GetValue(indexRow), Is.EqualTo("scene"));
            Assert.That(indexRowType.GetField("Reason").GetValue(indexRow), Is.EqualTo("thumb/risk high"));
            Assert.That(indexRowType.GetField("RecorderFrame").GetValue(indexRow), Is.EqualTo(42));
            Assert.That(indexRowType.GetField("ViewName").GetValue(indexRow), Is.EqualTo("left/hand front"));
            Assert.That(
                indexRowType.GetField("RelativePath").GetValue(indexRow),
                Is.EqualTo("Docs/Machine_Spirit/Local/ComparisonFrames/when-stamp/pose_thumb_risk_high_rt-left_hand_front_frame-00_12.png"));
        }

        [Test]
        public void Given_MmdAfterPlayScreenshotPath_When_BuildMmdModelScreenshotPath_Then_AppendsModelSuffix()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string screenshotPath = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "MMDQASessions",
                "automation_runs",
                "run-a",
                "screenshots",
                "06_after_play.png");

            string modelScreenshotPath = MotionComparisonProbeOutputPaths.BuildMmdModelScreenshotPath(screenshotPath);

            Assert.That(modelScreenshotPath, Is.EqualTo(Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "MMDQASessions",
                "automation_runs",
                "run-a",
                "screenshots",
                "06_after_play_model.png")));
        }

        [Test]
        public void Given_MmdScreenshotsDirectory_When_BuildMmdAfterPlayScreenshotPaths_Then_ReturnsKnownFallbackFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string screenshotsDir = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "MMDQASessions",
                "automation_runs",
                "run-a",
                "screenshots");

            string modelScreenshotPath = MotionComparisonProbeOutputPaths.BuildMmdAfterPlayModelScreenshotPath(screenshotsDir);
            string fullScreenshotPath = MotionComparisonProbeOutputPaths.BuildMmdAfterPlayFullScreenshotPath(screenshotsDir);

            Assert.That(modelScreenshotPath, Is.EqualTo(Path.Combine(screenshotsDir, "06_after_play_model.png")));
            Assert.That(fullScreenshotPath, Is.EqualTo(Path.Combine(screenshotsDir, "06_after_play.png")));
        }

        [Test]
        public void Given_ReportRelativeArtifactPath_When_ResolveMmdReportArtifactPath_Then_PrefersReportDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string reportDirectory = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "MMDQASessions",
                "automation_runs",
                "run-a");
            string reportScreenshot = Path.Combine(reportDirectory, "screenshots", "06_after_play.png");
            string staleProjectScreenshot = Path.Combine(root, "screenshots", "06_after_play.png");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportScreenshot));
                Directory.CreateDirectory(Path.GetDirectoryName(staleProjectScreenshot));
                File.WriteAllBytes(reportScreenshot, new byte[] { 1 });
                File.WriteAllBytes(staleProjectScreenshot, new byte[] { 2 });

                string resolved = MotionComparisonProbeOutputPaths.ResolveMmdReportArtifactPath(
                    "screenshots/06_after_play.png",
                    root,
                    reportDirectory);

                Assert.That(resolved, Is.EqualTo(reportScreenshot));
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
        public void Given_ReportRelativeDirectoryPath_When_ResolveMmdReportDirectoryPath_Then_PrefersReportDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string reportDirectory = Path.Combine(
                root,
                "Docs",
                "Machine_Spirit",
                "Local",
                "MMDQASessions",
                "automation_runs",
                "run-a");
            string reportScreenshots = Path.Combine(reportDirectory, "screenshots");
            string staleProjectScreenshots = Path.Combine(root, "screenshots");

            try
            {
                Directory.CreateDirectory(reportScreenshots);
                Directory.CreateDirectory(staleProjectScreenshots);

                string resolved = MotionComparisonProbeOutputPaths.ResolveMmdReportDirectoryPath(
                    "screenshots",
                    root,
                    reportDirectory);

                Assert.That(resolved, Is.EqualTo(reportScreenshots));
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
        public void Given_FolderPath_When_EnsureDirectoryExists_Then_CreatesFolder()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string folderPath = Path.Combine(root, "Docs", "Machine_Spirit", "Local", "ComparisonFrames");

            try
            {
                MotionComparisonProbeOutputPaths.EnsureDirectoryExists(folderPath);

                Assert.That(Directory.Exists(folderPath), Is.True);
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
        public void Given_ProbeSource_When_CheckedForDirectIoApis_Then_PathIoResponsibilityStaysInHelpers()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "VMDRecorderSample",
                "SampleScript",
                "MotionComparisonProbe.cs");

            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Not.Contain("using System.IO;"));
            Assert.That(source, Does.Not.Contain("File."));
            Assert.That(source, Does.Not.Contain("Directory."));
            Assert.That(source, Does.Not.Contain("Path."));
        }

        [Test]
        public void Given_ProbeSource_When_CheckedForScreenshotCapturePathHelpers_Then_UsesConsolidatedCapturePathHelper()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "VMDRecorderSample",
                "SampleScript",
                "MotionComparisonProbe.cs");

            string source = File.ReadAllText(sourcePath);
            string[] screenshotCapturePathHelpers =
            {
                "BuildScreenshotPngFileName",
                "BuildScreenshotPngPath",
                "BuildScreenshotIndexRow"
            };

            foreach (string helperName in screenshotCapturePathHelpers)
            {
                Assert.That(
                    source,
                    Does.Not.Contain($"MotionComparisonProbeOutputPaths.{helperName}"),
                    $"{helperName} belongs behind BuildScreenshotCaptureOutputPaths in {nameof(MotionComparisonProbeOutputPaths)}.");
            }

            Assert.That(
                CountOccurrences(source, "MotionComparisonProbeOutputPaths.BuildScreenshotCaptureOutputPaths"),
                Is.EqualTo(1));
        }

        [Test]
        public void Given_ProbeSource_When_CheckedForScreenshotSessionPathHelpers_Then_UsesConsolidatedScreenshotSessionPathHelper()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "VMDRecorderSample",
                "SampleScript",
                "MotionComparisonProbe.cs");

            string source = File.ReadAllText(sourcePath);
            string[] screenshotSessionPathHelpers =
            {
                "BuildScreenshotOutputPaths",
                "BuildFrameSessionIndexFileName",
                "BuildFrameSessionIndexData",
                "BuildScreenshotSessionOutputPaths"
            };

            foreach (string helperName in screenshotSessionPathHelpers)
            {
                Assert.That(
                    source,
                    Does.Not.Contain($"MotionComparisonProbeOutputPaths.{helperName}"),
                    $"{helperName} belongs behind BuildSessionArtifactOutputPaths in {nameof(MotionComparisonProbeOutputPaths)}.");
            }

            Assert.That(
                CountOccurrences(source, "MotionComparisonProbeOutputPaths.BuildSessionArtifactOutputPaths"),
                Is.EqualTo(1));
        }

        [Test]
        public void Given_ProbeSource_When_CheckedForScreenshotCaptureNameHelpers_Then_UsesConsolidatedNameHelper()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "VMDRecorderSample",
                "SampleScript",
                "MotionComparisonProbe.cs");

            string source = File.ReadAllText(sourcePath);
            string[] screenshotCaptureNameHelpers =
            {
                "BuildScreenshotFrameName",
                "BuildSampleScreenshotViewName",
                "BuildFingerCloseupViewName"
            };

            foreach (string helperName in screenshotCaptureNameHelpers)
            {
                Assert.That(
                    source,
                    Does.Not.Contain($"MotionComparisonProbeOutputPaths.{helperName}"),
                    $"{helperName} belongs behind BuildScreenshotCaptureNames in {nameof(MotionComparisonProbeOutputPaths)}.");
            }

            Assert.That(
                CountOccurrences(source, "MotionComparisonProbeOutputPaths.BuildScreenshotCaptureNames"),
                Is.EqualTo(1));
        }

        [Test]
        public void Given_ProbeSource_When_CheckedForSessionOutputPathHelpers_Then_UsesDefaultManifestNameBehindSessionOutputHelper()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "VMDRecorderSample",
                "SampleScript",
                "MotionComparisonProbe.cs");

            string source = File.ReadAllText(sourcePath);

            Assert.That(
                source,
                Does.Not.Contain("MotionComparisonProbeOutputPaths.BuildSessionManifestFileName"),
                $"{nameof(MotionComparisonProbeOutputPaths.BuildSessionManifestFileName)} belongs behind BuildSessionArtifactOutputPaths in {nameof(MotionComparisonProbeOutputPaths)}.");
            Assert.That(
                source,
                Does.Not.Contain("MotionComparisonProbeOutputPaths.BuildSessionOutputPaths"),
                $"{nameof(MotionComparisonProbeOutputPaths.BuildSessionOutputPaths)} belongs behind BuildSessionArtifactOutputPaths in {nameof(MotionComparisonProbeOutputPaths)}.");
            Assert.That(
                CountOccurrences(source, "MotionComparisonProbeOutputPaths.BuildSessionArtifactOutputPaths"),
                Is.EqualTo(1));
        }

        [Test]
        public void Given_ProbeSource_When_CheckedForSessionManifestArtifactPaths_Then_DoesNotUnpackRelativePathFields()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "VMDRecorderSample",
                "SampleScript",
                "MotionComparisonProbe.cs");

            string source = File.ReadAllText(sourcePath);
            string[] relativePathFields =
            {
                ".MetricsCsvRelativePath",
                ".FrameFolderRelativePath",
                ".FrameIndexCsvRelativePath",
                ".FrameSessionIndexRelativePath"
            };

            foreach (string fieldName in relativePathFields)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(fieldName),
                    $"{fieldName} belongs in the session manifest artifact path bundle.");
            }

            Assert.That(
                CountOccurrences(source, "MotionComparisonProbeOutputPaths.BuildSessionManifestOutputPaths"),
                Is.EqualTo(1));
        }

        [Test]
        public void Given_ProbeSource_When_CheckedForSamplingStartupPathHelpers_Then_UsesConsolidatedSessionPathHelper()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "VMDRecorderSample",
                "SampleScript",
                "MotionComparisonProbe.cs");

            string source = File.ReadAllText(sourcePath);
            string[] startupPathHelpers =
            {
                "BuildComparisonOutputRoots",
                "BuildMetricsEvidenceBaseName",
                "ShortenEvidenceBaseNameToFitFile",
                "BuildComparisonSessionIdBaseName",
                "ShortenEvidenceBaseNameToFitFolder",
                "BuildMetricsCsvOutputPath",
                "BuildMetricsCsvFileName",
                "BuildMetricsCsvExtension"
            };

            foreach (string helperName in startupPathHelpers)
            {
                Assert.That(
                    source,
                    Does.Not.Contain($"MotionComparisonProbeOutputPaths.{helperName}"),
                    $"{helperName} belongs behind BuildSamplingSessionOutputPaths in {nameof(MotionComparisonProbeOutputPaths)}.");
            }
        }

        private static int CountOccurrences(string value, string search)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(search))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += search.Length;
            }

            return count;
        }
    }
}
