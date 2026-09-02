using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class HumanoidMuscleRangeLimiter
    {
        internal static int ClampNonFingerMusclesInPlace(float[] muscles)
        {
            if (muscles == null)
            {
                return 0;
            }

            int changed = 0;
            int count = Mathf.Min(muscles.Length, HumanTrait.MuscleCount);
            for (int i = 0; i < count; i++)
            {
                if (IsFingerMuscle(HumanTrait.MuscleName[i]))
                {
                    continue;
                }

                float before = muscles[i];
                float after = Mathf.Clamp(before, -1f, 1f);
                if (Mathf.Approximately(before, after))
                {
                    continue;
                }

                muscles[i] = after;
                changed++;
            }

            return changed;
        }

        private static bool IsFingerMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            return muscleName.IndexOf("thumb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   muscleName.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   muscleName.IndexOf("middle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   muscleName.IndexOf("ring", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   muscleName.IndexOf("little", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
