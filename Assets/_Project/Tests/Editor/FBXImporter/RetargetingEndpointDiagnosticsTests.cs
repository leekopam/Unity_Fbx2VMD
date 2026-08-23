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
