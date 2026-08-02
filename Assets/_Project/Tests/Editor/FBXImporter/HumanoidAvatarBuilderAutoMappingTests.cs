using System.Collections.Generic;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidAvatarBuilderAutoMappingTests
    {
        [Test]
        public void Given_MixamoPrefixBones_When_BuildAutoMapping_Then_MapsPresentRequiredBones()
        {
            var root = new GameObject("TestRigRoot");
            try
            {
                Transform hips = new GameObject("mixamorig:Hips").transform;
                hips.SetParent(root.transform, false);

                Transform spine = new GameObject("mixamorig:Spine").transform;
                spine.SetParent(hips, false);

                Transform chest = new GameObject("mixamorig:Chest").transform;
                chest.SetParent(spine, false);

                Transform neck = new GameObject("mixamorig:Neck").transform;
                neck.SetParent(chest, false);

                Transform head = new GameObject("mixamorig:Head").transform;
                head.SetParent(neck, false);

                Dictionary<string, string> mapping = HumanoidAvatarBuilder.BuildAutoMapping(root);

                Assert.IsNotNull(mapping);
                Assert.Greater(mapping.Count, 0, "Auto mapping should map at least one required bone.");

                Assert.That(mapping.ContainsKey("Hips"), "Hips must be mapped when present.");
                Assert.AreEqual("mixamorig:Hips", mapping["Hips"]);

                Assert.That(mapping.ContainsKey("Spine"), "Spine must be mapped when present.");
                Assert.AreEqual("mixamorig:Spine", mapping["Spine"]);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

