#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static partial class YybVisualComparisonBatchRunner
    {
        private sealed class CaptureJob
        {
            public CaptureMode Mode;
            public string ScenePath;
            public string SceneName;
            public string DisplayName;
            public string ManualTargetNameToken;
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
    }
}
#endif
