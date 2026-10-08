using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 하체 접촉 보정을 사람이 프레임 단위로 뜯어보는 시각 검사 창임.
    /// 배치 측정 윈도우(FootContactDriftDiagnosticsWindow)가 수치 리포트를 만든다면
    /// 이 창은 모델 인스턴스를 띄워 프레임 슬라이더로 Seek하고 Scene view에
    /// 앵커·접촉점·의도 방침·게이트 수치를 직접 그려 눈으로 확인하게 함.
    /// </summary>
    internal sealed class FootContactInspectWindow : EditorWindow
    {
        private const string DefaultClipPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string DefaultModelPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const float AnchorGizmoSize = 0.035f;
        private const float PointGizmoSize = 0.018f;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        private string _clipPath = DefaultClipPath;
        private string _modelPath = DefaultModelPath;
        private bool _groundResponseEnabled = true;
        private bool _drawLabels = true;

        private GameObject _instance;
        private Animator _animator;
        private Transform _leftFoot;
        private Transform _leftToes;
        private Transform _rightFoot;
        private Transform _rightToes;
        private object _controller;
        private object _intentEstimate;
        private float _frameRate = 60f;
        private float _clipLength;
        private int _lastFrame;
        private int _frame;
        private bool _playing;
        private double _lastUpdateTime;
        private string _status = "준비되지 않음 — 클립과 모델을 고르고 [준비]를 누름.";

        [MenuItem("Tools/FBXImporter/하체 접촉 시각 검사")]
        private static void Open()
        {
            var window = GetWindow<FootContactInspectWindow>(false, "하체 접촉 시각 검사");
            window.minSize = new Vector2(400f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            ReleaseRig();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
            _clipPath = EditorGUILayout.TextField("소스 클립 FBX", _clipPath);
            _modelPath = EditorGUILayout.TextField("대상 모델 FBX", _modelPath);

            using (new EditorGUI.DisabledScope(_controller != null))
            {
                _groundResponseEnabled = EditorGUILayout.ToggleLeft(
                    "지면 응답(다리 도달·골반 게이트) 켜기", _groundResponseEnabled);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_controller != null))
                {
                    if (GUILayout.Button("준비"))
                    {
                        PrepareRig();
                    }
                }
                using (new EditorGUI.DisabledScope(_controller == null))
                {
                    if (GUILayout.Button("해제"))
                    {
                        ReleaseRig();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(_controller == null))
            {
                EditorGUILayout.LabelField("재생", EditorStyles.boldLabel);
                int wanted = EditorGUILayout.IntSlider(
                    $"프레임 ({_frame}/{_lastFrame})", _frame, 0, Mathf.Max(0, _lastFrame));
                if (wanted != _frame)
                {
                    SeekFrame(wanted);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("◀")) SeekFrame(_frame - 1);
                    _playing = GUILayout.Toggle(_playing,
                        _playing ? "■ 정지" : "▶ 재생", GUI.skin.button);
                    if (GUILayout.Button("▶")) SeekFrame(_frame + 1);
                    if (GUILayout.Button("처음")) SeekFrame(0);
                }
                _drawLabels = EditorGUILayout.ToggleLeft("Scene 라벨 표시", _drawLabels);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("상태", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void OnEditorUpdate()
        {
            // 씬 전환·플레이 진입으로 HideAndDontSave 인스턴스가 소멸하면 죽은
            // 참조를 든 채 Seek하므로 여기서 해제함.
            if (_controller != null && _animator == null)
            {
                ReleaseRig();
                return;
            }
            if (!_playing || _controller == null)
            {
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double delta = now - _lastUpdateTime;
            _lastUpdateTime = now;
            // 에디터 틱은 불규칙하므로 1틱에 최대 4프레임까지만 진행해
            // 사람 눈으로 접촉 전이를 따라갈 수 있게 함.
            int steps = Mathf.Clamp((int)(delta * _frameRate), 1, 4);
            SeekFrame(Mathf.Min(_frame + steps, _lastFrame));
            if (_frame >= _lastFrame)
            {
                _playing = false;
            }
        }

        private void SeekFrame(int frame)
        {
            if (_controller == null)
            {
                return;
            }

            _frame = Mathf.Clamp(frame, 0, _lastFrame);
            try
            {
                Invoke(_controller, "Seek",
                    Mathf.Min(_frame / _frameRate, _clipLength));
            }
            catch (TargetInvocationException exception)
            {
                // 재생 중 에디터 틱에서 반복 throw되지 않게 즉시 정지시킴.
                _playing = false;
                _status = "Seek 실패: " + (exception.InnerException?.Message ?? exception.Message);
                Repaint();
                return;
            }
            _status = BuildStatusText();
            SceneView.RepaintAll();
            Repaint();
        }

        private void PrepareRig()
        {
            ReleaseRig();
            try
            {
                EnsureHumanoidClipImport(_clipPath);
                AnimationClip clip = LoadHumanoidClip(_clipPath);
                GameObject sourceAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(_clipPath);
                GameObject modelAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(_modelPath);
                if (clip == null || sourceAsset == null)
                {
                    _status = $"Humanoid 클립을 찾지 못함: {_clipPath}";
                    return;
                }
                if (modelAsset == null)
                {
                    _status = $"모델 asset을 찾지 못함: {_modelPath}";
                    return;
                }

                _instance = Instantiate(modelAsset);
                _instance.name = "접촉 시각 검사";
                _instance.hideFlags = HideFlags.HideAndDontSave;
                _instance.SetActive(true);
                foreach (MonoBehaviour script in
                    _instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    script.enabled = false;
                }

                _animator = _instance.GetComponentInChildren<Animator>(true);
                if (_animator == null || _animator.avatar == null ||
                    !_animator.avatar.isValid || !_animator.avatar.isHuman)
                {
                    throw new InvalidOperationException(
                        $"유효한 Humanoid Avatar가 필요함: {_modelPath}");
                }
                _animator.enabled = true;
                _leftFoot = RequireBone(_animator, HumanBodyBones.LeftFoot);
                _leftToes = RequireBone(_animator, HumanBodyBones.LeftToes);
                _rightFoot = RequireBone(_animator, HumanBodyBones.RightFoot);
                _rightToes = RequireBone(_animator, HumanBodyBones.RightToes);

                _controller = CreateController();
                Invoke(_controller, "PrepareWithArmDirectionReference",
                    _animator, clip, sourceAsset);
                Invoke(_controller, "SetGroundResponseEnabled",
                    _groundResponseEnabled);
                _intentEstimate = GetIntentEstimate(_controller);

                _frameRate = clip.frameRate > 0f ? clip.frameRate : 60f;
                _clipLength = clip.length;
                _lastFrame = Mathf.Max(0, Mathf.CeilToInt(clip.length * _frameRate));
                _frame = 0;
                SeekFrame(0);
            }
            catch (Exception exception)
            {
                _status = "준비 실패: " + exception.Message;
                Debug.LogException(exception);
                ReleaseRig();
            }
        }

        private void ReleaseRig()
        {
            _playing = false;
            if (_controller is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _controller = null;
            _intentEstimate = null;
            if (_instance != null)
            {
                DestroyImmediate(_instance);
                _instance = null;
            }
            _animator = null;
            _leftFoot = _leftToes = _rightFoot = _rightToes = null;
            _status = "준비되지 않음 — 클립과 모델을 고르고 [준비]를 누름.";
            SceneView.RepaintAll();
        }

        // ===== Scene view 시각화 =====

        private void OnSceneGUI(SceneView view)
        {
            if (_controller == null || _animator == null)
            {
                return;
            }

            // 의도 방침 색: Confident Plant=초록, Slide=노랑, Uncertain=주황.
            DrawFoot(_leftFoot, _leftToes, isLeft: true);
            DrawFoot(_rightFoot, _rightToes, isLeft: false);

            // 지면 응답 표면 스냅샷 — 앵커·접촉점·지면 히트를 그림.
            if (TryGetSurfaceSnapshots(out object leftSurface, out object rightSurface))
            {
                DrawSurface(leftSurface, new Color(0.2f, 0.85f, 1f));
                DrawSurface(rightSurface, new Color(1f, 0.55f, 0.2f));
            }

            if (_drawLabels)
            {
                Handles.color = Color.white;
                Handles.Label(_animator.transform.position + Vector3.up * 0.02f,
                    BuildStatusText(), EditorStyles.whiteLabel);
            }
        }

        private void DrawFoot(Transform foot, Transform toes, bool isLeft)
        {
            if (foot == null || toes == null)
            {
                return;
            }

            IntentSpan span = FindIntentSpan(isLeft, _frame);
            Color color = span == null
                ? new Color(0.6f, 0.6f, 0.6f, 0.5f)
                : span.Certainty == "Uncertain"
                    ? new Color(1f, 0.6f, 0.1f, 0.9f)
                    : span.Mode == "Plant"
                        ? new Color(0.2f, 0.9f, 0.3f, 0.9f)
                        : new Color(0.95f, 0.9f, 0.15f, 0.9f);

            Handles.color = color;
            Handles.DrawLine(foot.position, toes.position);
            Handles.SphereHandleCap(-1, foot.position, Quaternion.identity,
                PointGizmoSize, EventType.Repaint);
            Handles.SphereHandleCap(-1, toes.position, Quaternion.identity,
                PointGizmoSize * 0.8f, EventType.Repaint);

            // 의도 구간의 지지 앵커를 표시 — 발이 이 점에 붙어 있어야 함.
            if (span != null)
            {
                Handles.SphereHandleCap(-1, span.Anchor, Quaternion.identity,
                    AnchorGizmoSize, EventType.Repaint);
                Handles.DrawLine(span.Anchor, foot.position);
                if (_drawLabels)
                {
                    Handles.Label(span.Anchor + Vector3.up * 0.01f,
                        $"{(isLeft ? "L" : "R")} {span.Mode}/{span.Certainty} " +
                        $"{span.Start}-{span.End}");
                }
            }
        }

        private void DrawSurface(object surface, Color color)
        {
            bool hasGround = GetBool(surface, "has_ground");
            Vector3 rearAnchor = GetVector(surface, "rear_anchor");
            Vector3 frontAnchor = GetVector(surface, "front_anchor");

            Handles.color = hasGround ? color : new Color(1f, 0.2f, 0.2f, 0.6f);
            // 목표 앵커는 접지 여부와 무관하게 유효해 항상 그림.
            Handles.SphereHandleCap(-1, rearAnchor, Quaternion.identity,
                PointGizmoSize, EventType.Repaint);
            Handles.SphereHandleCap(-1, frontAnchor, Quaternion.identity,
                PointGizmoSize, EventType.Repaint);
            if (!hasGround)
            {
                return;
            }

            // 미접지 스냅샷은 지면·접촉점이 Vector3.zero라 여기부터는 접지일 때만 유효함.
            Vector3 groundPoint = GetVector(surface, "ground_point");
            Vector3 groundNormal = GetVector(surface, "ground_normal");
            Vector3 rearPoint = GetVector(surface, "rear_point");
            Vector3 frontPoint = GetVector(surface, "front_point");
            Handles.DrawSolidDisc(groundPoint, groundNormal, AnchorGizmoSize * 0.8f);
            if (groundNormal.sqrMagnitude > 0.0001f)
            {
                Handles.ArrowHandleCap(-1, groundPoint,
                    Quaternion.LookRotation(groundNormal), 0.08f, EventType.Repaint);
            }
            // 목표 앵커(실선)와 실제 접촉점(와이어)이 어긋나면 틈이 보임.
            Handles.DrawWireDisc(rearPoint, groundNormal, PointGizmoSize);
            Handles.DrawWireDisc(frontPoint, groundNormal, PointGizmoSize);
            Handles.DrawLine(rearAnchor, rearPoint);
            Handles.DrawLine(frontAnchor, frontPoint);
        }

        // ===== 리플렉션 접근 (internal 제품 타입) =====

        private bool TryGetSurfaceSnapshots(out object left, out object right)
        {
            left = right = null;
            MethodInfo method = _controller.GetType().GetMethod(
                "TryCaptureCurrentFootSurface", Flags);
            if (method == null)
            {
                return false;
            }
            object[] arguments = { null, null };
            if (!(bool)method.Invoke(_controller, arguments))
            {
                return false;
            }
            left = arguments[0];
            right = arguments[1];
            return left != null || right != null;
        }

        private IntentSpan FindIntentSpan(bool isLeft, int frame)
        {
            IEnumerable spans = _intentEstimate?.GetType()
                .GetProperty(isLeft ? "Left" : "Right", Flags)
                ?.GetValue(_intentEstimate) as IEnumerable;
            if (spans == null)
            {
                return null;
            }
            foreach (object item in spans)
            {
                Type type = item.GetType();
                int start = (int)type.GetProperty("StartFrame", Flags).GetValue(item);
                int end = (int)type.GetProperty("EndFrameExclusive", Flags).GetValue(item);
                if (frame >= start && frame < end)
                {
                    return new IntentSpan
                    {
                        Start = start,
                        End = end,
                        Anchor = (Vector3)type.GetProperty("Anchor", Flags).GetValue(item),
                        Mode = type.GetProperty("Mode", Flags).GetValue(item)?.ToString() ?? "",
                        Certainty = type.GetProperty("Certainty", Flags).GetValue(item)?.ToString() ?? "",
                    };
                }
            }
            return null;
        }

        private string BuildStatusText()
        {
            if (_controller == null)
            {
                return _status;
            }

            object status = _controller.GetType()
                .GetProperty("LastGroundingStatus", Flags)?.GetValue(_controller);
            object gate = _controller.GetType()
                .GetProperty("LastGroundingGate", Flags)?.GetValue(_controller);
            IntentSpan left = FindIntentSpan(true, _frame);
            IntentSpan right = FindIntentSpan(false, _frame);
            return
                $"프레임 {_frame}/{_lastFrame}  상태={status}\n" +
                $"L: {SpanText(left)}   R: {SpanText(right)}\n" +
                $"stage={GetString(gate, "evaluation_stage")} " +
                $"raw={GetString(gate, "raw_pass")} held={GetString(gate, "held_pass")} " +
                $"err={GetString(gate, "target_error_m")}m " +
                $"clearance={GetString(gate, "sole_clearance_m")}m";
        }

        private static string SpanText(IntentSpan span)
        {
            return span == null ? "지지 없음" : $"{span.Mode}/{span.Certainty} {span.Start}-{span.End}";
        }

        private static object GetIntentEstimate(object controller)
        {
            FieldInfo field = controller.GetType().GetField("_footContactStabilizer", Flags);
            object stabilizer = field?.GetValue(controller);
            return stabilizer?.GetType()
                .GetProperty("IntentEstimate", Flags)?.GetValue(stabilizer);
        }

        private static object GetField(object owner, string name)
        {
            return owner?.GetType().GetField(name, Flags)?.GetValue(owner);
        }

        private static string GetString(object owner, string name)
        {
            object value = GetField(owner, name);
            switch (value)
            {
                case null: return "-";
                case bool flag: return flag ? "T" : "F";
                case float number: return number.ToString("F4");
                default: return value.ToString();
            }
        }

        private static bool GetBool(object owner, string name)
        {
            return GetField(owner, name) is bool flag && flag;
        }

        private static Vector3 GetVector(object owner, string name)
        {
            return GetField(owner, name) is Vector3 vector ? vector : Vector3.zero;
        }

        private static object CreateController()
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController", throwOnError: true);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, Flags);
            if (method == null)
            {
                throw new InvalidOperationException($"{methodName} 메서드가 필요합니다.");
            }
            return method.Invoke(target, arguments);
        }

        private static void EnsureHumanoidClipImport(string clipPath)
        {
            Type configurator = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator", throwOnError: false);
            configurator?.GetMethod("EnsureHumanoid", Flags)
                ?.Invoke(null, new object[] { clipPath });
        }

        private static AnimationClip LoadHumanoidClip(string clipPath)
        {
            foreach (UnityEngine.Object asset in
                AssetDatabase.LoadAllAssetsAtPath(clipPath))
            {
                if (asset is AnimationClip clip && clip.humanMotion &&
                    !clip.name.StartsWith("__", StringComparison.Ordinal))
                {
                    return clip;
                }
            }
            return null;
        }

        private static Transform RequireBone(Animator animator, HumanBodyBones bone)
        {
            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform == null)
            {
                throw new InvalidOperationException($"{bone} Humanoid 본이 필요합니다.");
            }
            return boneTransform;
        }

        private sealed class IntentSpan
        {
            internal int Start;
            internal int End;
            internal Vector3 Anchor;
            internal string Mode;
            internal string Certainty;
        }
    }
}
