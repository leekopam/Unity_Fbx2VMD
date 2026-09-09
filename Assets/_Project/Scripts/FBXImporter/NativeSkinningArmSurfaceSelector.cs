using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal enum NativeSkinningArmSide
    {
        Left,
        Right
    }

    internal sealed class NativeSkinningArmSurfaceSelection
    {
        internal NativeSkinningArmSurfaceSelection(
            SkinnedMeshRenderer renderer,
            NativeSkinningArmSide side,
            int connectedVertexCount,
            int mixedWeightVertexCount,
            int[] evaluatedVertexIndices,
            float armChainLength)
        {
            Renderer = renderer;
            Side = side;
            ConnectedVertexCount = connectedVertexCount;
            MixedWeightVertexCount = mixedWeightVertexCount;
            EvaluatedVertexIndices = evaluatedVertexIndices;
            ArmChainLength = armChainLength;
        }

        internal SkinnedMeshRenderer Renderer { get; }

        internal NativeSkinningArmSide Side { get; }

        internal int ConnectedVertexCount { get; }

        internal int MixedWeightVertexCount { get; }

        internal int[] EvaluatedVertexIndices { get; }

        internal float ArmChainLength { get; }
    }

    /// <summary>
    /// Humanoid 본과 스킨 가중치로 팔꿈치 주변 연결 표면을 찾음.
    /// </summary>
    internal static class NativeSkinningArmSurfaceSelector
    {
        private const float MinimumMixedLowerArmWeight = 0.05f;
        private const float MaximumMixedLowerArmWeight = 0.95f;
        private const int MinimumMixedVertexCount = 4;
        private const float ElbowWindowToArmLengthRatio = 0.226075f;

        internal static NativeSkinningArmSurfaceSelection[] FindAll(
            Animator animator)
        {
            if (animator == null)
            {
                throw new ArgumentNullException(nameof(animator));
            }
            if (!animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                return Array.Empty<NativeSkinningArmSurfaceSelection>();
            }

            var selections = new List<NativeSkinningArmSurfaceSelection>();
            SkinnedMeshRenderer[] renderers = animator
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                AddSideSelections(
                    animator,
                    renderer,
                    NativeSkinningArmSide.Left,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand,
                    selections);
                AddSideSelections(
                    animator,
                    renderer,
                    NativeSkinningArmSide.Right,
                    HumanBodyBones.RightUpperArm,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand,
                    selections);
            }

            return selections.ToArray();
        }

        private static void AddSideSelections(
            Animator animator,
            SkinnedMeshRenderer renderer,
            NativeSkinningArmSide side,
            HumanBodyBones upperArmBone,
            HumanBodyBones lowerArmBone,
            HumanBodyBones handBone,
            ICollection<NativeSkinningArmSurfaceSelection> selections)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null || mesh.vertexCount < 4)
            {
                return;
            }
#if !UNITY_EDITOR
            if (!mesh.isReadable)
            {
                return;
            }
#endif

            Transform upperArm = animator.GetBoneTransform(upperArmBone);
            Transform lowerArm = animator.GetBoneTransform(lowerArmBone);
            Transform hand = animator.GetBoneTransform(handBone);
            if (upperArm == null || lowerArm == null || hand == null)
            {
                return;
            }

            Transform[] bones = renderer.bones;
            int lowerArmIndex = Array.IndexOf(bones, lowerArm);
            if (lowerArmIndex < 0)
            {
                return;
            }

            Vector3 upperArmPosition = renderer.transform.InverseTransformPoint(
                upperArm.position);
            Vector3 lowerArmPosition = renderer.transform.InverseTransformPoint(
                lowerArm.position);
            Vector3 handPosition = renderer.transform.InverseTransformPoint(
                hand.position);
            float armChainLength = Vector3.Distance(
                    upperArmPosition,
                    lowerArmPosition) +
                Vector3.Distance(lowerArmPosition, handPosition);
            Vector3 incomingAxis = lowerArmPosition - upperArmPosition;
            if (armChainLength <= Mathf.Epsilon || incomingAxis.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }
            incomingAxis.Normalize();

            int[] triangles = mesh.triangles;
            BoneWeight[] boneWeights = mesh.boneWeights;
            Vector3[] vertices = mesh.vertices;
            if (triangles.Length < 3 || boneWeights.Length != vertices.Length)
            {
                return;
            }

            var components = new VertexDisjointSet(vertices.Length);
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                components.Union(triangles[index], triangles[index + 1]);
                components.Union(triangles[index + 1], triangles[index + 2]);
            }

            float halfWindow = armChainLength * ElbowWindowToArmLengthRatio;
            var mixedVertexCountByRoot = new Dictionary<int, int>();
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                float lowerArmWeight = CalculateBoneWeight(
                    boneWeights[vertexIndex],
                    lowerArmIndex);
                if (lowerArmWeight < MinimumMixedLowerArmWeight ||
                    lowerArmWeight > MaximumMixedLowerArmWeight ||
                    Mathf.Abs(Vector3.Dot(
                        vertices[vertexIndex] - lowerArmPosition,
                        incomingAxis)) > halfWindow)
                {
                    continue;
                }

                int root = components.Find(vertexIndex);
                mixedVertexCountByRoot[root] =
                    mixedVertexCountByRoot.TryGetValue(root, out int count)
                        ? count + 1
                        : 1;
            }

            foreach (KeyValuePair<int, int> candidate in mixedVertexCountByRoot
                         .Where(entry => entry.Value >= MinimumMixedVertexCount)
                         .OrderBy(entry => entry.Key))
            {
                int[] componentVertices = Enumerable.Range(0, vertices.Length)
                    .Where(vertexIndex => components.Find(vertexIndex) == candidate.Key)
                    .ToArray();
                int[] evaluatedVertices = componentVertices
                    .Where(vertexIndex => Mathf.Abs(Vector3.Dot(
                        vertices[vertexIndex] - lowerArmPosition,
                        incomingAxis)) <= halfWindow)
                    .ToArray();
                if (evaluatedVertices.Length < MinimumMixedVertexCount)
                {
                    continue;
                }

                selections.Add(new NativeSkinningArmSurfaceSelection(
                    renderer,
                    side,
                    componentVertices.Length,
                    candidate.Value,
                    evaluatedVertices,
                    armChainLength));
            }
        }

        private static float CalculateBoneWeight(BoneWeight weight, int boneIndex)
        {
            float total = 0f;
            if (weight.boneIndex0 == boneIndex)
            {
                total += weight.weight0;
            }
            if (weight.boneIndex1 == boneIndex)
            {
                total += weight.weight1;
            }
            if (weight.boneIndex2 == boneIndex)
            {
                total += weight.weight2;
            }
            if (weight.boneIndex3 == boneIndex)
            {
                total += weight.weight3;
            }
            return total;
        }

        private sealed class VertexDisjointSet
        {
            private readonly int[] _parents;
            private readonly byte[] _ranks;

            internal VertexDisjointSet(int count)
            {
                _parents = new int[count];
                _ranks = new byte[count];
                for (int index = 0; index < count; index++)
                {
                    _parents[index] = index;
                }
            }

            internal int Find(int value)
            {
                int root = value;
                while (_parents[root] != root)
                {
                    root = _parents[root];
                }
                while (_parents[value] != value)
                {
                    int parent = _parents[value];
                    _parents[value] = root;
                    value = parent;
                }
                return root;
            }

            internal void Union(int first, int second)
            {
                int firstRoot = Find(first);
                int secondRoot = Find(second);
                if (firstRoot == secondRoot)
                {
                    return;
                }

                if (_ranks[firstRoot] < _ranks[secondRoot])
                {
                    _parents[firstRoot] = secondRoot;
                    return;
                }
                if (_ranks[firstRoot] > _ranks[secondRoot])
                {
                    _parents[secondRoot] = firstRoot;
                    return;
                }

                _parents[secondRoot] = firstRoot;
                _ranks[firstRoot]++;
            }
        }
    }
}
