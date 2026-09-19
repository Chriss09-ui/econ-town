using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.3《生长的小镇》新区演出导演。只读 completedChapter / atmosphere 缓存 / macro 账本，
    /// 除消费 pendingCeremonies（队列+signals 双保险先例）与落成 Toast 外不写任何游戏状态。
    /// 演出从不占用 InputBlocked 或收日流程（§118 铁律 2）：任意移动/Esc 立即打断并呈现完工态。
    /// (围猎五#2 编译标记 v3:店内早退+DestroyImmediate 双态)
    /// </summary>
    public sealed class TownGrowthDirector : MonoBehaviour
    {
        public static TownGrowthDirector I { get; private set; }

        const float CeremonyDuration = 3f;      // §142：3 秒落成演出（灯亮/旗升）
        const float DustInterval = 0.35f;
        const int MaxDustPuffs = 30;            // §154 粒子预算内的施工尘土份额
        const float DustLifetime = 1.4f;

        class DustPuff
        {
            public SpriteRenderer renderer;
            public float age;
        }

        readonly List<DustPuff> dust = new List<DustPuff>();
        float dustTimer;
        float ceremonyRemain = -1f;     // >0 落成演出进行中
        GameObject ceremonyGlow;        // 「灯亮」的临时暖光 overlay（打断即消失）
        float dustMoteSeed;

        public static void Create()
        {
            if (I != null) return;
            new GameObject("TownGrowthDirector").AddComponent<TownGrowthDirector>();
        }

        void Awake() { I = this; }

        void OnDestroy()
        {
            if (I == this) I = null;
            // Director 销毁/场景卸载时清掉在场尘土(编辑器测试卫生;Play 下场景卸载本就会带走)。
            for (int i = 0; i < dust.Count; i++)
                if (dust[i].renderer != null) DestroyImmediate(dust[i].renderer.gameObject);
            dust.Clear();
        }

        /// <summary>次晨演出编排（GameBootstrap.TryNextDay 在 PlayDawn 之后调用）：
        /// 单日演出队列上限 2 段（§142），结业报告与开港演出各占一段优先级在前，
        /// 超出上限的新区落成降级为 Toast；相邻演出天然由弹窗/动画错开。</summary>
        public static void BeginMorning(GameState state)
        {
            if (I == null || state == null) return;
            MapBuilder.RefreshGrowthDistricts(state);
            GameBootstrap.RegisterDistrictInteractables();   // 新落成新区的交互点补注册（幂等）

            bool stageReportPending = ProgressionService.GetPendingStageReport(state) != null;
            int cap = 2 - (stageReportPending ? 1 : 0) - (PortDistrictDirector.MorningCeremonyPlayed ? 1 : 0);
            I.TryPlay(state, ProgressSignals.EastDistrictCeremony, "ceremony.east",
                TownLayoutData.DecorPoint("ceremony_east"), ref cap);
            I.TryPlay(state, ProgressSignals.CivicPlazaCeremony, "ceremony.plaza",
                TownLayoutData.DecorPoint("ceremony_plaza"), ref cap);
            I.TryPlay(state, ProgressSignals.FinancialStreetCeremony, "ceremony.financial",
                TownLayoutData.DecorPoint("ceremony_financial"), ref cap);
            SyncFlag(state);
        }

        /// <summary>读档/回清晨后对齐旗面颜色（财政立场只读投影）。</summary>
        public static void SyncFlag(GameState state)
        {
            if (state == null || state.macro == null) return;
            Color c;
            switch (state.macro.fiscalStance)
            {
                case FiscalStance.Expansionary: c = new Color32(0xc2, 0x3b, 0x2e, 255); break;  // 扩张·红
                case FiscalStance.Contractionary: c = new Color32(0x2e, 0x5a, 0xa8, 255); break; // 紧缩·蓝
                default: c = new Color32(0x3a, 0x7d, 0x44, 255); break;                          // 中性·绿
            }
            MapBuilder.SetGrowthFlagColor(c);
        }

        void TryPlay(GameState state, string signal, string momentTitle, Vector3 glowSpot, ref int cap)
        {
            if (!ProgressionService.HasPendingCeremony(state, signal)) return;
            if (cap <= 0)
            {
                // 降级：不播演出，直接呈现完工态 + Toast（§142 单日上限规则）
                ProgressionService.MarkCeremonyViewed(state, signal);
                if (UIManager.I != null) UIManager.I.ShowToast("小镇又长大了一点");
                return;
            }
            if (!ProgressionService.MarkCeremonyViewed(state, signal)) return;
            cap--;
            ceremonyRemain = CeremonyDuration;
            if (state.experience.settings.atmosphereEffects) SpawnCeremonyGlow(glowSpot);
            if (UIManager.I != null) UIManager.I.ShowToast("小镇又长大了一点");
            SaveSystem.SaveKeyMoment(state, momentTitle);
        }

        void SpawnCeremonyGlow(Vector3 spot)
        {
            if (ceremonyGlow == null)
            {
                ceremonyGlow = new GameObject("growth_ceremony_glow");
                SpriteRenderer sr = ceremonyGlow.AddComponent<SpriteRenderer>();
                sr.sprite = MapBuilder.WhiteSprite();
                sr.sortingOrder = 940;
                Material unlit = new Material(Shader.Find("Sprites/Default"));
                sr.sharedMaterial = unlit;
                // 出生即为半透明暖光:默认白色 α=1 会在首帧闪出大白板(2026-08-28 Play 实测)
                sr.color = new Color(1f, 0.88f, 0.62f, 0f);
            }
            ceremonyGlow.transform.position = spot;
            ceremonyGlow.transform.localScale = new Vector3(3.2f, 1.7f, 1f);
            ceremonyGlow.SetActive(true);
        }

        void Update()
        {
            GameState state = GameState.I;
            if (state == null) return;
            // 围猎五(#2):店内不看施工——尘土/落成光斑按世界坐标发射且不挂 TownRoot,果汁店
            // 店内矩形(0..16,0..11)与市民广场施工区(2.4..4.4,3..8)同域重叠,不早退就会
            // 「店里冒尘」(V0.9.7「室内下雨」同族)。已有尘土一并清掉并向账本报 0(§154 契约);
            // 落成演出按「打断=完工态」收尾。复审:港区同样早退——进港无章节闸(16-22 章市民
            // 广场施工期可自由进港),尘土矩形也落在中国港坐标域(x∈[-16.5,16.5])的堆场上。
            if (ShopInteriorController.IsInsideShopInterior || PortSceneController.IsInsidePort)
            {
                if (ceremonyRemain > 0f) { ceremonyRemain = 0f; EndCeremony(); }
                ClearDustAndReportZero();
                return;
            }
            bool fx = state.experience != null && state.experience.settings != null &&
                state.experience.settings.atmosphereEffects;

            // 1) 落成演出：旗升 + 灯亮，3 秒；任意移动/Esc 打断 → 直接呈现完工态
            if (ceremonyRemain > 0f)
            {
                if (PlayerWantsControl()) ceremonyRemain = 0f;
                ceremonyRemain -= Time.deltaTime;
                float t = Mathf.Clamp01(1f - Mathf.Max(0f, ceremonyRemain) / CeremonyDuration);
                MapBuilder.SetFlagRaised01(Mathf.SmoothStep(0f, 1f, t));
                if (ceremonyGlow != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                    Color c = new Color(1f, 0.88f, 0.62f,
                        Mathf.Clamp01(ceremonyRemain / CeremonyDuration) * 0.22f * pulse + 0.06f);
                    ceremonyGlow.GetComponent<SpriteRenderer>().color = c;
                }
                if (ceremonyRemain <= 0f) EndCeremony();
            }

            // 2) 金融街夜景亮化：α = 夜幕（完工后才有,落成前恒 0）
            bool finCompleted = TownGrowthService.FinancialStreet(
                state.progression != null ? state.progression.completedChapter : 0) == DistrictPhase.Completed;
            float lightsAlpha = fx && finCompleted ? AtmosphereDirector.NightFactor : 0f;
            MapBuilder.ApplyBankFacadeLightsAlpha(lightsAlpha);

            // 3) 施工尘土（§142：≤30 粒，只在围挡期与氛围效果开启时）
            bool anyConstruction = TownGrowthService.EastDistrict(
                state.progression != null ? state.progression.completedChapter : 0) == DistrictPhase.UnderConstruction ||
                TownGrowthService.CivicPlaza(
                state.progression != null ? state.progression.completedChapter : 0) == DistrictPhase.UnderConstruction ||
                TownGrowthService.FinancialStreet(
                state.progression != null ? state.progression.completedChapter : 0) == DistrictPhase.UnderConstruction;
            UpdateDust(anyConstruction && fx);
        }

        void EndCeremony()
        {
            MapBuilder.SetFlagRaised01(1f);   // 打断/播完都直接呈现完工态
            if (ceremonyGlow != null) ceremonyGlow.SetActive(false);
        }

        void UpdateDust(bool active)
        {
            if (active)
            {
                dustTimer -= Time.deltaTime;
                if (dustTimer <= 0f)
                {
                    dustTimer = DustInterval;
                    if (dust.Count < MaxDustPuffs) SpawnDust();
                }
            }
            for (int i = dust.Count - 1; i >= 0; i--)
            {
                DustPuff p = dust[i];
                p.age += Time.deltaTime;
                if (p.age >= DustLifetime)
                {
                    Destroy(p.renderer.gameObject);
                    dust.RemoveAt(i);
                    continue;
                }
                float k = p.age / DustLifetime;
                Vector3 pos = p.renderer.transform.position;
                pos.y += Time.deltaTime * 0.45f;
                p.renderer.transform.position = pos;
                Color c = p.renderer.color;
                c.a = (1f - k) * 0.8f;
                p.renderer.color = c;
            }
            ParticleBudget.Report("dust", dust.Count);   // §154 全局粒子账本
        }

        /// <summary>店内早退的清账:销毁全部在场尘土并向粒子账本报 0(§154 契约:系统停止时上报 0)。</summary>
        void ClearDustAndReportZero()
        {
            for (int i = 0; i < dust.Count; i++)
                if (dust[i].renderer != null)
                {
                    // EditMode(测试反射驱动)禁止 Destroy,必须 DestroyImmediate;Play 用 Destroy。
                    if (Application.isPlaying) Destroy(dust[i].renderer.gameObject);
                    else DestroyImmediate(dust[i].renderer.gameObject);
                }
            dust.Clear();
            ParticleBudget.Report("dust", 0);
        }

        void SpawnDust()
        {
            GameState state = GameState.I;
            int cc = state != null && state.progression != null ? state.progression.completedChapter : 0;
            // 三个施工区的尘土发射矩形（与围挡地块对齐）
            Vector2 min, max;
            if (TownGrowthService.EastDistrict(cc) == DistrictPhase.UnderConstruction) { min = new Vector2(24.6f, -8.8f); max = new Vector2(28.6f, 12.4f); }
            else if (TownGrowthService.CivicPlaza(cc) == DistrictPhase.UnderConstruction) { min = new Vector2(2.4f, 3.0f); max = new Vector2(4.4f, 8.0f); }
            else { min = new Vector2(16.4f, 7.4f); max = new Vector2(18.4f, 9.6f); }

            dustMoteSeed = (dustMoteSeed + 0.731f) % 1f;
            float fx1 = Mathf.Sin(dustMoteSeed * 43.7f) * 0.5f + 0.5f;
            float fy1 = Mathf.Abs(Mathf.Cos(dustMoteSeed * 91.3f));
            GameObject go = new GameObject("growth_dust");
            go.transform.position = new Vector3(
                Mathf.Lerp(min.x, max.x, fx1), Mathf.Lerp(min.y, max.y, fy1), 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.DustPuff();
            sr.sortingOrder = 930;
            dust.Add(new DustPuff { renderer = sr, age = 0f });
        }

        /// <summary>落成演出期间任意输入(移动/Esc/E)视为打断信号(§121 同款判定,统一入口)。</summary>
        static bool PlayerWantsControl()
        {
            return PlayerController.WantsControlInput();
        }
    }
}
