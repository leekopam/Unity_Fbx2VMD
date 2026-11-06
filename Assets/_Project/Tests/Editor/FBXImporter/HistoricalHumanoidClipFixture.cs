using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    internal sealed class HistoricalHumanoidClipFixture : IDisposable
    {
        private const string SourceClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        internal string ClipAssetPath { get; private set; }
        private string _fixtureFolder;
        private bool _ownsFixtureFolder;
        private byte[] _sourceClipBytes;
        private byte[] _sourceMetaBytes;
        private AnimationClip _detachedClip;

        internal HistoricalHumanoidClipFixture()
        {
            Assert.That(File.Exists(SourceClipAssetPath), Is.True);
            _sourceClipBytes = File.ReadAllBytes(SourceClipAssetPath);
            _sourceMetaBytes = File.ReadAllBytes(SourceClipAssetPath + ".meta");
            _fixtureFolder = "Assets/HistoricalHumanoidRegression_" + Guid.NewGuid().ToString("N");
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_fixtureFolder)),
                    Is.Not.Empty);
                _ownsFixtureFolder = true;
                ClipAssetPath = _fixtureFolder + "/satisfaction_2.fbx";
                Assert.That(AssetDatabase.CopyAsset(SourceClipAssetPath, ClipAssetPath), Is.True);
                var importer = (ModelImporter)AssetImporter.GetAtPath(ClipAssetPath);
                Assert.That(importer, Is.Not.Null);
                // 기존 반례는 Chest 누락 입력에서 수집됐으므로 복제본에 그 조건을 고정함.
                HumanDescription description = importer.humanDescription;
                HumanDescription before = description;
                string clipSettings = JsonUtility.ToJson(
                    AnimationUtility.GetAnimationClipSettings(LoadHumanoidClip()));
                description.human = description.human
                    .Where(bone => bone.humanName != "Chest").ToArray();
                importer.humanDescription = description;
                importer.SaveAndReimport();
                HumanDescription after = ((ModelImporter)AssetImporter.GetAtPath(ClipAssetPath))
                    .humanDescription;
                Assert.That(after.human, Is.EqualTo(
                    before.human.Where(bone => bone.humanName != "Chest").ToArray()));
                Assert.That(after.skeleton, Is.EqualTo(before.skeleton));
                Assert.That(after.armStretch, Is.EqualTo(before.armStretch));
                Assert.That(after.legStretch, Is.EqualTo(before.legStretch));
                Assert.That(after.upperArmTwist, Is.EqualTo(before.upperArmTwist));
                Assert.That(after.lowerArmTwist, Is.EqualTo(before.lowerArmTwist));
                Assert.That(after.upperLegTwist, Is.EqualTo(before.upperLegTwist));
                Assert.That(after.lowerLegTwist, Is.EqualTo(before.lowerLegTwist));
                Assert.That(after.hasTranslationDoF, Is.EqualTo(before.hasTranslationDoF));
                Assert.That(JsonUtility.ToJson(
                    AnimationUtility.GetAnimationClipSettings(LoadHumanoidClip())),
                    Is.EqualTo(clipSettings));
                Animator animator = AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath)
                    .GetComponentInChildren<Animator>(true);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
                Assert.That(animator.GetBoneTransform(HumanBodyBones.Chest), Is.Null);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_sourceClipBytes != null)
                {
                    CollectionAssert.AreEqual(_sourceClipBytes, File.ReadAllBytes(SourceClipAssetPath));
                }
                if (_sourceMetaBytes != null)
                {
                    CollectionAssert.AreEqual(_sourceMetaBytes, File.ReadAllBytes(SourceClipAssetPath + ".meta"));
                }
            }
            finally
            {
                if (_detachedClip != null)
                {
                    UnityEngine.Object.DestroyImmediate(_detachedClip);
                }
                DeleteHistoricalClipFixture();
            }
        }

        private void DeleteHistoricalClipFixture()
        {
            if (_ownsFixtureFolder && AssetDatabase.IsValidFolder(_fixtureFolder))
            {
                Assert.That(AssetDatabase.DeleteAsset(_fixtureFolder), Is.True,
                    "과거 반례용 임시 입력 정리 실패");
                _ownsFixtureFolder = false;
            }
            ClipAssetPath = null;
        }

        internal AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null);
            return clip;
        }

        internal AnimationClip LoadDetachedHumanoidClip()
        {
            // 과거 전환 검사의 정규화 이동 단위까지 복원하려면 원본 에셋 연결을 분리해야 함.
            _detachedClip ??= UnityEngine.Object.Instantiate(LoadHumanoidClip());
            Assert.That(AssetDatabase.GetAssetPath(_detachedClip), Is.Empty);
            return _detachedClip;
        }
    }
}
