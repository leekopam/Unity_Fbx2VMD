using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// ClothSerializeData의 값형 파라미터를 결정적(flat key→value) 스냅샷으로 덤프한다.
    /// 본/콜라이더/렌더러 같은 참조와 리스트는 '구조 데이터'라 스냅샷에서 제외 —
    /// 튜닝 값만 추적하므로 세션·오브젝트 ID가 달라도 비교 가능하다.
    /// 용도: 튜닝 회귀 diff, 캐릭터/클립별 설정 스냅샷 보관.
    /// </summary>
    public static class ClothSnapshotter
    {
        /// <summary>값형 필드를 "a.b.c = 값" 형태의 정렬된 사전으로 평탄화한다.</summary>
        public static SortedDictionary<string, string> Capture(ClothSerializeData sdata)
        {
            var map = new SortedDictionary<string, string>();
            if (sdata != null)
                Flatten(sdata, "", map, 0);
            return map;
        }

        /// <summary>클로스 오브젝트에서 바로 캡처한다.</summary>
        public static SortedDictionary<string, string> Capture(MagicaCloth cloth)
            => Capture(cloth?.SerializeData);

        static void Flatten(object obj, string prefix, SortedDictionary<string, string> map, int depth)
        {
            if (obj == null || depth > 8)
                return;
            var type = obj.GetType();
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var v = f.GetValue(obj);
                string key = string.IsNullOrEmpty(prefix) ? f.Name : prefix + "." + f.Name;
                var ft = f.FieldType;
                if (v == null)
                {
                    map[key] = "null";
                    continue;
                }
                if (ft == typeof(string))
                {
                    map[key] = "\"" + v + "\"";
                    continue;
                }
                if (ft.IsPrimitive || ft.IsEnum || ft == typeof(decimal))
                {
                    map[key] = System.Convert.ToString(v,
                        System.Globalization.CultureInfo.InvariantCulture);
                    continue;
                }
                // 참조·컬렉션은 구조 데이터 — 파라미터 스냅샷에서 제외
                if (typeof(UnityEngine.Object).IsAssignableFrom(ft) ||
                    typeof(System.Collections.IList).IsAssignableFrom(ft) ||
                    typeof(System.Collections.IDictionary).IsAssignableFrom(ft) ||
                    ft.IsSubclassOf(typeof(System.Delegate)))
                    continue;
                // 값형/설정 블록 — 재귀 평탄화로 필드 단위 diff를 확보한다
                Flatten(v, key, map, depth + 1);
            }
        }

        /// <summary>두 스냅샷의 의미 있는 차이를 "키: 이전 → 이후" 목록으로 반환한다.</summary>
        public static List<string> Diff(
            SortedDictionary<string, string> before, SortedDictionary<string, string> after)
        {
            var diffs = new List<string>();
            var keys = new SortedSet<string>();
            if (before != null) keys.UnionWith(before.Keys);
            if (after != null) keys.UnionWith(after.Keys);
            foreach (var k in keys)
            {
                string bv = null, av = null;
                bool hasB = before != null && before.TryGetValue(k, out bv);
                bool hasA = after != null && after.TryGetValue(k, out av);
                if (hasB && hasA && bv == av)
                    continue;
                diffs.Add($"{k}: {(hasB ? bv : "<없음>")} → {(hasA ? av : "<없음>")}");
            }
            return diffs;
        }

        /// <summary>스냅샷을 결정적 텍스트로 직렬화한다 (파일 저장·비교용).</summary>
        public static string Serialize(SortedDictionary<string, string> map)
        {
            var sb = new StringBuilder();
            foreach (var kv in map)
                sb.AppendLine($"{kv.Key} = {kv.Value}");
            return sb.ToString();
        }

        /// <summary>Serialize 출력을 다시 사전으로 읽는다.</summary>
        public static SortedDictionary<string, string> Deserialize(string text)
        {
            var map = new SortedDictionary<string, string>();
            if (string.IsNullOrEmpty(text))
                return map;
            foreach (var line in text.Split('\n'))
            {
                int eq = line.IndexOf(" = ", System.StringComparison.Ordinal);
                if (eq > 0)
                    map[line.Substring(0, eq)] = line.Substring(eq + 3).TrimEnd('\r');
            }
            return map;
        }
    }
}
