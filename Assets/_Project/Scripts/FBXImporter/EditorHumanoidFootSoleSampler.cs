#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 발 가중치로 선정한 밑창 영역과 현재 자세의 접촉점 좌표를 취득함.
    /// </summary>
    internal sealed class EditorHumanoidFootSoleSampler : IDisposable
    {
        private readonly Transform _foot;
        private readonly List<Surface> _surfaces = new List<Surface>();
        private Point[] _points;
        private Vector3[] _localPoints;
        private Vector3[] _worldPoints;
        private bool _hasSample;
        private bool _isDisposed;

        private EditorHumanoidFootSoleSampler(Transform foot) => _foot = foot;

        // 다음 TrySample에서 갱신되는 대여 버퍼이며 앵커는 좌표 값을 따로 복사해 보관함.
        internal Vector3[] LocalSolePoints => _hasSample ? _localPoints : null;

        internal static bool TryCreate(
            Transform foot, Transform toes, SkinnedMeshRenderer[] renderers,
            Vector3 calibrationUp, out EditorHumanoidFootSoleSampler sampler)
        {
            sampler = null;
            if (!HasSupportedTransform(foot) || toes == null || !toes.IsChildOf(foot) ||
                renderers == null || !IsUnitVector(calibrationUp))
                return false;

            Vector3 forward = Vector3.ProjectOnPlane(toes.position - foot.position, calibrationUp);
            if (!IsFinite(forward) || forward.sqrMagnitude < 0.00000001f)
                return false;

            var candidate = new EditorHumanoidFootSoleSampler(foot);
            try
            {
                if (!candidate.TryInitialize(renderers, calibrationUp, forward.normalized))
                    return false;
                sampler = candidate;
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
            finally
            {
                if (sampler == null)
                    candidate.Dispose();
            }
        }

        internal bool TrySample()
        {
            _hasSample = false;
            if (_isDisposed || !HasSupportedTransform(_foot))
                return false;

            foreach (Surface surface in _surfaces)
                if (!surface.TryBake())
                    return false;

            Matrix4x4 worldToFoot = _foot.worldToLocalMatrix;
            for (int i = 0; i < _points.Length; i++)
            {
                Point point = _points[i];
                Vector3 world = point.Surface.GetWorldPoint(point.Vertex);
                Vector3 local = worldToFoot.MultiplyPoint3x4(world);
                if (!IsFinite(world) || !IsFinite(local))
                    return false;
                _worldPoints[i] = world;
                _localPoints[i] = local;
            }
            _hasSample = true;
            return true;
        }

        internal bool TrySelectContact(
            bool isFront, Vector3 groundNormal, out int pointIndex, out Vector3 worldPoint)
        {
            pointIndex = -1;
            worldPoint = Vector3.zero;
            if (!_hasSample || !IsUnitVector(groundNormal))
                return false;

            float minimum = float.PositiveInfinity;
            for (int i = 0; i < _points.Length; i++)
            {
                if (!(isFront ? _points[i].IsFront : _points[i].IsRear))
                    continue;
                float height = Vector3.Dot(_worldPoints[i], groundNormal);
                // 입력 렌더러 순서와 정점 번호를 유지하여 같은 높이에서도 선택이 결정적이도록 함.
                if (height < minimum)
                {
                    minimum = height;
                    pointIndex = i;
                }
            }
            if (pointIndex < 0)
                return false;
            worldPoint = _worldPoints[pointIndex];
            return true;
        }

        internal bool TryGetLocalPoint(int pointIndex, out Vector3 point)
        {
            point = Vector3.zero;
            if (!_hasSample || pointIndex < 0 || pointIndex >= _points.Length)
                return false;
            point = _localPoints[pointIndex];
            return true;
        }

        internal bool TryGetPointIdentity(int pointIndex, out SkinnedMeshRenderer renderer, out int vertex)
        {
            renderer = null;
            vertex = -1;
            if (!_hasSample || pointIndex < 0 || pointIndex >= _points.Length)
                return false;
            renderer = _points[pointIndex].Surface.Renderer;
            vertex = _points[pointIndex].Vertex;
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _hasSample = false;
            foreach (Surface surface in _surfaces)
                surface.Dispose();
            _surfaces.Clear();
        }

        private bool TryInitialize(SkinnedMeshRenderer[] renderers, Vector3 up, Vector3 forward)
        {
            var candidates = new List<Point>();
            var seen = new HashSet<SkinnedMeshRenderer>();
            float minimumHeight = float.PositiveInfinity;
            float back = float.PositiveInfinity, front = float.NegativeInfinity;
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null || !seen.Add(renderer))
                    continue;
                Mesh mesh = renderer.sharedMesh;
                // Editor에서는 Read/Write가 꺼진 임포트 메시도 읽을 수 있어 실제 취득 결과로 판단함.
                BoneWeight[] weights = mesh.boneWeights;
                Transform[] bones = renderer.bones;
                if (weights.Length != mesh.vertexCount)
                    return false;
                var vertices = new List<int>();
                for (int i = 0; i < weights.Length; i++)
                {
                    BoneWeight w = weights[i];
                    float weight = GetFootWeight(bones, w.boneIndex0, w.weight0) +
                        GetFootWeight(bones, w.boneIndex1, w.weight1) +
                        GetFootWeight(bones, w.boneIndex2, w.weight2) +
                        GetFootWeight(bones, w.boneIndex3, w.weight3);
                    if (!IsFinite(weight))
                        return false;
                    if (weight >= 0.5f)
                        vertices.Add(i);
                }
                if (vertices.Count == 0)
                    continue;
                var surface = new Surface(renderer, mesh, bones);
                _surfaces.Add(surface);
                if (!surface.TryBake())
                    return false;
                foreach (int vertex in vertices)
                {
                    Vector3 world = surface.GetWorldPoint(vertex);
                    if (!IsFinite(world))
                        return false;
                    float height = Vector3.Dot(world, up), progress = Vector3.Dot(world, forward);
                    if (!IsFinite(height) || !IsFinite(progress))
                        return false;
                    minimumHeight = Mathf.Min(minimumHeight, height);
                    back = Mathf.Min(back, progress);
                    front = Mathf.Max(front, progress);
                    candidates.Add(new Point(surface, vertex));
                }
            }
            float length = front - back;
            if (candidates.Count == 0 || !IsFinite(length) || length <= 0.000001f)
                return false;

            candidates.RemoveAll(p => Vector3.Dot(p.Surface.GetWorldPoint(p.Vertex), up) > minimumHeight + length * 0.08f);
            back = float.PositiveInfinity;
            front = float.NegativeInfinity;
            foreach (Point p in candidates)
            {
                float progress = Vector3.Dot(p.Surface.GetWorldPoint(p.Vertex), forward);
                back = Mathf.Min(back, progress);
                front = Mathf.Max(front, progress);
            }
            _points = candidates.ToArray();
            for (int i = 0; i < _points.Length; i++)
            {
                float progress = Vector3.Dot(_points[i].Surface.GetWorldPoint(_points[i].Vertex), forward);
                _points[i].IsRear = progress <= back + (front - back) * 0.25f;
                _points[i].IsFront = progress >= back + (front - back) * 0.75f;
            }
            _localPoints = new Vector3[_points.Length];
            _worldPoints = new Vector3[_points.Length];
            return TrySample();
        }

        private float GetFootWeight(Transform[] bones, int index, float weight)
        {
            if (weight == 0f)
                return 0f;
            if (!IsFinite(weight) || weight < 0f || index < 0 || index >= bones.Length || bones[index] == null)
                return float.NaN;
            return bones[index].IsChildOf(_foot) ? weight : 0f;
        }

        private static bool HasSupportedTransform(Transform foot)
        {
            if (foot == null)
                return false;
            Vector3 scale = foot.lossyScale;
            if (!IsFinite(scale) || scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
                return false;
            Matrix4x4 matrix = foot.localToWorldMatrix;
            // 회전된 비균일 조상 스케일의 전단까지 검사하여 순수 계산기의 회전·균일 배율 계약을 지킴.
            return Vector3.Distance(matrix.MultiplyVector(Vector3.right) / scale.x, foot.rotation * Vector3.right) < 0.0001f &&
                Vector3.Distance(matrix.MultiplyVector(Vector3.up) / scale.x, foot.rotation * Vector3.up) < 0.0001f &&
                Vector3.Distance(matrix.MultiplyVector(Vector3.forward) / scale.x, foot.rotation * Vector3.forward) < 0.0001f;
        }

        private static bool IsUnitVector(Vector3 v) => IsFinite(v) && Mathf.Abs(v.sqrMagnitude - 1f) < 0.0001f;
        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private struct Point
        {
            internal readonly Surface Surface;
            internal readonly int Vertex;
            internal bool IsRear;
            internal bool IsFront;
            internal Point(Surface surface, int vertex)
            {
                Surface = surface;
                Vertex = vertex;
                IsRear = IsFront = false;
            }
        }

        private sealed class Surface : IDisposable
        {
            internal readonly SkinnedMeshRenderer Renderer;
            private readonly Mesh _source;
            private readonly Transform[] _bones;
            private readonly int _vertexCount;
            private readonly Mesh _baked;
            private readonly List<Vector3> _vertices;

            internal Surface(SkinnedMeshRenderer renderer, Mesh source, Transform[] bones)
            {
                Renderer = renderer;
                _source = source;
                _bones = bones;
                _vertexCount = source.vertexCount;
                _baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                _vertices = new List<Vector3>(source.vertexCount);
            }

            internal bool TryBake()
            {
                if (Renderer == null || _source == null || Renderer.sharedMesh != _source || _source.vertexCount != _vertexCount)
                    return false;
                Transform[] bones = Renderer.bones;
                if (bones.Length != _bones.Length)
                    return false;
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] != _bones[i])
                        return false;
                Renderer.BakeMesh(_baked);
                _baked.GetVertices(_vertices);
                return _vertices.Count == _source.vertexCount;
            }

            internal Vector3 GetWorldPoint(int vertex) => Renderer.localToWorldMatrix.MultiplyPoint3x4(_vertices[vertex]);
            public void Dispose() => UnityEngine.Object.DestroyImmediate(_baked);
        }
    }
}
#endif
