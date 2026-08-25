
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
            bool manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseRightArmMusclesOnly", false);
            bool manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseLeftArmMusclesOnly", false);
            bool manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride =
                GetCommandLineBool("-yybCompareManualAnimatorFullBodyPoseRightSleeveChainMusclesOnly", false);
            float manualAnimatorFullBodyPoseReferenceFrameGateStart = GetCommandLineFloat(
                "-yybCompareManualAnimatorFullBodyPoseFrameGateStart",
                DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart);
            float manualAnimatorFullBodyPoseReferenceFrameGateEnd = GetCommandLineFloat(
                "-yybCompareManualAnimatorFullBodyPoseFrameGateEnd",
                DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd);
            bool enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride =
                GetCommandLineBool("-yybCompareSetHumanPoseRightLegTwistOutputEnabled", false);
            float setHumanPoseRightLegTwistOutputReferenceWeight = GetCommandLineFloat(
                "-yybCompareSetHumanPoseRightLegTwistOutputWeight",
                DefaultSetHumanPoseRightLegTwistOutputReferenceWeight);
            float setHumanPoseRightLegTwistOutputReferenceMaxDelta = GetCommandLineFloat(
                "-yybCompareSetHumanPoseRightLegTwistOutputMaxDelta",
                DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta);
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
            bool enableRetargetArmStretchClampRuntimeOverride =
                GetCommandLineBool("-yybCompareRetargetArmStretchClampEnabled", false);
            float retargetArmStretchMuscleLimit = GetCommandLineFloat(
                "-yybCompareRetargetArmStretchMuscleLimit",
                DefaultRetargetArmStretchMuscleLimit);
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
            bool ShouldUseLeftSideForPostSetHumanPoseEndpointPosition =
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
            bool ShouldUseLeftSideForPreSetHumanPoseEndpointPosition =
                GetCommandLineBool("-yybComparePreSetHumanPoseEndpointPositionUseLeftSide", false);
            bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                GetCommandLineBool("-yybComparePreSetHumanPoseEndpointPositionUseGhostCurrentBasis", false);
            bool ShouldInvertPreSetHumanPoseEndpointPositionBodyX =
                GetCommandLineBool("-yybComparePreSetHumanPoseEndpointPositionInvertBodyPositionX", false);
            bool ShouldInvertPreSetHumanPoseEndpointPositionBodyZ =
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
            bool enableYybRightSleeveSilhouetteOffsetRuntimeOverride =
                GetCommandLineBool("-yybCompareYybRightSleeveSilhouetteOffsetEnabled", false);
            float yybRightSleeveSilhouetteLocalOffsetX = GetCommandLineFloat(
                "-yybCompareYybRightSleeveSilhouetteLocalOffsetX",
                DefaultYybRightSleeveSilhouetteLocalOffsetX);
            float yybRightSleeveSilhouetteLocalOffsetFrameGateStart = GetCommandLineFloat(
                "-yybCompareYybRightSleeveSilhouetteLocalOffsetFrameGateStart",
                DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateStart);
            float yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = GetCommandLineFloat(
                "-yybCompareYybRightSleeveSilhouetteLocalOffsetFrameGateEnd",
                DefaultYybRightSleeveSilhouetteLocalOffsetFrameGateEnd);
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
                manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceFrameGateStart,
                manualAnimatorFullBodyPoseReferenceFrameGateEnd,
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
                enableRetargetArmStretchClampRuntimeOverride,
                retargetArmStretchMuscleLimit,
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
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ,
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
                enableYybRightSleeveSilhouetteOffsetRuntimeOverride,
                yybRightSleeveSilhouetteLocalOffsetX,
                yybRightSleeveSilhouetteLocalOffsetFrameGateStart,
                yybRightSleeveSilhouetteLocalOffsetFrameGateEnd,
                enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                disableTargetHumanoidBonePositionLockRuntimeOverride,
                enableVmdPlaybackProbeRuntimeOverride,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                editorDiagnosticSmokeSegmentName,
                enableReferenceMmdTimingRuntimeOverride,
                diagnosticCaptureWidthOverride,
                diagnosticCaptureHeightOverride,
                diagnosticScreenshotPaddingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride,
                enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride:
                    enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride,
                setHumanPoseRightLegTwistOutputReferenceWeight:
                    setHumanPoseRightLegTwistOutputReferenceWeight,
                setHumanPoseRightLegTwistOutputReferenceMaxDelta:
                    setHumanPoseRightLegTwistOutputReferenceMaxDelta);
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
                manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceFrameGateStart,
                manualAnimatorFullBodyPoseReferenceFrameGateEnd,
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
                enableRetargetArmStretchClampRuntimeOverride,
                retargetArmStretchMuscleLimit,
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
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ,
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
                enableYybRightSleeveSilhouetteOffsetRuntimeOverride,
                yybRightSleeveSilhouetteLocalOffsetX,
                yybRightSleeveSilhouetteLocalOffsetFrameGateStart,
                yybRightSleeveSilhouetteLocalOffsetFrameGateEnd,
                enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                disableTargetHumanoidBonePositionLockRuntimeOverride,
                enableVmdPlaybackProbeRuntimeOverride,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                editorDiagnosticSmokeSegmentName,
                enableReferenceMmdTimingRuntimeOverride,
                diagnosticCaptureWidthOverride,
                diagnosticCaptureHeightOverride,
                diagnosticScreenshotPaddingOverride,
                diagnosticScreenshotVerticalViewportCenterOverride,
                enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride:
                    enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride,
                setHumanPoseRightLegTwistOutputReferenceWeight:
                    setHumanPoseRightLegTwistOutputReferenceWeight,
                setHumanPoseRightLegTwistOutputReferenceMaxDelta:
                    setHumanPoseRightLegTwistOutputReferenceMaxDelta);
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
            _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride =
                manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride =
                manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride =
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseReferenceFrameGateStart =
                Mathf.Max(0f, manualAnimatorFullBodyPoseReferenceFrameGateStart);
            _manualAnimatorFullBodyPoseReferenceFrameGateEnd =
                Mathf.Max(0f, manualAnimatorFullBodyPoseReferenceFrameGateEnd);
            _enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride =
                enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride;
            _setHumanPoseRightLegTwistOutputReferenceWeight = Mathf.Clamp01(
                setHumanPoseRightLegTwistOutputReferenceWeight);
            _setHumanPoseRightLegTwistOutputReferenceMaxDelta =
                Mathf.Max(0f, setHumanPoseRightLegTwistOutputReferenceMaxDelta);
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
            _enableRetargetArmStretchClampRuntimeOverride = enableRetargetArmStretchClampRuntimeOverride;
            _retargetArmStretchMuscleLimit = Mathf.Clamp(
                retargetArmStretchMuscleLimit,
                0f,
                DefaultRetargetArmStretchMuscleLimit);
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
            _postSetHumanPoseEndpointPositionUseLeftSide = ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
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
            _preSetHumanPoseEndpointPositionUseLeftSide = ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            _preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            _preSetHumanPoseEndpointPositionInvertBodyPositionX =
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
            _preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
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
            _enableYybRightSleeveSilhouetteOffsetRuntimeOverride =
                enableYybRightSleeveSilhouetteOffsetRuntimeOverride;
            _yybRightSleeveSilhouetteLocalOffsetX =
                Mathf.Clamp(yybRightSleeveSilhouetteLocalOffsetX, -0.2f, 0.2f);
            _yybRightSleeveSilhouetteLocalOffsetFrameGateStart =
                Mathf.Clamp(yybRightSleeveSilhouetteLocalOffsetFrameGateStart, 0f, 6000f);
            _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd =
                Mathf.Clamp(yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, 0f, 6000f);
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

        private static bool ApplyFinalIkFootGroundingRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return FinalIkFootGroundingRuntimeOverrideApplier.Apply(fileManager, enabled);
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
                     _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseReferenceFrameGateStart > 0f ||
                     _manualAnimatorFullBodyPoseReferenceFrameGateEnd > 0f)
            {
                ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorFullBodyPoseReferenceWeight,
                    _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                    _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseReferenceFrameGateStart,
                    _manualAnimatorFullBodyPoseReferenceFrameGateEnd);
            }

            if (_enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride)
            {
                ApplySetHumanPoseRightLegTwistOutputRuntimeOverride(
                    fileManager,
                    true,
                    _setHumanPoseRightLegTwistOutputReferenceWeight,
                    _setHumanPoseRightLegTwistOutputReferenceMaxDelta);
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

            if (_enableRetargetArmStretchClampRuntimeOverride)
            {
                ApplyRetargetArmStretchClampRuntimeOverride(
                    fileManager,
                    true,
                    _retargetArmStretchMuscleLimit);
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

            if (_enableYybRightSleeveSilhouetteOffsetRuntimeOverride)
            {
                ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
                    fileManager,
                    true,
                    _yybRightSleeveSilhouetteLocalOffsetX,
                    _yybRightSleeveSilhouetteLocalOffsetFrameGateStart,
                    _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd);
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

        private static bool ApplyManualAnimatorFootLocalRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyFootLocalRotation(fileManager, enabled);
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

        private static bool ApplySetHumanPoseRightLegTwistOutputRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxDelta)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyRightLegTwistOutput(
                fileManager,
                enabled,
                weight,
                maxDelta);
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

        private static bool ApplyManualAnimatorHandLocalRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyHandLocalRotation(fileManager, enabled);
        }

        private static bool ApplyManualAnimatorThumbLocalRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyThumbLocalRotation(fileManager, enabled);
        }

        private static bool ApplyManualAnimatorHandPalmFrameRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyHandPalmFrame(fileManager, enabled, weight);
        }

        private static bool ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float currentWeight,
            float forearmStretchClampMaxOffset)
        {
            return RetargetingRuntimeOverrideApplier.ApplyPoseVisualSpikeSmoothing(
                fileManager,
                enabled,
                currentWeight,
                forearmStretchClampMaxOffset);
        }

        private static bool ApplyRetargetArmStretchClampRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float stretchLimit)
        {
            return RetargetingRuntimeOverrideApplier.ApplyArmStretchClamp(
                fileManager,
                enabled,
                stretchLimit,
                DefaultRetargetArmStretchMuscleLimit);
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

        private static bool ApplyYybArmSleeveAnchorRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float sleeveInfluence,
            float shoulderCapInfluence,
            float maxDegrees)
        {
            return YybArmRuntimeOverrideApplier.ApplySleeveAnchor(
                fileManager,
                enabled,
                sleeveInfluence,
                shoulderCapInfluence,
                maxDegrees);
        }

        private static bool ApplyYybArmVisualTwistRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float upperArmInfluence,
            float forearmInfluence,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            return YybArmRuntimeOverrideApplier.ApplyVisualTwist(
                fileManager,
                enabled,
                upperArmInfluence,
                forearmInfluence,
                upperArmMaxDegrees,
                forearmMaxDegrees);
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

        private static bool HasManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride()
        {
            return ManualLowerBodySegmentDirectionRuntimeOverrideApplier.HasDetails(
                _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride(
            FBXVmdPipeline fileManager)
        {
            return ManualLowerBodySegmentDirectionRuntimeOverrideApplier.ApplyDetails(
                fileManager,
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

        private static bool ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyFootHipsAlignedResidualYaw(
                fileManager,
                enabled,
                weight,
                maxAngle);
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

        private static bool ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
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
            return HumanPoseEndpointRuntimeOverrideApplier.ApplyPreSetReference(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                frameGateStart,
                frameGateEnd,
                useLeftSide,
                useGhostCurrentBasis,
                invertBodyPositionX,
                invertBodyPositionZ);
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

        private static bool ApplyManualAnimatorBodyPositionXzRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float frameGateStart,
            float frameGateEnd,
            float frameGateBlendFrames,
            float axisXScale,
            float axisZScale)
        {
            return ManualPoseReferenceRuntimeOverrideApplier.ApplyBodyPositionXz(
                fileManager,
                enabled,
                weight,
                maxOffset,
                frameGateStart,
                frameGateEnd,
                frameGateBlendFrames,
                axisXScale,
                axisZScale);
        }

        private static bool ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float localOffsetX,
            float frameGateStart,
            float frameGateEnd)
        {
            return YybArmRuntimeOverrideApplier.ApplyRightSleeveSilhouetteOffset(
                fileManager,
                enabled,
                localOffsetX,
                frameGateStart,
                frameGateEnd);
        }

        private static bool ApplyTargetHumanoidBonePositionLockRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled)
        {
            return RetargetingRuntimeOverrideApplier.ApplyTargetHumanoidBonePositionLock(fileManager, enabled);
        }

        private static bool ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled)
        {
            return RetargetingRuntimeOverrideApplier.ApplyBodyPositionXzRootMotion(fileManager, enabled);
        }

        private static float NormalizeMmdIkDeltaGuardLimitOverride(float value)
        {
            return VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeLimit(value);
        }

        private static float NormalizePositiveFloat(float value, float fallbackValue)
        {
            return VisualComparisonRuntimeValueNormalizer.NormalizePositive(value, fallbackValue);
        }

        private static float NormalizeFiniteFloat(float value, float fallbackValue)
        {
            return VisualComparisonRuntimeValueNormalizer.NormalizeFinite(value, fallbackValue);
        }

        private static bool HasMmdIkDeltaGuardLimitOverride(float value)
        {
            return VmdIkDeltaGuardRuntimeOverrideApplier.HasLimit(value);
        }

        private static bool HasDiagnosticScreenshotFramingOverride(float value)
        {
            return VisualComparisonScreenshotOverridePolicy.HasFiniteFramingOverride(value);
        }

        private static int NormalizeMmdIkDeltaGuardRecoveryHoldFrames(int value)
        {
            return VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeRecoveryHoldFrames(value);
        }

        private static int NormalizeDiagnosticCaptureDimensionOverride(int value)
        {
            return VisualComparisonScreenshotOverridePolicy.NormalizeCaptureDimension(
                value,
                NoDiagnosticCaptureDimensionOverride);
        }

        private static float NormalizeDiagnosticScreenshotPaddingOverride(float value)
        {
            return VisualComparisonScreenshotOverridePolicy.NormalizePadding(
                value,
                NoDiagnosticScreenshotFramingOverride);
        }

        private static float NormalizeDiagnosticScreenshotVerticalViewportCenterOverride(float value)
        {
            return VisualComparisonScreenshotOverridePolicy.NormalizeVerticalViewportCenter(
                value,
                NoDiagnosticScreenshotFramingOverride);
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
                : null;
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
            PersistedState state = BuildCurrentPersistedState();
            VisualComparisonRunStateStore.SaveJson(RunnerStateSessionKey, JsonUtility.ToJson(state));
        }

        private static PersistedState BuildCurrentPersistedState()
        {
            return new PersistedState
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
                manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride =
                    _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride =
                    _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride =
                    _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                manualAnimatorFullBodyPoseReferenceFrameGateStart =
                    _manualAnimatorFullBodyPoseReferenceFrameGateStart,
                manualAnimatorFullBodyPoseReferenceFrameGateEnd =
                    _manualAnimatorFullBodyPoseReferenceFrameGateEnd,
                enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride =
                    _enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride,
                setHumanPoseRightLegTwistOutputReferenceWeight =
                    _setHumanPoseRightLegTwistOutputReferenceWeight,
                setHumanPoseRightLegTwistOutputReferenceMaxDelta =
                    _setHumanPoseRightLegTwistOutputReferenceMaxDelta,
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
                enableRetargetArmStretchClampRuntimeOverride =
                    _enableRetargetArmStretchClampRuntimeOverride,
                retargetArmStretchMuscleLimit = _retargetArmStretchMuscleLimit,
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
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition =
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
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition =
                    _preSetHumanPoseEndpointPositionUseLeftSide,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                    _preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX =
                    _preSetHumanPoseEndpointPositionInvertBodyPositionX,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ =
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
            _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride =
                state.manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride =
                state.manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride =
                state.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride;
            _manualAnimatorFullBodyPoseReferenceFrameGateStart = Mathf.Max(
                0f,
                NormalizeFiniteFloat(
                    state.manualAnimatorFullBodyPoseReferenceFrameGateStart,
                    DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart));
            _manualAnimatorFullBodyPoseReferenceFrameGateEnd = Mathf.Max(
                0f,
                NormalizeFiniteFloat(
                    state.manualAnimatorFullBodyPoseReferenceFrameGateEnd,
                    DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd));
            _enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride =
                state.enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride;
            _setHumanPoseRightLegTwistOutputReferenceWeight = Mathf.Clamp01(NormalizeFiniteFloat(
                state.setHumanPoseRightLegTwistOutputReferenceWeight,
                DefaultSetHumanPoseRightLegTwistOutputReferenceWeight));
            _setHumanPoseRightLegTwistOutputReferenceMaxDelta = Mathf.Max(0f, NormalizeFiniteFloat(
                state.setHumanPoseRightLegTwistOutputReferenceMaxDelta,
                DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta));
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
            _enableRetargetArmStretchClampRuntimeOverride =
                state.enableRetargetArmStretchClampRuntimeOverride;
            _retargetArmStretchMuscleLimit = Mathf.Clamp(
                NormalizePositiveFloat(
                    state.retargetArmStretchMuscleLimit,
                    DefaultRetargetArmStretchMuscleLimit),
                0f,
                DefaultRetargetArmStretchMuscleLimit);
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
                state.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
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
                state.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            _preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                state.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            _preSetHumanPoseEndpointPositionInvertBodyPositionX =
                state.ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
            _preSetHumanPoseEndpointPositionInvertBodyPositionZ =
                state.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
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

        private static bool TryResolveKnownMmdReferenceTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate,
            out int referenceTargetFrameCount)
        {
            return ReferenceFrameCountResolver.TryResolveKnownReference(
                fbxFileName,
                requestedDurationSeconds,
                configuredTargetFrameCount,
                referenceClipLengthSeconds,
                recordingFrameRate,
                SatisfactionReferenceOutputBaseName,
                SatisfactionReferenceMaxMmdFrame,
                out referenceTargetFrameCount);
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
            return VisualComparisonFrameQualityFailurePolicy.IsRawCandidateRole(summary);
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
            return VisualComparisonArtifactPathResolver.ResolveArtifactPath(
                path,
                _projectRoot,
                baseDirectory);
        }

        private static bool PathsReferToSameFile(string leftPath, string rightPath)
        {
            return VisualComparisonArtifactPathResolver.ReferToSameFile(leftPath, rightPath);
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

        private static bool IsGroundingVerticalStepAtMax(float stepToMaxRatio)
        {
            return VisualComparisonMetricCalculator.IsGroundingStepAtMax(stepToMaxRatio);
        }

        private static int CalculateMetricIntSpan(int first, int finish)
        {
            return VisualComparisonMetricCalculator.CalculateIntSpan(first, finish);
        }

        private static float CalculateMetricFloatSpan(float first, float finish)
        {
            return VisualComparisonMetricCalculator.CalculateFloatSpan(first, finish);
        }

        internal static string[] SplitSimpleCsvLine(string line)
        {
            return VisualComparisonCsvMetricReader.SplitLine(line);
        }

        private static Dictionary<string, int> BuildCsvIndexMap(string[] headers)
        {
            return VisualComparisonCsvMetricReader.BuildIndexMap(headers);
        }

        private static string GetCsvString(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return VisualComparisonCsvMetricReader.ReadString(row, indices, column);
        }

        private static int GetCsvInt(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return VisualComparisonCsvMetricReader.ReadInt(row, indices, column);
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

        private static string NormalizeFbxFileName(string fbxFileName)
        {
            return FbxReferenceClipPathResolver.NormalizeFileName(fbxFileName, DefaultFbxFileName);
        }

        private static string GetCommandLineValue(string name, string fallbackValue)
        {
            return CommandLineOptionReader.ReadValue(
                Environment.GetCommandLineArgs(),
                name,
                fallbackValue);
        }

        private static float GetCommandLineFloat(string name, float fallbackValue)
        {
            return CommandLineOptionReader.ReadFloat(
                Environment.GetCommandLineArgs(),
                name,
                fallbackValue);
        }

        private static int GetCommandLineInt(string name, int fallbackValue)
        {
            return CommandLineOptionReader.ReadInt(
                Environment.GetCommandLineArgs(),
                name,
                fallbackValue);
        }

        private static bool GetCommandLineBool(string name, bool fallbackValue)
        {
            return CommandLineOptionReader.ReadBool(
                Environment.GetCommandLineArgs(),
                name,
                fallbackValue);
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
        private sealed class SummaryCandidateArtifactSelection : VisualComparisonCandidateArtifactSelectionData
        {
        }

        [Serializable]
        private sealed class IntegratedVerticalSolvePrimaryExportManifest
        {
            public string raw_diagnostic_vmd_path = string.Empty;
        }

        [Serializable]
        internal sealed class SummaryFrameRoleDiagnostics : VisualComparisonFrameRoleDiagnosticsData
        {
            [NonSerialized]
            public readonly List<ReferenceMp4FrameMetricRow> referenceMp4CurrentClipRows =
                new List<ReferenceMp4FrameMetricRow>();
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

