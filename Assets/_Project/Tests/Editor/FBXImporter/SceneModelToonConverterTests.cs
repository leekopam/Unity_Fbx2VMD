using Fbx2Vmd.EditorTools;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class SceneModelToonConverterTests
    {
        [Test]
        public void Given_FaceNamedSource_When_CreateToonVariant_Then_ToonShaderAndTextureKept()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var source = new Material(Shader.Find("Standard"))
            {
                name = "5.face01",
                mainTexture = texture
            };

            Material variant = SceneModelToonConverter.CreateToonVariant(source);
            try
            {
                Assert.That(variant, Is.Not.Null);
                Assert.That(variant.shader.name, Does.Contain("lilToon"));
                Assert.That(variant.mainTexture, Is.EqualTo(texture));
                Assert.That(variant.name, Is.EqualTo(source.name));
            }
            finally
            {
                Object.DestroyImmediate(variant);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Given_ToonSource_When_CreateToonVariant_Then_ReturnsNull()
        {
            if (!ToonMaterialLibrary.TryInstantiate(
                    ToonMaterialRole.Opaque, ToonMaterialRole.Opaque, out Material toon))
            {
                Assert.Ignore("툰 프리셋이 아직 생성되지 않음");
            }

            try
            {
                Assert.That(SceneModelToonConverter.CreateToonVariant(toon), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(toon);
            }
        }

        [Test]
        public void Given_NullSource_When_CreateToonVariant_Then_ReturnsNull()
        {
            Assert.That(SceneModelToonConverter.CreateToonVariant(null), Is.Null);
        }

        [Test]
        public void Given_MToonSource_When_CreateToonVariant_Then_ShadeColorTransferred()
        {
            Shader mtoon = Shader.Find("VRM/MToon");
            if (mtoon == null)
            {
                Assert.Ignore("VRM/MToon 셰이더를 찾을 수 없음");
            }

            var source = new Material(mtoon) { name = "12.face" };
            var shadeColor = new Color(0.7f, 0.4f, 0.4f);
            source.SetColor("_ShadeColor", shadeColor);

            Material variant = SceneModelToonConverter.CreateToonVariant(source);
            try
            {
                Assert.That(variant, Is.Not.Null);
                Assert.That(variant.shader.name, Does.Contain("lilToon"));
                // 머티리얼 컬러는 감마 변환으로 미세한 오차가 생기므로 채널별 허용오차로 비교한다.
                Color actualShadow = variant.GetColor("_ShadowColor");
                Assert.That(actualShadow.r, Is.EqualTo(shadeColor.r).Within(0.01f));
                Assert.That(actualShadow.g, Is.EqualTo(shadeColor.g).Within(0.01f));
                Assert.That(actualShadow.b, Is.EqualTo(shadeColor.b).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(variant);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void Given_FaceSourceWithSdfTexture_When_CreateToonVariant_Then_SdfMaskAttached()
        {
            const string root = "Assets/ToonSdfTest_Temp";
            try
            {
                AssetDatabase.CreateFolder("Assets", "ToonSdfTest_Temp");
                AssetDatabase.CreateFolder(root, "Materials");
                AssetDatabase.CreateFolder(root, "sdf");

                var source = new Material(Shader.Find("Standard")) { name = "9.face99" };
                AssetDatabase.CreateAsset(source, $"{root}/Materials/9.face99.mat");

                var sdf = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                // CreateAsset로 .png 확장을 쓰면 Texture2D로 인식되지 않으므로 .asset으로 만든다.
                AssetDatabase.CreateAsset(sdf, $"{root}/sdf/9.face99_sdf.asset");
                AssetDatabase.Refresh();

                Material variant = SceneModelToonConverter.CreateToonVariant(source);
                try
                {
                    Assert.That(variant, Is.Not.Null);
                    Assert.That(variant.GetFloat("_ShadowMaskType"), Is.EqualTo(2f));
                    // 방위각 SDF와 맞추기 위해 Y 성분 비교는 꺼져 있어야 한다.
                    Assert.That(variant.GetFloat("_ShadowFlatBlur"), Is.EqualTo(0f));
                    Assert.That(variant.GetTexture("_ShadowStrengthMask"), Is.EqualTo(sdf));
                }
                finally
                {
                    Object.DestroyImmediate(variant);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Given_SdfTextureNamedByBaseName_When_CreateToonVariant_Then_SdfMaskAttached()
        {
            // "9.face97" 머티리얼에 접두 없는 "face97_sdf" 텍스처도 인식한다.
            const string root = "Assets/ToonSdfTest_Temp";
            try
            {
                AssetDatabase.CreateFolder("Assets", "ToonSdfTest_Temp");
                AssetDatabase.CreateFolder(root, "Materials");
                AssetDatabase.CreateFolder(root, "sdf");

                var source = new Material(Shader.Find("Standard")) { name = "9.face97" };
                AssetDatabase.CreateAsset(source, $"{root}/Materials/9.face97.mat");

                var sdf = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                AssetDatabase.CreateAsset(sdf, $"{root}/sdf/face97_sdf.asset");
                AssetDatabase.Refresh();

                Material variant = SceneModelToonConverter.CreateToonVariant(source);
                try
                {
                    Assert.That(variant, Is.Not.Null);
                    Assert.That(variant.GetFloat("_ShadowMaskType"), Is.EqualTo(2f));
                    Assert.That(variant.GetFloat("_ShadowFlatBlur"), Is.EqualTo(0f));
                    Assert.That(variant.GetTexture("_ShadowStrengthMask"), Is.EqualTo(sdf));
                }
                finally
                {
                    Object.DestroyImmediate(variant);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Given_FaceSourceWithoutSdfTexture_When_CreateToonVariant_Then_MaskTypeKept()
        {
            const string root = "Assets/ToonSdfTest_Temp";
            try
            {
                AssetDatabase.CreateFolder("Assets", "ToonSdfTest_Temp");
                AssetDatabase.CreateFolder(root, "Materials");

                var source = new Material(Shader.Find("Standard")) { name = "9.face98" };
                AssetDatabase.CreateAsset(source, $"{root}/Materials/9.face98.mat");
                AssetDatabase.Refresh();

                Material variant = SceneModelToonConverter.CreateToonVariant(source);
                try
                {
                    // SDF 텍스처가 없는 캐릭터는 현행 Face 파라미터(maskType=0)를 유지한다.
                    Assert.That(variant, Is.Not.Null);
                    Assert.That(variant.GetFloat("_ShadowMaskType"), Is.EqualTo(0f));
                    Assert.That(variant.GetTexture("_ShadowStrengthMask"), Is.Null);
                }
                finally
                {
                    Object.DestroyImmediate(variant);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Given_ToonVariantExists_When_ApplyToRunsAgain_Then_AssetGuidIsKept()
        {
            // 재변환 시 Toon_*.mat을 지우고 다시 만들면 GUID가 바뀌어
            // 변환본을 참조하는 다른 씬·프리팹이 깨진다 — 덮어쓰기로 ID를 유지해야 한다.
            const string root = "Assets/ToonGuidTest_Temp";
            var go = new GameObject("sdf-guid-test");
            try
            {
                AssetDatabase.CreateFolder("Assets", "ToonGuidTest_Temp");
                var source = new Material(Shader.Find("Standard")) { name = "10.guid" };
                AssetDatabase.CreateAsset(source, $"{root}/10.guid.mat");

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = source;

                SceneModelToonConverter.ApplyTo(go);
                string path = $"{root}/Toon/Toon_10.guid.mat";
                string guid1 = AssetDatabase.AssetPathToGUID(path);
                Assert.That(guid1, Is.Not.Empty);

                // 재변환 — 원본을 다시 달고 실행하면 기존 변환본을 덮어쓴다.
                renderer.sharedMaterial = source;
                SceneModelToonConverter.ApplyTo(go);
                string guid2 = AssetDatabase.AssetPathToGUID(path);
                Assert.That(guid2, Is.EqualTo(guid1));
                Assert.That(renderer.sharedMaterial,
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(path)));
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Given_ToonVariantAsset_When_ResolveSourceMaterial_Then_ReturnsOriginal()
        {
            // 이미 변환된 모델에서 베이크해도 원본 이름 규약(SDF 파일명)을 써야 한다.
            const string root = "Assets/ToonResolveTest_Temp";
            var go = new GameObject("resolve-test");
            try
            {
                AssetDatabase.CreateFolder("Assets", "ToonResolveTest_Temp");
                var source = new Material(Shader.Find("Standard")) { name = "11.face96" };
                AssetDatabase.CreateAsset(source, $"{root}/11.face96.mat");
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = source;
                SceneModelToonConverter.ApplyTo(go);

                Material variant = renderer.sharedMaterial;
                Assert.That(variant, Is.Not.EqualTo(source), "검증 전제 — 변환본이 달려야 함");
                Assert.That(SceneModelToonConverter.ResolveSourceMaterial(variant), Is.EqualTo(source));
                Assert.That(SceneModelToonConverter.ResolveSourceMaterial(source), Is.EqualTo(source));
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Given_NecktieNamedSource_When_CreateToonVariant_Then_IridescentMatCapAttached()
        {
            // 넥타이·소매·스커트 계열은 재변환해도 홀로그램 맷캡이 다시 붙어야 한다.
            var source = new Material(Shader.Find("Standard")) { name = "45.necktie" };

            Material variant = SceneModelToonConverter.CreateToonVariant(source);
            try
            {
                Assert.That(variant, Is.Not.Null);
                Assert.That(variant.GetFloat("_UseMatCap"), Is.EqualTo(1f));
                Assert.That(variant.GetTexture("_MatCapTex"), Is.Not.Null);
                Assert.That(variant.GetFloat("_MatCapBlendMode"), Is.EqualTo(1f));
                Assert.That(variant.GetFloat("_MatCapBlend"), Is.EqualTo(0.55f));
                Assert.That(variant.GetFloat("_MatCapZRotCancel"), Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(variant);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void Given_BodyNamedSource_When_CreateToonVariant_Then_MatCapKeptOff()
        {
            // 의상 부위가 아닌 머티리얼에는 맷캡을 붙이지 않는다.
            var source = new Material(Shader.Find("Standard")) { name = "0.body01" };

            Material variant = SceneModelToonConverter.CreateToonVariant(source);
            try
            {
                Assert.That(variant, Is.Not.Null);
                Assert.That(variant.GetFloat("_UseMatCap"), Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(variant);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void Given_TransparentShaderSource_When_CreateToonVariant_Then_AlphaSurface()
        {
            Shader transparent = Shader.Find("Unlit/Transparent");
            if (transparent == null)
            {
                Assert.Ignore("Unlit/Transparent 셰이더를 찾을 수 없음");
            }

            var source = new Material(transparent) { name = "6.eyes" };

            Material variant = SceneModelToonConverter.CreateToonVariant(source);
            try
            {
                // 알파가 있는 눈 슬롯은 불투명 Eye가 아니라 Cutout 계열로 내려가야 한다.
                Assert.That(variant, Is.Not.Null);
                Assert.That(variant.shader.name, Does.Contain("lilToon"));
                Assert.That(
                    variant.GetTag("RenderType", false),
                    Is.Not.EqualTo("Opaque"));
            }
            finally
            {
                Object.DestroyImmediate(variant);
                Object.DestroyImmediate(source);
            }
        }
    }
}
