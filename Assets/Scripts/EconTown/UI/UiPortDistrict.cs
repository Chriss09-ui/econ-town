using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public partial class UIManager
    {
        /// <summary>V0.7.2 报关行小黑板：只读 PortPresentationService，与世界经济页共用贸易账本。</summary>
        public void ShowCustomsBoard()
        {
            GameState s = GameState.I;
            if (s == null) return;
            InternationalTradeService.Ensure(s);
            ProgressionService.Ensure(s);

            GameObject root = BeginPanel("报关行 · 港口小黑板", 980f, 720f);
            RectTransform c = ContentOf(root);
            if (s.progression.completedChapter < InternationalTradeService.ExportUnlockChapter)
            {
                RectTransform locked = WorldCard(c, "港区施工中", 16f, 20f, 900f, 470f, WorldGold);
                ShopText(locked,
                    "岸吊、泊位与报关行正在施工。\n\n完成第六篇「钱、银行与央行」后，围挡会在次晨拆除，\n这里将用真实贸易账本显示出口装船、进口上岸与净出口 NX。",
                    22, UiTheme.TextMain, 44f, 108f, 812f, 220f, TextAnchor.MiddleCenter);
                CustomsFooterButtons(c, root);
                return;
            }

            PortDisplaySnapshot snapshot = PortPresentationService.Read(s);
            RectTransform exports = WorldCard(c, "进出口装卸换算", 16f, 20f, 438f, 256f, WorldGold);
            if (snapshot.row == null)
                ShopText(exports, "尚无已结算的贸易账本。\n完成一天结算后，码头才会出现可核对的箱量。",
                    19, UiTheme.TextSub, 24f, 80f, 388f, 100f, TextAnchor.MiddleCenter);
            else
                ShopText(exports,
                    "你的出口：今日 " + snapshot.playerExportCups.ToString("0") + " 杯\n" +
                    "近 7 日 " + snapshot.playerExportsLastSevenDays.ToString("0") + " 杯 ≈ " +
                    snapshot.exportContainerCount + " 个橙箱\n" +
                    "全镇出口（含商行）：¥" + snapshot.townExportValue.ToString("0") + "\n\n" +
                    "最近 3 日进口 M：¥" + snapshot.importsLastThreeDays.ToString("0") + "\n" +
                    "进口堆场：" + snapshot.importContainerCount + " 个蓝箱",
                    19, UiTheme.TextMain, 24f, 50f, 388f, 178f, TextAnchor.UpperLeft);

            RectTransform rates = WorldCard(c, "今日通关参数", 478f, 20f, 438f, 256f);
            ShopText(rates,
                "当日汇率：1 B币 = " + snapshot.exchangeRate.ToString("0.00") + " A币\n" +
                "你的出口配额：" + snapshot.exportQuotaCups + " 杯/日\n" +
                "岸吊档位：" + CraneTierName(snapshot.craneTier) +
                "（周期 " + snapshot.cranePeriodSeconds.ToString("0.0") + " 秒）\n" +
                "B 国状态：" + (snapshot.recessionActive ? "衰退中" : "正常"),
                19, UiTheme.TextMain, 24f, 56f, 388f, 158f, TextAnchor.UpperLeft);

            RectTransform teaching = WorldCard(c, "净出口不是单向的好坏分数", 16f, 296f, 900f, 224f,
                snapshot.row != null && snapshot.row.netExports >= 0f ? UiTheme.Good : UiTheme.Accent);
            string body = snapshot.row == null
                ? "NX 会在第一条贸易账本形成后显示。"
                : PortPresentationService.NetExportsTeachingLine(snapshot);
            if (snapshot.recessionActive)
                body += "\n\nB 国需求收缩中：理想订单 ×0.82（当前订单若仍高于你的供给上限，出货暂未受影响）。";
            Text teachingText = ShopText(teaching, body, 20, UiTheme.TextMain, 28f, 56f, 840f, 128f, TextAnchor.UpperLeft);
            float teachingShift = ExpandTopText(teachingText);
            if (teachingShift > 1f)
            {
                teaching.sizeDelta = new Vector2(teaching.sizeDelta.x, teaching.sizeDelta.y + teachingShift);
                GrowPanel(root, teachingShift);
            }

            CustomsFooterButtons(c, root, 548f + teachingShift);
        }

        static string CraneTierName(PortCraneTier tier)
        {
            if (tier == PortCraneTier.Slow) return "慢速";
            if (tier == PortCraneTier.Medium) return "中速";
            if (tier == PortCraneTier.Full) return "满速";
            return "静止 / 微摆";
        }

        void CustomsFooterButtons(RectTransform parent, GameObject root, float y = 548f)
        {
            Button economy = UiTheme.MakeButton(parent, "打开世界经济中心",
                () => { ClosePanel(root); ShowWorldEconomy(0); }, 18);
            PlaceTopLeft(economy.rectTransform(), 56f, y, 400f, 52f);
            UiTheme.SetButtonStyle(economy, WorldGold, UiTheme.Ink);

            Button close = UiTheme.MakeButton(parent, "返回码头", () => ClosePanel(root), 19);
            PlaceTopLeft(close.rectTransform(), 476f, y, 400f, 52f);
            UiTheme.SetButtonStyle(close, WorldTeal, Color.white);
        }
    }
}
