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
        [SerializeField] private string _rawVocalWavPath = "";
        [SerializeField] private bool _cleanVocal = true;
        [SerializeField] private Profile _profile;
        [SerializeField] private GameObject _targetCharacter;
        [SerializeField] private float _minVolumeGate = 0.02f;
        [SerializeField] private float _releaseDamp = 0.35f; // 입 닫힘/잡음 하강 감쇠(0=끔)
        [SerializeField] private bool _useWav2VecPhonemes; // DL 음소 분석 경로
        [SerializeField] private Wav2VecPhonemeExtractor.PhonemeLanguage _phonemeLanguage;
        [SerializeField] private AnimationClip _previewClip;
        [SerializeField] private bool _previewWithAudio = true;

        private Task<VocalStemSeparator.Result> _separating;
        private Task<VocalStemSeparator.Result> _cleaning;
        private Task<(bool ok, string error)> _extracting;
        private string _extractCsvPath;
        private string _extractWavPath; // 추출 시작 시점의 보컬 경로 스냅샷
        private string _extractOutputDir;
        private string _cleanStage = ""; // 정제 체인 현재 패스 라벨
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
                    _bgmWavPath = result.instrumentalPath ?? "";
                    // 정제 체인은 audio-separator 전용 — demucs 보컬에 걸면 확정 실패한다.
                    if (_cleanVocal && _engine == VocalStemSeparator.Engine.AudioSeparator)
                    {
                        _rawVocalWavPath = result.vocalPath;
                        StartCleanup();
                    }
                    else
                    {
                        _vocalWavPath = result.vocalPath;
                        _rawVocalWavPath = "";
                        SetMessage($"분리 완료 — 보컬: {Path.GetFileName(_vocalWavPath)}", MessageType.Info);
                    }
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
            if (_cleaning != null && _cleaning.IsCompleted)
            {
                bool cancelled = _cleaning.IsCanceled;
                VocalStemSeparator.Result result = cancelled || _cleaning.IsFaulted
                    ? new VocalStemSeparator.Result
                    {
                        error = cancelled
                            ? "사용자가 취소했습니다."
                            : _cleaning.Exception?.GetBaseException().Message
                    }
                    : _cleaning.Result;
                _cleaning = null;
                _sepProgress = 0f;
                EditorUtility.ClearProgressBar();
                if (result.success)
                {
                    _vocalWavPath = result.vocalPath;
                    foreach (string warning in result.warnings)
                    {
                        UnityEngine.Debug.LogWarning("[보컬 정제] " + warning);
                    }
                    string suffix = result.warnings.Count > 0
                        ? $" (주의 {result.warnings.Count}건 — 콘솔 확인)"
                        : "";
                    SetMessage($"정제 완료 — 분석용 보컬: {Path.GetFileName(_vocalWavPath)}{suffix}", MessageType.Info);
                }
                else if (cancelled)
                {
                    // 취소해도 원본 보컬로 베이크할 수 있게 복원한다.
                    _vocalWavPath = _rawVocalWavPath;
                    _cleanStage = "";
                    SetMessage("보컬 정제가 취소됐습니다.", MessageType.Info);
                }
                else
                {
                    // 정제 실패 시 원본 보컬로라도 진행할 수 있게 폴백한다.
                    _vocalWavPath = _rawVocalWavPath;
                    SetMessage("정제 실패(원본 보컬로 대체): " + result.error, MessageType.Warning);
                }
            }
            if (_extracting != null && _extracting.IsCompleted)
            {
                bool cancelled = _extracting.IsCanceled;
                string extractError = null;
                if (!cancelled)
                {
                    extractError = _extracting.IsFaulted
                        ? _extracting.Exception?.GetBaseException().Message
                        : (_extracting.Result.ok ? null : _extracting.Result.error);
                }
                _extracting = null;
                _sepProgress = 0f;
                EditorUtility.ClearProgressBar();
                if (cancelled)
                {
                    SetMessage("음소 추출이 취소됐습니다.", MessageType.Info);
                }
                else if (extractError != null)
                {
                    SetMessage(extractError, MessageType.Error);
                }
                else
                {
                    FinishWav2VecBake();
                }
            }
            // 미리듣기가 자연 종료하면 버튼 라벨을 "듣기"로 되돌린다.
            if (StemAudioPreview.ClearIfFinished())
            {
                Repaint();
            }
            // 재생 중엔 진행바가 따라가도록 매 프레임 다시 그린다.
            if (StemAudioPreview.PlayingPath != null && StemAudioPreview.IsPlaying())
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
            // 분리·정제·음소 추출 중엔 진행률 바 갱신을 위해 매 프레임 다시 그린다.
            if (_separating != null || _cleaning != null || _extracting != null)
            {
                Repaint();
                string title = _separating != null ? "보컬 분리"
                    : _cleaning != null ? "보컬 정제" : "음소 추출";
                string stage = _separating != null
                    ? (_sepProgress > 0f ? "오디오 스템 분리 중…" : "모델 로딩/입력 분석 중…")
                    : _cleaning != null
                    ? (string.IsNullOrEmpty(_cleanStage) ? "보컬 정제 중…" : _cleanStage + " 중…")
                    : (_sepProgress > 0f ? "wav2vec2 음소 분석 중…" : "모델 다운로드/로딩 중…");
                EditorUtility.DisplayProgressBar(title, stage,
                    _sepProgress > 0f ? _sepProgress : 0.05f);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("1. 음원 → 보컬/BGM 분리", EditorStyles.boldLabel);
            DrawPathRow("원본 음원", ref _sourceAudioPath, "mp3/wav/flac/m4a 선택",
                "wav;mp3;flac;m4a;ogg", preview: true);
            // 듣기 출력 장치 — 기본 장치와 실제로 듣는 장치가 다르면 소리가 안 들린다.
            string[] devices = StemAudioPreview.GetOutputDeviceNames();
            if (devices.Length > 0)
            {
                // 이름 목록의 index 0은 매퍼(-1) — 표시 인덱스와 장치 번호가 1 어긋난다.
                // 저장된 장치가 제거되면 번호가 범위 밖일 수 있으니 클램프한다.
                int next = EditorGUILayout.Popup(
                    new GUIContent("출력 장치",
                        "듣기 소리가 나갈 장치. 소리가 안 나오면 실제로 듣는 장치를 선택"),
                    Mathf.Clamp(StemAudioPreview.SelectedDeviceNumber + 1,
                        0, devices.Length - 1), devices);
                if (next - 1 != StemAudioPreview.SelectedDeviceNumber)
                {
                    StemAudioPreview.SelectedDeviceNumber = next - 1;
                }
            }
            _engine = (VocalStemSeparator.Engine)EditorGUILayout.EnumPopup("분리 엔진", _engine);
            _model = EditorGUILayout.TextField("모델", _model);
            using (new EditorGUI.DisabledScope(_engine != VocalStemSeparator.Engine.AudioSeparator))
            {
                _cleanVocal = EditorGUILayout.Toggle(
                    new GUIContent("보컬 정제",
                        "원곡에서 리드 보컬만 추출(카라오케 앙상블) → 잔향/에코 제거 → 노이즈 제거. 립싱크용 리드 보컬만 남긴다"),
                    _cleanVocal);
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(
                _provisioning != null || _separating != null || _cleaning != null
                || _extracting != null))
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

            using (new EditorGUI.DisabledScope(
                _separating != null || _cleaning != null || _provisioning != null
                || _extracting != null))
            {
                if (GUILayout.Button("보컬/BGM 분리"))
                {
                    StartSeparation();
                }
            }
            if (_separating != null || _cleaning != null || _provisioning != null
                || _extracting != null)
            {
                if (GUILayout.Button("취소"))
                {
                    _cts?.Cancel();
                }
            }
            if (_separating != null || _cleaning != null || _extracting != null)
            {
                // tqdm 퍼센트가 아직 안 잡히면 준비 단계로 표시한다.
                Rect rect = EditorGUILayout.GetControlRect(false, 18f);
                float shown = _sepProgress > 0f
                    ? _sepProgress
                    : Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.3f, 1f);
                string label = _separating != null
                    ? (_sepProgress > 0f
                        ? $"분리 중… {(_sepProgress * 100f):F0}%"
                        : "분리 준비 중…(모델 로딩/입력 분석)")
                    : _cleaning != null
                    ? (_sepProgress > 0f
                        ? $"{_cleanStage} 중… {(_sepProgress * 100f):F0}%"
                        : "정제 준비 중…(모델 로딩)")
                    : (_sepProgress > 0f
                        ? $"음소 추출 중… {(_sepProgress * 100f):F0}%"
                        : "음소 추출 준비 중…(모델 다운로드/로딩)");
                EditorGUI.ProgressBar(rect, shown, label);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. 보컬 → 립싱크 클립 베이크", EditorStyles.boldLabel);
            DrawPathRow("보컬 WAV", ref _vocalWavPath, "분리된 보컬 wav 선택", "wav",
                preview: true);
            if (!string.IsNullOrEmpty(_rawVocalWavPath))
            {
                DrawPathRow("정제 전 보컬", ref _rawVocalWavPath, "정제 전 보컬 wav", "wav",
                    preview: true);
            }
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
            _releaseDamp = EditorGUILayout.Slider("감쇄(노이즈/여운)", _releaseDamp, 0f, 0.95f);
            _useWav2VecPhonemes = EditorGUILayout.ToggleLeft(
                "wav2vec2 음소 분석 사용(코러스 잔류에 강함, uLipSync 대신)",
                _useWav2VecPhonemes);
            if (_useWav2VecPhonemes)
            {
                _phonemeLanguage = (Wav2VecPhonemeExtractor.PhonemeLanguage)
                    EditorGUILayout.EnumPopup(
                        new GUIContent("음소 언어",
                            "보컬 가사 언어 — 언어별 wav2vec2 모델을 사용한다(첫 실행 시 모델 다운로드)"),
                        _phonemeLanguage);
            }

            using (new EditorGUI.DisabledScope(
                _separating != null || _cleaning != null || _extracting != null
                || _provisioning != null))
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
                !previewPlaying && (_previewClip == null || _targetCharacter == null
                    || EditorApplication.isPlaying)))
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
                    previewPlaying = LipSyncClipPreview.IsPlaying; // 정지 후 stale 상태로 접근 방지
                }
            }
            if (previewPlaying && LipSyncClipPreview.Clip != null)
            {
                float len = Mathf.Max(LipSyncClipPreview.Clip.length, 0.001f);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("위치", GUILayout.Width(EditorGUIUtility.labelWidth));
                EditorGUI.BeginChangeCheck();
                float t = GUILayout.HorizontalSlider(LipSyncClipPreview.Time, 0f, len);
                if (EditorGUI.EndChangeCheck())
                {
                    LipSyncClipPreview.Seek(t);
                    // 보컬 동시 재생 중이면 같은 위치로 맞춰 입모양과 소리를 같이 확인한다.
                    if (StemAudioPreview.PlayingPath == _vocalWavPath
                        && StemAudioPreview.IsPlaying())
                    {
                        StemAudioPreview.Seek(t);
                    }
                }
                EditorGUILayout.LabelField(
                    $"{LipSyncClipPreview.Time:F1}/{len:F1}s", GUILayout.Width(95));
                EditorGUILayout.EndHorizontal();
            }

            DrawStatusLabel();
        }

        private void StartProvisioning(bool separateAfter)
        {
            if (_provisioning != null || _separating != null || _cleaning != null
                || _extracting != null)
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
            // 실행 중 Task의 CTS를 RenewCts가 폐기하므로 동시 실행은 차단한다.
            if (_provisioning != null || _separating != null || _cleaning != null
                || _extracting != null)
            {
                return;
            }
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

        /// <summary>분리된 보컬에 정제 체인(디리버브→코러스 제거→디노이즈)을 돌린다.</summary>
        private void StartCleanup()
        {
            _sepProgress = 0f;
            _cleanStage = "";
            _cleaning = VocalStemSeparator.CleanVocalAsync(
                _pythonPath, _rawVocalWavPath, _outputDir,
                null, _cts.Token, PythonEnvProvisioner.ProcessEnv(ProjectRoot),
                PythonEnvProvisioner.ModelsDir(ProjectRoot),
                p => _sepProgress = p,
                s => _cleanStage = s,
                originalMixPath: _sourceAudioPath,
                salvageSourcePath: _rawVocalWavPath);
            SetMessage("분리 완료 — 보컬 정제(잔향/코러스/노이즈) 중...", MessageType.Info);
        }

        private void Bake()
        {
            try
            {
                // 실행 중 Task의 CTS를 공유하므로 동시 실행은 차단한다.
                if (_provisioning != null || _separating != null || _cleaning != null
                    || _extracting != null)
                {
                    return;
                }
                if (!File.Exists(_vocalWavPath))
                {
                    SetMessage("보컬 WAV가 없습니다.", MessageType.Error);
                    return;
                }
                // 프로필은 uLipSync 분석 경로에서만 필요하다(wav2vec2는 미사용).
                if (_profile == null && !_useWav2VecPhonemes)
                {
                    SetMessage("uLipSync 프로필을 지정하세요.", MessageType.Error);
                    return;
                }
                if (_targetCharacter == null)
                {
                    SetMessage("대상 캐릭터를 지정하세요.", MessageType.Error);
                    return;
                }

                if (_useWav2VecPhonemes)
                {
                    // wav2vec2 음소 추출은 백그라운드 — 완료되면 Update가 FinishWav2VecBake로 이어준다.
                    if (string.IsNullOrEmpty(_pythonPath) || !File.Exists(_pythonPath))
                    {
                        SetMessage("python 경로가 없습니다. 환경 준비를 먼저 실행하세요.", MessageType.Error);
                        return;
                    }
                    // 추출 중 경로 편집으로 "구 wav의 CSV + 신 wav의 이름/길이"가
                    // 섞이지 않게 시작 시점 값을 스냅샷한다.
                    _extractWavPath = _vocalWavPath;
                    _extractOutputDir = _outputDir;
                    _extractCsvPath = Path.Combine(_outputDir,
                        Path.GetFileNameWithoutExtension(_vocalWavPath) + "_phonemes.csv");
                    Directory.CreateDirectory(_outputDir);
                    _cts?.Dispose();
                    _cts = new System.Threading.CancellationTokenSource();
                    _sepProgress = 0f;
                    _extracting = Wav2VecPhonemeExtractor.ExtractAsync(
                        _pythonPath, ProjectRoot, _vocalWavPath, _extractCsvPath,
                        Wav2VecPhonemeExtractor.ModelFor(_phonemeLanguage),
                        PythonEnvProvisioner.ProcessEnv(ProjectRoot),
                        _cts.Token, p => _sepProgress = p);
                    SetMessage("wav2vec2 음소 추출 중…(첫 실행은 모델 다운로드)", MessageType.Info);
                    return;
                }

                AudioClip vocal = WavFileReader.Load(_vocalWavPath, out string wavError);
                if (vocal == null)
                {
                    SetMessage(wavError, MessageType.Error);
                    return;
                }
                FinishBake(vocal, VocalLipSyncBaker.BakeAnalysis(vocal, _profile),
                    _vocalWavPath, _outputDir);
            }
            catch (System.Exception error)
            {
                SetMessage("베이크 실패: " + error.Message, MessageType.Error);
                Debug.LogException(error);
            }
        }

        /// <summary>wav2vec2 추출 완료 후 처리 — CSV를 BakedData로 변환해 베이크를 마무리한다.</summary>
        private void FinishWav2VecBake()
        {
            AudioClip vocal = null;
            BakedData data = null;
            try
            {
                vocal = WavFileReader.Load(_extractWavPath, out string wavError);
                if (vocal == null)
                {
                    SetMessage(wavError, MessageType.Error);
                    return;
                }
                data = Wav2VecPhonemeExtractor.CsvToBakedData(
                    File.ReadAllText(_extractCsvPath), vocal.length);
                FinishBake(vocal, data, _extractWavPath, _extractOutputDir);
                vocal = null;
                data = null; // FinishBake가 파괴했으므로 소유권 해제
            }
            catch (System.Exception error)
            {
                if (vocal != null)
                {
                    DestroyImmediate(vocal);
                }
                if (data != null)
                {
                    DestroyImmediate(data);
                }
                SetMessage("음소 변환/베이크 실패: " + error.Message, MessageType.Error);
                Debug.LogException(error);
            }
        }

        /// <summary>분석 결과(BakedData)를 클립으로 굽고 에셋으로 저장한다(두 분석 경로 공용).</summary>
        private void FinishBake(AudioClip vocal, BakedData data,
            string clipNameSource, string outputDir)
        {
            var warnings = new System.Collections.Generic.List<string>();
            // VRM 프록시가 없는 모델(MMD 등)은 모음 모프명을 스캔해 자동 바인딩한다.
            AnimationClip clip = VocalLipSyncBaker.BakeClip(
                data, _targetCharacter, _minVolumeGate, warnings, _releaseDamp);
            clip.name = Path.GetFileNameWithoutExtension(clipNameSource) + "_lipsync";

            string saveDir = Path.Combine(outputDir, "clips");
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
            bool playing = false;
            if (preview)
            {
                playing = StemAudioPreview.PlayingPath == path
                    && StemAudioPreview.IsPlaying();
                if (GUILayout.Button(playing ? "정지" : "듣기", GUILayout.Width(40)))
                {
                    string error = StemAudioPreview.Toggle(path);
                    if (error != null)
                    {
                        SetMessage(error, MessageType.Warning);
                    }
                    playing = StemAudioPreview.PlayingPath == path
                        && StemAudioPreview.IsPlaying();
                }
            }
            EditorGUILayout.EndHorizontal();
            // 재생 중인 행은 드래그로 특정 구간을 바로 확인할 수 있는 진행바를 단다.
            if (playing)
            {
                float dur = Mathf.Max(StemAudioPreview.DurationSec, 0.001f);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(EditorGUIUtility.labelWidth));
                // 변경 체크로 사용자 드래그일 때만 시크 — 매 프레임 재시킹 방지.
                EditorGUI.BeginChangeCheck();
                float pos = GUILayout.HorizontalSlider(
                    StemAudioPreview.PositionSec, 0f, dur);
                if (EditorGUI.EndChangeCheck())
                {
                    StemAudioPreview.Seek(pos);
                }
                EditorGUILayout.LabelField(
                    $"{StemAudioPreview.PositionSec:F1}/{dur:F1}s",
                    GUILayout.Width(95));
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>립싱크 클립을 에디트 모드로 캐릭터에 재생한다(AnimationMode, 정지 시 복원).</summary>
        private void StartClipPreview()
        {
            if (!LipSyncClipPreview.Start(_previewClip, _targetCharacter))
            {
                SetMessage(LipSyncClipPreview.LastError
                    ?? "다른 도구가 애니메이션 미리보기를 사용 중입니다.", MessageType.Warning);
                return;
            }
            // Toggle은 재생 중이면 '정지'로 동작하므로, 이미 같은 보컬이 흐르고 있으면 건드리지 않는다.
            bool vocalAlreadyPlaying = StemAudioPreview.PlayingPath == _vocalWavPath
                && StemAudioPreview.IsPlaying();
            if (_previewWithAudio && !vocalAlreadyPlaying
                && !string.IsNullOrEmpty(_vocalWavPath))
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
