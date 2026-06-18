using System;
using System.Globalization;

namespace Member_Han.Modules.Graphics
{
    [Serializable]
    public sealed class MainRecordingSettingsRuntimeState
    {
        public const string Playing = "playing";
        public const string Stopped = "stopped";

        public string playMode = Stopped;
        public string updatedAtUtc = string.Empty;

        public static MainRecordingSettingsRuntimeState Create(string playMode, DateTime utcNow)
        {
            return new MainRecordingSettingsRuntimeState
            {
                playMode = NormalizePlayMode(playMode),
                updatedAtUtc = utcNow.ToString("O"),
            };
        }

        public void Normalize()
        {
            playMode = NormalizePlayMode(playMode);
            if (!HasValidTimestamp(updatedAtUtc))
            {
                updatedAtUtc = string.Empty;
            }
        }

        public static string NormalizePlayMode(string value)
        {
            return string.Equals(value, Playing, StringComparison.OrdinalIgnoreCase)
                ? Playing
                : Stopped;
        }

        private static bool HasValidTimestamp(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   DateTime.TryParse(
                       value,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out _);
        }
    }
}
