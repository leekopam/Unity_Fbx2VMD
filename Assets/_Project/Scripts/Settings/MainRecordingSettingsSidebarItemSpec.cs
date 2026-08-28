namespace Fbx2Vmd.Settings
{
    public readonly struct MainRecordingSettingsSidebarItemSpec
    {
        public MainRecordingSettingsSidebarItemSpec(string label, string iconName, bool active, bool enabled)
        {
            Label = label;
            IconName = iconName;
            Active = active;
            Enabled = enabled;
        }

        public string Label { get; }
        public string IconName { get; }
        public bool Active { get; }
        public bool Enabled { get; }
    }
}
