using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.7.4《小镇大事记》UI:经济史面板的「大事记」页签(§150:回看无限次,旧档预填分级显示)。</summary>
    public partial class UIManager
    {
        void BuildChronicleTab(RectTransform content, GameState state, GameObject root)
        {
            // V0.9.17:11 段(五段篇间晋升过场)按 ChronicleOrder 编年序展示——枚举 int 已随存档
            // 落盘不能按时间线重排,错位由展示序数组解;11×90 远超内容区,照
            // UiProgression.ShowStageReportArchive(V0916)的 ScrollRect 样板加滚动(修复轮回K 的
            // 行距 90/卡高 84 基准保留)。复审#3:MakePanel 内容区实为 872×632(offsetMin 24,16/
            // offsetMax -24,-72),视口与说明行宽按 856(8+856=864≤872)、视口高 546(78+546=624≤632)。
            ExperienceText(content,
                "十一个关键节点的全屏插画档案。看过的可无限回看(引用首播当日的真实数字);" +
                "升级前已过去的节点按编年预填为静态记录。", 16, 8f, 48f, 856f, 24f, UiTheme.TextSub);
            Image scrollArea = UiTheme.MakeImage(content, new Color32(0x32, 0x2b, 0x27, 255), "chronicleScroll");
            PlaceTopLeft(scrollArea.rectTransform, 8f, 78f, 856f, 546f);
            scrollArea.raycastTarget = true;
            scrollArea.gameObject.AddComponent<RectMask2D>();
            GameObject contentObject = new GameObject("chronicleContent", typeof(RectTransform));
            contentObject.transform.SetParent(scrollArea.transform, false);
            RectTransform scrollContent = contentObject.GetComponent<RectTransform>();
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(.5f, 1f);
            scrollContent.anchoredPosition = Vector2.zero;
            for (int i = 0; i < CutsceneService.ChronicleOrder.Length; i++)
                BuildChronicleRow(scrollContent, state, root, CutsceneService.ChronicleOrder[i], 8f + i * 90f);
            scrollContent.sizeDelta = new Vector2(0f,
                Mathf.Max(546f, 8f + CutsceneService.ChronicleOrder.Length * 90f + 8f));
            ScrollRect scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = scrollArea.rectTransform;
            scroll.content = scrollContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            scroll.verticalNormalizedPosition = 1f;
        }

        void BuildChronicleRow(RectTransform content, GameState state, GameObject root, int id, float y)
        {
            CutsceneService.ChronicleTier tier = CutsceneService.Tier(state, id);
            CutsceneRecord record = CutsceneService.FindRecord(state, id);

            Image card = UiTheme.MakeImage(content, new Color32(0x3b, 0x31, 0x2a, 255), "chronicleCard");
            // V0.9.17 复审#3:父级改 856 宽滚动视口后卡 840(左 8+840=848,右留 8 对称)——
            // 超宽会被 RectMask2D 裁掉右缘票券框(V0916 档案按钮同坑)。
            PlaceTopLeft(card.rectTransform, 8f, y, 840f, 84f);   // 修复轮回K:卡高 88→84
            card.raycastTarget = false;

            // 插画预览:已看/预填显示原图,未发生用暗色剪影(不剧透)
            Image preview = UiTheme.MakeImage(card.transform,
                tier == CutsceneService.ChronicleTier.Locked ? new Color(0.22f, 0.22f, 0.22f) : Color.white,
                "preview");
            preview.sprite = PixelArt.Cutscene(id);
            PlaceTopLeft(preview.rectTransform, 10f, 10f, 128f, 72f);

            string status;
            Color statusColor;
            if (tier == CutsceneService.ChronicleTier.Viewed)
            {
                status = "Day " + record.worldDay + " · " + record.caption;
                statusColor = UiTheme.Accent;
            }
            else if (tier == CutsceneService.ChronicleTier.Prefilled)
            {
                status = CutsceneService.PrefillSummary(id);
                statusColor = UiTheme.TextSub;
            }
            else
            {
                status = "尚未发生——继续推进,这段记忆会在这里等你。";
                statusColor = UiTheme.TextSub;
            }

            Text title = UiTheme.MakeText(card.transform,
                (id) + " · " + CutsceneService.Title(id), 19, UiTheme.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
            PlaceTopLeft(title.rectTransform, 150f, 12f, 470f, 28f);
            Text detail = UiTheme.MakeText(card.transform, status, 16, statusColor, TextAnchor.UpperLeft);
            detail.rectTransform.Stretch();
            detail.rectTransform.offsetMin = new Vector2(150f, 8f);
            detail.rectTransform.offsetMax = new Vector2(-260f, -40f);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.verticalOverflow = VerticalWrapMode.Truncate;

            if (tier == CutsceneService.ChronicleTier.Viewed)
            {
                Button replay = UiTheme.MakeButton(card.transform, "回看", () =>
                {
                    // 回看不打已看标记、不写存档;文案数字替换为首播档案(§150/§151)
                    ClosePanel(root);
                    if (!CutsceneDirector.TryReplay(state, id, () => ShowEconomicHistory(1)))
                        ShowEconomicHistory(1);
                }, 18);
                PlaceTopLeft(replay.rectTransform(), 744f, 24f, 96f, 42f);
            }
            else if (tier == CutsceneService.ChronicleTier.Prefilled)
            {
                Text chip = UiTheme.MakeText(card.transform, "存档前的记录", 14, UiTheme.TextSub, TextAnchor.MiddleCenter);
                PlaceTopLeft(chip.rectTransform, 714f, 30f, 124f, 30f);
            }
        }
    }
}
