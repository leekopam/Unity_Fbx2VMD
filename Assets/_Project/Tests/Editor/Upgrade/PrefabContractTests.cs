using NUnit.Framework;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 프리팹 무결성 계약: Assets 전체 프리팹에서 MissingScript=0.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class PrefabContractTests
    {
        [Test]
        public void AllPrefabs_HaveNoMissingScripts()
        {
            var failures = new StringBuilder();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int missing = 0;
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                    }

                    if (missing > 0)
                    {
                        failures.AppendLine($"{path}: missing={missing}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Assert.That(failures.ToString(), Is.Empty,
                "Missing Script가 있는 프리팹이 있음 (스크립트/패키지 타입 손실 신호)");
        }
    }
}
