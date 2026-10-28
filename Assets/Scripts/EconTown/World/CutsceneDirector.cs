using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>
    /// V0.7.4《小镇大事记》过场导演(PRD §149/§150):
    /// 320×180 像素插画 ×4 整数放大铺满参考分辨率,打字机逐字正文,每段 1–2 处代码内动效;
    /// 「跳过」按钮与 Esc 两路立即完成并视为已看(§149 只定义这两路);
    /// 过场开始即写 viewedCutscenes(§137 双保险的标记侧),不写 checkpoint、不碰任何经济账本;
    /// atmosphereEffects=false 时按呈现层降级约定直接视为已看并放行宿主弹窗。
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        public static CutsceneDirector I { get; private set; }

        const float CharInterval = 0.055f;   // 打字机步进:四行约 5s 出齐
        const float HoldAfterTyped = 2.6f;   // 出齐后驻留,凑足 §149 的 6–10s 时长档
        const float MinDuration = 6f;

        class Pending
        {
            public int id;
            public System.Action done;
            public string textOverride;   // 回看时用首播档案数字替换当档数字
        }

        readonly List<Pending> queued = new List<Pending>();

        GameState state;
        int currentId;
        System.Action onDone;
        Text typingText;
        string fullText = "";
        float elapsed;
        float duration;
        bool finishing;

        // 内动效载体(按段建,段末随层一起销毁)
        readonly List<Image> pulseSprites = new List<Image>();
        readonly List<Image> rainSprites = new List<Image>();
        Image swaySprite;    // 彩旗/B 旗:绕 Z 摆动
        Image bobSprite;     // 价签:上下轻跳
        Image beamSprite;    // 灯塔光带:呼吸
        readonly List<float> rainSpeed = new List<float>();
        readonly List<float> rainPhase = new List<float>();

        public static void Create()
        {
            if (I != null) return;
            GameObject go = new GameObject("CutsceneDirector");
            go.AddComponent<CutsceneDirector>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public bool Playing { get { return UIManager.I != null && UIManager.I.CutsceneActive; } }

        /// <summary>
        /// 首播入口:开始即视为已看并记档;atmosphereEffects=false(设置关掉演出动画)或
        /// 非 Play 环境(测试/模拟驱动)时直接完成,宿主弹窗照常出现——过场永不阻塞流程(§152-1)。
        /// </summary>
        public static bool TryPlay(GameState s, int id, System.Action hostPopup)
        {
            if (s == null || !CutsceneService.ShouldPlay(s, id))
            {
                if (hostPopup != null) hostPopup();
                return false;
            }
            CutsceneService.MarkViewed(s, id);
            if (I == null || !Application.isPlaying || !s.experience.settings.atmosphereEffects)
            {
                if (hostPopup != null) hostPopup();
                return true;
            }
            I.Enqueue(new Pending { id = id, done = hostPopup });
            return true;
        }

        /// <summary>大事记回看:不改变看过标记,数字引用首播当日的档案记录(§150/§151)。</summary>
        public static bool TryReplay(GameState s, int id, System.Action onFinished)
        {
            if (I == null || !Application.isPlaying) return false;
            CutsceneRecord record = CutsceneService.FindRecord(s, id);
            if (record == null) return false;
            string text = string.Join("\n", CutsceneService.Lines(s, id));
            string captionNow = CutsceneService.Caption(s, id);
            if (!string.IsNullOrEmpty(captionNow) && text.Contains(captionNow))
                text = text.Replace(captionNow, record.caption);
            I.Enqueue(new Pending { id = id, done = onFinished, textOverride = text });
            return true;
        }

        void Enqueue(Pending pending)
        {
            queued.Add(pending);
            if (!Playing) DequeueNext();
        }

        void DequeueNext()
        {
            if (queued.Count == 0) return;
            Pending next = queued[0];
            queued.RemoveAt(0);
            Begin(next);
        }

        void Begin(Pending pending)
        {
            state = GameState.I;
            currentId = pending.id;
            onDone = pending.done;
            finishing = false;
            elapsed = 0f;
            pulseSprites.Clear();
            rainSprites.Clear();
            rainSpeed.Clear();
            rainPhase.Clear();
            swaySprite = null;
            bobSprite = null;
            beamSprite = null;
            string text = pending.textOverride;
            if (string.IsNullOrEmpty(text)) text = string.Join("\n", CutsceneService.Lines(state, pending.id));

            RectTransform layer = UIManager.I.AcquireCutsceneLayer();
            if (layer == null)   // Canvas 初始化失败的兜底:直接放行,绝不卡流程
            {
                Finish();
                return;
            }

            // 底色先铺满:插画没画到的像素不再透出底下的小镇和按钮。
            Image backing = UiTheme.MakeImage(layer, new Color32(0x14, 0x10, 0x0c, 255), "cutsceneBacking");
            backing.rectTransform.Stretch();
            backing.raycastTarget = true;

            // 底图:320×180 ×4 = 1280×720,Point 采样与全镇观感一致(§149)
            Image bg = UiTheme.MakeImage(layer, Color.white, "cutsceneBg");
            bg.sprite = PixelArt.Cutscene(pending.id);
            bg.rectTransform.Stretch();
            bg.raycastTarget = true;   // 挡住底下世界的点击

            BuildOverlays(pending.id, layer);

            // 底部文案带 + 打字机
            Image strip = UiTheme.MakeImage(layer, new Color32(0x14, 0x10, 0x0c, 255), "cutsceneText");
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.anchoredPosition = Vector2.zero;
            strip.rectTransform.sizeDelta = new Vector2(0f, 132f);
            typingText = UiTheme.MakeText(strip.transform, "", 24, UiTheme.TextMain, TextAnchor.UpperLeft);
            typingText.rectTransform.Stretch();
            typingText.rectTransform.offsetMin = new Vector2(84, 10);
            typingText.rectTransform.offsetMax = new Vector2(-84, -10);
            typingText.horizontalOverflow = HorizontalWrapMode.Wrap;
            typingText.verticalOverflow = VerticalWrapMode.Overflow;

            // 左上角档名小签。浅色插画（报纸、朝霞）上奶油字会消失，所以先垫一块实心底。
            string tagLabel = "小镇大事记 · " + CutsceneService.Title(pending.id);
            Image tagPlate = UiTheme.MakeImage(layer, new Color32(0x14, 0x10, 0x0c, 235), "cutsceneTagPlate");
            tagPlate.rectTransform.anchorMin = tagPlate.rectTransform.anchorMax = new Vector2(0f, 1f);
            tagPlate.rectTransform.pivot = new Vector2(0f, 1f);
            tagPlate.rectTransform.anchoredPosition = new Vector2(8f, -8f);
            tagPlate.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(36 + tagLabel.Length * 18, 200, 520), 36f);
            Text tag = UiTheme.MakeText(layer, tagLabel, 18,
                new Color32(0xff, 0xf1, 0xd3, 255), TextAnchor.MiddleLeft);
            tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(0f, 1f);
            tag.rectTransform.pivot = new Vector2(0f, 1f);
            tag.rectTransform.anchoredPosition = new Vector2(20f, -11f);
            tag.rectTransform.sizeDelta = new Vector2(tagPlate.rectTransform.sizeDelta.x - 24f, 30f);

            // 「跳过」常驻(§149 两路之一;另一路是 Esc)
            Button skip = UiTheme.MakeButton(layer, "跳过 ≫", Finish, 20);
            skip.rectTransform().anchorMin = skip.rectTransform().anchorMax = new Vector2(1f, 1f);
            skip.rectTransform().pivot = new Vector2(1f, 1f);
            skip.rectTransform().anchoredPosition = new Vector2(-16f, -16f);
            skip.rectTransform().sizeDelta = new Vector2(150f, 48f);

            fullText = text;
            // 溢出保护:按 CJK 方块字宽估算显示行数,超出文案带内容高(132 条带减去上下各 10)自动缩字号,
            // 防止以后改长文案时第四行尾部溢出条带(verticalOverflow=Overflow 无裁剪)。
            int fontSize = 24;
            while (fontSize > 16)
            {
                float charW = fontSize * 1.02f;
                float lineH = fontSize * 1.3f;
                int perLine = Mathf.Max(8, Mathf.FloorToInt(1112f / charW));
                string[] parts = fullText.Split('\n');
                int rows = 0;
                for (int i = 0; i < parts.Length; i++)
                    rows += Mathf.Max(1, Mathf.CeilToInt(parts[i].Length / (float)perLine));
                if (rows * lineH <= 110f) break;
                fontSize -= 2;
            }
            typingText.fontSize = fontSize;
            duration = Mathf.Max(MinDuration, fullText.Length * CharInterval + HoldAfterTyped);
            if (typingText != null) typingText.text = "";
        }

        // ————— 每段 1–2 处内动效(§149:旗飘/灯闪/雨落,代码驱动) —————

        Image Overlay(RectTransform layer, Sprite sprite, float px, float py)
        {
            Image img = UiTheme.MakeImage(layer, Color.white, "cutOverlay");
            img.sprite = sprite;
            img.rectTransform.sizeDelta = new Vector2(sprite.rect.width * 4f, sprite.rect.height * 4f);
            img.rectTransform.anchoredPosition = new Vector2((px + 0.5f) * 4f - 640f, (py + 0.5f) * 4f - 360f);
            return img;
        }

        void BuildOverlays(int id, RectTransform layer)
        {
            System.Random rng = new System.Random(42);   // 布局确定性:回看与双跑观感一致
            switch (id)
            {
                case (int)CutsceneId.SunnyTeaOpening:
                    swaySprite = Overlay(layer, PixelArt.CutscenePennant(), 160f, 148f);
                    for (int k = 0; k < 3; k++)
                        pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 62f + k * 96f, 120f - k * 18f));
                    break;
                case (int)CutsceneId.PriceWarErupts:
                    bobSprite = Overlay(layer, PixelArt.CutscenePriceTag(), 84f, 96f);
                    break;
                case (int)CutsceneId.CrisisClouds:
                    for (int k = 0; k < 26; k++)
                    {
                        Image drop = Overlay(layer, PixelArt.CutsceneRaindrop(),
                            rng.Next(6, 314), rng.Next(0, 180));
                        rainSprites.Add(drop);
                        rainSpeed.Add(rng.Next(0, 100) < 50 ? 150f : 96f);
                        // bug搜#14:相位域铺满整个循环跨度(780),开局 26 滴雨均匀散布全屏;
                        // 旧域 [0,180) 使开局全部挤在窄带里(方向修复前挤顶部,修复后挤底部)。
                        rainPhase.Add(rng.Next(0, 780));
                    }
                    for (int k = 0; k < 2; k++)
                        pulseSprites.Add(Overlay(layer, PixelArt.CutsceneCloudPatch(), 90f + k * 140f, 162f));
                    break;
                case (int)CutsceneId.TradeWarHeadline:
                    pulseSprites.Add(Overlay(layer, PixelArt.CutsceneWarningLamp(), 160f, 118f));
                    swaySprite = Overlay(layer, PixelArt.FlagCloth(), 298f, 92f);
                    if (swaySprite != null) swaySprite.color = new Color32(0x8e, 0x9e, 0xc4, 255);   // B 国冷色旗
                    break;
                case (int)CutsceneId.Finale:
                    beamSprite = Overlay(layer, PixelArt.CutsceneBeam(), 262f, 100f);
                    if (beamSprite != null)
                    {
                        beamSprite.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 196f);
                        beamSprite.color = new Color(1f, 1f, 1f, 0.3f);
                    }
                    for (int k = 0; k < 2; k++)
                        pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 60f + k * 180f, 150f));
                    break;
                case (int)CutsceneId.HousingMania:
                    // 找BUG围猎盲区1:第六段补内动效(§149 每段 1–2 处)——价格标签浮在照片框
                    // 天际线上轻晃,右侧涨幅柱区星芒脉冲(V0.9.5 落地时导演层漏配,EditMode 照不到)。
                    bobSprite = Overlay(layer, PixelArt.CutscenePriceTag(), 86f, 88f);
                    for (int k = 0; k < 3; k++)
                        pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(),
                            172f + k * 22f, 108f + (k % 2) * 6f));
                    break;
                case (int)CutsceneId.MoveInTown:
                    // V0.9.17 晋升线:窗台果汁杯与半开门口的晨光星点
                    for (int k = 0; k < 2; k++)
                        pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 194f - k * 49f, 84f + k * 6f));
                    break;
                case (int)CutsceneId.CouncilAppointment:
                    // 台灯下聘书红章与尽头空椅各一枚星芒(焦点引导)
                    pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 189f, 92f));
                    pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 258f, 142f));
                    break;
                case (int)CutsceneId.BureauHandover:
                    // 三张升起报表的上角星点,自左向右错高
                    for (int k = 0; k < 3; k++)
                        pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 92f + k * 66f, 122f + (k % 2) * 10f));
                    break;
                case (int)CutsceneId.BankMandate:
                    // 央行绿灯晕与怀表各一处脉冲
                    pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 42f, 99f));
                    pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 178f, 72f));
                    break;
                case (int)CutsceneId.Homecoming:
                    // 沉日光晕与门前钥匙串(复审#4:原 266,26 落在空街面且 y<37 被底部
                    // 文案带遮蔽——移到钥匙串实际位置 175,43,恰在 strip 上缘之上)
                    pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 124f, 156f));
                    pulseSprites.Add(Overlay(layer, PixelArt.CutsceneSparkle(), 172f, 68f));
                    break;
            }
        }

        void Update()
        {
            if (!Playing || finishing) return;

            // Esc = 第二路跳过(§149;跳过按钮是第一路)。移动键与 E 不构成跳过。
            if (PlayerController.CancelPressed()) { Finish(); return; }

            elapsed += Time.deltaTime;
            if (typingText != null)
            {
                int chars = Mathf.Min(fullText.Length, Mathf.FloorToInt(elapsed / CharInterval));
                typingText.text = fullText.Substring(0, chars);
            }
            AnimateOverlays(elapsed);
            ParticleBudget.Report("cutscene_rain", rainSprites.Count);   // §154 全局粒子账本
            if (elapsed >= duration) Finish();
        }

        void AnimateOverlays(float t)
        {
            if (swaySprite != null)
                swaySprite.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.7f) * 7f);
            if (bobSprite != null)
            {
                bobSprite.rectTransform.anchoredPosition = new Vector2(
                    bobSprite.rectTransform.anchoredPosition.x, 84f + Mathf.Abs(Mathf.Sin(t * 2.6f)) * 24f);
                bobSprite.color = new Color(1f, 1f, 1f, 0.62f + 0.38f * Mathf.Sin(t * 5f));
            }
            if (beamSprite != null)
                beamSprite.color = new Color(1f, 1f, 1f, 0.22f + 0.16f * (1f + Mathf.Sin(t * 1.3f)));
            for (int i = 0; i < pulseSprites.Count; i++)
                if (pulseSprites[i] != null)
                    pulseSprites[i].color = new Color(1f, 1f, 1f,
                        0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 2.2f + i * 1.9f)));
            for (int i = 0; i < rainSprites.Count; i++)
            {
                if (rainSprites[i] == null) continue;
                float y = rainPhase[i] - t * rainSpeed[i];
                const float span = 180f * 4f + 60f;
                // bug搜#14:y 随时间递减(下落),Repeat(y,span) 也随之递减——旧代码再套一层
                // span−Repeat 把方向镜像成「雨向上飘」(与 WeatherParticles 落雨方向相反)。
                float wrapped = Mathf.Repeat(y, span);
                rainSprites[i].rectTransform.anchoredPosition = new Vector2(
                    rainSprites[i].rectTransform.anchoredPosition.x, wrapped - 420f);
            }
        }

        void Finish()
        {
            if (finishing) return;
            finishing = true;
            if (UIManager.I != null) UIManager.I.ReleaseCutsceneLayer();
            ParticleBudget.Report("cutscene_rain", 0);   // §154 清账
            typingText = null;
            System.Action done = onDone;
            onDone = null;
            if (done != null) done();
            if (!finishing) return;   // onDone 里同步再开播下一段时会重入
            finishing = false;
            DequeueNext();
        }
    }
}
