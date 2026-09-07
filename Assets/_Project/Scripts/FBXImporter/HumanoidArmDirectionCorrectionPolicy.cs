namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 팔 구간별 방향 보정 적용 여부를 결정함.
    /// </summary>
    internal static class HumanoidArmDirectionCorrectionPolicy
    {
        private const float MaximumPreservedForearmErrorDegrees = 2f;

        internal static bool ShouldApplyForearmCorrection(float errorDegrees)
        {
            return IsFinite(errorDegrees) &&
                errorDegrees > MaximumPreservedForearmErrorDegrees;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
