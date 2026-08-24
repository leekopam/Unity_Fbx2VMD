using RootMotion.FinalIK;

namespace Fbx2Vmd.FBXImporter
{
    internal static class FinalIkFootGroundingRuntimeOverrideApplier
    {
        internal static bool Apply(FBXVmdPipeline pipeline, bool enabled)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.enableFinalIkFootGroundingExperiment = enabled;

            if (!enabled && pipeline.targetCharacter != null)
            {
                GrounderBipedIK grounder = pipeline.targetCharacter.GetComponent<GrounderBipedIK>();
                if (grounder != null)
                {
                    grounder.weight = 0f;
                    grounder.enabled = false;
                }

                BipedIK bipedIk = pipeline.targetCharacter.GetComponent<BipedIK>();
                if (bipedIk != null)
                {
                    bipedIk.fixTransforms = false;
                    bipedIk.enabled = false;
                }
            }

            return true;
        }
    }
}
