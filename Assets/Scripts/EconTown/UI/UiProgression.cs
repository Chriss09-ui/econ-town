using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public partial class UIManager
    {
        public void ShowProgression()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ProgressionService.Ensure(s);
            GameObject root = BeginPanel(
                (ChapterPlayService.IsChapterPlay ? "篇章游玩 · " : "") + "学习路线 · 你现在在哪里",
                1040f, 870f);
            RectTransform c = ContentOf(root);
            ChapterDefinition current = ProgressionService.Current(s);

            Image currentCard = UiTheme.MakeImage(c, new Color32(0x43, 0x35, 0x2b, 255), "currentChapterCard");
            PlaceTopLeft(currentCard.rectTransform, 8f, 0f, 976f, 116f);
            Text currentText = UiTheme.MakeText(currentCard.transform,
                current == null
                    ? "<b>自由经济时代</b>\n八篇主线已完成。你可以继续经营、进世界市场做贸易、进入挑战或创建经济实验。"
                    : "<size=16>" + ProgressionService.ActLabel(current.part) + "</size>\n<b>" +
                      ProgressionService.PartTitle(current.part) + " · Chapter " + current.number + "　" + current.title +
                      "</b>\n当前任务：" + current.task + "\n学习目标：" + current.learningGoal,
                20, UiTheme.TextMain, TextAnchor.MiddleLeft);
            currentText.rectTransform.Stretch();
            currentText.rectTransform.offsetMin = new Vector2(18f, 10f);
            currentText.rectTransform.offsetMax = new Vector2(-18f, -10f);

            // V0.9.5 第八篇:8 行行距从 74 压到 64;修复轮回J:行高同步 74 时代的 76→64——
            // 行距 64×行高 76 会相邻叠压 12px(第 8 行底=118+7×64+64=630,不与 654 按钮行重叠)。
            LearningPart[] parts = { LearningPart.Enterprise, LearningPart.Market, LearningPart.Resident,
                LearningPart.Government, LearningPart.Macro, LearningPart.Finance, LearningPart.Trade,
                LearningPart.Housing };
            int[] starts = { 1, 6, 11, 17, 23, 31, 43, 54 };
            int[] ends = { 5, 10, 16, 22, 30, 42, 53, 58 };
            for (int i = 0; i < parts.Length; i++)
                BuildPartRow(c, s, parts[i], starts[i], ends[i], 8f, 118f + i * 64f, 976f);

            string freeGoal = s.progression.positiveProfitGoalComplete
                ? "已完成：连续 3 天利润为正"
                : "自由目标：连续 3 天利润为正（当前 " + s.progression.positiveProfitStreak + "/3）";
            Text goal = UiTheme.MakeText(c, freeGoal, 17,
                s.progression.positiveProfitGoalComplete ? UiTheme.Good : UiTheme.TextSub,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            // 八篇布局:第 8 行底 630,目标行放 700 起不与行卡重叠。
            PlaceTopLeft(goal.rectTransform, 18f, 700f, 950f, 30f);

            Button briefing = UiTheme.MakeButton(c, "今日待办", () =>
            {
                ClosePanel(root);
                ShowTodayBriefing();
            }, 18);
            PlaceTopLeft(briefing.rectTransform(), 8f, 654f, 180f, 48f);

            Button experiment = UiTheme.MakeButton(c, "实验控制台", () =>
            {
                ClosePanel(root);
                ShowExperimentConsole();
            }, 18);
            PlaceTopLeft(experiment.rectTransform(), 202f, 654f, 180f, 48f);

            Button reports = UiTheme.MakeButton(c, "阶段报告", () =>
            {
                ClosePanel(root);
                ShowStageReportArchive();
            }, 19);
            PlaceTopLeft(reports.rectTransform(), 396f, 654f, 180f, 48f);

            Button challenges = UiTheme.MakeButton(c, "经济挑战", () =>
            {
                ClosePanel(root);
                ShowChallenges();
            }, 18);
            PlaceTopLeft(challenges.rectTransform(), 590f, 654f, 180f, 48f);
            challenges.interactable = s.sandbox != null && s.sandbox.unlocked;

            Button close = UiTheme.MakeButton(c, "返回小镇", () => ClosePanel(root), 19);
            PlaceTopLeft(close.rectTransform(), 784f, 654f, 180f, 48f);
        }

        void BuildPartRow(RectTransform parent, GameState s, LearningPart part, int start, int end,
            float x, float y, float width)
        {
            int entryPartNumber = ChapterPlayService.EntryPartNumber(s);
            ChapterPlayPartDefinition entryPart = ChapterPlayService.GetPart(entryPartNumber);
            bool standardPrerequisite = ChapterPlayService.IsChapterPlay && entryPart != null && end < entryPart.startChapter;
            int completed = standardPrerequisite ? 0 :
                Mathf.Clamp(s.progression.completedChapter - start + 1, 0, end - start + 1);
            bool active = !s.progression.mainlineComplete && s.progression.currentChapter >= start && s.progression.currentChapter <= end;
            bool done = completed >= end - start + 1;
            Color32 bg = standardPrerequisite ? new Color32(0x35, 0x3f, 0x42, 255) : done ? new Color32(0x2e, 0x57, 0x49, 255) : active
                ? new Color32(0x6b, 0x46, 0x26, 255) : new Color32(0x2d, 0x28, 0x25, 245);
            Image row = UiTheme.MakeImage(parent, bg, "part_" + (int)part);
            PlaceTopLeft(row.rectTransform, x, y, width, 64f);
            Text title = UiTheme.MakeText(row.transform,
                ProgressionService.PartTitle(part) + "\n<size=16>Chapter " + start + "—" + end + "　" +
                (standardPrerequisite ? "系统已准备，不计入玩家成绩" : completed + "/" + (end - start + 1) + " 已完成") + "</size>",
                20, UiTheme.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Stretch();
            title.rectTransform.offsetMin = new Vector2(18f, 8f);
            title.rectTransform.offsetMax = new Vector2(-220f, -8f);

            Text stateText = UiTheme.MakeText(row.transform,
                standardPrerequisite ? "标准前置" : done ? "已完成" : active ? "进行中" : "未解锁",
                18, standardPrerequisite ? UiTheme.Market : done ? new Color32(0x9a, 0xe0, 0xb9, 255) : active ? UiTheme.Accent : UiTheme.TextSub,
                TextAnchor.MiddleRight, FontStyle.Bold);
            stateText.rectTransform.anchorMin = stateText.rectTransform.anchorMax = new Vector2(1f, .5f);
            stateText.rectTransform.pivot = new Vector2(1f, .5f);
            stateText.rectTransform.anchoredPosition = new Vector2(-20f, 0f);
            stateText.rectTransform.sizeDelta = new Vector2(180f, 44f);
        }

        /// <summary>读档补发与晨闸共用的待看报告交付(V0.9.17 起为唯一交付口):
        /// 第八篇(Housing)报告先走 Finale 收官过场闸门,五个有晋升过场的篇先走「身份授予」过场,
        /// 其余篇直接弹——授予在前、成绩单在后。修复轮回G(迁址漏掉的第四处宿主):读档直接弹报告
        /// 会消费 pending,把「收弧当天没看完过场就退出重进」档的过场永久跳过、大事记永远停在
        /// 「存档前的记录」。</summary>
        public void DeliverPendingStageReport(StageReportRecord report)
        {
            if (report == null) return;
            int cutscene = report.part == LearningPart.Housing
                ? (int)CutsceneId.Finale
                : CutsceneService.PromotionCutsceneFor(report.part);
            // 复审#1:TryPlay 的 false 分支(不播)也已同步调用 hostPopup 恰好一次(与
            // GameBootstrap 其余调用点同契约)——外层再弹会叠两层报告面板(旧档在 53 章
            // 看过 Finale、升级后完成 58 章的档可达)。false 即 return,宿主弹窗只归 TryPlay。
            if (cutscene != 0)
            {
                CutsceneDirector.TryPlay(GameState.I, cutscene, () => ShowStageReport(report));
                return;
            }
            ShowStageReport(report);
        }

        public void ShowStageReport(StageReportRecord report, bool fromArchive = false)
        {
            if (report == null) return;
            // 弹窗展示即消费 pending：玩家用 ×/Esc 关闭也视为已展示
            // （报告本体持久在 stageReports，档案可随时回看），否则同一篇每天清晨都会再弹。
            if (!fromArchive) ProgressionService.MarkPendingStageReportViewed(GameState.I);
            GameObject root = BeginPanel(ProgressionService.PartTitle(report.part) + " · 阶段完成", 900f, 690f);
            RectTransform c = ContentOf(root);
            StringBuilder body = new StringBuilder();
            // V0.9.17 晋升线:头部幕名归属 + 尾部「下一程」委任文案(展示层现查,旧档报告零迁移)。
            body.Append("<b>").Append(ProgressionService.ActLabel(report.part)).Append("</b>\n\n")
                .Append("<b>游戏结果</b>\n").Append(report.resultSummary)
                .Append("\n\n<b>本篇已掌握</b>\n").Append(report.learnedSummary)
                .Append("\n\n<b>复习建议</b>\n").Append(report.reviewSummary)
                .Append("\n\n<b>你的关键决策</b>\n");
            if (report.keyDecisions == null || report.keyDecisions.Count == 0) body.Append("暂无手动标记的关键决策，后续选择会记录在这里。");
            else for (int i = 0; i < report.keyDecisions.Count; i++) body.Append("• ").Append(report.keyDecisions[i]).Append('\n');
            body.Append("\n<b>下一程</b>\n").Append(ProgressionService.NextJourneyText(report.part));
            Image scrollArea = UiTheme.MakeImage(c, new Color32(0x32, 0x2b, 0x27, 255), "stageReportScroll");
            PlaceTopLeft(scrollArea.rectTransform, 8f, 0f, 828f, 500f);
            scrollArea.raycastTarget = true;
            scrollArea.gameObject.AddComponent<RectMask2D>();
            GameObject contentObject = new GameObject("stageReportContent", typeof(RectTransform));
            contentObject.transform.SetParent(scrollArea.transform, false);
            RectTransform scrollContent = contentObject.GetComponent<RectTransform>();
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(.5f, 1f);
            scrollContent.anchoredPosition = Vector2.zero;

            Text text = UiTheme.MakeText(scrollContent, body.ToString(), 20, UiTheme.TextMain, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            text.rectTransform.sizeDelta = new Vector2(-32f, 100f);
            Canvas.ForceUpdateCanvases();
            float reportHeight = Mathf.Max(468f, text.preferredHeight);
            text.rectTransform.sizeDelta = new Vector2(-32f, reportHeight);
            scrollContent.sizeDelta = new Vector2(0f, reportHeight + 32f);
            ScrollRect scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = scrollArea.rectTransform;
            scroll.content = scrollContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            scroll.verticalNormalizedPosition = 1f;

            if (!fromArchive && ChapterPlayService.IsChapterPlay)
            {
                ChapterPlayPartDefinition playedPart = ChapterPlayService.GetPart(report.part);
                bool finale = playedPart != null && playedPart.number == ChapterPlayService.Parts.Count;
                Button nextPart = UiTheme.MakeButton(c, finale ? "继续自由经营" : "继续下一篇", () =>
                {
                    SaveSystem.SaveKeyMoment(GameState.I, "阶段报告已查看");
                    ClosePanel(root);
                    ShowProgression();
                }, 17);
                PlaceTopLeft(nextPart.rectTransform(), 8f, 512f, 190f, 54f);
                UiTheme.SetButtonStyle(nextPart, UiTheme.Accent, UiTheme.Ink);

                Button replay = UiTheme.MakeButton(c, "重玩本篇", () =>
                {
                    if (playedPart == null) return;
                    ShowConfirmation("重玩" + playedPart.Title,
                        "将用本篇的标准开局替换当前篇章游玩档。正式剧情不受影响。", () =>
                        {
                            ChapterPlayService.RequestStartPart(playedPart.number);
                            GameBootstrap.I.RestartGame();
                        }, "重玩本篇");
                }, 17);
                PlaceTopLeft(replay.rectTransform(), 210f, 512f, 190f, 54f);

                Button choose = UiTheme.MakeButton(c, "选择其他篇", () =>
                {
                    ClosePanel(root);
                    ShowChapterSelection();
                }, 17);
                PlaceTopLeft(choose.rectTransform(), 412f, 512f, 190f, 54f);

                Button story = UiTheme.MakeButton(c, "回到正式剧情", ReturnToStoryFromChapterPlay, 16);
                PlaceTopLeft(story.rectTransform(), 614f, 512f, 190f, 54f);
                return;
            }

            Button next = UiTheme.MakeButton(c,
                fromArchive ? "返回报告档案" : report.part == LearningPart.Housing ? "进入自由经济时代" : "继续下一篇", () =>
                {
                    if (fromArchive)
                    {
                        ClosePanel(root);
                        ShowStageReportArchive();
                        return;
                    }
                    SaveSystem.SaveKeyMoment(GameState.I, "阶段报告已查看");
                    ClosePanel(root);
                    ShowProgression();
                }, 21);
            next.rectTransform().anchorMin = next.rectTransform().anchorMax = new Vector2(.5f, 0f);
            next.rectTransform().pivot = new Vector2(.5f, 0f);
            next.rectTransform().anchoredPosition = new Vector2(0f, 8f);
            next.rectTransform().sizeDelta = new Vector2(320f, 54f);
        }

        public void ShowStageReportArchive()
        {
            GameState s = GameState.I;
            if (s == null) return;
            ProgressionService.Ensure(s);
            GameObject root = BeginPanel("阶段报告档案", 900f, 680f);
            RectTransform c = ContentOf(root);
            if (s.progression.stageReports.Count == 0)
                ShopText(c, "完成一篇主线后，这里会保存经营结果、学习结果和关键决策。",
                    20, UiTheme.TextSub, 40f, 180f, 772f, 90f, TextAnchor.MiddleCenter);
            else
            {
                // 围猎五#7:八篇恒 8 条×98px 行距 > 面板 content 可用高 592(第 7 条起溢出面板
                // 与屏幕,第八篇「住房」报告无法回看)——照同文件 ShowStageReport 正文先例加滚动。
                int count = s.progression.stageReports.Count;
                Image scrollArea = UiTheme.MakeImage(c, new Color32(0x32, 0x2b, 0x27, 255), "stageArchiveScroll");
                PlaceTopLeft(scrollArea.rectTransform, 8f, 66f, 828f, 526f);   // 底部让出返回按钮区
                scrollArea.raycastTarget = true;
                scrollArea.gameObject.AddComponent<RectMask2D>();
                GameObject contentObject = new GameObject("stageArchiveContent", typeof(RectTransform));
                contentObject.transform.SetParent(scrollArea.transform, false);
                RectTransform scrollContent = contentObject.GetComponent<RectTransform>();
                scrollContent.anchorMin = new Vector2(0f, 1f);
                scrollContent.anchorMax = new Vector2(1f, 1f);
                scrollContent.pivot = new Vector2(.5f, 1f);
                scrollContent.anchoredPosition = Vector2.zero;
                for (int i = 0; i < count; i++)
                {
                    StageReportRecord report = s.progression.stageReports[i];
                    StageReportRecord selected = report;
                    Button item = UiTheme.MakeButton(scrollContent,
                        "<b>" + ProgressionService.PartTitle(report.part) + "　Day " + report.completedWorldDay +
                        "</b>\n<size=15>" + report.resultSummary + "</size>",
                        () => { ClosePanel(root); ShowStageReport(selected, true); }, 18);
                    // 复审:按钮右缘须在 828 视口内(左 16+宽 796=812,右留白 16 对称)——
                    // 原 820 宽右缘 836 溢出 8px 被 RectMask2D 硬裁掉票券右边框。
                    PlaceTopLeft(item.rectTransform(), 16f, 8f + i * 98f, 796f, 84f);
                    UiTheme.SetButtonStyle(item, new Color32(0x3a, 0x31, 0x2b, 255), UiTheme.TextMain);
                }
                float listHeight = 8f + count * 98f + 8f;
                scrollContent.sizeDelta = new Vector2(0f, Mathf.Max(526f, listHeight));
                ScrollRect scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = scrollArea.rectTransform;
                scroll.content = scrollContent;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 32f;
                scroll.verticalNormalizedPosition = 1f;
            }
            Button close = UiTheme.MakeButton(c, "返回", () => ClosePanel(root), 20);
            close.rectTransform().anchorMin = close.rectTransform().anchorMax = new Vector2(.5f, 0f);
            close.rectTransform().pivot = new Vector2(.5f, 0f);
            close.rectTransform().anchoredPosition = new Vector2(0f, 8f);
            close.rectTransform().sizeDelta = new Vector2(220f, 52f);
        }
    }
}
