using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 파일 규칙 위반을 임포트 시점에 Console 경고로 알리는 에디터 가드
// 하드 게이트는 CheckMachine code-rules(check_code_rules.ps1)가 담당하고, 여기서는 조기 경고만 함
public class ProjectRuleAssetGuard : AssetPostprocessor
{
    private static readonly HashSet<string> ForbiddenFolderNames = new HashSet<string>
    {
        "Common", "Misc", "Etc", "Others", "Modules", "Manager"
    };

    private const string ProjectScopePrefix = "Assets/_Project";

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        var paths = importedAssets
            .Concat(movedAssets)
            .Where(p => p.StartsWith(ProjectScopePrefix) && p.EndsWith(".cs"));

        foreach (var path in paths)
        {
            CheckFolderName(path);
            CheckFileName(path);
        }
    }

    // 무한 확장형 폴더명 금지 규칙 검사
    private static void CheckFolderName(string path)
    {
        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
        foreach (var segment in dir.Split('/'))
        {
            if (ForbiddenFolderNames.Contains(segment))
            {
                Debug.LogWarning(
                    $"[ProjectRule] 금지 폴더명 '{segment}' 아래에 스크립트가 생성됨: {path}\n" +
                    "종류→기능 구조의 대체 위치를 검토하세요. (CheckMachine code-rules에서 fail)");
            }
        }
    }

    // Util/Helper 접미사 파일명 검사
    private static void CheckFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.EndsWith("Util") || name.EndsWith("Utils") ||
            name.EndsWith("Helper") || name.EndsWith("Helpers"))
        {
            Debug.LogWarning(
                $"[ProjectRule] Util/Helper 접미사 파일명은 새 파일에서 금지됨: {path}\n" +
                "[도메인 대상]+[책임] 형태의 이름을 사용하세요.");
        }
    }
}
