using System.Collections.Generic;
using UnityEngine;
using VRM;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 모델 내장 스프링본(UniVRM 0.x VRMSpringBone)이 시뮬레이션하는 본을 수집한다.
    /// 제작자 튜닝이 정본이므로 C-1 정책 — 네이티브 물리가 있는 체인에는
    /// MC2 클로스를 만들지 않아 이중 시뮬레이션을 막는다.
    /// </summary>
    public static class NativeSpringBoneProbe
    {
        /// <summary>
        /// 루트 하위의 모든 VRMSpringBone 컴포넌트를 스캔해
        /// 각 RootBones 서브트리 전체를 소유 본 집합으로 반환한다.
        /// </summary>
        public static HashSet<Transform> CollectOwnedBones(Transform root)
        {
            var owned = new HashSet<Transform>();
            if (root == null)
                return owned;

            foreach (var spring in root.GetComponentsInChildren<VRMSpringBone>(true))
            {
                if (spring == null || spring.RootBones == null)
                    continue;
                foreach (var rb in spring.RootBones)
                    if (rb != null)
                        AddSubtree(rb, owned);
            }
            return owned;
        }

        static void AddSubtree(Transform root, HashSet<Transform> set)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                set.Add(t);
        }
    }
}
