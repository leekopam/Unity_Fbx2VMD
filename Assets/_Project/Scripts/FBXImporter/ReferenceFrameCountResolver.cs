using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceFrameCountResolver
    {
        internal static int Resolve(
            string sourceFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate,
            string knownReferenceBaseName,
            int knownReferenceMaxFrameIndex)
        {
            return TryResolveKnownReference(
                    sourceFileName,
                    requestedDurationSeconds,
                    configuredTargetFrameCount,
                    referenceClipLengthSeconds,
                    recordingFrameRate,
                    knownReferenceBaseName,
                    knownReferenceMaxFrameIndex,
                    out int referenceTargetFrameCount)
                ? referenceTargetFrameCount
                : Mathf.Max(0, configuredTargetFrameCount);
        }

        internal static int ResolveSummaryTarget(int referenceTargetFrameCount, int candidateFrameCount)
        {
            _ = candidateFrameCount;
            return Mathf.Max(0, referenceTargetFrameCount);
        }

        internal static bool TryResolveKnownReference(
            string sourceFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate,
            string knownReferenceBaseName,
            int knownReferenceMaxFrameIndex,
            out int referenceTargetFrameCount)
        {
            referenceTargetFrameCount = 0;
            if (recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate) ||
                requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                configuredTargetFrameCount <= 0 ||
                referenceClipLengthSeconds <= 0f ||
                float.IsNaN(referenceClipLengthSeconds) ||
                float.IsInfinity(referenceClipLengthSeconds) ||
                knownReferenceMaxFrameIndex < 0)
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(sourceFileName ?? string.Empty);
            if (!string.Equals(cleanBaseName, knownReferenceBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int knownReferenceFrameCount = knownReferenceMaxFrameIndex + 1;
            float knownReferenceDurationSeconds = knownReferenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            bool clipCoversReference = referenceClipLengthSeconds + frameToleranceSeconds >= knownReferenceDurationSeconds;
            bool requestCoversReference = requestedDurationSeconds + frameToleranceSeconds >= knownReferenceDurationSeconds;
            bool configuredFramesCoverReference = configuredTargetFrameCount >= knownReferenceFrameCount;
            if (!clipCoversReference || !requestCoversReference || !configuredFramesCoverReference)
            {
                return false;
            }

            referenceTargetFrameCount = knownReferenceFrameCount;
            return true;
        }
    }
}
