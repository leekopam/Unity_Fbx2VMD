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
                "SanitizeFileName"
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
    }
}
