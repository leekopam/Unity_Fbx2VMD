using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Editor.Common
{
    /// <summary>
    /// 코루틴 이너레이터를 EditMode 테스트에서 수동으로 구동하는 도구임.
    /// Unity의 코루틴 완료·외부 중단 경로를 재현해, 물리 검증·계측 코루틴의
    /// 생명주기 안전(예외·중단·파괴 시 상태 복원) 회귀 테스트에 씀.
    /// </summary>
    internal static class EditorCoroutinePump
    {
        /// <summary>
        /// 이너레이터를 최대 maxSteps 단계까지 구동함.
        /// 완료(MoveNext가 false)면 true, 단계 한도 도달이면 false를 반환함.
        /// </summary>
        internal static bool RunToCompletion(IEnumerator routine, int maxSteps = 100000)
        {
            if (routine == null)
            {
                return true;
            }

            for (int step = 0; step < maxSteps; step++)
            {
                if (!routine.MoveNext())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 이너레이터를 steps 단계만큼만 구동하고 남은 상태로 둠.
        /// 도중 중단 시나리오의 전단계로 씀.
        /// </summary>
        internal static void Step(IEnumerator routine, int steps)
        {
            for (int index = 0; index < steps && routine.MoveNext(); index++)
            {
            }
        }

        /// <summary>
        /// Unity의 코루틴 외부 중단(Stop, 오브젝트 비활성·파괴)과 동일하게,
        /// 현재 활성 이너레이터 체인을 안쪽부터 Dispose해 각 단계의 finally를 실행함.
        /// yield로 중첩된 이너레이터도 함께 내려가므로 내부 finally까지 보장됨.
        /// </summary>
        internal static void Abort(IEnumerator routine)
        {
            if (routine == null)
            {
                return;
            }

            var chain = new Stack<IEnumerator>();
            IEnumerator current = routine;
            while (current != null)
            {
                chain.Push(current);
                current = current.Current as IEnumerator;
            }

            while (chain.Count > 0)
            {
                (chain.Pop() as IDisposable)?.Dispose();
            }
        }
    }
}
