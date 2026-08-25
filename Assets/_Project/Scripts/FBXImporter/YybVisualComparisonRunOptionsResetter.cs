using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonRunOptionsResetter
    {
        public static void ResetTransientSettings(
            YybVisualComparisonRunOptions options,
            YybVisualComparisonRunOptions defaults)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            string fbxFileName = options.fbxFileName;
            float durationSeconds = options.durationSeconds;
            int targetFrameCount = options.targetFrameCount;
            bool enableFingerCloseups = options.enableFingerCloseups;
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented =
                options.enableRecorderParentFrameIkOffsetsWhenCenterParented;
            string editorDiagnosticSmokeSegment = options.editorDiagnosticSmokeSegment;
            int diagnosticCaptureWidthOverride = options.diagnosticCaptureWidthOverride;
            int diagnosticCaptureHeightOverride = options.diagnosticCaptureHeightOverride;
            float diagnosticScreenshotPaddingOverride = options.diagnosticScreenshotPaddingOverride;
            float diagnosticScreenshotVerticalViewportCenterOverride =
                options.diagnosticScreenshotVerticalViewportCenterOverride;

            YybVisualComparisonRunOptionsCopier.Copy(defaults, options);

            options.fbxFileName = fbxFileName;
            options.durationSeconds = durationSeconds;
            options.targetFrameCount = targetFrameCount;
            options.enableFingerCloseups = enableFingerCloseups;
            options.enableRecorderParentFrameIkOffsetsWhenCenterParented =
                enableRecorderParentFrameIkOffsetsWhenCenterParented;
            options.editorDiagnosticSmokeSegment = editorDiagnosticSmokeSegment;
            options.diagnosticCaptureWidthOverride = diagnosticCaptureWidthOverride;
            options.diagnosticCaptureHeightOverride = diagnosticCaptureHeightOverride;
            options.diagnosticScreenshotPaddingOverride = diagnosticScreenshotPaddingOverride;
            options.diagnosticScreenshotVerticalViewportCenterOverride =
                diagnosticScreenshotVerticalViewportCenterOverride;
        }
    }
}
