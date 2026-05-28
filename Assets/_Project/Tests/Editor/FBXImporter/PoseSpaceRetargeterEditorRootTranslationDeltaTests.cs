using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterEditorRootTranslationDeltaTests
    {
        private static readonly Type[] EditorRootTranslationDeltaParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(Vector3),
            typeof(Vector3).MakeByRefType(),
            typeof(bool).MakeByRefType(),
            typeof(bool).MakeByRefType(),
            typeof(bool).MakeByRefType()
        };

        [Test]
        public void Given_FirstEditorDelta_When_CalculatingReferenceDelta_Then_AppliesWeightAndStartsSmoothing()
        {
            Vector3 delta = CalculateEditorRootTranslationReferenceDelta(
                rawEditorDelta: new Vector3(2f, 3f, 4f),
                ghostDelta: Vector3.zero,
                editorRootTranslationWeight: 0.5f,
                editorRootTranslationCurrentWeight: 0.25f,
                hasSmoothedEditorRootTranslationDelta: false,
                previousSmoothedEditorRootTranslationDelta: Vector3.zero,
                out Vector3 nextSmoothedDelta,
                out bool nextHasSmoothedDelta,
                out bool skippedByGhostDelta,
                out bool skippedByNonFinite);

            Assert.That(delta.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(nextSmoothedDelta, Is.EqualTo(delta));
            Assert.That(nextHasSmoothedDelta, Is.True);
            Assert.That(skippedByGhostDelta, Is.False);
            Assert.That(skippedByNonFinite, Is.False);
        }

        [Test]
        public void Given_PreviousSmoothedDelta_When_CalculatingReferenceDelta_Then_BlendsTowardWeightedDelta()
        {
            Vector3 delta = CalculateEditorRootTranslationReferenceDelta(
                rawEditorDelta: new Vector3(3f, 0f, 1f),
                ghostDelta: Vector3.zero,
                editorRootTranslationWeight: 1f,
                editorRootTranslationCurrentWeight: 0.25f,
                hasSmoothedEditorRootTranslationDelta: true,
                previousSmoothedEditorRootTranslationDelta: new Vector3(1f, 0f, 1f),
                out Vector3 nextSmoothedDelta,
                out bool nextHasSmoothedDelta,
                out bool skippedByGhostDelta,
                out bool skippedByNonFinite);

            Assert.That(delta.x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(nextSmoothedDelta, Is.EqualTo(delta));
            Assert.That(nextHasSmoothedDelta, Is.True);
            Assert.That(skippedByGhostDelta, Is.False);
            Assert.That(skippedByNonFinite, Is.False);
        }

        [Test]
        public void Given_GhostAlreadyMovedInXZ_When_CalculatingReferenceDelta_Then_SkipsAndKeepsSmoothingState()
        {
            Vector3 previousSmoothedDelta = new Vector3(0.1f, 0f, 0.2f);

            Vector3 delta = CalculateEditorRootTranslationReferenceDelta(
                rawEditorDelta: new Vector3(2f, 0f, 4f),
                ghostDelta: new Vector3(0.001f, 0f, 0f),
                editorRootTranslationWeight: 0.5f,
                editorRootTranslationCurrentWeight: 0.25f,
                hasSmoothedEditorRootTranslationDelta: true,
                previousSmoothedEditorRootTranslationDelta: previousSmoothedDelta,
                out Vector3 nextSmoothedDelta,
                out bool nextHasSmoothedDelta,
                out bool skippedByGhostDelta,
                out bool skippedByNonFinite);

            Assert.That(delta, Is.EqualTo(Vector3.zero));
            Assert.That(nextSmoothedDelta, Is.EqualTo(previousSmoothedDelta));
            Assert.That(nextHasSmoothedDelta, Is.True);
            Assert.That(skippedByGhostDelta, Is.True);
            Assert.That(skippedByNonFinite, Is.False);
        }

        [Test]
        public void Given_NonFiniteEditorDelta_When_CalculatingReferenceDelta_Then_SkipsAndKeepsSmoothingState()
        {
            Vector3 previousSmoothedDelta = new Vector3(0.1f, 0f, 0.2f);

            Vector3 delta = CalculateEditorRootTranslationReferenceDelta(
                rawEditorDelta: new Vector3(float.NaN, 0f, 0f),
                ghostDelta: Vector3.zero,
                editorRootTranslationWeight: 0.5f,
                editorRootTranslationCurrentWeight: 0.25f,
                hasSmoothedEditorRootTranslationDelta: true,
                previousSmoothedEditorRootTranslationDelta: previousSmoothedDelta,
                out Vector3 nextSmoothedDelta,
                out bool nextHasSmoothedDelta,
                out bool skippedByGhostDelta,
                out bool skippedByNonFinite);

            Assert.That(delta, Is.EqualTo(Vector3.zero));
            Assert.That(nextSmoothedDelta, Is.EqualTo(previousSmoothedDelta));
            Assert.That(nextHasSmoothedDelta, Is.True);
            Assert.That(skippedByGhostDelta, Is.False);
            Assert.That(skippedByNonFinite, Is.True);
        }

        private static Vector3 CalculateEditorRootTranslationReferenceDelta(
            Vector3 rawEditorDelta,
            Vector3 ghostDelta,
            float editorRootTranslationWeight,
            float editorRootTranslationCurrentWeight,
            bool hasSmoothedEditorRootTranslationDelta,
            Vector3 previousSmoothedEditorRootTranslationDelta,
            out Vector3 nextSmoothedEditorRootTranslationDelta,
            out bool nextHasSmoothedEditorRootTranslationDelta,
            out bool skippedByGhostDelta,
            out bool skippedByNonFinite)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "CalculateEditorRootTranslationReferenceDelta",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorRootTranslationDeltaParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for editor RootT translation delta smoothing.");

            object[] args =
            {
                rawEditorDelta,
                ghostDelta,
                editorRootTranslationWeight,
                editorRootTranslationCurrentWeight,
                hasSmoothedEditorRootTranslationDelta,
                previousSmoothedEditorRootTranslationDelta,
                previousSmoothedEditorRootTranslationDelta,
                hasSmoothedEditorRootTranslationDelta,
                false,
                false
            };

            Vector3 delta = (Vector3)method.Invoke(null, args);
            nextSmoothedEditorRootTranslationDelta = (Vector3)args[6];
            nextHasSmoothedEditorRootTranslationDelta = (bool)args[7];
            skippedByGhostDelta = (bool)args[8];
            skippedByNonFinite = (bool)args[9];
            return delta;
        }
    }
}
