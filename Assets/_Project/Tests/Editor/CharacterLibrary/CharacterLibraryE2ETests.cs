using Fbx2Vmd.CharacterLibrary;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VRM;
using Object = UnityEngine.Object;

namespace Tests.Editor.CharacterLibrary
{
    /// <summary>
    /// 실제 VRM 파일(HatsuneMikuNT.vrm)로 등록→로드→프리셋 왕복→영속화→정리까지 검증하는 E2E.
    /// 테스트용 VRM이 없으면 건너뛴다.
    /// </summary>
    public class CharacterLibraryE2ETests
    {
        private static string RealVrmPath => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Docs", "ref", "ExportedProject",
            "ExportedProject", "Assets", "StreamingAssets", "Characters",
            "HatsuneMikuNT.vrm"));

        [Test]
        public async Task E2E_RealVrm_RegisterLoadPreset_PersistAndCleanup()
        {
            string vrmPath = RealVrmPath;
            if (!File.Exists(vrmPath))
            {
                Assert.Ignore($"E2E용 VRM이 없습니다: {vrmPath}");
            }

            string folder = Path.Combine(
                Path.GetTempPath(),
                "UnityFbx2Vmd-CharacterLibraryE2E",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string libraryFile = Path.Combine(folder, "characters.json");
            // 실제 메타 리더로 VRM 메타 추출까지 검증한다.
            var registry = new CharacterLibraryRegistry(libraryFile);
            CharacterLoader.Result loaded = null;
            GameObject pipelineHost = null;

            try
            {
                // 1. 등록 — 실제 VRM 파일에서 메타/해시 추출
                CharacterLibraryRegistry.RegisterResult registered = registry.Register(vrmPath);
                Assert.That(registered.success, Is.True, registered.error);
                CharacterLibraryEntry entry = registered.entry;
                Assert.That(entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Ready));
                Assert.That(entry.displayName, Is.Not.Empty);
                Assert.That(entry.contentHash, Does.StartWith("sha256:"));

                // 2. 로드 — Humanoid Avatar와 스프링본이 있어야 파이프라인 대상이 된다
                var loader = new CharacterLoader();
                loaded = await loader.LoadAsync(entry.sourcePath, CancellationToken.None);
                Assert.That(loaded.success, Is.True, loaded.error);
                GameObject root = loaded.instance.Root;
                Assert.That(root, Is.Not.Null);

                Animator animator = root.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.avatar, Is.Not.Null);
                Assert.That(animator.avatar.isHuman, Is.True);
                VRMSpringBone[] springs = root.GetComponentsInChildren<VRMSpringBone>(true);
                Assert.That(springs.Length, Is.GreaterThan(0), "VRM에 스프링본이 없습니다");

                // 3. 프리셋 캡처 → 값 변경 → 적용으로 복원
                pipelineHost = new GameObject("pipeline");
                var pipeline = pipelineHost.AddComponent<FBXVmdPipeline>();
                root.transform.position = new Vector3(1f, 0f, 2f);
                float originalStiffness = springs[0].m_stiffnessForce;

                CharacterPreset preset = CharacterPresetSnapshot.CreateEmpty("E2E");
                preset.id = "e2e-preset";
                CharacterPresetSnapshot.CaptureRetarget(preset, pipeline);
                CharacterPresetSnapshot.CapturePlacement(preset, root, null);
                CharacterPresetSnapshot.CapturePhysics(preset, root);
                Assert.That(preset.sections.retarget.Count, Is.GreaterThan(0));
                Assert.That(preset.sections.physics.Count, Is.GreaterThan(0));

                root.transform.position = Vector3.zero;
                springs[0].m_stiffnessForce = originalStiffness + 0.5f;
                var warnings = CharacterPresetSnapshot.Apply(preset, pipeline, root, null);

                Assert.That(root.transform.position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
                Assert.That(springs[0].m_stiffnessForce,
                    Is.EqualTo(originalStiffness).Within(0.0001f));
                Assert.That(warnings, Is.Empty, string.Join("; ", warnings));

                // 4. 프리셋 저장 + 기본 지정 → 새 레지스트리로 재열어 영속화 확인
                CharacterPresetStore store = registry.OpenPresetStore(entry.id);
                CharacterPresetDocument document = store.LoadOrCreateDefault();
                document.presets.Add(preset);
                store.Save(document);
                registry.SyncPresetIds(entry.id, new[] { preset.id });
                Assert.That(registry.SetDefaultPreset(entry.id, preset.id), Is.True);

                var fresh = new CharacterLibraryRegistry(libraryFile);
                Assert.That(fresh.FindPreset(entry.id, preset.id), Is.Not.Null);
                Assert.That(fresh.FindById(entry.id).defaultPresetId,
                    Is.EqualTo(preset.id));

                // 5. 정리 — 엔트리 제거 시 프리셋 파일도 함께 삭제돼야 한다
                string presetPath = registry.ResolvePresetFilePath(entry.id);
                Assert.That(File.Exists(presetPath), Is.True);
                Assert.That(registry.Remove(entry.id), Is.True);
                Assert.That(File.Exists(presetPath), Is.False);
            }
            finally
            {
                if (loaded != null)
                {
                    CharacterLoader.DestroyInstance(loaded.instance);
                }
                if (pipelineHost != null)
                {
                    Object.DestroyImmediate(pipelineHost);
                }
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }
    }
}
