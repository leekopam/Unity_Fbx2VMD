using System.IO;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.EditorTools
{
    /// <summary>
    /// lilToon SDF 얼굴 그림자 마스크(_ShadowStrengthMask)를 메시 노멀로부터 UV 공간에 베이크함.
    /// lilToon은 sdf 텍셀을 cos(조명-정면 각도)와 비교해 음영 경계를 정하므로
    /// R = 0.5 - sin(방위각), G = 0.5 + sin(방위각) 인코딩을 따른다.
    /// 방위각은 모델 로컬 +Z 전방 기준 atan2(n.x, n.z)이다.
    /// </summary>
    public static class SdfFaceTextureBaker
    {
        const int DefaultResolution = 1024;

        /// <summary>선택 오브젝트에서 Face 역할 서브메시의 SDF 마스크를 베이크해
        /// "{모델 루트}/tex/sdf/{머티리얼명}_sdf.png"로 저장한다.
        /// 변환기가 탐색하는 "_sdf" 규약과 같은 위치·이름이다.</summary>
        [MenuItem("Tools/Toon/선택 오브젝트 얼굴 SDF 베이크")]
        public static void BakeSelectedFace()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[Toon] SDF를 베이크할 오브젝트를 먼저 선택해 주세요.");
                return;
            }

            int baked = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer smr)
                {
                    mesh = smr.sharedMesh;
                }
                else if (renderer.TryGetComponent(out MeshFilter filter))
                {
                    mesh = filter.sharedMesh;
                }

                if (mesh == null)
                {
                    continue;
                }

                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length && i < mesh.subMeshCount; i++)
                {
                    Material source = slots[i];
                    if (source == null
                        || ToonMaterialRoleResolver.ResolveCharacterRole(source.name)
                            != ToonMaterialRole.Face)
                    {
                        continue;
                    }

                    Texture2D tex = Bake(mesh, i, DefaultResolution);
                    if (tex == null)
                    {
                        Debug.LogWarning(
                            $"[Toon] SDF 베이크 실패(UV 또는 노멀 없음): {source.name}");
                        continue;
                    }

                    SaveSdfPng(tex, source);
                    baked++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Toon] 얼굴 SDF 베이크 완료. 대상={root.name}, 생성={baked}");
        }

        /// <summary>메시의 UV/노멀로 SDF 마스크를 베이크한다. 반환 텍스처는 호출자가 폐기해야 한다.
        /// 입력이 베이크 불가능하면 null.</summary>
        public static Texture2D Bake(Mesh mesh, int submeshIndex, int resolution)
        {
            if (mesh == null || resolution <= 0
                || submeshIndex < 0 || submeshIndex >= mesh.subMeshCount
                || mesh.uv.Length == 0 || mesh.normals.Length == 0)
            {
                return null;
            }

            var uvs = mesh.uv;
            var normals = mesh.normals;
            var triangles = mesh.GetTriangles(submeshIndex);

            // 덮이지 않는 영역은 중간값(정면 음영 경계)으로 채움.
            var pixels = new Color[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0.5f, 0.5f, 0f, 1f);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                RasterizeTriangle(
                    uvs[triangles[i]], uvs[triangles[i + 1]], uvs[triangles[i + 2]],
                    normals[triangles[i]], normals[triangles[i + 1]], normals[triangles[i + 2]],
                    pixels, resolution);
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // SDF는 색이 아니라 데이터 텍스처 — sRGB 보정·밉·압축을 끄고 저장한다.
        private static void SaveSdfPng(Texture2D tex, Material source)
        {
            string matPath = AssetDatabase.GetAssetPath(source);
            string grand = string.IsNullOrEmpty(matPath)
                ? null
                : Path.GetDirectoryName(Path.GetDirectoryName(matPath))?.Replace('\\', '/');
            // 머티리얼이 Assets 루트나 Packages 아래 있으면 경로가 샌다 — 폴백 폴더를 쓴다.
            string dir = string.IsNullOrEmpty(grand) || grand == "Assets"
                || !grand.StartsWith("Assets/")
                ? "Assets/ToonSdf"
                : grand + "/tex/sdf";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{source.name}_sdf.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        // UV 공간 삼각형을 바리센트릭 래스터라이즈해 픽셀별 보간 노멀의 SDF 값을 기록함.
        private static void RasterizeTriangle(
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            Vector3 n0, Vector3 n1, Vector3 n2,
            Color[] pixels, int resolution)
        {
            float x0 = uv0.x * resolution, y0 = uv0.y * resolution;
            float x1 = uv1.x * resolution, y1 = uv1.y * resolution;
            float x2 = uv2.x * resolution, y2 = uv2.y * resolution;

            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, Mathf.Min(x1, x2))));
            int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt(Mathf.Max(x0, Mathf.Max(x1, x2))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, Mathf.Min(y1, y2))));
            int maxY = Mathf.Min(resolution - 1, Mathf.CeilToInt(Mathf.Max(y0, Mathf.Max(y1, y2))));

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (Mathf.Abs(area) < 1e-8f)
                return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = Edge(x1, y1, x2, y2, px, py) / area;
                    float w1 = Edge(x2, y2, x0, y0, px, py) / area;
                    float w2 = Edge(x0, y0, x1, y1, px, py) / area;
                    if (w0 < 0f || w1 < 0f || w2 < 0f)
                        continue;

                    var n = (n0 * w0 + n1 * w1 + n2 * w2).normalized;
                    float azimuth = Mathf.Atan2(n.x, n.z);
                    float sin = Mathf.Sin(azimuth);
                    pixels[y * resolution + x] = new Color(
                        Mathf.Clamp01(0.5f - sin * 0.5f),
                        Mathf.Clamp01(0.5f + sin * 0.5f),
                        0f, 1f);
                }
            }
        }

        private static float Edge(float ax, float ay, float bx, float by, float px, float py)
        {
            return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        }
    }
}
