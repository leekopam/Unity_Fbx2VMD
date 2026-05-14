using NUnit.Framework;

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
    }
}

