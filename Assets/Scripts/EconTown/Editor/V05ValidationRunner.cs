#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>V0.5 宏观核算、微观联动、五页宏观 UI 与可读性验收。
    /// 注意:这里的 UI 断言描述"当前实现应有的样子",实现演进时需同步更新断言(手动验收工具,不进 CI)。</summary>
    public static class V05ValidationRunner
    {
        [MenuItem("EconTown/Validate V0.5")]
        public static void Run()
        {
            GameState previous = GameState.I;
            GameObject stateObject = null;
            UIManager ui = null;
            try
            {
                stateObject = new GameObject("V05ValidationState");
                GameState state = stateObject.AddComponent<GameState>();
                GameState.I = state;
                state.materials = 1200;
                state.planProduction = 130;
                state.employees = 2;
                state.price = 16f;
                state.competitor = new CompetitorState();
                GovernmentSimulator.Ensure(state);
                ResidentEconomy.Ensure(state);
                MacroEconomySimulator.Ensure(state);

                SeedLinkedMicroData(state);
                ValidateAccountingAndChains(state);
                ValidateKnowledgeCards();

                ui = UIManager.Create();
                ui.InitializeForValidation();
                ValidateMacroUi(ui, state);

                Debug.Log("[EconTown] V0.5 validation passed: micro-to-macro accounting, GDP/CPI/labor definitions, shocks, fiscal lag, 30 cards, five macro pages, graphs and drill-down navigation.");
            }
            catch (Exception e)
            {
                Debug.LogError("[EconTown] V0.5 validation failed: " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui.gameObject);
                if (stateObject != null) UnityEngine.Object.DestroyImmediate(stateObject);
                GameState.I = previous;
            }
        }

        static void SeedLinkedMicroData(GameState state)
        {
            state.resident.history.Add(new ResidentDayRecord
            {
                day = 1, income = 72f, spending = 48f, saved = 24f, happinessEnd = 69f
            });
            state.history.Add(new DayRecord
            {
                day = 1, price = 16f, consumerPrice = 16f, sold = 82, soldTourists = 12,
                revenue = 1312f, profit = 260f, materialCost = 420f, capitalInvestment = 180f,
                exports = 72f, imports = 96f, competitionActive = true, competitorSold = 64,
                competitorPrice = 18f
            });
            state.government.history.Add(new GovernmentCycleRecord
            {
                cycle = 1, taxRevenue = 900f, spending = 1040f, budgetBalance = -140f,
                publicServiceSpending = 280f, capitalSpending = 360f, debtEnd = 140f,
                employmentRate = 91f, enterpriseConfidence = 70f
            });
        }

        static void ValidateAccountingAndChains(GameState state)
        {
            MacroPeriodRecord baseline = MacroEconomySimulator.AdvancePeriod(state);
            float identity = baseline.consumption + baseline.investment + baseline.governmentSpending + baseline.netExports;
            if (Mathf.Abs(identity - baseline.nominalGdp) > .01f)
                throw new Exception("GDP 与 C + I + G + NX 不相等");
            if (Mathf.Abs(MacroEconomySimulator.ValueAddedGdp(2f, 5f, 10f) - 10f) > .01f)
                throw new Exception("增加值核算重复计算了中间品");
            if (baseline.employment + baseline.unemployment != baseline.laborForce ||
                baseline.laborForce > baseline.workingAgePopulation)
                throw new Exception("就业、失业或劳动力人口定义不一致");

            // 一日一期契约:同日重复 AdvancePeriod 会命中幂等护栏直接返回缓存,
            // 冲击/财政方案必须推进到新的一期才会真正应用。
            state.day = 2;
            MacroEconomySimulator.QueueShock(state, MacroShockType.EnergyCrisis);
            MacroPeriodRecord stagflation = MacroEconomySimulator.AdvancePeriod(state);
            if (stagflation.inflation <= 0f || stagflation.unemploymentRate <= 0f ||
                !stagflation.causalChain.Contains("通胀↑") || !stagflation.causalChain.Contains("失业↑"))
                throw new Exception("能源冲击没有形成 GDP/就业/通胀的滞胀传导");

            MacroEconomySimulator.SetFiscalPolicy(state, FiscalStance.Expansionary, 1000f, -2f);
            state.day = 3;
            int lagBefore = state.macro.fiscalLagStage;
            // 真实管线里时滞由 WorldSettlementService 在收日时推进;直调 AdvancePeriod 需手动补这一步。
            FiscalPolicyService.AdvanceLag(state.macro);
            MacroEconomySimulator.AdvancePeriod(state);
            if (state.macro.fiscalLagStage <= lagBefore || state.macro.fiscalMultiplier <= 1f ||
                !state.macro.policyPreview.Contains("赤字"))
                throw new Exception("扩张财政缺少乘数、时滞或风险预览");
        }

        static void ValidateKnowledgeCards()
        {
            string[] ids =
            {
                "gdp", "valueadded", "nominalgdp", "realgdp", "percapitagdp", "consumption", "investment",
                "governmentspending", "netexports", "cpi", "inflation", "unemploymentrate",
                "frictionalunemployment", "structuralunemployment", "cyclicalunemployment", "businesscycle",
                "potentialgdp", "outputgap", "aggregatedemand", "aggregatesupply", "demandshock", "supplyshock",
                "stagflation", "expansionaryfiscal", "contractionaryfiscal", "fiscalmultiplier", "mpc",
                "automaticstabilizer", "productivity", "longrungrowth"
            };
            HashSet<string> unique = new HashSet<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                if (CardLibrary.Get(ids[i]) == null) throw new Exception("缺少宏观知识卡: " + ids[i]);
                if (!unique.Add(ids[i])) throw new Exception("宏观知识卡 ID 重复: " + ids[i]);
            }
        }

        static void ValidateMacroUi(UIManager ui, GameState state)
        {
            string[] cards =
            {
                "macroCard_GDP 支出构成", "macroCard_CPI 固定消费篮子", "macroCard_劳动力人口怎么计算",
                "macroCard_AD–AS 总体经济图", "macroCard_财政立场"
            };
            string[] tabs = { "国民经济", "物价与通胀", "劳动就业", "总需求 / 总供给", "财政政策" };
            for (int tab = 0; tab < 5; tab++)
            {
                ui.ShowMacroDashboard(tab);
                Transform content = FindDashboard(ui);
                if (content.Find(cards[tab]) == null)
                    throw new Exception("宏观中心第 " + (tab + 1) + " 页缺少主卡片: " + cards[tab]);
                AssertTabStrip(content, tabs);
                AssertNoTopLevelOverflow(content);
                MacroEconomyGraph graph = content.GetComponentInChildren<MacroEconomyGraph>(true);
                if (graph == null || graph.rectTransform.rect.width < 500f || graph.rectTransform.rect.height < 80f)
                    throw new Exception("宏观中心第 " + (tab + 1) + " 页没有生成可读图表");
                ui.CloseTop();
            }

            ui.ShowMacroDashboard(3);
            Transform shockPage = FindDashboard(ui);
            if (shockPage.Find("macroCard_冲击实验") == null ||
                shockPage.Find("macroCard_冲击实验").GetComponentsInChildren<Button>(true).Length < 8)
                throw new Exception("AD-AS 页缺少七种冲击或概念入口");
            ui.CloseTop();

            ui.ShowMacroDashboard(4);
            Transform fiscalPage = FindDashboard(ui);
            if (fiscalPage.Find("macroCard_财政立场").GetComponentsInChildren<Button>(true).Length < 5)
                throw new Exception("财政页缺少立场与公共投资控制");
            Canvas canvas = ui.GetComponentInChildren<Canvas>(true);
            if (canvas == null || canvas.pixelPerfect) throw new Exception("宏观 UI Canvas 应为 pixelPerfect=false(SDF 中文显示)");
            CrispTextMirror[] mirrors = fiscalPage.parent.GetComponentsInChildren<CrispTextMirror>(true);
            if (mirrors.Length < 20) throw new Exception("宏观 UI 没有统一使用清晰 SDF 中文字体");
            for (int i = 0; i < mirrors.Length; i++)
                if (mirrors[i].Source != null && mirrors[i].Source.fontSize < UiTheme.MinimumBodyFontSize)
                    throw new Exception("宏观 UI 出现低于最小字号的文字");
            ui.CloseAll();
            if (ui.InputBlocked) throw new Exception("关闭宏观中心后人物仍被锁定");
        }

        static Transform FindDashboard(UIManager ui)
        {
            Transform content = ui.transform.Find("Canvas/panels/panel_macro_dashboard/content");
            if (content == null) throw new Exception("没有生成宏观经济主面板");
            Canvas.ForceUpdateCanvases();
            return content;
        }

        static void AssertTabStrip(Transform content, string[] tabs)
        {
            float previousRight = float.NegativeInfinity;
            for (int i = 0; i < tabs.Length; i++)
            {
                RectTransform tab = content.Find("macroTab_" + i) as RectTransform;
                if (tab == null) throw new Exception("宏观中心缺少页签: " + tabs[i]);
                float left = tab.anchoredPosition.x;
                if (left < previousRight - .1f) throw new Exception("宏观中心页签重叠: " + tabs[i]);
                previousRight = left + tab.sizeDelta.x;
            }
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
                    throw new Exception("UI 元素越出宏观面板: " + child.name);
            }
        }
    }
}
#endif
