using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public partial class UIManager
    {
        public void ShowStartMenu()
        {
            bool hasStory = SaveSystem.TryGetSummary(SaveSlot.Story, out SaveSystem.SaveSummary story);
            bool hasChapter = SaveSystem.TryGetSummary(SaveSlot.ChapterPlay, out SaveSystem.SaveSummary chapter);
            GameObject root = BeginPanel("经世小镇 · 从哪里开始", 760f, 570f);
            // 必须先选中一个真实档位，不允许绕过后用默认世界覆盖存档。
            MarkMandatoryDecision(root);
            RectTransform c = ContentOf(root);

            Text intro = UiTheme.MakeText(c,
                "完整走一遍八篇剧情，或直接从感兴趣的大篇开始。\n" +
                "篇章游玩会使用独立存档，不会改动正式剧情。",
                21, UiTheme.TextMain, TextAnchor.MiddleCenter);
            PlaceTopLeft(intro.rectTransform, 20f, 0f, 664f, 82f);

            Button continueStory = StartMenuButton(c,
                hasStory ? "继续剧情\n<size=15>" + SummaryText(story, false) + "</size>" :
                    "继续剧情\n<size=15>暂无正式存档</size>",
                20f, 104f, () =>
                {
                    ResolveMandatoryDecision(root);
                    SaveSystem.UseSlot(SaveSlot.Story);
                    if (SaveSystem.Load(GameState.I))
                    {
                        ChapterPlayService.ApplySharedSettings(GameState.I);
                        PostLoadRefresh(GameState.I);
                        ShowToast("已继续剧情：Day " + GameState.I.day);
                    }
                    else ShowToast("剧情存档无法读取，已保留当前世界");
                });
            // 保留旧自动化与辅助功能使用的稳定控件名，屏幕上仍显示「继续剧情」。
            continueStory.name = "btn_继续经营";
            continueStory.interactable = hasStory;

            StartMenuButton(c, "新的剧情\n<size=15>从第一篇开始完整游玩</size>", 360f, 104f, () =>
            {
                System.Action start = () =>
                {
                    ResolveMandatoryDecision(root);
                    ChapterPlayService.RequestNewStory();
                    GameBootstrap.I.RestartGame();
                };
                if (hasStory)
                    ShowConfirmation("确认开始新剧情",
                        "将删除现有正式剧情进度。篇章游玩档不受影响。", start, "开始新剧情");
                else start();
            });

            Button continueChapter = StartMenuButton(c,
                hasChapter ? "继续篇章游玩\n<size=15>" + SummaryText(chapter, true) + "</size>" :
                    "继续篇章游玩\n<size=15>暂无篇章存档</size>",
                20f, 220f, () =>
                {
                    ResolveMandatoryDecision(root);
                    ChapterPlayService.RequestContinuePart();
                    GameBootstrap.I.RestartGame();
                });
            continueChapter.interactable = hasChapter;

            Button choose = StartMenuButton(c, "选篇开始\n<size=15>从 8 个大篇中任选一篇</size>", 360f, 220f, () =>
            {
                ResolveMandatoryDecision(root);
                ShowChapterSelection(true);
            });
            UiTheme.SetButtonStyle(choose, UiTheme.Accent, UiTheme.Ink);

            Text note = UiTheme.MakeText(c,
                "剧情与篇章游玩各有一份主档、备份和关键节点。\n音量、显示与辅助设置在两个世界间共享。",
                17, UiTheme.TextSub, TextAnchor.MiddleCenter);
            PlaceTopLeft(note.rectTransform, 32f, 354f, 640f, 62f);
        }

        Button StartMenuButton(RectTransform parent, string label, float x, float y, System.Action action)
        {
            Button button = UiTheme.MakeButton(parent, label, action, 20);
            PlaceTopLeft(button.rectTransform(), x, y, 324f, 92f);
            return button;
        }

        static string SummaryText(SaveSystem.SaveSummary summary, bool chapterPlay)
        {
            if (summary.mainlineComplete)
                return (chapterPlay ? "篇章游玩 · " : "") + "八篇已完成 · Day " + summary.day;
            ChapterPlayPartDefinition part = ChapterPlayService.GetPartForChapter(summary.currentChapter);
            string partName = part != null ? part.Title : "主线";
            return partName + " · Chapter " + Mathf.Max(1, summary.currentChapter) + " · Day " + summary.day;
        }

        public void ShowChapterSelection(bool mandatory = false)
        {
            bool hasStory = SaveSystem.TryGetSummary(SaveSlot.Story, out SaveSystem.SaveSummary story);
            bool hasChapter = SaveSystem.HasSave(SaveSlot.ChapterPlay);
            GameObject root = BeginPanel("选篇开始 · 8 个大篇", 1040f, 790f);
            if (mandatory) MarkMandatoryDecision(root);
            RectTransform c = ContentOf(root);

            Text intro = UiTheme.MakeText(c,
                "选择的是一整个大篇：从本篇第一章开始，篇内仍按原顺序推进。全部篇默认开放。",
                19, UiTheme.TextMain, TextAnchor.MiddleCenter);
            PlaceTopLeft(intro.rectTransform, 18f, 0f, 940f, 44f);

            for (int i = 0; i < ChapterPlayService.Parts.Count; i++)
            {
                ChapterPlayPartDefinition part = ChapterPlayService.Parts[i];
                int column = i % 2;
                int row = i / 2;
                string status = StoryPartStatus(part, hasStory, story);
                Button card = UiTheme.MakeButton(c,
                    "<b>" + part.Title + "</b>\n<size=15>Chapter " + part.startChapter + "—" + part.endChapter +
                    " · " + part.theme + "\n正式剧情：" + status + "</size>",
                    () => ConfirmStartPart(root, mandatory, part, hasChapter, hasStory, story), 18);
                PlaceTopLeft(card.rectTransform(), 18f + column * 478f, 56f + row * 126f, 462f, 112f);
                UiTheme.SetButtonStyle(card, status == "进行中" ? UiTheme.Accent :
                    status == "已完成" ? UiTheme.Good : new Color32(0x3b, 0x31, 0x2a, 255),
                    status == "进行中" ? UiTheme.Ink : UiTheme.TextMain);
            }

            Button back = UiTheme.MakeButton(c, "返回", () =>
            {
                if (mandatory)
                {
                    ResolveMandatoryDecision(root);
                    ShowStartMenu();
                }
                else ClosePanel(root);
            }, 19);
            PlaceTopLeft(back.rectTransform(), 378f, 574f, 220f, 50f);
        }

        static string StoryPartStatus(ChapterPlayPartDefinition part, bool hasStory, SaveSystem.SaveSummary story)
        {
            if (!hasStory) return "尚未开始";
            if (story.completedChapter >= part.endChapter || story.mainlineComplete) return "已完成";
            if (story.currentChapter >= part.startChapter && story.currentChapter <= part.endChapter) return "进行中";
            return story.currentChapter < part.startChapter ? "未到达" : "已越过";
        }

        void ConfirmStartPart(GameObject selectionRoot, bool mandatory, ChapterPlayPartDefinition part,
            bool hasChapter, bool hasStory, SaveSystem.SaveSummary story)
        {
            bool spoiler = !hasStory || story.completedChapter < part.startChapter - 1;
            string message = "你将从" + part.Title + "开始。系统会准备好前置世界，并从 Chapter " +
                part.startChapter + " 按原顺序游玩。\n\n" +
                (spoiler ? "该篇包含你在正式剧情中尚未到达的内容，可能涉及剧透。\n" : "") +
                (hasChapter ? "开始后会替换现有篇章游玩档。\n" : "") +
                "正式剧情始终不受影响。";
            ShowConfirmation("从" + part.Title + "开始", message, () =>
            {
                if (mandatory) ResolveMandatoryDecision(selectionRoot);
                ChapterPlayService.RequestStartPart(part.number);
                GameBootstrap.I.RestartGame();
            }, "从本篇开始");
        }

        public void ShowChapterPlayIntro(ChapterPlayPartDefinition part)
        {
            if (part == null) return;
            ChapterDefinition chapter = ProgressionService.Current(GameState.I);
            GameObject root = BeginPanel("篇章游玩 · " + part.Title, 720f, 500f);
            RectTransform c = ContentOf(root);
            string firstChapter = chapter != null
                ? "Chapter " + chapter.number + "《" + chapter.title + "》"
                : "Chapter " + part.startChapter;
            Text body = UiTheme.MakeText(c,
                "<b>" + firstChapter + "</b>\n\n" + part.theme + "\n\n" +
                "这是一份确定性的标准篇首世界。前置城区与经济账本已准备好，" +
                "当前章的通关条件尚未完成。从现在起，任务、收日、报告和演出都沿用正式剧情规则。\n\n" +
                "左上角会常驻显示「篇章游玩」；可在设置中换篇或回到正式剧情。",
                20, UiTheme.TextMain, TextAnchor.UpperLeft);
            PlaceTopLeft(body.rectTransform, 24f, 10f, 616f, 270f);
            Button start = UiTheme.MakeButton(c, "开始本篇", () => ClosePanel(root), 21);
            PlaceTopLeft(start.rectTransform(), 220f, 310f, 240f, 54f);
            UiTheme.SetButtonStyle(start, UiTheme.Accent, UiTheme.Ink);
        }

        public void ReturnToStoryFromChapterPlay()
        {
            GameState state = GameState.I;
            if (state == null) return;
            if (ChapterPlayService.IsChapterPlay) SaveSystem.Save(state);
            if (SaveSystem.HasSave(SaveSlot.Story)) ChapterPlayService.RequestContinueStory();
            else ChapterPlayService.RequestNewStory();
            GameBootstrap.I.RestartGame();
        }
    }
}
