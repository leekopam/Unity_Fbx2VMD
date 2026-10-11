using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.EditorTools
{
    /// <summary>
    /// lilToon SDF 얼굴 그림자 마스크(_ShadowStrengthMask)를 UV 공간에 베이크함.
    /// lilToon은 "lns = lnSDF*0.5 + sdf*0.5 + 0.25"가 _ShadowBorder 이상이면 점등으로 보므로
    /// 각 텍셀에 "그 방향 빛에 점등되기 시작하는 방위각 경계"를 넣는다:
    /// R = 좌측광 경계, G = 우측광 경계, 값 = 2*border - 0.5 - cos(경계각).
    /// 표현 가능한 경계는 border=0.6 기준 약 45.5°~107.5°뿐 — 그 밖은 0/1로 포화된다.
    /// occluded를 넘기면 라이트 스윕 중 레이캐스트로 얼굴 자체그림자(코·눈두덩)까지 반영한다.
    /// 근사 한계: 오클루더는 바인드 포즈 sharedMesh를 TRS로 올린 것이라 스키닝 포즈와 어긋나고,
    /// Collider.Raycast는 뒷면·메시 내부 시작점을 치지 않아 일부 텍셀의 차광이 빠질 수 있다.
    /// </summary>
    public static class SdfFaceTextureBaker
    {
        const int DefaultResolution = 1024;
        const float DefaultShadowBorder = 0.6f;
        const string MenuPath = "Tools/Toon/선택 오브젝트 얼굴 SDF 베이크";
        // 방위각 스윕 해상도(도) — 굵게 훑은 뒤 첫 비점등 구간만 세밀히 다시 본다.
        // 6°보다 얇은 차광대는 건너뛸 수 있으나 결과는 밝은 쪽으로만 어긋난다.
        const float SweepStepDegrees = 2f;
        const float CoarseStepDegrees = 6f;
        const float SurfaceBias = 0.002f;

        /// <summary>선택 오브젝트에서 Face 역할 서브메시의 SDF 마스크를 베이크해
        /// 변환기가 찾는 기존 "_sdf" 텍스처를 덮어쓰고(GUID 유지), 없으면
        /// "{모델 루트}/tex/sdf/{원본 머티리얼명}_sdf.png"로 새로 저장한다.
        /// 이미 툰 변환된 모델을 선택해도 원본 머티리얼 기준으로 저장한다.</summary>
        [MenuItem(MenuPath)]
        public static void BakeSelectedFace()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[Toon] SDF를 베이크할 오브젝트를 먼저 선택해 주세요.");
                return;
            }

            var faceSlots = new List<(Renderer renderer, Mesh mesh, int submesh, Material source)>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = GetRendererMesh(renderer);
                if (mesh == null)
                {
                    continue;
                }

                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length && i < mesh.subMeshCount; i++)
                {
                    if (slots[i] == null)
                    {
                        continue;
                    }

                    Material source = SceneModelToonConverter.ResolveSourceMaterial(slots[i]);
                    if (ToonMaterialRoleResolver.ResolveCharacterRole(source.name)
                        != ToonMaterialRole.Face)
                    {
                        continue;
                    }

                    if (!mesh.isReadable)
                    {
                        Debug.LogWarning(
                            $"[Toon] 읽기 불가 메시라 SDF를 베이크할 수 없습니다: {source.name}");
                        continue;
                    }

                    faceSlots.Add((renderer, mesh, i, source));
                }
            }

            if (faceSlots.Count == 0)
            {
                Debug.LogWarning($"[Toon] '{root.name}' 아래에서 얼굴 역할 머티리얼을 찾지 못했습니다.");
                return;
            }

            // 얼굴 SDF는 얼굴 형상(코·눈두덩)의 그림자 진행만 담는다 — 머리카락·장식 그림자는
            // 런타임의 다른 경로 몫이고, 전신을 넣으면 머리카락 아래 피부가 항상 그늘로 굳는다.
            // 그래서 오클루더는 Face 서브메시만 합친 임시 메시로 만든다.
            var occluders = new List<Collider>();
            var tempObjects = new List<UnityEngine.Object>();
            int baked = 0;
            bool cancelled = false;
            try
            {
                foreach (var group in faceSlots.GroupBy(slot => slot.renderer))
                {
                    Mesh rendererMesh = group.First().mesh;
                    var faceMesh = new Mesh
                    {
                        name = "~sdf_face_occluder",
                        hideFlags = HideFlags.HideAndDontSave,
                        indexFormat = rendererMesh.indexFormat,
                        vertices = rendererMesh.vertices,
                        triangles = group
                            .SelectMany(slot => rendererMesh.GetTriangles(slot.submesh))
                            .ToArray()
                    };
                    var occluder = new GameObject("~sdf_occluder")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    tempObjects.Add(faceMesh);
                    tempObjects.Add(occluder);
                    occluder.transform.SetPositionAndRotation(
                        group.Key.transform.position, group.Key.transform.rotation);
                    occluder.transform.localScale = group.Key.transform.lossyScale;
                    var collider = occluder.AddComponent<MeshCollider>();
                    collider.sharedMesh = faceMesh;
                    occluders.Add(collider);
                }

                bool IsOccluded(Vector3 pos, Vector3 dir)
                {
                    var ray = new Ray(pos, dir);
                    foreach (Collider col in occluders)
                    {
                        if (col.Raycast(ray, out _, Mathf.Infinity))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                float border = ResolveShadowBorder();
                for (int k = 0; k < faceSlots.Count; k++)
                {
                    (Renderer renderer, Mesh mesh, int submesh, Material source) = faceSlots[k];
                    string label = $"{source.name} ({k + 1}/{faceSlots.Count})";

                    // lilToon의 얼굴 정면은 머티리얼이 아니라 렌더러 오브젝트의 +Z다.
                    Texture2D tex = Bake(mesh, submesh, DefaultResolution,
                        renderer.transform.localToWorldMatrix,
                        renderer.transform.forward, IsOccluded, border,
                        progress => cancelled = EditorUtility.DisplayCancelableProgressBar(
                            "얼굴 SDF 베이크", label, progress));
                    if (cancelled)
                    {
                        break;
                    }

                    if (tex == null)
                    {
                        Debug.LogWarning(
                            $"[Toon] SDF 베이크 실패(UV 또는 노멀 없음): {source.name}");
                        continue;
                    }

                    try
                    {
                        SaveSdfPng(tex, source);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                    baked++;
                }
            }
            finally
            {
                foreach (UnityEngine.Object temp in tempObjects)
                {
                    UnityEngine.Object.DestroyImmediate(temp);
                }
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            string result = cancelled ? "사용자 취소로 중단" : "완료";
            Debug.Log($"[Toon] 얼굴 SDF 베이크 {result}. 대상={root.name}, " +
                $"생성={baked}/{faceSlots.Count}");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanBakeSelectedFace()
        {
            return Selection.activeGameObject != null;
        }

        // 소비자는 Face 프리셋 복제본이므로 경계값도 원본 머티리얼이 아니라 프리셋에서 읽음.
        private static float ResolveShadowBorder()
        {
            var preset = Resources.Load<Material>(ToonMaterialLibrary.ResourceRoot
                + ToonMaterialLibrary.TemplateName(ToonMaterialRole.Face));
            if (preset != null && preset.HasProperty("_ShadowBorder"))
            {
                return preset.GetFloat("_ShadowBorder");
            }

            Debug.LogWarning("[Toon] Face 프리셋을 찾지 못해 기본 경계값으로 SDF를 베이크합니다.");
            return DefaultShadowBorder;
        }

        private static Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr)
            {
                return smr.sharedMesh;
            }
            return renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
        }

        /// <summary>메시의 UV/노멀로 SDF 마스크를 베이크한다. 반환 텍스처는 호출자가 폐기해야 한다.
        /// 입력이 베이크 불가능하면 null.</summary>
        public static Texture2D Bake(Mesh mesh, int submeshIndex, int resolution)
        {
            return Bake(mesh, submeshIndex, resolution,
                Matrix4x4.identity, Vector3.forward, null, DefaultShadowBorder);
        }

        /// <summary>월드 공간 라이트 스윕으로 SDF를 베이크한다.
        /// isOccluded는 (월드 위치, 조명 방향)에서 차광 여부를 돌려주는 술어 — null이면 노멀만으로 판정한다.
        /// forward는 모델 정면의 월드 방향(lilToon의 얼굴 정면과 같은 기준).
        /// reportProgress는 진행률(0~1)을 받아 취소 요청이면 true를 돌려준다 — 취소 시 null 반환.</summary>
        public static Texture2D Bake(Mesh mesh, int submeshIndex, int resolution,
            Matrix4x4 localToWorld, Vector3 forward,
            Func<Vector3, Vector3, bool> isOccluded, float shadowBorder,
            Func<float, bool> reportProgress = null)
        {
            if (mesh == null || resolution <= 0
                || submeshIndex < 0 || submeshIndex >= mesh.subMeshCount
                || mesh.uv.Length == 0 || mesh.normals.Length == 0)
            {
                return null;
            }

            var uvs = mesh.uv;
            var normals = mesh.normals;
            var vertices = mesh.vertices;
            var triangles = mesh.GetTriangles(submeshIndex);

            var worldNormals = new Vector3[resolution * resolution];
            var worldPositions = new Vector3[resolution * resolution];
            var covered = new bool[resolution * resolution];

            // 노멀은 역전치 행렬로 변환해야 비균등 스케일에서도 방향이 유지된다.
            Matrix4x4 normalMatrix = localToWorld.inverse.transpose;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                RasterizeTriangle(
                    uvs[triangles[i]], uvs[triangles[i + 1]], uvs[triangles[i + 2]],
                    normals[triangles[i]], normals[triangles[i + 1]], normals[triangles[i + 2]],
                    vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]],
                    localToWorld, normalMatrix, worldNormals, worldPositions, covered, resolution);
            }

            // 얼굴 정면의 수평 방위 기준 — lilToon은 _ShadowFlatBlur=0이면 XZ 성분만 비교한다.
            Vector3 faceF = new Vector3(forward.x, 0f, forward.z);
            if (faceF == Vector3.zero)
            {
                Debug.LogWarning("[Toon] SDF 기준 정면이 수직이라 임시 축으로 베이크합니다.");
                faceF = Vector3.forward;
            }
            else
            {
                faceF.Normalize();
            }

            // 자기 히트 방지 오프셋은 모델 스케일에 비례해야 한다(cm 단위 임포트 대비).
            float bias = SurfaceBias * Mathf.Max(0.0001f,
                localToWorld.lossyScale.magnitude * 0.57735026f);

            // 이 각도 이상의 경계는 인코딩이 1로 포화되므로 그 너머는 차광을 볼 필요가 없음.
            float sweepLimit = Mathf.Acos(Mathf.Clamp(2f * shadowBorder - 1.5f, -1f, 1f));

            // 덮이지 않는 영역은 90° 경계(일반 램버트와 같은 차광)로 채움.
            float flatBoundary = EncodeBoundary(Mathf.PI * 0.5f, shadowBorder);
            var pixels = new Color[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                bool isCancelRequested = reportProgress != null && (i & 0xFFF) == 0
                    && reportProgress(i / (float)pixels.Length);
                if (isCancelRequested)
                {
                    return null;
                }

                if (!covered[i])
                {
                    pixels[i] = new Color(flatBoundary, flatBoundary, 0f, 1f);
                    continue;
                }

                float boundaryLeft = FindLitBoundary(worldPositions[i], worldNormals[i],
                    -1f, faceF, isOccluded, bias, sweepLimit);
                float boundaryRight = FindLitBoundary(worldPositions[i], worldNormals[i],
                    1f, faceF, isOccluded, bias, sweepLimit);
                pixels[i] = new Color(
                    EncodeBoundary(boundaryLeft, shadowBorder),
                    EncodeBoundary(boundaryRight, shadowBorder), 0f, 1f);
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>정면(0°)에서 지정 쪽으로 방위각을 넓히며 처음으로 점등이 끊기는 각도(라디안)를 찾는다.
        /// 노멀만으로 정해지는 경계는 해석적으로 구하고, 그 안쪽(sweepLimit까지)만 레이캐스트로 차광을 훑는다.
        /// 끝까지 점등되면 180°를 반환한다.</summary>
        internal static float FindLitBoundary(Vector3 worldPos, Vector3 worldNormal,
            float sideSign, Vector3 faceF, Func<Vector3, Vector3, bool> isOccluded, float bias,
            float sweepLimit = Mathf.PI)
        {
            Vector3 right = new Vector3(faceF.z, 0f, -faceF.x);
            float geometricBoundary = FindGeometricBoundary(worldNormal, sideSign, faceF, right);
            if (isOccluded == null)
            {
                return geometricBoundary;
            }

            Vector3 origin = worldPos + worldNormal * bias;
            bool IsOccludedAt(float deg)
            {
                float phi = Mathf.Deg2Rad * deg;
                Vector3 lightDir = faceF * Mathf.Cos(phi) + right * Mathf.Sin(phi) * sideSign;
                return isOccluded(origin, lightDir);
            }

            // 기하 경계 안쪽은 노멀상 점등이 보장되므로 차광만 확인함.
            float limitDeg = Mathf.Rad2Deg * Mathf.Min(geometricBoundary, sweepLimit);
            float litThrough = 0f;
            for (float deg = CoarseStepDegrees; deg < limitDeg; deg += CoarseStepDegrees)
            {
                if (!IsOccludedAt(deg))
                {
                    litThrough = deg;
                    continue;
                }

                // 직전 점등각~이번 각 사이만 세밀히 다시 봐 첫 비점등각을 확정함.
                for (float fine = litThrough + SweepStepDegrees; fine < deg; fine += SweepStepDegrees)
                {
                    if (IsOccludedAt(fine))
                    {
                        return Mathf.Deg2Rad * fine;
                    }
                }
                return Mathf.Deg2Rad * deg;
            }
            return geometricBoundary;
        }

        /// <summary>노멀만으로 정해지는 점등 경계각(라디안). dot(빛, n) = a·cosφ + b·sinφ = R·cos(φ-δ)이므로
        /// 정면 근처에서 점등이면 경계는 δ+90°(최대 180°), 정면 근처부터 비점등이면 0이다.</summary>
        private static float FindGeometricBoundary(
            Vector3 worldNormal, float sideSign, Vector3 faceF, Vector3 right)
        {
            float a = Vector3.Dot(faceF, worldNormal);
            float b = Vector3.Dot(right, worldNormal) * sideSign;
            // 수직 노멀은 수평 빛과 직교해 어느 방위에서도 점등되지 않음.
            if (a * a + b * b < 1e-8f)
            {
                return 0f;
            }

            // 첫 표본각보다 좁은 정면 비점등 구간은 단일 임계값으로 표현할 수 없어 점등으로 근사함.
            float delta = Mathf.Atan2(b, a);
            bool isLitNearFront = delta > -Mathf.PI * 0.5f
                && delta < Mathf.PI * 0.5f + Mathf.Deg2Rad * CoarseStepDegrees;
            return isLitNearFront ? Mathf.Min(delta + Mathf.PI * 0.5f, Mathf.PI) : 0f;
        }

        /// <summary>경계 방위각을 lilToon SDF 텍셀 값으로 인코딩한다.
        /// 점등 조건 lns = lnSDF*0.5 + sdf*0.5 + 0.25 >= border 를 역산한 값.</summary>
        internal static float EncodeBoundary(float boundaryRad, float shadowBorder)
        {
            return Mathf.Clamp01(2f * shadowBorder - 0.5f - Mathf.Cos(boundaryRad));
        }

        // SDF는 색이 아니라 데이터 텍스처 — sRGB 보정·밉·압축을 끄고 저장한다.
        // 변환기가 이미 쓰는 PNG가 있으면 그 파일을 덮어써 GUID와 기존 툰 머티리얼 참조를 유지함.
        private static void SaveSdfPng(Texture2D tex, Material source)
        {
            string existing = SceneModelToonConverter.FindSdfTexturePath(source);
            bool canOverwrite = existing != null
                && existing.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
            string path = canOverwrite ? existing : BuildDefaultSdfPath(source);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());

            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Debug.Log($"[Toon] SDF 저장: {path}");
        }

        private static string BuildDefaultSdfPath(Material source)
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
            return $"{dir}/{source.name}_sdf.png";
        }

        // UV 공간 삼각형을 바리센트릭 래스터라이즈해 픽셀별 월드 위치·노멀을 보간해 기록함.
        private static void RasterizeTriangle(
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            Vector3 n0, Vector3 n1, Vector3 n2,
            Vector3 v0, Vector3 v1, Vector3 v2,
            Matrix4x4 localToWorld, Matrix4x4 normalMatrix,
            Vector3[] worldNormals, Vector3[] worldPositions, bool[] covered,
            int resolution)
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

                    int index = y * resolution + x;
                    var n = (n0 * w0 + n1 * w1 + n2 * w2).normalized;
                    var v = v0 * w0 + v1 * w1 + v2 * w2;
                    worldNormals[index] = normalMatrix.MultiplyVector(n).normalized;
                    worldPositions[index] = localToWorld.MultiplyPoint3x4(v);
                    covered[index] = true;
                }
            }
        }

        private static float Edge(float ax, float ay, float bx, float by, float px, float py)
        {
            return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        }
    }
}
