#if UNITY_EDITOR
using UnityEditor;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class VisualComparisonEnterPlayModeOptionsController
    {
        private bool _isCaptured;
        private bool _previousOptionsEnabled;
        private EnterPlayModeOptions _previousOptions;

        internal bool Apply(bool isBatchMode)
        {
            if (isBatchMode || _isCaptured)
            {
                return false;
            }

            _previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _previousOptions = EditorSettings.enterPlayModeOptions;
            _isCaptured = true;

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                _previousOptions | EnterPlayModeOptions.DisableDomainReload;
            return true;
        }

        internal bool Restore()
        {
            if (!_isCaptured)
            {
                return false;
            }

            EditorSettings.enterPlayModeOptions = _previousOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _previousOptionsEnabled;
            _isCaptured = false;
            return true;
        }
    }
}
#endif
