#if UNITY_EDITOR
using UnityEditor;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 에디터 Humanoid 모션의 루트 회전과 높이 이동을 본 자세에 보존함.
    /// </summary>
    internal static class HumanoidClipImportPolicy
    {
        internal static bool HasRootPoseContract(
            ModelImporterClipAnimation[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip == null ||
                    !clip.lockRootRotation ||
                    !clip.keepOriginalOrientation ||
                    !clip.lockRootHeightY)
                {
                    return false;
                }
            }

            return true;
        }

        internal static void ApplyRootPoseContract(
            ModelImporterClipAnimation[] clips)
        {
            if (clips == null)
            {
                return;
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                // 재생기가 XZ만 직접 복원하므로 회전과 Y 이동은 본 자세에 보존함.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
            }
        }
    }
}
#endif
