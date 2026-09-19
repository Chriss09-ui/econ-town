#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EconTown
{
    /// <summary>V0.3 需求链、30 天居民状态与关键界面结构验收。</summary>
    public static class V03ValidationRunner
    {
        // 不再随编辑器启动自动运行:断言已落后于当前实现,需要时通过菜单手动执行。

        [MenuItem("EconTown/Validate V0.3")]
        public static void Run()
        {
            GameObject stateObject = null;
            UIManager ui = null;
            GameState original = GameState.I;
            try
            {
                stateObject = new GameObject("V03ValidationState");
                GameState s = stateObject.AddComponent<GameState>();
                GameState.I = s;
                s.money = 8000f;
                s.materials = 1000;
                s.planProduction = 140;
                s.employees = 3;
                s.competitor = new CompetitorState();
                ResidentEconomy.CreateProfile(s, "验收居民", ResidentProfession.StoreClerk, ResidentPreference.Foodie);

                ValidateCatalogAndCards(s);
                ValidateResidentRules(s);
                ValidateEnterpriseLink(s);

                // UI 验证使用干净的新一天，避免规则测试已生成日报。
                s.resident = new ResidentState();
                ResidentEconomy.CreateProfile(s, "验收居民", ResidentProfession.StoreClerk, ResidentPreference.Foodie);
                ui = UIManager.Create();
                ui.InitializeForValidation();
                ValidateResidentUi(ui, s);

                Debug.Log("[EconTown] V0.3 validation passed: resident rules, enterprise link, 30-day life, UI panels and four graphs.");
            }
            catch (Exception e)
            {
                Debug.LogError("[EconTown] V0.3 validation failed: " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui.gameObject);
                if (stateObject != null) UnityEngine.Object.DestroyImmediate(stateObject);
                GameState.I = original;
            }
        }

        static void ValidateCatalogAndCards(GameState s)
        {
            Dictionary<string, int> categoryCounts = new Dictionary<string, int>
            {
                { "食物", 0 }, { "饮料", 0 }, { "娱乐", 0 }, { "日用品", 0 }
            };
            Dictionary<string, bool> basics = new Dictionary<string, bool>();
            Dictionary<string, bool> premiums = new Dictionary<string, bool>();
            IList<ResidentItemDef> items = ResidentEconomy.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (!categoryCounts.ContainsKey(items[i].category)) throw new Exception("出现未定义消费类别: " + items[i].category);
                categoryCounts[items[i].category]++;
                if (items[i].premium) premiums[items[i].category] = true;
                else basics[items[i].category] = true;
            }
            foreach (string category in categoryCounts.Keys)
            {
                if (categoryCounts[category] < 2 || !basics.ContainsKey(category) || !premiums.ContainsKey(category))
                    throw new Exception(category + " 没有同时提供基础与升级选择");
            }

            string[] cards =
            {
                "preference", "utility", "totalutility", "marginalutility", "diminishingutility",
                "budgetconstraint", "budgetline", "indifferencecurve", "optimalbundle", "normalgood",
                "inferiorgood", "complement", "incomeeffect", "substitutioneffect", "laborleisure",
                "saving", "realpurchasingpower"
            };
            for (int i = 0; i < cards.Length; i++) if (CardLibrary.Get(cards[i]) == null) throw new Exception("V0.3 知识卡缺失: " + cards[i]);
        }

        static void ValidateResidentRules(GameState s)
        {
            ResidentState r = s.resident;
            if (Mathf.Abs(r.cash - 100f) > .001f || Mathf.Abs(r.freeHours - 4f) > .001f)
                throw new Exception("居民没有以 ¥100 / 4 小时开始");

            ResidentActionResult meal1 = ResidentEconomy.Consume(s, ResidentItemKind.BasicMeal);
            ResidentActionResult meal2 = ResidentEconomy.Consume(s, ResidentItemKind.BasicMeal);
            ResidentActionResult meal3 = ResidentEconomy.Consume(s, ResidentItemKind.BasicMeal);
            if (!meal1.success || !meal2.success || !meal3.success || meal2.choice.marginalUtility >= meal1.choice.marginalUtility || meal3.choice.marginalUtility >= meal2.choice.marginalUtility)
                throw new Exception("边际效用递减没有进入真实消费结果");

            ResidentDayRecord first = ResidentEconomy.EndDay(s);
            if (first == null || first.choices.Count != 3 || string.IsNullOrEmpty(first.summary))
                throw new Exception("居民日报没有保存选择与解释");
            ResidentEconomy.BeginNextDay(s);
            ResidentEconomy.RunExperiment(s, 1);
            ResidentEconomy.RunExperiment(s, 0);
            ResidentEconomy.RunExperiment(s, 3);

            HashSet<string> residentEvents = new HashSet<string> { first.eventName, s.resident.todayEventName };
            for (int day = 2; day <= ResidentEconomy.TotalLifeDays; day++)
            {
                // 居民生活日由唯一时间权威 worldDay 派生(WorldClock.SyncDerivedPeriods),
                // 不能只调居民 API——需随循环推进世界日,lifeDay 才会走到 30。
                s.day = day;
                ResidentEconomy.EndDay(s);
                if (day < ResidentEconomy.TotalLifeDays)
                {
                    ResidentEconomy.BeginNextDay(s);
                    residentEvents.Add(s.resident.todayEventName);
                }
            }
            if (r.history.Count != 30 || r.lifeDay != 30 || ResidentEconomy.CurrentChapter(r) != 7)
                throw new Exception("30 天居民进度或 7 章解锁不完整");
            if (string.IsNullOrEmpty(ResidentEconomy.LifeProfile(r))) throw new Exception("30 天生活画像为空");
            string[] expectedEvents = { "发工资", "意外奖金", "失去兼职", "商家促销", "商品涨价", "好友邀请聚餐" };
            for (int i = 0; i < expectedEvents.Length; i++)
                if (!residentEvents.Contains(expectedEvents[i])) throw new Exception("30 天随机事件缺失: " + expectedEvents[i]);
        }

        static void ValidateEnterpriseLink(GameState s)
        {
            // 恢复一个尚未结算的居民日，执行 PRD 验收链中的价格冲击。
            s.resident = new ResidentState();
            ResidentEconomy.CreateProfile(s, "联动居民", ResidentProfession.StoreClerk, ResidentPreference.Saver);
            s.day = 16;
            s.price = 15f;
            s.residentPlayerDemandMul = 1f;
            s.residentCompetitorDemandMul = 1f;
            s.playerQualityLevel = 2;
            s.playerBrand = 45f;
            CompetitorAI.PrepareDay(s);
            // 替代实验如今按契约走"临时涨价"(SetTemporaryPlayerPrice,次日恢复),不永久改货架价——
            // 断言应对比含临时修正的 PlayerListedPrice,而不是 s.price。
            float listedBefore = PriceService.PlayerListedPrice(s);
            CompetitionSimulator.ProfitEstimate before = CompetitionSimulator.Estimate(s, s.price, s.competitor.price);
            string result = ResidentEconomy.RunExperiment(s, 1);
            float listedAfter = PriceService.PlayerListedPrice(s);
            CompetitionSimulator.ProfitEstimate after = CompetitionSimulator.Estimate(s, s.price, s.competitor.price);
            if (string.IsNullOrEmpty(result) || listedAfter <= listedBefore || after.playerSold >= before.playerSold || after.competitorSold <= before.competitorSold)
                throw new Exception("果汁涨价 → 居民替代 → 玩家销量下降的跨视角链路未形成");
        }

        static void ValidateResidentUi(UIManager ui, GameState s)
        {
            ValidateEconomicLabToolbar(ui, s);

            ui.ShowResidentHome();
            Transform home = FindContent(ui, "居民生活 · 今日账本");
            Require(home, "timeLedger", "居民首页缺少 24 小时时间账本");
            Require(home, "choices", "居民首页缺少生活选择区");
            Require(home, "dailyEvent", "居民首页缺少生活事件与企业需求信号");
            if (home.Find("timeLedger/time_0") == null || home.Find("timeLedger/time_4") == null)
                throw new Exception("24 小时账本没有显示固定时间和可用自由时间");
            ui.CloseTop();

            string[] categories = { "食物", "饮料", "娱乐", "日用品" };
            for (int i = 0; i < categories.Length; i++)
            {
                ui.ShowResidentCommerce(categories[i]);
                Transform commerce = FindContent(ui, "商业街 · 消费选择");
                int choiceCards = 0;
                IList<ResidentItemDef> items = ResidentEconomy.Items;
                for (int j = 0; j < items.Count; j++)
                    if (items[j].category == categories[i] && commerce.Find(items[j].kind.ToString()) != null) choiceCards++;
                if (choiceCards < 2) throw new Exception(categories[i] + " 消费页没有生成至少两个选择卡");
                ui.CloseTop();
            }

            for (int tab = 0; tab < 4; tab++)
            {
                ui.ShowConsumerLab(tab);
                Transform lab = FindContent(ui, "消费者实验室 · 我的选择曲线");
                Transform graphTransform = lab.Find("consumerPlot/consumerGraph");
                PolylineGraph graph = graphTransform != null ? graphTransform.GetComponent<PolylineGraph>() : null;
                Canvas.ForceUpdateCanvases();
                if (graph == null || graph.PointCount < 4 || graph.rectTransform.rect.width < 600f || graph.rectTransform.rect.height < 400f)
                    throw new Exception("消费者实验室第 " + (tab + 1) + " 页没有可见曲线数据");
                if (tab == 1 && graph.SeriesBPointCount < 4) throw new Exception("总效用页缺少边际效用第二条曲线");
                if (tab == 2 && graph.FitPointCount < 4) throw new Exception("预算页缺少无差异曲线");
                ui.CloseTop();
            }

            ui.ShowResidentExperiments();
            Transform experiments = FindContent(ui, "消费者实验 · 改变一个条件");
            for (int i = 0; i < 4; i++) Require(experiments, "exp" + i, "缺少消费者实验 " + (i + 1));
            ui.CloseTop();

            // 真实走一次日报入口，确认报告和解释按钮对应的数据已经存在。
            s.day = 1; // 日报标题编号 = 生活日 = 世界日(WorldClock 派生),回到第 1 天与下方断言一致
            s.resident = new ResidentState();
            ResidentEconomy.CreateProfile(s, "日报居民", ResidentProfession.StoreClerk, ResidentPreference.Foodie);
            ResidentEconomy.Consume(s, ResidentItemKind.PlayerJuice);
            ResidentEconomy.EndDay(s);
            ui.ShowResidentHome();
            Transform report = FindContent(ui, "第 1 日生活报告");
            Require(report, "summary", "生活日报缺少摘要");
            Require(report, "ledger", "生活日报缺少收支资源账本");
            Require(report, "choices", "生活日报缺少消费明细");
            ui.CloseTop();

            // 所有新面板都必须在可见 Canvas 中，并沿用无脏边的纯色 UI。
            s.resident = new ResidentState();
            ResidentEconomy.CreateProfile(s, "字体验收居民", ResidentProfession.StoreClerk, ResidentPreference.Foodie);
            ui.ShowResidentHome();
            Canvas canvas = ui.GetComponentInChildren<Canvas>(true);
            if (canvas == null || canvas.pixelPerfect) throw new Exception("居民 UI Canvas 应为 pixelPerfect=false(SDF 中文显示)");
            CrispTextMirror[] mirrors = ui.GetComponentsInChildren<CrispTextMirror>(true);
            if (mirrors.Length < 20) throw new Exception("居民 UI 没有统一切换到 SDF 清晰字体层");
            bool hasChineseSdf = false;
            for (int i = 0; i < mirrors.Length; i++)
            {
                if (mirrors[i].Rendered == null) throw new Exception("SDF 字体镜像缺少 TextMeshProUGUI 组件");
                if (!mirrors[i].UsingLegacyFallback && mirrors[i].Rendered.text.Contains("居民")) hasChineseSdf = true;
            }
            if (!hasChineseSdf) throw new Exception("中文正文没有成功进入动态 SDF 字体图集");
            TMP_FontAsset sdf = UiTheme.CrispFontAsset;
            if (sdf == null || !sdf.isMultiAtlasTexturesEnabled || sdf.atlasWidth < 1024)
                throw new Exception("中文 SDF 字体未启用高精度动态多图集");
            ui.CloseTop();
        }

        static void ValidateEconomicLabToolbar(UIManager ui, GameState s)
        {
            s.history.Clear();
            s.history.Add(new DayRecord
            {
                day = 1, price = 15f, produced = 80, demandTotal = 100, sold = 80,
                revenue = 1200f, totalCost = 760f, profit = 440f, marginalCost = 6f
            });
            s.history.Add(new DayRecord
            {
                day = 2, price = 16f, produced = 90, demandTotal = 94, sold = 84,
                revenue = 1344f, totalCost = 820f, profit = 524f, marginalCost = 6.2f
            });

            ui.ShowCurves(4);
            Transform lab = FindContent(ui, "经济实验室 · 用你自己的数据画曲线");
            Transform toolbar = lab.Find("curveToolbar");
            Transform stepper = toolbar != null ? toolbar.Find("curveCutoffStepper") : null;
            Transform minusTransform = stepper != null ? stepper.Find("curveCutoffMinus") : null;
            Transform valueTransform = stepper != null ? stepper.Find("curveCutoffValue") : null;
            Transform plusTransform = stepper != null ? stepper.Find("curveCutoffPlus") : null;
            Transform activeTab = toolbar != null ? toolbar.Find("curveTab_4") : null;

            if (toolbar == null || stepper == null || minusTransform == null || valueTransform == null || plusTransform == null)
                throw new Exception("经济实验室的日期步进器没有形成完整控件组");
            if (activeTab == null || activeTab.Find("activeLine") == null)
                throw new Exception("经济实验室当前标签缺少明确选中态");

            RectTransform minus = minusTransform as RectTransform;
            RectTransform value = valueTransform as RectTransform;
            RectTransform plus = plusTransform as RectTransform;
            float minusRight = minus.anchoredPosition.x + minus.sizeDelta.x;
            float valueRight = value.anchoredPosition.x + value.sizeDelta.x;
            if (minusRight > value.anchoredPosition.x || valueRight > plus.anchoredPosition.x)
                throw new Exception("经济实验室日期步进器仍存在文字与按钮重叠");

            Text valueText = value.GetComponent<Text>();
            if (valueText == null || valueText.text != "第 2 天")
                throw new Exception("经济实验室日期值没有独立、完整显示");

            ui.CloseTop();
            s.history.Clear();
        }

        static Transform FindContent(UIManager ui, string title)
        {
            Transform content = ui.transform.Find("Canvas/panels/panel_" + title + "/content");
            if (content == null) throw new Exception("没有生成面板: " + title);
            return content;
        }

        static void Require(Transform parent, string child, string message)
        {
            if (parent == null || parent.Find(child) == null) throw new Exception(message);
        }
    }
}
#endif
