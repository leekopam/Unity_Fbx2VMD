using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorHumanoidClipImportConfiguratorTests
    {
        [Test]
        public void Given_TemplateRigWithoutChest_When_EnsuringHumanoid_Then_CompletesChestWithoutReplacingExistingDescription()
        {
            const string source = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            if (!File.Exists(source)) Assert.Ignore("로컬 입력 FBX가 없는 환경에서는 실제 임포트 검증을 생략함");
            string folder = "Assets/HumanoidChestImport_" + Guid.NewGuid().ToString("N");
            string path = folder + "/satisfaction_2.fbx";
            string originalMeta = File.ReadAllText(source + ".meta");
            bool ownsFolder = false;
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
                ownsFolder = true;
                File.Copy(source, path);
                string meta = originalMeta;
                meta = System.Text.RegularExpressions.Regex.Replace(meta,
                    @"(?m)^guid: [0-9a-fA-F]{32}\r?$", "guid: " + Guid.NewGuid().ToString("N"));
                File.WriteAllText(path + ".meta", meta);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.DontDownloadFromCacheServer);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                HumanDescription input = importer.humanDescription;
                input.human = input.human.Where(bone => bone.humanName != "Chest").ToArray();
                importer.humanDescription = input;
                importer.SaveAndReimport();
                importer = (ModelImporter)AssetImporter.GetAtPath(path);
                HumanDescription before = importer.humanDescription;
                string clipSettings = CaptureClipSettings(path);
                Assert.That(clipSettings, Does.Contain("keepOriginalOrientation"));
                byte[] sourceBytes = File.ReadAllBytes(path);

                EnsureHumanoid(path);

                importer = (ModelImporter)AssetImporter.GetAtPath(path);
                HumanDescription after = importer.humanDescription;
                Assert.That(after.human.SingleOrDefault(bone => bone.humanName == "Chest").boneName,
                    Is.EqualTo("Skeleton_Spine1"));
                Assert.That(after.human.Where(bone => bone.humanName != "Chest"), Is.EqualTo(before.human));
                Assert.That(after.skeleton, Is.EqualTo(before.skeleton));
                Assert.That(after.armStretch, Is.EqualTo(before.armStretch));
                Assert.That(after.legStretch, Is.EqualTo(before.legStretch));
                Assert.That(after.upperArmTwist, Is.EqualTo(before.upperArmTwist));
                Assert.That(after.lowerArmTwist, Is.EqualTo(before.lowerArmTwist));
                Assert.That(after.upperLegTwist, Is.EqualTo(before.upperLegTwist));
                Assert.That(after.lowerLegTwist, Is.EqualTo(before.lowerLegTwist));
                Assert.That(after.hasTranslationDoF, Is.EqualTo(before.hasTranslationDoF));
                Assert.That(CaptureClipSettings(path), Is.EqualTo(clipSettings));
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Animator animator = model.GetComponentInChildren<Animator>(true);
                Assert.That(animator.GetBoneTransform(HumanBodyBones.Chest)?.name, Is.EqualTo("Skeleton_Spine1"));
                Assert.That(EnsureHumanoid(path), Is.False, "재실행 시 같은 설정을 변경하지 않아야 함");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(sourceBytes));
                Assert.That(File.ReadAllBytes(source), Is.EqualTo(sourceBytes));
                Assert.That(File.ReadAllText(source + ".meta"), Is.EqualTo(originalMeta));
            }
            finally
            {
                if (ownsFolder) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "검증용 에셋 정리 실패");
            }
        }

        [TestCase("compatible", true)]
        [TestCase("existingChest", false)]
        [TestCase("extraMissing", false)]
        [TestCase("wrongMapping", false)]
        [TestCase("wrongHierarchy", false)]
        [TestCase("duplicateBone", false)]
        [TestCase("aliasChest", false)]
        [TestCase("duplicateRole", false)]
        [TestCase("normalizedTemplateDuplicate", false)]
        [TestCase("emptyTemplateValue", false)]
        public void Given_TemplateChestMapping_When_Completing_Then_PreservesExistingEntriesOrAbstains(string scenario, bool expected)
        {
            var root = new GameObject("템플릿 가슴 본 검증");
            try
            {
                Transform Add(string name, Transform parent)
                {
                    var bone = new GameObject("Skeleton_" + name).transform;
                    bone.SetParent(parent, false);
                    return bone;
                }
                Transform hips = Add("Hips", root.transform);
                Transform spine = Add("Spine", hips);
                Transform chest = Add("Spine1", spine);
                Transform neck = Add("Neck", chest);
                Transform head = Add("Head", neck);
                Transform left = Add("LeftShoulder", chest);
                Transform right = Add("RightShoulder", chest);
                var template = new Dictionary<string, string>
                {
                    ["Hips"] = hips.name, ["Spine"] = spine.name, ["Chest"] = chest.name,
                    ["Neck"] = neck.name, ["Head"] = head.name,
                    ["LeftShoulder"] = left.name, ["RightShoulder"] = right.name
                };
                var limit = new HumanLimit { useDefaultValues = false, min = Vector3.one * -12f,
                    max = Vector3.one * 14f, center = Vector3.one, axisLength = 0.2f };
                HumanBone[] source = template.Where(entry => entry.Key != "Chest")
                    .Select(entry => new HumanBone { humanName = entry.Key, boneName = entry.Value, limit = limit }).ToArray();
                if (scenario == "existingChest") source = source.Concat(new[] { new HumanBone { humanName = "Chest", boneName = chest.name } }).ToArray();
                if (scenario == "extraMissing") source = source.Where(bone => bone.humanName != "Head").ToArray();
                if (scenario == "wrongMapping") source[0].boneName = spine.name;
                if (scenario == "wrongHierarchy") neck.SetParent(spine, false);
                if (scenario == "duplicateBone") Add("Spine1", spine);
                if (scenario == "aliasChest") chest.name = "Rig:" + chest.name;
                if (scenario == "duplicateRole") source[0] = source[1];
                if (scenario == "normalizedTemplateDuplicate") template.Add("Left Shoulder", left.name);
                if (scenario == "emptyTemplateValue") template["Chest"] = "";
                HumanBone[] original = (HumanBone[])source.Clone();
                Type type = typeof(FBXImportController).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator");
                MethodInfo method = type.GetMethod("TryCompleteTemplateChestMapping", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                object[] args = { root, source, template, null };
                Assert.That((bool)method.Invoke(null, args), Is.EqualTo(expected));
                var completed = (HumanBone[])args[3];
                Assert.That(source, Is.EqualTo(original));
                if (expected)
                {
                    Assert.That(completed.Take(source.Length), Is.EqualTo(original));
                    Assert.That(completed.Length, Is.EqualTo(source.Length + 1));
                    Assert.That(completed.Last().humanName, Is.EqualTo("Chest"));
                    Assert.That(completed.Last().boneName, Is.EqualTo(chest.name));
                    Assert.That(completed.Last().limit.useDefaultValues, Is.True);
                }
                else Assert.That(completed, Is.SameAs(source));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string CaptureClipSettings(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Single(candidate => !candidate.name.StartsWith("__preview__"));
            Assert.That(clip.humanMotion, Is.True);
            return JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Given_CompatibleMapping_When_ResolvingNames_Then_PreservesLimitsAndExactEntries(bool useAlias)
        {
            var root = new GameObject("매핑 검증");
            try
            {
                AddBone(root, "mixamorig:Hips");
                AddBone(root, "수동 척추");
                var limit = new HumanLimit { useDefaultValues = false, min = new Vector3(-10f, -20f, -30f),
                    max = new Vector3(10f, 20f, 30f), center = Vector3.one, axisLength = 0.25f };
                var source = new[] {
                    new HumanBone { humanName = "Hips", boneName = useAlias ? "Skeleton_Hips" : "mixamorig:Hips", limit = limit },
                    new HumanBone { humanName = "Spine", boneName = "수동 척추", limit = limit }
                };
                HumanBone[] original = (HumanBone[])source.Clone();
                Assert.That(Resolve(root, source, out HumanBone[] result), Is.True);
                Assert.That(result[0].boneName, Is.EqualTo("mixamorig:Hips"));
                Assert.That(result[1], Is.EqualTo(original[1]));
                Assert.That(result[0].limit, Is.EqualTo(limit));
                Assert.That(source, Is.EqualTo(original));
                Assert.That(ReferenceEquals(source, result), Is.EqualTo(!useAlias));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("missing")]
        [TestCase("duplicateHuman")]
        [TestCase("duplicateTransform")]
        [TestCase("reusedTransform")]
        [TestCase("ambiguousAlias")]
        [TestCase("ambiguousNormalized")]
        public void Given_UnresolvableMapping_When_ResolvingNames_Then_RejectsWithoutPartialChanges(string failure)
        {
            var root = new GameObject("매핑 실패 검증");
            try
            {
                AddBone(root, "mixamorig:Hips");
                AddBone(root, "수동 척추");
                var source = new[] {
                    new HumanBone { humanName = "Hips", boneName = "Skeleton_Hips" },
                    new HumanBone { humanName = "Spine", boneName = "수동 척추" }
                };
                if (failure == "missing") source[1].boneName = "없는 척추";
                if (failure == "duplicateHuman") source[1].humanName = "Hips";
                if (failure == "duplicateTransform") AddBone(root, "mixamorig:Hips");
                if (failure == "reusedTransform") source[1].boneName = "mixamorig:Hips";
                if (failure == "ambiguousAlias") AddBone(root, "otherRig:Hips");
                if (failure == "ambiguousNormalized")
                {
                    AddBone(root, "mixamorig_Hips");
                    source[0].boneName = "MIXAMORIG HIPS";
                }
                HumanBone[] original = (HumanBone[])source.Clone();
                Assert.That(Resolve(root, source, out HumanBone[] result), Is.False);
                Assert.That(result, Is.SameAs(source));
                Assert.That(source, Is.EqualTo(original));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Given_ExactNameWithSharedAlias_When_ResolvingNames_Then_PreservesExactMapping()
        {
            var root = new GameObject("정확 매핑 우선 검증");
            try
            {
                AddBone(root, "rigA:Hips");
                AddBone(root, "rigB:Hips");
                var source = new[] { new HumanBone { humanName = "Hips", boneName = "rigB:Hips" } };
                Assert.That(Resolve(root, source, out HumanBone[] result), Is.True);
                Assert.That(result, Is.SameAs(source));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Given_LocalSnakeMapping_When_ResolvingNames_Then_MatchesActualBonesWithoutChangingImporter()
        {
            const string path = "Assets/Resources/Import_FBX/Snake Hip Hop Dance.fbx";
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) Assert.Ignore("로컬 입력 FBX가 없는 환경에서는 실제 매핑 검증을 생략함");
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            HumanDescription original = importer.humanDescription;
            HumanBone[] source = original.human;
            var dependencyHash = AssetDatabase.GetAssetDependencyHash(path);
            Assert.That(Resolve(root, source, out HumanBone[] result), Is.True);
            Assert.That(result.Length, Is.EqualTo(source.Length));
            var names = root.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            for (int i = 0; i < source.Length; i++)
            {
                Assert.That(names, Does.Contain(result[i].boneName));
                Assert.That(result[i].humanName, Is.EqualTo(source[i].humanName));
                Assert.That(result[i].limit, Is.EqualTo(source[i].limit));
            }
            Assert.That(importer.humanDescription.human, Is.EqualTo(source));
            Assert.That(importer.humanDescription.skeleton, Is.EqualTo(original.skeleton));
            Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.EqualTo(dependencyHash));
        }

        [Test]
        public void Given_SkeletonConventionRig_When_BuildingFallback_Then_MapsRequiredBonesToActualNames()
        {
            var root = new GameObject("폴백 매핑 검증");
            try
            {
                foreach (string name in new[]
                    {
                        "Skeleton_Hips", "Skeleton_Spine", "Skeleton_Spine1", "Skeleton_Neck", "Skeleton_Head",
                        "Skeleton_LeftShoulder", "Skeleton_RightShoulder",
                        "Skeleton_LeftArm", "Skeleton_RightArm", "Skeleton_LeftForeArm", "Skeleton_RightForeArm",
                        "Skeleton_LeftHand", "Skeleton_RightHand",
                        "Skeleton_LeftUpLeg", "Skeleton_RightUpLeg", "Skeleton_LeftLeg", "Skeleton_RightLeg",
                        "Skeleton_LeftFoot", "Skeleton_RightFoot", "Skeleton_LeftToeBase", "Skeleton_RightToeBase",
                        "메쉬 노드"
                    })
                {
                    AddBone(root, name);
                }
                var current = new HumanDescription { human = new HumanBone[0], skeleton = new SkeletonBone[0] };
                Assert.That(BuildFallback(root, current, out HumanDescription result), Is.True);
                HumanBone[] mapped = result.human;
                Assert.That(mapped.Any(b => b.humanName == "Hips" && b.boneName == "Skeleton_Hips"), Is.True);
                Assert.That(mapped.Any(b => b.humanName == "Chest" && b.boneName == "Skeleton_Spine1"), Is.True);
                Assert.That(mapped.Any(b => b.humanName == "LeftUpperLeg" && b.boneName == "Skeleton_LeftUpLeg"), Is.True);
                Assert.That(mapped.Any(b => b.humanName == "LeftLowerLeg" && b.boneName == "Skeleton_LeftLeg"), Is.True);
                Assert.That(mapped.Any(b => b.humanName == "RightLowerArm" && b.boneName == "Skeleton_RightForeArm"), Is.True);
                Assert.That(mapped.Any(b => b.humanName == "LeftToes" && b.boneName == "Skeleton_LeftToeBase"), Is.True);
                Assert.That(result.skeleton.Length, Is.EqualTo(root.GetComponentsInChildren<Transform>(true).Length));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Given_RigMissingRequiredBones_When_BuildingFallback_Then_RejectsWithoutMapping()
        {
            var root = new GameObject("폴백 실패 검증");
            try
            {
                AddBone(root, "Skeleton_Spine");
                AddBone(root, "Skeleton_LeftHand");
                var current = new HumanDescription { human = new HumanBone[0], skeleton = new SkeletonBone[0] };
                Assert.That(BuildFallback(root, current, out HumanDescription result), Is.False);
                Assert.That(result.human, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static bool BuildFallback(GameObject root, HumanDescription current, out HumanDescription result)
        {
            Type type = typeof(FBXImportController).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator");
            MethodInfo method = type.GetMethod("TryBuildFallbackHumanDescription", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] args = { root, current, null };
            bool succeeded = (bool)method.Invoke(null, args);
            result = (HumanDescription)args[2];
            return succeeded;
        }

        private static bool EnsureHumanoid(string path)
        {
            Type type = typeof(FBXImportController).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator");
            MethodInfo method = type.GetMethod("EnsureHumanoid", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] { path });
        }

        private static bool Resolve(GameObject root, HumanBone[] source, out HumanBone[] result)
        {
            Type type = typeof(FBXImportController).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator");
            MethodInfo method = type.GetMethod("TryResolveHumanBoneNames", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] args = { root, source, null };
            bool succeeded = (bool)method.Invoke(null, args);
            result = (HumanBone[])args[2];
            return succeeded;
        }

        private static void AddBone(GameObject root, string name)
        {
            new GameObject(name).transform.SetParent(root.transform, false);
        }
    }
}
