using System.IO;
using System.Text;
using Fbx2Vmd.ClothPhysics;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.EditorPhysics
{
    /// <summary>
    /// 생성된 클로스의 파라미터 스냅샷을 저장·비교하는 에디터 메뉴.
    /// 저장본: Docs/Workflow/Local/snapshots/{오브젝트}/{클로스}.snapshot.txt
    /// diff로 의도치 않은 튜닝 회귀를 잡는다.
    /// </summary>
    public static class ClothSnapshotMenu
    {
        const string RootDir = "Docs/Workflow/Local/snapshots";

        [MenuItem("Fbx2Vmd/MC2 QA/클로스 파라미터 스냅샷 저장")]
        static void SaveSnapshots()
        {
            var setup = FindSetup();
            if (setup == null || setup.generatedCloths.Count == 0)
            {
                Debug.LogWarning("[MC2 QA] 생성된 클로스가 없습니다 — 물리 설정을 먼저 실행하세요.");
                return;
            }
            string dir = Path.Combine(RootDir, setup.name);
            Directory.CreateDirectory(dir);
            int n = 0;
            foreach (var cloth in setup.generatedCloths)
            {
                if (cloth == null) continue;
                File.WriteAllText(Path.Combine(dir, cloth.name + ".snapshot.txt"),
                    ClothSnapshotter.Serialize(ClothSnapshotter.Capture(cloth)));
                n++;
            }
            Debug.Log($"[MC2 QA] 스냅샷 {n}개 저장 → {dir}");
        }

        [MenuItem("Fbx2Vmd/MC2 QA/저장 스냅샷과 현재 상태 비교")]
        static void DiffSnapshots()
        {
            var setup = FindSetup();
            if (setup == null || setup.generatedCloths.Count == 0)
            {
                Debug.LogWarning("[MC2 QA] 생성된 클로스가 없습니다.");
                return;
            }
            string dir = Path.Combine(RootDir, setup.name);
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning("[MC2 QA] 저장된 스냅샷이 없습니다 — 먼저 '스냅샷 저장'을 실행하세요.");
                return;
            }
            var sb = new StringBuilder();
            int changed = 0;
            foreach (var cloth in setup.generatedCloths)
            {
                if (cloth == null) continue;
                string file = Path.Combine(dir, cloth.name + ".snapshot.txt");
                if (!File.Exists(file))
                {
                    sb.AppendLine($"{cloth.name}: 저장본 없음 (신규)");
                    continue;
                }
                var before = ClothSnapshotter.Deserialize(File.ReadAllText(file));
                var diffs = ClothSnapshotter.Diff(before, ClothSnapshotter.Capture(cloth));
                if (diffs.Count == 0)
                    continue;
                changed++;
                sb.AppendLine($"■ {cloth.name} — {diffs.Count}건 변경");
                foreach (var d in diffs)
                    sb.AppendLine("  " + d);
            }
            Debug.Log(changed == 0
                ? "[MC2 QA] 저장 스냅샷과 동일 — 변경 없음"
                : $"[MC2 QA] {changed}개 클로스 변경:\n{sb}");
        }

        static CharacterPhysicsSetup FindSetup()
        {
            // 씬에서 첫 CharacterPhysicsSetup을 찾는다 (플레이 모드 포함)
            return Object.FindFirstObjectByType<CharacterPhysicsSetup>();
        }
    }
}
