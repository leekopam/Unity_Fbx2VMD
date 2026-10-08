using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 표면 보정 캐시의 재사용 키를 계산함. 캐릭터·모션·메시·알고리즘 버전이
    /// 전부 일치할 때만 같은 키가 나오도록 해시 입력을 구성한다.
    /// </summary>
    internal static class NativeSkinningCorrectionCacheKey
    {
        // 보정 알고리즘·설정이 바뀌면 수동으로 올린다 — 설정은 코드 고정(static)이라
        // 이 상수 하나가 설정 변경 전부를 대표한다.
        internal const int AlgorithmVersion = 1;

        internal readonly struct RendererIdentity
        {
            internal RendererIdentity(
                string rendererPath,
                string meshAssetId,
                int vertexCount,
                string vertexHash)
            {
                RendererPath = rendererPath;
                MeshAssetId = meshAssetId;
                VertexCount = vertexCount;
                VertexHash = vertexHash;
            }

            internal string RendererPath { get; }
            internal string MeshAssetId { get; }
            internal int VertexCount { get; }
            internal string VertexHash { get; }
        }

#if UNITY_EDITOR
        internal static bool TryCompute(
            AnimationClip motionClip,
            string motionName,
            int frameCount,
            float frameRate,
            GameObject sourceModelAsset,
            Animator animator,
            NativeSkinningSurfaceContract[] contracts,
            out byte[] keyHash,
            out string errorMessage)
        {
            keyHash = null;
            errorMessage = string.Empty;
            if (animator == null || contracts == null || contracts.Length == 0)
            {
                errorMessage = "보정 캐시 키를 계산할 대상이 없습니다.";
                return false;
            }

            try
            {
                // 비영속 클립(런타임 생성 등)은 파일 해시를 잡을 수 없어 캐시 비활성.
                if (motionClip != null &&
                    string.IsNullOrEmpty(AssetDatabase.GetAssetPath(motionClip)))
                {
                    errorMessage =
                        "모션 클립이 에셋이 아니라 보정 캐시를 사용할 수 없습니다.";
                    return false;
                }

                var segments = new List<string>
                {
                    "v" + AlgorithmVersion,
                    Application.unityVersion,
                    "motion:" + (motionName ?? string.Empty),
                    "frames:" + frameCount,
                    "rate:" + frameRate.ToString("R"),
                    "clip:" + GetAssetIdentity(motionClip),
                    "clipFile:" + GetAssetFileHash(motionClip),
                    "sourceModel:" + GetAssetIdentity(sourceModelAsset),
                    // 가중치·바인드포즈만 바뀐 재임포트는 정점 수가 같아
                    // stale 히트가 되므로 소스 파일 내용 해시도 키에 넣는다.
                    "sourceModelFile:" + GetAssetFileHash(sourceModelAsset),
                    "avatar:" + GetAssetIdentity(animator.avatar)
                };

                // 렌더러 — 경로·메시 에셋·정점 내용. 경로 정렬로 순서를 고정한다.
                RendererIdentity[] identities = contracts
                    .Select(contract => contract.Renderer)
                    .Where(renderer => renderer != null)
                    .Distinct()
                    .Select(renderer => BuildIdentity(
                        animator,
                        renderer,
                        GetAssetIdentity(renderer.sharedMesh)))
                    .Where(identity => identity.HasValue)
                    .Select(identity => identity.Value)
                    .OrderBy(identity => identity.RendererPath,
                             StringComparer.Ordinal)
                    .ToArray();
                if (identities.Length == 0)
                {
                    errorMessage =
                        "보정 캐시 키를 계산할 Renderer가 없습니다.";
                    return false;
                }
                foreach (RendererIdentity identity in identities)
                {
                    segments.Add("renderer:" + identity.RendererPath);
                    segments.Add("mesh:" + identity.MeshAssetId);
                    segments.Add("verts:" + identity.VertexCount);
                    segments.Add("vhash:" + identity.VertexHash);
                }

                keyHash = ComputeKeyHash(segments);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"보정 캐시 키를 계산하지 못했습니다: {exception.Message}";
                return false;
            }
        }
#endif

        /// <summary>
        /// 빌드 환경용 캐시 키 — 에셋 식별자 대신 소스 FBX 파일 경로+내용 해시를
        /// 모션·소스 모델 식별자로 쓰고, 메시는 정점 내용 해시로 식별한다.
        /// </summary>
        internal static bool TryComputeRuntime(
            string sourceFilePath,
            string motionName,
            int frameCount,
            float frameRate,
            Animator animator,
            NativeSkinningSurfaceContract[] contracts,
            out byte[] keyHash,
            out string errorMessage)
        {
            keyHash = null;
            errorMessage = string.Empty;
            if (string.IsNullOrEmpty(sourceFilePath) ||
                animator == null ||
                contracts == null ||
                contracts.Length == 0)
            {
                errorMessage = "보정 캐시 키를 계산할 대상이 없습니다.";
                return false;
            }

            try
            {
                string sourceFileHash = ComputeFileHash(sourceFilePath);
                if (string.IsNullOrEmpty(sourceFileHash))
                {
                    errorMessage =
                        "소스 FBX 파일을 읽을 수 없어 보정 캐시를 사용할 수 없습니다.";
                    return false;
                }

                var segments = new List<string>
                {
                    "v" + AlgorithmVersion,
                    Application.unityVersion,
                    "runtime",
                    "motion:" + (motionName ?? string.Empty),
                    "frames:" + frameCount,
                    "rate:" + frameRate.ToString("R"),
                    "sourceFile:" + Path.GetFullPath(sourceFilePath),
                    "sourceFileHash:" + sourceFileHash,
                    "avatar:" + (animator.avatar == null
                        ? string.Empty
                        : animator.avatar.name)
                };

                RendererIdentity[] identities = contracts
                    .Select(contract => contract.Renderer)
                    .Where(renderer => renderer != null)
                    .Distinct()
                    .Select(renderer => BuildIdentity(
                        animator,
                        renderer,
                        GetRuntimeMeshIdentity(renderer.sharedMesh)))
                    .Where(identity => identity.HasValue)
                    .Select(identity => identity.Value)
                    .OrderBy(identity => identity.RendererPath,
                             StringComparer.Ordinal)
                    .ToArray();
                if (identities.Length == 0)
                {
                    errorMessage =
                        "보정 캐시 키를 계산할 Renderer가 없습니다.";
                    return false;
                }
                foreach (RendererIdentity identity in identities)
                {
                    segments.Add("renderer:" + identity.RendererPath);
                    segments.Add("mesh:" + identity.MeshAssetId);
                    segments.Add("verts:" + identity.VertexCount);
                    segments.Add("vhash:" + identity.VertexHash);
                }

                keyHash = ComputeKeyHash(segments);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"보정 캐시 키를 계산하지 못했습니다: {exception.Message}";
                return false;
            }
        }

        private static byte[] ComputeKeyHash(List<string> segments)
        {
            using (var hash = SHA256.Create())
            {
                byte[] source = Encoding.UTF8.GetBytes(
                    string.Join("\n", segments));
                byte[] full = hash.ComputeHash(source);
                var key = new byte[
                    NativeSkinningCorrectionCacheFileStore.KeyHashLength];
                Buffer.BlockCopy(full, 0, key, 0, key.Length);
                return key;
            }
        }

        // 정점 내용 해시가 곧 메시 실체 — 에셋 경로가 없는 런타임 메시도 식별된다.
        internal static string GetRuntimeMeshIdentity(Mesh mesh)
        {
            if (mesh == null)
            {
                return string.Empty;
            }
            return "vhash:" + ComputeVertexHash(mesh);
        }

        // Animator 루트 기준 상대 경로 — 씬 렌더러와 캐시 항목의 재바인딩 키.
        internal static string GetRendererPath(
            Animator animator,
            SkinnedMeshRenderer renderer)
        {
            if (animator == null || renderer == null)
            {
                return string.Empty;
            }
            Transform root = animator.transform;
            Transform current = renderer.transform;
            var segments = new List<string>();
            while (current != null && current != root)
            {
                // 동명 sibling이 허용되므로 이름만으론 경로가 충돌한다 —
                // sibling 인덱스를 붙여 계층이 안정적인 동안 1:1 매칭을 보장한다.
                segments.Add(current.name + "#" + current.GetSiblingIndex());
                current = current.parent;
            }
            if (current != root)
            {
                segments.Add(root.name + "#" + root.GetSiblingIndex());
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static RendererIdentity? BuildIdentity(
            Animator animator,
            SkinnedMeshRenderer renderer,
            string meshAssetId)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                return null;
            }
            return new RendererIdentity(
                GetRendererPath(animator, renderer),
                meshAssetId,
                mesh.vertexCount,
                ComputeVertexHash(mesh));
        }

        // 파일 내용 해시 — 소스 FBX 변경 감지의 공용 수단.
        internal static string ComputeFileHash(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return string.Empty;
            }
            using (var hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(fullPath))
            {
                return Convert.ToBase64String(hash.ComputeHash(stream)) +
                    ":" + stream.Length;
            }
        }

#if UNITY_EDITOR
        internal static string GetAssetIdentity(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                // 비영속 자산은 이름+내용 해시(정점 해시)가 실체를 대표한다.
                return "nonasset:" + asset.name;
            }
            return "guid:" + AssetDatabase.AssetPathToGUID(path) +
                ",path:" + path;
        }

        // 모션 FBX 같은 소스 파일은 내용 해시로 변경을 감지한다.
        private static string GetAssetFileHash(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            return ComputeFileHash(Path.GetFullPath(path));
        }
#endif

        private static string ComputeVertexHash(Mesh mesh)
        {
            // 정점 인덱스 재배열·임포트 설정·토폴로지 변경을 감지하기 위해
            // 정점 위치와 삼각형 인덱스를 함께 해시한다.
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            using (var stream = new MemoryStream(
                       vertices.Length * 12 + triangles.Length * 4 + 8))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(vertices.Length);
                foreach (Vector3 vertex in vertices)
                {
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
                writer.Write(triangles.Length);
                foreach (int index in triangles)
                {
                    writer.Write(index);
                }
                writer.Flush();
                using (var hash = SHA256.Create())
                {
                    return Convert.ToBase64String(
                        hash.ComputeHash(stream.GetBuffer(), 0,
                            (int)stream.Length));
                }
            }
        }
    }
}
