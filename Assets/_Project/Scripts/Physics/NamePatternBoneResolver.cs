using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// Humanoid Avatar가 없는 모델에서 본 이름 패턴으로 신체 본을 해석하는 폴백 해석기.
    /// 영어·일본어·MMD 로마자(kosi/kubi/ude/asi 등) 명명 규칙을 지원한다.
    /// 키워드는 가장 긴 매칭이 우선해 'forearm'이 'arm'에 흡수되지 않도록 한다.
    /// </summary>
    public static class NamePatternBoneResolver
    {
        public enum Side { None, Left, Right }

        struct Entry
        {
            public HumanBodyBones bone;
            public Side side;
            public string[] keys;
        }

        static Entry E(HumanBodyBones b, Side s, params string[] keys)
            // 키도 이름과 같은 규칙으로 정규화한다 — 구분자가 남은 키("upper body2")는
            // 정규화된 이름에 절대 매칭되지 않는 dead key가 된다.
            => new Entry
            {
                bone = b,
                side = s,
                keys = System.Array.ConvertAll(keys, Normalize),
            };

        // 팔·다리는 좌우 구분 필수. 긴 키워드가 짧은 키워드보다 우선 매칭됨.
        static readonly Entry[] Table =
        {
            E(HumanBodyBones.Head, Side.None, "head", "atama", "頭"),
            E(HumanBodyBones.Neck, Side.None, "neck", "kubi", "首"),
            E(HumanBodyBones.UpperChest, Side.None, "upperchest", "upper body2", "chest2", "spine3"),
            E(HumanBodyBones.Chest, Side.None, "chest", "mune", "胸", "上半身", "upperbody"),
            E(HumanBodyBones.Spine, Side.None, "spine", "sebone", "背骨", "胴"),
            // 'center'/'lower_body'는 MMD·Unity-chan 계열 리그의 hips 명명 (PronamaChan 확인)
            E(HumanBodyBones.Hips, Side.None, "hips", "hip", "pelvis", "kosi", "koshi",
                "腰", "骨盤", "下半身", "center", "センター", "lowerbody"),
            E(HumanBodyBones.LeftShoulder, Side.Left, "shoulder", "kata", "肩"),
            E(HumanBodyBones.RightShoulder, Side.Right, "shoulder", "kata", "肩"),
            E(HumanBodyBones.LeftLowerArm, Side.Left, "lowerarm", "forearm", "elbow", "hiji", "肘", "ひじ", "前腕"),
            E(HumanBodyBones.RightLowerArm, Side.Right, "lowerarm", "forearm", "elbow", "hiji", "肘", "ひじ", "前腕"),
            E(HumanBodyBones.LeftUpperArm, Side.Left, "upperarm", "arm", "ude", "腕", "上腕"),
            E(HumanBodyBones.RightUpperArm, Side.Right, "upperarm", "arm", "ude", "腕", "上腕"),
            E(HumanBodyBones.LeftHand, Side.Left, "hand", "wrist", "tekubi", "手首", "てくび", "手"),
            E(HumanBodyBones.RightHand, Side.Right, "hand", "wrist", "tekubi", "手首", "てくび", "手"),
            E(HumanBodyBones.LeftUpperLeg, Side.Left, "upperleg", "thigh", "upleg", "leg", "momo", "asi", "ashi", "足", "あし", "股", "太腿", "もも"),
            E(HumanBodyBones.RightUpperLeg, Side.Right, "upperleg", "thigh", "upleg", "leg", "momo", "asi", "ashi", "足", "あし", "股", "太腿", "もも"),
            E(HumanBodyBones.LeftLowerLeg, Side.Left, "lowerleg", "calf", "shin", "knee", "hiza", "sune", "膝", "ひざ", "脛", "すね"),
            E(HumanBodyBones.RightLowerLeg, Side.Right, "lowerleg", "calf", "shin", "knee", "hiza", "sune", "膝", "ひざ", "脛", "すね"),
            E(HumanBodyBones.LeftFoot, Side.Left, "foot", "feet", "ankle", "asikubi", "ashikubi", "足首", "あしくび"),
            E(HumanBodyBones.RightFoot, Side.Right, "foot", "feet", "ankle", "asikubi", "ashikubi", "足首", "あしくび"),
            E(HumanBodyBones.LeftToes, Side.Left, "toe", "toes", "ball", "tsumasaki", "tumasaki", "つま先", "爪先"),
            E(HumanBodyBones.RightToes, Side.Right, "toe", "toes", "ball", "tsumasaki", "tumasaki", "つま先", "爪先"),
            // 首と同じ 'kubi' 계열이 手首(てくび)와 충돌하지 않도록 손은 별도 키 사용
        };

        /// <summary>
        /// 루트 하위 전체 Transform을 이름 패턴으로 해석해 HumanBodyBones 매핑을 만든다.
        /// 같은 본에 여러 후보가 걸리면 먼저 찾은(상위에 가까운) 것을 유지한다.
        /// </summary>
        public static Dictionary<HumanBodyBones, Transform> Resolve(Transform root)
        {
            var map = new Dictionary<HumanBodyBones, Transform>();
            if (root == null)
                return map;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || string.IsNullOrEmpty(t.name))
                    continue;
                Side side = DetectSide(t.name);
                string norm = Normalize(t.name);

                HumanBodyBones best = HumanBodyBones.LastBone;
                int bestLen = 0;
                foreach (var e in Table)
                {
                    if (e.side != Side.None && e.side != side)
                        continue;
                    foreach (var key in e.keys)
                    {
                        if (key.Length > bestLen && norm.Contains(key))
                        {
                            best = e.bone;
                            bestLen = key.Length;
                        }
                    }
                }
                if (best != HumanBodyBones.LastBone && !map.ContainsKey(best))
                    map[best] = t;
            }
            return map;
        }

        /// <summary>좌/우 쪽 구분을 이름에서 추출한다. 판별 불가면 None.</summary>
        public static Side DetectSide(string rawName)
        {
            string lower = rawName.ToLowerInvariant();
            bool left = lower.Contains("left") || lower.Contains("hidari") || rawName.Contains("左");
            bool right = lower.Contains("right") || lower.Contains("migi") || rawName.Contains("右");
            if (left && !right)
                return Side.Left;
            if (right && !left)
                return Side.Right;
            if (left && right)
                return Side.None; // 양쪽 표기가 섞인 이름은 판별 불가

            // "arm_l", "L_leg" 같은 토큰형 표기
            var tokens = lower.Split('_', '-', '.', ' ', '(', ')', '[', ']');
            foreach (var tok in tokens)
            {
                if (tok == "l" || tok == "lt")
                    left = true;
                else if (tok == "r" || tok == "rt")
                    right = true;
            }
            if (left != right)
                return left ? Side.Left : Side.Right;

            // "ArmL"/"RArm" 같은 무구분자 표기 — 대문자 L/R만 인정해
            // 'collar'·'shoulder' 같은 소문자 종료 오탐을 막는다.
            if (rawName.Length >= 2)
            {
                char last = rawName[rawName.Length - 1];
                if (last == 'L')
                    left = true;
                else if (last == 'R')
                    right = true;
                // 접두 측면은 두 번째 글자가 대문자일 때만 (예: LArm은 허용, Left는 위 단어 검사가 담당)
                char first = rawName[0];
                if (char.IsUpper(rawName[1]))
                {
                    if (first == 'L')
                        left = true;
                    else if (first == 'R')
                        right = true;
                }
            }
            if (left == right)
                return Side.None;
            return left ? Side.Left : Side.Right;
        }

        /// <summary>구분자를 제거한 소문자 비교용 이름.</summary>
        static string Normalize(string name)
        {
            var chars = name.ToLowerInvariant().ToCharArray();
            int w = 0;
            foreach (char c in chars)
            {
                if (c != '_' && c != '-' && c != '.' && c != ' ')
                    chars[w++] = c;
            }
            return new string(chars, 0, w);
        }
    }
}
