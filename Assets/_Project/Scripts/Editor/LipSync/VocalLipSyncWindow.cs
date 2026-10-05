using System.IO;
using System.Threading.Tasks;
using Fbx2Vmd.CharacterLibrary;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEngine;
using uLipSync;
using VRM;

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

        private Task<VocalStemSeparator.Result> _separating;
        private string _message = "";
        private MessageType _messageType = MessageType.Info;

        [MenuItem("Tools/FBXImporter/보컬 립싱크")]
        private static void Open()
        {
            GetWindow<VocalLipSyncWindow>("보컬 립싱크");
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_pythonPath))
            {
                _pythonPath = VocalStemSeparator.FindPython() ?? "";
            }
            if (_profile == null)
            {
                _profile = AssetDatabase.LoadAssetAtPath<Profile>(DefaultProfilePath);
            }
        }

        private void Update()
        {
            if (_separating != null && _separating.IsCompleted)
            {
                VocalStemSeparator.Result result = _separating.IsFaulted
                    ? new VocalStemSeparator.Result { error = _separating.Exception?.GetBaseException().Message }
                    : _separating.Result;
                _separating = null;
                EditorUtility.ClearProgressBar();
                if (result.success)
                {
                    _vocalWavPath = result.vocalPath;
                    _bgmWavPath = result.instrumentalPath ?? "";
                    SetMessage($"분리 완료 — 보컬: {Path.GetFileName(_vocalWavPath)}", MessageType.Info);
                }
                else
                {
                    SetMessage("분리 실패: " + result.error, MessageType.Error);
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("1. 음원 → 보컬/BGM 분리", EditorStyles.boldLabel);
            DrawPathRow("원본 음원", ref _sourceAudioPath, "mp3/wav/flac/m4a 선택",
                "wav;mp3;flac;m4a;ogg");
            _engine = (VocalStemSeparator.Engine)EditorGUILayout.EnumPopup("분리 엔진", _engine);
            _model = EditorGUILayout.TextField("모델", _model);
            EditorGUILayout.BeginHorizontal();
            _pythonPath = EditorGUILayout.TextField("python 경로", _pythonPath);
            if (GUILayout.Button("자동 탐색", GUILayout.Width(80)))
            {
                _pythonPath = VocalStemSeparator.FindPython() ?? _pythonPath;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            _outputDir = EditorGUILayout.TextField("출력 폴더", _outputDir);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string picked = EditorUtility.OpenFolderPanel("출력 폴더", _outputDir, "");
                if (!string.IsNullOrEmpty(picked)) _outputDir = picked;
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(_separating != null))
            {
                if (GUILayout.Button("보컬/BGM 분리"))
                {
                    StartSeparation();
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. 보컬 → 립싱크 클립 베이크", EditorStyles.boldLabel);
            DrawPathRow("보컬 WAV", ref _vocalWavPath, "분리된 보컬 wav 선택", "wav");
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
            if (!string.IsNullOrEmpty(_bgmWavPath))
            {
                EditorGUILayout.LabelField("녹화용 BGM", _bgmWavPath, EditorStyles.wordWrappedMiniLabel);
            }
            DrawStatusLabel();
        }

        private void StartSeparation()
        {
            if (string.IsNullOrEmpty(_sourceAudioPath) || !File.Exists(_sourceAudioPath))
            {
                SetMessage("원본 음원 파일이 없습니다.", MessageType.Error);
                return;
            }
            if (!VocalStemSeparator.CheckInstalled(_engine, _pythonPath, out string installError))
            {
                SetMessage(installError, MessageType.Error);
                return;
            }
            Directory.CreateDirectory(_outputDir);
            _separating = VocalStemSeparator.SeparateAsync(
                _engine, _pythonPath, _sourceAudioPath, _outputDir, _model);
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
                VRMBlendShapeProxy proxy = VocalLipSyncBaker.FindProxy(_targetCharacter);
                if (proxy == null)
                {
                    SetMessage("대상 캐릭터에 VRMBlendShapeProxy가 없습니다.", MessageType.Error);
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
                AnimationClip clip = VocalLipSyncBaker.BakeClip(data, proxy, _minVolumeGate, warnings);
                clip.name = Path.GetFileNameWithoutExtension(_vocalWavPath) + "_lipsync";

                string saveDir = Path.Combine(_outputDir, "clips");
                Directory.CreateDirectory(saveDir);
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(saveDir, clip.name + ".anim").Replace('\\', '/'));
                AssetDatabase.CreateAsset(clip, assetPath);
                AssetDatabase.SaveAssets();
                DestroyImmediate(vocal);
                DestroyImmediate(data);

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

        private void DrawPathRow(string label, ref string path, string title, string extensions)
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
            EditorGUILayout.EndHorizontal();
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
