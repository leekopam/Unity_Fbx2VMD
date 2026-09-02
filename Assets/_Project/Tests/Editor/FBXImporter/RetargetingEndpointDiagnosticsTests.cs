using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RetargetingEndpointDiagnosticsTests
    {
        private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly Type[] StagePositionJumpParameterTypes =
        {
            typeof(string[]),
            typeof(Vector3[]),
            typeof(float),
            typeof(string).MakeByRefType(),
            typeof(Vector3).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        [Test]
        public void Given_FootAndToesReference_When_CalculatingPosition_Then_RecordsEachCorrectionStage()
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            Type snapshotType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnosticSnapshot");
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateReferencePosition",
                StaticNonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType(),
                    snapshotType.MakeByRefType()
                },
                modifiers: null);
            Assert.That(method, Is.Not.Null);

            Vector3 desiredFootPosition = new Vector3(0.4f, 1f, 0.4f);
            Vector3 desiredToesPosition = new Vector3(0.8f, 0.2f, 0.2f);
            Vector3 currentFootPosition = new Vector3(0f, 1f, 0f);
            Vector3 currentToesPosition = new Vector3(0f, 0.2f, 0f);
            const float weight = 0.5f;
            const float maxOffset = 0.3f;
            const float positiveZScale = 0.5f;
            const float toesBlendWeight = 1f;
            object[] args =
            {
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                currentFootPosition,
                Activator.CreateInstance(snapshotType)
            };

            bool calculated = (bool)method.Invoke(null, args);
            Vector3 nextFootPosition = (Vector3)args[8];
            object snapshot = args[9];

            Vector3 footDelta = desiredFootPosition - currentFootPosition;
            footDelta.y = 0f;
            Vector3 toesDelta = desiredToesPosition - currentToesPosition;
            toesDelta.y = 0f;
            Vector3 beforeClamp = (footDelta + toesDelta) * 0.5f;
            Vector3 afterClamp = Vector3.ClampMagnitude(beforeClamp, maxOffset);
            Vector3 afterPositiveZScale = afterClamp;
            afterPositiveZScale.z *= positiveZScale;
            Vector3 correction = afterPositiveZScale * weight;
            Vector3 expectedNextPosition = currentFootPosition + correction;

            Assert.That(calculated, Is.True);
            AssertVector3(ReadVector3(snapshot, "EndpointDeltaBeforeClamp"), beforeClamp);
            AssertVector3(ReadVector3(snapshot, "EndpointDeltaAfterClamp"), afterClamp);
            AssertVector3(ReadVector3(snapshot, "EndpointDeltaAfterPositiveZScale"), afterPositiveZScale);
            AssertVector3(ReadVector3(snapshot, "Correction"), correction);
            AssertVector3(ReadVector3(snapshot, "NextFootPosition"), expectedNextPosition);
            AssertVector3(nextFootPosition, expectedNextPosition);
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointPositiveZScale_When_CalculatingDesiredFootPosition_Then_ScalesOnlyPositiveZCarrier()
        {
            bool calculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.02f, 0f, 0.02f),
                desiredToesPosition: new Vector3(0.02f, 0f, 0.02f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.04f,
                positiveZScale: 0f,
                out Vector3 nextFootPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextFootPosition.x, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(nextFootPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointPositiveZScale_When_CorrectionExceedsCap_Then_DoesNotIncreaseBaselineClampedX()
        {
            bool baselineCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.08f, 0f, 0.06f),
                desiredToesPosition: new Vector3(0.08f, 0f, 0.06f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.05f,
                positiveZScale: 1f,
                out Vector3 baselineFootPosition);

            bool suppressedCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.08f, 0f, 0.06f),
                desiredToesPosition: new Vector3(0.08f, 0f, 0.06f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.05f,
                positiveZScale: 0f,
                out Vector3 suppressedFootPosition);

            Assert.That(baselineCalculated, Is.True);
            Assert.That(suppressedCalculated, Is.True);
            Assert.That(baselineFootPosition.x, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(baselineFootPosition.z, Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(suppressedFootPosition.x, Is.LessThanOrEqualTo(baselineFootPosition.x + 0.0001f));
            Assert.That(suppressedFootPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointToesBlend_When_RecalculatingDirection_Then_CanUseFootOnlyOrFootToesAverage()
        {
            bool averageCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.04f, 0f, 0f),
                desiredToesPosition: new Vector3(0f, 0f, 0.04f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.2f,
                positiveZScale: 1f,
                toesBlendWeight: 1f,
                out Vector3 averageFootPosition);

            bool footOnlyCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.04f, 0f, 0f),
                desiredToesPosition: new Vector3(0f, 0f, 0.04f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.2f,
                positiveZScale: 1f,
                toesBlendWeight: 0f,
                out Vector3 footOnlyPosition);

            Assert.That(averageCalculated, Is.True);
            Assert.That(footOnlyCalculated, Is.True);
            Assert.That(averageFootPosition.x, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(averageFootPosition.z, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(footOnlyPosition.x, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(footOnlyPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_EvaluatorXzReference_When_CalculatingPosition_Then_RecordsNormalizedCorrection()
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            Type snapshotType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnosticSnapshot");
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateEvaluatorXzReferencePosition",
                StaticNonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType(),
                    snapshotType.MakeByRefType()
                },
                modifiers: null);
            Assert.That(method, Is.Not.Null);

            Vector3 referenceFootPosition = new Vector3(1f, 1f, 1f);
            Vector3 currentFootPosition = new Vector3(1.5f, 1f, 1.4f);
            Vector3 firstMatchedFootOffset = new Vector3(0.1f, 0f, 0.1f);
            object[] args =
            {
                referenceFootPosition,
                currentFootPosition,
                firstMatchedFootOffset,
                0.2f,
                0.5f,
                0.1f,
                currentFootPosition,
                Activator.CreateInstance(snapshotType)
            };

            bool calculated = (bool)method.Invoke(null, args);
            Vector3 nextFootPosition = (Vector3)args[6];
            object snapshot = args[7];

            Assert.That(calculated, Is.True);
            AssertVector3(ReadVector3(snapshot, "EvaluatorXzNormalizedDelta"), new Vector3(0.4f, 0f, 0.3f));
            AssertVector3(ReadVector3(snapshot, "EvaluatorXzDesiredNormalizedDelta"), new Vector3(0.16f, 0f, 0.12f));
            AssertVector3(ReadVector3(snapshot, "EndpointDeltaAfterClamp"), new Vector3(-0.08f, 0f, -0.06f));
            AssertVector3(ReadVector3(snapshot, "Correction"), new Vector3(-0.04f, 0f, -0.03f));
            AssertVector3(nextFootPosition, new Vector3(1.46f, 1f, 1.37f));
        }

        [Test]
        public void Given_PostSetHumanPoseEvaluatorXzReference_When_FirstOffsetDrifts_Then_ReducesToTargetMagnitude()
        {
            bool calculated = TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
                referenceFootPosition: new Vector3(1.594633f, 0f, 0.070673f),
                currentFootPosition: new Vector3(0.324088f, 0f, 0.020131f),
                firstMatchedFootOffset: new Vector3(-1.375309f, 0f, 0.033983f),
                targetMagnitude: 0.049f,
                weight: 1f,
                maxOffset: 0.2f,
                out Vector3 nextFootPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextFootPosition.x, Is.EqualTo(0.25746f).Within(0.0001f));
            Assert.That(nextFootPosition.z, Is.EqualTo(0.073888f).Within(0.0001f));

            float remainingX = nextFootPosition.x - 1.594633f - (-1.375309f);
            float remainingZ = nextFootPosition.z - 0.070673f - 0.033983f;
            Assert.That(new Vector2(remainingX, remainingZ).magnitude, Is.EqualTo(0.049f).Within(0.0001f));
        }

        [Test]
        public void Given_HipsLocalReferenceWouldIncreaseEndpointTargetGap_When_CheckingTargetGapGuard_Then_RejectsCandidate()
        {
            bool shouldKeep = ShouldKeepHipsLocalPositionReferenceByTargetGap(
                referenceFootPosition: new Vector3(0f, 0f, 0f),
                referenceToesPosition: new Vector3(0.02f, 0f, 0f),
                beforeFootPosition: new Vector3(0.24f, 0f, 0f),
                beforeToesPosition: new Vector3(0.23f, 0f, 0f),
                afterFootPosition: new Vector3(0.2422f, 0f, 0f),
                afterToesPosition: new Vector3(0.2321f, 0f, 0f),
                maxAllowedIncrease: 0.0005f);

            Assert.That(shouldKeep, Is.False);
        }

        [Test]
        public void Given_HipsLocalReferencePreservesEndpointTargetGap_When_CheckingTargetGapGuard_Then_KeepsCandidate()
        {
            bool shouldKeep = ShouldKeepHipsLocalPositionReferenceByTargetGap(
                referenceFootPosition: new Vector3(0f, 0f, 0f),
                referenceToesPosition: new Vector3(0.02f, 0f, 0f),
                beforeFootPosition: new Vector3(0.24f, 0f, 0f),
                beforeToesPosition: new Vector3(0.23f, 0f, 0f),
                afterFootPosition: new Vector3(0.2398f, 0f, 0f),
                afterToesPosition: new Vector3(0.2297f, 0f, 0f),
                maxAllowedIncrease: 0.0005f);

            Assert.That(shouldKeep, Is.True);
        }

        [Test]
        public void Given_EndpointTargetGapAtAllowedIncrease_When_CheckingTargetGapGuard_Then_KeepsCandidate()
        {
            bool shouldKeep = ShouldKeepHipsLocalPositionReferenceByTargetGap(
                referenceFootPosition: Vector3.zero,
                referenceToesPosition: Vector3.zero,
                beforeFootPosition: new Vector3(0.25f, 0f, 0f),
                beforeToesPosition: new Vector3(0.25f, 0f, 0f),
                afterFootPosition: new Vector3(0.375f, 0f, 0f),
                afterToesPosition: new Vector3(0.375f, 0f, 0f),
                maxAllowedIncrease: 0.125f);

            Assert.That(shouldKeep, Is.True);
        }

        [Test]
        public void Given_NegativeAllowedIncrease_When_CheckingTargetGapGuard_Then_ClampsAllowanceToZero()
        {
            bool shouldKeep = ShouldKeepHipsLocalPositionReferenceByTargetGap(
                referenceFootPosition: Vector3.zero,
                referenceToesPosition: Vector3.zero,
                beforeFootPosition: new Vector3(0.25f, 0f, 0f),
                beforeToesPosition: new Vector3(0.25f, 0f, 0f),
                afterFootPosition: new Vector3(0.3125f, 0f, 0f),
                afterToesPosition: new Vector3(0.3125f, 0f, 0f),
                maxAllowedIncrease: -1f);

            Assert.That(shouldKeep, Is.False);
        }

        [Test]
        public void Given_NonFiniteEndpointPosition_When_CheckingTargetGapGuard_Then_FailsOpen()
        {
            bool shouldKeep = ShouldKeepHipsLocalPositionReferenceByTargetGap(
                referenceFootPosition: new Vector3(float.NaN, 0f, 0f),
                referenceToesPosition: Vector3.zero,
                beforeFootPosition: Vector3.zero,
                beforeToesPosition: Vector3.zero,
                afterFootPosition: Vector3.zero,
                afterToesPosition: Vector3.zero,
                maxAllowedIncrease: 0f);

            Assert.That(shouldKeep, Is.True);
        }

        [Test]
        public void Given_OnlyEndpointHeightChanges_When_CheckingTargetGapGuard_Then_IgnoresY()
        {
            bool shouldKeep = ShouldKeepHipsLocalPositionReferenceByTargetGap(
                referenceFootPosition: new Vector3(0f, 100f, 0f),
                referenceToesPosition: new Vector3(0f, -100f, 0f),
                beforeFootPosition: new Vector3(0f, -200f, 0f),
                beforeToesPosition: new Vector3(0f, 200f, 0f),
                afterFootPosition: new Vector3(0f, 500f, 0f),
                afterToesPosition: new Vector3(0f, -500f, 0f),
                maxAllowedIncrease: 0f);

            Assert.That(shouldKeep, Is.True);
        }

        [Test]
        public void Given_RetargetEndpointStagesWithFirstJump_When_AttributingStage_Then_ReportsExactlyFirstStageDelta()
        {
            bool attributed = TryFindFirstStagePositionJump(
                new[] { "pre_set", "after_set_human_pose", "after_manual_reference", "after_root_restore" },
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0.08f, 0f, -0.02f),
                    new Vector3(0.20f, 0f, -0.02f),
                    new Vector3(0.20f, 0f, -0.10f)
                },
                threshold: 0.05f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(attributed, Is.True);
            Assert.That(stage, Is.EqualTo("after_set_human_pose"));
            Assert.That(delta.x, Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(-0.02f).Within(0.0001f));
            Assert.That(magnitude, Is.EqualTo(new Vector3(0.08f, 0f, -0.02f).magnitude).Within(0.0001f));
        }

        [Test]
        public void Given_RetargetEndpointStagesWithinTolerance_When_AttributingStage_Then_ReturnsNoAttribution()
        {
            bool attributed = TryFindFirstStagePositionJump(
                new[] { "pre_set", "after_set_human_pose", "after_manual_reference" },
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0.01f, 0f, 0f),
                    new Vector3(0.02f, 0f, 0f)
                },
                threshold: 0.05f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(attributed, Is.False);
            AssertNoStageJump(stage, delta, magnitude);
        }

        [Test]
        public void Given_StagePositionJumpAtThreshold_When_FindingFirstJump_Then_ReturnsNoJump()
        {
            bool found = TryFindFirstStagePositionJump(
                new[] { "start", "at_threshold" },
                new[] { Vector3.zero, new Vector3(0.05f, 0f, 0f) },
                threshold: 0.05f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(found, Is.False);
            AssertNoStageJump(stage, delta, magnitude);
        }

        [Test]
        public void Given_NonFiniteStagePositionBeforeValidJump_When_FindingFirstJump_Then_SkipsInvalidPairs()
        {
            bool found = TryFindFirstStagePositionJump(
                new[] { "start", "invalid", "recovered", "jump" },
                new[]
                {
                    Vector3.zero,
                    new Vector3(float.NaN, 0f, 0f),
                    new Vector3(0.01f, 0f, 0f),
                    new Vector3(0.08f, 0f, 0f)
                },
                threshold: 0.05f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(found, Is.True);
            Assert.That(stage, Is.EqualTo("jump"));
            AssertVector3(delta, new Vector3(0.07f, 0f, 0f));
            Assert.That(magnitude, Is.EqualTo(0.07f).Within(0.0001f));
        }

        [Test]
        public void Given_NegativeStageJumpThreshold_When_FindingFirstJump_Then_ClampsThresholdToZero()
        {
            bool found = TryFindFirstStagePositionJump(
                new[] { "start", "jump" },
                new[] { Vector3.zero, new Vector3(0.001f, 0f, 0f) },
                threshold: -1f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(found, Is.True);
            Assert.That(stage, Is.EqualTo("jump"));
            AssertVector3(delta, new Vector3(0.001f, 0f, 0f));
            Assert.That(magnitude, Is.EqualTo(0.001f).Within(0.0001f));
        }

        [Test]
        public void Given_MismatchedStagePositionInputs_When_FindingFirstJump_Then_ReturnsNoJump()
        {
            bool found = TryFindFirstStagePositionJump(
                new[] { "start", "jump" },
                new[] { Vector3.zero },
                threshold: 0f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(found, Is.False);
            AssertNoStageJump(stage, delta, magnitude);
        }

        [Test]
        public void Given_EndpointDiagnosticCalculation_When_CheckingOwnership_Then_UsesDedicatedType()
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            Type snapshotType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnosticSnapshot");

            Assert.That(
                diagnosticsType.GetMember("TryCalculateReferencePosition", StaticNonPublic),
                Is.Not.Empty);
            Assert.That(
                diagnosticsType.GetMember("TryCalculateEvaluatorXzReferencePosition", StaticNonPublic),
                Is.Not.Empty);
            Assert.That(
                diagnosticsType.GetMember("ShouldKeepHipsLocalPositionReferenceByTargetGap", StaticNonPublic),
                Is.Not.Empty);
            Assert.That(
                diagnosticsType.GetMember("TryFindFirstStagePositionJump", StaticNonPublic),
                Is.Not.Empty);
            Assert.That(snapshotType.GetField("Correction", InstanceNonPublic), Is.Not.Null);

            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculatePostSetHumanPoseEndpointDesiredFootPosition",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "ShouldKeepEditorHipsLocalPositionReferenceByTargetGap",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateRightEndpointTargetGap",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateXzDistance",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryFindFirstRetargetEndpointStageJump",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            out Vector3 nextFootPosition)
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateReferencePosition",
                StaticNonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType()
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "Post-SetHumanPose endpoint candidate must expose positive-Z carrier scaling for middle-window probes.");

            object[] args =
            {
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                Vector3.zero
            };
            bool result = (bool)method.Invoke(null, args);
            nextFootPosition = (Vector3)args[7];
            return result;
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            out Vector3 nextFootPosition)
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateReferencePosition",
                StaticNonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType()
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "Post-SetHumanPose endpoint candidate must expose a runtime-only foot/toes blend for direction recalculation probes.");

            object[] args =
            {
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                Vector3.zero
            };
            bool result = (bool)method.Invoke(null, args);
            nextFootPosition = (Vector3)args[8];
            return result;
        }

        private static bool TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition)
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateEvaluatorXzReferencePosition",
                StaticNonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType()
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "Post-SetHumanPose evaluator-basis candidate must expose first-offset X/Z correction for middle-window probes.");

            object[] args =
            {
                referenceFootPosition,
                currentFootPosition,
                firstMatchedFootOffset,
                targetMagnitude,
                weight,
                maxOffset,
                Vector3.zero
            };

            bool result = (bool)method.Invoke(null, args);
            nextFootPosition = (Vector3)args[6];
            return result;
        }

        private static bool TryFindFirstStagePositionJump(
            string[] stageNames,
            Vector3[] positions,
            float threshold,
            out string stage,
            out Vector3 delta,
            out float magnitude)
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            MethodInfo method = diagnosticsType.GetMethod(
                "TryFindFirstStagePositionJump",
                StaticNonPublic,
                binder: null,
                types: StagePositionJumpParameterTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null,
                "RetargetingEndpointDiagnostics should own pure stage-position jump search.");

            object[] args =
            {
                stageNames,
                positions,
                threshold,
                "",
                Vector3.zero,
                0f
            };

            bool found = (bool)method.Invoke(null, args);
            stage = (string)args[3];
            delta = (Vector3)args[4];
            magnitude = (float)args[5];
            return found;
        }

        private static void AssertNoStageJump(string stage, Vector3 delta, float magnitude)
        {
            Assert.That(stage, Is.EqualTo(""));
            Assert.That(delta.x, Is.NaN);
            Assert.That(delta.y, Is.NaN);
            Assert.That(delta.z, Is.NaN);
            Assert.That(magnitude, Is.NaN);
        }

        private static bool ShouldKeepHipsLocalPositionReferenceByTargetGap(
            Vector3 referenceFootPosition,
            Vector3 referenceToesPosition,
            Vector3 beforeFootPosition,
            Vector3 beforeToesPosition,
            Vector3 afterFootPosition,
            Vector3 afterToesPosition,
            float maxAllowedIncrease)
        {
            Type diagnosticsType = GetRequiredType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics");
            MethodInfo method = diagnosticsType.GetMethod(
                "ShouldKeepHipsLocalPositionReferenceByTargetGap",
                StaticNonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float)
                },
                modifiers: null);
            Assert.That(method, Is.Not.Null);

            return (bool)method.Invoke(null, new object[]
            {
                referenceFootPosition,
                referenceToesPosition,
                beforeFootPosition,
                beforeToesPosition,
                afterFootPosition,
                afterToesPosition,
                maxAllowedIncrease
            });
        }

        private static Type GetRequiredType(string fullName)
        {
            Type type = typeof(PoseSpaceRetargeter).Assembly.GetType(fullName, throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{fullName} 타입이 필요함.");
            return type;
        }

        private static Vector3 ReadVector3(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 진단 필드가 필요함.");
            return (Vector3)field.GetValue(instance);
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }
    }
}
