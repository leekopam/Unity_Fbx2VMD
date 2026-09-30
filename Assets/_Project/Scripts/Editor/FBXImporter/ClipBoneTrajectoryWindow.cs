using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 임의의 AnimationClip을 Humanoid Animator에 프레임 단위로 적용해
    /// 본 궤적(월드·로컬 위치·회전)을 CSV로보내는 창임.
    /// 상체·하체 보정 데이터의 입력을 생산하며 보정 로직은 수정하지 않음.
    /// EditMode에서 AnimationMode 샘플링으로 동작해 재생 파이프라인이 필요 없음.
    /// </summary>
    public class ClipBoneTrajectoryWindow : EditorWindow
    {
        private enum BoneGroup { 상체, 하체, 전체 }

        private static readonly HumanBodyBones[] UpperBody =
        {
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        };

        private static readonly HumanBodyBones[] LowerBody =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
        };

        private GameObject _target;
        private AnimationClip _clip;
        private BoneGroup _group = BoneGroup.전체;
        private int _firstFrame;
        private int _lastFrame = -1; // -1이면 클립 끝까지
        private string _message = "";
        private bool _running;

        [MenuItem("Tools/FBXImporter/클립 본 궤적 추출")]
        private static void Open() =>
            GetWindow<ClipBoneTrajectoryWindow>(false, "클립 본 궤적 추출");

        private void OnGUI()
        {
            _target = (GameObject)EditorGUILayout.ObjectField(
                "대상 Humanoid", _target, typeof(GameObject), true);
            _clip = (AnimationClip)EditorGUILayout.ObjectField(
                "애니메이션 클립", _clip, typeof(AnimationClip), false);
            _group = (BoneGroup)EditorGUILayout.EnumPopup("본 범위", _group);
            EditorGUILayout.BeginHorizontal();
            _firstFrame = EditorGUILayout.IntField("시작 프레임", _firstFrame);
            _lastFrame = EditorGUILayout.IntField("끝 프레임(-1=끝까지)", _lastFrame);
            EditorGUILayout.EndHorizontal();

            string validation = Validate();
            if (validation != null)
            {
                EditorGUILayout.HelpBox(validation, MessageType.None);
            }
            using (new EditorGUI.DisabledScope(validation != null || _running))
            {
                if (GUILayout.Button(_running ? "추출 중…" : "CSV로 추출"))
                {
                    Extract();
                }
            }
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            }
        }

        private string Validate()
        {
            if (_target == null) return "대상 Humanoid 오브젝트를 선택하세요.";
            Animator animator = _target.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
                return "대상에 Humanoid Animator가 없습니다.";
            if (_clip == null) return "애니메이션 클립을 선택하세요.";
            if (_firstFrame < 0) return "시작 프레임이 0보다 작습니다.";
            return null;
        }

        private List<HumanBodyBones> SelectedBones()
        {
            var bones = new List<HumanBodyBones>();
            if (_group != BoneGroup.하체) bones.AddRange(UpperBody);
            if (_group != BoneGroup.상체) bones.AddRange(LowerBody);
            return bones;
        }

        private void Extract()
        {
            Animator animator = _target.GetComponent<Animator>();
            float frameRate = _clip.frameRate > 0f ? _clip.frameRate : 30f;
            int last = _lastFrame >= 0
                ? Mathf.Min(_lastFrame, Mathf.CeilToInt(_clip.length * frameRate))
                : Mathf.CeilToInt(_clip.length * frameRate);
            var transforms = new Dictionary<HumanBodyBones, Transform>();
            foreach (HumanBodyBones bone in SelectedBones())
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t != null) transforms[bone] = t;
            }
            if (transforms.Count == 0)
            {
                _message = "선택 범위에 매핑된 본이 없습니다.";
                return;
            }

            string path = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "vmd-analysis",
                $"{_clip.name}-clip-trajectory.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _running = true;
            bool sampling = false;
            try
            {
                AnimationMode.StartAnimationMode();
                sampling = true;
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine(
                        "frame,time_s,bone,world_x,world_y,world_z," +
                        "local_x,local_y,local_z,rot_x,rot_y,rot_z,rot_w");
                    for (int frame = _firstFrame; frame <= last; frame++)
                    {
                        float time = Mathf.Min(frame / frameRate, _clip.length);
                        AnimationMode.SampleAnimationClip(_target, _clip, time);
                        foreach (KeyValuePair<HumanBodyBones, Transform> pair in transforms)
                        {
                            Vector3 world = pair.Value.position;
                            Vector3 local = pair.Value.localPosition;
                            Quaternion rotation = pair.Value.localRotation;
                            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                "{0},{1:F6},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                                frame, time, pair.Key,
                                world.x, world.y, world.z,
                                local.x, local.y, local.z,
                                rotation.x, rotation.y, rotation.z, rotation.w));
                        }
                        EditorUtility.DisplayProgressBar("본 궤적 추출",
                            $"프레임 {frame}/{last}", (float)(frame - _firstFrame) / Math.Max(1, last - _firstFrame));
                    }
                }
                _message = $"궤적 저장: {path} ({last - _firstFrame + 1}프레임 × {transforms.Count}본)";
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception error)
            {
                _message = $"추출 실패: {error.Message}";
            }
            finally
            {
                if (sampling) AnimationMode.StopAnimationMode();
                EditorUtility.ClearProgressBar();
                _running = false;
            }
        }
    }
}
