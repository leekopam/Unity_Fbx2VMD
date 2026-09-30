using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MagicaCloth2;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 두 MagicaCloth 컴포넌트의 SerializeData 파라미터를 펼쳐 나란히 비교하고
    /// CSV로보내는 참고 도구임. 값을 읽고 차이만 보여주며 파라미터 적용은 하지 않음.
    /// (자동 적용·자동화는 다른 세션의 작업)
    /// </summary>
    public class MagicaClothParamCompareWindow : EditorWindow
    {
        private sealed class Row
        {
            internal string Path;
            internal string A;
            internal string B;
            internal bool Differs;
        }

        private MagicaCloth _reference;
        private MagicaCloth _target;
        private readonly List<Row> _rows = new List<Row>();
        private Vector2 _scroll;
        private string _message = "";
        private bool _onlyDiff;

        [MenuItem("Tools/FBXImporter/MagicaCloth 파라미터 비교")]
        private static void Open() =>
            GetWindow<MagicaClothParamCompareWindow>(false, "MC2 파라미터 비교");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("레퍼런스 ↔ 대상", EditorStyles.boldLabel);
            _reference = (MagicaCloth)EditorGUILayout.ObjectField(
                "레퍼런스", _reference, typeof(MagicaCloth), true);
            _target = (MagicaCloth)EditorGUILayout.ObjectField(
                "대상", _target, typeof(MagicaCloth), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("씬의 MagicaCloth 나열"))
            {
                ShowScenePicker();
            }
            using (new EditorGUI.DisabledScope(_target == null))
            {
                if (GUILayout.Button("대상 단일 추출 CSV"))
                {
                    ExportSingle(_target);
                }
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(_reference == null || _target == null))
            {
                if (GUILayout.Button("비교"))
                {
                    Compare();
                }
            }
            _onlyDiff = EditorGUILayout.ToggleLeft("다른 항목만 표시", _onlyDiff);
            if (_rows.Count > 0)
            {
                EditorGUILayout.LabelField($"{_rows.Count}행", EditorStyles.miniLabel);
                if (GUILayout.Button("비교 결과 CSV보내기"))
                {
                    ExportCompare();
                }
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (Row row in _rows)
            {
                if (_onlyDiff && !row.Differs) continue;
                Color previous = GUI.color;
                if (row.Differs) GUI.color = new Color(1f, 0.8f, 0.4f);
                EditorGUILayout.LabelField(
                    $"{row.Path}\n    A: {row.A}   B: {row.B}", EditorStyles.miniLabel);
                GUI.color = previous;
            }
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            }
        }

        private void ShowScenePicker()
        {
            // 열린 씬의 MagicaCloth를 나열해 레퍼런스/대상 선택을 돕는 메뉴임.
            var menu = new GenericMenu();
            foreach (MagicaCloth cloth in UnityEngine.Object.FindObjectsOfType<MagicaCloth>(true))
            {
                string label = cloth.name;
                menu.AddItem(new GUIContent("레퍼런스/" + label), false,
                    () => { _reference = cloth; });
                menu.AddItem(new GUIContent("대상/" + label), false,
                    () => { _target = cloth; });
            }
            if (menu.GetItemCount() == 0)
            {
                _message = "열린 씬에 MagicaCloth가 없습니다.";
                return;
            }
            menu.ShowAsContext();
        }

        private void Compare()
        {
            _rows.Clear();
            var a = new Dictionary<string, string>();
            var b = new Dictionary<string, string>();
            Flatten(_reference.SerializeData, "", a);
            Flatten(_target.SerializeData, "", b);
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string key in a.Keys) paths.Add(key);
            foreach (string key in b.Keys) paths.Add(key);
            foreach (string path in paths)
            {
                a.TryGetValue(path, out string av);
                b.TryGetValue(path, out string bv);
                _rows.Add(new Row
                {
                    Path = path,
                    A = av ?? "(없음)",
                    B = bv ?? "(없음)",
                    Differs = av != bv,
                });
            }
            _message = $"비교 완료: {_rows.Count}행";
        }

        // SerializeData의 공개 인스턴스 필드를 재귀적으로 펼쳐 경로=값 표를 만듦.
        private static void Flatten(object source, string prefix,
            IDictionary<string, string> output)
        {
            if (source == null)
            {
                if (!string.IsNullOrEmpty(prefix)) output[prefix] = "null";
                return;
            }
            Type type = source.GetType();
            if (type.IsPrimitive || source is string || type.IsEnum || type.IsPointer)
            {
                output[prefix] = Convert.ToString(source, CultureInfo.InvariantCulture);
                return;
            }
            if (source is AnimationCurve curve)
            {
                var text = new StringBuilder($"키{curve.length}");
                foreach (Keyframe key in curve.keys)
                {
                    text.Append(string.Format(CultureInfo.InvariantCulture,
                        "[{0:F2}:{1:F3}]", key.time, key.value));
                }
                output[prefix] = text.ToString();
                return;
            }
            if (source is UnityEngine.Object unityObject)
            {
                output[prefix] = unityObject == null ? "null" : unityObject.name;
                return;
            }
            if (source is IList list)
            {
                output[prefix] = $"{list.Count}건";
                int limit = Math.Min(list.Count, 8);
                for (int i = 0; i < limit; i++)
                {
                    Flatten(list[i], $"{prefix}[{i}]", output);
                }
                if (list.Count > limit)
                {
                    output[prefix + "…"] = $"외 {list.Count - limit}건";
                }
                return;
            }
            if (!type.IsClass && !type.FullName.StartsWith("UnityEngine"))
            {
                // Unity.Mathematics의 floatN·intN 등 구조체는 ToString이 충분히 읽기 쉬움.
                if (type.FullName.StartsWith("Unity.Mathematics") ||
                    type.Name.StartsWith("Vector") || type.Name.StartsWith("Quaternion"))
                {
                    output[prefix] = source.ToString();
                    return;
                }
            }
            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType.IsPointer || field.IsNotSerialized) continue;
                object value = field.GetValue(source);
                Flatten(value, string.IsNullOrEmpty(prefix)
                    ? field.Name : prefix + "." + field.Name, output);
            }
        }

        private string OutputDirectory()
        {
            string directory = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "physics-compare");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private void ExportSingle(MagicaCloth cloth)
        {
            var table = new Dictionary<string, string>();
            Flatten(cloth.SerializeData, "", table);
            string path = Path.Combine(OutputDirectory(),
                $"{cloth.name}-params-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("path,value");
                foreach (KeyValuePair<string, string> pair in
                         new SortedDictionary<string, string>(table, StringComparer.Ordinal))
                {
                    writer.WriteLine($"{Quote(pair.Key)},{Quote(pair.Value)}");
                }
            }
            _message = $"파라미터 저장: {path} ({table.Count}행)";
            EditorUtility.RevealInFinder(path);
        }

        private void ExportCompare()
        {
            string path = Path.Combine(OutputDirectory(),
                $"compare-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("path,reference,target,differs");
                foreach (Row row in _rows)
                {
                    writer.WriteLine(
                        $"{Quote(row.Path)},{Quote(row.A)},{Quote(row.B)},{(row.Differs ? 1 : 0)}");
                }
            }
            _message = $"비교 저장: {path} ({_rows.Count}행)";
            EditorUtility.RevealInFinder(path);
        }

        private static string Quote(string value) =>
            "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
