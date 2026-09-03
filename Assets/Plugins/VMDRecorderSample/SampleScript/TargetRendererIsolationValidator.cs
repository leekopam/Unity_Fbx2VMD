using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시각 회귀 캡처에서 대상 계층 외 Renderer가 출력되는지 검사함.
/// </summary>
public static class TargetRendererIsolationValidator
{
    public static bool TryValidateLoadedObjects(
        GameObject targetRoot,
        out string failureMessage)
    {
        return TryValidate(
            targetRoot,
            Resources.FindObjectsOfTypeAll<Renderer>(),
            out failureMessage);
    }

    public static bool TryValidate(
        GameObject targetRoot,
        IEnumerable<Renderer> renderers,
        out string failureMessage)
    {
        if (targetRoot == null)
        {
            failureMessage = "Editor smoke Renderer 격리 실패: 대상 캐릭터가 없습니다.";
            return false;
        }

        if (renderers == null)
        {
            failureMessage = "Editor smoke Renderer 격리 실패: Renderer 목록이 없습니다.";
            return false;
        }

        var offenderPaths = new List<string>();
        foreach (Renderer renderer in renderers)
        {
            if (!IsEnabledOutsideTarget(renderer, targetRoot.transform))
            {
                continue;
            }

            offenderPaths.Add(BuildHierarchyPath(renderer.transform));
        }

        if (offenderPaths.Count == 0)
        {
            failureMessage = string.Empty;
            return true;
        }

        failureMessage =
            $"Editor smoke Renderer 격리 실패: 대상 '{targetRoot.name}' 외 활성 Renderer " +
            $"{offenderPaths.Count}개가 있습니다: {string.Join(", ", offenderPaths)}";
        return false;
    }

    private static bool IsEnabledOutsideTarget(
        Renderer renderer,
        Transform targetRoot)
    {
        if (renderer == null ||
            !renderer.enabled ||
            !renderer.gameObject.activeInHierarchy ||
            !IsCharacterRenderer(renderer))
        {
            return false;
        }

        Transform rendererTransform = renderer.transform;
        return rendererTransform != targetRoot &&
                !rendererTransform.IsChildOf(targetRoot);
    }

    private static bool IsCharacterRenderer(Renderer renderer)
    {
        return renderer is SkinnedMeshRenderer ||
            renderer.GetComponentInParent<Animator>(includeInactive: true) != null;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<missing>";
        }

        var names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
