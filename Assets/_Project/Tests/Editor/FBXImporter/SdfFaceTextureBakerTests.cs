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
        public void 전방_노멀은_중간값으로_베이크된다()
        {
            var tex = SdfFaceTextureBaker.Bake(CreateQuad(Vector3.forward), 0, 32);
            var c = tex.GetPixel(16, 16);
            Assert.AreEqual(0.5f, c.r, 0.05f);
            Assert.AreEqual(0.5f, c.g, 0.05f);
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
            Assert.AreEqual(0.5f, c.r, 0.05f);
            Object.DestroyImmediate(tex);
        }
    }
}
