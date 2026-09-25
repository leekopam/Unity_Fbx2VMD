using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static class YybVrmDiagnosticExporter
    {
        public static byte[] Export(GameObject model)
        {
            VRM.VRMMetaObject meta = null;
            VRM.VRMExportSettings settings = null;
            try
            {
                // YYB 영문 Readme의 제작자와 재배포 금지 조건을 진단 출력에 반영함.
                meta = ScriptableObject.CreateInstance<VRM.VRMMetaObject>();
                meta.Title = "Hatsune Miku";
                meta.Version = "1.0";
                meta.Author = "SANMUYYB";
                meta.AllowedUser = VRM.AllowedUser.Everyone;
                meta.CommercialUssage = VRM.UssageLicense.Disallow;
                meta.ViolentUssage = VRM.UssageLicense.Disallow;
                meta.SexualUssage = VRM.UssageLicense.Disallow;
                meta.LicenseType = VRM.LicenseType.Redistribution_Prohibited;

                settings = ScriptableObject.CreateInstance<VRM.VRMExportSettings>();
                settings.PoseFreeze = true;
                settings.ForceTPose = true;
                settings.FreezeMeshUseCurrentBlendShapeWeight = false;
                return VRM.VRMEditorExporter.Export(model, meta, settings);
            }
            finally
            {
                if (settings != null) Object.DestroyImmediate(settings);
                if (meta != null) Object.DestroyImmediate(meta);
            }
        }
    }
}
