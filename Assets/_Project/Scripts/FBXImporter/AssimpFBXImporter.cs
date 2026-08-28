using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Assimp;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 런타임에서 Assimp 라이브러리를 사용하여 FBX 파일을 임포트하는 서비스
    /// </summary>
    public class AssimpFBXImporter
    {
        #region 상수
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
            return AssimpAnimationInspector.Inspect(path, BuildAssimpPostProcessSteps());
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
            return BuildImportedModel(path, scene);
        }

        private GameObject BuildImportedModel(string path, Scene scene)
        {
            GameObject rootObject = new GameObject(Path.GetFileNameWithoutExtension(path));

            _nodeMap.Clear();
            _sourceDirectory = ResolveSourceDirectory(path);
            BuildHierarchy(scene.RootNode, rootObject.transform);
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

            return BuildImportedModel(path, scene);
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

        #endregion

        #region 노드 처리
        private void BuildHierarchy(Node node, Transform parent)
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
                    BuildHierarchy(child, go.transform);
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

            List<UnityEngine.Vector3> vertices = RuntimeMeshGeometryCalculator.ConvertVertices(
                asmMesh.Vertices,
                FBX_TO_UNITY_UNIT_SCALE);
            unityMesh.SetVertices(vertices);

            if (asmMesh.HasNormals)
            {
                List<UnityEngine.Vector3> normals = RuntimeMeshGeometryCalculator.ConvertNormals(
                    asmMesh.Normals);
                unityMesh.SetNormals(normals);
            }

            if (asmMesh.HasTextureCoords(0))
            {
                List<UnityEngine.Vector2> uvs = RuntimeMeshGeometryCalculator.ConvertTextureCoordinates(
                    asmMesh.TextureCoordinateChannels[0]);
                unityMesh.SetUVs(0, uvs);
            }

            List<int> indices = RuntimeMeshGeometryCalculator.BuildTriangleIndices(asmMesh.Faces);
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

            AssimpRuntimeMaterialApplier.Apply(meshObject, asmMesh, scene, _sourceDirectory);
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
                    bindPoses.Add(RuntimeMeshGeometryCalculator.ConvertBindPoseMatrix(
                        bone.OffsetMatrix,
                        FBX_TO_UNITY_UNIT_SCALE));
                }

                foreach (var weight in bone.VertexWeights)
                {
                    int vIndex = weight.VertexID;
                    float val = weight.Weight;
                    if (vIndex >= weights.Length) continue;

                    RuntimeMeshBoneWeightCalculator.Add(
                        ref weights[vIndex],
                        ref weightCount[vIndex],
                        boneIndex,
                        val);
                }
            }

            RuntimeMeshBoneWeightCalculator.Normalize(weights);
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
            go.AddComponent<MeshRenderer>();
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
                clip.wrapMode = WrapMode.Loop;

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
                        string positionCurveNodeName = targetNode != null
                            ? targetNode.name
                            : Path.GetFileName(relativePath);
                        if (ImportNonRootPositionCurves ||
                            RuntimeAnimationPositionCurvePolicy.ShouldImport(
                                relativePath,
                                positionCurveNodeName))
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

                clip.frameRate = 60;

                // 컴포넌트에 클립 등록
                animComp.AddClip(clip, clip.name);
                clips.Add(clip);
            }

            _animationClips = clips.ToArray();

            if (clips.Count > 0)
            {
                animComp.clip = clips[0];
                Debug.Log($"[FBXImport] 애니메이션 클립 생성됨. 개수={clips.Count}");
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

}
