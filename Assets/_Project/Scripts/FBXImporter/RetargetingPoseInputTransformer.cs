using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class RetargetingPoseInputTransformer
    {
        internal static void TransformInPlace(float[] muscleValues)
        {
            if (muscleValues == null)
            {
                return;
            }

            for (int i = 0; i < muscleValues.Length; i++)
            {
                muscleValues[i] = RetargetingMuscleReferencePolicy.TransformPoseInputValue(
                    i,
                    muscleValues[i]);
            }
        }

        internal static void AlignWithReferenceCurvesInPlace(
            float[] muscleValues,
            Dictionary<int, AnimationCurve> referenceCurves,
            float time)
        {
            if (muscleValues == null || referenceCurves == null || referenceCurves.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, AnimationCurve> pair in referenceCurves)
            {
                if (pair.Key < 0 || pair.Key >= muscleValues.Length || pair.Value == null)
                {
                    continue;
                }

                float referenceValue = pair.Value.Evaluate(time);
                muscleValues[pair.Key] = RetargetingMuscleReferencePolicy.AlignPoseInputWithReference(
                    pair.Key,
                    muscleValues[pair.Key],
                    referenceValue);
            }
        }
    }
}
