using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor.FBXImporter
{
    public class RetargetingDiagnosticReporterTests
    {
        [Test]
        public void Given_RetargetDiagnostics_When_CheckingOwnership_Then_UsesDedicatedReporter()
        {
            ResolveReporter(out Type reporterType, out MethodInfo buildSummaryMethod);

            Assert.That(reporterType, Is.Not.Null);
            Assert.That(buildSummaryMethod, Is.Not.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "BuildActiveRetargeterThumbReferenceSummary",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "GetHierarchyPath",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                Array.Exists(
                    typeof(FBXVmdPipeline).GetMethods(
                        BindingFlags.Static | BindingFlags.NonPublic),
                    method => method.Name == "ReadRetargeterPrivateField"),
                Is.False);
        }

        [Test]
        public void Given_MissingRetargeter_When_BuildingThumbReferenceSummary_Then_ReturnsMissingLabel()
        {
            ResolveReporter(out _, out MethodInfo buildSummaryMethod);

            string summary = (string)buildSummaryMethod.Invoke(null, new object[] { null });

            Assert.That(summary, Is.EqualTo("retargeter=<none>"));
        }

        [Test]
        public void Given_RetargeterHierarchy_When_BuildingThumbReferenceSummary_Then_IncludesHierarchyPath()
        {
            ResolveReporter(out _, out MethodInfo buildSummaryMethod);
            var root = new GameObject("DiagnosticRoot");
            var child = new GameObject("RetargeterNode");
            child.transform.SetParent(root.transform, false);
            PoseSpaceRetargeter retargeter = child.AddComponent<PoseSpaceRetargeter>();

            try
            {
                string summary = (string)buildSummaryMethod.Invoke(
                    null,
                    new object[] { retargeter });

                Assert.That(summary, Does.Contain("retargeter=DiagnosticRoot/RetargeterNode"));
                Assert.That(summary, Does.Contain("targetAnimator=<null>"));
                Assert.That(summary, Does.Contain("referenceAnimator=<null>"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Given_Retargeter_When_LoggingPlaybackStability_Then_WritesExpectedContract()
        {
            MethodInfo logMethod = ResolveReporterMethod("LogPlaybackStability");
            var retargeterObject = new GameObject("Retargeter");
            PoseSpaceRetargeter retargeter = retargeterObject.AddComponent<PoseSpaceRetargeter>();

            try
            {
                const string expected =
                    "[FBXImport] Retarget playback stability: " +
                    "clipTimeClamp=False, maxClipStep=0.0000s, stepSpikes=0, " +
                    "poseSmooth=0, muscleOnlySmoothSkipped=0, maxPoseMuscleDelta=0.0000, " +
                    "hipsLocalClamp=0, maxHipsLocalDelta=0.0000m, " +
                    "thumbReference[retargeter=Retargeter, targetAnimator=<null>, " +
                    "thumbLocalRefConfig=True, preserveThumbMuscles=True, " +
                    "editorFingerRuntime=False, referenceAnimator=<null>, " +
                    "manualThumbActive=False, suppressLeft=False, suppressRight=False, " +
                    "leftLocalGuardClamp=0, rightLocalGuardClamp=0, " +
                    "leftLocalGuardPreserve=0, rightLocalGuardPreserve=0]";
                LogAssert.Expect(LogType.Log, expected);

                logMethod.Invoke(null, new object[] { retargeter });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(retargeterObject);
            }
        }

        [Test]
        public void Given_ThumbDiagnostics_When_LoggingEditorState_Then_PreservesSideMapping()
        {
            MethodInfo logMethod = ResolveReporterMethod("LogEditorSmokeThumbState");
            var target = new GameObject("DiagnosticTarget");
            target.AddComponent<HumanoidThumbDeformationGuard>();
            var retargeterObject = new GameObject("Retargeter");
            retargeterObject.transform.SetParent(target.transform, false);
            PoseSpaceRetargeter retargeter = retargeterObject.AddComponent<PoseSpaceRetargeter>();

            try
            {
                const string expected =
                    "[FBXImport] Editor smoke thumb state (capture): " +
                    "fbx=sample.fbx, segment=head, projectionMin=0.358, " +
                    "thumbReference[retargeter=DiagnosticTarget/Retargeter, targetAnimator=<null>, " +
                    "thumbLocalRefConfig=True, preserveThumbMuscles=True, " +
                    "editorFingerRuntime=False, referenceAnimator=<null>, " +
                    "manualThumbActive=False, suppressLeft=False, suppressRight=False, " +
                    "leftLocalGuardClamp=0, rightLocalGuardClamp=0, " +
                    "leftLocalGuardPreserve=0, rightLocalGuardPreserve=0], " +
                    "guardLeft[side=L, helper=<none>, source=<none>, state=missing], " +
                    "guardRight[side=R, helper=<none>, source=<none>, state=missing], " +
                    "retargeterLeft[side=L, helper=<none>, source=<none>, state=missing], " +
                    "retargeterRight[side=R, helper=<none>, source=<none>, state=missing]";
                LogAssert.Expect(LogType.Log, expected);

                logMethod.Invoke(
                    null,
                    new object[]
                    {
                        "capture",
                        "sample.fbx",
                        FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
                        0.358f,
                        target,
                        retargeter
                    });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_MissingTarget_When_LoggingEditorState_Then_DoesNotWriteLog()
        {
            MethodInfo logMethod = ResolveReporterMethod("LogEditorSmokeThumbState");

            logMethod.Invoke(
                null,
                new object[]
                {
                    "capture",
                    "sample.fbx",
                    FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
                    0.358f,
                    null,
                    null
                });

            LogAssert.NoUnexpectedReceived();
        }

        private static void ResolveReporter(
            out Type reporterType,
            out MethodInfo buildSummaryMethod)
        {
            reporterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingDiagnosticReporter",
                throwOnError: false);
            Assert.That(reporterType, Is.Not.Null);
            buildSummaryMethod = reporterType.GetMethod(
                "BuildThumbReferenceSummary",
                BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static MethodInfo ResolveReporterMethod(string methodName)
        {
            Type reporterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingDiagnosticReporter",
                throwOnError: false);
            Assert.That(reporterType, Is.Not.Null);
            MethodInfo method = reporterType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
