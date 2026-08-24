using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class FBXEditorDiagnosticRiskEvaluatorTests
    {
        [Test]
        public void Given_EditorDiagnosticRisk_When_CheckingOwnership_Then_UsesDedicatedEvaluator()
        {
            ResolveEvaluator(out Type evaluatorType, out Type inputType, out MethodInfo evaluateMethod);

            Assert.That(inputType, Is.Not.Null);
            Assert.That(evaluateMethod, Is.Not.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "BuildEditorSmokeThumbRiskFailureMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "IsFiniteDiagnosticRisk",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void Given_MissingProbe_When_EvaluatingRisk_Then_FailsWithDiagnosticMessage()
        {
            object evaluation = Evaluate((inputType, input) =>
            {
                SetInput(inputType, input, "HasProbe", false);
                SetInput(inputType, input, "FbxName", "motion.fbx");
            });

            Assert.That(ReadEvaluation(evaluation, "Outcome").ToString(), Is.EqualTo("Fail"));
            Assert.That(
                ReadEvaluation(evaluation, "Message"),
                Is.EqualTo(
                    "Editor smoke 실패: motion.fbx - MotionComparisonProbe가 없어 엄지 리스크 검증을 수행하지 못했습니다."));
        }

        [Test]
        public void Given_IncompleteCoverage_When_EvaluatingRisk_Then_FailsWithCoverageDetails()
        {
            object evaluation = Evaluate((inputType, input) =>
            {
                SetInput(inputType, input, "HasFullThumbAnatomyCoverage", false);
                SetInput(inputType, input, "LeftThumbCoreAnatomyObserved", false);
                SetInput(inputType, input, "LeftThumbHelperCoverageRequired", true);
                SetInput(inputType, input, "LeftThumbHelperCoverageSatisfied", false);
            });

            Assert.That(ReadEvaluation(evaluation, "Outcome").ToString(), Is.EqualTo("Fail"));
            Assert.That(
                (string)ReadEvaluation(evaluation, "Message"),
                Does.Contain("enabled=True, frames=1, leftCore=False"));
            Assert.That(
                (string)ReadEvaluation(evaluation, "Message"),
                Does.Contain("leftHelperRequired=True"));
        }

        [Test]
        public void Given_RiskWithinThreshold_When_EvaluatingRisk_Then_ReturnsNone()
        {
            object evaluation = Evaluate((inputType, input) =>
            {
                SetInput(inputType, input, "MaxGenericThumbAnatomyRisk", 0.5f);
                SetInput(inputType, input, "MaxYybDeformationRisk", float.NaN);
            });

            Assert.That(ReadEvaluation(evaluation, "Outcome").ToString(), Is.EqualTo("None"));
            Assert.That(ReadEvaluation(evaluation, "Message"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_ExceededRiskWithoutEvidence_When_EvaluatingRisk_Then_Fails()
        {
            object evaluation = Evaluate((inputType, input) =>
            {
                SetInput(inputType, input, "MaxGenericThumbAnatomyRisk", 0.75f);
                SetInput(inputType, input, "MaxThumbSpreadRisk", 0.6f);
                SetInput(inputType, input, "MaxThumbProjectionRisk", 0.7f);
                SetInput(inputType, input, "MaxThumbHelperSeparationRisk", 0.2f);
                SetInput(inputType, input, "MaxThumbWebbingRisk", 0.3f);
                SetInput(inputType, input, "NonBlankScreenshotCount", 7);
            });

            Assert.That(ReadEvaluation(evaluation, "Outcome").ToString(), Is.EqualTo("Fail"));
            Assert.That(
                (string)ReadEvaluation(evaluation, "Message"),
                Does.Contain("thumb anatomy risk 0.75 > 0.5"));
            Assert.That(
                (string)ReadEvaluation(evaluation, "Message"),
                Does.EndWith("same-frame visual evidence incomplete (nonblankScreenshots=7)"));
        }

        [Test]
        public void Given_ExceededRiskWithEvidence_When_EvaluatingRisk_Then_ReturnsWarning()
        {
            object evaluation = Evaluate((inputType, input) =>
            {
                SetInput(inputType, input, "MaxYybDeformationRisk", 0.8f);
                SetInput(inputType, input, "NonBlankScreenshotCount", 8);
            });

            Assert.That(ReadEvaluation(evaluation, "Outcome").ToString(), Is.EqualTo("Warn"));
            Assert.That(
                ReadEvaluation(evaluation, "Message"),
                Is.EqualTo("Editor smoke 실패: motion.fbx - YYB deformation risk 0.8 > 0.5"));
        }

        private static object Evaluate(Action<Type, object> configure)
        {
            ResolveEvaluator(out Type evaluatorType, out Type inputType, out MethodInfo evaluateMethod);
            object input = Activator.CreateInstance(inputType, nonPublic: true);
            ConfigureValidInput(inputType, input);
            configure(inputType, input);

            return evaluateMethod.Invoke(null, new[] { input });
        }

        private static void ConfigureValidInput(Type inputType, object input)
        {
            SetInput(inputType, input, "FbxName", "motion.fbx");
            SetInput(inputType, input, "HasProbe", true);
            SetInput(inputType, input, "RiskDiagnosticsEnabled", true);
            SetInput(inputType, input, "RiskEvaluationFrameCount", 1);
            SetInput(inputType, input, "HasFullThumbAnatomyCoverage", true);
            SetInput(inputType, input, "HasResolvedThumbHelperCoverage", true);
            SetInput(inputType, input, "LeftThumbCoreAnatomyObserved", true);
            SetInput(inputType, input, "RightThumbCoreAnatomyObserved", true);
            SetInput(inputType, input, "LeftThumbHelperCoverageSatisfied", true);
            SetInput(inputType, input, "RightThumbHelperCoverageSatisfied", true);
            SetInput(inputType, input, "MaxGenericThumbAnatomyRisk", 0.25f);
            SetInput(inputType, input, "MaxYybDeformationRisk", 0.25f);
            SetInput(inputType, input, "MaxGenericThumbAnatomyRiskThreshold", 0.5f);
            SetInput(inputType, input, "MaxYybDeformationRiskThreshold", 0.5f);
            SetInput(inputType, input, "NonBlankScreenshotCount", 8);
        }

        private static void SetInput(
            Type inputType,
            object input,
            string propertyName,
            object value)
        {
            PropertyInfo property = inputType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            property.SetValue(input, value);
        }

        private static object ReadEvaluation(object evaluation, string propertyName)
        {
            PropertyInfo property = evaluation.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(evaluation);
        }

        private static void ResolveEvaluator(
            out Type evaluatorType,
            out Type inputType,
            out MethodInfo evaluateMethod)
        {
            evaluatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FBXEditorDiagnosticRiskEvaluator",
                throwOnError: false);
            Assert.That(evaluatorType, Is.Not.Null);
            inputType = evaluatorType.GetNestedType("Input", BindingFlags.NonPublic);
            evaluateMethod = evaluatorType.GetMethod(
                "Evaluate",
                BindingFlags.Static | BindingFlags.NonPublic);
        }
    }
}
