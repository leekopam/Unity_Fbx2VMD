using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ToonMaterialRoleResolverTests
    {
        [TestCase("Hair", ToonMaterialRole.Hair)]
        [TestCase("hair_front", ToonMaterialRole.Hair)]
        [TestCase("髪", ToonMaterialRole.Hair)]
        [TestCase("Face", ToonMaterialRole.Face)]
        [TestCase("FaceMouth_00_FACE", ToonMaterialRole.Face)]
        [TestCase("顔", ToonMaterialRole.Face)]
        [TestCase("Skin", ToonMaterialRole.Skin)]
        [TestCase("body", ToonMaterialRole.Skin)]
        [TestCase("肌", ToonMaterialRole.Skin)]
        [TestCase("Eye_L", ToonMaterialRole.Eye)]
        [TestCase("瞳", ToonMaterialRole.Eye)]
        public void Given_MaterialName_When_ResolveCharacterRole_Then_ReturnsExpectedRole(
            string materialName, ToonMaterialRole expected)
        {
            Assert.That(
                ToonMaterialRoleResolver.ResolveCharacterRole(materialName),
                Is.EqualTo(expected));
        }

        [TestCase("eyelash")]
        [TestCase("Eyebrow")]
        [TestCase("まつ毛")]
        [TestCase("Cloth")]
        [TestCase("Material #25")]
        [TestCase("")]
        [TestCase(null)]
        public void Given_NonCharacterOrExcludedName_When_ResolveCharacterRole_Then_ReturnsNull(
            string materialName)
        {
            Assert.That(
                ToonMaterialRoleResolver.ResolveCharacterRole(materialName),
                Is.Null);
        }

        [Test]
        public void Given_TemplateState_When_TryInstantiate_Then_MatchesResourcePresence()
        {
            Material template = Resources.Load<Material>("ToonPresets/Toon_Opaque");

            bool result = ToonMaterialLibrary.TryInstantiate(
                ToonMaterialRole.Opaque, ToonMaterialRole.Opaque, out Material material);

            Assert.That(result, Is.EqualTo(template != null));
            if (result)
            {
                Assert.That(material.shader, Is.EqualTo(template.shader));
                UnityEngine.Object.DestroyImmediate(material);
            }
            else
            {
                Assert.That(material, Is.Null);
            }
        }

        [Test]
        public void Given_TemplateCreated_When_TryInstantiate_Then_ClonesAndKeepsShader()
        {
            var template = new Material(Shader.Find("Standard"))
            {
                name = "Toon_Opaque"
            };

            Material clone = new Material(template);
            try
            {
                Assert.That(clone, Is.Not.SameAs(template));
                Assert.That(clone.shader, Is.EqualTo(template.shader));
            }
            finally
            {
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(template);
            }
        }
    }
}
