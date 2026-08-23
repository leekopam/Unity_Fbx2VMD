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

        private static readonly Type[] LowerBodySegmentDirectionReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(Quaternion),
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
        public void Given_LowerBodySegmentDirectionReference_When_CalculatingCorrection_Then_RotatesTowardReferenceDirection()
        {
            bool calculated = TryCalculateEditorLowerBodySegmentDirectionReference(
                referenceSegmentDirection: Vector3.forward,
                currentSegmentDirection: Vector3.right,
                currentParentWorldRotation: Quaternion.identity,
                weight: 0.5f,
                maxAngleDegrees: 0f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.True);
            Assert.That(Quaternion.Angle(Quaternion.identity, nextRotation), Is.EqualTo(45f).Within(0.05f));
        }

        [Test]
        public void Given_LowerBodySegmentDirectionMaxAngle_When_CalculatingCorrection_Then_ClampsBeforeWeight()
        {
            bool calculated = TryCalculateEditorLowerBodySegmentDirectionReference(
                referenceSegmentDirection: Vector3.forward,
                currentSegmentDirection: Vector3.right,
                currentParentWorldRotation: Quaternion.identity,
                weight: 1f,
                maxAngleDegrees: 10f,
                out Quaternion nextRotation);

            Assert.That(calculated, Is.True);
            Assert.That(Quaternion.Angle(Quaternion.identity, nextRotation), Is.EqualTo(10f).Within(0.05f));
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

        private static bool TryCalculateEditorLowerBodySegmentDirectionReference(
            Vector3 referenceSegmentDirection,
            Vector3 currentSegmentDirection,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextRotation)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateEditorLowerBodySegmentDirectionReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LowerBodySegmentDirectionReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure segment-direction helper for morphology-aware lower-body runtime candidates.");

            object[] args =
            {
                referenceSegmentDirection,
                currentSegmentDirection,
                currentParentWorldRotation,
                weight,
                maxAngleDegrees,
                currentParentWorldRotation
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextRotation = (Quaternion)args[5];
            return calculated;
        }

    }
}
