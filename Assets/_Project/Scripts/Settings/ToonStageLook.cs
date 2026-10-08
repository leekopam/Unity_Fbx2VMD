using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Fbx2Vmd.Settings
{
    /// <summary>
    /// 무대 공연 룩 전역 볼륨 자동 설치.
    /// 씬에 PostProcessLayer가 붙은 카메라가 있고
    /// Resources/ToonPresets/ToonStageProfile이 존재하면,
    /// 재생 시작 시 전역 PostProcessVolume을 하나 만든다.
    /// 에디터와 플레이어 빌드에서 동일하게 동작한다.
    /// </summary>
    public static class ToonStageLook
    {
        const string ProfileResourcePath = "ToonPresets/ToonStageProfile";
        const string VolumeObjectName = "ToonStageLookVolume";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureVolume()
        {
            // 프로필 에셋 자체가 옵트인이다. PostProcessLayer가 없는 씬에서는
            // 볼륨이 있어도 렌더링에 영향이 없으므로 생성만 해둔다.
            var profile = Resources.Load<PostProcessProfile>(ProfileResourcePath);
            if (profile == null)
            {
                return;
            }

            foreach (PostProcessVolume volume in Object.FindObjectsOfType<PostProcessVolume>(true))
            {
                if (volume.isGlobal)
                {
                    return;
                }
            }

            var go = new GameObject(VolumeObjectName);
            var stageVolume = go.AddComponent<PostProcessVolume>();
            stageVolume.isGlobal = true;
            stageVolume.priority = 0f;
            stageVolume.weight = 1f;
            stageVolume.sharedProfile = profile;
            Object.DontDestroyOnLoad(go);
        }
    }
}
