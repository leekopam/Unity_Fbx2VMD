using UnityEngine;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    [DisallowMultipleComponent]
    public sealed class GhostSkeletonDebugRenderer : MonoBehaviour
    {
        private static readonly HumanBodyBones[,] BonePairs =
        {
            { HumanBodyBones.Hips, HumanBodyBones.Spine },
            { HumanBodyBones.Spine, HumanBodyBones.Chest },
            { HumanBodyBones.Chest, HumanBodyBones.Neck },
            { HumanBodyBones.Neck, HumanBodyBones.Head },
            { HumanBodyBones.Chest, HumanBodyBones.LeftUpperArm },
            { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm },
            { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
            { HumanBodyBones.Chest, HumanBodyBones.RightUpperArm },
            { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
            { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
            { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg },
            { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg },
            { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
            { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg },
            { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
            { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },
        };

        private const float LineWidth = 0.018f;
        private const float RootMarkerHalfSize = 0.16f;
        private const float MinScaleForUncompensatedDebugLines = 0.25f;
        private const float MaxDisplayScaleCompensation = 100f;
        private const float MinLossyScaleForDebugLines = 0.0001f;
        private static readonly Color LineColor = new Color(0.05f, 0.95f, 1f, 0.92f);
        private static readonly Color RootMarkerColor = new Color(1f, 0.85f, 0.05f, 0.95f);

        private readonly List<LineRenderer> boneLines = new List<LineRenderer>();
        private readonly List<LineRenderer> rootMarkerLines = new List<LineRenderer>();
        private Animator animator;
        private Material lineMaterial;
        private bool initialized;
        private bool visible;

        public void SetVisible(bool value)
        {
            visible = value;
            enabled = value;
            EnsureInitialized();
            SetLinesEnabled(value);
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void LateUpdate()
        {
            if (!visible)
            {
                return;
            }

            EnsureInitialized();
            UpdateRootMarker();
            UpdateBoneLines();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }

        private void EnsureInitialized()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            if (initialized)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            Shader fallbackShader = Shader.Find("Unlit/Color");
            lineMaterial = new Material(shader != null ? shader : fallbackShader);
            lineMaterial.color = LineColor;

            for (int i = 0; i < BonePairs.GetLength(0); i++)
            {
                boneLines.Add(CreateLine($"GhostBoneLine_{i:00}", LineColor));
            }

            for (int i = 0; i < 3; i++)
            {
                rootMarkerLines.Add(CreateLine($"GhostRootMarker_{i:00}", RootMarkerColor));
            }

            initialized = true;
        }

        private LineRenderer CreateLine(string lineName, Color color)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.material = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.enabled = visible;
            return line;
        }

        private void SetLinesEnabled(bool enabledValue)
        {
            foreach (LineRenderer line in boneLines)
            {
                if (line != null)
                {
                    line.enabled = enabledValue;
                }
            }

            foreach (LineRenderer line in rootMarkerLines)
            {
                if (line != null)
                {
                    line.enabled = enabledValue;
                }
            }
        }

        private void UpdateRootMarker()
        {
            Vector3 center = transform.position;
            SetLine(rootMarkerLines[0], center - Vector3.right * RootMarkerHalfSize, center + Vector3.right * RootMarkerHalfSize, true);
            SetLine(rootMarkerLines[1], center - Vector3.up * RootMarkerHalfSize, center + Vector3.up * RootMarkerHalfSize, true);
            SetLine(rootMarkerLines[2], center - Vector3.forward * RootMarkerHalfSize, center + Vector3.forward * RootMarkerHalfSize, true);
        }

        private void UpdateBoneLines()
        {
            for (int i = 0; i < BonePairs.GetLength(0); i++)
            {
                Transform from = GetBone(BonePairs[i, 0]);
                Transform to = GetBone(BonePairs[i, 1]);
                bool hasPair = from != null && to != null;
                SetLine(
                    boneLines[i],
                    hasPair ? GetDebugWorldPosition(from) : Vector3.zero,
                    hasPair ? GetDebugWorldPosition(to) : Vector3.zero,
                    hasPair);
            }
        }

        private Transform GetBone(HumanBodyBones bone)
        {
            if (animator == null || !animator.isHuman)
            {
                return null;
            }

            return animator.GetBoneTransform(bone);
        }

        private Vector3 GetDebugWorldPosition(Transform bone)
        {
            Vector3 center = transform.position;
            float displayScale = CalculateDisplayScaleCompensation(transform.lossyScale);
            return center + (bone.position - center) * displayScale;
        }

        private static float CalculateDisplayScaleCompensation(Vector3 lossyScale)
        {
            float maxScale = Mathf.Max(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z));

            if (maxScale <= MinLossyScaleForDebugLines || maxScale >= MinScaleForUncompensatedDebugLines)
            {
                return 1f;
            }

            return Mathf.Min(MaxDisplayScaleCompensation, 1f / maxScale);
        }

        private static void SetLine(LineRenderer line, Vector3 from, Vector3 to, bool enabledValue)
        {
            if (line == null)
            {
                return;
            }

            line.enabled = enabledValue;
            if (!enabledValue)
            {
                return;
            }

            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }
    }
}
