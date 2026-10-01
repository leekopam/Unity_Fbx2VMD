using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 스커트 본 체인 루트를 루프로 연결하는 BoneCloth를 자동 생성한다.
    /// MC2의 connectionMode=SequentialLoopMesh는 rootBones 등록 순서대로 면을 잇고
    /// 처음과 끝을 폐합한다 — MMD 스커트 본(허리 둘레 방사형 체인)에 적합하며
    /// MeshCloth 대비 프록시가 본 수십 개 수준으로 작아 부하가 낮다.
    /// 또한 정점=본이라 애니메이션 포즈 전제(백스톱)가 구조적으로 성립한다.
    /// </summary>
    public static class SkirtBoneClothBuilder
    {
        /// <summary>
        /// 스커트 체인 루트(depth==0)를 캐릭터 로컬 XZ 평면의 각도 순으로 정렬해 반환한다.
        /// SequentialLoopMesh는 인접 루트끼리 면을 잇기 때문에 링 순서가 필수다.
        /// </summary>
        public static List<Transform> CollectChainRootsOrdered(
            Transform characterRoot,
            Dictionary<Transform, int> skirtBoneDepths)
        {
            var roots = skirtBoneDepths
                .Where(kv => kv.Value == 0)
                .Select(kv => kv.Key)
                .ToList();
            if (roots.Count < 2)
                return roots;

            var inv = characterRoot.worldToLocalMatrix;
            var center = roots
                .Select(r => inv.MultiplyPoint3x4(r.position))
                .Aggregate(Vector3.zero, (a, b) => a + b) / roots.Count;
            return roots
                .OrderBy(r =>
                {
                    var p = inv.MultiplyPoint3x4(r.position) - center;
                    return Mathf.Atan2(p.z, p.x);
                })
                .ToList();
        }

        /// <summary>
        /// 루프 면 BoneCloth가 성립 가능한지 판정한다.
        /// 루트가 3개 미만이면 링 면이 불가능하다.
        /// </summary>
        public static bool CanBuildLoop(List<Transform> orderedRoots)
        {
            return orderedRoots != null && orderedRoots.Count >= 3;
        }

        /// <summary>
        /// SequentialLoopMesh BoneCloth를 생성한다.
        /// 루트 본이 고정 속성, 하위 체인 본이 이동 속성으로 자동 배정된다(MC2 규약).
        /// </summary>
        public static MagicaCloth Create(
            Transform parent,
            List<Transform> orderedRoots,
            List<ColliderComponent> colliders,
            bool useBackstop,
            float backstopDistance,
            bool usePresetBaseline = true,
            SkirtTuning? tuning = null)
        {
            if (!CanBuildLoop(orderedRoots))
                return null;

            var go = new GameObject(CharacterPhysicsSetup.ClothNamePrefix + "SkirtLoop");
            go.transform.SetParent(parent, false);
            var cloth = go.AddComponent<MagicaCloth>();
            var sdata = cloth.SerializeData;
            sdata.clothType = ClothProcess.ClothType.BoneCloth;
            sdata.connectionMode = RenderSetupData.BoneConnectionMode.SequentialLoopMesh;
            sdata.rootBones.AddRange(orderedRoots);

            if (usePresetBaseline)
                ClothPresetLibrary.TryImport(sdata, ClothPresetLibrary.Skirt,
                    tuning?.characterPresetKey, go.name);
            SkirtClothBuilder.ApplySimParams(sdata, colliders, useBackstop, backstopDistance,
                tuning ?? SkirtTuning.Default);
            return cloth;
        }
    }
}
