using System.IO;
using System.Threading.Tasks;
using Fbx2Vmd.CharacterLibrary;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEngine;
using uLipSync;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 노래 음원 → 보컬/BGM 분리(audio-separator/demucs) → 보컬 기반 립싱크 AnimationClip 베이크.
    /// 분리는 백그라운드 Task로 돌리고 Update에서 완료를 폴링한다.
    /// </summary>
    public class VocalLipSyncWindow : EditorWindow
    {
        private const string DefaultProfilePath =
            "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset";
        private const string DefaultOutputDir = "Assets/Generated/LipSync";

        [SerializeField] private string _sourceAudioPath = "";
        [SerializeField] private VocalStemSeparator.Engine _engine = VocalStemSeparator.Engine.AudioSeparator;
        [SerializeField] private string _model = VocalStemSeparator.DefaultModel;
        [SerializeField] private string _pythonPath = "";
        [SerializeField] private string _outputDir = DefaultOutputDir;
        [SerializeField] private string _vocalWavPath = "";
        [SerializeField] private string _bgmWavPath = "";
        [SerializeField] private Profile _profile;
        [SerializeField] private GameObject _targetCharacter;
        [SerializeField] private float _minVolumeGate = 0.02f;
        [SerializeField] private AnimationClip _previewClip;
        [SerializeField] private bool _previewWithAudio = true;

        private Task<VocalStemSeparator.Result> _separating;
        private Task<PythonEnvProvisioner.Result> _provisioning;
        private System.Threading.CancellationTokenSource _cts;
        private bool _separateAfterProvision;
        private bool _previewWasPlaying;
        private string _message = "";
        private MessageType _messageType = MessageType.Info;
        private float _sepProgress; // 분리 진행률 0~1 (워커 스레드에서 갱신)

        /// <summary>프로젝트 루트 — Assets의 부모.</summary>
        private static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem("Tools/FBXImporter/보컬 립싱크")]
        private static void Open()
        {
            GetWindow<VocalLipSyncWindow>("보컬 립싱크");
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_pythonPath))
            {
                // 프로비저닝된 venv가 이미 있으면 그걸 우선 쓴다.
                string venvPy = PythonEnvProvisioner.VenvPythonPath(
                    PythonEnvProvisioner.VenvDir(ProjectRoot));
                _pythonPath = File.Exists(venvPy)
                    ? venvPy
                    : VocalStemSeparator.FindPython() ?? "";
            }
            if (_profile == null)
            {
                _profile = AssetDatabase.LoadAssetAtPath<Profile>(DefaultProfilePath);
            }
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            LipSyncClipPreview.Stop();
            StemAudioPreview.Stop();
            EditorUtility.ClearProgressBar();
        }

        private void Update()
        {
            if (_provisioning != null && _provisioning.IsCompleted)
            {
                bool cancelled = _provisioning.IsCanceled;
                PythonEnvProvisioner.Result prov = cancelled || _provisioning.IsFaulted
                    ? new PythonEnvProvisioner.Result
                    {
                        error = cancelled
                            ? "사용자가 취소했습니다."
                            : _provisioning.Exception?.GetBaseException().Message
                    }
                    : _provisioning.Result;
                _provisioning = null;
                bool runSeparate = _separateAfterProvision;
                _separateAfterProvision = false;
                EditorUtility.ClearProgressBar();
                if (prov.success)
                {
                    _pythonPath = prov.pythonExe;
                    SetMessage(prov.reused
                        ? "Python 환경 확인 — 기존 venv 재사용"
                        : "Python 환경 설치 완료 — " + prov.venvDir, MessageType.Info);
                    if (runSeparate)
                    {
                        StartSeparation();
                    }
                }
                else if (cancelled)
                {
                    SetMessage("Python 환경 준비가 취소됐습니다.", MessageType.Info);
                }
                else
                {
                    SetMessage("Python 환경 준비 실패: " + prov.error, MessageType.Error);
                }
            }
            if (_separating != null && _separating.IsCompleted)
            {
                bool cancelled = _separating.IsCanceled;
                VocalStemSeparator.Result result = cancelled || _separating.IsFaulted
                    ? new VocalStemSeparator.Result
                    {
                        error = cancelled
                            ? "사용자가 취소했습니다."
                            : _separating.Exception?.GetBaseException().Message
                    }
                    : _separating.Result;
                _separating = null;
                _sepProgress = 0f;
                EditorUtility.ClearProgressBar();
                if (result.success)
                {
                    _vocalWavPath = result.vocalPath;
                    _bgmWavPath = result.instrumentalPath ?? "";
                    SetMessage($"분리 완료 — 보컬: {Path.GetFileName(_vocalWavPath)}", MessageType.Info);
                }
                else if (cancelled)
                {
                    SetMessage("분리가 취소됐습니다.", MessageType.Info);
                }
                else
                {
                    SetMessage("분리 실패: " + result.error, MessageType.Error);
                }
            }
            // 미리듣기가 자연 종료하면 버튼 라벨을 "듣기"로 되돌린다.
            if (StemAudioPreview.ClearIfFinished())
            {
                Repaint();
            }
            // 클립 미리보기: 진행 바 갱신 + 끝까지 재생돼 자동 정지되면 음성도 정리한다.
            bool previewPlaying = LipSyncClipPreview.IsPlaying;
            if (previewPlaying)
            {
                Repaint();
            }
            if (_previewWasPlaying && !previewPlaying)
            {
                StemAudioPreview.Stop();
                Repaint();
            }
            _previewWasPlaying = previewPlaying;
            // 분리 중엔 진행률 바 갱신을 위해 매 프레임 다시 그린다.
            if (_separating != null)
            {
                Repaint();
                EditorUtility.DisplayProgressBar("보컬 분리",
                    _sepProgress > 0f ? "오디오 스템 분리 중…" : "모델 로딩/입력 분석 중…",
                    _sepProgress > 0f ? _sepProgress : 0.05f);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("1. 음원 → 보컬/BGM 분리", EditorStyles.boldLabel);
            DrawPathRow("원본 음원", ref _sourceAudioPath, "mp3/wav/flac/m4a 선택",
                "wav;mp3;flac;m4a;ogg", preview: true);
            _engine = (VocalStemSeparator.Engine)EditorGUILayout.EnumPopup("분리 엔진", _engine);
            _model = EditorGUILayout.TextField("모델", _model);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_provisioning != null || _separating != null))
            {
                _pythonPath = EditorGUILayout.TextField("python 경로", _pythonPath);
                if (GUILayout.Button("환경 자동 준비", GUILayout.Width(100)))
                {
                    StartProvisioning(false);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (_provisioning != null)
            {
                EditorGUILayout.LabelField(
                    "python venv 생성 + 패키지 설치 중(최초 수 분 소요)...",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.BeginHorizontal();
            _outputDir = EditorGUILayout.TextField("출력 폴더", _outputDir);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string picked = EditorUtility.OpenFolderPanel("출력 폴더", _outputDir, "");
                if (!string.IsNullOrEmpty(picked)) _outputDir = picked;
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(_separating != null || _provisioning != null))
            {
                if (GUILayout.Button("보컬/BGM 분리"))
                {
                    StartSeparation();
                }
            }
            if (_separating != null || _provisioning != null)
            {
                if (GUILayout.Button("취소"))
                {
                    _cts?.Cancel();
                }
            }
            if (_separating != null)
            {
                // tqdm 퍼센트가 아직 안 잡히면 준비 단계로 표시한다.
                Rect rect = EditorGUILayout.GetControlRect(false, 18f);
                float shown = _sepProgress > 0f
                    ? _sepProgress
                    : Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.3f, 1f);
                string label = _sepProgress > 0f
                    ? $"분리 중… {(_sepProgress * 100f):F0}%"
                    : "분리 준비 중…(모델 로딩/입력 분석)";
                EditorGUI.ProgressBar(rect, shown, label);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. 보컬 → 립싱크 클립 베이크", EditorStyles.boldLabel);
            DrawPathRow("보컬 WAV", ref _vocalWavPath, "분리된 보컬 wav 선택", "wav",
                preview: true);
            _profile = (Profile)EditorGUILayout.ObjectField("uLipSync 프로필", _profile, typeof(Profile), false);
            EditorGUILayout.BeginHorizontal();
            _targetCharacter = (GameObject)EditorGUILayout.ObjectField(
                "대상 캐릭터", _targetCharacter, typeof(GameObject), true);
            if (GUILayout.Button("활성 캐릭터", GUILayout.Width(80)))
            {
                FBXVmdPipeline pipeline = CharacterLibraryManager.Current?.BoundPipeline;
                if (pipeline != null && pipeline.targetCharacter != null)
                {
                    _targetCharacter = pipeline.targetCharacter;
                }
            }
            EditorGUILayout.EndHorizontal();
            _minVolumeGate = EditorGUILayout.Slider("무음 게이트", _minVolumeGate, 0f, 0.2f);

            using (new EditorGUI.DisabledScope(_separating != null))
            {
                if (GUILayout.Button("립싱크 베이크 + 클립 저장"))
                {
                    Bake();
                }
            }

            EditorGUILayout.Space(8);
            DrawPathRow("녹화용 BGM", ref _bgmWavPath, "분리된 BGM wav 선택", "wav",
                preview: true);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("3. 클립 재생 테스트", EditorStyles.boldLabel);
            _previewClip = (AnimationClip)EditorGUILayout.ObjectField(
                "립싱크 클립", _previewClip, typeof(AnimationClip), false);
            _previewWithAudio = EditorGUILayout.Toggle("보컬 동시 재생", _previewWithAudio);
            bool previewPlaying = LipSyncClipPreview.IsPlaying;
            using (new EditorGUI.DisabledScope(
                !previewPlaying && (_previewClip == null || _targetCharacter == null)))
            {
                if (GUILayout.Button(previewPlaying ? "정지" : "캐릭터에 재생"))
                {
                    if (previewPlaying)
                    {
                        StopClipPreview();
                    }
                    else
                    {
                        StartClipPreview();
                    }
                }
            }
            if (previewPlaying)
            {
                Rect r = EditorGUILayout.GetControlRect(false, 18f);
                float len = Mathf.Max(LipSyncClipPreview.Clip.length, 0.001f);
                EditorGUI.ProgressBar(r, LipSyncClipPreview.Time / len,
                    $"재생 중… {LipSyncClipPreview.Time:F1} / {len:F1} s");
            }

            DrawStatusLabel();
        }

        private void StartProvisioning(bool separateAfter)
        {
            if (_provisioning != null || _separating != null)
            {
                return; // 진행 중 재진입 — 실행 중 Task의 CTS 폐기·동시 설치 방지
            }
            _separateAfterProvision = separateAfter;
            RenewCts();
            _provisioning = PythonEnvProvisioner.EnsureReadyAsync(
                ProjectRoot, _engine, _cts.Token);
            EditorUtility.DisplayProgressBar("Python 환경 준비",
                "venv 생성/패키지 설치 중(최초 수 분 소요)...", 0.3f);
            SetMessage("Python 환경 준비 중...", MessageType.Info);
        }

        private void RenewCts()
        {
            _cts?.Dispose();
            _cts = new System.Threading.CancellationTokenSource();
        }

        private void StartSeparation()
        {
            if (string.IsNullOrEmpty(_sourceAudioPath) || !File.Exists(_sourceAudioPath))
            {
                SetMessage("원본 음원 파일이 없습니다.", MessageType.Error);
                return;
            }
            // python 경로가 비었거나 파일이 없으면 환경 자동 준비부터 돌린다.
            if (string.IsNullOrEmpty(_pythonPath) || !File.Exists(_pythonPath))
            {
                StartProvisioning(true);
                return;
            }
            // 프로비저닝된 venv는 EnsureReady에서 이미 import 검증을 마쳤으므로
            // 메인스레드 동기 import 검사(최대 수 초 블로킹)를 생략한다.
            bool isProvisionedVenv = string.Equals(
                _pythonPath,
                PythonEnvProvisioner.VenvPythonPath(PythonEnvProvisioner.VenvDir(ProjectRoot)),
                System.StringComparison.OrdinalIgnoreCase);
            if (!isProvisionedVenv
                && !VocalStemSeparator.CheckInstalled(_engine, _pythonPath, out string installError))
            {
                // 수동 지정 python에 패키지가 없으면 프로젝트 venv를 자동 준비한다.
                SetMessage(installError + "\n→ 프로젝트 venv를 자동 준비합니다.", MessageType.Warning);
                StartProvisioning(true);
                return;
            }
            Directory.CreateDirectory(_outputDir);
            RenewCts();
            _sepProgress = 0f;
            _separating = VocalStemSeparator.SeparateAsync(
                _engine, _pythonPath, _sourceAudioPath, _outputDir, _model,
                _cts.Token, PythonEnvProvisioner.ProcessEnv(ProjectRoot),
                PythonEnvProvisioner.ModelsDir(ProjectRoot),
                p => _sepProgress = p);
            EditorUtility.DisplayProgressBar("보컬 분리", "오디오 스템 분리 중(수 분 소요)...", 0.5f);
            SetMessage("분리 실행 중...", MessageType.Info);
        }

        private void Bake()
        {
            try
            {
                if (!File.Exists(_vocalWavPath))
                {
                    SetMessage("보컬 WAV가 없습니다.", MessageType.Error);
                    return;
                }
                if (_profile == null)
                {
                    SetMessage("uLipSync 프로필을 지정하세요.", MessageType.Error);
                    return;
                }
                if (_targetCharacter == null)
                {
                    SetMessage("대상 캐릭터를 지정하세요.", MessageType.Error);
                    return;
                }

                AudioClip vocal = WavFileReader.Load(_vocalWavPath, out string wavError);
                if (vocal == null)
                {
                    SetMessage(wavError, MessageType.Error);
                    return;
                }

                BakedData data = VocalLipSyncBaker.BakeAnalysis(vocal, _profile);
                var warnings = new System.Collections.Generic.List<string>();
                // VRM 프록시가 없는 모델(MMD 등)은 모음 모프명을 스캔해 자동 바인딩한다.
                AnimationClip clip = VocalLipSyncBaker.BakeClip(
                    data, _targetCharacter, _minVolumeGate, warnings);
                clip.name = Path.GetFileNameWithoutExtension(_vocalWavPath) + "_lipsync";

                string saveDir = Path.Combine(_outputDir, "clips");
                Directory.CreateDirectory(saveDir);
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(saveDir, clip.name + ".anim").Replace('\\', '/'));
                AssetDatabase.CreateAsset(clip, assetPath);
                AssetDatabase.SaveAssets();
                DestroyImmediate(vocal);
                DestroyImmediate(data);
                _previewClip = clip;

                string extra = warnings.Count > 0 ? $" (경고 {warnings.Count}건)" : "";
                SetMessage($"클립 저장: {assetPath}{extra}", MessageType.Info);
                if (warnings.Count > 0)
                {
                    Debug.LogWarning("[VocalLipSync] " + string.Join("\n", warnings));
                }
            }
            catch (System.Exception error)
            {
                SetMessage("베이크 실패: " + error.Message, MessageType.Error);
                Debug.LogException(error);
            }
        }

        private void DrawPathRow(string label, ref string path, string title,
            string extensions, bool preview = false)
        {
            EditorGUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string picked = EditorUtility.OpenFilePanel(title, "", extensions);
                if (!string.IsNullOrEmpty(picked))
                {
                    path = picked;
                }
            }
            if (preview)
            {
                bool playing = StemAudioPreview.PlayingPath == path
                    && StemAudioPreview.IsPlaying();
                if (GUILayout.Button(playing ? "정지" : "듣기", GUILayout.Width(40)))
                {
                    string error = StemAudioPreview.Toggle(path);
                    if (error != null)
                    {
                        SetMessage(error, MessageType.Warning);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>립싱크 클립을 에디트 모드로 캐릭터에 재생한다(AnimationMode, 정지 시 복원).</summary>
        private void StartClipPreview()
        {
            LipSyncClipPreview.Start(_previewClip, _targetCharacter);
            if (!LipSyncClipPreview.IsPlaying)
            {
                return; // 클립/대상이 없으면 Start가 무시됨
            }
            if (_previewWithAudio && !string.IsNullOrEmpty(_vocalWavPath))
            {
                string error = StemAudioPreview.Toggle(_vocalWavPath);
                if (error != null)
                {
                    SetMessage(error, MessageType.Warning);
                }
            }
            SetMessage("클립 재생 중... 정지하면 원래 포즈로 복원됩니다.", MessageType.Info);
        }

        private void StopClipPreview()
        {
            LipSyncClipPreview.Stop();
            StemAudioPreview.Stop();
        }

        private void SetMessage(string message, MessageType type)
        {
            _message = message;
            _messageType = type;
            Repaint();
        }

        private void DrawStatusLabel()
        {
            if (string.IsNullOrEmpty(_message)) return;
            EditorGUILayout.HelpBox(_message, _messageType);
        }
    }
}
