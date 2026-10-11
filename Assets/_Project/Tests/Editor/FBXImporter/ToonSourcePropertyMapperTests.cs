using NUnit.Framework;
using UnityEngine;
using Fbx2Vmd.FBXImporter;

namespace Tests.Editor.FBXImporter
{
    public class ToonSourcePropertyMapperTests
    {
        // 머티리얼 컬러는 감마 변환으로 미세 오차가 생기므로 채널별 허용오차로 비교한다.
        private static void AssertColorNear(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f));
        }

        private static Material CreateMToonMaterial()
        {
            Shader shader = Shader.Find("VRM/MToon");
            Assert.IsNotNull(shader, "VRM/MToon 셰이더가 없음 — UniVRM 미설치 상태");
            return new Material(shader);
        }

        private static Material CreateLilToonMaterial()
        {
            Shader shader = Shader.Find("Hidden/lilToonCutoutOutline");
            Assert.IsNotNull(shader, "lilToon 셰이더를 찾을 수 없음");
            return new Material(shader);
        }

        [Test]
        public void MToon_음영색과_토니값이_툰으로_이식된다()
        {
            var source = CreateMToonMaterial();
            var target = CreateLilToonMaterial();
            var shadeColor = new Color(0.8f, 0.3f, 0.3f);
            source.SetColor("_ShadeColor", shadeColor);
            source.SetFloat("_ShadeToony", 1f);

            ToonSourcePropertyMapper.Apply(source, target);

            AssertColorNear(shadeColor, target.GetColor("_ShadowColor"));
            Assert.AreEqual(0f, target.GetFloat("_ShadowBlur"), 0.001f);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void MToon_아웃라인색이_이식된다()
        {
            var source = CreateMToonMaterial();
            var target = CreateLilToonMaterial();
            var outlineColor = new Color(0.1f, 0.2f, 0.9f);
            source.SetColor("_OutlineColor", outlineColor);

            ToonSourcePropertyMapper.Apply(source, target);

            AssertColorNear(outlineColor, target.GetColor("_OutlineColor"));
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void 소스에_없는_프로퍼티는_타겟을_바꾸지_않는다()
        {
            // Standard에는 _ShadeColor/_EdgeColor가 없으므로 매핑 없이 통과해야 한다.
            var source = new Material(Shader.Find("Standard"));
            var target = CreateLilToonMaterial();
            Color beforeOutline = target.GetColor("_OutlineColor");
            Color beforeShadow = target.GetColor("_ShadowColor");

            ToonSourcePropertyMapper.Apply(source, target);

            Assert.AreEqual(beforeOutline, target.GetColor("_OutlineColor"));
            Assert.AreEqual(beforeShadow, target.GetColor("_ShadowColor"));
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void null_입력에서_예외가_없다()
        {
            var target = CreateLilToonMaterial();
            Assert.DoesNotThrow(() => ToonSourcePropertyMapper.Apply(null, target));
            Assert.DoesNotThrow(() => ToonSourcePropertyMapper.Apply(target, null));
            Object.DestroyImmediate(target);
        }
    }
}
