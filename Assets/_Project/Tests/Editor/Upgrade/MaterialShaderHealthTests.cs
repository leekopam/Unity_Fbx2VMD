using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 머티리얼/셰이더 건강 계약: 셰이더 미할당·에러 셰이더 0건, 셰이더 인벤토리 JSON 내보내기.
    /// URP 버전 변경으로 패키지 셰이더(lilToon 등)가 깨지면 여기서 드러난다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class MaterialShaderHealthTests
    {
        [Test]
        public void AllMaterials_HaveValidShader()
        {
            var failures = new StringBuilder();

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                if (material.shader == null)
                {
                    failures.AppendLine($"{path}: shader=null");
                }
                else if (material.shader.name == "Hidden/InternalErrorShader")
                {
                    failures.AppendLine($"{path}: shader=InternalErrorShader");
                }
            }

            Assert.That(failures.ToString(), Is.Empty,
                "셰이더가 없거나 에러 셰이더인 머티리얼이 있음");
        }

        [Test]
        public void ShaderInventory_IsExportedForDiff()
        {
            var usage = new Dictionary<string, int>();

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                string shaderName = material == null || material.shader == null
                    ? "<missing-shader>"
                    : material.shader.name;

                usage[shaderName] = usage.TryGetValue(shaderName, out int n) ? n + 1 : 1;
            }

            var report = new StringBuilder("{\n");
            foreach (var pair in usage.OrderBy(p => p.Key))
            {
                report.Append("  \"").Append(pair.Key).Append("\": ").Append(pair.Value).Append(",\n");
            }
            if (report.Length > 2)
            {
                report.Remove(report.Length - 2, 1); // 후행 콤마 제거
            }
            report.Append("}\n");

            string reportPath = UpgradeReadinessReport.Write("material-shaders.json", report.ToString());
            Assert.That(File.Exists(reportPath), Is.True, "셰이더 인벤토리 리포트가 작성되지 않음");
        }
    }
}
