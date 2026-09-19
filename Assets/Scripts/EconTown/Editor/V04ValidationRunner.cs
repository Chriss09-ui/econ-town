#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.4 政府规则、地图建筑和七栏市政 UI 的结构与可读性验收。
    /// 注意:这里的 UI 断言描述"当前实现应有的样子",实现演进时需同步更新断言(手动验收工具,不进 CI)。</summary>
    public static class V04ValidationRunner
    {
        [MenuItem("EconTown/Validate V0.4")]
        public static void Run()
        {
            GameState previous = GameState.I;
            GameObject stateObject = null;
            GameObject mapObject = null;
            UIManager ui = null;
            try
            {
                stateObject = new GameObject("V04ValidationState");
                GameState state = stateObject.AddComponent<GameState>();
                GameState.I = state;
                state.materials = 1000;
                state.planProduction = 120;
                state.employees = 2;
                state.price = 15f;
                state.competitor = new CompetitorState();
                GovernmentSimulator.Ensure(state);

                ValidateGovernmentRules(state);
                ValidateMapBuildings(ref mapObject, state);

                ui = UIManager.Create();
                ui.InitializeForValidation();
                ValidateGovernmentUi(ui, state);

                Debug.Log("[EconTown] V0.4 validation passed: policy chains, fiscal/debt flow, town buildings, seven government tabs, graphs, events and term report.");
            }
            catch (Exception e)
            {
                Debug.LogError("[EconTown] V0.4 validation failed: " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui.gameObject);
                if (mapObject != null) UnityEngine.Object.DestroyImmediate(mapObject);
                if (stateObject != null) UnityEngine.Object.DestroyImmediate(stateObject);
                GameState.I = previous;
            }
        }

        static void ValidateGovernmentRules(GameState state)
        {
            float baselineConsumerPrice = GovernmentSimulator.DrinkConsumerPrice(state, state.price);
            GovernmentSimulator.SetPolicy(state, PolicyType.DrinkTax, 3f);
            DayRecord taxed = DaySimulator.Settle(state, DaySimulator.RollDemand(state), DaySimulator.Production(state));
            if (taxed.consumerPrice <= baselineConsumerPrice || taxed.producerNetPrice >= state.price || taxed.salesTaxRevenue <= 0f)
                throw new Exception("饮品税没有同时传导到消费者价、企业实收与政府税收");
            if (Mathf.Abs(ResidentEconomy.Price(state, ResidentItemKind.PlayerJuice) - taxed.consumerPrice) > .01f)
                throw new Exception("居民与企业没有读取同一份税后饮品价格");

            GovernmentSimulator.RunFreeRiderExperiment(state);
            if (state.government.chapter < 2 || state.government.freeRiderFundsRaised >= 1000f)
                throw new Exception("公共品搭便车实验没有形成筹资失败与章节推进");
            GovernmentSimulator.SetPolicy(state, PolicyType.PollutionTax, 4f);
            float pollutionBefore = state.government.factory.pollution;
            GovernmentCycleRecord cycle = GovernmentSimulator.AdvanceCycle(state);
            if (cycle.pollutionTaxRevenue <= 0f || state.government.factory.pollution >= pollutionBefore)
                throw new Exception("污染税没有形成财政收入与污染下降的联动");
        }

        static void ValidateMapBuildings(ref GameObject mapObject, GameState state)
        {
            GovernmentSimulator.BuildProject(state, PublicProjectType.StreetLights);
            mapObject = new GameObject("V04ValidationMap");
            MapBuilder.Build(mapObject.transform);
            Transform town = mapObject.transform.Find("Town");
            if (town == null || town.Find("B_市政厅_hall") == null || town.Find("B_河畔工厂_factory") == null ||
                town.Find("B_职业培训中心_training") == null)
                throw new Exception("小镇地图缺少市政厅、河畔工厂或职业培训中心");
            if (town.Find("PublicProjects/public_light_west") == null || town.Find("PublicProjects/public_light_east") == null)
                throw new Exception("公共项目建成后没有反馈到小镇地图");
            if (MapBuilder.HallDoor == MapBuilder.FactoryDoor || MapBuilder.HallDoor == MapBuilder.TrainingDoor)
                throw new Exception("V0.4 建筑交互点发生重叠");
        }

        static void ValidateGovernmentUi(UIManager ui, GameState state)
        {
            string[] expectedCards =
            {
                "govCard_本周期财政账本", "govCard_饮品市场政策", "govCard_补贴组合",
                "govCard_最低工资实验", "govCard_工厂治理", "govCard_免费搭便车实验", "govCard_城市脉搏"
            };
            int[] graphTabs = { 1, 4, 6 };
            for (int tab = 0; tab < 7; tab++)
            {
                ui.ShowGovernmentDashboard(tab);
                Transform content = FindDashboard(ui);
                if (content.Find(expectedCards[tab]) == null)
                    throw new Exception("市政府第 " + (tab + 1) + " 栏缺少主内容卡: " + expectedCards[tab]);
                AssertTabStrip(content);
                AssertNoTopLevelOverflow(content);
                for (int i = 0; i < graphTabs.Length; i++)
                    if (tab == graphTabs[i]) AssertGraph(content, tab);
                ui.CloseTop();
            }

            ui.ShowGovernmentDashboard(1);
            Transform taxContent = FindDashboard(ui);
            Transform taxRow = taxContent.Find("govCard_饮品市场政策/policy_DrinkTax");
            if (taxRow == null || taxRow.GetComponentsInChildren<Button>(true).Length < 3)
                throw new Exception("饮品税控制行缺少影响、减号或加号按钮");
            ui.CloseTop();

            // 事件页必须提供三个明确选择。
            state.government.currentEvent = new GovernmentEventState
            {
                id = "validation", title = "验收事件", description = "测试三选一事件布局。",
                optionA = "方案甲", optionB = "方案乙", optionC = "方案丙"
            };
            ui.ShowGovernmentEvent();
            Transform eventContent = ui.transform.Find("Canvas/panels/panel_government_event/content");
            if (eventContent == null || eventContent.GetComponentsInChildren<Button>(true).Length < 3 ||
                eventContent.parent.GetComponentsInChildren<Button>(true).Length < 4)
                throw new Exception("城市事件页没有三个政策选项和关闭路径");
            AssertNoTopLevelOverflow(eventContent);
            ui.CloseTop();
            state.government.currentEvent.resolved = true;

            // 任期报告不是评分惩罚页，而是四维结果与风格总结。
            state.government.termReportReady = true;
            ui.ShowMayorTermReport();
            Transform report = ui.transform.Find("Canvas/panels/panel_市长任期报告 · " + GovernmentSimulator.TermStyle(state.government) + "/content");
            if (report == null || report.Find("grade_经济活力") == null || report.Find("grade_居民福祉") == null ||
                report.Find("grade_环境质量") == null || report.Find("grade_财政健康") == null || report.Find("govCard_你的政策画像") == null)
                throw new Exception("任期报告缺少四维结果或政策画像");
            AssertNoTopLevelOverflow(report);

            Canvas canvas = ui.GetComponentInChildren<Canvas>(true);
            if (canvas == null || canvas.pixelPerfect) throw new Exception("市政府 UI Canvas 应为 pixelPerfect=false(SDF 中文显示)");
            CrispTextMirror[] mirrors = report.parent.GetComponentsInChildren<CrispTextMirror>(true);
            if (mirrors.Length < 12) throw new Exception("市政府 UI 没有统一使用清晰 SDF 中文字体");
            for (int i = 0; i < mirrors.Length; i++)
                if (mirrors[i].Source != null && mirrors[i].Source.fontSize < UiTheme.MinimumBodyFontSize)
                    throw new Exception("市政府 UI 出现低于最小字号的文字");
            ui.CloseAll();
            if (ui.InputBlocked) throw new Exception("关闭市政府面板后人物仍被锁定");
        }

        static Transform FindDashboard(UIManager ui)
        {
            Transform content = ui.transform.Find("Canvas/panels/panel_government_dashboard/content");
            if (content == null) throw new Exception("没有生成市政府主面板");
            return content;
        }

        static void AssertTabStrip(Transform content)
        {
            string[] tabs = { "财政总览", "税收与限价", "补贴与福利", "劳动就业", "环境治理", "公共建设", "城市数据" };
            float previousRight = float.NegativeInfinity;
            for (int i = 0; i < tabs.Length; i++)
            {
                RectTransform tab = content.Find("btn_" + tabs[i]) as RectTransform;
                if (tab == null) throw new Exception("市政府缺少栏目按钮: " + tabs[i]);
                float left = tab.anchoredPosition.x;
                if (left < previousRight - .1f) throw new Exception("市政府栏目按钮发生重叠: " + tabs[i]);
                previousRight = left + tab.sizeDelta.x;
            }
        }

        static void AssertGraph(Transform content, int tab)
        {
            Canvas.ForceUpdateCanvases();
            GovernmentPolicyGraph graph = content.GetComponentInChildren<GovernmentPolicyGraph>(true);
            if (graph == null || graph.rectTransform.rect.width < 500f || graph.rectTransform.rect.height < 280f)
                throw new Exception("市政府第 " + (tab + 1) + " 栏没有生成可读的政策曲线");
        }

        static void AssertNoTopLevelOverflow(Transform content)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform contentRect = content as RectTransform;
            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(contentRect, child);
                Rect r = contentRect.rect;
                if (bounds.min.x < r.xMin - 3f || bounds.max.x > r.xMax + 3f ||
                    bounds.min.y < r.yMin - 3f || bounds.max.y > r.yMax + 3f)
                    throw new Exception("UI 元素越出面板: " + child.name);
            }
        }
    }
}
#endif
