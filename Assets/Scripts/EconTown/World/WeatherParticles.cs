using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.1 装饰性天气与季节微粒（PRD §123B/§122）。
    /// 手搓 SpriteRenderer 池(FloatText.Runner 先例):雨 ≤120 / 雪 ≤80 / 秋叶 ≤25,
    /// 发射区域=相机视野,落地即回收,零经济副作用——一切输入只读 economy.atmosphere 缓存。
    /// 全局并发上限:同帧只刷"当前模式"一种粒子(§154 粒子预算内调度)。
    /// </summary>
    public class WeatherParticles : MonoBehaviour
    {
        public static WeatherParticles I { get; private set; }

        struct Mote
        {
            public GameObject go;
            public float fallSpeed;   // 格/秒
            public float swayAmp;     // 横向摆幅
            public float phase;
        }

        readonly List<Mote> active = new List<Mote>();
        int mode;                 // 当前模式 -1 无 / 0 雨 / 1 雪 / 2 秋叶 / 3 云团(同屏只跑一种)
        const int CapRain = 120, CapSnow = 80, CapLeaves = 25, CapClouds = 6;
        const int MaxSpawnPerFrame = 6;

        public void OnDestroy()
        {
            if (I == this) I = null;
            ClearAllAndReportZero();   // 围猎三#16:停止路径也要向账本报 0 清账,否则「weather」键跨场景残留虚高
        }

        public static void Create()
        {
            if (I != null) return;
            I = new GameObject("WeatherParticles").AddComponent<WeatherParticles>();
        }

        void LateUpdate()
        {
            GameState s = GameState.I;
            Camera cam = Camera.main;
            if (s == null || cam == null)
            {
                ClearAllAndReportZero();
                return;
            }
            var atm = s.economy != null ? s.economy.atmosphere : null;
            if (atm == null || !s.experience.settings.atmosphereEffects)
            {
                ClearAllAndReportZero();
                return;
            }
            // V0.9.7:店内不淋雨——粒子按相机视野发射,进店后不早退就会「室内下雨」。
            // 早退也须向 ParticleBudget 报 0 清账(§154 契约:系统停止时上报 0,否则账本残留虚高)
            if (ShopInteriorController.IsInsideShopInterior)
            {
                ClearAllAndReportZero();
                return;
            }

            // 候选互斥:雨/雪 > 多云云团 > 秋叶(秋季晴天才飘叶,PRD §122)。
            // V0.7.5 补齐 §123A「装饰阴天只有云层」的视觉载体:多云=6 团慢速漂移云,
            // 事件阴天(matup)把天气绑定为多云 → 同样的云团 + 灰蒙滤镜,玩家可区分。
            int season = AtmosphereService.SeasonIndex(Mathf.Max(1, s.day));
            int wantMode = -1, wantCap = 0;
            if (atm.weather == AtmosphereService.WeatherRain) { wantMode = 0; wantCap = CapRain; }
            else if (atm.weather == AtmosphereService.WeatherSnow) { wantMode = 1; wantCap = CapSnow; }
            else if (atm.weather == AtmosphereService.WeatherCloudy) { wantMode = 3; wantCap = CapClouds; }
            else if (season == 2 && atm.weather == AtmosphereService.WeatherClear)
            { wantMode = 2; wantCap = CapLeaves; }

            if (wantMode != mode)
            {
                mode = wantMode;         // 切模式时全量回收,下一帧起重新落新粒子
                ClearAll();
            }
            if (mode < 0) { ClearAllAndReportZero(); return; }

            float halfH = cam.orthographicSize + 0.8f;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;

            if (mode == 3)
            {
                UpdateClouds(c, halfH, halfW);
                ParticleBudget.Report("weather", active.Count);
                return;
            }

            int spawnBudget = MaxSpawnPerFrame;   // 帧限流防生成峰值尖刺
            while (active.Count < wantCap && spawnBudget-- > 0)
                SpawnMote(new Vector3(c.x + Random.Range(-halfW, halfW), c.y + Random.Range(halfH * 0.75f, halfH), 0f));

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Mote m = active[i];
                m.phase += Time.deltaTime * 2f;
                Vector3 p = m.go.transform.position;
                p.y -= m.fallSpeed * Time.deltaTime;
                p.x += Mathf.Sin(m.phase) * m.swayAmp * Time.deltaTime;
                m.go.transform.position = p;
                if (m.go.transform.position.y < c.y - halfH - 0.5f ||
                    Mathf.Abs(m.go.transform.position.x - c.x) > halfW + 1f)
                {
                    Destroy(m.go);
                    active.RemoveAt(i);
                    continue;
                }
                active[i] = m;
            }

            // 相机移动后视野外多余的粒子渐次回收,不为省几枚对象做精确裁剪
            while (active.Count > wantCap)
            {
                int last = active.Count - 1;
                Destroy(active[last].go);
                active.RemoveAt(last);
            }

            ParticleBudget.Report("weather", active.Count);   // §154 全局粒子账本
        }

        void SpawnMote(Vector3 pos)
        {
            Sprite sprite = PixelArt.WeatherMote(mode);
            if (sprite == null) return;
            GameObject go = new GameObject("weather_mote");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 320;                        // 盖过世界物、低于世界文字与 UI
            active.Add(new Mote
            {
                go = go,
                fallSpeed = mode == 0 ? Random.Range(7f, 9f) : mode == 1 ? Random.Range(1.6f, 2.6f) : Random.Range(1.2f, 2.0f),
                swayAmp = mode == 0 ? 0f : mode == 1 ? 0.9f : 1.6f,   // 叶子横飘最明显
                phase = Random.Range(0f, 6.28f)
            });
        }

        void ClearAll()
        {
            if (active.Count == 0) return;
            for (int i = 0; i < active.Count; i++)
                if (active[i].go != null) Destroy(active[i].go);
            active.Clear();
        }

        /// <summary>围猎三#1/#16:全部停止路径的统一出口——清场并向 §154 粒子账本报 0。
        /// 旧实现各早退只 ClearAll 不报数,「weather」键长期虚高;ClearAll 的空场早退
        /// 不清账本键,所以报数必须无条件执行。</summary>
        void ClearAllAndReportZero()
        {
            ClearAll();
            ParticleBudget.Report("weather", 0);
        }

        /// <summary>V0.7.5 云团模式(§123A 装饰阴天的云层):6 片低对比云影慢速横漂,越过相机视野即从另一侧
        /// 回流(云影是常驻漂移物,不销毁重建);只有极轻微上下浮动,避免像实体贴图悬在建筑前。</summary>
        void UpdateClouds(Vector3 c, float halfH, float halfW)
        {
            // 不足 6 团时补齐(初始或相机大跨度移动后)
            while (active.Count < CapClouds)
                SpawnCloud(new Vector3(
                    c.x + Random.Range(-halfW, halfW),
                    c.y + Random.Range(-halfH * 0.82f, halfH * 0.82f), 0f));

            float span = halfW * 2f + 8f;   // 云团横向循环带宽(视野外 4 单位缓冲)
            for (int i = 0; i < active.Count; i++)
            {
                Mote m = active[i];
                m.phase += Time.deltaTime * 0.35f;
                Vector3 p = m.go.transform.position;
                p.x += m.fallSpeed * Time.deltaTime;   // 云团复用 fallSpeed 字段作横漂速度
                p.y += Mathf.Sin(m.phase) * m.swayAmp * Time.deltaTime;
                if (p.x - c.x > halfW + 4f) p.x -= span;   // 右侧越界 → 从左侧回流
                // 围猎三#15:对称补左侧回流——相机向右追随时(云横漂慢)旧云全部落出左界,
                // 只靠「数量补齐」会以视野内随机点闪现补位,不是从边缘飘入。
                if (c.x - p.x > halfW + 4f) p.x += span;
                // 围猎三#15 复审:y 轴同病——云的 y 只有正弦浮动不会自漂,相机南北贯穿后
                // 全部云团永久留在原纬度(6 团满员时数量补齐也不触发),多云天空永久无云。
                if (p.y - c.y > halfH * 0.82f + 2f || c.y - p.y > halfH * 0.82f + 2f)
                    p.y = c.y + Random.Range(-halfH * 0.7f, halfH * 0.7f);
                m.go.transform.position = p;
                active[i] = m;
            }
        }

        void SpawnCloud(Vector3 pos)
        {
            Sprite sprite = PixelArt.WeatherCloudShadow(Random.Range(0, 3));
            if (sprite == null) return;
            GameObject go = new GameObject("weather_cloud_shadow");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            float xScale = Random.Range(1.45f, 2.15f);
            if (Random.value < 0.5f) xScale = -xScale;
            go.transform.localScale = new Vector3(xScale, Random.Range(1.0f, 1.35f), 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color32(0xff, 0xff, 0xff, (byte)Random.Range(185, 221));
            sr.sortingOrder = 310;                        // 低透明覆盖世界、低于雨和世界文字
            active.Add(new Mote
            {
                go = go,
                fallSpeed = Random.Range(0.24f, 0.4f),    // 横漂速度(格/秒)
                swayAmp = Random.Range(0.025f, 0.07f),
                phase = Random.Range(0f, 6.28f)
            });
        }
    }
}
