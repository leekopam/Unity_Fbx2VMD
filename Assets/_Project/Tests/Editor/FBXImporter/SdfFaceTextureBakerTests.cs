using NUnit.Framework;
using UnityEngine;
using Fbx2Vmd.EditorTools;

namespace Tests.Editor.FBXImporter
{
    public class SdfFaceTextureBakerTests
    {
        private static Mesh CreateQuad(Vector3 normal)
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f),
                },
                uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                },
                normals = new[] { normal, normal, normal, normal },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
            };
            return mesh;
        }

        [Test]
        public void 전방_노멀은_90도_경계값으로_베이크된다()
        {
            // _ShadowBorder=0.6 기준 90° 경계 → 2*0.6 - 0.5 - cos(90°) = 0.7.
            var tex = SdfFaceTextureBaker.Bake(CreateQuad(Vector3.forward), 0, 32);
            var c = tex.GetPixel(16, 16);
            Assert.AreEqual(0.7f, c.r, 0.05f);
            Assert.AreEqual(0.7f, c.g, 0.05f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void 오른쪽을_향한_노멀은_우측광_G가_높고_좌측광_R은_0이다()
        {
            // +X 노멀: +X 방향 조명에서 오래 밝게 유지 → G 채널이 커야 함.
            var tex = SdfFaceTextureBaker.Bake(CreateQuad(Vector3.right), 0, 32);
            var c = tex.GetPixel(16, 16);
            Assert.Greater(c.g, 0.9f);
            Assert.Less(c.r, 0.05f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void 비스듬한_노멀의_기하_경계는_해석적으로_정확하다()
        {
            // 오른쪽으로 45° 돌아간 노멀: 우측광은 135°, 좌측광은 45°까지 점등.
            Vector3 n = new Vector3(Mathf.Sin(Mathf.PI / 4f), 0f, Mathf.Cos(Mathf.PI / 4f));
            float right = SdfFaceTextureBaker.FindLitBoundary(
                Vector3.zero, n, 1f, Vector3.forward, null, 0f);
            float left = SdfFaceTextureBaker.FindLitBoundary(
                Vector3.zero, n, -1f, Vector3.forward, null, 0f);
            Assert.AreEqual(135f, right * Mathf.Rad2Deg, 0.01f);
            Assert.AreEqual(45f, left * Mathf.Rad2Deg, 0.01f);
        }

        [Test]
        public void 정면만_살짝_어두운_측후면_노멀은_넓게_점등으로_근사한다()
        {
            // 3° 뒤로 넘어간 측면 노멀: 3°~180° 점등이므로 단일 임계값으로는 180°가 가장 가깝다.
            float back = 3f * Mathf.Deg2Rad;
            Vector3 n = new Vector3(Mathf.Cos(back), 0f, -Mathf.Sin(back));
            float boundary = SdfFaceTextureBaker.FindLitBoundary(
                Vector3.zero, n, 1f, Vector3.forward, null, 0f);
            Assert.AreEqual(Mathf.PI, boundary, 0.001f);
        }

        [Test]
        public void 수직_노멀은_어느_방위에서도_점등되지_않는다()
        {
            float boundary = SdfFaceTextureBaker.FindLitBoundary(
                Vector3.zero, Vector3.up, 1f, Vector3.forward, null, 0f);
            Assert.AreEqual(0f, boundary);
        }

        [Test]
        public void 포화각_너머는_차광을_검사하지_않는다()
        {
            // 인코딩이 1로 포화되는 각도 이후의 차광은 결과를 바꾸지 못하므로 레이캐스트를 아낀다.
            float maxQueried = 0f;
            bool RecordAngle(Vector3 pos, Vector3 dir)
            {
                maxQueried = Mathf.Max(maxQueried, Vector3.Angle(Vector3.forward, dir));
                return false;
            }

            float sweepLimit = 100f * Mathf.Deg2Rad;
            float boundary = SdfFaceTextureBaker.FindLitBoundary(
                Vector3.zero, Vector3.right, 1f, Vector3.forward, RecordAngle, 0f, sweepLimit);
            Assert.AreEqual(Mathf.PI, boundary, 0.001f);
            Assert.Less(maxQueried, 100f);
        }

        [Test]
        public void 알파_채널은_항상_1이다()
        {
            var tex = SdfFaceTextureBaker.Bake(CreateQuad(Vector3.forward), 0, 32);
            var c = tex.GetPixel(16, 16);
            Assert.AreEqual(1f, c.a, 0.01f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void 잘못된_입력은_null을_반환한다()
        {
            var quad = CreateQuad(Vector3.forward);
            Assert.IsNull(SdfFaceTextureBaker.Bake(null, 0, 32));
            Assert.IsNull(SdfFaceTextureBaker.Bake(quad, -1, 32));
            Assert.IsNull(SdfFaceTextureBaker.Bake(quad, 5, 32));
            Assert.IsNull(SdfFaceTextureBaker.Bake(quad, 0, 0));
            var noUv = new Mesh { vertices = new[] { Vector3.zero }, triangles = new[] { 0, 0, 0 } };
            Assert.IsNull(SdfFaceTextureBaker.Bake(noUv, 0, 32));
            Object.DestroyImmediate(quad);
            Object.DestroyImmediate(noUv);
        }

        [Test]
        public void 삼각형_밖_영역은_기본값을_유지한다()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                uv = new[] { Vector2.zero, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f) },
                normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                triangles = new[] { 0, 1, 2 },
            };
            var tex = SdfFaceTextureBaker.Bake(mesh, 0, 32);
            var c = tex.GetPixel(28, 28);
            Assert.AreEqual(0.7f, c.r, 0.05f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void 좌측_빛이_차광되면_R_채널만_낮아진다()
        {
            // 좌측광(φ<0, lightDir.x<0)만 가리는 오클루더 — G(우측광 경계)는 그대로여야 한다.
            bool BlockLeft(Vector3 pos, Vector3 dir) => dir.x < 0f;

            var tex = SdfFaceTextureBaker.Bake(CreateQuad(Vector3.forward), 0, 32,
                Matrix4x4.identity, Vector3.forward, BlockLeft, 0.6f);
            var c = tex.GetPixel(16, 16);
            Assert.Less(c.r, 0.05f);
            Assert.AreEqual(0.7f, c.g, 0.05f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void 경계각은_lilToon_점등_조건의_역산이다()
        {
            // lit iff sdf >= 2*border - 0.5 - cos(phi) — 경계 φ를 그대로 인코딩한다.
            Assert.AreEqual(0.7f, SdfFaceTextureBaker.EncodeBoundary(Mathf.PI * 0.5f, 0.6f), 0.001f);
            Assert.AreEqual(0f, SdfFaceTextureBaker.EncodeBoundary(0f, 0.6f));
            Assert.AreEqual(1f, SdfFaceTextureBaker.EncodeBoundary(Mathf.PI, 0.6f));
        }
    }
}
