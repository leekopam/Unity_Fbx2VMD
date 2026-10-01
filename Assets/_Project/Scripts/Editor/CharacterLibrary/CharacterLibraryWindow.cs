using System;
using System.Collections.Generic;
using System.IO;
using Fbx2Vmd.CharacterLibrary;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리 창. VRM 등록·선택 로드·제거를 제공한다.
    /// 로드와 잠금은 씬의 CharacterLibraryManager에 위임해 단일 경로로 통일한다.
    /// </summary>
    public sealed class CharacterLibraryWindow : EditorWindow
    {
        private const float ThumbnailSize = 64f;
        private const float RowHeight = 72f;

        private CharacterLibraryManager _manager;
        private Vector2 _scroll;
        private string _message = string.Empty;
        private readonly Dictionary<string, Texture2D> _thumbnailCache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        // 엔트리별 프리셋 문서 캐시와 팝업 선택 인덱스.
        private readonly Dictionary<string, CharacterPresetDocument> _presetDocs =
            new Dictionary<string, CharacterPresetDocument>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _presetDocTimes =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _presetSelection =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private CharacterLibraryManager Manager =>
            _manager != null ? _manager : (_manager = CharacterLibraryManager.EnsureExists());

        private CharacterLibraryRegistry Registry => Manager.Registry;
        private bool IsLoading => Manager.IsLoading;

        [MenuItem("Tools/FBXImporter/캐릭터 라이브러리")]
        private static void Open()
        {
            GetWindow<CharacterLibraryWindow>(false, "캐릭터 라이브러리", true).Show();
        }

        private void OnEnable()
        {
            Manager.StateChanged += OnManagerStateChanged;
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.StateChanged -= OnManagerStateChanged;
            }
            ClearThumbnailCache();
        }

        private void OnManagerStateChanged(CharacterLibraryManager.LoadState state)
        {
            // 비동기 로드 진행 상황을 창에 반영한다.
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);

            if (Registry.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "등록된 캐릭터가 없습니다. [VRM 파일 추가]로 모델을 등록하세요.\n" +
                    "VRM 0.x 파일만 지원합니다.",
                    MessageType.Info);
            }
            else
            {
                DrawEntryList();
            }

            EditorGUILayout.Space(4f);
            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("VRM 파일 추가…", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                RegisterFromDialog();
            }

            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshAllStatuses();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("폴더 열기", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                string directory = Path.GetDirectoryName(Registry.LibraryFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    EditorUtility.RevealInFinder(directory);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntryList()
        {
            GameObject activeRoot = CharacterLibrarySceneBinding.FindActiveLibraryRoot();
            string activeMarkerId = activeRoot != null
                ? (activeRoot.GetComponent<CharacterLibraryInstance>()?.entryId ?? string.Empty)
                : string.Empty;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < Registry.Entries.Count; i++)
            {
                CharacterLibraryEntry entry = Registry.Entries[i];
                DrawEntryRow(entry, activeMarkerId);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntryRow(CharacterLibraryEntry entry, string activeMarkerId)
        {
            bool isActive = !string.IsNullOrEmpty(activeMarkerId) &&
                            string.Equals(entry.id, activeMarkerId, StringComparison.Ordinal);

            EditorGUILayout.BeginVertical(isActive ? "SelectionRect" : "box");
            EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

            Texture2D thumbnail = GetThumbnail(entry);
            Rect thumbnailRect = GUILayoutUtility.GetRect(
                ThumbnailSize, ThumbnailSize, GUILayout.Width(ThumbnailSize));
            if (thumbnail != null)
            {
                GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(thumbnailRect, new Color(0.18f, 0.18f, 0.18f));
                GUI.Label(thumbnailRect, "미리보기\n없음", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(entry.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"제작: {DisplayOrDash(entry.meta.author)}  |  {entry.vrmSpecVersion}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(entry.sourcePath, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            DrawStatusLabel(entry);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(76f));
            GUI.enabled = !IsLoading && entry.status != CharacterLibraryEntryStatus.Missing;
            if (GUILayout.Button(isActive ? "활성" : "불러오기",
                    GUILayout.Height(24f)))
            {
                if (!isActive)
                {
                    LoadEntry(entry);
                }
            }
            GUI.enabled = !IsLoading;

            if (entry.status == CharacterLibraryEntryStatus.Missing &&
                GUILayout.Button("다시 연결", GUILayout.Height(20f)))
            {
                RelinkFromDialog(entry);
            }

            if (GUILayout.Button("제거", GUILayout.Height(20f)))
            {
                RemoveEntry(entry);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            DrawPresetRow(entry);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 엔트리 하단의 프리셋 행: 선택 팝업, 기본 지정, 현재 상태 저장, 즉시 적용, 삭제.
        /// </summary>
        private void DrawPresetRow(CharacterLibraryEntry entry)
        {
            CharacterPresetDocument doc = GetPresets(entry.id);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("프리셋", EditorStyles.miniLabel, GUILayout.Width(34f));

            if (doc.presets.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "없음 — 캐릭터를 불러온 뒤 [현재 저장]으로 만듭니다",
                    EditorStyles.miniLabel);
            }
            else
            {
                _presetSelection.TryGetValue(entry.id, out int selected);
                if (selected < 0 || selected >= doc.presets.Count)
                {
                    selected = 0;
                }

                var names = new string[doc.presets.Count];
                for (int i = 0; i < doc.presets.Count; i++)
                {
                    bool isDefault = doc.presets[i].id == entry.defaultPresetId;
                    names[i] = (isDefault ? "◆ " : "　") + doc.presets[i].name;
                }

                int next = EditorGUILayout.Popup(
                    selected, names, GUILayout.Width(160f));
                _presetSelection[entry.id] = next;

                GUI.enabled = !IsLoading;
                if (GUILayout.Button("기본", GUILayout.Width(34f)))
                {
                    try
                    {
                        Registry.SetDefaultPreset(entry.id, doc.presets[next].id);
                        _message = $"기본 프리셋: {doc.presets[next].name}";
                    }
                    catch (Exception error)
                    {
                        _message = $"기본 프리셋 지정 실패: {error.Message}";
                    }
                    Repaint();
                }
                if (GUILayout.Button("적용", GUILayout.Width(34f)))
                {
                    ApplyPresetNow(entry, doc.presets[next]);
                }
                if (GUILayout.Button("삭제", GUILayout.Width(34f)))
                {
                    DeletePreset(entry, next);
                }
                GUI.enabled = true;
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = !IsLoading;
            if (GUILayout.Button("현재 저장", GUILayout.Width(64f)))
            {
                SaveCurrentAsPreset(entry);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private CharacterPresetDocument GetPresets(string entryId)
        {
            // 파일의 최종 쓰기 시각이 바뀌면 캐시를 버리고 다시 읽는다(외부 편집 대응).
            string path = Registry.ResolvePresetFilePath(entryId);
            DateTime writeTime = !string.IsNullOrEmpty(path) && File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;

            if (!_presetDocs.TryGetValue(entryId, out CharacterPresetDocument doc) || doc == null ||
                !_presetDocTimes.TryGetValue(entryId, out DateTime known) || known != writeTime)
            {
                doc = Registry.LoadPresets(entryId);
                _presetDocs[entryId] = doc;
                _presetDocTimes[entryId] = writeTime;
            }
            return doc;
        }

        private void UpdatePresetCache(string entryId, CharacterPresetDocument doc)
        {
            _presetDocs[entryId] = doc;
            string path = Registry.ResolvePresetFilePath(entryId);
            _presetDocTimes[entryId] = !string.IsNullOrEmpty(path) && File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;
        }

        /// <summary>
        /// 현재 파이프라인·캐릭터 상태를 새 프리셋으로 저장한다.
        /// 캡처는 활성 라이브러리 캐릭터가 이 엔트리일 때만 허용한다.
        /// </summary>
        private void SaveCurrentAsPreset(CharacterLibraryEntry entry)
        {
            FBXVmdPipeline pipeline = Manager.BoundPipeline;
            if (pipeline == null)
            {
                _message = "씬에 FBXVmdPipeline이 없습니다.";
                Repaint();
                return;
            }

            GameObject root = CharacterLibrarySceneBinding.FindActiveLibraryRoot(pipeline);
            string activeId = root != null
                ? root.GetComponent<CharacterLibraryInstance>()?.entryId
                : null;
            if (root == null || activeId != entry.id)
            {
                _message = "이 캐릭터를 먼저 불러온 뒤 저장하세요.";
                Repaint();
                return;
            }

            try
            {
                CharacterPresetDocument doc = Registry.LoadPresets(entry.id);
                CharacterPreset preset = CharacterPresetSnapshot.CreateEmpty(
                    $"프리셋 {doc.presets.Count + 1}");
                TargetIdlePoseGuard guard = pipeline.GetComponent<TargetIdlePoseGuard>();
                CharacterPresetSnapshot.CaptureRetarget(preset, pipeline);
                CharacterPresetSnapshot.CapturePlacement(preset, root, guard);
                CharacterPresetSnapshot.CapturePhysics(preset, root);

                doc.presets.Add(preset);
                CharacterPresetStore store = Registry.OpenPresetStore(entry.id);
                store.Save(doc);
                // 프리셋 파일은 저장됐지만 라이브러리 측 동기화가 실패할 수 있으므로 분리한다.
                bool synced = true;
                try
                {
                    Registry.SyncPresetIds(
                        entry.id, doc.presets.ConvertAll(p => p.id));
                    if (string.IsNullOrEmpty(entry.defaultPresetId))
                    {
                        Registry.SetDefaultPreset(entry.id, preset.id);
                        entry.defaultPresetId = preset.id;
                    }
                }
                catch (Exception syncError)
                {
                    synced = false;
                    _message = $"프리셋은 저장됐지만 목록 동기화에 실패했습니다: {syncError.Message}";
                }

                UpdatePresetCache(entry.id, doc);
                _presetSelection[entry.id] = doc.presets.Count - 1;
                if (synced)
                {
                    _message = $"프리셋 저장: {preset.name} ({entry.displayName})";
                }
            }
            catch (Exception error)
            {
                _message = $"프리셋 저장 실패: {error.Message}";
            }
            Repaint();
        }

        /// <summary>
        /// 선택한 프리셋을 현재 캐릭터/파이프라인에 즉시 적용한다.
        /// </summary>
        private void ApplyPresetNow(CharacterLibraryEntry entry, CharacterPreset preset)
        {
            FBXVmdPipeline pipeline = Manager.BoundPipeline;
            GameObject root = CharacterLibrarySceneBinding.FindActiveLibraryRoot(pipeline);
            string activeId = root != null
                ? root.GetComponent<CharacterLibraryInstance>()?.entryId
                : null;
            if (pipeline == null || root == null)
            {
                _message = "캐릭터를 먼저 불러오세요.";
                Repaint();
                return;
            }
            // 프리셋은 캐릭터 귀속이다 — 다른 캐릭터에 인덱스 기반 값을 쓰면 안 된다.
            if (activeId != entry.id)
            {
                _message = "이 프리셋은 해당 캐릭터를 불러온 뒤에만 적용할 수 있습니다.";
                Repaint();
                return;
            }

            try
            {
                TargetIdlePoseGuard guard = pipeline.GetComponent<TargetIdlePoseGuard>();
                List<string> warnings = CharacterPresetSnapshot.Apply(
                    preset, pipeline, root, guard);
                _message = warnings.Count == 0
                    ? $"프리셋 적용: {preset.name}"
                    : $"프리셋 적용(일부 {warnings.Count}건 미적용): {preset.name}";
                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(pipeline.gameObject.scene);
                }
            }
            catch (Exception error)
            {
                _message = $"프리셋 적용 실패: {error.Message}";
            }
            Repaint();
        }

        private void DeletePreset(CharacterLibraryEntry entry, int index)
        {
            try
            {
                CharacterPresetDocument doc = Registry.LoadPresets(entry.id);
                if (index < 0 || index >= doc.presets.Count)
                {
                    return;
                }
                string name = doc.presets[index].name;
                doc.presets.RemoveAt(index);
                CharacterPresetStore store = Registry.OpenPresetStore(entry.id);
                store.Save(doc);
                bool synced = true;
                try
                {
                    Registry.SyncPresetIds(entry.id, doc.presets.ConvertAll(p => p.id));
                }
                catch (Exception syncError)
                {
                    synced = false;
                    _message = $"프리셋은 삭제됐지만 목록 동기화에 실패했습니다: {syncError.Message}";
                }
                UpdatePresetCache(entry.id, doc);
                _presetSelection[entry.id] = 0;
                if (synced)
                {
                    _message = $"프리셋 삭제: {name}";
                }
            }
            catch (Exception error)
            {
                _message = $"프리셋 삭제 실패: {error.Message}";
            }
            Repaint();
        }

        private void DrawStatusLabel(CharacterLibraryEntry entry)
        {
            string label;
            switch (entry.status)
            {
                case CharacterLibraryEntryStatus.Missing:
                    label = "파일 없음 — 다시 연결이 필요합니다";
                    break;
                case CharacterLibraryEntryStatus.Changed:
                    label = "파일이 변경되었습니다";
                    break;
                case CharacterLibraryEntryStatus.Error:
                    label = "오류: " + entry.lastError;
                    break;
                default:
                    label = string.Empty;
                    break;
            }

            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            if (IsLoading)
            {
                EditorGUILayout.LabelField("캐릭터 로드 중…", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("취소", GUILayout.Width(60f)))
                {
                    Manager.CancelLoading();
                }
            }
            else
            {
                string status = !string.IsNullOrEmpty(_message)
                    ? _message
                    : Manager.LastError;
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            }

            GameObject activeRoot = CharacterLibrarySceneBinding.FindActiveLibraryRoot();
            GUI.enabled = !IsLoading && activeRoot != null;
            if (GUILayout.Button("씬 캐릭터로 복원", GUILayout.Width(110f)))
            {
                RestoreSceneCharacter();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void RegisterFromDialog()
        {
            string path = EditorUtility.OpenFilePanel(
                "VRM 캐릭터 선택", string.Empty, "vrm");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                CharacterLibraryRegistry.RegisterResult result = Registry.Register(path);
                _message = result.success
                    ? $"등록 완료: {result.entry.displayName}"
                    : result.error;
            }
            catch (Exception error)
            {
                _message = $"등록 중 오류가 발생했습니다: {error.Message}";
            }
            Repaint();
        }

        private void RelinkFromDialog(CharacterLibraryEntry entry)
        {
            string path = EditorUtility.OpenFilePanel(
                $"다시 연결할 파일 선택: {entry.displayName}", string.Empty, "vrm");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                _message = Registry.TryRelink(entry, path, out string error)
                    ? "다시 연결했습니다."
                    : error;
            }
            catch (Exception exception)
            {
                _message = $"다시 연결 중 오류가 발생했습니다: {exception.Message}";
            }
            Repaint();
        }

        private void RemoveEntry(CharacterLibraryEntry entry)
        {
            if (!EditorUtility.DisplayDialog(
                    "캐릭터 제거",
                    $"라이브러리에서 '{entry.displayName}'을(를) 제거하시겠습니까?\n" +
                    "원본 VRM 파일은 삭제되지 않습니다.",
                    "제거", "취소"))
            {
                return;
            }

            GameObject activeRoot = CharacterLibrarySceneBinding.FindActiveLibraryRoot();
            if (activeRoot != null &&
                string.Equals(activeRoot.GetComponent<CharacterLibraryInstance>()?.entryId,
                    entry.id, StringComparison.Ordinal))
            {
                RestoreSceneCharacter();
            }

            try
            {
                Registry.Remove(entry.id);
                _message = $"제거했습니다: {entry.displayName}";
            }
            catch (Exception error)
            {
                _message = $"제거 중 오류가 발생했습니다: {error.Message}";
            }
            Repaint();
        }

        private async void LoadEntry(CharacterLibraryEntry entry)
        {
            FBXVmdPipeline pipeline = FindObjectOfType<FBXVmdPipeline>(true);
            if (pipeline == null)
            {
                _message = "씬에 FBXVmdPipeline이 없어 캐릭터를 배치할 수 없습니다.";
                Repaint();
                return;
            }

            _message = string.Empty;
            bool success;
            try
            {
                // 로드·검증·커밋·잠금은 매니저가 단일 경로로 처리한다.
                success = await Manager.SelectCharacterAsync(entry.id);
            }
            catch (Exception error)
            {
                success = false;
                _message = $"로드 중 오류가 발생했습니다: {error.Message}";
            }

            // await 중 창이 닫혔으면 메시지 갱신을 건너뛴다.
            if (this == null)
            {
                return;
            }

            if (success)
            {
                // 기본 프리셋 자동 적용의 경고가 성공 메시지에 가려지지 않게 함께 표시한다.
                _message = string.IsNullOrEmpty(Manager.LastError)
                    ? $"불러왔습니다: {entry.displayName}"
                    : $"불러왔습니다: {entry.displayName} — {Manager.LastError}";
            }
            else if (string.IsNullOrEmpty(_message))
            {
                // catch에서 이미 오류를 적은 경우 그 메시지를 유지한다.
                _message = Manager.LastError;
            }

            if (success && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(pipeline.gameObject.scene);
            }
            Repaint();
        }

        private void RestoreSceneCharacter()
        {
            _message = Manager.RestoreSceneCharacter()
                ? "씬 캐릭터로 복원했습니다."
                : Manager.LastError;
            if (_manager != null && !Application.isPlaying)
            {
                FBXVmdPipeline pipeline = FindObjectOfType<FBXVmdPipeline>(true);
                if (pipeline != null)
                {
                    EditorSceneManager.MarkSceneDirty(pipeline.gameObject.scene);
                }
            }
            Repaint();
        }

        private void RefreshAllStatuses()
        {
            for (int i = 0; i < Registry.Entries.Count; i++)
            {
                Registry.RefreshEntry(Registry.Entries[i]);
            }
            _presetDocs.Clear();
            _presetDocTimes.Clear();
            try
            {
                Registry.Save();
                _message = "목록을 새로고침했습니다.";
            }
            catch (Exception error)
            {
                _message = $"상태 저장에 실패했습니다: {error.Message}";
            }
            Repaint();
        }

        private Texture2D GetThumbnail(CharacterLibraryEntry entry)
        {
            string path = Registry.ResolveThumbnailPath(entry);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            if (_thumbnailCache.TryGetValue(path, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                DestroyImmediate(texture);
                return null;
            }

            texture.hideFlags = HideFlags.HideAndDontSave;
            _thumbnailCache[path] = texture;
            return texture;
        }

        private void ClearThumbnailCache()
        {
            foreach (KeyValuePair<string, Texture2D> pair in _thumbnailCache)
            {
                if (pair.Value != null)
                {
                    DestroyImmediate(pair.Value);
                }
            }
            _thumbnailCache.Clear();
        }

        private static string DisplayOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
