#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Member_Han.Modules.FBXImporter.EditorTools
{
    [InitializeOnLoad]
    public static class YybVisualComparisonBatchRunner
    {
        private const string MenuRoot = "Machine Spirit/YYB Compare/";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string SubManualScenePath = "Assets/_Project/Scene/Sub_Manual.unity";
        private const string DefaultFbxFileName = "satisfaction_2.fbx";
        private const string SatisfactionReferenceOutputBaseName = "satisfaction_2";
        private const int SatisfactionReferenceMaxMmdFrame = 6000;
        private const string ManualControllerPath = "Assets/_ManualReference/SampleAnimation/TestAnimator1_Manual.controller";
        private const string FallbackControllerPath = "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller";
        private const string ProjectFbxDirectory = "Assets/_Project/FBX";
        private const string ImportFbxDirectory = "Assets/Resources/Import_FBX";
        private const string OutputRootDirectory = "Docs/Machine_Spirit/Local/ComparisonSessions";
        private const string MmdAutomationRunsRelativePath = "Docs/Machine_Spirit/Local/MMDQASessions/automation_runs";
        private const string LatestSummaryJsonRelativePath = "Docs/Machine_Spirit/Local/progress/evidence/yyb_visual_compare_latest.json";
        private const string LatestSummaryMarkdownRelativePath = "Docs/Machine_Spirit/Local/progress/evidence/yyb_visual_compare_latest.md";
        private const string SummaryJsonFileName = "yyb_visual_compare_summary.json";
        private const string SummaryMarkdownFileName = "yyb_visual_compare_summary.md";
        private const string RunnerTraceRelativePath = "Docs/Machine_Spirit/Local/runtime/yyb_visual_compare_runner_trace.log";
        private const string ReferenceMp4ProvenanceEvidenceRelativePath = "Docs/Machine_Spirit/Local/ReferenceAnalysis/main-recoding-a2-reference-mp4-provenance-evidence-20260609.md";
        private const string ReferenceMp4AnalysisResultRelativePath = "Docs/Machine_Spirit/Local/ReferenceAnalysis/when-20260608-204850_where-ref-mp4_who-yyb_what-detailed-mp4-analysis_why-main-recoding-problem-list_how-ffmpeg-24-samples/result.json";
        private const string ReferenceMp4FrameMetricsRelativePath = "Docs/Machine_Spirit/Local/ReferenceAnalysis/when-20260608-204850_where-ref-mp4_who-yyb_what-detailed-mp4-analysis_why-main-recoding-problem-list_how-ffmpeg-24-samples/frame-metrics.json";
        private const string ReferenceMp4ContactSheetRelativePath = "Docs/Machine_Spirit/Local/ReferenceAnalysis/when-20260608-204850_where-ref-mp4_who-yyb_what-detailed-mp4-analysis_why-main-recoding-problem-list_how-ffmpeg-24-samples/contact-sheet.png";
        private const string CandidateScreenshotFramingView = "front";
        private const float CandidateScreenshotBrightLuminanceThreshold = 0.08f;
        private const byte CandidateScreenshotOpaqueAlphaThreshold = 8;
        private const int ImageSpaceSilhouetteProfileBandCount = 4;
        private const int ReferenceAlignedVisualEvidenceMinMatchedSamples = 5;
        private const float ReferenceAlignedVisualEvidenceMaxSecondsGap = 0.1f;
        private const float ReferenceAlignedVisualEvidenceMaxBboxHeightDelta = 0.05f;
        private const float ReferenceAlignedVisualEvidenceMaxBottomGapDelta = 0.02f;
        private const float ReferenceAlignedVisualEvidenceMaxSilhouetteProfileL1Delta = 0.15f;
        private const float ReferenceAlignedVisualEvidenceMaxSilhouetteProfileBandDelta = 0.25f;
        private const float ReferenceAlignedVisualEvidenceMaxSilhouetteLandmarkEndpointDelta = 0.30f;
        private const float ReferenceAlignedVisualEvidenceEndpointPixelTolerance = 0.001f;
        private const int EvidenceSafeMaxFullPathLength = 240;
        private const float DefaultManualAnimatorBodyRotationReferenceWeight = 1f;
        private const float DefaultManualAnimatorFullBodyPoseReferenceWeight = 1f;
        private const float DefaultManualAnimatorHandPalmFrameWeight = 1f;
        private const float DefaultRetargetPoseVisualSpikeCurrentWeight = 0.65f;
        private const float DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset = 0f;
        private const float DefaultManualAnimatorHipsLocalPositionReferenceWeight = 0.25f;
        private const float DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset = 0.04f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceWeight = 0.45f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceMaxOffset = 0.025f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart = 0f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd = 0f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = 0f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceAxisXScale = 1f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceAxisZScale = 1f;
        private static readonly float[] ReferenceMp4ProbeDefaultLocalSampleTimes =
        {
            0f,
            3f,
            6f,
            10f,
            13.2f,
            20f,
            30f,
            60f,
            120f
        };
        private static readonly string[] RuntimeDiagnosticScriptPaths =
        {
            "Assets/Plugins/VMDRecorderSample/SampleScript/MotionComparisonProbe.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/FileManager.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/HumanoidArmDeformationGuard.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/HumanoidArmDirectionRetargetGuard.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/PoseSpaceRetargeter.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/Editor/YybVisualComparisonBatchRunner.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/Editor/YybVisualComparisonRequestWatcher.cs"
        };
        private const float DefaultDurationSeconds = 31f;
        private const float DefaultFrameRate = 30f;
        private const float DefaultStartDelaySeconds = 0.2f;
        private const float DefaultManualAnimatorBipedIkFootPositionReferenceWeight = 0.65f;
        private const float DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset = 0.12f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight = 1f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset = 0.04f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale = 1f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = 1f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart = 0f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = 0f;
        private const float DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = 0.049f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight = 1f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset = 0.025f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale = 1f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = 1f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart = 0f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = 0f;
        private const float DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight = 1f;
        private const float DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = 15f;
        private const float DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight = 1f;
        private const float DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 6.2f;
        private const float DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = 1f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = 0.25f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = 1f;
        private const float DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultYybArmSwingLimitWeight = 0.85f;
        private const float DefaultYybArmSwingMaxDownDot = 0.68f;
        private const float DefaultYybArmSwingMinHandHorizontalRatio = 0.05f;
        private const float DefaultYybArmSwingMaxHandBelowShoulderRatio = 0.75f;
        private const float DefaultYybArmSwingHorizontalReachLimitWeight = 0f;
        private const float DefaultYybArmSwingMaxHandHorizontalReachRatio = 0f;
        private const float DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio = 0f;
        private const float DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply = 0f;
        private const float DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight = 0.2f;
        private const float DefaultYybArmSwingRaisedPoseMinUpperArmDownDot = 0.55f;
        private const float DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio = 0.05f;
        private const float DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio = 0.55f;
        private const float DefaultYybArmDirectionUpperArmWeight = 0.65f;
        private const float DefaultYybArmDirectionForearmWeight = 0.75f;
        private const float DefaultYybArmDirectionUpperArmMaxDegrees = 65f;
        private const float DefaultYybArmDirectionForearmMaxDegrees = 85f;
        private const float DefaultYybArmDirectionLeftSideWeightScale = 1f;
        private const float DefaultYybArmDirectionRightSideWeightScale = 1f;
        private const float DefaultYybArmSleeveAnchorInfluence = 0.825f;
        private const float DefaultYybArmShoulderCapAnchorInfluence = 0f;
        private const float DefaultYybArmSleeveAnchorMaxDegrees = 85f;
        private const float DefaultYybArmVisualUpperArmInfluence = 0.35f;
        private const float DefaultYybArmVisualForearmInfluence = 0.75f;
        private const float DefaultYybArmVisualUpperArmMaxDegrees = 45f;
        private const float DefaultYybArmVisualForearmMaxDegrees = 75f;
        private const float NoMmdIkDeltaGuardLimitOverrideVmd = float.NaN;
        private const int NoMmdIkDeltaGuardRecoveryHoldFrames = -1;
        private const int NoDiagnosticCaptureDimensionOverride = 0;
        private const float NoDiagnosticScreenshotFramingOverride = float.NaN;
        private const double PlayModeEntryTimeoutSeconds = 15d;
        private const string RunnerStateSessionKey = "Member_Han.YybVisualComparison.RunnerStateJson";
        private const string ManualTestPrefabNameToken = "testPrefab";
        private const string ManualYybNameToken = "YYB Hatsune Miku_default_1.0ver";
        private const string ManualTestPrefabLabelSuffix = "testPrefab";
        private const string ManualYybLabelSuffix = "yyb";

        private struct ReferenceMmdTimingPlan
        {
            public bool Enabled;
            public bool HasCandidateTimingOverride;
            public float ReferenceMp4StartSeconds;
            public float CandidateClipStartSeconds;
            public float CandidateClipSecondsPerReferenceSecond;
            public float ReferenceDurationSeconds;
        }

        private enum CaptureMode
        {
            MainAuto,
            MainRecording,
            MainRecordingVmdPlaybackProbe,
            SubManualTestPrefab,
            SubManualYyb
        }

        private sealed class CaptureJob
        {
            public CaptureMode Mode;
            public string ScenePath;
            public string SceneName;
            public string DisplayName;
            public string ManualTargetNameToken;
        }

        private sealed class ManualAnimatorCapturePlan
        {
            public float StartTimeSeconds;
            public float DurationSeconds;
            public int TargetFrameCount;
            public string OutputBaseName;
            public string ComparisonLabel;
        }

        [Serializable]
        private sealed class CaptureResult
        {
            public string jobMode;
            public string jobDisplayName;
            public string sceneName;
            public string comparisonLabel;
            public string targetName;
            public bool success;
            public string error;
            public string vmdPath;
            public int frameCount;
            public long fileSizeBytes;
            public string comparisonSessionManifestPath;
            public string comparisonMetricsCsvPath;
            public string comparisonFrameFolderPath;
            public string comparisonFrameIndexPath;
            public string comparisonSessionId;
            public bool hasFileManagerEffectiveSettings;
            public bool useManualAnimatorFootLocalRotationReference;
            public float manualAnimatorFootLocalRotationReferenceWeight;
            public bool useManualAnimatorFullBodyPoseReference;
            public float manualAnimatorFullBodyPoseReferenceWeight;
            public bool manualAnimatorFullBodyPoseExcludeLowerBodyMuscles;
            public bool manualAnimatorFullBodyPoseLowerBodyMusclesOnly;
            public bool manualAnimatorFullBodyPoseLegTwistMusclesOnly;
            public bool useManualAnimatorBodyRotationReference;
            public float manualAnimatorBodyRotationReferenceWeight;
            public bool useManualAnimatorLowerBodySegmentDirectionReference;
            public float manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            public float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            public bool disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference;
            public float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            public bool disableManualAnimatorLowerLegToFootSegmentDirectionReference;
            public float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            public bool disableManualAnimatorFootToToesSegmentDirectionReference;
            public float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            public bool useManualAnimatorFootHipsAlignedResidualYawReference;
            public float manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            public float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            public bool usePostSetHumanPoseRightEndpointPositionReference;
            public float postSetHumanPoseRightEndpointPositionReferenceWeight;
            public float postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool postSetHumanPoseEndpointPositionUseLeftSide;
            public bool usePreSetHumanPoseRightEndpointPositionReference;
            public float preSetHumanPoseRightEndpointPositionReferenceWeight;
            public float preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool preSetHumanPoseEndpointPositionUseLeftSide;
            public bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            public bool preSetHumanPoseEndpointPositionInvertBodyPositionX;
            public bool preSetHumanPoseEndpointPositionInvertBodyPositionZ;
            public bool usePostSetHumanPoseRightFootEvaluatorXzReference;
            public float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            public bool useManualAnimatorBodyPositionXzReference;
            public float manualAnimatorBodyPositionXzReferenceWeight;
            public float manualAnimatorBodyPositionXzReferenceMaxOffset;
            public float manualAnimatorBodyPositionXzReferenceFrameGateStart;
            public float manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            public float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            public float manualAnimatorBodyPositionXzReferenceAxisXScale;
            public float manualAnimatorBodyPositionXzReferenceAxisZScale;
            public bool enableYybArmSwingLimitCorrection;
            public float yybArmSwingLimitWeight;
            public float yybArmSwingMaxDownDot;
            public float yybArmSwingMinHandHorizontalRatio;
            public float yybArmSwingMaxHandBelowShoulderRatio;
            public float yybArmSwingHorizontalReachLimitWeight;
            public float yybArmSwingMaxHandHorizontalReachRatio;
            public float yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            public float yybArmSwingHorizontalReachMinElbowAngleAfterApply;
            public float yybArmSwingRaisedPoseHorizontalReachLimitWeight;
            public float yybArmSwingRaisedPoseMinUpperArmDownDot;
            public float yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            public float yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            public bool enableYybArmSleeveAnchorCorrection;
            public bool enableYybArmVisualTwistCorrection;
        }

        [Serializable]
        private sealed class PersistedCaptureJob
        {
            public int mode;
            public string scenePath;
            public string sceneName;
            public string displayName;
            public string manualTargetNameToken;
        }

        [Serializable]
        private sealed class PersistedCaptureResult
        {
            public string jobMode;
            public string jobDisplayName;
            public string sceneName;
            public string comparisonLabel;
            public string targetName;
            public bool success;
            public string error;
            public string vmdPath;
            public int frameCount;
            public long fileSizeBytes;
            public string comparisonSessionManifestPath;
            public string comparisonMetricsCsvPath;
            public string comparisonFrameFolderPath;
            public string comparisonFrameIndexPath;
            public string comparisonSessionId;
            public bool hasFileManagerEffectiveSettings;
            public bool useManualAnimatorFootLocalRotationReference;
            public float manualAnimatorFootLocalRotationReferenceWeight;
            public bool useManualAnimatorFullBodyPoseReference;
            public float manualAnimatorFullBodyPoseReferenceWeight;
            public bool manualAnimatorFullBodyPoseExcludeLowerBodyMuscles;
            public bool manualAnimatorFullBodyPoseLowerBodyMusclesOnly;
            public bool manualAnimatorFullBodyPoseLegTwistMusclesOnly;
            public bool useManualAnimatorBodyRotationReference;
            public float manualAnimatorBodyRotationReferenceWeight;
            public bool useManualAnimatorLowerBodySegmentDirectionReference;
            public float manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            public float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            public bool disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference;
            public float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            public bool disableManualAnimatorLowerLegToFootSegmentDirectionReference;
            public float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            public bool disableManualAnimatorFootToToesSegmentDirectionReference;
            public float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            public bool useManualAnimatorFootHipsAlignedResidualYawReference;
            public float manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            public float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            public bool usePostSetHumanPoseRightEndpointPositionReference;
            public float postSetHumanPoseRightEndpointPositionReferenceWeight;
            public float postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool postSetHumanPoseEndpointPositionUseLeftSide;
            public bool usePreSetHumanPoseRightEndpointPositionReference;
            public float preSetHumanPoseRightEndpointPositionReferenceWeight;
            public float preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool preSetHumanPoseEndpointPositionUseLeftSide;
            public bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            public bool preSetHumanPoseEndpointPositionInvertBodyPositionX;
            public bool preSetHumanPoseEndpointPositionInvertBodyPositionZ;
            public bool usePostSetHumanPoseRightFootEvaluatorXzReference;
            public float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            public bool useManualAnimatorBodyPositionXzReference;
            public float manualAnimatorBodyPositionXzReferenceWeight;
            public float manualAnimatorBodyPositionXzReferenceMaxOffset;
            public float manualAnimatorBodyPositionXzReferenceFrameGateStart;
            public float manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            public float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            public float manualAnimatorBodyPositionXzReferenceAxisXScale;
            public float manualAnimatorBodyPositionXzReferenceAxisZScale;
            public bool enableYybArmSwingLimitCorrection;
            public float yybArmSwingLimitWeight;
            public float yybArmSwingMaxDownDot;
            public float yybArmSwingMinHandHorizontalRatio;
            public float yybArmSwingMaxHandBelowShoulderRatio;
            public float yybArmSwingHorizontalReachLimitWeight;
            public float yybArmSwingMaxHandHorizontalReachRatio;
            public float yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            public float yybArmSwingHorizontalReachMinElbowAngleAfterApply;
            public float yybArmSwingRaisedPoseHorizontalReachLimitWeight;
            public float yybArmSwingRaisedPoseMinUpperArmDownDot;
            public float yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            public float yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            public bool enableYybArmSleeveAnchorCorrection;
            public bool enableYybArmVisualTwistCorrection;
        }

        [Serializable]
        private sealed class PersistedState
        {
            public string fbxFileName;
            public float durationSeconds;
            public int targetFrameCount;
            public bool enableFingerCloseups;
            public bool enableRecorderParentFrameIkOffsetsWhenCenterParented;
            public float mmdIkDeltaGuardLimitOverrideVmd;
            public float mmdIkDeltaGuardRecoveryTriggerVmd;
            public float mmdIkDeltaGuardRecoveryDebtThresholdVmd;
            public int mmdIkDeltaGuardRecoveryHoldFrames;
            public bool enableFinalIkFootGroundingRuntimeOverride;
            public bool enableManualAnimatorFootLocalRotationRuntimeOverride;
            public bool disableManualAnimatorFootLocalRotationRuntimeOverride;
            public bool enableManualAnimatorFullBodyPoseRuntimeOverride;
            public bool disableManualAnimatorFullBodyPoseRuntimeOverride;
            public float manualAnimatorFullBodyPoseReferenceWeight;
            public bool manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride;
            public bool manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride;
            public bool manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride;
            public bool enableManualAnimatorBodyRotationRuntimeOverride;
            public bool disableManualAnimatorBodyRotationRuntimeOverride;
            public float manualAnimatorBodyRotationReferenceWeight;
            public bool enableManualAnimatorHandLocalRotationRuntimeOverride;
            public bool enableManualAnimatorThumbLocalRotationRuntimeOverride;
            public bool enableManualAnimatorHandPalmFrameRuntimeOverride;
            public float manualAnimatorHandPalmFrameWeight;
            public bool overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings;
            public bool enableRetargetPoseVisualSpikeSmoothingRuntimeOverride;
            public float retargetPoseVisualSpikeCurrentWeight;
            public float retargetPoseVisualSpikeForearmStretchClampMaxOffset;
            public bool enableYybArmSwingLimitRuntimeOverride;
            public float yybArmSwingLimitWeight;
            public float yybArmSwingMaxDownDot;
            public float yybArmSwingMinHandHorizontalRatio;
            public float yybArmSwingMaxHandBelowShoulderRatio;
            public float yybArmSwingHorizontalReachLimitWeight;
            public float yybArmSwingMaxHandHorizontalReachRatio;
            public float yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            public float yybArmSwingHorizontalReachMinElbowAngleAfterApply;
            public float yybArmSwingRaisedPoseHorizontalReachLimitWeight;
            public float yybArmSwingRaisedPoseMinUpperArmDownDot;
            public float yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            public float yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            public bool enableYybArmDirectionRetargetRuntimeOverride;
            public float yybArmDirectionUpperArmWeight;
            public float yybArmDirectionForearmWeight;
            public float yybArmDirectionUpperArmMaxDegrees;
            public float yybArmDirectionForearmMaxDegrees;
            public float yybArmDirectionLeftSideWeightScale;
            public float yybArmDirectionRightSideWeightScale;
            public bool overrideYybArmSleeveAnchorRuntimeSettings;
            public bool enableYybArmSleeveAnchorRuntimeOverride;
            public float yybArmSleeveAnchorInfluence;
            public float yybArmShoulderCapAnchorInfluence;
            public float yybArmSleeveAnchorMaxDegrees;
            public bool overrideYybArmVisualTwistRuntimeSettings;
            public bool enableYybArmVisualTwistRuntimeOverride;
            public float yybArmVisualUpperArmInfluence;
            public float yybArmVisualForearmInfluence;
            public float yybArmVisualUpperArmMaxDegrees;
            public float yybArmVisualForearmMaxDegrees;
            public bool enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            public bool enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            public bool disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            public bool disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            public bool disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            public bool disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            public bool disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            public bool enablePostSetHumanPoseRightEndpointPositionRuntimeOverride;
            public bool enablePreSetHumanPoseRightEndpointPositionRuntimeOverride;
            public bool enableManualAnimatorBipedIkFootPositionRuntimeOverride;
            public bool enableManualAnimatorHipsLocalPositionRuntimeOverride;
            public bool enableManualAnimatorBodyPositionXzRuntimeOverride;
            public bool enableRetargetBodyPositionXzRootMotionRuntimeOverride;
            public bool disableTargetHumanoidBonePositionLockRuntimeOverride;
            public float manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            public float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            public float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            public float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            public float manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            public float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            public float postSetHumanPoseRightEndpointPositionReferenceWeight;
            public float postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool postSetHumanPoseEndpointPositionUseLeftSide;
            public float preSetHumanPoseRightEndpointPositionReferenceWeight;
            public float preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool preSetHumanPoseEndpointPositionUseLeftSide;
            public bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            public bool preSetHumanPoseEndpointPositionInvertBodyPositionX;
            public bool preSetHumanPoseEndpointPositionInvertBodyPositionZ;
            public bool usePostSetHumanPoseRightFootEvaluatorXzReference;
            public float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            public float manualAnimatorBipedIkFootPositionReferenceWeight;
            public float manualAnimatorBipedIkFootPositionReferenceMaxOffset;
            public float manualAnimatorHipsLocalPositionReferenceWeight;
            public float manualAnimatorHipsLocalPositionReferenceMaxOffset;
            public float manualAnimatorBodyPositionXzReferenceWeight;
            public float manualAnimatorBodyPositionXzReferenceMaxOffset;
            public float manualAnimatorBodyPositionXzReferenceFrameGateStart;
            public float manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            public float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            public float manualAnimatorBodyPositionXzReferenceAxisXScale;
            public float manualAnimatorBodyPositionXzReferenceAxisZScale;
            public bool enableVmdPlaybackProbeRuntimeOverride;
            public bool applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            public string vmdPlaybackProbeSourceVmdPath;
            public string editorDiagnosticSmokeSegment;
            public bool enableReferenceMmdTimingRuntimeOverride;
            public int diagnosticCaptureWidthOverride;
            public int diagnosticCaptureHeightOverride;
            public float diagnosticScreenshotPaddingOverride;
            public float diagnosticScreenshotVerticalViewportCenterOverride;
            public bool isRunning;
            public bool activeJobFinished;
            public bool advanceAfterPlayStopPending;
            public bool playModeEntryPending;
            public string summarySessionId;
            public string summaryDirectory;
            public string projectRoot;
            public PersistedCaptureJob activeJob;
            public PersistedCaptureJob[] pendingJobs;
            public PersistedCaptureResult[] results;
            public string[] failures;
        }

        private static readonly Queue<CaptureJob> PendingJobs = new Queue<CaptureJob>();
        private static readonly List<CaptureResult> Results = new List<CaptureResult>();
        private static readonly List<string> Failures = new List<string>();

        private static CaptureJob _activeJob;
        private static FileManager _activeFileManager;
        private static HumanoidSampleCode _activeRecorder;
        private static AnimationClip _referenceClip;
        private static string _referenceClipAssetPath = string.Empty;
        private static RuntimeAnimatorController _fallbackController;
        private static string _fbxFileName = DefaultFbxFileName;
        private static float _durationSeconds = DefaultDurationSeconds;
        private static int _targetFrameCount = Mathf.CeilToInt(DefaultDurationSeconds * DefaultFrameRate);
        private static bool _enableFingerCloseups;
        private static bool _enableRecorderParentFrameIkOffsetsWhenCenterParented = true;
        private static float _mmdIkDeltaGuardLimitOverrideVmd = NoMmdIkDeltaGuardLimitOverrideVmd;
        private static float _mmdIkDeltaGuardRecoveryTriggerVmd = NoMmdIkDeltaGuardLimitOverrideVmd;
        private static float _mmdIkDeltaGuardRecoveryDebtThresholdVmd = NoMmdIkDeltaGuardLimitOverrideVmd;
        private static int _mmdIkDeltaGuardRecoveryHoldFrames = NoMmdIkDeltaGuardRecoveryHoldFrames;
        private static bool _enableFinalIkFootGroundingRuntimeOverride;
        private static bool _enableManualAnimatorFootLocalRotationRuntimeOverride;
        private static bool _disableManualAnimatorFootLocalRotationRuntimeOverride;
        private static bool _enableManualAnimatorFullBodyPoseRuntimeOverride;
        private static bool _disableManualAnimatorFullBodyPoseRuntimeOverride;
        private static float _manualAnimatorFullBodyPoseReferenceWeight =
            DefaultManualAnimatorFullBodyPoseReferenceWeight;
        private static bool _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride;
        private static bool _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride;
        private static bool _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride;
        private static bool _enableManualAnimatorBodyRotationRuntimeOverride;
        private static bool _disableManualAnimatorBodyRotationRuntimeOverride;
        private static float _manualAnimatorBodyRotationReferenceWeight =
            DefaultManualAnimatorBodyRotationReferenceWeight;
        private static bool _enableManualAnimatorHandLocalRotationRuntimeOverride;
        private static bool _enableManualAnimatorThumbLocalRotationRuntimeOverride;
        private static bool _enableManualAnimatorHandPalmFrameRuntimeOverride;
        private static float _manualAnimatorHandPalmFrameWeight = DefaultManualAnimatorHandPalmFrameWeight;
        private static bool _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings;
        private static bool _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = true;
        private static float _retargetPoseVisualSpikeCurrentWeight = DefaultRetargetPoseVisualSpikeCurrentWeight;
        private static float _retargetPoseVisualSpikeForearmStretchClampMaxOffset =
            DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset;
        private static bool _enableYybArmSwingLimitRuntimeOverride;
        private static float _yybArmSwingLimitWeight = DefaultYybArmSwingLimitWeight;
        private static float _yybArmSwingMaxDownDot = DefaultYybArmSwingMaxDownDot;
        private static float _yybArmSwingMinHandHorizontalRatio = DefaultYybArmSwingMinHandHorizontalRatio;
        private static float _yybArmSwingMaxHandBelowShoulderRatio =
            DefaultYybArmSwingMaxHandBelowShoulderRatio;
        private static float _yybArmSwingHorizontalReachLimitWeight =
            DefaultYybArmSwingHorizontalReachLimitWeight;
        private static float _yybArmSwingMaxHandHorizontalReachRatio =
            DefaultYybArmSwingMaxHandHorizontalReachRatio;
        private static float _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
            DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
        private static float _yybArmSwingHorizontalReachMinElbowAngleAfterApply =
            DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply;
        private static float _yybArmSwingRaisedPoseHorizontalReachLimitWeight =
            DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight;
        private static float _yybArmSwingRaisedPoseMinUpperArmDownDot =
            DefaultYybArmSwingRaisedPoseMinUpperArmDownDot;
        private static float _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
            DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
        private static float _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
            DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
        private static bool _enableYybArmDirectionRetargetRuntimeOverride;
        private static float _yybArmDirectionUpperArmWeight = DefaultYybArmDirectionUpperArmWeight;
        private static float _yybArmDirectionForearmWeight = DefaultYybArmDirectionForearmWeight;
        private static float _yybArmDirectionUpperArmMaxDegrees = DefaultYybArmDirectionUpperArmMaxDegrees;
        private static float _yybArmDirectionForearmMaxDegrees = DefaultYybArmDirectionForearmMaxDegrees;
        private static float _yybArmDirectionLeftSideWeightScale = DefaultYybArmDirectionLeftSideWeightScale;
        private static float _yybArmDirectionRightSideWeightScale = DefaultYybArmDirectionRightSideWeightScale;
        private static bool _overrideYybArmSleeveAnchorRuntimeSettings;
        private static bool _enableYybArmSleeveAnchorRuntimeOverride = true;
        private static float _yybArmSleeveAnchorInfluence = DefaultYybArmSleeveAnchorInfluence;
        private static float _yybArmShoulderCapAnchorInfluence = DefaultYybArmShoulderCapAnchorInfluence;
        private static float _yybArmSleeveAnchorMaxDegrees = DefaultYybArmSleeveAnchorMaxDegrees;
        private static bool _overrideYybArmVisualTwistRuntimeSettings;
        private static bool _enableYybArmVisualTwistRuntimeOverride = true;
        private static float _yybArmVisualUpperArmInfluence = DefaultYybArmVisualUpperArmInfluence;
        private static float _yybArmVisualForearmInfluence = DefaultYybArmVisualForearmInfluence;
        private static float _yybArmVisualUpperArmMaxDegrees = DefaultYybArmVisualUpperArmMaxDegrees;
        private static float _yybArmVisualForearmMaxDegrees = DefaultYybArmVisualForearmMaxDegrees;
        private static bool _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
        private static bool _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
        private static bool _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
        private static bool _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
        private static bool _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
        private static bool _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
        private static bool _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
        private static bool _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride;
        private static bool _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride;
        private static bool _enableManualAnimatorBipedIkFootPositionRuntimeOverride;
        private static bool _enableManualAnimatorHipsLocalPositionRuntimeOverride;
        private static bool _enableManualAnimatorBodyPositionXzRuntimeOverride;
        private static bool _enableRetargetBodyPositionXzRootMotionRuntimeOverride;
        private static bool _disableTargetHumanoidBonePositionLockRuntimeOverride;
        private static float _manualAnimatorLowerBodySegmentDirectionReferenceWeight =
            DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight;
        private static float _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
            DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
        private static float _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
            DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
        private static float _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
            DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
        private static float _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
            DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
        private static float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
        private static float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
        private static float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
        private static float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
        private static float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
        private static float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
        private static float _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
            DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
        private static float _manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
            DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight;
        private static float _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
            DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
        private static float _postSetHumanPoseRightEndpointPositionReferenceWeight =
            DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight;
        private static float _postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
            DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset;
        private static float _postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
            DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale;
        private static float _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
            DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
        private static float _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
            DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
        private static float _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
            DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
        private static bool _postSetHumanPoseEndpointPositionUseLeftSide;
        private static float _preSetHumanPoseRightEndpointPositionReferenceWeight =
            DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight;
        private static float _preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
            DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset;
        private static float _preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
            DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale;
        private static float _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
            DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
        private static float _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
            DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
        private static float _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
            DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
        private static bool _preSetHumanPoseEndpointPositionUseLeftSide;
        private static bool _preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
        private static bool _preSetHumanPoseEndpointPositionInvertBodyPositionX;
        private static bool _preSetHumanPoseEndpointPositionInvertBodyPositionZ;
        private static bool _usePostSetHumanPoseRightFootEvaluatorXzReference;
        private static float _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
            DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
        private static float _manualAnimatorBipedIkFootPositionReferenceWeight =
            DefaultManualAnimatorBipedIkFootPositionReferenceWeight;
        private static float _manualAnimatorBipedIkFootPositionReferenceMaxOffset =
            DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset;
        private static float _manualAnimatorHipsLocalPositionReferenceWeight =
            DefaultManualAnimatorHipsLocalPositionReferenceWeight;
        private static float _manualAnimatorHipsLocalPositionReferenceMaxOffset =
            DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset;
        private static float _manualAnimatorBodyPositionXzReferenceWeight =
            DefaultManualAnimatorBodyPositionXzReferenceWeight;
        private static float _manualAnimatorBodyPositionXzReferenceMaxOffset =
            DefaultManualAnimatorBodyPositionXzReferenceMaxOffset;
        private static float _manualAnimatorBodyPositionXzReferenceFrameGateStart =
            DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart;
        private static float _manualAnimatorBodyPositionXzReferenceFrameGateEnd =
            DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd;
        private static float _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
            DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
        private static float _manualAnimatorBodyPositionXzReferenceAxisXScale =
            DefaultManualAnimatorBodyPositionXzReferenceAxisXScale;
        private static float _manualAnimatorBodyPositionXzReferenceAxisZScale =
            DefaultManualAnimatorBodyPositionXzReferenceAxisZScale;
        private static bool _enableVmdPlaybackProbeRuntimeOverride;
        private static bool _applyVmdPlaybackProbeIkTargetsRuntimeOverride;
        private static string _vmdPlaybackProbeSourceVmdPath = string.Empty;
        private static bool _enableReferenceMmdTimingRuntimeOverride;
        private static FileManager.EditorDiagnosticSmokeSegment _editorDiagnosticSmokeSegment =
            FileManager.EditorDiagnosticSmokeSegment.Head;
        private static int _diagnosticCaptureWidthOverride = NoDiagnosticCaptureDimensionOverride;
        private static int _diagnosticCaptureHeightOverride = NoDiagnosticCaptureDimensionOverride;
        private static float _diagnosticScreenshotPaddingOverride = NoDiagnosticScreenshotFramingOverride;
        private static float _diagnosticScreenshotVerticalViewportCenterOverride = NoDiagnosticScreenshotFramingOverride;
        private static bool _isRunning;
        private static bool _activeJobFinished;
        private static bool _activeJobStartedInPlayMode;
        private static bool _advanceAfterPlayStopPending;
        private static bool _playModeEntryPending;
        private static double _playModeEntryRequestedAt;
        private static string _summarySessionId = string.Empty;
        private static string _summaryDirectory = string.Empty;
        private static string _projectRoot = string.Empty;
        private static bool _enterPlayModeOptionsCaptured;
        private static bool _previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions _previousEnterPlayModeOptions;

        public sealed class RunCompletionInfo
        {
            public bool passed;
            public string sessionId;
            public string summaryJsonPath;
            public string summaryMarkdownPath;
            public string latestSummaryJsonPath;
            public string latestSummaryMarkdownPath;
            public string[] failures;
            public int totalJobs;
            public int successJobs;
        }

        public static bool IsRunning => _isRunning;
        public static bool HasPersistedRunState()
        {
            string json = SessionState.GetString(RunnerStateSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            PersistedState state = JsonUtility.FromJson<PersistedState>(json);
            if (state == null || !state.isRunning)
            {
                ClearPersistedState();
                return false;
            }

            return true;
        }

        public static bool TryResumePersistedRun()
        {
            if (_isRunning)
            {
                return true;
            }

            if (!HasPersistedRunState())
            {
                return false;
            }

            TryResumeRunAfterDomainReload();
            return _isRunning || HasPersistedRunState();
        }

        public static event Action<RunCompletionInfo> RunCompleted;

        static YybVisualComparisonBatchRunner()
        {
            EditorApplication.delayCall += TryResumeRunAfterDomainReload;
        }

        [MenuItem(MenuRoot + "Run satisfaction_2 testPrefab vs Main_Auto", false, 2130)]
        private static void RunDefaultMenu()
        {
            StartRun(
                DefaultFbxFileName,
                DefaultDurationSeconds,
                enableFingerCloseups: false,
                enableRecorderParentFrameIkOffsetsWhenCenterParented: true,
                mmdIkDeltaGuardLimitOverrideVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryHoldFrames: NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride: false);
        }

        [MenuItem(MenuRoot + "Clear Stale Run State", false, 2139)]
        private static void ClearStaleRunStateMenu()
        {
            ClearStaleRunState("menu");
        }

        public static void ClearStaleRunState(string reason)
        {
            HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
            RestoreEnterPlayModeOptions();
            CleanupActiveSubscriptions();
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update -= TryAdvanceAfterPlayStop;
            PendingJobs.Clear();
            Results.Clear();
            Failures.Clear();
            _activeJob = null;
            _activeFileManager = null;
            _activeRecorder = null;
            _activeJobFinished = false;
            _advanceAfterPlayStopPending = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;
            _activeJobStartedInPlayMode = false;
            _mmdIkDeltaGuardLimitOverrideVmd = NoMmdIkDeltaGuardLimitOverrideVmd;
            _mmdIkDeltaGuardRecoveryTriggerVmd = NoMmdIkDeltaGuardLimitOverrideVmd;
            _mmdIkDeltaGuardRecoveryDebtThresholdVmd = NoMmdIkDeltaGuardLimitOverrideVmd;
            _mmdIkDeltaGuardRecoveryHoldFrames = NoMmdIkDeltaGuardRecoveryHoldFrames;
            _enableFinalIkFootGroundingRuntimeOverride = false;
            _enableManualAnimatorFootLocalRotationRuntimeOverride = false;
            _disableManualAnimatorFootLocalRotationRuntimeOverride = false;
            _enableManualAnimatorFullBodyPoseRuntimeOverride = false;
            _disableManualAnimatorFullBodyPoseRuntimeOverride = false;
            _manualAnimatorFullBodyPoseReferenceWeight = DefaultManualAnimatorFullBodyPoseReferenceWeight;
            _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride = false;
            _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride = false;
            _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride = false;
            _enableManualAnimatorBodyRotationRuntimeOverride = false;
            _disableManualAnimatorBodyRotationRuntimeOverride = false;
            _manualAnimatorBodyRotationReferenceWeight = DefaultManualAnimatorBodyRotationReferenceWeight;
            _enableManualAnimatorHandLocalRotationRuntimeOverride = false;
            _enableManualAnimatorThumbLocalRotationRuntimeOverride = false;
            _enableManualAnimatorHandPalmFrameRuntimeOverride = false;
            _manualAnimatorHandPalmFrameWeight = DefaultManualAnimatorHandPalmFrameWeight;
            _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings = false;
            _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = true;
            _retargetPoseVisualSpikeCurrentWeight = DefaultRetargetPoseVisualSpikeCurrentWeight;
            _retargetPoseVisualSpikeForearmStretchClampMaxOffset =
                DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset;
            _enableYybArmSwingLimitRuntimeOverride = false;
            _yybArmSwingLimitWeight = DefaultYybArmSwingLimitWeight;
            _yybArmSwingMaxDownDot = DefaultYybArmSwingMaxDownDot;
            _yybArmSwingMinHandHorizontalRatio = DefaultYybArmSwingMinHandHorizontalRatio;
            _yybArmSwingMaxHandBelowShoulderRatio = DefaultYybArmSwingMaxHandBelowShoulderRatio;
            _yybArmSwingHorizontalReachLimitWeight = DefaultYybArmSwingHorizontalReachLimitWeight;
            _yybArmSwingMaxHandHorizontalReachRatio = DefaultYybArmSwingMaxHandHorizontalReachRatio;
            _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            _yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply;
            _yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight;
            _yybArmSwingRaisedPoseMinUpperArmDownDot = DefaultYybArmSwingRaisedPoseMinUpperArmDownDot;
            _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            _enableYybArmDirectionRetargetRuntimeOverride = false;
            _yybArmDirectionUpperArmWeight = DefaultYybArmDirectionUpperArmWeight;
            _yybArmDirectionForearmWeight = DefaultYybArmDirectionForearmWeight;
            _yybArmDirectionUpperArmMaxDegrees = DefaultYybArmDirectionUpperArmMaxDegrees;
            _yybArmDirectionForearmMaxDegrees = DefaultYybArmDirectionForearmMaxDegrees;
            _yybArmDirectionLeftSideWeightScale = DefaultYybArmDirectionLeftSideWeightScale;
            _yybArmDirectionRightSideWeightScale = DefaultYybArmDirectionRightSideWeightScale;
            _overrideYybArmSleeveAnchorRuntimeSettings = false;
            _enableYybArmSleeveAnchorRuntimeOverride = true;
            _yybArmSleeveAnchorInfluence = DefaultYybArmSleeveAnchorInfluence;
            _yybArmShoulderCapAnchorInfluence = DefaultYybArmShoulderCapAnchorInfluence;
            _yybArmSleeveAnchorMaxDegrees = DefaultYybArmSleeveAnchorMaxDegrees;
            _overrideYybArmVisualTwistRuntimeSettings = false;
            _enableYybArmVisualTwistRuntimeOverride = true;
            _yybArmVisualUpperArmInfluence = DefaultYybArmVisualUpperArmInfluence;
            _yybArmVisualForearmInfluence = DefaultYybArmVisualForearmInfluence;
            _yybArmVisualUpperArmMaxDegrees = DefaultYybArmVisualUpperArmMaxDegrees;
            _yybArmVisualForearmMaxDegrees = DefaultYybArmVisualForearmMaxDegrees;
            _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false;
            _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false;
            _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false;
            _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false;
            _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride = false;
            _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride = false;
            _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride = false;
            _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride = false;
            _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride = false;
            _enableManualAnimatorBipedIkFootPositionRuntimeOverride = false;
            _enableManualAnimatorHipsLocalPositionRuntimeOverride = false;
            _enableManualAnimatorBodyPositionXzRuntimeOverride = false;
            _enableRetargetBodyPositionXzRootMotionRuntimeOverride = false;
            _disableTargetHumanoidBonePositionLockRuntimeOverride = false;
            _enableVmdPlaybackProbeRuntimeOverride = false;
            _applyVmdPlaybackProbeIkTargetsRuntimeOverride = false;
            _vmdPlaybackProbeSourceVmdPath = string.Empty;
            _enableReferenceMmdTimingRuntimeOverride = false;
            _editorDiagnosticSmokeSegment = FileManager.EditorDiagnosticSmokeSegment.Head;
            _manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight;
            _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            _manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            _postSetHumanPoseRightEndpointPositionReferenceWeight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight;
            _postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            _postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            _postSetHumanPoseEndpointPositionUseLeftSide = false;
            _preSetHumanPoseRightEndpointPositionReferenceWeight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight;
            _preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            _preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            _preSetHumanPoseEndpointPositionUseLeftSide = false;
            _preSetHumanPoseEndpointPositionUseGhostCurrentBasis = false;
            _preSetHumanPoseEndpointPositionInvertBodyPositionX = false;
            _preSetHumanPoseEndpointPositionInvertBodyPositionZ = false;
            _usePostSetHumanPoseRightFootEvaluatorXzReference = false;
            _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            _manualAnimatorBipedIkFootPositionReferenceWeight =
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight;
            _manualAnimatorBipedIkFootPositionReferenceMaxOffset =
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset;
            _manualAnimatorHipsLocalPositionReferenceWeight =
                DefaultManualAnimatorHipsLocalPositionReferenceWeight;
            _manualAnimatorHipsLocalPositionReferenceMaxOffset =
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset;
            _manualAnimatorBodyPositionXzReferenceWeight =
                DefaultManualAnimatorBodyPositionXzReferenceWeight;
            _manualAnimatorBodyPositionXzReferenceMaxOffset =
                DefaultManualAnimatorBodyPositionXzReferenceMaxOffset;
            _manualAnimatorBodyPositionXzReferenceFrameGateStart =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart;
            _manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd;
            _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            _manualAnimatorBodyPositionXzReferenceAxisXScale =
                DefaultManualAnimatorBodyPositionXzReferenceAxisXScale;
            _manualAnimatorBodyPositionXzReferenceAxisZScale =
                DefaultManualAnimatorBodyPositionXzReferenceAxisZScale;
            _isRunning = false;
            ClearPersistedState();
            AppendRunnerTrace($"stale run state cleared reason={reason}");
        }

        public static void RunBatch()
        {
            string fbxFileName = GetCommandLineValue("-yybCompareFbx", DefaultFbxFileName);
            float durationSeconds = GetCommandLineFloat("-yybCompareDuration", DefaultDurationSeconds);
            bool enableFingerCloseups = GetCommandLineBool("-yybCompareFingerCloseups", false);
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented =
                GetCommandLineBool("-yybCompareRecorderParentFrameIkOffsetsWhenCenterParented", true);
            float mmdIkDeltaGuardLimitOverrideVmd =
                GetCommandLineFloat("-yybCompareMmdIkDeltaGuardLimitVmd", NoMmdIkDeltaGuardLimitOverrideVmd);
            float mmdIkDeltaGuardRecoveryTriggerVmd =
                GetCommandLineFloat("-yybCompareMmdIkDeltaGuardRecoveryTriggerVmd", NoMmdIkDeltaGuardLimitOverrideVmd);
            float mmdIkDeltaGuardRecoveryDebtThresholdVmd =
                GetCommandLineFloat("-yybCompareMmdIkDeltaGuardRecoveryDebtVmd", NoMmdIkDeltaGuardLimitOverrideVmd);
            int mmdIkDeltaGuardRecoveryHoldFrames =
                GetCommandLineInt("-yybCompareMmdIkDeltaGuardRecoveryHoldFrames", NoMmdIkDeltaGuardRecoveryHoldFrames);
            bool enableFinalIkFootGroundingRuntimeOverride =
                GetCommandLineBool("-yybCompareFinalIkFootGroundingEnabled", false);
            bool enableManualAnimatorFootLocalRotationRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFootLocalRotationEnabled", false);
            bool disableManualAnimatorFootLocalRotationRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFootLocalRotationDisabled", false);
            bool enableManualAnimatorFullBodyPoseRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseEnabled", false);
            bool disableManualAnimatorFullBodyPoseRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseDisabled", false);
            float manualAnimatorFullBodyPoseReferenceWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorFullBodyPoseWeight",
                DefaultManualAnimatorFullBodyPoseReferenceWeight);
            bool manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseExcludeLowerBodyMuscles", false);
            bool manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseLowerBodyMusclesOnly", false);
            bool manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseLegTwistMusclesOnly", false);
            bool enableManualAnimatorBodyRotationRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorBodyRotationEnabled", false);
            bool disableManualAnimatorBodyRotationRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorBodyRotationDisabled", false);
            float manualAnimatorBodyRotationReferenceWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyRotationWeight",
                DefaultManualAnimatorBodyRotationReferenceWeight);
            bool enableManualAnimatorHandLocalRotationRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorHandLocalRotationEnabled", false);
            bool enableManualAnimatorThumbLocalRotationRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorThumbLocalRotationEnabled", false);
            bool enableManualAnimatorHandPalmFrameRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorHandPalmFrameEnabled", false);
            float manualAnimatorHandPalmFrameWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorHandPalmFrameWeight",
                DefaultManualAnimatorHandPalmFrameWeight);
            bool overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings =
                GetCommandLineBool("-yybCompareRetargetPoseVisualSpikeSmoothingOverride", false);
            bool enableRetargetPoseVisualSpikeSmoothingRuntimeOverride =
                GetCommandLineBool("-yybCompareRetargetPoseVisualSpikeSmoothingEnabled", true);
            float retargetPoseVisualSpikeCurrentWeight = GetCommandLineFloat(
                "-yybCompareRetargetPoseVisualSpikeCurrentWeight",
                DefaultRetargetPoseVisualSpikeCurrentWeight);
            float retargetPoseVisualSpikeForearmStretchClampMaxOffset = GetCommandLineFloat(
                "-yybCompareRetargetPoseVisualSpikeForearmStretchClampMaxOffset",
                DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset);
            bool enableYybArmSwingLimitRuntimeOverride =
                GetCommandLineBool("-yybCompareYybArmSwingLimitEnabled", false);
            float yybArmSwingLimitWeight = GetCommandLineFloat(
                "-yybCompareYybArmSwingLimitWeight",
                DefaultYybArmSwingLimitWeight);
            float yybArmSwingMaxDownDot = GetCommandLineFloat(
                "-yybCompareYybArmSwingMaxDownDot",
                DefaultYybArmSwingMaxDownDot);
            float yybArmSwingMinHandHorizontalRatio = GetCommandLineFloat(
                "-yybCompareYybArmSwingMinHandHorizontalRatio",
                DefaultYybArmSwingMinHandHorizontalRatio);
            float yybArmSwingMaxHandBelowShoulderRatio = GetCommandLineFloat(
                "-yybCompareYybArmSwingMaxHandBelowShoulderRatio",
                DefaultYybArmSwingMaxHandBelowShoulderRatio);
            float yybArmSwingHorizontalReachLimitWeight = GetCommandLineFloat(
                "-yybCompareYybArmSwingHorizontalReachLimitWeight",
                DefaultYybArmSwingHorizontalReachLimitWeight);
            float yybArmSwingMaxHandHorizontalReachRatio = GetCommandLineFloat(
                "-yybCompareYybArmSwingMaxHandHorizontalReachRatio",
                DefaultYybArmSwingMaxHandHorizontalReachRatio);
            float yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = GetCommandLineFloat(
                "-yybCompareYybArmSwingHorizontalReachMaxHandBelowShoulderRatio",
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio);
            float yybArmSwingHorizontalReachMinElbowAngleAfterApply = GetCommandLineFloat(
                "-yybCompareYybArmSwingHorizontalReachMinElbowAngleAfterApply",
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply);
            float yybArmSwingRaisedPoseHorizontalReachLimitWeight = GetCommandLineFloat(
                "-yybCompareYybArmSwingRaisedPoseHorizontalReachLimitWeight",
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight);
            float yybArmSwingRaisedPoseMinUpperArmDownDot = GetCommandLineFloat(
                "-yybCompareYybArmSwingRaisedPoseMinUpperArmDownDot",
                DefaultYybArmSwingRaisedPoseMinUpperArmDownDot);
            float yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = GetCommandLineFloat(
                "-yybCompareYybArmSwingRaisedPoseMaxHandBelowShoulderRatio",
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio);
            float yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = GetCommandLineFloat(
                "-yybCompareYybArmSwingRaisedPoseMaxHandHorizontalReachRatio",
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
            bool enableYybArmDirectionRetargetRuntimeOverride =
                GetCommandLineBool("-yybCompareYybArmDirectionRetargetEnabled", false);
            float yybArmDirectionUpperArmWeight = GetCommandLineFloat(
                "-yybCompareYybArmDirectionUpperArmWeight",
                DefaultYybArmDirectionUpperArmWeight);
            float yybArmDirectionForearmWeight = GetCommandLineFloat(
                "-yybCompareYybArmDirectionForearmWeight",
                DefaultYybArmDirectionForearmWeight);
            float yybArmDirectionUpperArmMaxDegrees = GetCommandLineFloat(
                "-yybCompareYybArmDirectionUpperArmMaxDegrees",
                DefaultYybArmDirectionUpperArmMaxDegrees);
            float yybArmDirectionForearmMaxDegrees = GetCommandLineFloat(
                "-yybCompareYybArmDirectionForearmMaxDegrees",
                DefaultYybArmDirectionForearmMaxDegrees);
            float yybArmDirectionLeftSideWeightScale = GetCommandLineFloat(
                "-yybCompareYybArmDirectionLeftSideWeightScale",
                DefaultYybArmDirectionLeftSideWeightScale);
            float yybArmDirectionRightSideWeightScale = GetCommandLineFloat(
                "-yybCompareYybArmDirectionRightSideWeightScale",
                DefaultYybArmDirectionRightSideWeightScale);
            bool overrideYybArmSleeveAnchorRuntimeSettings =
                GetCommandLineBool("-yybCompareYybArmSleeveAnchorOverride", false);
            bool enableYybArmSleeveAnchorRuntimeOverride =
                GetCommandLineBool("-yybCompareYybArmSleeveAnchorEnabled", true);
            float yybArmSleeveAnchorInfluence = GetCommandLineFloat(
                "-yybCompareYybArmSleeveAnchorInfluence",
                DefaultYybArmSleeveAnchorInfluence);
            float yybArmShoulderCapAnchorInfluence = GetCommandLineFloat(
                "-yybCompareYybArmShoulderCapAnchorInfluence",
                DefaultYybArmShoulderCapAnchorInfluence);
            float yybArmSleeveAnchorMaxDegrees = GetCommandLineFloat(
                "-yybCompareYybArmSleeveAnchorMaxDegrees",
                DefaultYybArmSleeveAnchorMaxDegrees);
            bool overrideYybArmVisualTwistRuntimeSettings =
                GetCommandLineBool("-yybCompareYybArmVisualTwistOverride", false);
            bool enableYybArmVisualTwistRuntimeOverride =
                GetCommandLineBool("-yybCompareYybArmVisualTwistEnabled", true);
            float yybArmVisualUpperArmInfluence = GetCommandLineFloat(
                "-yybCompareYybArmVisualUpperArmInfluence",
                DefaultYybArmVisualUpperArmInfluence);
            float yybArmVisualForearmInfluence = GetCommandLineFloat(
                "-yybCompareYybArmVisualForearmInfluence",
                DefaultYybArmVisualForearmInfluence);
            float yybArmVisualUpperArmMaxDegrees = GetCommandLineFloat(
                "-yybCompareYybArmVisualUpperArmMaxDegrees",
                DefaultYybArmVisualUpperArmMaxDegrees);
            float yybArmVisualForearmMaxDegrees = GetCommandLineFloat(
                "-yybCompareYybArmVisualForearmMaxDegrees",
                DefaultYybArmVisualForearmMaxDegrees);
            bool enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorLowerBodySegmentDirectionEnabled", false);
            bool disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorLowerBodySegmentDirectionDisabled", false);
            float manualAnimatorLowerBodySegmentDirectionReferenceWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorLowerBodySegmentDirectionWeight",
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight);
            float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = GetCommandLineFloat(
                "-yybCompareManualAnimatorLowerBodySegmentDirectionMaxAngle",
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
            bool disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorUpperLegToLowerLegSegmentDirectionDisabled", false);
            float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = GetCommandLineFloat(
                "-yybCompareManualAnimatorUpperLegToLowerLegSegmentDirectionMaxAngle",
                DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            bool disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorLowerLegToFootSegmentDirectionDisabled", false);
            float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = GetCommandLineFloat(
                "-yybCompareManualAnimatorLowerLegToFootSegmentDirectionMaxAngle",
                DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = GetCommandLineFloat(
                "-yybCompareManualAnimatorLeftLowerLegToFootSegmentDirectionMaxAngle",
                DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = GetCommandLineFloat(
                "-yybCompareManualAnimatorRightLowerLegToFootSegmentDirectionMaxAngle",
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                GetCommandLineFloat(
                    "-yybCompareManualAnimatorRightLowerLegToFootSegmentDirectionAxisXzScale",
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale);
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                GetCommandLineFloat(
                    "-yybCompareManualAnimatorRightLowerLegToFootSegmentDirectionBlendWeight",
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                GetCommandLineFloat(
                    "-yybCompareManualAnimatorRightLowerLegToFootSegmentDirectionFrameGateStart",
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                GetCommandLineFloat(
                    "-yybCompareManualAnimatorRightLowerLegToFootSegmentDirectionFrameGateEnd",
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                GetCommandLineFloat(
                    "-yybCompareManualAnimatorRightLowerLegToFootSegmentDirectionEndpointBlendWeight",
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
            bool disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFootToToesSegmentDirectionDisabled", false);
            float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = GetCommandLineFloat(
                "-yybCompareManualAnimatorFootToToesSegmentDirectionMaxAngle",
                DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            bool enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFootHipsAlignedResidualYawEnabled", false);
            bool disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFootHipsAlignedResidualYawDisabled", false);
            float manualAnimatorFootHipsAlignedResidualYawReferenceWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorFootHipsAlignedResidualYawWeight",
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight);
            float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = GetCommandLineFloat(
                "-yybCompareManualAnimatorFootHipsAlignedResidualYawMaxAngle",
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            bool enablePostSetHumanPoseRightEndpointPositionRuntimeOverride =
                GetCommandLineBool("-yybComparePostSetHumanPoseRightEndpointPositionEnabled", false);
            float postSetHumanPoseRightEndpointPositionReferenceWeight = GetCommandLineFloat(
                "-yybComparePostSetHumanPoseRightEndpointPositionWeight",
                DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight);
            float postSetHumanPoseRightEndpointPositionReferenceMaxOffset = GetCommandLineFloat(
                "-yybComparePostSetHumanPoseRightEndpointPositionMaxOffset",
                DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            float postSetHumanPoseRightEndpointPositionReferencePositiveZScale = GetCommandLineFloat(
                "-yybComparePostSetHumanPoseRightEndpointPositionPositiveZScale",
                DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale);
            float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = GetCommandLineFloat(
                "-yybComparePostSetHumanPoseRightEndpointPositionToesBlendWeight",
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
            float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = GetCommandLineFloat(
                "-yybComparePostSetHumanPoseRightEndpointPositionFrameGateStart",
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = GetCommandLineFloat(
                "-yybComparePostSetHumanPoseRightEndpointPositionFrameGateEnd",
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            bool postSetHumanPoseEndpointPositionUseLeftSide =
                GetCommandLineBool("-yybComparePostSetHumanPoseEndpointPositionUseLeftSide", false);
            bool enablePreSetHumanPoseRightEndpointPositionRuntimeOverride =
                GetCommandLineBool("-yybComparePreSetHumanPoseRightEndpointPositionEnabled", false);
            float preSetHumanPoseRightEndpointPositionReferenceWeight = GetCommandLineFloat(
                "-yybComparePreSetHumanPoseRightEndpointPositionWeight",
                DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight);
            float preSetHumanPoseRightEndpointPositionReferenceMaxOffset = GetCommandLineFloat(
                "-yybComparePreSetHumanPoseRightEndpointPositionMaxOffset",
                DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            float preSetHumanPoseRightEndpointPositionReferencePositiveZScale = GetCommandLineFloat(
                "-yybComparePreSetHumanPoseRightEndpointPositionPositiveZScale",
                DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale);
            float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = GetCommandLineFloat(
                "-yybComparePreSetHumanPoseRightEndpointPositionToesBlendWeight",
                DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
            float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = GetCommandLineFloat(
                "-yybComparePreSetHumanPoseRightEndpointPositionFrameGateStart",
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = GetCommandLineFloat(
                "-yybComparePreSetHumanPoseRightEndpointPositionFrameGateEnd",
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            bool preSetHumanPoseEndpointPositionUseLeftSide =
                GetCommandLineBool("-yybComparePreSetHumanPoseEndpointPositionUseLeftSide", false);
            bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                GetCommandLineBool("-yybComparePreSetHumanPoseEndpointPositionUseGhostCurrentBasis", false);
            bool preSetHumanPoseEndpointPositionInvertBodyPositionX =
                GetCommandLineBool("-yybComparePreSetHumanPoseEndpointPositionInvertBodyPositionX", false);
            bool preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                GetCommandLineBool("-yybComparePreSetHumanPoseEndpointPositionInvertBodyPositionZ", false);
            bool usePostSetHumanPoseRightFootEvaluatorXzReference = GetCommandLineBool(
                "-yybComparePostSetHumanPoseRightFootEvaluatorXzReferenceEnabled",
                false);
            float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = GetCommandLineFloat(
                "-yybComparePostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude",
                DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            bool enableManualAnimatorBipedIkFootPositionRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorBipedIkFootPositionEnabled", false);
            float manualAnimatorBipedIkFootPositionReferenceWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorBipedIkFootPositionWeight",
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight);
            float manualAnimatorBipedIkFootPositionReferenceMaxOffset = GetCommandLineFloat(
                "-yybCompareManualAnimatorBipedIkFootPositionMaxOffset",
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset);
            bool enableManualAnimatorHipsLocalPositionRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorHipsLocalPositionEnabled", false);
            float manualAnimatorHipsLocalPositionReferenceWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorHipsLocalPositionWeight",
                DefaultManualAnimatorHipsLocalPositionReferenceWeight);
            float manualAnimatorHipsLocalPositionReferenceMaxOffset = GetCommandLineFloat(
                "-yybCompareManualAnimatorHipsLocalPositionMaxOffset",
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset);
            bool enableManualAnimatorBodyPositionXzRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorBodyPositionXzEnabled", false);
            float manualAnimatorBodyPositionXzReferenceWeight = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyPositionXzWeight",
                DefaultManualAnimatorBodyPositionXzReferenceWeight);
            float manualAnimatorBodyPositionXzReferenceMaxOffset = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyPositionXzMaxOffset",
                DefaultManualAnimatorBodyPositionXzReferenceMaxOffset);
            float manualAnimatorBodyPositionXzReferenceFrameGateStart = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyPositionXzFrameGateStart",
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart);
            float manualAnimatorBodyPositionXzReferenceFrameGateEnd = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyPositionXzFrameGateEnd",
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd);
            float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyPositionXzFrameGateBlendFrames",
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames);
            float manualAnimatorBodyPositionXzReferenceAxisXScale = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyPositionXzAxisXScale",
                DefaultManualAnimatorBodyPositionXzReferenceAxisXScale);
            float manualAnimatorBodyPositionXzReferenceAxisZScale = GetCommandLineFloat(
                "-yybCompareManualAnimatorBodyPositionXzAxisZScale",
                DefaultManualAnimatorBodyPositionXzReferenceAxisZScale);
            bool disableTargetHumanoidBonePositionLockRuntimeOverride =
                GetCommandLineBool("-yybCompareTargetHumanoidBonePositionLockDisabled", false);
            bool enableRetargetBodyPositionXzRootMotionRuntimeOverride =
                GetCommandLineBool("-yybCompareRetargetBodyPositionXzRootMotionEnabled", false);
            bool enableVmdPlaybackProbeRuntimeOverride =
                GetCommandLineBool("-yybCompareVmdPlaybackProbeEnabled", false);
            bool applyVmdPlaybackProbeIkTargetsRuntimeOverride =
                GetCommandLineBool("-yybCompareVmdPlaybackProbeApplyIkTargets", false);
            string editorDiagnosticSmokeSegmentName = GetCommandLineValue("-yybCompareSegment", "head");
            bool enableReferenceMmdTimingRuntimeOverride =
                GetCommandLineBool("-yybCompareReferenceMmdTimingEnabled", false);
            int diagnosticCaptureWidthOverride = GetCommandLineInt(
                "-yybCompareDiagnosticCaptureWidth",
                NoDiagnosticCaptureDimensionOverride);
            int diagnosticCaptureHeightOverride = GetCommandLineInt(
                "-yybCompareDiagnosticCaptureHeight",
                NoDiagnosticCaptureDimensionOverride);
            float diagnosticScreenshotPaddingOverride = GetCommandLineFloat(
                "-yybCompareDiagnosticScreenshotPadding",
                NoDiagnosticScreenshotFramingOverride);
            float diagnosticScreenshotVerticalViewportCenterOverride = GetCommandLineFloat(
                "-yybCompareDiagnosticScreenshotVerticalViewportCenter",
                NoDiagnosticScreenshotFramingOverride);
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                mmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride,
                enableManualAnimatorFootLocalRotationRuntimeOverride,
                disableManualAnimatorFootLocalRotationRuntimeOverride,
                enableManualAnimatorFullBodyPoseRuntimeOverride,
                disableManualAnimatorFullBodyPoseRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceWeight,
                manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                enableManualAnimatorBodyRotationRuntimeOverride,
                disableManualAnimatorBodyRotationRuntimeOverride,
                manualAnimatorBodyRotationReferenceWeight,
                enableManualAnimatorHandLocalRotationRuntimeOverride,
                enableManualAnimatorThumbLocalRotationRuntimeOverride,
                enableManualAnimatorHandPalmFrameRuntimeOverride,
                manualAnimatorHandPalmFrameWeight,
                overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings,
                enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                retargetPoseVisualSpikeCurrentWeight,
                retargetPoseVisualSpikeForearmStretchClampMaxOffset,
                enableYybArmSwingLimitRuntimeOverride,
                yybArmSwingLimitWeight,
                yybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmDirectionRetargetRuntimeOverride,
                yybArmDirectionUpperArmWeight,
                yybArmDirectionForearmWeight,
                yybArmDirectionUpperArmMaxDegrees,
                yybArmDirectionForearmMaxDegrees,
                yybArmDirectionLeftSideWeightScale,
                yybArmDirectionRightSideWeightScale,
                overrideYybArmSleeveAnchorRuntimeSettings,
                enableYybArmSleeveAnchorRuntimeOverride,
                yybArmSleeveAnchorInfluence,
                yybArmShoulderCapAnchorInfluence,
                yybArmSleeveAnchorMaxDegrees,
                overrideYybArmVisualTwistRuntimeSettings,
                enableYybArmVisualTwistRuntimeOverride,
                yybArmVisualUpperArmInfluence,
                yybArmVisualForearmInfluence,
                yybArmVisualUpperArmMaxDegrees,
                yybArmVisualForearmMaxDegrees,
                enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                enablePostSetHumanPoseRightEndpointPositionRuntimeOverride,
                postSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                postSetHumanPoseEndpointPositionUseLeftSide,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                preSetHumanPoseEndpointPositionUseLeftSide,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                preSetHumanPoseEndpointPositionInvertBodyPositionX,
                preSetHumanPoseEndpointPositionInvertBodyPositionZ,
                usePostSetHumanPoseRightFootEvaluatorXzReference,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                enableManualAnimatorBipedIkFootPositionRuntimeOverride,
                manualAnimatorBipedIkFootPositionReferenceWeight,
                manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                enableManualAnimatorHipsLocalPositionRuntimeOverride,
                manualAnimatorHipsLocalPositionReferenceWeight,
                manualAnimatorHipsLocalPositionReferenceMaxOffset,
                enableManualAnimatorBodyPositionXzRuntimeOverride,
                manualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale,
                enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                disableTargetHumanoidBonePositionLockRuntimeOverride,
                enableVmdPlaybackProbeRuntimeOverride,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                editorDiagnosticSmokeSegmentName,
                enableReferenceMmdTimingRuntimeOverride,
                diagnosticCaptureWidthOverride,
                diagnosticCaptureHeightOverride,
                diagnosticScreenshotPaddingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride);
        }

        public static void RunWithOptions(string fbxFileName, float durationSeconds, bool enableFingerCloseups)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented: true,
                mmdIkDeltaGuardLimitOverrideVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryHoldFrames: NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride: false);
        }

        public static void RunWithOptions(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryHoldFrames: NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride: false);
        }

        public static void RunWithOptions(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented,
            float mmdIkDeltaGuardLimitOverrideVmd)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryHoldFrames: NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride: false);
        }

        public static void RunWithOptions(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented,
            float mmdIkDeltaGuardLimitOverrideVmd,
            float mmdIkDeltaGuardRecoveryTriggerVmd)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryHoldFrames: NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride: false);
        }

        public static void RunWithOptions(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented,
            float mmdIkDeltaGuardLimitOverrideVmd,
            float mmdIkDeltaGuardRecoveryTriggerVmd,
            float mmdIkDeltaGuardRecoveryDebtThresholdVmd,
            bool enableFinalIkFootGroundingRuntimeOverride)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                mmdIkDeltaGuardRecoveryHoldFrames: NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride);
        }

        public static void RunWithOptions(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented,
            float mmdIkDeltaGuardLimitOverrideVmd,
            float mmdIkDeltaGuardRecoveryTriggerVmd,
            float mmdIkDeltaGuardRecoveryDebtThresholdVmd,
            int mmdIkDeltaGuardRecoveryHoldFrames,
            bool enableFinalIkFootGroundingRuntimeOverride,
            bool enableManualAnimatorFootLocalRotationRuntimeOverride = false,
            bool disableManualAnimatorFootLocalRotationRuntimeOverride = false,
            bool enableManualAnimatorFullBodyPoseRuntimeOverride = false,
            bool disableManualAnimatorFullBodyPoseRuntimeOverride = false,
            float manualAnimatorFullBodyPoseReferenceWeight = DefaultManualAnimatorFullBodyPoseReferenceWeight,
            bool manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride = false,
            bool manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride = false,
            bool manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride = false,
            bool enableManualAnimatorBodyRotationRuntimeOverride = false,
            bool disableManualAnimatorBodyRotationRuntimeOverride = false,
            float manualAnimatorBodyRotationReferenceWeight = DefaultManualAnimatorBodyRotationReferenceWeight,
            bool enableManualAnimatorHandLocalRotationRuntimeOverride = false,
            bool enableManualAnimatorThumbLocalRotationRuntimeOverride = false,
            bool enableManualAnimatorHandPalmFrameRuntimeOverride = false,
            float manualAnimatorHandPalmFrameWeight = DefaultManualAnimatorHandPalmFrameWeight,
            bool overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings = false,
            bool enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = true,
            float retargetPoseVisualSpikeCurrentWeight = DefaultRetargetPoseVisualSpikeCurrentWeight,
            float retargetPoseVisualSpikeForearmStretchClampMaxOffset =
                DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset,
            bool enableYybArmSwingLimitRuntimeOverride = false,
            float yybArmSwingLimitWeight = DefaultYybArmSwingLimitWeight,
            float yybArmSwingMaxDownDot = DefaultYybArmSwingMaxDownDot,
            float yybArmSwingMinHandHorizontalRatio = DefaultYybArmSwingMinHandHorizontalRatio,
            float yybArmSwingMaxHandBelowShoulderRatio = DefaultYybArmSwingMaxHandBelowShoulderRatio,
            float yybArmSwingHorizontalReachLimitWeight = DefaultYybArmSwingHorizontalReachLimitWeight,
            float yybArmSwingMaxHandHorizontalReachRatio = DefaultYybArmSwingMaxHandHorizontalReachRatio,
            float yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
            float yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply,
            float yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
            float yybArmSwingRaisedPoseMinUpperArmDownDot = DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
            float yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
            float yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
            bool enableYybArmDirectionRetargetRuntimeOverride = false,
            float yybArmDirectionUpperArmWeight = DefaultYybArmDirectionUpperArmWeight,
            float yybArmDirectionForearmWeight = DefaultYybArmDirectionForearmWeight,
            float yybArmDirectionUpperArmMaxDegrees = DefaultYybArmDirectionUpperArmMaxDegrees,
            float yybArmDirectionForearmMaxDegrees = DefaultYybArmDirectionForearmMaxDegrees,
            float yybArmDirectionLeftSideWeightScale = DefaultYybArmDirectionLeftSideWeightScale,
            float yybArmDirectionRightSideWeightScale = DefaultYybArmDirectionRightSideWeightScale,
            bool overrideYybArmSleeveAnchorRuntimeSettings = false,
            bool enableYybArmSleeveAnchorRuntimeOverride = true,
            float yybArmSleeveAnchorInfluence = DefaultYybArmSleeveAnchorInfluence,
            float yybArmShoulderCapAnchorInfluence = DefaultYybArmShoulderCapAnchorInfluence,
            float yybArmSleeveAnchorMaxDegrees = DefaultYybArmSleeveAnchorMaxDegrees,
            bool overrideYybArmVisualTwistRuntimeSettings = false,
            bool enableYybArmVisualTwistRuntimeOverride = true,
            float yybArmVisualUpperArmInfluence = DefaultYybArmVisualUpperArmInfluence,
            float yybArmVisualForearmInfluence = DefaultYybArmVisualForearmInfluence,
            float yybArmVisualUpperArmMaxDegrees = DefaultYybArmVisualUpperArmMaxDegrees,
            float yybArmVisualForearmMaxDegrees = DefaultYybArmVisualForearmMaxDegrees,
            bool enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false,
            bool disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false,
            float manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight,
            float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
            bool disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride = false,
            float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
            bool disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride = false,
            float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
            float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
            bool disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride = false,
            float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
            bool enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false,
            bool disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false,
            float manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight,
            float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
            bool enablePostSetHumanPoseRightEndpointPositionRuntimeOverride = false,
            float postSetHumanPoseRightEndpointPositionReferenceWeight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight,
            float postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset,
            float postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale,
            float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
            float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
            float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
            bool postSetHumanPoseEndpointPositionUseLeftSide = false,
            bool enablePreSetHumanPoseRightEndpointPositionRuntimeOverride = false,
            float preSetHumanPoseRightEndpointPositionReferenceWeight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight,
            float preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset,
            float preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale,
            float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
            float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
            float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
            bool preSetHumanPoseEndpointPositionUseLeftSide = false,
            bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis = false,
            bool preSetHumanPoseEndpointPositionInvertBodyPositionX = false,
            bool preSetHumanPoseEndpointPositionInvertBodyPositionZ = false,
            bool usePostSetHumanPoseRightFootEvaluatorXzReference = false,
            float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
            bool enableManualAnimatorBipedIkFootPositionRuntimeOverride = false,
            float manualAnimatorBipedIkFootPositionReferenceWeight =
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight,
            float manualAnimatorBipedIkFootPositionReferenceMaxOffset =
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset,
            bool enableManualAnimatorHipsLocalPositionRuntimeOverride = false,
            float manualAnimatorHipsLocalPositionReferenceWeight =
                DefaultManualAnimatorHipsLocalPositionReferenceWeight,
            float manualAnimatorHipsLocalPositionReferenceMaxOffset =
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset,
            bool enableManualAnimatorBodyPositionXzRuntimeOverride = false,
            float manualAnimatorBodyPositionXzReferenceWeight =
                DefaultManualAnimatorBodyPositionXzReferenceWeight,
            float manualAnimatorBodyPositionXzReferenceMaxOffset =
                DefaultManualAnimatorBodyPositionXzReferenceMaxOffset,
            float manualAnimatorBodyPositionXzReferenceFrameGateStart =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart,
            float manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd,
            float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
            float manualAnimatorBodyPositionXzReferenceAxisXScale =
                DefaultManualAnimatorBodyPositionXzReferenceAxisXScale,
            float manualAnimatorBodyPositionXzReferenceAxisZScale =
                DefaultManualAnimatorBodyPositionXzReferenceAxisZScale,
            bool enableRetargetBodyPositionXzRootMotionRuntimeOverride = false,
            bool disableTargetHumanoidBonePositionLockRuntimeOverride = false,
            bool enableVmdPlaybackProbeRuntimeOverride = false,
            bool applyVmdPlaybackProbeIkTargetsRuntimeOverride = false,
            string editorDiagnosticSmokeSegmentName = "head",
            bool enableReferenceMmdTimingRuntimeOverride = false,
            int diagnosticCaptureWidthOverride = NoDiagnosticCaptureDimensionOverride,
            int diagnosticCaptureHeightOverride = NoDiagnosticCaptureDimensionOverride,
            float diagnosticScreenshotPaddingOverride = NoDiagnosticScreenshotFramingOverride,
            float diagnosticScreenshotVerticalViewportCenterOverride = NoDiagnosticScreenshotFramingOverride)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                mmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride,
                enableManualAnimatorFootLocalRotationRuntimeOverride,
                disableManualAnimatorFootLocalRotationRuntimeOverride,
                enableManualAnimatorFullBodyPoseRuntimeOverride,
                disableManualAnimatorFullBodyPoseRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceWeight,
                manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                enableManualAnimatorBodyRotationRuntimeOverride,
                disableManualAnimatorBodyRotationRuntimeOverride,
                manualAnimatorBodyRotationReferenceWeight,
                enableManualAnimatorHandLocalRotationRuntimeOverride,
                enableManualAnimatorThumbLocalRotationRuntimeOverride,
                enableManualAnimatorHandPalmFrameRuntimeOverride,
                manualAnimatorHandPalmFrameWeight,
                overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings,
                enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                retargetPoseVisualSpikeCurrentWeight,
                retargetPoseVisualSpikeForearmStretchClampMaxOffset,
                enableYybArmSwingLimitRuntimeOverride,
                yybArmSwingLimitWeight,
                yybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmDirectionRetargetRuntimeOverride,
                yybArmDirectionUpperArmWeight,
                yybArmDirectionForearmWeight,
                yybArmDirectionUpperArmMaxDegrees,
                yybArmDirectionForearmMaxDegrees,
                yybArmDirectionLeftSideWeightScale,
                yybArmDirectionRightSideWeightScale,
                overrideYybArmSleeveAnchorRuntimeSettings,
                enableYybArmSleeveAnchorRuntimeOverride,
                yybArmSleeveAnchorInfluence,
                yybArmShoulderCapAnchorInfluence,
                yybArmSleeveAnchorMaxDegrees,
                overrideYybArmVisualTwistRuntimeSettings,
                enableYybArmVisualTwistRuntimeOverride,
                yybArmVisualUpperArmInfluence,
                yybArmVisualForearmInfluence,
                yybArmVisualUpperArmMaxDegrees,
                yybArmVisualForearmMaxDegrees,
                enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                enablePostSetHumanPoseRightEndpointPositionRuntimeOverride,
                postSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                postSetHumanPoseEndpointPositionUseLeftSide,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                preSetHumanPoseEndpointPositionUseLeftSide,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                preSetHumanPoseEndpointPositionInvertBodyPositionX,
                preSetHumanPoseEndpointPositionInvertBodyPositionZ,
                usePostSetHumanPoseRightFootEvaluatorXzReference,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                enableManualAnimatorBipedIkFootPositionRuntimeOverride,
                manualAnimatorBipedIkFootPositionReferenceWeight,
                manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                enableManualAnimatorHipsLocalPositionRuntimeOverride,
                manualAnimatorHipsLocalPositionReferenceWeight,
                manualAnimatorHipsLocalPositionReferenceMaxOffset,
                enableManualAnimatorBodyPositionXzRuntimeOverride,
                manualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale,
                enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                disableTargetHumanoidBonePositionLockRuntimeOverride,
                enableVmdPlaybackProbeRuntimeOverride,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                editorDiagnosticSmokeSegmentName,
                enableReferenceMmdTimingRuntimeOverride,
                diagnosticCaptureWidthOverride,
                diagnosticCaptureHeightOverride,
                diagnosticScreenshotPaddingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride);
        }

        public static void RunWithOptions(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented,
            float mmdIkDeltaGuardLimitOverrideVmd,
            float mmdIkDeltaGuardRecoveryTriggerVmd,
            bool enableFinalIkFootGroundingRuntimeOverride)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd: NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryHoldFrames: NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride);
        }

        private static void StartRun(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented,
            float mmdIkDeltaGuardLimitOverrideVmd,
            float mmdIkDeltaGuardRecoveryTriggerVmd,
            float mmdIkDeltaGuardRecoveryDebtThresholdVmd,
            int mmdIkDeltaGuardRecoveryHoldFrames,
            bool enableFinalIkFootGroundingRuntimeOverride,
            bool enableManualAnimatorFootLocalRotationRuntimeOverride = false,
            bool disableManualAnimatorFootLocalRotationRuntimeOverride = false,
            bool enableManualAnimatorFullBodyPoseRuntimeOverride = false,
            bool disableManualAnimatorFullBodyPoseRuntimeOverride = false,
            float manualAnimatorFullBodyPoseReferenceWeight = DefaultManualAnimatorFullBodyPoseReferenceWeight,
            bool manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride = false,
            bool manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride = false,
            bool manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride = false,
            bool enableManualAnimatorBodyRotationRuntimeOverride = false,
            bool disableManualAnimatorBodyRotationRuntimeOverride = false,
            float manualAnimatorBodyRotationReferenceWeight = DefaultManualAnimatorBodyRotationReferenceWeight,
            bool enableManualAnimatorHandLocalRotationRuntimeOverride = false,
            bool enableManualAnimatorThumbLocalRotationRuntimeOverride = false,
            bool enableManualAnimatorHandPalmFrameRuntimeOverride = false,
            float manualAnimatorHandPalmFrameWeight = DefaultManualAnimatorHandPalmFrameWeight,
            bool overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings = false,
            bool enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = true,
            float retargetPoseVisualSpikeCurrentWeight = DefaultRetargetPoseVisualSpikeCurrentWeight,
            float retargetPoseVisualSpikeForearmStretchClampMaxOffset =
                DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset,
            bool enableYybArmSwingLimitRuntimeOverride = false,
            float yybArmSwingLimitWeight = DefaultYybArmSwingLimitWeight,
            float yybArmSwingMaxDownDot = DefaultYybArmSwingMaxDownDot,
            float yybArmSwingMinHandHorizontalRatio = DefaultYybArmSwingMinHandHorizontalRatio,
            float yybArmSwingMaxHandBelowShoulderRatio = DefaultYybArmSwingMaxHandBelowShoulderRatio,
            float yybArmSwingHorizontalReachLimitWeight = DefaultYybArmSwingHorizontalReachLimitWeight,
            float yybArmSwingMaxHandHorizontalReachRatio = DefaultYybArmSwingMaxHandHorizontalReachRatio,
            float yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
            float yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply,
            float yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
            float yybArmSwingRaisedPoseMinUpperArmDownDot = DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
            float yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
            float yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
            bool enableYybArmDirectionRetargetRuntimeOverride = false,
            float yybArmDirectionUpperArmWeight = DefaultYybArmDirectionUpperArmWeight,
            float yybArmDirectionForearmWeight = DefaultYybArmDirectionForearmWeight,
            float yybArmDirectionUpperArmMaxDegrees = DefaultYybArmDirectionUpperArmMaxDegrees,
            float yybArmDirectionForearmMaxDegrees = DefaultYybArmDirectionForearmMaxDegrees,
            float yybArmDirectionLeftSideWeightScale = DefaultYybArmDirectionLeftSideWeightScale,
            float yybArmDirectionRightSideWeightScale = DefaultYybArmDirectionRightSideWeightScale,
            bool overrideYybArmSleeveAnchorRuntimeSettings = false,
            bool enableYybArmSleeveAnchorRuntimeOverride = true,
            float yybArmSleeveAnchorInfluence = DefaultYybArmSleeveAnchorInfluence,
            float yybArmShoulderCapAnchorInfluence = DefaultYybArmShoulderCapAnchorInfluence,
            float yybArmSleeveAnchorMaxDegrees = DefaultYybArmSleeveAnchorMaxDegrees,
            bool overrideYybArmVisualTwistRuntimeSettings = false,
            bool enableYybArmVisualTwistRuntimeOverride = true,
            float yybArmVisualUpperArmInfluence = DefaultYybArmVisualUpperArmInfluence,
            float yybArmVisualForearmInfluence = DefaultYybArmVisualForearmInfluence,
            float yybArmVisualUpperArmMaxDegrees = DefaultYybArmVisualUpperArmMaxDegrees,
            float yybArmVisualForearmMaxDegrees = DefaultYybArmVisualForearmMaxDegrees,
            bool enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false,
            bool disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false,
            float manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight,
            float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
            bool disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride = false,
            float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
            bool disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride = false,
            float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
            float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
            float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
            bool disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride = false,
            float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
            bool enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false,
            bool disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false,
            float manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight,
            float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
            bool enablePostSetHumanPoseRightEndpointPositionRuntimeOverride = false,
            float postSetHumanPoseRightEndpointPositionReferenceWeight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight,
            float postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset,
            float postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale,
            float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
            float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
            float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
            bool postSetHumanPoseEndpointPositionUseLeftSide = false,
            bool enablePreSetHumanPoseRightEndpointPositionRuntimeOverride = false,
            float preSetHumanPoseRightEndpointPositionReferenceWeight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight,
            float preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset,
            float preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale,
            float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
            float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
            float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
            bool preSetHumanPoseEndpointPositionUseLeftSide = false,
            bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis = false,
            bool preSetHumanPoseEndpointPositionInvertBodyPositionX = false,
            bool preSetHumanPoseEndpointPositionInvertBodyPositionZ = false,
            bool usePostSetHumanPoseRightFootEvaluatorXzReference = false,
            float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
            bool enableManualAnimatorBipedIkFootPositionRuntimeOverride = false,
            float manualAnimatorBipedIkFootPositionReferenceWeight =
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight,
            float manualAnimatorBipedIkFootPositionReferenceMaxOffset =
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset,
            bool enableManualAnimatorHipsLocalPositionRuntimeOverride = false,
            float manualAnimatorHipsLocalPositionReferenceWeight =
                DefaultManualAnimatorHipsLocalPositionReferenceWeight,
            float manualAnimatorHipsLocalPositionReferenceMaxOffset =
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset,
            bool enableManualAnimatorBodyPositionXzRuntimeOverride = false,
            float manualAnimatorBodyPositionXzReferenceWeight =
                DefaultManualAnimatorBodyPositionXzReferenceWeight,
            float manualAnimatorBodyPositionXzReferenceMaxOffset =
                DefaultManualAnimatorBodyPositionXzReferenceMaxOffset,
            float manualAnimatorBodyPositionXzReferenceFrameGateStart =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart,
            float manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd,
            float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
            float manualAnimatorBodyPositionXzReferenceAxisXScale =
                DefaultManualAnimatorBodyPositionXzReferenceAxisXScale,
            float manualAnimatorBodyPositionXzReferenceAxisZScale =
                DefaultManualAnimatorBodyPositionXzReferenceAxisZScale,
            bool enableRetargetBodyPositionXzRootMotionRuntimeOverride = false,
            bool disableTargetHumanoidBonePositionLockRuntimeOverride = false,
            bool enableVmdPlaybackProbeRuntimeOverride = false,
            bool applyVmdPlaybackProbeIkTargetsRuntimeOverride = false,
            string editorDiagnosticSmokeSegmentName = "head",
            bool enableReferenceMmdTimingRuntimeOverride = false,
            int diagnosticCaptureWidthOverride = NoDiagnosticCaptureDimensionOverride,
            int diagnosticCaptureHeightOverride = NoDiagnosticCaptureDimensionOverride,
            float diagnosticScreenshotPaddingOverride = NoDiagnosticScreenshotFramingOverride,
            float diagnosticScreenshotVerticalViewportCenterOverride = NoDiagnosticScreenshotFramingOverride)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[YybVisualComparisonBatchRunner] 이미 실행 중입니다.");
                return;
            }

            HumanoidSampleCode.SetEditorAutoStartSuppressed(true);
            ApplyTemporaryEnterPlayModeOptions();

            _projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            _fbxFileName = NormalizeFbxFileName(fbxFileName);
            _durationSeconds = Mathf.Max(0.1f, durationSeconds);
            _targetFrameCount = Mathf.Max(1, Mathf.CeilToInt(_durationSeconds * DefaultFrameRate));
            _enableFingerCloseups = enableFingerCloseups;
            _enableRecorderParentFrameIkOffsetsWhenCenterParented = enableRecorderParentFrameIkOffsetsWhenCenterParented;
            _mmdIkDeltaGuardLimitOverrideVmd = NormalizeMmdIkDeltaGuardLimitOverride(mmdIkDeltaGuardLimitOverrideVmd);
            _mmdIkDeltaGuardRecoveryTriggerVmd = NormalizeMmdIkDeltaGuardLimitOverride(mmdIkDeltaGuardRecoveryTriggerVmd);
            _mmdIkDeltaGuardRecoveryDebtThresholdVmd = NormalizeMmdIkDeltaGuardLimitOverride(mmdIkDeltaGuardRecoveryDebtThresholdVmd);
            _mmdIkDeltaGuardRecoveryHoldFrames = NormalizeMmdIkDeltaGuardRecoveryHoldFrames(mmdIkDeltaGuardRecoveryHoldFrames);
            _enableFinalIkFootGroundingRuntimeOverride = enableFinalIkFootGroundingRuntimeOverride;
            _enableManualAnimatorFootLocalRotationRuntimeOverride = enableManualAnimatorFootLocalRotationRuntimeOverride;
            _disableManualAnimatorFootLocalRotationRuntimeOverride = disableManualAnimatorFootLocalRotationRuntimeOverride;
            _enableManualAnimatorFullBodyPoseRuntimeOverride = enableManualAnimatorFullBodyPoseRuntimeOverride;
            _disableManualAnimatorFullBodyPoseRuntimeOverride = disableManualAnimatorFullBodyPoseRuntimeOverride;
            _manualAnimatorFullBodyPoseReferenceWeight = Mathf.Clamp01(manualAnimatorFullBodyPoseReferenceWeight);
            _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride =
                manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride;
            _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride =
                manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride =
                manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride;
            _enableManualAnimatorBodyRotationRuntimeOverride = enableManualAnimatorBodyRotationRuntimeOverride;
            _disableManualAnimatorBodyRotationRuntimeOverride = disableManualAnimatorBodyRotationRuntimeOverride;
            _manualAnimatorBodyRotationReferenceWeight = Mathf.Clamp01(manualAnimatorBodyRotationReferenceWeight);
            _enableManualAnimatorHandLocalRotationRuntimeOverride = enableManualAnimatorHandLocalRotationRuntimeOverride;
            _enableManualAnimatorThumbLocalRotationRuntimeOverride = enableManualAnimatorThumbLocalRotationRuntimeOverride;
            _enableManualAnimatorHandPalmFrameRuntimeOverride = enableManualAnimatorHandPalmFrameRuntimeOverride;
            _manualAnimatorHandPalmFrameWeight = Mathf.Clamp01(manualAnimatorHandPalmFrameWeight);
            _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings =
                overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings;
            _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride =
                enableRetargetPoseVisualSpikeSmoothingRuntimeOverride;
            _retargetPoseVisualSpikeCurrentWeight = Mathf.Clamp(
                retargetPoseVisualSpikeCurrentWeight,
                0.1f,
                1f);
            _retargetPoseVisualSpikeForearmStretchClampMaxOffset =
                Mathf.Clamp01(retargetPoseVisualSpikeForearmStretchClampMaxOffset);
            _enableYybArmSwingLimitRuntimeOverride = enableYybArmSwingLimitRuntimeOverride;
            _yybArmSwingLimitWeight = Mathf.Clamp01(yybArmSwingLimitWeight);
            _yybArmSwingMaxDownDot = Mathf.Clamp01(yybArmSwingMaxDownDot);
            _yybArmSwingMinHandHorizontalRatio = Mathf.Clamp(yybArmSwingMinHandHorizontalRatio, 0f, 1.5f);
            _yybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp(
                yybArmSwingMaxHandBelowShoulderRatio,
                0f,
                1.5f);
            _yybArmSwingHorizontalReachLimitWeight = Mathf.Clamp01(yybArmSwingHorizontalReachLimitWeight);
            _yybArmSwingMaxHandHorizontalReachRatio = Mathf.Clamp(
                yybArmSwingMaxHandHorizontalReachRatio,
                0f,
                1.5f);
            _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = Mathf.Clamp(
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                0f,
                1.5f);
            _yybArmSwingHorizontalReachMinElbowAngleAfterApply = Mathf.Clamp(
                yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                0f,
                180f);
            _yybArmSwingRaisedPoseHorizontalReachLimitWeight = Mathf.Clamp01(
                yybArmSwingRaisedPoseHorizontalReachLimitWeight);
            _yybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01(
                yybArmSwingRaisedPoseMinUpperArmDownDot);
            _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp(
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                0f,
                1.5f);
            _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = Mathf.Clamp(
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                0f,
                1.5f);
            _enableYybArmDirectionRetargetRuntimeOverride = enableYybArmDirectionRetargetRuntimeOverride;
            _yybArmDirectionUpperArmWeight = Mathf.Clamp01(yybArmDirectionUpperArmWeight);
            _yybArmDirectionForearmWeight = Mathf.Clamp01(yybArmDirectionForearmWeight);
            _yybArmDirectionUpperArmMaxDegrees = Mathf.Clamp(yybArmDirectionUpperArmMaxDegrees, 0f, 120f);
            _yybArmDirectionForearmMaxDegrees = Mathf.Clamp(yybArmDirectionForearmMaxDegrees, 0f, 120f);
            _yybArmDirectionLeftSideWeightScale = Mathf.Clamp01(yybArmDirectionLeftSideWeightScale);
            _yybArmDirectionRightSideWeightScale = Mathf.Clamp01(yybArmDirectionRightSideWeightScale);
            _overrideYybArmSleeveAnchorRuntimeSettings = overrideYybArmSleeveAnchorRuntimeSettings;
            _enableYybArmSleeveAnchorRuntimeOverride = enableYybArmSleeveAnchorRuntimeOverride;
            _yybArmSleeveAnchorInfluence = Mathf.Clamp01(yybArmSleeveAnchorInfluence);
            _yybArmShoulderCapAnchorInfluence = Mathf.Clamp01(yybArmShoulderCapAnchorInfluence);
            _yybArmSleeveAnchorMaxDegrees = Mathf.Clamp(yybArmSleeveAnchorMaxDegrees, 0f, 120f);
            _overrideYybArmVisualTwistRuntimeSettings = overrideYybArmVisualTwistRuntimeSettings;
            _enableYybArmVisualTwistRuntimeOverride = enableYybArmVisualTwistRuntimeOverride;
            _yybArmVisualUpperArmInfluence = Mathf.Clamp01(yybArmVisualUpperArmInfluence);
            _yybArmVisualForearmInfluence = Mathf.Clamp01(yybArmVisualForearmInfluence);
            _yybArmVisualUpperArmMaxDegrees = Mathf.Clamp(yybArmVisualUpperArmMaxDegrees, 0f, 120f);
            _yybArmVisualForearmMaxDegrees = Mathf.Clamp(yybArmVisualForearmMaxDegrees, 0f, 120f);
            _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride =
                disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            _manualAnimatorLowerBodySegmentDirectionReferenceWeight = Mathf.Clamp01(
                manualAnimatorLowerBodySegmentDirectionReferenceWeight);
            _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = Mathf.Max(
                0f,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
            _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride =
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = Mathf.Max(
                0f,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride =
                disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = Mathf.Max(
                0f,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = Mathf.Max(
                0f,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = Mathf.Max(
                0f,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = Mathf.Clamp01(
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = Mathf.Clamp01(
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = Mathf.Max(
                0f,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = Mathf.Max(
                0f,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = Mathf.Clamp01(
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
            _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride =
                disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = Mathf.Max(
                0f,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            _manualAnimatorFootHipsAlignedResidualYawReferenceWeight = Mathf.Clamp01(
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight);
            _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = Mathf.Max(
                0f,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride =
                enablePostSetHumanPoseRightEndpointPositionRuntimeOverride;
            _postSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01(
                postSetHumanPoseRightEndpointPositionReferenceWeight);
            _postSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(
                0f,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            _postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale);
            _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(
                0f,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(
                0f,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            _postSetHumanPoseEndpointPositionUseLeftSide = postSetHumanPoseEndpointPositionUseLeftSide;
            _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride =
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride;
            _preSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01(
                preSetHumanPoseRightEndpointPositionReferenceWeight);
            _preSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(
                0f,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            _preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale);
            _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(
                0f,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(
                0f,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            _preSetHumanPoseEndpointPositionUseLeftSide = preSetHumanPoseEndpointPositionUseLeftSide;
            _preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            _preSetHumanPoseEndpointPositionInvertBodyPositionX =
                preSetHumanPoseEndpointPositionInvertBodyPositionX;
            _preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                preSetHumanPoseEndpointPositionInvertBodyPositionZ;
            _usePostSetHumanPoseRightFootEvaluatorXzReference =
                usePostSetHumanPoseRightFootEvaluatorXzReference;
            _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = Mathf.Max(
                0f,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            _enableManualAnimatorBipedIkFootPositionRuntimeOverride = enableManualAnimatorBipedIkFootPositionRuntimeOverride;
            _manualAnimatorBipedIkFootPositionReferenceWeight = Mathf.Clamp01(
                manualAnimatorBipedIkFootPositionReferenceWeight);
            _manualAnimatorBipedIkFootPositionReferenceMaxOffset = Mathf.Max(
                0f,
                manualAnimatorBipedIkFootPositionReferenceMaxOffset);
            _enableManualAnimatorHipsLocalPositionRuntimeOverride = enableManualAnimatorHipsLocalPositionRuntimeOverride;
            _manualAnimatorHipsLocalPositionReferenceWeight = Mathf.Clamp01(
                manualAnimatorHipsLocalPositionReferenceWeight);
            _manualAnimatorHipsLocalPositionReferenceMaxOffset = Mathf.Max(
                0f,
                manualAnimatorHipsLocalPositionReferenceMaxOffset);
            _enableManualAnimatorBodyPositionXzRuntimeOverride =
                enableManualAnimatorBodyPositionXzRuntimeOverride;
            _manualAnimatorBodyPositionXzReferenceWeight = Mathf.Clamp01(
                manualAnimatorBodyPositionXzReferenceWeight);
            _manualAnimatorBodyPositionXzReferenceMaxOffset = Mathf.Max(
                0f,
                manualAnimatorBodyPositionXzReferenceMaxOffset);
            _manualAnimatorBodyPositionXzReferenceFrameGateStart = Mathf.Max(
                0f,
                manualAnimatorBodyPositionXzReferenceFrameGateStart);
            _manualAnimatorBodyPositionXzReferenceFrameGateEnd = Mathf.Max(
                0f,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd);
            _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = Mathf.Max(
                0f,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames);
            _manualAnimatorBodyPositionXzReferenceAxisXScale = Mathf.Clamp01(
                manualAnimatorBodyPositionXzReferenceAxisXScale);
            _manualAnimatorBodyPositionXzReferenceAxisZScale = Mathf.Clamp01(
                manualAnimatorBodyPositionXzReferenceAxisZScale);
            _enableRetargetBodyPositionXzRootMotionRuntimeOverride =
                enableRetargetBodyPositionXzRootMotionRuntimeOverride;
            _disableTargetHumanoidBonePositionLockRuntimeOverride =
                disableTargetHumanoidBonePositionLockRuntimeOverride;
            _enableVmdPlaybackProbeRuntimeOverride = enableVmdPlaybackProbeRuntimeOverride;
            _applyVmdPlaybackProbeIkTargetsRuntimeOverride =
                enableVmdPlaybackProbeRuntimeOverride && applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            _vmdPlaybackProbeSourceVmdPath = string.Empty;
            _enableReferenceMmdTimingRuntimeOverride = enableReferenceMmdTimingRuntimeOverride;
            _editorDiagnosticSmokeSegment = ResolveEditorDiagnosticSmokeSegment(editorDiagnosticSmokeSegmentName);
            _diagnosticCaptureWidthOverride = NormalizeDiagnosticCaptureDimensionOverride(diagnosticCaptureWidthOverride);
            _diagnosticCaptureHeightOverride = NormalizeDiagnosticCaptureDimensionOverride(diagnosticCaptureHeightOverride);
            _diagnosticScreenshotPaddingOverride =
                NormalizeDiagnosticScreenshotPaddingOverride(diagnosticScreenshotPaddingOverride);
            _diagnosticScreenshotVerticalViewportCenterOverride =
                NormalizeDiagnosticScreenshotVerticalViewportCenterOverride(
                    diagnosticScreenshotVerticalViewportCenterOverride);

            try
            {
                _referenceClipAssetPath = ResolveReferenceClipAssetPath(
                    _fbxFileName,
                    assetPath => LoadFirstAnimationClip(assetPath) != null);
                _referenceClip = LoadFirstAnimationClip(_referenceClipAssetPath);
                if (_referenceClip == null)
                {
                    throw new InvalidOperationException($"비교 기준 AnimationClip을 찾지 못했습니다: {_fbxFileName}");
                }

                _fallbackController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ManualControllerPath) ??
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackControllerPath);
                if (_fallbackController == null)
                {
                    throw new InvalidOperationException("수동 비교용 Animator Controller를 찾지 못했습니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YybVisualComparisonBatchRunner] 준비 실패: {ex.Message}\n{ex.StackTrace}");
                RestoreEnterPlayModeOptions();
                EmitCompletion(
                    passed: false,
                    summaryJsonPath: string.Empty,
                    summaryMarkdownPath: string.Empty,
                    failures: new[] { ex.Message });
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            PendingJobs.Clear();
            Results.Clear();
            Failures.Clear();
            _activeJob = null;
            _activeFileManager = null;
            _activeRecorder = null;
            _activeJobFinished = false;
            _activeJobStartedInPlayMode = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;

            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.SubManualTestPrefab,
                ScenePath = SubManualScenePath,
                SceneName = "Sub_Manual",
                DisplayName = "Sub_Manual testPrefab 수동 기준",
                ManualTargetNameToken = ManualTestPrefabNameToken
            });
            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.SubManualYyb,
                ScenePath = SubManualScenePath,
                SceneName = "Sub_Manual",
                DisplayName = "Sub_Manual YYB 수동 기준",
                ManualTargetNameToken = ManualYybNameToken
            });
            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.MainRecording,
                ScenePath = MainRecordingScenePath,
                SceneName = "Main_Recoding",
                DisplayName = "Main_Recoding YYB 자동 경로",
                ManualTargetNameToken = string.Empty
            });
            if (_enableVmdPlaybackProbeRuntimeOverride)
            {
                PendingJobs.Enqueue(new CaptureJob
                {
                    Mode = CaptureMode.MainRecordingVmdPlaybackProbe,
                    ScenePath = MainRecordingScenePath,
                    SceneName = "Main_Recoding",
                    DisplayName = "Main_Recoding YYB VMD replay probe",
                    ManualTargetNameToken = string.Empty
                });
            }
            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.MainAuto,
                ScenePath = MainAutoScenePath,
                SceneName = "Main_Auto",
                DisplayName = "Main_Auto YYB 자동 경로",
                ManualTargetNameToken = string.Empty
            });

            string rawSummarySessionId =
                $"when-{DateTime.Now:yyyyMMdd-HHmmss}_where-MainAuto_vs_SubManual_who-testprefab-vs-yyb_what-visual-compare_why-runtime-match_how-unity-batch";
            _summarySessionId = BuildSafeSummarySessionId(rawSummarySessionId);
            string summaryRoot = Path.Combine(_projectRoot, OutputRootDirectory);
            _summaryDirectory = Path.Combine(summaryRoot, _summarySessionId);
            Directory.CreateDirectory(_summaryDirectory);

            _isRunning = true;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            SavePersistedState();

            Debug.Log(
                $"[YybVisualComparisonBatchRunner] 시작: fbx={_fbxFileName}, duration={_durationSeconds:F2}s, " +
                $"targetFrames={_targetFrameCount}, fingerCloseups={_enableFingerCloseups}, " +
                $"recorderParentIkOffsets={_enableRecorderParentFrameIkOffsetsWhenCenterParented}, " +
                $"mmdIkDeltaGuardLimitOverrideVmd={FormatRuntimeOverride(_mmdIkDeltaGuardLimitOverrideVmd)}, " +
                $"mmdIkDeltaGuardRecoveryTriggerVmd={FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryTriggerVmd)}, " +
                $"mmdIkDeltaGuardRecoveryDebtThresholdVmd={FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryDebtThresholdVmd)}, " +
                $"mmdIkDeltaGuardRecoveryHoldFrames={FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryHoldFrames)}, " +
                $"finalIkFootGrounding={_enableFinalIkFootGroundingRuntimeOverride}, " +
                $"manualFootLocalRotation={_enableManualAnimatorFootLocalRotationRuntimeOverride}, " +
                $"manualFullBodyPose={_enableManualAnimatorFullBodyPoseRuntimeOverride}/{_manualAnimatorFullBodyPoseReferenceWeight:F2}, " +
                $"manualBodyRotation={_enableManualAnimatorBodyRotationRuntimeOverride}/{_manualAnimatorBodyRotationReferenceWeight:F2}, " +
                $"manualHandLocalRotation={_enableManualAnimatorHandLocalRotationRuntimeOverride}, " +
                $"manualThumbLocalRotation={_enableManualAnimatorThumbLocalRotationRuntimeOverride}, " +
                $"manualHandPalmFrame={_enableManualAnimatorHandPalmFrameRuntimeOverride}/{_manualAnimatorHandPalmFrameWeight:F2}, " +
                $"retargetPoseVisualSpikeSmoothing={_overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings}/{_enableRetargetPoseVisualSpikeSmoothingRuntimeOverride}/{_retargetPoseVisualSpikeCurrentWeight:F2}/{_retargetPoseVisualSpikeForearmStretchClampMaxOffset:F2}, " +
                $"yybArmSwingLimit={_enableYybArmSwingLimitRuntimeOverride}/{_yybArmSwingLimitWeight:F2}, " +
                $"yybArmDirection={_enableYybArmDirectionRetargetRuntimeOverride}/{_yybArmDirectionUpperArmWeight:F2}/{_yybArmDirectionForearmWeight:F2}, " +
                $"yybArmSleeveAnchor={_overrideYybArmSleeveAnchorRuntimeSettings}/{_enableYybArmSleeveAnchorRuntimeOverride}/{_yybArmSleeveAnchorInfluence:F2}/{_yybArmShoulderCapAnchorInfluence:F2}/{_yybArmSleeveAnchorMaxDegrees:F1}, " +
                $"yybArmVisualTwist={_overrideYybArmVisualTwistRuntimeSettings}/{_enableYybArmVisualTwistRuntimeOverride}/{_yybArmVisualUpperArmInfluence:F2}/{_yybArmVisualForearmInfluence:F2}/{_yybArmVisualUpperArmMaxDegrees:F1}/{_yybArmVisualForearmMaxDegrees:F1}, " +
                $"manualHipsLocalPosition={_enableManualAnimatorHipsLocalPositionRuntimeOverride}/{_manualAnimatorHipsLocalPositionReferenceWeight:F2}/{_manualAnimatorHipsLocalPositionReferenceMaxOffset:F3}, " +
                $"retargetBodyPositionXzRootMotion={_enableRetargetBodyPositionXzRootMotionRuntimeOverride}, " +
                $"targetBoneLockDisabled={_disableTargetHumanoidBonePositionLockRuntimeOverride}, " +
                $"vmdPlaybackProbe={_enableVmdPlaybackProbeRuntimeOverride}, " +
                $"vmdPlaybackProbeApplyIkTargets={_applyVmdPlaybackProbeIkTargetsRuntimeOverride}, " +
                $"referenceMmdTiming={_enableReferenceMmdTimingRuntimeOverride}, " +
                $"segment={_editorDiagnosticSmokeSegment}, " +
                $"diagnosticCapture={FormatRuntimeOverride(_diagnosticCaptureWidthOverride)}x{FormatRuntimeOverride(_diagnosticCaptureHeightOverride)}, " +
                $"diagnosticFraming={FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotPaddingOverride)}/{FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotVerticalViewportCenterOverride)}, " +
                $"batchMode={Application.isBatchMode}");
            AppendRunnerTrace(
                $"run started fbx={_fbxFileName} duration={_durationSeconds:F2}s " +
                $"fingerCloseups={_enableFingerCloseups} recorderParentIkOffsets={_enableRecorderParentFrameIkOffsetsWhenCenterParented} " +
                $"mmdIkDeltaGuardLimitOverrideVmd={FormatRuntimeOverride(_mmdIkDeltaGuardLimitOverrideVmd)} " +
                $"mmdIkDeltaGuardRecoveryTriggerVmd={FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryTriggerVmd)} " +
                $"mmdIkDeltaGuardRecoveryDebtThresholdVmd={FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryDebtThresholdVmd)} " +
                $"mmdIkDeltaGuardRecoveryHoldFrames={FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryHoldFrames)} " +
                $"finalIkFootGrounding={_enableFinalIkFootGroundingRuntimeOverride} " +
                $"manualFootLocalRotation={_enableManualAnimatorFootLocalRotationRuntimeOverride} " +
                $"manualFullBodyPose={_enableManualAnimatorFullBodyPoseRuntimeOverride}/{_manualAnimatorFullBodyPoseReferenceWeight:F2} " +
                $"manualBodyRotation={_enableManualAnimatorBodyRotationRuntimeOverride}/{_manualAnimatorBodyRotationReferenceWeight:F2} " +
                $"manualHandLocalRotation={_enableManualAnimatorHandLocalRotationRuntimeOverride} " +
                $"manualThumbLocalRotation={_enableManualAnimatorThumbLocalRotationRuntimeOverride} " +
                $"manualHandPalmFrame={_enableManualAnimatorHandPalmFrameRuntimeOverride}/{_manualAnimatorHandPalmFrameWeight:F2} " +
                $"retargetPoseVisualSpikeSmoothing={_overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings}/{_enableRetargetPoseVisualSpikeSmoothingRuntimeOverride}/{_retargetPoseVisualSpikeCurrentWeight:F2}/{_retargetPoseVisualSpikeForearmStretchClampMaxOffset:F2} " +
                $"yybArmSwingLimit={_enableYybArmSwingLimitRuntimeOverride}/{_yybArmSwingLimitWeight:F2} " +
                $"yybArmDirection={_enableYybArmDirectionRetargetRuntimeOverride}/{_yybArmDirectionUpperArmWeight:F2}/{_yybArmDirectionForearmWeight:F2} " +
                $"yybArmSleeveAnchor={_overrideYybArmSleeveAnchorRuntimeSettings}/{_enableYybArmSleeveAnchorRuntimeOverride}/{_yybArmSleeveAnchorInfluence:F2}/{_yybArmShoulderCapAnchorInfluence:F2}/{_yybArmSleeveAnchorMaxDegrees:F1} " +
                $"yybArmVisualTwist={_overrideYybArmVisualTwistRuntimeSettings}/{_enableYybArmVisualTwistRuntimeOverride}/{_yybArmVisualUpperArmInfluence:F2}/{_yybArmVisualForearmInfluence:F2}/{_yybArmVisualUpperArmMaxDegrees:F1}/{_yybArmVisualForearmMaxDegrees:F1} " +
                $"manualHipsLocalPosition={_enableManualAnimatorHipsLocalPositionRuntimeOverride}/{_manualAnimatorHipsLocalPositionReferenceWeight:F2}/{_manualAnimatorHipsLocalPositionReferenceMaxOffset:F3} " +
                $"retargetBodyPositionXzRootMotion={_enableRetargetBodyPositionXzRootMotionRuntimeOverride} " +
                $"targetBoneLockDisabled={_disableTargetHumanoidBonePositionLockRuntimeOverride} " +
                $"vmdPlaybackProbe={_enableVmdPlaybackProbeRuntimeOverride} " +
                $"vmdPlaybackProbeApplyIkTargets={_applyVmdPlaybackProbeIkTargetsRuntimeOverride} " +
                $"referenceMmdTiming={_enableReferenceMmdTimingRuntimeOverride} " +
                $"segment={_editorDiagnosticSmokeSegment} " +
                $"diagnosticCapture={FormatRuntimeOverride(_diagnosticCaptureWidthOverride)}x{FormatRuntimeOverride(_diagnosticCaptureHeightOverride)} " +
                $"diagnosticFraming={FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotPaddingOverride)}/{FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotVerticalViewportCenterOverride)}");

            if (!Application.isBatchMode && RequestRuntimeDiagnosticScriptRefresh())
            {
                Debug.Log("[YybVisualComparisonBatchRunner] runtime diagnostics script refresh 대기 중입니다.");
                AppendRunnerTrace("runtime diagnostics script refresh requested; waiting before first job");
                EditorApplication.delayCall += ContinueStartRunAfterRefresh;
                return;
            }

            if (Application.isBatchMode)
            {
                AppendRunnerTrace("batch mode start skipping runtime diagnostics refresh");
            }

            StartNextJob();
        }

        private static void ContinueStartRunAfterRefresh()
        {
            if (!_isRunning || _activeJob != null || PendingJobs.Count == 0)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ContinueStartRunAfterRefresh;
                return;
            }

            AppendRunnerTrace("runtime diagnostics script refresh settled; continuing run");
            StartNextJob();
        }

        private static bool RequestRuntimeDiagnosticScriptRefresh()
        {
            return UnityManualRefreshGuard.RequestRefreshForAssets(
                RuntimeDiagnosticScriptPaths,
                "yyb_visual_comparison_runtime_diagnostics");
        }

        private static CaptureJob[] BuildCaptureJobs(bool enableVmdPlaybackProbeRuntimeOverride)
        {
            var jobs = new List<CaptureJob>
            {
                new CaptureJob
                {
                    Mode = CaptureMode.SubManualTestPrefab,
                    ScenePath = SubManualScenePath,
                    SceneName = "Sub_Manual",
                    DisplayName = "Sub_Manual testPrefab manual baseline",
                    ManualTargetNameToken = ManualTestPrefabNameToken
                },
                new CaptureJob
                {
                    Mode = CaptureMode.SubManualYyb,
                    ScenePath = SubManualScenePath,
                    SceneName = "Sub_Manual",
                    DisplayName = "Sub_Manual YYB manual baseline",
                    ManualTargetNameToken = ManualYybNameToken
                },
                new CaptureJob
                {
                    Mode = CaptureMode.MainRecording,
                    ScenePath = MainRecordingScenePath,
                    SceneName = "Main_Recoding",
                    DisplayName = "Main_Recoding YYB direct FBX baseline",
                    ManualTargetNameToken = string.Empty
                }
            };

            if (enableVmdPlaybackProbeRuntimeOverride)
            {
                jobs.Add(new CaptureJob
                {
                    Mode = CaptureMode.MainRecordingVmdPlaybackProbe,
                    ScenePath = MainRecordingScenePath,
                    SceneName = "Main_Recoding",
                    DisplayName = "Main_Recoding YYB VMD replay probe",
                    ManualTargetNameToken = string.Empty
                });
            }

            jobs.Add(new CaptureJob
            {
                Mode = CaptureMode.MainAuto,
                ScenePath = MainAutoScenePath,
                SceneName = "Main_Auto",
                DisplayName = "Main_Auto YYB automatic path",
                ManualTargetNameToken = string.Empty
            });
            return jobs.ToArray();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!_isRunning)
            {
                return;
            }

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    _playModeEntryPending = false;
                    _playModeEntryRequestedAt = 0d;
                    SavePersistedState();
                    EditorApplication.update -= TryEnterPlayModeForActiveJob;
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"}");
                    EditorApplication.delayCall += StartCurrentJobInPlayMode;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished} pending={_advanceAfterPlayStopPending}");
                    CleanupActiveSubscriptions();
                    if (_advanceAfterPlayStopPending)
                    {
                        QueueAdvanceAfterPlayStop("EnteredEditMode");
                    }
                    else if (_activeJob != null && !_activeJobFinished)
                    {
                        QueuePlayModeEntryForActiveJob("EnteredEditModeWithoutCompletion");
                    }
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished} pending={_advanceAfterPlayStopPending}");
                    if (_activeJob != null && !_activeJobFinished)
                    {
                        RecordFailure($"Play Mode가 작업 완료 전에 종료되었습니다: {_activeJob.DisplayName}");
                    }
                    break;
            }
        }

        private static void StartNextJob()
        {
            if (!CanStartNextJob(_isRunning, _activeJob != null, _activeJobFinished))
            {
                if (_isRunning)
                {
                    AppendRunnerTrace(
                        $"start next ignored active={_activeJob?.DisplayName ?? "<none>"} " +
                        $"finished={_activeJobFinished} pendingJobs={PendingJobs.Count}");
                }

                return;
            }

            _activeJob = null;
            _activeFileManager = null;
            _activeRecorder = null;
            _activeJobFinished = false;
            _activeJobStartedInPlayMode = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;

            if (PendingJobs.Count == 0)
            {
                FinalizeRun();
                return;
            }

            _activeJob = PendingJobs.Dequeue();
            Debug.Log($"[YybVisualComparisonBatchRunner] 다음 작업: {_activeJob.DisplayName}");
            AppendRunnerTrace($"next job={_activeJob.DisplayName} pendingJobs={PendingJobs.Count}");
            SavePersistedState();

            try
            {
                if (!string.Equals(EditorSceneManager.GetActiveScene().path, _activeJob.ScenePath, StringComparison.Ordinal))
                {
                    EditorSceneManager.OpenScene(_activeJob.ScenePath, OpenSceneMode.Single);
                }
            }
            catch (Exception ex)
            {
                RecordFailure($"씬 열기 실패: {_activeJob.ScenePath} / {ex.Message}");
                EditorApplication.delayCall += StartNextJob;
                return;
            }

            QueuePlayModeEntryForActiveJob("StartNextJob");
        }

        private static bool CanStartNextJob(bool isRunning, bool hasActiveJob, bool activeJobFinished)
        {
            return isRunning && (!hasActiveJob || activeJobFinished);
        }

        private static void StartCurrentJobInPlayMode()
        {
            if (!_isRunning || _activeJob == null || !EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
            {
                return;
            }

            if (_activeJobStartedInPlayMode)
            {
                return;
            }

            _activeJobStartedInPlayMode = true;

            try
            {
                switch (_activeJob.Mode)
                {
                    case CaptureMode.MainAuto:
                    case CaptureMode.MainRecording:
                    case CaptureMode.MainRecordingVmdPlaybackProbe:
                        StartMainSceneJob();
                        break;
                    case CaptureMode.SubManualTestPrefab:
                    case CaptureMode.SubManualYyb:
                        StartSubManualJob(_activeJob.ManualTargetNameToken);
                        break;
                    default:
                        throw new NotSupportedException($"지원하지 않는 작업 모드: {_activeJob.Mode}");
                }
            }
            catch (Exception ex)
            {
                _activeJobStartedInPlayMode = false;
                _activeJobFinished = true;
                RecordFailure($"{_activeJob.DisplayName} 시작 실패: {ex.Message}");
                RequestPlayModeStop();
            }
        }

        private static void StartMainSceneJob()
        {
            _activeFileManager = UnityEngine.Object.FindObjectOfType<FileManager>();
            if (_activeFileManager == null)
            {
                throw new InvalidOperationException($"{_activeJob.SceneName} 씬에서 FileManager를 찾지 못했습니다.");
            }

            ApplyMainSceneRuntimeOverrides(_activeFileManager);

            _activeRecorder = _activeFileManager.targetCharacter != null
                ? _activeFileManager.targetCharacter.GetComponent<HumanoidSampleCode>()
                : null;
            if (_activeRecorder != null)
            {
                UnityHumanoidVMDRecorder vmdRecorder = _activeRecorder.GetComponent<UnityHumanoidVMDRecorder>();
                if (vmdRecorder != null)
                {
                    vmdRecorder.EnableParentFrameIkOffsetCompensationWhenCenterParented =
                        _enableRecorderParentFrameIkOffsetsWhenCenterParented;
                    ApplyMmdIkDeltaGuardRuntimeOverride(
                        vmdRecorder,
                        _mmdIkDeltaGuardLimitOverrideVmd,
                        _mmdIkDeltaGuardRecoveryTriggerVmd,
                        _mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                        _mmdIkDeltaGuardRecoveryHoldFrames);
                    vmdRecorder.IgnoreInitialPosition = true;
                    if (_activeJob.Mode == CaptureMode.MainRecordingVmdPlaybackProbe &&
                        !ApplyVmdPlaybackProbeRuntimeOverride(
                            _activeRecorder.gameObject,
                            _vmdPlaybackProbeSourceVmdPath,
                            vmdRecorder,
                            _applyVmdPlaybackProbeIkTargetsRuntimeOverride))
                    {
                        throw new InvalidOperationException(
                            $"VMD playback probe source is not ready: {_vmdPlaybackProbeSourceVmdPath}");
                    }
                }
            }

            _activeFileManager.EditorDiagnosticSmokeFinished -= HandleMainSceneFinished;
            _activeFileManager.EditorDiagnosticSmokeFinished += HandleMainSceneFinished;
            SavePersistedState();

            ReferenceMmdTimingPlan timingPlan = BuildReferenceMmdTimingPlan(
                _referenceClip != null ? _referenceClip.length : 0f,
                _durationSeconds,
                _editorDiagnosticSmokeSegment,
                _enableReferenceMmdTimingRuntimeOverride);
            float referenceClipStartSeconds = timingPlan.ReferenceMp4StartSeconds;
            float[] referenceLocalSampleSeconds = LoadReferenceMp4CurrentClipLocalSampleSeconds(
                referenceClipStartSeconds,
                _durationSeconds);
            float[] probeSampleTimes = BuildReferenceMp4AlignedProbeSampleTimes(
                timingPlan.CandidateClipStartSeconds,
                _durationSeconds,
                referenceLocalSampleSeconds,
                timingPlan.CandidateClipSecondsPerReferenceSecond);
            bool started = _activeFileManager.StartEditorDiagnosticSmoke(
                _fbxFileName,
                _durationSeconds,
                _targetFrameCount,
                enableDiagnostics: true,
                enableFingerCloseups: _enableFingerCloseups,
                useDeterministicCaptureFramerate: true,
                diagnosticStartDelay: DefaultStartDelaySeconds,
                segment: _editorDiagnosticSmokeSegment,
                sampleTimesOverride: probeSampleTimes,
                captureWidthOverride: _diagnosticCaptureWidthOverride,
                captureHeightOverride: _diagnosticCaptureHeightOverride,
                diagnosticScreenshotPaddingOverride: _diagnosticScreenshotPaddingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride: _diagnosticScreenshotVerticalViewportCenterOverride,
                recordingStartTimeOverrideSeconds: timingPlan.HasCandidateTimingOverride
                    ? timingPlan.CandidateClipStartSeconds
                    : float.NaN,
                recordingPlaybackSpeedOverride: timingPlan.HasCandidateTimingOverride
                    ? timingPlan.CandidateClipSecondsPerReferenceSecond
                    : float.NaN);

            if (!started)
            {
                throw new InvalidOperationException("FileManager.StartEditorDiagnosticSmoke가 false를 반환했습니다.");
            }

            Debug.Log($"[YybVisualComparisonBatchRunner] 시작됨: {_activeJob.DisplayName}");
            AppendRunnerTrace(
                $"job started scene={_activeJob.SceneName} display={_activeJob.DisplayName} " +
                $"segment={GetEditorDiagnosticSmokeSegmentLabel(_editorDiagnosticSmokeSegment)} " +
                $"referenceClipStart={referenceClipStartSeconds:F3}s " +
                $"candidateClipStart={timingPlan.CandidateClipStartSeconds:F3}s " +
                $"candidateScale={timingPlan.CandidateClipSecondsPerReferenceSecond:F5} " +
                $"referenceTiming={timingPlan.Enabled} probeSamples={FormatProbeSampleTimes(probeSampleTimes)}");
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd)
        {
            return ApplyMmdIkDeltaGuardRuntimeOverride(
                recorder,
                overrideLimitVmd,
                NoMmdIkDeltaGuardLimitOverrideVmd);
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd)
        {
            return ApplyMmdIkDeltaGuardRuntimeOverride(
                recorder,
                overrideLimitVmd,
                recoveryTriggerVmd,
                NoMmdIkDeltaGuardLimitOverrideVmd);
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd)
        {
            return ApplyMmdIkDeltaGuardRuntimeOverride(
                recorder,
                overrideLimitVmd,
                recoveryTriggerVmd,
                recoveryDebtThresholdVmd,
                NoMmdIkDeltaGuardRecoveryHoldFrames);
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd,
            int recoveryHoldFrames)
        {
            float normalizedLimit = NormalizeMmdIkDeltaGuardLimitOverride(overrideLimitVmd);
            if (recorder == null || !HasMmdIkDeltaGuardLimitOverride(normalizedLimit))
            {
                return false;
            }

            recorder.ClampMmdIkExportDeltaSpikes = true;
            float normalizedRecoveryTrigger = NormalizeMmdIkDeltaGuardLimitOverride(recoveryTriggerVmd);
            if (HasMmdIkDeltaGuardLimitOverride(normalizedRecoveryTrigger))
            {
                recorder.UseMmdIkExportDeltaRecoveryLimit = true;
                recorder.MmdIkExportDeltaRecoveryLimitPerFrame = normalizedLimit;
                recorder.MmdIkExportDeltaRecoveryTriggerPerFrame = normalizedRecoveryTrigger;
                recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame =
                    NormalizeMmdIkDeltaGuardLimitOverride(recoveryDebtThresholdVmd);
                recorder.MmdIkExportDeltaRecoveryHoldFrames =
                    NormalizeMmdIkDeltaGuardRecoveryHoldFrames(recoveryHoldFrames);
                return true;
            }

            recorder.UseMmdIkExportDeltaRecoveryLimit = false;
            recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame = 0f;
            recorder.MmdIkExportDeltaRecoveryHoldFrames = 0;
            recorder.MaxMmdFootIkExportDeltaPerFrame = normalizedLimit;
            recorder.MaxMmdToeIkExportDeltaPerFrame = normalizedLimit;
            return true;
        }

        private static bool ApplyFinalIkFootGroundingRuntimeOverride(FileManager fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableFinalIkFootGroundingExperiment = enabled;

            if (!enabled && fileManager.targetCharacter != null)
            {
                GrounderBipedIK grounder = fileManager.targetCharacter.GetComponent<GrounderBipedIK>();
                if (grounder != null)
                {
                    grounder.weight = 0f;
                    grounder.enabled = false;
                }

                BipedIK bipedIk = fileManager.targetCharacter.GetComponent<BipedIK>();
                if (bipedIk != null)
                {
                    bipedIk.fixTransforms = false;
                    bipedIk.enabled = false;
                }
            }

            return true;
        }

        private static bool ApplyVmdPlaybackProbeRuntimeOverride(
            GameObject target,
            string sourceVmdPath,
            UnityHumanoidVMDRecorder recorder,
            bool applyIkTargets)
        {
            if (target == null ||
                string.IsNullOrWhiteSpace(sourceVmdPath) ||
                !File.Exists(sourceVmdPath))
            {
                return false;
            }

            VmdPlaybackProbe probe = target.GetComponent<VmdPlaybackProbe>();
            if (probe == null)
            {
                probe = target.AddComponent<VmdPlaybackProbe>();
            }

            bool useCenterAsParentOfAll = recorder != null && recorder.UseCenterAsParentOfAll;
            bool routeCenterBoneToGroove = recorder != null && recorder.RouteHumanoidCenterToGroove;
            probe.ConfigureRuntimePlayback(
                sourceVmdPath,
                useCenterAsParentOfAll,
                routeCenterBoneToGroove,
                applyIkTargets);
            return probe.PlaybackEnabled && probe.ApplyIkTargets == applyIkTargets;
        }

        private static bool ApplyMainSceneRuntimeOverrides(FileManager fileManager)
        {
            if (fileManager == null)
            {
                return false;
            }

            if (_enableFinalIkFootGroundingRuntimeOverride)
            {
                ApplyFinalIkFootGroundingRuntimeOverride(fileManager, true);
            }

            if (_disableManualAnimatorFootLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorFootLocalRotationRuntimeOverride(fileManager, false);
            }
            else if (_enableManualAnimatorFootLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorFootLocalRotationRuntimeOverride(fileManager, true);
            }

            if (_disableManualAnimatorFullBodyPoseRuntimeOverride)
            {
                ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorFullBodyPoseReferenceWeight,
                    false,
                    false);
            }
            else if (_enableManualAnimatorFullBodyPoseRuntimeOverride ||
                     _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride ||
                     _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride)
            {
                ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorFullBodyPoseReferenceWeight,
                    _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                    _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride);
            }

            if (_disableManualAnimatorBodyRotationRuntimeOverride)
            {
                ApplyManualAnimatorBodyRotationRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorBodyRotationReferenceWeight);
            }
            else if (_enableManualAnimatorBodyRotationRuntimeOverride)
            {
                ApplyManualAnimatorBodyRotationRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorBodyRotationReferenceWeight);
            }

            if (_enableManualAnimatorHandLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorHandLocalRotationRuntimeOverride(fileManager, true);
            }

            if (_enableManualAnimatorThumbLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorThumbLocalRotationRuntimeOverride(fileManager, true);
            }

            if (_enableManualAnimatorHandPalmFrameRuntimeOverride)
            {
                ApplyManualAnimatorHandPalmFrameRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorHandPalmFrameWeight);
            }

            if (_overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings)
            {
                ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
                    fileManager,
                    _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                    _retargetPoseVisualSpikeCurrentWeight,
                    _retargetPoseVisualSpikeForearmStretchClampMaxOffset);
            }

            if (_enableYybArmSwingLimitRuntimeOverride)
            {
                ApplyYybArmSwingLimitRuntimeOverride(
                    fileManager,
                    true,
                    _yybArmSwingLimitWeight,
                    _yybArmSwingMaxDownDot,
                    _yybArmSwingMinHandHorizontalRatio,
                    _yybArmSwingMaxHandBelowShoulderRatio,
                    _yybArmSwingHorizontalReachLimitWeight,
                    _yybArmSwingMaxHandHorizontalReachRatio,
                    _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                    _yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    _yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                    _yybArmSwingRaisedPoseMinUpperArmDownDot,
                    _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                    _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
            }

            if (_enableYybArmDirectionRetargetRuntimeOverride)
            {
                ApplyYybArmDirectionRetargetRuntimeOverride(
                    fileManager,
                    true,
                    _yybArmDirectionUpperArmWeight,
                    _yybArmDirectionForearmWeight,
                    _yybArmDirectionUpperArmMaxDegrees,
                    _yybArmDirectionForearmMaxDegrees,
                    _yybArmDirectionLeftSideWeightScale,
                    _yybArmDirectionRightSideWeightScale);
            }

            if (_overrideYybArmSleeveAnchorRuntimeSettings)
            {
                ApplyYybArmSleeveAnchorRuntimeOverride(
                    fileManager,
                    _enableYybArmSleeveAnchorRuntimeOverride,
                    _yybArmSleeveAnchorInfluence,
                    _yybArmShoulderCapAnchorInfluence,
                    _yybArmSleeveAnchorMaxDegrees);
            }

            if (_overrideYybArmVisualTwistRuntimeSettings)
            {
                ApplyYybArmVisualTwistRuntimeOverride(
                    fileManager,
                    _enableYybArmVisualTwistRuntimeOverride,
                    _yybArmVisualUpperArmInfluence,
                    _yybArmVisualForearmInfluence,
                    _yybArmVisualUpperArmMaxDegrees,
                    _yybArmVisualForearmMaxDegrees);
            }

            if (_disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride)
            {
                ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                    _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                    _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                    _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                    _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                    _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            }
            else if (_enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride)
            {
                ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                    _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                    _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                    _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                    _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                    _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            }
            else if (HasManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride())
            {
                ApplyManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride(fileManager);
            }

            if (_disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride)
            {
                ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            }
            else if (_enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride)
            {
                ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            }

            if (_enablePostSetHumanPoseRightEndpointPositionRuntimeOverride)
            {
                ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    fileManager,
                    true,
                    _postSetHumanPoseRightEndpointPositionReferenceWeight,
                    _postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    _postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                    _postSetHumanPoseEndpointPositionUseLeftSide,
                    _usePostSetHumanPoseRightFootEvaluatorXzReference,
                    _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            }

            if (_enablePreSetHumanPoseRightEndpointPositionRuntimeOverride)
            {
                ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    fileManager,
                    true,
                    _preSetHumanPoseRightEndpointPositionReferenceWeight,
                    _preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    _preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                    _preSetHumanPoseEndpointPositionUseLeftSide,
                    _preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                    _preSetHumanPoseEndpointPositionInvertBodyPositionX,
                    _preSetHumanPoseEndpointPositionInvertBodyPositionZ);
            }

            if (_enableManualAnimatorBipedIkFootPositionRuntimeOverride)
            {
                ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorBipedIkFootPositionReferenceWeight,
                    _manualAnimatorBipedIkFootPositionReferenceMaxOffset);
            }

            if (_enableManualAnimatorHipsLocalPositionRuntimeOverride)
            {
                ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorHipsLocalPositionReferenceWeight,
                    _manualAnimatorHipsLocalPositionReferenceMaxOffset);
            }

            if (_enableManualAnimatorBodyPositionXzRuntimeOverride)
            {
                ApplyManualAnimatorBodyPositionXzRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorBodyPositionXzReferenceWeight,
                    _manualAnimatorBodyPositionXzReferenceMaxOffset,
                    _manualAnimatorBodyPositionXzReferenceFrameGateStart,
                    _manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                    _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                    _manualAnimatorBodyPositionXzReferenceAxisXScale,
                    _manualAnimatorBodyPositionXzReferenceAxisZScale);
            }

            if (_enableRetargetBodyPositionXzRootMotionRuntimeOverride)
            {
                ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(fileManager, true);
            }

            if (_disableTargetHumanoidBonePositionLockRuntimeOverride)
            {
                ApplyTargetHumanoidBonePositionLockRuntimeOverride(fileManager, false);
            }

            return true;
        }

        private static bool ApplyManualAnimatorFootLocalRotationRuntimeOverride(FileManager fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorFootLocalRotationReference = enabled;
            fileManager.manualAnimatorFootLocalRotationReferenceWeight = enabled ? 1f : 0f;
            return true;
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(FileManager fileManager, bool enabled)
        {
            return ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorFullBodyPoseReferenceWeight);
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight)
        {
            return ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                fileManager,
                enabled,
                weight,
                false,
                false);
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            bool excludeLowerBodyMuscles,
            bool lowerBodyMusclesOnly = false,
            bool legTwistMusclesOnly = false)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorFullBodyPoseReference = enabled;
            fileManager.manualAnimatorFullBodyPoseReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorFullBodyPoseExcludeLowerBodyMuscles = enabled && excludeLowerBodyMuscles;
            fileManager.manualAnimatorFullBodyPoseLowerBodyMusclesOnly = enabled && lowerBodyMusclesOnly;
            fileManager.manualAnimatorFullBodyPoseLegTwistMusclesOnly = enabled && legTwistMusclesOnly;
            return true;
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(FileManager fileManager, bool enabled)
        {
            return ApplyManualAnimatorBodyRotationRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorBodyRotationReferenceWeight);
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorBodyRotationReference = enabled;
            fileManager.manualAnimatorBodyRotationReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            return true;
        }

        private static bool ApplyManualAnimatorHandLocalRotationRuntimeOverride(FileManager fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorHandLocalRotationReference = enabled;
            return true;
        }

        private static bool ApplyManualAnimatorThumbLocalRotationRuntimeOverride(FileManager fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorThumbLocalRotationReference = enabled;
            return true;
        }

        private static bool ApplyManualAnimatorHandPalmFrameRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorHandPalmFrameReference = enabled;
            fileManager.manualAnimatorHandPalmFrameWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            return true;
        }

        private static bool ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float currentWeight,
            float forearmStretchClampMaxOffset)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.smoothRetargetPoseOnVisualStepSpike = enabled;
            fileManager.RetargetPoseVisualSpikeCurrentWeight = Mathf.Clamp(currentWeight, 0.1f, 1f);
            fileManager.RetargetPoseVisualSpikeForearmStretchClampMaxOffset =
                Mathf.Clamp01(forearmStretchClampMaxOffset);
            return true;
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                DefaultYybArmSwingHorizontalReachLimitWeight,
                DefaultYybArmSwingMaxHandHorizontalReachRatio,
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply,
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
                DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                horizontalReachLimitWeight,
                maxHandHorizontalReachRatio,
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio,
            float horizontalReachMaxHandBelowShoulderRatio)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                horizontalReachLimitWeight,
                maxHandHorizontalReachRatio,
                horizontalReachMaxHandBelowShoulderRatio,
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio,
            float horizontalReachMaxHandBelowShoulderRatio,
            float horizontalReachMinElbowAngleAfterApply)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                horizontalReachLimitWeight,
                maxHandHorizontalReachRatio,
                horizontalReachMaxHandBelowShoulderRatio,
                horizontalReachMinElbowAngleAfterApply,
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
                DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio,
            float horizontalReachMaxHandBelowShoulderRatio,
            float horizontalReachMinElbowAngleAfterApply,
            float raisedPoseHorizontalReachLimitWeight = DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
            float raisedPoseMinUpperArmDownDot = DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
            float raisedPoseMaxHandBelowShoulderRatio = DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
            float raisedPoseMaxHandHorizontalReachRatio = DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmSwingLimitCorrection = enabled;
            fileManager.YybArmSwingLimitWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.YybArmSwingMaxDownDot = Mathf.Clamp01(maxDownDot);
            fileManager.YybArmSwingMinHandHorizontalRatio = Mathf.Clamp(minHandHorizontalRatio, 0f, 1.5f);
            fileManager.YybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp(maxHandBelowShoulderRatio, 0f, 1.5f);
            fileManager.YybArmSwingHorizontalReachLimitWeight = enabled
                ? Mathf.Clamp01(horizontalReachLimitWeight)
                : 0f;
            fileManager.YybArmSwingMaxHandHorizontalReachRatio = enabled
                ? Mathf.Clamp(maxHandHorizontalReachRatio, 0f, 1.5f)
                : 0f;
            fileManager.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio = enabled
                ? Mathf.Clamp(horizontalReachMaxHandBelowShoulderRatio, 0f, 1.5f)
                : 0f;
            fileManager.YybArmSwingHorizontalReachMinElbowAngleAfterApply = enabled
                ? Mathf.Clamp(horizontalReachMinElbowAngleAfterApply, 0f, 180f)
                : 0f;
            fileManager.YybArmSwingRaisedPoseHorizontalReachLimitWeight = enabled
                ? Mathf.Clamp01(raisedPoseHorizontalReachLimitWeight)
                : 0f;
            fileManager.YybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01(raisedPoseMinUpperArmDownDot);
            fileManager.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp(
                raisedPoseMaxHandBelowShoulderRatio,
                0f,
                1.5f);
            fileManager.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio = enabled
                ? Mathf.Clamp(raisedPoseMaxHandHorizontalReachRatio, 0f, 1.5f)
                : 0f;
            return true;
        }

        private static bool ApplyYybArmDirectionRetargetRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            return ApplyYybArmDirectionRetargetRuntimeOverride(
                fileManager,
                enabled,
                upperArmWeight,
                forearmWeight,
                upperArmMaxDegrees,
                forearmMaxDegrees,
                DefaultYybArmDirectionLeftSideWeightScale,
                DefaultYybArmDirectionRightSideWeightScale);
        }

        private static bool ApplyYybArmDirectionRetargetRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees,
            float leftSideWeightScale,
            float rightSideWeightScale)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmDirectionRetargetCorrection = enabled;
            fileManager.YybArmDirectionUpperArmWeight = enabled ? Mathf.Clamp01(upperArmWeight) : 0f;
            fileManager.YybArmDirectionForearmWeight = enabled ? Mathf.Clamp01(forearmWeight) : 0f;
            fileManager.YybArmDirectionUpperArmMaxDegrees = Mathf.Clamp(upperArmMaxDegrees, 0f, 120f);
            fileManager.YybArmDirectionForearmMaxDegrees = Mathf.Clamp(forearmMaxDegrees, 0f, 120f);
            fileManager.YybArmDirectionLeftSideWeightScale = enabled ? Mathf.Clamp01(leftSideWeightScale) : 0f;
            fileManager.YybArmDirectionRightSideWeightScale = enabled ? Mathf.Clamp01(rightSideWeightScale) : 0f;
            return true;
        }

        private static bool ApplyYybArmSleeveAnchorRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float sleeveInfluence,
            float shoulderCapInfluence,
            float maxDegrees)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmSleeveAnchorCorrection = enabled;
            fileManager.YybArmSleeveAnchorInfluence = enabled ? Mathf.Clamp01(sleeveInfluence) : 0f;
            fileManager.YybArmShoulderCapAnchorInfluence = enabled ? Mathf.Clamp01(shoulderCapInfluence) : 0f;
            fileManager.YybArmSleeveAnchorMaxDegrees = Mathf.Clamp(maxDegrees, 0f, 120f);
            return true;
        }

        private static bool ApplyYybArmVisualTwistRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float upperArmInfluence,
            float forearmInfluence,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmVisualTwistCorrection = enabled;
            fileManager.YybArmVisualUpperArmInfluence = enabled ? Mathf.Clamp01(upperArmInfluence) : 0f;
            fileManager.YybArmVisualForearmInfluence = enabled ? Mathf.Clamp01(forearmInfluence) : 0f;
            fileManager.YybArmVisualUpperArmMaxDegrees = Mathf.Clamp(upperArmMaxDegrees, 0f, 120f);
            fileManager.YybArmVisualForearmMaxDegrees = Mathf.Clamp(forearmMaxDegrees, 0f, 120f);
            return true;
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                leftLowerLegToFootMaxAngle,
                rightLowerLegToFootMaxAngle,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes,
                footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg: false,
                upperLegToLowerLegMaxAngle: DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableLowerLegToFoot: false,
                lowerLegToFootMaxAngle: DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                leftLowerLegToFootMaxAngle: DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootMaxAngle: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootAxisXzScale: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                rightLowerLegToFootBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                rightLowerLegToFootFrameGateStart: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                rightLowerLegToFootFrameGateEnd: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                rightLowerLegToFootEndpointBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes: false,
                footToToesMaxAngle: DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg: false,
                upperLegToLowerLegMaxAngle: DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableLowerLegToFoot: false,
                lowerLegToFootMaxAngle: DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                leftLowerLegToFootMaxAngle: DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootMaxAngle: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootAxisXzScale: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                rightLowerLegToFootBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                rightLowerLegToFootFrameGateStart: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                rightLowerLegToFootFrameGateEnd: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                rightLowerLegToFootEndpointBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes: disableFootToToes,
                footToToesMaxAngle: footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            float rightLowerLegToFootAxisXzScale,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                leftLowerLegToFootMaxAngle,
                rightLowerLegToFootMaxAngle,
                rightLowerLegToFootAxisXzScale,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes,
                footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                leftLowerLegToFootMaxAngle: 0f,
                rightLowerLegToFootMaxAngle: 0f,
                rightLowerLegToFootAxisXzScale: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                rightLowerLegToFootBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                rightLowerLegToFootFrameGateStart: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                rightLowerLegToFootFrameGateEnd: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                rightLowerLegToFootEndpointBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes: disableFootToToes,
                footToToesMaxAngle: footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            float rightLowerLegToFootAxisXzScale,
            float rightLowerLegToFootBlendWeight,
            float rightLowerLegToFootFrameGateStart,
            float rightLowerLegToFootFrameGateEnd,
            float rightLowerLegToFootEndpointBlendWeight,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorLowerBodySegmentDirectionReference = enabled;
            fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = Mathf.Max(0f, maxAngle);
            fileManager.disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                enabled && disableUpperLegToLowerLeg;
            fileManager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, upperLegToLowerLegMaxAngle);
            fileManager.disableManualAnimatorLowerLegToFootSegmentDirectionReference =
                enabled && disableLowerLegToFoot;
            fileManager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, lowerLegToFootMaxAngle);
            fileManager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, leftLowerLegToFootMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, rightLowerLegToFootMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                Mathf.Clamp01(rightLowerLegToFootAxisXzScale);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                Mathf.Clamp01(rightLowerLegToFootBlendWeight);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                Mathf.Max(0f, rightLowerLegToFootFrameGateStart);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                Mathf.Max(0f, rightLowerLegToFootFrameGateEnd);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                Mathf.Clamp01(rightLowerLegToFootEndpointBlendWeight);
            fileManager.disableManualAnimatorFootToToesSegmentDirectionReference = enabled && disableFootToToes;
            fileManager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = Mathf.Max(0f, footToToesMaxAngle);
            return true;
        }

        private static bool HasManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride()
        {
            return _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride ||
                _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle > 0f ||
                _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride ||
                _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle > 0f ||
                _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle > 0f ||
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle > 0f ||
                Mathf.Abs(
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale -
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale) > 0.0001f ||
                Mathf.Abs(
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight -
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight) > 0.0001f ||
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart > 0f ||
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd > 0f ||
                Mathf.Abs(
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight -
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight) > 0.0001f ||
                _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride ||
                _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle > 0f;
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride(
            FileManager fileManager)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            fileManager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            fileManager.disableManualAnimatorLowerLegToFootSegmentDirectionReference =
                _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            fileManager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            fileManager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                Mathf.Clamp01(_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                Mathf.Clamp01(_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                Mathf.Max(0f, _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                Mathf.Max(0f, _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                Mathf.Clamp01(_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
            fileManager.disableManualAnimatorFootToToesSegmentDirectionReference =
                _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            fileManager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            return true;
        }

        private static bool ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxAngle)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorFootHipsAlignedResidualYawReference = enabled;
            fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = Mathf.Max(0f, maxAngle);
            return true;
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(FileManager fileManager, bool enabled)
        {
            return ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight,
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float frameGateStart,
            float frameGateEnd)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                frameGateStart,
                frameGateEnd);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide = false)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                frameGateStart,
                frameGateEnd,
                useLeftSide,
                evaluatorXzReferenceEnabled: false,
                evaluatorXzTargetMagnitude:
                    DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide,
            bool evaluatorXzReferenceEnabled,
            float evaluatorXzTargetMagnitude)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.usePostSetHumanPoseRightEndpointPositionReference = enabled;
            fileManager.postSetHumanPoseRightEndpointPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.postSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            fileManager.postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(positiveZScale);
            fileManager.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(toesBlendWeight);
            fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            fileManager.postSetHumanPoseEndpointPositionUseLeftSide = enabled && useLeftSide;
            fileManager.usePostSetHumanPoseRightFootEvaluatorXzReference =
                enabled && evaluatorXzReferenceEnabled;
            fileManager.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                Mathf.Max(0f, evaluatorXzTargetMagnitude);
            return true;
        }

        private static bool ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide,
            bool useGhostCurrentBasis,
            bool invertBodyPositionX,
            bool invertBodyPositionZ)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.usePreSetHumanPoseRightEndpointPositionReference = enabled;
            fileManager.preSetHumanPoseRightEndpointPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.preSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            fileManager.preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(positiveZScale);
            fileManager.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(toesBlendWeight);
            fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            fileManager.preSetHumanPoseEndpointPositionUseLeftSide = enabled && useLeftSide;
            fileManager.preSetHumanPoseEndpointPositionUseGhostCurrentBasis = enabled && useGhostCurrentBasis;
            fileManager.preSetHumanPoseEndpointPositionInvertBodyPositionX = enabled && invertBodyPositionX;
            fileManager.preSetHumanPoseEndpointPositionInvertBodyPositionZ = enabled && invertBodyPositionZ;
            return true;
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorBipedIkFootPositionReference = enabled;
            fileManager.manualAnimatorBipedIkFootPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorBipedIkFootPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            return true;
        }

        private static bool ApplyManualAnimatorHipsLocalPositionRuntimeOverride(FileManager fileManager, bool enabled)
        {
            return ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorHipsLocalPositionReferenceWeight,
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset);
        }

        private static bool ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorHipsLocalPositionReference = enabled;
            fileManager.manualAnimatorHipsLocalPositionWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorHipsLocalPositionMaxOffset = Mathf.Max(0f, maxOffset);
            return true;
        }

        private static bool ApplyManualAnimatorBodyPositionXzRuntimeOverride(
            FileManager fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float frameGateStart,
            float frameGateEnd,
            float frameGateBlendFrames,
            float axisXScale,
            float axisZScale)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorBodyPositionXzReference = enabled;
            fileManager.manualAnimatorBodyPositionXzReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorBodyPositionXzReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            fileManager.manualAnimatorBodyPositionXzReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            fileManager.manualAnimatorBodyPositionXzReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            fileManager.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = Mathf.Max(0f, frameGateBlendFrames);
            fileManager.manualAnimatorBodyPositionXzReferenceAxisXScale = Mathf.Clamp01(axisXScale);
            fileManager.manualAnimatorBodyPositionXzReferenceAxisZScale = Mathf.Clamp01(axisZScale);
            return true;
        }

        private static bool ApplyTargetHumanoidBonePositionLockRuntimeOverride(
            FileManager fileManager,
            bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.lockTargetHumanoidBonePositions = enabled;
            return true;
        }

        private static bool ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(
            FileManager fileManager,
            bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useRetargetBodyPositionXZRootMotion = enabled;
            return true;
        }

        private static float NormalizeMmdIkDeltaGuardLimitOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return NoMmdIkDeltaGuardLimitOverrideVmd;
            }

            return value;
        }

        private static float NormalizePositiveFloat(float value, float fallbackValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallbackValue;
            }

            return value;
        }

        private static float NormalizeFiniteFloat(float value, float fallbackValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallbackValue;
            }

            return value;
        }

        private static bool HasMmdIkDeltaGuardLimitOverride(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool HasDiagnosticScreenshotFramingOverride(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int NormalizeMmdIkDeltaGuardRecoveryHoldFrames(int value)
        {
            return value > 0 ? value : 0;
        }

        private static int NormalizeDiagnosticCaptureDimensionOverride(int value)
        {
            return value > 0 ? value : NoDiagnosticCaptureDimensionOverride;
        }

        private static float NormalizeDiagnosticScreenshotPaddingOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return NoDiagnosticScreenshotFramingOverride;
            }

            return Mathf.Clamp(value, 0.25f, 2f);
        }

        private static float NormalizeDiagnosticScreenshotVerticalViewportCenterOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return NoDiagnosticScreenshotFramingOverride;
            }

            return Mathf.Clamp01(value);
        }

        private static FileManager.EditorDiagnosticSmokeSegment ResolveEditorDiagnosticSmokeSegment(string value)
        {
            if (string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase))
            {
                return FileManager.EditorDiagnosticSmokeSegment.Middle;
            }

            if (string.Equals(value, "tail", StringComparison.OrdinalIgnoreCase))
            {
                return FileManager.EditorDiagnosticSmokeSegment.Tail;
            }

            return FileManager.EditorDiagnosticSmokeSegment.Head;
        }

        private static ManualAnimatorCapturePlan BuildManualAnimatorCapturePlan(
            string labelSuffix,
            string fbxFileName,
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FileManager.EditorDiagnosticSmokeSegment segment)
        {
            float clipLength = Mathf.Max(0.1f, referenceClipLengthSeconds);
            float requestedDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float startTime = CalculateEditorDiagnosticSmokeStartTime(clipLength, requestedDuration, segment);
            float remainingLength = Mathf.Max(0.1f, clipLength - startTime);
            float captureDuration = Mathf.Min(requestedDuration, remainingLength);
            int targetFrameCount = Mathf.Max(1, Mathf.CeilToInt(captureDuration * DefaultFrameRate));
            string segmentToken = segment == FileManager.EditorDiagnosticSmokeSegment.Head
                ? string.Empty
                : $"_{GetEditorDiagnosticSmokeSegmentLabel(segment)}";
            string outputBaseName =
                $"{labelSuffix}_{Path.GetFileNameWithoutExtension(fbxFileName)}{segmentToken}_{Mathf.CeilToInt(captureDuration)}s_animtime";

            return new ManualAnimatorCapturePlan
            {
                StartTimeSeconds = startTime,
                DurationSeconds = captureDuration,
                TargetFrameCount = targetFrameCount,
                OutputBaseName = outputBaseName,
                ComparisonLabel = $"manual_{outputBaseName}"
            };
        }

        private static float CalculateEditorDiagnosticSmokeStartTime(
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FileManager.EditorDiagnosticSmokeSegment segment)
        {
            float clipLength = Mathf.Max(0.1f, referenceClipLengthSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            switch (segment)
            {
                case FileManager.EditorDiagnosticSmokeSegment.Middle:
                    return Mathf.Max(0f, (clipLength - safeDuration) * 0.5f);
                case FileManager.EditorDiagnosticSmokeSegment.Tail:
                    return Mathf.Max(0f, clipLength - safeDuration);
                default:
                    return 0f;
            }
        }

        private static string GetEditorDiagnosticSmokeSegmentLabel(FileManager.EditorDiagnosticSmokeSegment segment)
        {
            switch (segment)
            {
                case FileManager.EditorDiagnosticSmokeSegment.Middle:
                    return "middle";
                case FileManager.EditorDiagnosticSmokeSegment.Tail:
                    return "tail";
                default:
                    return "head";
            }
        }

        private static string FormatRuntimeOverride(float value)
        {
            return HasMmdIkDeltaGuardLimitOverride(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : "none";
        }

        private static string FormatRuntimeOverride(int value)
        {
            return value > 0
                ? value.ToString(CultureInfo.InvariantCulture)
                : "none";
        }

        private static string FormatDiagnosticScreenshotFramingOverride(float value)
        {
            return HasDiagnosticScreenshotFramingOverride(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : "none";
        }

        private static void StartSubManualJob(string targetNameToken)
        {
            _activeRecorder = SelectActiveManualRecorder(targetNameToken);
            if (_activeRecorder == null)
            {
                throw new InvalidOperationException($"Sub_Manual 수동 기준 대상을 찾지 못했습니다: {targetNameToken}");
            }
            if (!_activeRecorder.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException($"Sub_Manual 수동 기준 대상이 비활성 상태입니다: {GetHierarchyPath(_activeRecorder.transform)}");
            }

            Animator animator = _activeRecorder.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException($"Animator가 없습니다: {GetHierarchyPath(_activeRecorder.transform)}");
            }

            string labelSuffix = _activeJob.Mode == CaptureMode.SubManualTestPrefab
                ? ManualTestPrefabLabelSuffix
                : ManualYybLabelSuffix;
            ManualAnimatorCapturePlan capturePlan = BuildManualAnimatorCapturePlan(
                labelSuffix,
                _fbxFileName,
                _referenceClip.length,
                _durationSeconds,
                _editorDiagnosticSmokeSegment);
            PrepareManualAnimator(animator, _referenceClip, capturePlan.StartTimeSeconds);
            UnityHumanoidVMDRecorder vmdRecorder = _activeRecorder.GetComponent<UnityHumanoidVMDRecorder>();
            if (vmdRecorder != null)
            {
                vmdRecorder.IgnoreInitialPosition = true;
            }
            float[] probeSampleTimes = BuildReferenceMp4AlignedProbeSampleTimes(
                capturePlan.StartTimeSeconds,
                capturePlan.DurationSeconds);
            _activeRecorder.SetRecordingDiagnostics(
                enableProbe: true,
                enableFingerCloseups: _enableFingerCloseups,
                useCaptureFramerateForRegression: true,
                sampleTimesOverride: probeSampleTimes);
            _activeRecorder.SetReady($"{_activeJob.DisplayName} 준비");

            _activeRecorder.RecordingFinished -= HandleManualFinished;
            _activeRecorder.RecordingFinished += HandleManualFinished;
            SavePersistedState();

            if (!_activeRecorder.StartAutoRecording(
                    capturePlan.DurationSeconds,
                    capturePlan.OutputBaseName,
                    null,
                    capturePlan.TargetFrameCount,
                    capturePlan.ComparisonLabel,
                    overwriteExistingOutput: true))
            {
                throw new InvalidOperationException("HumanoidSampleCode.StartAutoRecording이 false를 반환했습니다.");
            }

            animator.speed = 1f;
            string comparisonLabel = capturePlan.ComparisonLabel;
            Debug.Log($"[YybVisualComparisonBatchRunner] 시작됨: {_activeJob.DisplayName} / {comparisonLabel}");
            AppendRunnerTrace(
                $"job started scene={_activeJob.SceneName} display={_activeJob.DisplayName} " +
                $"label={capturePlan.ComparisonLabel} segment={GetEditorDiagnosticSmokeSegmentLabel(_editorDiagnosticSmokeSegment)} " +
                $"start={capturePlan.StartTimeSeconds:F2}s duration={capturePlan.DurationSeconds:F2}s " +
                $"probeSamples={FormatProbeSampleTimes(probeSampleTimes)}");
        }

        private static void PrepareManualAnimator(Animator animator, AnimationClip clip, float startTimeSeconds)
        {
            RuntimeAnimatorController baseController = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController
                : _fallbackController;
            if (baseController == null)
            {
                throw new InvalidOperationException("Animator Override 기준 Controller가 없습니다.");
            }

            AnimatorOverrideController overrideController = new AnimatorOverrideController(baseController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            if (overrides.Count > 0 && overrides[0].Key != null)
            {
                overrideController[overrides[0].Key] = clip;
            }

            animator.runtimeAnimatorController = overrideController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            animator.speed = 0f;
            animator.Rebind();
            animator.Update(0f);
            float normalizedStartTime = clip != null && clip.length > 0f
                ? Mathf.Clamp01(startTimeSeconds / clip.length)
                : 0f;
            animator.Play(0, 0, normalizedStartTime);
            animator.Update(0f);
        }

        private static HumanoidSampleCode FindManualRecorder(string targetNameToken)
        {
            HumanoidSampleCode[] recorders = UnityEngine.Object.FindObjectsOfType<HumanoidSampleCode>(true);
            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                string hierarchyPath = GetHierarchyPath(recorder.transform);
                if (hierarchyPath.IndexOf(targetNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return recorder;
                }
            }

            return null;
        }

        private static HumanoidSampleCode SelectActiveManualRecorder(string targetNameToken)
        {
            HumanoidSampleCode[] recorders = UnityEngine.Object.FindObjectsOfType<HumanoidSampleCode>(true);
            HumanoidSampleCode selected = null;
            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                string hierarchyPath = GetHierarchyPath(recorder.transform);
                if (hierarchyPath.IndexOf(targetNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selected = recorder;
                    break;
                }
            }

            if (selected == null)
            {
                return null;
            }

            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                recorder.gameObject.SetActive(ReferenceEquals(recorder, selected));
            }

            return selected;
        }

        private static void HandleMainSceneFinished(string fbxFileName, VmdSaveResult result)
        {
            if (_activeJob == null ||
                (_activeJob.Mode != CaptureMode.MainAuto &&
                    _activeJob.Mode != CaptureMode.MainRecording &&
                    _activeJob.Mode != CaptureMode.MainRecordingVmdPlaybackProbe) ||
                _activeJobFinished)
            {
                return;
            }

            MotionComparisonProbe probe = _activeRecorder != null
                ? _activeRecorder.GetComponent<MotionComparisonProbe>()
                : UnityEngine.Object.FindObjectOfType<MotionComparisonProbe>();
            VmdSaveResult stableResult = BuildStableCandidateResult(result);
            if (_activeJob.Mode == CaptureMode.MainRecording &&
                !string.IsNullOrWhiteSpace(stableResult.FilePath) &&
                File.Exists(stableResult.FilePath))
            {
                _vmdPlaybackProbeSourceVmdPath = stableResult.FilePath;
            }

            FinalizeActiveJob(
                stableResult,
                probe,
                targetName: _activeFileManager != null && _activeFileManager.targetCharacter != null
                ? _activeFileManager.targetCharacter.name
                : $"{_activeJob.SceneName} Target");
        }

        private static void HandleManualFinished(VmdSaveResult result)
        {
            if (_activeJob == null || _activeJobFinished)
            {
                return;
            }

            MotionComparisonProbe probe = _activeRecorder != null
                ? _activeRecorder.GetComponent<MotionComparisonProbe>()
                : null;
            string targetName = _activeRecorder != null ? _activeRecorder.gameObject.name : _activeJob.ManualTargetNameToken;
            FinalizeActiveJob(result, probe, targetName);
        }

        private static void FinalizeActiveJob(VmdSaveResult result, MotionComparisonProbe probe, string targetName)
        {
            _activeJobFinished = true;
            _activeJobStartedInPlayMode = false;

            var captureResult = new CaptureResult
            {
                jobMode = _activeJob.Mode.ToString(),
                jobDisplayName = _activeJob.DisplayName,
                sceneName = _activeJob.SceneName,
                comparisonLabel = probe != null ? probe.name : string.Empty,
                targetName = targetName,
                success = result.Success,
                error = result.Success ? string.Empty : result.ErrorMessage,
                vmdPath = MakeProjectRelativePath(result.FilePath),
                frameCount = result.FrameCount,
                fileSizeBytes = result.FileSizeBytes,
                comparisonSessionManifestPath = probe != null ? MakeProjectRelativePath(probe.LastSessionManifestPath) : string.Empty,
                comparisonMetricsCsvPath = probe != null ? MakeProjectRelativePath(probe.LastCsvPath) : string.Empty,
                comparisonFrameFolderPath = probe != null ? MakeProjectRelativePath(probe.LastScreenshotFolder) : string.Empty,
                comparisonFrameIndexPath = probe != null && !string.IsNullOrEmpty(probe.LastScreenshotFolder)
                    ? MakeProjectRelativePath(Path.Combine(probe.LastScreenshotFolder, "index.csv"))
                    : string.Empty,
                comparisonSessionId = probe != null && !string.IsNullOrEmpty(probe.LastSessionManifestPath)
                    ? Path.GetFileName(Path.GetDirectoryName(probe.LastSessionManifestPath))
                    : string.Empty
            };
            CaptureFileManagerEffectiveSettings(captureResult, _activeFileManager);
            Results.Add(captureResult);
            SavePersistedState();

            if (result.Success)
            {
                Debug.Log(
                    $"[YybVisualComparisonBatchRunner] 완료: {_activeJob.DisplayName}, " +
                    $"frames={result.FrameCount}, bytes={result.FileSizeBytes}, " +
                    $"session={captureResult.comparisonSessionId}");
                AppendRunnerTrace($"job completed display={_activeJob.DisplayName} session={captureResult.comparisonSessionId}");
            }
            else
            {
                RecordFailure($"{_activeJob.DisplayName} 실패: {result.ErrorMessage}");
            }

            RequestPlayModeStop();
        }

        private static void CaptureFileManagerEffectiveSettings(CaptureResult result, FileManager fileManager)
        {
            if (result == null || fileManager == null)
            {
                return;
            }

            result.hasFileManagerEffectiveSettings = true;
            result.useManualAnimatorFootLocalRotationReference =
                fileManager.useManualAnimatorFootLocalRotationReference;
            result.manualAnimatorFootLocalRotationReferenceWeight =
                fileManager.manualAnimatorFootLocalRotationReferenceWeight;
            result.useManualAnimatorFullBodyPoseReference =
                fileManager.useManualAnimatorFullBodyPoseReference;
            result.manualAnimatorFullBodyPoseReferenceWeight =
                fileManager.manualAnimatorFullBodyPoseReferenceWeight;
            result.manualAnimatorFullBodyPoseExcludeLowerBodyMuscles =
                fileManager.manualAnimatorFullBodyPoseExcludeLowerBodyMuscles;
            result.manualAnimatorFullBodyPoseLowerBodyMusclesOnly =
                fileManager.manualAnimatorFullBodyPoseLowerBodyMusclesOnly;
            result.manualAnimatorFullBodyPoseLegTwistMusclesOnly =
                fileManager.manualAnimatorFullBodyPoseLegTwistMusclesOnly;
            result.useManualAnimatorBodyRotationReference = fileManager.useManualAnimatorBodyRotationReference;
            result.manualAnimatorBodyRotationReferenceWeight = fileManager.manualAnimatorBodyRotationReferenceWeight;
            result.useManualAnimatorLowerBodySegmentDirectionReference =
                fileManager.useManualAnimatorLowerBodySegmentDirectionReference;
            result.manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            result.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            result.disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                fileManager.disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference;
            result.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            result.disableManualAnimatorLowerLegToFootSegmentDirectionReference =
                fileManager.disableManualAnimatorLowerLegToFootSegmentDirectionReference;
            result.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            result.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            result.disableManualAnimatorFootToToesSegmentDirectionReference =
                fileManager.disableManualAnimatorFootToToesSegmentDirectionReference;
            result.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            result.useManualAnimatorFootHipsAlignedResidualYawReference =
                fileManager.useManualAnimatorFootHipsAlignedResidualYawReference;
            result.manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            result.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            result.usePostSetHumanPoseRightEndpointPositionReference =
                fileManager.usePostSetHumanPoseRightEndpointPositionReference;
            result.postSetHumanPoseRightEndpointPositionReferenceWeight =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceWeight;
            result.postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            result.postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                fileManager.postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            result.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            result.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            result.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            result.postSetHumanPoseEndpointPositionUseLeftSide =
                fileManager.postSetHumanPoseEndpointPositionUseLeftSide;
            result.usePreSetHumanPoseRightEndpointPositionReference =
                fileManager.usePreSetHumanPoseRightEndpointPositionReference;
            result.preSetHumanPoseRightEndpointPositionReferenceWeight =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceWeight;
            result.preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            result.preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                fileManager.preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            result.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            result.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            result.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            result.preSetHumanPoseEndpointPositionUseLeftSide =
                fileManager.preSetHumanPoseEndpointPositionUseLeftSide;
            result.preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                fileManager.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            result.preSetHumanPoseEndpointPositionInvertBodyPositionX =
                fileManager.preSetHumanPoseEndpointPositionInvertBodyPositionX;
            result.preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                fileManager.preSetHumanPoseEndpointPositionInvertBodyPositionZ;
            result.usePostSetHumanPoseRightFootEvaluatorXzReference =
                fileManager.usePostSetHumanPoseRightFootEvaluatorXzReference;
            result.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                fileManager.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            result.useManualAnimatorBodyPositionXzReference =
                fileManager.useManualAnimatorBodyPositionXzReference;
            result.manualAnimatorBodyPositionXzReferenceWeight =
                fileManager.manualAnimatorBodyPositionXzReferenceWeight;
            result.manualAnimatorBodyPositionXzReferenceMaxOffset =
                fileManager.manualAnimatorBodyPositionXzReferenceMaxOffset;
            result.manualAnimatorBodyPositionXzReferenceFrameGateStart =
                fileManager.manualAnimatorBodyPositionXzReferenceFrameGateStart;
            result.manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                fileManager.manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            result.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                fileManager.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            result.manualAnimatorBodyPositionXzReferenceAxisXScale =
                fileManager.manualAnimatorBodyPositionXzReferenceAxisXScale;
            result.manualAnimatorBodyPositionXzReferenceAxisZScale =
                fileManager.manualAnimatorBodyPositionXzReferenceAxisZScale;
            result.enableYybArmSwingLimitCorrection = fileManager.enableYybArmSwingLimitCorrection;
            result.yybArmSwingLimitWeight = fileManager.YybArmSwingLimitWeight;
            result.yybArmSwingMaxDownDot = fileManager.YybArmSwingMaxDownDot;
            result.yybArmSwingMinHandHorizontalRatio = fileManager.YybArmSwingMinHandHorizontalRatio;
            result.yybArmSwingMaxHandBelowShoulderRatio = fileManager.YybArmSwingMaxHandBelowShoulderRatio;
            result.yybArmSwingHorizontalReachLimitWeight = fileManager.YybArmSwingHorizontalReachLimitWeight;
            result.yybArmSwingMaxHandHorizontalReachRatio = fileManager.YybArmSwingMaxHandHorizontalReachRatio;
            result.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                fileManager.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            result.yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                fileManager.YybArmSwingHorizontalReachMinElbowAngleAfterApply;
            result.yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                fileManager.YybArmSwingRaisedPoseHorizontalReachLimitWeight;
            result.yybArmSwingRaisedPoseMinUpperArmDownDot =
                fileManager.YybArmSwingRaisedPoseMinUpperArmDownDot;
            result.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                fileManager.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            result.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                fileManager.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            result.enableYybArmSleeveAnchorCorrection = fileManager.enableYybArmSleeveAnchorCorrection;
            result.enableYybArmVisualTwistCorrection = fileManager.enableYybArmVisualTwistCorrection;
        }

        private static VmdSaveResult BuildStableCandidateResult(VmdSaveResult result)
        {
            if (_activeJob == null ||
                _activeJob.Mode == CaptureMode.MainAuto ||
                string.IsNullOrWhiteSpace(result.FilePath) ||
                !File.Exists(result.FilePath))
            {
                return result;
            }

            string copyPath = BuildCandidateVmdEvidencePath(_activeJob, result.FilePath);
            if (string.IsNullOrWhiteSpace(copyPath))
            {
                return result;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(copyPath) ?? _summaryDirectory);
            File.Copy(result.FilePath, copyPath, overwrite: true);
            string exportRotationDiagnosticsCsvPath = CopyStableCandidateSiblingArtifact(
                result.FilePath,
                result.ExportRotationDiagnosticsCsvPath,
                copyPath);
            string exportIkSourceDiagnosticsCsvPath = CopyStableCandidateSiblingArtifact(
                result.FilePath,
                result.ExportIkSourceDiagnosticsCsvPath,
                copyPath);
            return new VmdSaveResult
            {
                Success = result.Success,
                FilePath = copyPath,
                ErrorMessage = result.ErrorMessage ?? string.Empty,
                FrameCount = result.FrameCount,
                FileSizeBytes = new FileInfo(copyPath).Length,
                ExportRotationDiagnosticsCsvPath = exportRotationDiagnosticsCsvPath,
                ExportIkSourceDiagnosticsCsvPath = exportIkSourceDiagnosticsCsvPath
            };
        }

        private static string CopyStableCandidateSiblingArtifact(
            string sourceVmdPath,
            string sourceArtifactPath,
            string candidateVmdPath)
        {
            if (string.IsNullOrWhiteSpace(sourceArtifactPath) ||
                !File.Exists(sourceArtifactPath) ||
                string.IsNullOrWhiteSpace(candidateVmdPath))
            {
                return string.Empty;
            }

            string destinationPath = BuildStableCandidateSiblingArtifactPath(
                sourceVmdPath,
                sourceArtifactPath,
                candidateVmdPath);
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return string.Empty;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? _summaryDirectory);
            File.Copy(sourceArtifactPath, destinationPath, overwrite: true);
            return destinationPath;
        }

        private static string BuildStableCandidateSiblingArtifactPath(
            string sourceVmdPath,
            string sourceArtifactPath,
            string candidateVmdPath)
        {
            string artifactFileName = Path.GetFileName(sourceArtifactPath);
            string candidateDirectory = Path.GetDirectoryName(candidateVmdPath) ?? _summaryDirectory;
            string candidateBaseName = Path.GetFileNameWithoutExtension(candidateVmdPath);
            if (string.IsNullOrWhiteSpace(artifactFileName) ||
                string.IsNullOrWhiteSpace(candidateDirectory) ||
                string.IsNullOrWhiteSpace(candidateBaseName))
            {
                return string.Empty;
            }

            string sourceBaseName = Path.GetFileNameWithoutExtension(sourceVmdPath);
            string suffix = !string.IsNullOrWhiteSpace(sourceBaseName) &&
                artifactFileName.StartsWith(sourceBaseName, StringComparison.OrdinalIgnoreCase)
                    ? artifactFileName.Substring(sourceBaseName.Length)
                    : $".{SanitizeFileName(Path.GetFileNameWithoutExtension(sourceArtifactPath))}{Path.GetExtension(sourceArtifactPath)}";
            if (string.IsNullOrWhiteSpace(suffix))
            {
                suffix = Path.GetExtension(sourceArtifactPath);
            }

            return Path.Combine(candidateDirectory, $"{candidateBaseName}{suffix}");
        }

        private static string BuildCandidateVmdEvidencePath(CaptureJob job, string sourceVmdPath)
        {
            if (job == null || string.IsNullOrWhiteSpace(_summaryDirectory))
            {
                return string.Empty;
            }

            string sourceExtension = Path.GetExtension(sourceVmdPath);
            if (string.IsNullOrWhiteSpace(sourceExtension))
            {
                sourceExtension = ".vmd";
            }

            string fileName = BuildCandidateVmdEvidenceFileName(job.Mode, sourceExtension);
            return Path.Combine(_summaryDirectory, fileName);
        }

        private static void RequestPlayModeStop()
        {
            CleanupActiveSubscriptions();
            _activeJobStartedInPlayMode = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;
            _advanceAfterPlayStopPending = true;
            SavePersistedState();
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            QueueAdvanceAfterPlayStop("RequestPlayModeStop");
            AppendRunnerTrace($"request play stop active={_activeJob?.DisplayName ?? "<none>"} playing={EditorApplication.isPlaying}");

            if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
            }
            else
            {
                QueueAdvanceAfterPlayStop("AlreadyInEditMode");
            }
        }

        private static void QueueAdvanceAfterPlayStop(string reason)
        {
            if (!_advanceAfterPlayStopPending)
            {
                return;
            }

            EditorApplication.update -= TryAdvanceAfterPlayStop;
            EditorApplication.update += TryAdvanceAfterPlayStop;
            AppendRunnerTrace(
                $"advance queued reason={reason} active={_activeJob?.DisplayName ?? "<none>"} " +
                $"playing={EditorApplication.isPlaying} willChange={EditorApplication.isPlayingOrWillChangePlaymode}");
        }

        private static void TryAdvanceAfterPlayStop()
        {
            if (!_advanceAfterPlayStopPending)
            {
                EditorApplication.update -= TryAdvanceAfterPlayStop;
                return;
            }

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            _advanceAfterPlayStopPending = false;
            SavePersistedState();
            EditorApplication.update -= TryAdvanceAfterPlayStop;
            AppendRunnerTrace($"advance firing active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished}");
            StartNextJob();
        }

        private static void QueuePlayModeEntryForActiveJob(string reason)
        {
            if (!_isRunning || _activeJob == null)
            {
                return;
            }

            if (!_playModeEntryPending)
            {
                _playModeEntryPending = true;
                SavePersistedState();
            }

            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update += TryEnterPlayModeForActiveJob;
            AppendRunnerTrace(
                $"playmode entry queued reason={reason} active={_activeJob.DisplayName} " +
                $"playing={EditorApplication.isPlaying} willChange={EditorApplication.isPlayingOrWillChangePlaymode} " +
                $"compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");
        }

        private static void TryEnterPlayModeForActiveJob()
        {
            if (!_playModeEntryPending)
            {
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                return;
            }

            if (!_isRunning || _activeJob == null)
            {
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                RecoverFromMissingActiveJob(_isRunning
                    ? "TryEnterPlayModeForActiveJob"
                    : "TryEnterPlayModeForActiveJobNotRunning");
                return;
            }

            if (_advanceAfterPlayStopPending)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                _playModeEntryPending = false;
                _playModeEntryRequestedAt = 0d;
                SavePersistedState();
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                EditorApplication.delayCall += StartCurrentJobInPlayMode;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (!string.Equals(EditorSceneManager.GetActiveScene().path, _activeJob.ScenePath, StringComparison.Ordinal))
            {
                return;
            }

            if (_playModeEntryRequestedAt <= 0d)
            {
                _playModeEntryRequestedAt = EditorApplication.timeSinceStartup;
                SavePersistedState();
            }
            else if (EditorApplication.timeSinceStartup - _playModeEntryRequestedAt > PlayModeEntryTimeoutSeconds)
            {
                _playModeEntryPending = false;
                _playModeEntryRequestedAt = 0d;
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                RecordFailure($"Play Mode 진입 시간 초과: {_activeJob.DisplayName}");
                _activeJobFinished = true;
                RequestPlayModeStop();
                return;
            }

            EditorApplication.isPlaying = true;
        }

        private static void CleanupActiveSubscriptions()
        {
            if (_activeFileManager != null)
            {
                _activeFileManager.EditorDiagnosticSmokeFinished -= HandleMainSceneFinished;
            }

            if (_activeRecorder != null)
            {
                _activeRecorder.RecordingFinished -= HandleManualFinished;
            }
        }

        private static void RecordFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Failures.Add(message);
            Debug.LogError($"[YybVisualComparisonBatchRunner] {message}");
            AppendRunnerTrace($"failure={message}");
            SavePersistedState();
        }

        private static void FinalizeRun()
        {
            AppendRunnerTrace($"finalize started results={Results.Count} failures={Failures.Count}");
            HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
            RestoreEnterPlayModeOptions();
            CleanupActiveSubscriptions();
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update -= TryAdvanceAfterPlayStop;

            string summaryJsonPath = Path.Combine(_summaryDirectory, SummaryJsonFileName);
            string summaryMarkdownPath = Path.Combine(_summaryDirectory, SummaryMarkdownFileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(summaryJsonPath) ?? _summaryDirectory);
                SummaryFrameRoleDiagnostics frameRoleDiagnostics = BuildCurrentSummaryFrameRoleDiagnostics();
                MotionComparisonFrameQualitySummary[] frameQualitySummaries =
                    BuildFrameQualitySummaries(frameRoleDiagnostics);
                PromoteFrameQualityFailuresToRunFailures(frameQualitySummaries, frameRoleDiagnostics);
                WriteSummaryJson(summaryJsonPath, frameQualitySummaries, frameRoleDiagnostics);
                WriteSummaryMarkdown(summaryMarkdownPath, frameQualitySummaries, frameRoleDiagnostics);
                CopyLatestSummary(summaryJsonPath, LatestSummaryJsonRelativePath);
                CopyLatestSummary(summaryMarkdownPath, LatestSummaryMarkdownRelativePath);
            }
            catch (Exception ex)
            {
                string message = $"summary finalize failed: {ex.Message}";
                if (!Failures.Contains(message))
                {
                    Failures.Add(message);
                }

                Debug.LogError($"[YybVisualComparisonBatchRunner] {message}\n{ex.StackTrace}");
                AppendRunnerTrace(message);
            }

            string resultMessage =
                $"[YybVisualComparisonBatchRunner] 종료: success={Results.Count(result => result.success)}/{Results.Count}, " +
                $"failures={Failures.Count}, summary={MakeProjectRelativePath(summaryJsonPath)}";
            if (Failures.Count > 0)
            {
                Debug.LogWarning(resultMessage);
            }
            else
            {
                Debug.Log(resultMessage);
            }

            EmitCompletion(
                passed: Failures.Count == 0,
                summaryJsonPath: summaryJsonPath,
                summaryMarkdownPath: summaryMarkdownPath,
                failures: Failures.ToArray());
            _isRunning = false;
            ClearPersistedState();
            AppendRunnerTrace($"finalize completed passed={Failures.Count == 0} results={Results.Count}");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(Failures.Count > 0 ? 1 : 0);
            }
        }

        private static void EmitCompletion(bool passed, string summaryJsonPath, string summaryMarkdownPath, string[] failures)
        {
            if (!passed)
            {
                HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
                RestoreEnterPlayModeOptions();
                ClearPersistedState();
            }

            RunCompleted?.Invoke(new RunCompletionInfo
            {
                passed = passed,
                sessionId = _summarySessionId,
                summaryJsonPath = MakeProjectRelativePath(summaryJsonPath),
                summaryMarkdownPath = MakeProjectRelativePath(summaryMarkdownPath),
                latestSummaryJsonPath = LatestSummaryJsonRelativePath,
                latestSummaryMarkdownPath = LatestSummaryMarkdownRelativePath,
                failures = failures ?? Array.Empty<string>(),
                totalJobs = Results.Count,
                successJobs = Results.Count(result => result.success)
            });
        }

        private static void ApplyTemporaryEnterPlayModeOptions()
        {
            if (Application.isBatchMode || _enterPlayModeOptionsCaptured)
            {
                return;
            }

            _previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            _enterPlayModeOptionsCaptured = true;

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = _previousEnterPlayModeOptions | EnterPlayModeOptions.DisableDomainReload;
        }

        private static void RestoreEnterPlayModeOptions()
        {
            if (!_enterPlayModeOptionsCaptured)
            {
                return;
            }

            EditorSettings.enterPlayModeOptions = _previousEnterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _previousEnterPlayModeOptionsEnabled;
            _enterPlayModeOptionsCaptured = false;
        }

        private static void SavePersistedState()
        {
            PersistedState state = new PersistedState
            {
                fbxFileName = _fbxFileName,
                durationSeconds = _durationSeconds,
                targetFrameCount = _targetFrameCount,
                enableFingerCloseups = _enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented = _enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd = _mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd = _mmdIkDeltaGuardRecoveryTriggerVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd = _mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                mmdIkDeltaGuardRecoveryHoldFrames = _mmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride = _enableFinalIkFootGroundingRuntimeOverride,
                enableManualAnimatorFootLocalRotationRuntimeOverride = _enableManualAnimatorFootLocalRotationRuntimeOverride,
                disableManualAnimatorFootLocalRotationRuntimeOverride = _disableManualAnimatorFootLocalRotationRuntimeOverride,
                enableManualAnimatorFullBodyPoseRuntimeOverride = _enableManualAnimatorFullBodyPoseRuntimeOverride,
                disableManualAnimatorFullBodyPoseRuntimeOverride = _disableManualAnimatorFullBodyPoseRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceWeight = _manualAnimatorFullBodyPoseReferenceWeight,
                manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride =
                    _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride =
                    _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride =
                    _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                enableManualAnimatorBodyRotationRuntimeOverride = _enableManualAnimatorBodyRotationRuntimeOverride,
                disableManualAnimatorBodyRotationRuntimeOverride = _disableManualAnimatorBodyRotationRuntimeOverride,
                manualAnimatorBodyRotationReferenceWeight = _manualAnimatorBodyRotationReferenceWeight,
                enableManualAnimatorHandLocalRotationRuntimeOverride = _enableManualAnimatorHandLocalRotationRuntimeOverride,
                enableManualAnimatorThumbLocalRotationRuntimeOverride = _enableManualAnimatorThumbLocalRotationRuntimeOverride,
                enableManualAnimatorHandPalmFrameRuntimeOverride = _enableManualAnimatorHandPalmFrameRuntimeOverride,
                manualAnimatorHandPalmFrameWeight = _manualAnimatorHandPalmFrameWeight,
                overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings =
                    _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings,
                enableRetargetPoseVisualSpikeSmoothingRuntimeOverride =
                    _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                retargetPoseVisualSpikeCurrentWeight = _retargetPoseVisualSpikeCurrentWeight,
                retargetPoseVisualSpikeForearmStretchClampMaxOffset =
                    _retargetPoseVisualSpikeForearmStretchClampMaxOffset,
                enableYybArmSwingLimitRuntimeOverride = _enableYybArmSwingLimitRuntimeOverride,
                yybArmSwingLimitWeight = _yybArmSwingLimitWeight,
                yybArmSwingMaxDownDot = _yybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio = _yybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio = _yybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight = _yybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio = _yybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                    _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                    _yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                    _yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot = _yybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                    _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                    _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmDirectionRetargetRuntimeOverride = _enableYybArmDirectionRetargetRuntimeOverride,
                yybArmDirectionUpperArmWeight = _yybArmDirectionUpperArmWeight,
                yybArmDirectionForearmWeight = _yybArmDirectionForearmWeight,
                yybArmDirectionUpperArmMaxDegrees = _yybArmDirectionUpperArmMaxDegrees,
                yybArmDirectionForearmMaxDegrees = _yybArmDirectionForearmMaxDegrees,
                yybArmDirectionLeftSideWeightScale = _yybArmDirectionLeftSideWeightScale,
                yybArmDirectionRightSideWeightScale = _yybArmDirectionRightSideWeightScale,
                overrideYybArmSleeveAnchorRuntimeSettings = _overrideYybArmSleeveAnchorRuntimeSettings,
                enableYybArmSleeveAnchorRuntimeOverride = _enableYybArmSleeveAnchorRuntimeOverride,
                yybArmSleeveAnchorInfluence = _yybArmSleeveAnchorInfluence,
                yybArmShoulderCapAnchorInfluence = _yybArmShoulderCapAnchorInfluence,
                yybArmSleeveAnchorMaxDegrees = _yybArmSleeveAnchorMaxDegrees,
                overrideYybArmVisualTwistRuntimeSettings = _overrideYybArmVisualTwistRuntimeSettings,
                enableYybArmVisualTwistRuntimeOverride = _enableYybArmVisualTwistRuntimeOverride,
                yybArmVisualUpperArmInfluence = _yybArmVisualUpperArmInfluence,
                yybArmVisualForearmInfluence = _yybArmVisualForearmInfluence,
                yybArmVisualUpperArmMaxDegrees = _yybArmVisualUpperArmMaxDegrees,
                yybArmVisualForearmMaxDegrees = _yybArmVisualForearmMaxDegrees,
                enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride =
                    _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                    _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride =
                    _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride =
                    _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride =
                    _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                enablePostSetHumanPoseRightEndpointPositionRuntimeOverride =
                    _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride =
                    _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                enableManualAnimatorBipedIkFootPositionRuntimeOverride = _enableManualAnimatorBipedIkFootPositionRuntimeOverride,
                enableManualAnimatorHipsLocalPositionRuntimeOverride = _enableManualAnimatorHipsLocalPositionRuntimeOverride,
                enableManualAnimatorBodyPositionXzRuntimeOverride =
                    _enableManualAnimatorBodyPositionXzRuntimeOverride,
                enableRetargetBodyPositionXzRootMotionRuntimeOverride =
                    _enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                disableTargetHumanoidBonePositionLockRuntimeOverride =
                    _disableTargetHumanoidBonePositionLockRuntimeOverride,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight = _manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                    _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                    _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight = _manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                postSetHumanPoseRightEndpointPositionReferenceWeight =
                    _postSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    _postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    _postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                postSetHumanPoseEndpointPositionUseLeftSide =
                    _postSetHumanPoseEndpointPositionUseLeftSide,
                preSetHumanPoseRightEndpointPositionReferenceWeight =
                    _preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    _preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    _preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                preSetHumanPoseEndpointPositionUseLeftSide =
                    _preSetHumanPoseEndpointPositionUseLeftSide,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                    _preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                preSetHumanPoseEndpointPositionInvertBodyPositionX =
                    _preSetHumanPoseEndpointPositionInvertBodyPositionX,
                preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                    _preSetHumanPoseEndpointPositionInvertBodyPositionZ,
                usePostSetHumanPoseRightFootEvaluatorXzReference =
                    _usePostSetHumanPoseRightFootEvaluatorXzReference,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                    _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                manualAnimatorBipedIkFootPositionReferenceWeight = _manualAnimatorBipedIkFootPositionReferenceWeight,
                manualAnimatorBipedIkFootPositionReferenceMaxOffset = _manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                manualAnimatorHipsLocalPositionReferenceWeight = _manualAnimatorHipsLocalPositionReferenceWeight,
                manualAnimatorHipsLocalPositionReferenceMaxOffset = _manualAnimatorHipsLocalPositionReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceWeight = _manualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset = _manualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart =
                    _manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                    _manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                    _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale =
                    _manualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale =
                    _manualAnimatorBodyPositionXzReferenceAxisZScale,
                enableVmdPlaybackProbeRuntimeOverride = _enableVmdPlaybackProbeRuntimeOverride,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride = _applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                vmdPlaybackProbeSourceVmdPath = _vmdPlaybackProbeSourceVmdPath,
                editorDiagnosticSmokeSegment = _editorDiagnosticSmokeSegment.ToString(),
                enableReferenceMmdTimingRuntimeOverride = _enableReferenceMmdTimingRuntimeOverride,
                diagnosticCaptureWidthOverride = _diagnosticCaptureWidthOverride,
                diagnosticCaptureHeightOverride = _diagnosticCaptureHeightOverride,
                diagnosticScreenshotPaddingOverride = _diagnosticScreenshotPaddingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride = _diagnosticScreenshotVerticalViewportCenterOverride,
                isRunning = _isRunning,
                activeJobFinished = _activeJobFinished,
                advanceAfterPlayStopPending = _advanceAfterPlayStopPending,
                playModeEntryPending = _playModeEntryPending,
                summarySessionId = _summarySessionId,
                summaryDirectory = _summaryDirectory,
                projectRoot = _projectRoot,
                activeJob = ToPersistedJob(_activeJob),
                pendingJobs = PendingJobs.Select(ToPersistedJob).ToArray(),
                results = Results.Select(ToPersistedResult).ToArray(),
                failures = Failures.ToArray()
            };

            SessionState.SetString(RunnerStateSessionKey, JsonUtility.ToJson(state));
        }

        private static void ClearPersistedState()
        {
            SessionState.EraseString(RunnerStateSessionKey);
        }

        private static void TryResumeRunAfterDomainReload()
        {
            if (_isRunning)
            {
                return;
            }

            if (!HasPersistedRunState())
            {
                return;
            }

            string json = SessionState.GetString(RunnerStateSessionKey, string.Empty);
            PersistedState state = JsonUtility.FromJson<PersistedState>(json);
            try
            {
                RestoreFromPersistedState(state);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YybVisualComparisonBatchRunner] 상태 복구 실패: {ex.Message}\n{ex.StackTrace}");
                RecordFailure($"상태 복구 실패: {ex.Message}");
                ClearPersistedState();
                _isRunning = false;
                HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
                RestoreEnterPlayModeOptions();
            }
        }

        private static void RestoreFromPersistedState(PersistedState state)
        {
            _fbxFileName = string.IsNullOrWhiteSpace(state.fbxFileName) ? DefaultFbxFileName : state.fbxFileName;
            _durationSeconds = Mathf.Max(0.1f, state.durationSeconds);
            _targetFrameCount = Mathf.Max(1, state.targetFrameCount);
            _enableFingerCloseups = state.enableFingerCloseups;
            _enableRecorderParentFrameIkOffsetsWhenCenterParented = state.enableRecorderParentFrameIkOffsetsWhenCenterParented;
            _mmdIkDeltaGuardLimitOverrideVmd = NormalizeMmdIkDeltaGuardLimitOverride(state.mmdIkDeltaGuardLimitOverrideVmd);
            _mmdIkDeltaGuardRecoveryTriggerVmd = NormalizeMmdIkDeltaGuardLimitOverride(state.mmdIkDeltaGuardRecoveryTriggerVmd);
            _mmdIkDeltaGuardRecoveryDebtThresholdVmd = NormalizeMmdIkDeltaGuardLimitOverride(state.mmdIkDeltaGuardRecoveryDebtThresholdVmd);
            _mmdIkDeltaGuardRecoveryHoldFrames = NormalizeMmdIkDeltaGuardRecoveryHoldFrames(state.mmdIkDeltaGuardRecoveryHoldFrames);
            _enableFinalIkFootGroundingRuntimeOverride = state.enableFinalIkFootGroundingRuntimeOverride;
            _enableManualAnimatorFootLocalRotationRuntimeOverride = state.enableManualAnimatorFootLocalRotationRuntimeOverride;
            _disableManualAnimatorFootLocalRotationRuntimeOverride = state.disableManualAnimatorFootLocalRotationRuntimeOverride;
            _enableManualAnimatorFullBodyPoseRuntimeOverride = state.enableManualAnimatorFullBodyPoseRuntimeOverride;
            _disableManualAnimatorFullBodyPoseRuntimeOverride = state.disableManualAnimatorFullBodyPoseRuntimeOverride;
            _manualAnimatorFullBodyPoseReferenceWeight = float.IsNaN(state.manualAnimatorFullBodyPoseReferenceWeight) ||
                float.IsInfinity(state.manualAnimatorFullBodyPoseReferenceWeight)
                    ? DefaultManualAnimatorFullBodyPoseReferenceWeight
                    : Mathf.Clamp01(state.manualAnimatorFullBodyPoseReferenceWeight);
            _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride =
                state.manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride;
            _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride =
                state.manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride =
                state.manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride;
            _enableManualAnimatorBodyRotationRuntimeOverride = state.enableManualAnimatorBodyRotationRuntimeOverride;
            _disableManualAnimatorBodyRotationRuntimeOverride = state.disableManualAnimatorBodyRotationRuntimeOverride;
            _manualAnimatorBodyRotationReferenceWeight = float.IsNaN(state.manualAnimatorBodyRotationReferenceWeight) ||
                float.IsInfinity(state.manualAnimatorBodyRotationReferenceWeight)
                    ? DefaultManualAnimatorBodyRotationReferenceWeight
                    : Mathf.Clamp01(state.manualAnimatorBodyRotationReferenceWeight);
            _enableManualAnimatorHandLocalRotationRuntimeOverride = state.enableManualAnimatorHandLocalRotationRuntimeOverride;
            _enableManualAnimatorThumbLocalRotationRuntimeOverride = state.enableManualAnimatorThumbLocalRotationRuntimeOverride;
            _enableManualAnimatorHandPalmFrameRuntimeOverride = state.enableManualAnimatorHandPalmFrameRuntimeOverride;
            _manualAnimatorHandPalmFrameWeight = Mathf.Clamp01(NormalizePositiveFloat(
                state.manualAnimatorHandPalmFrameWeight,
                DefaultManualAnimatorHandPalmFrameWeight));
            _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings =
                state.overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings;
            _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride =
                state.enableRetargetPoseVisualSpikeSmoothingRuntimeOverride;
            _retargetPoseVisualSpikeCurrentWeight = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.retargetPoseVisualSpikeCurrentWeight,
                    DefaultRetargetPoseVisualSpikeCurrentWeight),
                0.1f,
                1f);
            _retargetPoseVisualSpikeForearmStretchClampMaxOffset = Mathf.Clamp01(
                NormalizePositiveFloat(
                    state.retargetPoseVisualSpikeForearmStretchClampMaxOffset,
                    DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset));
            _enableYybArmSwingLimitRuntimeOverride = state.enableYybArmSwingLimitRuntimeOverride;
            _yybArmSwingLimitWeight = Mathf.Clamp01(NormalizePositiveFloat(
                state.yybArmSwingLimitWeight,
                DefaultYybArmSwingLimitWeight));
            _yybArmSwingMaxDownDot = Mathf.Clamp01(NormalizePositiveFloat(
                state.yybArmSwingMaxDownDot,
                DefaultYybArmSwingMaxDownDot));
            _yybArmSwingMinHandHorizontalRatio = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmSwingMinHandHorizontalRatio,
                    DefaultYybArmSwingMinHandHorizontalRatio),
                0f,
                1.5f);
            _yybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmSwingMaxHandBelowShoulderRatio,
                    DefaultYybArmSwingMaxHandBelowShoulderRatio),
                0f,
                1.5f);
            _yybArmSwingHorizontalReachLimitWeight = Mathf.Clamp01(NormalizePositiveFloat(
                state.yybArmSwingHorizontalReachLimitWeight,
                DefaultYybArmSwingHorizontalReachLimitWeight));
            _yybArmSwingMaxHandHorizontalReachRatio = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmSwingMaxHandHorizontalReachRatio,
                    DefaultYybArmSwingMaxHandHorizontalReachRatio),
                0f,
                1.5f);
            _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                    DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio),
                0f,
                1.5f);
            _yybArmSwingHorizontalReachMinElbowAngleAfterApply = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply),
                0f,
                180f);
            _yybArmSwingRaisedPoseHorizontalReachLimitWeight = Mathf.Clamp01(NormalizePositiveFloat(
                state.yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight));
            _yybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01(NormalizePositiveFloat(
                state.yybArmSwingRaisedPoseMinUpperArmDownDot,
                DefaultYybArmSwingRaisedPoseMinUpperArmDownDot));
            _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                    DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio),
                0f,
                1.5f);
            _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                    DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio),
                0f,
                1.5f);
            _enableYybArmDirectionRetargetRuntimeOverride = state.enableYybArmDirectionRetargetRuntimeOverride;
            _yybArmDirectionUpperArmWeight = Mathf.Clamp01(NormalizePositiveFloat(
                state.yybArmDirectionUpperArmWeight,
                DefaultYybArmDirectionUpperArmWeight));
            _yybArmDirectionForearmWeight = Mathf.Clamp01(NormalizePositiveFloat(
                state.yybArmDirectionForearmWeight,
                DefaultYybArmDirectionForearmWeight));
            _yybArmDirectionUpperArmMaxDegrees = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmDirectionUpperArmMaxDegrees,
                    DefaultYybArmDirectionUpperArmMaxDegrees),
                0f,
                120f);
            _yybArmDirectionForearmMaxDegrees = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.yybArmDirectionForearmMaxDegrees,
                    DefaultYybArmDirectionForearmMaxDegrees),
                0f,
                120f);
            _yybArmDirectionLeftSideWeightScale = Mathf.Clamp01(NormalizeFiniteFloat(
                state.yybArmDirectionLeftSideWeightScale,
                DefaultYybArmDirectionLeftSideWeightScale));
            _yybArmDirectionRightSideWeightScale = Mathf.Clamp01(NormalizeFiniteFloat(
                state.yybArmDirectionRightSideWeightScale,
                DefaultYybArmDirectionRightSideWeightScale));
            _overrideYybArmSleeveAnchorRuntimeSettings = state.overrideYybArmSleeveAnchorRuntimeSettings;
            _enableYybArmSleeveAnchorRuntimeOverride = state.enableYybArmSleeveAnchorRuntimeOverride;
            _yybArmSleeveAnchorInfluence = Mathf.Clamp01(NormalizeFiniteFloat(
                state.yybArmSleeveAnchorInfluence,
                DefaultYybArmSleeveAnchorInfluence));
            _yybArmShoulderCapAnchorInfluence = Mathf.Clamp01(NormalizeFiniteFloat(
                state.yybArmShoulderCapAnchorInfluence,
                DefaultYybArmShoulderCapAnchorInfluence));
            _yybArmSleeveAnchorMaxDegrees = Mathf.Clamp(
                NormalizeFiniteFloat(
                    state.yybArmSleeveAnchorMaxDegrees,
                    DefaultYybArmSleeveAnchorMaxDegrees),
                0f,
                120f);
            _overrideYybArmVisualTwistRuntimeSettings = state.overrideYybArmVisualTwistRuntimeSettings;
            _enableYybArmVisualTwistRuntimeOverride = state.enableYybArmVisualTwistRuntimeOverride;
            _yybArmVisualUpperArmInfluence = Mathf.Clamp01(NormalizeFiniteFloat(
                state.yybArmVisualUpperArmInfluence,
                DefaultYybArmVisualUpperArmInfluence));
            _yybArmVisualForearmInfluence = Mathf.Clamp01(NormalizeFiniteFloat(
                state.yybArmVisualForearmInfluence,
                DefaultYybArmVisualForearmInfluence));
            _yybArmVisualUpperArmMaxDegrees = Mathf.Clamp(
                NormalizeFiniteFloat(
                    state.yybArmVisualUpperArmMaxDegrees,
                    DefaultYybArmVisualUpperArmMaxDegrees),
                0f,
                120f);
            _yybArmVisualForearmMaxDegrees = Mathf.Clamp(
                NormalizeFiniteFloat(
                    state.yybArmVisualForearmMaxDegrees,
                    DefaultYybArmVisualForearmMaxDegrees),
                0f,
                120f);
            _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = state.enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = state.enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride =
                state.disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                state.disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride =
                state.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride =
                state.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride =
                state.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride =
                state.enablePostSetHumanPoseRightEndpointPositionRuntimeOverride;
            _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride =
                state.enablePreSetHumanPoseRightEndpointPositionRuntimeOverride;
            _enableManualAnimatorBipedIkFootPositionRuntimeOverride = state.enableManualAnimatorBipedIkFootPositionRuntimeOverride;
            _enableManualAnimatorHipsLocalPositionRuntimeOverride = state.enableManualAnimatorHipsLocalPositionRuntimeOverride;
            _enableManualAnimatorBodyPositionXzRuntimeOverride =
                state.enableManualAnimatorBodyPositionXzRuntimeOverride;
            _enableRetargetBodyPositionXzRootMotionRuntimeOverride =
                state.enableRetargetBodyPositionXzRootMotionRuntimeOverride;
            _disableTargetHumanoidBonePositionLockRuntimeOverride =
                state.disableTargetHumanoidBonePositionLockRuntimeOverride;
            _manualAnimatorLowerBodySegmentDirectionReferenceWeight = NormalizePositiveFloat(
                state.manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight);
            _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = NormalizePositiveFloat(
                state.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
            _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = NormalizePositiveFloat(
                state.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = NormalizePositiveFloat(
                state.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = NormalizePositiveFloat(
                state.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = NormalizePositiveFloat(
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale));
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight));
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = NormalizePositiveFloat(
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = NormalizePositiveFloat(
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight));
            _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = NormalizePositiveFloat(
                state.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            _manualAnimatorFootHipsAlignedResidualYawReferenceWeight = NormalizePositiveFloat(
                state.manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight);
            _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = NormalizePositiveFloat(
                state.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            _postSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.postSetHumanPoseRightEndpointPositionReferenceWeight,
                    DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight));
            _postSetHumanPoseRightEndpointPositionReferenceMaxOffset = NormalizePositiveFloat(
                state.postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            _postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale));
            _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight));
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = NormalizePositiveFloat(
                state.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = NormalizePositiveFloat(
                state.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            _postSetHumanPoseEndpointPositionUseLeftSide =
                state.postSetHumanPoseEndpointPositionUseLeftSide;
            _preSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.preSetHumanPoseRightEndpointPositionReferenceWeight,
                    DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight));
            _preSetHumanPoseRightEndpointPositionReferenceMaxOffset = NormalizePositiveFloat(
                state.preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            _preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale));
            _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight));
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = NormalizePositiveFloat(
                state.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = NormalizePositiveFloat(
                state.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            _preSetHumanPoseEndpointPositionUseLeftSide =
                state.preSetHumanPoseEndpointPositionUseLeftSide;
            _preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                state.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            _preSetHumanPoseEndpointPositionInvertBodyPositionX =
                state.preSetHumanPoseEndpointPositionInvertBodyPositionX;
            _preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                state.preSetHumanPoseEndpointPositionInvertBodyPositionZ;
            _usePostSetHumanPoseRightFootEvaluatorXzReference =
                state.usePostSetHumanPoseRightFootEvaluatorXzReference;
            _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = NormalizePositiveFloat(
                state.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            _manualAnimatorBipedIkFootPositionReferenceWeight = NormalizePositiveFloat(
                state.manualAnimatorBipedIkFootPositionReferenceWeight,
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight);
            _manualAnimatorBipedIkFootPositionReferenceMaxOffset = NormalizePositiveFloat(
                state.manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset);
            _manualAnimatorHipsLocalPositionReferenceWeight = NormalizePositiveFloat(
                state.manualAnimatorHipsLocalPositionReferenceWeight,
                DefaultManualAnimatorHipsLocalPositionReferenceWeight);
            _manualAnimatorHipsLocalPositionReferenceMaxOffset = NormalizePositiveFloat(
                state.manualAnimatorHipsLocalPositionReferenceMaxOffset,
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset);
            _manualAnimatorBodyPositionXzReferenceWeight = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.manualAnimatorBodyPositionXzReferenceWeight,
                    DefaultManualAnimatorBodyPositionXzReferenceWeight));
            _manualAnimatorBodyPositionXzReferenceMaxOffset = NormalizePositiveFloat(
                state.manualAnimatorBodyPositionXzReferenceMaxOffset,
                DefaultManualAnimatorBodyPositionXzReferenceMaxOffset);
            _manualAnimatorBodyPositionXzReferenceFrameGateStart = NormalizePositiveFloat(
                state.manualAnimatorBodyPositionXzReferenceFrameGateStart,
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart);
            _manualAnimatorBodyPositionXzReferenceFrameGateEnd = NormalizePositiveFloat(
                state.manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd);
            _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = NormalizePositiveFloat(
                state.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames);
            _manualAnimatorBodyPositionXzReferenceAxisXScale = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.manualAnimatorBodyPositionXzReferenceAxisXScale,
                    DefaultManualAnimatorBodyPositionXzReferenceAxisXScale));
            _manualAnimatorBodyPositionXzReferenceAxisZScale = Mathf.Clamp01(
                NormalizeFiniteFloat(
                    state.manualAnimatorBodyPositionXzReferenceAxisZScale,
                    DefaultManualAnimatorBodyPositionXzReferenceAxisZScale));
            _enableVmdPlaybackProbeRuntimeOverride = state.enableVmdPlaybackProbeRuntimeOverride;
            _applyVmdPlaybackProbeIkTargetsRuntimeOverride =
                state.enableVmdPlaybackProbeRuntimeOverride &&
                state.applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            _vmdPlaybackProbeSourceVmdPath = state.vmdPlaybackProbeSourceVmdPath ?? string.Empty;
            _enableReferenceMmdTimingRuntimeOverride = state.enableReferenceMmdTimingRuntimeOverride;
            _editorDiagnosticSmokeSegment = ResolveEditorDiagnosticSmokeSegment(state.editorDiagnosticSmokeSegment);
            _diagnosticCaptureWidthOverride =
                NormalizeDiagnosticCaptureDimensionOverride(state.diagnosticCaptureWidthOverride);
            _diagnosticCaptureHeightOverride =
                NormalizeDiagnosticCaptureDimensionOverride(state.diagnosticCaptureHeightOverride);
            _diagnosticScreenshotPaddingOverride =
                NormalizeDiagnosticScreenshotPaddingOverride(state.diagnosticScreenshotPaddingOverride);
            _diagnosticScreenshotVerticalViewportCenterOverride =
                NormalizeDiagnosticScreenshotVerticalViewportCenterOverride(
                    state.diagnosticScreenshotVerticalViewportCenterOverride);
            _summarySessionId = state.summarySessionId ?? string.Empty;
            _summaryDirectory = state.summaryDirectory ?? string.Empty;
            _projectRoot = state.projectRoot ?? (Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath);
            _activeJobFinished = state.activeJobFinished;
            _advanceAfterPlayStopPending = state.advanceAfterPlayStopPending;
            _playModeEntryPending = state.playModeEntryPending;
            _playModeEntryRequestedAt = 0d;
            _activeJobStartedInPlayMode = false;
            _activeJob = FromPersistedJob(state.activeJob);

            PendingJobs.Clear();
            int skippedPendingJobs = 0;
            if (state.pendingJobs != null)
            {
                foreach (PersistedCaptureJob job in state.pendingJobs)
                {
                    CaptureJob restoredJob = FromPersistedJob(job);
                    if (restoredJob == null)
                    {
                        skippedPendingJobs++;
                        continue;
                    }

                    PendingJobs.Enqueue(restoredJob);
                }
            }

            Results.Clear();
            int skippedResults = 0;
            if (state.results != null)
            {
                foreach (PersistedCaptureResult result in state.results)
                {
                    CaptureResult restoredResult = FromPersistedResult(result);
                    if (restoredResult == null)
                    {
                        skippedResults++;
                        continue;
                    }

                    Results.Add(restoredResult);
                }
            }

            Failures.Clear();
            if (state.failures != null)
            {
                Failures.AddRange(state.failures.Where(message => !string.IsNullOrWhiteSpace(message)));
            }

            _activeFileManager = null;
            _activeRecorder = null;
            _isRunning = true;
            HumanoidSampleCode.SetEditorAutoStartSuppressed(true);
            ApplyTemporaryEnterPlayModeOptions();
            LoadRunAssetsForResume();

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update -= TryAdvanceAfterPlayStop;
            if (_playModeEntryPending)
            {
                EditorApplication.update += TryEnterPlayModeForActiveJob;
            }
            if (_advanceAfterPlayStopPending)
            {
                EditorApplication.update += TryAdvanceAfterPlayStop;
            }

            Debug.Log(
                $"[YybVisualComparisonBatchRunner] 상태 복구: active={_activeJob?.DisplayName ?? "<none>"}, " +
                $"pending={PendingJobs.Count}, playing={EditorApplication.isPlaying}");
            AppendRunnerTrace(
                $"state restored active={_activeJob?.DisplayName ?? "<none>"} pendingJobs={PendingJobs.Count} " +
                $"activeFinished={_activeJobFinished} advancePending={_advanceAfterPlayStopPending} playing={EditorApplication.isPlaying} " +
                $"skippedPendingJobs={skippedPendingJobs} skippedResults={skippedResults}");

            if (_advanceAfterPlayStopPending)
            {
                QueueAdvanceAfterPlayStop("RestoreFromPersistedState");
            }
            else if (_playModeEntryPending)
            {
                if (_activeJob != null)
                {
                    QueuePlayModeEntryForActiveJob("RestoreFromPersistedState");
                }
                else
                {
                    RecoverFromMissingActiveJob("RestoreFromPersistedState");
                }
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += StartCurrentJobInPlayMode;
            }
            else if (_activeJob != null && _activeJobFinished)
            {
                EditorApplication.delayCall += StartNextJob;
            }
            else if (_activeJob != null)
            {
                EditorApplication.delayCall += () => { QueuePlayModeEntryForActiveJob("RestoreActiveJob"); };
            }
            else if (PendingJobs.Count > 0)
            {
                StartNextJob();
            }
            else
            {
                FinalizeRun();
            }
        }

        private static void RecoverFromMissingActiveJob(string reason)
        {
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;
            SavePersistedState();

            if (!_isRunning)
            {
                return;
            }

            if (_advanceAfterPlayStopPending)
            {
                AppendRunnerTrace($"missing active job deferred reason={reason} advancePending=True pendingJobs={PendingJobs.Count}");
                return;
            }

            if (PendingJobs.Count > 0)
            {
                AppendRunnerTrace($"missing active job recovered reason={reason} pendingJobs={PendingJobs.Count}");
                EditorApplication.delayCall += StartNextJob;
                return;
            }

            AppendRunnerTrace($"missing active job finalizing reason={reason}");
            EditorApplication.delayCall += FinalizeRun;
        }

        private static void AppendRunnerTrace(string message)
        {
            try
            {
                string projectRoot = string.IsNullOrWhiteSpace(_projectRoot)
                    ? (Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath)
                    : _projectRoot;
                string path = Path.Combine(projectRoot, RunnerTraceRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectRoot);
                File.AppendAllText(
                    path,
                    $"{DateTime.Now.ToString("o", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore trace write failures. The runner must keep going even if diagnostics cannot be written.
            }
        }

        private static void LoadRunAssetsForResume()
        {
            _referenceClipAssetPath = ResolveReferenceClipAssetPath(
                _fbxFileName,
                assetPath => LoadFirstAnimationClip(assetPath) != null);
            _referenceClip = LoadFirstAnimationClip(_referenceClipAssetPath);
            if (_referenceClip == null)
            {
                throw new InvalidOperationException($"비교 기준 AnimationClip을 찾지 못했습니다: {_fbxFileName}");
            }

            _fallbackController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ManualControllerPath) ??
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackControllerPath);
            if (_fallbackController == null)
            {
                throw new InvalidOperationException("수동 비교용 Animator Controller를 찾지 못했습니다.");
            }
        }

        private static PersistedCaptureJob ToPersistedJob(CaptureJob job)
        {
            if (job == null)
            {
                return null;
            }

            return new PersistedCaptureJob
            {
                mode = (int)job.Mode,
                scenePath = job.ScenePath,
                sceneName = job.SceneName,
                displayName = job.DisplayName,
                manualTargetNameToken = job.ManualTargetNameToken
            };
        }

        private static CaptureJob FromPersistedJob(PersistedCaptureJob job)
        {
            if (job == null)
            {
                return null;
            }

            bool hasScenePath = !string.IsNullOrWhiteSpace(job.scenePath);
            bool hasDisplayName = !string.IsNullOrWhiteSpace(job.displayName);
            bool modeInRange = Enum.IsDefined(typeof(CaptureMode), job.mode);
            if (!hasScenePath || !hasDisplayName || !modeInRange)
            {
                return null;
            }

            return new CaptureJob
            {
                Mode = (CaptureMode)job.mode,
                ScenePath = job.scenePath,
                SceneName = job.sceneName,
                DisplayName = job.displayName,
                ManualTargetNameToken = job.manualTargetNameToken
            };
        }

        private static PersistedCaptureResult ToPersistedResult(CaptureResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new PersistedCaptureResult
            {
                jobMode = result.jobMode,
                jobDisplayName = result.jobDisplayName,
                sceneName = result.sceneName,
                comparisonLabel = result.comparisonLabel,
                targetName = result.targetName,
                success = result.success,
                error = result.error,
                vmdPath = result.vmdPath,
                frameCount = result.frameCount,
                fileSizeBytes = result.fileSizeBytes,
                comparisonSessionManifestPath = result.comparisonSessionManifestPath,
                comparisonMetricsCsvPath = result.comparisonMetricsCsvPath,
                comparisonFrameFolderPath = result.comparisonFrameFolderPath,
                comparisonFrameIndexPath = result.comparisonFrameIndexPath,
                comparisonSessionId = result.comparisonSessionId,
                hasFileManagerEffectiveSettings = result.hasFileManagerEffectiveSettings,
                useManualAnimatorFootLocalRotationReference = result.useManualAnimatorFootLocalRotationReference,
                manualAnimatorFootLocalRotationReferenceWeight = result.manualAnimatorFootLocalRotationReferenceWeight,
                useManualAnimatorFullBodyPoseReference = result.useManualAnimatorFullBodyPoseReference,
                manualAnimatorFullBodyPoseReferenceWeight = result.manualAnimatorFullBodyPoseReferenceWeight,
                manualAnimatorFullBodyPoseExcludeLowerBodyMuscles =
                    result.manualAnimatorFullBodyPoseExcludeLowerBodyMuscles,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnly =
                    result.manualAnimatorFullBodyPoseLowerBodyMusclesOnly,
                manualAnimatorFullBodyPoseLegTwistMusclesOnly =
                    result.manualAnimatorFullBodyPoseLegTwistMusclesOnly,
                useManualAnimatorBodyRotationReference = result.useManualAnimatorBodyRotationReference,
                manualAnimatorBodyRotationReferenceWeight = result.manualAnimatorBodyRotationReferenceWeight,
                useManualAnimatorLowerBodySegmentDirectionReference =
                    result.useManualAnimatorLowerBodySegmentDirectionReference,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                    result.manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                    result.disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableManualAnimatorLowerLegToFootSegmentDirectionReference =
                    result.disableManualAnimatorLowerLegToFootSegmentDirectionReference,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableManualAnimatorFootToToesSegmentDirectionReference =
                    result.disableManualAnimatorFootToToesSegmentDirectionReference,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                useManualAnimatorFootHipsAlignedResidualYawReference =
                    result.useManualAnimatorFootHipsAlignedResidualYawReference,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                    result.manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                    result.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                usePostSetHumanPoseRightEndpointPositionReference =
                    result.usePostSetHumanPoseRightEndpointPositionReference,
                postSetHumanPoseRightEndpointPositionReferenceWeight =
                    result.postSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    result.postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    result.postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    result.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    result.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    result.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                postSetHumanPoseEndpointPositionUseLeftSide =
                    result.postSetHumanPoseEndpointPositionUseLeftSide,
                usePreSetHumanPoseRightEndpointPositionReference =
                    result.usePreSetHumanPoseRightEndpointPositionReference,
                preSetHumanPoseRightEndpointPositionReferenceWeight =
                    result.preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    result.preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    result.preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    result.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    result.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    result.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                preSetHumanPoseEndpointPositionUseLeftSide =
                    result.preSetHumanPoseEndpointPositionUseLeftSide,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                    result.preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                preSetHumanPoseEndpointPositionInvertBodyPositionX =
                    result.preSetHumanPoseEndpointPositionInvertBodyPositionX,
                preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                    result.preSetHumanPoseEndpointPositionInvertBodyPositionZ,
                usePostSetHumanPoseRightFootEvaluatorXzReference =
                    result.usePostSetHumanPoseRightFootEvaluatorXzReference,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                    result.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                useManualAnimatorBodyPositionXzReference =
                    result.useManualAnimatorBodyPositionXzReference,
                manualAnimatorBodyPositionXzReferenceWeight =
                    result.manualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset =
                    result.manualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale =
                    result.manualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale =
                    result.manualAnimatorBodyPositionXzReferenceAxisZScale,
                enableYybArmSwingLimitCorrection = result.enableYybArmSwingLimitCorrection,
                yybArmSwingLimitWeight = result.yybArmSwingLimitWeight,
                yybArmSwingMaxDownDot = result.yybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio = result.yybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio = result.yybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight = result.yybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio = result.yybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                    result.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                    result.yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                    result.yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot = result.yybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                    result.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                    result.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmSleeveAnchorCorrection = result.enableYybArmSleeveAnchorCorrection,
                enableYybArmVisualTwistCorrection = result.enableYybArmVisualTwistCorrection
            };
        }

        private static CaptureResult FromPersistedResult(PersistedCaptureResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new CaptureResult
            {
                jobMode = result.jobMode,
                jobDisplayName = result.jobDisplayName,
                sceneName = result.sceneName,
                comparisonLabel = result.comparisonLabel,
                targetName = result.targetName,
                success = result.success,
                error = result.error,
                vmdPath = result.vmdPath,
                frameCount = result.frameCount,
                fileSizeBytes = result.fileSizeBytes,
                comparisonSessionManifestPath = result.comparisonSessionManifestPath,
                comparisonMetricsCsvPath = result.comparisonMetricsCsvPath,
                comparisonFrameFolderPath = result.comparisonFrameFolderPath,
                comparisonFrameIndexPath = result.comparisonFrameIndexPath,
                comparisonSessionId = result.comparisonSessionId,
                hasFileManagerEffectiveSettings = result.hasFileManagerEffectiveSettings,
                useManualAnimatorFootLocalRotationReference = result.useManualAnimatorFootLocalRotationReference,
                manualAnimatorFootLocalRotationReferenceWeight = result.manualAnimatorFootLocalRotationReferenceWeight,
                useManualAnimatorFullBodyPoseReference = result.useManualAnimatorFullBodyPoseReference,
                manualAnimatorFullBodyPoseReferenceWeight = result.manualAnimatorFullBodyPoseReferenceWeight,
                manualAnimatorFullBodyPoseExcludeLowerBodyMuscles =
                    result.manualAnimatorFullBodyPoseExcludeLowerBodyMuscles,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnly =
                    result.manualAnimatorFullBodyPoseLowerBodyMusclesOnly,
                manualAnimatorFullBodyPoseLegTwistMusclesOnly =
                    result.manualAnimatorFullBodyPoseLegTwistMusclesOnly,
                useManualAnimatorBodyRotationReference = result.useManualAnimatorBodyRotationReference,
                manualAnimatorBodyRotationReferenceWeight = result.manualAnimatorBodyRotationReferenceWeight,
                useManualAnimatorLowerBodySegmentDirectionReference =
                    result.useManualAnimatorLowerBodySegmentDirectionReference,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                    result.manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                    result.disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableManualAnimatorLowerLegToFootSegmentDirectionReference =
                    result.disableManualAnimatorLowerLegToFootSegmentDirectionReference,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableManualAnimatorFootToToesSegmentDirectionReference =
                    result.disableManualAnimatorFootToToesSegmentDirectionReference,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                useManualAnimatorFootHipsAlignedResidualYawReference =
                    result.useManualAnimatorFootHipsAlignedResidualYawReference,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                    result.manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                    result.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                usePostSetHumanPoseRightEndpointPositionReference =
                    result.usePostSetHumanPoseRightEndpointPositionReference,
                postSetHumanPoseRightEndpointPositionReferenceWeight =
                    result.postSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    result.postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    result.postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    result.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    result.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    result.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                postSetHumanPoseEndpointPositionUseLeftSide =
                    result.postSetHumanPoseEndpointPositionUseLeftSide,
                usePreSetHumanPoseRightEndpointPositionReference =
                    result.usePreSetHumanPoseRightEndpointPositionReference,
                preSetHumanPoseRightEndpointPositionReferenceWeight =
                    result.preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    result.preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    result.preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    result.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    result.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    result.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                preSetHumanPoseEndpointPositionUseLeftSide =
                    result.preSetHumanPoseEndpointPositionUseLeftSide,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                    result.preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                preSetHumanPoseEndpointPositionInvertBodyPositionX =
                    result.preSetHumanPoseEndpointPositionInvertBodyPositionX,
                preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                    result.preSetHumanPoseEndpointPositionInvertBodyPositionZ,
                usePostSetHumanPoseRightFootEvaluatorXzReference =
                    result.usePostSetHumanPoseRightFootEvaluatorXzReference,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                    result.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                useManualAnimatorBodyPositionXzReference =
                    result.useManualAnimatorBodyPositionXzReference,
                manualAnimatorBodyPositionXzReferenceWeight =
                    result.manualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset =
                    result.manualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale =
                    result.manualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale =
                    result.manualAnimatorBodyPositionXzReferenceAxisZScale,
                enableYybArmSwingLimitCorrection = result.enableYybArmSwingLimitCorrection,
                yybArmSwingLimitWeight = result.yybArmSwingLimitWeight,
                yybArmSwingMaxDownDot = result.yybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio = result.yybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio = result.yybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight = result.yybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio = result.yybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                    result.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                    result.yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                    result.yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot = result.yybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                    result.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                    result.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmSleeveAnchorCorrection = result.enableYybArmSleeveAnchorCorrection,
                enableYybArmVisualTwistCorrection = result.enableYybArmVisualTwistCorrection
            };
        }

        private static void WriteSummaryJson(
            string path,
            MotionComparisonFrameQualitySummary[] frameQualitySummaries = null,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = null)
        {
            frameRoleDiagnostics = frameRoleDiagnostics ?? BuildCurrentSummaryFrameRoleDiagnostics();
            frameQualitySummaries = frameQualitySummaries ?? BuildFrameQualitySummaries(frameRoleDiagnostics);
            int summaryTargetFrameCount = ResolveSummaryTargetFrameCount();
            SummaryContainer summary = new SummaryContainer
            {
                session_id = _summarySessionId,
                generated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                fbx_file = _fbxFileName,
                duration_seconds = _durationSeconds,
                target_frame_count = summaryTargetFrameCount,
                segment = _editorDiagnosticSmokeSegment.ToString(),
                finger_closeups = _enableFingerCloseups,
                recorder_parent_ik_offsets_when_center_parented = _enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmd_ik_delta_guard_limit_override_vmd = _mmdIkDeltaGuardLimitOverrideVmd,
                mmd_ik_delta_guard_recovery_trigger_vmd = _mmdIkDeltaGuardRecoveryTriggerVmd,
                mmd_ik_delta_guard_recovery_debt_vmd = _mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                mmd_ik_delta_guard_recovery_hold_frames = _mmdIkDeltaGuardRecoveryHoldFrames,
                final_ik_foot_grounding_enabled = _enableFinalIkFootGroundingRuntimeOverride,
                manual_animator_foot_local_rotation_enabled = _enableManualAnimatorFootLocalRotationRuntimeOverride,
                manual_animator_foot_local_rotation_disabled = _disableManualAnimatorFootLocalRotationRuntimeOverride,
                manual_animator_full_body_pose_enabled = _enableManualAnimatorFullBodyPoseRuntimeOverride,
                manual_animator_full_body_pose_disabled = _disableManualAnimatorFullBodyPoseRuntimeOverride,
                manual_animator_full_body_pose_weight = _manualAnimatorFullBodyPoseReferenceWeight,
                manual_animator_full_body_pose_exclude_lower_body_muscles =
                    _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                manual_animator_full_body_pose_lower_body_muscles_only =
                    _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                manual_animator_full_body_pose_leg_twist_muscles_only =
                    _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                manual_animator_body_rotation_enabled = _enableManualAnimatorBodyRotationRuntimeOverride,
                manual_animator_body_rotation_disabled = _disableManualAnimatorBodyRotationRuntimeOverride,
                manual_animator_body_rotation_weight = _manualAnimatorBodyRotationReferenceWeight,
                manual_animator_hand_local_rotation_enabled = _enableManualAnimatorHandLocalRotationRuntimeOverride,
                manual_animator_thumb_local_rotation_enabled = _enableManualAnimatorThumbLocalRotationRuntimeOverride,
                manual_animator_hand_palm_frame_enabled = _enableManualAnimatorHandPalmFrameRuntimeOverride,
                manual_animator_hand_palm_frame_weight = _manualAnimatorHandPalmFrameWeight,
                retarget_pose_visual_spike_smoothing_override =
                    _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings,
                retarget_pose_visual_spike_smoothing_enabled =
                    _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                retarget_pose_visual_spike_current_weight = _retargetPoseVisualSpikeCurrentWeight,
                retarget_pose_visual_spike_forearm_stretch_clamp_max_offset =
                    _retargetPoseVisualSpikeForearmStretchClampMaxOffset,
                yyb_arm_swing_limit_enabled = _enableYybArmSwingLimitRuntimeOverride,
                yyb_arm_swing_limit_weight = _yybArmSwingLimitWeight,
                yyb_arm_swing_max_down_dot = _yybArmSwingMaxDownDot,
                yyb_arm_swing_min_hand_horizontal_ratio = _yybArmSwingMinHandHorizontalRatio,
                yyb_arm_swing_max_hand_below_shoulder_ratio = _yybArmSwingMaxHandBelowShoulderRatio,
                yyb_arm_swing_horizontal_reach_limit_weight = _yybArmSwingHorizontalReachLimitWeight,
                yyb_arm_swing_max_hand_horizontal_reach_ratio = _yybArmSwingMaxHandHorizontalReachRatio,
                yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio =
                    _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply =
                    _yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yyb_arm_swing_raised_pose_horizontal_reach_limit_weight =
                    _yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yyb_arm_swing_raised_pose_min_upper_arm_down_dot =
                    _yybArmSwingRaisedPoseMinUpperArmDownDot,
                yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio =
                    _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio =
                    _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                yyb_arm_direction_retarget_enabled = _enableYybArmDirectionRetargetRuntimeOverride,
                yyb_arm_direction_upper_arm_weight = _yybArmDirectionUpperArmWeight,
                yyb_arm_direction_forearm_weight = _yybArmDirectionForearmWeight,
                yyb_arm_direction_upper_arm_max_degrees = _yybArmDirectionUpperArmMaxDegrees,
                yyb_arm_direction_forearm_max_degrees = _yybArmDirectionForearmMaxDegrees,
                yyb_arm_direction_left_side_weight_scale = _yybArmDirectionLeftSideWeightScale,
                yyb_arm_direction_right_side_weight_scale = _yybArmDirectionRightSideWeightScale,
                yyb_arm_sleeve_anchor_override = _overrideYybArmSleeveAnchorRuntimeSettings,
                yyb_arm_sleeve_anchor_enabled = _enableYybArmSleeveAnchorRuntimeOverride,
                yyb_arm_sleeve_anchor_influence = _yybArmSleeveAnchorInfluence,
                yyb_arm_shoulder_cap_anchor_influence = _yybArmShoulderCapAnchorInfluence,
                yyb_arm_sleeve_anchor_max_degrees = _yybArmSleeveAnchorMaxDegrees,
                yyb_arm_visual_twist_override = _overrideYybArmVisualTwistRuntimeSettings,
                yyb_arm_visual_twist_enabled = _enableYybArmVisualTwistRuntimeOverride,
                yyb_arm_visual_upper_arm_influence = _yybArmVisualUpperArmInfluence,
                yyb_arm_visual_forearm_influence = _yybArmVisualForearmInfluence,
                yyb_arm_visual_upper_arm_max_degrees = _yybArmVisualUpperArmMaxDegrees,
                yyb_arm_visual_forearm_max_degrees = _yybArmVisualForearmMaxDegrees,
                manual_animator_lower_body_segment_direction_enabled = _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                manual_animator_lower_body_segment_direction_disabled =
                    _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                manual_animator_lower_body_segment_direction_weight = _manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manual_animator_lower_body_segment_direction_max_angle = _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                manual_animator_upper_leg_to_lower_leg_segment_direction_disabled =
                    _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle =
                    _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                manual_animator_lower_leg_to_foot_segment_direction_disabled =
                    _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                manual_animator_lower_leg_to_foot_segment_direction_max_angle =
                    _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manual_animator_left_lower_leg_to_foot_segment_direction_max_angle =
                    _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manual_animator_right_lower_leg_to_foot_segment_direction_max_angle =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                manual_animator_foot_to_toes_segment_direction_disabled =
                    _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                manual_animator_foot_to_toes_segment_direction_max_angle =
                    _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                manual_animator_foot_hips_aligned_residual_yaw_enabled = _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                manual_animator_foot_hips_aligned_residual_yaw_disabled =
                    _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                manual_animator_foot_hips_aligned_residual_yaw_weight = _manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manual_animator_foot_hips_aligned_residual_yaw_max_angle = _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                post_set_human_pose_right_endpoint_position_enabled =
                    _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride,
                post_set_human_pose_right_endpoint_position_weight =
                    _postSetHumanPoseRightEndpointPositionReferenceWeight,
                post_set_human_pose_right_endpoint_position_max_offset =
                    _postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                post_set_human_pose_right_endpoint_position_positive_z_scale =
                    _postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                post_set_human_pose_right_endpoint_position_toes_blend_weight =
                    _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                post_set_human_pose_right_endpoint_position_frame_gate_start =
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                post_set_human_pose_right_endpoint_position_frame_gate_end =
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                post_set_human_pose_endpoint_position_use_left_side =
                    _postSetHumanPoseEndpointPositionUseLeftSide,
                pre_set_human_pose_right_endpoint_position_enabled =
                    _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                pre_set_human_pose_right_endpoint_position_weight =
                    _preSetHumanPoseRightEndpointPositionReferenceWeight,
                pre_set_human_pose_right_endpoint_position_max_offset =
                    _preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                pre_set_human_pose_right_endpoint_position_positive_z_scale =
                    _preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                pre_set_human_pose_right_endpoint_position_toes_blend_weight =
                    _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                pre_set_human_pose_right_endpoint_position_frame_gate_start =
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                pre_set_human_pose_right_endpoint_position_frame_gate_end =
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                pre_set_human_pose_endpoint_position_use_left_side =
                    _preSetHumanPoseEndpointPositionUseLeftSide,
                pre_set_human_pose_endpoint_position_use_ghost_current_basis =
                    _preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                pre_set_human_pose_endpoint_position_invert_body_position_x =
                    _preSetHumanPoseEndpointPositionInvertBodyPositionX,
                pre_set_human_pose_endpoint_position_invert_body_position_z =
                    _preSetHumanPoseEndpointPositionInvertBodyPositionZ,
                post_set_human_pose_right_foot_evaluator_xz_reference_enabled =
                    _usePostSetHumanPoseRightFootEvaluatorXzReference,
                post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude =
                    _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                manual_animator_biped_ik_foot_position_enabled = _enableManualAnimatorBipedIkFootPositionRuntimeOverride,
                manual_animator_biped_ik_foot_position_weight = _manualAnimatorBipedIkFootPositionReferenceWeight,
                manual_animator_biped_ik_foot_position_max_offset = _manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                manual_animator_hips_local_position_enabled = _enableManualAnimatorHipsLocalPositionRuntimeOverride,
                manual_animator_hips_local_position_weight = _manualAnimatorHipsLocalPositionReferenceWeight,
                manual_animator_hips_local_position_max_offset = _manualAnimatorHipsLocalPositionReferenceMaxOffset,
                manual_animator_body_position_xz_enabled = _enableManualAnimatorBodyPositionXzRuntimeOverride,
                manual_animator_body_position_xz_weight = _manualAnimatorBodyPositionXzReferenceWeight,
                manual_animator_body_position_xz_max_offset = _manualAnimatorBodyPositionXzReferenceMaxOffset,
                manual_animator_body_position_xz_frame_gate_start =
                    _manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manual_animator_body_position_xz_frame_gate_end =
                    _manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manual_animator_body_position_xz_frame_gate_blend_frames =
                    _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manual_animator_body_position_xz_axis_x_scale =
                    _manualAnimatorBodyPositionXzReferenceAxisXScale,
                manual_animator_body_position_xz_axis_z_scale =
                    _manualAnimatorBodyPositionXzReferenceAxisZScale,
                retarget_body_position_xz_root_motion_enabled =
                    _enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                target_humanoid_bone_position_lock_disabled =
                    _disableTargetHumanoidBonePositionLockRuntimeOverride,
                vmd_playback_probe_enabled = _enableVmdPlaybackProbeRuntimeOverride,
                vmd_playback_probe_apply_ik_targets = _applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                vmd_playback_probe_source_vmd_path = MakeProjectRelativePath(_vmdPlaybackProbeSourceVmdPath),
                reference_mmd_timing_enabled = _enableReferenceMmdTimingRuntimeOverride,
                diagnostic_capture_width_override = _diagnosticCaptureWidthOverride,
                diagnostic_capture_height_override = _diagnosticCaptureHeightOverride,
                diagnostic_screenshot_padding_override = _diagnosticScreenshotPaddingOverride,
                diagnostic_screenshot_vertical_viewport_center_override =
                    _diagnosticScreenshotVerticalViewportCenterOverride,
                reference_clip_name = _referenceClip != null ? _referenceClip.name : string.Empty,
                reference_clip_asset_path = _referenceClipAssetPath,
                results = Results.ToArray(),
                frame_count_roles = frameRoleDiagnostics,
                sample_ordering_diagnostics = BuildSampleOrderingDiagnostics(),
                selected_candidate_artifact = BuildCandidateArtifactSelection(frameQualitySummaries),
                frame_quality_summaries = frameQualitySummaries,
                failures = Failures.ToArray()
            };

            string json = JsonUtility.ToJson(summary, true);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static void WriteSummaryMarkdown(
            string path,
            MotionComparisonFrameQualitySummary[] frameQualitySummaries = null,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = null)
        {
            frameRoleDiagnostics = frameRoleDiagnostics ?? BuildCurrentSummaryFrameRoleDiagnostics();
            frameQualitySummaries = frameQualitySummaries ?? BuildFrameQualitySummaries(frameRoleDiagnostics);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# YYB Visual Comparison Batch");
            builder.AppendLine();
            builder.AppendLine($"- session id: `{_summarySessionId}`");
            builder.AppendLine($"- generated at: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            builder.AppendLine($"- fbx file: `{_fbxFileName}`");
            builder.AppendLine($"- duration seconds: `{_durationSeconds:F2}`");
            builder.AppendLine($"- target frames: `{ResolveSummaryTargetFrameCount()}`");
            builder.AppendLine($"- segment: `{_editorDiagnosticSmokeSegment}`");
            builder.AppendLine($"- finger closeups: `{_enableFingerCloseups}`");
            builder.AppendLine($"- recorder parent IK offsets (center-parented): `{_enableRecorderParentFrameIkOffsetsWhenCenterParented}`");
            builder.AppendLine($"- MMD IK delta guard runtime override VMD: `{FormatRuntimeOverride(_mmdIkDeltaGuardLimitOverrideVmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery trigger VMD: `{FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryTriggerVmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery debt VMD: `{FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryDebtThresholdVmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery hold frames: `{FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryHoldFrames)}`");
            builder.AppendLine($"- Final IK foot grounding runtime override: `{_enableFinalIkFootGroundingRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body localRotation runtime override: `{_enableManualAnimatorFootLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body localRotation runtime disable: `{_disableManualAnimatorFootLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose runtime override: `{_enableManualAnimatorFullBodyPoseRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose runtime disable: `{_disableManualAnimatorFullBodyPoseRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose weight: `{_manualAnimatorFullBodyPoseReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator full-body pose exclude lower-body muscles: `{_manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose lower-body muscles only: `{_manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose leg twist muscles only: `{_manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator body rotation runtime override: `{_enableManualAnimatorBodyRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator body rotation runtime disable: `{_disableManualAnimatorBodyRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator body rotation weight: `{_manualAnimatorBodyRotationReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator hand local rotation runtime override: `{_enableManualAnimatorHandLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator thumb local rotation runtime override: `{_enableManualAnimatorThumbLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator hand palm-frame runtime override: `{_enableManualAnimatorHandPalmFrameRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator hand palm-frame weight: `{_manualAnimatorHandPalmFrameWeight:F3}`");
            builder.AppendLine($"- Retarget pose visual spike smoothing runtime settings override: `{_overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings}`");
            builder.AppendLine($"- Retarget pose visual spike smoothing enabled: `{_enableRetargetPoseVisualSpikeSmoothingRuntimeOverride}`");
            builder.AppendLine($"- Retarget pose visual spike current weight: `{_retargetPoseVisualSpikeCurrentWeight:F3}`");
            builder.AppendLine($"- Retarget pose visual spike forearm stretch clamp max offset: `{_retargetPoseVisualSpikeForearmStretchClampMaxOffset:F3}`");
            builder.AppendLine($"- YYB arm swing limit runtime override: `{_enableYybArmSwingLimitRuntimeOverride}`");
            builder.AppendLine($"- YYB arm swing limit weight: `{_yybArmSwingLimitWeight:F3}`");
            builder.AppendLine($"- YYB arm swing max down dot: `{_yybArmSwingMaxDownDot:F3}`");
            builder.AppendLine($"- YYB arm swing min hand horizontal ratio: `{_yybArmSwingMinHandHorizontalRatio:F3}`");
            builder.AppendLine($"- YYB arm swing max hand below shoulder ratio: `{_yybArmSwingMaxHandBelowShoulderRatio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach limit weight: `{_yybArmSwingHorizontalReachLimitWeight:F3}`");
            builder.AppendLine($"- YYB arm swing max hand horizontal reach ratio: `{_yybArmSwingMaxHandHorizontalReachRatio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach max hand below shoulder ratio: `{_yybArmSwingHorizontalReachMaxHandBelowShoulderRatio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach min elbow angle after apply: `{_yybArmSwingHorizontalReachMinElbowAngleAfterApply:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose horizontal reach limit weight: `{_yybArmSwingRaisedPoseHorizontalReachLimitWeight:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose min upper-arm down dot: `{_yybArmSwingRaisedPoseMinUpperArmDownDot:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose max hand below shoulder ratio: `{_yybArmSwingRaisedPoseMaxHandBelowShoulderRatio:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose max hand horizontal reach ratio: `{_yybArmSwingRaisedPoseMaxHandHorizontalReachRatio:F3}`");
            builder.AppendLine($"- YYB arm direction retarget runtime override: `{_enableYybArmDirectionRetargetRuntimeOverride}`");
            builder.AppendLine($"- YYB arm direction upper-arm weight: `{_yybArmDirectionUpperArmWeight:F3}`");
            builder.AppendLine($"- YYB arm direction forearm weight: `{_yybArmDirectionForearmWeight:F3}`");
            builder.AppendLine($"- YYB arm direction upper-arm max degrees: `{_yybArmDirectionUpperArmMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm direction forearm max degrees: `{_yybArmDirectionForearmMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm direction left-side weight scale: `{_yybArmDirectionLeftSideWeightScale:F3}`");
            builder.AppendLine($"- YYB arm direction right-side weight scale: `{_yybArmDirectionRightSideWeightScale:F3}`");
            builder.AppendLine($"- YYB arm sleeve anchor runtime settings override: `{_overrideYybArmSleeveAnchorRuntimeSettings}`");
            builder.AppendLine($"- YYB arm sleeve anchor runtime enabled: `{_enableYybArmSleeveAnchorRuntimeOverride}`");
            builder.AppendLine($"- YYB arm sleeve anchor influence: `{_yybArmSleeveAnchorInfluence:F3}`");
            builder.AppendLine($"- YYB arm shoulder cap anchor influence: `{_yybArmShoulderCapAnchorInfluence:F3}`");
            builder.AppendLine($"- YYB arm sleeve anchor max degrees: `{_yybArmSleeveAnchorMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm visual twist runtime settings override: `{_overrideYybArmVisualTwistRuntimeSettings}`");
            builder.AppendLine($"- YYB arm visual twist runtime enabled: `{_enableYybArmVisualTwistRuntimeOverride}`");
            builder.AppendLine($"- YYB arm visual upper-arm influence: `{_yybArmVisualUpperArmInfluence:F3}`");
            builder.AppendLine($"- YYB arm visual forearm influence: `{_yybArmVisualForearmInfluence:F3}`");
            builder.AppendLine($"- YYB arm visual upper-arm max degrees: `{_yybArmVisualUpperArmMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm visual forearm max degrees: `{_yybArmVisualForearmMaxDegrees:F3}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction runtime override: `{_enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction runtime disable: `{_disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction weight: `{_manualAnimatorLowerBodySegmentDirectionReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction max angle: `{_manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator UpperLegToLowerLeg segment direction runtime disable: `{_disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator UpperLegToLowerLeg segment direction max angle override: `{_manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator LowerLegToFoot segment direction runtime disable: `{_disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator LowerLegToFoot segment direction max angle override: `{_manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator Left LowerLegToFoot segment direction max angle override: `{_manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction max angle override: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction axis X/Z scale: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction blend weight: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction frame gate: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart:F0}-{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction endpoint blend weight: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight:F3}`");
            builder.AppendLine($"- Manual Animator FootToToes segment direction runtime disable: `{_disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator FootToToes segment direction max angle override: `{_manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw runtime override: `{_enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw runtime disable: `{_disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw weight: `{_manualAnimatorFootHipsAlignedResidualYawReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw max angle: `{_manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position runtime override: `{_enablePostSetHumanPoseRightEndpointPositionRuntimeOverride}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position weight: `{_postSetHumanPoseRightEndpointPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position max offset: `{_postSetHumanPoseRightEndpointPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position positive-Z scale: `{_postSetHumanPoseRightEndpointPositionReferencePositiveZScale:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position toes blend weight: `{_postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position frame gate: `{_postSetHumanPoseRightEndpointPositionReferenceFrameGateStart:F0}-{_postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Post-SetHumanPose endpoint position use left side: `{_postSetHumanPoseEndpointPositionUseLeftSide}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position runtime override: `{_enablePreSetHumanPoseRightEndpointPositionRuntimeOverride}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position weight: `{_preSetHumanPoseRightEndpointPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position max offset: `{_preSetHumanPoseRightEndpointPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position positive-Z scale: `{_preSetHumanPoseRightEndpointPositionReferencePositiveZScale:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position toes blend weight: `{_preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position frame gate: `{_preSetHumanPoseRightEndpointPositionReferenceFrameGateStart:F0}-{_preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint position use left side: `{_preSetHumanPoseEndpointPositionUseLeftSide}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint position use ghost/current basis: `{_preSetHumanPoseEndpointPositionUseGhostCurrentBasis}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint bodyPosition invert X/Z: `{_preSetHumanPoseEndpointPositionInvertBodyPositionX}/{_preSetHumanPoseEndpointPositionInvertBodyPositionZ}`");
            builder.AppendLine($"- Post-SetHumanPose right foot evaluator X/Z reference: `{_usePostSetHumanPoseRightFootEvaluatorXzReference}`");
            builder.AppendLine($"- Post-SetHumanPose right foot evaluator X/Z target magnitude: `{_postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude:F3}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position runtime override: `{_enableManualAnimatorBipedIkFootPositionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position weight: `{_manualAnimatorBipedIkFootPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position max offset: `{_manualAnimatorBipedIkFootPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Manual Animator Hips local-position runtime override: `{_enableManualAnimatorHipsLocalPositionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator Hips local-position weight: `{_manualAnimatorHipsLocalPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator Hips local-position max offset: `{_manualAnimatorHipsLocalPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z runtime override: `{_enableManualAnimatorBodyPositionXzRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z weight: `{_manualAnimatorBodyPositionXzReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z max offset: `{_manualAnimatorBodyPositionXzReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z frame gate: `{_manualAnimatorBodyPositionXzReferenceFrameGateStart:F0}-{_manualAnimatorBodyPositionXzReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z frame gate blend frames: `{_manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames:F0}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z axis scale: `{_manualAnimatorBodyPositionXzReferenceAxisXScale:F3}/{_manualAnimatorBodyPositionXzReferenceAxisZScale:F3}`");
            builder.AppendLine($"- Retarget bodyPosition X/Z root motion runtime override: `{_enableRetargetBodyPositionXzRootMotionRuntimeOverride}`");
            builder.AppendLine($"- Target humanoid bone position lock disabled runtime override: `{_disableTargetHumanoidBonePositionLockRuntimeOverride}`");
            builder.AppendLine($"- VMD playback probe runtime override: `{_enableVmdPlaybackProbeRuntimeOverride}`");
            builder.AppendLine($"- VMD playback probe apply IK targets: `{_applyVmdPlaybackProbeIkTargetsRuntimeOverride}`");
            builder.AppendLine($"- VMD playback probe source VMD: `{EscapeMarkdown(MakeProjectRelativePath(_vmdPlaybackProbeSourceVmdPath))}`");
            builder.AppendLine($"- reference MMD timing runtime override: `{_enableReferenceMmdTimingRuntimeOverride}`");
            builder.AppendLine($"- diagnostic capture width override: `{FormatRuntimeOverride(_diagnosticCaptureWidthOverride)}`");
            builder.AppendLine($"- diagnostic capture height override: `{FormatRuntimeOverride(_diagnosticCaptureHeightOverride)}`");
            builder.AppendLine($"- diagnostic screenshot padding override: `{FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotPaddingOverride)}`");
            builder.AppendLine($"- diagnostic screenshot viewport center override: `{FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotVerticalViewportCenterOverride)}`");
            builder.AppendLine($"- reference clip: `{(_referenceClip != null ? _referenceClip.name : "")}`");
            builder.AppendLine($"- reference clip asset: `{EscapeMarkdown(_referenceClipAssetPath)}`");
            builder.AppendLine();

            builder.AppendLine("## Frame Count Roles");
            builder.AppendLine();
            builder.AppendLine($"- ref target: `{frameRoleDiagnostics.reference_target_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.target_frame_count_role)})");
            builder.AppendLine($"- Sub_Manual baseline recorded frames: `{frameRoleDiagnostics.baseline_recorded_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.baseline_recorded_frame_count_role)})");
            builder.AppendLine($"- Main_Auto candidate recorded frames: `{frameRoleDiagnostics.candidate_recorded_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.candidate_recorded_frame_count_role)})");
            builder.AppendLine($"- metric basis: {EscapeMarkdown(frameRoleDiagnostics.frame_quality_metric_basis)}");
            builder.AppendLine();
            builder.AppendLine("## Reference MP4 Diagnostics");
            builder.AppendLine();
            builder.AppendLine($"- provenance: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_provenance_evidence_path)}` (exists={frameRoleDiagnostics.reference_mp4_provenance_evidence_exists})");
            builder.AppendLine($"- analysis result: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_analysis_result_path)}` (exists={frameRoleDiagnostics.reference_mp4_analysis_result_exists})");
            builder.AppendLine($"- frame metrics: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_frame_metrics_path)}` (exists={frameRoleDiagnostics.reference_mp4_frame_metrics_exists})");
            builder.AppendLine($"- contact sheet: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_contact_sheet_path)}` (exists={frameRoleDiagnostics.reference_mp4_contact_sheet_exists})");
            builder.AppendLine($"- canonical context: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_canonical_context)}");
            builder.AppendLine($"- video: `{frameRoleDiagnostics.reference_mp4_width}x{frameRoleDiagnostics.reference_mp4_height}`, `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_avg_frame_rate)}`, frames `{frameRoleDiagnostics.reference_mp4_total_video_frames}`, duration `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_stream_duration_seconds)}`");
            builder.AppendLine($"- bbox metrics: samples `{frameRoleDiagnostics.reference_mp4_frame_metrics_sample_count}`, avg height `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_avg_bbox_height_ratio)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_avg_bbox_width_ratio)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_center_x_range_ratio)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_max_bottom_gap_ratio)}`");
            builder.AppendLine($"- current clip coverage: start `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_start_seconds)}`, end `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_end_seconds)}`, duration `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_duration_seconds)}`, samples `{frameRoleDiagnostics.reference_mp4_current_clip_sample_count}`, first local `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_first_sample_seconds)}`, last local `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_last_sample_seconds)}`, coverage `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_sample_coverage_ratio)}`, gap `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_sample_gap_seconds)}`");
            builder.AppendLine($"- current clip bbox metrics: avg height `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_avg_bbox_height_ratio)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_avg_bbox_width_ratio)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_center_x_range_ratio)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_max_bottom_gap_ratio)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_avg_bright_area_ratio)}`");
            builder.AppendLine($"- current clip coverage basis: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_current_clip_sample_basis)}");
            builder.AppendLine($"- current clip framing basis: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_current_clip_framing_metric_basis)}");
            builder.AppendLine($"- candidate screenshot framing: index `{EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_frame_index_path)}` (exists={frameRoleDiagnostics.candidate_screenshot_frame_index_exists}), view `{EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_frame_metrics_view)}`, samples `{frameRoleDiagnostics.candidate_screenshot_frame_metrics_sample_count}`, nonblank `{frameRoleDiagnostics.candidate_screenshot_nonblank_frame_count}`, avg height `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_bbox_height_ratio)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_bbox_width_ratio)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_center_x_range_ratio)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_max_bottom_gap_ratio)}`, max top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_max_top_gap_ratio)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_bright_area_ratio)}`");
            builder.AppendLine($"- candidate screenshot timing: samples `{frameRoleDiagnostics.candidate_screenshot_time_sample_count}`, first `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_first_sample_seconds)}`, last `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_last_sample_seconds)}`, coverage `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_sample_coverage_ratio)}`, gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_sample_gap_seconds)}`, max ref gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_max_ref_sample_seconds_gap)}`, avg ref gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_ref_sample_seconds_gap)}`");
            builder.AppendLine($"- candidate/ref time-matched framing: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_sample_count}`, max time gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_seconds_gap)}`, avg bbox height abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta)}`, max bbox height abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta)}`, avg bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta)}`, max bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta)}`, avg center X abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta)}`, max bottom gap abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta)}`, avg bright area abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched limb bands: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_limb_band_sample_count}`, avg upper span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta)}`, max upper span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta)}`, avg lower span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta)}`, max lower span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched silhouette profile: bands `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_profile_band_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_profile_sample_count}`, avg L1 abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta)}`, max L1 abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta)}`, max band abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched silhouette landmarks: bands `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_landmark_band_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_landmark_sample_count}`, avg endpoint abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta)}`, max endpoint abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched image-space keypoints: keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_keypoint_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_keypoint_sample_count}`, avg L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta)}`, max L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta)}`");
            builder.AppendLine($"- candidate/ref bbox-normalized keypoints: keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count}`, avg L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta)}`, avg removed `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization)}`, max removed `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization)}`");
            builder.AppendLine($"- candidate/ref non-hair bbox-normalized keypoints: keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count}`, avg L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta)}`, max label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label)}`");
            builder.AppendLine($"- candidate/ref bbox-normalized max keypoint attribution: label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label)}`, keypoint `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_index}`, ref seconds `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds)}`, candidate seconds `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds)}`, recorder frame `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame}`, x delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta)}`, y delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta)}`");
            builder.AppendLine($"- candidate/ref bbox-normalized max keypoint crop context: ref touches edge `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_touches_frame_edge}`, candidate touches edge `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_touches_frame_edge}`, ref bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap)}`, ref top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_top_gap)}`, candidate bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap)}`, candidate top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_top_gap)}`");
            builder.AppendLine($"- candidate/ref crop-safe time-matched: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_sample_count}`, avg bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta)}`, max bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_width_ratio_abs_delta)}`, silhouette samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_silhouette_profile_sample_count}`, avg silhouette L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_silhouette_profile_l1_abs_delta)}`, max silhouette L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_silhouette_profile_l1_abs_delta)}`");
            builder.AppendLine($"- candidate/ref crop-safe keypoints: image samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_image_space_keypoint_sample_count}`, avg image L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta)}`, max image L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_image_space_keypoint_l1_delta)}`, bbox-normalized samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count}`, avg bbox-normalized L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max bbox-normalized L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta)}`");
            builder.AppendLine($"- candidate/ref keypoint-local crop-safe bbox-normalized keypoints: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count}`, keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count}`, excluded keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count}`, avg L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta)}`, max label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label)}`");
            builder.AppendLine($"- candidate vs ref framing deltas: avg height `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_avg_bbox_height_ratio_delta)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_avg_bbox_width_ratio_delta)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_center_x_range_ratio_delta)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_max_bottom_gap_ratio_delta)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_avg_bright_area_ratio_delta)}`");
            builder.AppendLine($"- candidate vs current-clip ref framing deltas: avg height `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_avg_bbox_width_ratio_delta)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_center_x_range_ratio_delta)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_avg_bright_area_ratio_delta)}`");
            builder.AppendLine($"- candidate screenshot basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_frame_metrics_basis)}");
            builder.AppendLine($"- candidate screenshot timing basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_sample_timing_basis)}");
            builder.AppendLine($"- candidate/ref time-matched basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_framing_metric_basis)}");
            builder.AppendLine($"- candidate/ref image-space limb span basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_limb_span_basis)}");
            builder.AppendLine($"- candidate/ref image-space limb band basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_limb_band_basis)}");
            builder.AppendLine($"- candidate/ref silhouette profile basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_profile_basis)}");
            builder.AppendLine($"- candidate/ref silhouette landmark basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_landmark_basis)}");
            builder.AppendLine($"- candidate/ref image-space keypoint basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_keypoint_basis)}");
            builder.AppendLine($"- candidate/ref bbox-normalized keypoint basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_basis)}");
            builder.AppendLine($"- candidate/ref non-hair bbox-normalized keypoint basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis)}");
            builder.AppendLine($"- candidate/ref crop-safe basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_basis)}");
            builder.AppendLine($"- candidate/ref keypoint-local crop-safe basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_basis)}");
            builder.AppendLine($"- basis: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_analysis_metric_basis)}");
            builder.AppendLine();

            builder.AppendLine("## Results");
            builder.AppendLine();
            builder.AppendLine("| job | scene | target | success | session | csv | frames | vmd |");
            builder.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (CaptureResult result in Results)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdown(result.jobDisplayName)} | {EscapeMarkdown(result.sceneName)} | {EscapeMarkdown(result.targetName)} | {result.success} | " +
                    $"`{EscapeMarkdown(result.comparisonSessionId)}` | `{EscapeMarkdown(result.comparisonMetricsCsvPath)}` | " +
                    $"`{EscapeMarkdown(result.comparisonFrameFolderPath)}` | `{EscapeMarkdown(result.vmdPath)}` |");
            }

            CaptureResult[] effectiveSettingsResults = Results
                .Where(result => result.hasFileManagerEffectiveSettings)
                .ToArray();
            if (effectiveSettingsResults.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Main Scene Effective Settings");
                builder.AppendLine();
                builder.AppendLine("| job | foot local rot | full-body pose | body rot | lower segment | foot yaw | post-set endpoint | pre-set endpoint | evaluator X/Z | arm swing | sleeve | visual twist |");
                builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (CaptureResult result in effectiveSettingsResults)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(result.jobDisplayName)} | " +
                        $"{FormatEnabledWeight(result.useManualAnimatorFootLocalRotationReference, result.manualAnimatorFootLocalRotationReferenceWeight)} | " +
                        $"{FormatEnabledWeight(result.useManualAnimatorFullBodyPoseReference, result.manualAnimatorFullBodyPoseReferenceWeight)} | " +
                        $"{FormatEnabledWeight(result.useManualAnimatorBodyRotationReference, result.manualAnimatorBodyRotationReferenceWeight)} | " +
                        $"{FormatEnabledWeightCap(result.useManualAnimatorLowerBodySegmentDirectionReference, result.manualAnimatorLowerBodySegmentDirectionReferenceWeight, result.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle)} | " +
                        $"{FormatEnabledWeightCap(result.useManualAnimatorFootHipsAlignedResidualYawReference, result.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, result.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle)} | " +
                        $"{FormatEnabledWeightCapScaleBlendGate(result.usePostSetHumanPoseRightEndpointPositionReference, result.postSetHumanPoseRightEndpointPositionReferenceWeight, result.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, result.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, result.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, result.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart, result.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd)} | " +
                        $"{FormatEnabledWeightCapScaleBlendGate(result.usePreSetHumanPoseRightEndpointPositionReference, result.preSetHumanPoseRightEndpointPositionReferenceWeight, result.preSetHumanPoseRightEndpointPositionReferenceMaxOffset, result.preSetHumanPoseRightEndpointPositionReferencePositiveZScale, result.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, result.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart, result.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd)} | " +
                        $"{FormatEvaluatorXzReferenceSettings(result)} | " +
                        $"{FormatArmSwingSettings(result)} | " +
                        $"{result.enableYybArmSleeveAnchorCorrection} | " +
                        $"{result.enableYybArmVisualTwistCorrection} |");
                }
            }

            SummarySampleOrderingDiagnostic[] sampleOrderingDiagnostics = BuildSampleOrderingDiagnostics();
            if (sampleOrderingDiagnostics.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Sample Ordering Diagnostics");
                builder.AppendLine();
                builder.AppendLine("| job | scene | rows | first reason | first recorderFrame | first engine frame | recorder span | engine span | first clip time | first grounding step | first step/max | first step at max | grounding clamp delta | grounding smooth delta | finish recorderFrame | finish engine frame |");
                builder.AppendLine("|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|");
                foreach (SummarySampleOrderingDiagnostic diagnostic in sampleOrderingDiagnostics)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(diagnostic.job_mode)} | {EscapeMarkdown(diagnostic.scene_name)} | {diagnostic.metric_row_count} | " +
                        $"{EscapeMarkdown(diagnostic.first_metric_reason)} | {diagnostic.first_metric_recorder_frame} | " +
                        $"{diagnostic.first_metric_engine_frame_count} | {diagnostic.recording_metric_recorder_frame_span} | " +
                        $"{diagnostic.recording_metric_engine_frame_span} | {FormatQualityFloat(diagnostic.first_metric_animation_clip_time)} | " +
                        $"{FormatQualityFloat(diagnostic.first_metric_grounding_vertical_step_last)} | " +
                        $"{FormatQualityFloat(diagnostic.first_metric_grounding_vertical_step_to_max_ratio)} | " +
                        $"{diagnostic.first_metric_grounding_vertical_step_at_max_step} | " +
                        $"{diagnostic.recording_grounding_step_clamp_delta} | {diagnostic.recording_grounding_smoothed_delta} | " +
                        $"{diagnostic.finish_metric_recorder_frame} | {diagnostic.finish_metric_engine_frame_count} |");
                }
            }

            SummaryCandidateArtifactSelection selectedCandidate = BuildCandidateArtifactSelection(frameQualitySummaries);
            if (selectedCandidate != null && !string.IsNullOrWhiteSpace(selectedCandidate.selected_candidate_vmd_path))
            {
                builder.AppendLine();
                builder.AppendLine("## Selected Candidate Artifact");
                builder.AppendLine();
                builder.AppendLine("| selected role | output role | status | acceptance artifact | metrics | vmd | manifest | files | raw status | corrected status | preserves raw diagnostic | basis |");
                builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
                builder.AppendLine(
                    $"| {EscapeMarkdown(selectedCandidate.selected_candidate_role)} | {EscapeMarkdown(selectedCandidate.selected_candidate_output_role)} | " +
                    $"{EscapeMarkdown(selectedCandidate.selected_candidate_status)} | {selectedCandidate.selected_candidate_is_acceptance_artifact} | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_metrics_csv)}` | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_vmd_path)}` | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_manifest_path)}` | " +
                    $"vmd={selectedCandidate.selected_candidate_vmd_exists}, metrics={selectedCandidate.selected_candidate_metrics_exists}, manifest={selectedCandidate.selected_candidate_manifest_exists}, rawVmdDiff={selectedCandidate.selected_candidate_differs_from_raw_vmd}, rawMetricsDiff={selectedCandidate.selected_candidate_differs_from_raw_metrics} | " +
                    $"{EscapeMarkdown(selectedCandidate.raw_candidate_status)} | {EscapeMarkdown(selectedCandidate.corrected_candidate_status)} | " +
                    $"{selectedCandidate.selected_candidate_preserves_raw_diagnostic} | " +
                    $"{EscapeMarkdown(selectedCandidate.selected_candidate_acceptance_basis)}; {EscapeMarkdown(selectedCandidate.selection_basis)} |");
            }

            if (frameQualitySummaries.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Frame Quality Gate");
                builder.AppendLine();
                builder.AppendLine("| baseline | candidate | evaluation | status | mmd | compared frames | foot min Y | root delta | center step | local foot IK min Y | effective foot IK min Y | metrics | mmd screenshot | mmd report | vmd | reason |");
                builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---:|---|---|---|---|---|---|");
                foreach (MotionComparisonFrameQualitySummary summary in frameQualitySummaries)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(summary.baseline_label)} | {EscapeMarkdown(summary.candidate_label)} | {EscapeMarkdown(summary.frame_quality_evaluation_role)} | {EscapeMarkdown(summary.status)} | " +
                        $"{EscapeMarkdown(summary.mmd_result_status)} | {summary.compared_frames} | {FormatQualityFloat(summary.min_candidate_foot_bottom_y)} | " +
                        $"{FormatQualityFloat(summary.max_same_frame_root_position_delta)} | {FormatQualityFloat(summary.max_candidate_vmd_center_step)} | " +
                        $"{FormatQualityFloat(summary.min_candidate_vmd_foot_ik_y)} | {FormatQualityFloat(summary.min_candidate_vmd_effective_foot_ik_y)} | " +
                        $"`{EscapeMarkdown(summary.candidate_metrics_csv)}` | " +
                        $"`{EscapeMarkdown(summary.mmd_after_play_screenshot_path)}` | `{EscapeMarkdown(summary.mmd_report_path)}` | " +
                        $"`{EscapeMarkdown(summary.candidate_vmd_path)}` | " +
                        $"{EscapeMarkdown(summary.status_reason)} |");
                }
            }

            if (Failures.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Failures");
                builder.AppendLine();
                foreach (string failure in Failures)
                {
                    builder.AppendLine($"- {EscapeMarkdown(failure)}");
                }
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummaries()
        {
            return BuildFrameQualitySummaries(BuildCurrentSummaryFrameRoleDiagnostics());
        }

        private static SummaryFrameRoleDiagnostics BuildCurrentSummaryFrameRoleDiagnostics()
        {
            return BuildSummaryFrameRoleDiagnostics(
                ResolveSummaryTargetFrameCount(),
                ResolveFrameCount(CaptureMode.SubManualTestPrefab),
                ResolveFrameCount(CaptureMode.MainAuto));
        }

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummaries(
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            CaptureResult baseline = Results.FirstOrDefault(result =>
                string.Equals(result.jobMode, CaptureMode.SubManualTestPrefab.ToString(), StringComparison.Ordinal));
            if (baseline == null)
            {
                return Array.Empty<MotionComparisonFrameQualitySummary>();
            }

            List<MotionComparisonFrameQualitySummary> frameQualitySummaries = new List<MotionComparisonFrameQualitySummary>();
            foreach (CaptureResult candidate in EnumerateMainSceneCandidates())
            {
                frameQualitySummaries.AddRange(BuildFrameQualitySummariesForCandidate(baseline, candidate));
            }

            foreach (MotionComparisonFrameQualitySummary frameQualitySummary in frameQualitySummaries)
            {
                MotionComparisonProbeReportWriter.AttachLatestMmdAutomationEvidence(
                    frameQualitySummary,
                    _projectRoot,
                    Path.Combine(_projectRoot, MmdAutomationRunsRelativePath));
            }

            MotionComparisonFrameQualitySummary[] summaries = frameQualitySummaries.ToArray();
            ApplyImportedFbxVisualEvidenceFrameQualityPolicy(summaries, frameRoleDiagnostics);
            return summaries;
        }

        private static void PromoteFrameQualityFailuresToRunFailures(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = null)
        {
            foreach (string failure in BuildFrameQualityFailureMessages(frameQualitySummaries, frameRoleDiagnostics))
            {
                if (!Failures.Contains(failure))
                {
                    Failures.Add(failure);
                }
            }
        }

        private static string[] BuildFrameQualityFailureMessages(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            return BuildFrameQualityFailureMessages(frameQualitySummaries, null);
        }

        private static string[] BuildFrameQualityFailureMessages(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            if (frameQualitySummaries == null || frameQualitySummaries.Length == 0)
            {
                return Array.Empty<string>();
            }

            if (frameRoleDiagnostics != null)
            {
                ApplyImportedFbxVisualEvidenceFrameQualityPolicy(frameQualitySummaries, frameRoleDiagnostics);
            }

            List<string> failures = new List<string>();
            bool acceptedUserFacingArtifactPreservesRawDiagnostic =
                HasAcceptedUserFacingArtifactPreservingRawDiagnostic(frameQualitySummaries);
            foreach (MotionComparisonFrameQualitySummary summary in frameQualitySummaries)
            {
                if (summary == null ||
                    !string.Equals(summary.status, "fail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (acceptedUserFacingArtifactPreservesRawDiagnostic &&
                    IsRawCandidateRole(summary))
                {
                    continue;
                }

                string candidate = string.IsNullOrWhiteSpace(summary.candidate_label)
                    ? "unknown candidate"
                    : summary.candidate_label;
                string role = string.IsNullOrWhiteSpace(summary.frame_quality_evaluation_role)
                    ? "unknown_role"
                    : summary.frame_quality_evaluation_role;
                string reason = string.IsNullOrWhiteSpace(summary.status_reason)
                    ? "status=fail"
                    : summary.status_reason;

                failures.Add(
                    "frame-quality gate failed: " +
                    $"candidate={candidate}; " +
                    $"role={role}; " +
                    $"reason={reason}; " +
                    $"metrics={summary.candidate_metrics_csv ?? string.Empty}; " +
                    $"vmd={summary.candidate_vmd_path ?? string.Empty}");
            }

            return failures.ToArray();
        }

        private static bool HasAcceptedUserFacingArtifactPreservingRawDiagnostic(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            SummaryCandidateArtifactSelection selection = BuildCandidateArtifactSelection(frameQualitySummaries);
            return selection != null &&
                selection.selected_candidate_is_acceptance_artifact &&
                selection.selected_candidate_preserves_raw_diagnostic &&
                string.Equals(selection.selected_candidate_output_role, "user_facing_export_artifact", StringComparison.Ordinal);
        }

        private static void ApplyImportedFbxVisualEvidenceFrameQualityPolicy(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            if (!HasReferenceAlignedImportedFbxVisualEvidence(frameRoleDiagnostics) ||
                frameQualitySummaries == null ||
                frameQualitySummaries.Length == 0)
            {
                return;
            }

            foreach (MotionComparisonFrameQualitySummary summary in frameQualitySummaries)
            {
                if (summary == null ||
                    !string.Equals(summary.status, "fail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsSubManualPoseOnlyResidual(summary))
                {
                    MarkFrameQualityAsImportedFbxReferenceAligned(
                        summary,
                        "Sub_Manual Unity pose delta kept as diagnostic because time-matched ref MP4 image-space evidence is aligned");
                    continue;
                }

                if (IsEvaluationCandidateRole(summary) &&
                    HasReferenceAlignedCorrectedCounterpart(summary, frameQualitySummaries))
                {
                    MarkFrameQualityAsImportedFbxReferenceAligned(
                        summary,
                        "raw replay vertical residual kept as diagnostic because corrected candidate and ref MP4 image-space evidence are aligned");
                }
            }
        }

        private static bool HasReferenceAlignedImportedFbxVisualEvidence(
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            if (frameRoleDiagnostics == null)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(frameRoleDiagnostics.candidate_screenshot_frame_metrics_error) &&
                string.IsNullOrWhiteSpace(frameRoleDiagnostics.reference_mp4_analysis_error) &&
                string.IsNullOrWhiteSpace(frameRoleDiagnostics.reference_mp4_frame_metrics_error) &&
                frameRoleDiagnostics.reference_mp4_current_clip_sample_count >= ReferenceAlignedVisualEvidenceMinMatchedSamples &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_sample_count >= ReferenceAlignedVisualEvidenceMinMatchedSamples &&
                frameRoleDiagnostics.candidate_screenshot_nonblank_frame_count >=
                    frameRoleDiagnostics.candidate_vs_reference_time_matched_sample_count &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_seconds_gap) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_seconds_gap <= ReferenceAlignedVisualEvidenceMaxSecondsGap &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxBboxHeightDelta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxBottomGapDelta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxSilhouetteProfileL1Delta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxSilhouetteProfileBandDelta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta) &&
                IsWithinEndpointPixelTolerance(
                    frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta,
                    ReferenceAlignedVisualEvidenceMaxSilhouetteLandmarkEndpointDelta);
        }

        private static bool IsWithinEndpointPixelTolerance(float value, float threshold)
        {
            return value <= threshold + ReferenceAlignedVisualEvidenceEndpointPixelTolerance;
        }

        private static bool IsSubManualPoseOnlyResidual(MotionComparisonFrameQualitySummary summary)
        {
            string reason = summary.status_reason ?? string.Empty;
            return reason.IndexOf("same-frame limb pose delta threshold exceeded", StringComparison.OrdinalIgnoreCase) >= 0 &&
                reason.IndexOf("YYB deformation risk", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("below-floor", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("root position delta threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("one-frame root/center/IK teleport", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("stationary preview limb-motion root travel", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("same-frame hips Y delta fail threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("same-frame foot bottom Y delta fail threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool HasReferenceAlignedCorrectedCounterpart(
            MotionComparisonFrameQualitySummary summary,
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            string candidate = summary.candidate_label ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            return frameQualitySummaries.Any(other =>
                !ReferenceEquals(other, summary) &&
                other != null &&
                IsCorrectedCandidateForRawCandidate(candidate, other.candidate_label ?? string.Empty) &&
                IsCorrectedCandidateRole(other) &&
                (string.Equals(other.status, "pass", StringComparison.OrdinalIgnoreCase) ||
                    IsSubManualPoseOnlyResidual(other)));
        }

        private static bool IsCorrectedCandidateForRawCandidate(string rawCandidateLabel, string correctedCandidateLabel)
        {
            if (string.IsNullOrWhiteSpace(rawCandidateLabel) ||
                string.IsNullOrWhiteSpace(correctedCandidateLabel))
            {
                return false;
            }

            return string.Equals(correctedCandidateLabel, rawCandidateLabel, StringComparison.Ordinal) ||
                correctedCandidateLabel.StartsWith(rawCandidateLabel + " ", StringComparison.Ordinal);
        }

        private static bool IsEvaluationCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return string.Equals(
                summary.frame_quality_evaluation_role,
                "evaluation_candidate_metrics",
                StringComparison.Ordinal);
        }

        private static bool IsCorrectedCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return string.Equals(
                summary.frame_quality_evaluation_role,
                "corrected_candidate_metrics",
                StringComparison.Ordinal);
        }

        private static void MarkFrameQualityAsImportedFbxReferenceAligned(
            MotionComparisonFrameQualitySummary summary,
            string basis)
        {
            summary.status = "pass";
            summary.status_reason = string.IsNullOrWhiteSpace(summary.status_reason)
                ? basis
                : $"{basis}; diagnostic={summary.status_reason}";
        }

        private static IEnumerable<CaptureResult> EnumerateMainSceneCandidates()
        {
            return Results.Where(result =>
                result != null &&
                IsMainSceneCandidateMode(result.jobMode) &&
                ShouldBuildFrameQualityDiagnostic(result.success, result.comparisonMetricsCsvPath, result.vmdPath));
        }

        private static bool IsMainSceneCandidateMode(string jobMode)
        {
            return string.Equals(jobMode, CaptureMode.MainRecording.ToString(), StringComparison.Ordinal) ||
                string.Equals(jobMode, CaptureMode.MainRecordingVmdPlaybackProbe.ToString(), StringComparison.Ordinal) ||
                string.Equals(jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal);
        }

        private static bool ShouldBuildFrameQualityDiagnostic(bool success, string metricsCsvPath, string vmdPath)
        {
            return success ||
                (!string.IsNullOrWhiteSpace(metricsCsvPath) &&
                    !string.IsNullOrWhiteSpace(vmdPath));
        }

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummariesForCandidate(
            CaptureResult baseline,
            CaptureResult candidate)
        {
            if (baseline == null || candidate == null)
            {
                return Array.Empty<MotionComparisonFrameQualitySummary>();
            }

            ResolveShortCandidateVmdPath(candidate);
            MotionComparisonFrameQualitySummary summary = MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                baseline.jobDisplayName,
                ToAbsoluteProjectPath(baseline.comparisonMetricsCsvPath),
                candidate.jobDisplayName,
                ToAbsoluteProjectPath(candidate.comparisonMetricsCsvPath),
                ToAbsoluteProjectPath(candidate.vmdPath),
                baseline.frameCount,
                candidate.frameCount,
                ResolveSummaryTargetFrameCount());
            string integratedVerticalSolveRole = ResolveIntegratedVerticalSolveRole(candidate.jobMode);
            if (!string.IsNullOrEmpty(integratedVerticalSolveRole) &&
                MotionComparisonProbeReportWriter.TryPromoteVerticalSolveCorrectedCandidateToPrimaryExport(
                    summary,
                    out VerticalSolvePrimaryExportPromotion promotion))
            {
                candidate.fileSizeBytes = promotion.promoted_vmd_bytes;
                summary = MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                    baseline.jobDisplayName,
                    ToAbsoluteProjectPath(baseline.comparisonMetricsCsvPath),
                    candidate.jobDisplayName,
                    ToAbsoluteProjectPath(candidate.comparisonMetricsCsvPath),
                    ToAbsoluteProjectPath(candidate.vmdPath),
                    baseline.frameCount,
                    candidate.frameCount,
                    ResolveSummaryTargetFrameCount());
                summary.frame_quality_evaluation_role = integratedVerticalSolveRole;
                summary.frame_quality_evaluation_basis = ResolveIntegratedVerticalSolveBasis(candidate.jobMode);
                summary.vertical_solve_corrected_candidate_manifest_path = promotion.integrated_manifest_path;
            }
            MotionComparisonFrameQualitySummary[] summaries =
                MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(summary);
            return summaries;
        }

        private static string ResolveIntegratedVerticalSolveRole(string jobMode)
        {
            if (string.Equals(jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal))
            {
                return "main_auto_integrated_vertical_solve_metrics";
            }

            if (string.Equals(jobMode, CaptureMode.MainRecordingVmdPlaybackProbe.ToString(), StringComparison.Ordinal))
            {
                return "vmd_replay_integrated_vertical_solve_metrics";
            }

            return string.Empty;
        }

        private static string ResolveIntegratedVerticalSolveBasis(string jobMode)
        {
            if (string.Equals(jobMode, CaptureMode.MainRecordingVmdPlaybackProbe.ToString(), StringComparison.Ordinal))
            {
                return "primary VMD replay diagnostic output after bounded vertical solve promotion; raw replay metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
            }

            return "primary Main_Auto result paths after bounded vertical solve promotion; raw metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
        }

        private static void ResolveShortCandidateVmdPath(CaptureResult candidate)
        {
            if (candidate == null ||
                !IsMainSceneCandidateMode(candidate.jobMode) ||
                string.Equals(candidate.jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(_summaryDirectory))
            {
                return;
            }

            string sourceExtension = Path.GetExtension(candidate.vmdPath);
            if (string.IsNullOrWhiteSpace(sourceExtension))
            {
                sourceExtension = ".vmd";
            }

            string shortPath = Path.Combine(
                _summaryDirectory,
                BuildCandidateVmdEvidenceFileName(candidate.jobMode, sourceExtension));
            if (!File.Exists(shortPath))
            {
                return;
            }

            string currentAbsolutePath = ToAbsoluteProjectPath(candidate.vmdPath);
            if (string.Equals(currentAbsolutePath, shortPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            candidate.vmdPath = MakeProjectRelativePath(shortPath);
            candidate.fileSizeBytes = new FileInfo(shortPath).Length;
            SavePersistedState();
        }

        private static int ResolveFrameCount(CaptureMode mode)
        {
            CaptureResult result = Results.FirstOrDefault(captureResult =>
                string.Equals(captureResult.jobMode, mode.ToString(), StringComparison.Ordinal));
            return result != null ? result.frameCount : 0;
        }

        private static int ResolveMainAutoFrameCount()
        {
            return ResolveFrameCount(CaptureMode.MainAuto);
        }

        private static int ResolveSummaryTargetFrameCount()
        {
            return ResolveSummaryTargetFrameCount(
                ResolveReferenceMmdTargetFrameCount(
                    _fbxFileName,
                    _durationSeconds,
                    _targetFrameCount,
                    _referenceClip != null ? _referenceClip.length : 0f,
                    DefaultFrameRate),
                ResolveMainAutoFrameCount());
        }

        private static int ResolveSummaryTargetFrameCount(int referenceTargetFrameCount, int mainAutoFrameCount)
        {
            _ = mainAutoFrameCount;
            return Mathf.Max(0, referenceTargetFrameCount);
        }

        private static int ResolveReferenceMmdTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate)
        {
            if (TryResolveKnownMmdReferenceTargetFrameCount(
                    fbxFileName,
                    requestedDurationSeconds,
                    configuredTargetFrameCount,
                    referenceClipLengthSeconds,
                    recordingFrameRate,
                    out int referenceTargetFrameCount))
            {
                return referenceTargetFrameCount;
            }

            return Mathf.Max(0, configuredTargetFrameCount);
        }

        private static bool TryResolveKnownMmdReferenceTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate,
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
                float.IsInfinity(referenceClipLengthSeconds))
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(fbxFileName ?? string.Empty);
            if (!string.Equals(cleanBaseName, SatisfactionReferenceOutputBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int knownReferenceFrameCount = SatisfactionReferenceMaxMmdFrame + 1;
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

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                _durationSeconds,
                ResolveReferenceMp4CurrentClipStartSeconds());
        }

        private static float ResolveReferenceMp4CurrentClipStartSeconds()
        {
            float referenceClipLengthSeconds = _referenceClip != null ? _referenceClip.length : 0f;
            ReferenceMmdTimingPlan timingPlan = BuildReferenceMmdTimingPlan(
                referenceClipLengthSeconds,
                _durationSeconds,
                _editorDiagnosticSmokeSegment,
                _enableReferenceMmdTimingRuntimeOverride);
            return timingPlan.ReferenceMp4StartSeconds;
        }

        private static float ResolveKnownReferenceMmdDurationSeconds()
        {
            return (SatisfactionReferenceMaxMmdFrame + 1) / DefaultFrameRate;
        }

        private static ReferenceMmdTimingPlan BuildReferenceMmdTimingPlan(
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FileManager.EditorDiagnosticSmokeSegment segment,
            bool enabled)
        {
            float safeClipLength = Mathf.Max(0f, referenceClipLengthSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float defaultStart = CalculateEditorDiagnosticSmokeStartTime(
                safeClipLength,
                safeDuration,
                segment);

            ReferenceMmdTimingPlan plan = new ReferenceMmdTimingPlan
            {
                Enabled = false,
                HasCandidateTimingOverride = false,
                ReferenceMp4StartSeconds = defaultStart,
                CandidateClipStartSeconds = defaultStart,
                CandidateClipSecondsPerReferenceSecond = 1f,
                ReferenceDurationSeconds = safeClipLength
            };

            float knownReferenceDuration = ResolveKnownReferenceMmdDurationSeconds();
            if (!enabled ||
                safeClipLength <= 0f ||
                knownReferenceDuration <= 0f ||
                float.IsNaN(knownReferenceDuration) ||
                float.IsInfinity(knownReferenceDuration))
            {
                return plan;
            }

            float referenceStart = CalculateEditorDiagnosticSmokeStartTime(
                knownReferenceDuration,
                safeDuration,
                segment);
            float candidateScale = Mathf.Max(0.0001f, safeClipLength / knownReferenceDuration);
            float candidateStart = referenceStart * candidateScale;
            float maxCandidateStart = Mathf.Max(0f, safeClipLength - (safeDuration * candidateScale));

            plan.Enabled = true;
            plan.HasCandidateTimingOverride = true;
            plan.ReferenceMp4StartSeconds = referenceStart;
            plan.CandidateClipStartSeconds = Mathf.Clamp(candidateStart, 0f, maxCandidateStart);
            plan.CandidateClipSecondsPerReferenceSecond = candidateScale;
            plan.ReferenceDurationSeconds = knownReferenceDuration;
            return plan;
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds)
        {
            return BuildReferenceMp4AlignedProbeSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                LoadReferenceMp4CurrentClipLocalSampleSeconds(
                    referenceClipStartSeconds,
                    requestedDurationSeconds));
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            float[] referenceLocalSampleSeconds)
        {
            return BuildReferenceMp4AlignedProbeSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                referenceLocalSampleSeconds,
                1f);
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            float[] referenceLocalSampleSeconds,
            float candidateClipSecondsPerReferenceSecond)
        {
            float safeStart = Mathf.Max(0f, referenceClipStartSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float localSampleScale =
                candidateClipSecondsPerReferenceSecond <= 0f ||
                float.IsNaN(candidateClipSecondsPerReferenceSecond) ||
                float.IsInfinity(candidateClipSecondsPerReferenceSecond)
                    ? 1f
                    : candidateClipSecondsPerReferenceSecond;
            var absoluteSampleTimes = new List<float>();
            AddSegmentLocalProbeSamples(
                absoluteSampleTimes,
                safeStart,
                safeDuration,
                ReferenceMp4ProbeDefaultLocalSampleTimes,
                localSampleScale);
            AddSegmentLocalProbeSamples(
                absoluteSampleTimes,
                safeStart,
                safeDuration,
                referenceLocalSampleSeconds,
                localSampleScale);

            if (absoluteSampleTimes.Count <= 0)
            {
                return Array.Empty<float>();
            }

            absoluteSampleTimes.Sort();
            var deduplicated = new List<float>(absoluteSampleTimes.Count);
            float dedupeSeconds = (0.5f / DefaultFrameRate) + 0.0001f;
            foreach (float sampleTime in absoluteSampleTimes)
            {
                if (deduplicated.Count > 0 &&
                    Mathf.Abs(deduplicated[deduplicated.Count - 1] - sampleTime) <= dedupeSeconds)
                {
                    deduplicated[deduplicated.Count - 1] = sampleTime;
                    continue;
                }

                deduplicated.Add(sampleTime);
            }

            return deduplicated.ToArray();
        }

        private static void AddSegmentLocalProbeSamples(
            List<float> absoluteSampleTimes,
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            IEnumerable<float> localSampleSeconds,
            float localSampleScale)
        {
            if (absoluteSampleTimes == null || localSampleSeconds == null)
            {
                return;
            }

            const float epsilonSeconds = 0.0001f;
            foreach (float localSampleSecond in localSampleSeconds)
            {
                if (float.IsNaN(localSampleSecond) ||
                    float.IsInfinity(localSampleSecond) ||
                    localSampleSecond < -epsilonSeconds ||
                    localSampleSecond > requestedDurationSeconds + epsilonSeconds)
                {
                    continue;
                }

                absoluteSampleTimes.Add(referenceClipStartSeconds + (Mathf.Clamp(
                    localSampleSecond,
                    0f,
                    requestedDurationSeconds) * Mathf.Max(0.0001f, localSampleScale)));
            }
        }

        private static float[] LoadReferenceMp4CurrentClipLocalSampleSeconds(
            float referenceClipStartSeconds,
            float requestedDurationSeconds)
        {
            string frameMetricsPath = ResolveProjectRelativePath(ReferenceMp4FrameMetricsRelativePath);
            if (!File.Exists(frameMetricsPath))
            {
                return Array.Empty<float>();
            }

            try
            {
                ReferenceMp4FrameMetrics metrics = JsonUtility.FromJson<ReferenceMp4FrameMetrics>(
                    File.ReadAllText(frameMetricsPath, Encoding.UTF8));
                return ExtractReferenceMp4CurrentClipLocalSampleSeconds(
                    metrics,
                    referenceClipStartSeconds,
                    requestedDurationSeconds);
            }
            catch (Exception ex)
            {
                AppendRunnerTrace(
                    $"reference mp4 sample load failed path={ReferenceMp4FrameMetricsRelativePath} " +
                    $"error={ex.GetType().Name}: {ex.Message}");
                return Array.Empty<float>();
            }
        }

        private static float[] ExtractReferenceMp4CurrentClipLocalSampleSeconds(
            ReferenceMp4FrameMetrics metrics,
            float referenceClipStartSeconds,
            float requestedDurationSeconds)
        {
            if (metrics == null || metrics.rows == null)
            {
                return Array.Empty<float>();
            }

            float safeStart = Mathf.Max(0f, referenceClipStartSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float endSeconds = safeStart + safeDuration;
            const float epsilonSeconds = 0.0001f;
            var localSampleSeconds = new List<float>();
            foreach (ReferenceMp4FrameMetricRow row in metrics.rows)
            {
                if (row == null ||
                    float.IsNaN(row.seconds) ||
                    float.IsInfinity(row.seconds) ||
                    row.seconds < safeStart - epsilonSeconds ||
                    row.seconds > endSeconds + epsilonSeconds)
                {
                    continue;
                }

                localSampleSeconds.Add(Mathf.Clamp(row.seconds - safeStart, 0f, safeDuration));
            }

            localSampleSeconds.Sort();
            return localSampleSeconds.ToArray();
        }

        private static string FormatProbeSampleTimes(float[] sampleTimes)
        {
            if (sampleTimes == null || sampleTimes.Length <= 0)
            {
                return "none";
            }

            return string.Join(
                "/",
                sampleTimes.Select(sampleTime => sampleTime.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                0f);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                ReferenceMp4ProvenanceEvidenceRelativePath,
                ReferenceMp4AnalysisResultRelativePath,
                ReferenceMp4FrameMetricsRelativePath,
                ReferenceMp4ContactSheetRelativePath);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                0f,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath,
                ResolveCandidateFrameIndexPathForDiagnostics());
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath,
            string candidateFrameIndexPath)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                0f,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath,
                candidateFrameIndexPath);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath,
            string candidateFrameIndexPath)
        {
            SummaryFrameRoleDiagnostics diagnostics = new SummaryFrameRoleDiagnostics
            {
                reference_target_frame_count = Mathf.Max(0, referenceTargetFrameCount),
                baseline_recorded_frame_count = Mathf.Max(0, baselineRecordedFrameCount),
                candidate_recorded_frame_count = Mathf.Max(0, candidateRecordedFrameCount),
                baseline_frame_count_delta_from_reference_target = referenceTargetFrameCount > 0
                    ? baselineRecordedFrameCount - referenceTargetFrameCount
                    : 0,
                candidate_frame_count_delta_from_reference_target = referenceTargetFrameCount > 0
                    ? candidateRecordedFrameCount - referenceTargetFrameCount
                    : 0,
                target_frame_count_role = "ref_mmd_mp4 expected frame range for the full satisfaction_2 reference",
                baseline_recorded_frame_count_role = "Sub_Manual recorded comparison baseline; reported separately and not used as target_frame_count",
                candidate_recorded_frame_count_role = "Main_Auto candidate capture under test",
                frame_quality_metric_basis = "Unity pose metrics compare Sub_Manual and Main_Auto rows by recorderFrame; the ref_mmd_mp4 count is only the frame-count target",
                vmd_export_metric_basis = "VMD export spike and floor metrics are evaluated on the Main_Auto candidate VMD"
            };
            AttachReferenceMp4Diagnostics(
                diagnostics,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath);
            AttachCandidateScreenshotFrameDiagnostics(diagnostics, candidateFrameIndexPath);
            return diagnostics;
        }

        private static void AttachReferenceMp4Diagnostics(
            SummaryFrameRoleDiagnostics diagnostics,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.reference_mp4_provenance_evidence_path = referenceMp4ProvenanceEvidencePath ?? string.Empty;
            diagnostics.reference_mp4_analysis_result_path = referenceMp4AnalysisResultPath ?? string.Empty;
            diagnostics.reference_mp4_frame_metrics_path = referenceMp4FrameMetricsPath ?? string.Empty;
            diagnostics.reference_mp4_contact_sheet_path = referenceMp4ContactSheetPath ?? string.Empty;
            diagnostics.reference_mp4_canonical_context =
                "Ref MP4 is a manually postprocessed MMD render from Sub_Manual testPrefab + satisfaction_2; it anchors visual framing/provenance while Unity pose gates compare Sub_Manual metrics to main candidates.";
            diagnostics.reference_mp4_analysis_metric_basis =
                "MP4 analysis supplies visual bbox/framing context; frame-quality gates remain same-recorderFrame Unity metrics and VMD export checks.";
            diagnostics.reference_mp4_current_clip_start_seconds = Mathf.Max(0f, referenceClipStartSeconds);
            diagnostics.reference_mp4_current_clip_duration_seconds = Mathf.Max(0f, requestedDurationSeconds);
            diagnostics.reference_mp4_current_clip_end_seconds =
                diagnostics.reference_mp4_current_clip_start_seconds +
                diagnostics.reference_mp4_current_clip_duration_seconds;
            diagnostics.reference_mp4_current_clip_first_sample_seconds = float.NaN;
            diagnostics.reference_mp4_current_clip_last_sample_seconds = float.NaN;
            diagnostics.reference_mp4_current_clip_sample_gap_seconds =
                diagnostics.reference_mp4_current_clip_duration_seconds;
            diagnostics.reference_mp4_current_clip_sample_basis =
                "Counts reference MP4 frame-metrics rows whose seconds are within the active clip start and requested duration for this visual compare run; stored sample seconds are local to the clip start.";
            diagnostics.reference_mp4_current_clip_framing_metric_basis =
                "Aggregates ref MP4 bbox/framing rows within the active clip start and requested duration, so head/middle/tail candidate screenshot deltas are aligned to the matching reference video window.";
            diagnostics.reference_mp4_current_clip_avg_bbox_height_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_bbox_width_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_center_x_range_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_max_bottom_gap_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_bright_area_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_upper_limb_span_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_lower_limb_span_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_sample_seconds = Array.Empty<float>();

            string resultPath = ResolveProjectRelativePath(diagnostics.reference_mp4_analysis_result_path);
            string frameMetricsPath = ResolveProjectRelativePath(diagnostics.reference_mp4_frame_metrics_path);
            string contactSheetPath = ResolveProjectRelativePath(diagnostics.reference_mp4_contact_sheet_path);
            diagnostics.reference_mp4_provenance_evidence_exists =
                File.Exists(ResolveProjectRelativePath(diagnostics.reference_mp4_provenance_evidence_path));
            diagnostics.reference_mp4_analysis_result_exists = File.Exists(resultPath);
            diagnostics.reference_mp4_frame_metrics_exists = File.Exists(frameMetricsPath);
            diagnostics.reference_mp4_contact_sheet_exists = File.Exists(contactSheetPath);

            if (diagnostics.reference_mp4_analysis_result_exists)
            {
                try
                {
                    ReferenceMp4AnalysisResult analysis = JsonUtility.FromJson<ReferenceMp4AnalysisResult>(
                        File.ReadAllText(resultPath, Encoding.UTF8));
                    if (analysis != null)
                    {
                        diagnostics.reference_mp4_analysis_schema = analysis.schema ?? string.Empty;
                        diagnostics.reference_mp4_extracted_frame_count = Mathf.Max(0, analysis.extractedFrameCount);
                        if (analysis.video != null)
                        {
                            diagnostics.reference_mp4_width = Mathf.Max(0, analysis.video.width);
                            diagnostics.reference_mp4_height = Mathf.Max(0, analysis.video.height);
                            diagnostics.reference_mp4_avg_frame_rate = analysis.video.avg_frame_rate ?? string.Empty;
                            diagnostics.reference_mp4_stream_duration_seconds = ParseInvariantFloat(analysis.video.stream_duration);
                            diagnostics.reference_mp4_total_video_frames = ParseInvariantInt(analysis.video.nb_frames);
                        }
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.reference_mp4_analysis_error = ex.GetType().Name + ": " + ex.Message;
                }
            }

            if (diagnostics.reference_mp4_frame_metrics_exists)
            {
                try
                {
                    ReferenceMp4FrameMetrics metrics = JsonUtility.FromJson<ReferenceMp4FrameMetrics>(
                        File.ReadAllText(frameMetricsPath, Encoding.UTF8));
                    if (metrics != null)
                    {
                        diagnostics.reference_mp4_frame_metrics_schema = metrics.schema ?? string.Empty;
                        diagnostics.reference_mp4_frame_metrics_sample_count = Mathf.Max(0, metrics.sampleCount);
                        diagnostics.reference_mp4_frame_metrics_extracted_frame_count = Mathf.Max(0, metrics.extractedFrameCount);
                        diagnostics.reference_mp4_avg_bbox_height_ratio = metrics.avgBBoxHeightRatio;
                        diagnostics.reference_mp4_avg_bbox_width_ratio = metrics.avgBBoxWidthRatio;
                        diagnostics.reference_mp4_center_x_range_ratio = metrics.centerXRangeRatio;
                        diagnostics.reference_mp4_max_bottom_gap_ratio = metrics.maxBottomGapRatio;
                        diagnostics.reference_mp4_avg_bright_area_ratio = metrics.avgBrightAreaRatio;
                        AttachReferenceMp4CurrentClipCoverage(diagnostics, metrics);
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.reference_mp4_frame_metrics_error = ex.GetType().Name + ": " + ex.Message;
                }
            }
        }

        private static void AttachReferenceMp4CurrentClipCoverage(
            SummaryFrameRoleDiagnostics diagnostics,
            ReferenceMp4FrameMetrics metrics)
        {
            if (diagnostics == null || metrics == null || metrics.rows == null)
            {
                return;
            }

            float startSeconds = Mathf.Max(0f, diagnostics.reference_mp4_current_clip_start_seconds);
            float durationSeconds = Mathf.Max(0f, diagnostics.reference_mp4_current_clip_duration_seconds);
            float endSeconds = startSeconds + durationSeconds;
            diagnostics.reference_mp4_current_clip_end_seconds = endSeconds;
            if (durationSeconds <= 0f)
            {
                diagnostics.reference_mp4_current_clip_sample_gap_seconds = 0f;
                return;
            }

            const float epsilonSeconds = 0.0001f;
            int count = 0;
            float firstSeconds = float.PositiveInfinity;
            float lastSeconds = float.NegativeInfinity;
            float sumBBoxHeight = 0f;
            float sumBBoxWidth = 0f;
            float sumBrightArea = 0f;
            float sumUpperLimbSpan = 0f;
            float sumLowerLimbSpan = 0f;
            int limbSpanSampleCount = 0;
            float maxBottomGap = float.NegativeInfinity;
            float minCenterX = float.PositiveInfinity;
            float maxCenterX = float.NegativeInfinity;
            var sampleSeconds = new List<float>();
            diagnostics.referenceMp4CurrentClipRows.Clear();
            foreach (ReferenceMp4FrameMetricRow row in metrics.rows)
            {
                if (row == null)
                {
                    continue;
                }

                float seconds = row.seconds;
                if (float.IsNaN(seconds) ||
                    seconds < startSeconds - epsilonSeconds ||
                    seconds > endSeconds + epsilonSeconds)
                {
                    continue;
                }

                float localSeconds = Mathf.Clamp(seconds - startSeconds, 0f, durationSeconds);
                count++;
                firstSeconds = Mathf.Min(firstSeconds, localSeconds);
                lastSeconds = Mathf.Max(lastSeconds, localSeconds);
                diagnostics.referenceMp4CurrentClipRows.Add(row);
                sampleSeconds.Add(localSeconds);
                sumBBoxHeight += row.bboxHeightRatio;
                sumBBoxWidth += row.bboxWidthRatio;
                sumBrightArea += row.brightAreaRatio;
                maxBottomGap = Mathf.Max(maxBottomGap, row.bottomGapRatio);
                minCenterX = Mathf.Min(minCenterX, row.centerXRatio);
                maxCenterX = Mathf.Max(maxCenterX, row.centerXRatio);
                string framePath = ResolveProjectRelativePath(row.framePath);
                if (TryAnalyzeCandidateScreenshotFrame(framePath, out CandidateScreenshotFrameMetric imageMetric, out _) &&
                    IsFiniteMetric(imageMetric.UpperLimbSpanRatio) &&
                    IsFiniteMetric(imageMetric.LowerLimbSpanRatio))
                {
                    row.upperLimbSpanRatio = imageMetric.UpperLimbSpanRatio;
                    row.lowerLimbSpanRatio = imageMetric.LowerLimbSpanRatio;
                    row.silhouetteSpanProfile = imageMetric.SilhouetteSpanProfile;
                    row.silhouetteEndpointProfile = imageMetric.SilhouetteEndpointProfile;
                    row.imageSpaceKeypointProfile = imageMetric.ImageSpaceKeypointProfile;
                    row.hasNonHairBrightPixels = imageMetric.HasNonHairBrightPixels;
                    row.nonHairBBoxHeightRatio = imageMetric.NonHairBBoxHeightRatio;
                    row.nonHairBBoxWidthRatio = imageMetric.NonHairBBoxWidthRatio;
                    row.nonHairCenterXRatio = imageMetric.NonHairCenterX;
                    row.nonHairBottomGapRatio = imageMetric.NonHairBottomGapRatio;
                    row.nonHairImageSpaceKeypointProfile = imageMetric.NonHairImageSpaceKeypointProfile;
                    sumUpperLimbSpan += imageMetric.UpperLimbSpanRatio;
                    sumLowerLimbSpan += imageMetric.LowerLimbSpanRatio;
                    limbSpanSampleCount++;
                }
            }

            diagnostics.reference_mp4_current_clip_sample_count = count;
            diagnostics.reference_mp4_current_clip_sample_seconds = sampleSeconds.ToArray();
            if (count <= 0)
            {
                diagnostics.reference_mp4_current_clip_sample_gap_seconds = durationSeconds;
                return;
            }

            diagnostics.reference_mp4_current_clip_first_sample_seconds = firstSeconds;
            diagnostics.reference_mp4_current_clip_last_sample_seconds = lastSeconds;
            diagnostics.reference_mp4_current_clip_sample_coverage_ratio = Mathf.Clamp01(lastSeconds / durationSeconds);
            diagnostics.reference_mp4_current_clip_sample_gap_seconds = Mathf.Max(0f, durationSeconds - lastSeconds);
            diagnostics.reference_mp4_current_clip_avg_bbox_height_ratio = sumBBoxHeight / count;
            diagnostics.reference_mp4_current_clip_avg_bbox_width_ratio = sumBBoxWidth / count;
            diagnostics.reference_mp4_current_clip_center_x_range_ratio = maxCenterX - minCenterX;
            diagnostics.reference_mp4_current_clip_max_bottom_gap_ratio = maxBottomGap;
            diagnostics.reference_mp4_current_clip_avg_bright_area_ratio = sumBrightArea / count;
            if (limbSpanSampleCount > 0)
            {
                diagnostics.reference_mp4_current_clip_avg_upper_limb_span_ratio =
                    sumUpperLimbSpan / limbSpanSampleCount;
                diagnostics.reference_mp4_current_clip_avg_lower_limb_span_ratio =
                    sumLowerLimbSpan / limbSpanSampleCount;
            }
        }

        private static string ResolveCandidateFrameIndexPathForDiagnostics()
        {
            CaptureResult mainAuto = Results.FirstOrDefault(result =>
                result != null &&
                string.Equals(result.jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(result.comparisonFrameIndexPath));
            if (mainAuto != null)
            {
                return mainAuto.comparisonFrameIndexPath;
            }

            CaptureResult fallback = Results.FirstOrDefault(result =>
                result != null &&
                IsMainSceneCandidateMode(result.jobMode) &&
                !string.IsNullOrWhiteSpace(result.comparisonFrameIndexPath));
            return fallback != null
                ? fallback.comparisonFrameIndexPath
                : string.Empty;
        }

        private static void AttachCandidateScreenshotFrameDiagnostics(
            SummaryFrameRoleDiagnostics diagnostics,
            string candidateFrameIndexPath)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.candidate_screenshot_frame_index_path = candidateFrameIndexPath ?? string.Empty;
            diagnostics.candidate_screenshot_frame_metrics_view = CandidateScreenshotFramingView;
            diagnostics.candidate_screenshot_frame_metrics_basis =
                "Computes lightweight bbox/framing metrics from Unity candidate screenshot index front-view PNGs and compares them to the ref MP4 bbox/framing metrics. Bbox width is used as an image-space horizontal limb span proxy.";
            diagnostics.candidate_screenshot_avg_bbox_height_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_bbox_width_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_upper_limb_span_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_lower_limb_span_ratio = float.NaN;
            diagnostics.candidate_screenshot_center_x_range_ratio = float.NaN;
            diagnostics.candidate_screenshot_max_bottom_gap_ratio = float.NaN;
            diagnostics.candidate_screenshot_max_top_gap_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_bright_area_ratio = float.NaN;
            diagnostics.candidate_screenshot_time_sample_count = 0;
            diagnostics.candidate_screenshot_first_sample_seconds = float.NaN;
            diagnostics.candidate_screenshot_last_sample_seconds = float.NaN;
            diagnostics.candidate_screenshot_sample_coverage_ratio = float.NaN;
            diagnostics.candidate_screenshot_sample_gap_seconds =
                Mathf.Max(0f, diagnostics.reference_mp4_current_clip_duration_seconds);
            diagnostics.candidate_screenshot_max_ref_sample_seconds_gap = float.NaN;
            diagnostics.candidate_screenshot_avg_ref_sample_seconds_gap = float.NaN;
            diagnostics.candidate_screenshot_ref_sample_gap_count = 0;
            diagnostics.candidate_screenshot_sample_seconds = Array.Empty<float>();
            diagnostics.candidate_screenshot_sample_timing_basis =
                "Derives candidate screenshot sample seconds from index.csv recorderFrame values and the candidate recorded frame count over requested duration.";
            diagnostics.candidate_vs_reference_avg_bbox_height_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_avg_bbox_width_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_center_x_range_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_max_bottom_gap_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_avg_bright_area_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_avg_bbox_width_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_center_x_range_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_avg_bright_area_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_max_seconds_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_limb_band_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_band_count = 0;
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_band_count = 0;
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label = string.Empty;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_top_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_top_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_silhouette_profile_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count = 0;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label = string.Empty;
            diagnostics.candidate_vs_reference_time_matched_framing_metric_basis =
                "Compares each current-clip ref MP4 sample with the nearest candidate screenshot sample in seconds, then reports absolute bbox/framing deltas.";
            diagnostics.candidate_vs_reference_time_matched_image_space_limb_span_basis =
                "Uses front-view bbox width ratio as an image-space horizontal limb span proxy because tracked 2D keypoints are not yet available in the ref MP4 analysis.";
            diagnostics.candidate_vs_reference_time_matched_image_space_limb_band_basis =
                "Computes upper/lower silhouette band widths from the same ref MP4 and candidate PNG pixels, then compares time-matched samples as a keypoint-free image-space limb span proxy.";
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_basis =
                "Computes a 4-band bottom-to-top silhouette width profile from the same ref MP4 and candidate PNG pixels, then compares time-matched profile L1 deltas as a keypoint-free image-space limb/pose proxy.";
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_basis =
                "Computes 4-band left/right silhouette endpoints from the same ref MP4 and candidate PNG pixels, then compares time-matched endpoint deltas as keypoint-free image-space silhouette landmarks.";
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_basis =
                "Computes deterministic 2D silhouette keypoints from shared bright-pixel PNG analysis: bottom/top bbox centerline endpoints and 4-band left/right endpoints.";
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_basis =
                "Computes bbox-normalized deterministic 2D silhouette keypoints after normalizing each sample into its own bbox coordinate space; bottom/top centers use each silhouette bbox centerline so sparse hair/skirt edge pixels do not dominate horizontal max residual attribution.";
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis =
                "Computes a parallel bbox-normalized deterministic keypoint aggregate after excluding cyan/teal hair-like and dark teal hair-shadow silhouette pixels, so YYB twintail/hair motion cannot be mistaken for arm/leg endpoint residual.";
            diagnostics.candidate_vs_reference_time_matched_crop_safe_basis =
                "Aggregates only time-matched samples where neither the reference MP4 frame nor the candidate screenshot touches the frame edge; edge-touch samples are reported by the full metrics but excluded from crop-safe pose/shape aggregates.";
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_basis =
                "Aggregates bbox-normalized deterministic keypoints after excluding only keypoints directly affected by bottom/top frame-edge contact; this keypoint-local crop-safe view can retain middle-band pose/shape residuals from samples whose cap endpoints are cropped.";

            string resolvedIndexPath = ResolveProjectRelativePath(diagnostics.candidate_screenshot_frame_index_path);
            diagnostics.candidate_screenshot_frame_index_exists = File.Exists(resolvedIndexPath);
            if (!diagnostics.candidate_screenshot_frame_index_exists)
            {
                return;
            }

            try
            {
                CandidateScreenshotFrameMetrics metrics = BuildCandidateScreenshotFrameMetrics(resolvedIndexPath);
                diagnostics.candidate_screenshot_frame_metrics_sample_count = metrics.SampleCount;
                diagnostics.candidate_screenshot_nonblank_frame_count = metrics.NonblankCount;
                if (metrics.SampleCount > 0)
                {
                    diagnostics.candidate_screenshot_avg_bbox_height_ratio = metrics.AvgBBoxHeightRatio;
                    diagnostics.candidate_screenshot_avg_bbox_width_ratio = metrics.AvgBBoxWidthRatio;
                    diagnostics.candidate_screenshot_avg_upper_limb_span_ratio = metrics.AvgUpperLimbSpanRatio;
                    diagnostics.candidate_screenshot_avg_lower_limb_span_ratio = metrics.AvgLowerLimbSpanRatio;
                    diagnostics.candidate_screenshot_center_x_range_ratio = metrics.CenterXRangeRatio;
                    diagnostics.candidate_screenshot_max_bottom_gap_ratio = metrics.MaxBottomGapRatio;
                    diagnostics.candidate_screenshot_max_top_gap_ratio = metrics.MaxTopGapRatio;
                    diagnostics.candidate_screenshot_avg_bright_area_ratio = metrics.AvgBrightAreaRatio;
                    diagnostics.candidate_vs_reference_avg_bbox_height_ratio_delta =
                        metrics.AvgBBoxHeightRatio - diagnostics.reference_mp4_avg_bbox_height_ratio;
                    diagnostics.candidate_vs_reference_avg_bbox_width_ratio_delta =
                        metrics.AvgBBoxWidthRatio - diagnostics.reference_mp4_avg_bbox_width_ratio;
                    diagnostics.candidate_vs_reference_center_x_range_ratio_delta =
                        float.IsNaN(metrics.CenterXRangeRatio)
                            ? float.NaN
                            : metrics.CenterXRangeRatio - diagnostics.reference_mp4_center_x_range_ratio;
                    diagnostics.candidate_vs_reference_max_bottom_gap_ratio_delta =
                        metrics.MaxBottomGapRatio - diagnostics.reference_mp4_max_bottom_gap_ratio;
                    diagnostics.candidate_vs_reference_avg_bright_area_ratio_delta =
                        metrics.AvgBrightAreaRatio - diagnostics.reference_mp4_avg_bright_area_ratio;
                    diagnostics.candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta =
                        metrics.AvgBBoxHeightRatio - diagnostics.reference_mp4_current_clip_avg_bbox_height_ratio;
                    diagnostics.candidate_vs_reference_current_clip_avg_bbox_width_ratio_delta =
                        metrics.AvgBBoxWidthRatio - diagnostics.reference_mp4_current_clip_avg_bbox_width_ratio;
                    diagnostics.candidate_vs_reference_current_clip_center_x_range_ratio_delta =
                        float.IsNaN(metrics.CenterXRangeRatio)
                            ? float.NaN
                            : metrics.CenterXRangeRatio - diagnostics.reference_mp4_current_clip_center_x_range_ratio;
                    diagnostics.candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta =
                        metrics.MaxBottomGapRatio - diagnostics.reference_mp4_current_clip_max_bottom_gap_ratio;
                    diagnostics.candidate_vs_reference_current_clip_avg_bright_area_ratio_delta =
                        metrics.AvgBrightAreaRatio - diagnostics.reference_mp4_current_clip_avg_bright_area_ratio;
                }

                AttachCandidateScreenshotTimingDiagnostics(diagnostics, metrics);

                if (!string.IsNullOrWhiteSpace(metrics.Error))
                {
                    diagnostics.candidate_screenshot_frame_metrics_error = metrics.Error;
                }
            }
            catch (Exception ex)
            {
                diagnostics.candidate_screenshot_frame_metrics_error = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void AttachCandidateScreenshotTimingDiagnostics(
            SummaryFrameRoleDiagnostics diagnostics,
            CandidateScreenshotFrameMetrics metrics)
        {
            if (diagnostics == null || metrics == null || metrics.Samples.Count <= 0)
            {
                return;
            }

            float durationSeconds = Mathf.Max(0f, diagnostics.reference_mp4_current_clip_duration_seconds);
            if (durationSeconds <= 0f || diagnostics.candidate_recorded_frame_count <= 0)
            {
                return;
            }

            float framesPerSecond = diagnostics.candidate_recorded_frame_count / durationSeconds;
            if (framesPerSecond <= 0f || float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond))
            {
                return;
            }

            var seconds = new List<float>();
            var timedSamples = new List<CandidateScreenshotFrameSample>();
            foreach (CandidateScreenshotFrameSample sample in metrics.Samples)
            {
                if (sample == null || sample.RecorderFrame < 0)
                {
                    continue;
                }

                sample.Seconds = Mathf.Clamp(sample.RecorderFrame / framesPerSecond, 0f, durationSeconds);
                seconds.Add(sample.Seconds);
                timedSamples.Add(sample);
            }

            if (seconds.Count <= 0)
            {
                return;
            }

            seconds.Sort();
            diagnostics.candidate_screenshot_time_sample_count = seconds.Count;
            diagnostics.candidate_screenshot_sample_seconds = seconds.ToArray();
            diagnostics.candidate_screenshot_first_sample_seconds = seconds[0];
            diagnostics.candidate_screenshot_last_sample_seconds = seconds[seconds.Count - 1];
            diagnostics.candidate_screenshot_sample_coverage_ratio =
                Mathf.Clamp01(diagnostics.candidate_screenshot_last_sample_seconds / durationSeconds);
            diagnostics.candidate_screenshot_sample_gap_seconds =
                Mathf.Max(0f, durationSeconds - diagnostics.candidate_screenshot_last_sample_seconds);

            float[] referenceSeconds = diagnostics.reference_mp4_current_clip_sample_seconds;
            if (referenceSeconds == null || referenceSeconds.Length <= 0)
            {
                return;
            }

            int gapCount = 0;
            float gapSum = 0f;
            float maxGap = 0f;
            foreach (float referenceSecond in referenceSeconds)
            {
                if (float.IsNaN(referenceSecond))
                {
                    continue;
                }

                float nearestGap = float.PositiveInfinity;
                foreach (float candidateSecond in seconds)
                {
                    nearestGap = Mathf.Min(nearestGap, Mathf.Abs(candidateSecond - referenceSecond));
                }

                if (float.IsInfinity(nearestGap))
                {
                    continue;
                }

                gapCount++;
                gapSum += nearestGap;
                maxGap = Mathf.Max(maxGap, nearestGap);
            }

            diagnostics.candidate_screenshot_ref_sample_gap_count = gapCount;
            if (gapCount > 0)
            {
                diagnostics.candidate_screenshot_max_ref_sample_seconds_gap = maxGap;
                diagnostics.candidate_screenshot_avg_ref_sample_seconds_gap = gapSum / gapCount;
            }

            AttachCandidateScreenshotTimeMatchedFramingDiagnostics(diagnostics, timedSamples);
        }

        private static void AttachCandidateScreenshotTimeMatchedFramingDiagnostics(
            SummaryFrameRoleDiagnostics diagnostics,
            List<CandidateScreenshotFrameSample> candidateSamples)
        {
            if (diagnostics == null ||
                candidateSamples == null ||
                candidateSamples.Count <= 0 ||
                diagnostics.referenceMp4CurrentClipRows.Count <= 0)
            {
                return;
            }

            int count = 0;
            float maxSecondsGap = 0f;
            float sumBBoxHeightDelta = 0f;
            float maxBBoxHeightDelta = 0f;
            float sumBBoxWidthDelta = 0f;
            float maxBBoxWidthDelta = 0f;
            float sumCenterXDelta = 0f;
            float maxBottomGapDelta = 0f;
            float sumBrightAreaDelta = 0f;
            int limbBandCount = 0;
            float sumUpperLimbSpanDelta = 0f;
            float maxUpperLimbSpanDelta = 0f;
            float sumLowerLimbSpanDelta = 0f;
            float maxLowerLimbSpanDelta = 0f;
            int silhouetteProfileBandCount = 0;
            int silhouetteProfileCount = 0;
            float sumSilhouetteProfileL1Delta = 0f;
            float maxSilhouetteProfileL1Delta = 0f;
            float maxSilhouetteProfileBandDelta = 0f;
            int silhouetteLandmarkBandCount = 0;
            int silhouetteLandmarkCount = 0;
            float sumSilhouetteLandmarkEndpointDelta = 0f;
            float maxSilhouetteLandmarkEndpointDelta = 0f;
            int imageSpaceKeypointCount = 0;
            int imageSpaceKeypointSampleCount = 0;
            float sumImageSpaceKeypointL1Delta = 0f;
            float maxImageSpaceKeypointL1Delta = 0f;
            int bboxNormalizedImageSpaceKeypointCount = 0;
            int bboxNormalizedImageSpaceKeypointSampleCount = 0;
            float sumBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            int nonHairBBoxNormalizedImageSpaceKeypointCount = 0;
            int nonHairBBoxNormalizedImageSpaceKeypointSampleCount = 0;
            float sumNonHairBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            string maxNonHairBBoxNormalizedImageSpaceKeypointLabel = string.Empty;
            int cropSafeSampleCount = 0;
            float sumCropSafeBBoxWidthDelta = 0f;
            float maxCropSafeBBoxWidthDelta = 0f;
            int cropSafeSilhouetteProfileCount = 0;
            float sumCropSafeSilhouetteProfileL1Delta = 0f;
            float maxCropSafeSilhouetteProfileL1Delta = 0f;
            int cropSafeImageSpaceKeypointSampleCount = 0;
            float sumCropSafeImageSpaceKeypointL1Delta = 0f;
            float maxCropSafeImageSpaceKeypointL1Delta = 0f;
            int cropSafeBBoxNormalizedImageSpaceKeypointSampleCount = 0;
            float sumCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            int keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount = 0;
            int keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount = 0;
            int keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount = 0;
            float sumKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            string maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel = string.Empty;
            float referenceClipStartSeconds = Mathf.Max(
                0f,
                diagnostics.reference_mp4_current_clip_start_seconds);
            float referenceClipDurationSeconds = Mathf.Max(
                0f,
                diagnostics.reference_mp4_current_clip_duration_seconds);
            foreach (ReferenceMp4FrameMetricRow referenceRow in diagnostics.referenceMp4CurrentClipRows)
            {
                if (referenceRow == null || float.IsNaN(referenceRow.seconds))
                {
                    continue;
                }

                float referenceLocalSeconds = Mathf.Clamp(
                    referenceRow.seconds - referenceClipStartSeconds,
                    0f,
                    referenceClipDurationSeconds);
                CandidateScreenshotFrameSample nearestSample = null;
                float nearestGap = float.PositiveInfinity;
                foreach (CandidateScreenshotFrameSample candidateSample in candidateSamples)
                {
                    if (candidateSample == null ||
                        candidateSample.Metric == null ||
                        !candidateSample.Metric.HasBrightPixels ||
                        float.IsNaN(candidateSample.Seconds))
                    {
                        continue;
                    }

                    float gap = Mathf.Abs(candidateSample.Seconds - referenceLocalSeconds);
                    if (gap < nearestGap)
                    {
                        nearestGap = gap;
                        nearestSample = candidateSample;
                    }
                }

                if (nearestSample == null || float.IsInfinity(nearestGap))
                {
                    continue;
                }

                CandidateScreenshotFrameMetric candidateMetric = nearestSample.Metric;
                float bboxHeightDelta = Mathf.Abs(candidateMetric.BBoxHeightRatio - referenceRow.bboxHeightRatio);
                float bboxWidthDelta = Mathf.Abs(candidateMetric.BBoxWidthRatio - referenceRow.bboxWidthRatio);
                float centerXDelta = Mathf.Abs(candidateMetric.CenterX - referenceRow.centerXRatio);
                float bottomGapDelta = Mathf.Abs(candidateMetric.BottomGapRatio - referenceRow.bottomGapRatio);
                float brightAreaDelta = Mathf.Abs(candidateMetric.BrightAreaRatio - referenceRow.brightAreaRatio);
                float referenceTopGapRatio = ResolveFrameTopGapRatio(
                    referenceRow.bottomGapRatio,
                    referenceRow.bboxHeightRatio);
                bool referenceTouchesFrameEdge = IsFrameEdgeTouched(referenceRow.bottomGapRatio, referenceTopGapRatio);
                bool candidateTouchesFrameEdge =
                    IsFrameEdgeTouched(candidateMetric.BottomGapRatio, candidateMetric.TopGapRatio);
                bool cropSafeSample = !referenceTouchesFrameEdge && !candidateTouchesFrameEdge;
                if (cropSafeSample)
                {
                    cropSafeSampleCount++;
                    sumCropSafeBBoxWidthDelta += bboxWidthDelta;
                    maxCropSafeBBoxWidthDelta = Mathf.Max(maxCropSafeBBoxWidthDelta, bboxWidthDelta);
                }

                if (IsFiniteMetric(candidateMetric.UpperLimbSpanRatio) &&
                    IsFiniteMetric(candidateMetric.LowerLimbSpanRatio) &&
                    IsFiniteMetric(referenceRow.upperLimbSpanRatio) &&
                    IsFiniteMetric(referenceRow.lowerLimbSpanRatio))
                {
                    float upperLimbSpanDelta =
                        Mathf.Abs(candidateMetric.UpperLimbSpanRatio - referenceRow.upperLimbSpanRatio);
                    float lowerLimbSpanDelta =
                        Mathf.Abs(candidateMetric.LowerLimbSpanRatio - referenceRow.lowerLimbSpanRatio);
                    limbBandCount++;
                    sumUpperLimbSpanDelta += upperLimbSpanDelta;
                    maxUpperLimbSpanDelta = Mathf.Max(maxUpperLimbSpanDelta, upperLimbSpanDelta);
                    sumLowerLimbSpanDelta += lowerLimbSpanDelta;
                    maxLowerLimbSpanDelta = Mathf.Max(maxLowerLimbSpanDelta, lowerLimbSpanDelta);
                }
                if (TryComputeSilhouetteProfileDelta(
                    candidateMetric.SilhouetteSpanProfile,
                    referenceRow.silhouetteSpanProfile,
                    out int matchedBandCount,
                    out float silhouetteProfileL1Delta,
                    out float silhouetteProfileBandDelta))
                {
                    silhouetteProfileBandCount = Mathf.Max(silhouetteProfileBandCount, matchedBandCount);
                    silhouetteProfileCount++;
                    sumSilhouetteProfileL1Delta += silhouetteProfileL1Delta;
                    maxSilhouetteProfileL1Delta = Mathf.Max(maxSilhouetteProfileL1Delta, silhouetteProfileL1Delta);
                    maxSilhouetteProfileBandDelta = Mathf.Max(maxSilhouetteProfileBandDelta, silhouetteProfileBandDelta);
                    if (cropSafeSample)
                    {
                        cropSafeSilhouetteProfileCount++;
                        sumCropSafeSilhouetteProfileL1Delta += silhouetteProfileL1Delta;
                        maxCropSafeSilhouetteProfileL1Delta =
                            Mathf.Max(maxCropSafeSilhouetteProfileL1Delta, silhouetteProfileL1Delta);
                    }
                }
                if (TryComputeSilhouetteEndpointDelta(
                    candidateMetric.SilhouetteEndpointProfile,
                    referenceRow.silhouetteEndpointProfile,
                    out int matchedEndpointBandCount,
                    out float silhouetteEndpointDelta,
                    out float silhouetteEndpointMaxDelta))
                {
                    silhouetteLandmarkBandCount = Mathf.Max(silhouetteLandmarkBandCount, matchedEndpointBandCount);
                    silhouetteLandmarkCount++;
                    sumSilhouetteLandmarkEndpointDelta += silhouetteEndpointDelta;
                    maxSilhouetteLandmarkEndpointDelta =
                        Mathf.Max(maxSilhouetteLandmarkEndpointDelta, silhouetteEndpointMaxDelta);
                }
                if (TryComputeImageSpaceKeypointDelta(
                    candidateMetric.ImageSpaceKeypointProfile,
                    referenceRow.imageSpaceKeypointProfile,
                    out int matchedKeypointCount,
                    out float keypointL1Delta,
                    out float keypointMaxL1Delta))
                {
                    imageSpaceKeypointCount = Mathf.Max(imageSpaceKeypointCount, matchedKeypointCount);
                    imageSpaceKeypointSampleCount++;
                    sumImageSpaceKeypointL1Delta += keypointL1Delta;
                    maxImageSpaceKeypointL1Delta =
                        Mathf.Max(maxImageSpaceKeypointL1Delta, keypointMaxL1Delta);
                    if (cropSafeSample)
                    {
                        cropSafeImageSpaceKeypointSampleCount++;
                        sumCropSafeImageSpaceKeypointL1Delta += keypointL1Delta;
                        maxCropSafeImageSpaceKeypointL1Delta =
                            Mathf.Max(maxCropSafeImageSpaceKeypointL1Delta, keypointMaxL1Delta);
                    }
                }
                if (TryComputeBBoxNormalizedImageSpaceKeypointDelta(
                    candidateMetric.ImageSpaceKeypointProfile,
                    candidateMetric.CenterX,
                    candidateMetric.BBoxWidthRatio,
                    candidateMetric.BottomGapRatio,
                    candidateMetric.BBoxHeightRatio,
                    referenceRow.imageSpaceKeypointProfile,
                    referenceRow.centerXRatio,
                    referenceRow.bboxWidthRatio,
                    referenceRow.bottomGapRatio,
                    referenceRow.bboxHeightRatio,
                    out int matchedBBoxNormalizedKeypointCount,
                    out float bboxNormalizedKeypointL1Delta,
                    out float bboxNormalizedKeypointMaxL1Delta,
                    out int bboxNormalizedKeypointMaxIndex,
                    out float bboxNormalizedKeypointMaxXDelta,
                    out float bboxNormalizedKeypointMaxYDelta,
                    out float bboxNormalizedKeypointMaxCandidateX,
                    out float bboxNormalizedKeypointMaxCandidateY,
                    out float bboxNormalizedKeypointMaxReferenceX,
                    out float bboxNormalizedKeypointMaxReferenceY))
                {
                    bboxNormalizedImageSpaceKeypointCount =
                        Mathf.Max(bboxNormalizedImageSpaceKeypointCount, matchedBBoxNormalizedKeypointCount);
                    bboxNormalizedImageSpaceKeypointSampleCount++;
                    sumBBoxNormalizedImageSpaceKeypointL1Delta += bboxNormalizedKeypointL1Delta;
                    if (cropSafeSample)
                    {
                        cropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        sumCropSafeBBoxNormalizedImageSpaceKeypointL1Delta += bboxNormalizedKeypointL1Delta;
                        maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                            Mathf.Max(maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta, bboxNormalizedKeypointMaxL1Delta);
                    }
                    if (TryComputeKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointDelta(
                        candidateMetric.ImageSpaceKeypointProfile,
                        candidateMetric.CenterX,
                        candidateMetric.BBoxWidthRatio,
                        candidateMetric.BottomGapRatio,
                        candidateMetric.BBoxHeightRatio,
                        candidateMetric.TopGapRatio,
                        referenceRow.imageSpaceKeypointProfile,
                        referenceRow.centerXRatio,
                        referenceRow.bboxWidthRatio,
                        referenceRow.bottomGapRatio,
                        referenceRow.bboxHeightRatio,
                        referenceTopGapRatio,
                        out int matchedKeypointLocalCropSafeKeypointCount,
                        out int excludedKeypointLocalCropSafeKeypointCount,
                        out float keypointLocalCropSafeKeypointL1Delta,
                        out float keypointLocalCropSafeKeypointMaxL1Delta,
                        out int keypointLocalCropSafeKeypointMaxIndex))
                    {
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount =
                            Mathf.Max(
                                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount,
                                matchedKeypointLocalCropSafeKeypointCount);
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount +=
                            excludedKeypointLocalCropSafeKeypointCount;
                        sumKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta +=
                            keypointLocalCropSafeKeypointL1Delta;
                        if (keypointLocalCropSafeKeypointMaxL1Delta >
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta)
                        {
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                                keypointLocalCropSafeKeypointMaxL1Delta;
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel =
                                ResolveImageSpaceKeypointLabel(keypointLocalCropSafeKeypointMaxIndex);
                        }
                    }

                    if (bboxNormalizedKeypointMaxL1Delta > maxBBoxNormalizedImageSpaceKeypointL1Delta)
                    {
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_index =
                            bboxNormalizedKeypointMaxIndex;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label =
                            ResolveImageSpaceKeypointLabel(bboxNormalizedKeypointMaxIndex);
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds =
                            referenceRow.seconds;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds =
                            nearestSample.Seconds;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame =
                            nearestSample.RecorderFrame;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta =
                            bboxNormalizedKeypointMaxXDelta;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta =
                            bboxNormalizedKeypointMaxYDelta;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_x =
                            bboxNormalizedKeypointMaxCandidateX;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_y =
                            bboxNormalizedKeypointMaxCandidateY;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_x =
                            bboxNormalizedKeypointMaxReferenceX;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_y =
                            bboxNormalizedKeypointMaxReferenceY;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap =
                            referenceRow.bottomGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_top_gap =
                            referenceTopGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap =
                            candidateMetric.BottomGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_top_gap =
                            candidateMetric.TopGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_touches_frame_edge =
                            referenceTouchesFrameEdge;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_touches_frame_edge =
                            candidateTouchesFrameEdge;
                    }
                    maxBBoxNormalizedImageSpaceKeypointL1Delta =
                        Mathf.Max(maxBBoxNormalizedImageSpaceKeypointL1Delta, bboxNormalizedKeypointMaxL1Delta);
                }

                if (candidateMetric.HasNonHairBrightPixels &&
                    referenceRow.hasNonHairBrightPixels &&
                    TryComputeBBoxNormalizedImageSpaceKeypointDelta(
                        candidateMetric.NonHairImageSpaceKeypointProfile,
                        candidateMetric.NonHairCenterX,
                        candidateMetric.NonHairBBoxWidthRatio,
                        candidateMetric.NonHairBottomGapRatio,
                        candidateMetric.NonHairBBoxHeightRatio,
                        referenceRow.nonHairImageSpaceKeypointProfile,
                        referenceRow.nonHairCenterXRatio,
                        referenceRow.nonHairBBoxWidthRatio,
                        referenceRow.nonHairBottomGapRatio,
                        referenceRow.nonHairBBoxHeightRatio,
                        out int matchedNonHairBBoxNormalizedKeypointCount,
                        out float nonHairBBoxNormalizedKeypointL1Delta,
                        out float nonHairBBoxNormalizedKeypointMaxL1Delta,
                        out int nonHairBBoxNormalizedKeypointMaxIndex,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    nonHairBBoxNormalizedImageSpaceKeypointCount =
                        Mathf.Max(
                            nonHairBBoxNormalizedImageSpaceKeypointCount,
                            matchedNonHairBBoxNormalizedKeypointCount);
                    nonHairBBoxNormalizedImageSpaceKeypointSampleCount++;
                    sumNonHairBBoxNormalizedImageSpaceKeypointL1Delta += nonHairBBoxNormalizedKeypointL1Delta;
                    if (nonHairBBoxNormalizedKeypointMaxL1Delta >
                        maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta)
                    {
                        maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta =
                            nonHairBBoxNormalizedKeypointMaxL1Delta;
                        maxNonHairBBoxNormalizedImageSpaceKeypointLabel =
                            ResolveImageSpaceKeypointLabel(nonHairBBoxNormalizedKeypointMaxIndex);
                    }
                }

                count++;
                maxSecondsGap = Mathf.Max(maxSecondsGap, nearestGap);
                sumBBoxHeightDelta += bboxHeightDelta;
                maxBBoxHeightDelta = Mathf.Max(maxBBoxHeightDelta, bboxHeightDelta);
                sumBBoxWidthDelta += bboxWidthDelta;
                maxBBoxWidthDelta = Mathf.Max(maxBBoxWidthDelta, bboxWidthDelta);
                sumCenterXDelta += centerXDelta;
                maxBottomGapDelta = Mathf.Max(maxBottomGapDelta, bottomGapDelta);
                sumBrightAreaDelta += brightAreaDelta;
            }

            diagnostics.candidate_vs_reference_time_matched_sample_count = count;
            if (count <= 0)
            {
                return;
            }

            diagnostics.candidate_vs_reference_time_matched_max_seconds_gap = maxSecondsGap;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta =
                sumBBoxHeightDelta / count;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta =
                maxBBoxHeightDelta;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta =
                sumBBoxWidthDelta / count;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta =
                maxBBoxWidthDelta;
            diagnostics.candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta =
                sumCenterXDelta / count;
            diagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta =
                maxBottomGapDelta;
            diagnostics.candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta =
                sumBrightAreaDelta / count;
            diagnostics.candidate_vs_reference_time_matched_limb_band_sample_count = limbBandCount;
            if (limbBandCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta =
                    sumUpperLimbSpanDelta / limbBandCount;
                diagnostics.candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta =
                    maxUpperLimbSpanDelta;
                diagnostics.candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta =
                    sumLowerLimbSpanDelta / limbBandCount;
                diagnostics.candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta =
                    maxLowerLimbSpanDelta;
            }
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_band_count =
                silhouetteProfileBandCount;
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_sample_count =
                silhouetteProfileCount;
            if (silhouetteProfileCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta =
                    sumSilhouetteProfileL1Delta / silhouetteProfileCount;
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta =
                    maxSilhouetteProfileL1Delta;
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta =
                    maxSilhouetteProfileBandDelta;
            }
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_band_count =
                silhouetteLandmarkBandCount;
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_sample_count =
                silhouetteLandmarkCount;
            if (silhouetteLandmarkCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta =
                    sumSilhouetteLandmarkEndpointDelta / silhouetteLandmarkCount;
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta =
                    maxSilhouetteLandmarkEndpointDelta;
            }
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_count =
                imageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_sample_count =
                imageSpaceKeypointSampleCount;
            if (imageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta =
                    sumImageSpaceKeypointL1Delta / imageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta =
                    maxImageSpaceKeypointL1Delta;
            }
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count =
                bboxNormalizedImageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count =
                bboxNormalizedImageSpaceKeypointSampleCount;
            if (bboxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumBBoxNormalizedImageSpaceKeypointL1Delta / bboxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxBBoxNormalizedImageSpaceKeypointL1Delta;
                if (imageSpaceKeypointSampleCount > 0)
                {
                    diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization =
                        diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta -
                        diagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta;
                    diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization =
                        diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta -
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta;
                }
            }

            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count =
                nonHairBBoxNormalizedImageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count =
                nonHairBBoxNormalizedImageSpaceKeypointSampleCount;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label =
                maxNonHairBBoxNormalizedImageSpaceKeypointLabel ?? string.Empty;
            if (nonHairBBoxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumNonHairBBoxNormalizedImageSpaceKeypointL1Delta /
                    nonHairBBoxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_sample_count = cropSafeSampleCount;
            if (cropSafeSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta =
                    sumCropSafeBBoxWidthDelta / cropSafeSampleCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_width_ratio_abs_delta =
                    maxCropSafeBBoxWidthDelta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_silhouette_profile_sample_count =
                cropSafeSilhouetteProfileCount;
            if (cropSafeSilhouetteProfileCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_silhouette_profile_l1_abs_delta =
                    sumCropSafeSilhouetteProfileL1Delta / cropSafeSilhouetteProfileCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_silhouette_profile_l1_abs_delta =
                    maxCropSafeSilhouetteProfileL1Delta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_image_space_keypoint_sample_count =
                cropSafeImageSpaceKeypointSampleCount;
            if (cropSafeImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta =
                    sumCropSafeImageSpaceKeypointL1Delta / cropSafeImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_image_space_keypoint_l1_delta =
                    maxCropSafeImageSpaceKeypointL1Delta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count =
                cropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
            if (cropSafeBBoxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumCropSafeBBoxNormalizedImageSpaceKeypointL1Delta /
                    cropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta;
            }
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count =
                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count =
                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count =
                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label =
                maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel ?? string.Empty;
            if (keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta /
                    keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta;
            }
        }

        private static CandidateScreenshotFrameMetrics BuildCandidateScreenshotFrameMetrics(string frameIndexPath)
        {
            var metrics = new CandidateScreenshotFrameMetrics();
            string[] lines = File.ReadAllLines(frameIndexPath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return metrics;
            }

            string[] headers = SplitSimpleCsvLine(lines[0]);
            int viewIndex = IndexOfHeader(headers, "view");
            int pathIndex = IndexOfHeader(headers, "path");
            int recorderFrameIndex = IndexOfHeader(headers, "recorderFrame");
            if (pathIndex < 0)
            {
                return metrics;
            }

            float sumHeight = 0f;
            float sumWidth = 0f;
            float sumUpperLimbSpan = 0f;
            float sumLowerLimbSpan = 0f;
            int limbSpanCount = 0;
            float sumBrightArea = 0f;
            float maxBottomGap = 0f;
            float maxTopGap = 0f;
            float minCenterX = float.PositiveInfinity;
            float maxCenterX = float.NegativeInfinity;
            var errors = new List<string>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] cells = SplitSimpleCsvLine(lines[i]);
                if (pathIndex >= cells.Length)
                {
                    continue;
                }

                if (viewIndex >= 0 &&
                    viewIndex < cells.Length &&
                    !string.Equals(cells[viewIndex], CandidateScreenshotFramingView, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string screenshotPath = ResolveProjectRelativePath(cells[pathIndex]);
                if (!TryAnalyzeCandidateScreenshotFrame(screenshotPath, out CandidateScreenshotFrameMetric frameMetric, out string error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        errors.Add(error);
                    }

                    continue;
                }

                metrics.SampleCount++;
                int parsedRecorderFrame = -1;
                if (recorderFrameIndex >= 0 &&
                    recorderFrameIndex < cells.Length &&
                    int.TryParse(cells[recorderFrameIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int recorderFrame))
                {
                    parsedRecorderFrame = recorderFrame;
                    metrics.RecorderFrames.Add(parsedRecorderFrame);
                }
                metrics.Samples.Add(new CandidateScreenshotFrameSample(parsedRecorderFrame, frameMetric));

                sumHeight += frameMetric.BBoxHeightRatio;
                sumWidth += frameMetric.BBoxWidthRatio;
                if (IsFiniteMetric(frameMetric.UpperLimbSpanRatio) &&
                    IsFiniteMetric(frameMetric.LowerLimbSpanRatio))
                {
                    sumUpperLimbSpan += frameMetric.UpperLimbSpanRatio;
                    sumLowerLimbSpan += frameMetric.LowerLimbSpanRatio;
                    limbSpanCount++;
                }
                sumBrightArea += frameMetric.BrightAreaRatio;
                maxBottomGap = Mathf.Max(maxBottomGap, frameMetric.BottomGapRatio);
                maxTopGap = Mathf.Max(maxTopGap, frameMetric.TopGapRatio);
                if (frameMetric.HasBrightPixels)
                {
                    metrics.NonblankCount++;
                    minCenterX = Mathf.Min(minCenterX, frameMetric.CenterX);
                    maxCenterX = Mathf.Max(maxCenterX, frameMetric.CenterX);
                }
            }

            if (metrics.SampleCount <= 0)
            {
                metrics.Error = string.Join("; ", errors);
                return metrics;
            }

            metrics.AvgBBoxHeightRatio = sumHeight / metrics.SampleCount;
            metrics.AvgBBoxWidthRatio = sumWidth / metrics.SampleCount;
            if (limbSpanCount > 0)
            {
                metrics.AvgUpperLimbSpanRatio = sumUpperLimbSpan / limbSpanCount;
                metrics.AvgLowerLimbSpanRatio = sumLowerLimbSpan / limbSpanCount;
            }
            metrics.AvgBrightAreaRatio = sumBrightArea / metrics.SampleCount;
            metrics.MaxBottomGapRatio = maxBottomGap;
            metrics.MaxTopGapRatio = maxTopGap;
            metrics.CenterXRangeRatio = metrics.NonblankCount > 0
                ? maxCenterX - minCenterX
                : float.NaN;
            metrics.Error = string.Join("; ", errors);
            return metrics;
        }

        private static bool TryAnalyzeCandidateScreenshotFrame(
            string screenshotPath,
            out CandidateScreenshotFrameMetric metric,
            out string error)
        {
            metric = new CandidateScreenshotFrameMetric
            {
                CenterX = float.NaN,
                BottomGapRatio = 1f,
                TopGapRatio = 1f
            };
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(screenshotPath) || !File.Exists(screenshotPath))
            {
                error = "missing screenshot: " + (screenshotPath ?? string.Empty);
                return false;
            }

            byte[] bytes = File.ReadAllBytes(screenshotPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    error = "unreadable screenshot: " + screenshotPath;
                    return false;
                }

                int width = texture.width;
                int height = texture.height;
                if (width <= 0 || height <= 0)
                {
                    error = "empty screenshot dimensions: " + screenshotPath;
                    return false;
                }

                Color32[] pixels = texture.GetPixels32();
                int minX = width;
                int minY = height;
                int maxX = -1;
                int maxY = -1;
                int brightPixelCount = 0;
                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[rowOffset + x];
                        if (!IsCandidateBrightPixel(pixel))
                        {
                            continue;
                        }

                        brightPixelCount++;
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                int totalPixels = Mathf.Max(1, width * height);
                metric.BrightAreaRatio = brightPixelCount / (float)totalPixels;
                metric.HasBrightPixels = brightPixelCount > 0;
                if (!metric.HasBrightPixels)
                {
                    return true;
                }

                metric.BBoxHeightRatio = (maxY - minY + 1) / (float)height;
                metric.BBoxWidthRatio = (maxX - minX + 1) / (float)width;
                metric.CenterX = ((minX + maxX + 1) * 0.5f) / width;
                metric.BottomGapRatio = minY / (float)height;
                metric.TopGapRatio = (height - maxY - 1) / (float)height;
                FillBandedImageSpaceLimbSpanMetrics(pixels, width, height, minY, maxY, metric);
                metric.SilhouetteSpanProfile = BuildSilhouetteSpanProfile(
                    pixels,
                    width,
                    minY,
                    maxY,
                    ImageSpaceSilhouetteProfileBandCount);
                metric.SilhouetteEndpointProfile = BuildSilhouetteEndpointProfile(
                    pixels,
                    width,
                    minY,
                    maxY,
                    ImageSpaceSilhouetteProfileBandCount);
                metric.ImageSpaceKeypointProfile = BuildImageSpaceSilhouetteKeypointProfile(
                    pixels,
                    width,
                    height,
                    minY,
                    maxY,
                    ImageSpaceSilhouetteProfileBandCount);
                if (TryAnalyzeImageSpaceSilhouette(
                    pixels,
                    width,
                    height,
                    IsCandidateNonHairBrightPixel,
                    out float nonHairBBoxHeightRatio,
                    out float nonHairBBoxWidthRatio,
                    out float nonHairCenterX,
                    out float nonHairBottomGapRatio,
                    out float nonHairTopGapRatio,
                    out float[] nonHairImageSpaceKeypointProfile))
                {
                    metric.HasNonHairBrightPixels = true;
                    metric.NonHairBBoxHeightRatio = nonHairBBoxHeightRatio;
                    metric.NonHairBBoxWidthRatio = nonHairBBoxWidthRatio;
                    metric.NonHairCenterX = nonHairCenterX;
                    metric.NonHairBottomGapRatio = nonHairBottomGapRatio;
                    metric.NonHairTopGapRatio = nonHairTopGapRatio;
                    metric.NonHairImageSpaceKeypointProfile = nonHairImageSpaceKeypointProfile;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool TryAnalyzeImageSpaceSilhouette(
            Color32[] pixels,
            int width,
            int height,
            Func<Color32, bool> pixelPredicate,
            out float bboxHeightRatio,
            out float bboxWidthRatio,
            out float centerX,
            out float bottomGapRatio,
            out float topGapRatio,
            out float[] imageSpaceKeypointProfile)
        {
            bboxHeightRatio = float.NaN;
            bboxWidthRatio = float.NaN;
            centerX = float.NaN;
            bottomGapRatio = float.NaN;
            topGapRatio = float.NaN;
            imageSpaceKeypointProfile = Array.Empty<float>();
            if (pixels == null || width <= 0 || height <= 0 || pixelPredicate == null)
            {
                return false;
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            bboxHeightRatio = (maxY - minY + 1) / (float)height;
            bboxWidthRatio = (maxX - minX + 1) / (float)width;
            centerX = ((minX + maxX + 1) * 0.5f) / width;
            bottomGapRatio = minY / (float)height;
            topGapRatio = (height - maxY - 1) / (float)height;
            imageSpaceKeypointProfile = BuildImageSpaceSilhouetteKeypointProfile(
                pixels,
                width,
                height,
                minY,
                maxY,
                ImageSpaceSilhouetteProfileBandCount,
                pixelPredicate);
            return true;
        }

        private static void FillBandedImageSpaceLimbSpanMetrics(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            CandidateScreenshotFrameMetric metric)
        {
            if (pixels == null || metric == null || width <= 0 || height <= 0 || maxY < minY)
            {
                return;
            }

            int bboxHeight = maxY - minY + 1;
            int upperStartY = minY + Mathf.CeilToInt(bboxHeight * 0.5f);
            int lowerMinX = width;
            int lowerMaxX = -1;
            int upperMinX = width;
            int upperMaxX = -1;
            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * width;
                bool upperBand = y >= upperStartY;
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = pixels[rowOffset + x];
                    if (!IsCandidateBrightPixel(pixel))
                    {
                        continue;
                    }

                    if (upperBand)
                    {
                        upperMinX = Mathf.Min(upperMinX, x);
                        upperMaxX = Mathf.Max(upperMaxX, x);
                    }
                    else
                    {
                        lowerMinX = Mathf.Min(lowerMinX, x);
                        lowerMaxX = Mathf.Max(lowerMaxX, x);
                    }
                }
            }

            if (upperMaxX >= upperMinX)
            {
                metric.UpperLimbSpanRatio = (upperMaxX - upperMinX + 1) / (float)width;
            }

            if (lowerMaxX >= lowerMinX)
            {
                metric.LowerLimbSpanRatio = (lowerMaxX - lowerMinX + 1) / (float)width;
            }
        }

        private static float[] BuildSilhouetteSpanProfile(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            int bandCount)
        {
            if (pixels == null || width <= 0 || maxY < minY || bandCount <= 0)
            {
                return Array.Empty<float>();
            }

            int bboxHeight = maxY - minY + 1;
            var minXByBand = new int[bandCount];
            var maxXByBand = new int[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                minXByBand[i] = width;
                maxXByBand[i] = -1;
            }

            for (int y = minY; y <= maxY; y++)
            {
                int bandIndex = Mathf.Clamp(((y - minY) * bandCount) / bboxHeight, 0, bandCount - 1);
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!IsCandidateBrightPixel(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minXByBand[bandIndex] = Mathf.Min(minXByBand[bandIndex], x);
                    maxXByBand[bandIndex] = Mathf.Max(maxXByBand[bandIndex], x);
                }
            }

            var profile = new float[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                profile[i] = maxXByBand[i] >= minXByBand[i]
                    ? (maxXByBand[i] - minXByBand[i] + 1) / (float)width
                    : 0f;
            }

            return profile;
        }

        private static bool TryComputeSilhouetteProfileDelta(
            float[] candidateProfile,
            float[] referenceProfile,
            out int bandCount,
            out float l1Delta,
            out float maxBandDelta)
        {
            bandCount = 0;
            l1Delta = float.NaN;
            maxBandDelta = float.NaN;
            if (candidateProfile == null || referenceProfile == null)
            {
                return false;
            }

            int length = Mathf.Min(candidateProfile.Length, referenceProfile.Length);
            if (length <= 0)
            {
                return false;
            }

            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteCount = 0;
            for (int i = 0; i < length; i++)
            {
                float candidate = candidateProfile[i];
                float reference = referenceProfile[i];
                if (!IsFiniteMetric(candidate) || !IsFiniteMetric(reference))
                {
                    continue;
                }

                float delta = Mathf.Abs(candidate - reference);
                sumDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                finiteCount++;
            }

            if (finiteCount <= 0)
            {
                return false;
            }

            bandCount = finiteCount;
            l1Delta = sumDelta / finiteCount;
            maxBandDelta = maxDelta;
            return true;
        }

        private static float[] BuildSilhouetteEndpointProfile(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            int bandCount)
        {
            if (pixels == null || width <= 0 || maxY < minY || bandCount <= 0)
            {
                return Array.Empty<float>();
            }

            int bboxHeight = maxY - minY + 1;
            var minXByBand = new int[bandCount];
            var maxXByBand = new int[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                minXByBand[i] = width;
                maxXByBand[i] = -1;
            }

            for (int y = minY; y <= maxY; y++)
            {
                int bandIndex = Mathf.Clamp(((y - minY) * bandCount) / bboxHeight, 0, bandCount - 1);
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!IsCandidateBrightPixel(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minXByBand[bandIndex] = Mathf.Min(minXByBand[bandIndex], x);
                    maxXByBand[bandIndex] = Mathf.Max(maxXByBand[bandIndex], x);
                }
            }

            var endpoints = new float[bandCount * 2];
            for (int i = 0; i < bandCount; i++)
            {
                int leftIndex = i * 2;
                int rightIndex = leftIndex + 1;
                if (maxXByBand[i] >= minXByBand[i])
                {
                    endpoints[leftIndex] = minXByBand[i] / (float)width;
                    endpoints[rightIndex] = (maxXByBand[i] + 1) / (float)width;
                }
                else
                {
                    endpoints[leftIndex] = float.NaN;
                    endpoints[rightIndex] = float.NaN;
                }
            }

            return endpoints;
        }

        private static bool TryComputeSilhouetteEndpointDelta(
            float[] candidateEndpoints,
            float[] referenceEndpoints,
            out int bandCount,
            out float endpointDelta,
            out float maxEndpointDelta)
        {
            bandCount = 0;
            endpointDelta = float.NaN;
            maxEndpointDelta = float.NaN;
            if (candidateEndpoints == null || referenceEndpoints == null)
            {
                return false;
            }

            int length = Mathf.Min(candidateEndpoints.Length, referenceEndpoints.Length);
            if (length <= 1)
            {
                return false;
            }

            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteEndpointCount = 0;
            for (int i = 0; i < length; i++)
            {
                float candidate = candidateEndpoints[i];
                float reference = referenceEndpoints[i];
                if (!IsFiniteMetric(candidate) || !IsFiniteMetric(reference))
                {
                    continue;
                }

                float delta = Mathf.Abs(candidate - reference);
                sumDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                finiteEndpointCount++;
            }

            if (finiteEndpointCount <= 0)
            {
                return false;
            }

            bandCount = finiteEndpointCount / 2;
            endpointDelta = sumDelta / finiteEndpointCount;
            maxEndpointDelta = maxDelta;
            return true;
        }

        private static float[] BuildImageSpaceSilhouetteKeypointProfile(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            int bandCount)
        {
            return BuildImageSpaceSilhouetteKeypointProfile(
                pixels,
                width,
                height,
                minY,
                maxY,
                bandCount,
                IsCandidateBrightPixel);
        }

        private static float[] BuildImageSpaceSilhouetteKeypointProfile(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            int bandCount,
            Func<Color32, bool> pixelPredicate)
        {
            if (pixels == null || width <= 0 || height <= 0 || maxY < minY || bandCount <= 0)
            {
                return Array.Empty<float>();
            }

            var keypoints = new List<float>((2 + (bandCount * 2)) * 2);
            AppendBBoxCenterlineEndpointKeypoint(
                pixels,
                width,
                height,
                minY,
                maxY,
                useBottomEndpoint: true,
                keypoints,
                pixelPredicate);
            AppendBBoxCenterlineEndpointKeypoint(
                pixels,
                width,
                height,
                minY,
                maxY,
                useBottomEndpoint: false,
                keypoints,
                pixelPredicate);

            int bboxHeight = maxY - minY + 1;
            var minXByBand = new int[bandCount];
            var maxXByBand = new int[bandCount];
            var minYByBand = new int[bandCount];
            var maxYByBand = new int[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                minXByBand[i] = width;
                maxXByBand[i] = -1;
                minYByBand[i] = height;
                maxYByBand[i] = -1;
            }

            for (int y = minY; y <= maxY; y++)
            {
                int bandIndex = Mathf.Clamp(((y - minY) * bandCount) / bboxHeight, 0, bandCount - 1);
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minXByBand[bandIndex] = Mathf.Min(minXByBand[bandIndex], x);
                    maxXByBand[bandIndex] = Mathf.Max(maxXByBand[bandIndex], x);
                    minYByBand[bandIndex] = Mathf.Min(minYByBand[bandIndex], y);
                    maxYByBand[bandIndex] = Mathf.Max(maxYByBand[bandIndex], y);
                }
            }

            for (int i = 0; i < bandCount; i++)
            {
                if (maxXByBand[i] >= minXByBand[i])
                {
                    float y = ((minYByBand[i] + maxYByBand[i] + 1) * 0.5f) / height;
                    AppendKeypoint(keypoints, minXByBand[i] / (float)width, y);
                    AppendKeypoint(keypoints, (maxXByBand[i] + 1) / (float)width, y);
                }
                else
                {
                    AppendMissingKeypoint(keypoints);
                    AppendMissingKeypoint(keypoints);
                }
            }

            return keypoints.ToArray();
        }

        private static void AppendBBoxCenterlineEndpointKeypoint(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            bool useBottomEndpoint,
            List<float> keypoints,
            Func<Color32, bool> pixelPredicate)
        {
            int minX = width;
            int maxX = -1;
            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                }
            }

            if (maxX >= minX)
            {
                float endpointY = (useBottomEndpoint ? minY : maxY) / (float)height;
                AppendKeypoint(keypoints, ((minX + maxX + 1) * 0.5f) / width, endpointY);
            }
            else
            {
                AppendMissingKeypoint(keypoints);
            }
        }

        private static void AppendKeypoint(List<float> keypoints, float x, float y)
        {
            keypoints.Add(x);
            keypoints.Add(y);
        }

        private static void AppendMissingKeypoint(List<float> keypoints)
        {
            keypoints.Add(float.NaN);
            keypoints.Add(float.NaN);
        }

        private static string ResolveImageSpaceKeypointLabel(int keypointIndex)
        {
            if (keypointIndex == 0)
            {
                return "bottom_center";
            }

            if (keypointIndex == 1)
            {
                return "top_center";
            }

            int bandEndpointIndex = keypointIndex - 2;
            if (bandEndpointIndex < 0)
            {
                return $"keypoint_{keypointIndex}";
            }

            int bandIndex = bandEndpointIndex / 2;
            string side = bandEndpointIndex % 2 == 0 ? "left" : "right";
            return $"band_{bandIndex}_{side}";
        }

        private static bool TryComputeImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float[] referenceKeypoints,
            out int keypointCount,
            out float l1Delta,
            out float maxL1Delta)
        {
            keypointCount = 0;
            l1Delta = float.NaN;
            maxL1Delta = float.NaN;
            if (candidateKeypoints == null || referenceKeypoints == null)
            {
                return false;
            }

            int length = Mathf.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteKeypointCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFiniteMetric(candidateX) ||
                    !IsFiniteMetric(candidateY) ||
                    !IsFiniteMetric(referenceX) ||
                    !IsFiniteMetric(referenceY))
                {
                    continue;
                }

                float delta = Mathf.Abs(candidateX - referenceX) + Mathf.Abs(candidateY - referenceY);
                sumDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                finiteKeypointCount++;
            }

            if (finiteKeypointCount <= 0)
            {
                return false;
            }

            keypointCount = finiteKeypointCount;
            l1Delta = sumDelta / finiteKeypointCount;
            maxL1Delta = maxDelta;
            return true;
        }

        private static bool TryComputeBBoxNormalizedImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            out int keypointCount,
            out float l1Delta,
            out float maxL1Delta)
        {
            return TryComputeBBoxNormalizedImageSpaceKeypointDelta(
                candidateKeypoints,
                candidateCenterX,
                candidateBBoxWidth,
                candidateBottomGap,
                candidateBBoxHeight,
                referenceKeypoints,
                referenceCenterX,
                referenceBBoxWidth,
                referenceBottomGap,
                referenceBBoxHeight,
                out keypointCount,
                out l1Delta,
                out maxL1Delta,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private static bool TryComputeBBoxNormalizedImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            out int keypointCount,
            out float l1Delta,
            out float maxL1Delta,
            out int maxKeypointIndex,
            out float maxXDelta,
            out float maxYDelta,
            out float maxCandidateX,
            out float maxCandidateY,
            out float maxReferenceX,
            out float maxReferenceY)
        {
            keypointCount = 0;
            l1Delta = float.NaN;
            maxL1Delta = float.NaN;
            maxKeypointIndex = -1;
            maxXDelta = float.NaN;
            maxYDelta = float.NaN;
            maxCandidateX = float.NaN;
            maxCandidateY = float.NaN;
            maxReferenceX = float.NaN;
            maxReferenceY = float.NaN;
            if (candidateKeypoints == null ||
                referenceKeypoints == null ||
                !IsFiniteMetric(candidateCenterX) ||
                !IsFiniteMetric(candidateBBoxWidth) ||
                !IsFiniteMetric(candidateBottomGap) ||
                !IsFiniteMetric(candidateBBoxHeight) ||
                !IsFiniteMetric(referenceCenterX) ||
                !IsFiniteMetric(referenceBBoxWidth) ||
                !IsFiniteMetric(referenceBottomGap) ||
                !IsFiniteMetric(referenceBBoxHeight) ||
                candidateBBoxWidth <= 0f ||
                candidateBBoxHeight <= 0f ||
                referenceBBoxWidth <= 0f ||
                referenceBBoxHeight <= 0f)
            {
                return false;
            }

            int length = Mathf.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            float candidateLeft = candidateCenterX - (candidateBBoxWidth * 0.5f);
            float referenceLeft = referenceCenterX - (referenceBBoxWidth * 0.5f);
            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteKeypointCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFiniteMetric(candidateX) ||
                    !IsFiniteMetric(candidateY) ||
                    !IsFiniteMetric(referenceX) ||
                    !IsFiniteMetric(referenceY))
                {
                    continue;
                }

                float candidateNormalizedX = (candidateX - candidateLeft) / candidateBBoxWidth;
                float candidateNormalizedY = (candidateY - candidateBottomGap) / candidateBBoxHeight;
                float referenceNormalizedX = (referenceX - referenceLeft) / referenceBBoxWidth;
                float referenceNormalizedY = (referenceY - referenceBottomGap) / referenceBBoxHeight;
                float delta =
                    Mathf.Abs(candidateNormalizedX - referenceNormalizedX) +
                    Mathf.Abs(candidateNormalizedY - referenceNormalizedY);
                sumDelta += delta;
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxKeypointIndex = i / 2;
                    maxXDelta = Mathf.Abs(candidateNormalizedX - referenceNormalizedX);
                    maxYDelta = Mathf.Abs(candidateNormalizedY - referenceNormalizedY);
                    maxCandidateX = candidateNormalizedX;
                    maxCandidateY = candidateNormalizedY;
                    maxReferenceX = referenceNormalizedX;
                    maxReferenceY = referenceNormalizedY;
                }
                finiteKeypointCount++;
            }

            if (finiteKeypointCount <= 0)
            {
                return false;
            }

            keypointCount = finiteKeypointCount;
            l1Delta = sumDelta / finiteKeypointCount;
            maxL1Delta = maxDelta;
            return true;
        }

        private static bool TryComputeKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float candidateTopGap,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            float referenceTopGap,
            out int keypointCount,
            out int excludedKeypointCount,
            out float l1Delta,
            out float maxL1Delta,
            out int maxKeypointIndex)
        {
            keypointCount = 0;
            excludedKeypointCount = 0;
            l1Delta = float.NaN;
            maxL1Delta = float.NaN;
            maxKeypointIndex = -1;
            if (candidateKeypoints == null ||
                referenceKeypoints == null ||
                !IsFiniteMetric(candidateCenterX) ||
                !IsFiniteMetric(candidateBBoxWidth) ||
                !IsFiniteMetric(candidateBottomGap) ||
                !IsFiniteMetric(candidateBBoxHeight) ||
                !IsFiniteMetric(candidateTopGap) ||
                !IsFiniteMetric(referenceCenterX) ||
                !IsFiniteMetric(referenceBBoxWidth) ||
                !IsFiniteMetric(referenceBottomGap) ||
                !IsFiniteMetric(referenceBBoxHeight) ||
                !IsFiniteMetric(referenceTopGap) ||
                candidateBBoxWidth <= 0f ||
                candidateBBoxHeight <= 0f ||
                referenceBBoxWidth <= 0f ||
                referenceBBoxHeight <= 0f)
            {
                return false;
            }

            int length = Mathf.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            int totalKeypointCount = length / 2;
            int bandCount = Mathf.Max(0, (totalKeypointCount - 2) / 2);
            float candidateLeft = candidateCenterX - (candidateBBoxWidth * 0.5f);
            float referenceLeft = referenceCenterX - (referenceBBoxWidth * 0.5f);
            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteKeypointCount = 0;
            int excludedCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                int keypointIndex = i / 2;
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFiniteMetric(candidateX) ||
                    !IsFiniteMetric(candidateY) ||
                    !IsFiniteMetric(referenceX) ||
                    !IsFiniteMetric(referenceY))
                {
                    continue;
                }

                if (IsKeypointAffectedByVerticalFrameEdge(
                        keypointIndex,
                        bandCount,
                        referenceBottomGap,
                        referenceTopGap) ||
                    IsKeypointAffectedByVerticalFrameEdge(
                        keypointIndex,
                        bandCount,
                        candidateBottomGap,
                        candidateTopGap))
                {
                    excludedCount++;
                    continue;
                }

                float candidateNormalizedX = (candidateX - candidateLeft) / candidateBBoxWidth;
                float candidateNormalizedY = (candidateY - candidateBottomGap) / candidateBBoxHeight;
                float referenceNormalizedX = (referenceX - referenceLeft) / referenceBBoxWidth;
                float referenceNormalizedY = (referenceY - referenceBottomGap) / referenceBBoxHeight;
                float delta =
                    Mathf.Abs(candidateNormalizedX - referenceNormalizedX) +
                    Mathf.Abs(candidateNormalizedY - referenceNormalizedY);
                sumDelta += delta;
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxKeypointIndex = keypointIndex;
                }
                finiteKeypointCount++;
            }

            if (finiteKeypointCount <= 0)
            {
                excludedKeypointCount = excludedCount;
                return false;
            }

            keypointCount = finiteKeypointCount;
            excludedKeypointCount = excludedCount;
            l1Delta = sumDelta / finiteKeypointCount;
            maxL1Delta = maxDelta;
            return true;
        }

        private static bool IsKeypointAffectedByVerticalFrameEdge(
            int keypointIndex,
            int bandCount,
            float bottomGapRatio,
            float topGapRatio)
        {
            bool bottomTouched = IsFiniteMetric(bottomGapRatio) &&
                                 bottomGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance;
            bool topTouched = IsFiniteMetric(topGapRatio) &&
                              topGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance;
            if (!bottomTouched && !topTouched)
            {
                return false;
            }

            if (keypointIndex == 0)
            {
                return bottomTouched;
            }

            if (keypointIndex == 1)
            {
                return topTouched;
            }

            int bandEndpointIndex = keypointIndex - 2;
            if (bandEndpointIndex < 0 || bandCount <= 0)
            {
                return bottomTouched || topTouched;
            }

            int bandIndex = bandEndpointIndex / 2;
            return (bottomTouched && bandIndex == 0) ||
                   (topTouched && bandIndex >= bandCount - 1);
        }

        private static bool IsCandidateBrightPixel(Color32 pixel)
        {
            float luminance =
                ((pixel.r * 0.2126f) + (pixel.g * 0.7152f) + (pixel.b * 0.0722f)) / 255f;
            return pixel.a > CandidateScreenshotOpaqueAlphaThreshold &&
                   luminance > CandidateScreenshotBrightLuminanceThreshold;
        }

        private static bool IsCandidateNonHairBrightPixel(Color32 pixel)
        {
            return IsCandidateBrightPixel(pixel) && !IsCandidateHairLikePixel(pixel);
        }

        private static bool IsCandidateHairLikePixel(Color32 pixel)
        {
            return IsCandidateCyanTealHairLikePixel(pixel) ||
                   IsCandidateDarkTealHairShadowPixel(pixel);
        }

        private static bool IsCandidateCyanTealHairLikePixel(Color32 pixel)
        {
            if (!IsCandidateBrightPixel(pixel))
            {
                return false;
            }

            return pixel.g >= 90 &&
                   pixel.b >= 90 &&
                   pixel.r <= 170 &&
                   pixel.g >= pixel.r * 1.15f &&
                   pixel.b >= pixel.r * 1.10f &&
                   Mathf.Abs(pixel.g - pixel.b) <= 100;
        }

        private static bool IsCandidateDarkTealHairShadowPixel(Color32 pixel)
        {
            if (!IsCandidateBrightPixel(pixel))
            {
                return false;
            }

            return pixel.r <= 80 &&
                   pixel.g >= 25 &&
                   pixel.b >= 25 &&
                   pixel.g >= pixel.r * 1.35f &&
                   pixel.b >= pixel.r * 1.35f &&
                   Mathf.Abs(pixel.g - pixel.b) <= 80;
        }

        private static bool IsFiniteMetric(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float ResolveFrameTopGapRatio(float bottomGapRatio, float bboxHeightRatio)
        {
            if (!IsFiniteMetric(bottomGapRatio) || !IsFiniteMetric(bboxHeightRatio))
            {
                return float.NaN;
            }

            return Mathf.Max(0f, 1f - bottomGapRatio - bboxHeightRatio);
        }

        private static bool IsFrameEdgeTouched(float bottomGapRatio, float topGapRatio)
        {
            return (IsFiniteMetric(bottomGapRatio) &&
                    bottomGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance) ||
                   (IsFiniteMetric(topGapRatio) &&
                    topGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance);
        }

        private static int IndexOfHeader(string[] headers, string headerName)
        {
            if (headers == null)
            {
                return -1;
            }

            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i], headerName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string ResolveProjectRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                return relativePath ?? string.Empty;
            }

            string projectRoot = ResolveProjectRootForDiagnostics();
            return string.IsNullOrWhiteSpace(projectRoot)
                ? relativePath
                : Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolveProjectRootForDiagnostics()
        {
            if (!string.IsNullOrWhiteSpace(_projectRoot))
            {
                return _projectRoot;
            }

            string dataPath = Application.dataPath;
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                DirectoryInfo parent = Directory.GetParent(dataPath);
                if (parent != null)
                {
                    return parent.FullName;
                }
            }

            return Directory.GetCurrentDirectory();
        }

        private static int ParseInvariantInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        private static float ParseInvariantFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : float.NaN;
        }

        private static SummaryCandidateArtifactSelection BuildCandidateArtifactSelection(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            SummaryCandidateArtifactSelection selection = new SummaryCandidateArtifactSelection();
            if (frameQualitySummaries == null || frameQualitySummaries.Length == 0)
            {
                selection.selection_basis = "no frame_quality summary is available";
                return selection;
            }

            MotionComparisonFrameQualitySummary mainAutoIntegrated = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "main_auto_integrated_vertical_solve_metrics", StringComparison.Ordinal));
            MotionComparisonFrameQualitySummary raw = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (raw == null)
            {
                raw = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal));
            }

            if (raw == null)
            {
                raw = mainAutoIntegrated;
            }

            if (raw == null)
            {
                raw = frameQualitySummaries.FirstOrDefault(summary => summary != null);
            }

            MotionComparisonFrameQualitySummary corrected = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "corrected_candidate_metrics", StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (corrected == null)
            {
                corrected = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(summary.frame_quality_evaluation_role, "corrected_candidate_metrics", StringComparison.Ordinal));
            }

            FillRawCandidateSelectionFields(selection, raw);
            FillCorrectedCandidateSelectionFields(selection, corrected);

            bool correctedPasses = corrected != null &&
                string.Equals(corrected.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(corrected.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(corrected.candidate_metrics_csv);
            bool integratedPrimaryPasses = mainAutoIntegrated != null &&
                string.Equals(mainAutoIntegrated.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(mainAutoIntegrated.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(mainAutoIntegrated.candidate_metrics_csv);
            if (integratedPrimaryPasses)
            {
                FillSelectedCandidateFields(selection, mainAutoIntegrated);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = true;
                FillSelectedCandidateAcceptanceEvidence(selection, raw, mainAutoIntegrated, mainAutoIntegrated.vertical_solve_corrected_candidate_manifest_path);
                selection.selection_basis =
                    "primary Main_Auto export paths passed after bounded vertical solve integration; raw diagnostic artifacts remain preserved";
                return selection;
            }

            if (correctedPasses)
            {
                FillSelectedCandidateFields(selection, corrected);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = raw != null;
                FillSelectedCandidateAcceptanceEvidence(selection, raw, corrected, raw != null ? raw.vertical_solve_corrected_candidate_manifest_path : string.Empty);
                selection.selection_basis = selection.selected_candidate_is_acceptance_artifact
                    ? "corrected candidate passed frame-quality gates and is selected for user-facing export; raw candidate remains recorded for diagnostics"
                    : "corrected candidate passed metric gates but did not produce a distinct export VMD; selected as diagnostic evidence while raw candidate remains recorded";
                return selection;
            }

            if (raw != null)
            {
                FillSelectedCandidateFields(selection, raw);
                bool rawPasses = string.Equals(raw.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(raw.candidate_vmd_path) &&
                    !string.IsNullOrWhiteSpace(raw.candidate_metrics_csv);
                if (rawPasses)
                {
                    selection.selected_candidate_output_role = "user_facing_export_artifact";
                    selection.selected_candidate_preserves_raw_diagnostic = false;
                    FillSelectedCandidateAcceptanceEvidence(selection, raw, raw, string.Empty);
                    selection.selection_basis =
                        "raw candidate passed frame-quality gates and is selected for user-facing export; no corrected candidate was required";
                }
                else
                {
                    selection.selection_basis = corrected == null
                        ? "no corrected candidate is available; selected raw/evaluation candidate for diagnostics"
                        : "corrected candidate is not passing; selected raw/evaluation candidate for diagnostics";
                }
            }

            return selection;
        }

        private static bool IsMainAutoSummary(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                !string.IsNullOrWhiteSpace(summary.candidate_label) &&
                summary.candidate_label.IndexOf("Main_Auto", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void FillSelectedCandidateFields(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary summary)
        {
            if (selection == null || summary == null)
            {
                return;
            }

            selection.selected_candidate_role = summary.frame_quality_evaluation_role ?? string.Empty;
            selection.selected_candidate_status = summary.status ?? string.Empty;
            selection.selected_candidate_status_reason = summary.status_reason ?? string.Empty;
            selection.selected_candidate_metrics_csv = summary.candidate_metrics_csv ?? string.Empty;
            selection.selected_candidate_vmd_path = summary.candidate_vmd_path ?? string.Empty;
        }

        private static void FillSelectedCandidateAcceptanceEvidence(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary raw,
            MotionComparisonFrameQualitySummary selected,
            string selectedManifestPath)
        {
            if (selection == null || selected == null)
            {
                return;
            }

            selection.selected_candidate_manifest_path = selectedManifestPath ?? string.Empty;
            bool selectedCorrectedArtifact = string.Equals(
                selected.frame_quality_evaluation_role,
                "corrected_candidate_metrics",
                StringComparison.Ordinal);
            bool selectedIntegratedPrimary = string.Equals(
                selected.frame_quality_evaluation_role,
                "main_auto_integrated_vertical_solve_metrics",
                StringComparison.Ordinal);
            bool selectedRawPrimary = IsRawCandidateRole(selected);
            if (selectedCorrectedArtifact)
            {
                EnsureCorrectedCandidateSelectionManifest(selection, raw);
            }

            selection.selected_candidate_vmd_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(selection.selected_candidate_vmd_path);
            selection.selected_candidate_metrics_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_metrics_csv) &&
                File.Exists(selection.selected_candidate_metrics_csv);
            selection.selected_candidate_manifest_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_manifest_path) &&
                File.Exists(selection.selected_candidate_manifest_path);
            string integratedRawDiagnosticVmdPath = selectedIntegratedPrimary
                ? ResolveIntegratedPrimaryRawDiagnosticVmdPath(selection.selected_candidate_manifest_path)
                : string.Empty;
            bool differsFromRawSummary =
                raw != null &&
                !string.IsNullOrWhiteSpace(raw.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(raw.candidate_vmd_path) &&
                selection.selected_candidate_vmd_exists &&
                !PathsReferToSameFile(raw.candidate_vmd_path, selection.selected_candidate_vmd_path) &&
                FilesDiffer(raw.candidate_vmd_path, selection.selected_candidate_vmd_path);
            bool differsFromIntegratedRawDiagnostic =
                selectedIntegratedPrimary &&
                !string.IsNullOrWhiteSpace(integratedRawDiagnosticVmdPath) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(integratedRawDiagnosticVmdPath) &&
                selection.selected_candidate_vmd_exists &&
                !PathsReferToSameFile(integratedRawDiagnosticVmdPath, selection.selected_candidate_vmd_path) &&
                FilesDiffer(integratedRawDiagnosticVmdPath, selection.selected_candidate_vmd_path);
            selection.selected_candidate_differs_from_raw_vmd =
                differsFromRawSummary || differsFromIntegratedRawDiagnostic;
            selection.selected_candidate_differs_from_raw_metrics =
                raw != null &&
                !string.IsNullOrWhiteSpace(raw.candidate_metrics_csv) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_metrics_csv) &&
                File.Exists(raw.candidate_metrics_csv) &&
                selection.selected_candidate_metrics_exists &&
                !PathsReferToSameFile(raw.candidate_metrics_csv, selection.selected_candidate_metrics_csv) &&
                FilesDiffer(raw.candidate_metrics_csv, selection.selected_candidate_metrics_csv);

            bool selectedPasses = string.Equals(selected.status, "pass", StringComparison.OrdinalIgnoreCase);
            bool hasRequiredFiles = selectedCorrectedArtifact
                ? selection.selected_candidate_vmd_exists &&
                  selection.selected_candidate_metrics_exists &&
                  selection.selected_candidate_manifest_exists &&
                  selection.selected_candidate_differs_from_raw_vmd
                : selectedIntegratedPrimary
                    ? selection.selected_candidate_vmd_exists &&
                      selection.selected_candidate_metrics_exists &&
                      selection.selected_candidate_manifest_exists &&
                      selection.selected_candidate_differs_from_raw_vmd
                    : selectedRawPrimary &&
                      selection.selected_candidate_vmd_exists &&
                      selection.selected_candidate_metrics_exists;
            selection.selected_candidate_is_acceptance_artifact =
                selectedPasses &&
                (selectedCorrectedArtifact || selectedIntegratedPrimary || selectedRawPrimary) &&
                string.Equals(selection.selected_candidate_output_role, "user_facing_export_artifact", StringComparison.Ordinal) &&
                (selectedRawPrimary || selection.selected_candidate_preserves_raw_diagnostic) &&
                hasRequiredFiles;
            if (selectedCorrectedArtifact &&
                selection.selected_candidate_vmd_exists &&
                selection.selected_candidate_metrics_exists &&
                selection.selected_candidate_manifest_exists &&
                !selection.selected_candidate_differs_from_raw_vmd)
            {
                selection.selected_candidate_output_role = "diagnostic_artifact";
            }

            selection.selected_candidate_acceptance_basis = selection.selected_candidate_is_acceptance_artifact
                ? selectedIntegratedPrimary
                    ? "selected primary Main_Auto export VMD/metrics/manifest is the final acceptance/export candidate; raw diagnostic files remain preserved"
                    : selectedCorrectedArtifact
                        ? "selected corrected VMD/metrics/manifest is the final acceptance/export candidate; raw candidate remains diagnostic"
                        : "selected raw VMD/metrics is the final acceptance/export candidate; no corrected artifact was required"
                : selectedCorrectedArtifact &&
                  selection.selected_candidate_vmd_exists &&
                  selection.selected_candidate_metrics_exists &&
                  selection.selected_candidate_manifest_exists &&
                  !selection.selected_candidate_differs_from_raw_vmd
                    ? "selected corrected metrics/manifest use a raw-copy VMD, so they are diagnostic only; raw candidate remains the diagnostic baseline"
                    : "selected candidate is not a final acceptance/export artifact yet; raw candidate remains the diagnostic baseline";
        }

        private static bool IsRawCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                (string.Equals(summary.frame_quality_evaluation_role, "raw_candidate_metrics", StringComparison.Ordinal) ||
                    string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal));
        }

        private static void EnsureCorrectedCandidateSelectionManifest(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary raw)
        {
            if (selection == null || raw == null)
            {
                return;
            }

            string manifestPath = ResolveSelectionArtifactPath(selection.selected_candidate_manifest_path, string.Empty);
            if (string.IsNullOrWhiteSpace(manifestPath) || File.Exists(manifestPath))
            {
                return;
            }

            string rawMetricsPath = ResolveSelectionArtifactPath(raw.candidate_metrics_csv, string.Empty);
            string rawVmdPath = ResolveSelectionArtifactPath(raw.candidate_vmd_path, string.Empty);
            string correctedMetricsPath = ResolveSelectionArtifactPath(selection.selected_candidate_metrics_csv, string.Empty);
            string correctedVmdPath = ResolveSelectionArtifactPath(selection.selected_candidate_vmd_path, string.Empty);
            if (!File.Exists(rawMetricsPath) ||
                !File.Exists(rawVmdPath) ||
                !File.Exists(correctedMetricsPath) ||
                !File.Exists(correctedVmdPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json =
                "{" +
                "\"artifact_role\":\"corrected_vertical_solve_candidate\"," +
                "\"generated_at\":\"" + EscapeJson(DateTime.Now.ToString("o", CultureInfo.InvariantCulture)) + "\"," +
                "\"raw_candidate_metrics_csv\":\"" + EscapeJson(raw.candidate_metrics_csv) + "\"," +
                "\"raw_candidate_vmd_path\":\"" + EscapeJson(raw.candidate_vmd_path) + "\"," +
                "\"corrected_candidate_metrics_csv\":\"" + EscapeJson(selection.selected_candidate_metrics_csv) + "\"," +
                "\"corrected_candidate_vmd_path\":\"" + EscapeJson(selection.selected_candidate_vmd_path) + "\"," +
                "\"frame_quality_evaluator\":\"raw_frame_quality_evaluator\"," +
                "\"manifest_source\":\"yyb_visual_candidate_selection\"" +
                "}";
            File.WriteAllText(manifestPath, json, Encoding.UTF8);
        }

        private static string ResolveIntegratedPrimaryRawDiagnosticVmdPath(string manifestPath)
        {
            string absoluteManifestPath = ResolveSelectionArtifactPath(manifestPath, string.Empty);
            if (string.IsNullOrWhiteSpace(absoluteManifestPath) || !File.Exists(absoluteManifestPath))
            {
                return string.Empty;
            }

            try
            {
                IntegratedVerticalSolvePrimaryExportManifest manifest =
                    JsonUtility.FromJson<IntegratedVerticalSolvePrimaryExportManifest>(
                        File.ReadAllText(absoluteManifestPath, Encoding.UTF8));
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.raw_diagnostic_vmd_path))
                {
                    return string.Empty;
                }

                return ResolveSelectionArtifactPath(
                    manifest.raw_diagnostic_vmd_path,
                    Path.GetDirectoryName(absoluteManifestPath));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string ResolveSelectionArtifactPath(string path, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            if (!string.IsNullOrWhiteSpace(_projectRoot))
            {
                return ToAbsoluteProjectPath(normalized);
            }

            return string.IsNullOrWhiteSpace(baseDirectory)
                ? normalized
                : Path.Combine(baseDirectory, normalized);
        }

        private static bool PathsReferToSameFile(string leftPath, string rightPath)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(leftPath),
                    Path.GetFullPath(rightPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool FilesDiffer(string leftPath, string rightPath)
        {
            FileInfo left = new FileInfo(leftPath);
            FileInfo right = new FileInfo(rightPath);
            if (left.Length != right.Length)
            {
                return true;
            }

            byte[] leftBytes = File.ReadAllBytes(leftPath);
            byte[] rightBytes = File.ReadAllBytes(rightPath);
            if (leftBytes.Length != rightBytes.Length)
            {
                return true;
            }

            for (int i = 0; i < leftBytes.Length; i++)
            {
                if (leftBytes[i] != rightBytes[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static void FillRawCandidateSelectionFields(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary raw)
        {
            if (selection == null || raw == null)
            {
                return;
            }

            selection.raw_candidate_status = raw.status ?? string.Empty;
            selection.raw_candidate_status_reason = raw.status_reason ?? string.Empty;
            selection.raw_candidate_metrics_csv = raw.candidate_metrics_csv ?? string.Empty;
            selection.raw_candidate_vmd_path = raw.candidate_vmd_path ?? string.Empty;
        }

        private static void FillCorrectedCandidateSelectionFields(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary corrected)
        {
            if (selection == null || corrected == null)
            {
                return;
            }

            selection.corrected_candidate_status = corrected.status ?? string.Empty;
            selection.corrected_candidate_status_reason = corrected.status_reason ?? string.Empty;
            selection.corrected_candidate_metrics_csv = corrected.candidate_metrics_csv ?? string.Empty;
            selection.corrected_candidate_vmd_path = corrected.candidate_vmd_path ?? string.Empty;
        }

        private static SummarySampleOrderingDiagnostic[] BuildSampleOrderingDiagnostics()
        {
            return Results
                .Select(result => BuildSampleOrderingDiagnostic(
                    result.jobMode,
                    result.sceneName,
                    result.comparisonMetricsCsvPath))
                .ToArray();
        }

        private static SummarySampleOrderingDiagnostic BuildSampleOrderingDiagnostic(
            string jobMode,
            string sceneName,
            string metricsCsvPath)
        {
            SummarySampleOrderingDiagnostic diagnostic = new SummarySampleOrderingDiagnostic
            {
                job_mode = jobMode ?? string.Empty,
                scene_name = sceneName ?? string.Empty,
                metrics_csv = metricsCsvPath ?? string.Empty
            };

            string absolutePath = ToAbsoluteProjectPath(metricsCsvPath);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return diagnostic;
            }

            string[] lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return diagnostic;
            }

            string[] headers = SplitSimpleCsvLine(lines[0]);
            Dictionary<string, int> indices = BuildCsvIndexMap(headers);
            List<string[]> rows = new List<string[]>();
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    continue;
                }

                rows.Add(SplitSimpleCsvLine(lines[lineIndex]));
            }

            diagnostic.metric_row_count = rows.Count;
            if (rows.Count == 0)
            {
                return diagnostic;
            }

            string[] first = rows[0];
            string[] finish = rows.LastOrDefault(row =>
                string.Equals(GetCsvString(row, indices, "reason"), "finish", StringComparison.OrdinalIgnoreCase))
                ?? rows[rows.Count - 1];

            diagnostic.first_metric_reason = GetCsvString(first, indices, "reason");
            diagnostic.first_metric_recorder_frame = GetCsvInt(first, indices, "recorderFrame");
            diagnostic.first_metric_engine_frame_count = GetCsvInt(first, indices, "frameCount");
            diagnostic.first_metric_time_since_level_load = GetCsvFloat(first, indices, "timeSinceLevelLoad");
            diagnostic.first_metric_animation_clip_time = GetCsvFloat(first, indices, "animationClipTime");
            diagnostic.first_metric_grounding_vertical_step_last = GetCsvFloat(first, indices, "retargetGroundingVerticalStepLast");
            diagnostic.first_metric_grounding_initial_vertical_step = GetCsvFloat(first, indices, "retargetGroundingInitialVerticalStep");
            diagnostic.first_metric_grounding_step_clamp_count = GetCsvInt(first, indices, "retargetGroundingStepClampCount");
            diagnostic.first_metric_grounding_smoothed_count = GetCsvInt(first, indices, "retargetGroundingSmoothedCount");
            diagnostic.first_metric_grounding_max_step_per_frame = GetCsvFloat(first, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.first_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                first,
                indices,
                diagnostic.first_metric_grounding_vertical_step_last,
                diagnostic.first_metric_grounding_max_step_per_frame);
            diagnostic.first_metric_grounding_vertical_step_at_max_step =
                IsGroundingVerticalStepAtMax(diagnostic.first_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.finish_metric_reason = GetCsvString(finish, indices, "reason");
            diagnostic.finish_metric_recorder_frame = GetCsvInt(finish, indices, "recorderFrame");
            diagnostic.finish_metric_engine_frame_count = GetCsvInt(finish, indices, "frameCount");
            diagnostic.finish_metric_time_since_level_load = GetCsvFloat(finish, indices, "timeSinceLevelLoad");
            diagnostic.finish_metric_animation_clip_time = GetCsvFloat(finish, indices, "animationClipTime");
            diagnostic.finish_metric_grounding_vertical_step_last = GetCsvFloat(finish, indices, "retargetGroundingVerticalStepLast");
            diagnostic.finish_metric_grounding_step_clamp_count = GetCsvInt(finish, indices, "retargetGroundingStepClampCount");
            diagnostic.finish_metric_grounding_smoothed_count = GetCsvInt(finish, indices, "retargetGroundingSmoothedCount");
            diagnostic.finish_metric_grounding_max_step_per_frame = GetCsvFloat(finish, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.finish_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                finish,
                indices,
                diagnostic.finish_metric_grounding_vertical_step_last,
                diagnostic.finish_metric_grounding_max_step_per_frame);
            diagnostic.finish_metric_grounding_vertical_step_at_max_step =
                IsGroundingVerticalStepAtMax(diagnostic.finish_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.recording_metric_recorder_frame_span = CalculateMetricIntSpan(
                diagnostic.first_metric_recorder_frame,
                diagnostic.finish_metric_recorder_frame);
            diagnostic.recording_metric_engine_frame_span = CalculateMetricIntSpan(
                diagnostic.first_metric_engine_frame_count,
                diagnostic.finish_metric_engine_frame_count);
            diagnostic.recording_metric_time_since_level_load_span = CalculateMetricFloatSpan(
                diagnostic.first_metric_time_since_level_load,
                diagnostic.finish_metric_time_since_level_load);
            diagnostic.recording_grounding_step_clamp_delta = CalculateMetricIntSpan(
                diagnostic.first_metric_grounding_step_clamp_count,
                diagnostic.finish_metric_grounding_step_clamp_count);
            diagnostic.recording_grounding_smoothed_delta = CalculateMetricIntSpan(
                diagnostic.first_metric_grounding_smoothed_count,
                diagnostic.finish_metric_grounding_smoothed_count);
            diagnostic.recording_phase_span_role =
                "finish-first recording phase metrics; absolute first engine frame includes scene load/import/prewarm startup offset and can vary between Unity batch runs";
            diagnostic.grounding_step_limit_role =
                "prewarm residual is identified by the first recorder-frame grounding step reaching its configured max; recording clamp/smoothed deltas are finish-first counters inside the captured phase";
            return diagnostic;
        }

        private static float ResolveGroundingStepToMaxRatio(
            string[] row,
            Dictionary<string, int> indices,
            float step,
            float maxStep)
        {
            float reportedRatio = GetCsvFloat(row, indices, "retargetGroundingLastStepToMaxStepRatio");
            if (!float.IsNaN(reportedRatio) && !float.IsInfinity(reportedRatio))
            {
                return reportedRatio;
            }

            if (float.IsNaN(step) ||
                float.IsInfinity(step) ||
                float.IsNaN(maxStep) ||
                float.IsInfinity(maxStep) ||
                maxStep <= 0f)
            {
                return float.NaN;
            }

            return Mathf.Abs(step) / maxStep;
        }

        private static bool IsGroundingVerticalStepAtMax(float stepToMaxRatio)
        {
            return !float.IsNaN(stepToMaxRatio) &&
                !float.IsInfinity(stepToMaxRatio) &&
                stepToMaxRatio >= 0.95f;
        }

        private static int CalculateMetricIntSpan(int first, int finish)
        {
            if (first < 0 || finish < 0)
            {
                return -1;
            }

            return finish - first;
        }

        private static float CalculateMetricFloatSpan(float first, float finish)
        {
            if (float.IsNaN(first) ||
                float.IsNaN(finish) ||
                float.IsInfinity(first) ||
                float.IsInfinity(finish))
            {
                return float.NaN;
            }

            return finish - first;
        }

        private static string[] SplitSimpleCsvLine(string line)
        {
            return (line ?? string.Empty).Split(',');
        }

        private static Dictionary<string, int> BuildCsvIndexMap(string[] headers)
        {
            Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                if (!indices.ContainsKey(headers[index]))
                {
                    indices.Add(headers[index], index);
                }
            }

            return indices;
        }

        private static string GetCsvString(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            if (row == null ||
                indices == null ||
                string.IsNullOrEmpty(column) ||
                !indices.TryGetValue(column, out int index) ||
                index < 0 ||
                index >= row.Length)
            {
                return string.Empty;
            }

            return row[index] ?? string.Empty;
        }

        private static int GetCsvInt(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return int.TryParse(
                GetCsvString(row, indices, column),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : 0;
        }

        private static float GetCsvFloat(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return float.TryParse(
                GetCsvString(row, indices, column),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : float.NaN;
        }

        private static string ToAbsoluteProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(_projectRoot, normalized);
        }

        private static string FormatQualityFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? "n/a"
                : value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatEnabledWeight(bool enabled, float weight)
        {
            return $"{enabled}/{FormatQualityFloat(weight)}";
        }

        private static string FormatEnabledWeightCap(bool enabled, float weight, float cap)
        {
            return $"{enabled}/{FormatQualityFloat(weight)}/{FormatQualityFloat(cap)}";
        }

        private static string FormatEnabledWeightCapScale(bool enabled, float weight, float cap, float scale)
        {
            return $"{enabled}/{FormatQualityFloat(weight)}/{FormatQualityFloat(cap)}/{FormatQualityFloat(scale)}";
        }

        private static string FormatEnabledWeightCapScaleGate(
            bool enabled,
            float weight,
            float cap,
            float scale,
            float frameGateStart,
            float frameGateEnd)
        {
            return $"{FormatEnabledWeightCapScale(enabled, weight, cap, scale)}/{FormatQualityFloat(frameGateStart)}-{FormatQualityFloat(frameGateEnd)}";
        }

        private static string FormatEnabledWeightCapScaleBlendGate(
            bool enabled,
            float weight,
            float cap,
            float scale,
            float blend,
            float frameGateStart,
            float frameGateEnd)
        {
            return $"{FormatEnabledWeightCapScale(enabled, weight, cap, scale)}/blend:{FormatQualityFloat(blend)}/{FormatQualityFloat(frameGateStart)}-{FormatQualityFloat(frameGateEnd)}";
        }

        private static string FormatEvaluatorXzReferenceSettings(CaptureResult result)
        {
            return result == null
                ? "False/n/a"
                : $"{result.usePostSetHumanPoseRightFootEvaluatorXzReference}/{FormatQualityFloat(result.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude)}";
        }

        private static string FormatArmSwingSettings(CaptureResult result)
        {
            if (result == null)
            {
                return "n/a";
            }

            return
                $"{result.enableYybArmSwingLimitCorrection}/" +
                $"{FormatQualityFloat(result.yybArmSwingLimitWeight)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMaxDownDot)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMinHandHorizontalRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMaxHandBelowShoulderRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingHorizontalReachLimitWeight)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMaxHandHorizontalReachRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingHorizontalReachMinElbowAngleAfterApply)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseHorizontalReachLimitWeight)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseMinUpperArmDownDot)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio)}";
        }

        private static void CopyLatestSummary(string sourcePath, string relativeTargetPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            string targetPath = Path.Combine(_projectRoot, relativeTargetPath);
            string targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        private static AnimationClip LoadFirstAnimationClip(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.humanMotion)
                {
                    return clip;
                }
            }

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static string ResolveReferenceClipAssetPath(string fbxFileName, Func<string, bool> hasReferenceClip)
        {
            string normalizedFileName = NormalizeFbxFileName(fbxFileName);
            string projectCandidate = Path.Combine(ProjectFbxDirectory, normalizedFileName).Replace('\\', '/');
            if (hasReferenceClip(projectCandidate))
            {
                return projectCandidate;
            }

            string importCandidate = Path.Combine(ImportFbxDirectory, normalizedFileName).Replace('\\', '/');
            if (hasReferenceClip(importCandidate))
            {
                return importCandidate;
            }

            return importCandidate;
        }

        private static string NormalizeFbxFileName(string fbxFileName)
        {
            string name = string.IsNullOrWhiteSpace(fbxFileName) ? DefaultFbxFileName : fbxFileName.Trim();
            return string.Equals(Path.GetExtension(name), ".fbx", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(name)
                : Path.GetFileNameWithoutExtension(name) + ".fbx";
        }

        private static string GetCommandLineValue(string name, string fallbackValue)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return fallbackValue;
        }

        private static float GetCommandLineFloat(string name, float fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallbackValue;
        }

        private static int GetCommandLineInt(string name, int fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallbackValue;
        }

        private static bool GetCommandLineBool(string name, bool fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallbackValue;
            }

            if (bool.TryParse(value, out bool parsedBool))
            {
                return parsedBool;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                ? true
                : string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : fallbackValue;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string MakeProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            string normalizedProjectRoot = _projectRoot.Replace('\\', '/').TrimEnd('/');
            string normalizedAbsolute = absolutePath.Replace('\\', '/');
            if (normalizedAbsolute.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedAbsolute.Substring(normalizedProjectRoot.Length + 1);
            }

            return normalizedAbsolute;
        }

        private static string EscapeMarkdown(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "\\|");
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string BuildSafeSummarySessionId(string sessionId)
        {
            string safeSessionId = SanitizeFileName(sessionId);
            string rootFolder = Path.Combine(_projectRoot, OutputRootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int leafFileNameLength = Mathf.Max(SummaryJsonFileName.Length, SummaryMarkdownFileName.Length);
            int maxSessionIdLength = EvidenceSafeMaxFullPathLength
                                     - rootFolder.Length
                                     - 1
                                     - 1
                                     - leafFileNameLength;
            maxSessionIdLength = Mathf.Max(16, maxSessionIdLength);
            return ShortenFileNameToLength(safeSessionId, maxSessionIdLength);
        }

        private static string SanitizeFileName(string fileName)
        {
            string safeName = string.IsNullOrWhiteSpace(fileName) ? "yyb_visual_compare" : fileName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return safeName.Replace(' ', '_');
        }

        private static string BuildCandidateVmdEvidenceFileName(CaptureMode mode, string extension)
        {
            return BuildCandidateVmdEvidenceFileName(mode.ToString(), extension);
        }

        private static string BuildCandidateVmdEvidenceFileName(string mode, string extension)
        {
            string safeExtension = string.IsNullOrWhiteSpace(extension) ? ".vmd" : extension;
            string shortMode = string.Equals(mode, CaptureMode.MainRecording.ToString(), StringComparison.Ordinal)
                ? "rec"
                : string.Equals(mode, CaptureMode.MainRecordingVmdPlaybackProbe.ToString(), StringComparison.Ordinal)
                    ? "replay"
                    : string.Equals(mode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal)
                        ? "auto"
                        : SanitizeFileName(mode);
            return $"vmd-{shortMode}{safeExtension}";
        }

        private static string ShortenFileNameToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            int safeMaxLength = Mathf.Max(10, maxLength);
            if (value.Length <= safeMaxLength)
            {
                return value;
            }

            const int hashLength = 8;
            int prefixLength = Mathf.Max(1, safeMaxLength - hashLength - 1);
            return $"{value.Substring(0, prefixLength)}_{CalculateStableHash(value):x8}";
        }

        private static uint CalculateStableHash(string value)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            uint hash = offsetBasis;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }

        [Serializable]
        private sealed class SummaryContainer
        {
            public string session_id;
            public string generated_at;
            public string fbx_file;
            public float duration_seconds;
            public int target_frame_count;
            public string segment;
            public bool finger_closeups;
            public bool recorder_parent_ik_offsets_when_center_parented;
            public float mmd_ik_delta_guard_limit_override_vmd;
            public float mmd_ik_delta_guard_recovery_trigger_vmd;
            public float mmd_ik_delta_guard_recovery_debt_vmd;
            public int mmd_ik_delta_guard_recovery_hold_frames;
            public bool final_ik_foot_grounding_enabled;
            public bool manual_animator_foot_local_rotation_enabled;
            public bool manual_animator_foot_local_rotation_disabled;
            public bool manual_animator_full_body_pose_enabled;
            public bool manual_animator_full_body_pose_disabled;
            public float manual_animator_full_body_pose_weight;
            public bool manual_animator_full_body_pose_exclude_lower_body_muscles;
            public bool manual_animator_full_body_pose_lower_body_muscles_only;
            public bool manual_animator_full_body_pose_leg_twist_muscles_only;
            public bool manual_animator_body_rotation_enabled;
            public bool manual_animator_body_rotation_disabled;
            public float manual_animator_body_rotation_weight;
            public bool manual_animator_hand_local_rotation_enabled;
            public bool manual_animator_thumb_local_rotation_enabled;
            public bool manual_animator_hand_palm_frame_enabled;
            public float manual_animator_hand_palm_frame_weight;
            public bool retarget_pose_visual_spike_smoothing_override;
            public bool retarget_pose_visual_spike_smoothing_enabled;
            public float retarget_pose_visual_spike_current_weight;
            public float retarget_pose_visual_spike_forearm_stretch_clamp_max_offset;
            public bool yyb_arm_swing_limit_enabled;
            public float yyb_arm_swing_limit_weight;
            public float yyb_arm_swing_max_down_dot;
            public float yyb_arm_swing_min_hand_horizontal_ratio;
            public float yyb_arm_swing_max_hand_below_shoulder_ratio;
            public float yyb_arm_swing_horizontal_reach_limit_weight;
            public float yyb_arm_swing_max_hand_horizontal_reach_ratio;
            public float yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio;
            public float yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply;
            public float yyb_arm_swing_raised_pose_horizontal_reach_limit_weight;
            public float yyb_arm_swing_raised_pose_min_upper_arm_down_dot;
            public float yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio;
            public float yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio;
            public bool yyb_arm_direction_retarget_enabled;
            public float yyb_arm_direction_upper_arm_weight;
            public float yyb_arm_direction_forearm_weight;
            public float yyb_arm_direction_upper_arm_max_degrees;
            public float yyb_arm_direction_forearm_max_degrees;
            public float yyb_arm_direction_left_side_weight_scale;
            public float yyb_arm_direction_right_side_weight_scale;
            public bool yyb_arm_sleeve_anchor_override;
            public bool yyb_arm_sleeve_anchor_enabled;
            public float yyb_arm_sleeve_anchor_influence;
            public float yyb_arm_shoulder_cap_anchor_influence;
            public float yyb_arm_sleeve_anchor_max_degrees;
            public bool yyb_arm_visual_twist_override;
            public bool yyb_arm_visual_twist_enabled;
            public float yyb_arm_visual_upper_arm_influence;
            public float yyb_arm_visual_forearm_influence;
            public float yyb_arm_visual_upper_arm_max_degrees;
            public float yyb_arm_visual_forearm_max_degrees;
            public bool manual_animator_lower_body_segment_direction_enabled;
            public bool manual_animator_lower_body_segment_direction_disabled;
            public float manual_animator_lower_body_segment_direction_weight;
            public float manual_animator_lower_body_segment_direction_max_angle;
            public bool manual_animator_upper_leg_to_lower_leg_segment_direction_disabled;
            public float manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle;
            public bool manual_animator_lower_leg_to_foot_segment_direction_disabled;
            public float manual_animator_lower_leg_to_foot_segment_direction_max_angle;
            public float manual_animator_left_lower_leg_to_foot_segment_direction_max_angle;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_max_angle;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight;
            public bool manual_animator_foot_to_toes_segment_direction_disabled;
            public float manual_animator_foot_to_toes_segment_direction_max_angle;
            public bool manual_animator_foot_hips_aligned_residual_yaw_enabled;
            public bool manual_animator_foot_hips_aligned_residual_yaw_disabled;
            public float manual_animator_foot_hips_aligned_residual_yaw_weight;
            public float manual_animator_foot_hips_aligned_residual_yaw_max_angle;
            public bool post_set_human_pose_right_endpoint_position_enabled;
            public float post_set_human_pose_right_endpoint_position_weight;
            public float post_set_human_pose_right_endpoint_position_max_offset;
            public float post_set_human_pose_right_endpoint_position_positive_z_scale;
            public float post_set_human_pose_right_endpoint_position_toes_blend_weight;
            public float post_set_human_pose_right_endpoint_position_frame_gate_start;
            public float post_set_human_pose_right_endpoint_position_frame_gate_end;
            public bool post_set_human_pose_endpoint_position_use_left_side;
            public bool pre_set_human_pose_right_endpoint_position_enabled;
            public float pre_set_human_pose_right_endpoint_position_weight;
            public float pre_set_human_pose_right_endpoint_position_max_offset;
            public float pre_set_human_pose_right_endpoint_position_positive_z_scale;
            public float pre_set_human_pose_right_endpoint_position_toes_blend_weight;
            public float pre_set_human_pose_right_endpoint_position_frame_gate_start;
            public float pre_set_human_pose_right_endpoint_position_frame_gate_end;
            public bool pre_set_human_pose_endpoint_position_use_left_side;
            public bool pre_set_human_pose_endpoint_position_use_ghost_current_basis;
            public bool pre_set_human_pose_endpoint_position_invert_body_position_x;
            public bool pre_set_human_pose_endpoint_position_invert_body_position_z;
            public bool post_set_human_pose_right_foot_evaluator_xz_reference_enabled;
            public float post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude;
            public bool manual_animator_biped_ik_foot_position_enabled;
            public float manual_animator_biped_ik_foot_position_weight;
            public float manual_animator_biped_ik_foot_position_max_offset;
            public bool manual_animator_hips_local_position_enabled;
            public float manual_animator_hips_local_position_weight;
            public float manual_animator_hips_local_position_max_offset;
            public bool manual_animator_body_position_xz_enabled;
            public float manual_animator_body_position_xz_weight;
            public float manual_animator_body_position_xz_max_offset;
            public float manual_animator_body_position_xz_frame_gate_start;
            public float manual_animator_body_position_xz_frame_gate_end;
            public float manual_animator_body_position_xz_frame_gate_blend_frames;
            public float manual_animator_body_position_xz_axis_x_scale;
            public float manual_animator_body_position_xz_axis_z_scale;
            public bool retarget_body_position_xz_root_motion_enabled;
            public bool target_humanoid_bone_position_lock_disabled;
            public bool vmd_playback_probe_enabled;
            public bool vmd_playback_probe_apply_ik_targets;
            public string vmd_playback_probe_source_vmd_path;
            public bool reference_mmd_timing_enabled;
            public int diagnostic_capture_width_override;
            public int diagnostic_capture_height_override;
            public float diagnostic_screenshot_padding_override;
            public float diagnostic_screenshot_vertical_viewport_center_override;
            public string reference_clip_name;
            public string reference_clip_asset_path;
            public CaptureResult[] results;
            public SummaryFrameRoleDiagnostics frame_count_roles;
            public SummarySampleOrderingDiagnostic[] sample_ordering_diagnostics;
            public SummaryCandidateArtifactSelection selected_candidate_artifact;
            public MotionComparisonFrameQualitySummary[] frame_quality_summaries;
            public string[] failures;
        }

        [Serializable]
        private sealed class SummaryCandidateArtifactSelection
        {
            public string selected_candidate_role;
            public string selected_candidate_output_role;
            public string selected_candidate_status;
            public string selected_candidate_status_reason;
            public string selected_candidate_metrics_csv;
            public string selected_candidate_vmd_path;
            public bool selected_candidate_preserves_raw_diagnostic;
            public string selected_candidate_manifest_path;
            public bool selected_candidate_vmd_exists;
            public bool selected_candidate_metrics_exists;
            public bool selected_candidate_manifest_exists;
            public bool selected_candidate_differs_from_raw_vmd;
            public bool selected_candidate_differs_from_raw_metrics;
            public bool selected_candidate_is_acceptance_artifact;
            public string selected_candidate_acceptance_basis;
            public string raw_candidate_status;
            public string raw_candidate_status_reason;
            public string raw_candidate_metrics_csv;
            public string raw_candidate_vmd_path;
            public string corrected_candidate_status;
            public string corrected_candidate_status_reason;
            public string corrected_candidate_metrics_csv;
            public string corrected_candidate_vmd_path;
            public string selection_basis;
        }

        [Serializable]
        private sealed class IntegratedVerticalSolvePrimaryExportManifest
        {
            public string raw_diagnostic_vmd_path = string.Empty;
        }

        [Serializable]
        private sealed class SummaryFrameRoleDiagnostics
        {
            public int reference_target_frame_count;
            public int baseline_recorded_frame_count;
            public int candidate_recorded_frame_count;
            public int baseline_frame_count_delta_from_reference_target;
            public int candidate_frame_count_delta_from_reference_target;
            public string target_frame_count_role;
            public string baseline_recorded_frame_count_role;
            public string candidate_recorded_frame_count_role;
            public string frame_quality_metric_basis;
            public string vmd_export_metric_basis;
            public string reference_mp4_provenance_evidence_path;
            public string reference_mp4_analysis_result_path;
            public string reference_mp4_frame_metrics_path;
            public string reference_mp4_contact_sheet_path;
            public bool reference_mp4_provenance_evidence_exists;
            public bool reference_mp4_analysis_result_exists;
            public bool reference_mp4_frame_metrics_exists;
            public bool reference_mp4_contact_sheet_exists;
            public string reference_mp4_canonical_context;
            public string reference_mp4_analysis_metric_basis;
            public string reference_mp4_analysis_schema;
            public string reference_mp4_frame_metrics_schema;
            public string reference_mp4_avg_frame_rate;
            public int reference_mp4_width;
            public int reference_mp4_height;
            public int reference_mp4_total_video_frames;
            public float reference_mp4_stream_duration_seconds;
            public int reference_mp4_extracted_frame_count;
            public int reference_mp4_frame_metrics_sample_count;
            public int reference_mp4_frame_metrics_extracted_frame_count;
            public float reference_mp4_avg_bbox_height_ratio;
            public float reference_mp4_avg_bbox_width_ratio;
            public float reference_mp4_center_x_range_ratio;
            public float reference_mp4_max_bottom_gap_ratio;
            public float reference_mp4_avg_bright_area_ratio;
            public float reference_mp4_current_clip_start_seconds;
            public float reference_mp4_current_clip_end_seconds;
            public float reference_mp4_current_clip_duration_seconds;
            public int reference_mp4_current_clip_sample_count;
            public float reference_mp4_current_clip_first_sample_seconds;
            public float reference_mp4_current_clip_last_sample_seconds;
            public float reference_mp4_current_clip_sample_coverage_ratio;
            public float reference_mp4_current_clip_sample_gap_seconds;
            public string reference_mp4_current_clip_sample_basis;
            public string reference_mp4_current_clip_framing_metric_basis;
            public float reference_mp4_current_clip_avg_bbox_height_ratio;
            public float reference_mp4_current_clip_avg_bbox_width_ratio;
            public float reference_mp4_current_clip_center_x_range_ratio;
            public float reference_mp4_current_clip_max_bottom_gap_ratio;
            public float reference_mp4_current_clip_avg_bright_area_ratio;
            public float reference_mp4_current_clip_avg_upper_limb_span_ratio;
            public float reference_mp4_current_clip_avg_lower_limb_span_ratio;
            public float[] reference_mp4_current_clip_sample_seconds;
            public string candidate_screenshot_frame_index_path;
            public bool candidate_screenshot_frame_index_exists;
            public string candidate_screenshot_frame_metrics_view;
            public string candidate_screenshot_frame_metrics_basis;
            public int candidate_screenshot_frame_metrics_sample_count;
            public int candidate_screenshot_nonblank_frame_count;
            public float candidate_screenshot_avg_bbox_height_ratio;
            public float candidate_screenshot_avg_bbox_width_ratio;
            public float candidate_screenshot_avg_upper_limb_span_ratio;
            public float candidate_screenshot_avg_lower_limb_span_ratio;
            public float candidate_screenshot_center_x_range_ratio;
            public float candidate_screenshot_max_bottom_gap_ratio;
            public float candidate_screenshot_max_top_gap_ratio;
            public float candidate_screenshot_avg_bright_area_ratio;
            public int candidate_screenshot_time_sample_count;
            public float[] candidate_screenshot_sample_seconds;
            public float candidate_screenshot_first_sample_seconds;
            public float candidate_screenshot_last_sample_seconds;
            public float candidate_screenshot_sample_coverage_ratio;
            public float candidate_screenshot_sample_gap_seconds;
            public int candidate_screenshot_ref_sample_gap_count;
            public float candidate_screenshot_max_ref_sample_seconds_gap;
            public float candidate_screenshot_avg_ref_sample_seconds_gap;
            public string candidate_screenshot_sample_timing_basis;
            public float candidate_vs_reference_avg_bbox_height_ratio_delta;
            public float candidate_vs_reference_avg_bbox_width_ratio_delta;
            public float candidate_vs_reference_center_x_range_ratio_delta;
            public float candidate_vs_reference_max_bottom_gap_ratio_delta;
            public float candidate_vs_reference_avg_bright_area_ratio_delta;
            public float candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta;
            public float candidate_vs_reference_current_clip_avg_bbox_width_ratio_delta;
            public float candidate_vs_reference_current_clip_center_x_range_ratio_delta;
            public float candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta;
            public float candidate_vs_reference_current_clip_avg_bright_area_ratio_delta;
            public int candidate_vs_reference_time_matched_sample_count;
            public float candidate_vs_reference_time_matched_max_seconds_gap;
            public float candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta;
            public int candidate_vs_reference_time_matched_limb_band_sample_count;
            public float candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta;
            public int candidate_vs_reference_time_matched_silhouette_profile_band_count;
            public int candidate_vs_reference_time_matched_silhouette_profile_sample_count;
            public float candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta;
            public float candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta;
            public float candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta;
            public int candidate_vs_reference_time_matched_silhouette_landmark_band_count;
            public int candidate_vs_reference_time_matched_silhouette_landmark_sample_count;
            public float candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta;
            public float candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta;
            public int candidate_vs_reference_time_matched_image_space_keypoint_count;
            public int candidate_vs_reference_time_matched_image_space_keypoint_sample_count;
            public float candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta;
            public float candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta;
            public int candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count;
            public int candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count;
            public float candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta;
            public float candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization;
            public float candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization;
            public int candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count;
            public int candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count;
            public float candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta;
            public float candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta;
            public string candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label;
            public int candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_index;
            public string candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds;
            public int candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_x;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_y;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_x;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_y;
            public bool candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_touches_frame_edge;
            public bool candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_touches_frame_edge;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_top_gap;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap;
            public float candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_top_gap;
            public int candidate_vs_reference_time_matched_crop_safe_sample_count;
            public float candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta;
            public float candidate_vs_reference_time_matched_crop_safe_max_bbox_width_ratio_abs_delta;
            public int candidate_vs_reference_time_matched_crop_safe_silhouette_profile_sample_count;
            public float candidate_vs_reference_time_matched_crop_safe_avg_silhouette_profile_l1_abs_delta;
            public float candidate_vs_reference_time_matched_crop_safe_max_silhouette_profile_l1_abs_delta;
            public int candidate_vs_reference_time_matched_crop_safe_image_space_keypoint_sample_count;
            public float candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta;
            public float candidate_vs_reference_time_matched_crop_safe_max_image_space_keypoint_l1_delta;
            public int candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count;
            public float candidate_vs_reference_time_matched_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta;
            public float candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta;
            public int candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count;
            public int candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count;
            public int candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count;
            public float candidate_vs_reference_time_matched_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta;
            public float candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta;
            public string candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label;
            public string candidate_vs_reference_time_matched_framing_metric_basis;
            public string candidate_vs_reference_time_matched_image_space_limb_span_basis;
            public string candidate_vs_reference_time_matched_image_space_limb_band_basis;
            public string candidate_vs_reference_time_matched_silhouette_profile_basis;
            public string candidate_vs_reference_time_matched_silhouette_landmark_basis;
            public string candidate_vs_reference_time_matched_image_space_keypoint_basis;
            public string candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_basis;
            public string candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis;
            public string candidate_vs_reference_time_matched_crop_safe_basis;
            public string candidate_vs_reference_time_matched_keypoint_local_crop_safe_basis;
            public string candidate_screenshot_frame_metrics_error;
            public string reference_mp4_analysis_error;
            public string reference_mp4_frame_metrics_error;
            [NonSerialized]
            public readonly List<ReferenceMp4FrameMetricRow> referenceMp4CurrentClipRows =
                new List<ReferenceMp4FrameMetricRow>();
        }

        private sealed class CandidateScreenshotFrameMetrics
        {
            public int SampleCount;
            public int NonblankCount;
            public float AvgBBoxHeightRatio = float.NaN;
            public float AvgBBoxWidthRatio = float.NaN;
            public float AvgUpperLimbSpanRatio = float.NaN;
            public float AvgLowerLimbSpanRatio = float.NaN;
            public float CenterXRangeRatio = float.NaN;
            public float MaxBottomGapRatio = float.NaN;
            public float MaxTopGapRatio = float.NaN;
            public float AvgBrightAreaRatio = float.NaN;
            public readonly List<int> RecorderFrames = new List<int>();
            public readonly List<CandidateScreenshotFrameSample> Samples =
                new List<CandidateScreenshotFrameSample>();
            public string Error = string.Empty;
        }

        private sealed class CandidateScreenshotFrameSample
        {
            public CandidateScreenshotFrameSample(int recorderFrame, CandidateScreenshotFrameMetric metric)
            {
                RecorderFrame = recorderFrame;
                Metric = metric;
                Seconds = float.NaN;
            }

            public int RecorderFrame;
            public CandidateScreenshotFrameMetric Metric;
            public float Seconds;
        }

        private sealed class CandidateScreenshotFrameMetric
        {
            public bool HasBrightPixels;
            public float BBoxHeightRatio;
            public float BBoxWidthRatio;
            public float UpperLimbSpanRatio = float.NaN;
            public float LowerLimbSpanRatio = float.NaN;
            public float[] SilhouetteSpanProfile = Array.Empty<float>();
            public float[] SilhouetteEndpointProfile = Array.Empty<float>();
            public float[] ImageSpaceKeypointProfile = Array.Empty<float>();
            public bool HasNonHairBrightPixels;
            public float NonHairBBoxHeightRatio = float.NaN;
            public float NonHairBBoxWidthRatio = float.NaN;
            public float NonHairCenterX = float.NaN;
            public float NonHairBottomGapRatio = float.NaN;
            public float NonHairTopGapRatio = float.NaN;
            public float[] NonHairImageSpaceKeypointProfile = Array.Empty<float>();
            public float CenterX = float.NaN;
            public float BottomGapRatio = 1f;
            public float TopGapRatio = 1f;
            public float BrightAreaRatio;
        }

        [Serializable]
        private sealed class ReferenceMp4AnalysisResult
        {
            public string schema = string.Empty;
            public int extractedFrameCount = 0;
            public ReferenceMp4Video video = null;
        }

        [Serializable]
        private sealed class ReferenceMp4Video
        {
            public int width = 0;
            public int height = 0;
            public string avg_frame_rate = string.Empty;
            public string stream_duration = string.Empty;
            public string nb_frames = string.Empty;
        }

        [Serializable]
        private sealed class ReferenceMp4FrameMetrics
        {
            public string schema = string.Empty;
            public int sampleCount = 0;
            public int extractedFrameCount = 0;
            public float avgBBoxHeightRatio = 0f;
            public float avgBBoxWidthRatio = 0f;
            public float centerXRangeRatio = 0f;
            public float maxBottomGapRatio = 0f;
            public float avgBrightAreaRatio = 0f;
            public ReferenceMp4FrameMetricRow[] rows = null;
        }

        [Serializable]
        private sealed class ReferenceMp4FrameMetricRow
        {
            public float seconds = float.NaN;
            public string framePath = string.Empty;
            public float bboxHeightRatio = 0f;
            public float bboxWidthRatio = 0f;
            public float centerXRatio = 0f;
            public float bottomGapRatio = 0f;
            public float brightAreaRatio = 0f;
            [NonSerialized]
            public float upperLimbSpanRatio = float.NaN;
            [NonSerialized]
            public float lowerLimbSpanRatio = float.NaN;
            [NonSerialized]
            public float[] silhouetteSpanProfile = Array.Empty<float>();
            [NonSerialized]
            public float[] silhouetteEndpointProfile = Array.Empty<float>();
            [NonSerialized]
            public float[] imageSpaceKeypointProfile = Array.Empty<float>();
            [NonSerialized]
            public bool hasNonHairBrightPixels;
            [NonSerialized]
            public float nonHairBBoxHeightRatio = float.NaN;
            [NonSerialized]
            public float nonHairBBoxWidthRatio = float.NaN;
            [NonSerialized]
            public float nonHairCenterXRatio = float.NaN;
            [NonSerialized]
            public float nonHairBottomGapRatio = float.NaN;
            [NonSerialized]
            public float[] nonHairImageSpaceKeypointProfile = Array.Empty<float>();
        }

        [Serializable]
        private sealed class SummarySampleOrderingDiagnostic
        {
            public string job_mode;
            public string scene_name;
            public string metrics_csv;
            public int metric_row_count;
            public string first_metric_reason;
            public int first_metric_recorder_frame;
            public int first_metric_engine_frame_count;
            public float first_metric_time_since_level_load;
            public float first_metric_animation_clip_time;
            public float first_metric_grounding_vertical_step_last;
            public float first_metric_grounding_initial_vertical_step;
            public int first_metric_grounding_step_clamp_count;
            public int first_metric_grounding_smoothed_count;
            public float first_metric_grounding_max_step_per_frame;
            public float first_metric_grounding_vertical_step_to_max_ratio;
            public bool first_metric_grounding_vertical_step_at_max_step;
            public string finish_metric_reason;
            public int finish_metric_recorder_frame;
            public int finish_metric_engine_frame_count;
            public float finish_metric_time_since_level_load;
            public float finish_metric_animation_clip_time;
            public float finish_metric_grounding_vertical_step_last;
            public int finish_metric_grounding_step_clamp_count;
            public int finish_metric_grounding_smoothed_count;
            public float finish_metric_grounding_max_step_per_frame;
            public float finish_metric_grounding_vertical_step_to_max_ratio;
            public bool finish_metric_grounding_vertical_step_at_max_step;
            public int recording_metric_recorder_frame_span;
            public int recording_metric_engine_frame_span;
            public float recording_metric_time_since_level_load_span;
            public int recording_grounding_step_clamp_delta;
            public int recording_grounding_smoothed_delta;
            public string recording_phase_span_role;
            public string grounding_step_limit_role;
        }
    }
}
#endif
