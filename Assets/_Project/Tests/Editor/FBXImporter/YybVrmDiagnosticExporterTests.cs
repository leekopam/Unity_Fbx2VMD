using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class YybVrmDiagnosticExporterTests
{
    [Test]
    public void ExportYybModelProducesGlb()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab");
        Assert.IsNotNull(prefab);
        GameObject model = UnityEngine.Object.Instantiate(prefab);
        try
        {
            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx");
            Assert.IsNotNull(avatar);
            model.GetComponent<Animator>().avatar = avatar;
            UnityEngine.Object.DestroyImmediate(model.GetComponent<IKControl>());
            byte[] bytes = YybVrmDiagnosticExporter.Export(model);
            Assert.Greater(bytes.Length, 20);
            Assert.AreEqual("glTF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(model);
        }
    }
}
