using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class RuntimeHumanoidReferencePoseApplier
    {
        private const float ClipStartTimeSeconds = 0f;

        internal static bool TryApply(GameObject root, AnimationClip clip)
        {
            if (root == null || clip == null)
            {
                return false;
            }

            // Assimp 계층에서 빠진 FBX 초기 로컬 자세를 첫 유효 샘플로 복원한 뒤 Avatar를 생성함.
            clip.SampleAnimation(root, ClipStartTimeSeconds);
            return true;
        }
    }
}
