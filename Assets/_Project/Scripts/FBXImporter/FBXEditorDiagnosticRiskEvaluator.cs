#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Editor smoke 엄지 진단 수치와 증거 충족 여부를 판정함.
    /// </summary>
    internal static class FBXEditorDiagnosticRiskEvaluator
    {
        internal enum Outcome
        {
            None,
            Fail,
            Warn
        }

        internal sealed class Input
        {
            internal string FbxName { get; set; }
            internal bool HasProbe { get; set; }
            internal bool RiskDiagnosticsEnabled { get; set; }
            internal int RiskEvaluationFrameCount { get; set; }
            internal bool HasFullThumbAnatomyCoverage { get; set; }
            internal bool HasResolvedThumbHelperCoverage { get; set; }
            internal bool LeftThumbCoreAnatomyObserved { get; set; }
            internal bool RightThumbCoreAnatomyObserved { get; set; }
            internal bool LeftThumbHelperCoverageRequired { get; set; }
            internal bool RightThumbHelperCoverageRequired { get; set; }
            internal bool LeftThumbHelperCoverageSatisfied { get; set; }
            internal bool RightThumbHelperCoverageSatisfied { get; set; }
            internal float MaxGenericThumbAnatomyRisk { get; set; }
            internal float MaxYybDeformationRisk { get; set; }
            internal float MaxThumbSpreadRisk { get; set; }
            internal float MaxThumbProjectionRisk { get; set; }
            internal float MaxThumbHelperSeparationRisk { get; set; }
            internal float MaxThumbWebbingRisk { get; set; }
            internal float MaxGenericThumbAnatomyRiskThreshold { get; set; }
            internal float MaxYybDeformationRiskThreshold { get; set; }
            internal int NonBlankScreenshotCount { get; set; }
        }

        internal readonly struct Evaluation
        {
            internal Evaluation(Outcome outcome, string message)
            {
                Outcome = outcome;
                Message = message ?? string.Empty;
            }

            internal Outcome Outcome { get; }
            internal string Message { get; }
        }

        internal static Evaluation Evaluate(Input input)
        {
            string fbxName = string.IsNullOrWhiteSpace(input?.FbxName)
                ? "unknown.fbx"
                : input.FbxName;
            if (input == null || !input.HasProbe)
            {
                return Fail(
                    $"Editor smoke 실패: {fbxName} - MotionComparisonProbe가 없어 엄지 리스크 검증을 수행하지 못했습니다.");
            }

            if (!input.RiskDiagnosticsEnabled ||
                input.RiskEvaluationFrameCount <= 0 ||
                !input.HasFullThumbAnatomyCoverage ||
                !input.HasResolvedThumbHelperCoverage)
            {
                return Fail(BuildDiagnosticUnavailableMessage(input, fbxName));
            }

            bool genericExceeded =
                IsFinite(input.MaxGenericThumbAnatomyRisk) &&
                input.MaxGenericThumbAnatomyRisk > input.MaxGenericThumbAnatomyRiskThreshold;
            bool yybExceeded =
                IsFinite(input.MaxYybDeformationRisk) &&
                input.MaxYybDeformationRisk > input.MaxYybDeformationRiskThreshold;
            if (!genericExceeded && !yybExceeded)
            {
                return new Evaluation(Outcome.None, string.Empty);
            }

            string message = BuildRiskFailureMessage(
                input,
                fbxName,
                genericExceeded,
                yybExceeded);
            if (input.NonBlankScreenshotCount < 8)
            {
                return Fail(
                    $"{message}; same-frame visual evidence incomplete " +
                    $"(nonblankScreenshots={input.NonBlankScreenshotCount})");
            }

            return new Evaluation(Outcome.Warn, message);
        }

        private static Evaluation Fail(string message)
        {
            return new Evaluation(Outcome.Fail, message);
        }

        private static string BuildDiagnosticUnavailableMessage(Input input, string fbxName)
        {
            return
                $"Editor smoke 실패: {fbxName} - 엄지 리스크 진단 범위가 부족합니다 " +
                $"(enabled={input.RiskDiagnosticsEnabled}, frames={input.RiskEvaluationFrameCount}, " +
                $"leftCore={input.LeftThumbCoreAnatomyObserved}, rightCore={input.RightThumbCoreAnatomyObserved}, " +
                $"leftHelperRequired={input.LeftThumbHelperCoverageRequired}, rightHelperRequired={input.RightThumbHelperCoverageRequired}, " +
                $"leftHelperOk={input.LeftThumbHelperCoverageSatisfied}, rightHelperOk={input.RightThumbHelperCoverageSatisfied})";
        }

        private static string BuildRiskFailureMessage(
            Input input,
            string fbxName,
            bool genericExceeded,
            bool yybExceeded)
        {
            var reasons = new List<string>();
            if (genericExceeded)
            {
                reasons.Add(
                    $"thumb anatomy risk {FormatRisk(input.MaxGenericThumbAnatomyRisk)} > " +
                    $"{FormatRisk(input.MaxGenericThumbAnatomyRiskThreshold)} " +
                    $"(spread={FormatRisk(input.MaxThumbSpreadRisk)}, " +
                    $"projection={FormatRisk(input.MaxThumbProjectionRisk)}, " +
                    $"helper={FormatRisk(input.MaxThumbHelperSeparationRisk)}, " +
                    $"webbing={FormatRisk(input.MaxThumbWebbingRisk)})");
            }

            if (yybExceeded)
            {
                reasons.Add(
                    $"YYB deformation risk {FormatRisk(input.MaxYybDeformationRisk)} > " +
                    $"{FormatRisk(input.MaxYybDeformationRiskThreshold)}");
            }

            return $"Editor smoke 실패: {fbxName} - {string.Join("; ", reasons)}";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string FormatRisk(float value)
        {
            return IsFinite(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : "n/a";
        }
    }
}
#endif
