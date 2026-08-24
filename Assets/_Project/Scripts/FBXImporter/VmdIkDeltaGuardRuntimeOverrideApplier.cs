namespace Fbx2Vmd.FBXImporter
{
    internal static class VmdIkDeltaGuardRuntimeOverrideApplier
    {
        internal static bool Apply(
            UnityHumanoidVMDRecorder recorder,
            float limitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd,
            int recoveryHoldFrames)
        {
            float normalizedLimit = NormalizeLimit(limitVmd);
            if (recorder == null || !HasLimit(normalizedLimit))
            {
                return false;
            }

            recorder.ClampMmdIkExportDeltaSpikes = true;
            float normalizedRecoveryTrigger = NormalizeLimit(recoveryTriggerVmd);
            if (HasLimit(normalizedRecoveryTrigger))
            {
                recorder.UseMmdIkExportDeltaRecoveryLimit = true;
                recorder.MmdIkExportDeltaRecoveryLimitPerFrame = normalizedLimit;
                recorder.MmdIkExportDeltaRecoveryTriggerPerFrame = normalizedRecoveryTrigger;
                recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame =
                    NormalizeLimit(recoveryDebtThresholdVmd);
                recorder.MmdIkExportDeltaRecoveryHoldFrames = NormalizeRecoveryHoldFrames(recoveryHoldFrames);
                return true;
            }

            recorder.UseMmdIkExportDeltaRecoveryLimit = false;
            recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame = 0f;
            recorder.MmdIkExportDeltaRecoveryHoldFrames = 0;
            recorder.MaxMmdFootIkExportDeltaPerFrame = normalizedLimit;
            recorder.MaxMmdToeIkExportDeltaPerFrame = normalizedLimit;
            return true;
        }

        internal static float NormalizeLimit(float value)
        {
            return HasLimit(value) ? value : float.NaN;
        }

        internal static bool HasLimit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        internal static int NormalizeRecoveryHoldFrames(int value)
        {
            return value > 0 ? value : 0;
        }
    }
}
