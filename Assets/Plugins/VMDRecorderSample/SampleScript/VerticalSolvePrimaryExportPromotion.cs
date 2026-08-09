using System;

[Serializable]
public sealed class VerticalSolvePrimaryExportPromotion
{
    public string raw_metrics_csv;
    public string raw_vmd_path;
    public string raw_diagnostic_metrics_csv;
    public string raw_diagnostic_vmd_path;
    public string corrected_metrics_csv;
    public string corrected_vmd_path;
    public string integrated_manifest_path;
    public long promoted_vmd_bytes;
}
