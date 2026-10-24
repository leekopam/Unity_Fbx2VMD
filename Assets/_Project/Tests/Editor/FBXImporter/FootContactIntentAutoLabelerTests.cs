// Jev 자동 라벨 제안기의 순수 로직(요청 JSON·응답 파싱·라벨 매핑·CSV 왕복)을
// 네트워크 없이 검증함. 실제 API 호출은 창에서 수동 실행으로만 확인한다.
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Fbx2Vmd.EditorTools.FBXImporter;

namespace Tests.Editor.FBXImporter
{
    public class FootContactIntentAutoLabelerTests
    {
        private const string ResponseSample =
            "{\"model\":\"jev-1.14\",\"answers\":{" +
            "\"touching_f0\":{\"type\":\"noul\",\"noul\":0.95}," +
            "\"touching_f1\":{\"type\":\"noul\",\"noul\":0.9}," +
            "\"touching_f2\":{\"type\":\"noul\",\"noul\":0.15}," +
            "\"touching_f3\":{\"type\":\"noul\",\"noul\":0.9}," +
            "\"contact_f0\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
            "\"contact_f1\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.8}," +
            "\"contact_f2\":{\"type\":\"choice\",\"choice\":\"airborne\",\"confidence\":0.7}," +
            "\"contact_f3\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.95}," +
            "\"transition_frame\":{\"type\":\"choice\",\"choice\":\"none\",\"confidence\":0.9}," +
            "\"motion\":{\"type\":\"choice\",\"choice\":\"pinned\",\"confidence\":0.8," +
            "\"probabilities\":{\"pinned\":0.8,\"roll\":0.2}}}," +
            "\"usage\":{\"input_tokens\":100,\"output_tokens\":20}}";

        [Test]
        public void Given_Span_When_SamplingFrames_Then_EndpointsIncludedNoDuplicates()
        {
            int[] frames =
                FootContactIntentAutoLabeler.SampleFrames(100, 199);
            Assert.That(frames[0], Is.EqualTo(100));
            Assert.That(frames[frames.Length - 1], Is.EqualTo(199));
            Assert.That(frames.Length, Is.EqualTo(6));
            CollectionAssert.AllItemsAreUnique(frames);
            // 1프레임 스팬은 시작 1개만 반환.
            Assert.That(FootContactIntentAutoLabeler.SampleFrames(50, 50),
                Is.EqualTo(new[] { 50 }));
        }

        [Test]
        public void Given_Frames_When_BuildingRequest_Then_QuestionsAndAssetsWired()
        {
            byte[][] pngs = { new byte[] { 1, 2, 3 }, new byte[] { 4, 5 } };
            int[] frames = { 100, 199 };
            var measures = new[]
            {
                new FootContactIntentAutoLabeler.FrameMeasure
                {
                    Frame = 100, AnkleCm = 8.2f, ToeCm = 3.1f,
                    SpeedCms = 0f, Vdir = "기준",
                },
                new FootContactIntentAutoLabeler.FrameMeasure
                {
                    Frame = 199, AnkleCm = 6.1f, ToeCm = 2.8f,
                    SpeedCms = 12f, Vdir = "하강",
                },
            };
            var bounded = new JObject
            {
                ["prev"] = new JObject { ["label"] = "plant" },
                ["next"] = "클립 끝",
            };
            string json = FootContactIntentAutoLabeler.BuildRequestJson(
                "satisfaction_2.fbx", "left", 100, 199, frames, pngs,
                measures, bounded);
            JObject request = JObject.Parse(json);
            Assert.That(request.Value<string>("model"), Is.EqualTo("jev-latest"));
            JObject questions = (JObject)request["questions"];
            Assert.That(questions["contact_f0"].Value<string>("type"),
                Is.EqualTo("choice"));
            Assert.That(questions["touching_f0"].Value<string>("type"),
                Is.EqualTo("noul"));
            Assert.That(questions["transition_frame"].Value<string>("type"),
                Is.EqualTo("choice"));
            Assert.That(questions["motion"].Value<string>("type"),
                Is.EqualTo("choice"));
            // 두 질의 모두 '불확실' 선택지를 유지해야 보류 라벨이 나옴.
            Assert.That(((JObject)questions["contact_f0"]["criteria"])
                .ContainsKey("uncertain"), Is.True);
            Assert.That(((JObject)questions["motion"]["criteria"])
                .ContainsKey("uncertain"), Is.True);
            // 전이 질의는 샘플 프레임 전부 + none 선택지를 가져야 함.
            JObject trans = (JObject)questions["transition_frame"]["criteria"];
            Assert.That(trans.ContainsKey("100"), Is.True);
            Assert.That(trans.ContainsKey("199"), Is.True);
            Assert.That(trans.ContainsKey("none"), Is.True);
            JObject state = (JObject)request["state"];
            // 측정값과 이웃 맥락이 state에 실려야 한다.
            JArray meas = (JArray)state["measurements"];
            Assert.That(meas.Count, Is.EqualTo(2));
            Assert.That(meas[0].Value<int>("frame"), Is.EqualTo(100));
            Assert.That(meas[1].Value<string>("vdir"), Is.EqualTo("하강"));
            Assert.That(state["bounded_by"]["prev"].Value<string>("label"),
                Is.EqualTo("plant"));
            JObject assets = (JObject)request["assets"];
            Assert.That(assets.Count, Is.EqualTo(2));
            // state의 자산 참조 문자열이 assets 키와 정확히 일치해야 함.
            foreach (var image in state)
            {
                if (image.Key.StartsWith("frame_image_"))
                {
                    Assert.That(assets.ContainsKey((string)image.Value), Is.True,
                        image.Key);
                }
            }
        }

        [Test]
        public void Given_Response_When_Parsing_Then_MapsToKoreanLabels()
        {
            var result = FootContactIntentAutoLabeler.ParseResponse(
                ResponseSample, 100, 199, new[] { 100, 133, 166, 199 });
            Assert.That(result.Count, Is.EqualTo(1));
            // touching: f0~f3 닿음×3(2.75) vs 공중(0.85) → 닿음 과반.
            // 닿은 프레임의 부위 투표는 전부 whole → 발 전체, 점유율 1.0.
            Assert.That(result[0].Contact, Is.EqualTo("발 전체"));
            Assert.That(result[0].Motion, Is.EqualTo("고정"));
            Assert.That(result[0].From, Is.EqualTo(100));
            Assert.That(result[0].To, Is.EqualTo(199));
            Assert.That(result[0].ContactConf, Is.EqualTo(1f).Within(0.01f));
            Assert.That(result[0].MotionConf, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void Given_TransitionResponse_When_Parsing_Then_SplitsIntoTwoRows()
        {
            const string json =
                "{\"model\":\"jev-1.14\",\"answers\":{" +
                "\"touching_f0\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"touching_f1\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"touching_f2\":{\"type\":\"noul\",\"noul\":0.85}," +
                "\"touching_f3\":{\"type\":\"noul\",\"noul\":0.1}," +
                "\"touching_f4\":{\"type\":\"noul\",\"noul\":0.1}," +
                "\"touching_f5\":{\"type\":\"noul\",\"noul\":0.15}," +
                "\"contact_f0\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"contact_f1\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"contact_f2\":{\"type\":\"choice\",\"choice\":\"toes\",\"confidence\":0.8}," +
                "\"contact_f3\":{\"type\":\"choice\",\"choice\":\"airborne\",\"confidence\":0.9}," +
                "\"contact_f4\":{\"type\":\"choice\",\"choice\":\"airborne\",\"confidence\":0.9}," +
                "\"contact_f5\":{\"type\":\"choice\",\"choice\":\"airborne\",\"confidence\":0.85}," +
                "\"transition_frame\":{\"type\":\"choice\",\"choice\":\"160\",\"confidence\":0.8}," +
                "\"motion\":{\"type\":\"choice\",\"choice\":\"pinned\",\"confidence\":0.8}}," +
                "\"usage\":{}}";
            int[] frames = { 100, 120, 140, 160, 180, 199 };
            var result = FootContactIntentAutoLabeler.ParseResponse(
                json, 100, 199, frames);
            // 전이 160 지목 → [100,159] + [160,199] 두 행.
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].From, Is.EqualTo(100));
            Assert.That(result[0].To, Is.EqualTo(159));
            Assert.That(result[0].Contact, Is.EqualTo("발 전체"));
            Assert.That(result[0].Motion, Is.EqualTo("고정"));
            Assert.That(result[1].From, Is.EqualTo(160));
            Assert.That(result[1].To, Is.EqualTo(199));
            Assert.That(result[1].Contact, Is.EqualTo("공중"));
            // 공중 세그먼트는 동작 의도가 성립하지 않아 불확실로 내림.
            Assert.That(result[1].Motion, Is.EqualTo("불확실"));
        }

        [Test]
        public void Given_WeakTransition_When_Parsing_Then_NoSplit()
        {
            // transition 신뢰도가 0.6 미만이면 무시하고 단일 행.
            const string json =
                "{\"model\":\"jev-1.14\",\"answers\":{" +
                "\"touching_f0\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"touching_f1\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"touching_f2\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"contact_f0\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"contact_f1\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"contact_f2\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"transition_frame\":{\"type\":\"choice\",\"choice\":\"140\",\"confidence\":0.4}," +
                "\"motion\":{\"type\":\"choice\",\"choice\":\"pinned\",\"confidence\":0.8}}," +
                "\"usage\":{}}";
            var result = FootContactIntentAutoLabeler.ParseResponse(
                json, 100, 199, new[] { 100, 140, 199 });
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Contact, Is.EqualTo("발 전체"));
        }

        [Test]
        public void Given_ResponseWithoutContactFrames_When_Parsing_Then_Throws()
        {
            const string noContact =
                "{\"model\":\"jev-1.14\",\"answers\":{" +
                "\"motion\":{\"type\":\"choice\",\"choice\":\"pinned\"," +
                "\"confidence\":0.8}},\"usage\":{}}";
            Assert.Throws<InvalidDataException>(() =>
                FootContactIntentAutoLabeler.ParseResponse(
                    noContact, 0, 10, new[] { 0, 10 }));
        }

        [Test]
        public void Given_LowConfidence_When_Mapping_Then_FallsBackToUncertain()
        {
            string low = FootContactIntentAutoLabeler.MapLabel("whole",
                new Dictionary<string, string> { ["whole"] = "발 전체" },
                FootContactIntentAutoLabeler.MinSuggestConfidence - 0.01f);
            Assert.That(low, Is.EqualTo("불확실"));
            // 알 수 없는 선택지도 불확실로 내림.
            string unknown = FootContactIntentAutoLabeler.MapLabel("mystery",
                new Dictionary<string, string> { ["whole"] = "발 전체" }, 0.99f);
            Assert.That(unknown, Is.EqualTo("불확실"));
            // 임계 이상이면 실제 라벨 유지.
            string high = FootContactIntentAutoLabeler.MapLabel("whole",
                new Dictionary<string, string> { ["whole"] = "발 전체" },
                FootContactIntentAutoLabeler.MinSuggestConfidence);
            Assert.That(high, Is.EqualTo("발 전체"));
        }

        [Test]
        public void Given_SuggestedCsv_When_Reading_Then_RoundTripsRowsAndConfidence()
        {
            string path = Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "suggested-labels-test.csv");
            File.WriteAllLines(path, new[]
            {
                FootContactIntentAutoLabeler.SuggestedHeader,
                "10,39,left,발 전체,고정,jev,intent:plant,0.9,0.8",
                "50,51,right,불확실,불확실,jev,uncertain_span,0.1,0.2",
                "bad,line",
            });
            List<FootContactIntentAutoLabeler.SuggestedRow> rows =
                FootContactIntentAutoLabeler.ReadSuggestions(path);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].From, Is.EqualTo(10));
            Assert.That(rows[0].To, Is.EqualTo(39));
            Assert.That(rows[0].IsLeft, Is.True);
            Assert.That(rows[0].Contact, Is.EqualTo("발 전체"));
            Assert.That(rows[0].Motion, Is.EqualTo("고정"));
            Assert.That(rows[0].ContactConf, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(rows[1].IsLeft, Is.False);
            // 없는 파일은 빈 목록.
            Assert.That(FootContactIntentAutoLabeler
                .ReadSuggestions(path + ".missing").Count, Is.EqualTo(0));
            File.Delete(path);
        }

        [Test]
        public void Given_PartialCoverage_When_FilteringSubs_Then_UncoveredSubsRemain()
        {
            // 부모 [0,99]가 [0,49]+[50,99]로 나뉘고 앞쪽만 제안이 기록된
            // 경우 — 부모 포함 검사만 보면 스킵되지만 실제로는 뒤쪽 하위를
            // 다시 질의해야 한다(B1 회귀: 부분 커버가 전체 스킵이 되면 안 됨).
            var subs = new List<FootContactIntentAutoLabeler.SpanJob>
            {
                new FootContactIntentAutoLabeler.SpanJob
                    { From = 0, To = 49, IsLeft = true },
                new FootContactIntentAutoLabeler.SpanJob
                    { From = 50, To = 99, IsLeft = true },
            };
            var rows = new List<FootContactIntentAutoLabeler.SuggestedRow>
            {
                new FootContactIntentAutoLabeler.SuggestedRow
                    { From = 0, To = 29, IsLeft = true },
            };
            var keys = new HashSet<string>();
            var fresh = FootContactIntentAutoLabeler.FilterFreshSubs(
                subs, rows, keys);
            Assert.That(fresh.Count, Is.EqualTo(1));
            Assert.That(fresh[0].From, Is.EqualTo(50));
        }

        [Test]
        public void Given_UncertainNeighbor_When_FindingBounds_Then_Skipped()
        {
            // 불확실 이웃은 추정기 추측이라 bounded_by 맥락에서 제외한다.
            var intents = new[]
            {
                new FootContactIntentAutoLabeler.IntentRowDto
                {
                    side = "left", start_frame = 0, end_frame_exclusive = 100,
                    mode = "plant", certainty = "confident",
                },
                new FootContactIntentAutoLabeler.IntentRowDto
                {
                    side = "left", start_frame = 300, end_frame_exclusive = 400,
                    mode = "plant", certainty = "uncertain",
                },
            };
            FootContactIntentAutoLabeler.FindBounds(intents, "left", 150, 200,
                out var prev, out var next);
            Assert.That(prev, Is.Not.Null);
            Assert.That(prev.mode, Is.EqualTo("plant"));
            // 300~400 의도는 불확실이라 next로 선택되지 않는다.
            Assert.That(next, Is.Null);
        }

        [Test]
        public void Given_NoTouchingAnswers_When_Parsing_Then_ContactUncertain()
        {
            // touching_f* 답변이 없으면 닿음 집계에 표가 없어 불확실로 둔다.
            const string json =
                "{\"model\":\"jev-1.14\",\"answers\":{" +
                "\"contact_f0\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"contact_f1\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"motion\":{\"type\":\"choice\",\"choice\":\"pinned\",\"confidence\":0.8}}," +
                "\"usage\":{}}";
            var result = FootContactIntentAutoLabeler.ParseResponse(
                json, 100, 199, new[] { 100, 199 });
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Contact, Is.EqualTo("불확실"));
        }

        [Test]
        public void Given_BoundaryNotInFrames_When_Parsing_Then_NoSplit()
        {
            // transition 지목이 샘플 프레임 목록에 없는 숫자면 분할하지 않는다.
            const string json =
                "{\"model\":\"jev-1.14\",\"answers\":{" +
                "\"touching_f0\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"touching_f1\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"touching_f2\":{\"type\":\"noul\",\"noul\":0.9}," +
                "\"contact_f0\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"contact_f1\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"contact_f2\":{\"type\":\"choice\",\"choice\":\"whole\",\"confidence\":0.9}," +
                "\"transition_frame\":{\"type\":\"choice\",\"choice\":\"150\",\"confidence\":0.9}," +
                "\"motion\":{\"type\":\"choice\",\"choice\":\"pinned\",\"confidence\":0.8}}," +
                "\"usage\":{}}";
            var result = FootContactIntentAutoLabeler.ParseResponse(
                json, 100, 199, new[] { 100, 140, 199 });
            Assert.That(result.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_HeightCurve_When_FindingSplits_Then_SplitsAtTransitions()
        {
            // 접지(낮음) 40프레임 → 상승 → 공중(높음) 40프레임:
            // 극값 2개(저점·고점)라 중간점 경계 1개로 2구간.
            var twoPhase = new float[80];
            for (int i = 0; i < 40; i++) twoPhase[i] = 0.1f;
            for (int i = 40; i < 60; i++) twoPhase[i] = 0.1f + 0.3f * (i - 40) / 20f;
            for (int i = 60; i < 80; i++) twoPhase[i] = 0.4f;
            var splits = FootContactIntentAutoLabeler.FindSplitOffsets(
                twoPhase, 0.02f, 6, 6);
            Assert.That(splits.Count, Is.EqualTo(1));
            Assert.That(splits[0], Is.InRange(45, 55));
            // 평탄한 곡선은 극값이 없어 분할 없음.
            var flat = new float[60];
            for (int i = 0; i < 60; i++) flat[i] = 0.1f;
            Assert.That(FootContactIntentAutoLabeler.FindSplitOffsets(
                flat, 0.02f, 6, 6).Count, Is.EqualTo(0));
            // 두 번 왕복하는 진동 곡선 → 공중 2회라 3구간(접지-공중-접지 또는 그 반대).
            var wave = new float[120];
            for (int i = 0; i < 120; i++)
                wave[i] = 0.1f + 0.3f * Mathf.Abs(Mathf.Sin(i * Mathf.PI / 30f));
            var waveSplits = FootContactIntentAutoLabeler.FindSplitOffsets(
                wave, 0.02f, 6, 6);
            Assert.That(waveSplits.Count, Is.GreaterThanOrEqualTo(2));
            // 최소 길이 미만 구간이 생기지 않음.
            foreach (int s in waveSplits)
            {
                Assert.That(s, Is.GreaterThanOrEqualTo(6));
                Assert.That(120 - s, Is.GreaterThanOrEqualTo(6));
            }
        }

        private static void MakeTraj(float footY, float toeY, float toeZ,
            out Vector3[] feet, out Vector3[] toes)
        {
            feet = new Vector3[10];
            toes = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                feet[i] = new Vector3(0f, footY, 0f);
                toes[i] = new Vector3(0f, toeY, toeZ);
            }
        }

        [Test]
        public void Given_PinnedSpan_When_RewritingMotion_Then_FillsPinned()
        {
            MakeTraj(0.06f, 0.0f, 0.1f, out var feet, out var toes);
            int changed = 0;
            string result = FootContactIntentAutoLabeler.RewriteMotionLine(
                "1,9,left,발 전체,불확실,jev,uncertain_span,0.8,0.2",
                60f, feet, toes, feet, toes, 0.05f, ref changed);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(result, Does.Contain(",고정,"));
            Assert.That(result, Does.Contain("motion=local"));
        }

        [Test]
        public void Given_RollingSpan_When_RewritingMotion_Then_FillsRoll()
        {
            // 발끝은 낮게 붙고 발목이 발끝 축 위로 상하 요동 — 수직비율이
            // 계속 0.6을 넘고 발목은 움직이므로 고정이 아니라 구르기.
            var feet = new Vector3[10];
            var toes = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                feet[i] = new Vector3(0f, 0.09f + 0.01f * Mathf.Sin(i), 0f);
                toes[i] = new Vector3(0f, 0.0f, 0.1f);
            }
            int changed = 0;
            string result = FootContactIntentAutoLabeler.RewriteMotionLine(
                "1,9,left,앞꿈치,불확실,jev,uncertain_span,0.8,0.2",
                60f, feet, toes, feet, toes, 0.05f, ref changed);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(result, Does.Contain(",구르기,"));
        }

        [Test]
        public void Given_MovingSpan_When_RewritingMotion_Then_StaysUncertain()
        {
            // 발끝이 매 프레임 1cm씩 수평 이동 — 고정도 롤도 임계 미만이라
            // 의도된 이동인지 결함인지 수치로 못 가려 기권을 유지한다.
            var feet = new Vector3[10];
            var toes = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                feet[i] = new Vector3(0.01f * i, 0.06f, 0f);
                toes[i] = new Vector3(0.01f * i, 0.0f, 0.1f);
            }
            int changed = 0;
            string line = "1,9,left,발 전체,불확실,jev,uncertain_span,0.8,0.2";
            string result = FootContactIntentAutoLabeler.RewriteMotionLine(
                line, 60f, feet, toes, feet, toes, 0.05f, ref changed);
            Assert.That(changed, Is.EqualTo(0));
            Assert.That(result, Is.EqualTo(line));
        }

        [Test]
        public void Given_UnknownSide_When_Rewriting_Then_Untouched()
        {
            // left/right 외의 side는 어느 발 궤적으로도 재분류하지 않는다.
            MakeTraj(0.06f, 0.0f, 0.1f, out var feet, out var toes);
            int changed = 0;
            string line = "1,9,both,발 전체,불확실,jev,uncertain_span,0.8,0.2";
            Assert.That(FootContactIntentAutoLabeler.RewriteMotionLine(
                line, 60f, feet, toes, feet, toes, 0.05f, ref changed),
                Is.EqualTo(line));
            Assert.That(changed, Is.EqualTo(0));
        }

        [Test]
        public void Given_AirborneOrResolvedRow_When_Rewriting_Then_Untouched()
        {
            MakeTraj(0.06f, 0.0f, 0.1f, out var feet, out var toes);
            int changed = 0;
            // 공중 접촉은 동작 재분류 대상이 아니다.
            string air = "1,9,left,공중,불확실,jev,uncertain_span,0.8,0.2";
            Assert.That(FootContactIntentAutoLabeler.RewriteMotionLine(
                air, 60f, feet, toes, feet, toes, 0.05f, ref changed),
                Is.EqualTo(air));
            // 이미 동작이 채워진 행도 건드리지 않는다(멱등).
            string done = "1,9,left,발 전체,고정,jev,intent:plant,0.8,0.7";
            Assert.That(FootContactIntentAutoLabeler.RewriteMotionLine(
                done, 60f, feet, toes, feet, toes, 0.05f, ref changed),
                Is.EqualTo(done));
            Assert.That(changed, Is.EqualTo(0));
        }

        [Test]
        public void Given_SuggestedRows_When_CheckingCoverage_Then_InsideRowsBlockRequery()
        {
            var rows = new List<FootContactIntentAutoLabeler.SuggestedRow>
            {
                // 전이 분리로 생긴 부모 범위보다 작은 행.
                new FootContactIntentAutoLabeler.SuggestedRow
                    { From = 400, To = 411, IsLeft = true },
                new FootContactIntentAutoLabeler.SuggestedRow
                    { From = 412, To = 458, IsLeft = true },
            };
            // 전이 행이 안쪽에 있는 부모 스팬 → 재질의 차단.
            Assert.That(FootContactIntentAutoLabeler.HasSuggestionInside(
                rows, 400, 458, true), Is.True);
            // 안쪽 행이 없는 범위 → 통과.
            Assert.That(FootContactIntentAutoLabeler.HasSuggestionInside(
                rows, 500, 600, true), Is.False);
            // 다른 발은 무관.
            Assert.That(FootContactIntentAutoLabeler.HasSuggestionInside(
                rows, 400, 458, false), Is.False);
            // 스팬이 행보다 작으면(부분 겹침) 차단하지 않음.
            Assert.That(FootContactIntentAutoLabeler.HasSuggestionInside(
                rows, 405, 410, true), Is.False);
            // null 목록은 항상 통과.
            Assert.That(FootContactIntentAutoLabeler.HasSuggestionInside(
                null, 0, 100, true), Is.False);
        }

        [Test]
        public void Given_FrontView_When_BuildingRequest_Then_FrontAssetsAndHintWired()
        {
            byte[][] pngs = { new byte[] { 1 }, new byte[] { 2 } };
            byte[][] fronts = { new byte[] { 9 }, new byte[] { 8 } };
            int[] frames = { 10, 20 };
            string json = FootContactIntentAutoLabeler.BuildRequestJson(
                "clip.fbx", "right", 10, 20, frames, pngs, null, null,
                fronts);
            JObject request = JObject.Parse(json);
            JObject assets = (JObject)request["assets"];
            Assert.That(assets.Count, Is.EqualTo(4));
            JObject state = (JObject)request["state"];
            Assert.That(state.ContainsKey("frame_front_image_0"), Is.True);
            Assert.That(state.ContainsKey("frame_front_image_1"), Is.True);
            // state의 앞면 참조가 실제 자산 키와 일치해야 한다.
            foreach (var entry in state)
            {
                if (entry.Key.StartsWith("frame_"))
                {
                    Assert.That(assets.ContainsKey((string)entry.Value),
                        Is.True, entry.Key);
                }
            }
            // 앞면 뷰가 있을 때만 두 각도 참고 지시가 들어간다.
            JObject questions = (JObject)request["questions"];
            Assert.That(questions["transition_frame"].Value<string>("instructions"),
                Does.Contain("frame_front_image_"));
            Assert.That(questions["touching_f0"]
                .Value<string>("instructions"),
                Does.Contain("frame_front_image_"));
            // 앞면이 없으면 지시·자산 모두 생기지 않는다(하위호환).
            string single = FootContactIntentAutoLabeler.BuildRequestJson(
                "clip.fbx", "right", 10, 20, frames, pngs, null, null);
            JObject singleReq = JObject.Parse(single);
            Assert.That(singleReq["questions"]["transition_frame"].Value<string>("instructions"),
                Does.Not.Contain("frame_front_image_"));
            Assert.That(((JObject)singleReq["state"])
                .ContainsKey("frame_front_image_0"), Is.False);
            Assert.That(singleReq["questions"]["touching_f0"]
                .Value<string>("instructions"),
                Does.Not.Contain("frame_front_image_"));
        }

        [Test]
        public void Given_Suggestions_When_SelectingRequery_Then_OnlyUnresolvedUnmarked()
        {
            var rows = new List<FootContactIntentAutoLabeler.SuggestedRow>
            {
                // 접촉 불확실 → 대상.
                new FootContactIntentAutoLabeler.SuggestedRow
                {
                    From = 0, To = 9, IsLeft = true, SideKnown = true,
                    Contact = "불확실", Motion = "불확실",
                },
                // 접촉 해소·동작 불확실 → 대상.
                new FootContactIntentAutoLabeler.SuggestedRow
                {
                    From = 10, To = 19, IsLeft = false, SideKnown = true,
                    Contact = "발 전체", Motion = "불확실",
                },
                // 이미 재질의한 행 → 제외.
                new FootContactIntentAutoLabeler.SuggestedRow
                {
                    From = 20, To = 29, IsLeft = true, SideKnown = true,
                    Contact = "불확실", Motion = "불확실",
                    Notes = "uncertain_span|requery_v2",
                },
                // 둘 다 해소 → 제외.
                new FootContactIntentAutoLabeler.SuggestedRow
                {
                    From = 30, To = 39, IsLeft = true, SideKnown = true,
                    Contact = "발 전체", Motion = "고정",
                },
                // side 불명 → 잘못된 발 촬영 위험이라 제외.
                new FootContactIntentAutoLabeler.SuggestedRow
                {
                    From = 40, To = 49, IsLeft = true, SideKnown = false,
                    Contact = "불확실", Motion = "불확실",
                },
                new FootContactIntentAutoLabeler.SuggestedRow
                {
                    From = 50, To = 59, IsLeft = true, SideKnown = true,
                    Contact = "공중", Motion = "불확실",
                },
            };
            var targets = FootContactIntentAutoLabeler.SelectRequeryRows(rows);
            Assert.That(targets.Count, Is.EqualTo(2));
            Assert.That(targets[0].From, Is.EqualTo(0));
            Assert.That(targets[1].From, Is.EqualTo(10));
            // null과 빈 목록도 안전.
            Assert.That(FootContactIntentAutoLabeler
                .SelectRequeryRows(null).Count, Is.EqualTo(0));
        }

        [Test]
        public void Given_Csv_When_Reading_Then_NotesReviewerSideKept()
        {
            string path = Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "suggested-notes-test.csv");
            File.WriteAllLines(path, new[]
            {
                FootContactIntentAutoLabeler.SuggestedHeader,
                "1,9,left,불확실,불확실,jev,uncertain_span|motion=local,0.3,0.1",
                "10,19,both,불확실,불확실,jev,uncertain_span,0.2,0.1",
            });
            var rows = FootContactIntentAutoLabeler.ReadSuggestions(path);
            Assert.That(rows[0].Reviewer, Is.EqualTo("jev"));
            Assert.That(rows[0].Notes,
                Is.EqualTo("uncertain_span|motion=local"));
            Assert.That(rows[0].SideKnown, Is.True);
            // 불명 side는 IsLeft=false로 떨어지되 SideKnown으로 구분된다.
            Assert.That(rows[1].SideKnown, Is.False);
            File.Delete(path);
        }

        [Test]
        public void Given_RequeryMerge_When_Merging_Then_TargetReplacedOthersKept()
        {
            var lines = new List<string>
            {
                FootContactIntentAutoLabeler.SuggestedHeader,
                "1,9,left,불확실,불확실,jev,uncertain_span,0.3,0.1",
                "10,19,right,발 전체,고정,jev,intent:plant,0.9,0.8",
                "20,29,left,불확실,불확실,jev,uncertain_span,0.2,0.1",
            };
            var repl = new Dictionary<string, List<string>>
            {
                // 전이 분리로 원본 1행이 2행 치환되는 경우도 한 키로 처리.
                [FootContactIntentAutoLabeler.RowKey(1, 9, true)] =
                    new List<string>
                    {
                        "1,5,left,발 전체,고정,jev,uncertain_span|requery_v2|t=6,0.9,0.8",
                        "6,9,left,공중,불확실,jev,uncertain_span|requery_v2|t=6,0.9,0.1",
                    },
                [FootContactIntentAutoLabeler.RowKey(20, 29, true)] =
                    new List<string>
                    {
                        "20,29,left,뒤꿈치,불확실,jev,uncertain_span|requery_v2,0.8,0.1",
                    },
            };
            var output = new System.Text.StringBuilder();
            int merged = FootContactIntentAutoLabeler.MergeRequeryLines(
                lines, repl, output);
            Assert.That(merged, Is.EqualTo(2));
            string[] result = SplitLines(output.ToString());
            // 헤더 + 치환 2행 + 보존 1행 + 치환 1행.
            Assert.That(result.Length, Is.EqualTo(5));
            // 대상 아닌 행은 원문 그대로 보존.
            Assert.That(result[3], Is.EqualTo(lines[2]));
            // 치환 행에는 재질의 마커가 달린다.
            Assert.That(result[1], Does.Contain("requery_v2"));
            Assert.That(result[4], Does.Contain("requery_v2"));
        }

        // AppendLine의 CRLF를 끊고 빈 꼬리를 제외한 줄 배열.
        private static string[] SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\n" },
                    System.StringSplitOptions.RemoveEmptyEntries);

        [Test]
        public void Given_DuplicateKeyRows_When_Merging_Then_OnlyFirstReplaced()
        {
            // 원본에 같은 (from,to,side) 행이 중복돼 있으면 첫 행만
            // 치환하고 나머지는 원문 유지 — 결과 복제를 막는다.
            var lines = new List<string>
            {
                FootContactIntentAutoLabeler.SuggestedHeader,
                "1,9,left,불확실,불확실,jev,a,0.3,0.1",
                "1,9,left,불확실,불확실,jev,b,0.3,0.1",
            };
            var repl = new Dictionary<string, List<string>>
            {
                [FootContactIntentAutoLabeler.RowKey(1, 9, true)] =
                    new List<string> { "1,9,left,발 전체,고정,jev,c,0.9,0.9" },
            };
            var output = new System.Text.StringBuilder();
            Assert.That(FootContactIntentAutoLabeler.MergeRequeryLines(
                lines, repl, output), Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain(",jev,b,"));
        }

        [Test]
        public void Given_MalformedLines_When_Merging_Then_PassedThrough()
        {
            var lines = new List<string>
            {
                FootContactIntentAutoLabeler.SuggestedHeader,
                "\"quoted,cell\",9,left,불확실,불확실,jev,n,0.3,0.1",
                "not,a,csv,row",
                "1,9,both,불확실,불확실,jev,n,0.3,0.1",
            };
            var repl = new Dictionary<string, List<string>>
            {
                [FootContactIntentAutoLabeler.RowKey(1, 9, true)] =
                    new List<string> { "1,9,left,발 전체,고정,jev,n,0.9,0.9" },
            };
            var output = new System.Text.StringBuilder();
            int merged = FootContactIntentAutoLabeler.MergeRequeryLines(
                lines, repl, output);
            // both 행은 side 불명이라 키가 안 나와 치환되지 않는다.
            Assert.That(merged, Is.EqualTo(0));
            Assert.That(SplitLines(output.ToString()).Length, Is.EqualTo(4));
        }

        [Test]
        public void Given_Notes_When_AppendingMarker_Then_PipeJoined()
        {
            Assert.That(FootContactIntentAutoLabeler.AppendNote(
                null, "requery_v2"), Is.EqualTo("requery_v2"));
            Assert.That(FootContactIntentAutoLabeler.AppendNote(
                "uncertain_span", "requery_v2"),
                Is.EqualTo("uncertain_span|requery_v2"));
        }

        [Test]
        public void Given_LockedCsv_When_FinishingRequery_Then_ResponseBufferSurvives()
        {
            string folder = Path.Combine(Application.temporaryCachePath,
                "jev-recovery-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string csv = Path.Combine(folder, "suggested-labels.csv");
            string original = FootContactIntentAutoLabeler.SuggestedHeader + "\n" +
                "1,9,left,불확실,불확실,jev,a,0.3,0.1\n";
            File.WriteAllText(csv, original);
            var window = ScriptableObject.CreateInstance<FootContactIntentAutoLabeler>();
            var flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            var type = typeof(FootContactIntentAutoLabeler);
            type.GetField("_csvPath", flags).SetValue(window, csv);
            type.GetField("_requeryOriginal", flags).SetValue(window, original);
            type.GetField("_requeryMode", flags).SetValue(window, true);
            var buffer = (Dictionary<string, List<string>>)type
                .GetField("_requeryLines", flags).GetValue(window);
            buffer.Add("1:9:left", new List<string>
            {
                "1,9,left,발 전체,고정,jev,requery_v2,0.9,0.9",
            });
            try
            {
                using (var locked = new FileStream(csv, FileMode.Open,
                    FileAccess.ReadWrite, FileShare.None))
                {
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                        new System.Text.RegularExpressions.Regex("IOException"));
                    type.GetMethod("StopRun", flags).Invoke(window,
                        new object[] { false, "처리 종료" });
                    Assert.That(buffer.Count, Is.EqualTo(1),
                        "저장 실패 때 성공한 응답을 버리면 재호출 비용이 발생함");
                    Assert.That(File.Exists(csv + ".requery-pending.json"), Is.True);
                    string status = (string)type.GetField("_status", flags).GetValue(window);
                    Assert.That(status, Does.Contain("처리 종료").And.Contain("저장 실패"));
                    Assert.That(type.GetMethod("BlockPendingRequery", flags)
                        .Invoke(window, null), Is.True);
                }
                Assert.That(File.ReadAllText(csv), Is.EqualTo(original));
                var reopened = ScriptableObject.CreateInstance<FootContactIntentAutoLabeler>();
                try
                {
                    type.GetField("_intentFiles", flags).SetValue(reopened,
                        new List<string> { Path.Combine(folder, "contact-intent.json") });
                    Assert.That(Path.GetFullPath((string)type.GetMethod("PendingCsvPath", flags)
                        .Invoke(reopened, null)), Is.EqualTo(Path.GetFullPath(csv)));
                    type.GetMethod("ScanDefects", flags).Invoke(reopened, null);
                    Assert.That(type.GetField("_status", flags).GetValue(reopened),
                        Does.Contain("미저장 재질의 결과"));
                }
                finally { Object.DestroyImmediate(reopened); }
                type.GetMethod("RetryRequerySave", flags).Invoke(window, null);
                Assert.That(buffer, Is.Empty);
                Assert.That(File.Exists(csv + ".requery-pending.json"), Is.False);
                Assert.That(File.ReadAllText(csv), Does.Contain("requery_v2"));
            }
            finally
            {
                buffer.Clear();
                Object.DestroyImmediate(window);
                Directory.Delete(folder, true);
            }
        }

        [TestCase("normal")]
        [TestCase("locked")]
        [TestCase("changed")]
        [TestCase("already_applied")]
        [TestCase("corrupt")]
        [TestCase("deleted")]
        public void Given_Checkpoint_When_Recovering_Then_PreservesOtherWork(string failure)
        {
            string folder = Path.Combine(Application.temporaryCachePath,
                "jev-checkpoint-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string csv = Path.Combine(folder, "suggested-labels.csv");
            string pending = csv + ".requery-pending.json";
            string human = Path.Combine(folder, "human-labels.csv");
            string original = FootContactIntentAutoLabeler.SuggestedHeader + "\n" +
                "1,9,left,불확실,불확실,jev,a,0.3,0.1\n" +
                "20,29,right,발 전체,고정,human,keep,1,1";
            var replacements = new Dictionary<string, List<string>>
            {
                ["1:9:left"] = new List<string>
                {
                    "1,4,left,발 전체,고정,jev,requery_v2,0.9,0.9",
                    "5,9,left,공중,불확실,jev,requery_v2,0.9,0",
                },
            };
            try
            {
                File.WriteAllText(csv, original);
                File.WriteAllText(human, "사람 확정 내용 보존");
                FootContactIntentAutoLabeler.SaveRequeryCheckpoint(csv, original, replacements);
                if (failure == "locked")
                {
                    using (var locked = new FileStream(csv, FileMode.Open,
                        FileAccess.ReadWrite, FileShare.None))
                        Assert.Throws<IOException>(() =>
                            FootContactIntentAutoLabeler.ApplyRequeryCheckpoint(csv));
                    Assert.That(File.ReadAllText(csv), Is.EqualTo(original));
                    Assert.That(File.Exists(pending), Is.True);
                }
                if (failure == "changed")
                {
                    File.AppendAllText(csv, "\n다른 작업의 변경");
                    string changed = File.ReadAllText(csv);
                    Assert.Throws<IOException>(() =>
                        FootContactIntentAutoLabeler.ApplyRequeryCheckpoint(csv));
                    Assert.That(File.ReadAllText(csv), Is.EqualTo(changed));
                    Assert.That(File.Exists(pending), Is.True);
                    return;
                }
                if (failure == "corrupt")
                {
                    JObject document = JObject.Parse(File.ReadAllText(pending));
                    document["result"] = "손상된 결과";
                    File.WriteAllText(pending, document.ToString());
                    Assert.Throws<InvalidDataException>(() =>
                        FootContactIntentAutoLabeler.ApplyRequeryCheckpoint(csv));
                    Assert.That(File.ReadAllText(csv), Is.EqualTo(original));
                    Assert.That(File.Exists(pending), Is.True);
                    return;
                }
                if (failure == "deleted")
                {
                    File.Delete(csv);
                    Assert.Throws<FileNotFoundException>(() =>
                        FootContactIntentAutoLabeler.ApplyRequeryCheckpoint(csv));
                    Assert.That(File.Exists(pending), Is.True);
                    File.WriteAllText(csv, original);
                }
                if (failure == "already_applied")
                    File.WriteAllText(csv, JObject.Parse(File.ReadAllText(pending))
                        .Value<string>("result"));
                Assert.That(FootContactIntentAutoLabeler.ApplyRequeryCheckpoint(csv), Is.EqualTo(1));
                Assert.That(File.Exists(pending), Is.False);
                string saved = File.ReadAllText(csv);
                Assert.That(saved, Does.Contain("20,29,right,발 전체,고정,human,keep,1,1"));
                Assert.That(SplitLines(saved).Length, Is.EqualTo(4));
                Assert.That(FootContactIntentAutoLabeler.SelectRequeryRows(
                    FootContactIntentAutoLabeler.ReadSuggestions(csv)), Is.Empty);
            }
            finally
            {
                Assert.That(File.ReadAllText(human), Is.EqualTo("사람 확정 내용 보존"));
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void Given_DuplicateAndNegativeFrames_When_SelectingRequery_Then_Excluded()
        {
            var rows = new List<FootContactIntentAutoLabeler.SuggestedRow>();
            foreach (int start in new[] { -1, 1, 1 })
                rows.Add(new FootContactIntentAutoLabeler.SuggestedRow
                {
                    From = start, To = 9, IsLeft = true, SideKnown = true,
                    Contact = "불확실", Motion = "불확실",
                });
            Assert.That(FootContactIntentAutoLabeler.SelectRequeryRows(rows), Is.Empty);
        }
    }
}
