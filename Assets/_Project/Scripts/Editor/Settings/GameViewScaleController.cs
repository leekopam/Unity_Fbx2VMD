using System;
using System.Reflection;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    public static class GameViewScaleController
    {
        public static bool IsCurrentZoomScale(Vector2 expected, float tolerance)
        {
            if (!TryGetCurrentZoomScale(out Vector2 current))
            {
                return false;
            }

            return Mathf.Abs(current.x - expected.x) <= tolerance
                && Mathf.Abs(current.y - expected.y) <= tolerance;
        }

        public static bool TryApply(GraphicGameViewScaleMode mode)
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                Debug.LogWarning("GraphicSetting에서 UnityEditor.GameView를 찾을 수 없습니다.");
                return false;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView == null)
            {
                return false;
            }

            gameView.Show();
            if (mode == GraphicGameViewScaleMode.OneX && TrySetZoomAreaScale(gameView, Vector2.one))
            {
                gameView.Repaint();
                return true;
            }

            if (mode == GraphicGameViewScaleMode.Fit && TryInvokeSizeSelection(gameView, 0))
            {
                gameView.Repaint();
                return true;
            }

            Debug.LogWarning("GraphicSetting에서 현재 Unity 버전의 GameView 스케일 표시를 변경할 수 없습니다.");
            return false;
        }

        private static bool TryGetCurrentZoomScale(out Vector2 scale)
        {
            scale = Vector2.zero;
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return false;
            }

            EditorWindow gameView = FindOpenGameView(gameViewType);
            return gameView != null && TryGetZoomAreaScale(gameView, out scale);
        }

        private static EditorWindow FindOpenGameView(Type gameViewType)
        {
            UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
            foreach (UnityEngine.Object gameView in gameViews)
            {
                if (gameView is EditorWindow window)
                {
                    return window;
                }
            }

            return null;
        }

        private static bool TryGetZoomAreaScale(EditorWindow gameView, out Vector2 scale)
        {
            scale = Vector2.zero;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo zoomAreaField = gameView.GetType().GetField("m_ZoomArea", flags);
            object zoomArea = zoomAreaField?.GetValue(gameView);
            if (zoomArea == null)
            {
                return false;
            }

            FieldInfo scaleField = zoomArea.GetType().GetField("m_Scale", flags);
            if (scaleField == null || scaleField.FieldType != typeof(Vector2))
            {
                return false;
            }

            scale = (Vector2)scaleField.GetValue(zoomArea);
            return true;
        }

        private static bool TrySetZoomAreaScale(EditorWindow gameView, Vector2 scale)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo zoomAreaField = gameView.GetType().GetField("m_ZoomArea", flags);
            object zoomArea = zoomAreaField?.GetValue(gameView);
            if (zoomArea == null)
            {
                return false;
            }

            FieldInfo scaleField = zoomArea.GetType().GetField("m_Scale", flags);
            if (scaleField == null || scaleField.FieldType != typeof(Vector2))
            {
                return false;
            }

            scaleField.SetValue(zoomArea, scale);
            return true;
        }

        private static bool TryInvokeSizeSelection(EditorWindow gameView, int index)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = gameView.GetType().GetMethod("SizeSelectionCallback", flags);
            if (method == null)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 2)
            {
                method.Invoke(gameView, new object[] { index, null });
                return true;
            }

            if (parameters.Length == 1)
            {
                method.Invoke(gameView, new object[] { index });
                return true;
            }

            return false;
        }
    }
}
