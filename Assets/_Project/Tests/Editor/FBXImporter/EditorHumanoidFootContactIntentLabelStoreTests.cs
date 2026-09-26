using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorHumanoidFootContactIntentLabelStoreTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type StoreType = typeof(FBXVmdPipeline).Assembly
            .GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootContactIntentLabelStore", true);
        private static readonly Type LabelType = typeof(FBXVmdPipeline).Assembly
            .GetType("Fbx2Vmd.FBXImporter.HumanoidFootContactIntentLabel", true);

        [Test]
        public void Given_ValidKoreanRow_When_Parsing_Then_ReadsSpanSideAndMode()
        {
            // human-labels.csv 서식: from,to,side,contact_label,motion_label,reviewer,notes
            object[] parsed = ParseRowArgs("12,40,left,발 전체,고정,reviewer,메모");
            object label = parsed[2];
            Assert.That((bool)parsed[1], Is.True);
            Assert.That(Property(label, "StartFrame"), Is.EqualTo(12));
            Assert.That(Property(label, "EndFrameInclusive"), Is.EqualTo(40));
            Assert.That((bool?)Property(label, "IsSupport"), Is.True);
            Assert.That(Property(label, "Mode").ToString(), Is.EqualTo("Plant"));
        }

        [Test]
        public void Given_AirborneAndSlideLabels_When_Parsing_Then_MapsToIntentDomain()
        {
            object airborne = ParseRow("5,9,right,공중,불확실,reviewer,");
            Assert.That((bool?)Property(airborne, "IsSupport"), Is.False);
            Assert.That(Property(airborne, "Mode"), Is.Null);
            // 의도된 이동은 수평 활주로 해석함.
            object slide = ParseRow("5,9,right,발 전체,의도된 이동,reviewer,");
            Assert.That(Property(slide, "Mode").ToString(), Is.EqualTo("Slide"));
            // 굴러가는 접촉은 앵커가 수평 고정이므로 Plant로 해석함.
            object roll = ParseRow("5,9,right,뒤꿈치,구르기,reviewer,");
            Assert.That((bool?)Property(roll, "IsSupport"), Is.True);
            Assert.That(Property(roll, "Mode").ToString(), Is.EqualTo("Plant"));
        }

        [Test]
        public void Given_InvalidOrUnlabeledRows_When_Parsing_Then_Rejects()
        {
            // 헤더, 미기록 행, 역전 범위, 알 수 없는 발은 표식으로 쓰지 않음.
            Assert.That(TryParse("from_frame,to_frame,side,contact_label,motion_label,reviewer,notes"),
                Is.False);
            Assert.That(TryParse("10,20,left,,,,모두 미기록"), Is.False);
            Assert.That(TryParse("20,10,left,발 전체,고정,a,b"), Is.False);
            Assert.That(TryParse("10,20,both,발 전체,고정,a,b"), Is.False);
            // 접촉·이동 표식이 모두 알 수 없는 값이면 행을 버림.
            Assert.That(TryParse("10,20,left,없는표식,없는모드,a,b"), Is.False);
            // 모드만 유효해도 접촉 판정을 바꾸지 않는 모드 오버라이드로 받아들임.
            Assert.That(TryParse("10,20,left,없는표식,의도된 이동,a,b"), Is.True);
        }

        private static bool TryParse(string line)
        {
            object[] args = { line, null, null };
            return (bool)StoreType.GetMethod("TryParseRow", Flags).Invoke(null, args);
        }

        private static object ParseRow(string line)
        {
            object[] args = { line, null, null };
            Assert.That(StoreType.GetMethod("TryParseRow", Flags).Invoke(null, args),
                Is.True, line);
            return args[2];
        }

        private static object[] ParseRowArgs(string line)
        {
            object[] args = { line, null, null };
            Assert.That(StoreType.GetMethod("TryParseRow", Flags).Invoke(null, args),
                Is.True, line);
            return args;
        }

        private static object Property(object instance, string name) =>
            LabelType.GetProperty(name, Flags).GetValue(instance);
    }
}
