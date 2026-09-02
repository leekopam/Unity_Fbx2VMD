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
    }
}
