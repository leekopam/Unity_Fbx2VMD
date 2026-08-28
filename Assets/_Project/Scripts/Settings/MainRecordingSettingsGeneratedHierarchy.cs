using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    internal static class MainRecordingSettingsGeneratedHierarchy
    {
        internal const string ContentObjectName = "GeneratedContent";
        private static readonly string[] LegacyRootChildNames =
        {
            "Page",
            "Rail",
            "RailIconPrimary",
            "RailIconGraph",
            "RailIconLight",
            "RailActiveMarker",
            "Sidebar",
            "SidebarHeader",
            "SidebarTitle",
            "SidebarGroupCamera",
            "SidebarCamera",
            "SidebarGroupEnvironment",
            "SidebarEnvironment",
            "SidebarLight",
            "SidebarBottomTools",
            "MainTitle",
            "MainViewport",
            "StaticScrollbar",
            "CloseButton",
            "Notification"
        };

        private static readonly string[] RootImageChildNames =
        {
            "Page",
            "Rail",
            "RailActiveMarker",
            "Sidebar",
            "SidebarHeader",
            "StaticScrollbar"
        };

        private static readonly string[] RootTextChildNames =
        {
            "RailIconPrimary",
            "RailIconGraph",
            "RailIconLight",
            "SidebarTitle",
            "SidebarGroupCamera",
            "SidebarCamera",
            "SidebarGroupEnvironment",
            "SidebarEnvironment",
            "SidebarLight",
            "SidebarBottomTools",
            "MainTitle",
            "Notification"
        };

        internal static int ContentChildCount => LegacyRootChildNames.Length;

        internal static bool HasOwnedRootChildren(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).GetComponent<MainRecordingSettingsGeneratedContent>() != null)
                {
                    return true;
                }
            }

            var legacyChildren = new List<Transform>(ContentChildCount);
            return TryCollectLegacyRootChildren(root, legacyChildren);
        }

        internal static string FindNotificationText(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform notificationTransform =
                    child.GetComponent<MainRecordingSettingsGeneratedContent>() != null
                    ? child.Find("Notification")
                    : null;
                TextMeshProUGUI label = notificationTransform != null
                    ? notificationTransform.GetComponent<TextMeshProUGUI>()
                    : null;
                if (label != null)
                {
                    return label.text;
                }
            }

            Transform legacyNotification = root.Find("Notification");
            TextMeshProUGUI legacyLabel = legacyNotification != null
                ? legacyNotification.GetComponent<TextMeshProUGUI>()
                : null;
            return legacyLabel != null ? legacyLabel.text : string.Empty;
        }

        internal static bool TryFindContent(Transform root, out RectTransform contentRoot)
        {
            contentRoot = null;
            if (!TryFindSingleOwnedContent(root, out Transform contentTransform))
            {
                return false;
            }

            contentRoot = contentTransform.GetComponent<RectTransform>();
            return contentRoot != null;
        }

        internal static bool HasCompleteContent(
            RectTransform contentRoot,
            MainRecordingSettingsCardSpec[] cards)
        {
            if (contentRoot.name != ContentObjectName ||
                contentRoot.GetComponent<MainRecordingSettingsGeneratedContent>() == null ||
                contentRoot.childCount != ContentChildCount)
            {
                return false;
            }

            var generatedNames = new HashSet<string>();
            for (int i = 0; i < contentRoot.childCount; i++)
            {
                string childName = contentRoot.GetChild(i).name;
                if (!IsLegacyRootChildName(childName) || !generatedNames.Add(childName))
                {
                    return false;
                }
            }

            Transform mainViewport = contentRoot.Find("MainViewport");
            Transform mainContent = mainViewport != null ? mainViewport.Find("MainContent") : null;
            Transform closeButtonTransform = contentRoot.Find("CloseButton");
            if (!HasRequiredRootComponents(contentRoot) ||
                mainViewport == null ||
                mainViewport.GetComponent<RectMask2D>() == null ||
                mainViewport.childCount != 1 ||
                mainContent == null ||
                closeButtonTransform == null ||
                closeButtonTransform.GetComponent<Button>() == null ||
                closeButtonTransform.GetComponent<Image>() == null ||
                closeButtonTransform.childCount != 1 ||
                !HasComponentAtPath<TextMeshProUGUI>(contentRoot, "CloseButton/CloseButton Text"))
            {
                return false;
            }

            if (mainContent.childCount != cards.Length * 4)
            {
                return false;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                string title = cards[i].Title;
                Transform buttonTransform = mainContent.Find(title + " Button");
                if (!HasComponentAtPath<Image>(mainContent, title + " Card") ||
                    !HasComponentAtPath<TextMeshProUGUI>(mainContent, title + " Title") ||
                    !HasComponentAtPath<TextMeshProUGUI>(mainContent, title + " Body") ||
                    buttonTransform == null ||
                    buttonTransform.GetComponent<Button>() == null ||
                    buttonTransform.GetComponent<Image>() == null ||
                    buttonTransform.childCount != 1 ||
                    !HasComponentAtPath<TextMeshProUGUI>(
                        buttonTransform,
                        title + " Button Text"))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool TryResolveControls(
            RectTransform contentRoot,
            MainRecordingSettingsCardSpec[] cards,
            out Button closeButton,
            out TextMeshProUGUI notificationText,
            out Button[] cardButtons)
        {
            closeButton = null;
            notificationText = null;
            cardButtons = null;
            if (!HasCompleteContent(contentRoot, cards))
            {
                return false;
            }

            Transform mainContent = contentRoot.Find("MainViewport/MainContent");
            closeButton = contentRoot.Find("CloseButton").GetComponent<Button>();
            notificationText = contentRoot.Find("Notification").GetComponent<TextMeshProUGUI>();
            var resolvedCardButtons = new Button[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                Transform buttonTransform = mainContent.Find(cards[i].Title + " Button");
                Button button = buttonTransform != null
                    ? buttonTransform.GetComponent<Button>()
                    : null;
                if (button == null)
                {
                    return false;
                }

                resolvedCardButtons[i] = button;
            }

            cardButtons = resolvedCardButtons;
            return true;
        }

        internal static bool TryCollectLegacyRootChildren(
            Transform root,
            List<Transform> legacyChildren)
        {
            if (HasMarkedContent(root))
            {
                return false;
            }

            var legacyNames = new HashSet<string>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!IsLegacyRootChildName(child.name))
                {
                    continue;
                }

                if (!legacyNames.Add(child.name))
                {
                    return false;
                }

                legacyChildren.Add(child);
            }

            return legacyChildren.Count == ContentChildCount;
        }

        internal static void MoveChildren(List<Transform> children, Transform contentRoot)
        {
            foreach (Transform child in children)
            {
                child.SetParent(contentRoot, true);
            }
        }

        internal static void MarkOwned(RectTransform contentRoot)
        {
            if (contentRoot.GetComponent<MainRecordingSettingsGeneratedContent>() == null)
            {
                contentRoot.gameObject.AddComponent<MainRecordingSettingsGeneratedContent>();
            }
        }

        internal static int FindOwnedSiblingIndex(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).GetComponent<MainRecordingSettingsGeneratedContent>() != null)
                {
                    return i;
                }
            }

            return -1;
        }

        internal static void RestoreSiblingIndex(Transform contentRoot, int siblingIndex)
        {
            if (siblingIndex < 0 || contentRoot.parent == null)
            {
                return;
            }

            int maximumSiblingIndex = contentRoot.parent.childCount - 1;
            contentRoot.SetSiblingIndex(Mathf.Min(siblingIndex, maximumSiblingIndex));
        }

        internal static void RemoveOwnedRootChildren(Transform root)
        {
            var ownedChildren = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.GetComponent<MainRecordingSettingsGeneratedContent>() != null)
                {
                    ownedChildren.Add(child);
                }
            }

            foreach (Transform child in ownedChildren)
            {
                GameObject childObject = child.gameObject;
                child.SetParent(null, false);
                childObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Object.Destroy(childObject);
                }
                else
                {
                    Object.DestroyImmediate(childObject);
                }
            }
        }

        private static bool TryFindSingleOwnedContent(Transform parent, out Transform child)
        {
            child = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform candidate = parent.GetChild(i);
                if (candidate.GetComponent<MainRecordingSettingsGeneratedContent>() == null)
                {
                    continue;
                }

                if (child != null)
                {
                    child = null;
                    return false;
                }

                child = candidate;
            }

            return child != null;
        }

        private static bool HasMarkedContent(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).GetComponent<MainRecordingSettingsGeneratedContent>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRequiredRootComponents(Transform contentRoot)
        {
            for (int i = 0; i < RootImageChildNames.Length; i++)
            {
                if (!HasComponentAtPath<Image>(contentRoot, RootImageChildNames[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < RootTextChildNames.Length; i++)
            {
                if (!HasComponentAtPath<TextMeshProUGUI>(contentRoot, RootTextChildNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasComponentAtPath<T>(Transform root, string path)
            where T : Component
        {
            Transform target = root.Find(path);
            return target != null && target.GetComponent<T>() != null;
        }

        private static bool IsLegacyRootChildName(string childName)
        {
            for (int i = 0; i < LegacyRootChildNames.Length; i++)
            {
                if (LegacyRootChildNames[i] == childName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
