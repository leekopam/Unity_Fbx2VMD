using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    internal static class MainRecordingSettingsPopupViewBuilder
    {
        internal static Button[] Build(
            Transform parent,
            MainRecordingSettingsElementBuilder elementBuilder,
            out Button closeButton,
            out TextMeshProUGUI notificationText)
        {
            elementBuilder.CreateImage(
                "Page",
                parent,
                RectFull(),
                MainRecordingSettingsLayoutSpec.PageColor);
            BuildRail(parent, elementBuilder);
            BuildSidebar(parent, elementBuilder);
            return BuildMainArea(parent, elementBuilder, out closeButton, out notificationText);
        }

        private static void BuildRail(
            Transform parent,
            MainRecordingSettingsElementBuilder elementBuilder)
        {
            elementBuilder.CreateImage(
                "Rail",
                parent,
                new Rect(0f, 0f, MainRecordingSettingsLayoutSpec.RailWidth, MainRecordingSettingsLayoutSpec.ReferenceHeight),
                MainRecordingSettingsLayoutSpec.RailColor);
            elementBuilder.CreateText("RailIconPrimary", parent, "□", 28, MainRecordingSettingsLayoutSpec.ActiveColor,
                FontStyles.Bold, TextAlignmentOptions.Center, new Rect(13f, 46f, 30f, 30f));
            elementBuilder.CreateText("RailIconGraph", parent, "Σ", 28, new Color32(95, 108, 120, 255),
                FontStyles.Bold, TextAlignmentOptions.Center, new Rect(13f, 104f, 30f, 30f));
            elementBuilder.CreateText("RailIconLight", parent, "!", 24, new Color32(95, 108, 120, 255),
                FontStyles.Bold, TextAlignmentOptions.Center, new Rect(13f, 160f, 30f, 30f));
            elementBuilder.CreateImage("RailActiveMarker", parent, new Rect(50f, 34f, 4f, 40f),
                MainRecordingSettingsLayoutSpec.ActiveColor);
        }

        private static void BuildSidebar(
            Transform parent,
            MainRecordingSettingsElementBuilder elementBuilder)
        {
            elementBuilder.CreateImage(
                "Sidebar",
                parent,
                new Rect(
                    MainRecordingSettingsLayoutSpec.SidebarX,
                    MainRecordingSettingsLayoutSpec.SidebarY,
                    MainRecordingSettingsLayoutSpec.SidebarWidth,
                    MainRecordingSettingsLayoutSpec.SidebarHeight),
                MainRecordingSettingsLayoutSpec.SidebarColor);

            elementBuilder.CreateImage("SidebarHeader", parent, new Rect(62f, 39f, 237f, 35f),
                MainRecordingSettingsLayoutSpec.SidebarHeaderColor);
            elementBuilder.CreateText("SidebarTitle", parent, MainRecordingSettingsLayoutSpec.WindowTitle, 16,
                MainRecordingSettingsLayoutSpec.ActiveColor, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Rect(101f, 46f, 178f, 22f));

            MainRecordingSettingsSidebarItemSpec[] sidebarItems = MainRecordingSettingsLayoutSpec.SidebarItems;

            elementBuilder.CreateText("SidebarGroupCamera", parent, "시네마토그래피", 11, new Color32(52, 64, 76, 255),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Rect(66f, 95f, 180f, 18f));
            elementBuilder.CreateText("SidebarCamera", parent, sidebarItems[0].Label, 15, new Color32(36, 43, 51, 255),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Rect(101f, 124f, 160f, 22f));
            elementBuilder.CreateText("SidebarGroupEnvironment", parent, "환경", 11, new Color32(52, 64, 76, 255),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Rect(66f, 172f, 180f, 18f));
            elementBuilder.CreateText("SidebarEnvironment", parent, sidebarItems[1].Label, 15, new Color32(36, 43, 51, 255),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Rect(101f, 199f, 170f, 22f));
            elementBuilder.CreateText("SidebarLight", parent, sidebarItems[2].Label, 15, new Color32(36, 43, 51, 255),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Rect(101f, 238f, 178f, 22f));

            elementBuilder.CreateText("SidebarBottomTools", parent, "+  -  □  ✎  ⊞  ···", 25,
                MainRecordingSettingsLayoutSpec.ActiveColor, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft, new Rect(72f, 631f, 190f, 34f));
        }

        private static Button[] BuildMainArea(
            Transform parent,
            MainRecordingSettingsElementBuilder elementBuilder,
            out Button closeButton,
            out TextMeshProUGUI notificationText)
        {
            elementBuilder.CreateText("MainTitle", parent, MainRecordingSettingsLayoutSpec.WindowTitle, 22,
                new Color32(52, 64, 76, 255), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Rect(
                    MainRecordingSettingsLayoutSpec.MainX,
                    MainRecordingSettingsLayoutSpec.TitleY,
                    360f,
                    34f));

            Rect viewportRect = new Rect(305f, 31f, 950f, 644f);
            RectTransform viewport = elementBuilder.CreateRectTransform("MainViewport", parent, viewportRect);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = elementBuilder.CreateRectTransform(
                "MainContent",
                viewport,
                new Rect(0f, 0f, viewportRect.width, 780f));

            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            var cardButtons = new Button[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                float y = (MainRecordingSettingsLayoutSpec.CardY - viewportRect.y) +
                          (i * (MainRecordingSettingsLayoutSpec.CardHeight + MainRecordingSettingsLayoutSpec.CardGap));
                cardButtons[i] = BuildCard(content, cards[i], new Rect(
                    MainRecordingSettingsLayoutSpec.CardX - viewportRect.x,
                    y,
                    MainRecordingSettingsLayoutSpec.CardWidth,
                    MainRecordingSettingsLayoutSpec.CardHeight),
                    elementBuilder);
            }

            elementBuilder.CreateImage("StaticScrollbar", parent, new Rect(1257f, 34f, 4f, 406f),
                MainRecordingSettingsLayoutSpec.ActiveColor);
            closeButton = elementBuilder.CreateButton(
                "CloseButton",
                parent,
                "닫기",
                new Rect(1190f, 632f, 56f, 32f),
                true);
            notificationText = elementBuilder.CreateText("Notification", parent, string.Empty, 13,
                new Color32(80, 88, 96, 255), FontStyles.Normal,
                TextAlignmentOptions.MidlineRight, new Rect(860f, 632f, 300f, 32f));
            return cardButtons;
        }

        private static Button BuildCard(
            Transform parent,
            MainRecordingSettingsCardSpec card,
            Rect rect,
            MainRecordingSettingsElementBuilder elementBuilder)
        {
            elementBuilder.CreateImage(card.Title + " Card", parent, rect, card.BackgroundColor);
            elementBuilder.CreateText(card.Title + " Title", parent, card.Title, 25, Color.white, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardTitleY,
                    310f,
                    38f));
            elementBuilder.CreateText(card.Title + " Body", parent, card.Body, 15, card.BodyTextColor, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardBodyY,
                    330f,
                    46f));

            return elementBuilder.CreateButton(
                card.Title + " Button",
                parent,
                card.ButtonLabel,
                new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardButtonY,
                    MainRecordingSettingsLayoutSpec.CardButtonWidth,
                    MainRecordingSettingsLayoutSpec.CardButtonHeight),
                card.Enabled);
        }

        private static Rect RectFull()
        {
            return new Rect(
                0f,
                0f,
                MainRecordingSettingsLayoutSpec.ReferenceWidth,
                MainRecordingSettingsLayoutSpec.ReferenceHeight);
        }
    }
}
