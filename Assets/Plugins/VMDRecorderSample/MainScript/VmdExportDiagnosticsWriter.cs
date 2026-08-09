using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using BoneNames = UnityHumanoidVMDRecorder.BoneNames;
using ExportIkSourceDiagnosticSample = UnityHumanoidVMDRecorder.ExportIkSourceDiagnosticSample;
using ExportRotationDiagnosticAggregate = UnityHumanoidVMDRecorder.ExportRotationDiagnosticAggregate;
using ExportRotationDiagnosticSample = UnityHumanoidVMDRecorder.ExportRotationDiagnosticSample;

internal static class VmdExportDiagnosticsWriter
{
    internal static List<ExportIkSourceDiagnosticSample> BuildFinalExportIkSourceDiagnosticSamples(
        IEnumerable<ExportIkSourceDiagnosticSample> samples,
        IReadOnlyDictionary<BoneNames, List<Vector3>> finalVmdPositions,
        int safeFrameCount,
        Func<Vector3, Vector3> convertVmdExportPositionToUnityMeters)
    {
        var finalSamples = new List<ExportIkSourceDiagnosticSample>();
        if (samples == null)
        {
            return finalSamples;
        }

        foreach (ExportIkSourceDiagnosticSample sample in samples)
        {
            Vector3 exportedUnityPosition = sample.ExportedUnityPosition;
            if (finalVmdPositions != null &&
                sample.RecorderFrameNumber >= 0 &&
                sample.RecorderFrameNumber < safeFrameCount &&
                finalVmdPositions.TryGetValue(sample.BoneName, out var finalPositions) &&
                finalPositions != null &&
                sample.RecorderFrameNumber < finalPositions.Count)
            {
                exportedUnityPosition = convertVmdExportPositionToUnityMeters(finalPositions[sample.RecorderFrameNumber]);
            }

            finalSamples.Add(new ExportIkSourceDiagnosticSample(
                sample.RecorderFrameNumber,
                sample.UnityFrameNumber,
                sample.SampleTime,
                sample.BoneName,
                sample.RootReferencePosition,
                sample.SourceWorldPosition,
                sample.SourceRelativePosition,
                exportedUnityPosition,
                sample.DirectFootWorldPosition,
                sample.DirectFootRootPosition,
                sample.RecorderRootPosition,
                sample.SourceRecorderRootPosition,
                sample.DirectFootRecorderRootPosition));
        }

        return finalSamples;
    }

    internal static string BuildExportRotationDiagnosticsCsv(IEnumerable<ExportRotationDiagnosticAggregate> aggregates)
    {
        var builder = new StringBuilder();
        builder.AppendLine("boneName,boneIndex,sampleCount,maxGhostVsSourceLocalDeltaFrame,maxGhostVsSourceLocalDeltaDegrees,maxParentRestBasisCorrectedVsSourceLocalDeltaFrame,maxParentRestBasisCorrectedVsSourceLocalDeltaDegrees,maxExportVsSourceLocalDeltaFrame,maxExportVsSourceLocalDeltaDegrees,exportSourceMode");

        if (aggregates == null)
        {
            return builder.ToString();
        }

        foreach (ExportRotationDiagnosticAggregate aggregate in aggregates.OrderBy(row => (int)row.BoneName))
        {
            builder.Append(CsvEscape(aggregate.BoneName.ToString()));
            builder.Append(',');
            builder.Append(((int)aggregate.BoneName).ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(aggregate.SampleCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(aggregate.MaxGhostVsSourceLocalDeltaFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(aggregate.MaxGhostVsSourceLocalDeltaDegrees));
            builder.Append(',');
            builder.Append(aggregate.MaxParentRestBasisCorrectedVsSourceLocalDeltaFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(aggregate.MaxParentRestBasisCorrectedVsSourceLocalDeltaDegrees));
            builder.Append(',');
            builder.Append(aggregate.MaxExportVsSourceLocalDeltaFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(aggregate.MaxExportVsSourceLocalDeltaDegrees));
            builder.Append(',');
            builder.Append(CsvEscape(aggregate.ExportSourceMode));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    internal static string BuildExportRotationDiagnosticSamplesCsv(IEnumerable<ExportRotationDiagnosticSample> samples)
    {
        var builder = new StringBuilder();
        builder.AppendLine("frameNumber,boneName,boneIndex,sourceMode,exportSourceMode,ghostVsSourceLocalDeltaDegrees,parentRestBasisCorrectedVsSourceLocalDeltaDegrees,exportVsSourceLocalDeltaDegrees,sourceLocalDeltaX,sourceLocalDeltaY,sourceLocalDeltaZ,sourceLocalDeltaW,exportLocalX,exportLocalY,exportLocalZ,exportLocalW,exportVmdX,exportVmdY,exportVmdZ,exportVmdW");

        if (samples == null)
        {
            return builder.ToString();
        }

        foreach (ExportRotationDiagnosticSample sample in samples.OrderBy(row => row.FrameNumber).ThenBy(row => (int)row.Diagnostic.BoneName))
        {
            VmdBoneRotationDiagnostic diagnostic = sample.Diagnostic;
            builder.Append(sample.FrameNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(diagnostic.BoneName.ToString()));
            builder.Append(',');
            builder.Append(((int)diagnostic.BoneName).ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(diagnostic.SourceMode));
            builder.Append(',');
            builder.Append(CsvEscape(diagnostic.ExportSourceMode));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(diagnostic.GhostVsSourceLocalDeltaAngleDegrees));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(diagnostic.ParentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(diagnostic.ExportVsSourceLocalDeltaAngleDegrees));
            AppendQuaternion(builder, diagnostic.SourceLocalDeltaRotation);
            AppendQuaternion(builder, diagnostic.ExportLocalRotation);
            AppendQuaternion(builder, diagnostic.ExportVmdRotation);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    internal static string BuildExportIkSourceDiagnosticsCsv(IEnumerable<ExportIkSourceDiagnosticSample> samples)
    {
        var builder = new StringBuilder();
        builder.AppendLine("recorderFrame,unityFrame,sampleTime,boneName,boneIndex,rootReferencePosition,sourceWorldPosition,sourceRelativePosition,exportedUnityPosition,directFootWorldPosition,directFootRootPosition,recorderRootPosition,sourceRecorderRootPosition,directFootRecorderRootPosition,sourceRelativeVsSourceRecorderRootDelta,sourceRelativeVsDirectFootRecorderRootDelta,exportedUnityVsSourceRelativeDelta,exportedUnityVsSourceRecorderRootDelta");

        if (samples == null)
        {
            return builder.ToString();
        }

        foreach (ExportIkSourceDiagnosticSample sample in samples.OrderBy(row => row.RecorderFrameNumber).ThenBy(row => (int)row.BoneName))
        {
            builder.Append(sample.RecorderFrameNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(sample.UnityFrameNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(sample.SampleTime));
            builder.Append(',');
            builder.Append(CsvEscape(sample.BoneName.ToString()));
            builder.Append(',');
            builder.Append(((int)sample.BoneName).ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.RootReferencePosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceWorldPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRelativePosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.ExportedUnityPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.DirectFootWorldPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.DirectFootRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.RecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.DirectFootRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRelativePosition - sample.SourceRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRelativePosition - sample.DirectFootRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.ExportedUnityPosition - sample.SourceRelativePosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.ExportedUnityPosition - sample.SourceRecorderRootPosition));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatDiagnosticFloat(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string FormatDiagnosticVector3(Vector3 value)
    {
        return CsvEscape(
            FormatDiagnosticFloat(value.x) + "|" +
            FormatDiagnosticFloat(value.y) + "|" +
            FormatDiagnosticFloat(value.z));
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void AppendQuaternion(StringBuilder builder, Quaternion value)
    {
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.x));
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.y));
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.z));
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.w));
    }
}
