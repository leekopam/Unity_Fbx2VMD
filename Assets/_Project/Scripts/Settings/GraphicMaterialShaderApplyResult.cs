namespace Fbx2Vmd.Settings
{
public readonly struct GraphicMaterialShaderApplyResult
    {
        public GraphicMaterialShaderApplyResult(
            int processedMaterials,
            int changedMaterials,
            int changedProperties,
            int skippedMaterials,
            int skippedProperties)
        {
            ProcessedMaterials = processedMaterials;
            ChangedMaterials = changedMaterials;
            ChangedProperties = changedProperties;
            SkippedMaterials = skippedMaterials;
            SkippedProperties = skippedProperties;
        }

        public int ProcessedMaterials { get; }
        public int ChangedMaterials { get; }
        public int ChangedProperties { get; }
        public int SkippedMaterials { get; }
        public int SkippedProperties { get; }

        public override string ToString()
        {
            return
                $"processed={ProcessedMaterials}, changedMaterials={ChangedMaterials}, changedProperties={ChangedProperties}, skippedMaterials={SkippedMaterials}, skippedProperties={SkippedProperties}";
        }
    }
}
