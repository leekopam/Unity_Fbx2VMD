#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 실행 증거 폴더의 human-labels.csv에서 같은 입력 FBX의 사람 확정 표식을 누적함.
    /// 열 순서는 run-product-smoke.mjs가 기록하는 서식과 같아야 함.
    /// </summary>
    internal static class EditorHumanoidFootContactIntentLabelStore
    {
        private const int SideColumn = 2;
        private const int ContactLabelColumn = 3;
        private const int MotionLabelColumn = 4;

        [Serializable]
        private sealed class StateJson
        {
            public string input;
        }

        internal static HumanoidFootContactIntentLabelSet Load(string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
                return HumanoidFootContactIntentLabelSet.Empty;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return HumanoidFootContactIntentLabelSet.Empty;
            string root = Path.Combine(projectRoot,
                "Docs", "Workflow", "Local", "evidence", "boogle");
            if (!Directory.Exists(root)) return HumanoidFootContactIntentLabelSet.Empty;

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "human-labels.csv", SearchOption.AllDirectories);
            }
            catch
            {
                return HumanoidFootContactIntentLabelSet.Empty;
            }
            // 파일 순서를 고정해 겹치는 표식의 해석이 실행마다 달라지지 않게 함.
            Array.Sort(files, StringComparer.Ordinal);

            var left = new List<HumanoidFootContactIntentLabel>();
            var right = new List<HumanoidFootContactIntentLabel>();
            int fileCount = 0, rowCount = 0;
            foreach (string file in files)
            {
                // 표식은 같은 입력 FBX의 증거 폴더와 짝지었을 때만 유효함.
                string statePath = Path.Combine(Path.GetDirectoryName(file), "state.json");
                if (!TryReadInput(statePath, out string input) ||
                    !MatchesInput(input, sourceKey))
                    continue;
                try
                {
                    bool accepted = false;
                    foreach (string line in File.ReadLines(file))
                    {
                        if (!TryParseRow(line, out bool isLeft,
                                out HumanoidFootContactIntentLabel label))
                            continue;
                        (isLeft ? left : right).Add(label);
                        rowCount++;
                        accepted = true;
                    }
                    if (accepted) fileCount++;
                }
                catch (IOException)
                {
                    continue;
                }
            }
            return new HumanoidFootContactIntentLabelSet(left, right, fileCount, rowCount);
        }

        private static bool TryReadInput(string path, out string input)
        {
            input = null;
            try
            {
                if (!File.Exists(path)) return false;
                input = JsonUtility.FromJson<StateJson>(File.ReadAllText(path))?.input;
                return !string.IsNullOrEmpty(input);
            }
            catch
            {
                return false;
            }
        }

        // state.json의 input은 파일명(확장자 포함)이고 호출 측 키는 에셋명일 수 있어 둘 다 비교함.
        private static bool MatchesInput(string input, string sourceKey)
        {
            return string.Equals(input, sourceKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(input), sourceKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(input), sourceKey,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryParseRow(string line, out bool isLeft,
            out HumanoidFootContactIntentLabel label)
        {
            isLeft = false;
            label = default;
            if (string.IsNullOrWhiteSpace(line)) return false;
            string[] cells = line.Split(',');
            if (cells.Length <= MotionLabelColumn ||
                !int.TryParse(cells[0].Trim().Trim('"'), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int from) ||
                !int.TryParse(cells[1].Trim().Trim('"'), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int to) ||
                from < 0 || to < from) return false;
            string side = cells[SideColumn].Trim().Trim('"');
            if (side == "left") isLeft = true;
            else if (side != "right") return false;
            bool? isSupport = ParseContact(cells[ContactLabelColumn]);
            HumanoidFootContactIntentMode? mode = ParseMotion(cells[MotionLabelColumn]);
            if (!isSupport.HasValue && !mode.HasValue) return false;
            label = new HumanoidFootContactIntentLabel(from, to, isSupport, mode);
            return true;
        }

        // 표식값은 validate-human-labels.mjs가 검증하는 한국어 어휘와 같음.
        private static bool? ParseContact(string value)
        {
            switch (value.Trim().Trim('"'))
            {
                case "공중": return false;
                case "앞꿈치":
                case "뒤꿈치":
                case "발 전체": return true;
                default: return null;
            }
        }

        private static HumanoidFootContactIntentMode? ParseMotion(string value)
        {
            switch (value.Trim().Trim('"'))
            {
                // 굴러가는 접촉도 앵커는 수평으로 고정되므로 Plant로 다룸.
                case "고정":
                case "구르기": return HumanoidFootContactIntentMode.Plant;
                case "의도된 이동": return HumanoidFootContactIntentMode.Slide;
                default: return null;
            }
        }
    }
}
#endif
