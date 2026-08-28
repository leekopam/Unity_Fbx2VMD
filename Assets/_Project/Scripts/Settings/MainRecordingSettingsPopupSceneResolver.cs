using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    internal static class MainRecordingSettingsPopupSceneResolver
    {
        internal static MainRecordingSettingsPopup EnsurePopup(RecordingSetting owner)
        {
            Canvas canvas = ResolveCanvas(owner);
            MainRecordingSettingsPopup popup =
                canvas.GetComponentInChildren<MainRecordingSettingsPopup>(true);
            if (popup == null)
            {
                var popupObject = new GameObject(
                    MainRecordingSettingsLayoutSpec.PopupObjectName,
                    typeof(RectTransform));
                popupObject.layer = canvas.gameObject.layer;
                popupObject.transform.SetParent(canvas.transform, false);
                popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
            }

            popup.Bind(owner, MainRecordingSettingsActions.ResolveFBXVmdPipeline(owner));
            popup.EnsureBuilt();
            return popup;
        }

        private static Canvas ResolveCanvas(RecordingSetting owner)
        {
            Canvas canvas = owner != null ? owner.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                return canvas;
            }

            GameObject canvasObject = GameObject.Find(
                MainRecordingSettingsLayoutSpec.CanvasObjectName);
            canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
            if (canvas != null)
            {
                return canvas;
            }

            var fallbackObject = new GameObject(
                MainRecordingSettingsLayoutSpec.CanvasObjectName,
                typeof(RectTransform));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                fallbackObject.layer = uiLayer;
            }

            canvas = fallbackObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fallbackObject.AddComponent<CanvasScaler>();
            fallbackObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }
    }
}
