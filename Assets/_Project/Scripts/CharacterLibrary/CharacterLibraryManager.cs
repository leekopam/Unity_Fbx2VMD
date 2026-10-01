using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fbx2Vmd.FBXImporter;
using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리 선택 로드를 오케스트레이션하는 씬 컴포넌트.
    /// 단일 비행 잠금으로 동시 로드를 막고, 실패 시 기존 타겟을 보존한다.
    /// 에디터 창은 EnsureExists()로 이 매니저에 위임해 잠금과 레지스트리를 공유한다.
    /// </summary>
    public sealed class CharacterLibraryManager : MonoBehaviour
    {
        public enum LoadState
        {
            Idle,
            Loading,
            Validating,
            Activating,
            Active,
            Failed,
        }

        [Tooltip("비어 있으면 씬에서 FBXVmdPipeline을 찾습니다.")]
        [SerializeField] private FBXVmdPipeline _pipeline;

        [Tooltip("씬에 직접 배치한 경우에만, Play 시작 시 마지막으로 사용한 라이브러리 캐릭터를 자동으로 다시 불러옵니다.")]
        [SerializeField] private bool _autoLoadLastCharacter = true;

        private static CharacterLibraryManager _current;

        // EnsureExists가 만든 임시 헬퍼 여부. 헬퍼는 Start 자동 복원을 하지 않는다.
        private bool _autoCreated;

        private CharacterLibraryRegistry _registry;
        private CharacterLoader _loader;
        private CancellationTokenSource _cancellation;
        private LoadState _state = LoadState.Idle;
        private string _lastError = string.Empty;
        private string _activeEntryId = string.Empty;

        public LoadState State => _state;
        public string LastError => _lastError;
        public string ActiveEntryId => _activeEntryId;
        public bool IsLoading => _state == LoadState.Loading ||
                                 _state == LoadState.Validating ||
                                 _state == LoadState.Activating;

        public event System.Action<LoadState> StateChanged;

        /// <summary>
        /// 씬의 매니저를 돌려준다. 없으면 숨김 헬퍼 오브젝트를 만들어 반환한다.
        /// 편집 모드에서는 계층에 보이지 않고 저장되지 않는다.
        /// </summary>
        public static CharacterLibraryManager EnsureExists()
        {
            if (_current != null)
            {
                return _current;
            }

            _current = FindObjectOfType<CharacterLibraryManager>(true);
            if (_current != null)
            {
                return _current;
            }

            var host = new GameObject(nameof(CharacterLibraryManager));
            // 편집 모드 헬퍼는 계층에 보이지 않게 하고 씬에 저장되지 않게 한다.
            host.hideFlags = Application.isPlaying
                ? HideFlags.DontSave
                : HideFlags.HideInHierarchy | HideFlags.DontSave;
            _current = host.AddComponent<CharacterLibraryManager>();
            _current._autoCreated = true;
            return _current;
        }

        private void Awake()
        {
            _current = this;
            _registry = new CharacterLibraryRegistry();
            _loader = new CharacterLoader();
            EnsurePipeline();
        }

        /// <summary>
        /// 생성 없이 현재 매니저를 돌려준다. 저장 가드 등 외부 경로가 상태를 정리할 때 사용한다.
        /// </summary>
        public static CharacterLibraryManager Current => _current;

        /// <summary>
        /// 씬 저장 가드처럼 매니저를 거치지 않은 복원이 일어났을 때 내부 상태를 정리한다.
        /// 레지스트리의 activeCharacterId는 다음 Play 자동 복원을 위해 그대로 둔다.
        /// </summary>
        public void NotifyExternalRestore()
        {
            _activeEntryId = string.Empty;
            if (_state == LoadState.Active)
            {
                SetState(LoadState.Idle);
            }
        }

        private void Start()
        {
            // 마지막으로 사용한 캐릭터를 자동 복원한다.
            // 임시 헬퍼(창이 만든 매니저)는 Play 중 임의 시점 로드를 막기 위해 제외한다.
            if (_autoCreated || !_autoLoadLastCharacter || !Application.isPlaying)
            {
                return;
            }

            string activeId = Registry.ActiveCharacterId;
            if (string.IsNullOrEmpty(activeId) || Registry.FindById(activeId) == null)
            {
                return;
            }

            AutoRestoreCharacter(activeId);
        }

        private async void AutoRestoreCharacter(string entryId)
        {
            try
            {
                await SelectCharacterAsync(entryId);
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
            }
        }

        public CharacterLibraryRegistry Registry
        {
            get
            {
                if (_registry == null)
                {
                    _registry = new CharacterLibraryRegistry();
                }
                return _registry;
            }
        }

        /// <summary>
        /// 라이브러리 엔트리를 로드해 파이프라인 타겟으로 교체한다.
        /// 실패 시 false를 돌려주고 기존 타겟은 유지된다.
        /// </summary>
        public async Task<bool> SelectCharacterAsync(string entryId)
        {
            if (IsLoading)
            {
                _lastError = "이미 캐릭터 로드가 진행 중입니다.";
                return false;
            }

            CharacterLibraryEntry entry = Registry.FindById(entryId);
            if (entry == null)
            {
                _lastError = "라이브러리에 없는 캐릭터입니다.";
                return false;
            }

            if (!EnsurePipeline())
            {
                _lastError = "씬에 FBXVmdPipeline이 없습니다.";
                SetState(LoadState.Failed);
                return false;
            }

            Registry.RefreshEntry(entry);
            // 외부 변경으로 문서가 리로드됐을 수 있으므로 최신 엔트리를 다시 잡는다.
            entry = Registry.FindById(entryId);
            if (entry == null)
            {
                _lastError = "라이브러리에서 제거된 캐릭터입니다.";
                SetState(LoadState.Failed);
                return false;
            }

            if (entry.status == CharacterLibraryEntryStatus.Missing)
            {
                _lastError = $"파일이 없습니다: {entry.sourcePath}";
                TrySaveRegistry();
                SetState(LoadState.Failed);
                return false;
            }

            // 로컬에 캡처해 await 중 OnDestroy가 _cancellation을 비워도 Dispose가 안전하다.
            var cts = _cancellation = new CancellationTokenSource();
            SetState(LoadState.Loading);
            _lastError = string.Empty;

            CharacterLoader.Result result = null;
            try
            {
                if (_loader == null)
                {
                    // 편집 모드 헬퍼는 Awake가 돌지 않으므로 지연 생성한다.
                    _loader = new CharacterLoader();
                }
                result = await _loader.LoadAsync(entry.sourcePath, cts.Token);
            }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_cancellation, cts))
                {
                    _cancellation = null;
                }
            }

            // await 중 매니저가 파괴됐으면 로드된 인스턴스만 정리하고 끝낸다.
            if (this == null)
            {
                if (result != null)
                {
                    CharacterLoader.DestroyInstance(result.instance);
                }
                return false;
            }

            if (!EnsurePipeline())
            {
                CharacterLoader.DestroyInstance(result != null ? result.instance : null);
                _lastError = "로드 중 FBXVmdPipeline이 사라졌습니다.";
                SetState(LoadState.Failed);
                return false;
            }

            if (result.cancelled)
            {
                SetState(LoadState.Idle);
                return false;
            }

            if (!result.success)
            {
                _lastError = result.error;
                // await 중 문서가 리로드됐을 수 있으니 최신 엔트리에 기록한다.
                CharacterLibraryEntry failedEntry = Registry.FindById(entry.id);
                if (failedEntry != null)
                {
                    failedEntry.lastError = result.error;
                    failedEntry.status = CharacterLibraryEntryStatus.Error;
                    TrySaveRegistry();
                }
                SetState(LoadState.Failed);
                return false;
            }

            SetState(LoadState.Validating);
            // Humanoid 검증은 CharacterLoader 내부에서 완료됐다.

            SetState(LoadState.Activating);
            try
            {
                CharacterLibrarySceneBinding.Commit(_pipeline, entry.id, result.instance);
            }
            catch (System.Exception error)
            {
                // 커밋 중 오류가 나도 로드된 루트가 남지 않게 정리한다.
                CharacterLoader.DestroyInstance(result.instance);
                _lastError = $"캐릭터 적용 실패: {error.Message}";
                SetState(LoadState.Failed);
                return false;
            }
            CharacterLibraryEntry committedEntry = Registry.FindById(entry.id);
            if (committedEntry != null)
            {
                committedEntry.compatibility.humanoidValid = true;
                committedEntry.compatibility.checkedAtUtc =
                    System.DateTime.UtcNow.ToString("O");
                committedEntry.status = CharacterLibraryEntryStatus.Ready;
            }

            _activeEntryId = entry.id;
            // 기본 프리셋이 지정돼 있으면 커밋된 캐릭터에 자동 적용한다(실패해도 로드는 유지).
            if (committedEntry != null && !string.IsNullOrEmpty(committedEntry.defaultPresetId))
            {
                TryApplyDefaultPreset(committedEntry, result.instance.Root);
            }
            // 저장 실패가 로드 성공 상태를 덮지 않게 격리한다.
            try
            {
                Registry.MarkUsed(entry.id);
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                _lastError = $"라이브러리 기록 저장 실패: {error.Message}";
            }
            SetState(LoadState.Active);
            return true;
        }

        /// <summary>
        /// 라이브러리 캐릭터를 제거하고 스왑 전 씬 캐릭터로 되돌린다.
        /// </summary>
        public bool RestoreSceneCharacter()
        {
            if (IsLoading)
            {
                return false;
            }

            if (!EnsurePipeline())
            {
                _lastError = "씬에 FBXVmdPipeline이 없습니다.";
                return false;
            }

            bool restored = CharacterLibrarySceneBinding.Restore(
                _pipeline, out bool removedLibraryCharacter);
            if (restored || removedLibraryCharacter)
            {
                _activeEntryId = string.Empty;
                try
                {
                    Registry.ClearActiveCharacter();
                }
                catch (System.Exception error)
                {
                    Debug.LogException(error);
                }
                SetState(LoadState.Idle);
            }
            if (!restored)
            {
                _lastError = removedLibraryCharacter
                    ? "라이브러리 캐릭터를 제거했습니다. 이전 씬 캐릭터는 다른 캐릭터가 사용 중이거나 찾지 못했습니다."
                    : "복원할 라이브러리 캐릭터가 없습니다.";
            }
            return restored;
        }

        public void CancelLoading()
        {
            if (_cancellation != null)
            {
                _cancellation.Cancel();
            }
        }

        /// <summary>
        /// 창의 프리셋 캡처 등이 쓰는 바인딩된 파이프라인 접근자.
        /// </summary>
        public FBXVmdPipeline BoundPipeline => EnsurePipeline() ? _pipeline : null;

        /// <summary>
        /// 기본 프리셋을 읽어 파이프라인·캐릭터·유휴 가드에 적용한다.
        /// 프리셋 오류는 로드 성공을 깨지 않고 경고로만 남긴다.
        /// </summary>
        private void TryApplyDefaultPreset(CharacterLibraryEntry entry, GameObject characterRoot)
        {
            try
            {
                CharacterPreset preset = Registry.FindPreset(entry.id, entry.defaultPresetId);
                if (preset == null)
                {
                    return;
                }

                TargetIdlePoseGuard guard = _pipeline != null
                    ? _pipeline.GetComponent<TargetIdlePoseGuard>()
                    : null;
                List<string> warnings = CharacterPresetSnapshot.Apply(
                    preset, _pipeline, characterRoot, guard);
                if (warnings.Count > 0)
                {
                    string summary = $"프리셋 '{preset.name}' 일부 미적용({warnings.Count}건)";
                    Debug.LogWarning(
                        $"[CharacterLibrary] {summary}: {string.Join(", ", warnings)}");
                    _lastError = string.IsNullOrEmpty(_lastError)
                        ? summary
                        : _lastError + " | " + summary;
                }
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                _lastError = string.IsNullOrEmpty(_lastError)
                    ? $"프리셋 적용 실패: {error.Message}"
                    : _lastError + $" | 프리셋 적용 실패: {error.Message}";
            }
        }

        /// <summary>
        /// 저장 예외를 로깅만 하고 상태 머신 진행은 막지 않는다.
        /// </summary>
        private void TrySaveRegistry()
        {
            try
            {
                Registry.Save();
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                string message = $"라이브러리 저장 실패: {error.Message}";
                _lastError = string.IsNullOrEmpty(_lastError)
                    ? message
                    : _lastError + " | " + message;
            }
        }

        private bool EnsurePipeline()
        {
            if (_pipeline != null)
            {
                return true;
            }

            _pipeline = FindObjectOfType<FBXVmdPipeline>(true);
            return _pipeline != null;
        }

        private void SetState(LoadState state)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
            StateChanged?.Invoke(state);
        }

        private void OnDestroy()
        {
            CancelLoading();
            if (_cancellation != null)
            {
                _cancellation.Dispose();
                _cancellation = null;
            }
            if (_current == this)
            {
                _current = null;
            }
        }
    }
}
