using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// 공식 프리셋 베이스라인 경로 검증.
    /// Resources/PhysicsPresets의 JSON을 ImportJson으로 읽는다.
    /// </summary>
    public class ClothPresetLibraryTests
    {
        [Test]
        public void Given_KnownPreset_When_TryImport_Then_FieldsPopulated()
        {
            var s = new ClothSerializeData();
            bool ok = ClothPresetLibrary.TryImport(s, ClothPresetLibrary.LongHair);
            Assert.IsTrue(ok);
            // 템플릿이 다루지 않는 필드가 프리셋값(0.2)으로 채워진다 — 기본값은 1.0
            Assert.That(s.wind.influence, Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void Given_UnknownPreset_When_TryImport_Then_FalseAndUnchanged()
        {
            var s = new ClothSerializeData();
            float before = s.wind.influence;
            bool ok = ClothPresetLibrary.TryImport(s, "MC2_Preset_DoesNotExist");
            Assert.IsFalse(ok);
            Assert.That(s.wind.influence, Is.EqualTo(before));
        }

        [Test]
        public void Given_NullOrEmpty_When_TryImport_Then_False()
        {
            var s = new ClothSerializeData();
            Assert.IsFalse(ClothPresetLibrary.TryImport(s, null));
            Assert.IsFalse(ClothPresetLibrary.TryImport(s, ""));
            Assert.IsFalse(ClothPresetLibrary.TryImport(null, ClothPresetLibrary.Skirt));
        }

        [Test]
        public void Given_BaselineOn_When_HairApply_Then_UnmanagedFieldsFromPreset()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true,
                HairTuning.Default, true);
            // 관리 필드는 템플릿이 덮어쓰고, 비관리 필드(wind)는 프리셋값 유지
            Assert.That(s.angleRestorationConstraint.stiffness.value,
                Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(s.wind.influence, Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void Given_BaselineOff_When_HairApply_Then_OnlyTemplateValues()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true,
                HairTuning.Default, false);
            Assert.That(s.wind.influence, Is.EqualTo(1.0f).Within(0.001f),
                "프리셋 미적용 — ClothSerializeData 기본값 유지");
        }

        [Test]
        public void Given_AllMappedPresets_When_TryImport_Then_AllFound()
        {
            // 자동화가 참조하는 프리셋 전부가 Resources에 존재하고 파싱되는지 확인
            foreach (string name in new[]
            {
                ClothPresetLibrary.FrontHair, ClothPresetLibrary.LongHair,
                ClothPresetLibrary.ShortHair, ClothPresetLibrary.Accessory,
                ClothPresetLibrary.Skirt,
            })
            {
                var s = new ClothSerializeData();
                Assert.IsTrue(ClothPresetLibrary.TryImport(s, name),
                    $"프리셋 누락 또는 파싱 실패: {name}");
            }
        }
    }
}
