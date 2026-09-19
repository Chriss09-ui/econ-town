#if UNITY_EDITOR

using NUnit.Framework;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public class EconTownWebGLFontTests
    {
        [Test]
        public void BundledFontProvidesChineseGlyphsForLegacyAndTmpText()
        {
            Font bundled = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Assert.IsNotNull(bundled, "WebGL 必须携带项目内中文字库，不能依赖操作系统字体。");
            Assert.AreSame(bundled, UiTheme.GameFont, "UI 应优先使用随项目打包的中文字库。");

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(bundled);
            Assert.IsNotNull(fontAsset, "内置中文字库必须能创建 TMP 动态 SDF 字体。");
            try
            {
                Assert.IsTrue(fontAsset.TryAddCharacters("经世小镇", out string missingCharacters),
                    "内置字体缺少必要中文字符：" + missingCharacters);
            }
            finally
            {
                Object.DestroyImmediate(fontAsset);
            }
        }

        [Test]
        public void CompactShopLabelsDoNotTruncateNotoLineMetrics()
        {
            UIManager ui = UIManager.Create();
            GameObject parentObject = new GameObject("shopTextTest", typeof(RectTransform));
            try
            {
                MethodInfo shopText = typeof(UIManager).GetMethod("ShopText",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(shopText, "经营面板文字工厂缺失。");

                Text label = (Text)shopText.Invoke(ui, new object[]
                {
                    parentObject.GetComponent<RectTransform>(), "今日菜单", 22, UiTheme.TextMain,
                    0f, 0f, 180f, 28f, TextAnchor.MiddleLeft, FontStyle.Bold
                });

                Assert.AreEqual(VerticalWrapMode.Overflow, label.verticalOverflow,
                    "紧凑中文标签不得因 Noto 行高超过 RectTransform 而整行被裁掉。");
            }
            finally
            {
                Object.DestroyImmediate(parentObject);
                if (ui != null) Object.DestroyImmediate(ui.gameObject);
            }
        }

        [Test]
        public void CompactButtonsDoNotTruncateNotoLineMetrics()
        {
            GameObject parentObject = new GameObject("buttonTextTest", typeof(RectTransform));
            try
            {
                Button button = UiTheme.MakeButton(parentObject.transform, "增加杯数", null, 17);
                Text label = button.GetComponentInChildren<Text>();

                Assert.IsNotNull(label, "按钮必须包含可见文字。");
                Assert.AreEqual(VerticalWrapMode.Overflow, label.verticalOverflow,
                    "WebGL 紧凑按钮不得因 Noto 行高而裁掉整行。");
            }
            finally
            {
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void UnlockedShopStepperRendersAndRefreshesPlannedCupCount()
        {
            GameObject stateObject = null;
            UIManager ui = null;
            try
            {
                stateObject = new GameObject("unlockedShopFontTest");
                GameState state = stateObject.AddComponent<GameState>();
                GameState.I = state;
                state.progression = new ProgressionState { currentChapter = 4, completedChapter = 3 };
                state.phase = GamePhase.Morning;
                state.day = 4;
                state.planProduction = 80;
                state.employees = 2;
                state.materials = 300;

                ui = UIManager.Create();
                ui.InitializeForValidation();
                ui.ShowShop();
                Canvas.ForceUpdateCanvases();

                Transform content = ui.transform.Find("Canvas/panels/panel_果汁店 · 今日经营/content");
                Assert.IsNotNull(content, "解锁后必须能创建果汁店经营面板。");
                Text[] texts = content.GetComponentsInChildren<Text>(true);
                Text productionLabel = TextByValue(texts, "计划制作");
                Text productionValue = TextByValue(texts, "80 杯");
                Assert.IsNotNull(productionLabel, "解锁后必须显示“计划制作”。");
                Assert.IsNotNull(productionValue, "解锁后必须显示当前计划杯数。");
                Assert.AreSame(UiTheme.GameFont, productionLabel.font);
                Assert.AreSame(UiTheme.GameFont, productionValue.font);
                Assert.AreEqual(VerticalWrapMode.Overflow, productionLabel.verticalOverflow);
                Assert.AreEqual(VerticalWrapMode.Overflow, productionValue.verticalOverflow);

                RectTransform productionRow = productionLabel.transform.parent as RectTransform;
                Assert.IsNotNull(productionRow);
                Button plus = ButtonByLabel(productionRow.GetComponentsInChildren<Button>(true), "+");
                Button minus = ButtonByLabel(productionRow.GetComponentsInChildren<Button>(true), "-");
                Assert.IsNotNull(minus, "计划制作行缺少减号按钮。");
                Assert.IsNotNull(plus, "计划制作行缺少加号按钮。");
                Assert.AreEqual(VerticalWrapMode.Overflow, minus.GetComponentInChildren<Text>().verticalOverflow);
                Assert.AreEqual(VerticalWrapMode.Overflow, plus.GetComponentInChildren<Text>().verticalOverflow);

                plus.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(90, state.planProduction);
                Assert.AreEqual("90 杯", productionValue.text,
                    "点击加号后，经营面板应立即刷新杯数文字。");
            }
            finally
            {
                if (ui != null) Object.DestroyImmediate(ui.gameObject);
                if (stateObject != null) Object.DestroyImmediate(stateObject);
                GameState.I = null;
            }
        }

        static Text TextByValue(Text[] texts, string value)
        {
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] != null && texts[i].text == value) return texts[i];
            return null;
        }

        static Button ButtonByLabel(Button[] buttons, string value)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i] != null ? buttons[i].GetComponentInChildren<Text>() : null;
                if (label != null && label.text == value) return buttons[i];
            }
            return null;
        }
    }
}

#endif
