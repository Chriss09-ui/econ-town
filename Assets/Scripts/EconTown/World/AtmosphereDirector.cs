using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.1《小镇的呼吸》时段色调导演（PRD §121/§123）。
    /// 只调制 GameBootstrap.GlobalLight2D 的颜色与强度——整图统一乘色,不碰像素采样
    /// (Point 不糊),且 ScreenSpaceOverlay Canvas 不受 Light2D 影响,HUD 文字永不被染色。
    ///
    /// 色调 = 时段档(清晨冷青/营业黄昏暖橙/报告夜幕) × 状态层(事件灰蒙/炎热暖化),
    /// 满足 §154"滤镜最多两层:时段 × 复合状态"预算。
    /// 全部输入只读 economy.atmosphere 缓存与 phase,零经济副作用;关掉氛围效果开关回到基准光。
    /// </summary>
    public class AtmosphereDirector : MonoBehaviour
    {
        public static AtmosphereDirector I { get; private set; }

        // 基准光(呈现层全关时的恒定配色,即 V0.7 现状)
        static readonly Color BaseColor = new Color32(0xff, 0xf1, 0xd3, 255);
        const float BaseIntensity = 0.94f;

        static readonly Color MorningColor = new Color32(0xdc, 0xf0, 0xef, 255); // 清晨·冷青微亮
        static readonly Color BusinessColor = new Color32(0xff, 0xd9, 0xa6, 255); // 营业·黄昏暖橙
        static readonly Color ReportColor = new Color32(0x92, 0xa4, 0xcc, 255);  // 报告·夜幕深蓝

        const float MorningIntensity = 0.99f;
        const float BusinessIntensity = 1.0f;
        const float ReportIntensity = 0.74f;
        const float TintSpeedPerSecond = 2.2f;   // 常规阶段过渡速度(Color.Lerp t 因子)

        float dawnRemain = -1f;          // >0 表示夜→晨渐变进行中(秒)
        const float DawnDuration = 1.2f;
        Color dawnFrom;                  // 渐变起点快照(收日时的夜幕光)

        Color currentColor = BaseColor;
        float currentIntensity = BaseIntensity;

        public void OnDestroy()
        {
            if (I == this) I = null;
        }

        public static void Create()
        {
            if (I != null) return;
            I = new GameObject("AtmosphereDirector").AddComponent<AtmosphereDirector>();
        }

        /// <summary>收日→晨 1.2 秒渐变演出(GameBootstrap.TryNextDay 在置回清晨后调用);
        /// 夜幕起点快照保证从任何色调状态都能平滑走完。任意移动/Esc 输入可立即打断。</summary>
        public static void PlayDawn()
        {
            if (I == null) return;
            var s = GameState.I;
            if (s == null || !s.experience.settings.atmosphereEffects) return;
            I.dawnFrom = I.currentColor;
            I.dawnRemain = DawnDuration;
        }

        /// <summary>0 白天 → 1 全夜幕；橱窗窗光等自发光元素按它决定显隐强度(见 MapBuilder 窗光层)。</summary>
        public static float NightFactor { get; private set; }

        void Update()
        {
            var light = GameBootstrap.GlobalLight2D;
            GameState s = GameState.I;
            if (light == null || s == null) return;

            ConsumeSeasonRequest(s);

            bool fx = s.experience.settings.atmosphereEffects;
            Color targetColor;
            float targetIntensity;
            float targetNight;

            if (!fx)
            {
                targetColor = BaseColor;
                targetIntensity = BaseIntensity;
                targetNight = 0f;
            }
            else
            {
                ComputePhaseTarget(s, out targetColor, out targetIntensity, out targetNight);

                if (dawnRemain > 0f)
                {
                    if (PlayerWantsControl())
                        dawnRemain = 0f;         // 任意移动/Esc/E 立即打断,直接进入常规过渡
                    dawnRemain -= Time.deltaTime;
                    float k = Mathf.Clamp01(1f - Mathf.Max(0f, dawnRemain) / DawnDuration);
                    k = k * k * (3f - 2f * k);   // SmoothStep 缓动
                    currentColor = Color.Lerp(dawnFrom, targetColor, k);
                    currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, TintSpeedPerSecond * Time.deltaTime);
                    NightFactor = Mathf.Lerp(NightFactor, targetNight, TintSpeedPerSecond * Time.deltaTime);
                    Apply(light);
                    return;
                }
            }

            float moveT = TintSpeedPerSecond * Time.deltaTime;
            currentColor = Color.Lerp(currentColor, targetColor, moveT);
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, 0.8f * Time.deltaTime);
            NightFactor = Mathf.MoveTowards(NightFactor, targetNight, 1.5f * Time.deltaTime);
            Apply(light);

            SyncFestivalDecor(s, fx);
            SyncWindowLights(s, fx);
            MapBuilder.UpdateEcoVisualization(s);
            SyncStreetPopulation(s);
            WatchPerformance(s);
        }

        float lowFpsAccum;

        /// <summary>§119-4 性能提示:连续 5 秒实测帧率 <45 时弹一次性 Toast,
        /// performanceTipShown 随存档走(每存档只提示一次,不自动关闭任何效果)。
        /// 仅前台计时——编辑器失焦/后台降帧不构成误报。</summary>
        void WatchPerformance(GameState s)
        {
            if (s.experience.settings.performanceTipShown || !Application.isFocused) return;
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 60f;
            lowFpsAccum = fps < 45f ? lowFpsAccum + Time.unscaledDeltaTime : 0f;
            if (lowFpsAccum >= 5f)
            {
                s.experience.settings.performanceTipShown = true;
                lowFpsAccum = 0f;
                if (UIManager.I != null) UIManager.I.ShowToast("画面效果较重，可在 设置→氛围效果 中关闭");
            }
        }

        /// <summary>小镇节日彩旗灯笼随 atmosphere.festivalDecor 缓存切换;夜幕(NightFactor>0.55)切夜态贴图。</summary>
        void SyncFestivalDecor(GameState s, bool fx)
        {
            var atm = s.economy != null ? s.economy.atmosphere : null;
            bool wantFestival = fx && atm != null && atm.festivalDecor;
            MapBuilder.UpdateFestivalDecor(wantFestival, fx && NightFactor > 0.55f);
        }

        /// <summary>橱窗灯光 α(PRD §125):非玩家建筑按信心三档(≥60 全亮 /40–60 七成 /<40 四成)×夜幕权重;
        /// 玩家店单独规则:昨日开门=亮(随满意度微调),歇业=熄灯。ecoVisibility 关闭全部隐藏。</summary>
        void SyncWindowLights(GameState s, bool fx)
        {
            bool eco = s.experience.settings.ecoVisibility;
            var atm = s.economy != null ? s.economy.atmosphere : null;
            float nightW = Mathf.Lerp(0.25f, 1f, NightFactor);
            float worldAlpha = 0f, playerAlpha = 0f;

            if (eco && atm != null)
            {
                float tierAlpha = atm.windowTier == 2 ? 1f : atm.windowTier == 1 ? 0.7f : atm.windowTier == 0 ? 0.4f : 0.7f; // -1 未初始化取中档
                worldAlpha = tierAlpha * nightW;

                if (s.history.Count > 0)
                {
                    DayRecord last = s.history[s.history.Count - 1];
                    if (last.shopOpen)
                    {
                        float sat01 = Mathf.Clamp01((last.satisfaction - 40f) / 60f);
                        playerAlpha = Mathf.Lerp(0.75f, 1f, sat01) * Mathf.Max(nightW, 0.5f);   // 玩家店白天也保持可见
                    }
                }
            }
            if (!fx) { /* 氛围效果关闭时无夜幕,NightFactor→0 已自然压暗 */ }
            MapBuilder.ApplyWindowLightAlpha(worldAlpha, playerAlpha);
            // V0.8.3 路灯夜辉:氛围效果开启时随夜幕点亮(只有路灯项目建成才挂了辉光,属公共资本非经济仪表)
            MapBuilder.ApplyPublicLightAlpha(fx ? nightW : 0f);
        }

        /// <summary>街景人数执行器(PRD §124):在场闲逛 NPC 数逼近缓存 streetCurrent。
        /// 计数口径 = 可对话身份 + 临时漫步者(ambient)——补回者 dialogue:false 不进对话,
        /// 若不计入统计执行器会永远认为人不够而无限刷人。
        /// 减员顺序 游客→白领→学生,工厂主/居民代表永不消失(政府视角支持率台词依赖);
        /// 补回按 学生:白领:游客 ≈4:4:2 轮转。ecoVisibility 关闭时目标恒定 12 人(PRD §127)。</summary>
        static int refillSequenceIndex;
        static readonly string[] RefillSequence = { "学生", "白领", "学生", "白领", "游客" };   // ≈4:4:2
        const int MaxStreetOpsPerFrame = 6;   // 大幅增删时分帧消化,防单帧尖峰

        void SyncStreetPopulation(GameState s)
        {
            bool eco = s.experience.settings.ecoVisibility;
            var atm = s.economy != null ? s.economy.atmosphere : null;
            int target = eco && atm != null ? atm.streetCurrent : 12;

            int current = 0;
            for (int i = NpcAgent.All.Count - 1; i >= 0; i--)
            {
                NpcAgent n = NpcAgent.All[i];
                if (n != null && (n.IsDialogueTarget || n.IsAmbientStroller)) current++;
            }

            int budget = MaxStreetOpsPerFrame;
            // 围猎三#13:玩家在港区/店内时 TownRoot 隐藏、建筑碰撞体不参与物理查询,
            // 补刷漫步者的出生点避障全盲会卡进建筑——回镇前跳过补刷(下一帧自然补齐),减员照常。
            bool townHidden = PortSceneController.IsInsidePort || ShopInteriorController.IsInsideShopInterior;
            while (!townHidden && current < target && budget-- > 0)   // 复苏补回:轮转身份生成临时漫步者
            {
                int sequence = refillSequenceIndex;
                string type = RefillSequence[sequence % RefillSequence.Length];
                refillSequenceIndex++;
                NpcAgent.Spawn(type,
                    TownLayoutData.StreetActivityPoints[sequence % TownLayoutData.StreetActivityPoints.Length],
                    dialogue: false, ambient: true);
                current++;
            }
            while (current > target && budget-- > 0)   // 减员:游客 → 白领 → 学生依次递减(工厂主/居民代表保底不删)
            {
                NpcAgent victim =
                    FindStreetNpc("游客") ?? FindStreetNpc("白领") ?? FindStreetNpc("学生");
                if (victim == null) break;                       // 三类已空:保底防线兜住 streetTarget ≥5 的合法性
                Destroy(victim.gameObject);
                current--;
            }
        }

        static NpcAgent FindStreetNpc(string typeName)
        {
            for (int i = 0; i < NpcAgent.All.Count; i++)
            {
                NpcAgent n = NpcAgent.All[i];
                if (n == null) continue;
                bool removable = (n.IsDialogueTarget && n.TypeName == typeName) ||
                                 (n.IsAmbientStroller && n.TypeName == typeName);
                if (removable) return n;
            }
            return null;
        }

        void Apply(UnityEngine.Rendering.Universal.Light2D light)
        {
            light.color = currentColor;
            light.intensity = currentIntensity;
        }

        /// <summary>消费换季重建请求(AtmosphereService.ResolveDaily 在收日跨季时置位)。
        /// 读档等"缓存与画面需要对齐"的场景由 NotifyStateChanged 显式触发。</summary>
        void ConsumeSeasonRequest(GameState s)
        {
            var atm = s.economy != null ? s.economy.atmosphere : null;
            if (atm == null || !atm.seasonRequest) return;
            atm.seasonRequest = false;
            MapBuilder.ApplySeason(s);
        }

        /// <summary>读档成功后调用：按存档恢复的氛围缓存(季节/天气/街景人数)立即对齐场景呈现。
        /// 之后各视觉层(粒子、街景人数)在其 Update 中按开关与缓存自愈。</summary>
        public static void NotifyStateChanged()
        {
            if (I == null || GameState.I == null) return;
            MapBuilder.ApplySeason(GameState.I);
            MapBuilder.RefreshGrowthDistricts(GameState.I);   // V0.7.3:新区两态按读档章节对齐
            TownGrowthDirector.SyncFlag(GameState.I);
            GameBootstrap.RegisterDistrictInteractables();    // 读档≥动工章的旧档补注册交互点
        }

        /// <summary>收日渐变期间任意输入(移动/Esc/E)视为打断信号(PRD §121)。
        /// 实际判定统一走 PlayerController.WantsControlInput(项目为 Input System,裸调 legacy Input 会抛)。</summary>
        static bool PlayerWantsControl()
        {
            return PlayerController.WantsControlInput();
        }

        /// <summary>阶段目标色调(时段层)× 事件状态层(gloom 灰蒙 / heatWave 暖化)的合成。</summary>
        static void ComputePhaseTarget(GameState s, out Color color, out float intensity, out float night)
        {
            switch (s.phase)
            {
                case GamePhase.Business:
                    color = BusinessColor; intensity = BusinessIntensity; night = 0.45f; break;
                case GamePhase.Report:
                    color = ReportColor; intensity = ReportIntensity; night = 1f; break;
                default:
                    color = MorningColor; intensity = MorningIntensity; night = 0.15f; break;
            }

            var atm = s.economy != null ? s.economy.atmosphere : null;
            if (atm != null)
            {
                if (atm.gloomOverlay)
                {
                    // 事件阴天:云层之外再叠灰蒙(PRD §123A);装饰阴天只有云粒子、不动这里的色
                    color = Color.Lerp(color, new Color32(0x9a, 0xa0, 0xa6, 255), 0.45f);
                    intensity *= 0.85f;
                }
                else if (atm.heatWave)
                {
                    color = Color.Lerp(color, new Color32(0xff, 0xc2, 0x7a, 255), 0.30f);
                    intensity *= 1.05f;
                }
            }
        }
    }
}
