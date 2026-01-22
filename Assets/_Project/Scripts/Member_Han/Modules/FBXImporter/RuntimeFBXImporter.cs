using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using Assimp;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// 런타임에서 Assimp 라이브러리를 사용하여 FBX 파일을 임포트하는 서비스
    /// </summary>
    public class RuntimeFBXImporter : IModelImporterService
    {
        #region 상수
        private const int MAX_BONE_WEIGHTS_PER_VERTEX = 4;
        private const int VERTEX_INDEX_FORMAT_THRESHOLD = 65535;
        #endregion

        #region Private 필드
        // 노드 이름으로 Transform을 찾기 위한 맵 (본 할당용)
        private Dictionary<string, Transform> _nodeMap = new Dictionary<string, Transform>();
        
        // 생성된 AnimationClip 저장 (외부 접근용)
        private AnimationClip[] _animationClips;
        #endregion

        #region IModelImporterService 구현
        public async Task<GameObject> ImportAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"파일을 찾을 수 없음: {path}");
                return null;
            }

            // Assimp 네이티브 라이브러리 미리 로드
            if (!AssimpLibraryLoader.IsLoaded)
            {
                AssimpLibraryLoader.LoadLibrary();
            }

            // 1. 백그라운드 스레드에서 Assimp 임포트 실행
            Debug.Log($"[RuntimeFBXImporter] Assimp 임포트 시작: {path}");
            Scene scene = await Task.Run(() => ImportWithAssimp(path));

            if (scene == null)
            {
                Debug.LogError("FBX 임포트 실패");
                return null;
            }

            // 2. 메인 스레드에서 GameObject 생성
            GameObject rootObject = new GameObject(Path.GetFileNameWithoutExtension(path));

            _nodeMap.Clear();
            BuildHierarchy(scene.RootNode, rootObject.transform, scene);
            ProcessMeshes(scene.RootNode, scene);
            ProcessAnimations(scene, rootObject);

            return rootObject;
        }
        
        /// <summary>
        /// 생성된 AnimationClip 배열 반환
        /// </summary>
        public AnimationClip[] GetAnimationClips()
        {
            return _animationClips ?? new AnimationClip[0];
        }
        #endregion

        #region Assimp 초기화
        private Scene ImportWithAssimp(string path)
        {
            AssimpContext importer = new AssimpContext();

            // FBX 피벗 보존 설정 (본 정확도를 위해)
            importer.SetConfig(new Assimp.Configs.FBXPreservePivotsConfig(false));

            PostProcessSteps steps = PostProcessSteps.Triangulate |
                                     PostProcessSteps.FlipUVs |
                                     PostProcessSteps.LimitBoneWeights |
                                     PostProcessSteps.GenerateNormals |
                                     PostProcessSteps.CalculateTangentSpace |
                                     PostProcessSteps.MakeLeftHanded |
                                     PostProcessSteps.FlipWindingOrder;

            try
            {
                Debug.Log("[RuntimeFBXImporter] importer.ImportFile 호출 중...");
                Scene scene = importer.ImportFile(path, steps);
                
                if (scene == null)
                {
                    Debug.LogError("[RuntimeFBXImporter] importer.ImportFile이 null을 반환함");
                }
                else
                {
                    Debug.Log($"[RuntimeFBXImporter] 임포트 성공. 메시 수: {scene.MeshCount}, 애니메이션 수: {scene.AnimationCount}");
                }
                
                return scene;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RuntimeFBXImporter] Assimp 예외: {e.Message}\n{e.StackTrace}");
                return null;
            }
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

            go.transform.localPosition = new UnityEngine.Vector3(aPos.X, aPos.Y, aPos.Z);
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
                vertices.Add(new UnityEngine.Vector3(v.X, v.Y, v.Z));
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
                SetupSkinnedMesh(go, unityMesh, asmMesh, vertices.Count);
            }
            else
            {
                SetupStaticMesh(go, unityMesh);
            }
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
                    Debug.LogWarning($"계층 구조에서 본을 찾을 수 없음: {boneName}");
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
        #endregion

        #region 애니메이션 처리
        private void ProcessAnimations(Scene scene, GameObject rootObject)
        {
            if (!scene.HasAnimations)
            {
                _animationClips = new AnimationClip[0];
                return;
            }

            // Ghost Retargeting을 위해 Animator 사용 (Legacy Animation 대신)
            Animator animator = rootObject.AddComponent<Animator>();
            List<AnimationClip> clips = new List<AnimationClip>();

            foreach (var anim in scene.Animations)
            {
                AnimationClip clip = new AnimationClip();
                clip.name = anim.Name;
                if (string.IsNullOrEmpty(clip.name))
                {
                    clip.name = "Animation_" + scene.Animations.IndexOf(anim);
                }

                // Animator 호환을 위해 Non-Legacy로 설정 (Ghost Retargeting용)
                clip.legacy = false;

                foreach (var channel in anim.NodeAnimationChannels)
                {
                    if (!_nodeMap.TryGetValue(channel.NodeName, out Transform targetNode)) continue;

                    string relativePath = GetRelativePath(rootObject.transform, targetNode);

                    // 위치 애니메이션
                    if (channel.HasPositionKeys)
                    {
                        SetPositionCurves(clip, relativePath, channel.PositionKeys);
                    }

                    // 회전 애니메이션
                    if (channel.HasRotationKeys)
                    {
                        SetRotationCurves(clip, relativePath, channel.RotationKeys);
                    }

                    // 스케일 애니메이션
                    if (channel.HasScalingKeys)
                    {
                        SetScaleCurves(clip, relativePath, channel.ScalingKeys);
                    }
                }

                // Animator는 AnimatorController를 통해 재생하므로 여기서는 클립만 저장
                // (FileManager에서 AnimatorOverrideController로 할당 예정)
                
                // WrapMode 설정: 한 번만 재생 (사용자 요구사항)
                clip.wrapMode = WrapMode.Once;
                // [NEW] Legacy Animation 사용을 위해 true 설정
                clip.legacy = true;
                
                // 애니메이션 길이 보정 (Assimp TicksPerSecond 오류 대응)
                double duration = anim.DurationInTicks / anim.TicksPerSecond;
                
                // 비정상적으로 긴 경우 보정 (10분 이상)
                if (duration > 600)
                {
                    Debug.LogWarning($"[RuntimeFBXImporter] ⚠️ 비정상적으로 긴 애니메이션 감지: {duration}초");
                    Debug.LogWarning($"  - DurationInTicks: {anim.DurationInTicks}");
                    Debug.LogWarning($"  - TicksPerSecond: {anim.TicksPerSecond}");
                    
                    // TicksPerSecond가 0 또는 1인 경우 60fps로 재계산
                    if (anim.TicksPerSecond == 0 || anim.TicksPerSecond == 1)
                    {
                        duration = anim.DurationInTicks / 60.0;
                        Debug.LogWarning($"  - 보정된 Duration: {duration}초 (60fps 기준)");
                    }
                }
                
                // frameRate 명시적 설정
                clip.frameRate = 60;
                
                // 생성된 클립을 리스트에 추가
                clips.Add(clip);
            }
            
            // 생성된 클립들을 필드에 저장
            _animationClips = clips.ToArray();
            Debug.Log($"[RuntimeFBXImporter] AnimationClip {_animationClips.Length}개 생성 완료");
        }

        private void SetPositionCurves(AnimationClip clip, string relativePath, List<VectorKey> positionKeys)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            foreach (var key in positionKeys)
            {
                curveX.AddKey((float)key.Time, key.Value.X);
                curveY.AddKey((float)key.Time, key.Value.Y);
                curveZ.AddKey((float)key.Time, key.Value.Z);
            }

            clip.SetCurve(relativePath, typeof(Transform), "localPosition.x", curveX);
            clip.SetCurve(relativePath, typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve(relativePath, typeof(Transform), "localPosition.z", curveZ);
        }

        private void SetRotationCurves(AnimationClip clip, string relativePath, List<QuaternionKey> rotationKeys)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            var curveW = new AnimationCurve();

            foreach (var key in rotationKeys)
            {
                curveX.AddKey((float)key.Time, key.Value.X);
                curveY.AddKey((float)key.Time, key.Value.Y);
                curveZ.AddKey((float)key.Time, key.Value.Z);
                curveW.AddKey((float)key.Time, key.Value.W);
            }

            clip.SetCurve(relativePath, typeof(Transform), "localRotation.x", curveX);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.y", curveY);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.z", curveZ);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.w", curveW);
        }

        private void SetScaleCurves(AnimationClip clip, string relativePath, List<VectorKey> scaleKeys)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            foreach (var key in scaleKeys)
            {
                curveX.AddKey((float)key.Time, key.Value.X);
                curveY.AddKey((float)key.Time, key.Value.Y);
                curveZ.AddKey((float)key.Time, key.Value.Z);
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

            string pluginsPath = Path.Combine(Application.dataPath, "Plugins", ASSIMP_PLUGIN_FOLDER, ASSIMP_DLL_NAME);

            if (!File.Exists(pluginsPath))
            {
                Debug.LogWarning($"[AssimpLibraryLoader] assimp.dll을 찾을 수 없음: {pluginsPath}. 일반 조회 시도 중.");
                return;
            }

            Debug.Log($"[AssimpLibraryLoader] 네이티브 라이브러리 로드 중: {pluginsPath}");
            System.IntPtr handle = LoadLibrary(pluginsPath);
            
            if (handle == System.IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.LogError($"[AssimpLibraryLoader] assimp.dll 로드 실패. 오류 코드: {errorCode}");
            }
            else
            {
                Debug.Log($"[AssimpLibraryLoader] assimp.dll 로드 성공 핸들: {handle}");
                IsLoaded = true;
            }
        }
        #endregion
    }
    #endregion
}
