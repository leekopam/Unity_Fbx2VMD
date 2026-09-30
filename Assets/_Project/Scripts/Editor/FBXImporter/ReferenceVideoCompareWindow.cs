using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 외부에서 추출한 레퍼런스 영상 프레임(PNG 열)과 Unity 출력을 프레임 정렬해
    /// 나란히 비교하는 창임. EditMode에서 AnimationMode 샘플링 + 카메라 렌더로
    /// 같은 프레임의 Unity PNG를 만들고 뷰포트 프레이밍 지표를 CSV로 남김.
    /// 픽셀 일치가 아닌 시각 나란히 비교·프레이밍 계량이 목적임.
    /// </summary>
    public class ReferenceVideoCompareWindow : EditorWindow
    {
        private string _referenceFolder = "";
        private GameObject _target;
        private AnimationClip _clip;
        private Camera _camera;
        private float _frameOffset; // 참고 프레임→클립 프레임 오프셋
        private float _fpsRatio = 1f; // 참고 fps / 클립 fps 비율
        private int _captureWidth = 1280;
        private int _captureHeight = 720;
        private string _message = "";
        private Vector2 _scroll;
        private List<string> _refFiles = new List<string>();

        [MenuItem("Tools/FBXImporter/레퍼런스 영상 정렬 비교")]
        private static void Open() =>
            GetWindow<ReferenceVideoCompareWindow>(false, "영상 정렬 비교");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("레퍼런스 프레임 폴더", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _referenceFolder = EditorGUILayout.TextField(_referenceFolder);
            if (GUILayout.Button("선택", GUILayout.Width(60)))
            {
                string picked = EditorUtility.OpenFolderPanel("PNG 프레임 폴더", "", "");
                if (!string.IsNullOrEmpty(picked)) _referenceFolder = picked;
            }
            EditorGUILayout.EndHorizontal();

            _target = (GameObject)EditorGUILayout.ObjectField(
                "대상 Humanoid", _target, typeof(GameObject), true);
            _clip = (AnimationClip)EditorGUILayout.ObjectField(
                "애니메이션 클립", _clip, typeof(AnimationClip), false);
            _camera = (Camera)EditorGUILayout.ObjectField(
                "카메라(비우면 Main)", _camera, typeof(Camera), true);
            _frameOffset = EditorGUILayout.FloatField("프레임 오프셋", _frameOffset);
            _fpsRatio = EditorGUILayout.FloatField("fps 비율(영상/클립)", _fpsRatio);
            EditorGUILayout.BeginHorizontal();
            _captureWidth = EditorGUILayout.IntField("캡처 너비", _captureWidth);
            _captureHeight = EditorGUILayout.IntField("높이", _captureHeight);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("프레임 목록 읽기") && !string.IsNullOrWhiteSpace(_referenceFolder))
            {
                LoadReferenceFrames();
            }
            EditorGUILayout.LabelField($"참고 프레임 {_refFiles.Count}장", EditorStyles.miniLabel);

            string validation = Validate();
            if (validation != null)
            {
                EditorGUILayout.HelpBox(validation, MessageType.None);
            }
            using (new EditorGUI.DisabledScope(validation != null))
            {
                if (GUILayout.Button("정렬 캡처 + 지표 CSV"))
                {
                    RunComparison();
                }
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(120));
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private void LoadReferenceFrames()
        {
            try
            {
                _refFiles = Directory.GetFiles(_referenceFolder, "*.png")
                    .Concat(Directory.GetFiles(_referenceFolder, "*.jpg"))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception error)
            {
                _message = $"프레임 읽기 실패: {error.Message}";
            }
        }

        private string Validate()
        {
            if (_refFiles.Count == 0) return "레퍼런스 프레임을 먼저 읽으세요.";
            if (_target == null || _target.GetComponent<Animator>() == null ||
                !_target.GetComponent<Animator>().isHuman)
                return "Humanoid Animator 대상이 필요합니다.";
            if (_clip == null) return "애니메이션 클립이 필요합니다.";
            if (_fpsRatio <= 0f) return "fps 비율이 0보다 커야 합니다.";
            return null;
        }

        private void RunComparison()
        {
            Camera camera = _camera != null ? _camera : Camera.main;
            if (camera == null)
            {
                _message = "씬에 카메라가 없습니다.";
                return;
            }
            float frameRate = _clip.frameRate > 0f ? _clip.frameRate : 30f;
            int clipLastFrame = Mathf.CeilToInt(_clip.length * frameRate);
            string outputDir = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "video-compare",
                $"{_clip.name}-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(outputDir);
            Renderer[] renderers = _target.GetComponentsInChildren<Renderer>(true);

            bool sampling = false;
            int captured = 0;
            try
            {
                AnimationMode.StartAnimationMode();
                sampling = true;
                string csvPath = Path.Combine(outputDir, "compare.csv");
                using (var writer = new StreamWriter(csvPath, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine(
                        "ref_index,ref_file,clip_frame,unity_png," +
                        "center_x,center_y,height,top,bottom");
                    for (int index = 0; index < _refFiles.Count; index++)
                    {
                        // 참고 프레임 i ↔ 시각 i/영상fps ↔ 클립 프레임 i·클립fps/영상fps
                        // _fpsRatio는 영상/클립이므로 나눗셈으로 변환한다.
                        // CSV의 clip_frame이 실제 샘플된 프레임과 같게 유효 범위로 클램프함.
                        int clipFrame = Mathf.Clamp(
                            Mathf.RoundToInt(index / _fpsRatio + _frameOffset),
                            0, clipLastFrame);
                        float time = Mathf.Min(clipFrame / frameRate, _clip.length);
                        AnimationMode.SampleAnimationClip(_target, _clip, time);
                        string png = Path.Combine(outputDir, $"unity-{index:000000}-f{clipFrame}.png");
                        CaptureCameraPng(camera, png, _captureWidth, _captureHeight);
                        // 렌더러 바운드를 뷰포트 좌표로 투영해 프레이밍 지표를 계산함.
                        Vector4 bounds = ViewportBounds(renderers, camera);
                        writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "{0},\"{1}\",{2},\"{3}\",{4:F4},{5:F4},{6:F4},{7:F4},{8:F4}",
                            index, Path.GetFileName(_refFiles[index]), clipFrame,
                            Path.GetFileName(png),
                            bounds.x, bounds.y, bounds.z, bounds.w, bounds.y - bounds.z * 0.5f));
                        captured++;
                        if (index % 10 == 0)
                        {
                            if (EditorUtility.DisplayCancelableProgressBar("영상 정렬 비교",
                                    $"프레임 {index}/{_refFiles.Count}",
                                    (float)index / _refFiles.Count))
                                break;
                        }
                    }
                }
                // 참고 프레임 복사본도 같은 폴더에 두면 나란히 비교가 쉬움.
                string refDir = Path.Combine(outputDir, "reference");
                Directory.CreateDirectory(refDir);
                // CSV 행과 1:1 대응이 되도록 캡처된 프레임만 복사한다(중도 취소 시 불일치 방지).
                for (int index = 0; index < captured; index++)
                {
                    File.Copy(_refFiles[index],
                        Path.Combine(refDir, $"ref-{index:000000}{Path.GetExtension(_refFiles[index])}"),
                        overwrite: true);
                }
                _message = $"비교 산출 저장: {outputDir} ({captured}프레임)";
                EditorUtility.RevealInFinder(outputDir);
            }
            catch (Exception error)
            {
                _message = $"비교 실패: {error.Message}";
            }
            finally
            {
                if (sampling) AnimationMode.StopAnimationMode();
                EditorUtility.ClearProgressBar();
            }
        }

        private static void CaptureCameraPng(Camera camera, string file, int width, int height)
        {
            var renderTexture = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(file, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // 뷰포트 공간 (x=중심x, y=중심y, z=높이, w=상단). 카메라 뒤면 NaN.
        private static Vector4 ViewportBounds(Renderer[] renderers, Camera camera)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f,
                            (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 viewport = camera.WorldToViewportPoint(corner);
                    if (viewport.z < 0f) continue;
                    min = Vector3.Min(min, viewport);
                    max = Vector3.Max(max, viewport);
                    any = true;
                }
            }
            if (!any) return new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
            return new Vector4(
                (min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f,
                max.y - min.y, max.y);
        }
    }
}
