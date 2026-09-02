using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public sealed class YybVisualComparisonBatchRunnerStructureTests
    {
        [Test]
        public void Given_ExtractedResponsibilities_When_CheckingRunner_Then_NoLegacyPrivateWrappersRemain()
        {
            string[] obsoleteMethodNames =
            {
                "ApplyFinalIkFootGroundingRuntimeOverride",
                "ApplyManualAnimatorFootLocalRotationRuntimeOverride",
                "ApplySetHumanPoseRightLegTwistOutputRuntimeOverride",
                "ApplyManualAnimatorHandLocalRotationRuntimeOverride",
                "ApplyManualAnimatorThumbLocalRotationRuntimeOverride",
                "ApplyManualAnimatorHandPalmFrameRuntimeOverride",
                "ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride",
                "ApplyRetargetArmStretchClampRuntimeOverride",
                "ApplyYybArmSleeveAnchorRuntimeOverride",
                "ApplyYybArmVisualTwistRuntimeOverride",
                "HasManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride",
                "ApplyManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride",
                "ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride",
                "ApplyPreSetHumanPoseEndpointPositionRuntimeOverride",
                "ApplyManualAnimatorBodyPositionXzRuntimeOverride",
                "ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride",
                "ApplyTargetHumanoidBonePositionLockRuntimeOverride",
                "ApplyRetargetBodyPositionXzRootMotionRuntimeOverride",
                "NormalizeMmdIkDeltaGuardLimitOverride",
                "NormalizePositiveFloat",
                "NormalizeFiniteFloat",
                "NormalizeMmdIkDeltaGuardRecoveryHoldFrames",
                "NormalizeDiagnosticCaptureDimensionOverride",
                "NormalizeDiagnosticScreenshotPaddingOverride",
                "NormalizeDiagnosticScreenshotVerticalViewportCenterOverride",
                "FindManualRecorder",
                "TryResolveKnownMmdReferenceTargetFrameCount",
                "IsGroundingVerticalStepAtMax",
                "CalculateMetricIntSpan",
                "CalculateMetricFloatSpan",
                "BuildCsvIndexMap",
                "GetCsvString",
                "GetCsvInt",
                "NormalizeFbxFileName",
                "EscapeJson",
                "ApplyMmdIkDeltaGuardRuntimeOverride",
                "ApplyVmdPlaybackProbeRuntimeOverride",
                "ApplyManualAnimatorFullBodyPoseRuntimeOverride",
                "ApplyManualAnimatorBodyRotationRuntimeOverride",
                "ApplyYybArmSwingLimitRuntimeOverride",
                "ApplyYybArmDirectionRetargetRuntimeOverride",
                "ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride",
                "ApplyManualAnimatorBipedIkFootPositionRuntimeOverride",
                "ApplyPostSetHumanPoseEndpointPositionRuntimeOverride",
                "ApplyManualAnimatorHipsLocalPositionRuntimeOverride",
                "BuildCandidateVmdEvidenceFileName",
                "FormatProbeSampleTimes",
                "GetEditorDiagnosticSmokeSegmentLabel",
                "ToAbsoluteProjectPath",
                "MakeProjectRelativePath",
                "SanitizeFileName",
                "CopyLatestSummary",
                "WriteSummaryJson",
                "GetCsvFloat",
                "ResolveGroundingStepToMaxRatio",
                "CalculateEditorDiagnosticSmokeStartTime",
                "ResolveEditorDiagnosticSmokeSegment",
                "CaptureFBXVmdPipelineEffectiveSettings",
                "RequestRuntimeDiagnosticScriptRefresh",
                "ResolveMainAutoFrameCount",
                "BuildFrameQualityFailureMessages",
                "HasAcceptedUserFacingArtifactPreservingRawDiagnostic",
                "BuildManualAnimatorCapturePlan",
                "IsMainSceneCandidateMode",
                "ShouldBuildFrameQualityDiagnostic",
                "ResolveIntegratedVerticalSolveRole",
                "ResolveIntegratedVerticalSolveBasis",
                "CanStartNextJob",
                "SelectActiveManualRecorder",
                "BuildStableCandidateResult",
                "BuildCandidateVmdEvidencePath",
                "SanitizeComparisonArtifactFileName"
            };

            MethodInfo[] privateStaticMethods = typeof(YybVisualComparisonBatchRunner).GetMethods(
                BindingFlags.NonPublic | BindingFlags.Static);
            string[] remainingMethodNames = obsoleteMethodNames
                .Where(methodName => privateStaticMethods.Any(method => method.Name == methodName))
                .ToArray();

            Assert.That(
                remainingMethodNames,
                Is.Empty,
                $"추출 완료된 private wrapper가 남아 있습니다: {string.Join(", ", remainingMethodNames)}");
        }

        [Test]
        public void Given_ReferenceAlignedSampleTimePlanner_When_CheckingRunner_Then_KeepsOnlyRuntimeAdapter()
        {
            MethodInfo[] sampleTimeMethods = typeof(YybVisualComparisonBatchRunner).GetMethods(
                    BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => method.Name == "BuildReferenceMp4AlignedProbeSampleTimes")
                .ToArray();

            Assert.That(
                sampleTimeMethods,
                Has.Length.EqualTo(1),
                "참조 정렬 샘플 시간 계산은 pure planner가 소유하고 runner에는 런타임 입력 adapter 하나만 남아야 합니다.");
            Assert.That(sampleTimeMethods[0].GetParameters(), Has.Length.EqualTo(2));
        }

        [Test]
        public void Given_RuntimeDiagnosticRefreshPaths_When_CheckingRunner_Then_UsesCurrentScriptLocations()
        {
            FieldInfo field = typeof(YybVisualComparisonBatchRunner).GetField(
                "RuntimeDiagnosticScriptPaths",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null);

            var paths = (string[])field.GetValue(null);
            Assert.That(
                paths,
                Does.Contain("Assets/_Project/Scripts/FBXImporter/YybVisualComparisonBatchRunner.cs"));
            Assert.That(
                paths,
                Does.Contain("Assets/_Project/Scripts/FBXImporter/YybVisualComparisonRequestWatcher.cs"));
            Assert.That(
                paths.Any(path => path.Contains("/Editor/YybVisualComparison")),
                Is.False,
                "이동 전 Editor 하위 경로가 남으면 실제 진단 스크립트가 refresh 대상에서 제외됩니다.");
        }
    }
}
