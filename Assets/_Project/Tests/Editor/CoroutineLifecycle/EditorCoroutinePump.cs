using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Editor.CoroutineLifecycle
{
    /// <summary>
    /// EditMode에서 코루틴(중첩 이너레이터 포함)을 수동으로 구동하는 펌프임.
    /// Start로 세션을 만들면 활성 이너레이터 체인을 추적하고, Abort가
    /// 안쪽부터 Dispose해 Unity의 코루틴 중단(finally 실행) 동작을 재현함.
    /// </summary>
    internal static class EditorCoroutinePump
    {
        internal static Session Start(IEnumerator routine)
        {
            var session = new Session(routine);
            return session;
        }

        internal sealed class Session
        {
            private readonly Stack<IEnumerator> _active = new Stack<IEnumerator>();
            private bool _finished;

            internal Session(IEnumerator routine)
            {
                if (routine != null)
                {
                    _active.Push(routine);
                }
            }

            internal bool IsFinished => _finished;

            /// <summary>중첩 구조를 따라 최대 steps번 MoveNext함. 실제 진행 횟수를 반환함.</summary>
            internal int Step(int steps)
            {
                int done = 0;
                while (_active.Count > 0 && done < steps)
                {
                    IEnumerator current = _active.Peek();
                    bool advanced;
                    try
                    {
                        advanced = current.MoveNext();
                    }
                    catch
                    {
                        DisposeInsideOut();
                        throw;
                    }
                    if (!advanced)
                    {
                        _active.Pop();
                        continue;
                    }
                    if (current.Current is IEnumerator nested)
                    {
                        _active.Push(nested);
                    }
                    done++;
                }
                if (_active.Count == 0) _finished = true;
                return done;
            }

            /// <summary>코루틴이 끝까지 돌았으면 true를 반환함. 예외는 호출자에게 전파됨.</summary>
            internal bool RunToCompletion()
            {
                while (_active.Count > 0)
                {
                    Step(int.MaxValue - 1);
                }
                return true;
            }

            /// <summary>
            /// 외부 중단을 재현함 — 활성 이너레이터를 안쪽부터 Dispose해
            /// 각 단계의 finally가 실행되게 함.
            /// </summary>
            internal void Abort()
            {
                DisposeInsideOut();
                _finished = true;
            }

            private void DisposeInsideOut()
            {
                while (_active.Count > 0)
                {
                    IEnumerator current = _active.Pop();
                    try
                    {
                        (current as IDisposable)?.Dispose();
                    }
                    catch
                    {
                        // 정리 중 예외는 삼킴.
                    }
                }
            }
        }
    }
}
