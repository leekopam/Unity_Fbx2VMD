namespace Fbx2Vmd.FBXImporter
{
    internal sealed class ReferenceVideoTimingPlan
    {
        public bool Enabled { get; set; }
        public bool HasCandidateTimingOverride { get; set; }
        public float ReferenceVideoStartSeconds { get; set; }
        public float CandidateClipStartSeconds { get; set; }
        public float CandidateClipSecondsPerReferenceSecond { get; set; }
        public float ReferenceDurationSeconds { get; set; }
    }
}
