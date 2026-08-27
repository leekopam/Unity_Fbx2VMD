using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.Settings.EditorTools
{
    [InitializeOnLoad]
    public static class GraphicSettingGameViewScaleAutoApplier
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const double MaintainIntervalSeconds = 0.5d;
        private static bool isScheduled;
        private static double nextMaintainTime;

        static GraphicSettingGameViewScaleAutoApplier()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.update -= MaintainActiveSceneSettingGameViewScale;
            EditorApplication.update += MaintainActiveSceneSettingGameViewScale;
            ScheduleApply();
        }

        public static void ScheduleApply()
        {
            if (isScheduled)
            {
                return;
            }

            isScheduled = true;
            EditorApplication.delayCall += ApplyScheduled;
        }

        public static bool ApplyActiveSceneSettingGameViewScale()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            GraphicSetting setting = FindActiveSceneGraphicSetting();
            return setting != null && GameViewScaleController.TryApply(setting.GameViewScaleMode);
        }

        public static bool ApplyActiveSceneSettingGameViewScaleIfDrifted()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            GraphicSetting setting = FindActiveSceneGraphicSetting();
            if (setting == null || setting.GameViewScaleMode != GraphicGameViewScaleMode.OneX)
            {
                return false;
            }

            return !GameViewScaleController.IsCurrentZoomScale(Vector2.one, 0.001f)
                && GameViewScaleController.TryApply(setting.GameViewScaleMode);
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == MainRecordingScenePath)
            {
                ScheduleApply();
            }
        }

        private static void MaintainActiveSceneSettingGameViewScale()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextMaintainTime)
            {
                return;
            }

            nextMaintainTime = now + MaintainIntervalSeconds;
            ApplyActiveSceneSettingGameViewScaleIfDrifted();
        }

        private static void ApplyScheduled()
        {
            isScheduled = false;
            ApplyActiveSceneSettingGameViewScale();
        }

        private static GraphicSetting FindActiveSceneGraphicSetting()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.path != MainRecordingScenePath)
            {
                return null;
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var setting = root.GetComponent<GraphicSetting>();
                if (setting != null)
                {
                    return setting;
                }

                setting = root.GetComponentInChildren<GraphicSetting>(true);
                if (setting != null)
                {
                    return setting;
                }
            }

            return null;
        }
    }
}
