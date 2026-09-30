using System.Collections;
using NUnit.Framework;

namespace Tests.Editor.CoroutineLifecycle
{
    /// <summary>
    /// 코루틴 구동 컴포넌트의 생명주기 불변식을 검증하는 테스트 템플릿임.
    /// 완료·Stop·외부 중단(파괴/비활성) 3경로에서 "시작 전 상태로 복원됨"을
    /// 공통으로 검증한다. 물리 검증·계측 컴포넌트가 재구현되면 Fixture만
    /// 실제 컴포넌트로 교체해 같은 검증을 적용함.
    /// </summary>
    public class CoroutineLifecycleHarnessTests
    {
        // 실제 컴포넌트 적용 시 이 픽스처 대신 컴포넌트+릭(Animator·설정 등)을 만들고
        // 아래 테스트의 캡처/복원 대상만 바꾸면 됨.
        private sealed class Fixture
        {
            internal bool AnimatorSuppressed;
            internal bool BonesMoved;
            internal bool IsRunning;
            internal EditorCoroutinePump.Session Pump;

            internal void Run()
            {
                if (IsRunning)
                {
                    return;
                }

                IsRunning = true;
                Pump = EditorCoroutinePump.Start(Sequence());
            }

            internal void Stop()
            {
                Restore();
                IsRunning = false;
                Pump = null;
            }

            // 외부 중단 경로 — Unity가 코루틴을 죽일 때 finally가 상태를 복원해야 함.
            internal void Abort() => Pump?.Abort();

            private IEnumerator Sequence()
            {
                AnimatorSuppressed = true;
                BonesMoved = true;
                try
                {
                    yield return null; // 단계 1
                    yield return null; // 단계 2
                }
                finally
                {
                    Restore();
                    IsRunning = false;
                    Pump = null;
                }
            }

            private void Restore()
            {
                AnimatorSuppressed = false;
                BonesMoved = false;
            }
        }

        [Test]
        public void 완료_경로에서_상태가_복원된다()
        {
            var fixture = new Fixture();
            fixture.Run();
            Assert.That(fixture.Pump.RunToCompletion(), Is.True);
            Assert.That(fixture.IsRunning, Is.False);
            Assert.That(fixture.AnimatorSuppressed, Is.False);
            Assert.That(fixture.BonesMoved, Is.False);
        }

        [Test]
        public void Stop_경로에서_상태가_복원된다()
        {
            var fixture = new Fixture();
            fixture.Run();
            fixture.Pump.Step(1);
            fixture.Stop();
            Assert.That(fixture.IsRunning, Is.False);
            Assert.That(fixture.AnimatorSuppressed, Is.False);
            Assert.That(fixture.BonesMoved, Is.False);
        }

        [Test]
        public void 외부_중단_경로에서도_finally가_상태를_복원한다()
        {
            var fixture = new Fixture();
            fixture.Run();
            fixture.Pump.Step(1);
            fixture.Abort();
            Assert.That(fixture.IsRunning, Is.False);
            Assert.That(fixture.AnimatorSuppressed, Is.False);
            Assert.That(fixture.BonesMoved, Is.False);
        }

        [Test]
        public void 중첩_이너레이터의_finally도_중단_시_실행된다()
        {
            // 중첩 yield가 있는 코루틴의 안쪽 finally까지 Abort가 내려가는지 검증함.
            bool innerCleaned = false;
            bool outerCleaned = false;
            IEnumerator Inner()
            {
                try { yield return null; }
                finally { innerCleaned = true; }
            }
            IEnumerator Outer()
            {
                try { yield return Inner(); }
                finally { outerCleaned = true; }
            }

            var session = EditorCoroutinePump.Start(Outer());
            session.Step(2);
            session.Abort();
            Assert.That(outerCleaned, Is.True, "바깥 finally가 실행되어야 함");
            Assert.That(innerCleaned, Is.True, "안쪽 finally도 실행되어야 함");
        }
    }
}
