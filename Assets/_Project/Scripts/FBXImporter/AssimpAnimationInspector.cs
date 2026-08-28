using Assimp;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class AssimpAnimationInspector
    {
        internal static AssimpFBXImporter.AnimationInspectionReport Inspect(
            string path,
            PostProcessSteps postProcessSteps)
        {
            var report = new AssimpFBXImporter.AnimationInspectionReport
            {
                FileReadable = !string.IsNullOrEmpty(path) && File.Exists(path)
            };

            if (!report.FileReadable)
            {
                report.ErrorMessage = $"FBX file not found: {path}";
                return report;
            }

            if (!AssimpLibraryLoader.IsLoaded)
            {
                AssimpLibraryLoader.LoadLibrary();
            }

            try
            {
                using (AssimpContext importer = new AssimpContext())
                {
                    importer.SetConfig(new Assimp.Configs.FBXPreservePivotsConfig(false));

                    Scene scene = importer.ImportFile(path, postProcessSteps);
                    if (scene == null)
                    {
                        report.ErrorMessage = "Assimp returned a null scene.";
                        return report;
                    }

                    PopulateReport(scene, report);
                    return report;
                }
            }
            catch (System.Exception e)
            {
                report.ErrorMessage = NormalizeErrorMessage(e.Message);
                return report;
            }
        }

        internal static void PopulateReport(
            Scene scene,
            AssimpFBXImporter.AnimationInspectionReport report)
        {
            report.ImportSucceeded = true;
            report.AnimationCount = scene.AnimationCount;

            var names = new List<string>();
            var lengths = new List<string>();
            foreach (var animation in scene.Animations)
            {
                string animationName = string.IsNullOrWhiteSpace(animation.Name)
                    ? $"Animation_{names.Count}"
                    : animation.Name;
                names.Add(animationName);

                float duration = CalculateDurationSeconds(animation);
                report.MaxAnimationLengthSeconds = Mathf.Max(report.MaxAnimationLengthSeconds, duration);
                lengths.Add(duration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

                report.NodeAnimationChannelCount += animation.NodeAnimationChannelCount;
                foreach (var channel in animation.NodeAnimationChannels)
                {
                    report.PositionKeyCount += channel.PositionKeyCount;
                    report.RotationKeyCount += channel.RotationKeyCount;
                    report.ScaleKeyCount += channel.ScalingKeyCount;
                }
            }

            report.AnimationNames = string.Join("|", names);
            report.AnimationLengthsSeconds = string.Join("|", lengths);
        }

        internal static string NormalizeErrorMessage(string message)
        {
            return message.Replace('\r', ' ').Replace('\n', ' ');
        }

        internal static float CalculateDurationSeconds(Assimp.Animation animation)
        {
            if (animation == null)
            {
                return 0f;
            }

            double ticksPerSecond = animation.TicksPerSecond;
            if (ticksPerSecond <= 1.0)
            {
                ticksPerSecond = 60.0;
            }

            if (ticksPerSecond <= 0.0)
            {
                return 0f;
            }

            double duration = animation.DurationInTicks / ticksPerSecond;
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0.0)
            {
                return 0f;
            }

            float durationSeconds = (float)duration;
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds))
            {
                return 0f;
            }

            return durationSeconds;
        }
    }
}
