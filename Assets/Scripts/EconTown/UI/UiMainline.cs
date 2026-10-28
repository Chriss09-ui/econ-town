using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public partial class UIManager
    {
        public bool TryShowMorningMainline(GameState state)
        {
            if (state == null || CutsceneActive || IsAnyPanelOpen) return false;
            if (state.phase != GamePhase.Morning) return false;
            ProgressionService.Ensure(state);
            if (state.progression.mainlineComplete) return false;
            bool handoff = state.progression.pendingHandoffChapter > 0;
            if (!handoff && state.progression.mainlineCueSeenDay == state.day) return false;
            ShowMainlineMorningCard(state, handoff);
            return true;
        }

        void ShowMainlineMorningCard(GameState state, bool handoff)
        {
            MainlineGuide.Cue cue = MainlineGuide.Build(state);
            string title = handoff ? "这一章做完了" : "今天只做这一件事";
            GameObject root = BeginPanel(title, 820f, 520f);
            RectTransform content = ContentOf(root);
            string body = handoff
                ? MainlineGuide.HandoffText(state) + "\n\n下一步只做一件事：\n" + cue.line
                : cue.line;
            if (!string.IsNullOrEmpty(cue.detail)) body += "\n\n" + cue.detail;
            body += "\n\n" + MainlineGuide.RoleSentence(state);
            Text text = UiTheme.MakeText(content, body, 22, UiTheme.TextMain, TextAnchor.UpperLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24f, 92f);
            text.rectTransform.offsetMax = new Vector2(-24f, -12f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            FitTextAboveFooter(text);

            Button go = UiTheme.MakeButton(content, "去做", () =>
            {
                DismissMainlineCard(state);
                ClosePanel(root);
                GoToMainline(cue);
            }, 22);
            go.rectTransform().anchorMin = go.rectTransform().anchorMax = new Vector2(0f, 0f);
            go.rectTransform().pivot = new Vector2(0f, 0f);
            go.rectTransform().anchoredPosition = new Vector2(150f, 16f);
            go.rectTransform().sizeDelta = new Vector2(240f, 56f);
            UiTheme.SetButtonStyle(go, UiTheme.Accent, UiTheme.Ink);

            Button later = UiTheme.MakeButton(content, "先逛逛", () =>
            {
                DismissMainlineCard(state);
                ClosePanel(root);
            }, 22);
            later.rectTransform().anchorMin = later.rectTransform().anchorMax = new Vector2(0f, 0f);
            later.rectTransform().pivot = new Vector2(0f, 0f);
            later.rectTransform().anchoredPosition = new Vector2(420f, 16f);
            later.rectTransform().sizeDelta = new Vector2(240f, 56f);
        }

        void DismissMainlineCard(GameState state)
        {
            if (state == null || state.progression == null) return;
            state.progression.mainlineCueSeenDay = state.day;
            state.progression.pendingHandoffChapter = 0;
            state.Changed();
        }

        public void GoToMainline(MainlineGuide.Cue cue)
        {
            GameState state = GameState.I;
            switch (cue.destination)
            {
                case MainlineDestination.Shop:
                    ShowShop();
                    break;
                case MainlineDestination.Market:
                    ShowMarketDashboard();
                    break;
                case MainlineDestination.Resident:
                    ShowResidentHome();
                    break;
                case MainlineDestination.Government:
                    ShowGovernmentDashboard(cue.tab);
                    break;
                case MainlineDestination.Macro:
                    ShowMacroDashboard(cue.tab);
                    break;
                case MainlineDestination.Finance:
                    ShowFinanceCenter(cue.tab);
                    break;
                case MainlineDestination.CentralBank:
                    ShowFinanceCenter(cue.tab);
                    break;
                case MainlineDestination.World:
                    ShowWorldEconomy(cue.tab);
                    break;
                case MainlineDestination.Port:
                    if (MainlineGuide.CanEnterTrade(state)) PortSceneController.EnterPort();
                    else ShowToast(MainlineGuide.TradeLockReason());
                    break;
                case MainlineDestination.Housing:
                    ShowHousing();
                    break;
                case MainlineDestination.Assets:
                    ShowAssetAllocation();
                    break;
                case MainlineDestination.Competition:
                    ShowCompetitionIntro();
                    break;
                case MainlineDestination.PriceWar:
                    ShowPriceWarChoice();
                    break;
                case MainlineDestination.MarketExperiments:
                    ShowMarketExperiments();
                    break;
                case MainlineDestination.Comparative:
                    ShowComparativeAdvantageLab();
                    break;
                case MainlineDestination.Trilemma:
                    ShowTrilemmaLab();
                    break;
            }
        }

        void OpenDock(int index)
        {
            GameState state = GameState.I;
            if (state == null) return;
            string block = MainlineGuide.DockBlockReason(state, index);
            if (!string.IsNullOrEmpty(block))
            {
                ShowToast(block);
                return;
            }
            switch (index)
            {
                case 0: ShowProgression(); break;
                case 1:
                    ShowShop();
                    break;
                case 2:
                    ShowMarketDashboard();
                    break;
                case 3: ShowCurves(0); break;
                case 4:
                    ShowResidentHome();
                    break;
                case 5:
                    ShowGovernmentDashboard();
                    break;
                case 6:
                    ShowMacroDashboard();
                    break;
                case 7:
                    ShowFinanceCenter();
                    break;
                case 8: ShowHandbook(); break;
            }
        }

        void ApplyDockSpotlight(MainlineGuide.Cue cue)
        {
            if (dockButtons == null) return;
            GameState state = GameState.I;
            for (int i = 0; i < dockButtons.Length; i++)
            {
                Button button = dockButtons[i];
                if (button == null) continue;
                bool blocked = state != null && !string.IsNullOrEmpty(MainlineGuide.DockBlockReason(state, i));
                bool spotlight = cue.dockIndex == i;
                if (spotlight) UiTheme.SetButtonStyle(button, UiTheme.Accent, UiTheme.Ink);
                else if (blocked) UiTheme.SetButtonStyle(button, new Color32(0x3a, 0x34, 0x30, 255), new Color32(0xb0, 0xa2, 0x90, 255));
                else UiTheme.SetButtonStyle(button, UiTheme.Paper, UiTheme.Ink);
            }
        }
    }
}
