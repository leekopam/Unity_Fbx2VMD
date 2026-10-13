using NUnit.Framework;
using RootMotion.FinalIK;
using UnityEngine;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// Final IK 기능 계약. 다리/팔 IK 경로가 IKSolverTrigonometric과 LimbIK 계열에
    /// 의존하므로 에셋 버전이 런타임과 맞지 않으면 여기서 드러난다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class FinalIkContractTests
    {
        private GameObject _root;
        private Transform _bone1;
        private Transform _bone2;
        private Transform _bone3;

        [SetUp]
        public void BuildThreeBoneChain()
        {
            _root = new GameObject("FIK_ContractRoot");
            _bone1 = new GameObject("bone1").transform;
            _bone2 = new GameObject("bone2").transform;
            _bone3 = new GameObject("bone3").transform;

            _bone1.SetParent(_root.transform, false);
            _bone2.SetParent(_bone1, false);
            _bone3.SetParent(_bone2, false);

            _bone1.localPosition = new Vector3(0f, 1f, 0f);
            _bone2.localPosition = new Vector3(0f, -1f, 0f);
            _bone3.localPosition = new Vector3(0f, -1f, 0f);
        }

        [TearDown]
        public void DestroyChain()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void IKSolverTrigonometric_Solve_MovesTipTowardTarget()
        {
            Vector3 target = new Vector3(0.5f, -0.5f, 0f);
            Vector3 before = _bone3.position;
            float distanceBefore = Vector3.Distance(before, target);

            // 프로젝트 코드와 같은 정적 호출 — HumanoidGroundResponse가 이 경로를 쓴다.
            IKSolverTrigonometric.Solve(
                _bone1, _bone2, _bone3, target, Vector3.back, 1f);

            Vector3 after = _bone3.position;
            Assert.That(float.IsNaN(after.x) || float.IsNaN(after.y) || float.IsNaN(after.z), Is.False,
                "IK 솔버가 NaN 위치를 반환함");
            Assert.That(Vector3.Distance(after, target), Is.LessThan(distanceBefore),
                "IK 솔버 호출 후 팁이 타깃에 접근하지 않음");
        }

        [Test]
        public void LimbIK_Component_SolverInitiatesAndUpdates()
        {
            var limbIk = _root.AddComponent<LimbIK>();
            limbIk.solver.SetChain(_bone1, _bone2, _bone3, _root.transform);
            limbIk.solver.SetIKPosition(new Vector3(0.3f, -0.8f, 0f));

            Assert.DoesNotThrow(() => limbIk.solver.Initiate(_root.transform),
                "LimbIK 솔버 Initiate 실패 — FinalIK 에셋/런타임 불일치 신호");
            Assert.DoesNotThrow(() => limbIk.solver.Update(),
                "LimbIK 솔버 Update 실패");
        }
    }
}
