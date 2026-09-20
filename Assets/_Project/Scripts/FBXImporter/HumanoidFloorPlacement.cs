using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 기본 자세의 실제 밑창을 기준으로 캐릭터 전체를 지면에 배치함.
    /// </summary>
    internal static class HumanoidFloorPlacement
    {
        internal static bool TryPlace(Animator animator)
        {
            if (animator == null || !animator.isHuman || !animator.gameObject.activeInHierarchy)
                return false;

            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (!TryMeasureSoles(animator.transform, left, right, out Vector3 leftSole, out Vector3 rightSole))
                return false;

            Physics.SyncTransforms();
            float distance = Mathf.Max(animator.humanScale, 0.01f);
            if (!TryFindFloor(animator.transform, leftSole, distance, out float leftY) ||
                !TryFindFloor(animator.transform, rightSole, distance, out float rightY))
                return false;

            // 양발 중 하나를 관통시키지 않는 이동량으로 시작 위치를 한 번 정렬함.
            float offset = Mathf.Max(leftY - leftSole.y, rightY - rightSole.y);
            animator.transform.position += Vector3.up * offset;
            return true;
        }

        private static bool TryMeasureSoles(Transform root, Transform left, Transform right,
            out Vector3 leftSole, out Vector3 rightSole)
        {
            leftSole = rightSole = new Vector3(0f, float.PositiveInfinity, 0f);
            if (left == null || right == null)
                return false;

            var baked = new Mesh();
            var vertices = new List<Vector3>();
            try
            {
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Mesh mesh = renderer.sharedMesh;
                    if (!renderer.enabled || mesh == null)
                        continue;
#if !UNITY_EDITOR
                    // 빌드에서 읽을 수 없는 메시로 부정확한 배치값을 만들지 않음.
                    if (!mesh.isReadable)
                        return false;
#endif
                    BoneWeight[] weights = mesh.boneWeights;
                    Transform[] bones = renderer.bones;
                    if (weights.Length != mesh.vertexCount)
                        return false;

                    renderer.BakeMesh(baked);
                    baked.GetVertices(vertices);
                    if (vertices.Count != weights.Length)
                        return false;

                    for (int i = 0; i < weights.Length; i++)
                    {
                        Vector3 world = renderer.transform.TransformPoint(vertices[i]);
                        if (!IsFinite(world))
                            return false;
                        if (world.y < leftSole.y && GetFootWeight(weights[i], bones, left) >= 0.5f)
                            leftSole = world;
                        if (world.y < rightSole.y && GetFootWeight(weights[i], bones, right) >= 0.5f)
                            rightSole = world;
                    }
                }
                return IsFinite(leftSole) && IsFinite(rightSole);
            }
            catch (UnityException)
            {
                return false;
            }
            finally
            {
                if (Application.isPlaying)
                    Object.Destroy(baked);
                else
                    Object.DestroyImmediate(baked);
            }
        }

        private static float GetFootWeight(BoneWeight weight, Transform[] bones, Transform foot)
        {
            float Sum(int index, float value) => value > 0f && index >= 0 && index < bones.Length &&
                bones[index] != null && bones[index].IsChildOf(foot) ? value : 0f;
            return Sum(weight.boneIndex0, weight.weight0) + Sum(weight.boneIndex1, weight.weight1) +
                Sum(weight.boneIndex2, weight.weight2) + Sum(weight.boneIndex3, weight.weight3);
        }

        private static bool TryFindFloor(Transform root, Vector3 sole, float distance, out float height)
        {
            height = 0f;
            float nearest = float.PositiveInfinity;
            RaycastHit[] hits = Physics.RaycastAll(sole + Vector3.up * distance * 0.5f,
                Vector3.down, distance * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(root) || root.IsChildOf(hit.transform) ||
                    hit.normal.y < 0.5f || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                height = hit.point.y;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
