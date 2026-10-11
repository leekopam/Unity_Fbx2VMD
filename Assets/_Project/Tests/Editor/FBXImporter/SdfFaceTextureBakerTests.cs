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
        public void 좌측_노멀은_G가_높고_우측_노멀은_R이_높다()
        {
            // +X 노멀: +X 방향 조명에서 오래 밝게 유지 → G 채널이 커야 함.
            var tex = SdfFaceTextureBaker.Bake(CreateQuad(Vector3.right), 0, 32);
            var c = tex.GetPixel(16, 16);
            Assert.Greater(c.g, c.r);
            Assert.Greater(c.g, 0.9f);
            Object.DestroyImmediate(tex);
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
