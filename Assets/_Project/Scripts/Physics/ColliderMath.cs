using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 콜라이더 피팅과 침투 측정에 사용하는 순수 기하 함수 모음.
    /// </summary>
    public static class ColliderMath
    {
        /// <summary>
        /// 점 p에서 선분 a-b까지의 거리.
        /// </summary>
        public static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-12f)
                return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>
        /// 구 콜라이더에 대한 점의 침투량. 양수=밖, 음수=침투(음수 크기=침투 깊이).
        /// </summary>
        public static float SignedDistanceSphere(Vector3 p, Vector3 center, float radius)
        {
            return Vector3.Distance(p, center) - radius;
        }

        /// <summary>
        /// 캡슐(선분 a-b, 균일 반경)에 대한 점의 침투량.
        /// </summary>
        public static float SignedDistanceCapsule(Vector3 p, Vector3 a, Vector3 b, float radius)
        {
            return DistancePointToSegment(p, a, b) - radius;
        }

        /// <summary>
        /// 값 목록의 백분위수를 반환한다. (0~1). 빈 목록이면 0.
        /// 이상치에 강한 콜라이더 반경 산출에 사용한다.
        /// </summary>
        public static float Percentile(IList<float> values, float percentile)
        {
            if (values == null || values.Count == 0)
                return 0f;
            var sorted = new List<float>(values);
            sorted.Sort();
            float idx = Mathf.Clamp01(percentile) * (sorted.Count - 1);
            int lo = Mathf.FloorToInt(idx);
            int hi = Mathf.CeilToInt(idx);
            return Mathf.Lerp(sorted[lo], sorted[hi], idx - lo);
        }

        /// <summary>
        /// 버텍스 집합으로부터 구 반경을 피팅한다.
        /// 중심(center)에서 가장 먼 버텍스의 지정 백분위 거리를 반경으로 사용.
        /// </summary>
        public static float FitSphereRadius(Vector3 center, IList<Vector3> verts, float percentile = 0.95f)
        {
            if (verts == null || verts.Count == 0)
                return 0f;
            var dists = new List<float>(verts.Count);
            foreach (var v in verts)
                dists.Add(Vector3.Distance(v, center));
            return Percentile(dists, percentile);
        }

        /// <summary>
        /// 선분 a-b를 축으로 하는 캡슐의 반경을 버텍스 집합으로부터 피팅한다.
        /// </summary>
        public static float FitCapsuleRadius(Vector3 a, Vector3 b, IList<Vector3> verts, float percentile = 0.9f)
        {
            if (verts == null || verts.Count == 0)
                return 0f;
            var dists = new List<float>(verts.Count);
            foreach (var v in verts)
                dists.Add(DistancePointToSegment(v, a, b));
            return Percentile(dists, percentile);
        }
    }
}
