using System;

namespace Fbx2Vmd.FBXImporter
{
    [Serializable]
    internal class VisualComparisonCandidateArtifactSelectionData
    {
        public string selected_candidate_role;
        public string selected_candidate_output_role;
        public string selected_candidate_status;
        public string selected_candidate_status_reason;
        public string selected_candidate_metrics_csv;
        public string selected_candidate_vmd_path;
        public bool selected_candidate_preserves_raw_diagnostic;
        public string selected_candidate_manifest_path;
        public bool selected_candidate_vmd_exists;
        public bool selected_candidate_metrics_exists;
        public bool selected_candidate_manifest_exists;
        public bool selected_candidate_differs_from_raw_vmd;
        public bool selected_candidate_differs_from_raw_metrics;
        public bool selected_candidate_is_acceptance_artifact;
        public string selected_candidate_acceptance_basis;
        public string raw_candidate_status;
        public string raw_candidate_status_reason;
        public string raw_candidate_metrics_csv;
        public string raw_candidate_vmd_path;
        public string corrected_candidate_status;
        public string corrected_candidate_status_reason;
        public string corrected_candidate_metrics_csv;
        public string corrected_candidate_vmd_path;
        public string selection_basis;
    }
}
