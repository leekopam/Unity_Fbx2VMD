using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using Assimp;

namespace Fbx2Vmd.FBXImporter
{
    public sealed class FBXImportException : Exception
    {
        public FBXImportException(string message)
            : base(message)
        {
        }

        public FBXImportException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 런타임에서 Assimp 라이브러리를 사용하여 FBX 파일을 임포트하는 서비스
    /// </summary>
    public class AssimpFBXImporter
    {
        #region 상수
        private const int MAX_BONE_WEIGHTS_PER_VERTEX = 4;
        private const int VERTEX_INDEX_FORMAT_THRESHOLD = 65535;
        private const float FBX_TO_UNITY_UNIT_SCALE = 0.01f;

        #endregion

        #region Private 필드
        // 노드 이름으로 Transform을 찾기 위한 맵 (본 할당용)
        private Dictionary<string, Transform> _nodeMap = new Dictionary<string, Transform>();
        
        private string _sourceDirectory = string.Empty;

        // 생성된 AnimationClip 저장 (외부 접근용)
        private AnimationClip[] _animationClips;
        #endregion

        public bool ImportScaleCurves { get; set; } = false;
        public bool ImportNonRootPositionCurves { get; set; } = false;

        public sealed class AnimationInspectionReport
        {
            public bool FileReadable;
            public bool ImportSucceeded;
            public string ErrorMessage = "";
            public int AnimationCount;
            public int NodeAnimationChannelCount;
            public int PositionKeyCount;
            public int RotationKeyCount;
            public int ScaleKeyCount;
            public string AnimationNames = "";
            public string AnimationLengthsSeconds = "";
            public float MaxAnimationLengthSeconds;
        }

        public static AnimationInspectionReport InspectAnimationFile(string path)
        {
            var report = new AnimationInspectionReport
            {
                FileReadable = !string.IsNullOrEmpty(path) && File.Exists(path)
            };

            if (!report.FileReadable)
            {
                report.ErrorMessage = $"FBX file not found: {path}";
                return report;
            }

            if (!AssimpLibraryLoader.IsLoaded)
            {
                AssimpLibraryLoader.LoadLibrary();
            }

            try
            {
                using (AssimpContext importer = new AssimpContext())
                {
                    importer.SetConfig(new Assimp.Configs.FBXPreservePivotsConfig(false));

                    Scene scene = importer.ImportFile(path, BuildAssimpPostProcessSteps());
                    if (scene == null)
                    {
                        report.ErrorMessage = "Assimp returned a null scene.";
                        return report;
                    }

                    report.ImportSucceeded = true;
                    report.AnimationCount = scene.AnimationCount;

                    var names = new List<string>();
                    var lengths = new List<string>();
                    foreach (var animation in scene.Animations)
                    {
                        string animationName = string.IsNullOrWhiteSpace(animation.Name)
                            ? $"Animation_{names.Count}"
                            : animation.Name;
                        names.Add(animationName);

                        float duration = CalculateAnimationDurationSeconds(animation);
                        report.MaxAnimationLengthSeconds = Mathf.Max(report.MaxAnimationLengthSeconds, duration);
                        lengths.Add(duration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

                        report.NodeAnimationChannelCount += animation.NodeAnimationChannelCount;
                        foreach (var channel in animation.NodeAnimationChannels)
                        {
                            report.PositionKeyCount += channel.PositionKeyCount;
                            report.RotationKeyCount += channel.RotationKeyCount;
                            report.ScaleKeyCount += channel.ScalingKeyCount;
                        }
                    }

                    report.AnimationNames = string.Join("|", names);
                    report.AnimationLengthsSeconds = string.Join("|", lengths);
                    return report;
                }
            }
            catch (System.Exception e)
            {
                report.ErrorMessage = e.Message.Replace('\r', ' ').Replace('\n', ' ');
                return report;
            }
        }

        #region FBX 임포트
        public async Task<GameObject> ImportAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"[FBXImport] FBX 파일을 찾을 수 없음. 경로={path}");
                return null;
            }

            // Assimp 네이티브 라이브러리 미리 로드
            if (!AssimpLibraryLoader.IsLoaded)
            {
                AssimpLibraryLoader.LoadLibrary();
            }

            // 백그라운드 스레드에서 Assimp 임포트 실행
            Scene scene = await Task.Run(() => ImportWithAssimp(path));

            if (scene == null)
            {
                Debug.LogError("[FBXImport] FBX 임포트 실패함.");
                return null;
            }

            // 메인 스레드에서 GameObject 생성
            GameObject rootObject = new GameObject(Path.GetFileNameWithoutExtension(path));

            _nodeMap.Clear();
            _sourceDirectory = ResolveSourceDirectory(path);
            BuildHierarchy(scene.RootNode, rootObject.transform, scene);
            ProcessMeshes(scene.RootNode, scene);
            ProcessAnimations(scene, rootObject);

            ApplyRuntimeRootTransform(rootObject);

            return rootObject;
        }

        /// <summary>
        /// 생성된 AnimationClip 배열 반환
        /// </summary>
        public AnimationClip[] GetAnimationClips()
        {
            return _animationClips ?? new AnimationClip[0];
        }

#if UNITY_EDITOR
        public GameObject ImportSynchronouslyForEditorDiagnostics(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"[FBXImport] FBX 파일을 찾을 수 없음. 경로={path}");
                return null;
            }

            if (!AssimpLibraryLoader.IsLoaded)
            {
                AssimpLibraryLoader.LoadLibrary();
            }

            Scene scene = ImportWithAssimp(path);
            if (scene == null)
            {
                Debug.LogError("[FBXImport] FBX 임포트 실패함.");
                return null;
            }

            GameObject rootObject = new GameObject(Path.GetFileNameWithoutExtension(path));

            _nodeMap.Clear();
            _sourceDirectory = ResolveSourceDirectory(path);
            BuildHierarchy(scene.RootNode, rootObject.transform, scene);
            ProcessMeshes(scene.RootNode, scene);
            ProcessAnimations(scene, rootObject);
            ApplyRuntimeRootTransform(rootObject);

            return rootObject;
        }
#endif
        #endregion

        #region Assimp 초기화
        private Scene ImportWithAssimp(string path)
        {
            AssimpContext importer = new AssimpContext();

            // FBX 피벗 보존 설정 (본 정확도를 위해)
            importer.SetConfig(new Assimp.Configs.FBXPreservePivotsConfig(false));

            PostProcessSteps steps = BuildAssimpPostProcessSteps();

            try
            {
                Scene scene = importer.ImportFile(path, steps);

                if (scene == null)
                {
                    throw new FBXImportException($"Assimp returned no scene for '{Path.GetFileName(path)}'.");
                }

                return scene;
            }
            catch (AssimpException exception)
            {
                throw new FBXImportException($"Assimp failed to import '{Path.GetFileName(path)}'.", exception);
            }
        }

        private static PostProcessSteps BuildAssimpPostProcessSteps()
        {
            return PostProcessSteps.Triangulate |
                   PostProcessSteps.FlipUVs |
                   PostProcessSteps.LimitBoneWeights |
                   PostProcessSteps.GenerateNormals |
                   PostProcessSteps.CalculateTangentSpace |
                   PostProcessSteps.MakeLeftHanded |
                   PostProcessSteps.FlipWindingOrder;
        }

        private static void ApplyRuntimeRootTransform(GameObject rootObject)
        {
            if (rootObject == null)
            {
                return;
            }

            rootObject.transform.localScale = UnityEngine.Vector3.one;

            // 좌표계 변환으로 인해 뒤를 보는 현상 보정 (180도 회전)
            // MakeLeftHanded로 인해 Z축이 반전되었으므로, 다시 180도 돌려 앞을 보게 함
            rootObject.transform.rotation = UnityEngine.Quaternion.Euler(0, 180f, 0);
        }

#if UNITY_EDITOR
        public static PostProcessSteps BuildAssimpPostProcessStepsForEditorDiagnostics()
        {
            return BuildAssimpPostProcessSteps();
        }
#endif

        private static float CalculateAnimationDurationSeconds(Assimp.Animation animation)
        {
            if (animation == null)
            {
                return 0f;
            }

            double ticksPerSecond = animation.TicksPerSecond;
            if (ticksPerSecond <= 1.0)
            {
                ticksPerSecond = 60.0;
            }

            if (ticksPerSecond <= 0.0)
            {
                return 0f;
            }

            double duration = animation.DurationInTicks / ticksPerSecond;
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0.0)
            {
                return 0f;
            }

            return (float)duration;
        }
        #endregion

        #region 노드 처리
        private void BuildHierarchy(Node node, Transform parent, Scene scene)
        {
            GameObject go = new GameObject(node.Name);
            go.transform.SetParent(parent, false);

            _nodeMap[node.Name] = go.transform;

            // Assimp Transform을 Unity Transform으로 변환 (분해)
            Assimp.Vector3D aPos, aScale;
            Assimp.Quaternion aRot;
            node.Transform.Decompose(out aScale, out aRot, out aPos);

            go.transform.localPosition = new UnityEngine.Vector3(aPos.X, aPos.Y, aPos.Z) * FBX_TO_UNITY_UNIT_SCALE;
            go.transform.localRotation = new UnityEngine.Quaternion(aRot.X, aRot.Y, aRot.Z, aRot.W);
            go.transform.localScale = new UnityEngine.Vector3(aScale.X, aScale.Y, aScale.Z);

            if (node.HasChildren)
            {
                foreach (Node child in node.Children)
                {
                    BuildHierarchy(child, go.transform, scene);
                }
            }
        }
        #endregion

        #region 메시 처리
        private void ProcessMeshes(Node node, Scene scene)
        {
            if (node.HasMeshes)
            {
                if (_nodeMap.TryGetValue(node.Name, out Transform t))
                {
                    foreach (int meshIndex in node.MeshIndices)
                    {
                        Assimp.Mesh asmMesh = scene.Meshes[meshIndex];
                        CreateMesh(t.gameObject, asmMesh, scene);
                    }
                }
            }

            if (node.HasChildren)
            {
                foreach (Node child in node.Children)
                {
                    ProcessMeshes(child, scene);
                }
            }
        }

        private void CreateMesh(GameObject go, Assimp.Mesh asmMesh, Scene scene)
        {
            GameObject meshObject = ResolveMeshObject(go, asmMesh);
            UnityEngine.Mesh unityMesh = new UnityEngine.Mesh();
            unityMesh.name = asmMesh.Name;

            // 버텍스 인덱스 포맷 설정
            if (asmMesh.VertexCount > VERTEX_INDEX_FORMAT_THRESHOLD)
            {
                unityMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            // 버텍스
            List<UnityEngine.Vector3> vertices = new List<UnityEngine.Vector3>();
            foreach (var v in asmMesh.Vertices)
            {
                vertices.Add(new UnityEngine.Vector3(v.X, v.Y, v.Z) * FBX_TO_UNITY_UNIT_SCALE);
            }
            unityMesh.SetVertices(vertices);

            // 노멀
            if (asmMesh.HasNormals)
            {
                List<UnityEngine.Vector3> normals = new List<UnityEngine.Vector3>();
                foreach (var n in asmMesh.Normals)
                {
                    normals.Add(new UnityEngine.Vector3(n.X, n.Y, n.Z));
                }
                unityMesh.SetNormals(normals);
            }

            // UV
            if (asmMesh.HasTextureCoords(0))
            {
                List<UnityEngine.Vector2> uvs = new List<UnityEngine.Vector2>();
                foreach (var uv in asmMesh.TextureCoordinateChannels[0])
                {
                    uvs.Add(new UnityEngine.Vector2(uv.X, uv.Y));
                }
                unityMesh.SetUVs(0, uvs);
            }

            // 삼각형 인덱스
            List<int> indices = new List<int>();
            foreach (var face in asmMesh.Faces)
            {
                if (face.IndexCount == 3)
                {
                    indices.Add(face.Indices[0]);
                    indices.Add(face.Indices[1]);
                    indices.Add(face.Indices[2]);
                }
            }
            unityMesh.SetTriangles(indices, 0);

            // 본이 있으면 SkinnedMeshRenderer, 없으면 일반 MeshRenderer
            if (asmMesh.HasBones)
            {
                SetupSkinnedMesh(meshObject, unityMesh, asmMesh, vertices.Count);
            }
            else
            {
                SetupStaticMesh(meshObject, unityMesh);
            }

            AssimpMaterialFactory.AssignRuntimeMaterial(meshObject, asmMesh, scene, _sourceDirectory);
        }

        private static GameObject ResolveMeshObject(GameObject nodeObject, Assimp.Mesh asmMesh)
        {
            if (nodeObject == null || nodeObject.GetComponent<Renderer>() == null)
            {
                return nodeObject;
            }

            string meshName = string.IsNullOrWhiteSpace(asmMesh?.Name)
                ? "ImportedMesh"
                : asmMesh.Name;
            var meshObject = new GameObject(meshName);
            meshObject.transform.SetParent(nodeObject.transform, false);
            return meshObject;
        }

        private void SetupSkinnedMesh(GameObject go, UnityEngine.Mesh unityMesh, Assimp.Mesh asmMesh, int vertexCount)
        {
            SkinnedMeshRenderer smr = go.AddComponent<SkinnedMeshRenderer>();

            BoneWeight[] weights = new BoneWeight[vertexCount];
            List<Transform> bones = new List<Transform>();
            List<UnityEngine.Matrix4x4> bindPoses = new List<UnityEngine.Matrix4x4>();
            int[] weightCount = new int[vertexCount];

            foreach (var bone in asmMesh.Bones)
            {
                string boneName = bone.Name;
                if (!_nodeMap.TryGetValue(boneName, out Transform boneTrans))
                {
                    Debug.LogWarning($"[FBXImport] 계층 구조에서 본을 찾을 수 없음. 본={boneName}");
                    continue;
                }

                int boneIndex = bones.IndexOf(boneTrans);
                if (boneIndex == -1)
                {
                    boneIndex = bones.Count;
                    bones.Add(boneTrans);
                    bindPoses.Add(ToUnityMatrix(bone.OffsetMatrix));
                }

                foreach (var weight in bone.VertexWeights)
                {
                    int vIndex = weight.VertexID;
                    float val = weight.Weight;
                    if (vIndex >= weights.Length) continue;

                    AddBoneWeight(ref weights[vIndex], ref weightCount[vIndex], boneIndex, val);
                }
            }

            NormalizeBoneWeights(weights);
            unityMesh.boneWeights = weights;
            unityMesh.bindposes = bindPoses.ToArray();
            unityMesh.RecalculateBounds();

            smr.sharedMesh = unityMesh;
            smr.bones = bones.ToArray();

            // 루트 본 설정 (보통 첫 번째 본)
            if (bones.Count > 0)
            {
                smr.rootBone = bones[0];
            }
        }

        private void SetupStaticMesh(GameObject go, UnityEngine.Mesh unityMesh)
        {
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = unityMesh;
            unityMesh.RecalculateBounds();
        }



        private static string ResolveSourceDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
            }
            catch (System.Exception)
            {
                return string.Empty;
            }
        }

        private void AddBoneWeight(ref BoneWeight bw, ref int count, int boneIndex, float weight)
        {
            if (weight <= 0) return;

            // Assimp LimitBoneWeights 단계에서 최대 4개로 제한하지만 구조체를 올바르게 채워야 함
            if (count == 0) { bw.boneIndex0 = boneIndex; bw.weight0 = weight; }
            else if (count == 1) { bw.boneIndex1 = boneIndex; bw.weight1 = weight; }
            else if (count == 2) { bw.boneIndex2 = boneIndex; bw.weight2 = weight; }
            else if (count == 3) { bw.boneIndex3 = boneIndex; bw.weight3 = weight; }
            count++;
        }

        private static void NormalizeBoneWeights(BoneWeight[] weights)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight weight = weights[i];
                float total = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                if (total <= 0f)
                {
                    continue;
                }

                weight.weight0 /= total;
                weight.weight1 /= total;
                weight.weight2 /= total;
                weight.weight3 /= total;
                weights[i] = weight;
            }
        }
        #endregion

        #region 애니메이션 처리
        private void ProcessAnimations(Scene scene, GameObject rootObject)
        {
            if (!scene.HasAnimations)
            {
                _animationClips = new AnimationClip[0];
                return;
            }

            // Ghost Retargeting을 위해 Legacy Animation 컴포넌트 사용
            // Ghost가 '가방' 역할과 '재생기' 역할을 동시에 하도록 생산 즉시 부착
            UnityEngine.Animation animComp = rootObject.GetComponent<UnityEngine.Animation>();
            if (animComp == null) animComp = rootObject.AddComponent<UnityEngine.Animation>();

            List<AnimationClip> clips = new List<AnimationClip>();

            foreach (var anim in scene.Animations)
            {
                AnimationClip clip = new AnimationClip();
                clip.name = anim.Name;
                if (string.IsNullOrEmpty(clip.name))
                {
                    clip.name = "Animation_" + scene.Animations.IndexOf(anim);
                }

                // 런타임 Legacy 재생을 위해 true 설정
                clip.legacy = true;
                clip.wrapMode = WrapMode.Loop; // 기본 반복 재생

                // FBX tick → Unity seconds 변환
                double ticksPerSecond = anim.TicksPerSecond;
                if (ticksPerSecond <= 1.0)
                {
                    ticksPerSecond = 60.0;
                    Debug.LogWarning($"[FBXImport] TicksPerSecond 데이터가 없어 기본값 60 FPS 사용함. 값={anim.TicksPerSecond}");
                }
                float timeScale = 1.0f / (float)ticksPerSecond;

                foreach (var channel in anim.NodeAnimationChannels)
                {
                    if (!_nodeMap.TryGetValue(channel.NodeName, out Transform targetNode)) continue;

                    string relativePath = GetRelativePath(rootObject.transform, targetNode);
                    // 위치 애니메이션
                    if (channel.HasPositionKeys)
                    {
                        if (ImportNonRootPositionCurves || ShouldImportPositionCurves(relativePath, targetNode))
                        {
                            SetPositionCurves(clip, relativePath, channel.PositionKeys, timeScale);
                        }
                    }
                    // 회전 애니메이션
                    if (channel.HasRotationKeys)
                    {
                        SetRotationCurves(clip, relativePath, channel.RotationKeys, timeScale);
                    }
                    // 스케일 애니메이션
                    if (channel.HasScalingKeys)
                    {
                        if (ImportScaleCurves)
                        {
                            SetScaleCurves(clip, relativePath, channel.ScalingKeys, timeScale);
                        }
                    }
                }

                // 애니메이션 길이 보정
                double duration = anim.DurationInTicks / anim.TicksPerSecond;
                if (duration > 600)
                {
                    if (anim.TicksPerSecond == 0 || anim.TicksPerSecond == 1)
                        duration = anim.DurationInTicks / 60.0;
                }
                clip.frameRate = 60;

                // 컴포넌트에 클립 등록
                animComp.AddClip(clip, clip.name);
                clips.Add(clip);
            }

            // 생성된 클립들을 필드에 저장
            _animationClips = clips.ToArray();

            // 클립 강제 납품 및 로깅
            if (clips.Count > 0)
            {
                animComp.clip = clips[0]; // 기본 클립 설정
                // TimeScale은 루프 내에서 계산되지만, 여기서는 성공 사실을 강조
            if (clips.Count > 0)
            {
                animComp.clip = clips[0]; // 기본 클립 설정
                // TimeScale은 루프 내에서 계산되지만, 여기서는 성공 사실을 강조
                Debug.Log($"[FBXImport] 애니메이션 클립 생성됨. 개수={clips.Count}");
            }
            }
            else
            {
                Debug.LogWarning("[FBXImport] 애니메이션 클립이 생성되지 않음.");
            }
        }

        private void SetPositionCurves(
            AnimationClip clip, string relativePath, List<VectorKey> positionKeys, float timeScale)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            foreach (var key in positionKeys)
            {
                float time = (float)key.Time * timeScale;
                curveX.AddKey(time, key.Value.X * FBX_TO_UNITY_UNIT_SCALE);
                curveY.AddKey(time, key.Value.Y * FBX_TO_UNITY_UNIT_SCALE);
                curveZ.AddKey(time, key.Value.Z * FBX_TO_UNITY_UNIT_SCALE);
            }

            clip.SetCurve(relativePath, typeof(Transform), "localPosition.x", curveX);
            clip.SetCurve(relativePath, typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve(relativePath, typeof(Transform), "localPosition.z", curveZ);
        }

        private static bool ShouldImportPositionCurves(string relativePath, Transform targetNode)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return true;
            }

            string nodeName = targetNode != null ? targetNode.name : Path.GetFileName(relativePath);
            if (string.IsNullOrEmpty(nodeName))
            {
                return false;
            }

            string normalizedName = nodeName.Replace(" ", "").Replace("_", "").Replace(":", "").ToLowerInvariant();
            return normalizedName.Contains("root")
                || normalizedName.Contains("hips")
                || normalizedName.Contains("pelvis")
                || normalizedName.Contains("center")
                || normalizedName.Contains("groove");
        }

        private void SetRotationCurves(AnimationClip clip, string relativePath, List<QuaternionKey> rotationKeys, float timeScale)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            var curveW = new AnimationCurve();

            foreach (var key in rotationKeys)
            {
                float time = (float)key.Time * timeScale;
                curveX.AddKey(time, key.Value.X);
                curveY.AddKey(time, key.Value.Y);
                curveZ.AddKey(time, key.Value.Z);
                curveW.AddKey(time, key.Value.W);
            }

            clip.SetCurve(relativePath, typeof(Transform), "localRotation.x", curveX);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.y", curveY);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.z", curveZ);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.w", curveW);
        }

        private void SetScaleCurves(AnimationClip clip, string relativePath, List<VectorKey> scaleKeys, float timeScale)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            foreach (var key in scaleKeys)
            {
                float time = (float)key.Time * timeScale;
                curveX.AddKey(time, key.Value.X);
                curveY.AddKey(time, key.Value.Y);
                curveZ.AddKey(time, key.Value.Z);
            }

            clip.SetCurve(relativePath, typeof(Transform), "localScale.x", curveX);
            clip.SetCurve(relativePath, typeof(Transform), "localScale.y", curveY);
            clip.SetCurve(relativePath, typeof(Transform), "localScale.z", curveZ);
        }
        #endregion

        #region 유틸리티 메서드
        private UnityEngine.Matrix4x4 ToUnityMatrix(Assimp.Matrix4x4 m)
        {
            UnityEngine.Matrix4x4 mat = new UnityEngine.Matrix4x4();
            mat.m00 = m.A1; mat.m01 = m.A2; mat.m02 = m.A3; mat.m03 = m.A4;
            mat.m10 = m.B1; mat.m11 = m.B2; mat.m12 = m.B3; mat.m13 = m.B4;
            mat.m20 = m.C1; mat.m21 = m.C2; mat.m22 = m.C3; mat.m23 = m.C4;
            mat.m30 = m.D1; mat.m31 = m.D2; mat.m32 = m.D3; mat.m33 = m.D4;
            mat.m03 *= FBX_TO_UNITY_UNIT_SCALE;
            mat.m13 *= FBX_TO_UNITY_UNIT_SCALE;
            mat.m23 *= FBX_TO_UNITY_UNIT_SCALE;
            return mat;
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return "";

            string path = target.name;
            while (target.parent != null && target.parent != root)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }
        #endregion
    }

    #region Assimp 라이브러리 로더
    /// <summary>
    /// 네이티브 DLL을 수동으로 로드하는 헬퍼 클래스
    /// </summary>
    public static class AssimpLibraryLoader
    {
        #region 상수
        private const string ASSIMP_DLL_NAME = "assimp.dll";
        private const string ASSIMP_PLUGIN_FOLDER = "Assimp-net";
        #endregion

        #region Public 필드
        public static bool IsLoaded = false;
        #endregion

        #region DLL Import
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern System.IntPtr LoadLibrary(string lpFileName);
        #endregion

        #region 공개 API
        public static void LoadLibrary()
        {
            if (IsLoaded) return;

            // 빌드 환경 및 에디터 환경을 모두 고려한 검색 경로 목록
            string[] possiblePaths = new string[]
            {
                // 에디터 기본 경로 (Assets/Plugins/Assimp-net/assimp.dll)
                Path.Combine(Application.dataPath, "Plugins", ASSIMP_PLUGIN_FOLDER, ASSIMP_DLL_NAME),

                // 빌드: 실행 파일 옆 Plugins 폴더
                Path.Combine(Application.dataPath, "Plugins", ASSIMP_DLL_NAME),

                // 빌드: x86_64 서브폴더
                Path.Combine(Application.dataPath, "Plugins", "x86_64", ASSIMP_DLL_NAME),

                // 빌드: Assimp-net 서브폴더 보존 시
                Path.Combine(Application.dataPath, "Plugins", ASSIMP_PLUGIN_FOLDER, ASSIMP_DLL_NAME)
            };

            string validPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    validPath = path;
                    break;
                }
            }

            if (validPath == null)
            {
                Debug.LogError($"[FBXImport] assimp.dll을 찾을 수 없음. 검색 경로:\n{string.Join("\n", possiblePaths)}");
                return;
            }

            Debug.Log($"[FBXImport] 네이티브 라이브러리 찾음. 경로={validPath}");
            System.IntPtr handle = LoadLibrary(validPath);

            if (handle == System.IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.LogError($"[FBXImport] 네이티브 라이브러리 불러오기 실패함. 오류 코드={errorCode}, 경로={validPath}");
            }
            else
            {
                Debug.Log($"[FBXImport] 네이티브 라이브러리 불러오기 완료됨. 핸들={handle}");
                IsLoaded = true;
            }
        }
        #endregion
    }
    #endregion

}
