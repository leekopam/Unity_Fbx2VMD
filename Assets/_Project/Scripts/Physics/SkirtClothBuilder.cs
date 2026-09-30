using System.Collections.Generic;
using System.Text.RegularExpressions;
using MagicaCloth2;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using VertexAttribute = MagicaCloth2.VertexAttribute;
using UVertexAttribute = UnityEngine.Rendering.VertexAttribute;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 런타임 추출로 생성된 스커트 전용 렌더러의 마커.
    /// 원본 렌더러와 원본 메시 참조를 들고 있어 정리 시 복원할 수 있다.
    /// </summary>
    public class ExtractedSkirtMesh : MonoBehaviour
    {
        public SkinnedMeshRenderer sourceRenderer;
        public Mesh originalMesh;
    }

    /// <summary>
    /// 스커트 본 체인과 스키닝 가중치로 MeshCloth를 자동 생성한다.
    /// MC2 공식 런타임 경로(ClothSerializeData2.vertexAttributeList)를 사용해
    /// 수동 버텍스 페인팅 없이 고정/이동 속성을 결정한다.
    /// - 스커트 체인 루트(fixedChainDepth 이하)에 스키닝된 버텍스 → Fixed (허리 고정부)
    /// - 더 깊은 스커트 본에 스키닝된 버텍스 → Move
    /// - 스커트 가중치 합이 임계 미만 → Invalid (본래 본 스키닝 유지, 프록시 메시에서 제외)
    /// MC2 렌더러 한도(65535 버텍스)를 넘는 메시는 스커트 삼각형만 전용
    /// 렌더러로 런타임 추출해 등록한다(ExtractSkirtRenderer).
    /// 파라미터는 공식 MC2_Preset_Skirt / 런타임 구축 예제 값을 따른다.
    /// </summary>
    public static class SkirtClothBuilder
    {
        public static readonly Regex SkirtBoneRegex =
            new Regex(@"skirt|スカート", RegexOptions.IgnoreCase);

        /// <summary>MC2 RenderSetupData의 렌더러당 최대 버텍스 수</summary>
        public const int MaxRendererVertices = 65535;

        public struct Result
        {
            public MagicaCloth cloth;
            public int skirtBoneCount;
            public int fixedVertexCount;
            public int moveVertexCount;
            public int rendererCount;
            public List<ExtractedSkirtMesh> extractedMeshes;
            public List<string> warnings;
        }

        /// <summary>
        /// 루트 하위에서 스커트 본을 수집하고 각 본의 체인 깊이를 계산한다.
        /// 깊이 0 = 스커트 체인의 루트(부모가 비스커트 본, 보통 허리).
        /// 연속 스커트 부모 수를 깊이로 사용 — 명명 숫자 규칙에 의존하지 않는다.
        /// </summary>
        public static Dictionary<Transform, int> CollectSkirtBoneDepths(Transform root)
        {
            var depths = new Dictionary<Transform, int>();
            if (root == null)
                return depths;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!SkirtBoneRegex.IsMatch(t.name) || t.name.StartsWith("MC2"))
                    continue;
                int depth = 0;
                for (var p = t.parent; p != null && SkirtBoneRegex.IsMatch(p.name); p = p.parent)
                    depth++;
                depths[t] = depth;
            }
            return depths;
        }

        /// <summary>
        /// 렌더러 메시의 버텍스별 속성을 계산한다.
        /// 스커트 본 가중치 합이 threshold 이상인 버텍스만 클로스 대상 —
        /// 그 중 주도 본의 체인 깊이가 fixedChainDepth 이하면 Fixed, 아니면 Move.
        /// 반환 배열 길이는 항상 메시 버텍스 수와 동일 (MC2 계약).
        /// </summary>
        public static VertexAttribute[] BuildVertexAttributes(
            SkinnedMeshRenderer smr,
            Dictionary<Transform, int> skirtBoneDepths,
            float skirtWeightThreshold,
            int fixedChainDepth,
            out int fixedCount,
            out int moveCount)
        {
            fixedCount = 0;
            moveCount = 0;
            var mesh = smr.sharedMesh;
            int vcnt = mesh.vertexCount;
            var attrs = new VertexAttribute[vcnt]; // 기본값 = Invalid (Flag 없음)

            var bones = smr.bones;
            if (bones == null || bones.Length == 0)
                return attrs;

            // 본 인덱스 → 스커트 깊이 (비스커트 본은 사전 부재)
            var boneDepthByIndex = new Dictionary<int, int>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && skirtBoneDepths.TryGetValue(bones[i], out int d))
                    boneDepthByIndex[i] = d;
            }
            if (boneDepthByIndex.Count == 0)
                return attrs;

            using var bonesPerVertex = mesh.GetBonesPerVertex();
            using var allWeights = mesh.GetAllBoneWeights();
            if (bonesPerVertex.Length != vcnt)
                return attrs;

            int wi = 0;
            for (int v = 0; v < vcnt; v++)
            {
                int n = bonesPerVertex[v];
                float skirtWeight = 0f;
                int bestDepth = int.MaxValue;
                float bestW = -1f;
                for (int k = 0; k < n; k++, wi++)
                {
                    var bw = allWeights[wi];
                    if (!boneDepthByIndex.TryGetValue(bw.boneIndex, out int depth))
                        continue;
                    skirtWeight += bw.weight;
                    if (bw.weight > bestW)
                    {
                        bestW = bw.weight;
                        bestDepth = depth;
                    }
                }
                if (skirtWeight >= skirtWeightThreshold)
                {
                    attrs[v] = bestDepth <= fixedChainDepth
                        ? VertexAttribute.Fixed : VertexAttribute.Move;
                    if (bestDepth <= fixedChainDepth) fixedCount++;
                    else moveCount++;
                }
                // 임계 미만 → Invalid 유지 (스커트 아님 — 본래 스키닝 유지)
            }
            return attrs;
        }

        /// <summary>
        /// 스커트 버텍스를 참조하는 삼각형(3버텍스 모두 클로스 대상)만 모아
        /// 전용 SkinnedMeshRenderer로 추출한다. 원본 메시는 클론 후 해당
        /// 삼각형을 제거하고 원본 렌더러에 적용한다(에셋 자체는 변경하지 않음).
        /// 경계 버텍스는 Fixed 속성으로 원래 스키닝 위치를 추적하므로
        /// 본체 메시와의 이음매가 유지된다.
        /// 반환된 렌더러의 모든 버텍스는 스커트 대상 — remappedAttrs는
        /// 추출 메시의 버텍스 순서에 맞춰 재배열된 속성이다.
        /// </summary>
        public static ExtractedSkirtMesh ExtractSkirtRenderer(
            SkinnedMeshRenderer source, VertexAttribute[] attrs,
            out VertexAttribute[] remappedAttrs)
        {
            remappedAttrs = null;
            var mesh = source.sharedMesh;
            if (mesh == null || attrs == null || attrs.Length != mesh.vertexCount)
                return null;

            int subCount = mesh.subMeshCount;
            var extractTris = new List<int>[subCount];
            var remainTris = new List<int>[subCount];
            var topology = new MeshTopology[subCount];
            var used = new bool[mesh.vertexCount];
            bool any = false;
            for (int s = 0; s < subCount; s++)
            {
                extractTris[s] = new List<int>();
                remainTris[s] = new List<int>();
                topology[s] = mesh.GetTopology(s);
                if (topology[s] != MeshTopology.Triangles)
                {
                    // 비삼각형 서브메시는 스커트 추출 대상에서 제외하고 원형 유지
                    remainTris[s].AddRange(mesh.GetIndices(s));
                    continue;
                }
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    bool skirt = !attrs[tris[i]].IsInvalid()
                        && !attrs[tris[i + 1]].IsInvalid()
                        && !attrs[tris[i + 2]].IsInvalid();
                    var dst = skirt ? extractTris[s] : remainTris[s];
                    dst.Add(tris[i]); dst.Add(tris[i + 1]); dst.Add(tris[i + 2]);
                    if (skirt)
                    {
                        used[tris[i]] = used[tris[i + 1]] = used[tris[i + 2]] = true;
                        any = true;
                    }
                }
            }
            if (!any)
                return null;

            // 버텍스 리맵 + 속성 재배열
            int vcnt = mesh.vertexCount;
            var remap = new int[vcnt];
            int ncnt = 0;
            for (int v = 0; v < vcnt; v++)
                if (used[v]) remap[v] = ncnt++;

            var newMesh = new Mesh { name = mesh.name + "_skirt" };
            var verts = mesh.vertices;
            var newVerts = new Vector3[ncnt];
            remappedAttrs = new VertexAttribute[ncnt];
            for (int v = 0; v < vcnt; v++)
                if (used[v])
                {
                    newVerts[remap[v]] = verts[v];
                    remappedAttrs[remap[v]] = attrs[v];
                }
            newMesh.vertices = newVerts;
            CopyVertexChannel(mesh, newMesh, remap, used, vcnt, UVertexAttribute.Normal);
            CopyVertexChannel(mesh, newMesh, remap, used, vcnt, UVertexAttribute.Tangent);
            CopyVertexChannel(mesh, newMesh, remap, used, vcnt, UVertexAttribute.Color);
            for (var uv = UVertexAttribute.TexCoord0; uv <= UVertexAttribute.TexCoord7; uv++)
                CopyVertexChannel(mesh, newMesh, remap, used, vcnt, uv);

            // 본 가중치 복사 (추출 메시도 원본 bones 배열을 공유)
            using var allWeights = mesh.GetAllBoneWeights();
            using var bonesPerVertex = mesh.GetBonesPerVertex();
            var newBpv = new NativeArray<byte>(ncnt, Allocator.Temp);
            var newWeights = new List<BoneWeight1>();
            int woff = 0;
            for (int v = 0; v < vcnt; v++)
            {
                int n = bonesPerVertex[v];
                if (used[v])
                {
                    newBpv[remap[v]] = (byte)n;
                    for (int k = 0; k < n; k++)
                        newWeights.Add(allWeights[woff + k]);
                }
                woff += n;
            }
            using (var weightsNA = new NativeArray<BoneWeight1>(newWeights.ToArray(), Allocator.Temp))
                newMesh.SetBoneWeights(newBpv, weightsNA);
            newBpv.Dispose();
            newMesh.bindposes = mesh.bindposes;

            // 서브메시 구조/머티리얼 순서 유지
            newMesh.subMeshCount = subCount;
            for (int s = 0; s < subCount; s++)
            {
                var tris = extractTris[s];
                var remapped = new int[tris.Count];
                for (int i = 0; i < tris.Count; i++)
                    remapped[i] = remap[tris[i]];
                newMesh.SetTriangles(remapped, s);
            }
            newMesh.RecalculateBounds();

            // 블렌드셰이프 복사 — 스커트 버텍스에 델타가 있는 셰이프는
            // 프레임 구성을 유지한 채 전체 프레임을 이관한다
            for (int s = 0; s < mesh.blendShapeCount; s++)
            {
                int frames = mesh.GetBlendShapeFrameCount(s);
                var fdv = new Vector3[frames][];
                var fdn = new Vector3[frames][];
                var fdt = new Vector3[frames][];
                bool hasDelta = false;
                for (int f = 0; f < frames; f++)
                {
                    fdv[f] = new Vector3[vcnt];
                    fdn[f] = new Vector3[vcnt];
                    fdt[f] = new Vector3[vcnt];
                    mesh.GetBlendShapeFrameVertices(s, f, fdv[f], fdn[f], fdt[f]);
                    if (!hasDelta)
                        for (int v = 0; v < vcnt; v++)
                            if (used[v] && fdv[f][v] != Vector3.zero) { hasDelta = true; break; }
                }
                if (!hasDelta)
                    continue;
                string shapeName = mesh.GetBlendShapeName(s);
                for (int f = 0; f < frames; f++)
                {
                    var ndv = new Vector3[ncnt];
                    var ndn = new Vector3[ncnt];
                    var ndt = new Vector3[ncnt];
                    for (int v = 0; v < vcnt; v++)
                        if (used[v])
                        {
                            int nv = remap[v];
                            ndv[nv] = fdv[f][v]; ndn[nv] = fdn[f][v]; ndt[nv] = fdt[f][v];
                        }
                    newMesh.AddBlendShapeFrame(
                        shapeName, mesh.GetBlendShapeFrameWeight(s, f), ndv, ndn, ndt);
                }
            }

            // 원본 메시 클론에서 스커트 삼각형 제거 (토폴로지 유지)
            var remainMesh = Object.Instantiate(mesh);
            remainMesh.name = mesh.name + "_body";
            for (int s = 0; s < subCount; s++)
                remainMesh.SetIndices(remainTris[s], topology[s], s);

            // 추출 렌더러 생성 — 원본 설정/본/머티리얼 상속
            var go = new GameObject(CharacterPhysicsSetup.ClothNamePrefix + "SkirtMesh_" + source.name);
            go.transform.SetParent(source.transform.parent, false);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = newMesh;
            smr.bones = source.bones;
            smr.rootBone = source.rootBone;
            smr.sharedMaterials = source.sharedMaterials;
            smr.localBounds = mesh.bounds;
            smr.updateWhenOffscreen = source.updateWhenOffscreen;
            smr.shadowCastingMode = source.shadowCastingMode;
            smr.receiveShadows = source.receiveShadows;

            var marker = go.AddComponent<ExtractedSkirtMesh>();
            marker.sourceRenderer = source;
            marker.originalMesh = mesh;
            source.sharedMesh = remainMesh;
            return marker;
        }

        static void CopyVertexChannel(
            Mesh src, Mesh dst, int[] remap, bool[] used, int vcnt, UVertexAttribute attr)
        {
            if (!src.HasVertexAttribute(attr))
                return;
            switch (attr)
            {
                case UVertexAttribute.Normal:
                    var n = src.normals;
                    var dn = new Vector3[dst.vertexCount];
                    for (int v = 0; v < vcnt; v++) if (used[v]) dn[remap[v]] = n[v];
                    dst.normals = dn;
                    break;
                case UVertexAttribute.Tangent:
                    var t = src.tangents;
                    var dt = new Vector4[dst.vertexCount];
                    for (int v = 0; v < vcnt; v++) if (used[v]) dt[remap[v]] = t[v];
                    dst.tangents = dt;
                    break;
                case UVertexAttribute.Color:
                    var c = src.colors32;
                    var dc = new Color32[dst.vertexCount];
                    for (int v = 0; v < vcnt; v++) if (used[v]) dc[remap[v]] = c[v];
                    dst.colors32 = dc;
                    break;
                default:
                    // TexCoord0~7 — 차원 수 유지
                    int channel = attr - UVertexAttribute.TexCoord0;
                    var uv = new List<Vector4>(vcnt);
                    src.GetUVs(channel, uv);
                    var duv = new Vector4[dst.vertexCount];
                    for (int v = 0; v < vcnt; v++) if (used[v]) duv[remap[v]] = uv[v];
                    dst.SetUVs(channel, new List<Vector4>(duv));
                    break;
            }
        }

        /// <summary>
        /// 추출로 생성된 렌더러를 제거하고 원본 메시를 복원한다.
        /// </summary>
        public static void CleanupExtractions(Transform root)
        {
            if (root == null)
                return;
            foreach (var m in root.GetComponentsInChildren<ExtractedSkirtMesh>(true))
            {
                if (m.sourceRenderer != null && m.originalMesh != null)
                {
                    var remain = m.sourceRenderer.sharedMesh;
                    m.sourceRenderer.sharedMesh = m.originalMesh;
                    if (remain != null && remain != m.originalMesh)
                        DestroyObject(remain);
                }
                var smr = m.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                    DestroyObject(smr.sharedMesh);
                DestroyObject(m.gameObject);
            }
        }

        static void DestroyObject(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        /// <summary>
        /// 스커트 MeshCloth를 생성한다. 스커트 버텍스가 있는 렌더러만 등록한다.
        /// </summary>
        public static Result Create(
            Transform parent,
            SkinnedMeshRenderer[] renderers,
            Dictionary<Transform, int> skirtBoneDepths,
            List<ColliderComponent> colliders,
            float torso,
            float skirtWeightThreshold,
            int fixedChainDepth)
        {
            var result = new Result
            {
                warnings = new List<string>(),
                extractedMeshes = new List<ExtractedSkirtMesh>()
            };
            result.skirtBoneCount = skirtBoneDepths.Count;

            var inputs = new List<(SkinnedMeshRenderer smr, VertexAttribute[] attrs)>();
            foreach (var smr in renderers)
            {
                if (smr == null || smr.sharedMesh == null)
                    continue;
                if (!smr.sharedMesh.isReadable)
                {
                    result.warnings.Add($"{smr.name}: 메시 Read/Write 비활성 — 임포터 설정에서 Read/Write Enabled 필요");
                    continue;
                }
                var attrs = BuildVertexAttributes(
                    smr, skirtBoneDepths, skirtWeightThreshold, fixedChainDepth,
                    out int fc, out int mc);
                if (fc + mc == 0)
                    continue;
                // MC2 렌더러 한도(65535) 초과 메시는 스커트 삼각형만 추출해 등록
                if (smr.sharedMesh.vertexCount > MaxRendererVertices)
                {
                    var extracted = ExtractSkirtRenderer(smr, attrs, out var remapped);
                    if (extracted == null)
                    {
                        result.warnings.Add($"{smr.name}: 스커트 삼각형 추출 실패");
                        continue;
                    }
                    result.extractedMeshes.Add(extracted);
                    inputs.Add((extracted.GetComponent<SkinnedMeshRenderer>(), remapped));
                }
                else
                {
                    inputs.Add((smr, attrs));
                }
                result.fixedVertexCount += fc;
                result.moveVertexCount += mc;
            }
            if (inputs.Count == 0)
                return result; // 스커트 버텍스 없음 — MeshCloth 불가
            result.rendererCount = inputs.Count;

            var go = new GameObject(CharacterPhysicsSetup.ClothNamePrefix + "Skirt");
            go.transform.SetParent(parent, false);
            var cloth = go.AddComponent<MagicaCloth>();
            var sdata = cloth.SerializeData;
            sdata.clothType = ClothProcess.ClothType.MeshCloth;
            foreach (var (smr, _) in inputs)
                sdata.sourceRenderers.Add(smr);

            var sdata2 = cloth.GetSerializeData2();
            sdata2.vertexAttributeList.Clear();
            foreach (var (_, attrs) in inputs)
                sdata2.vertexAttributeList.Add(attrs);

            // 프록시 메시 리덕션 — 체형 기준 ~3% (공식 예제 0.02m 내외)
            if (torso > 0f)
            {
                sdata.reductionSetting.simpleDistance = torso * 0.035f;
                sdata.reductionSetting.shapeDistance = torso * 0.04f;
            }

            // 공식 MeshCloth_Skirt 프리셋 + 런타임 예제 파라미터
            sdata.updateMode = ClothUpdateMode.AnimatorLinkage;
            sdata.normalAxis = ClothNormalAxis.Up;
            sdata.gravity = 5.0f;
            sdata.damping.SetValue(0.1f);
            sdata.radius.SetValue(0.02f);
            sdata.stablizationTimeAfterReset = 0.1f;

            var inertia = sdata.inertiaConstraint;
            inertia.movementInertiaSmoothing = 0.4f;
            inertia.movementSpeedLimit.SetValue(true, 5.0f);
            inertia.rotationSpeedLimit.SetValue(true, 720.0f);
            inertia.localMovementSpeedLimit.SetValue(true, 3.0f);
            inertia.localRotationSpeedLimit.SetValue(true, 360.0f);
            inertia.depthInertia = 0.7f;
            inertia.centrifualAcceleration = 0.1f;
            inertia.particleSpeedLimit.SetValue(true, 4.0f);
            inertia.teleportDistance = 0.5f;
            inertia.teleportRotation = 90.0f;

            sdata.tetherConstraint.distanceCompression = 0.3f;
            sdata.distanceConstraint.stiffness.SetValue(1.0f, 1.0f, 0.5f, true);
            sdata.triangleBendingConstraint.stiffness = 1.0f; // MeshCloth 사실상 필수

            sdata.angleRestorationConstraint.useAngleRestoration = true;
            sdata.angleRestorationConstraint.stiffness.SetValue(0.2f, 1.0f, 0.5f, true);
            sdata.angleRestorationConstraint.velocityAttenuation = 0.7f;

            sdata.angleLimitConstraint.useAngleLimit = true;
            sdata.angleLimitConstraint.limitAngle.SetValue(60.0f, 0.0f, 1.0f, true);
            sdata.angleLimitConstraint.stiffness = 1.0f;

            sdata.motionConstraint.useMaxDistance = false;
            sdata.motionConstraint.useBackstop = false;
            sdata.motionConstraint.stiffness = 1.0f;

            sdata.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Point;
            sdata.colliderCollisionConstraint.friction = 0.05f;
            sdata.colliderCollisionConstraint.colliderList.Clear();
            sdata.colliderCollisionConstraint.colliderList.AddRange(colliders);

            sdata.springConstraint.useSpring = true;
            sdata.springConstraint.springPower = 0.03f;
            sdata.springConstraint.limitDistance = 0.05f;

            if (result.fixedVertexCount == 0)
                result.warnings.Add("고정 버텍스 없음 — 스커트가 몸에서 떨어질 수 있음. fixedChainDepth 상향 검토");

            result.cloth = cloth;
            return result;
        }
    }
}
