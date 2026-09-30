using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Fbx2Vmd.ClothPhysics;
using Fbx2Vmd.Profiling;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// PhysicsValidationSequence·PhysicsPerfRunner 코루틴 생명주기 검증.
    /// 예외·중단 경로에서도 Animator/본/실행 플래그/프로파일 런이 복원되어야 한다.
    /// 수동 MoveNext 펌프로 EditMode에서 코루틴을 구동한다.
    /// </summary>
    public class PhysicsCoroutineLifecycleTests
    {
        static readonly MethodInfo RunSequenceMethod =
            typeof(PhysicsValidationSequence).GetMethod(
                "RunSequence", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo RunMeasurementMethod =
            typeof(PhysicsPerfRunner).GetMethod(
                "RunMeasurement", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            PipelineRunProfiler.AbortRun();
            foreach (var go in created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            created.Clear();
        }

        GameObject NewObject(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            created.Add(go);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        static IEnumerator StartSequence(PhysicsValidationSequence seq)
        {
            return (IEnumerator)RunSequenceMethod.Invoke(seq, null);
        }

        /// <summary>중첩 코루틴(yield return IEnumerator)을 Unity 러너처럼 스택으로 펌프한다.</summary>
        static void PumpAll(IEnumerator routine, int maxSteps = 100000)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            int steps = 0;
            while (stack.Count > 0)
            {
                Assert.Less(steps++, maxSteps,
                    "코루틴 펌프 한도 초과 — Time.deltaTime이 0이면 RunPhase가 끝나지 않습니다.");
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
        }

        /// <summary>Head·Hips만 있는 최소 Generic 리그 — Setup()의 이름 패턴 폴백을 통과한다.</summary>
        GameObject BuildMinimalRig(out CharacterPhysicsSetup setup, out Animator animator)
        {
            var root = NewObject("Rig");
            animator = root.AddComponent<Animator>();
            var hips = NewObject("Hips", root.transform);
            NewObject("Head", hips.transform);
            setup = root.AddComponent<CharacterPhysicsSetup>();
            setup.targetAnimator = animator;
            return root;
        }

        [Test]
        public void Given_RootDestroyedMidRun_When_Disposed_Then_FinallyRestoresState()
        {
            var animGo = NewObject("ExternalAnimator");
            var animator = animGo.AddComponent<Animator>();
            var root = NewObject("Model");
            var setup = root.AddComponent<CharacterPhysicsSetup>();
            setup.targetAnimator = animator;
            var seq = root.AddComponent<PhysicsValidationSequence>();
            seq.setup = setup;
            seq.phaseDuration = 0.5f;

            IEnumerator routine = StartSequence(seq);
            Assert.IsTrue(routine.MoveNext(), "첫 yield까지 실행되어야 합니다.");
            Assert.IsTrue(seq.isRunning);
            Assert.IsFalse(animator.enabled, "시퀀스 시작 시 Animator가 비활성화되어야 합니다.");

            var firstPhase = routine.Current as IEnumerator;
            Assert.IsNotNull(firstPhase, "첫 yield는 몸 회전 RunPhase여야 합니다.");
            Assert.IsTrue(firstPhase.MoveNext(), "회전이 한 스텝은 적용되어야 합니다.");
            Assert.Greater(Quaternion.Angle(root.transform.rotation, Quaternion.identity), 0.001f);

            // 시퀀스 도중 루트 파괴 — 다음 스텝의 transform 접근에서 MissingReferenceException 유발.
            // Unity는 코루틴 사망 시 enumerator를 Dispose하며 이 때 finally가 실행된다.
            Object.DestroyImmediate(root);
            Assert.Throws<MissingReferenceException>(() => firstPhase.MoveNext());
            (routine as IDisposable)?.Dispose();

            Assert.IsFalse(seq.isRunning, "finally가 실행 플래그를 리셋해야 합니다.");
            Assert.IsTrue(animator.enabled, "finally가 비활성화한 Animator를 복원해야 합니다.");
            Assert.That(seq.lastReport, Does.Contain("중단"),
                "중단 경로도 lastReport를 남겨 진단 가능해야 합니다.");
        }

        [Test]
        public void Given_SequencePumpedToEnd_Then_CompletesAndRestoresAnimatorAndBones()
        {
            var root = BuildMinimalRig(out var setup, out var animator);
            var seq = root.AddComponent<PhysicsValidationSequence>();
            seq.setup = setup;
            seq.phaseDuration = 0.001f;
            seq.armSwingCount = 1;

            IEnumerator routine = StartSequence(seq);
            PumpAll(routine);

            Assert.IsFalse(seq.isRunning);
            Assert.IsTrue(animator.enabled, "완료 후 Animator가 다시 활성화되어야 합니다.");
            Assert.That(seq.lastReport, Does.Contain("완료"));
            Assert.Less(Quaternion.Angle(root.transform.rotation, Quaternion.identity), 0.01f,
                "시퀀스가 루트 회전을 원위치로 복원해야 합니다.");
        }

        [Test]
        public void Given_RunningSequence_When_Stopped_Then_BonesAndAnimatorRestored()
        {
            var root = BuildMinimalRig(out var setup, out var animator);
            var seq = root.AddComponent<PhysicsValidationSequence>();
            seq.setup = setup;
            seq.phaseDuration = 0.5f;

            IEnumerator routine = StartSequence(seq);
            Assert.IsTrue(routine.MoveNext());
            var firstPhase = routine.Current as IEnumerator;
            Assert.IsTrue(firstPhase.MoveNext(), "회전이 한 스텝은 적용되어야 합니다.");
            Assert.IsFalse(animator.enabled);
            Assert.Greater(Quaternion.Angle(root.transform.rotation, Quaternion.identity), 0.001f,
                "회전이 실제로 적용된 상태에서 중단을 검증해야 합니다.");

            seq.Stop();
            (routine as IDisposable)?.Dispose();

            Assert.IsFalse(seq.isRunning);
            Assert.IsTrue(animator.enabled, "Stop()이 Animator를 복원해야 합니다.");
            Assert.Less(Quaternion.Angle(root.transform.rotation, Quaternion.identity), 0.01f,
                "Stop()이 이동된 본을 원위치시켜야 합니다.");
        }

        [Test]
        public void Given_RunningMeasurement_When_Stopped_Then_SettingsRestoredAndNotWedged()
        {
            var root = BuildMinimalRig(out var setup, out _);
            setup.usePreBuild = false;
            var runner = root.AddComponent<PhysicsPerfRunner>();
            runner.setup = setup;
            runner.warmupFrames = 10;
            runner.measureFrames = 30;
            runner.buildTimeoutFrames = 10;

            var routine = (IEnumerator)RunMeasurementMethod.Invoke(runner, null);
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            for (int i = 0; i < 4 && stack.Count > 0; i++)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
            Assert.IsTrue(runner.isRunning);
            Assert.IsTrue(setup.usePreBuild, "PreBuild 패스가 usePreBuild를 true로 패치해야 합니다.");

            runner.Stop();
            // Unity는 코루틴 중단 시 각 enumerator를 Dispose한다 — 스택의 중첩 열거자도 정리
            while (stack.Count > 0)
                (stack.Pop() as IDisposable)?.Dispose();

            Assert.IsFalse(runner.isRunning, "중단 후 isRunning이 남으면 Run()이 영구 무력화됩니다.");
            Assert.IsFalse(setup.usePreBuild, "중단 경로에서도 usePreBuild가 원복되어야 합니다.");
        }

        [Test]
        public void Given_MeasurementPumpedToEnd_Then_CompletesAndRestoresSettings()
        {
            var root = BuildMinimalRig(out var setup, out _);
            setup.usePreBuild = false;
            var runner = root.AddComponent<PhysicsPerfRunner>();
            runner.setup = setup;
            runner.warmupFrames = 10;
            runner.measureFrames = 30;
            runner.buildTimeoutFrames = 10;

            var routine = (IEnumerator)RunMeasurementMethod.Invoke(runner, null);
            PumpAll(routine);

            Assert.IsFalse(runner.isRunning);
            Assert.IsFalse(setup.usePreBuild, "완료 후 usePreBuild가 원복되어야 합니다.");
            Assert.That(runner.lastSummary, Does.Contain("리포트"));

            string report = PipelineRunProfiler.LastWrittenReportPath;
            if (!string.IsNullOrEmpty(report) && File.Exists(report))
                File.Delete(report);
        }
    }
}
