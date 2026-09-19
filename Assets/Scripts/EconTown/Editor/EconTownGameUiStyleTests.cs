#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>共用 HUD/弹窗的视觉合同：具体页可变，框体、密度与安全区不得退回调试界面。</summary>
    public class EconTownGameUiStyleTests
    {
        [SetUp]
        public void SetUp()
        {
            DestroyExistingUiManagers();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyExistingUiManagers();
        }

        [Test]
        public void Hud_UsesCompactPixelChrome_AndNinePrimaryShortcuts()
        {
            UIManager ui = UIManager.Create();
            ui.InitializeForValidation();
            Canvas.ForceUpdateCanvases();

            Transform canvas = ui.transform.Find("Canvas");
            Image top = canvas.Find("topbar").GetComponent<Image>();
            Image dock = canvas.Find("bottomDock").GetComponent<Image>();
            Assert.AreEqual(Image.Type.Sliced, top.type);
            Assert.AreEqual(Image.Type.Sliced, dock.type);
            Assert.IsNotNull(top.sprite);
            Assert.IsNotNull(dock.sprite);
            Assert.AreEqual(58f, top.rectTransform.sizeDelta.y, .01f);
            Assert.AreEqual(62f, dock.rectTransform.sizeDelta.y, .01f);
            Assert.Less(dock.rectTransform.sizeDelta.x, 1000f, "底部导航不得再铺满整个屏幕。");
            Assert.AreEqual(9, dock.GetComponentsInChildren<Button>(true).Length);
            Assert.IsNotNull(top.transform.Find("btn_设置"), "低频设置应收到顶栏，给底部高频入口让位。");

            Image prompt = canvas.Find("interactionPromptBg").GetComponent<Image>();
            Assert.IsFalse(prompt.gameObject.activeSelf);
            ui.ShowPrompt("[E] 查看报关行");
            Assert.IsTrue(prompt.gameObject.activeSelf);
            Assert.AreEqual(Image.Type.Sliced, prompt.type);
        }

        [Test]
        public void Modal_UsesLedgerFrame_SpineRivets_AndTicketButtons()
        {
            UIManager ui = UIManager.Create();
            ui.InitializeForValidation();
            ui.ShowConfirmation("视觉验收", "确认这份小镇账单吗？", () => { });

            Transform root = ui.transform.Find("Canvas/panels/panel_视觉验收");
            Assert.IsNotNull(root);
            Image frame = root.GetComponent<Image>();
            Assert.IsNotNull(frame.sprite);
            Assert.AreEqual(Image.Type.Sliced, frame.type);
            Assert.IsNull(root.GetComponent<Shadow>(), "不使用会在非整数缩放下生成阶梯脏块的 Shadow。");

            Transform titlebar = root.Find("titlebar");
            Assert.IsNotNull(titlebar.Find("ledgerTab"));
            Assert.IsNotNull(titlebar.Find("ledgerTab/rivet_0"));
            Assert.IsNotNull(titlebar.Find("ledgerTab/rivet_1"));
            Assert.IsNotNull(titlebar.Find("ledgerTab/rivet_2"));
            Assert.IsNotNull(titlebar.Find("titleRule"));

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            Assert.GreaterOrEqual(buttons.Length, 3);
            for (int i = 0; i < buttons.Length; i++)
            {
                Assert.IsNotNull(buttons[i].image.sprite, buttons[i].name + " 缺少像素票据框。");
                Assert.AreEqual(Image.Type.Sliced, buttons[i].image.type);
            }
        }

        [Test]
        public void Toast_IsANarrowFramedTicket_WithBrassTab()
        {
            UIManager ui = UIManager.Create();
            ui.InitializeForValidation();
            ui.ShowToast("市场价格已更新，新的报价将在明日生效。");

            RectTransform toast = ui.transform.Find("Canvas/toasts/toast") as RectTransform;
            Assert.IsNotNull(toast);
            Assert.AreEqual(420f, toast.sizeDelta.x, .01f);
            Assert.AreEqual(Image.Type.Sliced, toast.GetComponent<Image>().type);
            Assert.IsNotNull(toast.Find("toastTab"));
        }

        static void DestroyExistingUiManagers()
        {
            UIManager[] managers = Resources.FindObjectsOfTypeAll<UIManager>();
            for (int i = 0; i < managers.Length; i++)
                if (managers[i] != null && managers[i].gameObject.scene.IsValid())
                    Object.DestroyImmediate(managers[i].gameObject);
        }
    }
}

#endif
