
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

namespace Fbx2Vmd.FBXImporter
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
        private const string OutputRootDirectory = "Docs/Workflow/Local/ComparisonSessions";
        private const string MmdAutomationRunsRelativePath = "Docs/Workflow/Local/MMDQASessions/automation_runs";
        private const string LatestSummaryJsonRelativePath = "Docs/Workflow/Local/progress/evidence/yyb_visual_compare_latest.json";
        private const string LatestSummaryMarkdownRelativePath = "Docs/Workflow/Local/progress/evidence/yyb_visual_compare_latest.md";
        private const string SummaryJsonFileName = "yyb_visual_compare_summary.json";
        private const string SummaryMarkdownFileName = "yyb_visual_compare_summary.md";
        private const string RunnerTraceRelativePath = "Docs/Workflow/Local/runtime/yyb_visual_compare_runner_trace.log";
        private const string ReferenceMp4ProvenanceEvidenceRelativePath = "Docs/Workflow/Local/ReferenceAnalysis/main-recoding-a2-reference-mp4-provenance-evidence-20260609.md";
        private const string ReferenceMp4AnalysisResultRelativePath = "Docs/Workflow/Local/ReferenceAnalysis/when-20260608-204850_where-ref-mp4_who-yyb_what-detailed-mp4-analysis_why-main-recoding-problem-list_how-ffmpeg-24-samples/result.json";
        private const string ReferenceMp4FrameMetricsRelativePath = "Docs/Workflow/Local/ReferenceAnalysis/when-20260608-204850_where-ref-mp4_who-yyb_what-detailed-mp4-analysis_why-main-recoding-problem-list_how-ffmpeg-24-samples/frame-metrics.json";
        private const string ReferenceMp4ContactSheetRelativePath = "Docs/Workflow/Local/ReferenceAnalysis/when-20260608-204850_where-ref-mp4_who-yyb_what-detailed-mp4-analysis_why-main-recoding-problem-list_how-ffmpeg-24-samples/contact-sheet.png";
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
        private const float ReferenceAlignedVisualEvidenceMaxBBoxNormalizedImageSpaceKeypointL1Delta = 0.30f;
        internal const float ReferenceAlignedVisualEvidenceEndpointPixelTolerance = 0.001f;
        private const int EvidenceSafeMaxFullPathLength = 240;
        private const float DefaultManualAnimatorBodyRotationReferenceWeight = 1f;
        private const float DefaultManualAnimatorFullBodyPoseReferenceWeight = 1f;
        private const float DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart = 0f;
        private const float DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd = 0f;
        private const float DefaultSetHumanPoseRightLegTwistOutputReferenceWeight = 1f;
        private const float DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta = 0.02f;
        private const float DefaultManualAnimatorHandPalmFrameWeight = 1f;
        private const float DefaultRetargetPoseVisualSpikeCurrentWeight = 0.65f;
        private const float DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset = 0f;
        private const float DefaultRetargetArmStretchMuscleLimit = 0.5f;
        private const float DefaultManualAnimatorHipsLocalPositionReferenceWeight = 0.25f;
        private const float DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset = 0.04f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceWeight = 0.45f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceMaxOffset = 0.025f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart = 0f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd = 0f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = 0f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceAxisXScale = 1f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceAxisZScale = 1f;
        private const float DefaultYybRightSleeveSilhouetteLocalOffsetX = 0f;
        private const float DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateStart = 0f;
        private const float DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateEnd = 0f;
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
            "Assets/_Project/Scripts/FBXImporter/FBXVmdPipeline.cs",
            "Assets/_Project/Scripts/FBXImporter/HumanoidArmDeformationGuard.cs",
            "Assets/_Project/Scripts/FBXImporter/HumanoidArmDirectionRetargetGuard.cs",
            "Assets/_Project/Scripts/FBXImporter/PoseSpaceRetargeter.cs",
            "Assets/_Project/Scripts/FBXImporter/Editor/YybVisualComparisonBatchRunner.cs",
            "Assets/_Project/Scripts/FBXImporter/Editor/YybVisualComparisonRequestWatcher.cs"
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
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = 0.125f;
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
        private const string RunnerStateSessionKey = "Fbx2Vmd.YybVisualComparison.RunnerStateJson";
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

        [Serializable]
        private sealed class CaptureResult : YybVisualComparisonCaptureResultData
        {
        }

        [Serializable]
        private sealed class PersistedCaptureJob : VisualComparisonCaptureJobStateData
        {
        }

        [Serializable]
        private sealed class PersistedCaptureResult : YybVisualComparisonCaptureResultData
        {
        }

        [Serializable]
        private sealed class PersistedState : YybVisualComparisonRunStateData
        {
        }

        private static readonly Queue<CaptureJob> PendingJobs = new Queue<CaptureJob>();
        private static readonly List<CaptureResult> Results = new List<CaptureResult>();
        private static readonly List<string> Failures = new List<string>();

        private static CaptureJob _activeJob;
        private static YybVisualComparisonRunOptions _currentRunOptions;
        private static FBXVmdPipeline _activeFBXVmdPipeline;
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
        private static bool _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride;
        private static bool _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride;
        private static bool _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride;
        private static float _manualAnimatorFullBodyPoseReferenceFrameGateStart =
            DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart;
        private static float _manualAnimatorFullBodyPoseReferenceFrameGateEnd =
            DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd;
        private static bool _enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride;
        private static float _setHumanPoseRightLegTwistOutputReferenceWeight =
            DefaultSetHumanPoseRightLegTwistOutputReferenceWeight;
        private static float _setHumanPoseRightLegTwistOutputReferenceMaxDelta =
            DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta;
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
        private static bool _enableRetargetArmStretchClampRuntimeOverride;
        private static float _retargetArmStretchMuscleLimit = DefaultRetargetArmStretchMuscleLimit;
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
        private static bool _enableYybRightSleeveSilhouetteOffsetRuntimeOverride;
        private static float _yybRightSleeveSilhouetteLocalOffsetX =
            DefaultYybRightSleeveSilhouetteLocalOffsetX;
        private static float _yybRightSleeveSilhouetteLocalOffsetFrameGateStart =
            DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateStart;
        private static float _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd =
            DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateEnd;
        private static bool _enableVmdPlaybackProbeRuntimeOverride;
        private static bool _applyVmdPlaybackProbeIkTargetsRuntimeOverride;
        private static string _vmdPlaybackProbeSourceVmdPath = string.Empty;
        private static bool _enableReferenceMmdTimingRuntimeOverride;
        private static FBXVmdPipeline.EditorDiagnosticSmokeSegment _editorDiagnosticSmokeSegment =
            FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head;
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
        private static readonly VisualComparisonEnterPlayModeOptionsController EnterPlayModeOptionsController =
            new VisualComparisonEnterPlayModeOptionsController();

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
            string json = VisualComparisonRunStateStore.ReadJson(RunnerStateSessionKey);
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
            RunWithOptions(
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
            _activeFBXVmdPipeline = null;
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
            _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride = false;
            _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride = false;
            _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride = false;
            _manualAnimatorFullBodyPoseReferenceFrameGateStart =
                DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart;
            _manualAnimatorFullBodyPoseReferenceFrameGateEnd =
                DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd;
            _enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride = false;
            _setHumanPoseRightLegTwistOutputReferenceWeight =
                DefaultSetHumanPoseRightLegTwistOutputReferenceWeight;
            _setHumanPoseRightLegTwistOutputReferenceMaxDelta =
                DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta;
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
            _enableRetargetArmStretchClampRuntimeOverride = false;
            _retargetArmStretchMuscleLimit = DefaultRetargetArmStretchMuscleLimit;
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
            _editorDiagnosticSmokeSegment = FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head;
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
            _enableYybRightSleeveSilhouetteOffsetRuntimeOverride = false;
            _yybRightSleeveSilhouetteLocalOffsetX =
                DefaultYybRightSleeveSilhouetteLocalOffsetX;
            _yybRightSleeveSilhouetteLocalOffsetFrameGateStart =
                DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateStart;
            _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd =
                DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateEnd;
            _isRunning = false;
            ClearPersistedState();
            AppendRunnerTrace($"stale run state cleared reason={reason}");
        }

        public static void RunBatch()
        {
            YybVisualComparisonRunOptions options = CreateDefaultRunOptions();
            YybVisualComparisonCommandLineOptionsReader.Apply(
                Environment.GetCommandLineArgs(),
                options);
            StartRun(options);
        }

        private static YybVisualComparisonRunOptions CreateDefaultRunOptions()
        {
            return new YybVisualComparisonRunOptions
            {
                fbxFileName = DefaultFbxFileName,
                durationSeconds = DefaultDurationSeconds,
                enableFingerCloseups = false,
                enableRecorderParentFrameIkOffsetsWhenCenterParented = true,
                mmdIkDeltaGuardLimitOverrideVmd = NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd = NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd = NoMmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryHoldFrames = NoMmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride = false,
                enableManualAnimatorFootLocalRotationRuntimeOverride = false,
                disableManualAnimatorFootLocalRotationRuntimeOverride = false,
                enableManualAnimatorFullBodyPoseRuntimeOverride = false,
                disableManualAnimatorFullBodyPoseRuntimeOverride = false,
                manualAnimatorFullBodyPoseReferenceWeight = DefaultManualAnimatorFullBodyPoseReferenceWeight,
                manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride = false,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride = false,
                manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride = false,
                manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride = false,
                manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride = false,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride = false,
                manualAnimatorFullBodyPoseReferenceFrameGateStart = DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart,
                manualAnimatorFullBodyPoseReferenceFrameGateEnd = DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd,
                enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride = false,
                setHumanPoseRightLegTwistOutputReferenceWeight = DefaultSetHumanPoseRightLegTwistOutputReferenceWeight,
                setHumanPoseRightLegTwistOutputReferenceMaxDelta = DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta,
                enableManualAnimatorBodyRotationRuntimeOverride = false,
                disableManualAnimatorBodyRotationRuntimeOverride = false,
                manualAnimatorBodyRotationReferenceWeight = DefaultManualAnimatorBodyRotationReferenceWeight,
                enableManualAnimatorHandLocalRotationRuntimeOverride = false,
                enableManualAnimatorThumbLocalRotationRuntimeOverride = false,
                enableManualAnimatorHandPalmFrameRuntimeOverride = false,
                manualAnimatorHandPalmFrameWeight = DefaultManualAnimatorHandPalmFrameWeight,
                overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings = false,
                enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = true,
                retargetPoseVisualSpikeCurrentWeight = DefaultRetargetPoseVisualSpikeCurrentWeight,
                retargetPoseVisualSpikeForearmStretchClampMaxOffset = DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset,
                enableRetargetArmStretchClampRuntimeOverride = false,
                retargetArmStretchMuscleLimit = DefaultRetargetArmStretchMuscleLimit,
                enableYybArmSwingLimitRuntimeOverride = false,
                yybArmSwingLimitWeight = DefaultYybArmSwingLimitWeight,
                yybArmSwingMaxDownDot = DefaultYybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio = DefaultYybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio = DefaultYybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight = DefaultYybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio = DefaultYybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply = DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight = DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot = DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmDirectionRetargetRuntimeOverride = false,
                yybArmDirectionUpperArmWeight = DefaultYybArmDirectionUpperArmWeight,
                yybArmDirectionForearmWeight = DefaultYybArmDirectionForearmWeight,
                yybArmDirectionUpperArmMaxDegrees = DefaultYybArmDirectionUpperArmMaxDegrees,
                yybArmDirectionForearmMaxDegrees = DefaultYybArmDirectionForearmMaxDegrees,
                yybArmDirectionLeftSideWeightScale = DefaultYybArmDirectionLeftSideWeightScale,
                yybArmDirectionRightSideWeightScale = DefaultYybArmDirectionRightSideWeightScale,
                overrideYybArmSleeveAnchorRuntimeSettings = false,
                enableYybArmSleeveAnchorRuntimeOverride = true,
                yybArmSleeveAnchorInfluence = DefaultYybArmSleeveAnchorInfluence,
                yybArmShoulderCapAnchorInfluence = DefaultYybArmShoulderCapAnchorInfluence,
                yybArmSleeveAnchorMaxDegrees = DefaultYybArmSleeveAnchorMaxDegrees,
                overrideYybArmVisualTwistRuntimeSettings = false,
                enableYybArmVisualTwistRuntimeOverride = true,
                yybArmVisualUpperArmInfluence = DefaultYybArmVisualUpperArmInfluence,
                yybArmVisualForearmInfluence = DefaultYybArmVisualForearmInfluence,
                yybArmVisualUpperArmMaxDegrees = DefaultYybArmVisualUpperArmMaxDegrees,
                yybArmVisualForearmMaxDegrees = DefaultYybArmVisualForearmMaxDegrees,
                enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false,
                disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = false,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight = DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride = false,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride = false,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride = false,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false,
                disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = false,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight = DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                enablePostSetHumanPoseRightEndpointPositionRuntimeOverride = false,
                postSetHumanPoseRightEndpointPositionReferenceWeight = DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset = DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale = DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = false,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride = false,
                preSetHumanPoseRightEndpointPositionReferenceWeight = DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset = DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale = DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = false,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis = false,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX = false,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = false,
                usePostSetHumanPoseRightFootEvaluatorXzReference = false,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                enableManualAnimatorBipedIkFootPositionRuntimeOverride = false,
                manualAnimatorBipedIkFootPositionReferenceWeight = DefaultManualAnimatorBipedIkFootPositionReferenceWeight,
                manualAnimatorBipedIkFootPositionReferenceMaxOffset = DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset,
                enableManualAnimatorHipsLocalPositionRuntimeOverride = false,
                manualAnimatorHipsLocalPositionReferenceWeight = DefaultManualAnimatorHipsLocalPositionReferenceWeight,
                manualAnimatorHipsLocalPositionReferenceMaxOffset = DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset,
                enableManualAnimatorBodyPositionXzRuntimeOverride = false,
                manualAnimatorBodyPositionXzReferenceWeight = DefaultManualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset = DefaultManualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart = DefaultManualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd = DefaultManualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale = DefaultManualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale = DefaultManualAnimatorBodyPositionXzReferenceAxisZScale,
                enableYybRightSleeveSilhouetteOffsetRuntimeOverride = false,
                yybRightSleeveSilhouetteLocalOffsetX = DefaultYybRightSleeveSilhouetteLocalOffsetX,
                yybRightSleeveSilhouetteLocalOffsetFrameGateStart = DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateStart,
                yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateEnd,
                disableTargetHumanoidBonePositionLockRuntimeOverride = false,
                enableRetargetBodyPositionXzRootMotionRuntimeOverride = false,
                enableVmdPlaybackProbeRuntimeOverride = false,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride = false,
                editorDiagnosticSmokeSegment = "head",
                enableReferenceMmdTimingRuntimeOverride = false,
                diagnosticCaptureWidthOverride = NoDiagnosticCaptureDimensionOverride,
                diagnosticCaptureHeightOverride = NoDiagnosticCaptureDimensionOverride,
                diagnosticScreenshotPaddingOverride = NoDiagnosticScreenshotFramingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride = NoDiagnosticScreenshotFramingOverride
            };
        }

        public static void RunWithOptions(string fbxFileName, float durationSeconds, bool enableFingerCloseups)
        {
            RunWithOptions(
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
            RunWithOptions(
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
            RunWithOptions(
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
            RunWithOptions(
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
            RunWithOptions(
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
            bool manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride = false,
            bool manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride = false,
            bool manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride = false,
            float manualAnimatorFullBodyPoseReferenceFrameGateStart =
                DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart,
            float manualAnimatorFullBodyPoseReferenceFrameGateEnd =
                DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd,
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
            bool enableRetargetArmStretchClampRuntimeOverride = false,
            float retargetArmStretchMuscleLimit = DefaultRetargetArmStretchMuscleLimit,
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
            bool ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = false,
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
            bool ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = false,
            bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis = false,
            bool ShouldInvertPreSetHumanPoseEndpointPositionBodyX = false,
            bool ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = false,
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
            bool enableYybRightSleeveSilhouetteOffsetRuntimeOverride = false,
            float yybRightSleeveSilhouetteLocalOffsetX =
                DefaultYybRightSleeveSilhouetteLocalOffsetX,
            float yybRightSleeveSilhouetteLocalOffsetFrameGateStart =
                DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateStart,
            float yybRightSleeveSilhouetteLocalOffsetFrameGateEnd =
                DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateEnd,
            bool enableRetargetBodyPositionXzRootMotionRuntimeOverride = false,
            bool disableTargetHumanoidBonePositionLockRuntimeOverride = false,
            bool enableVmdPlaybackProbeRuntimeOverride = false,
            bool applyVmdPlaybackProbeIkTargetsRuntimeOverride = false,
            string editorDiagnosticSmokeSegmentName = "head",
            bool enableReferenceMmdTimingRuntimeOverride = false,
            int diagnosticCaptureWidthOverride = NoDiagnosticCaptureDimensionOverride,
            int diagnosticCaptureHeightOverride = NoDiagnosticCaptureDimensionOverride,
            float diagnosticScreenshotPaddingOverride = NoDiagnosticScreenshotFramingOverride,
            float diagnosticScreenshotVerticalViewportCenterOverride = NoDiagnosticScreenshotFramingOverride,
            bool enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride = false,
            float setHumanPoseRightLegTwistOutputReferenceWeight =
                DefaultSetHumanPoseRightLegTwistOutputReferenceWeight,
            float setHumanPoseRightLegTwistOutputReferenceMaxDelta =
                DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta)
        {
            StartRun(new YybVisualComparisonRunOptions
            {
                fbxFileName = fbxFileName,
                durationSeconds = durationSeconds,
                enableFingerCloseups = enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented = enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmdIkDeltaGuardLimitOverrideVmd = mmdIkDeltaGuardLimitOverrideVmd,
                mmdIkDeltaGuardRecoveryTriggerVmd = mmdIkDeltaGuardRecoveryTriggerVmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd = mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                mmdIkDeltaGuardRecoveryHoldFrames = mmdIkDeltaGuardRecoveryHoldFrames,
                enableFinalIkFootGroundingRuntimeOverride = enableFinalIkFootGroundingRuntimeOverride,
                enableManualAnimatorFootLocalRotationRuntimeOverride = enableManualAnimatorFootLocalRotationRuntimeOverride,
                disableManualAnimatorFootLocalRotationRuntimeOverride = disableManualAnimatorFootLocalRotationRuntimeOverride,
                enableManualAnimatorFullBodyPoseRuntimeOverride = enableManualAnimatorFullBodyPoseRuntimeOverride,
                disableManualAnimatorFullBodyPoseRuntimeOverride = disableManualAnimatorFullBodyPoseRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceWeight = manualAnimatorFullBodyPoseReferenceWeight,
                manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride = manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride = manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride = manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride = manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride = manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride = manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceFrameGateStart = manualAnimatorFullBodyPoseReferenceFrameGateStart,
                manualAnimatorFullBodyPoseReferenceFrameGateEnd = manualAnimatorFullBodyPoseReferenceFrameGateEnd,
                enableManualAnimatorBodyRotationRuntimeOverride = enableManualAnimatorBodyRotationRuntimeOverride,
                disableManualAnimatorBodyRotationRuntimeOverride = disableManualAnimatorBodyRotationRuntimeOverride,
                manualAnimatorBodyRotationReferenceWeight = manualAnimatorBodyRotationReferenceWeight,
                enableManualAnimatorHandLocalRotationRuntimeOverride = enableManualAnimatorHandLocalRotationRuntimeOverride,
                enableManualAnimatorThumbLocalRotationRuntimeOverride = enableManualAnimatorThumbLocalRotationRuntimeOverride,
                enableManualAnimatorHandPalmFrameRuntimeOverride = enableManualAnimatorHandPalmFrameRuntimeOverride,
                manualAnimatorHandPalmFrameWeight = manualAnimatorHandPalmFrameWeight,
                overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings = overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings,
                enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                retargetPoseVisualSpikeCurrentWeight = retargetPoseVisualSpikeCurrentWeight,
                retargetPoseVisualSpikeForearmStretchClampMaxOffset = retargetPoseVisualSpikeForearmStretchClampMaxOffset,
                enableRetargetArmStretchClampRuntimeOverride = enableRetargetArmStretchClampRuntimeOverride,
                retargetArmStretchMuscleLimit = retargetArmStretchMuscleLimit,
                enableYybArmSwingLimitRuntimeOverride = enableYybArmSwingLimitRuntimeOverride,
                yybArmSwingLimitWeight = yybArmSwingLimitWeight,
                yybArmSwingMaxDownDot = yybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio = yybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio = yybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight = yybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio = yybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply = yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight = yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot = yybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmDirectionRetargetRuntimeOverride = enableYybArmDirectionRetargetRuntimeOverride,
                yybArmDirectionUpperArmWeight = yybArmDirectionUpperArmWeight,
                yybArmDirectionForearmWeight = yybArmDirectionForearmWeight,
                yybArmDirectionUpperArmMaxDegrees = yybArmDirectionUpperArmMaxDegrees,
                yybArmDirectionForearmMaxDegrees = yybArmDirectionForearmMaxDegrees,
                yybArmDirectionLeftSideWeightScale = yybArmDirectionLeftSideWeightScale,
                yybArmDirectionRightSideWeightScale = yybArmDirectionRightSideWeightScale,
                overrideYybArmSleeveAnchorRuntimeSettings = overrideYybArmSleeveAnchorRuntimeSettings,
                enableYybArmSleeveAnchorRuntimeOverride = enableYybArmSleeveAnchorRuntimeOverride,
                yybArmSleeveAnchorInfluence = yybArmSleeveAnchorInfluence,
                yybArmShoulderCapAnchorInfluence = yybArmShoulderCapAnchorInfluence,
                yybArmSleeveAnchorMaxDegrees = yybArmSleeveAnchorMaxDegrees,
                overrideYybArmVisualTwistRuntimeSettings = overrideYybArmVisualTwistRuntimeSettings,
                enableYybArmVisualTwistRuntimeOverride = enableYybArmVisualTwistRuntimeOverride,
                yybArmVisualUpperArmInfluence = yybArmVisualUpperArmInfluence,
                yybArmVisualForearmInfluence = yybArmVisualForearmInfluence,
                yybArmVisualUpperArmMaxDegrees = yybArmVisualUpperArmMaxDegrees,
                yybArmVisualForearmMaxDegrees = yybArmVisualForearmMaxDegrees,
                enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight = manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride = disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride = disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride = disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight = manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                enablePostSetHumanPoseRightEndpointPositionRuntimeOverride = enablePostSetHumanPoseRightEndpointPositionRuntimeOverride,
                postSetHumanPoseRightEndpointPositionReferenceWeight = postSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset = postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale = postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = ShouldUseLeftSideForPostSetHumanPoseEndpointPosition,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride = enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                preSetHumanPoseRightEndpointPositionReferenceWeight = preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset = preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale = preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = ShouldUseLeftSideForPreSetHumanPoseEndpointPosition,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis = preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX = ShouldInvertPreSetHumanPoseEndpointPositionBodyX,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = ShouldInvertPreSetHumanPoseEndpointPositionBodyZ,
                usePostSetHumanPoseRightFootEvaluatorXzReference = usePostSetHumanPoseRightFootEvaluatorXzReference,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                enableManualAnimatorBipedIkFootPositionRuntimeOverride = enableManualAnimatorBipedIkFootPositionRuntimeOverride,
                manualAnimatorBipedIkFootPositionReferenceWeight = manualAnimatorBipedIkFootPositionReferenceWeight,
                manualAnimatorBipedIkFootPositionReferenceMaxOffset = manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                enableManualAnimatorHipsLocalPositionRuntimeOverride = enableManualAnimatorHipsLocalPositionRuntimeOverride,
                manualAnimatorHipsLocalPositionReferenceWeight = manualAnimatorHipsLocalPositionReferenceWeight,
                manualAnimatorHipsLocalPositionReferenceMaxOffset = manualAnimatorHipsLocalPositionReferenceMaxOffset,
                enableManualAnimatorBodyPositionXzRuntimeOverride = enableManualAnimatorBodyPositionXzRuntimeOverride,
                manualAnimatorBodyPositionXzReferenceWeight = manualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset = manualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart = manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd = manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale = manualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale = manualAnimatorBodyPositionXzReferenceAxisZScale,
                enableYybRightSleeveSilhouetteOffsetRuntimeOverride = enableYybRightSleeveSilhouetteOffsetRuntimeOverride,
                yybRightSleeveSilhouetteLocalOffsetX = yybRightSleeveSilhouetteLocalOffsetX,
                yybRightSleeveSilhouetteLocalOffsetFrameGateStart = yybRightSleeveSilhouetteLocalOffsetFrameGateStart,
                yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = yybRightSleeveSilhouetteLocalOffsetFrameGateEnd,
                enableRetargetBodyPositionXzRootMotionRuntimeOverride = enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                disableTargetHumanoidBonePositionLockRuntimeOverride = disableTargetHumanoidBonePositionLockRuntimeOverride,
                enableVmdPlaybackProbeRuntimeOverride = enableVmdPlaybackProbeRuntimeOverride,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride = applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                editorDiagnosticSmokeSegment = editorDiagnosticSmokeSegmentName,
                enableReferenceMmdTimingRuntimeOverride = enableReferenceMmdTimingRuntimeOverride,
                diagnosticCaptureWidthOverride = diagnosticCaptureWidthOverride,
                diagnosticCaptureHeightOverride = diagnosticCaptureHeightOverride,
                diagnosticScreenshotPaddingOverride = diagnosticScreenshotPaddingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride = diagnosticScreenshotVerticalViewportCenterOverride,
                enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride = enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride,
                setHumanPoseRightLegTwistOutputReferenceWeight = setHumanPoseRightLegTwistOutputReferenceWeight,
                setHumanPoseRightLegTwistOutputReferenceMaxDelta = setHumanPoseRightLegTwistOutputReferenceMaxDelta
            });
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
            RunWithOptions(
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

        private static void StartRun(YybVisualComparisonRunOptions runOptions)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[YybVisualComparisonBatchRunner] 이미 실행 중입니다.");
                return;
            }

            HumanoidSampleCode.SetEditorAutoStartSuppressed(true);
            ApplyTemporaryEnterPlayModeOptions();

            _projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            YybVisualComparisonRunOptionsNormalizer.Normalize(
                runOptions,
                CreateDefaultRunOptions(),
                DefaultFrameRate);
            ApplyRunOptions(runOptions);
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
            _activeFBXVmdPipeline = null;
            _activeRecorder = null;
            _activeJobFinished = false;
            _activeJobStartedInPlayMode = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;

            foreach (CaptureJob job in BuildCaptureJobs(_enableVmdPlaybackProbeRuntimeOverride))
            {
                PendingJobs.Enqueue(job);
            }

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
                Debug.Log("[YybVisualComparisonBatchRunner] 런타임 진단 스크립트 새로고침 대기 중.");
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

        private static void ApplyRunOptions(YybVisualComparisonRunOptions options)
        {
            _currentRunOptions = options;
            _fbxFileName = options.fbxFileName;
            _durationSeconds = options.durationSeconds;
            _targetFrameCount = options.targetFrameCount;
            _enableFingerCloseups = options.enableFingerCloseups;
            _enableRecorderParentFrameIkOffsetsWhenCenterParented = options.enableRecorderParentFrameIkOffsetsWhenCenterParented;
            _mmdIkDeltaGuardLimitOverrideVmd = options.mmdIkDeltaGuardLimitOverrideVmd;
            _mmdIkDeltaGuardRecoveryTriggerVmd = options.mmdIkDeltaGuardRecoveryTriggerVmd;
            _mmdIkDeltaGuardRecoveryDebtThresholdVmd = options.mmdIkDeltaGuardRecoveryDebtThresholdVmd;
            _mmdIkDeltaGuardRecoveryHoldFrames = options.mmdIkDeltaGuardRecoveryHoldFrames;
            _enableFinalIkFootGroundingRuntimeOverride = options.enableFinalIkFootGroundingRuntimeOverride;
            _enableManualAnimatorFootLocalRotationRuntimeOverride = options.enableManualAnimatorFootLocalRotationRuntimeOverride;
            _disableManualAnimatorFootLocalRotationRuntimeOverride = options.disableManualAnimatorFootLocalRotationRuntimeOverride;
            _enableManualAnimatorFullBodyPoseRuntimeOverride = options.enableManualAnimatorFullBodyPoseRuntimeOverride;
            _disableManualAnimatorFullBodyPoseRuntimeOverride = options.disableManualAnimatorFullBodyPoseRuntimeOverride;
            _manualAnimatorFullBodyPoseReferenceWeight = options.manualAnimatorFullBodyPoseReferenceWeight;
            _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride = options.manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride;
            _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride = options.manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride = options.manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride = options.manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride = options.manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride = options.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseReferenceFrameGateStart = options.manualAnimatorFullBodyPoseReferenceFrameGateStart;
            _manualAnimatorFullBodyPoseReferenceFrameGateEnd = options.manualAnimatorFullBodyPoseReferenceFrameGateEnd;
            _enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride = options.enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride;
            _setHumanPoseRightLegTwistOutputReferenceWeight = options.setHumanPoseRightLegTwistOutputReferenceWeight;
            _setHumanPoseRightLegTwistOutputReferenceMaxDelta = options.setHumanPoseRightLegTwistOutputReferenceMaxDelta;
            _enableManualAnimatorBodyRotationRuntimeOverride = options.enableManualAnimatorBodyRotationRuntimeOverride;
            _disableManualAnimatorBodyRotationRuntimeOverride = options.disableManualAnimatorBodyRotationRuntimeOverride;
            _manualAnimatorBodyRotationReferenceWeight = options.manualAnimatorBodyRotationReferenceWeight;
            _enableManualAnimatorHandLocalRotationRuntimeOverride = options.enableManualAnimatorHandLocalRotationRuntimeOverride;
            _enableManualAnimatorThumbLocalRotationRuntimeOverride = options.enableManualAnimatorThumbLocalRotationRuntimeOverride;
            _enableManualAnimatorHandPalmFrameRuntimeOverride = options.enableManualAnimatorHandPalmFrameRuntimeOverride;
            _manualAnimatorHandPalmFrameWeight = options.manualAnimatorHandPalmFrameWeight;
            _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings = options.overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings;
            _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = options.enableRetargetPoseVisualSpikeSmoothingRuntimeOverride;
            _retargetPoseVisualSpikeCurrentWeight = options.retargetPoseVisualSpikeCurrentWeight;
            _retargetPoseVisualSpikeForearmStretchClampMaxOffset = options.retargetPoseVisualSpikeForearmStretchClampMaxOffset;
            _enableRetargetArmStretchClampRuntimeOverride = options.enableRetargetArmStretchClampRuntimeOverride;
            _retargetArmStretchMuscleLimit = options.retargetArmStretchMuscleLimit;
            _enableYybArmSwingLimitRuntimeOverride = options.enableYybArmSwingLimitRuntimeOverride;
            _yybArmSwingLimitWeight = options.yybArmSwingLimitWeight;
            _yybArmSwingMaxDownDot = options.yybArmSwingMaxDownDot;
            _yybArmSwingMinHandHorizontalRatio = options.yybArmSwingMinHandHorizontalRatio;
            _yybArmSwingMaxHandBelowShoulderRatio = options.yybArmSwingMaxHandBelowShoulderRatio;
            _yybArmSwingHorizontalReachLimitWeight = options.yybArmSwingHorizontalReachLimitWeight;
            _yybArmSwingMaxHandHorizontalReachRatio = options.yybArmSwingMaxHandHorizontalReachRatio;
            _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = options.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            _yybArmSwingHorizontalReachMinElbowAngleAfterApply = options.yybArmSwingHorizontalReachMinElbowAngleAfterApply;
            _yybArmSwingRaisedPoseHorizontalReachLimitWeight = options.yybArmSwingRaisedPoseHorizontalReachLimitWeight;
            _yybArmSwingRaisedPoseMinUpperArmDownDot = options.yybArmSwingRaisedPoseMinUpperArmDownDot;
            _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = options.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = options.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            _enableYybArmDirectionRetargetRuntimeOverride = options.enableYybArmDirectionRetargetRuntimeOverride;
            _yybArmDirectionUpperArmWeight = options.yybArmDirectionUpperArmWeight;
            _yybArmDirectionForearmWeight = options.yybArmDirectionForearmWeight;
            _yybArmDirectionUpperArmMaxDegrees = options.yybArmDirectionUpperArmMaxDegrees;
            _yybArmDirectionForearmMaxDegrees = options.yybArmDirectionForearmMaxDegrees;
            _yybArmDirectionLeftSideWeightScale = options.yybArmDirectionLeftSideWeightScale;
            _yybArmDirectionRightSideWeightScale = options.yybArmDirectionRightSideWeightScale;
            _overrideYybArmSleeveAnchorRuntimeSettings = options.overrideYybArmSleeveAnchorRuntimeSettings;
            _enableYybArmSleeveAnchorRuntimeOverride = options.enableYybArmSleeveAnchorRuntimeOverride;
            _yybArmSleeveAnchorInfluence = options.yybArmSleeveAnchorInfluence;
            _yybArmShoulderCapAnchorInfluence = options.yybArmShoulderCapAnchorInfluence;
            _yybArmSleeveAnchorMaxDegrees = options.yybArmSleeveAnchorMaxDegrees;
            _overrideYybArmVisualTwistRuntimeSettings = options.overrideYybArmVisualTwistRuntimeSettings;
            _enableYybArmVisualTwistRuntimeOverride = options.enableYybArmVisualTwistRuntimeOverride;
            _yybArmVisualUpperArmInfluence = options.yybArmVisualUpperArmInfluence;
            _yybArmVisualForearmInfluence = options.yybArmVisualForearmInfluence;
            _yybArmVisualUpperArmMaxDegrees = options.yybArmVisualUpperArmMaxDegrees;
            _yybArmVisualForearmMaxDegrees = options.yybArmVisualForearmMaxDegrees;
            _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = options.enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = options.disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            _manualAnimatorLowerBodySegmentDirectionReferenceWeight = options.manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = options.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride = options.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = options.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride = options.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = options.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = options.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride = options.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = options.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = options.enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = options.disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            _manualAnimatorFootHipsAlignedResidualYawReferenceWeight = options.manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = options.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride = options.enablePostSetHumanPoseRightEndpointPositionRuntimeOverride;
            _postSetHumanPoseRightEndpointPositionReferenceWeight = options.postSetHumanPoseRightEndpointPositionReferenceWeight;
            _postSetHumanPoseRightEndpointPositionReferenceMaxOffset = options.postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            _postSetHumanPoseRightEndpointPositionReferencePositiveZScale = options.postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = options.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = options.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = options.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            _postSetHumanPoseEndpointPositionUseLeftSide = options.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride = options.enablePreSetHumanPoseRightEndpointPositionRuntimeOverride;
            _preSetHumanPoseRightEndpointPositionReferenceWeight = options.preSetHumanPoseRightEndpointPositionReferenceWeight;
            _preSetHumanPoseRightEndpointPositionReferenceMaxOffset = options.preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            _preSetHumanPoseRightEndpointPositionReferencePositiveZScale = options.preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = options.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = options.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = options.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            _preSetHumanPoseEndpointPositionUseLeftSide = options.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            _preSetHumanPoseEndpointPositionUseGhostCurrentBasis = options.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            _preSetHumanPoseEndpointPositionInvertBodyPositionX = options.ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
            _preSetHumanPoseEndpointPositionInvertBodyPositionZ = options.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
            _usePostSetHumanPoseRightFootEvaluatorXzReference = options.usePostSetHumanPoseRightFootEvaluatorXzReference;
            _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = options.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            _enableManualAnimatorBipedIkFootPositionRuntimeOverride = options.enableManualAnimatorBipedIkFootPositionRuntimeOverride;
            _manualAnimatorBipedIkFootPositionReferenceWeight = options.manualAnimatorBipedIkFootPositionReferenceWeight;
            _manualAnimatorBipedIkFootPositionReferenceMaxOffset = options.manualAnimatorBipedIkFootPositionReferenceMaxOffset;
            _enableManualAnimatorHipsLocalPositionRuntimeOverride = options.enableManualAnimatorHipsLocalPositionRuntimeOverride;
            _manualAnimatorHipsLocalPositionReferenceWeight = options.manualAnimatorHipsLocalPositionReferenceWeight;
            _manualAnimatorHipsLocalPositionReferenceMaxOffset = options.manualAnimatorHipsLocalPositionReferenceMaxOffset;
            _enableManualAnimatorBodyPositionXzRuntimeOverride = options.enableManualAnimatorBodyPositionXzRuntimeOverride;
            _manualAnimatorBodyPositionXzReferenceWeight = options.manualAnimatorBodyPositionXzReferenceWeight;
            _manualAnimatorBodyPositionXzReferenceMaxOffset = options.manualAnimatorBodyPositionXzReferenceMaxOffset;
            _manualAnimatorBodyPositionXzReferenceFrameGateStart = options.manualAnimatorBodyPositionXzReferenceFrameGateStart;
            _manualAnimatorBodyPositionXzReferenceFrameGateEnd = options.manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = options.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            _manualAnimatorBodyPositionXzReferenceAxisXScale = options.manualAnimatorBodyPositionXzReferenceAxisXScale;
            _manualAnimatorBodyPositionXzReferenceAxisZScale = options.manualAnimatorBodyPositionXzReferenceAxisZScale;
            _enableYybRightSleeveSilhouetteOffsetRuntimeOverride = options.enableYybRightSleeveSilhouetteOffsetRuntimeOverride;
            _yybRightSleeveSilhouetteLocalOffsetX = options.yybRightSleeveSilhouetteLocalOffsetX;
            _yybRightSleeveSilhouetteLocalOffsetFrameGateStart = options.yybRightSleeveSilhouetteLocalOffsetFrameGateStart;
            _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = options.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd;
            _enableRetargetBodyPositionXzRootMotionRuntimeOverride = options.enableRetargetBodyPositionXzRootMotionRuntimeOverride;
            _disableTargetHumanoidBonePositionLockRuntimeOverride = options.disableTargetHumanoidBonePositionLockRuntimeOverride;
            _enableVmdPlaybackProbeRuntimeOverride = options.enableVmdPlaybackProbeRuntimeOverride;
            _applyVmdPlaybackProbeIkTargetsRuntimeOverride = options.applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            _vmdPlaybackProbeSourceVmdPath = options.vmdPlaybackProbeSourceVmdPath;
            _enableReferenceMmdTimingRuntimeOverride = options.enableReferenceMmdTimingRuntimeOverride;
            _editorDiagnosticSmokeSegment = ResolveEditorDiagnosticSmokeSegment(options.editorDiagnosticSmokeSegment);
            _diagnosticCaptureWidthOverride = options.diagnosticCaptureWidthOverride;
            _diagnosticCaptureHeightOverride = options.diagnosticCaptureHeightOverride;
            _diagnosticScreenshotPaddingOverride = options.diagnosticScreenshotPaddingOverride;
            _diagnosticScreenshotVerticalViewportCenterOverride = options.diagnosticScreenshotVerticalViewportCenterOverride;
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
            var profile = new VisualComparisonCaptureProfile(
                modelDisplayName: "YYB",
                manualReferenceDisplayName: "testPrefab",
                manualReferenceTargetNameToken: ManualTestPrefabNameToken,
                manualTargetNameToken: ManualYybNameToken,
                manualScene: new VisualComparisonScene(SubManualScenePath, "Sub_Manual"),
                recordingScene: new VisualComparisonScene(MainRecordingScenePath, "Main_Recoding"),
                automaticScene: new VisualComparisonScene(MainAutoScenePath, "Main_Auto"));

            return VisualComparisonCaptureJobPlanner
                .Build(profile, enableVmdPlaybackProbeRuntimeOverride)
                .Select(MapCaptureJob)
                .ToArray();
        }

        private static CaptureJob MapCaptureJob(VisualComparisonCaptureJob job)
        {
            return new CaptureJob
            {
                Mode = MapCaptureMode(job.Role),
                ScenePath = job.ScenePath,
                SceneName = job.SceneName,
                DisplayName = job.DisplayName,
                ManualTargetNameToken = job.TargetNameToken
            };
        }

        private static CaptureMode MapCaptureMode(VisualComparisonCaptureRole role)
        {
            switch (role)
            {
                case VisualComparisonCaptureRole.ManualReference:
                    return CaptureMode.SubManualTestPrefab;
                case VisualComparisonCaptureRole.ManualTarget:
                    return CaptureMode.SubManualYyb;
                case VisualComparisonCaptureRole.DirectRecording:
                    return CaptureMode.MainRecording;
                case VisualComparisonCaptureRole.PlaybackProbe:
                    return CaptureMode.MainRecordingVmdPlaybackProbe;
                case VisualComparisonCaptureRole.Automatic:
                    return CaptureMode.MainAuto;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "지원하지 않는 캡처 역할입니다.");
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            VisualComparisonPlayModeTransitionAction action =
                VisualComparisonPlayModeTransitionPlanner.Resolve(
                    MapPlayModePhase(state),
                    _isRunning,
                    _activeJob != null,
                    _activeJobFinished,
                    _advanceAfterPlayStopPending);

            switch (action)
            {
                case VisualComparisonPlayModeTransitionAction.Ignore:
                    return;
                case VisualComparisonPlayModeTransitionAction.StartActiveJob:
                    _playModeEntryPending = false;
                    _playModeEntryRequestedAt = 0d;
                    SavePersistedState();
                    EditorApplication.update -= TryEnterPlayModeForActiveJob;
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"}");
                    EditorApplication.delayCall += StartCurrentJobInPlayMode;
                    break;
                case VisualComparisonPlayModeTransitionAction.CleanupOnly:
                case VisualComparisonPlayModeTransitionAction.QueueAdvanceAfterPlayStop:
                case VisualComparisonPlayModeTransitionAction.QueuePlayModeEntry:
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished} pending={_advanceAfterPlayStopPending}");
                    CleanupActiveSubscriptions();
                    if (action == VisualComparisonPlayModeTransitionAction.QueueAdvanceAfterPlayStop)
                    {
                        QueueAdvanceAfterPlayStop("EnteredEditMode");
                    }
                    else if (action == VisualComparisonPlayModeTransitionAction.QueuePlayModeEntry)
                    {
                        QueuePlayModeEntryForActiveJob("EnteredEditModeWithoutCompletion");
                    }
                    break;
                case VisualComparisonPlayModeTransitionAction.ObservePlayModeExit:
                case VisualComparisonPlayModeTransitionAction.ReportPrematureExit:
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished} pending={_advanceAfterPlayStopPending}");
                    if (action == VisualComparisonPlayModeTransitionAction.ReportPrematureExit)
                    {
                        RecordFailure($"Play Mode가 작업 완료 전에 종료되었습니다: {_activeJob.DisplayName}");
                    }
                    break;
            }
        }

        private static VisualComparisonPlayModePhase MapPlayModePhase(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    return VisualComparisonPlayModePhase.EnteredPlayMode;
                case PlayModeStateChange.EnteredEditMode:
                    return VisualComparisonPlayModePhase.EnteredEditMode;
                case PlayModeStateChange.ExitingPlayMode:
                    return VisualComparisonPlayModePhase.ExitingPlayMode;
                default:
                    return VisualComparisonPlayModePhase.Other;
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
            _activeFBXVmdPipeline = null;
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
            _activeFBXVmdPipeline = UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                .GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<FBXVmdPipeline>(true))
                .FirstOrDefault(candidate => candidate != null);
            if (_activeFBXVmdPipeline == null)
            {
                throw new InvalidOperationException($"{_activeJob.SceneName} 씬에서 FBXVmdPipeline를 찾지 못했습니다.");
            }

            ApplyMainSceneRuntimeOverrides(_activeFBXVmdPipeline);

            _activeRecorder = _activeFBXVmdPipeline.targetCharacter != null
                ? _activeFBXVmdPipeline.targetCharacter.GetComponent<HumanoidSampleCode>()
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

            _activeFBXVmdPipeline.EditorDiagnosticSmokeFinished -= HandleMainSceneFinished;
            _activeFBXVmdPipeline.EditorDiagnosticSmokeFinished += HandleMainSceneFinished;
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
            bool started = _activeFBXVmdPipeline.StartEditorDiagnosticSmoke(
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
                throw new InvalidOperationException("FBXVmdPipeline.StartEditorDiagnosticSmoke가 false를 반환했습니다.");
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
            return VmdIkDeltaGuardRuntimeOverrideApplier.Apply(
                recorder,
                overrideLimitVmd,
                recoveryTriggerVmd,
                recoveryDebtThresholdVmd,
                recoveryHoldFrames);
        }

        private static bool ApplyVmdPlaybackProbeRuntimeOverride(
            GameObject target,
            string sourceVmdPath,
            UnityHumanoidVMDRecorder recorder,
            bool applyIkTargets)
        {
            return VmdPlaybackProbeRuntimeOverrideApplier.Apply(
                target,
                sourceVmdPath,
                recorder,
                applyIkTargets);
        }

        private static bool ApplyMainSceneRuntimeOverrides(FBXVmdPipeline fileManager)
        {
            return YybVisualComparisonRuntimeOverrideCoordinator.Apply(
                fileManager,
                BuildCurrentPersistedState(),
                DefaultRetargetArmStretchMuscleLimit,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorFullBodyPoseReferenceWeight);
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            bool excludeLowerBodyMuscles,
            bool lowerBodyMusclesOnly = false,
            bool legTwistMusclesOnly = false,
            bool rightArmMusclesOnly = false,
            bool leftArmMusclesOnly = false,
            bool rightSleeveChainMusclesOnly = false,
            float frameGateStart = DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart,
            float frameGateEnd = DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyFullBodyPose(
                fileManager,
                enabled,
                weight,
                excludeLowerBodyMuscles,
                lowerBodyMusclesOnly,
                legTwistMusclesOnly,
                rightArmMusclesOnly,
                leftArmMusclesOnly,
                rightSleeveChainMusclesOnly,
                frameGateStart,
                frameGateEnd);
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorBodyRotationRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorBodyRotationReferenceWeight);
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyBodyRotation(fileManager, enabled, weight);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            return YybArmRuntimeOverrideApplier.ApplySwingLimit(
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
                raisedPoseHorizontalReachLimitWeight,
                raisedPoseMinUpperArmDownDot,
                raisedPoseMaxHandBelowShoulderRatio,
                raisedPoseMaxHandHorizontalReachRatio);
        }

        private static bool ApplyYybArmDirectionRetargetRuntimeOverride(
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees,
            float leftSideWeightScale,
            float rightSideWeightScale)
        {
            return YybArmRuntimeOverrideApplier.ApplyDirection(
                fileManager,
                enabled,
                upperArmWeight,
                forearmWeight,
                upperArmMaxDegrees,
                forearmMaxDegrees,
                leftSideWeightScale,
                rightSideWeightScale);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            return ManualLowerBodySegmentDirectionRuntimeOverrideApplier.Apply(
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
                rightLowerLegToFootBlendWeight,
                rightLowerLegToFootFrameGateStart,
                rightLowerLegToFootFrameGateEnd,
                rightLowerLegToFootEndpointBlendWeight,
                disableFootToToes,
                footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight,
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
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
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd)
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
                useLeftSide: false,
                evaluatorXzReferenceEnabled: false,
                evaluatorXzTargetMagnitude:
                    DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
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
            return HumanPoseEndpointRuntimeOverrideApplier.ApplyPostSetReference(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                frameGateStart,
                frameGateEnd,
                useLeftSide,
                evaluatorXzReferenceEnabled,
                evaluatorXzTargetMagnitude);
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyBipedIkFootPosition(
                fileManager,
                enabled,
                weight,
                maxOffset);
        }

        private static bool ApplyManualAnimatorHipsLocalPositionRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorHipsLocalPositionReferenceWeight,
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset);
        }

        private static bool ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyHipsLocalPosition(
                fileManager,
                enabled,
                weight,
                maxOffset);
        }

        private static bool HasMmdIkDeltaGuardLimitOverride(float value)
        {
            return VmdIkDeltaGuardRuntimeOverrideApplier.HasLimit(value);
        }

        private static bool HasDiagnosticScreenshotFramingOverride(float value)
        {
            return VisualComparisonScreenshotOverridePolicy.HasFiniteFramingOverride(value);
        }

        private static FBXVmdPipeline.EditorDiagnosticSmokeSegment ResolveEditorDiagnosticSmokeSegment(string value)
        {
            return VisualComparisonCaptureSegmentPlanner.ResolveSegment(value);
        }

        private static VisualComparisonManualCapturePlan BuildManualAnimatorCapturePlan(
            string labelSuffix,
            string fbxFileName,
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            return VisualComparisonCaptureSegmentPlanner.BuildManualCapturePlan(
                labelSuffix,
                fbxFileName,
                referenceClipLengthSeconds,
                requestedDurationSeconds,
                DefaultFrameRate,
                segment);
        }

        private static float CalculateEditorDiagnosticSmokeStartTime(
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            return VisualComparisonCaptureSegmentPlanner.CalculateStartTime(
                referenceClipLengthSeconds,
                requestedDurationSeconds,
                segment);
        }

        private static string GetEditorDiagnosticSmokeSegmentLabel(FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            return VisualComparisonCaptureSegmentPlanner.GetSegmentLabel(segment);
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
            VisualComparisonManualCapturePlan capturePlan = BuildManualAnimatorCapturePlan(
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
                : null;
            VmdSaveResult stableResult = BuildStableCandidateResult(result);
            if (_activeJob.Mode == CaptureMode.MainRecording &&
                !string.IsNullOrWhiteSpace(stableResult.FilePath) &&
                File.Exists(stableResult.FilePath))
            {
                _vmdPlaybackProbeSourceVmdPath = stableResult.FilePath;
                _currentRunOptions.vmdPlaybackProbeSourceVmdPath = stableResult.FilePath;
            }

            FinalizeActiveJob(
                stableResult,
                probe,
                targetName: _activeFBXVmdPipeline != null && _activeFBXVmdPipeline.targetCharacter != null
                ? _activeFBXVmdPipeline.targetCharacter.name
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
            CaptureFBXVmdPipelineEffectiveSettings(captureResult, _activeFBXVmdPipeline);
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

        private static void CaptureFBXVmdPipelineEffectiveSettings(CaptureResult result, FBXVmdPipeline fileManager)
        {
            YybVisualComparisonEffectiveSettingsSnapshotter.Capture(result, fileManager);
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

            return VisualComparisonCandidateArtifactStore.Copy(
                result,
                copyPath,
                _summaryDirectory,
                SanitizeFileName);
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
            if (_activeFBXVmdPipeline != null)
            {
                _activeFBXVmdPipeline.EditorDiagnosticSmokeFinished -= HandleMainSceneFinished;
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
                SummaryContainer summary = BuildSummaryContainer(frameQualitySummaries, frameRoleDiagnostics);
                WriteSummaryJson(summaryJsonPath, summary);
                WriteSummaryMarkdown(summaryMarkdownPath, summary);
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
            EnterPlayModeOptionsController.Apply(Application.isBatchMode);
        }

        private static void RestoreEnterPlayModeOptions()
        {
            EnterPlayModeOptionsController.Restore();
        }

        private static void SavePersistedState()
        {
            PersistedState state = BuildCurrentPersistedState();
            VisualComparisonRunStateStore.SaveJson(RunnerStateSessionKey, JsonUtility.ToJson(state));
        }

        private static PersistedState BuildCurrentPersistedState()
        {
            PersistedState state = new PersistedState
            {
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
            YybVisualComparisonRunOptionsCopier.Copy(_currentRunOptions, state);
            state.editorDiagnosticSmokeSegment = _editorDiagnosticSmokeSegment.ToString();
            state.vmdPlaybackProbeSourceVmdPath = _vmdPlaybackProbeSourceVmdPath;
            return state;
        }
        private static void ClearPersistedState()
        {
            VisualComparisonRunStateStore.Clear(RunnerStateSessionKey);
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

            string json = VisualComparisonRunStateStore.ReadJson(RunnerStateSessionKey);
            PersistedState state = JsonUtility.FromJson<PersistedState>(json);
            try
            {
                RestoreFromPersistedState(state);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YybVisualComparisonBatchRunner] 상태 복원 실패: {ex.Message}\n{ex.StackTrace}");
                RecordFailure($"상태 복원 실패: {ex.Message}");
                ClearPersistedState();
                _isRunning = false;
                HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
                RestoreEnterPlayModeOptions();
            }
        }

        private static void RestoreFromPersistedState(PersistedState state)
        {
            YybVisualComparisonPersistedRunStateNormalizer.Normalize(
                state,
                CreateDefaultRunOptions());
            ApplyRunOptions(state);
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
                foreach (VisualComparisonCaptureJobStateData job in state.pendingJobs)
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
                foreach (YybVisualComparisonCaptureResultData result in state.results)
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

            _activeFBXVmdPipeline = null;
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

            ResumeRestoredRun();
        }

        private static void ResumeRestoredRun()
        {
            VisualComparisonRunResumeAction action = VisualComparisonRunResumePlanner.Resolve(
                _advanceAfterPlayStopPending,
                _playModeEntryPending,
                _activeJob != null,
                EditorApplication.isPlaying,
                _activeJobFinished,
                PendingJobs.Count > 0);

            switch (action)
            {
                case VisualComparisonRunResumeAction.QueueAdvanceAfterPlayStop:
                    QueueAdvanceAfterPlayStop("RestoreFromPersistedState");
                    break;
                case VisualComparisonRunResumeAction.QueuePlayModeEntry:
                    QueuePlayModeEntryForActiveJob("RestoreFromPersistedState");
                    break;
                case VisualComparisonRunResumeAction.RecoverMissingActiveJob:
                    RecoverFromMissingActiveJob("RestoreFromPersistedState");
                    break;
                case VisualComparisonRunResumeAction.DeferActiveJobStartInPlayMode:
                    EditorApplication.delayCall += StartCurrentJobInPlayMode;
                    break;
                case VisualComparisonRunResumeAction.DeferNextJob:
                    EditorApplication.delayCall += StartNextJob;
                    break;
                case VisualComparisonRunResumeAction.DeferActiveJobEntry:
                    EditorApplication.delayCall += RestoreActiveJobEntry;
                    break;
                case VisualComparisonRunResumeAction.StartNextJob:
                    StartNextJob();
                    break;
                case VisualComparisonRunResumeAction.FinalizeRun:
                    FinalizeRun();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "지원하지 않는 실행 복원 동작입니다.");
            }
        }

        private static void RestoreActiveJobEntry()
        {
            QueuePlayModeEntryForActiveJob("RestoreActiveJob");
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
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[YybVisualComparisonBatchRunner] Runner 트레이스 쓰기 실패: {ex.Message}");
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

        private static CaptureJob FromPersistedJob(VisualComparisonCaptureJobStateData job)
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

            PersistedCaptureResult persistedResult = new PersistedCaptureResult();
            YybVisualComparisonCaptureResultDataCopier.Copy(result, persistedResult);
            return persistedResult;
        }

        private static CaptureResult FromPersistedResult(YybVisualComparisonCaptureResultData result)
        {
            if (result == null)
            {
                return null;
            }

            CaptureResult captureResult = new CaptureResult();
            YybVisualComparisonCaptureResultDataCopier.Copy(result, captureResult);
            return captureResult;
        }

        private static SummaryContainer BuildSummaryContainer(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries = null,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = null)
        {
            frameRoleDiagnostics = frameRoleDiagnostics ?? BuildCurrentSummaryFrameRoleDiagnostics();
            frameQualitySummaries = frameQualitySummaries ?? BuildFrameQualitySummaries(frameRoleDiagnostics);
            PersistedState state = BuildCurrentPersistedState();
            SummaryContainer summary = new SummaryContainer();
            YybVisualComparisonSummarySettingsSnapshotter.Capture(
                summary,
                state,
                ResolveSummaryTargetFrameCount(),
                MakeProjectRelativePath(state.vmdPlaybackProbeSourceVmdPath));
            summary.generated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            summary.reference_clip_name = _referenceClip != null ? _referenceClip.name : string.Empty;
            summary.reference_clip_asset_path = _referenceClipAssetPath;
            summary.results = Results.ToArray();
            summary.frame_count_roles = frameRoleDiagnostics;
            summary.sample_ordering_diagnostics = BuildSampleOrderingDiagnostics();
            summary.selected_candidate_artifact = BuildCandidateArtifactSelection(frameQualitySummaries);
            summary.frame_quality_summaries = frameQualitySummaries;
            summary.failures = Failures.ToArray();

            return summary;
        }

        private static void WriteSummaryJson(string path, SummaryContainer summary)
        {
            VisualComparisonSummaryFileStore.WriteJson(path, summary);
        }

        private static void WriteSummaryMarkdown(
            string path,
            SummaryContainer summaryData)
        {
            if (summaryData == null)
            {
                throw new ArgumentNullException(nameof(summaryData));
            }

            string markdown = YybVisualComparisonSummaryMarkdownRenderer.Render(
                summaryData,
                ReferenceAlignedVisualEvidenceMaxBBoxNormalizedImageSpaceKeypointL1Delta);
            VisualComparisonSummaryFileStore.WriteText(path, markdown);
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
            if (frameRoleDiagnostics != null)
            {
                ApplyImportedFbxVisualEvidenceFrameQualityPolicy(frameQualitySummaries, frameRoleDiagnostics);
            }

            bool acceptedUserFacingArtifactPreservesRawDiagnostic =
                HasAcceptedUserFacingArtifactPreservingRawDiagnostic(frameQualitySummaries);
            return VisualComparisonFrameQualityFailurePolicy.BuildFailureMessages(
                frameQualitySummaries,
                acceptedUserFacingArtifactPreservesRawDiagnostic);
        }

        private static bool HasAcceptedUserFacingArtifactPreservingRawDiagnostic(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            VisualComparisonCandidateArtifactSelectionData selection =
                BuildCandidateArtifactSelection(frameQualitySummaries);
            return selection != null &&
                selection.selected_candidate_is_acceptance_artifact &&
                selection.selected_candidate_preserves_raw_diagnostic &&
                string.Equals(selection.selected_candidate_output_role, "user_facing_export_artifact", StringComparison.Ordinal);
        }

        private static void ApplyImportedFbxVisualEvidenceFrameQualityPolicy(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            YybVisualComparisonReferenceAlignmentPolicy.Apply(
                frameQualitySummaries,
                HasReferenceAlignedImportedFbxVisualEvidence(frameRoleDiagnostics));
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
            return ReferenceFrameCountResolver.ResolveSummaryTarget(
                referenceTargetFrameCount,
                mainAutoFrameCount);
        }

        private static int ResolveReferenceMmdTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate)
        {
            return ReferenceFrameCountResolver.Resolve(
                fbxFileName,
                requestedDurationSeconds,
                configuredTargetFrameCount,
                referenceClipLengthSeconds,
                recordingFrameRate,
                SatisfactionReferenceOutputBaseName,
                SatisfactionReferenceMaxMmdFrame);
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
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
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
            return ReferenceAlignedSampleTimePlanner.Build(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                ReferenceMp4ProbeDefaultLocalSampleTimes,
                referenceLocalSampleSeconds,
                candidateClipSecondsPerReferenceSecond,
                DefaultFrameRate);
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
            YybScreenshotDiagnosticAnalyzer.AttachCandidateScreenshotFrameDiagnostics(
                diagnostics,
                candidateFrameIndexPath,
                _projectRoot);
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
            diagnostics.reference_mp4_contact_sheet_exists = File.Exists(contactSheetPath);

            ReferenceVideoDiagnosticsData referenceVideo =
                ReferenceVideoDiagnosticsReader.Read(resultPath, frameMetricsPath);
            diagnostics.reference_mp4_analysis_result_exists = referenceVideo.AnalysisFileExists;
            diagnostics.reference_mp4_analysis_error = referenceVideo.AnalysisError;
            diagnostics.reference_mp4_analysis_schema = referenceVideo.AnalysisSchema;
            diagnostics.reference_mp4_extracted_frame_count = referenceVideo.ExtractedFrameCount;
            diagnostics.reference_mp4_width = referenceVideo.VideoWidth;
            diagnostics.reference_mp4_height = referenceVideo.VideoHeight;
            diagnostics.reference_mp4_avg_frame_rate = referenceVideo.AverageFrameRate;
            diagnostics.reference_mp4_stream_duration_seconds = referenceVideo.StreamDurationSeconds;
            diagnostics.reference_mp4_total_video_frames = referenceVideo.TotalVideoFrames;
            diagnostics.reference_mp4_frame_metrics_exists = referenceVideo.FrameMetricsFileExists;
            diagnostics.reference_mp4_frame_metrics_error = referenceVideo.FrameMetricsError;
            diagnostics.reference_mp4_frame_metrics_schema = referenceVideo.FrameMetricsSchema;
            diagnostics.reference_mp4_frame_metrics_sample_count = referenceVideo.FrameMetricsSampleCount;
            diagnostics.reference_mp4_frame_metrics_extracted_frame_count =
                referenceVideo.FrameMetricsExtractedFrameCount;
            diagnostics.reference_mp4_avg_bbox_height_ratio = referenceVideo.AverageBBoxHeightRatio;
            diagnostics.reference_mp4_avg_bbox_width_ratio = referenceVideo.AverageBBoxWidthRatio;
            diagnostics.reference_mp4_center_x_range_ratio = referenceVideo.CenterXRangeRatio;
            diagnostics.reference_mp4_max_bottom_gap_ratio = referenceVideo.MaxBottomGapRatio;
            diagnostics.reference_mp4_avg_bright_area_ratio = referenceVideo.AverageBrightAreaRatio;
            AttachReferenceMp4CurrentClipCoverage(
                diagnostics,
                referenceVideo.FrameMetricRows);
        }

        private static void AttachReferenceMp4CurrentClipCoverage(
            SummaryFrameRoleDiagnostics diagnostics,
            Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow[] rows)
        {
            if (diagnostics == null || rows == null)
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
            foreach (Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow row in rows)
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
                if (YybScreenshotDiagnosticAnalyzer.TryAnalyzeCandidateScreenshotFrame(
                        framePath,
                        out var imageMetric,
                        out _) &&
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

        internal static bool IsFiniteMetric(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static float ResolveFrameTopGapRatio(float bottomGapRatio, float bboxHeightRatio)
        {
            if (!IsFiniteMetric(bottomGapRatio) || !IsFiniteMetric(bboxHeightRatio))
            {
                return float.NaN;
            }

            return Mathf.Max(0f, 1f - bottomGapRatio - bboxHeightRatio);
        }

        internal static bool IsFrameEdgeTouched(float bottomGapRatio, float topGapRatio)
        {
            return (IsFiniteMetric(bottomGapRatio) &&
                    bottomGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance) ||
                   (IsFiniteMetric(topGapRatio) &&
                    topGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance);
        }

        internal static int IndexOfHeader(string[] headers, string headerName)
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

            return VisualComparisonArtifactPathResolver.ResolveProjectRelative(
                relativePath,
                ResolveProjectRootForDiagnostics());
        }

        private static string ResolveProjectRootForDiagnostics()
        {
            if (!string.IsNullOrWhiteSpace(_projectRoot))
            {
                return _projectRoot;
            }

            string dataPath = Application.dataPath;
            DirectoryInfo projectRoot = string.IsNullOrWhiteSpace(dataPath)
                ? null
                : Directory.GetParent(dataPath);
            if (projectRoot == null)
            {
                throw new InvalidOperationException("Unity 프로젝트 루트를 확인할 수 없습니다.");
            }

            return projectRoot.FullName;
        }

        private static VisualComparisonCandidateArtifactSelectionData BuildCandidateArtifactSelection(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            return VisualComparisonCandidateArtifactSelector.Select(
                frameQualitySummaries,
                _projectRoot);
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
            SummarySampleOrderingDiagnostic diagnostic = new SummarySampleOrderingDiagnostic();
            VisualComparisonSampleOrderingDiagnosticBuilder.Populate(
                diagnostic,
                jobMode,
                sceneName,
                metricsCsvPath,
                _projectRoot);
            return diagnostic;
        }

        private static float ResolveGroundingStepToMaxRatio(
            string[] row,
            Dictionary<string, int> indices,
            float step,
            float maxStep)
        {
            float reportedRatio = GetCsvFloat(row, indices, "retargetGroundingLastStepToMaxStepRatio");
            return VisualComparisonMetricCalculator.ResolveGroundingStepToMaxRatio(
                reportedRatio,
                step,
                maxStep);
        }

        internal static string[] SplitSimpleCsvLine(string line)
        {
            return VisualComparisonCsvMetricReader.SplitLine(line);
        }

        private static float GetCsvFloat(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return VisualComparisonCsvMetricReader.ReadFloat(row, indices, column);
        }

        private static string ToAbsoluteProjectPath(string path)
        {
            return VisualComparisonArtifactPathResolver.ToAbsoluteProjectPath(path, _projectRoot);
        }

        private static void CopyLatestSummary(string sourcePath, string relativeTargetPath)
        {
            VisualComparisonSummaryFileStore.CopyLatest(sourcePath, _projectRoot, relativeTargetPath);
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
            return FbxReferenceClipPathResolver.Resolve(
                fbxFileName,
                DefaultFbxFileName,
                ProjectFbxDirectory,
                ImportFbxDirectory,
                hasReferenceClip);
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
            return VisualComparisonArtifactPathResolver.MakeProjectRelative(absolutePath, _projectRoot);
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
            return VisualComparisonArtifactNamePolicy.SanitizeFileName(
                fileName,
                "yyb_visual_compare");
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
            return VisualComparisonArtifactNamePolicy.BuildEvidenceFileName(
                "vmd",
                shortMode,
                safeExtension,
                ".vmd",
                "yyb_visual_compare");
        }

        private static string ShortenFileNameToLength(string value, int maxLength)
        {
            return VisualComparisonArtifactNamePolicy.ShortenToLength(value, maxLength);
        }

        [Serializable]
        private sealed class SummaryContainer : YybVisualComparisonSummaryData
        {
        }

        [Serializable]
        internal sealed class SummaryFrameRoleDiagnostics : VisualComparisonFrameRoleDiagnosticsData
        {
            [NonSerialized]
            public readonly List<Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow>
                referenceMp4CurrentClipRows =
                    new List<Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow>();
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
        internal sealed class ReferenceMp4FrameMetricRow : Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow
        {
        }

        [Serializable]
        private sealed class SummarySampleOrderingDiagnostic : VisualComparisonSampleOrderingDiagnosticData
        {
        }
    }
}
#endif

