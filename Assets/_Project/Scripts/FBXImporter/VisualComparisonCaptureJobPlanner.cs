#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal enum VisualComparisonCaptureRole
    {
        ManualReference,
        ManualTarget,
        DirectRecording,
        PlaybackProbe,
        Automatic
    }

    internal sealed class VisualComparisonScene
    {
        public VisualComparisonScene(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("씬 경로가 필요합니다.", nameof(path));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("씬 이름이 필요합니다.", nameof(name));
            }

            Path = path;
            Name = name;
        }

        public string Path { get; }
        public string Name { get; }
    }

    internal sealed class VisualComparisonCaptureProfile
    {
        public VisualComparisonCaptureProfile(
            string modelDisplayName,
            string manualReferenceDisplayName,
            string manualReferenceTargetNameToken,
            string manualTargetNameToken,
            VisualComparisonScene manualScene,
            VisualComparisonScene recordingScene,
            VisualComparisonScene automaticScene)
        {
            ModelDisplayName = RequireText(modelDisplayName, nameof(modelDisplayName));
            ManualReferenceDisplayName = RequireText(
                manualReferenceDisplayName,
                nameof(manualReferenceDisplayName));
            ManualReferenceTargetNameToken = RequireText(
                manualReferenceTargetNameToken,
                nameof(manualReferenceTargetNameToken));
            ManualTargetNameToken = RequireText(manualTargetNameToken, nameof(manualTargetNameToken));
            ManualScene = manualScene ?? throw new ArgumentNullException(nameof(manualScene));
            RecordingScene = recordingScene ?? throw new ArgumentNullException(nameof(recordingScene));
            AutomaticScene = automaticScene ?? throw new ArgumentNullException(nameof(automaticScene));
        }

        public string ModelDisplayName { get; }
        public string ManualReferenceDisplayName { get; }
        public string ManualReferenceTargetNameToken { get; }
        public string ManualTargetNameToken { get; }
        public VisualComparisonScene ManualScene { get; }
        public VisualComparisonScene RecordingScene { get; }
        public VisualComparisonScene AutomaticScene { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("값이 필요합니다.", parameterName);
            }

            return value;
        }
    }

    internal sealed class VisualComparisonCaptureJob
    {
        public VisualComparisonCaptureJob(
            VisualComparisonCaptureRole role,
            VisualComparisonScene scene,
            string displayName,
            string targetNameToken)
        {
            Role = role;
            ScenePath = scene.Path;
            SceneName = scene.Name;
            DisplayName = displayName;
            TargetNameToken = targetNameToken;
        }

        public VisualComparisonCaptureRole Role { get; }
        public string ScenePath { get; }
        public string SceneName { get; }
        public string DisplayName { get; }
        public string TargetNameToken { get; }
    }

    internal static class VisualComparisonCaptureJobPlanner
    {
        internal static VisualComparisonCaptureJob[] Build(
            VisualComparisonCaptureProfile profile,
            bool includePlaybackProbe)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var jobs = new List<VisualComparisonCaptureJob>(includePlaybackProbe ? 5 : 4)
            {
                CreateJob(
                    VisualComparisonCaptureRole.ManualReference,
                    profile.ManualScene,
                    $"{profile.ManualScene.Name} {profile.ManualReferenceDisplayName} 수동 기준",
                    profile.ManualReferenceTargetNameToken),
                CreateJob(
                    VisualComparisonCaptureRole.ManualTarget,
                    profile.ManualScene,
                    $"{profile.ManualScene.Name} {profile.ModelDisplayName} 수동 기준",
                    profile.ManualTargetNameToken),
                CreateJob(
                    VisualComparisonCaptureRole.DirectRecording,
                    profile.RecordingScene,
                    $"{profile.RecordingScene.Name} {profile.ModelDisplayName} 자동 경로",
                    string.Empty)
            };

            if (includePlaybackProbe)
            {
                jobs.Add(CreateJob(
                    VisualComparisonCaptureRole.PlaybackProbe,
                    profile.RecordingScene,
                    $"{profile.RecordingScene.Name} {profile.ModelDisplayName} VMD replay probe",
                    string.Empty));
            }

            jobs.Add(CreateJob(
                VisualComparisonCaptureRole.Automatic,
                profile.AutomaticScene,
                $"{profile.AutomaticScene.Name} {profile.ModelDisplayName} 자동 경로",
                string.Empty));

            return jobs.ToArray();
        }

        private static VisualComparisonCaptureJob CreateJob(
            VisualComparisonCaptureRole role,
            VisualComparisonScene scene,
            string displayName,
            string targetNameToken)
        {
            return new VisualComparisonCaptureJob(role, scene, displayName, targetNameToken);
        }
    }
}
#endif
