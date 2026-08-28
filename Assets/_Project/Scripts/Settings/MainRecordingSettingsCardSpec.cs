using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public readonly struct MainRecordingSettingsCardSpec
    {
        public MainRecordingSettingsCardSpec(
            string title,
            string body,
            string buttonLabel,
            Color backgroundColor,
            Color bodyTextColor,
            MainRecordingSettingsActionType action,
            bool enabled)
        {
            Title = title;
            Body = body;
            ButtonLabel = buttonLabel;
            BackgroundColor = backgroundColor;
            BodyTextColor = bodyTextColor;
            Action = action;
            Enabled = enabled;
        }

        public string Title { get; }
        public string Body { get; }
        public string ButtonLabel { get; }
        public Color BackgroundColor { get; }
        public Color BodyTextColor { get; }
        public MainRecordingSettingsActionType Action { get; }
        public bool Enabled { get; }
    }
}
