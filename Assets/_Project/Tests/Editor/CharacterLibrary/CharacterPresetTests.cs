using Fbx2Vmd.CharacterLibrary;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using VRM;

namespace Tests.Editor.CharacterLibrary
{
    public class CharacterPresetTests
    {
        [Test]
        public void Given_PresetDocument_When_SaveAndLoad_Then_RoundTrips()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "presets", "abc.json");
            var store = new CharacterPresetStore(path);
            var document = new CharacterPresetDocument { characterId = "abc" };
            document.presets.Add(new CharacterPreset
            {
                id = "p1",
                name = "기본 포즈",
                sections = new CharacterPresetSections
                {
                    retarget =
                    {
                        new PresetFieldValue
                        {
                            key = "_GroundedFootLockWeight",
                            type = PresetFieldType.Float,
                            value = "0.7",
                        },
                    },
                    placement =
                    {
                        new PresetFieldValue
                        {
                            key = "position",
                            type = PresetFieldType.Vector3,
                            value = "1,2,3",
                        },
                    },
                },
            });

            store.Save(document);
            CharacterPresetDocument loaded = store.LoadOrCreateDefault();

            Assert.That(loaded.characterId, Is.EqualTo("abc"));
            Assert.That(loaded.presets.Count, Is.EqualTo(1));
            Assert.That(loaded.presets[0].name, Is.EqualTo("기본 포즈"));
            Assert.That(loaded.presets[0].sections.retarget.Count, Is.EqualTo(1));
            Assert.That(loaded.presets[0].sections.placement[0].value, Is.EqualTo("1,2,3"));
        }

        [Test]
        public void Given_CorruptPresetFile_When_Loading_Then_BacksUpAndReturnsDefault()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "abc.json");
            File.WriteAllText(path, "{ broken");

            var store = new CharacterPresetStore(path);
            CharacterPresetDocument loaded = store.LoadOrCreateDefault();

            Assert.That(loaded.presets, Is.Empty);
            Assert.That(Directory.GetFiles(folder, "abc.json.corrupt-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Given_LockedPresetFile_When_Saving_Then_BlockedAndFilePreserved()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "abc.json");
            string originalJson = "{\"schemaVersion\":1,\"presets\":[]}";
            File.WriteAllText(path, originalJson);

            var store = new CharacterPresetStore(path);
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Error, new System.Text.RegularExpressions.Regex("파일을 읽지 못했습니다"));
                store.LoadOrCreateDefault();
                Assert.That(store.LastReadFailed, Is.True);
                Assert.Throws<InvalidOperationException>(
                    () => store.Save(new CharacterPresetDocument()));
            }
            Assert.That(File.ReadAllText(path), Is.EqualTo(originalJson));
        }

        [Test]
        public void Given_DuplicatePresetIds_When_Normalizing_Then_KeepsFirstOnly()
        {
            var document = new CharacterPresetDocument();
            document.presets.Add(new CharacterPreset { id = "same", name = "첫째" });
            document.presets.Add(new CharacterPreset { id = "same", name = "둘째" });

            CharacterPresetDocument normalized =
                CharacterPresetDocumentNormalizer.Normalize(document);

            Assert.That(normalized.presets.Count, Is.EqualTo(1));
            Assert.That(normalized.presets[0].name, Is.EqualTo("첫째"));
        }

        [Test]
        public void Given_NullPresetDocument_When_Normalizing_Then_ReturnsDefault()
        {
            CharacterPresetDocument normalized =
                CharacterPresetDocumentNormalizer.Normalize(null);

            Assert.That(normalized, Is.Not.Null);
            Assert.That(normalized.schemaVersion, Is.EqualTo(1));
            Assert.That(normalized.presets, Is.Not.Null);
        }

        [Test]
        public void Given_PipelineValues_When_CaptureAndApply_Then_RestoresFields()
        {
            var host = new GameObject("pipeline");
            try
            {
                var pipeline = host.AddComponent<FBXVmdPipeline>();
                FieldInfo weightField = typeof(FBXVmdPipeline).GetField(
                    "_GroundedFootLockWeight",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(weightField, Is.Not.Null, "프리셋 대상 필드가 없습니다");
                weightField.SetValue(pipeline, 0.9f);

                var preset = CharacterPresetSnapshot.CreateEmpty("테스트");
                CharacterPresetSnapshot.CaptureRetarget(preset, pipeline);

                weightField.SetValue(pipeline, 0.1f);
                var warnings = CharacterPresetSnapshot.Apply(preset, pipeline, null, null);

                Assert.That(
                    (float)weightField.GetValue(pipeline),
                    Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(warnings, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Given_PresetWithUnknownField_When_Applying_Then_SkipsWithWarning()
        {
            var host = new GameObject("pipeline");
            try
            {
                var pipeline = host.AddComponent<FBXVmdPipeline>();
                var preset = CharacterPresetSnapshot.CreateEmpty("테스트");
                preset.sections.retarget.Add(new PresetFieldValue
                {
                    key = "_notARealField",
                    type = PresetFieldType.Float,
                    value = "1.0",
                });
                // 직렬화되지 않은 필드는 적용하지 않고 경고만 낸다.
                preset.sections.retarget.Add(new PresetFieldValue
                {
                    key = "_targetCharacter",
                    type = PresetFieldType.String,
                    value = "x",
                });

                var warnings = CharacterPresetSnapshot.Apply(preset, pipeline, null, null);

                Assert.That(warnings.Count, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Given_CharacterTransform_When_CaptureAndApplyPlacement_Then_Restores()
        {
            var root = new GameObject("char");
            var applied = new GameObject("char2");
            try
            {
                root.transform.position = new Vector3(1f, 2f, 3f);
                root.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                root.transform.localScale = new Vector3(2f, 2f, 2f);

                var preset = CharacterPresetSnapshot.CreateEmpty("테스트");
                CharacterPresetSnapshot.CapturePlacement(preset, root, null);

                applied.transform.position = Vector3.zero;
                applied.transform.rotation = Quaternion.identity;
                var warnings = CharacterPresetSnapshot.Apply(preset, null, applied, null);

                Assert.That(applied.transform.position, Is.EqualTo(root.transform.position));
                Assert.That(applied.transform.rotation.eulerAngles.y,
                    Is.EqualTo(45f).Within(0.01f));
                Assert.That(applied.transform.localScale, Is.EqualTo(root.transform.localScale));
                Assert.That(warnings, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(applied);
            }
        }

        [Test]
        public void Given_SpringBoneValues_When_CaptureAndApplyPhysics_Then_Restores()
        {
            var root = new GameObject("char");
            var boneA = new GameObject("a");
            var boneB = new GameObject("b");
            try
            {
                boneA.transform.SetParent(root.transform);
                boneB.transform.SetParent(root.transform);
                VRMSpringBone spring = boneA.AddComponent<VRMSpringBone>();
                spring.m_stiffnessForce = 2.5f;
                spring.m_dragForce = 0.7f;

                var preset = CharacterPresetSnapshot.CreateEmpty("테스트");
                CharacterPresetSnapshot.CapturePhysics(preset, root);
                Assert.That(preset.sections.physics.Count, Is.GreaterThan(0));

                spring.m_stiffnessForce = 0.1f;
                spring.m_dragForce = 0.1f;
                var warnings = CharacterPresetSnapshot.Apply(preset, null, root, null);

                Assert.That(spring.m_stiffnessForce, Is.EqualTo(2.5f).Within(0.0001f));
                Assert.That(spring.m_dragForce, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(warnings, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Given_CharacterIdWithTraversal_When_ResolvingPresetPath_Then_RejectsIt()
        {
            string libraryFile = Path.Combine("C:", "lib", "characters.json");

            Assert.That(
                CharacterLibraryPathResolver.ResolvePresetFilePath(libraryFile, "..\\evil"),
                Is.EqualTo(string.Empty));
            Assert.That(
                CharacterLibraryPathResolver.ResolvePresetFilePath(libraryFile, "abc"),
                Is.EqualTo(Path.Combine("C:", "lib", "presets", "abc.json")));
        }

        [Test]
        public void Given_PipelineGlobals_When_CaptureRetarget_Then_ExcludedFromPreset()
        {
            var host = new GameObject("pipeline");
            try
            {
                var pipeline = host.AddComponent<FBXVmdPipeline>();
                // 전역 설정 계열 필드가 실제로 존재하는지 먼저 확인한다.
                Assert.That(typeof(FBXVmdPipeline).GetField("_motionVideoOutputFolder",
                    BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
                Assert.That(typeof(FBXVmdPipeline).GetField("_showBoneMappingLog",
                    BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);

                var preset = CharacterPresetSnapshot.CreateEmpty("테스트");
                CharacterPresetSnapshot.CaptureRetarget(preset, pipeline);

                for (int i = 0; i < preset.sections.retarget.Count; i++)
                {
                    string key = preset.sections.retarget[i].key;
                    Assert.That(key, Does.Not.Contain("Folder"));
                    Assert.That(key, Does.Not.Contain("_show"));
                    Assert.That(key, Does.Not.Contain("Recording"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Given_GuardKeysWithoutGuard_When_Applying_Then_WarnsInsteadOfDropping()
        {
            var preset = CharacterPresetSnapshot.CreateEmpty("테스트");
            preset.sections.placement.Add(new PresetFieldValue
            {
                key = "guard._lockTargetPoseUntilImport",
                type = PresetFieldType.Bool,
                value = "false",
            });

            var warnings = CharacterPresetSnapshot.Apply(preset, null, null, null);

            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("가드"));
        }

        [Test]
        public void Given_MismatchedEnumType_When_Applying_Then_SkipsWithWarning()
        {
            var root = new GameObject("char");
            var bone = new GameObject("b");
            try
            {
                bone.transform.SetParent(root.transform);
                var spring = bone.AddComponent<VRMSpringBone>();
                spring.m_updateType = VRMSpringBone.SpringBoneUpdateType.LateUpdate;

                var preset = CharacterPresetSnapshot.CreateEmpty("테스트");
                preset.sections.physics.Add(new PresetFieldValue
                {
                    key = "spring[0].m_updateType",
                    type = PresetFieldType.EnumPrefix + "Some.Other.Enum",
                    value = "0",
                });

                var warnings = CharacterPresetSnapshot.Apply(preset, null, root, null);

                Assert.That(warnings.Count, Is.EqualTo(1));
                Assert.That(spring.m_updateType,
                    Is.EqualTo(VRMSpringBone.SpringBoneUpdateType.LateUpdate));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Given_Registry_When_OpeningPresetStoreTwice_Then_ReusesInstance()
        {
            string folder = CreateTempFolder();
            string libraryPath = Path.Combine(folder, "characters.json");
            var registry = new CharacterLibraryRegistry(libraryPath);

            CharacterPresetStore first = registry.OpenPresetStore("entry-a");
            CharacterPresetStore second = registry.OpenPresetStore("entry-a");

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first),
                "같은 엔트리의 스토어는 재사용돼 LastReadFailed 보호가 유지돼야 합니다");
        }

        [Test]
        public void Given_RegistryPresetStoreReadFailure_When_Saving_Then_Blocked()
        {
            string folder = CreateTempFolder();
            string libraryPath = Path.Combine(folder, "characters.json");
            var registry = new CharacterLibraryRegistry(libraryPath);
            string presetPath = registry.ResolvePresetFilePath("entry-b");
            Directory.CreateDirectory(Path.GetDirectoryName(presetPath));
            File.WriteAllText(presetPath, "{\"schemaVersion\":1,\"presets\":[]}");

            CharacterPresetStore store = registry.OpenPresetStore("entry-b");
            using (new FileStream(presetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Error, new System.Text.RegularExpressions.Regex("파일을 읽지 못했습니다"));
                registry.LoadPresets("entry-b");
                Assert.Throws<InvalidOperationException>(
                    () => store.Save(new CharacterPresetDocument()),
                    "읽기 실패가 관찰된 스토어는 저장이 차단돼야 합니다");
            }
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "UnityFbx2Vmd-CharacterPresetTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
