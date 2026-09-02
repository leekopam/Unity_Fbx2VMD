using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterHipsLocalPositionReferenceTests
    {
        private static Type ManualPoseReferenceApplierType =>
            typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ManualPoseReferenceApplier",
                throwOnError: true);

        private static readonly Type[] HipsLocalPositionReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(bool),
            typeof(Vector3),
            typeof(float),
            typeof(float),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] AnchoredHipsLocalPositionReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(bool),
            typeof(Vector3),
            typeof(bool),
            typeof(Vector3),
            typeof(float),
            typeof(float),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] RecordingStartHipsBaselineFlipParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] FootLocalRotationReferenceParameterTypes =
        {
            typeof(Quaternion),
            typeof(Quaternion),
            typeof(float),
            typeof(Quaternion).MakeByRefType()
        };

        private static readonly Type[] FootIkPositionReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(float),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] BodyPositionXzFrameGateWeightParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] BodyPositionXzReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] SignCorrectedBodyPositionXzReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] SignCorrectedBodyPositionXzReferenceWithInversionParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(bool),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] FrameWithinGateParameterTypes =
        {
            typeof(int),
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] ActiveFrameGateParameterTypes =
        {
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] HipsAlignedEndpointPositionReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(Transform),
            typeof(Vector3),
            typeof(Vector3),
            typeof(Transform),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] LowerBodySegmentDirectionReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(Quaternion),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(Quaternion).MakeByRefType()
        };

        [Test]
        public void Given_RestReference_When_CalculatingHipsLocalPosition_Then_AppliesReferenceDeltaWithWeight()
        {
            bool calculated = TryCalculateHipsLocalPositionReference(
                referenceCurrentLocalPosition: new Vector3(0.1f, 0.08f, -0.02f),
                referenceRestLocalPosition: new Vector3(0.02f, 0.04f, -0.01f),
                hasReferenceRestLocalPosition: true,
                currentLocalPosition: new Vector3(0f, 1f, 0f),
                weight: 0.5f,
                maxOffset: 0f,
                out Vector3 nextLocalPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextLocalPosition.x, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(nextLocalPosition.y, Is.EqualTo(1.02f).Within(0.0001f));
            Assert.That(nextLocalPosition.z, Is.EqualTo(-0.005f).Within(0.0001f));
        }

        [Test]
        public void Given_NoRestReference_When_CalculatingHipsLocalPosition_Then_UsesReferenceAbsolutePosition()
        {
            bool calculated = TryCalculateHipsLocalPositionReference(
                referenceCurrentLocalPosition: new Vector3(0.02f, 0.9f, 0.03f),
                referenceRestLocalPosition: Vector3.zero,
                hasReferenceRestLocalPosition: false,
                currentLocalPosition: new Vector3(0f, 1f, 0f),
                weight: 1f,
                maxOffset: 0f,
                out Vector3 nextLocalPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextLocalPosition.x, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(nextLocalPosition.y, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(nextLocalPosition.z, Is.EqualTo(0.03f).Within(0.0001f));
        }

        [Test]
        public void Given_MaxOffset_When_CalculatingHipsLocalPosition_Then_ClampsDeltaBeforeApplyingWeight()
        {
            bool calculated = TryCalculateHipsLocalPositionReference(
                referenceCurrentLocalPosition: new Vector3(0.2f, 0f, 0f),
                referenceRestLocalPosition: Vector3.zero,
                hasReferenceRestLocalPosition: false,
                currentLocalPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.1f,
                out Vector3 nextLocalPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextLocalPosition.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(nextLocalPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(nextLocalPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_TargetRestAnchor_When_CurrentHipsCollapsed_Then_AppliesReferenceDeltaFromTargetRest()
        {
            bool calculated = TryCalculateAnchoredHipsLocalPositionReference(
                referenceCurrentLocalPosition: new Vector3(0f, 0.02f, 0f),
                referenceRestLocalPosition: new Vector3(0f, 0.04f, 0f),
                hasReferenceRestLocalPosition: true,
                targetRestLocalPosition: new Vector3(0f, 0.75f, 0f),
                hasTargetRestLocalPosition: true,
                currentLocalPosition: new Vector3(0f, 0.55f, 0f),
                weight: 1f,
                maxOffset: 0f,
                out Vector3 nextLocalPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextLocalPosition.y, Is.EqualTo(0.73f).Within(0.0001f), "Reference delta must be anchored to the YYB rest Hips, not added to an already-collapsed current Hips.");
        }

        [Test]
        public void Given_NonFiniteReference_When_CalculatingHipsLocalPosition_Then_ReturnsFalseAndKeepsCurrent()
        {
            Vector3 current = new Vector3(0f, 1f, 0f);

            bool calculated = TryCalculateHipsLocalPositionReference(
                referenceCurrentLocalPosition: new Vector3(float.NaN, 0f, 0f),
                referenceRestLocalPosition: Vector3.zero,
                hasReferenceRestLocalPosition: false,
                currentLocalPosition: current,
                weight: 1f,
                maxOffset: 0f,
                out Vector3 nextLocalPosition);

            Assert.That(calculated, Is.False);
            Assert.That(nextLocalPosition, Is.EqualTo(current));
        }

        [Test]
        public void Given_RecordingStartHipsLocalBaselineChangesPastThreshold_When_CheckingFlip_Then_ReportsFlip()
        {
            Assert.That(IsRecordingStartHipsBaselineFlip(0.829f, 0.800f, 0.02f), Is.True);
            Assert.That(IsRecordingStartHipsBaselineFlip(0.829f, 0.812f, 0.02f), Is.False);
            Assert.That(IsRecordingStartHipsBaselineFlip(float.NaN, 0.800f, 0.02f), Is.False);
        }

        [Test]
        public void Given_FootLocalRotationReference_When_CalculatingBlend_Then_SlerpsTowardReference()
        {
            Quaternion current = Quaternion.identity;
            Quaternion reference = Quaternion.Euler(0f, 90f, 0f);

            bool calculated = TryCalculateEditorFootLocalRotationReference(
                reference,
                current,
                0.5f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.True);
            Assert.That(Quaternion.Angle(current, nextRotation), Is.EqualTo(45f).Within(0.05f));
            Assert.That(Quaternion.Angle(nextRotation, reference), Is.EqualTo(45f).Within(0.05f));
        }

        [Test]
        public void Given_ManualPoseLocalRotationAccess_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            Type applierType = typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ManualPoseReferenceApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null,
                "ManualPoseReferenceApplier should own Animator and Transform localRotation access.");

            string[] applierMethodNames =
            {
                "ApplyExactLocalRotationReference",
                "ApplyBlendedLocalRotationReference",
                "TryCalculateLocalRotationReference"
            };
            foreach (string methodName in applierMethodNames)
            {
                Assert.That(
                    applierType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Not.Null,
                    $"ManualPoseReferenceApplier should expose {methodName}.");
            }

            string[] extractedMethodNames =
            {
                "ApplyEditorHumanoidHandLocalRotationReferenceBone",
                "ApplyEditorHumanoidFootLocalRotationReferenceBone",
                "TryCalculateEditorFootLocalRotationReference"
            };
            foreach (string methodName in extractedMethodNames)
            {
                Assert.That(
                    typeof(PoseSpaceRetargeter).GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Null,
                    $"PoseSpaceRetargeter should delegate {methodName}.");
            }
        }

        [Test]
        public void Given_ManualHipsLocalPositionAccess_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            string[] applierMethodNames =
            {
                "TryResolveHipsLocalPositionReference",
                "ApplyHipsLocalPosition",
                "TryCalculateHipsLocalPositionReference"
            };
            foreach (string methodName in applierMethodNames)
            {
                Assert.That(
                    ManualPoseReferenceApplierType.GetMember(
                        methodName,
                        BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Not.Empty,
                    $"ManualPoseReferenceApplier should own {methodName}.");
            }

            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateEditorHipsLocalPositionReference",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty,
                "PoseSpaceRetargeter should delegate Hips localPosition calculation.");
        }

        [Test]
        public void Given_BodyPositionXzFrameGate_When_CalculatingWeight_Then_BlendsAtBothEdges()
        {
            Assert.That(
                CalculateBodyPositionXzFrameGateWeight(165f, 180f, 180f, 30f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                CalculateBodyPositionXzFrameGateWeight(180f, 180f, 180f, 30f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                CalculateBodyPositionXzFrameGateWeight(195f, 180f, 180f, 30f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                CalculateBodyPositionXzFrameGateWeight(211f, 180f, 180f, 30f),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                CalculateBodyPositionXzFrameGateWeight(240f, 0f, 0f, 30f),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_BodyPositionXzFrameGateCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            Assert.That(
                ManualPoseReferenceApplierType.GetMethod(
                    "CalculateBodyPositionXzFrameGateWeight",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: BodyPositionXzFrameGateWeightParameterTypes,
                    modifiers: null),
                Is.Not.Null);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMethod(
                    "CalculateManualAnimatorBodyPositionXzFrameGateWeight",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void Given_ManualAnimatorBodyPositionXzReference_When_CalculatingSolverInput_Then_ClampsXzOnly()
        {
            bool calculated = TryCalculateBodyPositionXzReference(
                currentBodyPosition: new Vector3(0.1f, 1.2f, -0.2f),
                referenceBodyPosition: new Vector3(0.3f, 2.4f, -0.6f),
                weight: 1f,
                maxOffset: 0.05f,
                axisXScale: 1f,
                axisZScale: 1f,
                out Vector3 nextBodyPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextBodyPosition.y, Is.EqualTo(1.2f).Within(0.000001f),
                "The solver input candidate must not disturb the existing Y basis.");
            Assert.That(
                Vector2.Distance(new Vector2(0.1f, -0.2f), new Vector2(nextBodyPosition.x, nextBodyPosition.z)),
                Is.EqualTo(0.05f).Within(0.00001f),
                "The candidate should bound X/Z bodyPosition motion instead of applying a full root jump.");
        }

        [Test]
        public void Given_ManualAnimatorBodyPositionXzAxisScale_When_CalculatingSolverInput_Then_ReducesOnlyRequestedAxis()
        {
            bool calculated = TryCalculateBodyPositionXzReference(
                currentBodyPosition: new Vector3(1f, 2f, 3f),
                referenceBodyPosition: new Vector3(5f, 9f, 7f),
                weight: 1f,
                maxOffset: 0.08f,
                axisXScale: 1f,
                axisZScale: 0f,
                out Vector3 nextBodyPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextBodyPosition.x, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(nextBodyPosition.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(nextBodyPosition.z, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Given_LeftFootCurrentIsNegativeXPositiveZFromGhost_When_CalculatingSignCorrectedBodyPosition_Then_MovesTowardGhost()
        {
            Vector3 currentBodyPosition = new Vector3(0.1f, 1.2f, -0.2f);
            Vector3 ghostFootPosition = new Vector3(0.000073f, 0f, -0.000769f);
            Vector3 currentFootPosition = new Vector3(-0.150225f, 0f, 0.022191f);

            bool calculated = TryCalculateSignCorrectedBodyPositionXzReference(
                currentBodyPosition,
                ghostFootPosition,
                currentFootPosition,
                weight: 1f,
                maxOffset: 0.012f,
                axisXScale: 1f,
                axisZScale: 1f,
                out Vector3 nextBodyPosition);

            Vector3 bodyDelta = nextBodyPosition - currentBodyPosition;
            Vector3 translatedFootPosition = currentFootPosition + new Vector3(bodyDelta.x, 0f, bodyDelta.z);

            Assert.That(calculated, Is.True);
            Assert.That(nextBodyPosition.y, Is.EqualTo(currentBodyPosition.y).Within(0.000001f));
            Assert.That(bodyDelta.x, Is.GreaterThan(0f), "Frame 300 left foot needs +X correction toward the ghost row, not the previous -X target drift.");
            Assert.That(bodyDelta.z, Is.LessThan(0f), "Frame 300 left foot needs -Z correction toward the ghost row.");
            Assert.That(new Vector2(bodyDelta.x, bodyDelta.z).magnitude, Is.EqualTo(0.012f).Within(0.00001f));
            Assert.That(
                Vector2.Distance(
                    new Vector2(translatedFootPosition.x, translatedFootPosition.z),
                    new Vector2(ghostFootPosition.x, ghostFootPosition.z)),
                Is.LessThan(
                    Vector2.Distance(
                        new Vector2(currentFootPosition.x, currentFootPosition.z),
                        new Vector2(ghostFootPosition.x, ghostFootPosition.z))),
                "The row-local translation basis must reduce the measured ghost/current gap before runtime visual compare.");
        }

        [Test]
        public void Given_LeftFootRealizedZMovesOppositeIntended_When_InvertingBodyPositionZ_Then_FlipsOnlyZInput()
        {
            Vector3 currentBodyPosition = new Vector3(0.1f, 1.2f, -0.2f);
            Vector3 ghostFootPosition = new Vector3(0.000073f, 0f, -0.000769f);
            Vector3 currentFootPosition = new Vector3(-0.150225f, 0f, 0.022191f);

            bool calculated = TryCalculateSignCorrectedBodyPositionXzReference(
                currentBodyPosition,
                ghostFootPosition,
                currentFootPosition,
                weight: 1f,
                maxOffset: 0.012f,
                axisXScale: 1f,
                axisZScale: 1f,
                invertX: false,
                invertZ: true,
                out Vector3 nextBodyPosition);

            Vector3 bodyDelta = nextBodyPosition - currentBodyPosition;

            Assert.That(calculated, Is.True);
            Assert.That(nextBodyPosition.y, Is.EqualTo(currentBodyPosition.y).Within(0.000001f));
            Assert.That(bodyDelta.x, Is.GreaterThan(0f),
                "The Z inversion candidate must keep the existing +X left-foot correction input.");
            Assert.That(bodyDelta.z, Is.GreaterThan(0f),
                "The frame 300/600 diagnostic showed realized endpoint motion overreacting in -Z, so the runtime candidate needs a positive bodyPosition Z input.");
            Assert.That(new Vector2(bodyDelta.x, bodyDelta.z).magnitude, Is.EqualTo(0.012f).Within(0.00001f));
        }

        [Test]
        public void Given_BodyPositionXzCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            Assert.That(
                ManualPoseReferenceApplierType.GetMethod(
                    "TryCalculateBodyPositionXzReference",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: BodyPositionXzReferenceParameterTypes,
                    modifiers: null),
                Is.Not.Null);
            Assert.That(
                ManualPoseReferenceApplierType.GetMethod(
                    "TryCalculateSignCorrectedBodyPositionXzReference",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: SignCorrectedBodyPositionXzReferenceParameterTypes,
                    modifiers: null),
                Is.Not.Null);
            Assert.That(
                ManualPoseReferenceApplierType.GetMethod(
                    "TryCalculateSignCorrectedBodyPositionXzReference",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: SignCorrectedBodyPositionXzReferenceWithInversionParameterTypes,
                    modifiers: null),
                Is.Not.Null);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateManualAnimatorBodyPositionXzReference",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateSignCorrectedRowLocalBodyPositionXzReference",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
        }

        [Test]
        public void Given_InclusiveFrameGate_When_CheckingFrame_Then_PreservesDisabledInvalidAndRoundedBounds()
        {
            Assert.That(HasActiveFrameGate(0f, 0f), Is.False);
            Assert.That(HasActiveFrameGate(300f, 180f), Is.False);
            Assert.That(HasActiveFrameGate(-10f, 180f), Is.True);
            Assert.That(HasActiveFrameGate(179.5f, 180.49f), Is.True);
            Assert.That(IsFrameWithinGate(240, 0f, 0f), Is.True);
            Assert.That(IsFrameWithinGate(240, 300f, 180f), Is.True);
            Assert.That(IsFrameWithinGate(179, 179.5f, 180.49f), Is.False);
            Assert.That(IsFrameWithinGate(180, 179.5f, 180.49f), Is.True);
            Assert.That(IsFrameWithinGate(181, 179.5f, 180.49f), Is.False);
        }

        [Test]
        public void Given_SingleFrameFallbackGate_When_EndIsInvalid_Then_UsesRoundedStartFrameOnly()
        {
            Assert.That(HasConfiguredFrameGate(0f, 0f), Is.False);
            Assert.That(HasConfiguredFrameGate(90f, 0f), Is.True);
            Assert.That(IsFrameWithinSingleFrameFallbackGate(240, 0f, 0f), Is.True);
            Assert.That(IsFrameWithinSingleFrameFallbackGate(89, 89.5f, 0f), Is.False);
            Assert.That(IsFrameWithinSingleFrameFallbackGate(90, 89.5f, 0f), Is.True);
            Assert.That(IsFrameWithinSingleFrameFallbackGate(91, 89.5f, 0f), Is.False);
            Assert.That(IsFrameWithinSingleFrameFallbackGate(300, 299.5f, 180f), Is.True);
        }

        [Test]
        public void Given_ManualFootIkPositionAccess_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            string[] applierMethodNames =
            {
                "TryResolveHipsTransforms",
                "TryResolveFootIkPositionReference",
                "TryCalculateFootIkPositionReference"
            };
            foreach (string methodName in applierMethodNames)
            {
                Assert.That(
                    ManualPoseReferenceApplierType.GetMember(
                        methodName,
                        BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Not.Empty,
                    $"ManualPoseReferenceApplier should own {methodName}.");
            }

            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateEditorFootIkPositionReference",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty,
                "PoseSpaceRetargeter should delegate foot IK position calculation.");
        }

        [Test]
        public void Given_ManualFootReference_When_CalculatingFootIkTarget_Then_UsesReferenceHipsRelativePosition()
        {
            Vector3 referenceHips = new Vector3(1f, 1f, 1f);
            Vector3 referenceFoot = new Vector3(1.2f, 0.15f, 1.4f);
            Vector3 targetHips = new Vector3(10f, 2f, -3f);
            Vector3 currentFoot = new Vector3(10.1f, 1.2f, -2.8f);

            bool calculated = TryCalculateFootIkPositionReference(
                referenceFoot,
                referenceHips,
                currentFoot,
                targetHips,
                weight: 1f,
                maxOffset: 0f,
                out Vector3 nextPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextPosition.x, Is.EqualTo(10.2f).Within(0.0001f));
            Assert.That(nextPosition.y, Is.EqualTo(1.15f).Within(0.0001f));
            Assert.That(nextPosition.z, Is.EqualTo(-2.6f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualFootReferenceMaxOffset_When_CalculatingFootIkTarget_Then_ClampsBeforeWeight()
        {
            bool calculated = TryCalculateFootIkPositionReference(
                referenceFootPosition: new Vector3(2f, 0f, 0f),
                referenceHipsPosition: Vector3.zero,
                currentFootPosition: Vector3.zero,
                targetHipsPosition: Vector3.zero,
                weight: 0.5f,
                maxOffset: 0.2f,
                out Vector3 nextPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextPosition.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(nextPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(nextPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_HipsAlignedEndpointReference_When_TargetRootRotates_Then_MapsOffsetAndKeepsEndpointHeight()
        {
            GameObject referenceRootObject = new GameObject("ReferenceRoot");
            GameObject targetRootObject = new GameObject("TargetRoot");
            try
            {
                targetRootObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                bool calculated = TryCalculateHipsAlignedEndpointPositionReference(
                    referenceEndpointPosition: new Vector3(2f, 0f, 3f),
                    referenceHipsPosition: new Vector3(1f, 1f, 1f),
                    referenceRoot: referenceRootObject.transform,
                    targetHipsPosition: new Vector3(10f, 2f, -3f),
                    currentTargetEndpointPosition: new Vector3(9f, 0.25f, -2f),
                    targetRoot: targetRootObject.transform,
                    out Vector3 desiredEndpointPosition);

                Assert.That(calculated, Is.True);
                Assert.That(desiredEndpointPosition.x, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(desiredEndpointPosition.y, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(desiredEndpointPosition.z, Is.EqualTo(-4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(referenceRootObject);
                UnityEngine.Object.DestroyImmediate(targetRootObject);
            }
        }

        [Test]
        public void Given_HipsAlignedEndpointAccess_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            string[] applierMethodNames =
            {
                "TryResolveHipsAlignedEndpointPositionReference",
                "TryCalculateHipsAlignedEndpointPositionReference"
            };
            foreach (string methodName in applierMethodNames)
            {
                Assert.That(
                    ManualPoseReferenceApplierType.GetMember(
                        methodName,
                        BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Not.Empty,
                    $"ManualPoseReferenceApplier should own {methodName}.");
            }

            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateEditorFootHipsAlignedDesiredFootPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Empty,
                "PoseSpaceRetargeter should delegate hips-aligned endpoint position resolution.");
        }

        [Test]
        public void Given_LowerBodySegmentDirectionReference_When_CalculatingCorrection_Then_RotatesTowardReferenceDirection()
        {
            bool calculated = TryCalculateSegmentDirectionReference(
                referenceSegmentDirection: Vector3.forward,
                currentSegmentDirection: Vector3.right,
                currentParentWorldRotation: Quaternion.identity,
                weight: 0.5f,
                maxAngleDegrees: 0f,
                correctionAxisXzScale: 1f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.True);
            Assert.That(Quaternion.Angle(Quaternion.identity, nextRotation), Is.EqualTo(45f).Within(0.05f));
        }

        [Test]
        public void Given_LowerBodySegmentDirectionMaxAngle_When_CalculatingCorrection_Then_ClampsBeforeWeight()
        {
            bool calculated = TryCalculateSegmentDirectionReference(
                referenceSegmentDirection: Vector3.forward,
                currentSegmentDirection: Vector3.right,
                currentParentWorldRotation: Quaternion.identity,
                weight: 0.5f,
                maxAngleDegrees: 10f,
                correctionAxisXzScale: 1f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.True);
            Assert.That(Quaternion.Angle(Quaternion.identity, nextRotation), Is.EqualTo(5f).Within(0.05f));
        }

        [Test]
        public void Given_LowerBodySegmentDirectionAxisScale_When_CalculatingCorrection_Then_RemovesXzAxisContribution()
        {
            bool calculated = TryCalculateSegmentDirectionReference(
                referenceSegmentDirection: new Vector3(1f, 1f, 0f),
                currentSegmentDirection: Vector3.forward,
                currentParentWorldRotation: Quaternion.identity,
                weight: 1f,
                maxAngleDegrees: 0f,
                correctionAxisXzScale: 0f,
                out Quaternion nextRotation);

            nextRotation.ToAngleAxis(out float angle, out Vector3 axis);

            Assert.That(calculated, Is.True);
            Assert.That(angle, Is.EqualTo(90f).Within(0.05f));
            Assert.That(Mathf.Abs(axis.x), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(axis.y), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Mathf.Abs(axis.z), Is.LessThan(0.0001f));
        }

        [Test]
        public void Given_LowerBodySegmentDirectionAxisScaleRemovesCorrection_When_CalculatingCorrection_Then_KeepsRotation()
        {
            Quaternion currentRotation = Quaternion.Euler(10f, 20f, 30f);
            bool calculated = TryCalculateSegmentDirectionReference(
                referenceSegmentDirection: Vector3.up,
                currentSegmentDirection: Vector3.forward,
                currentParentWorldRotation: currentRotation,
                weight: 1f,
                maxAngleDegrees: 0f,
                correctionAxisXzScale: 0f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.False);
            Assert.That(nextRotation, Is.EqualTo(currentRotation));
        }

        [Test]
        public void Given_ZeroSegmentDirectionWeight_When_CalculatingCorrection_Then_KeepsRotation()
        {
            Quaternion currentRotation = Quaternion.Euler(10f, 20f, 30f);
            bool calculated = TryCalculateSegmentDirectionReference(
                referenceSegmentDirection: Vector3.forward,
                currentSegmentDirection: Vector3.right,
                currentParentWorldRotation: currentRotation,
                weight: 0f,
                maxAngleDegrees: 0f,
                correctionAxisXzScale: 1f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.False);
            Assert.That(nextRotation, Is.EqualTo(currentRotation));
        }

        [Test]
        public void Given_NonFiniteSegmentDirection_When_CalculatingCorrection_Then_KeepsRotation()
        {
            Quaternion currentRotation = Quaternion.Euler(10f, 20f, 30f);
            bool calculated = TryCalculateSegmentDirectionReference(
                referenceSegmentDirection: new Vector3(float.NaN, 0f, 0f),
                currentSegmentDirection: Vector3.right,
                currentParentWorldRotation: currentRotation,
                weight: 1f,
                maxAngleDegrees: 0f,
                correctionAxisXzScale: 1f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.False);
            Assert.That(nextRotation, Is.EqualTo(currentRotation));
        }

        [Test]
        public void Given_SegmentDirectionCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            MethodInfo applierMethod = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateSegmentDirectionReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LowerBodySegmentDirectionReferenceParameterTypes,
                modifiers: null);

            Assert.That(applierMethod, Is.Not.Null,
                "ManualPoseReferenceApplier should own segment-direction calculation.");
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateEditorLowerBodySegmentDirectionReference",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty,
                "PoseSpaceRetargeter should delegate segment-direction calculation.");
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "ScaleCorrectionAxisXz",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty,
                "PoseSpaceRetargeter should delegate correction-axis scaling.");
        }

        private static bool TryCalculateHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateHipsLocalPositionReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HipsLocalPositionReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "ManualPoseReferenceApplier should expose the pure Hips localPosition reference calculation.");

            object[] args =
            {
                referenceCurrentLocalPosition,
                referenceRestLocalPosition,
                hasReferenceRestLocalPosition,
                currentLocalPosition,
                weight,
                maxOffset,
                currentLocalPosition
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextLocalPosition = (Vector3)args[6];
            return calculated;
        }

        private static bool TryCalculateAnchoredHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 targetRestLocalPosition,
            bool hasTargetRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateHipsLocalPositionReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: AnchoredHipsLocalPositionReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "ManualPoseReferenceApplier should expose the anchored Hips localPosition calculation so reference deltas do not compound current-pose collapse.");

            object[] args =
            {
                referenceCurrentLocalPosition,
                referenceRestLocalPosition,
                hasReferenceRestLocalPosition,
                targetRestLocalPosition,
                hasTargetRestLocalPosition,
                currentLocalPosition,
                weight,
                maxOffset,
                currentLocalPosition
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextLocalPosition = (Vector3)args[8];
            return calculated;
        }

        private static bool IsRecordingStartHipsBaselineFlip(
            float beforeLocalY,
            float afterLocalY,
            float warningThreshold)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "IsRecordingStartHipsBaselineFlip",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RecordingStartHipsBaselineFlipParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure guard for recording-start Hips localPosition baseline flips.");
            return (bool)method.Invoke(null, new object[] { beforeLocalY, afterLocalY, warningThreshold });
        }

        private static bool TryCalculateEditorFootLocalRotationReference(
            Quaternion referenceLocalRotation,
            Quaternion currentLocalRotation,
            float weight,
            out Quaternion nextLocalRotation)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateLocalRotationReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FootLocalRotationReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "ManualPoseReferenceApplier should expose the localRotation reference calculation.");

            object[] args =
            {
                referenceLocalRotation,
                currentLocalRotation,
                weight,
                currentLocalRotation
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextLocalRotation = (Quaternion)args[3];
            return calculated;
        }

        private static bool TryCalculateFootIkPositionReference(
            Vector3 referenceFootPosition,
            Vector3 referenceHipsPosition,
            Vector3 currentFootPosition,
            Vector3 targetHipsPosition,
            float weight,
            float maxOffset,
            out Vector3 nextPosition)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateFootIkPositionReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FootIkPositionReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "ManualPoseReferenceApplier should expose the pure foot IK target calculation.");

            object[] args =
            {
                referenceFootPosition,
                referenceHipsPosition,
                currentFootPosition,
                targetHipsPosition,
                weight,
                maxOffset,
                currentFootPosition
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextPosition = (Vector3)args[6];
            return calculated;
        }

        private static float CalculateBodyPositionXzFrameGateWeight(
            float currentFrame,
            float startFrame,
            float endFrame,
            float blendFrames)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "CalculateBodyPositionXzFrameGateWeight",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: BodyPositionXzFrameGateWeightParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should own the pure body position XZ frame gate calculation.");
            return (float)method.Invoke(null, new object[]
            {
                currentFrame,
                startFrame,
                endFrame,
                blendFrames
            });
        }

        private static bool TryCalculateBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 referenceBodyPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            out Vector3 nextBodyPosition)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateBodyPositionXzReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: BodyPositionXzReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should own the bounded body position XZ calculation.");

            object[] args =
            {
                currentBodyPosition,
                referenceBodyPosition,
                weight,
                maxOffset,
                axisXScale,
                axisZScale,
                Vector3.zero
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextBodyPosition = (Vector3)args[6];
            return calculated;
        }

        private static bool TryCalculateSignCorrectedBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 ghostFootPosition,
            Vector3 currentFootPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            out Vector3 nextBodyPosition)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateSignCorrectedBodyPositionXzReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: SignCorrectedBodyPositionXzReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should own the sign-corrected body position XZ calculation.");

            object[] args =
            {
                currentBodyPosition,
                ghostFootPosition,
                currentFootPosition,
                weight,
                maxOffset,
                axisXScale,
                axisZScale,
                Vector3.zero
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextBodyPosition = (Vector3)args[7];
            return calculated;
        }

        private static bool TryCalculateSignCorrectedBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 ghostFootPosition,
            Vector3 currentFootPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            bool invertX,
            bool invertZ,
            out Vector3 nextBodyPosition)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateSignCorrectedBodyPositionXzReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: SignCorrectedBodyPositionXzReferenceWithInversionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should own body position XZ axis inversion.");

            object[] args =
            {
                currentBodyPosition,
                ghostFootPosition,
                currentFootPosition,
                weight,
                maxOffset,
                axisXScale,
                axisZScale,
                invertX,
                invertZ,
                Vector3.zero
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextBodyPosition = (Vector3)args[9];
            return calculated;
        }

        private static bool IsFrameWithinGate(int currentFrame, float startFrame, float endFrame)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "IsFrameWithinGate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FrameWithinGateParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should own the shared inclusive frame gate calculation.");
            return (bool)method.Invoke(null, new object[] { currentFrame, startFrame, endFrame });
        }

        private static bool HasActiveFrameGate(float startFrame, float endFrame)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "HasActiveFrameGate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ActiveFrameGateParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should expose frame gate activation without reading animation time.");
            return (bool)method.Invoke(null, new object[] { startFrame, endFrame });
        }

        private static bool HasConfiguredFrameGate(float startFrame, float endFrame)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "HasConfiguredFrameGate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ActiveFrameGateParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should detect configured gates before reading animation time.");
            return (bool)method.Invoke(null, new object[] { startFrame, endFrame });
        }

        private static bool IsFrameWithinSingleFrameFallbackGate(
            int currentFrame,
            float startFrame,
            float endFrame)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "IsFrameWithinSingleFrameFallbackGate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FrameWithinGateParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should own the single-frame fallback gate calculation.");
            return (bool)method.Invoke(null, new object[] { currentFrame, startFrame, endFrame });
        }

        private static bool TryCalculateHipsAlignedEndpointPositionReference(
            Vector3 referenceEndpointPosition,
            Vector3 referenceHipsPosition,
            Transform referenceRoot,
            Vector3 targetHipsPosition,
            Vector3 currentTargetEndpointPosition,
            Transform targetRoot,
            out Vector3 desiredEndpointPosition)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateHipsAlignedEndpointPositionReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HipsAlignedEndpointPositionReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should expose hips-aligned endpoint position calculation.");

            object[] args =
            {
                referenceEndpointPosition,
                referenceHipsPosition,
                referenceRoot,
                targetHipsPosition,
                currentTargetEndpointPosition,
                targetRoot,
                currentTargetEndpointPosition
            };

            bool calculated = (bool)method.Invoke(null, args);
            desiredEndpointPosition = (Vector3)args[6];
            return calculated;
        }

        private static bool TryCalculateSegmentDirectionReference(
            Vector3 referenceSegmentDirection,
            Vector3 currentSegmentDirection,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            float correctionAxisXzScale,
            out Quaternion nextRotation)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "TryCalculateSegmentDirectionReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LowerBodySegmentDirectionReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should expose generic segment-direction calculation.");

            object[] args =
            {
                referenceSegmentDirection,
                currentSegmentDirection,
                currentParentWorldRotation,
                weight,
                maxAngleDegrees,
                correctionAxisXzScale,
                currentParentWorldRotation
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextRotation = (Quaternion)args[6];
            return calculated;
        }

    }
}
