#if UNITY_EDITOR
using UnityEditor;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 에디터 Humanoid 모션이 원본 root 회전을 보존하도록 clip 임포트 계약을 적용함.
    /// </summary>
    internal static class HumanoidClipImportPolicy
    {
        internal static bool HasRootRotationContract(
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
                    !clip.keepOriginalOrientation)
                {
                    return false;
                }
            }

            return true;
        }

        internal static void ApplyRootRotationContract(
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

                // 재생기가 root motion을 쓰지 않으므로 원본 회전을 본 자세에 보존함.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
            }
        }
    }
}
#endif
