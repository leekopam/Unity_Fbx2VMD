namespace Fbx2Vmd.Settings
{
    public readonly struct MainRecordingSettingsActionResult
    {
        public MainRecordingSettingsActionResult(bool succeeded, string userMessage)
        {
            Succeeded = succeeded;
            UserMessage = userMessage ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string UserMessage { get; }

        public static MainRecordingSettingsActionResult Success(string userMessage)
        {
            return new MainRecordingSettingsActionResult(true, userMessage);
        }

        public static MainRecordingSettingsActionResult Failure(string userMessage)
        {
            return new MainRecordingSettingsActionResult(false, userMessage);
        }
    }
}
