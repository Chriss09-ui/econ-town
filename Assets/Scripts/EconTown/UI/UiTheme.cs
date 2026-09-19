using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using TMPro;

namespace EconTown
{
    /// <summary>UI 基础设施:中文字体解析、控件工厂、港务账本风像素主题。</summary>
    public static class UiTheme
    {
        // 取自港区夜色、海水、黄铜铆钉和纸质货单，不再使用通用咖啡色仪表盘。
        public static readonly Color BgDark = new Color32(0x17, 0x2d, 0x33, 248);
        public static readonly Color Panel = new Color32(0x20, 0x39, 0x3e, 250);
        public static readonly Color PanelLight = new Color32(0x2d, 0x4c, 0x50, 250);
        public static readonly Color Accent = new Color32(0xd8, 0xa8, 0x4e, 255);
        public static readonly Color Market = new Color32(0x50, 0x93, 0x8c, 255);
        public static readonly Color Paper = new Color32(0xe8, 0xd5, 0xad, 255);
        public static readonly Color Ink = new Color32(0x1f, 0x29, 0x28, 255);
        public static readonly Color Good = new Color32(0x7e, 0xb5, 0x6c, 255);
        public static readonly Color Bad = new Color32(0xb8, 0x5b, 0x4c, 255);
        public static readonly Color TextMain = new Color32(0xf6, 0xea, 0xd0, 255);
        public static readonly Color TextSub = new Color32(0xd0, 0xc4, 0xa8, 255);

        public const int MinimumBodyFontSize = 16;
        const string BundledChineseFontResourcePath = "Fonts/NotoSansCJKsc-Regular";

        static Font gameFont;
        static bool gameFontIsBuiltin;
        static bool gameFontIsProjectAsset;
        static TMP_FontAsset crispFontAsset;
        static bool crispFontUnavailable;
        static float crispFontRetryAfter = -1f;
        static Texture2D panelChromeTexture;
        static Texture2D ticketChromeTexture;
        static Sprite panelChromeSprite;
        static Sprite ticketChromeSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticCache()
        {
            // 禁用 Domain Reload 时静态字体资产跨 Play 存活:OS 动态 Font 与 TMP 多图集都是原生
            // 资源,旧的一份永不释放(其图集体积远大于 Kenney 地块贴图);失败标志一并复位,
            // 环境恢复后无需重启进程(P2-8/P2-11)。内置字体(LegacyRuntime)不归我们销毁,置空即可。
            if (crispFontAsset != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(crispFontAsset);
                else UnityEngine.Object.DestroyImmediate(crispFontAsset);
            }
            crispFontAsset = null;
            if (gameFont != null && !gameFontIsBuiltin && !gameFontIsProjectAsset)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(gameFont);
                else UnityEngine.Object.DestroyImmediate(gameFont);
            }
            gameFont = null;
            gameFontIsBuiltin = false;
            gameFontIsProjectAsset = false;
            crispFontUnavailable = false;
            crispFontRetryAfter = -1f;
            DestroyGenerated(ref panelChromeSprite, ref panelChromeTexture);
            DestroyGenerated(ref ticketChromeSprite, ref ticketChromeTexture);
        }

        static void DestroyGenerated(ref Sprite sprite, ref Texture2D texture)
        {
            if (sprite != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            if (texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
            }
            sprite = null;
            texture = null;
        }

        public static Sprite PanelChromeSprite
        {
            get
            {
                if (panelChromeSprite == null)
                    panelChromeSprite = CreateChromeSprite("ui_panel_chrome", false, out panelChromeTexture);
                return panelChromeSprite;
            }
        }

        public static Sprite TicketChromeSprite
        {
            get
            {
                if (ticketChromeSprite == null)
                    ticketChromeSprite = CreateChromeSprite("ui_ticket_chrome", true, out ticketChromeTexture);
                return ticketChromeSprite;
            }
        }

        static Sprite CreateChromeSprite(string name, bool raised, out Texture2D texture)
        {
            const int size = 24;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name + "_texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool cutCorner = (x < 3 && y < 3 && x + y < 3) ||
                        (x < 3 && y >= size - 3 && x + (size - 1 - y) < 3) ||
                        (x >= size - 3 && y < 3 && (size - 1 - x) + y < 3) ||
                        (x >= size - 3 && y >= size - 3 &&
                            (size - 1 - x) + (size - 1 - y) < 3);
                    if (cutCorner)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    byte value = edge == 0 ? (byte)62 : edge <= 2 ? (byte)112 : (byte)238;
                    if (raised && edge >= 3)
                    {
                        if (y >= size - 5) value = 255;
                        else if (y <= 4) value = 178;
                    }
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(6f, 6f, 6f, 6f));
            sprite.name = name;
            return sprite;
        }

        public static void ApplyPixelFrame(Image image)
        {
            if (image == null) return;
            image.sprite = PanelChromeSprite;
            image.type = Image.Type.Sliced;
        }

        public static void ApplyTicketFrame(Image image)
        {
            if (image == null) return;
            image.sprite = TicketChromeSprite;
            image.type = Image.Type.Sliced;
        }

        public static Font GameFont
        {
            get
            {
                if (gameFont != null) return gameFont;
                gameFont = Resources.Load<Font>(BundledChineseFontResourcePath);
                if (gameFont != null)
                {
                    gameFontIsBuiltin = false;
                    gameFontIsProjectAsset = true;
                    return PrepareFont(gameFont);
                }

                string[] wanted = { "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", "Heiti SC", "Noto Sans CJK SC", "WenQuanYi Micro Hei" };
                string[] installed = Font.GetOSInstalledFontNames();
                if (installed != null)
                {
                    for (int i = 0; i < wanted.Length; i++)
                    {
                        for (int j = 0; j < installed.Length; j++)
                        {
                            if (installed[j] == wanted[i])
                            {
                                gameFont = Font.CreateDynamicFontFromOSFont(wanted[i], 32);
                                gameFontIsBuiltin = false;
                                gameFontIsProjectAsset = false;
                                return PrepareFont(gameFont);
                            }
                        }
                    }
                }
                gameFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                gameFontIsBuiltin = true;
                gameFontIsProjectAsset = false;
                return PrepareFont(gameFont);
            }
        }

        static Font PrepareFont(Font font)
        {
            if (font == null || font.material == null) return font;
            Texture atlas = font.material.mainTexture;
            // 字体不是像素画。Point 会让中文斜线和横折断裂，缩放时反而显得发灰发虚。
            if (atlas != null) atlas.filterMode = FilterMode.Bilinear;
            return font;
        }

        /// <summary>运行时中文动态 SDF 字体。多图集避免知识卡与长报告挤爆单张字库。</summary>
        public static TMP_FontAsset CrispFontAsset
        {
            get
            {
                if (crispFontAsset != null) return crispFontAsset;
                // 失败后带重试时间戳缓存:环境恢复(如字体安装/权限)后两分钟内自动重试,
                // 不再永久短路到进程重启(P2-11)。
                if (crispFontUnavailable && Time.realtimeSinceStartup < crispFontRetryAfter) return null;
                Font source = GameFont;
                if (source == null)
                {
                    crispFontUnavailable = true;
                    crispFontRetryAfter = Time.realtimeSinceStartup + 120f;
                    return null;
                }
                // 项目字体随 WebGL 构建携带,可直接提供字形数据；OS 字体则仍按字体族解析真实文件。
                crispFontAsset = gameFontIsProjectAsset
                    ? TMP_FontAsset.CreateFontAsset(source, 128, 9, GlyphRenderMode.SDFAA,
                        1024, 1024, AtlasPopulationMode.Dynamic, true)
                    : TMP_FontAsset.CreateFontAsset(source.name, string.Empty, 128);
                if (crispFontAsset == null)
                {
                    crispFontUnavailable = true;
                    crispFontRetryAfter = Time.realtimeSinceStartup + 120f;
                    return null;
                }
                crispFontUnavailable = false;
                crispFontRetryAfter = -1f;
                crispFontAsset.name = "EconTown Chinese Dynamic SDF";
                crispFontAsset.isMultiAtlasTexturesEnabled = true;
                Texture2D atlas = crispFontAsset.atlasTexture;
                if (atlas != null) atlas.filterMode = FilterMode.Bilinear;
                return crispFontAsset;
            }
        }

        public static Text MakeText(Transform parent, string content, int size, Color? color = null,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            GameObject go = new GameObject("text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = GameFont;
            PrepareFont(t.font);
            t.text = content;
            t.fontSize = Mathf.Max(MinimumBodyFontSize, size);
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color ?? TextMain;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL 的 TMP 运行时多图集在新增中文进入后续图集时可能整段不绘制。
            // 构建已携带 Noto 字体，网页端直接保留 Legacy Text 可稳定覆盖全部中文。
#else
            CrispTextMirror.Attach(t, CrispFontAsset);
#endif
            return t;
        }

        public static Image MakeImage(Transform parent, Color color, string name = "img")
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Button MakeButton(Transform parent, string label, System.Action onClick, int fontSize = 22)
        {
            GameObject go = new GameObject("btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = Paper;
            img.raycastTarget = true;
            ApplyTicketFrame(img);
            Button b = go.GetComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.96f, 0.86f);
            cb.pressedColor = new Color(0.82f, 0.71f, 0.58f);
            cb.selectedColor = new Color(1f, 0.93f, 0.78f);
            cb.disabledColor = new Color(0.55f, 0.51f, 0.47f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            Text t = MakeText(go.transform, label, Mathf.Max(17, fontSize), Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Noto CJK 在 WebGL 的行高可能略大于紧凑按钮的可用高度。
            // Truncate 会直接裁掉整行，导致增减杯数等按钮看起来“没有字”。
            t.verticalOverflow = VerticalWrapMode.Overflow;
            b.onClick.AddListener(() => { if (onClick != null) onClick(); });
            return b;
        }

        public static void SetButtonStyle(Button button, Color background, Color foreground)
        {
            if (button == null) return;
            button.image.color = background;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = foreground;
        }

        /// <summary>带标题栏的圆角像素面板,返回内容区。</summary>
        public static RectTransform MakePanel(Transform parent, string title, float w, float h)
        {
            GameObject root = new GameObject("panel_" + title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            Image bg = root.GetComponent<Image>();
            bg.color = BgDark;
            bg.raycastTarget = true;
            ApplyPixelFrame(bg);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.SetAsLastSibling();
            root.AddComponent<UiPanelViewportFitter>().Configure(w, h);

            Image bar = MakeImage(root.transform, PanelLight, "titlebar");
            ApplyTicketFrame(bar);
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            bar.rectTransform.sizeDelta = new Vector2(-20f, 52f);

            Image ledgerTab = MakeImage(bar.transform, Market, "ledgerTab");
            ledgerTab.rectTransform.anchorMin = new Vector2(0f, 0f);
            ledgerTab.rectTransform.anchorMax = new Vector2(0f, 1f);
            ledgerTab.rectTransform.pivot = new Vector2(0f, 0.5f);
            ledgerTab.rectTransform.anchoredPosition = Vector2.zero;
            ledgerTab.rectTransform.sizeDelta = new Vector2(9f, 0f);

            for (int i = 0; i < 3; i++)
            {
                Image rivet = MakeImage(ledgerTab.transform, Paper, "rivet_" + i);
                rivet.rectTransform.anchorMin = rivet.rectTransform.anchorMax = new Vector2(.5f, .25f + i * .25f);
                rivet.rectTransform.pivot = new Vector2(.5f, .5f);
                rivet.rectTransform.anchoredPosition = Vector2.zero;
                rivet.rectTransform.sizeDelta = new Vector2(4f, 4f);
            }

            Image titleRule = MakeImage(bar.transform, Accent, "titleRule");
            titleRule.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleRule.rectTransform.anchorMax = new Vector2(1f, 0f);
            titleRule.rectTransform.pivot = new Vector2(.5f, 0f);
            titleRule.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            titleRule.rectTransform.sizeDelta = new Vector2(0f, 3f);

            Text t = MakeText(bar.transform, title, 24, TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.name = "titleLabel";
            t.rectTransform.Stretch();
            t.rectTransform.offsetMin = new Vector2(28, 0);
            t.rectTransform.offsetMax = new Vector2(-64, 0);

            // 所有模态面板都提供明确的鼠标退出路径，避免玩家误以为人物卡死。
            Button close = MakeButton(bar.transform, "×", () =>
            {
                if (UIManager.I != null) UIManager.I.ClosePanel(root);
            }, 26);
            RectTransform closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-6f, 0f);
            closeRt.sizeDelta = new Vector2(42f, 40f);
            SetButtonStyle(close, Bad, TextMain);
            Text closeText = close.GetComponentInChildren<Text>();
            if (closeText != null) closeText.color = TextMain;

            GameObject content = new GameObject("content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            RectTransform crt = content.GetComponent<RectTransform>();
            crt.Stretch();
            crt.offsetMin = new Vector2(24, 16);
            crt.offsetMax = new Vector2(-24, -72);
            return crt;
        }

        // 原此处有一个静态 ClosePanel(直销毁、绕过 UIManager 的面板栈记账):全仓零调用的
        // 危险死代码,一旦被误用会在栈内留下悬空引用,已删除(P2-11)。
    }

    /// <summary>在窗口尺寸变化时只缩小过大的面板，避免面板被 Game View 边缘裁掉。</summary>
    [DisallowMultipleComponent]
    public sealed class UiPanelViewportFitter : MonoBehaviour
    {
        RectTransform rectTransform;
        Vector2 requestedSize;
        Vector2 lastParentSize;

        public void Configure(float width, float height)
        {
            rectTransform = GetComponent<RectTransform>();
            requestedSize = new Vector2(width, height);
            FitNow();
        }

        void LateUpdate()
        {
            RectTransform parent = transform.parent as RectTransform;
            if (parent == null || parent.rect.size == lastParentSize) return;
            FitNow();
        }

        public void FitNow()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            RectTransform parent = transform.parent as RectTransform;
            if (rectTransform == null || parent == null || requestedSize.x <= 0f || requestedSize.y <= 0f) return;
            lastParentSize = parent.rect.size;
            float fitWidth = Mathf.Max(1f, lastParentSize.x - 32f) / requestedSize.x;
            float fitHeight = Mathf.Max(1f, lastParentSize.y - 32f) / requestedSize.y;
            float scale = Mathf.Clamp(Mathf.Min(fitWidth, fitHeight), 0.65f, 1f);
            rectTransform.localScale = Vector3.one * scale;
        }
    }

    public static class RectEx
    {
        public static void Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void SetInset(this RectTransform rt, Vector2 xy, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = xy;
            rt.sizeDelta = size;
        }
    }
}
