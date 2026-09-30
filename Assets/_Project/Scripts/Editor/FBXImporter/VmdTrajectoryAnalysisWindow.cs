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
    /// VMD 파일을 읽어 본 궤적·IK 스위치·두 VMD 차이를 CSV로보내는 분석 창임.
    /// 하체·상체 보정 데이터 가공의 입력을 생산하고, 보정 로직 자체는 건드리지 않음.
    /// </summary>
    public class VmdTrajectoryAnalysisWindow : EditorWindow
    {
        private string _vmdPath = "";
        private string _comparePath = "";
        private VmdMotionData _motion;
        private string _summary = "";
        private string _message = "";
        private Vector2 _scroll;
        private List<string> _boneLines = new List<string>();

        [MenuItem("Tools/FBXImporter/VMD 궤적 분석")]
        private static void Open() =>
            GetWindow<VmdTrajectoryAnalysisWindow>(false, "VMD 궤적 분석");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VMD 파일", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _vmdPath = EditorGUILayout.TextField(_vmdPath);
            if (GUILayout.Button("선택", GUILayout.Width(60)))
            {
                string picked = EditorUtility.OpenFilePanel("VMD 선택", "", "vmd");
                if (!string.IsNullOrEmpty(picked)) _vmdPath = picked;
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_vmdPath)))
            {
                if (GUILayout.Button("읽기"))
                {
                    ReadMotion();
                }
            }
            if (_motion == null)
            {
                if (!string.IsNullOrEmpty(_message))
                {
                    EditorGUILayout.HelpBox(_message, MessageType.Warning);
                }
                return;
            }
            EditorGUILayout.LabelField(_summary, EditorStyles.wordWrappedLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(300));
            foreach (string line in _boneLines)
            {
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("보내기", EditorStyles.boldLabel);
            if (GUILayout.Button("본 궤적 CSV보내기"))
            {
                ExportBoneCsv();
            }
            if (GUILayout.Button("IK 스위치 타임라인 CSV보내기"))
            {
                ExportIkCsv();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("두 VMD 비교(동일 본·동일 프레임 차이)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _comparePath = EditorGUILayout.TextField(_comparePath);
            if (GUILayout.Button("선택", GUILayout.Width(60)))
            {
                string picked = EditorUtility.OpenFilePanel("비교 대상 VMD", "", "vmd");
                if (!string.IsNullOrEmpty(picked)) _comparePath = picked;
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_comparePath)))
            {
                if (GUILayout.Button("비교 리포트보내기"))
                {
                    ExportCompareReport();
                }
            }
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            }
        }

        private void ReadMotion()
        {
            try
            {
                _motion = VmdMotionReader.Read(_vmdPath);
                BuildSummary();
                _message = "";
            }
            catch (Exception error)
            {
                _motion = null;
                _message = $"VMD 읽기 실패: {error.Message}";
            }
        }

        private void BuildSummary()
        {
            uint maxFrame = 0;
            var counts = new Dictionary<string, int>();
            foreach (VmdBoneFrame frame in _motion.BoneFrames)
            {
                maxFrame = Math.Max(maxFrame, frame.FrameIndex);
                counts[frame.BoneName] = counts.TryGetValue(frame.BoneName, out int n) ? n + 1 : 1;
            }
            _summary =
                $"모델: {_motion.ModelName} · 본 프레임 {_motion.BoneFrames.Count} · " +
                $"모프 {_motion.MorphFrames.Count} · IK {_motion.IkFrameCount} · 최대 프레임 {maxFrame}";
            _boneLines.Clear();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                string human = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                    pair.Key, out VmdHumanoidBoneBinding binding) &&
                    binding.HasHumanBodyBone
                        ? binding.HumanBodyBone.ToString()
                        : binding.IsIkTarget ? "IK" : "-";
                _boneLines.Add($"{pair.Key} → {human} · {pair.Value}키");
            }
            _boneLines.Sort(StringComparer.Ordinal);
        }

        private string OutputPath(string suffix)
        {
            string directory = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "vmd-analysis");
            Directory.CreateDirectory(directory);
            string baseName = Path.GetFileNameWithoutExtension(_vmdPath);
            return Path.Combine(directory, $"{baseName}-{suffix}.csv");
        }

        private void ExportBoneCsv()
        {
            string path = OutputPath("bone-trajectory");
            var sorted = new List<VmdBoneFrame>(_motion.BoneFrames);
            sorted.Sort((a, b) => a.FrameIndex != b.FrameIndex
                ? a.FrameIndex.CompareTo(b.FrameIndex)
                : string.CompareOrdinal(a.BoneName, b.BoneName));
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(
                    "frame,bone,human_bone,vmd_x,vmd_y,vmd_z,unity_x,unity_y,unity_z," +
                    "rot_x,rot_y,rot_z,rot_w");
                foreach (VmdBoneFrame frame in sorted)
                {
                    Vector3 unity = VmdUnityTransformConverter.ConvertVmdPositionToUnityMeters(
                        frame.Position);
                    string human = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                        frame.BoneName, out VmdHumanoidBoneBinding binding) &&
                        binding.HasHumanBodyBone ? binding.HumanBodyBone.ToString() : "";
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                        frame.FrameIndex, Escape(frame.BoneName), human,
                        frame.Position.x, frame.Position.y, frame.Position.z,
                        unity.x, unity.y, unity.z,
                        frame.Rotation.x, frame.Rotation.y, frame.Rotation.z, frame.Rotation.w));
                }
            }
            _message = $"본 궤적 저장: {path}";
            EditorUtility.RevealInFinder(path);
        }

        private void ExportIkCsv()
        {
            string path = OutputPath("ik-timeline");
            var sorted = new List<VmdIkFrame>(_motion.IkFrames);
            sorted.Sort((a, b) => a.FrameIndex.CompareTo(b.FrameIndex));
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("frame,left_foot_ik,left_toe_ik,right_foot_ik,right_toe_ik");
                foreach (VmdIkFrame frame in sorted)
                {
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4}",
                        frame.FrameIndex,
                        frame.LeftFootEnabled ? 1 : 0, frame.LeftToeEnabled ? 1 : 0,
                        frame.RightFootEnabled ? 1 : 0, frame.RightToeEnabled ? 1 : 0));
                }
            }
            _message = $"IK 타임라인 저장: {path} ({sorted.Count}행)";
            EditorUtility.RevealInFinder(path);
        }

        private void ExportCompareReport()
        {
            VmdMotionData other;
            try
            {
                other = VmdMotionReader.Read(_comparePath);
            }
            catch (Exception error)
            {
                _message = $"비교 대상 읽기 실패: {error.Message}";
                return;
            }
            var otherByKey = new Dictionary<string, VmdBoneFrame>();
            foreach (VmdBoneFrame frame in other.BoneFrames)
            {
                otherByKey[frame.BoneName + ":" + frame.FrameIndex] = frame;
            }
            // 본별 위치 차(mm)·회전 차(도) 최대/평균을 요약해 파이프라인 출력과
            // 레퍼런스 VMD의 계량 비교를 만듦.
            var stats = new Dictionary<string, float[]>();
            var counts = new Dictionary<string, int>();
            int compared = 0;
            foreach (VmdBoneFrame frame in _motion.BoneFrames)
            {
                if (!otherByKey.TryGetValue(frame.BoneName + ":" + frame.FrameIndex,
                        out VmdBoneFrame otherFrame))
                    continue;
                compared++;
                float posMm = Vector3.Distance(
                    VmdUnityTransformConverter.ConvertVmdPositionToUnityMeters(frame.Position),
                    VmdUnityTransformConverter.ConvertVmdPositionToUnityMeters(otherFrame.Position))
                    * 1000f;
                float rotDeg = Quaternion.Angle(frame.Rotation, otherFrame.Rotation);
                if (!stats.TryGetValue(frame.BoneName, out float[] acc))
                {
                    acc = new float[4]; // 최대pos, 합pos, 최대rot, 합rot
                    stats[frame.BoneName] = acc;
                }
                acc[0] = Math.Max(acc[0], posMm);
                acc[1] += posMm;
                acc[2] = Math.Max(acc[2], rotDeg);
                acc[3] += rotDeg;
                counts[frame.BoneName] =
                    counts.TryGetValue(frame.BoneName, out int n) ? n + 1 : 1;
            }
            if (compared == 0)
            {
                _message = "공통 본·프레임이 없어 비교할 수 없습니다.";
                return;
            }
            string path = OutputPath("compare-" + Path.GetFileNameWithoutExtension(_comparePath));
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(
                    "bone,human_bone,frames,max_pos_mm,mean_pos_mm,max_rot_deg,mean_rot_deg");
                var bones = new List<string>(stats.Keys);
                bones.Sort(StringComparer.Ordinal);
                foreach (string bone in bones)
                {
                    int frames = counts[bone];
                    float[] acc = stats[bone];
                    string human = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                        bone, out VmdHumanoidBoneBinding binding) &&
                        binding.HasHumanBodyBone ? binding.HumanBodyBone.ToString() : "";
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3:F3},{4:F3},{5:F3},{6:F3}",
                        Escape(bone), human, frames,
                        acc[0], acc[1] / frames, acc[2], acc[3] / frames));
                }
            }
            _message = $"비교 리포트 저장: {path} ({compared}키 비교)";
            EditorUtility.RevealInFinder(path);
        }

        // 쉼표·따옴표·줄바꿈이 있으면 따옴표로 감싸고 내부 따옴표를 두 개로 이스케이프함.
        private static string Escape(string value) =>
            !string.IsNullOrEmpty(value) && (value.Contains(",") ||
                value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
    }
}
