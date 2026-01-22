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

            // [FIX 1] 좌표계 변환으로 인해 뒤를 보는 현상 보정 (180도 회전)
            // MakeLeftHanded로 인해 Z축이 반전되었으므로, 다시 180도 돌려 앞을 보게 함
            rootObject.transform.rotation = UnityEngine.Quaternion.Euler(0, 180f, 0);
            Debug.Log("[RuntimeFBXImporter] 🔄 모델 방향 180도 보정 완료 (Back -> Front)");

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

            // [변경] Ghost Retargeting을 위해 Legacy Animation 컴포넌트 사용
            // Ghost가 '가방' 역할과 '재생기' 역할을 동시에 하도록 생산 즉시 부착
            UnityEngine.Animation animComp = rootObject.GetComponent<UnityEngine.Animation>();
            if (animComp == null) animComp = rootObject.AddComponent<UnityEngine.Animation>();

            // Animator는 구조적 필요에 의해 남겨두거나 삭제 가능하나, 
            // 리타겟팅 로직에서 Animator를 끄고 Animation을 쓰기로 했으므로 
            // 호환성을 위해 Animator 추가 코드는 유지하되, 리타겟터가 이를 제어함.
            // 단, 데이터 전달을 위해 Animation 컴포넌트가 필수.
            // Animator animator = rootObject.AddComponent<Animator>(); (선택사항, 일단 유지)

            List<AnimationClip> clips = new List<AnimationClip>();

            foreach (var anim in scene.Animations)
            {
                AnimationClip clip = new AnimationClip();
                clip.name = anim.Name;
                if (string.IsNullOrEmpty(clip.name))
                {
                    clip.name = "Animation_" + scene.Animations.IndexOf(anim);
                }

                // [중요] 런타임 Legacy 재생을 위해 true 설정
                clip.legacy = true;
                clip.wrapMode = WrapMode.Loop; // 기본 반복 재생

                // 애니메이션 길이 보정
                double ticksPerSecond = anim.TicksPerSecond;
                if (ticksPerSecond <= 1.0)
                {
                    ticksPerSecond = 60.0;
                    Debug.LogWarning($"[RuntimeFBXImporter] ⚠️ TicksPerSecond 데이터 누락 (val={anim.TicksPerSecond}). 60 FPS로 강제합니다.");
                }
                float timeScale = 1.0f / (float)ticksPerSecond;

                foreach (var channel in anim.NodeAnimationChannels)
                {
                    if (!_nodeMap.TryGetValue(channel.NodeName, out Transform targetNode)) continue;

                    string relativePath = GetRelativePath(rootObject.transform, targetNode);

                    // 위치 애니메이션
                    if (channel.HasPositionKeys)
                    {
                        SetPositionCurves(clip, relativePath, channel.PositionKeys, timeScale);
                    }

                    // 회전 애니메이션
                    if (channel.HasRotationKeys)
                    {
                        SetRotationCurves(clip, relativePath, channel.RotationKeys, timeScale);
                    }

                    // 스케일 애니메이션
                    if (channel.HasScalingKeys)
                    {
                        SetScaleCurves(clip, relativePath, channel.ScalingKeys, timeScale);
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
                
                // [납품] 컴포넌트에 클립 등록
                animComp.AddClip(clip, clip.name);
                clips.Add(clip);
            }
            
            // 생성된 클립들을 필드에 저장
            _animationClips = clips.ToArray();
            
            // [FIX 3] 클립 강제 납품 및 로깅
            if (clips.Count > 0)
            {
                animComp.clip = clips[0]; // 기본 클립 설정
                // TimeScale은 루프 내에서 계산되지만, 여기서는 성공 사실을 강조
                Debug.Log($"[RuntimeFBXImporter] ✅ 클립 {clips.Count}개 생성 및 바인딩 완료. (TimeScale 적용됨, Legacy={clips[0].legacy})");
            }
            else
            {
                Debug.LogWarning("[RuntimeFBXImporter] ⚠️ 생성된 애니메이션 클립이 없습니다.");
            }
        }

        private void SetPositionCurves(AnimationClip clip, string relativePath, List<VectorKey> positionKeys, float timeScale)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            foreach (var key in positionKeys)
            {
                float time = (float)key.Time * timeScale;
                curveX.AddKey(time, key.Value.X);
                curveY.AddKey(time, key.Value.Y);
                curveZ.AddKey(time, key.Value.Z);
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
