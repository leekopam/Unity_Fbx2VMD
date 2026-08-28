using Assimp;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class AssimpRuntimeAnimationConversionTests
    {
        [Test]
        public void Given_RuntimeAnimationConversion_When_CheckingSource_Then_HasNoDeadDurationOrDuplicateClipAssignment()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpFBXImporter.cs"));
            const string clipAssignment = "animComp.clip = clips[0];";
            int assignmentCount = source.Split(
                new[] { clipAssignment },
                StringSplitOptions.None).Length - 1;

            Assert.That(
                source,
                Does.Not.Contain("double duration = anim.DurationInTicks / anim.TicksPerSecond;"));
            Assert.That(assignmentCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void Given_SyntheticAnimation_When_Processing_Then_RegistersLegacyClipAndPreservesCurveTiming()
        {
            var rootObject = new GameObject("ImportedRoot");
            var hipsObject = new GameObject("Hips");
            AnimationClip[] clips = null;

            try
            {
                hipsObject.transform.SetParent(rootObject.transform, false);
                var importer = new AssimpFBXImporter();
                SetNodeMap(importer, hipsObject.transform);

                Scene scene = CreateScene();
                InvokeProcessAnimations(importer, scene, rootObject);

                clips = importer.GetAnimationClips();
                Assert.That(clips, Has.Length.EqualTo(1));

                AnimationClip clip = clips[0];
                Assert.That(clip.name, Is.EqualTo("Motion"));
                Assert.That(clip.legacy, Is.True);
                Assert.That(clip.wrapMode, Is.EqualTo(WrapMode.Loop));
                Assert.That(clip.frameRate, Is.EqualTo(60f));

                UnityEngine.Animation animationComponent = rootObject.GetComponent<UnityEngine.Animation>();
                Assert.That(animationComponent, Is.Not.Null);
                Assert.That(animationComponent.clip, Is.SameAs(clip));

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.That(bindings, Has.Length.EqualTo(3));
                Assert.That(bindings.All(binding => binding.path == "Hips"), Is.True);

                EditorCurveBinding xBinding = bindings.Single(binding => binding.propertyName.EndsWith(".x"));
                AnimationCurve xCurve = AnimationUtility.GetEditorCurve(clip, xBinding);
                Assert.That(xCurve.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                if (clips != null)
                {
                    foreach (AnimationClip createdClip in clips)
                    {
                        if (createdClip != null)
                        {
                            UnityEngine.Object.DestroyImmediate(createdClip);
                        }
                    }
                }

                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static Scene CreateScene()
        {
            var scene = new Scene();
            var animation = new Assimp.Animation
            {
                Name = "Motion",
                DurationInTicks = 60d,
                TicksPerSecond = 60d
            };
            var channel = new NodeAnimationChannel { NodeName = "Hips" };
            channel.PositionKeys.Add(new VectorKey(0d, new Vector3D(0f, 0f, 0f)));
            channel.PositionKeys.Add(new VectorKey(60d, new Vector3D(100f, 0f, 0f)));
            animation.NodeAnimationChannels.Add(channel);
            scene.Animations.Add(animation);
            return scene;
        }

        private static void SetNodeMap(AssimpFBXImporter importer, Transform hips)
        {
            FieldInfo nodeMapField = typeof(AssimpFBXImporter).GetField(
                "_nodeMap",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(nodeMapField, Is.Not.Null);
            nodeMapField.SetValue(importer, new Dictionary<string, Transform>
            {
                [hips.name] = hips
            });
        }

        private static void InvokeProcessAnimations(
            AssimpFBXImporter importer,
            Scene scene,
            GameObject rootObject)
        {
            MethodInfo processMethod = typeof(AssimpFBXImporter).GetMethod(
                "ProcessAnimations",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(processMethod, Is.Not.Null);
            processMethod.Invoke(importer, new object[] { scene, rootObject });
        }
    }
}
