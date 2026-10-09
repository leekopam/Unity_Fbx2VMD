using System.Text.RegularExpressions;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 머리카락 본의 부위 분류.
    /// </summary>
    public enum HairPart
    {
        Unknown = 0,
        Front,
        Side,
        Back,
        Tail,
        Ahoge,
        Accessory,
    }

    /// <summary>
    /// 본 이름을 분석하여 머리카락 부위를 분류하는 순수 함수 모음.
    /// 영어/일본어 로마자/MMD 명명 규칙을 지원한다.
    /// </summary>
    public static class HairBoneClassifier
    {
        // 부위별 이름 패턴 (정규식, 대소문자 무시)
        static readonly (HairPart part, string pattern)[] PartPatterns =
        {
            (HairPart.Ahoge,    @"ahoge|アホ毛|antenna"),
            (HairPart.Tail,     @"twin|twintail|pigtail|pony|ponytail|tail|ツインテ|ポニテ|テール|サイドテール"),
            (HairPart.Front,    @"front|bang|maegami|前髪|forehead"),
            (HairPart.Side,     @"side|yokogami|横髪|temple|sidelock"),
            (HairPart.Back,     @"back|ushiro|後髪|ushirogami|rear"),
            // bow는 elbow 오탐 때문에 단독 금지 — hairbow/bowknot/bowtie만 허용
            // 넥타이는 neck 제외 패턴과 충돌하므로 tie+숫자/명시 키워드만 허용
            (HairPart.Accessory,@"ribbon|リボン|hair_?bow|bowknot|bow_?tie|necktie|ネクタイ|tie\d|饰|飾|accessory|kanzashi|簪|hair_?pin|hair_?ornament|scrunchie|シュシュ|chain|チェーン|strap|ornament|tassel|房|charm|buckle"),
        };

        // 머리카락을 나타내는 공통 키워드
        static readonly Regex HairKeyword = new Regex(
            @"hair|髪|kami|joint_?h|毛束|twin|pony|ahoge",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 물리 대상에서 제외할 얼굴/기타 본 패턴
        static readonly Regex ExclusionKeyword = new Regex(
            @"eye|hitomi|目|brow|mayu|眉|jaw|ago|顎|teeth|歯|tongue|舌|lip|口|cheek|ほほ|ほっぺ|" +
            @"bust|breast|胸|mune|skirt|スカート|sleeve|袖|sode|neck|head|neck$|face|顔|nose|鼻|ear|耳|mimi",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 이름이 머리카락 본 후보인지 반환한다.
        /// </summary>
        public static bool IsHairBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return false;
            if (IsExcluded(boneName))
                return false;
            return HairKeyword.IsMatch(boneName) || Classify(boneName) != HairPart.Unknown;
        }

        /// <summary>
        /// 이름이 물리 제외 대상(얼굴/몸통 본)인지 반환한다.
        /// </summary>
        public static bool IsExcluded(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return true;
            // 넥타이 본은 "neck" 패턴에 걸리면 안 되므로 선처리로 허용
            if (Regex.IsMatch(boneName, @"necktie|ネクタイ|tie\d", RegexOptions.IgnoreCase))
                return false;
            return ExclusionKeyword.IsMatch(boneName);
        }

        /// <summary>
        /// 본 이름에서 머리카락 부위를 추론한다. 판별 불가 시 Unknown.
        /// </summary>
        public static HairPart Classify(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return HairPart.Unknown;
            if (IsExcluded(boneName))
                return HairPart.Unknown;

            foreach (var (part, pattern) in PartPatterns)
            {
                if (Regex.IsMatch(boneName, pattern, RegexOptions.IgnoreCase))
                    return part;
            }
            return HairPart.Unknown;
        }

        /// <summary>
        /// 이름으로 부위를 판별할 수 없는 머리카락 본에 대해
        /// 머리 기준 로컬 위치로 부위를 추론한다.
        /// forward=머리 정면(+Z), up=위(+Y)는 Unity Humanoid 기준.
        /// </summary>
        /// <param name="localFromHead">머리 본 기준 머리카락 루트의 상대 위치</param>
        public static HairPart ClassifyByPosition(UnityEngine.Vector3 localFromHead)
        {
            // 앞쪽이면 앞머리
            if (localFromHead.z > 0.02f && localFromHead.z > UnityEngine.Mathf.Abs(localFromHead.x))
                return HairPart.Front;
            // 위쪽+옆쪽 돌출은 트윈테일/포니테일 가능성 (옆머리보다 먼저 판정)
            if (localFromHead.y > 0.03f && UnityEngine.Mathf.Abs(localFromHead.x) > 0.03f)
                return HairPart.Tail;
            // 옆으로 크게 벗어나면 옆머리
            if (UnityEngine.Mathf.Abs(localFromHead.x) > UnityEngine.Mathf.Abs(localFromHead.z))
                return HairPart.Side;
            return HairPart.Back;
        }
    }
}
