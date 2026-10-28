using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// 程序化像素美术工厂:地块、建筑、角色动画帧、装饰物、UI 九宫格皮肤。
    /// 全部运行时生成(FilterMode.Point,无外部素材)。
    /// </summary>
    public static class PixelArt
    {
        public const int PPU = 16;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, CharacterFrames> characterCache = new Dictionary<string, CharacterFrames>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            // 禁用 Domain Reload 时静态缓存跨 Play 存活,只 Clear 会漏掉原生纹理与精灵,逐次进 Play 泄漏全套资源。
            HashSet<UnityEngine.Object> doomed = new HashSet<UnityEngine.Object>();
            CollectDoomed(cache.Values, doomed);
            foreach (CharacterFrames frames in characterCache.Values)
            {
                CollectDoomed(frames.down, doomed);
                CollectDoomed(frames.up, doomed);
                CollectDoomed(frames.side, doomed);
            }
            cache.Clear();
            characterCache.Clear();
            foreach (UnityEngine.Object o in doomed)
                if (o != null) UnityEngine.Object.Destroy(o);
        }

        static void CollectDoomed(IEnumerable<Sprite> sprites, HashSet<UnityEngine.Object> doomed)
        {
            if (sprites == null) return;
            foreach (Sprite sp in sprites)
            {
                if (sp == null) continue;
                doomed.Add(sp);
                if (sp.texture != null) doomed.Add(sp.texture);
            }
        }

        static bool TryGetCached(string key, out Sprite sprite)
        {
            if (cache.TryGetValue(key, out sprite) && sprite != null)
                return true;

            cache.Remove(key);
            sprite = null;
            return false;
        }

        // ————— 基础绘制 —————

        static Texture2D NewTex(int w, int h)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(new Color32[w * h]);
            return t;
        }

        static void Apply(Texture2D t) { t.Apply(); }

        static void P(Texture2D t, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= t.width || y >= t.height) return;
            t.SetPixel(x, y, c);
        }

        static void Rect(Texture2D t, int x, int y, int w, int h, Color c)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    P(t, x + i, y + j, c);
        }

        static void Frame(Texture2D t, int x, int y, int w, int h, Color c)
        {
            for (int i = 0; i < w; i++) { P(t, x + i, y, c); P(t, x + i, y + h - 1, c); }
            for (int j = 0; j < h; j++) { P(t, x, y + j, c); P(t, x + w - 1, y + j, c); }
        }

        static void VLine(Texture2D t, int x, int y0, int y1, Color c) { for (int y = y0; y <= y1; y++) P(t, x, y, c); }
        static void HLine(Texture2D t, int y, int x0, int x1, Color c) { for (int x = x0; x <= x1; x++) P(t, x, y, c); }

        static Color RGB(byte r, byte g, byte b) { return new Color32(r, g, b, 255); }
        static Color Shade(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

        static Sprite Finish(Texture2D t, string key, Vector4 border = default(Vector4))
        {
            Sprite s = Sprite.Create(t, new Rect(0, 0, t.width, t.height),
                new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect, border);
            s.name = key;   // 统一以缓存键命名:E2E 与视觉序列一致性可按名验证(PRD §119-2)
            cache[key] = s;
            return s;
        }

        // ————— 地块 —————

        public static Sprite Grass(int variant) { return Grass(0, variant); }

        /// <summary>V0.7.1 季节变体：形状不变(key 含季节后缀生成新贴图)，只换基色——运行期严禁 ResetCache，
        /// 旧季条目自然失引用由跨 Play 清理回收(PRD §122)。</summary>
        public static Sprite Grass(int season, int variant)
        {
            string key = "grass_s" + season + "_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color baseC, bladeDark, bladeLight;
            switch (season)
            {
                case 1: baseC = RGB(0x58, 0xa0, 0x4a); bladeDark = RGB(0x46, 0x82, 0x3a); bladeLight = RGB(0x84, 0xc2, 0x66); break; // 夏·浓绿
                case 2: baseC = RGB(0xc4, 0xa8, 0x52); bladeDark = RGB(0x9a, 0x83, 0x3e); bladeLight = RGB(0xdc, 0xc8, 0x78); break; // 秋·橙黄
                case 3: baseC = RGB(0xad, 0xb8, 0xa6); bladeDark = RGB(0x8d, 0x9a, 0x8a); bladeLight = RGB(0xd4, 0xdc, 0xcf); break; // 冬·灰绿偏白
                default: baseC = RGB(0x74, 0xae, 0x5a); bladeDark = RGB(0x5a, 0x94, 0x46); bladeLight = RGB(0x8f, 0xc4, 0x72); break; // 春·嫩绿
            }
            Rect(t, 0, 0, 16, 16, baseC);
            System.Random r = new System.Random(77 + variant * 31);
            for (int i = 0; i < 26; i++)
            {
                int x = r.Next(16), y = r.Next(16);
                P(t, x, y, Shade(baseC, 0.92f + (float)r.NextDouble() * 0.08f));
            }
            for (int i = 0; i < 4 + variant; i++)
            {
                int x = r.Next(1, 15), y = r.Next(1, 15);
                P(t, x, y, bladeDark);
                P(t, x, y + 1, bladeLight);
            }
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite Road(int variant)
        {
            string key = "road" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color baseC = RGB(0xcf, 0xb7, 0x89);
            Rect(t, 0, 0, 16, 16, baseC);
            System.Random r = new System.Random(500 + variant * 17);
            for (int i = 0; i < 20; i++)
                P(t, r.Next(16), r.Next(16), Shade(baseC, 0.94f));
            for (int i = 0; i < 3; i++)
            {
                int x = r.Next(2, 13), y = r.Next(2, 13);
                Rect(t, x, y, 2, 2, RGB(0xb5, 0x9c, 0x70));   // 石子
                P(t, x, y + 1, RGB(0xe2, 0xcc, 0xa2));
            }
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite Plaza(int variant)
        {
            string key = "plaza" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color baseC = RGB(0xd9, 0xbe, 0x8a);
            Rect(t, 0, 0, 16, 16, baseC);
            for (int x = 0; x < 16; x += 8)
                for (int y = 0; y < 16; y += 8)
                {
                    Frame(t, x, y, 8, 8, RGB(0xbf, 0xa2, 0x70));
                    P(t, x + 1, y + 1, RGB(0xe8, 0xd2, 0xa2));
                }
            if (variant == 1) { P(t, 4, 4, RGB(0xb5, 0x9c, 0x70)); P(t, 11, 12, RGB(0xb5, 0x9c, 0x70)); }
            Apply(t);
            return Finish(t, key);
        }

        // ————— 装饰物 —————

        public static Sprite Tree() { return Tree(0); }

        /// <summary>V0.7.1 季节变体树：树干全年不变，树冠按季换色（冬灰绿覆霜）。</summary>
        public static Sprite Tree(int season)
        {
            string key = "tree_s" + season;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(20, 26);
            Color c1, c2, c3, hi, border;
            switch (season)
            {
                case 1: c1 = RGB(0x3f, 0x7f, 0x34); c2 = RGB(0x49, 0x89, 0x3d); c3 = RGB(0x54, 0x8f, 0x48); hi = RGB(0x74, 0xb2, 0x5a); border = RGB(0x2f, 0x5f, 0x28); break;
                case 2: c1 = RGB(0xa0, 0x6a, 0x36); c2 = RGB(0xb3, 0x7a, 0x3e); c3 = RGB(0xc0, 0x8a, 0x46); hi = RGB(0xd8, 0xa4, 0x5a); border = RGB(0x7a, 0x50, 0x28); break;
                case 3: c1 = RGB(0x6e, 0x7d, 0x72); c2 = RGB(0x7a, 0x89, 0x7e); c3 = RGB(0x86, 0x95, 0x8a); hi = RGB(0xa2, 0xb2, 0xa4); border = RGB(0x55, 0x64, 0x5a); break;
                default: c1 = RGB(0x4e, 0x8a, 0x3c); c2 = RGB(0x5a, 0x9a, 0x46); c3 = RGB(0x67, 0xa8, 0x50); hi = RGB(0x82, 0xc2, 0x66); border = RGB(0x3e, 0x70, 0x30); break;
            }
            Rect(t, 8, 0, 4, 8, RGB(0x7a, 0x54, 0x36));            // 树干
            Rect(t, 8, 0, 1, 8, RGB(0x5e, 0x3f, 0x27));
            Rect(t, 3, 7, 14, 6, c1);                              // 树冠三层
            Rect(t, 5, 13, 10, 5, c2);
            Rect(t, 7, 18, 6, 4, c3);
            Frame(t, 3, 7, 14, 6, border);
            P(t, 6, 15, hi); P(t, 12, 17, hi);
            P(t, 9, 20, hi);
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite Flower(int variant) { return Flower(0, variant); }

        /// <summary>V0.7.1 季节变体花：冬季由雪绒白花瓣兜底（冬季地图层不再生成花丛，此档仅为防御）。</summary>
        public static Sprite Flower(int season, int variant)
        {
            string key = "flower_s" + season + "_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(8, 8);
            VLine(t, 3, 0, 3, season == 3 ? RGB(0x6e, 0x7d, 0x72) : RGB(0x4e, 0x8a, 0x3c));
            P(t, 4, 1, season == 3 ? RGB(0x6e, 0x7d, 0x72) : RGB(0x4e, 0x8a, 0x3c));
            Color petal = season == 3 ? RGB(0xe8, 0xec, 0xf2)
                : variant == 0 ? RGB(0xe2, 0x7a, 0xb8) : variant == 1 ? RGB(0xf2, 0xd4, 0x5a) : RGB(0xe8, 0x8a, 0x4a);
            P(t, 3, 5, petal); P(t, 2, 4, petal); P(t, 4, 4, petal); P(t, 3, 3, petal);
            P(t, 3, 4, season == 3 ? RGB(0xcf, 0xd8, 0xe4) : RGB(0xff, 0xf2, 0xb0));
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite Shrub() { return Shrub(0); }

        /// <summary>低矮灌木丛：与树木同步换季，用于组织草地边界而不遮挡建筑。</summary>
        public static Sprite Shrub(int season)
        {
            string key = "shrub_s" + season;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(18, 13);
            Color dark, mid, light, highlight;
            switch (season)
            {
                case 1: dark = RGB(0x2f, 0x6d, 0x32); mid = RGB(0x46, 0x86, 0x3d); light = RGB(0x5c, 0x9c, 0x4b); highlight = RGB(0x79, 0xb8, 0x60); break;
                case 2: dark = RGB(0x7a, 0x4e, 0x2c); mid = RGB(0x9a, 0x62, 0x32); light = RGB(0xb8, 0x78, 0x3a); highlight = RGB(0xd3, 0x9a, 0x4e); break;
                case 3: dark = RGB(0x5b, 0x69, 0x60); mid = RGB(0x71, 0x80, 0x76); light = RGB(0x8a, 0x99, 0x8e); highlight = RGB(0xd8, 0xe0, 0xda); break;
                default: dark = RGB(0x35, 0x70, 0x31); mid = RGB(0x4d, 0x8b, 0x3d); light = RGB(0x63, 0xa1, 0x4c); highlight = RGB(0x83, 0xbf, 0x66); break;
            }
            Rect(t, 1, 2, 16, 5, dark);
            Rect(t, 3, 6, 6, 5, dark);
            Rect(t, 9, 5, 7, 6, dark);
            Rect(t, 2, 3, 14, 3, mid);
            Rect(t, 4, 6, 5, 4, mid);
            Rect(t, 10, 6, 5, 4, light);
            P(t, 5, 9, highlight); P(t, 12, 9, highlight); P(t, 8, 6, light); P(t, 15, 5, highlight);
            if (season == 3)
            {
                HLine(t, 10, 4, 8, highlight);
                HLine(t, 10, 11, 14, highlight);
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>低矮高草簇：填补大片草坪的空白，四季换色但不形成碰撞。</summary>
        public static Sprite GrassTuft(int season)
        {
            string key = "grass_tuft_s" + season;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(18, 12);
            Color dark, mid, light;
            switch (season)
            {
                case 1: dark = RGB(0x35, 0x72, 0x34); mid = RGB(0x4f, 0x8d, 0x43); light = RGB(0x70, 0xa8, 0x55); break;
                case 2: dark = RGB(0x72, 0x59, 0x2f); mid = RGB(0x9b, 0x76, 0x38); light = RGB(0xc0, 0x96, 0x4b); break;
                case 3: dark = RGB(0x6c, 0x72, 0x67); mid = RGB(0x8a, 0x90, 0x82); light = RGB(0xc7, 0xce, 0xc5); break;
                default: dark = RGB(0x3b, 0x76, 0x35); mid = RGB(0x55, 0x91, 0x42); light = RGB(0x76, 0xad, 0x56); break;
            }
            // 三簇不同高度的叶片，底部留透明空隙，避免看起来像一条绿砖。
            VLine(t, 3, 1, 7, dark); VLine(t, 4, 1, 9, mid); VLine(t, 5, 1, 6, light);
            P(t, 2, 5, dark); P(t, 6, 4, mid);
            VLine(t, 9, 1, 5, dark); VLine(t, 10, 1, 8, mid); VLine(t, 11, 1, 6, light);
            P(t, 8, 4, mid); P(t, 12, 5, dark);
            VLine(t, 14, 1, 6, dark); VLine(t, 15, 1, 9, mid); P(t, 16, 4, light);
            HLine(t, 1, 2, 16, new Color32(0x31, 0x58, 0x2e, 90));
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>可辨识的野花片：比单朵花更适合作为草地视觉落点。</summary>
        public static Sprite MeadowPatch(int season, int variant)
        {
            variant = Mathf.Clamp(variant, 0, 2);
            string key = "meadow_patch_s" + season + "_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(24, 13);
            Color green = season == 2 ? RGB(0x83, 0x6a, 0x35)
                : season == 3 ? RGB(0x7d, 0x86, 0x7b) : RGB(0x4d, 0x8d, 0x42);
            Color lightGreen = season == 2 ? RGB(0xad, 0x88, 0x43)
                : season == 3 ? RGB(0xb8, 0xc0, 0xb8) : RGB(0x72, 0xab, 0x56);
            Color petalA = variant == 0 ? RGB(0xf0, 0x8b, 0xc4)
                : variant == 1 ? RGB(0xf2, 0xd4, 0x62) : RGB(0xf0, 0x98, 0x52);
            Color petalB = variant == 0 ? RGB(0xff, 0xd0, 0xe8)
                : variant == 1 ? RGB(0xff, 0xef, 0xa0) : RGB(0xff, 0xc0, 0x78);
            int[] stems = { 3, 7, 12, 17, 21 };
            int[] heights = { 6, 9, 7, 10, 6 };
            for (int i = 0; i < stems.Length; i++)
            {
                VLine(t, stems[i], 1, heights[i] - 1, i % 2 == 0 ? green : lightGreen);
                P(t, stems[i], heights[i], i % 2 == 0 ? petalA : petalB);
                P(t, stems[i] - 1, heights[i] - 1, petalA);
                P(t, stems[i] + 1, heights[i] - 1, petalB);
            }
            HLine(t, 1, 2, 22, new Color32(0x31, 0x58, 0x2e, 70));
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>三块不同尺寸的景观石，用于草地花境的视觉收口。</summary>
        public static Sprite RockCluster()
        {
            Sprite cached;
            if (TryGetCached("rock_cluster", out cached)) return cached;
            Texture2D t = NewTex(20, 12);
            Color shadow = new Color32(0x2e, 0x42, 0x32, 52);
            Color outline = RGB(0x66, 0x6a, 0x63);
            Color stone = RGB(0x91, 0x94, 0x88);
            Color light = RGB(0xb5, 0xb7, 0xaa);
            Rect(t, 1, 0, 18, 2, shadow);
            Rect(t, 1, 2, 7, 5, outline); Rect(t, 2, 3, 5, 3, stone); P(t, 3, 5, light);
            Rect(t, 7, 2, 10, 8, outline); Rect(t, 8, 3, 8, 6, stone); HLine(t, 8, 9, 13, light);
            Rect(t, 15, 2, 4, 4, outline); Rect(t, 16, 3, 2, 2, light);
            Apply(t);
            return Finish(t, "rock_cluster");
        }

        /// <summary>木面铁脚的小镇长椅，作为草地中可辨识的休息点。</summary>
        public static Sprite ParkBench()
        {
            Sprite cached;
            if (TryGetCached("park_bench", out cached)) return cached;
            Texture2D t = NewTex(26, 18);
            Color metal = RGB(0x4d, 0x50, 0x4d);
            Color woodDark = RGB(0x6b, 0x43, 0x28);
            Color wood = RGB(0xa6, 0x6b, 0x38);
            Color woodLight = RGB(0xca, 0x8d, 0x4b);
            Rect(t, 4, 11, 18, 5, woodDark);
            Rect(t, 5, 12, 16, 3, wood); HLine(t, 14, 6, 19, woodLight);
            Rect(t, 3, 7, 20, 4, woodDark);
            Rect(t, 4, 8, 18, 2, wood); HLine(t, 9, 5, 20, woodLight);
            Rect(t, 5, 2, 3, 6, metal); Rect(t, 18, 2, 3, 6, metal);
            Rect(t, 3, 1, 6, 2, metal); Rect(t, 17, 1, 6, 2, metal);
            Apply(t);
            return Finish(t, "park_bench");
        }

        // ————— V0.7.2 港口区 —————

        public static Sprite HarborWater(int variant)
        {
            string key = "harbor_water_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color deep = RGB(0x35, 0x72, 0x86);
            Color mid = RGB(0x43, 0x86, 0x96);
            Color light = RGB(0x6a, 0xa8, 0xb0);
            Rect(t, 0, 0, 16, 16, deep);
            int offset = variant * 3;
            HLine(t, 4 + offset % 5, 1, 9, mid);
            HLine(t, 11 - offset % 4, 7, 15, light);
            P(t, 0, 5 + variant, mid); P(t, 10, 10 - variant, light);
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite HarborYard(int variant)
        {
            string key = "harbor_yard_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color slab = RGB(0x9e, 0x9a, 0x86);
            Rect(t, 0, 0, 16, 16, slab);
            Frame(t, 0, 0, 16, 16, RGB(0x7f, 0x7d, 0x70));
            HLine(t, 8, 0, 15, RGB(0xb5, 0xb0, 0x98));
            if (variant == 1) { P(t, 4, 4, RGB(0x78, 0x77, 0x6b)); P(t, 12, 13, RGB(0xc2, 0xbc, 0xa1)); }
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite HarborShore()
        {
            Sprite cached;
            if (TryGetCached("harbor_shore", out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Rect(t, 0, 0, 7, 16, RGB(0x91, 0x8b, 0x78));
            Rect(t, 7, 0, 4, 16, RGB(0xc8, 0xb4, 0x84));
            Rect(t, 11, 0, 5, 16, RGB(0x45, 0x83, 0x94));
            for (int y = 1; y < 16; y += 4)
            {
                P(t, 8, y, RGB(0xe2, 0xd0, 0xa0));
                P(t, 12, y + 1, RGB(0x78, 0xb0, 0xb5));
            }
            Apply(t);
            return Finish(t, "harbor_shore");
        }

        public static Sprite HarborCrane()
        {
            Sprite cached;
            if (TryGetCached("harbor_crane", out cached)) return cached;
            Texture2D t = NewTex(56, 88);
            Color dark = RGB(0x4d, 0x42, 0x37);
            Color orange = RGB(0xe0, 0x87, 0x2e);
            Color light = RGB(0xf2, 0xb0, 0x45);
            Rect(t, 8, 0, 7, 72, dark); Rect(t, 10, 3, 3, 67, orange);
            Rect(t, 40, 0, 7, 72, dark); Rect(t, 42, 3, 3, 67, orange);
            Rect(t, 5, 68, 46, 8, dark); Rect(t, 8, 70, 40, 4, light);
            Rect(t, 27, 73, 5, 13, dark); Rect(t, 28, 74, 3, 11, orange);
            HLine(t, 66, 14, 40, dark);
            for (int x = 15; x <= 39; x += 6) VLine(t, x, 40, 66, dark);
            Rect(t, 18, 0, 20, 5, dark); Rect(t, 20, 2, 16, 3, orange);
            Apply(t);
            return Finish(t, "harbor_crane");
        }

        public static Sprite HarborCraneLight(bool lit)
        {
            string key = "harbor_crane_light_" + (lit ? "on" : "off");
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(8, 8);
            Color rim = RGB(0x62, 0x4a, 0x31);
            Color bulb = lit ? RGB(0xff, 0xe6, 0x78) : RGB(0x88, 0x7c, 0x62);
            Rect(t, 1, 1, 6, 6, rim); Rect(t, 2, 2, 4, 4, bulb);
            if (lit) { P(t, 3, 6, RGB(0xff, 0xf5, 0xc0)); P(t, 6, 3, RGB(0xff, 0xf5, 0xc0)); }
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite HarborContainer(bool imported, bool tariffStripe)
        {
            string key = "harbor_container_" + (imported ? "import" : "export") + (tariffStripe ? "_tariff" : "");
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(18, 14);
            Color dark = imported ? RGB(0x2f, 0x62, 0x78) : RGB(0x8f, 0x4e, 0x24);
            Color body = imported ? RGB(0x4f, 0x91, 0xa8) : RGB(0xd0, 0x78, 0x32);
            Color hi = imported ? RGB(0x72, 0xb0, 0xbf) : RGB(0xef, 0xa0, 0x4a);
            Rect(t, 0, 1, 18, 12, dark); Rect(t, 1, 2, 16, 10, body);
            for (int x = 3; x < 17; x += 4) VLine(t, x, 3, 10, dark);
            HLine(t, 10, 2, 15, hi);
            if (tariffStripe) Rect(t, 0, 6, 18, 3, RGB(0xc8, 0x3d, 0x32));
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>港区常驻空箱堆：只负责环境密度，不代表当日进出口账本。</summary>
        public static Sprite HarborContainerStack(int palette, int tiers)
        {
            palette = Mathf.Clamp(palette, 0, 3);
            tiers = Mathf.Clamp(tiers, 1, 2);
            string key = "harbor_container_stack_" + palette + "_" + tiers;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(46, tiers == 1 ? 17 : 31);
            Color[,] colors = {
                { RGB(0xc7, 0x69, 0x2d), RGB(0xf0, 0x9a, 0x45), RGB(0x3f, 0x75, 0x84) },
                { RGB(0x73, 0x75, 0x3e), RGB(0xa7, 0x9a, 0x4e), RGB(0x4d, 0x5f, 0x42) },
                { RGB(0x8b, 0x43, 0x3a), RGB(0xc4, 0x64, 0x52), RGB(0x58, 0x45, 0x3e) },
                { RGB(0x3f, 0x6e, 0x8a), RGB(0x63, 0x95, 0xac), RGB(0x3e, 0x50, 0x59) }
            };
            Color dark = colors[palette, 2];
            PaintContainerUnit(t, 1, 1, colors[palette, 0], dark, Shade(colors[palette, 0], 1.2f));
            PaintContainerUnit(t, 24, 1, colors[palette, 1], dark, Shade(colors[palette, 1], 1.18f));
            if (tiers == 2)
            {
                int secondPalette = (palette + 1) % 4;
                PaintContainerUnit(t, 3, 15, colors[secondPalette, 1], colors[secondPalette, 2],
                    Shade(colors[secondPalette, 1], 1.16f));
                PaintContainerUnit(t, 26, 15, colors[secondPalette, 0], colors[secondPalette, 2],
                    Shade(colors[secondPalette, 0], 1.18f));
            }
            Apply(t);
            return Finish(t, key);
        }

        static void PaintContainerUnit(Texture2D t, int x, int y, Color body, Color dark, Color hi)
        {
            Rect(t, x, y, 21, 13, dark); Rect(t, x + 1, y + 1, 19, 11, body);
            for (int rib = x + 4; rib < x + 20; rib += 4) VLine(t, rib, y + 2, y + 10, dark);
            HLine(t, y + 10, x + 2, x + 18, hi);
            Rect(t, x + 2, y + 2, 2, 2, hi); Rect(t, x + 17, y + 2, 2, 2, hi);
        }

        public static Sprite HarborPalletCargo()
        {
            Sprite cached;
            if (TryGetCached("harbor_pallet_cargo", out cached)) return cached;
            Texture2D t = NewTex(32, 24);
            Color woodDark = RGB(0x5a, 0x3d, 0x2a);
            Color wood = RGB(0x9a, 0x68, 0x3d);
            Color carton = RGB(0xc7, 0x9a, 0x62);
            Rect(t, 2, 1, 28, 4, woodDark);
            for (int x = 4; x < 30; x += 7) Rect(t, x, 0, 3, 3, woodDark);
            Rect(t, 4, 5, 11, 9, woodDark); Rect(t, 5, 6, 9, 7, carton);
            Rect(t, 17, 5, 11, 12, woodDark); Rect(t, 18, 6, 9, 10, Shade(carton, .9f));
            Rect(t, 8, 14, 11, 9, woodDark); Rect(t, 9, 15, 9, 7, Shade(carton, 1.08f));
            VLine(t, 12, 6, 12, wood); VLine(t, 23, 6, 15, wood); VLine(t, 16, 15, 21, wood);
            Apply(t);
            return Finish(t, "harbor_pallet_cargo");
        }

        public static Sprite HarborDrumRack()
        {
            Sprite cached;
            if (TryGetCached("harbor_drum_rack", out cached)) return cached;
            Texture2D t = NewTex(32, 22);
            Color frame = RGB(0x4d, 0x47, 0x3f);
            Color yellow = RGB(0xd9, 0x9a, 0x35);
            Color teal = RGB(0x45, 0x84, 0x82);
            Rect(t, 2, 1, 28, 3, frame); Rect(t, 3, 4, 3, 16, frame); Rect(t, 26, 4, 3, 16, frame);
            for (int i = 0; i < 4; i++)
            {
                int x = 6 + i * 6;
                Color body = (i & 1) == 0 ? yellow : teal;
                Rect(t, x, 5, 5, 14, frame); Rect(t, x + 1, 6, 3, 12, body);
                HLine(t, 9, x, x + 4, Shade(body, 1.18f)); HLine(t, 15, x, x + 4, frame);
            }
            Apply(t);
            return Finish(t, "harbor_drum_rack");
        }

        public static Sprite HarborForklift()
        {
            Sprite cached;
            if (TryGetCached("harbor_forklift", out cached)) return cached;
            Texture2D t = NewTex(38, 30);
            Color dark = RGB(0x45, 0x40, 0x39);
            Color orange = RGB(0xe0, 0x8b, 0x2f);
            Color hi = RGB(0xf3, 0xb2, 0x48);
            Rect(t, 4, 7, 22, 12, dark); Rect(t, 6, 9, 18, 9, orange);
            Rect(t, 14, 18, 11, 9, dark); Rect(t, 16, 19, 7, 6, RGB(0x80, 0xa4, 0xa6));
            Rect(t, 27, 3, 3, 25, dark); Rect(t, 30, 3, 7, 2, dark); Rect(t, 30, 7, 6, 2, dark);
            Rect(t, 7, 3, 7, 7, dark); Rect(t, 18, 3, 7, 7, dark);
            Rect(t, 9, 4, 3, 3, RGB(0x28, 0x2b, 0x2b)); Rect(t, 20, 4, 3, 3, RGB(0x28, 0x2b, 0x2b));
            HLine(t, 17, 8, 21, hi);
            Apply(t);
            return Finish(t, "harbor_forklift");
        }

        public static Sprite HarborBollard()
        {
            Sprite cached;
            if (TryGetCached("harbor_bollard", out cached)) return cached;
            Texture2D t = NewTex(12, 16);
            Color dark = RGB(0x45, 0x42, 0x3c);
            Color yellow = RGB(0xd8, 0x9a, 0x32);
            Rect(t, 2, 2, 8, 11, dark); Rect(t, 3, 4, 6, 8, yellow);
            Rect(t, 1, 11, 10, 3, dark); Rect(t, 0, 0, 12, 3, dark);
            HLine(t, 9, 3, 8, Shade(yellow, 1.2f));
            Apply(t);
            return Finish(t, "harbor_bollard");
        }

        public static Sprite HarborLaneMarking()
        {
            Sprite cached;
            if (TryGetCached("harbor_lane_marking", out cached)) return cached;
            Texture2D t = NewTex(64, 8);
            Color yellow = new Color32(0xe7, 0xc2, 0x62, 210);
            for (int x = 0; x < 64; x += 12) Rect(t, x, 3, 7, 2, yellow);
            Apply(t);
            return Finish(t, "harbor_lane_marking");
        }

        public static Sprite HarborDock()
        {
            Sprite cached;
            if (TryGetCached("harbor_dock", out cached)) return cached;
            Texture2D t = NewTex(64, 18);
            Color dark = RGB(0x54, 0x42, 0x32);
            Color wood = RGB(0x91, 0x6b, 0x45);
            Rect(t, 0, 6, 64, 10, dark); Rect(t, 0, 9, 64, 6, wood);
            for (int x = 3; x < 64; x += 8) { VLine(t, x, 7, 15, RGB(0xb8, 0x8a, 0x56)); Rect(t, x, 0, 3, 8, dark); }
            Apply(t);
            return Finish(t, "harbor_dock");
        }

        public static Sprite CargoShip(bool recession, int loadTier)
        {
            loadTier = Mathf.Clamp(loadTier, 0, 3);
            string key = "cargo_ship_" + (recession ? "recession_" : "normal_") + loadTier;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(78, 36);
            Color hullDark = RGB(0x39, 0x43, 0x4b);
            Color hull = recession ? RGB(0x66, 0x78, 0x7c) : RGB(0x3f, 0x78, 0x88);
            int waterline = recession ? 8 : 6 + loadTier;
            for (int y = waterline; y < 20; y++)
            {
                int inset = Mathf.Max(0, (y - 16) / 2);
                Rect(t, 5 + inset, y, 68 - inset * 2, 1, y < waterline + 3 ? hullDark : hull);
            }
            Rect(t, 15, 18, 48, 4, hullDark);
            Color[] cargo = { RGB(0xd0, 0x78, 0x32), RGB(0x4f, 0x91, 0xa8), RGB(0x8e, 0x9b, 0x52) };
            int stacks = 3 + loadTier * 2;
            for (int i = 0; i < stacks; i++)
            {
                int x = 17 + (i % 7) * 6;
                int y = 22 + (i / 7) * 6;
                Rect(t, x, y, 5, 5, cargo[i % cargo.Length]);
            }
            Rect(t, 58, 21, 12, 10, RGB(0xe4, 0xe0, 0xc8));
            Rect(t, 61, 25, 3, 3, RGB(0x58, 0x7d, 0x8b));
            Rect(t, 67, 25, 2, 3, RGB(0x58, 0x7d, 0x8b));
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite ConstructionFence()
        {
            Sprite cached;
            if (TryGetCached("construction_fence", out cached)) return cached;
            Texture2D t = NewTex(32, 24);
            Color dark = RGB(0x5c, 0x4a, 0x36);
            Color pale = RGB(0xef, 0xd4, 0x9a);
            Color hazard = RGB(0xd8, 0x78, 0x32);
            Rect(t, 1, 8, 30, 9, dark); Rect(t, 2, 9, 28, 7, pale);
            for (int x = -4; x < 36; x += 10)
                for (int d = 0; d < 8; d++) P(t, x + d, 9 + d % 7, hazard);
            Rect(t, 4, 0, 3, 10, dark); Rect(t, 25, 0, 3, 10, dark);
            Apply(t);
            return Finish(t, "construction_fence");
        }

        public static Sprite CustomsBoard()
        {
            Sprite cached;
            if (TryGetCached("customs_board", out cached)) return cached;
            Texture2D t = NewTex(42, 38);
            Color wood = RGB(0x75, 0x50, 0x32);
            Rect(t, 4, 8, 34, 27, wood); Frame(t, 4, 8, 34, 27, RGB(0x4d, 0x36, 0x27));
            Rect(t, 8, 12, 26, 18, RGB(0x2f, 0x4d, 0x47));
            HLine(t, 27, 11, 30, RGB(0xcf, 0xc6, 0x9a)); HLine(t, 22, 11, 27, RGB(0xcf, 0xc6, 0x9a));
            Rect(t, 8, 0, 4, 10, wood); Rect(t, 30, 0, 4, 10, wood);
            Apply(t);
            return Finish(t, "customs_board");
        }

        public static Sprite PortFlag(bool red)
        {
            string key = "port_flag_" + (red ? "red" : "teal");
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(24, 38);
            Rect(t, 3, 0, 3, 38, RGB(0x4f, 0x45, 0x3b));
            Color cloth = red ? RGB(0xc4, 0x3d, 0x36) : RGB(0x37, 0x82, 0x78);
            Rect(t, 6, 25, 15, 10, cloth); Rect(t, 18, 26, 4, 8, cloth);
            HLine(t, 33, 7, 17, Shade(cloth, 1.18f));
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite HarborFog()
        {
            Sprite cached;
            if (TryGetCached("harbor_fog", out cached)) return cached;
            Texture2D t = NewTex(64, 40);
            Color mistA = new Color32(0xd2, 0xdb, 0xda, 38);
            Color mistB = new Color32(0xe0, 0xe6, 0xe4, 52);
            // 三条错开的破碎雾带。用空洞和渐短边缘代替整块半透明矩形。
            int[] starts = { 2, 9, 20 };
            int[] ends = { 61, 56, 53 };
            int[] bases = { 8, 18, 27 };
            int[] heights = { 7, 6, 4 };
            for (int band = 0; band < starts.Length; band++)
                for (int x = starts[band]; x <= ends[band]; x++)
                {
                    int edge = Mathf.Min(x - starts[band], ends[band] - x);
                    int columnHeight = Mathf.Min(heights[band], 2 + edge / 3);
                    for (int y = 0; y < columnHeight; y++)
                    {
                        int hash = x * 17 + y * 11 + band * 23;
                        if (hash % 5 == 0 || (edge < 3 && hash % 2 == 0)) continue;
                        P(t, x, bases[band] + y + ((x / 7 + band) & 1),
                            hash % 3 == 0 ? mistB : mistA);
                    }
                }
            Apply(t);
            return Finish(t, "harbor_fog");
        }

        /// <summary>V0.7.1 冬季屋顶积雪 overlay：按建筑种类的雪线登记表沿各自屋脊描白，
        /// 其余全透明——作为建筑子 SpriteRenderer 叠放天然对齐，不重生成建筑贴图、不动交互注册。</summary>
        public static Sprite SnowCap(string kind)
        {
            EnsureBuildingTables(kind);
            string key = "snow_cap_" + kind;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(BuildingSpecs.CanvasW(kind), BuildingSpecs.CanvasH(kind));
            System.Random r = new System.Random(FnvSeed("snow:" + kind));
            foreach (SnowBand b in snowTable[kind])
            {
                Rect(t, b.x0, b.topY, b.x1 - b.x0 + 1, 3, RGB(0xf4, 0xf8, 0xfc));
                for (int x = b.x0; x <= b.x1; x += 3)                       // 雪缘垂坠碎雪
                {
                    int depth = r.Next(0, 3);
                    for (int d = 1; d <= depth; d++) P(t, x, b.topY - d, RGB(0xea, 0xf0, 0xf6));
                }
                HLine(t, b.topY - 1, b.x0, b.x1, new Color32(0xdd, 0xe6, 0xee, 160));
            }
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite Lamp()
        {
            Sprite cached;
            if (TryGetCached("lamp", out cached)) return cached;
            Texture2D t = NewTex(8, 24);
            Rect(t, 3, 0, 2, 17, RGB(0x4a, 0x4a, 0x52));
            Rect(t, 2, 17, 4, 2, RGB(0x3a, 0x3a, 0x42));
            Rect(t, 2, 19, 4, 4, RGB(0xff, 0xe9, 0xa8));           // 灯
            Frame(t, 2, 19, 4, 4, RGB(0x4a, 0x4a, 0x52));
            Rect(t, 1, 0, 6, 2, RGB(0x3a, 0x3a, 0x42));
            Apply(t);
            return Finish(t, "lamp");
        }

        // ————— V0.7.1 天气粒子 & 节日装饰 —————

        /// <summary>4×4 世界像素天气微粒(kind: 0雨滴 / 1雪花 / 2秋叶),World 尺寸 = 4px÷PPU16 = 0.25 格。</summary>
        public static Sprite WeatherMote(int kind)
        {
            string key = "mote_" + kind;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(4, 4);
            switch (kind)
            {
                case 0: Rect(t, 1, 0, 2, 4, new Color32(0xa8, 0xc0, 0xdc, 210)); break;   // 雨滴(竖长)
                case 1: Rect(t, 0, 0, 4, 4, new Color32(0xf2, 0xf6, 0xfc, 230)); break;   // 雪花
                default:
                    Rect(t, 0, 1, 3, 3, new Color32(0xd8, 0x9a, 0x4a, 220));              // 秋叶
                    P(t, 3, 1, new Color32(0xb8, 0x7a, 0x36, 200)); break;
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>小镇节日彩旗灯笼串(night=false 日态 / true 夜态加灯笼亮黄点)。</summary>
        public static Sprite FestivalBunting(bool night)
        {
            string key = "festival_bunting" + (night ? "_n" : "");
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(48, 14);
            HLine(t, 12, 0, 47, RGB(0xd8, 0xd0, 0xbc));                      // 绳
            for (int x = 1; x < 46; x += 6)                                   // 三角旗交替三色
            {
                Color c = (x / 6) % 3 == 0 ? RGB(0xe2, 0x6a, 0x5a) : (x / 6) % 3 == 1 ? RGB(0xf2, 0xc4, 0x4a) : RGB(0x5a, 0xa8, 0x92);
                for (int d = 0; d < 5; d++)
                    Rect(t, x + d / 3, 11 - d * 2 - d % 2, 4 - d / 2, 2, c);
            }
            Rect(t, 20, 1, 8, 5, night ? RGB(0xff, 0xe9, 0xa8) : RGB(0xe8, 0xca, 0x76));   // 灯笼
            Frame(t, 19, 0, 10, 7, RGB(0xb8, 0x44, 0x34));
            P(t, 23, 13, RGB(0xb8, 0x44, 0x34)); P(t, 24, 13, RGB(0xb8, 0x44, 0x34));
            if (night)
            {
                P(t, 21, 2, RGB(0xff, 0xf6, 0xd0)); P(t, 26, 3, RGB(0xff, 0xf6, 0xd0));    // 夜态高光
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>V0.7.1 橱窗暖黄窗光 overlay：与 Building 同画布（尺寸走 BuildingSpecs 规格）、
        /// 按各建筑的「可亮窗矩形」登记表画发光像素,其余全透明——登记表由建筑绘制过程写入,窗位天然对齐。</summary>
        public static Sprite WindowGlow(string kind)
        {
            EnsureBuildingTables(kind);
            string key = "winglow_" + kind;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(BuildingSpecs.CanvasW(kind), BuildingSpecs.CanvasH(kind));
            Color warm = new Color32(0xff, 0xdc, 0x92, 235);
            Color core = new Color32(0xff, 0xf6, 0xd2, 245);
            Color frameWarm = new Color32(0xc8, 0x9a, 0x56, 200);
            foreach (GlowRect w in glowTable[kind])
            {
                if (w.w <= 0 || w.h <= 0) continue;
                Rect(t, w.x, w.y, w.w, w.h, warm);
                Frame(t, w.x, w.y, w.w, w.h, frameWarm);
                int cx = Mathf.Min(w.x + 2, w.x + w.w - 2);
                int cy = Mathf.Max(w.y + 1, w.y + w.h - 3);
                P(t, cx, cy, core); P(t, cx + 1, cy, core);
                P(t, cx, cy - 1 > w.y ? cy - 1 : cy, core);
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>V0.7.1 墙面小木牌底板:「转让」「营业时间调整」等世界空间文字叠 TextMesh 显示。</summary>
        public static Sprite WoodenSign()
        {
            Sprite cached;
            if (TryGetCached("wooden_sign", out cached)) return cached;
            Texture2D t = NewTex(38, 30);
            Rect(t, 2, 8, 34, 18, RGB(0xb0, 0x88, 0x58));
            Frame(t, 2, 8, 34, 18, RGB(0x7a, 0x58, 0x36));
            HLine(t, 15, 3, 34, RGB(0x93, 0x6e, 0x45));
            VLine(t, 6, 0, 8, RGB(0x5e, 0x42, 0x28)); VLine(t, 31, 0, 8, RGB(0x5e, 0x42, 0x28));
            Apply(t);
            return Finish(t, "wooden_sign");
        }

        /// <summary>V0.7.1 过热负信号:垃圾桶旁的小堆积杂物(纸箱+瓶罐)。</summary>
        public static Sprite ClutterPile()
        {
            Sprite cached;
            if (TryGetCached("clutter_pile", out cached)) return cached;
            Texture2D t = NewTex(18, 12);
            Rect(t, 1, 0, 7, 6, RGB(0xa8, 0x7c, 0x4e));                      // 倒下的纸箱
            Frame(t, 1, 0, 7, 6, RGB(0x7a, 0x58, 0x36));
            Rect(t, 9, 0, 4, 4, RGB(0xc8, 0xd0, 0xa8));                      // 塑料筐
            P(t, 5, 6, RGB(0x5a, 0x94, 0x46)); P(t, 6, 6, RGB(0x5a, 0x94, 0x46));   // 果皮
            P(t, 10, 4, RGB(0x8a, 0xb8, 0xd0)); Rect(t, 12, 2, 2, 3, RGB(0xd8, 0xb0, 0x58));  // 瓶罐
            Apply(t);
            return Finish(t, "clutter_pile");
        }

        /// <summary>仅供测试(PRD §119-4):贴图缓存条目监控。</summary>
        public static int CacheCountForTest { get { return cache.Count; } }

        public static Sprite Crate()
        {
            Sprite cached;
            if (TryGetCached("crate", out cached)) return cached;
            Texture2D t = NewTex(12, 12);
            Rect(t, 0, 0, 12, 12, RGB(0xa8, 0x7c, 0x4e));
            Frame(t, 0, 0, 12, 12, RGB(0x7a, 0x58, 0x36));
            HLine(t, 6, 1, 10, RGB(0x8a, 0x64, 0x3e));
            for (int i = 1; i < 11; i++) { P(t, i, i, RGB(0x8a, 0x64, 0x3e)); P(t, 11 - i, i, RGB(0x8a, 0x64, 0x3e)); }
            Apply(t);
            return Finish(t, "crate");
        }

        /// <summary>建筑落地影子。宽度按 BuildingSpecs.shadowWidth 三档（64/88/112）烘焙，
        /// key 与历史值保持一致（64 档沿用 "building_shadow"，其余加宽度后缀）。</summary>
        public static Sprite BuildingShadow(int width = 64)
        {
            string key = width == 64 ? "building_shadow" : "building_shadow_" + width;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(width, 18);
            Color outer = new Color32(0x22, 0x32, 0x25, 34);
            Color inner = new Color32(0x22, 0x32, 0x25, 58);
            int margin = 4, span = width - margin * 2;
            for (int y = 2; y < 15; y++)
            {
                int inset = Mathf.Abs(8 - y) / 2;
                Rect(t, margin + inset, y, span - inset * 2, 1, y > 4 && y < 12 ? inner : outer);
            }
            Apply(t);
            return Finish(t, key);
        }

        public static Sprite CharacterShadow()
        {
            Sprite cached;
            if (TryGetCached("character_shadow", out cached)) return cached;
            Texture2D t = NewTex(14, 6);
            Color outer = new Color32(0x20, 0x30, 0x22, 38);
            Color inner = new Color32(0x20, 0x30, 0x22, 62);
            Rect(t, 2, 1, 10, 4, outer);
            Rect(t, 4, 1, 6, 4, inner);
            Rect(t, 1, 2, 12, 2, outer);
            Apply(t);
            return Finish(t, "character_shadow");
        }

        public static Sprite Fountain()
        {
            Sprite cached;
            if (TryGetCached("fountain", out cached)) return cached;
            Texture2D t = NewTex(56, 58);
            Color shadow = new Color32(0x29, 0x3d, 0x31, 52);
            Color outline = RGB(0x5f, 0x64, 0x62);
            Color stoneDark = RGB(0x83, 0x86, 0x80);
            Color stone = RGB(0xbb, 0xbb, 0xad);
            Color stoneWarm = RGB(0xd1, 0xc9, 0xae);
            Color stoneLight = RGB(0xe8, 0xe1, 0xc6);
            Color waterDark = RGB(0x35, 0x82, 0x9e);
            Color water = RGB(0x58, 0xb8, 0xcb);
            Color waterLight = RGB(0xaf, 0xed, 0xe9);
            Color foam = RGB(0xe9, 0xff, 0xfa);

            // 宽阔的阶梯式投影和石座，让喷泉真正“坐”在市政广场上。
            Rect(t, 8, 1, 40, 2, shadow);
            Rect(t, 4, 2, 48, 2, shadow);
            Rect(t, 2, 3, 52, 2, shadow);
            Rect(t, 8, 5, 40, 3, outline);
            Rect(t, 5, 7, 46, 3, outline);
            Rect(t, 3, 9, 50, 6, outline);
            Rect(t, 5, 10, 46, 4, stoneDark);
            Rect(t, 7, 11, 42, 3, stoneWarm);
            HLine(t, 13, 9, 46, stoneLight);

            // 主水盆用冷暖两组石色做出厚重包边，水面是明确的椭圆层次。
            Rect(t, 2, 14, 52, 6, outline);
            Rect(t, 4, 15, 48, 5, stone);
            Rect(t, 6, 16, 44, 4, waterDark);
            Rect(t, 8, 17, 40, 3, water);
            HLine(t, 19, 10, 45, waterLight);
            HLine(t, 18, 13, 20, foam);
            HLine(t, 18, 35, 42, foam);
            P(t, 7, 16, stoneLight); P(t, 48, 16, stoneLight);

            // 下层柱座与中层水盆：不再是一根细棍，而是可辨识的古典比例。
            Rect(t, 20, 18, 16, 4, outline);
            Rect(t, 22, 19, 12, 4, stoneDark);
            Rect(t, 23, 21, 10, 13, outline);
            Rect(t, 24, 22, 8, 12, stoneDark);
            Rect(t, 25, 22, 6, 12, stone);
            VLine(t, 26, 23, 32, stoneLight);

            Rect(t, 15, 32, 26, 7, outline);
            Rect(t, 17, 33, 22, 5, stoneDark);
            Rect(t, 18, 34, 20, 4, stoneWarm);
            Rect(t, 20, 36, 16, 3, waterDark);
            HLine(t, 38, 22, 33, waterLight);
            Rect(t, 21, 31, 14, 2, stoneDark);

            // 上层小盆、花苞式喷口与中央水柱。
            Rect(t, 25, 38, 6, 9, outline);
            Rect(t, 26, 39, 4, 8, stone);
            VLine(t, 27, 40, 45, stoneLight);
            Rect(t, 20, 45, 16, 5, outline);
            Rect(t, 22, 46, 12, 4, stoneWarm);
            Rect(t, 24, 48, 8, 2, waterDark);
            Rect(t, 26, 49, 4, 4, outline);
            Rect(t, 27, 50, 2, 3, stoneLight);
            VLine(t, 27, 53, 56, waterLight);
            VLine(t, 28, 54, 56, foam);

            // 上盆落水。
            P(t, 22, 48, waterLight); P(t, 21, 47, waterLight); P(t, 20, 45, water);
            P(t, 19, 43, water); P(t, 19, 41, waterLight); P(t, 20, 39, foam);
            P(t, 33, 48, waterLight); P(t, 34, 47, waterLight); P(t, 35, 45, water);
            P(t, 36, 43, water); P(t, 36, 41, waterLight); P(t, 35, 39, foam);

            // 中盆向主池的两组对称水帘，用断续高光保留像素感。
            P(t, 17, 36, waterLight); P(t, 16, 35, waterLight); P(t, 15, 34, water);
            P(t, 14, 32, water); P(t, 13, 30, waterLight); P(t, 12, 28, water);
            P(t, 11, 26, water); P(t, 10, 24, waterLight); P(t, 10, 22, water); P(t, 11, 20, foam);
            P(t, 38, 36, waterLight); P(t, 39, 35, waterLight); P(t, 40, 34, water);
            P(t, 41, 32, water); P(t, 42, 30, waterLight); P(t, 43, 28, water);
            P(t, 44, 26, water); P(t, 45, 24, waterLight); P(t, 45, 22, water); P(t, 44, 20, foam);

            // 水面反光与落水泡沫。
            HLine(t, 17, 21, 26, waterLight);
            HLine(t, 17, 29, 34, waterLight);
            P(t, 9, 18, foam); P(t, 12, 19, waterLight); P(t, 43, 19, waterLight); P(t, 46, 18, foam);
            Apply(t);
            return Finish(t, "fountain");
        }

        public static Sprite NoticeBoard()
        {
            Sprite cached;
            if (TryGetCached("notice_board", out cached)) return cached;
            Texture2D t = NewTex(28, 30);
            Color wood = RGB(0x8e, 0x5d, 0x36);
            Color woodDark = RGB(0x5f, 0x3d, 0x27);
            Rect(t, 5, 7, 18, 17, wood);
            Frame(t, 5, 7, 18, 17, woodDark);
            Rect(t, 8, 10, 12, 10, RGB(0xf0, 0xdc, 0xb0));
            Rect(t, 9, 17, 8, 1, RGB(0xc9, 0x8a, 0x58));
            Rect(t, 9, 14, 10, 1, RGB(0x7a, 0x68, 0x54));
            Rect(t, 9, 11, 7, 1, RGB(0x7a, 0x68, 0x54));
            Rect(t, 7, 23, 14, 3, woodDark);
            Rect(t, 8, 0, 3, 8, woodDark);
            Rect(t, 18, 0, 3, 8, woodDark);
            Apply(t);
            return Finish(t, "notice_board");
        }

        // ————— 建筑(V0.7.2 起持续扩充：每个 kind 使用专属剪影，告别火柴盒同质化)—————
        //
        // 结构约定(PRD §119 呈现层纪律的延伸):
        // - 全部 64×56 画布、底边贴地一致,位置/排序/碰撞完全不受影响;
        // - 每栋 painter 同时登记「可亮窗矩形」与「雪线」到登记表,
        //   WindowGlow/SnowCap 只读登记表取几何——overlay 与贴图永不错位。

        /// <summary>夜态发光窗矩形(贴图坐标,含 1px 内缩)。</summary>
        public struct GlowRect
        {
            public int x, y, w, h;
            public GlowRect(int gx, int gy, int gw, int gh) { x = gx; y = gy; w = gw; h = gh; }
        }

        /// <summary>冬季积雪条:x0..x1 横跨。注意实际渲染方向(SnowCap):雪体自 topY <b>向上</b>覆 3px
        /// (即 topY 应取屋面行的上沿或屋面行本身,雪堆凸出屋面之上),另有垂坠碎雪向下探至 topY-3、
        /// 半透明阴影线在 topY-1——登记带时「向下覆 3px」的旧认知已废(围猎三#22/#23 复审改写)。</summary>
        public struct SnowBand
        {
            public int x0, x1, topY;
            public SnowBand(int ax, int bx, int ay) { x0 = ax; x1 = bx; topY = ay; }
        }

        static readonly Dictionary<string, GlowRect[]> glowTable = new Dictionary<string, GlowRect[]>();
        static readonly Dictionary<string, SnowBand[]> snowTable = new Dictionary<string, SnowBand[]>();

        public static Sprite Building(string kind)
        {
            EnsureBuildingTables(kind);
            string key = "bld_" + kind;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            return Finish(BakeBuilding(kind), key);
        }

        /// <summary>某栋的可亮窗矩形(测试与排障入口)。</summary>
        public static GlowRect[] BuildingWindows(string kind)
        {
            EnsureBuildingTables(kind);
            return glowTable.TryGetValue(kind, out var arr) ? arr : new GlowRect[0];
        }

        /// <summary>某栋的雪线(V0.7.1 冬季验收口径)。</summary>
        public static SnowBand[] BuildingSnowLines(string kind)
        {
            EnsureBuildingTables(kind);
            return snowTable.TryGetValue(kind, out var arr) ? arr : new SnowBand[0];
        }

        static void EnsureBuildingTables(string kind)
        {
            if (glowTable.ContainsKey(kind) && snowTable.ContainsKey(kind)) return;
            BakeBuilding(kind);
        }

        static Texture2D BakeBuilding(string kind)
        {
            Texture2D t = NewTex(BuildingSpecs.CanvasW(kind), BuildingSpecs.CanvasH(kind));
            List<GlowRect> g = new List<GlowRect>();
            List<SnowBand> s = new List<SnowBand>();
            switch (kind)
            {
                case "shop": PaintShopfront(t, g, s); break;
                case "competitor": PaintTeahouse(t, g, s); break;
                case "market": PaintMarketStall(t, g, s); break;
                case "bank": PaintClassicalBank(t, g, s); break;
                case "centralbank": PaintCentralBankTower(t, g, s); break;
                case "port": PaintHarborWarehouse(t, g, s); break;
                case "hall": PaintTownHall(t, g, s); break;
                case "training": PaintSchoolhouse(t, g, s); break;
                case "school": PaintCommunitySchool(t, g, s); break;
                case "factory": PaintFactory(t, g, s); break;
                case "restaurant": PaintChaletRestaurant(t, g, s); break;
                case "cinema": PaintMoviePalace(t, g, s); break;
                case "convenience": PaintCornerStore(t, g, s); break;
                case "decor_a": PaintParasolKiosk(t, g, s); break;
                case "decor_b": PaintFlowerCottage(t, g, s); break;
                case "bookstore": PaintBookstore(t, g, s); break;
                case "empty_a": PaintEmptyShop(t, g, s, 0); break;
                case "empty_b": PaintEmptyShop(t, g, s, 1); break;
                default: PaintHome(t, g, s); break;
            }
            Apply(t);
            glowTable[kind] = g.ToArray();
            snowTable[kind] = s.ToArray();
            return t;
        }

        static int FnvSeed(string s)
        {
            unchecked
            {
                int h = (int)2166136261u;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                return h;
            }
        }

        /// <summary>坡屋顶(带挑檐):檐口最宽,线性收窄到屋脊;snow 非空时坡面每 3 行登记雪线。</summary>
        static void GableRoof(Texture2D t, int x0, int x1, int yBase, int yApex, Color c, List<SnowBand> snow)
        {
            int cx = (x0 + x1) / 2;
            int rows = Mathf.Max(1, yApex - yBase);
            Color edge = Shade(c, 0.72f);
            for (int y = yBase; y <= yApex; y++)
            {
                float k = (float)(y - yBase) / rows;
                int w = Mathf.Max(2, Mathf.RoundToInt((x1 - x0 + 1) * (1f - k)));
                int lx = cx - w / 2;
                HLine(t, y, lx, lx + w - 1, c);
                P(t, lx, y, edge); P(t, lx + w - 1, y, edge);
                if (((y - yBase) % 3 == 0 || y == yApex) && w > 4 && snow != null)
                    snow.Add(new SnowBand(lx + 1, lx + w - 2, y));
            }
            HLine(t, yApex, cx - 1, cx + (x1 - x0 + 1) % 2, Shade(c, 1.14f));       // 脊线高光
        }

        /// <summary>条纹遮阳篷(波浪垂角+底口阴影)。</summary>
        static void Awning(Texture2D t, int x0, int x1, int yTop, int h, Color a, Color b)
        {
            for (int x = x0; x <= x1; x++)
            {
                Color cc = ((x - x0) / 4) % 2 == 0 ? a : b;
                VLine(t, x, yTop - h + 1, yTop, cc);
                if ((x - x0) % 8 == 3) P(t, x, yTop - h, Shade(a, 0.85f));
            }
            HLine(t, yTop + 1, x0, x1, Shade(a, 0.7f));
        }

        /// <summary>玻璃窗:画框填色并登记夜光矩形(内缩 1px 保住日间窗框)。</summary>
        static void FramedGlass(Texture2D t, int x, int y, int w, int h, Color glass, List<GlowRect> glow)
        {
            Rect(t, x, y, w, h, glass);
            Frame(t, x, y, w, h, Shade(glass, 0.5f));
            if (w > 2 && h > 2 && glow != null) glow.Add(new GlowRect(x + 1, y + 1, w - 2, h - 2));
        }

        // ————— 我的家:奶油小屋,三角坡顶+砖烟囱+花箱 —————

        static void PaintHome(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color wall = RGB(0xEF, 0xDC, 0xBE), timber = RGB(0x7A, 0x54, 0x36), brick = RGB(0x9A, 0x66, 0x50);
            Rect(t, 16, 0, 32, 24, wall); Frame(t, 16, 0, 32, 24, Shade(wall, 0.78f));
            for (int x = 18; x < 47; x += 5) P(t, x, 1, Shade(wall, 0.88f));
            GableRoof(t, 10, 54, 23, 40, RGB(0xA8, 0x5A, 0x44), s);

            Rect(t, 44, 29, 6, 11, brick); Frame(t, 44, 29, 6, 11, Shade(brick, 0.7f));   // 烟囱穿出右坡
            Rect(t, 43, 40, 8, 2, Shade(brick, 0.72f));
            P(t, 48, 45, new Color32(0xd4, 0xd4, 0xd4, 190)); P(t, 50, 48, new Color32(0xe2, 0xe2, 0xe2, 165));

            Rect(t, 20, 0, 9, 14, timber); Frame(t, 20, 0, 9, 14, Shade(timber, 0.62f));
            P(t, 27, 7, RGB(0xE2, 0xC8, 0x8A));
            FramedGlass(t, 30, 10, 8, 10, RGB(0x54, 0x74, 0x8E), g);
            FramedGlass(t, 40, 10, 8, 10, RGB(0x54, 0x74, 0x8E), g);
            Rect(t, 39, 8, 10, 2, timber);                                                // 花箱
            P(t, 41, 10, RGB(0xE2, 0x7A, 0xB8)); P(t, 43, 10, RGB(0xF2, 0xD4, 0x5A)); P(t, 46, 10, RGB(0xE8, 0x8A, 0x4A));

            // 围猎三#23:阁楼圆窗(y30-34)整扇落在 GableRoof 的自动雪带范围内——
            // SnowCap 雪体自主雪行向上覆 3px、垂坠/阴影线向下探 3px,触及窗即 topY∈[28,37];
            // 登记窗前把这些雪带收缩到窗两侧,冬季窗不再被白雪盖没。
            for (int i = s.Count - 1; i >= 0; i--)
            {
                SnowBand b = s[i];
                if (b.topY < 28 || b.topY > 37) continue;
                if (b.x1 < 29 || b.x0 > 35) continue;
                s.RemoveAt(i);
                if (b.x0 <= 28) s.Add(new SnowBand(b.x0, 28, b.topY));
                if (b.x1 >= 36) s.Add(new SnowBand(36, b.x1, b.topY));
            }
            Rect(t, 30, 30, 5, 5, RGB(0x54, 0x74, 0x8E)); Frame(t, 30, 30, 5, 5, Shade(wall, 0.6f));
            g.Add(new GlowRect(31, 31, 3, 3));                                            // 圆形阁楼窗
        }

        // ————— 果汁店:平檐店面——女儿墙招牌带+条纹雨棚+大橱窗 —————

        static void PaintShopfront(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color wall = RGB(0xE8, 0xC4, 0x9A), terra = RGB(0xC4, 0x54, 0x44), timber = RGB(0x6A, 0x44, 0x28);
            Rect(t, 8, 46, 48, 3, Shade(terra, 0.8f));
            Rect(t, 10, 38, 44, 8, terra); Frame(t, 10, 38, 44, 8, Shade(terra, 0.62f));
            Rect(t, 15, 39, 25, 6, RGB(0xF7, 0xEE, 0xD4)); Frame(t, 15, 39, 25, 6, RGB(0x8A, 0x5A, 0x3A));
            Rect(t, 18, 40, 3, 3, RGB(0xF2, 0xA8, 0x4A)); P(t, 19, 43, RGB(0xE8, 0x8A, 0x3A));   // 果汁杯
            VLine(t, 22, 40, 42, RGB(0xD9, 0x5A, 0x4A));
            HLine(t, 42, 26, 36, RGB(0x8A, 0x5A, 0x3A)); HLine(t, 40, 26, 33, RGB(0xC9, 0xA0, 0x74)); // 店名字迹

            Rect(t, 12, 0, 40, 38, wall); Frame(t, 12, 0, 40, 38, Shade(wall, 0.78f));
            for (int x = 14; x < 51; x += 6) P(t, x, 1, Shade(wall, 0.9f));

            Awning(t, 9, 43, 35, 5, RGB(0xD9, 0x5A, 0x4A), RGB(0xF2, 0xEE, 0xE2));
            FramedGlass(t, 12, 10, 24, 20, RGB(0x54, 0x74, 0x8E), g);
            VLine(t, 23, 11, 28, Shade(RGB(0x54, 0x74, 0x8E), 0.55f));
            HLine(t, 19, 13, 34, Shade(RGB(0x54, 0x74, 0x8E), 0.55f));
            P(t, 16, 13, RGB(0x8A, 0xB8, 0x5A)); Rect(t, 20, 12, 2, 2, RGB(0xE0, 0x9a, 0x3e));
            P(t, 30, 13, RGB(0xC0, 0x5A, 0x4A)); P(t, 33, 14, RGB(0x67, 0x9f, 0xc0));            // 橱窗商品剪影

            Rect(t, 41, 0, 11, 16, timber); Frame(t, 41, 0, 11, 16, Shade(timber, 0.62f));
            P(t, 42, 7, RGB(0xE2, 0xC8, 0x8A));
            FramedGlass(t, 43, 17, 7, 4, RGB(0x54, 0x74, 0x8E), g);
            Rect(t, 42, 21, 9, 4, RGB(0xF2, 0xEE, 0xE2)); Frame(t, 42, 21, 9, 4, timber);
            P(t, 44, 23, RGB(0x4F, 0x9B, 0x70)); P(t, 47, 23, RGB(0x4F, 0x9B, 0x70));            // 「营业中」灯点

            s.Add(new SnowBand(8, 55, 49));
        }

        // ————— SUNNY TEA:三层叠檐茶馆,檐角起翘+红灯笼 —————

        static void PaintTeahouse(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color cream = RGB(0xE8, 0xE0, 0xCC), teal = RGB(0x2F, 0x6F, 0x68), tealL = RGB(0x3E, 0x88, 0x83);
            Color gold = RGB(0xE8, 0xB8, 0x4A), red = RGB(0xC0, 0x3A, 0x2E), oak = RGB(0x5A, 0x3A, 0x24);
            Rect(t, 12, 0, 40, 32, cream); Frame(t, 12, 0, 40, 32, Shade(cream, 0.76f));

            Rect(t, 16, 14, 8, 9, RGB(0xEA, 0xDA, 0xAE)); Frame(t, 16, 14, 8, 9, oak);          // 和纸格窗
            VLine(t, 19, 15, 22, Shade(oak, 0.9f)); HLine(t, 18, 17, 22, Shade(oak, 0.9f));
            Rect(t, 40, 14, 8, 9, RGB(0xEA, 0xDA, 0xAE)); Frame(t, 40, 14, 8, 9, oak);
            VLine(t, 43, 15, 22, Shade(oak, 0.9f)); HLine(t, 18, 41, 46, Shade(oak, 0.9f));
            g.Add(new GlowRect(17, 15, 6, 7)); g.Add(new GlowRect(41, 15, 6, 7));

            Rect(t, 27, 0, 10, 18, oak); Frame(t, 27, 0, 10, 18, Shade(oak, 0.6f));              // 双开门
            VLine(t, 32, 1, 16, Shade(oak, 0.7f)); P(t, 30, 8, gold); P(t, 33, 8, gold);
            Rect(t, 27, 20, 10, 6, RGB(0xE9, 0xD9, 0xA8)); Frame(t, 27, 20, 10, 6, oak);
            VLine(t, 32, 21, 24, Shade(oak, 0.9f)); HLine(t, 22, 28, 35, Shade(oak, 0.9f));
            g.Add(new GlowRect(28, 21, 8, 4));

            Rect(t, 5, 32, 54, 6, teal); Frame(t, 5, 32, 54, 6, Shade(teal, 0.7f));              // 下层大檐
            P(t, 4, 38, Shade(teal, 0.6f)); P(t, 3, 39, Shade(teal, 0.6f));                      // 檐角起翘
            P(t, 59, 38, Shade(teal, 0.6f)); P(t, 60, 39, Shade(teal, 0.6f));

            Rect(t, 8, 25, 5, 6, red); Frame(t, 8, 25, 5, 6, Shade(red, 0.7f));                  // 左灯笼
            VLine(t, 10, 30, 31, oak); P(t, 10, 24, gold);
            g.Add(new GlowRect(9, 26, 3, 4));
            Rect(t, 51, 25, 5, 6, red); Frame(t, 51, 25, 5, 6, Shade(red, 0.7f));                // 右灯笼
            VLine(t, 53, 30, 31, oak); P(t, 53, 24, gold);
            g.Add(new GlowRect(52, 26, 3, 4));

            Rect(t, 12, 38, 40, 6, tealL); Frame(t, 12, 38, 40, 6, Shade(tealL, 0.7f));
            Rect(t, 24, 44, 16, 5, teal); Frame(t, 24, 44, 16, 5, Shade(teal, 0.7f));
            P(t, 31, 49, gold); P(t, 32, 49, gold);

            s.Add(new SnowBand(5, 58, 38));
            s.Add(new SnowBand(12, 51, 44));
        }

        // ————— 原料市场:敞开式货摊——条纹凉棚+吊秤+果箱麻袋 —————

        static void PaintMarketStall(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color wood = RGB(0x7A, 0x54, 0x36), coral = RGB(0xE2, 0x6A, 0x4E), cream = RGB(0xF5, 0xEF, 0xE0);
            // V0.9.6 加高档 64×72:摊柱贯通到顶、凉棚上移 12px、棚上加市场招牌(台面/果箱/吊挂原样)
            Rect(t, 8, 0, 4, 70, wood); Rect(t, 52, 0, 4, 70, wood);                             // 摊柱(贯通)
            for (int y = 4; y < 70; y += 5) { P(t, 8, y, Shade(wood, 0.75f)); P(t, 52, y, Shade(wood, 0.75f)); }
            Rect(t, 7, 70, 6, 2, Shade(wood, 0.62f)); Rect(t, 51, 70, 6, 2, Shade(wood, 0.62f)); // 柱顶压帽

            for (int x = 4; x <= 59; x++)                                                        // 条纹凉棚
            {
                bool coralStripe = ((x - 4) / 4) % 2 == 0;
                VLine(t, x, 50, 57, coralStripe ? coral : cream);
                if ((x - 4) % 8 == 7) P(t, x, 49, Shade(coralStripe ? coral : cream, 0.8f));     // 波浪垂边
            }
            HLine(t, 58, 4, 59, RGB(0x8A, 0x3A, 0x28)); HLine(t, 50, 5, 58, Shade(coral, 0.7f));

            Rect(t, 13, 59, 38, 10, RGB(0x8A, 0x64, 0x3E));                                      // 棚顶市场招牌
            Frame(t, 13, 59, 38, 10, Shade(wood, 0.6f));
            Rect(t, 15, 61, 34, 6, RGB(0xEF, 0xE0, 0xC0));
            for (int i = 0; i < 4; i++)
            {
                int gx = 17 + i * 8;
                Rect(t, gx, 62, 5, 4, Shade(wood, 0.8f)); P(t, gx + 2, 63, RGB(0xEF, 0xE0, 0xC0));
            }

            Rect(t, 14, 18, 36, 14, RGB(0xF0, 0xE8, 0xD4));                                      // 亚麻底布
            Frame(t, 14, 18, 36, 14, Shade(RGB(0xF0, 0xE8, 0xD4), 0.85f));

            Rect(t, 10, 12, 44, 4, RGB(0x8A, 0x64, 0x3E));                                       // 台面
            Rect(t, 12, 4, 40, 8, RGB(0x6E, 0x4E, 0x30));
            HLine(t, 15, 11, 53, RGB(0xB0, 0x88, 0x58));

            Color[] fruits = { RGB(0xE2, 0x5A, 0x4A), RGB(0xF2, 0xA8, 0x4A), RGB(0x8A, 0xC4, 0x5A) };
            int[] bx = { 14, 27, 40 };                                                           // 果箱三连
            for (int i = 0; i < 3; i++)
            {
                Rect(t, bx[i], 15, 10, 6, RGB(0xA8, 0x7C, 0x4E)); Frame(t, bx[i], 15, 10, 6, RGB(0x7A, 0x58, 0x36));
                P(t, bx[i] + 2, 20, fruits[i]); P(t, bx[i] + 4, 20, fruits[i]);
                P(t, bx[i] + 6, 20, fruits[i]); P(t, bx[i] + 3, 19, fruits[i]);
            }

            VLine(t, 32, 34, 49, Shade(wood, 0.7f));                                             // 吊秤(绳随棚上移)
            Rect(t, 31, 31, 3, 3, RGB(0xD9, 0xA8, 0x3A));
            HLine(t, 30, 29, 35, RGB(0x9A, 0xA2, 0xAE)); P(t, 29, 29, RGB(0x9A, 0xA2, 0xAE)); P(t, 35, 29, RGB(0x9A, 0xA2, 0xAE));

            Rect(t, 18, 27, 12, 6, RGB(0xEF, 0xE0, 0xC0)); Frame(t, 18, 27, 12, 6, wood);        // 手写价牌
            HLine(t, 31, 20, 27, Shade(wood, 0.8f)); HLine(t, 29, 20, 25, Shade(wood, 0.8f));
            VLine(t, 19, 33, 49, Shade(wood, 0.7f)); VLine(t, 28, 33, 49, Shade(wood, 0.7f));

            Rect(t, 2, 0, 5, 6, RGB(0xD8, 0xB8, 0x88)); P(t, 4, 6, Shade(wood, 0.7f));           // 麻袋
            Rect(t, 57, 0, 5, 5, RGB(0xD8, 0xB8, 0x88)); P(t, 59, 5, Shade(wood, 0.7f));

            Rect(t, 44, 29, 5, 5, RGB(0xE9, 0xD9, 0xA8)); Frame(t, 44, 29, 5, 5, wood);          // 吊灯
            VLine(t, 46, 34, 49, Shade(wood, 0.7f));
            g.Add(new GlowRect(45, 30, 3, 3));

            s.Add(new SnowBand(5, 58, 58)); s.Add(new SnowBand(14, 49, 69));
        }

        // ————— 小镇银行(M 档 88×72):暖石材金融会馆 —————
        // 保持旧画布、墙脚、门点和碰撞不变；用“宽阶—凹入口—四柱—厚檐—镶边山花”建立近中远三层进深。
        // 它应明显比沿街商铺精致，但仍低于独立院落内的中央银行副中心。

        static void PaintClassicalBank(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color outline = RGB(0x56, 0x60, 0x69), shadow = RGB(0x7D, 0x82, 0x84);
            Color stoneDark = RGB(0xA9, 0xAA, 0xA3), stone = RGB(0xD7, 0xD2, 0xC4);
            Color stoneWarm = RGB(0xE5, 0xDC, 0xC6), stoneLight = RGB(0xF2, 0xEC, 0xDC);
            Color slate = RGB(0x48, 0x5D, 0x72), slateDark = RGB(0x31, 0x43, 0x55);
            Color bronze = RGB(0x80, 0x59, 0x32), bronzeLight = RGB(0xAA, 0x78, 0x3E);
            Color gold = RGB(0xD7, 0xA6, 0x3D), glass = RGB(0x43, 0x61, 0x79);
            Color glassLight = RGB(0x76, 0x98, 0xAC);

            // 通长五级台阶和深色接地线，最大轮廓保持在旧碰撞带内。
            Rect(t, 4, 0, 80, 2, outline);
            Rect(t, 6, 1, 76, 3, stoneDark);
            Rect(t, 9, 3, 70, 3, stone);
            Rect(t, 12, 5, 64, 3, stoneWarm);
            Rect(t, 15, 7, 58, 2, stoneLight);
            HLine(t, 8, 15, 72, shadow);

            // 主体先铺石墙与水平砌缝，再叠入口和窗，避免原先一整块灰色平面。
            Rect(t, 11, 9, 66, 35, outline);
            Rect(t, 13, 10, 62, 33, stone);
            HLine(t, 17, 14, 74, Shade(stone, .91f));
            HLine(t, 26, 14, 74, Shade(stone, .91f));
            HLine(t, 35, 14, 74, Shade(stone, .91f));
            VLine(t, 14, 11, 41, stoneLight); VLine(t, 73, 11, 41, shadow);

            // 两侧高窗：双层石套、深蓝玻璃和金属横档。
            Rect(t, 15, 15, 12, 21, stoneLight); Frame(t, 15, 15, 12, 21, shadow);
            FramedGlass(t, 17, 17, 8, 17, glass, g);
            HLine(t, 25, 18, 23, glassLight); HLine(t, 19, 18, 23, slateDark);
            Rect(t, 61, 15, 12, 21, stoneLight); Frame(t, 61, 15, 12, 21, shadow);
            FramedGlass(t, 63, 17, 8, 17, glass, g);
            HLine(t, 25, 64, 69, glassLight); HLine(t, 19, 64, 69, slateDark);

            // 中央入口向后凹一层；铜质双开门、玻璃气窗和门环都是独立层次。
            Rect(t, 29, 10, 30, 32, slateDark); Frame(t, 29, 10, 30, 32, outline);
            Rect(t, 32, 11, 24, 29, Shade(stoneWarm, .82f));
            Rect(t, 37, 9, 14, 25, bronze); Frame(t, 37, 9, 14, 25, Shade(bronze, .58f));
            VLine(t, 43, 10, 32, Shade(bronze, .62f)); VLine(t, 44, 10, 32, bronzeLight);
            Rect(t, 39, 24, 10, 8, glass); Frame(t, 39, 24, 10, 8, slateDark);
            HLine(t, 30, 40, 47, glassLight);
            g.Add(new GlowRect(40, 25, 8, 6));
            P(t, 41, 17, gold); P(t, 46, 17, gold);

            // 四根带柱础、柱头和内侧阴影的石柱，明确形成门廊进深。
            int[] colX = BankColumnX();
            for (int i = 0; i < colX.Length; i++)
            {
                int x = colX[i];
                Rect(t, x, 11, 4, 31, stoneLight);
                VLine(t, x, 12, 40, shadow);
                VLine(t, x + 2, 12, 40, stone);
                Rect(t, x - 1, 9, 6, 4, stoneDark);
                HLine(t, 12, x, x + 4, stoneLight);
                Rect(t, x - 1, 40, 6, 5, stoneWarm);
                HLine(t, 43, x - 2, x + 5, outline);
            }

            // 厚檐由暗到亮分三层；中间嵌金融徽带，不再是一排随意金币点。
            Rect(t, 8, 44, 72, 3, outline);
            Rect(t, 10, 47, 68, 6, stoneWarm);
            HLine(t, 48, 11, 76, stoneLight);
            for (int x = 13; x <= 74; x += 5) Rect(t, x, 47, 2, 2, shadow);
            Rect(t, 35, 49, 18, 3, slate);
            for (int x = 38; x <= 50; x += 4) P(t, x, 50, gold);
            Rect(t, 7, 53, 74, 4, slateDark);
            HLine(t, 56, 9, 78, slate);

            // 深蓝石板外缘包住暖石山花，增强材质对比和金融建筑的稳重感。
            GableRoof(t, 7, 80, 57, 69, slate, s);
            const int roofCenter = 43;
            const int roofRows = 12;
            for (int y = 58; y <= 67; y++)
            {
                float k = (float)(y - 57) / roofRows;
                int roofWidth = Mathf.Max(2, Mathf.RoundToInt(74f * (1f - k)));
                int left = roofCenter - roofWidth / 2;
                if (roofWidth > 8) HLine(t, y, left + 2, left + roofWidth - 3, stoneWarm);
            }

            // 山花中央使用盾牌式金币徽章，角部与屋脊加小型顶饰。
            Rect(t, 39, 59, 9, 7, gold); Frame(t, 39, 59, 9, 7, Shade(gold, .62f));
            Rect(t, 41, 61, 5, 3, slate); P(t, 43, 62, stoneLight);
            Rect(t, 6, 55, 5, 3, stoneDark); Rect(t, 77, 55, 5, 3, stoneDark);
            Rect(t, 41, 68, 5, 3, slateDark); P(t, 43, 71, gold);

            // 门廊壁灯是独立小光源；窗光仍由 WindowGlow 统一接管。
            Rect(t, 30, 28, 3, 4, bronze); P(t, 31, 29, gold);
            Rect(t, 55, 28, 3, 4, bronze); P(t, 56, 29, gold);
            g.Add(new GlowRect(30, 28, 3, 4)); g.Add(new GlowRect(55, 28, 3, 4));

            s.Add(new SnowBand(7, 80, 54));
        }

        // ————— 中央银行:钟塔穹顶,全场制高点 —————

        // ————— 中央银行(L 档 112×88,V0.8.1 地标):双翼+高塔+柱廊基座+大钟+穹顶,全场制高点 —————

        static void PaintCentralBankTower(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color wing = RGB(0xB7, 0xCC, 0xC4), tower = RGB(0xC9, 0xDE, 0xD6), dome = RGB(0x3F, 0x75, 0x70);
            Color gold = RGB(0xE8, 0xB8, 0x4A), deepWin = RGB(0x3D, 0x62, 0x70), pale = RGB(0xD9, 0xE8, 0xE2);

            Rect(t, 2, 0, 26, 26, wing); Frame(t, 2, 0, 26, 26, Shade(wing, 0.72f));           // 两翼
            Rect(t, 1, 26, 28, 4, Shade(dome, 0.9f));
            Rect(t, 84, 0, 26, 26, wing); Frame(t, 84, 0, 26, 26, Shade(wing, 0.72f));
            Rect(t, 83, 26, 28, 4, Shade(dome, 0.9f));
            FramedGlass(t, 6, 8, 7, 10, deepWin, g);
            FramedGlass(t, 17, 8, 7, 10, deepWin, g);
            FramedGlass(t, 88, 8, 7, 10, deepWin, g);
            FramedGlass(t, 99, 8, 7, 10, deepWin, g);

            Rect(t, 30, 0, 52, 64, tower);                                                     // 主塔
            VLine(t, 30, 0, 63, Shade(tower, 0.86f)); VLine(t, 31, 0, 63, Shade(tower, 0.9f));
            VLine(t, 80, 0, 63, Shade(tower, 0.9f)); VLine(t, 81, 0, 63, Shade(tower, 0.86f));
            HLine(t, 28, 30, 81, Shade(tower, 0.92f));

            int[] colX = { 33, 44, 67, 78 };                                                   // 柱廊基座(V0.8.1)
            for (int i = 0; i < 4; i++)
            {
                Rect(t, colX[i], 0, 4, 28, pale);
                VLine(t, colX[i] + 1, 1, 27, Shade(pale, 0.88f));
                Rect(t, colX[i] - 1, 28, 6, 2, Shade(tower, 0.9f));
            }

            Rect(t, 51, 0, 12, 20, RGB(0x2E, 0x5F, 0x58)); Frame(t, 51, 0, 12, 20, Shade(dome, 0.7f));
            Rect(t, 53, 2, 8, 9, RGB(0x2E, 0x4F, 0x55)); Frame(t, 53, 2, 8, 9, Shade(dome, 0.6f));
            g.Add(new GlowRect(54, 3, 6, 7));
            P(t, 56, 16, gold); P(t, 55, 17, gold); P(t, 57, 17, gold); P(t, 56, 18, gold);    // 门楣金星

            FramedGlass(t, 35, 33, 11, 12, deepWin, g);                                        // 塔身大窗对
            FramedGlass(t, 66, 33, 11, 12, deepWin, g);
            Rect(t, 34, 49, 4, 3, Shade(dome, 1.2f)); Rect(t, 74, 49, 4, 3, Shade(dome, 1.2f)); // 通风格栅

            Rect(t, 48, 48, 14, 13, RGB(0xF2, 0xF6, 0xEE));                                    // 大钟
            Frame(t, 48, 48, 14, 13, Shade(dome, 0.8f));
            P(t, 48, 48, tower); P(t, 61, 48, tower); P(t, 48, 60, tower); P(t, 61, 60, tower);
            VLine(t, 55, 51, 56, Shade(dome, 0.8f)); HLine(t, 55, 52, 57, Shade(dome, 0.8f));
            P(t, 55, 55, gold);
            g.Add(new GlowRect(49, 49, 12, 11));

            Rect(t, 28, 64, 56, 3, dome);                                                      // 檐口

            int[][] domeRows =                                                                   // 半圆穹顶
            {
                new[] { 34, 77 }, new[] { 35, 76 }, new[] { 37, 74 }, new[] { 38, 73 },
                new[] { 40, 71 }, new[] { 42, 69 }, new[] { 44, 67 }, new[] { 46, 65 },
                new[] { 48, 63 }, new[] { 50, 61 }, new[] { 52, 59 }, new[] { 54, 56 }
            };
            for (int i = 0; i < domeRows.Length; i++)
            {
                HLine(t, 68 + i, domeRows[i][0], domeRows[i][1], dome);
                P(t, domeRows[i][0], 68 + i, Shade(dome, 0.8f));
            }
            P(t, 55, 80, Shade(gold, 0.8f)); P(t, 55, 81, gold);                                // 顶饰金球

            s.Add(new SnowBand(1, 28, 27));
            s.Add(new SnowBand(83, 110, 27));
            s.Add(new SnowBand(28, 84, 65));
            s.Add(new SnowBand(36, 74, 69));
        }

        // ————— 国际贸易港:波纹板仓房+单坡顶+龙门吊吊装集装箱 —————

        static void PaintHarborWarehouse(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color wall = RGB(0xB9, 0xC8, 0xD8), slate = RGB(0x5A, 0x72, 0x88), steel = RGB(0x3E, 0x4A, 0x56);
            Color shut = RGB(0x93, 0xA0, 0xAE), orange = RGB(0xE0, 0x78, 0x30), cableC = RGB(0x9A, 0xA2, 0xAE);

            Rect(t, 4, 0, 52, 24, wall);                                                         // 波纹板仓房
            for (int x = 6; x < 55; x += 3) VLine(t, x, 2, 23, Shade(wall, 0.9f));
            Rect(t, 4, 0, 52, 2, Shade(wall, 0.8f));
            Rect(t, 4, 24, 52, 2, Shade(slate, 0.9f));                                           // 檐下板带

            for (int i = 0; i < 52; i++)                                                         // 单坡屋面向左抬升
            {
                int hgt = (6 * (51 - i) + 51) / 52;
                if (hgt <= 0) continue;
                int topRow = 25 + hgt - 1;
                VLine(t, 4 + i, 25, topRow, slate);
                P(t, 4 + i, topRow, Shade(slate, 1.18f));
            }
            int[] skyX = { 13, 28, 43 };                                                          // 屋面天窗凸条
            for (int n = 0; n < 3; n++)
            {
                int sx = skyX[n];
                int hgt = (6 * (51 - (sx - 4)) + 51) / 52;
                Rect(t, sx - 1, 25 + hgt, 4, 2, RGB(0xD8, 0xE2, 0xEA));
            }

            Rect(t, 12, 0, 18, 16, shut); Frame(t, 12, 0, 18, 16, Shade(slate, 0.8f));           // 大型滑门
            for (int y = 3; y < 15; y += 4) HLine(t, y, 13, 28, Shade(shut, 0.82f));
            for (int x = 13; x <= 28; x += 2) P(t, x, 2, (x / 2) % 2 == 0 ? RGB(0xE2, 0xC0, 0x3A) : steel);
            P(t, 16, 17, Shade(slate, 0.8f)); P(t, 25, 17, Shade(slate, 0.8f));                  // 吊轮

            Rect(t, 34, 0, 6, 12, steel); Frame(t, 34, 0, 6, 12, Shade(steel, 0.7f));            // 人行门
            int[] phX = { 6, 9, 41 };                                                             // 舷窗
            for (int i = 0; i < phX.Length; i++)
            {
                P(t, phX[i], 19, Shade(slate, 0.8f)); P(t, phX[i], 18, Shade(slate, 0.8f));
                P(t, phX[i] + 1, 19, Shade(slate, 0.8f)); P(t, phX[i] - 1, 19, Shade(slate, 0.8f));
                P(t, phX[i], 19, RGB(0x8F, 0xB8, 0xC4));
            }

            FramedGlass(t, 44, 12, 8, 6, RGB(0x5F, 0x7E, 0x7A), g);                              // 办公室窗
            g.Add(new GlowRect(35, 1, 4, 9));

            VLine(t, 50, 25, 49, steel); VLine(t, 54, 25, 49, steel);                            // 龙门吊桁架塔
            for (int y = 27; y < 49; y += 3) { P(t, 51, y, steel); P(t, 52, y, steel); P(t, 53, y, steel); }
            Rect(t, 48, 50, 8, 2, steel);
            Rect(t, 20, 46, 34, 2, steel);                                                        // 悬臂梁
            for (int x = 22; x <= 52; x += 3) P(t, x, 45, Shade(steel, 1.3f));
            Rect(t, 55, 43, 4, 4, steel);                                                         // 配重块
            VLine(t, 30, 44, 39, cableC);                                                         // 吊缆
            HLine(t, 38, 27, 32, steel);                                                          // 吊梁
            Rect(t, 26, 31, 9, 6, orange); Frame(t, 26, 31, 9, 6, Shade(orange, 0.55f));          // 集装箱
            for (int x = 28; x <= 32; x += 2) VLine(t, x, 32, 35, Shade(orange, 0.82f));
            VLine(t, 28, 37, 37, steel); VLine(t, 31, 37, 37, steel);
            P(t, 57, 52, RGB(0xD9, 0x5A, 0x4A)); P(t, 58, 52, RGB(0xD9, 0x5A, 0x4A)); P(t, 56, 52, steel); // 塔顶小旗

            s.Add(new SnowBand(5, 17, 30));
            s.Add(new SnowBand(21, 33, 28));
            s.Add(new SnowBand(37, 55, 26));
            s.Add(new SnowBand(20, 54, 48));
            s.Add(new SnowBand(48, 55, 52));
        }

        // ————— 市政厅(XL 128×96):全镇主地标 —————
        // 用“宽阶基座—双翼柱廊—中央山花—高钟塔”四层轮廓压过央行；
        // 门、落影与旧世界坐标保持不变，只升级主体体量和建筑语言。

        static void PaintTownHall(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color sand = RGB(0xDD, 0xC8, 0x9E), lightSand = RGB(0xEE, 0xDE, 0xBD);
            Color stone = RGB(0xC8, 0xC1, 0xAE), darkStone = RGB(0x87, 0x7C, 0x68);
            Color oak = RGB(0x65, 0x40, 0x27), roof = RGB(0x88, 0x59, 0x32);
            Color glass = RGB(0x4F, 0x70, 0x89), gold = RGB(0xD9, 0xA8, 0x3A);
            Color civicRed = RGB(0xB8, 0x46, 0x3E);

            // 宽阔的四级台阶与通长石基：先在地面上建立“公共建筑”的重量。
            Rect(t, 6, 0, 116, 2, darkStone);
            Rect(t, 10, 1, 108, 2, stone);
            Rect(t, 16, 3, 96, 2, Shade(stone, .95f));
            Rect(t, 24, 5, 80, 2, Shade(stone, 1.05f));
            Rect(t, 32, 7, 64, 2, lightSand);

            // 双翼主体+中央凸出的行政主楼。
            Rect(t, 8, 8, 38, 39, sand); Frame(t, 8, 8, 38, 39, Shade(sand, .72f));
            Rect(t, 82, 8, 38, 39, sand); Frame(t, 82, 8, 38, 39, Shade(sand, .72f));
            Rect(t, 40, 8, 48, 47, lightSand); Frame(t, 40, 8, 48, 47, Shade(sand, .68f));
            Rect(t, 8, 8, 112, 6, stone); HLine(t, 13, 9, 118, Shade(stone, .72f));             // 通长基座
            HLine(t, 31, 9, 45, Shade(sand, .83f)); HLine(t, 31, 82, 118, Shade(sand, .83f));  // 水平束带

            // 六根翼楼壁柱+四扇高窗，形成稳定的古典节奏。
            int[] pilasters = { 9, 24, 39, 84, 99, 114 };
            for (int i = 0; i < pilasters.Length; i++)
            {
                int x = pilasters[i];
                Rect(t, x, 11, 3, 32, Shade(stone, 1.06f));
                VLine(t, x, 12, 41, Shade(stone, .78f));
                Rect(t, x - 1, 10, 5, 3, stone);
                Rect(t, x - 1, 40, 5, 4, lightSand);
            }
            FramedGlass(t, 14, 19, 9, 15, glass, g);
            FramedGlass(t, 29, 19, 9, 15, glass, g);
            FramedGlass(t, 90, 19, 9, 15, glass, g);
            FramedGlass(t, 105, 19, 9, 15, glass, g);

            // 两翼檐口和石栏，拉开横向仪式感。
            Rect(t, 5, 46, 44, 4, roof); Frame(t, 5, 46, 44, 4, Shade(roof, .68f));
            Rect(t, 79, 46, 44, 4, roof); Frame(t, 79, 46, 44, 4, Shade(roof, .68f));
            HLine(t, 51, 8, 45, lightSand); HLine(t, 51, 82, 119, lightSand);
            for (int x = 10; x <= 42; x += 8) Rect(t, x, 50, 2, 5, stone);
            for (int x = 84; x <= 116; x += 8) Rect(t, x, 50, 2, 5, stone);

            // 中央柱廊：宽双开门、四柱、厚檐和三角山花共同对齐广场中轴。
            Rect(t, 56, 8, 16, 23, oak); Frame(t, 56, 8, 16, 23, Shade(oak, .58f));
            VLine(t, 63, 9, 29, Shade(oak, 1.18f)); VLine(t, 64, 9, 29, Shade(oak, .55f));
            P(t, 61, 18, gold); P(t, 67, 18, gold);
            g.Add(new GlowRect(57, 9, 14, 20));

            int[] porticoColumns = { 43, 50, 76, 83 };
            for (int i = 0; i < porticoColumns.Length; i++)
            {
                int x = porticoColumns[i];
                Rect(t, x, 11, 4, 29, stone);
                VLine(t, x, 12, 38, Shade(stone, .72f));
                Rect(t, x - 1, 9, 6, 4, darkStone);
                Rect(t, x - 1, 38, 6, 4, lightSand);
            }
            Rect(t, 38, 41, 52, 5, stone); Frame(t, 38, 41, 52, 5, darkStone);
            for (int i = 0; i < 10; i++)
                HLine(t, 46 + i, 40 + i * 2, 87 - i * 2, i < 2 ? roof : lightSand);
            Rect(t, 61, 48, 6, 5, gold); Frame(t, 61, 48, 6, 5, Shade(gold, .65f));              // 市徽
            P(t, 63, 50, lightSand); P(t, 64, 50, lightSand);

            // 高钟塔：宽阔钟面、重檐、铜色钻顶和旗帜，形成全镇唯一主天际线。
            Rect(t, 50, 54, 28, 24, sand); Frame(t, 50, 54, 28, 24, Shade(sand, .67f));
            VLine(t, 53, 56, 75, lightSand); VLine(t, 74, 56, 75, lightSand);
            Rect(t, 56, 59, 16, 14, RGB(0xF4, 0xEF, 0xDF)); Frame(t, 56, 59, 16, 14, oak);
            P(t, 63, 71, oak); P(t, 70, 65, oak); P(t, 63, 60, oak); P(t, 57, 65, oak);
            P(t, 63, 65, roof); P(t, 62, 66, oak); P(t, 61, 67, oak);
            P(t, 64, 66, oak); P(t, 65, 67, oak); P(t, 66, 68, oak);
            g.Add(new GlowRect(57, 60, 14, 12));
            Rect(t, 47, 77, 34, 5, roof); Frame(t, 47, 77, 34, 5, Shade(roof, .64f));
            for (int i = 0; i < 8; i++)
            {
                HLine(t, 82 + i, 49 + i * 2, 78 - i * 2, roof);
                if (i == 0 || i == 4) s.Add(new SnowBand(49 + i * 2, 78 - i * 2, 82 + i));
            }
            VLine(t, 63, 89, 95, darkStone);
            Rect(t, 64, 90, 11, 5, civicRed);
            HLine(t, 94, 64, 74, Shade(civicRed, 1.12f));
            P(t, 73, 92, gold); P(t, 63, 95, gold);

            // 侧翼垂直市政旗增加正面庄重感，不与窗口争抢识别。
            Rect(t, 12, 35, 4, 9, civicRed); Rect(t, 112, 35, 4, 9, civicRed);
            HLine(t, 42, 12, 15, gold); HLine(t, 42, 112, 115, gold);

            s.Add(new SnowBand(5, 48, 48));
            s.Add(new SnowBand(79, 122, 48));
            s.Add(new SnowBand(38, 89, 43));
            s.Add(new SnowBand(47, 80, 79));
        }

        // ————— 职业培训中心:山形屋顶校舍+钟亭+打开的书壁画 —————

        // ————— 职业培训中心(M 档 88×72,V0.8.1 地标化):拉宽校舍+双前门+书壁画居中,山花钟亭保留 —————
        // 塔顶以上预留画布余量(V0.8.3 学校扩建走同地块加层,PRD §176)

        static void PaintSchoolhouse(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color ivory = RGB(0xEF, 0xE6, 0xC8), tealR = RGB(0x4D, 0x86, 0x7E), oak = RGB(0x6A, 0x44, 0x28);
            Color bookTeal = RGB(0x3E, 0x70, 0x6C), gold = RGB(0xD9, 0xA8, 0x3A);
            Rect(t, 6, 0, 76, 34, ivory); Frame(t, 6, 0, 76, 34, Shade(ivory, 0.78f));
            HLine(t, 12, 7, 81, Shade(ivory, 0.9f));                                           // 束带层

            // V0.9.6 加高档 88×96:一层之上加整层教室,钟塔随屋脊上移——校舍成为西区制高点
            Rect(t, 6, 34, 76, 28, ivory); Frame(t, 6, 34, 76, 28, Shade(ivory, 0.78f));       // 二层墙体
            HLine(t, 34, 7, 81, Shade(ivory, 0.66f));                                          // 楼层分隔线
            HLine(t, 35, 7, 81, Shade(ivory, 0.94f));
            HLine(t, 60, 7, 81, Shade(ivory, 0.9f));                                           // 二层束带
            HLine(t, 61, 8, 79, Shade(ivory, 0.72f));                                          // 檐口投影
            GableRoof(t, 2, 85, 62, 79, tealR, s);                                             // 宽幅山形屋顶

            Rect(t, 39, 80, 10, 3, ivory); Frame(t, 39, 80, 10, 3, Shade(ivory, 0.7f));        // 钟亭基座
            Rect(t, 41, 83, 6, 7, Shade(ivory, 0.6f));                                          // 钟洞暗部
            P(t, 41, 83, ivory); P(t, 46, 83, ivory);                                           // 拱切角
            Rect(t, 42, 85, 4, 3, gold); P(t, 41, 86, gold); P(t, 46, 86, gold);                // 铜
            P(t, 43, 88, Shade(gold, 0.7f)); P(t, 44, 88, Shade(gold, 0.7f));
            HLine(t, 90, 39, 48, tealR); HLine(t, 91, 41, 46, tealR);                           // 钟亭小顶
            P(t, 43, 93, gold); P(t, 44, 93, gold); P(t, 43, 92, Shade(gold, 0.8f));

            Rect(t, 29, 0, 9, 14, oak); Frame(t, 29, 0, 9, 14, Shade(oak, 0.62f));             // 双前门
            Rect(t, 50, 0, 9, 14, oak); Frame(t, 50, 0, 9, 14, Shade(oak, 0.62f));
            P(t, 35, 7, RGB(0xE2, 0xC8, 0x8A)); P(t, 52, 7, RGB(0xE2, 0xC8, 0x8A));            // 门灯座
            g.Add(new GlowRect(30, 1, 7, 11)); g.Add(new GlowRect(51, 1, 7, 11));
            FramedGlass(t, 30, 15, 7, 3, RGB(0x54, 0x74, 0x8E), g);                            // 门上气窗
            FramedGlass(t, 51, 15, 7, 3, RGB(0x54, 0x74, 0x8E), g);

            FramedGlass(t, 9, 14, 9, 12, RGB(0x44, 0x58, 0x6E), g);                            // 一层教室高窗×4
            FramedGlass(t, 19, 14, 9, 12, RGB(0x44, 0x58, 0x6E), g);
            FramedGlass(t, 60, 14, 9, 12, RGB(0x44, 0x58, 0x6E), g);
            FramedGlass(t, 70, 14, 9, 12, RGB(0x44, 0x58, 0x6E), g);

            FramedGlass(t, 10, 40, 10, 14, RGB(0x44, 0x58, 0x6E), g);                          // 二层教室高窗×4
            FramedGlass(t, 24, 40, 10, 14, RGB(0x44, 0x58, 0x6E), g);
            FramedGlass(t, 54, 40, 10, 14, RGB(0x44, 0x58, 0x6E), g);
            FramedGlass(t, 68, 40, 10, 14, RGB(0x44, 0x58, 0x6E), g);
            VLine(t, 14, 41, 53, Shade(ivory, 0.8f)); VLine(t, 28, 41, 53, Shade(ivory, 0.8f)); // 窗中竖棂
            VLine(t, 58, 41, 53, Shade(ivory, 0.8f)); VLine(t, 72, 41, 53, Shade(ivory, 0.8f));

            Rect(t, 37, 39, 14, 15, RGB(0xF6, 0xEF, 0xDA));                                    // 二层正中大钟
            Frame(t, 37, 39, 14, 15, bookTeal); Frame(t, 38, 40, 12, 13, Shade(bookTeal, 0.75f));
            VLine(t, 44, 44, 50, Shade(bookTeal, 0.9f)); HLine(t, 46, 44, 48, Shade(bookTeal, 0.9f));
            P(t, 44, 42, gold); P(t, 44, 51, gold); P(t, 40, 46, gold); P(t, 48, 46, gold);
            g.Add(new GlowRect(38, 40, 12, 13));

            Rect(t, 36, 21, 16, 9, RGB(0xF6, 0xEF, 0xDA)); Frame(t, 36, 21, 16, 9, bookTeal);  // 打开的书
            VLine(t, 43, 22, 28, Shade(bookTeal, 0.9f));
            HLine(t, 23, 38, 42, RGB(0xB9, 0xAA, 0x84)); HLine(t, 25, 38, 42, RGB(0xB9, 0xAA, 0x84));
            HLine(t, 23, 45, 49, RGB(0xB9, 0xAA, 0x84)); HLine(t, 25, 45, 49, RGB(0xB9, 0xAA, 0x84));
            P(t, 40, 24, gold); P(t, 47, 24, gold);                                             // 书页金饰

            s.Add(new SnowBand(40, 47, 91));                                                   // 钟亭小顶雪线
        }

        // ————— 社区学校:红砖双层校舍+对称教室窗+书本校徽+小钟亭 —————

        static void PaintCommunitySchool(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color brick = RGB(0xB7, 0x69, 0x52), brickD = RGB(0x7A, 0x43, 0x3A), brickL = RGB(0xCF, 0x84, 0x66);
            Color stone = RGB(0xE8, 0xDD, 0xBE), stoneD = RGB(0xB8, 0xA9, 0x86);
            Color roof = RGB(0x3E, 0x5A, 0x67), roofL = RGB(0x5D, 0x7D, 0x86);
            Color glass = RGB(0x4C, 0x6C, 0x82), oak = RGB(0x70, 0x45, 0x2D), gold = RGB(0xD7, 0xA5, 0x3C);

            Rect(t, 3, 0, 58, 38, brick); Frame(t, 3, 0, 58, 38, brickD);                       // 双层红砖主体
            Rect(t, 3, 0, 58, 4, stoneD); HLine(t, 4, 4, 59, stone);                           // 厚石基座
            for (int y = 8; y <= 34; y += 5) HLine(t, y, 4, 59, Shade(brick, .82f));           // 水平砖缝
            for (int y = 9; y < 34; y += 10)
                for (int x = 9 + ((y / 5) % 2) * 4; x < 59; x += 9)
                    VLine(t, x, y, y + 3, Shade(brick, .84f));                                 // 错缝砖面
            HLine(t, 20, 3, 60, stoneD); HLine(t, 21, 4, 59, stone);                           // 楼层石带
            Rect(t, 2, 36, 60, 4, stone); Frame(t, 2, 36, 60, 4, stoneD);                     // 厚檐口

            int[] lowerWindows = { 7, 17, 39, 49 };
            for (int i = 0; i < lowerWindows.Length; i++)
            {
                int x = lowerWindows[i];
                FramedGlass(t, x, 7, 8, 10, glass, g);
                VLine(t, x + 4, 8, 15, stoneD);
            }
            int[] upperWindows = { 7, 17, 39, 49 };
            for (int i = 0; i < upperWindows.Length; i++)
            {
                int x = upperWindows[i];
                FramedGlass(t, x, 24, 8, 9, glass, g);
                VLine(t, x + 4, 25, 31, stoneD);
            }

            Rect(t, 24, 2, 16, 19, stone); Frame(t, 24, 2, 16, 19, stoneD);                    // 中央入口石框
            Rect(t, 28, 3, 8, 15, oak); Frame(t, 28, 3, 8, 15, Shade(oak, .62f));
            VLine(t, 31, 4, 16, Shade(oak, 1.15f)); P(t, 34, 10, gold);
            Rect(t, 28, 17, 8, 3, glass); Frame(t, 28, 17, 8, 3, stoneD);
            g.Add(new GlowRect(29, 4, 6, 13));

            Rect(t, 25, 24, 14, 9, stone); Frame(t, 25, 24, 14, 9, roof);                      // 书本校徽
            VLine(t, 31, 25, 31, roof); HLine(t, 27, 27, 30, stoneD); HLine(t, 29, 27, 30, stoneD);
            HLine(t, 27, 33, 36, stoneD); HLine(t, 29, 33, 36, stoneD);
            P(t, 31, 30, gold); P(t, 32, 30, gold);

            GableRoof(t, 0, 63, 40, 54, roof, s);                                              // 横向深色坡屋顶
            HLine(t, 41, 3, 60, roofL);
            Rect(t, 25, 52, 14, 8, stone); Frame(t, 25, 52, 14, 8, stoneD);                   // 小钟亭
            Rect(t, 29, 54, 6, 5, roof); P(t, 31, 56, gold); P(t, 32, 56, gold);
            HLine(t, 60, 23, 40, roof); HLine(t, 61, 26, 37, roof); HLine(t, 62, 29, 34, roofL);
            P(t, 31, 63, gold); P(t, 32, 63, gold);
            s.Add(new SnowBand(23, 40, 61));
        }

        // ————— 河畔工厂:砖墙+锯齿屋顶+大烟囱+货运卷帘门 —————

        // ————— 河畔工厂(M 档 88×72,V0.8.1 地标化):加高砖墙+四锯齿屋顶;大烟囱改为独立道具(见 Decorate) —————

        static void PaintFactory(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color brick = RGB(0xA8, 0x7A, 0x5A), slateRoof = RGB(0x68, 0x61, 0x58);
            Color steel = RGB(0x3E, 0x4A, 0x56), glassI = RGB(0x5F, 0x7E, 0x7A);
            Rect(t, 4, 0, 80, 38, brick); Frame(t, 4, 0, 80, 38, Shade(brick, 0.72f));
            for (int y = 4; y < 38; y += 4) HLine(t, y, 5, 83, Shade(brick, 0.85f));          // 砖缝
            for (int x = 9; x < 84; x += 8)
                for (int y = (x / 8) % 2 == 0 ? 2 : 6; y < 38; y += 8) P(t, x, y, Shade(brick, 0.88f));

            Rect(t, 9, 0, 22, 20, RGB(0x93, 0xA0, 0xAE)); Frame(t, 9, 0, 22, 20, Shade(slateRoof, 0.7f)); // 卷帘门
            for (int y = 3; y < 20; y += 3) HLine(t, y, 10, 29, Shade(RGB(0x93, 0xA0, 0xAE), 0.82f));
            for (int x = 10; x <= 29; x += 2) P(t, x, 2, (x / 2) % 2 == 0 ? RGB(0xE2, 0xC0, 0x3A) : steel);
            P(t, 12, 21, Shade(slateRoof, 0.75f)); P(t, 27, 21, Shade(slateRoof, 0.75f));      // 吊轮

            Rect(t, 62, 0, 7, 13, steel); Frame(t, 62, 0, 7, 13, Shade(steel, 0.7f));          // 人行门

            FramedGlass(t, 6, 28, 12, 7, glassI, g);                                           // 高侧窗带
            FramedGlass(t, 33, 28, 15, 7, glassI, g);
            FramedGlass(t, 59, 28, 12, 7, glassI, g);

            for (int tooth = 0; tooth < 4; tooth++)                                            // 四联锯齿屋顶
            {
                int bx = 4 + tooth * 20;
                for (int k = 0; k <= 11; k++)
                {
                    int xs = bx + (k * 19 + 11) / 12;
                    HLine(t, 38 + k, xs, bx + 19, k % 3 == 0 ? Shade(slateRoof, 1.08f) : slateRoof);
                    P(t, xs, 38 + k, RGB(0x9F, 0xC4, 0xBE));                                   // 采光坡缘
                    // 围猎三#21:锯齿坡左低右高,雪带必须逐段跟随坡面高度(仿 GableRoof 每 3 行
                    // 登记一条,topY=坡面行+1)。旧的两条固定 topY 41/48 在坡左段悬在空中。
                    if (k % 3 == 0 && xs + 1 <= bx + 18)
                        s.Add(new SnowBand(xs + 1, bx + 18, 39 + k));
                }
            }

            Rect(t, 37, 22, 2, 1, steel); Rect(t, 37, 23, 1, 3, steel);                        // 墙灯
            g.Add(new GlowRect(36, 23, 5, 3));
        }

        /// <summary>工厂独立大烟囱(V0.8.1):16×72,砖身收分+箍环+深色烟口;基座落地、烟口高出厂房屋脊。</summary>
        public static Sprite FactoryChimney()
        {
            Sprite cached;
            if (TryGetCached("prop_factory_chimney", out cached)) return cached;
            Texture2D t = NewTex(16, 72);
            Color brick = RGB(0x8F, 0x64, 0x48), brickD = RGB(0x6E, 0x4C, 0x38), soot = RGB(0x4A, 0x42, 0x3C);
            Rect(t, 1, 0, 14, 10, brick); Frame(t, 1, 0, 14, 10, brickD);                       // 基座
            Rect(t, 3, 10, 10, 48, brick); Frame(t, 3, 10, 10, 48, brickD);                     // 收分砖身
            for (int y = 14; y < 58; y += 6) HLine(t, y, 4, 11, Shade(brick, 0.9f));
            HLine(t, 22, 3, 12, RGB(0x7A, 0x8A, 0x94)); HLine(t, 23, 3, 12, RGB(0x5E, 0x6C, 0x76)); // 检修箍环
            HLine(t, 40, 3, 12, RGB(0x7A, 0x8A, 0x94)); HLine(t, 41, 3, 12, RGB(0x5E, 0x6C, 0x76));
            Rect(t, 2, 58, 12, 6, Shade(brick, 0.92f)); Frame(t, 2, 58, 12, 6, brickD);         // 顶沿
            Rect(t, 4, 64, 8, 8, soot); Frame(t, 4, 64, 8, 8, Shade(soot, 0.7f));               // 深色烟口
            P(t, 6, 67, Shade(soot, 1.3f)); P(t, 9, 68, Shade(soot, 1.2f));
            Apply(t);
            return Finish(t, "prop_factory_chimney");
        }

        /// <summary>烟囱静态烟缕(§154 粒子预算不动——烟是贴图不是粒子);三团渐散灰雾。</summary>
        public static Sprite FactorySmoke()
        {
            Sprite cached;
            if (TryGetCached("prop_factory_smoke", out cached)) return cached;
            Texture2D t = NewTex(22, 34);
            Color c1 = new Color32(0xC8, 0xC4, 0xBC, 190);
            Color c2 = new Color32(0xCF, 0xCB, 0xC4, 140);
            Color c3 = new Color32(0xD6, 0xD3, 0xCC, 90);
            Rect(t, 9, 0, 5, 8, c1);
            Rect(t, 7, 7, 8, 6, c2); P(t, 6, 9, c2); P(t, 15, 8, c2);
            Rect(t, 5, 13, 9, 6, c3); P(t, 4, 15, c3); P(t, 14, 14, c3); P(t, 10, 19, c3);
            Rect(t, 3, 19, 8, 4, new Color32(0xDC, 0xDA, 0xD4, 60));
            P(t, 2, 20, new Color32(0xDC, 0xDA, 0xD4, 60)); P(t, 12, 21, new Color32(0xDC, 0xDA, 0xD4, 60));
            Apply(t);
            return Finish(t, "prop_factory_smoke");
        }

        // ————— V0.8.2 地标院落围合(PRD §170):铁艺/绿篱/链网围栏 + 门柱 + 道具落影 —————
        // 全部静态常驻:不进雪线登记表(低构件积雪不可读)、不进季节重建、不读任何状态。

        /// <summary>铁艺围栏段(市政,V0.8.2b 重绘):砖基座+铁矛条+顶轨+每 24px 砖柱,总高 22px
        /// (含底部 2px 接触落影,基座让围栏在草地上有质量感);px 为段长(调用方按 0.25 世界步进取整)。</summary>
        public static Sprite FenceIron(int px)
        {
            string key = "fence_iron_" + px;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(px, 22);
            Color iron = RGB(0x46, 0x4C, 0x58), ironL = RGB(0x6E, 0x76, 0x84), ironD = RGB(0x30, 0x34, 0x3E);
            Color brick = RGB(0x9A, 0x6A, 0x3A), brickD = RGB(0x74, 0x4E, 0x2C), cap = RGB(0xC8, 0xC2, 0xAA);
            Rect(t, 0, 0, px, 2, new Color32(0x1A, 0x14, 0x10, 60));                // 接触落影
            Rect(t, 0, 2, px, 3, brick); HLine(t, 3, 0, px - 1, Shade(brick, 0.86f)); // 砖基座+横缝
            Rect(t, 0, 5, px, 1, cap); P(t, 0, 5, Shade(cap, 0.9f));                // 基座压顶
            HLine(t, 15, 0, px - 1, iron); HLine(t, 16, 0, px - 1, ironL);          // 顶轨
            for (int x = 0; x < px; x++)
            {
                int m = x % 24;
                bool pier = m < 6 || x >= px - 6;                                   // 首尾必有柱收口
                if (pier)
                {
                    VLine(t, x, 2, 19, brick);
                    if (m == 0 || m == 5 || x == px - 6) VLine(t, x, 2, 19, brickD);
                    for (int y = 7; y < 19; y += 4) P(t, x, y, brickD);             // 砖缝
                }
                else if (m % 4 == 1)
                {
                    VLine(t, x, 6, 14, iron); P(t, x, 7, ironL);                    // 铁矛条
                    P(t, x, 17, ironL); P(t, x, 18, iron); P(t, x, 19, ironD);      // 矛尖
                }
            }
            for (int x = 0; x < px; x++)
            {
                int m = x % 24;
                if (m < 6 || x >= px - 6)
                {
                    P(t, x, 20, cap); P(t, x, 21, Shade(cap, 0.84f));               // 柱压顶
                    P(t, x, 19, brickD);                                            // 压顶投影
                }
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>绿篱围栏段(文教,V0.8.2b 重绘):修剪起伏顶+叶团双色+零星花点+培土沿+每 24px 砖柱,
        /// 总高 16px(含底部 2px 接触落影)。</summary>
        public static Sprite FenceHedge(int px)
        {
            string key = "fence_hedge_" + px;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(px, 16);
            Color g1 = RGB(0x46, 0x82, 0x3A), g2 = RGB(0x38, 0x68, 0x30), gL = RGB(0x66, 0xA4, 0x50), gD = RGB(0x2C, 0x52, 0x26);
            Color soil = RGB(0x54, 0x44, 0x34), brick = RGB(0x9A, 0x6A, 0x3A), brickD = RGB(0x74, 0x4E, 0x2C), cap = RGB(0xC8, 0xC2, 0xAA);
            Color bloomR = RGB(0xC8, 0x54, 0x54), bloomW = RGB(0xE8, 0xE0, 0xCE);
            Rect(t, 0, 0, px, 2, new Color32(0x1A, 0x14, 0x10, 60));                // 接触落影
            Rect(t, 0, 2, px, 1, soil);                                             // 培土沿
            for (int x = 0; x < px; x++)
            {
                int m = x % 24;
                int crest = x % 6 < 3 ? 12 : 11;                                    // 修剪起伏(3px 一浪)
                if (m < 4 || x >= px - 4)
                {
                    VLine(t, x, 2, 14, brick);
                    if (m == 0 || m == 3 || x == px - 4) VLine(t, x, 2, 14, brickD);
                    for (int y = 5; y < 13; y += 4) P(t, x, y, brickD);
                    P(t, x, 14, cap); P(t, x, 15, Shade(cap, 0.84f)); P(t, x, 13, brickD);
                }
                else
                {
                    HLine(t, 3, x, x, gD);                                          // 篱底背光
                    for (int y = 4; y <= crest; y++)
                    {
                        Color c = (x * 7 + y * 5) % 9 < 2 ? g2 : g1;                // 叶团双色
                        if ((x * 13 + y * 7) % 19 == 0 && y > crest - 3) c = gL;    // 受光叶簇(集中在上部)
                        if ((x * 11 + y * 3) % 37 == 0) c = bloomR;                 // 零星红花
                        else if ((x * 17 + y * 5) % 53 == 0) c = bloomW;            // 零星白花
                        P(t, x, y, c);
                    }
                    P(t, x, crest, gL);                                             // 浪尖受光
                }
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>链网围栏段(工业,V0.8.2b 重绘):织网提深加重(草地上可读)+双轨+每 16px 钢柱,
        /// 总高 18px(含底部 2px 接触落影)。</summary>
        public static Sprite FenceChain(int px)
        {
            string key = "fence_chain_" + px;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(px, 18);
            Color post = RGB(0x64, 0x6A, 0x76), postD = RGB(0x48, 0x4E, 0x58), postL = RGB(0x86, 0x8C, 0x96);
            Color mesh = new Color32(0x70, 0x76, 0x80, 210), meshL = new Color32(0x9A, 0xA0, 0xA8, 210);
            Rect(t, 0, 0, px, 2, new Color32(0x1A, 0x14, 0x10, 60));                // 接触落影
            HLine(t, 2, 0, px - 1, postD); HLine(t, 3, 0, px - 1, post);            // 下轨
            HLine(t, 13, 0, px - 1, post); HLine(t, 14, 0, px - 1, postL);          // 上轨
            for (int y = 4; y <= 12; y++)
                for (int x = 0; x < px; x++)
                {
                    int m = x % 16;
                    if (m < 3 || x >= px - 3) continue;
                    bool weave = (x + y) % 4 == 0;
                    if (weave || (x - y + 80) % 4 == 0) P(t, x, y, weave ? meshL : mesh); // 菱形织网(亮暗两股)
                }
            for (int x = 0; x < px; x++)
            {
                int m = x % 16;
                if (m < 3 || x >= px - 3)
                {
                    VLine(t, x, 2, 16, post);
                    if (m == 0 || m == 2 || x == px - 3) VLine(t, x, 2, 16, postD);
                    P(t, x, 17, postL); P(t, x, 2, postD);                          // 柱顶/接地
                }
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>院落门柱(V0.8.2b 放大灯头):style 0=市政砖柱+顶灯 / 1=文教砖柱+石球 / 2=工业钢柱 /
        /// 3=住宅白木短柱+尖顶帽(V0.9.6,只占画布下 23 行以配 14px 矮栅栏),12×34。</summary>
        public static Sprite GatePier(int style)
        {
            string key = "fence_pier_" + style;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(12, 34);
            Color brick = RGB(0x9A, 0x6A, 0x3A), brickD = RGB(0x74, 0x4E, 0x2C);
            Color cap = RGB(0xC8, 0xC2, 0xAA), steel = RGB(0x6E, 0x74, 0x7E), steelD = RGB(0x52, 0x58, 0x62);
            Color ironD = RGB(0x2C, 0x30, 0x38), glass = RGB(0xF6, 0xE2, 0xA0);
            Color wood = RGB(0xE6, 0xDE, 0xCA), woodD = RGB(0xB4, 0xA8, 0x90);
            if (style == 3)
            {
                Rect(t, 3, 0, 6, 2, Shade(wood, 0.78f));                            // 柱脚
                Rect(t, 4, 2, 4, 15, wood);                                          // 柱身
                VLine(t, 4, 2, 16, woodD); VLine(t, 7, 2, 16, RGB(0xF6, 0xF1, 0xE2));
                Rect(t, 3, 17, 6, 2, RGB(0xF0, 0xE9, 0xD8));                         // 压顶
                HLine(t, 16, 3, 8, woodD);
                Rect(t, 4, 19, 4, 2, wood); Rect(t, 5, 21, 2, 2, RGB(0xF6, 0xF1, 0xE2)); // 尖顶帽
                P(t, 4, 19, woodD); P(t, 7, 19, woodD);
                Apply(t);
                return Finish(t, key);
            }
            Color body = style == 2 ? steel : brick, edge = style == 2 ? steelD : brickD;
            Rect(t, 2, 0, 8, 3, Shade(body, 0.8f));                                 // 基座
            Rect(t, 3, 3, 6, 19, body);                                             // 柱身
            VLine(t, 3, 3, 21, edge); VLine(t, 8, 3, 21, edge);
            for (int y = 6; y < 21; y += 5) HLine(t, y, 4, 7, Shade(body, 0.88f));  // (砖)缝线
            Rect(t, 1, 22, 10, 3, cap); HLine(t, 21, 1, 10, Shade(cap, 0.72f));     // 压顶+投影
            if (style == 0)
            {
                Rect(t, 4, 25, 4, 1, ironD);                                        // 灯座托
                Rect(t, 3, 26, 6, 6, ironD);                                        // 灯框
                Rect(t, 4, 27, 4, 4, glass);                                        // 暖玻璃
                Rect(t, 5, 28, 2, 2, RGB(0xFF, 0xF0, 0xBE));                        // 灯芯
                P(t, 4, 27, Shade(glass, 1.08f));                                   // 玻璃受光
                Rect(t, 3, 32, 6, 1, ironD);                                        // 灯帽
                P(t, 5, 33, ironD); P(t, 6, 33, ironD);                             // 顶尖
            }
            else if (style == 1)
            {
                Rect(t, 4, 30, 4, 2, Shade(cap, 0.94f));                            // 石球顶(压暗,防读成白球)
                P(t, 4, 29, Shade(cap, 0.92f)); P(t, 5, 29, Shade(cap, 0.88f));
                P(t, 7, 30, Shade(cap, 0.8f)); P(t, 7, 31, Shade(cap, 0.74f));      // 球体明暗
                P(t, 5, 33, Shade(cap, 0.86f)); P(t, 6, 33, Shade(cap, 0.86f));
            }
            else
            {
                VLine(t, 5, 25, 31, steelD); VLine(t, 6, 25, 31, steelD);            // 短杆顶
                P(t, 5, 32, Shade(steelD, 1.3f)); P(t, 6, 32, steelD);               // 杆帽
                P(t, 5, 8, steelD); P(t, 6, 14, steelD);                            // 铆点
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>纵向围栏段:横向贴图旋转 90° 复用(柱距沿行进方向、栏杆成竖线)——2D 俯视栅栏通用惯例。
        /// V0.8.2c:铁艺(style 0)改原生窄幅 FenceIronV——旋转把 22px 立面厚度变成横向厚度(1.375 格),
        /// 与邻楼贴墙,是"围栏生硬"的主因之一。
        /// V0.9.6b:绿篱(style 1)同样改原生窄幅 FenceHedgeV——旋转把横向的"砖柱带"转成横档,
        /// 6 格长的纵篱整条读作绿色木栏杆(端到端截图复看,公园东西两界最明显)。
        /// 只剩链网(style 2)仍走旋转:织网本身是各向同性的网格,转 90° 形态不变。</summary>
        public static Sprite FenceVertical(int style, int px)
        {
            if (style == 0) return FenceIronV(px);
            if (style == 1) return FenceHedgeV(px);
            if (style == 3) return FencePicketV(px);   // V0.9.6 木栅栏同样走原生窄幅,不用旋转
            string key = "fence_chain_" + px + "v";
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D s = FenceChain(px).texture;
            Texture2D t = NewTex(s.height, s.width);
            for (int x = 0; x < s.width; x++)
                for (int y = 0; y < s.height; y++)
                    t.SetPixel(y, s.width - 1 - x, s.GetPixel(x, y));
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>纵向绿篱(原生窄幅 16×px,V0.9.6b)。砖柱只留首尾,沿程改用双色叶团+起伏叶冠,
        /// 东缘压暗作背光面——这样一条长纵篱读作"一道篱",而不是一排横木条。</summary>
        public static Sprite FenceHedgeV(int px)
        {
            string key = "fence_hedge_" + px + "v";
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, px);
            Color g1 = RGB(0x46, 0x82, 0x3A), g2 = RGB(0x38, 0x68, 0x30), gL = RGB(0x66, 0xA4, 0x50), gD = RGB(0x2C, 0x52, 0x26);
            Color soil = RGB(0x54, 0x44, 0x34), brick = RGB(0x9A, 0x6A, 0x3A), brickD = RGB(0x74, 0x4E, 0x2C), cap = RGB(0xC8, 0xC2, 0xAA);
            Color bloomR = RGB(0xC8, 0x54, 0x54), bloomW = RGB(0xE8, 0xE0, 0xCE);
            Rect(t, 0, 0, 2, px, new Color32(0x1A, 0x14, 0x10, 60));                // 西缘接触落影
            VLine(t, 2, 0, px - 1, soil);                                           // 培土沿
            for (int y = 0; y < px; y++)
            {
                if (y < 4 || y >= px - 4)                                            // 首尾砖柱(端头收口)
                {
                    Rect(t, 2, y, 12, 1, y == 0 || y == px - 1 ? brickD : brick);
                    P(t, 13, y, cap); P(t, 3, y, brickD);
                    continue;
                }
                int crest = y % 6 < 3 ? 13 : 12;                                     // 叶冠起伏(3px 一浪)
                for (int x = 3; x <= crest; x++)
                {
                    Color c = (x * 7 + y * 5) % 9 < 2 ? g2 : g1;                     // 叶团双色
                    if ((x * 13 + y * 7) % 19 == 0 && x < 7) c = gL;                 // 受光叶簇集中在西侧
                    if ((x * 11 + y * 3) % 37 == 0) c = bloomR;                      // 零星红花
                    else if ((x * 17 + y * 5) % 53 == 0) c = bloomW;                 // 零星白花
                    P(t, x, y, c);
                }
                P(t, crest, y, gD);                                                  // 东缘背光
                if (crest + 1 < 16) P(t, crest + 1, y, Shade(gD, .8f));
            }
            Apply(t);
            return Finish(t, key);
        }


        /// <summary>纵向铁艺围栏(V0.8.2c 原生窄幅,14×px):西缘落影+双轨+沿程矛条虚线+每 24px 砖柱带,
        /// 首尾必有柱——厚度 0.875 格,告别旋转立面的"厚板"感。</summary>
        public static Sprite FenceIronV(int px)
        {
            string key = "fence_iron_" + px + "v";
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(14, px);
            Color iron = RGB(0x46, 0x4C, 0x58), ironL = RGB(0x6E, 0x76, 0x84), ironD = RGB(0x30, 0x34, 0x3E);
            Color brick = RGB(0x9A, 0x6A, 0x3A), brickD = RGB(0x74, 0x4E, 0x2C), cap = RGB(0xC8, 0xC2, 0xAA);
            Rect(t, 0, 0, 2, px, new Color32(0x1A, 0x14, 0x10, 60));                // 西缘接触落影
            VLine(t, 3, 0, px - 1, iron); VLine(t, 4, 0, px - 1, ironL);            // 西轨
            VLine(t, 9, 0, px - 1, ironL); VLine(t, 10, 0, px - 1, iron);           // 东轨
            for (int y = 0; y < px; y++)
            {
                bool post = y % 24 < 6 || y >= px - 6;                              // 沿程砖柱带,首尾必有柱
                if (post)
                {
                    Rect(t, 2, y, 10, 1, brick);
                    if (y % 24 == 0 || y >= px - 6) { VLine(t, 2, y, y, brickD); VLine(t, 11, y, y, brickD); }
                }
                else if (y % 4 == 1)
                {
                    Rect(t, 6, y, 2, 1, iron); P(t, 6, y, ironL);                   // 矛条侧影(虚线)
                }
            }
            for (int y = 0; y < px; y++)
            {
                int m = y % 24;
                if (m == 5)                                                          // 柱带压顶(每段收口)
                {
                    HLine(t, y, 2, 11, cap); P(t, 2, y, Shade(cap, 0.84f)); P(t, 11, y, Shade(cap, 0.84f));
                }
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>白色木栅栏段(V0.9.6 第 4 种隔断,住宅/商铺地块界):尖头栅条+双横杆+每 24px 加粗立柱,
        /// 总高 14px(含底部 2px 接触落影)——比绿篱 16/链网 18/铁艺 22 更矮，读作"住家院栏"而非官家围墙。</summary>
        public static Sprite FencePicket(int px)
        {
            string key = "fence_picket_" + px;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(px, 14);
            Color white = RGB(0xE6, 0xDE, 0xCA), whiteL = RGB(0xF6, 0xF1, 0xE2), whiteD = RGB(0xB4, 0xA8, 0x90);
            Color rail = RGB(0xD2, 0xC6, 0xAC), railD = RGB(0xA0, 0x94, 0x7C);
            Rect(t, 0, 0, px, 2, new Color32(0x1A, 0x14, 0x10, 60));                // 接触落影
            for (int x = 0; x < px; x++)
            {
                int m = x % 24;
                if (m < 3 || x >= px - 3)                                            // 立柱带(首尾必有柱)
                {
                    VLine(t, x, 2, 12, white);
                    if (m == 0 || x == px - 3) VLine(t, x, 2, 12, whiteD);
                    P(t, x, 13, whiteL);
                    continue;
                }
                if (m % 4 < 2)                                                       // 4px 一档:2px 栅条 + 2px 空隙
                {
                    VLine(t, x, 2, 10, m % 4 == 0 ? white : whiteL);
                    P(t, x, 11, m % 4 == 0 ? whiteL : whiteD);                       // 尖头
                }
            }
            HLine(t, 4, 0, px - 1, railD); HLine(t, 5, 0, px - 1, rail);            // 下横杆
            HLine(t, 8, 0, px - 1, railD); HLine(t, 9, 0, px - 1, rail);            // 上横杆
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>纵向白色木栅栏(原生窄幅 12×px,照 FenceIronV 惯例):西缘落影+双纵杆+沿程栅条+每 24px 柱带。</summary>
        public static Sprite FencePicketV(int px)
        {
            string key = "fence_picket_" + px + "v";
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(12, px);
            Color white = RGB(0xE6, 0xDE, 0xCA), whiteL = RGB(0xF6, 0xF1, 0xE2), whiteD = RGB(0xB4, 0xA8, 0x90);
            Color rail = RGB(0xD2, 0xC6, 0xAC), railD = RGB(0xA0, 0x94, 0x7C);
            Rect(t, 0, 0, 2, px, new Color32(0x1A, 0x14, 0x10, 60));                // 西缘接触落影
            for (int y = 0; y < px; y++)
            {
                if (y % 24 < 3 || y >= px - 3)                                       // 柱带
                {
                    Rect(t, 2, y, 9, 1, white);
                    P(t, 2, y, whiteD); P(t, 10, y, whiteL);
                    continue;
                }
                if (y % 4 < 2)                                                       // 栅条侧影
                {
                    Rect(t, 3, y, 8, 1, y % 4 == 0 ? white : whiteL);
                    P(t, 10, y, whiteD);
                }
            }
            VLine(t, 4, 0, px - 1, railD); VLine(t, 5, 0, px - 1, rail);            // 西纵杆
            VLine(t, 8, 0, px - 1, rail); VLine(t, 9, 0, px - 1, railD);            // 东纵杆
            Apply(t);
            return Finish(t, key);
        }

        // ————— V0.9.6 街具家族(装饰灯/邮筒/自行车架/花箱/凉亭)—————
        // 全部纯装饰、无碰撞、无状态、不进季节表；pivot 居中、PPU16(摆位口径 = 基线 + 画布半高)。
        // 装饰灯故意**不带夜辉**：亮灯是「路灯」公共项目的成果(政府要花钱建),街心矮灯只是白天的街具。

        /// <summary>街心装饰矮灯(12×26):铸铁柱+球形暖玻璃灯罩+尖顶帽。</summary>
        public static Sprite ParkLamp()
        {
            Sprite cached;
            if (TryGetCached("park_lamp", out cached)) return cached;
            Texture2D t = NewTex(12, 26);
            Color iron = RGB(0x40, 0x46, 0x52), ironL = RGB(0x68, 0x70, 0x7E), ironD = RGB(0x28, 0x2C, 0x36);
            Color glass = RGB(0xF2, 0xE0, 0xA6), core = RGB(0xFF, 0xF4, 0xCE);
            Rect(t, 2, 0, 8, 1, new Color32(0x1A, 0x14, 0x10, 70));                 // 接触落影
            Rect(t, 3, 1, 6, 2, ironD); Rect(t, 4, 3, 4, 2, iron);                  // 台座
            Rect(t, 5, 5, 2, 11, iron); VLine(t, 5, 5, 15, ironL);                  // 灯杆
            Rect(t, 3, 16, 6, 1, ironD);                                            // 灯托
            Rect(t, 2, 17, 8, 5, ironD); Rect(t, 3, 18, 6, 3, glass);               // 灯罩
            Rect(t, 4, 19, 4, 1, core); P(t, 3, 20, Shade(glass, 1.08f));
            Rect(t, 2, 22, 8, 1, ironD); Rect(t, 4, 23, 4, 1, ironD);              // 灯帽
            P(t, 5, 24, ironD); P(t, 6, 24, ironD); P(t, 5, 25, ironL);            // 顶尖
            Apply(t);
            return Finish(t, "park_lamp");
        }

        /// <summary>邮筒(10×18):朱红筒身+投信口+白环+顶盖。</summary>
        public static Sprite Mailbox()
        {
            Sprite cached;
            if (TryGetCached("mailbox", out cached)) return cached;
            Texture2D t = NewTex(10, 18);
            Color red = RGB(0xB8, 0x3E, 0x38), redD = RGB(0x8A, 0x2A, 0x26), redL = RGB(0xD8, 0x5E, 0x50);
            Color white = RGB(0xEE, 0xE6, 0xD4), iron = RGB(0x38, 0x34, 0x38);
            Rect(t, 1, 0, 8, 1, new Color32(0x1A, 0x14, 0x10, 70));                 // 接触落影
            Rect(t, 4, 1, 2, 3, iron);                                              // 立柱
            Rect(t, 2, 4, 6, 11, red); Frame(t, 2, 4, 6, 11, redD);
            VLine(t, 3, 5, 13, redL);                                               // 受光侧
            HLine(t, 8, 3, 6, white); HLine(t, 9, 3, 6, white);                     // 白环
            Rect(t, 3, 11, 4, 2, iron); P(t, 3, 12, Shade(iron, 1.4f));             // 投信口
            Rect(t, 1, 15, 8, 2, redD); Rect(t, 2, 17, 6, 1, red);                  // 顶盖
            Apply(t);
            return Finish(t, "mailbox");
        }

        /// <summary>自行车架(20×14):双 U 形钢管+一辆停靠的自行车侧影。</summary>
        public static Sprite BikeRack()
        {
            Sprite cached;
            if (TryGetCached("bike_rack", out cached)) return cached;
            Texture2D t = NewTex(20, 14);
            Color steel = RGB(0x7A, 0x82, 0x8E), steelL = RGB(0xA2, 0xAA, 0xB4), steelD = RGB(0x56, 0x5C, 0x66);
            Color frame = RGB(0x36, 0x74, 0x86), tyre = RGB(0x2E, 0x2C, 0x30), seat = RGB(0x3A, 0x32, 0x2E);
            Rect(t, 1, 0, 18, 1, new Color32(0x1A, 0x14, 0x10, 70));                // 接触落影
            for (int i = 0; i < 2; i++)                                             // 双 U 形架
            {
                int x0 = 2 + i * 11;
                VLine(t, x0, 1, 7, steel); VLine(t, x0 + 5, 1, 7, steel);
                HLine(t, 8, x0, x0 + 5, steelL);
                P(t, x0, 7, steelD); P(t, x0 + 5, 7, steelD);
            }
            Frame(t, 5, 2, 5, 5, tyre); Frame(t, 12, 2, 5, 5, tyre);               // 双轮
            P(t, 7, 4, steelD); P(t, 14, 4, steelD);
            HLine(t, 6, 8, 14, frame); VLine(t, 11, 4, 9, frame);                   // 车架+立管
            HLine(t, 9, 9, 12, seat); P(t, 15, 9, frame); P(t, 16, 10, frame);      // 座垫+车把
            Apply(t);
            return Finish(t, "bike_rack");
        }

        /// <summary>街边花箱(18×12):木箱+培土+三色花簇。</summary>
        public static Sprite PlanterBox()
        {
            Sprite cached;
            if (TryGetCached("planter_box", out cached)) return cached;
            Texture2D t = NewTex(18, 12);
            Color wood = RGB(0x8A, 0x62, 0x3E), woodD = RGB(0x64, 0x46, 0x2A), woodL = RGB(0xAA, 0x80, 0x54);
            Color soil = RGB(0x4E, 0x3E, 0x30), leaf = RGB(0x4A, 0x86, 0x3E);
            Rect(t, 1, 0, 16, 1, new Color32(0x1A, 0x14, 0x10, 70));                // 接触落影
            Rect(t, 1, 1, 16, 6, wood); Frame(t, 1, 1, 16, 6, woodD);
            for (int x = 3; x < 16; x += 4) VLine(t, x, 2, 5, woodL);               // 木板缝
            Rect(t, 2, 7, 14, 1, soil);                                             // 培土
            for (int x = 2; x < 16; x++) P(t, x, 8, (x % 3 == 0) ? Shade(leaf, 1.15f) : leaf);
            P(t, 4, 9, RGB(0xE2, 0x7A, 0xB8)); P(t, 5, 10, RGB(0xE2, 0x7A, 0xB8));  // 花簇
            P(t, 8, 9, RGB(0xF2, 0xD4, 0x5A)); P(t, 9, 10, RGB(0xF2, 0xD4, 0x5A));
            P(t, 12, 9, RGB(0xE8, 0x8A, 0x4A)); P(t, 13, 10, RGB(0xE8, 0x8A, 0x4A));
            P(t, 6, 9, leaf); P(t, 11, 9, leaf); P(t, 14, 9, leaf);
            Apply(t);
            return Finish(t, "planter_box");
        }

        /// <summary>公园凉亭(30×34):六柱木亭+双坡瓦顶+顶饰;唯一带碰撞的街具(基座 1.6×0.8)。</summary>
        public static Sprite Gazebo()
        {
            Sprite cached;
            if (TryGetCached("gazebo", out cached)) return cached;
            Texture2D t = NewTex(30, 34);
            Color wood = RGB(0x8A, 0x62, 0x3E), woodD = RGB(0x60, 0x42, 0x28), woodL = RGB(0xB0, 0x86, 0x58);
            Color tile = RGB(0x6E, 0x50, 0x46), tileL = RGB(0x94, 0x6E, 0x5E), stone = RGB(0xB8, 0xB0, 0x9C);
            Rect(t, 2, 0, 26, 2, new Color32(0x1A, 0x14, 0x10, 66));                // 接触落影
            Rect(t, 3, 2, 24, 3, stone); Frame(t, 3, 2, 24, 3, Shade(stone, 0.76f)); // 石台基
            HLine(t, 4, 4, 25, Shade(stone, 1.08f));
            int[] postX = { 4, 10, 19, 25 };                                        // 四根前后柱
            for (int i = 0; i < postX.Length; i++)
            {
                VLine(t, postX[i], 5, 20, wood); VLine(t, postX[i] + 1, 5, 20, woodD);
                P(t, postX[i], 20, woodL);
            }
            Rect(t, 4, 14, 23, 1, woodD);                                           // 座椅横板(亭内)
            Rect(t, 5, 15, 21, 1, woodL);
            Rect(t, 2, 21, 26, 2, woodD);                                           // 檐梁
            GableRoof(t, 1, 28, 23, 32, tile, null);                                // 双坡瓦顶
            HLine(t, 24, 3, 26, tileL);
            P(t, 14, 33, woodL); P(t, 15, 33, woodL);                               // 顶饰
            Apply(t);
            return Finish(t, "gazebo");
        }

        /// <summary>道具椭圆落影(门柱/公告板接地用),半透黑 16×6。</summary>
        public static Sprite PropShadow()
        {
            Sprite cached;
            if (TryGetCached("prop_shadow", out cached)) return cached;
            Texture2D t = NewTex(16, 6);
            Color c = new Color32(0x1A, 0x14, 0x10, 72);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 6; y++)
                {
                    float dx = (x - 7.5f) / 7.5f, dy = (y - 2.5f) / 2.5f;
                    if (dx * dx + dy * dy <= 1f) P(t, x, y, c);
                }
            Apply(t);
            return Finish(t, "prop_shadow");
        }

        // V0.8.2d 已撤的 CourtLamp()/GateArch() 于 V0.9 前置清理(撤后零调用,母版 §186-3)。

        // ————— V0.8.3 公共资本看得见(PRD §176):路灯夜辉/公园周界/公交候车亭/学校扩建/净化塔 —————

        /// <summary>路灯夜辉:14×14 暖光晕,α 由 AtmosphereDirector 随夜幕驱动;
        /// 必须挂不受 Light2D 压暗的 unlit 材质(同橱窗窗光纪律)。灯杆常驻,建成前不挂本辉。</summary>
        public static Sprite LampGlow()
        {
            Sprite cached;
            if (TryGetCached("lamp_glow", out cached)) return cached;
            Texture2D t = NewTex(14, 14);
            Color core = new Color32(0xFF, 0xF0, 0xBE, 235), mid = new Color32(0xFF, 0xE0, 0x9A, 150),
                edge = new Color32(0xFF, 0xD8, 0x84, 60);
            for (int y = 0; y < 14; y++)
                for (int x = 0; x < 14; x++)
                {
                    float d = Mathf.Sqrt((x - 6.5f) * (x - 6.5f) + (y - 6.5f) * (y - 6.5f));
                    if (d <= 2.2f) P(t, x, y, core);
                    else if (d <= 4.4f) P(t, x, y, mid);
                    else if (d <= 6.6f) P(t, x, y, edge);
                }
            Apply(t);
            return Finish(t, "lamp_glow");
        }

        /// <summary>公园周界:80×52 矮篱围合+四角砖墩+南面入口豁口,内部镂空透出草地;建成才摆,预留态只有木牌。</summary>
        public static Sprite ParkPerimeter()
        {
            Sprite cached;
            if (TryGetCached("park_perimeter", out cached)) return cached;
            Texture2D t = NewTex(80, 52);
            Color g1 = RGB(0x46, 0x82, 0x3A), g2 = RGB(0x38, 0x68, 0x30), gL = RGB(0x66, 0xA4, 0x50), gD = RGB(0x2C, 0x52, 0x26);
            Color brick = RGB(0x9A, 0x6A, 0x3A), brickD = RGB(0x74, 0x4E, 0x2C), cap = RGB(0xC8, 0xC2, 0xAA), soil = RGB(0x54, 0x44, 0x34);
            Color bloomR = RGB(0xC8, 0x54, 0x54), bloomW = RGB(0xE8, 0xE0, 0xCE);
            Rect(t, 2, 0, 76, 2, soil); Rect(t, 2, 50, 76, 2, soil);                 // 南北培土沿
            Rect(t, 2, 2, 31, 10, g2); Rect(t, 47, 2, 31, 10, g2);                   // 南篱(中央豁口 x33..46)
            Rect(t, 2, 40, 76, 10, g2);                                              // 北篱
            Rect(t, 2, 12, 10, 28, g2); Rect(t, 68, 12, 10, 28, g2);                 // 西东篱列
            for (int x = 4; x < 76; x += 6)                                          // 修剪起伏与受光
            {
                int top = x % 12 < 6 ? 10 : 9;
                if (x >= 4 && x < 31) VLine(t, x, 3, top, g1);
                if (x >= 48 && x < 76) VLine(t, x, 3, top, g1);
                VLine(t, x, 41, top + 39 > 49 ? 49 : top + 39, g1);
                P(t, x, top + 1, gL);
            }
            for (int y = 14; y < 38; y += 6) { HLine(t, y, 4, 10, g1); HLine(t, y, 70, 76, g1); P(t, 5, y + 1, gL); P(t, 71, y + 1, gL); }
            Rect(t, 0, 0, 8, 14, brick); Frame(t, 0, 0, 8, 14, brickD);              // 四角砖墩
            Rect(t, 72, 0, 8, 14, brick); Frame(t, 72, 0, 8, 14, brickD);
            Rect(t, 0, 38, 8, 14, brick); Frame(t, 0, 38, 8, 14, brickD);
            Rect(t, 72, 38, 8, 14, brick); Frame(t, 72, 38, 8, 14, brickD);
            Rect(t, 0, 14, 8, 2, cap); Rect(t, 72, 14, 8, 2, cap);
            HLine(t, 14, 1, 7, brickD); HLine(t, 14, 73, 79, brickD);                // 墩身砖缝
            Rect(t, 30, 2, 3, 2, cap); Rect(t, 47, 2, 3, 2, cap);                    // 豁口压顶石
            P(t, 12, 8, bloomR); P(t, 24, 5, bloomW); P(t, 58, 7, bloomR); P(t, 70, 44, bloomW); P(t, 20, 45, bloomR); P(t, 40, 46, bloomW);
            VLine(t, 12, 2, 11, gD); VLine(t, 68, 2, 11, gD);                        // 内缘阴影
            Apply(t);
            return Finish(t, "park_perimeter");
        }

        /// <summary>公交候车亭:36×30,悬挑顶棚+双立柱+玻璃背板+时刻牌+木座椅;带碰撞体(基座)。</summary>
        public static Sprite BusShelter()
        {
            Sprite cached;
            if (TryGetCached("bus_shelter", out cached)) return cached;
            Texture2D t = NewTex(36, 30);
            Color roof = RGB(0x3E, 0x70, 0x6C), roofD = RGB(0x2E, 0x54, 0x50), roofL = RGB(0x5A, 0x9A, 0x92);
            Color metal = RGB(0x4A, 0x50, 0x58), metalD = RGB(0x34, 0x3A, 0x42);
            Color glass = new Color32(0x9A, 0xC4, 0xD4, 120), glassF = RGB(0x74, 0x84, 0x8E);
            Color wood = RGB(0xA6, 0x6B, 0x38), woodD = RGB(0x6B, 0x43, 0x28), paper = RGB(0xE8, 0xE4, 0xD8);
            Rect(t, 0, 24, 36, 5, roof); HLine(t, 24, 0, 35, roofL); HLine(t, 28, 0, 35, roofD);   // 悬挑顶棚
            Rect(t, 2, 4, 3, 20, metal); Rect(t, 31, 4, 3, 20, metal);                             // 立柱
            Rect(t, 1, 2, 5, 3, metalD); Rect(t, 30, 2, 5, 3, metalD);                             // 柱脚
            Rect(t, 6, 12, 24, 11, glass); Frame(t, 6, 12, 24, 11, glassF);                       // 玻璃背板
            VLine(t, 17, 12, 22, glassF); VLine(t, 28, 12, 22, glassF);
            Rect(t, 24, 6, 10, 9, paper); Frame(t, 24, 6, 10, 9, metalD);                          // 时刻牌
            HLine(t, 8, 25, 32, metalD); HLine(t, 10, 25, 33, metalD); HLine(t, 12, 25, 31, metalD);
            Rect(t, 6, 4, 24, 3, woodD); Rect(t, 7, 5, 22, 2, wood);                               // 座椅
            Rect(t, 7, 0, 2, 4, metalD); Rect(t, 27, 0, 2, 4, metalD);
            Rect(t, 3, 0, 30, 1, new Color32(0x1A, 0x14, 0x10, 60));                               // 接触落影
            Apply(t);
            return Finish(t, "bus_shelter");
        }

        /// <summary>社区学校扩建屋冠:64×26,内嵌在常驻校舍屋顶；只改变公共项目建成后的外观。</summary>
        public static Sprite SchoolAnnex()
        {
            Sprite cached;
            if (TryGetCached("school_annex", out cached)) return cached;
            Texture2D t = NewTex(64, 26);
            Color ivory = RGB(0xE8, 0xDD, 0xBE), tealR = RGB(0x3E, 0x5A, 0x67), gold = RGB(0xD7, 0xA5, 0x3C);
            Color glass = RGB(0x4C, 0x6C, 0x82), frame = RGB(0xA9, 0x98, 0x76);
            Rect(t, 15, 0, 34, 12, ivory); Frame(t, 15, 0, 34, 12, frame);                         // 新阅览室
            for (int x = 19; x <= 39; x += 10)
            {
                Rect(t, x, 2, 7, 7, glass); Frame(t, x, 2, 7, 7, frame);
                VLine(t, x + 3, 3, 7, Shade(glass, 1.18f));
            }
            GableRoof(t, 11, 53, 12, 22, tealR, new List<SnowBand>());                            // 小山花屋冠
            Rect(t, 28, 18, 8, 6, ivory); Frame(t, 28, 18, 8, 6, frame);
            Rect(t, 30, 19, 4, 3, tealR); P(t, 31, 20, gold); P(t, 32, 20, gold);                // 钟面
            P(t, 31, 25, gold); P(t, 32, 25, gold);
            Apply(t);
            return Finish(t, "school_annex");
        }

        /// <summary>环保净化塔:22×46,绿色洗涤塔+钢箍+检修梯+顶部透气罩+东南向管道(指向工厂),带小碰撞体。</summary>
        public static Sprite PurificationTower()
        {
            Sprite cached;
            if (TryGetCached("purification_tower", out cached)) return cached;
            Texture2D t = NewTex(22, 46);
            Color green = RGB(0x4D, 0x86, 0x7E), greenD = RGB(0x3A, 0x68, 0x62), greenL = RGB(0x66, 0xA4, 0x9A);
            Color steel = RGB(0x6E, 0x74, 0x7E), steelD = RGB(0x52, 0x58, 0x62);
            Color vent = new Color32(0x9A, 0xC4, 0xD4, 140);
            Rect(t, 2, 0, 18, 4, steelD); Frame(t, 2, 0, 18, 4, RGB(0x3E, 0x44, 0x4E));            // 基座
            Rect(t, 5, 4, 12, 32, green); Frame(t, 5, 4, 12, 32, greenD);                          // 塔身
            VLine(t, 15, 5, 34, greenL);                                                           // 右缘受光
            for (int y = 10; y < 34; y += 8) { Rect(t, 4, y, 14, 2, steel); P(t, 4, y, steelD); P(t, 17, y, steelD); }  // 钢箍
            Rect(t, 4, 36, 14, 3, steel); HLine(t, 35, 4, 17, steelD);                             // 顶罩
            Rect(t, 8, 39, 6, 4, steelD); Rect(t, 9, 40, 4, 3, vent);                              // 顶部透气口
            VLine(t, 3, 6, 34, steelD);                                                            // 检修梯
            for (int y = 8; y < 34; y += 5) P(t, 4, y, steelD);
            Rect(t, 17, 2, 5, 3, steel); P(t, 17, 2, steelD); P(t, 21, 3, steelD); P(t, 21, 4, steelD);   // 东南向管道
            Rect(t, 3, 0, 16, 1, new Color32(0x1A, 0x14, 0x10, 60));                               // 接触落影
            Apply(t);
            return Finish(t, "purification_tower");
        }

        /// <summary>花坛砖(V0.8.2b 二稿):石缘+亮土+双层花(茎+冠)+叶丛,16×16 地砖——远看是花丛近看有结构。</summary>
        public static Sprite FlowerbedTile()
        {
            Sprite cached;
            if (TryGetCached("tile_flowerbed", out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color stone = RGB(0x9A, 0x94, 0x88), stoneD = RGB(0x7E, 0x78, 0x6C), soil = RGB(0x6A, 0x54, 0x3E), soilD = RGB(0x58, 0x44, 0x32);
            Color leaf = RGB(0x46, 0x82, 0x3A), leafD = RGB(0x38, 0x68, 0x30);
            Color bloomR = RGB(0xD8, 0x58, 0x58), bloomY = RGB(0xE8, 0xB8, 0x50), bloomW = RGB(0xF0, 0xE8, 0xD8);
            Rect(t, 0, 0, 16, 16, stone); Frame(t, 0, 0, 16, 16, stoneD);           // 石缘
            P(t, 4, 0, stoneD); P(t, 11, 0, stoneD); P(t, 0, 6, stoneD); P(t, 15, 9, stoneD); // 缘石接缝
            Rect(t, 2, 2, 12, 12, soil);                                            // 培土
            for (int i = 0; i < 7; i++) P(t, 3 + (i * 5) % 10, 3 + (i * 7) % 10, soilD); // 土粒
            P(t, 6, 5, leaf); P(t, 11, 4, leaf); P(t, 4, 6, leaf); P(t, 8, 9, leaf); // 叶丛
            P(t, 12, 9, leafD); P(t, 6, 11, leafD); P(t, 9, 13, leaf); P(t, 3, 12, leafD);
            P(t, 4, 4, leafD); P(t, 4, 3, bloomR);                                  // 花:茎+冠(双层)
            P(t, 9, 3, bloomY); P(t, 9, 4, leafD);
            P(t, 12, 7, bloomW); P(t, 12, 8, leafD);
            P(t, 3, 9, bloomR); P(t, 3, 10, leafD);
            P(t, 7, 8, bloomY); P(t, 7, 9, leafD);
            P(t, 10, 11, bloomR); P(t, 10, 12, leafD);
            P(t, 5, 13, bloomW); P(t, 13, 12, bloomR);
            Apply(t);
            return Finish(t, "tile_flowerbed");
        }

        /// <summary>前庭石砖(V0.8.2b):冷灰色磨光石板+受光斜缘,16×16——与广场暖棕砖区分出"官家地面"。</summary>
        public static Sprite ForecourtStoneTile()
        {
            Sprite cached;
            if (TryGetCached("tile_forecourt", out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color base_ = RGB(0xCE, 0xC8, 0xB6), hi = RGB(0xDE, 0xD8, 0xC6), lo = RGB(0xAE, 0xA6, 0x92);
            Rect(t, 0, 0, 16, 16, base_);
            HLine(t, 14, 0, 15, hi); VLine(t, 14, 1, 15, hi);                        // 受光缘(上/右)
            HLine(t, 0, 0, 15, lo); VLine(t, 0, 0, 0, lo);                           // 背光缘(下)
            P(t, 0, 0, lo); P(t, 5, 8, lo); P(t, 6, 8, lo); P(t, 10, 3, lo);         // 石斑
            P(t, 3, 12, hi); P(t, 12, 11, hi);
            Apply(t);
            return Finish(t, "tile_forecourt");
        }

        // —— V0.9.7 果汁店店内(不进建筑剪影体系:地板/墙是 Tile,家具由场景层摆放)——

        // —— V0.9.11 店内主题 tile:四件套加 theme 档(照 Grass(season,variant) 键后缀先例)。
        // theme 0=果汁店原样(键与绘制逐字节不变,像素级回归);1=暖炉餐厅;2=星光电影院;3=原料市场。
        // 绘制语句全主题共用,theme 只换色板——保证几何/纹理结构跨店一致。

        /// <summary>店内木地板(V0.9.7 三稿):16×16 两块 8px 长板条、低对比板缝+顺纹木纹,
        /// 不留竖拼缝——首稿 4px 板条读作砖墙,二稿每格竖拼缝仍有砖格感(实机截图复看)。
        /// V0.9.11 theme:1 餐厅奶油/浅棕双色拼板(两变体换底色即棋盘);2 影院墨绿地毯+金碎星;
        /// 3 市场磨面石板+裂纹。V0.9.12 theme:4 茶室浅茶木;5 家暖黄木;6 便利屋白亮瓷砖棋盘;
        /// 7 书店深木;8 花店粉白砖棋盘;9 零嘴亭橙黄砖。V0.9.13 theme:10 银行淡青灰瓷砖;
        /// 11 央行大理石白灰;12 市政厅暗红毯;13 培训中心浅灰绿砖;14 工厂水泥——
        /// 一律素色低对比(V0.9.11b 复看纪律)。</summary>
        public static Sprite InteriorFloorTile(int variant)
        {
            return InteriorFloorTile(0, variant);
        }

        public static Sprite InteriorFloorTile(int theme, int variant)
        {
            string key = theme == 0
                ? "tile_interior_floor_" + variant
                : "tile_interior_floor_" + variant + "_t" + theme;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color wood, woodD, woodL, seam;
            if (theme == 1)
            {
                // 餐厅:两变体换底色→(x*7+y*5)%2 交铺天然成奶油/浅棕棋盘
                wood = variant == 0 ? RGB(0xF3, 0xE4, 0xC2) : RGB(0xE3, 0xD2, 0xAC);
                woodD = RGB(0xD4, 0xC2, 0x9E); woodL = RGB(0xFB, 0xF3, 0xDC); seam = RGB(0xC9, 0xB4, 0x94);
            }
            else if (theme == 2)
            {
                // 影院:素色暗紫观影毯(用户复看:金碎星太花里胡哨)——三色同系,只留低对比织纹
                wood = RGB(0x3A, 0x2A, 0x48); woodD = RGB(0x34, 0x26, 0x40); woodL = RGB(0x40, 0x2F, 0x50); seam = RGB(0x2E, 0x21, 0x3C);
            }
            else if (theme == 3)
            {
                // 市场:磨面石板两变体+石缝灰
                wood = variant == 0 ? RGB(0xB5, 0xAD, 0x9F) : RGB(0xA7, 0x9E, 0x8F);
                woodD = RGB(0x9A, 0x91, 0x84); woodL = RGB(0xC5, 0xBD, 0xB0); seam = RGB(0x8F, 0x86, 0x7A);
            }
            else if (theme == 4)
            {
                // 茶室:浅茶木,比果汁店(theme 0)浅一档偏黄
                wood = RGB(0xD8, 0xC4, 0x9A); woodD = RGB(0xC6, 0xB0, 0x88); woodL = RGB(0xE4, 0xD2, 0xAC); seam = RGB(0xB4, 0x9E, 0x76);
            }
            else if (theme == 5)
            {
                // 我的家:暖黄木(家用地板比商铺暖)
                wood = RGB(0xE0, 0xC7, 0x96); woodD = RGB(0xD0, 0xB4, 0x82); woodL = RGB(0xEC, 0xD6, 0xA8); seam = RGB(0xBE, 0xA2, 0x70);
            }
            else if (theme == 6)
            {
                // 便利屋:白亮瓷砖两变体棋盘+灰缝
                wood = variant == 0 ? RGB(0xE9, 0xE9, 0xE4) : RGB(0xDD, 0xDD, 0xD6);
                woodD = RGB(0xD2, 0xD2, 0xCA); woodL = RGB(0xF2, 0xF2, 0xEC); seam = RGB(0xC6, 0xC6, 0xBE);
            }
            else if (theme == 7)
            {
                // 书店:深棕木(书房色,比果汁店深一档)
                wood = RGB(0x9C, 0x7A, 0x50); woodD = RGB(0x8C, 0x6C, 0x44); woodL = RGB(0xAA, 0x88, 0x5C); seam = RGB(0x78, 0x5A, 0x38);
            }
            else if (theme == 8)
            {
                // 花店:粉白砖两变体棋盘
                wood = variant == 0 ? RGB(0xF2, 0xE6, 0xE2) : RGB(0xE6, 0xD6, 0xD2);
                woodD = RGB(0xD8, 0xC6, 0xC2); woodL = RGB(0xF8, 0xEE, 0xEC); seam = RGB(0xCC, 0xBA, 0xB6);
            }
            else if (theme == 9)
            {
                // 零嘴亭:橙黄地砖
                wood = RGB(0xF2, 0xD8, 0x94); woodD = RGB(0xE4, 0xC6, 0x82); woodL = RGB(0xF8, 0xE2, 0xA4); seam = RGB(0xD4, 0xB6, 0x72);
            }
            else if (theme == 10)
            {
                // 小镇银行:淡青灰瓷砖两变体棋盘+灰缝(营业厅的干净冷调)
                wood = variant == 0 ? RGB(0xE8, 0xEC, 0xEA) : RGB(0xDD, 0xE2, 0xDF);
                woodD = RGB(0xD0, 0xD6, 0xD2); woodL = RGB(0xF0, 0xF4, 0xF1); seam = RGB(0xC6, 0xCC, 0xC8);
            }
            else if (theme == 11)
            {
                // 中央银行:大理石白灰两变体棋盘+米缝(柱廊大厅的气派)
                wood = variant == 0 ? RGB(0xF0, 0xED, 0xE6) : RGB(0xE2, 0xDF, 0xD6);
                woodD = RGB(0xD6, 0xD2, 0xC8); woodL = RGB(0xF8, 0xF5, 0xEE); seam = RGB(0xCC, 0xC6, 0xBA);
            }
            else if (theme == 12)
            {
                // 市政厅:暗红地毯两变体低对比拼花(政务大厅,红要压暗不艳;
                // 复看返工:首稿饱和度偏高,整体再压暗一档)
                wood = variant == 0 ? RGB(0x74, 0x32, 0x2E) : RGB(0x6A, 0x2E, 0x2A);
                woodD = RGB(0x5C, 0x24, 0x20); woodL = RGB(0x80, 0x3A, 0x34); seam = RGB(0x50, 0x1C, 0x18);
            }
            else if (theme == 13)
            {
                // 培训中心:浅灰绿教室地砖两变体棋盘
                wood = variant == 0 ? RGB(0xE2, 0xE8, 0xE0) : RGB(0xD6, 0xDC, 0xD4);
                woodD = RGB(0xC8, 0xCE, 0xC6); woodL = RGB(0xEA, 0xF0, 0xE8); seam = RGB(0xBE, 0xC4, 0xBC);
            }
            else if (theme == 14)
            {
                // 工厂:水泥地两变体+防滑纹(车间灰色系)
                wood = variant == 0 ? RGB(0xB8, 0xB8, 0xB4) : RGB(0xAC, 0xAC, 0xA8);
                woodD = RGB(0x9E, 0x9E, 0x9A); woodL = RGB(0xC2, 0xC2, 0xBE); seam = RGB(0x94, 0x94, 0x90);
            }
            else
            {
                wood = RGB(0xB4, 0x86, 0x56); woodD = RGB(0xA4, 0x78, 0x4A); woodL = RGB(0xC2, 0x96, 0x62); seam = RGB(0x8E, 0x66, 0x40);
            }
            Rect(t, 0, 0, 16, 16, wood);
            HLine(t, 0, 0, 15, seam); HLine(t, 8, 0, 15, seam);                        // 板缝(低对比)
            HLine(t, 3, 1, 15, woodD); HLine(t, 5, 0, 14, woodD);                      // 顺纹木纹
            HLine(t, 11, 1, 15, woodD); HLine(t, 13, 0, 12, woodD);
            if (variant == 0) { P(t, 4, 4, woodD); P(t, 11, 12, woodD); P(t, 7, 6, woodL); }
            else { P(t, 9, 3, woodD); P(t, 2, 13, woodD); P(t, 13, 7, woodL); P(t, 5, 10, woodD); }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>店内墙面(V0.9.7):奶油灰泥+稀疏斑驳,16×16——上墙两行,窗位另用 InteriorWindowTile。
        /// V0.9.11 theme:1 餐厅奶油灰泥+桁架木点;2 影院墨紫灰泥+紫斑;3 市场白灰墙+木梁点。</summary>
        public static Sprite InteriorWallTile()
        {
            return InteriorWallTile(0);
        }

        public static Sprite InteriorWallTile(int theme)
        {
            string key = theme == 0 ? "tile_interior_wall" : "tile_interior_wall_t" + theme;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color plaster = InteriorWallPalette(theme, 0), plasterD = InteriorWallPalette(theme, 1), plasterL = InteriorWallPalette(theme, 2);
            Rect(t, 0, 0, 16, 16, plaster);
            P(t, 3, 9, plasterD); P(t, 11, 4, plasterD); P(t, 7, 13, plasterD); P(t, 5, 2, plasterD);
            P(t, 13, 11, plasterL); P(t, 2, 3, plasterL); P(t, 9, 7, plasterL);
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>店内墙窗(V0.9.7):灰泥墙+木框方窗(中梃十字+玻璃反光+窗台),16×16——嵌在上墙下排。
        /// V0.9.11 theme 只换框色:1 餐厅桁架木框;2 影院金框;3 市场珊瑚框。</summary>
        public static Sprite InteriorWindowTile()
        {
            return InteriorWindowTile(0);
        }

        public static Sprite InteriorWindowTile(int theme)
        {
            string key = theme == 0 ? "tile_interior_window" : "tile_interior_window_t" + theme;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color plaster = InteriorWallPalette(theme, 0);
            Color frame = theme == 1 ? RGB(0x7A, 0x52, 0x30)
                : theme == 2 ? RGB(0xC9, 0xA4, 0x4A)
                : theme == 3 ? RGB(0xE2, 0x6A, 0x4E)
                : theme == 4 ? RGB(0x6E, 0x8B, 0x5A)     // V0.9.12 茶室抹茶框
                : theme == 5 ? RGB(0xB0, 0x7A, 0x4A)     // 我的家暖橙木框
                : theme == 6 ? RGB(0x7A, 0x88, 0x94)     // 便利屋蓝灰框
                : theme == 7 ? RGB(0x5E, 0x40, 0x28)     // 书店深木框
                : theme == 8 ? RGB(0xC9, 0x8B, 0x96)     // 花店玫瑰粉框
                : theme == 9 ? RGB(0xE0, 0x9A, 0x4A)     // 零嘴亭橙框
                : theme == 10 ? RGB(0x6E, 0x50, 0x30)    // V0.9.13 小镇银行胡桃深木框
                : theme == 11 ? RGB(0xA4, 0x84, 0x3A)    // 中央银行金棕框
                : theme == 12 ? RGB(0x7E, 0x38, 0x32)    // 市政厅深红木框
                : theme == 13 ? RGB(0x44, 0x68, 0x4C)    // 培训中心墨绿框(黑板呼应)
                : theme == 14 ? RGB(0x5A, 0x6C, 0x80)    // 工厂钢蓝框
                : RGB(0x8A, 0x5E, 0x38);
            // theme 0 框色保持 V0.9.7 原值(像素级回归);主题档才用 Shade 推导
            Color frameD = theme == 0 ? RGB(0x6A, 0x44, 0x28) : Shade(frame, .78f);
            Color sill = theme == 0 ? RGB(0x9A, 0x6E, 0x44) : Shade(frame, 1.18f);
            Color glass = RGB(0x54, 0x74, 0x8E), glassL = RGB(0x7E, 0x9E, 0xB4);
            Rect(t, 0, 0, 16, 16, plaster);
            Rect(t, 3, 3, 10, 10, frame);
            Rect(t, 4, 4, 8, 8, glass);
            VLine(t, 7, 4, 11, frameD); HLine(t, 7, 4, 11, frameD);                    // 中梃十字
            P(t, 5, 9, glassL); P(t, 6, 8, glassL);                                    // 玻璃反光
            Rect(t, 2, 2, 12, 1, sill);                                                // 窗台
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>店内墙裙(V0.9.7):深木护墙板+面板凹槽+顶栏,16×16——上墙贴地一行,
        /// 让墙带读作「墙」而不是一条色带。V0.9.11 theme:1 餐厅桁架木;2 影院紫绒;3 市场原木。</summary>
        public static Sprite InteriorWainscotTile()
        {
            return InteriorWainscotTile(0);
        }

        public static Sprite InteriorWainscotTile(int theme)
        {
            string key = theme == 0 ? "tile_interior_wainscot" : "tile_interior_wainscot_t" + theme;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 16);
            Color panel, rail;
            if (theme == 1) { panel = RGB(0x7A, 0x52, 0x30); rail = RGB(0x9A, 0x6B, 0x3E); }
            else if (theme == 2) { panel = RGB(0x70, 0x58, 0x8A); rail = RGB(0x8A, 0x70, 0xA8); }
            else if (theme == 3) { panel = RGB(0x7A, 0x54, 0x36); rail = RGB(0x94, 0x70, 0x4E); }
            else if (theme == 4) { panel = RGB(0x86, 0xA0, 0x6C); rail = RGB(0x9C, 0xB2, 0x7E); }   // V0.9.12 茶室抹茶
            else if (theme == 5) { panel = RGB(0xB0, 0x7A, 0x4A); rail = RGB(0xC4, 0x92, 0x58); }   // 我的家暖橙木
            else if (theme == 6) { panel = RGB(0x7E, 0x92, 0xA0); rail = RGB(0x94, 0xA8, 0xB4); }   // 便利屋蓝灰
            else if (theme == 7) { panel = RGB(0x6B, 0x4A, 0x2E); rail = RGB(0x82, 0x62, 0x42); }   // 书店深棕
            else if (theme == 8) { panel = RGB(0xC9, 0x8B, 0x96); rail = RGB(0xD8, 0xA2, 0xAA); }   // 花店玫瑰粉
            else if (theme == 9) { panel = RGB(0xE0, 0x9A, 0x4A); rail = RGB(0xE8, 0xAE, 0x64); }   // 零嘴亭橙
            else if (theme == 10) { panel = RGB(0x5E, 0x44, 0x26); rail = RGB(0x74, 0x58, 0x2E); }  // V0.9.13 小镇银行深胡桃
            else if (theme == 11) { panel = RGB(0x8A, 0x6A, 0x2E); rail = RGB(0xA4, 0x84, 0x3A); }  // 中央银行金棕
            else if (theme == 12) { panel = RGB(0x6E, 0x2A, 0x24); rail = RGB(0x84, 0x38, 0x30); }  // 市政厅深红木
            else if (theme == 13) { panel = RGB(0x3E, 0x5A, 0x44); rail = RGB(0x4C, 0x6C, 0x52); }  // 培训中心墨绿
            else if (theme == 14) { panel = RGB(0x46, 0x58, 0x6C); rail = RGB(0x56, 0x68, 0x80); }  // 工厂钢蓝
            else { panel = RGB(0x6A, 0x44, 0x28); rail = RGB(0x8A, 0x5E, 0x38); }
            Color panelD = theme == 0 ? RGB(0x54, 0x34, 0x1E) : Shade(panel, .78f);   // theme 0 保持 V0.9.7 原值
            Rect(t, 0, 0, 16, 16, panel);
            Rect(t, 0, 13, 16, 3, rail); HLine(t, 13, 0, 15, panelD);                  // 顶栏
            for (int x = 3; x < 16; x += 5) VLine(t, x, 1, 12, panelD);                // 面板凹槽
            P(t, 1, 6, rail); P(t, 6, 9, rail); P(t, 11, 4, rail);                     // 木纹高光
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>店内墙色板取色(V0.9.11):窗 tile 的底墙与墙 tile 同源,单点取色避免两处漂移。
        /// V0.9.12 追加 theme 4-9(茶室/家/便利屋/书店/花店/零嘴亭)。
        /// V0.9.13 追加 theme 10-14(银行/央行/市政厅/培训中心/工厂)。</summary>
        static Color InteriorWallPalette(int theme, int index)
        {
            // index:0=底色 1=斑驳暗 2=高光
            if (theme == 1) return index == 0 ? RGB(0xF3, 0xE4, 0xC2) : index == 1 ? RGB(0x7A, 0x52, 0x30) : RGB(0xFB, 0xF3, 0xDC);
            if (theme == 2) return index == 0 ? RGB(0x38, 0x28, 0x4A) : index == 1 ? RGB(0x2E, 0x20, 0x40) : RGB(0x46, 0x34, 0x58);
            if (theme == 3) return index == 0 ? RGB(0xDC, 0xD4, 0xC4) : index == 1 ? RGB(0x7A, 0x54, 0x36) : RGB(0xE8, 0xE2, 0xD6);
            if (theme == 4) return index == 0 ? RGB(0xEF, 0xE8, 0xD4) : index == 1 ? RGB(0xDF, 0xD6, 0xBE) : RGB(0xF7, 0xF1, 0xE0);
            if (theme == 5) return index == 0 ? RGB(0xF6, 0xEF, 0xDF) : index == 1 ? RGB(0xE6, 0xDC, 0xCA) : RGB(0xFC, 0xF6, 0xE8);
            if (theme == 6) return index == 0 ? RGB(0xF4, 0xF4, 0xF0) : index == 1 ? RGB(0xE4, 0xE4, 0xDE) : RGB(0xFA, 0xFA, 0xF6);
            if (theme == 7) return index == 0 ? RGB(0xE8, 0xDF, 0xC8) : index == 1 ? RGB(0xD8, 0xCC, 0xB0) : RGB(0xF0, 0xE9, 0xD6);
            if (theme == 8) return index == 0 ? RGB(0xF9, 0xF3, 0xEF) : index == 1 ? RGB(0xE9, 0xDE, 0xD8) : RGB(0xFF, 0xF9, 0xF5);
            if (theme == 9) return index == 0 ? RGB(0xF8, 0xEF, 0xD9) : index == 1 ? RGB(0xE8, 0xDC, 0xC2) : RGB(0xFF, 0xF7, 0xE4);
            if (theme == 10) return index == 0 ? RGB(0xF2, 0xEF, 0xE8) : index == 1 ? RGB(0xE2, 0xDE, 0xD4) : RGB(0xFA, 0xF7, 0xF0);   // V0.9.13 小镇银行米白
            if (theme == 11) return index == 0 ? RGB(0xF6, 0xF1, 0xE6) : index == 1 ? RGB(0xE6, 0xE0, 0xD2) : RGB(0xFC, 0xF8, 0xEE);   // 中央银行暖米
            if (theme == 12) return index == 0 ? RGB(0xF0, 0xEC, 0xE2) : index == 1 ? RGB(0xE0, 0xDA, 0xCE) : RGB(0xF8, 0xF4, 0xEA);   // 市政厅米白
            if (theme == 13) return index == 0 ? RGB(0xF4, 0xF2, 0xEC) : index == 1 ? RGB(0xE4, 0xE2, 0xDA) : RGB(0xFA, 0xF8, 0xF2);   // 培训中心白
            if (theme == 14) return index == 0 ? RGB(0xE8, 0xE6, 0xE0) : index == 1 ? RGB(0xD8, 0xD6, 0xD0) : RGB(0xF0, 0xEE, 0xE8);   // 工厂浅灰
            return index == 0 ? RGB(0xE9, 0xDD, 0xC2) : index == 1 ? RGB(0xD9, 0xCB, 0xAC) : RGB(0xF4, 0xEA, 0xD4);
        }

        /// <summary>店内圆桌(V0.9.7):圆桌面+独柱+两侧圆凳,30×22——座位区主件(替代 16px Kenney
        /// 小件:实机截图复看,16px 道具在大房间里小到读不出「家具」)。</summary>
        public static Sprite CafeTable()
        {
            Sprite cached;
            if (TryGetCached("prop_cafe_table", out cached)) return cached;
            Texture2D t = NewTex(30, 22);
            Color wood = RGB(0x9A, 0x6A, 0x40), woodD = RGB(0x76, 0x4E, 0x2E), woodL = RGB(0xB8, 0x86, 0x54);
            Color stool = RGB(0xC2, 0x56, 0x3C), stoolD = RGB(0x9A, 0x40, 0x2E);
            // 两侧圆凳
            Rect(t, 1, 4, 6, 3, stool); Rect(t, 23, 4, 6, 3, stool);
            Rect(t, 3, 1, 2, 3, stoolD); Rect(t, 25, 1, 2, 3, stoolD);
            // 独柱 + 圆桌面(受光前缘)
            Rect(t, 13, 2, 4, 10, woodD);
            Rect(t, 2, 12, 26, 5, wood); Rect(t, 2, 12, 26, 2, woodL); Frame(t, 2, 12, 26, 5, woodD);
            P(t, 6, 14, woodL); P(t, 20, 15, woodL);
            Apply(t);
            return Finish(t, "prop_cafe_table");
        }

        /// <summary>店内货架(V0.9.7):木框三层,摆果汁瓶与备用杯,36×32——靠墙主件,给后墙内容。</summary>
        public static Sprite ShelfUnit()
        {
            Sprite cached;
            if (TryGetCached("prop_shelf_unit", out cached)) return cached;
            Texture2D t = NewTex(36, 32);
            Color wood = RGB(0x8A, 0x5E, 0x38), woodD = RGB(0x6A, 0x44, 0x28), shelf = RGB(0xA8, 0x7A, 0x4C);
            Color juice = RGB(0xF2, 0xA8, 0x4A), juiceD = RGB(0xE8, 0x8A, 0x3A), berry = RGB(0xD9, 0x5A, 0x4A), cup = RGB(0xF2, 0xEE, 0xE2);
            Rect(t, 0, 0, 36, 32, wood); Frame(t, 0, 0, 36, 32, woodD);
            Rect(t, 3, 3, 30, 26, RGB(0x3A, 0x2D, 0x24));                              // 背板暗色
            for (int s = 0; s < 3; s++)
            {
                int y = 4 + s * 9;
                Rect(t, 3, y, 30, 2, shelf);                                           // 层板
                for (int b = 0; b < 5; b++)                                            // 每层五只瓶/杯
                {
                    int bx = 6 + b * 6;
                    Color c = (s + b) % 2 == 0 ? juice : berry;
                    Rect(t, bx, y + 3, 3, 5, c); P(t, bx + 1, y + 8, c);
                    if (b == 4) Rect(t, bx, y + 3, 3, 3, cup);
                    else P(t, bx, y + 3, juiceD);
                }
            }
            Apply(t);
            return Finish(t, "prop_shelf_unit");
        }

        /// <summary>便利店分类货架：0 零食、1 饮料、2 日化、3 速食、4 烟酒柜。
        /// 架体规格与 ShelfUnit 一致，只改陈列和分区色，所以旧布局与碰撞完全不变。</summary>
        public static Sprite ConvenienceShelf(int category)
        {
            category = Mathf.Clamp(category, 0, 4);
            string key = "prop_convenience_shelf_" + category;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;

            Texture2D t = NewTex(36, 32);
            Color frame = RGB(0x6D, 0x78, 0x80), frameD = RGB(0x48, 0x52, 0x5A);
            Color shelf = RGB(0xD8, 0xD9, 0xD3), back = RGB(0xEB, 0xEA, 0xE2);
            Color[] signs =
            {
                RGB(0xD8, 0x6A, 0x4E), RGB(0x52, 0x91, 0xB8), RGB(0x72, 0x9A, 0x68),
                RGB(0xE0, 0xA2, 0x42), RGB(0x78, 0x58, 0x50)
            };
            Color accent = signs[category];
            Rect(t, 0, 0, 36, 32, frame); Frame(t, 0, 0, 36, 32, frameD);
            Rect(t, 3, 3, 30, 24, back);
            Rect(t, 3, 27, 30, 3, accent); HLine(t, 30, 4, 31, Shade(accent, 1.16f));
            for (int row = 0; row < 3; row++)
            {
                int baseY = 5 + row * 8;
                Rect(t, 3, baseY, 30, 2, shelf);
                HLine(t, baseY, 4, 31, frameD);
                for (int item = 0; item < 5; item++)
                {
                    int x = 5 + item * 6;
                    int variant = (row + item) % 3;
                    if (category == 0)                                                // 薯片/糖果彩袋
                    {
                        Color c = variant == 0 ? RGB(0xD9, 0x58, 0x48) : variant == 1 ? RGB(0xE5, 0xBE, 0x45) : RGB(0x61, 0x9A, 0x62);
                        Rect(t, x, baseY + 2, 4, 5, c); P(t, x + 1, baseY + 4, RGB(0xF5, 0xEF, 0xD8));
                    }
                    else if (category == 1)                                           // 水/汽水/果汁瓶
                    {
                        Color c = variant == 0 ? RGB(0x55, 0x9E, 0xC4) : variant == 1 ? RGB(0xE4, 0x8C, 0x3D) : RGB(0x75, 0xAD, 0x72);
                        Rect(t, x + 1, baseY + 2, 3, 5, c); P(t, x + 2, baseY + 7, frameD);
                        HLine(t, baseY + 4, x + 1, x + 3, RGB(0xF1, 0xEF, 0xE6));
                    }
                    else if (category == 2)                                           // 纸巾/洗涤/清洁包装
                    {
                        Color c = variant == 0 ? RGB(0xE8, 0xE3, 0xD4) : variant == 1 ? RGB(0x77, 0xA4, 0xB8) : RGB(0xAA, 0x88, 0xB0);
                        Rect(t, x, baseY + 2, 5, 5, c); Frame(t, x, baseY + 2, 5, 5, Shade(c, .72f));
                        HLine(t, baseY + 4, x + 1, x + 3, RGB(0xF7, 0xF4, 0xEA));
                    }
                    else if (category == 3)                                           // 杯面/罐头/速食碗
                    {
                        Color c = variant == 0 ? RGB(0xD8, 0x62, 0x42) : variant == 1 ? RGB(0xE3, 0xB4, 0x46) : RGB(0x70, 0x91, 0xAD);
                        Rect(t, x, baseY + 2, 5, 4, c); HLine(t, baseY + 6, x, x + 4, RGB(0xF3, 0xE8, 0xC8));
                        P(t, x + 2, baseY + 3, RGB(0xF5, 0xEF, 0xDA));
                    }
                    else                                                              // 收银后方烟酒细长包装
                    {
                        Color c = variant == 0 ? RGB(0xA8, 0x45, 0x3C) : variant == 1 ? RGB(0xD1, 0xA3, 0x42) : RGB(0x4F, 0x68, 0x78);
                        Rect(t, x + 1, baseY + 1, 3, 6, c); HLine(t, baseY + 5, x + 1, x + 3, RGB(0xF0, 0xE2, 0xB8));
                    }
                }
                // 层板价签打破纯色长条，远景也能读出“便利店”。
                P(t, 8 + row * 7, baseY, RGB(0xF7, 0xF2, 0xE6));
                P(t, 9 + row * 7, baseY, RGB(0xD9, 0x58, 0x48));
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>店内果汁冷柜(V0.9.7):白钢身+玻璃门(柜内果汁瓶)+把手,24×34——靠墙主件。</summary>
        public static Sprite FruitCooler()
        {
            Sprite cached;
            if (TryGetCached("prop_fruit_cooler", out cached)) return cached;
            Texture2D t = NewTex(24, 34);
            Color body = RGB(0xE2, 0xE4, 0xE6), bodyD = RGB(0xB8, 0xBC, 0xC2), glass = RGB(0x54, 0x74, 0x8E), glassL = RGB(0x7E, 0x9E, 0xB4);
            Color juice = RGB(0xF2, 0xA8, 0x4A), berry = RGB(0xD9, 0x5A, 0x4A);
            Rect(t, 0, 0, 24, 34, body); Frame(t, 0, 0, 24, 34, bodyD);
            Rect(t, 2, 28, 20, 4, bodyD);                                              // 顶机
            Rect(t, 3, 4, 16, 24, glass); Rect(t, 4, 5, 14, 22, glass);
            for (int s = 0; s < 3; s++)                                                // 三层果汁
            {
                int y = 7 + s * 7;
                Rect(t, 5, y, 12, 1, bodyD);
                Rect(t, 6, y + 2, 3, 4, juice); Rect(t, 10, y + 2, 3, 4, berry); Rect(t, 14, y + 2, 3, 4, juice);
            }
            P(t, 6, 22, glassL); P(t, 8, 18, glassL);                                  // 玻璃反光
            Rect(t, 19, 8, 2, 14, bodyD);                                              // 把手
            Apply(t);
            return Finish(t, "prop_fruit_cooler");
        }

        /// <summary>店内盆栽(V0.9.7):陶盆+双叶团,14×22——墙脚绿意。</summary>
        public static Sprite PottedPlant()
        {
            Sprite cached;
            if (TryGetCached("prop_potted_plant", out cached)) return cached;
            Texture2D t = NewTex(14, 22);
            Color pot = RGB(0xB8, 0x6A, 0x3C), potD = RGB(0x92, 0x50, 0x2C);
            Color leaf = RGB(0x46, 0x82, 0x3A), leafD = RGB(0x36, 0x66, 0x2C);
            Rect(t, 3, 0, 8, 6, pot); Rect(t, 2, 5, 10, 2, potD); Frame(t, 3, 0, 8, 6, potD);
            Rect(t, 3, 12, 8, 8, leaf); Rect(t, 5, 16, 4, 5, leaf);
            P(t, 4, 13, leafD); P(t, 9, 18, leafD); P(t, 7, 20, leaf); P(t, 2, 14, leaf); P(t, 11, 15, leaf);
            Apply(t);
            return Finish(t, "prop_potted_plant");
        }

        /// <summary>收银柜台(V0.9.7):红白条纹前裙+木台面,台上收银机/果汁壶/成品杯——店内交互锚点,
        /// 配色呼应店外雨篷(#D95A4A/#F2EEE2)与 cut1 果汁杯(#F2A84A/#E88A3A)。40×22。</summary>
        public static Sprite JuiceCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_juice_counter", out cached)) return cached;
            Texture2D t = NewTex(40, 22);
            Color wood = RGB(0x8A, 0x5E, 0x38), woodD = RGB(0x6A, 0x44, 0x28), top = RGB(0xC8, 0x98, 0x60);
            Color stripeR = RGB(0xD9, 0x5A, 0x4A), stripeW = RGB(0xF2, 0xEE, 0xE2);
            Color rego = RGB(0x3A, 0x2D, 0x24), regoL = RGB(0x5A, 0x48, 0x3A);
            Color juice = RGB(0xF2, 0xA8, 0x4A), juiceD = RGB(0xE8, 0x8A, 0x3A), cup = RGB(0xF2, 0xEE, 0xE2);
            Rect(t, 0, 4, 40, 12, wood); Frame(t, 0, 4, 40, 12, woodD);                // 柜身
            for (int x = 1; x < 39; x += 6) Rect(t, x, 5, 3, 10, stripeR);             // 红白条纹前裙
            Rect(t, 0, 16, 40, 3, top); HLine(t, 16, 0, 39, woodD); HLine(t, 18, 0, 39, woodD); // 台面板
            Rect(t, 5, 16, 9, 6, rego); Rect(t, 6, 20, 7, 1, regoL);                   // 收银机+显示屏
            P(t, 7, 18, regoL); P(t, 9, 18, regoL); P(t, 11, 18, regoL);               // 按键
            Rect(t, 21, 17, 6, 5, juiceD); Rect(t, 22, 18, 4, 3, juice);               // 果汁壶
            Rect(t, 27, 18, 1, 3, cup);                                                // 壶把
            Rect(t, 32, 17, 4, 5, cup); Rect(t, 33, 19, 2, 2, juice);                  // 成品杯
            VLine(t, 35, 21, 22, juiceD);                                              // 吸管
            Apply(t);
            return Finish(t, "prop_juice_counter");
        }

        /// <summary>榨汁台(V0.9.7):奶油钢身+进料斗+摇柄+接杯,24×26——收银台旁的生产设备陈设。</summary>
        public static Sprite JuicePress()
        {
            Sprite cached;
            if (TryGetCached("prop_juice_press", out cached)) return cached;
            Texture2D t = NewTex(24, 26);
            Color steel = RGB(0xD8, 0xD4, 0xC8), steelD = RGB(0xAE, 0xA8, 0x9A), dark = RGB(0x4A, 0x44, 0x38);
            Color juice = RGB(0xF2, 0xA8, 0x4A), cup = RGB(0xF2, 0xEE, 0xE2);
            Rect(t, 2, 0, 20, 4, steelD); Rect(t, 3, 0, 18, 1, dark);                  // 底座
            Rect(t, 4, 4, 16, 14, steel); Frame(t, 4, 4, 16, 14, steelD);              // 机身
            Rect(t, 7, 18, 10, 5, dark); Rect(t, 8, 22, 8, 1, steelD);                 // 进料斗
            Rect(t, 10, 12, 4, 3, steelD);                                             // 出汁口
            VLine(t, 12, 9, 12, juice);                                                // 汁流
            Rect(t, 9, 5, 6, 4, cup); Rect(t, 10, 7, 4, 1, juice);                     // 接杯
            P(t, 20, 14, dark); P(t, 21, 15, dark); P(t, 22, 16, dark);                // 摇柄
            Apply(t);
            return Finish(t, "prop_juice_press");
        }

        /// <summary>店内门垫(V0.9.7):砖红编织纹门垫,20×10——平铺门口,无碰撞,进出交互锚点。</summary>
        public static Sprite InteriorDoorMat()
        {
            Sprite cached;
            if (TryGetCached("prop_door_mat", out cached)) return cached;
            Texture2D t = NewTex(20, 10);
            Color mat = RGB(0xA8, 0x4A, 0x3C), matD = RGB(0x7E, 0x36, 0x2C), matL = RGB(0xC0, 0x5E, 0x4C);
            Rect(t, 0, 0, 20, 10, mat); Frame(t, 0, 0, 20, 10, matD);
            for (int y = 2; y <= 7; y += 2)
                for (int x = 2 + (y % 4 == 0 ? 0 : 1); x < 18; x += 2) P(t, x, y, y % 4 == 0 ? matD : matL);
            Apply(t);
            return Finish(t, "prop_door_mat");
        }

        /// <summary>货板堆(V0.8.2b):木托盘+双箱+顶箱,22×14——工厂后场装卸面陈设(无碰撞)。</summary>
        public static Sprite PalletStack()
        {
            Sprite cached;
            if (TryGetCached("prop_pallets", out cached)) return cached;
            Texture2D t = NewTex(22, 14);
            Color plank = RGB(0x8A, 0x6A, 0x42), plankD = RGB(0x6A, 0x4E, 0x30);
            Color box = RGB(0xB0, 0x88, 0x50), boxD = RGB(0x7A, 0x58, 0x34), boxL = RGB(0xC8, 0xA0, 0x68);
            Rect(t, 0, 0, 22, 2, plank); HLine(t, 1, 0, 21, plankD);                 // 托盘面板
            for (int x = 1; x < 22; x += 5) Rect(t, x, 0, 2, 2, plankD);             // 盘肋
            Rect(t, 1, 2, 9, 8, box); Frame(t, 1, 2, 9, 8, boxD);                    // 左箱
            HLine(t, 5, 2, 8, boxD); VLine(t, 5, 3, 9, boxD);                        // 封条十字
            Rect(t, 12, 2, 9, 6, box); Frame(t, 12, 2, 9, 6, boxD);                  // 右箱(矮)
            P(t, 13, 7, boxL); P(t, 2, 9, boxL);                                     // 箱面受光
            Rect(t, 5, 10, 8, 4, box); Frame(t, 5, 10, 8, 4, boxD);                  // 顶箱
            P(t, 6, 13, boxL);
            Apply(t);
            return Finish(t, "prop_pallets");
        }

        /// <summary>油桶对(V0.8.2b):钢蓝+锈红两只立桶带箍线,16×12——工厂后场陈设(无碰撞)。</summary>
        public static Sprite OilDrums()
        {
            Sprite cached;
            if (TryGetCached("prop_drums", out cached)) return cached;
            Texture2D t = NewTex(16, 12);
            Color steel = RGB(0x5A, 0x70, 0x80), steelD = RGB(0x42, 0x54, 0x62), steelL = RGB(0x74, 0x8A, 0x9A);
            Color rust = RGB(0x9A, 0x5A, 0x40), rustD = RGB(0x76, 0x42, 0x2E), rustL = RGB(0xB2, 0x6E, 0x50);
            Rect(t, 1, 0, 7, 11, steel); VLine(t, 1, 0, 10, steelD); VLine(t, 7, 0, 10, steelD);
            for (int y = 3; y <= 9; y += 3) HLine(t, y, 2, 6, steelD);               // 桶箍
            P(t, 2, 9, steelL); P(t, 2, 5, steelL);                                  // 受光
            HLine(t, 10, 1, 7, steelL);                                              // 桶口沿
            Rect(t, 8, 0, 7, 10, rust); VLine(t, 8, 0, 9, rustD); VLine(t, 14, 0, 9, rustD);
            for (int y = 2; y <= 8; y += 3) HLine(t, y, 9, 13, rustD);
            P(t, 9, 8, rustL); P(t, 9, 4, rustL);
            HLine(t, 9, 8, 14, rustL);
            P(t, 6, 0, new Color32(0x1A, 0x14, 0x10, 70)); P(t, 13, 0, new Color32(0x1A, 0x14, 0x10, 70)); // 接地
            Apply(t);
            return Finish(t, "prop_drums");
        }

        // —— V0.9.11 打样三店店内家具(暖炉餐厅/星光电影院/原料市场,风格同 V0.9.7 多格自绘件)——

        /// <summary>暖炉(V0.9.11 餐厅):石砌炉体+炉口火焰+炉柴+吊锅钩,26×30——北墙视觉锚点。</summary>
        public static Sprite RestaurantHearth()
        {
            Sprite cached;
            if (TryGetCached("prop_hearth", out cached)) return cached;
            Texture2D t = NewTex(26, 30);
            Color stone = RGB(0x8A, 0x83, 0x78), stoneD = RGB(0x6E, 0x68, 0x5E), stoneL = RGB(0xA2, 0x9A, 0x8E);
            Color fire = RGB(0xE8, 0x8A, 0x3A), fireL = RGB(0xF0, 0xC3, 0x57), fireD = RGB(0xC0, 0x5A, 0x2E);
            Color wood = RGB(0x7A, 0x52, 0x30), dark = RGB(0x2A, 0x1C, 0x14);
            Rect(t, 1, 0, 24, 18, stone); Frame(t, 1, 0, 24, 18, stoneD);              // 石砌炉体
            for (int y = 3; y < 17; y += 5) HLine(t, y, 2, 23, stoneD);                // 砌缝
            P(t, 4, 7, stoneL); P(t, 12, 12, stoneL); P(t, 20, 5, stoneL);             // 石面受光
            Rect(t, 5, 2, 16, 9, dark); Frame(t, 5, 2, 16, 9, stoneD);                 // 炉口(墨膛)
            Rect(t, 7, 3, 3, 3, fireD); Rect(t, 11, 3, 4, 4, fire); P(t, 12, 6, fireL); P(t, 13, 4, fireL); // 火焰
            HLine(t, 6, 7, 19, fireD);                                                 // 炉底炭床
            Rect(t, 8, 7, 2, 2, wood); Rect(t, 12, 7, 2, 2, wood);                     // 炉柴
            Rect(t, 3, 18, 20, 3, stoneD); HLine(t, 21, 3, 22, stoneL);                // 炉台搁板
            VLine(t, 12, 21, 26, dark); Rect(t, 10, 26, 6, 2, dark);                   // 吊钩+烟道口
            HLine(t, 8, 20, 25, stoneD); P(t, 9, 22, stoneL); P(t, 16, 24, stoneL);    // 楣木
            Rect(t, 0, 0, 2, 3, wood); Rect(t, 24, 0, 2, 3, wood);                     // 两侧垫木
            Apply(t);
            return Finish(t, "prop_hearth");
        }

        /// <summary>灶台(V0.9.11 餐厅):铸铁身+双灶眼+双锅+背烟囱,28×26——后厨区主件。</summary>
        public static Sprite KitchenStove()
        {
            Sprite cached;
            if (TryGetCached("prop_stove", out cached)) return cached;
            Texture2D t = NewTex(28, 26);
            Color iron = RGB(0x4A, 0x44, 0x38), ironD = RGB(0x34, 0x30, 0x28), ironL = RGB(0x66, 0x5E, 0x50);
            Color steel = RGB(0x8E, 0x8A, 0x80), pot = RGB(0x3A, 0x36, 0x2E);
            Color flame = RGB(0xE8, 0x8A, 0x3A), steam = new Color32(0xE8, 0xE4, 0xDC, 90);
            Rect(t, 1, 0, 26, 12, iron); Frame(t, 1, 0, 26, 12, ironD);                // 铸铁身
            Rect(t, 2, 10, 24, 2, ironL); HLine(t, 12, 2, 25, ironD);                  // 灶面
            Rect(t, 5, 12, 7, 3, ironD); Rect(t, 16, 12, 7, 3, ironD);                 // 双灶眼
            P(t, 7, 13, flame); P(t, 18, 13, flame);                                   // 灶火
            Rect(t, 4, 15, 9, 6, pot); Frame(t, 4, 15, 9, 6, ironD); Rect(t, 5, 20, 7, 1, steel); // 左锅+锅盖钮
            Rect(t, 15, 15, 9, 5, pot); Frame(t, 15, 15, 9, 5, ironD); P(t, 24, 16, steel); // 右锅+把手
            Rect(t, 10, 6, 8, 6, ironD); Rect(t, 11, 4, 6, 2, ironD);                  // 背烟囱
            P(t, 13, 21, steam); P(t, 15, 23, steam); P(t, 12, 24, steam);             // 锅汽
            Rect(t, 0, 0, 1, 2, ironD); Rect(t, 27, 0, 1, 2, ironD);                   // 支脚
            Apply(t);
            return Finish(t, "prop_stove");
        }

        /// <summary>点餐台(V0.9.11 餐厅):木身+奶油台面,台上吐司架/蛋糕罩+小收银机,40×22——
        /// 店内消费交互锚点(照 JuiceCounter 骨架,内容换餐品)。</summary>
        public static Sprite DiningCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_dining_counter", out cached)) return cached;
            Texture2D t = NewTex(40, 22);
            Color wood = RGB(0x7A, 0x52, 0x30), woodD = RGB(0x5E, 0x3E, 0x22), top = RGB(0xF3, 0xE4, 0xC2), topD = RGB(0xD9, 0xC8, 0xA4);
            Color rego = RGB(0x3A, 0x2D, 0x24), regoL = RGB(0x5A, 0x48, 0x3A);
            Color toast = RGB(0xD9, 0xA8, 0x5A), toastD = RGB(0xB8, 0x86, 0x42);
            Color cake = RGB(0xF2, 0xEE, 0xE2), cakeD = RGB(0xD9, 0x5A, 0x4A);
            Rect(t, 0, 4, 40, 12, wood); Frame(t, 0, 4, 40, 12, woodD);                // 柜身
            for (int x = 3; x < 38; x += 7) VLine(t, x, 5, 14, woodD);                 // 护板凹槽
            P(t, 2, 8, RGB(0x9A, 0x6B, 0x3E)); P(t, 18, 10, RGB(0x9A, 0x6B, 0x3E)); P(t, 34, 7, RGB(0x9A, 0x6B, 0x3E)); // 木纹高光
            Rect(t, 0, 16, 40, 4, top); HLine(t, 16, 0, 39, topD); HLine(t, 19, 0, 39, topD); // 奶油台面
            Rect(t, 4, 16, 9, 6, rego); Rect(t, 5, 20, 7, 1, regoL);                   // 收银机
            P(t, 6, 18, regoL); P(t, 8, 18, regoL); P(t, 10, 18, regoL);
            for (int b = 0; b < 3; b++) Rect(t, 17 + b * 4, 17, 3, 2, b == 1 ? toastD : toast); // 吐司架
            Rect(t, 30, 17, 7, 5, cake); HLine(t, 18, 30, 36, cakeD); P(t, 33, 20, cakeD);     // 蛋糕罩+草莓
            Apply(t);
            return Finish(t, "prop_dining_counter");
        }

        /// <summary>放映银幕(V0.9.11 影院,用户复看二稿加宽):墨框+微亮幕面+落地幕脚,64×26——
        /// 大半墙宽的整幅银幕,北墙中央视觉锚点。</summary>
        public static Sprite CinemaScreen()
        {
            Sprite cached;
            if (TryGetCached("prop_cinema_screen", out cached)) return cached;
            Texture2D t = NewTex(64, 26);
            Color frame = RGB(0x30, 0x1F, 0x3E), frameD = RGB(0x22, 0x14, 0x2E);
            Color curtain = RGB(0xE8, 0xE4, 0xF0), curtainD = RGB(0xB8, 0xB0, 0xC8), curtainL = RGB(0xF6, 0xF4, 0xFA);
            Color gold = RGB(0xF0, 0xC3, 0x57);
            Rect(t, 0, 2, 64, 22, frame); Frame(t, 0, 2, 64, 22, frameD);              // 墨框
            Rect(t, 3, 5, 58, 16, curtain);                                            // 幕面
            for (int y = 5; y < 21; y++)                                               // 顶部微亮渐变
                HLine(t, y, 4, 59, Color.Lerp(curtainD, curtain, (y - 5) / 15f));
            HLine(t, 20, 4, 59, curtainD);                                             // 幕底阴影
            P(t, 10, 17, curtainL); P(t, 11, 16, curtainL); P(t, 46, 18, curtainL); P(t, 47, 17, curtainL); // 幕面高光斜线
            HLine(t, 23, 2, 61, gold); P(t, 1, 23, gold); P(t, 62, 23, gold);          // 框顶金饰线
            Rect(t, 8, 0, 6, 2, frameD); Rect(t, 50, 0, 6, 2, frameD);                 // 幕脚撑
            Apply(t);
            return Finish(t, "prop_cinema_screen");
        }

        /// <summary>观影座排(V0.9.11 影院):三联绒座+扶手+杯架,44×18——面向银幕两两成排。</summary>
        public static Sprite CinemaSeatRow()
        {
            Sprite cached;
            if (TryGetCached("prop_cinema_seats", out cached)) return cached;
            Texture2D t = NewTex(44, 18);
            Color velvet = RGB(0xC0, 0x50, 0x48), velvetD = RGB(0x9A, 0x3A, 0x36), velvetL = RGB(0xD4, 0x6A, 0x5E);
            Color arm = RGB(0x30, 0x1F, 0x3E), armL = RGB(0x46, 0x32, 0x58), cup = RGB(0x22, 0x14, 0x2E);
            Rect(t, 0, 0, 44, 4, velvetD);                                             // 底座暗沿
            for (int s = 0; s < 3; s++)
            {
                int bx = 3 + s * 14;
                Rect(t, bx, 4, 10, 8, velvet); Frame(t, bx, 4, 10, 8, velvetD);        // 座面
                Rect(t, bx + 1, 12, 8, 6, velvetD);                                    // 靠背
                HLine(t, 13, bx + 1, bx + 8, velvetL);                                 // 靠背顶受光
                P(t, bx + 2, 6, velvetL); P(t, bx + 7, 9, velvetL);                    // 绒面高光
                Rect(t, bx + 3, 2, 4, 2, cup);                                         // 杯架口
            }
            for (int a = 0; a < 4; a++) VLine(t, 1 + a * 14, 0, 14, arm);              // 四只扶手
            P(t, 1, 14, armL); P(t, 15, 14, armL); P(t, 29, 14, armL); P(t, 43, 14, armL); // 扶手顶
            Apply(t);
            return Finish(t, "prop_cinema_seats");
        }

        /// <summary>售票亭(V0.9.11 影院):墨身+金饰+拱窗+灯泡跑马+台面票槽,32×28——购票交互锚点。</summary>
        public static Sprite CinemaTicketBooth()
        {
            Sprite cached;
            if (TryGetCached("prop_ticket_booth", out cached)) return cached;
            Texture2D t = NewTex(32, 28);
            Color body = RGB(0x30, 0x1F, 0x3E), bodyD = RGB(0x22, 0x14, 0x2E), gold = RGB(0xF0, 0xC3, 0x57), goldD = RGB(0xC9, 0xA4, 0x4A);
            Color glass = RGB(0x54, 0x74, 0x8E), glassL = RGB(0x8E, 0xAE, 0xC4);
            Color cream = RGB(0xF2, 0xEE, 0xE2);
            Rect(t, 2, 0, 28, 20, body); Frame(t, 2, 0, 28, 20, bodyD);                // 亭身
            Rect(t, 5, 8, 22, 10, glass); Frame(t, 5, 8, 22, 10, goldD);               // 拱窗玻璃
            for (int x = 8; x < 26; x += 4) P(t, x, 8, goldD);                         // 窗顶棂点
            VLine(t, 16, 9, 17, goldD);                                                // 中棂
            P(t, 8, 15, glassL); P(t, 9, 14, glassL);                                  // 玻璃反光
            for (int x = 4; x < 28; x += 4) P(t, x, 21, gold); P(t, 6, 21, gold); P(t, 26, 21, gold); // 跑马灯泡
            HLine(t, 22, 2, 29, gold); HLine(t, 0, 2, 29, goldD);                      // 檐口双金线
            Rect(t, 8, 23, 16, 3, cream); Frame(t, 8, 23, 16, 3, goldD);               // 台面
            Rect(t, 13, 24, 6, 1, bodyD);                                              // 票槽
            Rect(t, 4, 0, 3, 2, gold); Rect(t, 25, 0, 3, 2, gold);                     // 顶饰
            Apply(t);
            return Finish(t, "prop_ticket_booth");
        }

        /// <summary>摊台(V0.9.11 市场):木摊身+珊瑚条纹台沿+台上果蔬箱,36×24——采购交互锚点。</summary>
        public static Sprite MarketStallTable()
        {
            Sprite cached;
            if (TryGetCached("prop_stall_table", out cached)) return cached;
            Texture2D t = NewTex(36, 24);
            Color wood = RGB(0x7A, 0x54, 0x36), woodD = RGB(0x5E, 0x40, 0x28), woodL = RGB(0x94, 0x70, 0x4E);
            Color stripe = RGB(0xE2, 0x6A, 0x4E), stripeD = RGB(0xC0, 0x50, 0x3C);
            Color crate = RGB(0xB0, 0x88, 0x50), crateD = RGB(0x7A, 0x58, 0x34);
            Color leaf = RGB(0x46, 0x82, 0x3A), fruit = RGB(0xD9, 0x5A, 0x4A), fruitY = RGB(0xF2, 0xA8, 0x4A);
            Rect(t, 0, 2, 36, 12, wood); Frame(t, 0, 2, 36, 12, woodD);                // 摊身
            for (int x = 2; x < 34; x += 6) Rect(t, x, 3, 3, 10, stripe);              // 珊瑚条纹前裙
            HLine(t, 14, 1, 34, stripeD);                                              // 台沿
            Rect(t, 3, 14, 30, 3, woodL); HLine(t, 16, 3, 32, woodD);                 // 台面板
            Rect(t, 4, 17, 12, 7, crate); Frame(t, 4, 17, 12, 7, crateD);              // 左果箱
            HLine(t, 19, 5, 14, leaf); P(t, 7, 21, leaf); P(t, 11, 22, leaf);          // 叶菜层
            P(t, 6, 19, fruit); P(t, 9, 20, fruit); P(t, 13, 19, fruitY);              // 果色点
            Rect(t, 20, 17, 12, 7, crate); Frame(t, 20, 17, 12, 7, crateD);            // 右果箱
            HLine(t, 19, 21, 30, fruitY); P(t, 23, 21, fruit); P(t, 27, 22, fruit); P(t, 29, 19, leaf);
            Rect(t, 0, 0, 3, 2, woodD); Rect(t, 33, 0, 3, 2, woodD);                   // 摊脚
            Apply(t);
            return Finish(t, "prop_stall_table");
        }

        /// <summary>麻袋堆(V0.9.11 市场):三只束口麻袋叠靠,20×18——货栈角落陈设。</summary>
        public static Sprite SackPile()
        {
            Sprite cached;
            if (TryGetCached("prop_sacks", out cached)) return cached;
            Texture2D t = NewTex(20, 18);
            Color sack = RGB(0xC8, 0xA8, 0x68), sackD = RGB(0xA8, 0x8A, 0x50), sackL = RGB(0xDC, 0xC2, 0x8A);
            Color tie = RGB(0x8A, 0x6A, 0x42);
            Rect(t, 0, 0, 9, 10, sack); Frame(t, 0, 0, 9, 10, sackD);                  // 左袋
            Rect(t, 3, 10, 3, 2, tie); P(t, 4, 12, tie);                               // 束口
            P(t, 2, 3, sackL); P(t, 6, 6, sackL); P(t, 1, 8, sackD);
            Rect(t, 11, 0, 9, 10, sack); Frame(t, 11, 0, 9, 10, sackD);                // 右袋
            Rect(t, 14, 10, 3, 2, tie); P(t, 15, 12, tie);
            P(t, 13, 4, sackL); P(t, 17, 7, sackL); P(t, 12, 8, sackD);
            Rect(t, 5, 12, 10, 6, sack); Frame(t, 5, 12, 10, 6, sackD);                // 前袋(叠靠)
            HLine(t, 15, 6, 13, sackD); P(t, 8, 17, sackL); P(t, 11, 14, sackL);       // 袋身褶
            P(t, 9, 16, tie); P(t, 10, 16, tie);                                       // 前袋束口
            Apply(t);
            return Finish(t, "prop_sacks");
        }

        /// <summary>吊秤(V0.9.11 市场):立杆+横臂+黄铜秤盘+砝码,18×26——过秤交易的道具感件。</summary>
        public static Sprite HangingScale()
        {
            Sprite cached;
            if (TryGetCached("prop_scale", out cached)) return cached;
            Texture2D t = NewTex(18, 26);
            Color wood = RGB(0x7A, 0x54, 0x36), woodD = RGB(0x5E, 0x40, 0x28);
            Color brass = RGB(0xC9, 0xA4, 0x4A), brassD = RGB(0xA0, 0x80, 0x34), brassL = RGB(0xE2, 0xC2, 0x72);
            Rect(t, 3, 0, 3, 12, wood); P(t, 4, 12, woodD); Rect(t, 1, 0, 7, 2, woodD); // 立杆+底脚
            HLine(t, 5, 4, 15, wood); P(t, 15, 4, brass); P(t, 5, 4, brass);           // 横臂+铜梢
            VLine(t, 12, 6, 10, brassD);                                               // 吊线
            Rect(t, 7, 10, 10, 3, brass); Frame(t, 7, 10, 10, 3, brassD);              // 秤盘
            P(t, 9, 11, brassL); P(t, 13, 11, brassL);                                 // 盘面受光
            Rect(t, 9, 13, 6, 2, brassD);                                              // 盘底
            Rect(t, 10, 7, 4, 3, brass); Frame(t, 10, 7, 4, 3, brassD); P(t, 11, 8, brassL); // 吊砝码
            Rect(t, 14, 6, 3, 4, brassD); P(t, 15, 9, brassL);                         // 小砝码
            Apply(t);
            return Finish(t, "prop_scale");
        }

        // ————— V0.9.12 第二批店内家具(茶室/家/便利屋/书店/花店/零嘴亭) —————

        /// <summary>茶台(V0.9.12 SUNNY TEA):抹绿台面+木柜身+台上圆茶壶与两只茶杯,40×22——
        /// 竞争情报面板的站位本件。</summary>
        public static Sprite TeaCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_tea_counter", out cached)) return cached;
            Texture2D t = NewTex(40, 22);
            Color matcha = RGB(0x86, 0xA0, 0x6C), matchaL = RGB(0x9C, 0xB2, 0x7E), matchaD = RGB(0x6E, 0x8A, 0x56);
            Color wood = RGB(0x8A, 0x6A, 0x42), woodD = RGB(0x6A, 0x50, 0x30);
            Rect(t, 3, 0, 34, 10, wood); Frame(t, 3, 0, 34, 10, woodD);                 // 柜身
            VLine(t, 13, 1, 8, woodD); VLine(t, 26, 1, 8, woodD);                       // 前板凹槽
            P(t, 6, 6, matchaL); P(t, 32, 5, matchaL);                                  // 木纹高光
            Rect(t, 0, 10, 40, 2, matcha); HLine(t, 11, 0, 39, matchaD);                // 台面前沿
            Rect(t, 0, 12, 40, 4, matchaL); HLine(t, 15, 1, 38, matcha);                // 台面顶板
            Rect(t, 9, 16, 7, 5, RGB(0x4E, 0x7A, 0x6A));                                // 圆茶壶(壶身)
            Rect(t, 11, 21, 3, 1, RGB(0x38, 0x5C, 0x4E)); P(t, 12, 20, RGB(0x6A, 0x94, 0x82)); // 壶盖+盖顶
            P(t, 16, 18, RGB(0x4E, 0x7A, 0x6A)); P(t, 17, 17, RGB(0x38, 0x5C, 0x4E));   // 壶嘴
            P(t, 8, 18, RGB(0x38, 0x5C, 0x4E)); P(t, 8, 19, RGB(0x4E, 0x7A, 0x6A));     // 壶把
            Rect(t, 23, 16, 4, 3, RGB(0xF2, 0xEE, 0xE2)); P(t, 24, 18, RGB(0xB0, 0x76, 0x4A)); // 茶杯a(白杯茶面)
            Rect(t, 30, 16, 4, 3, RGB(0xF2, 0xEE, 0xE2)); P(t, 31, 18, RGB(0xB0, 0x76, 0x4A)); // 茶杯b
            Apply(t);
            return Finish(t, "prop_tea_counter");
        }

        /// <summary>茶桌(V0.9.12 SUNNY TEA):矮方茶桌+中央小茶壶+两只杯垫,26×20——
        /// 茶室座位区主件(两列两行网格摆)。</summary>
        public static Sprite TeaTable()
        {
            Sprite cached;
            if (TryGetCached("prop_tea_table", out cached)) return cached;
            Texture2D t = NewTex(26, 20);
            Color wood = RGB(0xB4, 0x8A, 0x5C), woodD = RGB(0x8E, 0x6A, 0x44), woodL = RGB(0xC8, 0xA0, 0x72);
            Rect(t, 2, 0, 3, 13, woodD); Rect(t, 21, 0, 3, 13, woodD);                  // 桌腿
            Rect(t, 4, 3, 18, 10, wood);                                                // 桌裙
            Rect(t, 0, 13, 26, 4, wood); HLine(t, 16, 1, 24, woodL);                    // 桌面+受光沿
            HLine(t, 12, 1, 24, woodD);
            Rect(t, 10, 16, 6, 3, RGB(0x4E, 0x7A, 0x6A)); P(t, 12, 16, RGB(0x38, 0x5C, 0x4E)); // 小茶壶
            P(t, 5, 16, RGB(0xE2, 0xD6, 0xB8)); P(t, 4, 17, RGB(0xE2, 0xD6, 0xB8));     // 杯垫a
            P(t, 20, 16, RGB(0xE2, 0xD6, 0xB8)); P(t, 21, 17, RGB(0xE2, 0xD6, 0xB8));   // 杯垫b
            Apply(t);
            return Finish(t, "prop_tea_table");
        }

        /// <summary>家用床(V0.9.12 我的家):木床头板+米白床垫+暖橙被子+白枕头,36×26——
        /// 卧室区主件(靠北墙东侧)。</summary>
        public static Sprite HomeBed()
        {
            Sprite cached;
            if (TryGetCached("prop_home_bed", out cached)) return cached;
            Texture2D t = NewTex(36, 26);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xA2, 0x7A, 0x52);
            Color linen = RGB(0xF2, 0xEA, 0xDA), linenD = RGB(0xDC, 0xD0, 0xBA);
            Color quilt = RGB(0xE0, 0x9A, 0x5A), quiltD = RGB(0xC4, 0x7E, 0x42);
            Rect(t, 3, 19, 30, 6, wood); Frame(t, 3, 19, 30, 6, woodD); HLine(t, 24, 4, 31, woodL); // 床头板
            Rect(t, 3, 19, 3, 6, woodD); Rect(t, 30, 19, 3, 6, woodD);                  // 床柱
            Rect(t, 1, 6, 34, 13, linen); Frame(t, 1, 6, 34, 13, linenD);               // 床垫
            Rect(t, 1, 6, 34, 7, quilt); HLine(t, 12, 1, 34, quiltD);                   // 被子盖床尾
            HLine(t, 6, 2, 33, RGB(0xEA, 0xAA, 0x70));                                  // 被面折线
            Rect(t, 8, 14, 14, 4, RGB(0xFA, 0xF6, 0xEC)); Frame(t, 8, 14, 14, 4, linenD); // 枕头贴床头
            Rect(t, 3, 0, 4, 3, woodD); Rect(t, 29, 0, 4, 3, woodD);                    // 床脚
            Apply(t);
            return Finish(t, "prop_home_bed");
        }

        /// <summary>家用书桌(V0.9.12 我的家):暖木桌面+两侧抽屉柜+墨绿台灯+摊开的书,30×22——
        /// 今日账本面板的站位本件。</summary>
        public static Sprite HomeDesk()
        {
            Sprite cached;
            if (TryGetCached("prop_home_desk", out cached)) return cached;
            Texture2D t = NewTex(30, 22);
            Color wood = RGB(0xC0, 0x90, 0x58), woodD = RGB(0x94, 0x6C, 0x40), woodL = RGB(0xD2, 0xA4, 0x6C);
            Color drawer = RGB(0xA8, 0x7A, 0x48);
            Rect(t, 1, 4, 8, 11, drawer); Frame(t, 1, 4, 8, 11, woodD);                 // 左抽屉柜
            Rect(t, 21, 4, 8, 11, drawer); Frame(t, 21, 4, 8, 11, woodD);               // 右抽屉柜
            HLine(t, 7, 2, 7, woodD); HLine(t, 11, 2, 7, woodD);                        // 抽屉缝
            HLine(t, 7, 22, 27, woodD); HLine(t, 11, 22, 27, woodD);
            P(t, 4, 8, woodL); P(t, 25, 8, woodL);                                      // 抽屉把手
            Rect(t, 0, 15, 30, 4, wood); HLine(t, 18, 1, 28, woodL);                    // 桌面
            Rect(t, 21, 18, 7, 3, RGB(0x4E, 0x6A, 0x52));                               // 台灯罩
            VLine(t, 24, 16, 18, RGB(0x38, 0x50, 0x3C));                                // 灯杆
            P(t, 23, 15, RGB(0x38, 0x50, 0x3C)); P(t, 25, 15, RGB(0x38, 0x50, 0x3C));   // 灯底
            Rect(t, 4, 18, 8, 2, RGB(0xFA, 0xF6, 0xEC)); VLine(t, 7, 18, 19, RGB(0xC8, 0xC0, 0xAA)); // 摊开的书
            Apply(t);
            return Finish(t, "prop_home_desk");
        }

        /// <summary>便利屋收银台(V0.9.12):白亮台面+蓝灰柜身+扫码枪与小收银屏,36×22——
        /// 日用品消费面板的站位本件。</summary>
        public static Sprite StoreCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_store_counter", out cached)) return cached;
            Texture2D t = NewTex(36, 22);
            Color white = RGB(0xF2, 0xF2, 0xEC), whiteL = RGB(0xFA, 0xFA, 0xF6), whiteD = RGB(0xD8, 0xD8, 0xD0);
            Color body = RGB(0x7E, 0x92, 0xA0), bodyD = RGB(0x62, 0x74, 0x82), bodyL = RGB(0x94, 0xA8, 0xB4);
            Rect(t, 2, 0, 32, 13, body); Frame(t, 2, 0, 32, 13, bodyD);                 // 柜身
            VLine(t, 12, 1, 11, bodyD); VLine(t, 23, 1, 11, bodyD);                     // 前板分割
            P(t, 6, 6, bodyL); P(t, 29, 7, bodyL);                                      // 高光
            Rect(t, 0, 13, 36, 3, white); HLine(t, 15, 1, 34, whiteD);                  // 台面
            Rect(t, 0, 16, 36, 2, whiteL);                                              // 台面顶沿
            Rect(t, 24, 18, 5, 4, RGB(0x4A, 0x50, 0x58)); P(t, 26, 19, RGB(0x7A, 0xC9, 0x6A)); // 扫码枪(绿指示)
            Rect(t, 9, 18, 7, 4, RGB(0x6A, 0x70, 0x78)); Rect(t, 10, 19, 5, 2, RGB(0x9A, 0xD4, 0xE8)); // 收银小屏
            Apply(t);
            return Finish(t, "prop_store_counter");
        }

        /// <summary>双面书架(V0.9.12 东街书店):深木外框+四层搁板+多色书脊,40×36——
        /// 书架巷道主件(两列两行网格摆)。</summary>
        public static Sprite BookshelfRow()
        {
            Sprite cached;
            if (TryGetCached("prop_bookshelf", out cached)) return cached;
            Texture2D t = NewTex(40, 36);
            Color oak = RGB(0x6B, 0x4A, 0x2E), oakD = RGB(0x52, 0x36, 0x20), oakL = RGB(0x82, 0x62, 0x42);
            Rect(t, 1, 2, 38, 32, oak); Frame(t, 1, 2, 38, 32, oakD);                   // 外框
            Rect(t, 0, 32, 40, 4, oakL); HLine(t, 33, 0, 39, oakD);                     // 顶檐板
            Color[] spines = {
                RGB(0xC2, 0x58, 0x48), RGB(0xE2, 0xC2, 0x5A), RGB(0x5E, 0x8A, 0x5A),
                RGB(0x5A, 0x7A, 0x9A), RGB(0x9A, 0x6E, 0x46), RGB(0xB8, 0x86, 0xA8)
            };
            int[] shelves = { 4, 11, 18, 25 };                                          // 四层底 y
            for (int s = 0; s < shelves.Length; s++)
            {
                int y0 = shelves[s] + 1, y1 = shelves[s] + 6;                           // 书脊区间
                for (int x = 4; x < 37; x += 3)
                {
                    Color c = spines[(x / 3 + s) % spines.Length];
                    VLine(t, x, y0, y1, c); VLine(t, x + 1, y0, y1, Shade(c, .8f));    // 书脊+侧影
                }
                HLine(t, shelves[s], 3, 36, oakL);                                      // 搁板
            }
            Apply(t);
            return Finish(t, "prop_bookshelf");
        }

        /// <summary>花架(V0.9.12 花店):阶梯两层木架+五盆彩色花,32×28——
        /// 花店陈列主件。</summary>
        public static Sprite FlowerRack()
        {
            Sprite cached;
            if (TryGetCached("prop_flower_rack", out cached)) return cached;
            Texture2D t = NewTex(32, 28);
            Color wood = RGB(0xA8, 0x7A, 0x4E), woodD = RGB(0x84, 0x5E, 0x38), woodL = RGB(0xBC, 0x90, 0x62);
            Color pot = RGB(0xB0, 0x62, 0x4A), potD = RGB(0x8E, 0x4C, 0x38);
            Color red = RGB(0xE2, 0x6A, 0x6A), yellow = RGB(0xF2, 0xD4, 0x5A), pink = RGB(0xE2, 0x9A, 0xC2);
            Rect(t, 3, 0, 3, 16, woodD); Rect(t, 26, 0, 3, 16, woodD);                  // 架腿
            Rect(t, 1, 4, 30, 3, wood); HLine(t, 6, 2, 29, woodL);                      // 下层台
            Rect(t, 5, 12, 22, 3, wood); HLine(t, 14, 6, 25, woodL);                    // 上层台
            int[] potX = { 4, 13, 22 };                                                 // 下层三盆
            Color[] petal = { red, yellow, pink };
            for (int i = 0; i < potX.Length; i++)
            {
                Rect(t, potX[i], 7, 6, 4, pot); Frame(t, potX[i], 7, 6, 4, potD);       // 花盆
                P(t, potX[i] + 1, 11, petal[i]); P(t, potX[i] + 3, 12, petal[(i + 1) % 3]); P(t, potX[i] + 4, 11, petal[i]); // 花头
            }
            int[] upPotX = { 8, 18 };                                                   // 上层两盆
            for (int i = 0; i < upPotX.Length; i++)
            {
                Rect(t, upPotX[i], 15, 6, 4, pot); Frame(t, upPotX[i], 15, 6, 4, potD);
                P(t, upPotX[i] + 1, 19, petal[(i + 2) % 3]); P(t, upPotX[i] + 3, 20, petal[i]); P(t, upPotX[i] + 4, 19, petal[(i + 1) % 3]);
            }
            Apply(t);
            return Finish(t, "prop_flower_rack");
        }

        /// <summary>花桶(V0.9.12 花店):铁灰圆桶+三支插花,16×20——门口成排的小品件。</summary>
        public static Sprite FlowerBucket()
        {
            Sprite cached;
            if (TryGetCached("prop_flower_bucket", out cached)) return cached;
            Texture2D t = NewTex(16, 20);
            Color iron = RGB(0x8A, 0x8E, 0x94), ironD = RGB(0x66, 0x6A, 0x70), ironL = RGB(0xA2, 0xA6, 0xAC);
            Color stem = RGB(0x5E, 0x8A, 0x5A);
            Rect(t, 4, 0, 8, 9, iron); Frame(t, 4, 0, 8, 9, ironD); HLine(t, 4, 4, 11, ironL); // 桶身+箍
            VLine(t, 6, 9, 14, stem); VLine(t, 8, 9, 16, stem); VLine(t, 10, 9, 13, stem);    // 三支花茎
            P(t, 5, 10, stem); P(t, 9, 12, stem);                                       // 叶
            P(t, 6, 15, RGB(0xE2, 0x6A, 0x6A)); P(t, 5, 14, RGB(0xE2, 0x6A, 0x6A));     // 红花
            P(t, 8, 17, RGB(0xF2, 0xD4, 0x5A)); P(t, 9, 16, RGB(0xF2, 0xD4, 0x5A));     // 黄花
            P(t, 10, 14, RGB(0xE2, 0x9A, 0xC2)); P(t, 11, 13, RGB(0xE2, 0x9A, 0xC2));   // 粉花
            Apply(t);
            return Finish(t, "prop_flower_bucket");
        }

        /// <summary>小卖窗(V0.9.12 零嘴亭):奶油亭身+深色窗口+橙白条纹遮阳檐+挂零食袋,36×28——
        /// 零嘴亭的氛围交互本件。</summary>
        public static Sprite SnackWindow()
        {
            Sprite cached;
            if (TryGetCached("prop_snack_window", out cached)) return cached;
            Texture2D t = NewTex(36, 28);
            Color cream = RGB(0xF4, 0xE8, 0xCE), creamD = RGB(0xD8, 0xC8, 0xA8);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xC0, 0x90, 0x58);
            Color awn = RGB(0xE0, 0x9A, 0x4A), awnD = RGB(0xC4, 0x7E, 0x36);
            Rect(t, 4, 0, 4, 6, woodD); Rect(t, 28, 0, 4, 6, woodD);                    // 支脚
            Rect(t, 2, 6, 32, 14, cream); Frame(t, 2, 6, 32, 14, creamD);               // 亭身
            Rect(t, 6, 9, 24, 8, RGB(0x4A, 0x3A, 0x2C)); Frame(t, 6, 9, 24, 8, wood);   // 窗口(框木内暗)
            Rect(t, 2, 17, 32, 3, woodL); HLine(t, 19, 3, 32, wood);                    // 窗台
            int[] bagX = { 10, 16, 22 };                                                // 窗口挂零食袋
            Color[] bags = { RGB(0xC2, 0x58, 0x48), RGB(0xE2, 0xC2, 0x5A), RGB(0x5E, 0x8A, 0x5A) };
            for (int i = 0; i < bagX.Length; i++)
            {
                Rect(t, bagX[i], 11, 3, 4, bags[i]);
                P(t, bagX[i] + 1, 10, wood);                                            // 挂钩
            }
            Rect(t, 0, 20, 36, 4, awn);                                                 // 遮阳檐(贴住亭身顶——复看:原 y22 与身顶 y19 有 2px 缝,檐读作浮空)
            for (int x = 2; x < 36; x += 6) VLine(t, x, 20, 23, awnD);                  // 檐条纹
            HLine(t, 24, 1, 34, RGB(0xEE, 0xB0, 0x6A));
            Apply(t);
            return Finish(t, "prop_snack_window");
        }

        /// <summary>书台(V0.9.12 东街书店):深棕柜身+奶油台面,台上一摞书+立式样书牌+小收银屏,36×22——
        /// 翻看氛围交互的站位本件(复审 visual:此前复用餐厅 DiningCounter,吐司+蛋糕罩主题错位)。</summary>
        public static Sprite BookCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_book_counter", out cached)) return cached;
            Texture2D t = NewTex(36, 22);
            Color wood = RGB(0x5E, 0x40, 0x28), woodD = RGB(0x46, 0x2E, 0x1C), woodL = RGB(0x76, 0x54, 0x36);
            Color top = RGB(0xE8, 0xDF, 0xC8), topD = RGB(0xD0, 0xC4, 0xA8);
            Color page = RGB(0xFA, 0xF6, 0xEC), pageD = RGB(0xD8, 0xD0, 0xBC);
            Color[] covers = { RGB(0xC2, 0x58, 0x48), RGB(0x5A, 0x7A, 0x9A), RGB(0x5E, 0x8A, 0x5A) };
            Rect(t, 0, 4, 36, 12, wood); Frame(t, 0, 4, 36, 12, woodD);                 // 柜身
            for (int x = 3; x < 34; x += 7) VLine(t, x, 5, 14, woodD);                 // 护板凹槽
            P(t, 2, 8, woodL); P(t, 16, 10, woodL); P(t, 32, 7, woodL);                // 木纹高光
            Rect(t, 0, 16, 36, 4, top); HLine(t, 16, 0, 35, topD); HLine(t, 19, 0, 35, topD); // 奶油台面
            Rect(t, 4, 16, 9, 6, RGB(0x6A, 0x70, 0x78)); Rect(t, 5, 17, 7, 3, RGB(0x9A, 0xD4, 0xE8)); // 小收银屏
            for (int b = 0; b < 3; b++) Rect(t, 16 + b * 3, 17 - b, 10, 2, b == 2 ? page : covers[b]); // 一摞横书(阶梯)
            HLine(t, 18, 16, 25, pageD);                                               // 顶书页边
            Rect(t, 28, 18, 6, 4, covers[2]); P(t, 29, 22, page); P(t, 32, 22, page);  // 立式样书牌
            Apply(t);
            return Finish(t, "prop_book_counter");
        }

        /// <summary>包花台(V0.9.12 花店):木柜身+奶油台面,台上牛皮纸卷+小桶插花+剪刀,36×22——
        /// 挑花氛围交互的站位本件(复审 visual:此前复用餐厅 DiningCounter 主题错位)。</summary>
        public static Sprite FlowerWrapCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_flower_counter", out cached)) return cached;
            Texture2D t = NewTex(36, 22);
            Color wood = RGB(0xA8, 0x7A, 0x4E), woodD = RGB(0x84, 0x5E, 0x38), woodL = RGB(0xBC, 0x90, 0x62);
            Color top = RGB(0xF4, 0xEC, 0xE0), topD = RGB(0xE0, 0xD4, 0xC6);
            Color kraft = RGB(0xC9, 0xA8, 0x72), kraftD = RGB(0xA8, 0x8A, 0x5A);
            Color iron = RGB(0x8A, 0x8E, 0x94), stem = RGB(0x5E, 0x8A, 0x5A);
            Rect(t, 0, 4, 36, 12, wood); Frame(t, 0, 4, 36, 12, woodD);                 // 柜身
            for (int x = 3; x < 34; x += 7) VLine(t, x, 5, 14, woodD);
            P(t, 2, 8, woodL); P(t, 16, 10, woodL); P(t, 32, 7, woodL);
            Rect(t, 0, 16, 36, 4, top); HLine(t, 16, 0, 35, topD); HLine(t, 19, 0, 35, topD); // 奶油台面
            Rect(t, 3, 17, 10, 4, kraft); Frame(t, 3, 17, 10, 4, kraftD); HLine(t, 19, 4, 12, kraftD); // 牛皮纸卷
            Rect(t, 17, 16, 6, 5, iron); HLine(t, 17, 17, 22, RGB(0xA2, 0xA6, 0xAC)); // 小花桶
            VLine(t, 19, 20, 22, stem); VLine(t, 21, 20, 22, stem);                    // 两支花茎
            P(t, 19, 21, RGB(0xE2, 0x6A, 0x6A)); P(t, 21, 21, RGB(0xE2, 0x9A, 0xC2));  // 红花+粉花
            P(t, 28, 18, iron); P(t, 30, 20, iron); VLine(t, 29, 18, 20, RGB(0x6A, 0x6E, 0x74)); // 剪刀(双刃+轴)
            Apply(t);
            return Finish(t, "prop_flower_counter");
        }

        // ————— V0.9.13 第三批六间(机构与产业):银行柜台/等候椅/大理石柱/央行长柜/政务台/
        // 黑板/课桌/讲台/灌装传送带/机床——机构场所的辨识度件 —————

        /// <summary>银行柜台(V0.9.13 小镇银行):深胡桃高柜+浅石台面,台上一排铜护栏+存折台牌,
        /// 40×24——营业厅存取业务的站位本件。</summary>
        public static Sprite BankCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_bank_counter", out cached)) return cached;
            Texture2D t = NewTex(40, 24);
            Color wood = RGB(0x5E, 0x44, 0x26), woodD = RGB(0x46, 0x32, 0x1A), woodL = RGB(0x76, 0x58, 0x36);
            Color top = RGB(0xE8, 0xE4, 0xD8), topD = RGB(0xD2, 0xCE, 0xC0);
            Color brass = RGB(0xB0, 0x8A, 0x3E), brassD = RGB(0x8E, 0x6C, 0x2C);
            Rect(t, 0, 4, 40, 12, wood); Frame(t, 0, 4, 40, 12, woodD);                 // 高柜身
            for (int x = 3; x < 38; x += 7) VLine(t, x, 5, 14, woodD);                  // 护板凹槽
            P(t, 2, 8, woodL); P(t, 18, 10, woodL); P(t, 36, 7, woodL);
            Rect(t, 0, 16, 40, 4, top); HLine(t, 16, 0, 39, topD); HLine(t, 19, 0, 39, topD); // 浅石台面
            // 复看返工:纯镂空护栏读作「台面与栏杆上下脱节」——柱间补淡青玻璃(柜台窗口感,
            // 一并立住「银行柜台」辨识度),铜柱与横杆画在玻璃之上
            Rect(t, 2, 20, 36, 3, RGB(0xB4, 0xC8, 0xD2));
            HLine(t, 21, 2, 37, RGB(0xC8, 0xDC, 0xE4)); HLine(t, 22, 2, 37, RGB(0xA2, 0xB8, 0xC4));
            for (int x = 2; x < 38; x += 4) VLine(t, x, 20, 22, brass);                 // 台上铜护栏(一排细柱)
            HLine(t, 20, 0, 39, brassD); HLine(t, 23, 0, 39, brass);                    // 护栏横杆双线
            Rect(t, 4, 20, 6, 3, brass); HLine(t, 21, 5, 8, RGB(0xF4, 0xF0, 0xE4));     // 存折台牌(铜座+纸面)
            Apply(t);
            return Finish(t, "prop_bank_counter");
        }

        /// <summary>等候长椅(V0.9.13 银行/市政厅):木框三座垫、无靠背,30×12——营业厅/政务大厅
        /// 等候区的成排件(两两成列摆放)。</summary>
        public static Sprite WaitingBench()
        {
            Sprite cached;
            if (TryGetCached("prop_waiting_bench", out cached)) return cached;
            Texture2D t = NewTex(30, 12);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xA4, 0x7A, 0x50);
            Color pad = RGB(0x8A, 0x9A, 0xA8), padD = RGB(0x74, 0x84, 0x94);
            Rect(t, 1, 5, 28, 3, pad);                                                  // 三连座垫
            VLine(t, 10, 5, 7, padD); VLine(t, 19, 5, 7, padD);                         // 座垫分缝
            Rect(t, 0, 4, 30, 1, wood); HLine(t, 8, 1, 28, woodD);                      // 垫下木档
            Rect(t, 2, 0, 3, 4, woodD); Rect(t, 25, 0, 3, 4, woodD);                    // 椅腿
            Rect(t, 0, 4, 2, 4, wood); Rect(t, 28, 4, 2, 4, wood);                      // 两端木档头
            P(t, 4, 5, woodL); P(t, 15, 6, woodL);
            Apply(t);
            return Finish(t, "prop_waiting_bench");
        }

        /// <summary>大理石柱(V0.9.13 中央银行):柱础+槽纹柱身+柱头,16×34——央行柱廊大厅的
        /// 空间件(进口通道两侧各一)。</summary>
        public static Sprite MarbleColumn()
        {
            Sprite cached;
            if (TryGetCached("prop_marble_column", out cached)) return cached;
            Texture2D t = NewTex(16, 34);
            Color marble = RGB(0xED, 0xEA, 0xE2), marbleD = RGB(0xD8, 0xD4, 0xC8), marbleL = RGB(0xF6, 0xF3, 0xEC);
            Color trim = RGB(0xC4, 0xBE, 0xB0);
            Rect(t, 2, 0, 12, 3, trim); HLine(t, 2, 1, 13, marbleD);                    // 柱础
            Rect(t, 4, 3, 8, 26, marble);                                              // 柱身
            VLine(t, 6, 4, 27, marbleD); VLine(t, 10, 4, 27, marbleD);                  // 竖槽纹(三瓣)
            VLine(t, 8, 4, 27, marbleL);                                               // 受光瓣
            Rect(t, 2, 29, 12, 3, trim); HLine(t, 30, 2, 13, marbleD); Rect(t, 3, 32, 10, 2, marble); // 柱头
            Apply(t);
            return Finish(t, "prop_marble_column");
        }

        /// <summary>央行长柜(V0.9.13 中央银行):浅大理石柜身+金线包边+台面立式利率卡,44×24——
        /// 央行政策页的站位本件(比商业银行柜台更宽、配色更亮)。</summary>
        public static Sprite PolicyCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_policy_counter", out cached)) return cached;
            Texture2D t = NewTex(44, 24);
            Color body = RGB(0xE6, 0xE2, 0xD6), bodyD = RGB(0xD2, 0xCE, 0xC0);
            Color gold = RGB(0xA4, 0x84, 0x3A), goldD = RGB(0x84, 0x68, 0x2A);
            Color top = RGB(0xF2, 0xEF, 0xE6);
            Color paper = RGB(0xFA, 0xF7, 0xEE);
            Rect(t, 0, 4, 44, 12, body); Frame(t, 0, 4, 44, 12, gold);                  // 大理石柜身金线包边
            VLine(t, 21, 5, 14, bodyD); VLine(t, 22, 5, 14, bodyD);                     // 双中缝
            Rect(t, 0, 16, 44, 4, top); HLine(t, 19, 0, 43, bodyD);                     // 台面
            Rect(t, 4, 17, 8, 3, paper); HLine(t, 18, 5, 10, RGB(0xC4, 0x58, 0x48));    // 台上文件(红头页)
            Rect(t, 16, 17, 8, 3, paper); HLine(t, 18, 17, 22, bodyD);
            Rect(t, 30, 13, 8, 7, paper); Frame(t, 30, 13, 8, 7, gold);                 // 立式利率卡(金框)
            HLine(t, 16, 31, 36, goldD); P(t, 33, 17, RGB(0xC4, 0x58, 0x48)); P(t, 35, 17, RGB(0xC4, 0x58, 0x48));
            Apply(t);
            return Finish(t, "prop_policy_counter");
        }

        /// <summary>政务长台(V0.9.13 市政厅):深红木柜身+浅木台面,台上两摞文件+小台名牌,44×24——
        /// 财政总览面板的站位本件。</summary>
        public static Sprite GovernmentDesk()
        {
            Sprite cached;
            if (TryGetCached("prop_government_desk", out cached)) return cached;
            Texture2D t = NewTex(44, 24);
            Color wood = RGB(0x6E, 0x2A, 0x24), woodD = RGB(0x54, 0x1E, 0x1A), woodL = RGB(0x8A, 0x3C, 0x32);
            Color top = RGB(0xC8, 0xA4, 0x68), topD = RGB(0xB0, 0x8C, 0x54);
            Color paper = RGB(0xFA, 0xF7, 0xEE), paperD = RGB(0xDE, 0xDA, 0xCE);
            Color gold = RGB(0xA4, 0x84, 0x3A);
            Rect(t, 0, 4, 44, 12, wood); Frame(t, 0, 4, 44, 12, woodD);                 // 深红木柜身
            for (int x = 3; x < 42; x += 7) VLine(t, x, 5, 14, woodD);
            P(t, 2, 8, woodL); P(t, 20, 10, woodL); P(t, 40, 7, woodL);
            Rect(t, 0, 16, 44, 4, top); HLine(t, 16, 0, 43, topD); HLine(t, 19, 0, 43, topD); // 浅木台面
            for (int b = 0; b < 3; b++) Rect(t, 4 + b * 2, 17 - b, 7, 2, paper);        // 文件摞一(阶梯)
            HLine(t, 18, 4, 10, paperD);
            for (int b = 0; b < 2; b++) Rect(t, 14 + b * 2, 17 - b, 6, 2, paper);       // 文件摞二
            Rect(t, 32, 17, 8, 4, paper); Frame(t, 32, 17, 8, 4, gold);                 // 金边台名牌
            Apply(t);
            return Finish(t, "prop_government_desk");
        }

        /// <summary>黑板(V0.9.13 职业培训中心):墨绿板面+木框+粉笔槽+白色板书,36×26——教室
        /// 北墙的核心辨识件(讲台在板前)。</summary>
        public static Sprite Blackboard()
        {
            Sprite cached;
            if (TryGetCached("prop_blackboard", out cached)) return cached;
            Texture2D t = NewTex(36, 26);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xA4, 0x7A, 0x50);
            Color board = RGB(0x2E, 0x46, 0x36), boardD = RGB(0x24, 0x3A, 0x2C);
            Color chalk = RGB(0xE8, 0xE8, 0xE0);
            Rect(t, 2, 4, 32, 20, wood); Rect(t, 4, 6, 28, 16, board);                  // 木框+板面
            Frame(t, 4, 6, 28, 16, boardD);
            Rect(t, 2, 1, 32, 2, woodL); HLine(t, 2, 2, 32, woodD);                     // 粉笔槽
            P(t, 8, 2, chalk); P(t, 22, 2, chalk);                                      // 槽上两截粉笔
            HLine(t, 7, 8, 16, chalk); HLine(t, 8, 10, 18, chalk); HLine(t, 9, 12, 19, chalk); // 板书横行(歪斜感)
            P(t, 15, 15, chalk); P(t, 17, 14, chalk); P(t, 19, 13, chalk);              // 板书箭头上扬
            VLine(t, 19, 13, 16, chalk);
            P(t, 25, 16, RGB(0xE2, 0xB4, 0x6A)); P(t, 27, 15, RGB(0xE2, 0xB4, 0x6A));  // 黄粉笔补注
            Apply(t);
            return Finish(t, "prop_blackboard");
        }

        /// <summary>双人课桌(V0.9.13 职业培训中心):浅木桌面+钢档板+桌上小书,34×16——教室
        /// 成排成列的主件(两行两列摆放)。</summary>
        public static Sprite SchoolDeskRow()
        {
            Sprite cached;
            if (TryGetCached("prop_school_desk", out cached)) return cached;
            Texture2D t = NewTex(34, 16);
            Color wood = RGB(0xC8, 0xA4, 0x68), woodD = RGB(0xAA, 0x88, 0x50);
            Color steel = RGB(0x8A, 0x8E, 0x94), steelD = RGB(0x6A, 0x6E, 0x74);
            Rect(t, 1, 8, 32, 4, wood); HLine(t, 8, 1, 32, woodD); HLine(t, 11, 1, 32, RGB(0xD8, 0xB8, 0x80)); // 桌面
            Rect(t, 1, 2, 2, 6, steel); Rect(t, 31, 2, 2, 6, steel);                    // 两侧钢档板
            VLine(t, 16, 4, 7, steelD); VLine(t, 17, 4, 7, steelD);                     // 中档(双人分界)
            Rect(t, 4, 5, 4, 3, steelD); Rect(t, 26, 5, 4, 3, steelD);                  // 桌腿
            Rect(t, 5, 6, 4, 2, RGB(0x5A, 0x7A, 0x9A));                                 // 桌上小书(蓝)
            Rect(t, 22, 6, 4, 2, RGB(0xC2, 0x58, 0x48));                                // 桌上小书(红)
            Apply(t);
            return Finish(t, "prop_school_desk");
        }

        /// <summary>讲台(V0.9.13 职业培训中心):木讲台身+斜面讲义+小话筒,20×26——黑板前的
        /// 站位本件(培训补贴面板)。</summary>
        public static Sprite Podium()
        {
            Sprite cached;
            if (TryGetCached("prop_podium", out cached)) return cached;
            Texture2D t = NewTex(20, 26);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xA4, 0x7A, 0x50);
            Color paper = RGB(0xFA, 0xF7, 0xEE);
            Rect(t, 4, 2, 12, 16, wood); Frame(t, 4, 2, 12, 16, woodD);                 // 台身
            HLine(t, 9, 5, 14, woodD); P(t, 6, 6, woodL); P(t, 13, 12, woodL);
            Rect(t, 3, 18, 14, 4, woodL); HLine(t, 21, 3, 16, woodD);                   // 斜台面(受光)
            Rect(t, 6, 19, 8, 3, paper); HLine(t, 20, 7, 12, RGB(0xC4, 0xBE, 0xB0));    // 摊开讲义
            VLine(t, 16, 19, 23, RGB(0x6A, 0x6E, 0x74)); P(t, 16, 24, RGB(0x4A, 0x4E, 0x54)); // 小话筒杆+头
            Apply(t);
            return Finish(t, "prop_podium");
        }

        /// <summary>灌装传送带(V0.9.13 河畔工厂):钢架滚轴带+带上一排果汁瓶,40×18——车间
        /// 产线的站位本件(果汁镇工厂=灌装线,主线呼应)。</summary>
        public static Sprite ConveyorUnit()
        {
            Sprite cached;
            if (TryGetCached("prop_conveyor", out cached)) return cached;
            Texture2D t = NewTex(40, 18);
            Color steel = RGB(0x8A, 0x8E, 0x94), steelD = RGB(0x6A, 0x6E, 0x74);
            Color belt = RGB(0x4A, 0x4E, 0x54), beltD = RGB(0x3A, 0x3E, 0x44), beltL = RGB(0x5A, 0x5E, 0x64);
            Color juice = RGB(0xE2, 0x82, 0x3C), juiceD = RGB(0xC4, 0x66, 0x28), cap = RGB(0x5E, 0x8A, 0x5A);
            Rect(t, 0, 6, 40, 6, belt); Frame(t, 0, 6, 40, 6, steel);                   // 传送带体
            for (int x = 2; x < 38; x += 4) VLine(t, x, 7, 10, beltD);                  // 滚轴纹
            HLine(t, 11, 1, 38, beltL);
            Rect(t, 2, 0, 3, 6, steelD); Rect(t, 35, 0, 3, 6, steelD);                  // 钢支脚
            for (int i = 0; i < 4; i++)
            {
                int bx = 4 + i * 9;                                                    // 带上一排果汁瓶
                Rect(t, bx, 12, 4, 5, juice); Frame(t, bx, 12, 4, 5, juiceD);
                Rect(t, bx + 1, 11, 2, 1, steelD); P(t, bx + 1, 10, cap);               // 瓶口+绿盖
            }
            Apply(t);
            return Finish(t, "prop_conveyor");
        }

        /// <summary>机床(V0.9.13 河畔工厂):钢蓝机身+仪表盘+顶部管道+指示灯,28×34——车间
        /// 两侧的重装备件(成对摆放)。</summary>
        public static Sprite FactoryMachine()
        {
            Sprite cached;
            if (TryGetCached("prop_factory_machine", out cached)) return cached;
            Texture2D t = NewTex(28, 34);
            Color body = RGB(0x56, 0x68, 0x80), bodyD = RGB(0x3E, 0x4E, 0x60), bodyL = RGB(0x6A, 0x7C, 0x94);
            Color steel = RGB(0x8A, 0x8E, 0x94), steelD = RGB(0x6A, 0x6E, 0x74);
            Color panel = RGB(0x3A, 0x46, 0x56);
            Rect(t, 2, 6, 24, 18, body); Frame(t, 2, 6, 24, 18, bodyD);                 // 机身
            HLine(t, 7, 3, 25, bodyL); P(t, 4, 10, bodyL); P(t, 23, 16, bodyL);
            Rect(t, 6, 16, 10, 6, panel); Frame(t, 6, 16, 10, 6, bodyD);                // 仪表面板
            P(t, 8, 19, RGB(0x6A, 0xE2, 0x8A)); P(t, 11, 19, RGB(0xE2, 0xC2, 0x5A)); P(t, 14, 19, RGB(0xE2, 0x5A, 0x5A)); // 三色仪表灯
            Rect(t, 6, 24, 4, 3, steel); HLine(t, 26, 7, 9, steelD);                    // 检修小门
            Rect(t, 19, 24, 5, 3, steel); HLine(t, 26, 20, 23, steelD);
            Rect(t, 6, 24, 16, 1, bodyD);                                              // 门楣
            Rect(t, 6, 28, 4, 6, steel); VLine(t, 8, 29, 33, steelD);                   // 顶部管道(竖)
            Rect(t, 6, 31, 16, 3, steel); Frame(t, 6, 31, 16, 3, steelD);               // 横管联箱
            P(t, 22, 5, RGB(0xE2, 0x5A, 0x5A)); P(t, 24, 5, RGB(0x6A, 0xE2, 0x8A));    // 机身顶红绿指示灯
            Apply(t);
            return Finish(t, "prop_factory_machine");
        }

        // ————— V0.9.14 店内加密与去同质化:上墙挂件族(画/钟/灯/海报/徽章/干花/工具板/地图/
        // 零食串/意见箱)——填此前全空的上墙带;配色一律暖木框+低饱和内容,服从各店 theme —————

        /// <summary>挂画(V0.9.14):暖木框小幅,四变体内容——0 水果静物/1 山景/2 海景/3 全家福,
        /// 20×16——上墙带密度件(多店复用,靠变体错开)。</summary>
        public static Sprite WallArt(int variant)
        {
            string key = "prop_wall_art_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(20, 16);
            Color frame = RGB(0x8A, 0x62, 0x40), frameD = RGB(0x66, 0x46, 0x2C), frameL = RGB(0xA4, 0x7A, 0x50);
            Color paper = RGB(0xF4, 0xEE, 0xE0);
            Rect(t, 0, 0, 20, 16, frame); Frame(t, 0, 0, 20, 16, frameD);
            Rect(t, 2, 2, 16, 12, paper);
            P(t, 1, 1, frameL); P(t, 18, 1, frameL);
            if (variant == 0)
            {
                // 水果静物:陶碗+两枚橙子+青果(果汁店/餐厅)
                Rect(t, 6, 8, 8, 3, RGB(0xC2, 0x8A, 0x5A));
                P(t, 7, 7, RGB(0xE2, 0x82, 0x3C)); P(t, 9, 6, RGB(0xE2, 0x82, 0x3C));
                P(t, 12, 7, RGB(0x8A, 0xA8, 0x5A)); HLine(t, 5, 10, 12, RGB(0xD8, 0xC8, 0xA8));
            }
            else if (variant == 1)
            {
                // 山景:两重山+雪顶(茶室/书店)
                Rect(t, 2, 6, 16, 8, RGB(0xB8, 0xC8, 0xB4));
                Rect(t, 3, 4, 7, 10, RGB(0x7E, 0x96, 0x84)); Rect(t, 10, 5, 8, 9, RGB(0x94, 0xAA, 0x92));
                HLine(t, 5, 4, 7, RGB(0xF4, 0xF4, 0xF0)); HLine(t, 12, 5, 15, RGB(0xF4, 0xF4, 0xF0));
                P(t, 6, 3, RGB(0xE2, 0xC2, 0x5A)); P(t, 14, 3, RGB(0xE2, 0xC2, 0x5A));   // 日/鸟
            }
            else if (variant == 2)
            {
                // 海景:海平线+帆影+飞鸟(零嘴亭/我的家)
                Rect(t, 2, 2, 16, 7, RGB(0x8E, 0xB4, 0xC8)); Rect(t, 2, 9, 16, 5, RGB(0x5A, 0x84, 0x9E));
                HLine(t, 9, 2, 17, RGB(0xE8, 0xF0, 0xF4));
                Rect(t, 8, 5, 3, 4, RGB(0xF4, 0xF4, 0xF0)); VLine(t, 9, 3, 5, RGB(0x8A, 0x62, 0x40)); // 白帆小船
                P(t, 4, 4, RGB(0x3A, 0x3A, 0x3A)); P(t, 6, 3, RGB(0x3A, 0x3A, 0x3A)); P(t, 14, 4, RGB(0x3A, 0x3A, 0x3A));
            }
            else
            {
                // 全家福:三人剪影+暖底(我的家)
                Rect(t, 2, 2, 16, 12, RGB(0xF0, 0xE0, 0xC4));
                Rect(t, 5, 5, 3, 3, RGB(0x8A, 0x5A, 0x3A)); Rect(t, 4, 8, 5, 6, RGB(0x8A, 0x5A, 0x3A));   // 爸
                Rect(t, 11, 5, 3, 3, RGB(0xC2, 0x86, 0x6A)); Rect(t, 10, 8, 5, 6, RGB(0xC2, 0x86, 0x6A)); // 妈
                Rect(t, 8, 7, 3, 2, RGB(0x6A, 0x4A, 0x30)); Rect(t, 7, 9, 4, 5, RGB(0x6A, 0x4A, 0x30));   // 孩子
            }
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>挂钟(V0.9.14):圆钟面+双指针+挂点,12×14——银行/市政厅/我的家的墙面件。</summary>
        public static Sprite WallClock()
        {
            Sprite cached;
            if (TryGetCached("prop_wall_clock", out cached)) return cached;
            Texture2D t = NewTex(12, 14);
            Color face = RGB(0xF4, 0xF0, 0xE4), rim = RGB(0x6A, 0x4A, 0x2E), hand = RGB(0x3A, 0x32, 0x2A);
            Rect(t, 1, 2, 10, 10, rim);
            Rect(t, 2, 3, 8, 8, face);
            Rect(t, 5, 0, 2, 2, rim);                                                // 挂点
            VLine(t, 5, 4, 6, hand); HLine(t, 5, 6, 8, hand); P(t, 5, 6, hand);      // 时针+分针
            P(t, 2, 3, RGB(0xC0, 0xB8, 0xA8)); P(t, 9, 9, RGB(0xC0, 0xB8, 0xA8));    // 钟面铆点
            Apply(t);
            return Finish(t, "prop_wall_clock");
        }

        /// <summary>壁灯(V0.9.14):黄铜托架+暖罩灯,10×12——餐厅/影院/茶室的墙面件(成对挂)。</summary>
        public static Sprite WallLamp()
        {
            Sprite cached;
            if (TryGetCached("prop_wall_lamp", out cached)) return cached;
            Texture2D t = NewTex(10, 12);
            Color brass = RGB(0xB0, 0x8A, 0x3E), brassD = RGB(0x8E, 0x6C, 0x2C);
            Color shade = RGB(0xE8, 0xC8, 0x8A), glow = RGB(0xF8, 0xE8, 0xB8);
            Rect(t, 4, 0, 6, 4, shade); HLine(t, 0, 4, 9, brass); HLine(t, 1, 5, 8, glow);  // 罩+受光檐
            VLine(t, 4, 4, 9, brass); VLine(t, 5, 4, 9, brassD); Rect(t, 2, 9, 6, 2, brass); // 托臂+壁座
            P(t, 7, 5, glow); P(t, 6, 6, glow);                                       // 暖光晕
            Apply(t);
            return Finish(t, "prop_wall_lamp");
        }

        /// <summary>零嘴亭吊灯：短绳+橙色搪瓷灯罩+可见灯泡，24×24。</summary>
        public static Sprite SnackPendantLamp()
        {
            Sprite cached;
            if (TryGetCached("prop_snack_pendant", out cached)) return cached;
            Texture2D t = NewTex(24, 24);
            Color cord = RGB(0x66, 0x4A, 0x32), metal = RGB(0xB4, 0x78, 0x32);
            Color shade = RGB(0xE5, 0x9A, 0x42), shadeD = RGB(0xB9, 0x68, 0x2D);
            Color bulb = RGB(0xFF, 0xEF, 0xB0), core = RGB(0xFF, 0xFA, 0xD8);
            VLine(t, 11, 14, 23, cord); VLine(t, 12, 14, 23, Shade(cord, 1.18f));
            Rect(t, 9, 12, 6, 3, metal); Frame(t, 9, 12, 6, 3, Shade(metal, .72f));
            HLine(t, 11, 7, 16, shadeD); Rect(t, 8, 8, 8, 4, shade);
            P(t, 7, 8, shadeD); P(t, 16, 8, shadeD); HLine(t, 7, 8, 15, Shade(shade, 1.14f));
            Rect(t, 10, 4, 4, 4, bulb); P(t, 11, 5, core); P(t, 12, 5, core);
            Apply(t);
            return Finish(t, "prop_snack_pendant");
        }

        /// <summary>零嘴亭吊灯局部暖光：只覆盖灯泡周围，不再为整面墙添加发白矩形。</summary>
        public static Sprite SnackPendantGlow()
        {
            Sprite cached;
            if (TryGetCached("prop_snack_pendant_glow", out cached)) return cached;
            Texture2D t = NewTex(24, 24);
            Color outer = new Color32(0xFF, 0xD7, 0x78, 42);
            Color middle = new Color32(0xFF, 0xE2, 0x8C, 78);
            Color core = new Color32(0xFF, 0xF5, 0xC8, 190);
            Rect(t, 5, 1, 14, 10, outer);
            Rect(t, 7, 2, 10, 8, middle);
            Rect(t, 9, 4, 6, 5, core);
            P(t, 5, 1, Color.clear); P(t, 18, 1, Color.clear);
            P(t, 5, 10, Color.clear); P(t, 18, 10, Color.clear);
            Apply(t);
            return Finish(t, "prop_snack_pendant_glow");
        }

        /// <summary>立式菜单黑板(V0.9.14 餐厅):斜撑小黑板+粉笔菜行+汤碗角标,16×24——
        /// 「今日例汤」是餐厅辨识件。</summary>
        public static Sprite ChalkMenuBoard()
        {
            Sprite cached;
            if (TryGetCached("prop_menu_board", out cached)) return cached;
            Texture2D t = NewTex(16, 24);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C);
            Color board = RGB(0x2E, 0x3A, 0x32), chalk = RGB(0xE8, 0xE8, 0xE0);
            Rect(t, 1, 6, 14, 15, wood); Rect(t, 3, 8, 10, 11, board);
            Rect(t, 1, 0, 2, 7, woodD); Rect(t, 13, 0, 2, 7, woodD);                  // 斜撑前腿
            Rect(t, 6, 0, 4, 7, wood);                                               // 背撑
            HLine(t, 9, 4, 11, chalk); HLine(t, 10, 4, 10, chalk); HLine(t, 11, 5, 12, chalk); // 菜行
            P(t, 4, 14, RGB(0xE8, 0xC8, 0x8A)); Rect(t, 4, 15, 4, 2, RGB(0xE8, 0xE8, 0xE0)); // 角标汤碗
            P(t, 12, 12, chalk);
            Apply(t);
            return Finish(t, "prop_menu_board");
        }

        /// <summary>电影海报(V0.9.14 影院):竖版海报框+片名条+主画,16×22,两变体——
        /// 0 紫夜场(月+星)/1 蓝星场(星轨)。成对挂墙是影院辨识件。</summary>
        public static Sprite MoviePoster(int variant)
        {
            string key = "prop_movie_poster_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 22);
            Color frame = RGB(0x6A, 0x4A, 0x2E), paper = RGB(0xF4, 0xEE, 0xE0);
            Color bg = variant == 0 ? RGB(0x46, 0x34, 0x64) : RGB(0x34, 0x4E, 0x6E);
            Color accent = variant == 0 ? RGB(0xE2, 0xC2, 0x5A) : RGB(0x9A, 0xC8, 0xE8);
            Rect(t, 0, 0, 16, 22, frame); Rect(t, 2, 2, 12, 18, paper);
            Rect(t, 3, 3, 10, 12, bg);
            if (variant == 0) { P(t, 10, 5, accent); P(t, 6, 7, RGB(0xE8, 0xE8, 0xF0)); P(t, 8, 9, RGB(0xE8, 0xE8, 0xF0)); P(t, 5, 11, accent); }
            else { HLine(t, 4, 6, 11, accent); HLine(t, 5, 7, 10, accent); P(t, 9, 5, RGB(0xF4, 0xF4, 0xF0)); }
            Rect(t, 3, 16, 10, 3, accent); HLine(t, 17, 4, 11, variant == 0 ? RGB(0x6A, 0x50, 0x8A) : RGB(0x50, 0x6A, 0x8A)); // 片名条
            P(t, 1, 1, RGB(0xA4, 0x7A, 0x50)); P(t, 14, 1, RGB(0xA4, 0x7A, 0x50));
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>徽章挂旗(V0.9.14 市政厅/央行):竖旗+盾徽+流苏穗,18×24——机构大厅的威仪件。</summary>
        public static Sprite EmblemBanner()
        {
            Sprite cached;
            if (TryGetCached("prop_emblem", out cached)) return cached;
            Texture2D t = NewTex(18, 24);
            Color cloth = RGB(0x7E, 0x38, 0x32), clothD = RGB(0x64, 0x28, 0x24), gold = RGB(0xC9, 0xA4, 0x4A);
            Color shield = RGB(0xE8, 0xE0, 0xCC), shieldD = RGB(0xC4, 0xB8, 0x9C);
            Rect(t, 1, 0, 16, 18, cloth); Frame(t, 1, 0, 16, 18, clothD);
            Rect(t, 1, 0, 16, 2, gold);                                              // 旗杆横梁(金)
            for (int x = 1; x < 17; x += 4) { P(t, x, 18, gold); P(t, x + 2, 20, gold); } // 三角流苏
            Rect(t, 5, 5, 8, 9, shield); Frame(t, 5, 5, 8, 9, shieldD);
            Rect(t, 7, 7, 4, 4, gold); P(t, 8, 8, cloth); P(t, 9, 8, cloth);          // 盾心徽记
            P(t, 4, 2, gold); P(t, 13, 2, gold);
            Apply(t);
            return Finish(t, "prop_emblem");
        }

        /// <summary>干花束(V0.9.14 花店):倒挂麻绳束+穗状干花,14×20——花房的墙面件(成对挂)。</summary>
        public static Sprite DriedFlowers()
        {
            Sprite cached;
            if (TryGetCached("prop_dried_flowers", out cached)) return cached;
            Texture2D t = NewTex(14, 20);
            Color rope = RGB(0xC9, 0xA8, 0x72), stem = RGB(0x9A, 0x7E, 0x4E);
            Color bud1 = RGB(0xC9, 0x8B, 0x96), bud2 = RGB(0xC4, 0x9A, 0x5A), bud3 = RGB(0x8E, 0x7A, 0x5A);
            P(t, 7, 0, rope); Rect(t, 6, 1, 3, 2, rope); HLine(t, 2, 3, 12, rope);   // 挂绳+横结
            Rect(t, 6, 3, 3, 10, stem);                                              // 主茎
            P(t, 4, 6, bud1); P(t, 3, 8, bud1); P(t, 10, 5, bud2); P(t, 11, 7, bud2); // 两侧穗花
            P(t, 4, 10, bud3); P(t, 10, 9, bud3); P(t, 5, 13, bud1); P(t, 9, 12, bud2);
            VLine(t, 5, 12, 16, stem); VLine(t, 8, 12, 15, stem); VLine(t, 7, 13, 17, stem); // 束尾
            Apply(t);
            return Finish(t, "prop_dried_flowers");
        }

        /// <summary>挂零食袋串(V0.9.14 零嘴亭):横杆+三只彩袋,18×14——小卖部的墙面件。</summary>
        public static Sprite SnackString()
        {
            Sprite cached;
            if (TryGetCached("prop_snack_string", out cached)) return cached;
            Texture2D t = NewTex(18, 14);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C);
            Color[] bags = { RGB(0xC2, 0x58, 0x48), RGB(0xE2, 0xC2, 0x5A), RGB(0x5E, 0x8A, 0x5A) };
            HLine(t, 0, 2, 17, wood); VLine(t, 0, 1, 4, woodD); VLine(t, 17, 1, 4, woodD); // 横杆
            for (int i = 0; i < 3; i++)
            {
                int bx = 2 + i * 6;
                VLine(t, bx + 2, 3, 4, woodD);                                       // 挂绳
                Rect(t, bx, 4, 5, 7, bags[i]); Frame(t, bx, 4, 5, 7, Shade(bags[i], .78f));
                P(t, bx + 2, 6, RGB(0xF4, 0xF0, 0xE4));                              // 袋面标签点
            }
            Apply(t);
            return Finish(t, "prop_snack_string");
        }

        /// <summary>工具挂板(V0.9.14 工厂):洞洞板+扳手/锤/钳剪影+挂钉,26×22——车间的墙面件。</summary>
        public static Sprite ToolWall()
        {
            Sprite cached;
            if (TryGetCached("prop_tool_wall", out cached)) return cached;
            Texture2D t = NewTex(26, 22);
            Color board = RGB(0x9A, 0x82, 0x62), boardD = RGB(0x7E, 0x6A, 0x4E);
            Color steel = RGB(0x8A, 0x8E, 0x94), steelD = RGB(0x5A, 0x5E, 0x64), wood = RGB(0xB0, 0x84, 0x54);
            Rect(t, 0, 0, 26, 22, board); Frame(t, 0, 0, 26, 22, boardD);
            for (int y = 3; y < 20; y += 4) for (int x = 2; x < 25; x += 4) P(t, x, y, boardD); // 洞阵
            Rect(t, 3, 3, 8, 2, steel); Rect(t, 9, 5, 2, 2, steel); P(t, 11, 4, steelD);        // 扳手
            Rect(t, 15, 3, 5, 2, steel); Rect(t, 14, 5, 2, 6, steelD); Rect(t, 15, 10, 4, 3, wood); // 锤(柄木)
            Rect(t, 3, 12, 2, 7, steelD); Rect(t, 5, 12, 4, 2, steel); Rect(t, 5, 17, 4, 2, steel); // 钳
            Rect(t, 21, 6, 2, 8, wood); P(t, 20, 5, steel); P(t, 23, 5, steel);                 // 螺丝刀排
            Apply(t);
            return Finish(t, "prop_tool_wall");
        }

        /// <summary>世界地图挂画(V0.9.14 培训中心/书店):蓝海+陆地色块+经纬线+木框,26×18——
        /// 教室与书店共用的墙面件(错开变体用)。</summary>
        public static Sprite WorldMap()
        {
            Sprite cached;
            if (TryGetCached("prop_world_map", out cached)) return cached;
            Texture2D t = NewTex(26, 18);
            Color frame = RGB(0x6A, 0x4A, 0x2E), sea = RGB(0x8E, 0xB4, 0xC8), seaD = RGB(0x7A, 0xA2, 0xB8);
            Color land = RGB(0xD8, 0xC8, 0x9A), landD = RGB(0xC2, 0xB0, 0x82);
            Rect(t, 0, 0, 26, 18, frame); Rect(t, 2, 2, 22, 14, sea);
            Rect(t, 4, 4, 7, 5, land); P(t, 6, 9, land); P(t, 5, 10, landD);         // 西陆
            Rect(t, 12, 3, 8, 6, land); Rect(t, 14, 9, 5, 4, land); P(t, 18, 13, landD); // 东陆+岛
            Rect(t, 9, 11, 2, 2, landD);                                              // 群岛
            HLine(t, 8, 2, 23, seaD); VLine(t, 12, 2, 15, seaD);                      // 经纬线
            P(t, 1, 1, RGB(0xA4, 0x7A, 0x50)); P(t, 24, 1, RGB(0xA4, 0x7A, 0x50));
            Apply(t);
            return Finish(t, "prop_world_map");
        }

        /// <summary>意见箱(V0.9.14 市政厅):挂墙小木箱+投递口+纸条,14×16——政务大厅的细节件。</summary>
        public static Sprite SuggestionBox()
        {
            Sprite cached;
            if (TryGetCached("prop_suggestion_box", out cached)) return cached;
            Texture2D t = NewTex(14, 16);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xA4, 0x7A, 0x50);
            Color paper = RGB(0xF4, 0xF0, 0xE4);
            Rect(t, 1, 2, 12, 12, wood); Frame(t, 1, 2, 12, 12, woodD);
            Rect(t, 3, 5, 8, 2, woodD);                                              // 投递口
            Rect(t, 5, 4, 3, 2, paper);                                              // 半投纸条
            P(t, 2, 3, woodL); P(t, 11, 3, woodL); Rect(t, 6, 0, 2, 2, woodD);       // 挂耳
            HLine(t, 13, 3, 10, woodD); P(t, 7, 12, woodL);
            Apply(t);
            return Finish(t, "prop_suggestion_box");
        }

        // ————— V0.9.14 第二批:地面特色件(爆米花机/红毯/屏风/关东煮/杂志架/取书梯/落地灯/
        // 吊篮/取号机/隔离柱/金库门/文件柜/大盆栽/地球仪/油桶/灭火器/水果箱堆/书堆/促销牌) —————

        /// <summary>爆米花机(V0.9.14 影院):红白条纹小车+玻璃罩+爆米花堆,20×26——影院候场辨识件。</summary>
        public static Sprite PopcornMachine()
        {
            Sprite cached;
            if (TryGetCached("prop_popcorn", out cached)) return cached;
            Texture2D t = NewTex(20, 26);
            Color red = RGB(0xC2, 0x4A, 0x3E), cream = RGB(0xF4, 0xE8, 0xD0);
            Color glass = RGB(0xB8, 0xD0, 0xDC), corn = RGB(0xF2, 0xE2, 0xA8), cornD = RGB(0xE0, 0xC8, 0x84);
            Rect(t, 2, 8, 16, 12, red); HLine(t, 9, 2, 17, cream);                    // 条纹车身
            Rect(t, 4, 10, 12, 8, glass); Frame(t, 4, 10, 12, 8, RGB(0x8A, 0x8E, 0x94));
            Rect(t, 5, 11, 10, 3, corn); P(t, 6, 10, corn); P(t, 9, 10, cornD); P(t, 12, 10, corn); // 爆米花堆
            Rect(t, 5, 14, 8, 3, RGB(0x8A, 0x5A, 0x3A));                             // 接物斗
            Rect(t, 3, 20, 14, 3, cream); HLine(t, 21, 3, 16, RGB(0xD8, 0xC8, 0xA8)); // 台面
            Rect(t, 3, 23, 3, 3, RGB(0x6A, 0x46, 0x2C)); Rect(t, 14, 23, 3, 3, RGB(0x6A, 0x46, 0x2C)); // 轮脚
            VLine(t, 9, 5, 8, RGB(0x8A, 0x8E, 0x94)); P(t, 9, 4, red); P(t, 10, 4, red); // 顶旗杆+旗
            Apply(t);
            return Finish(t, "prop_popcorn");
        }

        /// <summary>红毯(V0.9.14 影院/银行):竖长毯(门内铺向主区)+金边+踏光,16×44——仪式件,
        /// isTrigger 摆放(可踩行)。</summary>
        public static Sprite RedCarpet()
        {
            Sprite cached;
            if (TryGetCached("prop_red_carpet", out cached)) return cached;
            Texture2D t = NewTex(16, 44);
            Color rug = RGB(0x8E, 0x36, 0x30), rugD = RGB(0x76, 0x2A, 0x26), gold = RGB(0xC9, 0xA4, 0x4A);
            Rect(t, 1, 0, 14, 44, rug);
            VLine(t, 0, 0, 43, gold); VLine(t, 15, 0, 43, gold); HLine(t, 0, 0, 15, gold); HLine(t, 43, 0, 15, gold); // 金边框
            VLine(t, 3, 2, 41, rugD); VLine(t, 12, 2, 41, rugD);                      // 织纹竖线
            for (int y = 5; y < 40; y += 9) { P(t, 7, y, gold); P(t, 8, y, gold); P(t, 7, y + 1, rugD); P(t, 8, y + 1, rugD); } // 暗纹徽点
            HLine(t, 20, 2, 13, RGB(0xA4, 0x44, 0x3C));                               // 中段受光
            Apply(t);
            return Finish(t, "prop_red_carpet");
        }

        /// <summary>地毯(V0.9.14 我的家):平面地垫两变体——0 暖条纹/1 格纹,40×24——
        /// 生活区的软装件,isTrigger 摆放(可踩行)。</summary>
        public static Sprite AreaRug(int variant)
        {
            string key = "prop_area_rug_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(40, 24);
            Color baseC = variant == 0 ? RGB(0xC9, 0x9A, 0x6A) : RGB(0x9A, 0x7E, 0x92);
            Color stripe = variant == 0 ? RGB(0xB0, 0x7E, 0x50) : RGB(0x84, 0x6A, 0x7A);
            Color edge = variant == 0 ? RGB(0x8A, 0x5E, 0x38) : RGB(0x6A, 0x54, 0x62);
            Rect(t, 0, 0, 40, 24, baseC);
            Frame(t, 0, 0, 40, 24, edge);
            if (variant == 0)
            {
                for (int y = 3; y < 22; y += 5) HLine(t, y, 2, 37, stripe);           // 暖条纹
                HLine(t, 11, 3, 36, RGB(0xD8, 0xAE, 0x80));                           // 受光
            }
            else
            {
                for (int y = 3; y < 22; y += 4) HLine(t, y, 2, 37, stripe);
                for (int x = 3; x < 38; x += 4) VLine(t, x, 2, 21, StripeSoft(stripe));
                P(t, 19, 11, RGB(0xB4, 0x94, 0xAA)); P(t, 20, 12, RGB(0xB4, 0x94, 0xAA));
            }
            Apply(t);
            return Finish(t, key);
        }

        static Color StripeSoft(Color c) { return new Color(c.r * 1.12f, c.g * 1.12f, c.b * 1.12f, 1f); }

        /// <summary>木格屏风(V0.9.14 茶室):三折格棂+木框+落脚墩,24×36——茶室半私密区的隔断件。</summary>
        public static Sprite WoodenScreen()
        {
            Sprite cached;
            if (TryGetCached("prop_screen", out cached)) return cached;
            Texture2D t = NewTex(24, 36);
            Color wood = RGB(0x8A, 0x6A, 0x44), woodD = RGB(0x66, 0x4C, 0x30), woodL = RGB(0xA0, 0x80, 0x56);
            Color paper = RGB(0xF0, 0xE8, 0xD2);
            Rect(t, 0, 2, 24, 30, wood); Frame(t, 0, 2, 24, 30, woodD);
            VLine(t, 11, 2, 31, woodD); VLine(t, 12, 2, 31, woodD);                  // 折痕
            Rect(t, 3, 5, 7, 24, paper); Rect(t, 14, 5, 7, 24, paper);               // 两扇纸面
            for (int y = 7; y < 28; y += 5) { HLine(t, 3, y, 9, wood); HLine(t, 14, y, 20, wood); } // 格棂
            for (int x = 4; x < 10; x += 4) VLine(t, x, 6, 28, Shade(woodL, 1.08f));
            Rect(t, 2, 32, 5, 3, woodD); Rect(t, 17, 32, 5, 3, woodD);               // 落脚墩
            P(t, 11, 1, woodL); P(t, 12, 1, woodL);
            Apply(t);
            return Finish(t, "prop_screen");
        }

        /// <summary>关东煮台(V0.9.14 便利屋):台车+双汤锅+玻璃盖+串串,26×24——便利店热食辨识件。</summary>
        public static Sprite OdenCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_oden", out cached)) return cached;
            Texture2D t = NewTex(26, 24);
            Color steel = RGB(0x8A, 0x8E, 0x94), steelD = RGB(0x6A, 0x6E, 0x74);
            Color broth = RGB(0xE2, 0xC2, 0x8A), brothD = RGB(0xC9, 0xA8, 0x6A);
            Color glass = RGB(0xD4, 0xE2, 0xE8);
            Rect(t, 1, 6, 24, 14, steel); Frame(t, 1, 6, 24, 14, steelD);            // 台车
            HLine(t, 8, 2, 23, RGB(0xB4, 0xB8, 0xBE));
            Rect(t, 3, 8, 9, 8, broth); Rect(t, 14, 8, 9, 8, broth);                 // 双汤锅
            Frame(t, 3, 8, 9, 8, steelD); Frame(t, 14, 8, 9, 8, steelD);
            P(t, 5, 9, brothD); P(t, 7, 10, RGB(0xF4, 0xE8, 0xD0)); P(t, 16, 9, brothD);
            VLine(t, 6, 9, 12, RGB(0xD8, 0xA8, 0x5A)); P(t, 6, 13, RGB(0xE8, 0xE8, 0xE0)); // 萝卜串
            VLine(t, 9, 9, 12, RGB(0xC8, 0x94, 0x4A)); P(t, 9, 13, RGB(0xE8, 0xE8, 0xE0)); // 牛筋串
            VLine(t, 17, 9, 12, RGB(0xC8, 0x94, 0x4A)); P(t, 17, 13, RGB(0xE8, 0xE8, 0xE0));
            VLine(t, 20, 9, 12, RGB(0xD8, 0xA8, 0x5A)); P(t, 20, 13, RGB(0xE8, 0xE8, 0xE0));
            Rect(t, 2, 3, 22, 4, glass); HLine(t, 6, 3, 23, steel);                  // 玻璃防尘罩
            Rect(t, 3, 20, 3, 4, steelD); Rect(t, 20, 20, 3, 4, steelD);             // 台脚
            Apply(t);
            return Finish(t, "prop_oden");
        }

        /// <summary>杂志架(V0.9.14 便利屋/书店):斜面三层+彩面杂志,18×22——报刊架密度件。</summary>
        public static Sprite MagazineRack()
        {
            Sprite cached;
            if (TryGetCached("prop_magazine_rack", out cached)) return cached;
            Texture2D t = NewTex(18, 22);
            Color steel = RGB(0x7A, 0x7E, 0x84), steelD = RGB(0x5A, 0x5E, 0x64);
            Color[] covers = { RGB(0xC2, 0x58, 0x48), RGB(0x5A, 0x7A, 0x9A), RGB(0x5E, 0x8A, 0x5A), RGB(0xE2, 0xC2, 0x5A) };
            Rect(t, 1, 4, 16, 14, steel); Frame(t, 1, 4, 16, 14, steelD);
            for (int s = 0; s < 3; s++)
            {
                int sy = 6 + s * 4;
                HLine(t, sy + 3, 3, 14, steelD);                                     // 斜托
                Rect(t, 3, sy, 12, 3, covers[s]);                                    // 杂志排
                Rect(t, 3, sy, 3, 3, covers[(s + 1) % 4]);
            }
            Rect(t, 2, 18, 14, 2, steelD); Rect(t, 3, 20, 2, 2, steelD); Rect(t, 13, 20, 2, 2, steelD); // 底座脚
            P(t, 4, 5, covers[3]); P(t, 13, 9, covers[3]);
            Apply(t);
            return Finish(t, "prop_magazine_rack");
        }

        /// <summary>取书梯(V0.9.14 书店):A 字梯+横档+顶部挂钩,18×34——书店高架的取书件。</summary>
        public static Sprite LibraryLadder()
        {
            Sprite cached;
            if (TryGetCached("prop_library_ladder", out cached)) return cached;
            Texture2D t = NewTex(18, 34);
            Color wood = RGB(0x9A, 0x6A, 0x40), woodD = RGB(0x76, 0x4E, 0x2E), woodL = RGB(0xB8, 0x86, 0x54);
            for (int y = 2; y < 32; y++)                                             // A 字两腿
            {
                int spread = (int)((31 - y) * .12f);
                VLine(t, 8 - spread / 2, y, y, wood);
                VLine(t, 9 + spread / 2, y, y, woodD);
            }
            Rect(t, 7, 0, 4, 3, woodL);                                              // 顶挂钩
            for (int r = 0; r < 6; r++)
            {
                int ry = 6 + r * 4;
                int spread = (int)((31 - ry) * .12f);
                Rect(t, 8 - spread / 2, ry, spread + 1, 2, woodL);                   // 横档
            }
            Rect(t, 6, 31, 7, 2, woodD); Rect(t, 5, 32, 4, 2, woodD); Rect(t, 10, 32, 4, 2, woodD); // 撑脚
            Apply(t);
            return Finish(t, "prop_library_ladder");
        }

        /// <summary>落地灯(V0.9.14 书店/我的家):灯罩+杆+底座+暖光,16×30——阅读角落灯。</summary>
        public static Sprite FloorLamp()
        {
            Sprite cached;
            if (TryGetCached("prop_floor_lamp", out cached)) return cached;
            Texture2D t = NewTex(16, 30);
            Color shade = RGB(0xE8, 0xC8, 0x8A), shadeD = RGB(0xD0, 0xAE, 0x6E);
            Color pole = RGB(0x6A, 0x4A, 0x2E), glow = RGB(0xF8, 0xE8, 0xB8);
            Rect(t, 3, 2, 10, 6, shade); Frame(t, 3, 2, 10, 6, shadeD);
            HLine(t, 3, 7, 12, shadeD); HLine(t, 4, 8, 11, glow);                    // 罩檐+暖光
            VLine(t, 7, 8, 24, pole); VLine(t, 8, 8, 24, Shade(pole, 1.2f));
            Rect(t, 4, 24, 8, 3, pole); Rect(t, 2, 26, 12, 3, pole);                 // 灯颈+底座
            P(t, 3, 28, shadeD); P(t, 12, 28, shadeD);
            Apply(t);
            return Finish(t, "prop_floor_lamp");
        }

        /// <summary>吊挂花篮(V0.9.14 花店):三链+篮+垂花,16×18——花房顶部垂绿件(成对挂)。</summary>
        public static Sprite HangingBasket()
        {
            Sprite cached;
            if (TryGetCached("prop_hanging_basket", out cached)) return cached;
            Texture2D t = NewTex(16, 18);
            Color chain = RGB(0x8A, 0x8E, 0x94), weave = RGB(0xC9, 0xA8, 0x72), weaveD = RGB(0xA8, 0x8A, 0x5A);
            Color[] blooms = { RGB(0xE2, 0x6A, 0x6A), RGB(0xE2, 0x9A, 0xC2), RGB(0xF2, 0xD8, 0x66) };
            VLine(t, 3, 0, 5, chain); VLine(t, 12, 0, 5, chain); VLine(t, 7, 0, 4, chain); // 三链
            HLine(t, 3, 5, 12, chain);
            Rect(t, 3, 6, 10, 3, weave); HLine(t, 7, 4, 11, weaveD);                 // 篮沿
            Rect(t, 4, 9, 8, 4, weave); HLine(t, 12, 5, 10, weaveD);                 // 篮身
            P(t, 5, 6, blooms[0]); P(t, 8, 6, blooms[1]); P(t, 10, 6, blooms[2]);     // 篮口花
            VLine(t, 6, 13, 16, RGB(0x5E, 0x8A, 0x5A)); VLine(t, 9, 13, 15, RGB(0x5E, 0x8A, 0x5A)); // 垂蔓
            P(t, 6, 17, blooms[1]); P(t, 9, 16, blooms[0]);
            Apply(t);
            return Finish(t, "prop_hanging_basket");
        }

        /// <summary>取号机(V0.9.14 银行):立柱+触屏+出票口+底座,14×24——银行排队辨识件。</summary>
        public static Sprite QueueMachine()
        {
            Sprite cached;
            if (TryGetCached("prop_queue_machine", out cached)) return cached;
            Texture2D t = NewTex(14, 24);
            Color shell = RGB(0x5E, 0x6E, 0x7E), shellD = RGB(0x46, 0x54, 0x62);
            Color screen = RGB(0x9A, 0xD4, 0xE8), paper = RGB(0xF4, 0xF0, 0xE4);
            Rect(t, 2, 4, 10, 16, shell); Frame(t, 2, 4, 10, 16, shellD);
            Rect(t, 3, 6, 8, 6, screen); HLine(t, 7, 4, 9, RGB(0x6A, 0xA4, 0xC0));  // 触屏
            P(t, 4, 7, RGB(0xE8, 0xF4, 0xF8)); P(t, 6, 8, RGB(0xE8, 0xF4, 0xF8));
            Rect(t, 4, 13, 6, 2, shellD); Rect(t, 5, 15, 4, 3, paper);               // 出票口+票
            HLine(t, 17, 4, 9, shellD);
            Rect(t, 1, 20, 12, 3, shellD); Rect(t, 3, 1, 8, 3, shell);               // 底座+顶檐
            P(t, 2, 2, RGB(0x6A, 0xE2, 0x8A));                                       // 指示灯
            Apply(t);
            return Finish(t, "prop_queue_machine");
        }

        /// <summary>隔离柱(V0.9.14 银行/市政厅):铜座+立柱+球顶,10×28——排队动线件(成排摆)。</summary>
        public static Sprite RopeStanchion()
        {
            Sprite cached;
            if (TryGetCached("prop_stanchion", out cached)) return cached;
            Texture2D t = NewTex(10, 28);
            Color brass = RGB(0xB0, 0x8A, 0x3E), brassD = RGB(0x8E, 0x6C, 0x2C), brassL = RGB(0xD0, 0xAC, 0x60);
            Rect(t, 1, 22, 8, 4, brassD); HLine(t, 26, 1, 8, RGB(0x74, 0x58, 0x24)); // 底座
            VLine(t, 4, 6, 22, brass); VLine(t, 5, 6, 22, brassD);                   // 柱身
            P(t, 4, 5, brassL); P(t, 5, 5, brassL); Rect(t, 3, 3, 4, 2, brassL);     // 球顶
            P(t, 2, 9, brassD); P(t, 7, 12, brassD);                                 // 高光纹
            Apply(t);
            return Finish(t, "prop_stanchion");
        }

        /// <summary>金库门(V0.9.14 央行):大圆门+辐条锁盘+铆钉框,30×30——央行库房辨识件。</summary>
        public static Sprite VaultDoor()
        {
            Sprite cached;
            if (TryGetCached("prop_vault_door", out cached)) return cached;
            Texture2D t = NewTex(30, 30);
            Color steel = RGB(0x8E, 0x92, 0x98), steelD = RGB(0x6A, 0x6E, 0x74), steelL = RGB(0xAC, 0xB0, 0xB6);
            Color dark = RGB(0x4A, 0x4E, 0x54);
            Rect(t, 0, 0, 30, 30, steel); Frame(t, 0, 0, 30, 30, steelD);
            for (int x = 2; x < 28; x += 5) { P(t, x, 1, steelD); P(t, x, 28, steelD); P(t, 1, x, steelD); P(t, 28, x, steelD); } // 铆钉圈
            Rect(t, 4, 4, 22, 22, dark); Frame(t, 4, 4, 22, 22, steelD);             // 门洞
            Rect(t, 6, 6, 18, 18, steel); Frame(t, 6, 6, 18, 18, steelL);
            for (int i = 0; i < 4; i++)                                              // 辐条锁盘
            {
                int a = i * 5;
                VLine(t, 14 + (i % 2 == 0 ? -a / 2 : a / 2) + 1, 7 + a / 3, 22 - a / 3, steelD);
                HLine(t, 7 + a / 3, 8 + (i % 2 == 0 ? -a / 2 : a / 2), 21 - (i % 2 == 0 ? -a / 2 : a / 2) + 1, steelD);
            }
            Rect(t, 12, 12, 6, 6, steelD); P(t, 14, 14, steelL); P(t, 15, 14, steelL); // 盘心轴
            Rect(t, 26, 12, 3, 2, steelL); Rect(t, 26, 16, 3, 2, steelD);            // 门侧锁栓
            Apply(t);
            return Finish(t, "prop_vault_door");
        }

        /// <summary>文件柜(V0.9.14 央行/银行):三层抽屉+拉手+标签,18×26——机构库房件。</summary>
        public static Sprite FilingCabinet()
        {
            Sprite cached;
            if (TryGetCached("prop_filing_cabinet", out cached)) return cached;
            Texture2D t = NewTex(18, 26);
            Color steel = RGB(0x7E, 0x86, 0x90), steelD = RGB(0x5E, 0x66, 0x70), steelL = RGB(0x96, 0x9E, 0xA8);
            Rect(t, 1, 2, 16, 22, steel); Frame(t, 1, 2, 16, 22, steelD);
            for (int d = 0; d < 3; d++)
            {
                int dy = 4 + d * 7;
                Rect(t, 3, dy, 12, 5, Shade(steel, .92f)); Frame(t, 3, dy, 12, 5, steelD);
                Rect(t, 7, dy + 4, 4, 1, steelL);                                    // 拉手槽
                Rect(t, 4, dy + 1, 5, 1, RGB(0xE8, 0xE0, 0xC8));                     // 标签
                P(t, 13, dy + 2, steelD);
            }
            Rect(t, 2, 24, 14, 2, steelD); P(t, 2, 3, steelL); P(t, 15, 20, steelL);
            Apply(t);
            return Finish(t, "prop_filing_cabinet");
        }

        /// <summary>大盆栽(V0.9.14 银行/市政厅/央行):多枝阔叶+陶盆,20×30——机构大厅的绿件
        /// (替代千篇一律的 PottedPlant 双角模式)。</summary>
        public static Sprite BigPlant()
        {
            Sprite cached;
            if (TryGetCached("prop_big_plant", out cached)) return cached;
            Texture2D t = NewTex(20, 30);
            Color leaf = RGB(0x5E, 0x8A, 0x5A), leafD = RGB(0x4A, 0x72, 0x48), leafL = RGB(0x76, 0xA2, 0x6E);
            Color pot = RGB(0xB0, 0x6A, 0x48), potD = RGB(0x8E, 0x52, 0x36);
            VLine(t, 9, 14, 20, RGB(0x6A, 0x4E, 0x36)); VLine(t, 10, 14, 20, RGB(0x5A, 0x42, 0x2C)); // 主干
            Rect(t, 3, 3, 6, 5, leaf); P(t, 2, 6, leafD); P(t, 8, 3, leafL);         // 左枝叶
            Rect(t, 11, 2, 6, 6, leaf); P(t, 17, 5, leafD); P(t, 11, 2, leafL);      // 右枝叶
            Rect(t, 6, 7, 8, 5, leafD); P(t, 7, 6, leaf); P(t, 13, 11, leafL);       // 中层
            Rect(t, 2, 9, 4, 4, leaf); Rect(t, 14, 9, 4, 4, leafD);                  // 垂枝
            Rect(t, 4, 20, 12, 5, pot); HLine(t, 20, 5, 14, potD); Rect(t, 5, 25, 10, 3, potD); // 陶盆
            HLine(t, 21, 4, 15, RGB(0xC8, 0x84, 0x5E));
            Apply(t);
            return Finish(t, "prop_big_plant");
        }

        /// <summary>地球仪(V0.9.14 培训中心):弧架+球+底座,16×22——教室讲台件。</summary>
        public static Sprite GlobeStand()
        {
            Sprite cached;
            if (TryGetCached("prop_globe", out cached)) return cached;
            Texture2D t = NewTex(16, 22);
            Color sea = RGB(0x6A, 0x94, 0xAE), land = RGB(0xC9, 0xB4, 0x84), landD = RGB(0xB0, 0x9A, 0x6C);
            Color brass = RGB(0xB0, 0x8A, 0x3E), brassD = RGB(0x8E, 0x6C, 0x2C);
            Rect(t, 4, 4, 8, 8, sea); Frame(t, 4, 4, 8, 8, brassD);
            Rect(t, 5, 5, 3, 2, land); P(t, 8, 6, landD); Rect(t, 8, 8, 3, 2, land); P(t, 6, 9, landD); // 陆块
            HLine(t, 8, 4, 11, brassD);                                              // 赤道弧
            VLine(t, 3, 2, 12, brass); VLine(t, 12, 2, 12, brass); Rect(t, 2, 1, 12, 1, brass); // 经弧
            Rect(t, 6, 12, 4, 3, brass); Rect(t, 4, 15, 8, 3, brassD); HLine(t, 15, 5, 10, brass); // 支柱底座
            P(t, 7, 3, RGB(0xF4, 0xF4, 0xF0));                                       // 高光
            Apply(t);
            return Finish(t, "prop_globe");
        }

        /// <summary>油桶(V0.9.14 工厂):桶身+双箍+警示标签,16×20,两变体色——车间仓储件。</summary>
        public static Sprite OilDrum(int variant)
        {
            string key = "prop_oil_drum_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(16, 20);
            Color body = variant == 0 ? RGB(0x5A, 0x74, 0x8C) : RGB(0xC2, 0x6A, 0x3E);
            Color bodyD = Shade(body, .78f), bodyL = Shade(body, 1.15f);
            Rect(t, 2, 2, 12, 16, body); Frame(t, 2, 2, 12, 16, bodyD);
            HLine(t, 6, 2, 13, bodyD); HLine(t, 13, 2, 13, bodyD);                   // 双箍
            Rect(t, 3, 1, 10, 2, bodyL); HLine(t, 18, 3, 12, bodyD);                 // 桶盖沿
            Rect(t, 5, 8, 6, 4, RGB(0xF4, 0xF0, 0xE4)); Frame(t, 5, 8, 6, 4, bodyD); // 标签
            P(t, 6, 9, RGB(0xE2, 0x5A, 0x5A)); P(t, 8, 9, RGB(0xE2, 0x5A, 0x5A)); P(t, 10, 9, RGB(0xE2, 0x5A, 0x5A)); // 警示条
            HLine(t, 10, 6, 10, bodyD); P(t, 3, 3, bodyL); P(t, 12, 15, bodyD);
            Apply(t);
            return Finish(t, key);
        }

        /// <summary>灭火器(V0.9.14 工厂/培训中心):红瓶+黑喷嘴+压力表+挂座,10×18——安全细节件。</summary>
        public static Sprite FireExtinguisher()
        {
            Sprite cached;
            if (TryGetCached("prop_fire_ext", out cached)) return cached;
            Texture2D t = NewTex(10, 18);
            Color red = RGB(0xC2, 0x3A, 0x30), redD = RGB(0x94, 0x2A, 0x22);
            Color black = RGB(0x3A, 0x3E, 0x42), steel = RGB(0x8A, 0x8E, 0x94);
            Rect(t, 3, 5, 4, 10, red); Frame(t, 3, 5, 4, 10, redD);
            Rect(t, 3, 2, 4, 2, black);                                              // 顶阀
            VLine(t, 5, 0, 2, black); Rect(t, 6, 1, 2, 1, black);                    // 喷管拐
            P(t, 7, 3, steel);                                                       // 压力表
            P(t, 4, 6, RGB(0xE0, 0x6A, 0x60));                                       // 高光
            Rect(t, 1, 15, 8, 2, steel); Rect(t, 2, 14, 1, 1, steel); Rect(t, 7, 14, 1, 1, steel); // 挂座底
            Apply(t);
            return Finish(t, "prop_fire_ext");
        }

        /// <summary>水果箱堆(V0.9.14 果汁店/市场):两叠木箱+橙子冒尖,20×18——进货感小物。</summary>
        public static Sprite FruitCrateStack()
        {
            Sprite cached;
            if (TryGetCached("prop_fruit_crates", out cached)) return cached;
            Texture2D t = NewTex(20, 18);
            Color wood = RGB(0xB0, 0x84, 0x54), woodD = RGB(0x8A, 0x64, 0x3C);
            Color orange = RGB(0xE2, 0x82, 0x3C), orangeD = RGB(0xC4, 0x66, 0x28);
            Rect(t, 1, 1, 9, 7, wood); Frame(t, 1, 1, 9, 7, woodD);                  // 上箱
            P(t, 3, 1, orange); P(t, 5, 0, orange); P(t, 7, 1, orangeD);             // 冒尖橙
            Rect(t, 10, 1, 9, 7, wood); Frame(t, 10, 1, 9, 7, woodD);
            P(t, 12, 1, orange); P(t, 14, 0, orangeD); P(t, 16, 1, orange);
            Rect(t, 3, 8, 14, 8, wood); Frame(t, 3, 8, 14, 8, woodD);                // 下大箱
            HLine(t, 9, 4, 16, woodD); HLine(t, 13, 4, 16, woodD);
            P(t, 4, 2, RGB(0xC9, 0xA4, 0x4A)); P(t, 13, 2, RGB(0xC9, 0xA4, 0x4A));  // 箱角钉
            Apply(t);
            return Finish(t, "prop_fruit_crates");
        }

        /// <summary>地书堆(V0.9.14 书店):三本叠书+书签,14×12——阅读区散件。</summary>
        public static Sprite BookStack()
        {
            Sprite cached;
            if (TryGetCached("prop_book_stack", out cached)) return cached;
            Texture2D t = NewTex(14, 12);
            Color[] covers = { RGB(0x5A, 0x7A, 0x9A), RGB(0xC2, 0x58, 0x48), RGB(0x5E, 0x8A, 0x5A) };
            Color page = RGB(0xF4, 0xF0, 0xE4), pageD = RGB(0xD8, 0xD0, 0xBC);
            Rect(t, 1, 2, 12, 3, covers[0]); Rect(t, 2, 4, 11, 1, page); HLine(t, 3, 2, 12, pageD);
            Rect(t, 2, 5, 10, 3, covers[1]); Rect(t, 3, 7, 9, 1, page); HLine(t, 6, 3, 10, pageD);
            Rect(t, 3, 8, 8, 3, covers[2]); Rect(t, 4, 10, 7, 1, page);
            P(t, 10, 6, RGB(0xE2, 0xC2, 0x5A));                                      // 书签带
            P(t, 2, 3, RGB(0x7A, 0x9A, 0xB8)); P(t, 4, 6, RGB(0xD8, 0x74, 0x64));
            Apply(t);
            return Finish(t, "prop_book_stack");
        }

        /// <summary>促销立牌(V0.9.14 便利屋):A 字立牌+特价角标,14×20——便利店的促销件。</summary>
        public static Sprite PromoSign()
        {
            Sprite cached;
            if (TryGetCached("prop_promo_sign", out cached)) return cached;
            Texture2D t = NewTex(14, 20);
            Color board = RGB(0xF4, 0xE8, 0xD0), boardD = RGB(0xD8, 0xC8, 0xA8);
            Color red = RGB(0xC2, 0x3A, 0x30), wood = RGB(0x8A, 0x62, 0x40);
            Rect(t, 1, 1, 12, 13, board); Frame(t, 1, 1, 12, 13, boardD);
            Rect(t, 2, 0, 3, 3, red); Rect(t, 9, 0, 3, 3, red);                      // 角标
            HLine(t, 4, 3, 10, red); HLine(t, 5, 3, 10, red);                        // 「特」行
            HLine(t, 8, 3, 9, RGB(0x5A, 0x7A, 0x9A)); HLine(t, 10, 4, 9, RGB(0x5A, 0x7A, 0x9A)); // 价行
            Rect(t, 3, 14, 3, 6, wood); Rect(t, 8, 14, 3, 6, wood);                  // A 字腿
            Apply(t);
            return Finish(t, "prop_promo_sign");
        }

        // ————— V0.9.14b 差异化二稿:银行营业厅件(ATM/叫号屏/填单台/点钞机)、央行件(会议长桌/
        // 钱垛)、花店加密件(阶梯花架/盆栽排)、零嘴亭专属零食架 —————

        /// <summary>ATM 机(V0.9.14b 小镇银行):钢蓝机身+屏幕+键盘+出钞口+读卡器,22×28——
        /// 自助区成排摆,营业厅第一辨识件。</summary>
        public static Sprite AtmMachine()
        {
            Sprite cached;
            if (TryGetCached("prop_atm", out cached)) return cached;
            Texture2D t = NewTex(22, 28);
            Color body = RGB(0x56, 0x68, 0x80), bodyD = RGB(0x3E, 0x4E, 0x60), bodyL = RGB(0x6E, 0x80, 0x98);
            Color screen = RGB(0x9A, 0xD4, 0xE8), key = RGB(0xC8, 0xCC, 0xD2);
            Rect(t, 2, 2, 18, 24, body); Frame(t, 2, 2, 18, 24, bodyD);
            Rect(t, 4, 16, 14, 8, bodyD);                                                // 下段面板
            Rect(t, 5, 17, 11, 5, key);                                                  // 键盘区
            P(t, 6, 18, bodyD); P(t, 8, 18, bodyD); P(t, 10, 18, bodyD); P(t, 12, 18, bodyD);
            P(t, 7, 20, bodyD); P(t, 9, 20, bodyD); P(t, 11, 20, bodyD);                 // 键位
            Rect(t, 4, 6, 12, 8, screen); Frame(t, 4, 6, 12, 8, bodyD);                  // 屏幕
            HLine(t, 9, 5, 14, RGB(0x6A, 0xA4, 0xC0)); P(t, 6, 7, RGB(0xE8, 0xF4, 0xF8)); // 屏内容
            P(t, 12, 11, RGB(0xE2, 0xC2, 0x5A)); P(t, 13, 11, RGB(0xE2, 0xC2, 0x5A));    // 余额字
            Rect(t, 4, 4, 12, 1, bodyL);                                                 // 出钞口檐
            Rect(t, 7, 3, 7, 1, bodyD); P(t, 8, 2, RGB(0xE2, 0x5A, 0x5A));              // 读卡器+指示灯
            Rect(t, 8, 15, 5, 1, RGB(0xF4, 0xF0, 0xE4));                                 // 出票缝
            P(t, 3, 24, bodyL); P(t, 18, 24, bodyL);
            Apply(t);
            return Finish(t, "prop_atm");
        }

        /// <summary>叫号显示屏(V0.9.14b 小镇银行):黑底 LED 屏+红色号码+边框挂件,26×18——
        /// 挂柜台后墙,「请 003 号到 2 号窗口」的营业厅语汇。</summary>
        public static Sprite TellerScreen()
        {
            Sprite cached;
            if (TryGetCached("prop_teller_screen", out cached)) return cached;
            Texture2D t = NewTex(26, 18);
            Color frame = RGB(0x46, 0x54, 0x62), led = RGB(0x1E, 0x24, 0x2A), red = RGB(0xE8, 0x5A, 0x4A);
            Color green = RGB(0x6A, 0xE2, 0x8A), amber = RGB(0xE2, 0xC2, 0x5A);
            Rect(t, 0, 0, 26, 18, frame); Frame(t, 0, 0, 26, 18, RGB(0x36, 0x42, 0x4E));
            Rect(t, 2, 2, 22, 11, led);
            HLine(t, 3, 5, 13, RGB(0x3A, 0x44, 0x4C)); HLine(t, 4, 6, 14, RGB(0x3A, 0x44, 0x4C)); // 滚动条
            for (int x = 6; x <= 10; x++) { P(t, x, 8, red); P(t, x, 9, red); }          // 「00」
            Rect(t, 12, 7, 3, 4, red); Rect(t, 14, 7, 1, 2, red); P(t, 14, 10, red);    // 「3」
            P(t, 18, 8, green); P(t, 20, 8, amber);                                      // 状态灯
            Rect(t, 3, 14, 20, 2, RGB(0x5E, 0x6E, 0x7E));                               // 下檐
            P(t, 4, 16, RGB(0x6A, 0xE2, 0x8A)); P(t, 21, 16, RGB(0xE2, 0x5A, 0x5A));    // 檐灯
            Apply(t);
            return Finish(t, "prop_teller_screen");
        }

        /// <summary>填单台(V0.9.14b 小镇银行):斜面写字台+凭单格+笔+理财折页架,30×22——
        /// 营业厅「先填单」的服务件。</summary>
        public static Sprite FillDesk()
        {
            Sprite cached;
            if (TryGetCached("prop_fill_desk", out cached)) return cached;
            Texture2D t = NewTex(30, 22);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xA4, 0x7A, 0x50);
            Color paper = RGB(0xF4, 0xF0, 0xE4), paperD = RGB(0xD8, 0xD0, 0xBC);
            Rect(t, 1, 6, 28, 10, wood); Frame(t, 1, 6, 28, 10, woodD);                 // 台身
            Rect(t, 3, 8, 8, 6, woodD);                                                  // 凭单格
            for (int y = 9; y < 14; y += 2) HLine(t, y, 4, 9, RGB(0x9A, 0x74, 0x4E));   // 格档
            Rect(t, 3, 9, 6, 1, paper); Rect(t, 3, 11, 5, 1, paperD); Rect(t, 3, 13, 6, 1, paper); // 格内凭单
            Rect(t, 1, 16, 28, 3, woodL); HLine(t, 17, 2, 27, woodD);                   // 斜面台
            Rect(t, 5, 17, 8, 2, paper); HLine(t, 18, 6, 11, paperD);                   // 摆开的单张
            Rect(t, 16, 16, 9, 3, paper); Frame(t, 16, 16, 9, 3, paperD);               // 理财折页
            P(t, 18, 17, RGB(0xC2, 0x58, 0x48)); P(t, 21, 17, RGB(0x5A, 0x7A, 0x9A));   // 折页彩页
            VLine(t, 13, 16, 19, RGB(0x3A, 0x3E, 0x44)); P(t, 13, 15, RGB(0xE2, 0xC2, 0x5A)); // 笔
            Rect(t, 2, 19, 2, 2, woodD); Rect(t, 26, 19, 2, 2, woodD);                  // 台脚
            Apply(t);
            return Finish(t, "prop_fill_desk");
        }

        /// <summary>点钞机(V0.9.14b 小镇银行):小机身+送钞口+一沓钞票,14×12——柜台上的忙碌细节。</summary>
        public static Sprite MoneyCounter()
        {
            Sprite cached;
            if (TryGetCached("prop_money_counter", out cached)) return cached;
            Texture2D t = NewTex(14, 12);
            Color body = RGB(0x5E, 0x6E, 0x7E), bodyD = RGB(0x46, 0x54, 0x62);
            Color bill = RGB(0x8E, 0xB8, 0x94), billD = RGB(0x6E, 0x94, 0x76), band = RGB(0xC2, 0x58, 0x48);
            Rect(t, 1, 1, 12, 7, body); Frame(t, 1, 1, 12, 7, bodyD);
            Rect(t, 3, 2, 8, 2, RGB(0x9A, 0xD4, 0xE8));                                  // 计数屏
            P(t, 4, 3, RGB(0xE8, 0xF4, 0xF8)); P(t, 7, 3, RGB(0xE8, 0xF4, 0xF8));
            Rect(t, 3, 5, 8, 1, bodyD);                                                  // 送钞槽
            Rect(t, 2, 8, 10, 3, bill); HLine(t, 8, 3, 10, billD); HLine(t, 10, 3, 10, billD); // 钞票沓
            Rect(t, 6, 8, 2, 3, band);                                                   // 扎条
            P(t, 2, 8, billD); P(t, 11, 10, billD);
            Apply(t);
            return Finish(t, "prop_money_counter");
        }

        /// <summary>会议长桌(V0.9.14b 央行):深木长桌+中央文件+水杯,40×20——
        /// 货币政策会议桌(政策利率是开会定的)。</summary>
        public static Sprite MeetingTable()
        {
            Sprite cached;
            if (TryGetCached("prop_meeting_table", out cached)) return cached;
            Texture2D t = NewTex(40, 20);
            Color wood = RGB(0x6E, 0x2A, 0x24), woodD = RGB(0x54, 0x1E, 0x1A), woodL = RGB(0x8A, 0x3C, 0x32);
            Color paper = RGB(0xFA, 0xF7, 0xEE), glass = RGB(0xB8, 0xD0, 0xDC);
            Rect(t, 0, 4, 40, 10, wood); Frame(t, 0, 4, 40, 10, woodD);                 // 桌身
            Rect(t, 0, 14, 40, 3, woodL); HLine(t, 15, 1, 38, woodD); HLine(t, 16, 2, 37, woodD); // 双层桌沿
            Rect(t, 5, 0, 3, 6, woodD); Rect(t, 32, 0, 3, 6, woodD);                    // 桌腿(透视)
            Rect(t, 13, 6, 8, 5, paper); HLine(t, 7, 14, 19, RGB(0xC4, 0xBE, 0xB0)); HLine(t, 9, 14, 19, RGB(0xC4, 0xBE, 0xB0)); // 中央文件
            Rect(t, 23, 6, 6, 4, paper); P(t, 25, 7, RGB(0xC2, 0x58, 0x48));            // 红头页
            Rect(t, 31, 5, 4, 5, glass); HLine(t, 6, 31, 34, RGB(0xD4, 0xE2, 0xE8));    // 水杯
            P(t, 2, 6, woodL); P(t, 37, 8, woodL); P(t, 20, 10, woodL);
            Apply(t);
            return Finish(t, "prop_meeting_table");
        }

        /// <summary>钱垛托盘(V0.9.14b 央行):木托+成捆钞票堆(绿扎捆),26×20——
        /// 金库门前的发行银行语汇(央行的货就是钱)。</summary>
        public static Sprite CashPallet()
        {
            Sprite cached;
            if (TryGetCached("prop_cash_pallet", out cached)) return cached;
            Texture2D t = NewTex(26, 20);
            Color wood = RGB(0xB0, 0x84, 0x54), woodD = RGB(0x8A, 0x64, 0x3C);
            Color bill = RGB(0x8E, 0xB8, 0x94), billD = RGB(0x6E, 0x94, 0x76);
            Color[] bands = { RGB(0xC2, 0x58, 0x48), RGB(0xE2, 0xC2, 0x5A), RGB(0x5A, 0x7A, 0x9A) };
            Rect(t, 1, 1, 24, 5, wood); HLine(t, 2, 2, 23, woodD); VLine(t, 6, 2, 4, woodD); VLine(t, 12, 2, 4, woodD); VLine(t, 18, 2, 4, woodD); // 木托
            Rect(t, 3, 6, 8, 4, bill); HLine(t, 6, 4, 10, billD); Rect(t, 5, 6, 2, 4, bands[0]); // 钱捆一
            Rect(t, 13, 6, 8, 4, bill); HLine(t, 6, 14, 20, billD); Rect(t, 15, 6, 2, 4, bands[1]); // 钱捆二
            Rect(t, 5, 10, 8, 4, bill); HLine(t, 10, 6, 12, billD); Rect(t, 7, 10, 2, 4, bands[2]); // 上层一
            Rect(t, 12, 10, 7, 4, bill); HLine(t, 10, 13, 18, billD); Rect(t, 14, 10, 2, 4, bands[0]);
            Rect(t, 9, 14, 8, 4, bill); HLine(t, 14, 10, 16, billD); Rect(t, 11, 14, 2, 4, bands[1]); // 顶层
            P(t, 4, 7, billD); P(t, 20, 11, billD); P(t, 16, 15, billD);
            Apply(t);
            return Finish(t, "prop_cash_pallet");
        }

        /// <summary>阶梯花架(V0.9.14b 花店):三层阶梯木架各摆两三盆不同色花,34×32——
        /// 花房丰满度主件。</summary>
        public static Sprite FlowerStandTiered()
        {
            Sprite cached;
            if (TryGetCached("prop_flower_tiered", out cached)) return cached;
            Texture2D t = NewTex(34, 32);
            Color wood = RGB(0xA8, 0x7A, 0x4E), woodD = RGB(0x84, 0x5E, 0x38), woodL = RGB(0xBC, 0x90, 0x62);
            Color[] blooms = { RGB(0xE2, 0x6A, 0x6A), RGB(0xE2, 0x9A, 0xC2), RGB(0xF2, 0xD8, 0x66), RGB(0xE8, 0xE8, 0xE0), RGB(0x9A, 0x6A, 0xC2) };
            Color pot = RGB(0xC9, 0xA8, 0x72), leaf = RGB(0x5E, 0x8A, 0x5A);
            Rect(t, 2, 24, 30, 4, wood); HLine(t, 25, 3, 30, woodD);                    // 底层台
            Rect(t, 5, 16, 24, 4, wood); HLine(t, 17, 6, 28, woodD);                    // 中层台
            Rect(t, 8, 8, 18, 4, wood); HLine(t, 9, 9, 25, woodD);                      // 顶层台
            VLine(t, 4, 26, 30, woodD); VLine(t, 29, 26, 30, woodD); Rect(t, 3, 30, 28, 2, wood); // 支脚
            int[] bx = { 4, 12, 20, 7, 15, 23, 11, 19 };                                // 八盆花
            int[] by = { 25, 25, 25, 17, 17, 17, 9, 9 };
            for (int i = 0; i < bx.Length; i++)
            {
                Rect(t, bx[i], by[i] - 4, 5, 4, pot);                                  // 花盆
                P(t, bx[i] + 2, by[i] - 5, leaf); P(t, bx[i] + 1, by[i] - 7, blooms[i % 5]); P(t, bx[i] + 3, by[i] - 6, blooms[(i + 2) % 5]);
            }
            P(t, 2, 25, woodL); P(t, 31, 26, woodL); P(t, 6, 17, woodL);
            Apply(t);
            return Finish(t, "prop_flower_tiered");
        }

        /// <summary>盆栽花排(V0.9.14b 花店):三盆不同色小花等距成排,32×14——地面加密件。</summary>
        public static Sprite PottedFlowerRow()
        {
            Sprite cached;
            if (TryGetCached("prop_flower_row", out cached)) return cached;
            Texture2D t = NewTex(32, 14);
            Color pot = RGB(0xC9, 0xA8, 0x72), potD = RGB(0xA8, 0x8A, 0x5A);
            Color[] blooms = { RGB(0xE2, 0x6A, 0x6A), RGB(0xF2, 0xD8, 0x66), RGB(0xE2, 0x9A, 0xC2) };
            Color leaf = RGB(0x5E, 0x8A, 0x5A);
            for (int i = 0; i < 3; i++)
            {
                int bx = 3 + i * 11;
                Rect(t, bx, 2, 6, 5, pot); HLine(t, 6, bx + 1, bx + 4, potD);           // 盆
                P(t, bx + 2, 1, leaf); P(t, bx + 3, 1, leaf);                           // 叶
                P(t, bx + 1, 0, blooms[i]); P(t, bx + 4, 0, blooms[i]);                 // 花
                P(t, bx + 2, 13, potD); P(t, bx + 4, 13, potD);                         // 盆脚影
            }
            Apply(t);
            return Finish(t, "prop_flower_row");
        }

        /// <summary>零食架(V0.9.14b 零嘴亭):四层四个品类——薯片袋堆/糖果罐/饼干盒/挂装零食串,
        /// 36×34——替换通用货架(彩色小瓶读作饮料),让小卖部「卖的不止饮料」。</summary>
        public static Sprite SnackShelf()
        {
            Sprite cached;
            if (TryGetCached("prop_snack_shelf", out cached)) return cached;
            Texture2D t = NewTex(36, 34);
            Color wood = RGB(0x8A, 0x62, 0x40), woodD = RGB(0x66, 0x46, 0x2C), woodL = RGB(0xA4, 0x7A, 0x50);
            Color chip = RGB(0xE2, 0xC2, 0x5A), chipD = RGB(0xC4, 0xA4, 0x44);
            Color candy = RGB(0xC9, 0x8B, 0x96), jar = RGB(0xD8, 0xE2, 0xE4);
            Color box = RGB(0xC2, 0x7A, 0x4A), bagG = RGB(0x5E, 0x8A, 0x5A), bagR = RGB(0xC2, 0x58, 0x48);
            Rect(t, 1, 0, 34, 32, wood); Frame(t, 1, 0, 34, 32, woodD);                 // 架身
            VLine(t, 2, 1, 31, woodD); VLine(t, 33, 1, 31, woodD);                      // 侧柱
            for (int s = 0; s < 4; s++)
            {
                int sy = 24 - s * 8;
                Rect(t, 3, sy, 30, 3, woodL); HLine(t, sy, 4, 31, woodD);               // 层板
                if (s == 0)
                {
                    // 底层:薯片袋堆(三袋两两叠)
                    Rect(t, 6, sy - 5, 7, 5, chip); Rect(t, 13, sy - 5, 7, 5, chip); Rect(t, 20, sy - 5, 7, 5, chip);
                    HLine(t, sy - 2, 7, 12, chipD); HLine(t, sy - 2, 14, 19, chipD); HLine(t, sy - 2, 21, 26, chipD);
                    P(t, 9, sy - 3, RGB(0xF4, 0xF0, 0xE4)); P(t, 16, sy - 3, RGB(0xF4, 0xF0, 0xE4));
                    Rect(t, 27, sy - 4, 5, 4, chipD);                                   // 侧立一袋
                }
                else if (s == 1)
                {
                    // 二层:糖果罐(玻璃罐+彩糖)
                    for (int j = 0; j < 3; j++)
                    {
                        int jx = 6 + j * 9;
                        Rect(t, jx, sy - 6, 6, 6, jar); Frame(t, jx, sy - 6, 6, 6, woodD); // 玻璃罐
                        P(t, jx + 1, sy - 3, candy); P(t, jx + 3, sy - 2, candy); P(t, jx + 4, sy - 4, candy);
                        Rect(t, jx + 1, sy - 7, 4, 1, wood);                            // 罐盖
                    }
                }
                else if (s == 2)
                {
                    // 三层:饼干盒(彩盒两摞)
                    Rect(t, 5, sy - 4, 8, 4, box); HLine(t, sy - 2, 6, 12, RGB(0xE2, 0xB0, 0x7A)); // 饼干盒
                    Rect(t, 5, sy - 8, 8, 4, RGB(0xD8, 0xA0, 0x4E));
                    Rect(t, 16, sy - 4, 8, 4, RGB(0x5A, 0x7A, 0x9A)); HLine(t, sy - 2, 17, 23, bagG); // 蓝盒
                    Rect(t, 16, sy - 8, 8, 4, RGB(0x7A, 0x94, 0xAA));
                    Rect(t, 26, sy - 6, 6, 6, box); P(t, 28, sy - 4, RGB(0xF4, 0xF0, 0xE4)); // 立盒
                }
                else
                {
                    // 顶层:挂装零食串(挂钩+三彩袋)
                    for (int h = 0; h < 4; h++)
                    {
                        int hx = 6 + h * 7;
                        P(t, hx + 2, sy - 7, woodD);                                   // 挂钩
                        Rect(t, hx, sy - 6, 5, 6, h % 2 == 0 ? bagR : bagG);
                        P(t, hx + 2, sy - 4, RGB(0xF4, 0xF0, 0xE4));                   // 袋面标
                    }
                }
            }
            P(t, 3, 1, woodL); P(t, 32, 2, woodL);
            Apply(t);
            return Finish(t, "prop_snack_shelf");
        }

        // ————— 暖炉餐厅:木桁架山墙小馆——陡坡顶+吊灯+刀叉挂牌 —————

        // ————— 暖炉餐厅(M 档 88×72,V0.9.6 体量分化):陡坡大屋顶铺开 + 木桁架立面 + 双分格大窗 + 石烟囱 —————
        // 保留识别要素:陡坡红瓦/木桁架/窗台花箱/挂牌/烟囱炊烟;新增语汇:体量横向铺开、加高门洞与气窗

        static void PaintChaletRestaurant(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color cream = RGB(0xF3, 0xE4, 0xC2), timber = RGB(0x7A, 0x52, 0x30), oak = RGB(0x6A, 0x44, 0x28);
            GableRoof(t, 6, 81, 32, 62, RGB(0xB8, 0x63, 0x3F), s);                                // 陡坡大屋顶
            HLine(t, 31, 16, 71, Shade(cream, 0.72f));                                            // 檐口投影

            Rect(t, 18, 0, 52, 32, cream); Frame(t, 18, 0, 52, 32, Shade(cream, 0.78f));
            int[] beamX = { 19, 31, 57, 69 };                                                     // 木桁架立柱
            for (int i = 0; i < beamX.Length; i++) VLine(t, beamX[i], 1, 31, timber);
            HLine(t, 16, 18, 69, timber);

            Rect(t, 40, 0, 9, 17, oak); Frame(t, 40, 0, 9, 17, Shade(oak, 0.62f));
            P(t, 40, 16, cream); P(t, 48, 16, cream);                                             // 拱切角
            P(t, 46, 8, RGB(0xE2, 0xC8, 0x8A));
            FramedGlass(t, 41, 18, 7, 4, RGB(0x54, 0x74, 0x8E), g);
            g.Add(new GlowRect(41, 1, 7, 14));

            FramedGlass(t, 22, 18, 15, 11, RGB(0x63, 0x83, 0x9B), g);                             // 分格大窗
            VLine(t, 29, 19, 28, timber); HLine(t, 23, 22, 36, timber);
            FramedGlass(t, 52, 18, 15, 11, RGB(0x63, 0x83, 0x9B), g);
            VLine(t, 59, 19, 28, timber); HLine(t, 23, 52, 66, timber);

            Rect(t, 35, 17, 3, 5, RGB(0xF2, 0xDC, 0x92));                                         // 门旁吊灯
            P(t, 36, 22, oak); P(t, 35, 23, oak); P(t, 37, 23, oak);
            g.Add(new GlowRect(34, 17, 5, 5));

            VLine(t, 11, 31, 33, timber); VLine(t, 18, 31, 33, timber);                           // 挂绳贴住檐底
            Rect(t, 8, 21, 13, 10, RGB(0xA8, 0x7C, 0x4E)); Frame(t, 8, 21, 13, 10, Shade(timber, 0.8f));
            VLine(t, 11, 24, 28, RGB(0xF2, 0xEE, 0xE2)); VLine(t, 13, 24, 28, RGB(0xF2, 0xEE, 0xE2));
            VLine(t, 12, 24, 26, RGB(0xF2, 0xEE, 0xE2));
            P(t, 16, 24, RGB(0xF2, 0xEE, 0xE2)); P(t, 17, 25, RGB(0xF2, 0xEE, 0xE2));
            P(t, 17, 26, RGB(0xF2, 0xEE, 0xE2)); P(t, 16, 27, RGB(0xF2, 0xEE, 0xE2));

            Rect(t, 22, 14, 15, 3, timber);                                                       // 窗台花箱
            P(t, 25, 13, RGB(0xE2, 0x7A, 0xB8)); P(t, 29, 13, RGB(0xF2, 0xD4, 0x5A)); P(t, 33, 13, RGB(0xE8, 0x8A, 0x4A));
            Rect(t, 52, 14, 15, 3, timber);
            P(t, 55, 13, RGB(0xE2, 0x7A, 0xB8)); P(t, 59, 13, RGB(0xF2, 0xD4, 0x5A)); P(t, 63, 13, RGB(0xE8, 0x8A, 0x4A));

            Rect(t, 30, 40, 8, 15, RGB(0xB0, 0xA0, 0x8A));                                        // 左坡石烟囱
            Frame(t, 30, 40, 8, 15, Shade(timber, 0.8f));
            Rect(t, 29, 55, 10, 2, Shade(timber, 0.7f));
            P(t, 33, 58, new Color32(0xe2, 0xe2, 0xe2, 175)); P(t, 35, 61, new Color32(0xd4, 0xd4, 0xd4, 150));

            s.Add(new SnowBand(29, 38, 56));
        }

        // ————— 星光电影院:紫罗兰立面+灯泡跑马灯+玻璃售票亭 —————

        // ————— 星光电影院(M 档 88×72,V0.8.1 地标化):紫罗兰横向立面 + 双垂直霓虹柱 + 加宽跑马灯箱 —————
        // 保留识别要素:灯泡排/金星徽记/跑马灯箱/售票亭/海报灯箱;新增语汇:体量铺开、双垂直霓虹柱

        static void PaintMoviePalace(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color plum = RGB(0x70, 0x58, 0x8A), plumD = RGB(0x52, 0x3F, 0x65), ink = RGB(0x30, 0x1F, 0x3E);
            Color gold = RGB(0xF0, 0xC3, 0x57), tealD = RGB(0x3E, 0x70, 0x6C);
            Rect(t, 12, 0, 64, 46, plum); Frame(t, 12, 0, 64, 46, plumD);
            Rect(t, 10, 46, 68, 5, plumD);                                                     // 山墙压顶
            for (int x = 12; x <= 75; x += 4) P(t, x, 47, gold);                               // 灯泡排
            P(t, 11, 49, plumD); P(t, 11, 50, plumD); P(t, 76, 49, plumD); P(t, 76, 50, plumD);

            P(t, 43, 49, gold); P(t, 42, 50, gold); P(t, 44, 50, gold);                        // 屋顶金星徽记
            HLine(t, 51, 41, 46, gold); P(t, 42, 52, gold); P(t, 44, 52, gold); P(t, 43, 53, gold);
            P(t, 46, 51, RGB(0xff, 0xf1, 0xb0));

            // 双垂直霓虹柱(V0.8.1):影院的夜航标——墨底金框、三色灯珠旋转上升
            int[] neonX = { 17, 66 };
            Color[] neonDots = { RGB(0xE8, 0x7A, 0x9E), RGB(0x5F, 0xC4, 0xC4), gold };
            for (int n = 0; n < 2; n++)
            {
                Rect(t, neonX[n] - 1, 6, 5, 38, ink); Frame(t, neonX[n] - 1, 6, 5, 38, gold);
                for (int y = 9, i = 0; y < 41; y += 4, i++)
                {
                    P(t, neonX[n], y, neonDots[i % 3]);
                    P(t, neonX[n] + 1, y + 2, neonDots[(i + 2) % 3]);
                }
                g.Add(new GlowRect(neonX[n], 8, 2, 34));
            }

            Rect(t, 24, 32, 2, 10, ink); Rect(t, 61, 32, 2, 10, ink);                          // 门楣托架
            Rect(t, 20, 34, 48, 11, ink); Frame(t, 20, 34, 48, 11, gold);                      // 跑马灯箱
            Color[] bands = { RGB(0xE8, 0x7A, 0x9E), RGB(0x5F, 0xC4, 0xC4), gold };
            for (int x = 22, i = 0; x < 65; x += 6, i++) Rect(t, x, 38, 5, 3, bands[i % 3]);
            for (int x = 23; x < 64; x += 5) P(t, x, 36, gold);
            for (int y = 35; y <= 43; y += 2) { P(t, 21, y, plum); P(t, 66, y, plum); }        // 胶片齿孔

            FramedGlass(t, 25, 0, 12, 15, RGB(0x8F, 0xB8, 0xAC), g);                           // 双开玻璃门
            FramedGlass(t, 51, 0, 12, 15, RGB(0x8F, 0xB8, 0xAC), g);
            HLine(t, 6, 26, 36, tealD); HLine(t, 6, 51, 62, tealD);

            Rect(t, 39, 0, 10, 18, RGB(0xF2, 0xE2, 0xBE)); Frame(t, 39, 0, 10, 18, tealD);     // 售票亭
            FramedGlass(t, 41, 3, 6, 9, RGB(0x7F, 0xA8, 0xB8), g);
            P(t, 43, 7, tealD); P(t, 45, 7, tealD); P(t, 44, 6, tealD);                        // 售票圆孔
            Rect(t, 39, 16, 10, 2, gold);

            FramedGlass(t, 26, 21, 8, 10, RGB(0xD8, 0x7A, 0x9E), g);                           // 海报灯箱·昼
            P(t, 29, 28, gold); P(t, 30, 27, gold); P(t, 30, 29, gold);
            FramedGlass(t, 54, 21, 8, 10, RGB(0x5F, 0x87, 0xB8), g);                           // 海报灯箱·夜
            P(t, 58, 26, RGB(0xf2, 0xf6, 0xfc)); P(t, 60, 28, RGB(0xff, 0xf1, 0xb0));

            P(t, 36, 30, gold); P(t, 46, 27, gold); P(t, 50, 31, gold);

            s.Add(new SnowBand(10, 77, 51));
            s.Add(new SnowBand(20, 67, 45));
        }

        // ————— 便利屋:现代方盒店——整幅橱窗+绿色招牌带+屋顶广告牌 —————

        static void PaintCornerStore(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color mint = RGB(0xCF, 0xE0, 0xC8), green = RGB(0x4F, 0x88, 0x68), alu = RGB(0x8A, 0x97, 0xAE);
            Color cyanWin = RGB(0x9F, 0xC4, 0xCF);
            Rect(t, 9, 0, 46, 8, mint);                                                           // 踢脚墙
            for (int y = 2; y < 8; y += 3) HLine(t, y, 12, 51, Shade(mint, 0.9f));

            Rect(t, 9, 8, 46, 24, cyanWin);                                                       // 整幅橱窗
            HLine(t, 15, 10, 54, Shade(cyanWin, 0.78f)); HLine(t, 21, 10, 54, Shade(cyanWin, 0.78f));
            Color[] goods = { RGB(0xD8, 0x5A, 0x4A), RGB(0x8A, 0xB8, 0x5A), RGB(0xF0, 0xA3, 0x3E) };
            for (int i = 0; i < 8; i++)
            {
                int gx = 11 + i * 5;
                if (gx > 24 && gx < 37) continue;                                                 // 让位门洞
                Rect(t, gx, 12, 2, 2, goods[i % 3]);
                Rect(t, gx, 18, 2, 2, goods[(i + 1) % 3]);
            }
            Rect(t, 26, 8, 12, 24, RGB(0x8D, 0xB4, 0xBF));                                        // 自动门洞
            Frame(t, 26, 8, 12, 24, alu);
            P(t, 31, 30, RGB(0xE0, 0x50, 0x40));                                                  // 感应器
            for (int x = 28; x <= 35; x += 2) P(t, x, 10, (x / 2) % 2 == 0 ? RGB(0xF2, 0xEE, 0xE2) : RGB(0xE0, 0x50, 0x40));
            Frame(t, 9, 8, 46, 24, alu);

            Rect(t, 6, 34, 52, 8, green);                                                         // 绿色招牌带
            Rect(t, 10, 36, 34, 4, RGB(0xF6, 0xEF, 0xDA));
            for (int x = 12; x < 43; x += 4) Rect(t, x, 37, 2, 2, green);
            Rect(t, 52, 35, 6, 6, RGB(0xE8, 0x91, 0x2E));                                         // 24H 灯箱
            P(t, 54, 38, RGB(0xff, 0xf6, 0xd0)); P(t, 56, 38, RGB(0xff, 0xf6, 0xd0));
            P(t, 55, 37, RGB(0xff, 0xf6, 0xd0)); P(t, 55, 39, RGB(0xff, 0xf6, 0xd0));
            Rect(t, 5, 42, 54, 2, Shade(green, 0.55f));

            Rect(t, 44, 44, 10, 6, RGB(0xB9, 0xBD, 0xC2)); Frame(t, 44, 44, 10, 6, Shade(alu, 0.8f)); // 空调外机
            HLine(t, 47, 45, 52, Shade(alu, 0.9f));
            Rect(t, 8, 44, 1, 6, alu); Rect(t, 20, 44, 1, 6, alu);                                // 广告牌腿
            Rect(t, 6, 50, 18, 5, RGB(0xF6, 0xEF, 0xDA)); Frame(t, 6, 50, 18, 5, alu);
            P(t, 11, 52, RGB(0xE0, 0x78, 0x30)); P(t, 12, 52, RGB(0xE0, 0x78, 0x30));
            P(t, 11, 51, RGB(0xE0, 0x78, 0x30)); P(t, 12, 51, RGB(0xE0, 0x78, 0x30));
            HLine(t, 52, 16, 21, RGB(0x5F, 0x9E, 0xA0));

            g.Add(new GlowRect(10, 9, 15, 22));
            g.Add(new GlowRect(27, 9, 10, 22));
            g.Add(new GlowRect(38, 9, 16, 22));
            g.Add(new GlowRect(52, 35, 6, 6));

            s.Add(new SnowBand(5, 58, 44));
            s.Add(new SnowBand(43, 54, 50));
            s.Add(new SnowBand(6, 23, 55));
        }

        // ————— 临街小铺甲:遮阳伞零食亭——四角撑伞+A 字黑板菜单 —————

        static void PaintParasolKiosk(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color wood = RGB(0x7A, 0x54, 0x36), coralA = RGB(0xE2, 0x6A, 0x4E), coralB = RGB(0xF0, 0x92, 0x74);
            Color creamA = RGB(0xF6, 0xEF, 0xDC), creamB = RGB(0xEA, 0xDF, 0xC4);
            int[][] rimRows =                                                                     // 伞面(上尖下宽)
            {
                new[] { 28, 36 }, new[] { 24, 40 }, new[] { 20, 44 },
                new[] { 17, 47 }, new[] { 14, 50 },
            };
            // 围猎三#22:y 轴向上——旧写法 24+i 把最宽行画在最高处(上宽下尖倒漏斗),
            // 窄尖行沉到伞底;翻转后窄行在上(伞尖)、宽行在下(伞缘),伞尖/垂穗归位。
            for (int i = 0; i < rimRows.Length; i++)
            {
                int y = 28 - i;
                for (int x = rimRows[i][0]; x <= rimRows[i][1]; x++)
                {
                    bool warmFamily = ((x / 5) + y) % 2 == 0;
                    bool hi = (x / 9) % 2 == 0;
                    Color cc = warmFamily ? (hi ? coralA : coralB) : (hi ? creamA : creamB);
                    P(t, x, y, cc);
                }
            }
            for (int x = 14; x <= 50; x += 3)                                                     // 伞缘垂穗(宽缘 y24 之下)
            {
                P(t, x, 23, (x / 3) % 2 == 0 ? Shade(coralA, 0.85f) : Shade(creamA, 0.85f));
                if ((x / 3) % 2 == 0) P(t, x, 22, Shade(coralA, 0.7f));
            }
            P(t, 31, 30, RGB(0xD9, 0xA8, 0x3A)); P(t, 32, 30, RGB(0xD9, 0xA8, 0x3A));             // 伞顶尖(再高一行,露出窄段雪带 topY=29 之上)
            VLine(t, 14, 20, 24, wood); VLine(t, 49, 20, 24, wood);                               // 支伞斜杆(撑到宽缘)

            Rect(t, 16, 0, 32, 22, RGB(0xF6, 0xDF, 0xC2)); Frame(t, 16, 0, 32, 22, Shade(creamA, 0.76f));
            Rect(t, 19, 11, 26, 10, RGB(0x59, 0x45, 0x2F));                                       // 取货窗口
            Frame(t, 19, 11, 26, 10, wood);
            Color[] jars = { RGB(0xE2, 0x5A, 0x4A), RGB(0xF2, 0xA8, 0x4A), RGB(0x8A, 0xC4, 0x5A), RGB(0x67, 0x9F, 0xC0) };
            for (int i = 0; i < 6; i++) Rect(t, 21 + i * 4, 14, 2, 3, jars[i % 4]);
            // 原来将整块取货窗注册为发光区，夜间会盖掉货物和窗框。
            // 现改为窗内局部吊灯：有明确光源，又能保留零食细节。
            VLine(t, 31, 19, 21, wood);
            Rect(t, 28, 17, 8, 2, RGB(0xD8, 0x8B, 0x3A));
            HLine(t, 16, 29, 34, RGB(0xA8, 0x5F, 0x2E));
            Rect(t, 31, 14, 3, 3, RGB(0xFF, 0xED, 0xA8));
            P(t, 32, 15, RGB(0xFF, 0xFA, 0xD8));
            g.Add(new GlowRect(29, 14, 7, 5));

            Rect(t, 15, 8, 34, 3, RGB(0xA8, 0x7C, 0x4E));                                         // 外挑柜台
            HLine(t, 11, 15, 48, RGB(0xC8, 0xA0, 0x74));
            Rect(t, 42, 12, 5, 4, RGB(0xD9, 0xA8, 0x3A)); Frame(t, 42, 12, 5, 4, Shade(wood, 0.7f)); // 黄铜收银盒

            Rect(t, 5, 1, 11, 13, wood); Rect(t, 6, 2, 9, 11, RGB(0x3A, 0x3A, 0x40));             // A 字手写板
            HLine(t, 5, 8, 12, RGB(0xd8, 0xd0, 0xbc)); HLine(t, 8, 8, 10, RGB(0xd8, 0xd0, 0xbc)); HLine(t, 8, 12, 13, RGB(0xd8, 0xd0, 0xbc));
            HLine(t, 11, 8, 13, RGB(0xd8, 0xd0, 0xbc));

            Rect(t, 50, 0, 10, 6, RGB(0xA8, 0x7C, 0x4E)); Frame(t, 50, 0, 10, 6, wood);           // 果筐堆
            Rect(t, 51, 6, 8, 5, RGB(0x9a, 0x6e, 0x44)); Frame(t, 51, 6, 8, 5, wood);
            P(t, 52, 11, RGB(0xF2, 0xA8, 0x4A)); P(t, 55, 11, RGB(0xF2, 0xA8, 0x4A)); P(t, 57, 11, RGB(0xE2, 0x5A, 0x4A));

            Color[] flags = { RGB(0xE2, 0x6A, 0x5A), RGB(0xF2, 0xC4, 0x4A), RGB(0x5A, 0xA8, 0x92) };
            for (int i = 0; i <= 10; i++)                                                          // 跨摊小彩旗
            {
                int x = 15 + i * 3;
                int yy = 23 - Mathf.RoundToInt(Mathf.Sin(i / 10f * Mathf.PI) * 2f);
                P(t, x, yy, flags[i % 3]);
            }

            // 围猎三#22:雪带沿翻转后的伞面逐段铺雪(topY=伞面行,雪体向上覆 3px 凸出坡缘,
            // GableRoof 同型)——宽缘(y24)、中段(y26)、窄顶(y27,向上仅凸 1px)。
            // 金色伞尖(y30)在窄顶雪体(27..29)之上露出。
            s.Add(new SnowBand(14, 50, 24));
            s.Add(new SnowBand(20, 44, 26));
            s.Add(new SnowBand(28, 36, 27));
        }

        // ————— 临街小铺乙:黄油色小花店 cottage——大橱窗+垂吊盆栽 —————

        static void PaintFlowerCottage(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color butter = RGB(0xF2, 0xDF, 0xA6), sageR = RGB(0x6F, 0x8F, 0x4E), oak = RGB(0x6A, 0x44, 0x28);
            Color winSoft = RGB(0x8F, 0xB8, 0xAC), terra = RGB(0xC8, 0x7A, 0x5A), brick = RGB(0x9A, 0x66, 0x50);
            GableRoof(t, 10, 54, 20, 36, sageR, s);                                               // 陡坡苔绿顶
            Rect(t, 15, 0, 34, 22, butter); Frame(t, 15, 0, 34, 22, Shade(butter, 0.78f));

            FramedGlass(t, 17, 5, 13, 12, winSoft, g);                                            // 大展示窗
            P(t, 19, 6, RGB(0xE2, 0x7A, 0xB8)); P(t, 22, 6, RGB(0xF2, 0xD4, 0x5A));
            P(t, 25, 6, RGB(0xE8, 0x8A, 0x4A)); P(t, 28, 6, RGB(0xD8, 0x7A, 0x9E));               // 桶装鲜花剪影
            VLine(t, 20, 7, 9, RGB(0x5A, 0x94, 0x46)); VLine(t, 23, 7, 9, RGB(0x5A, 0x94, 0x46));
            VLine(t, 26, 7, 9, RGB(0x5A, 0x94, 0x46)); VLine(t, 29, 7, 9, RGB(0x5A, 0x94, 0x46));
            Awning(t, 16, 31, 18, 3, RGB(0x8F, 0xAF, 0x74), RGB(0xF6, 0xEF, 0xDA));               // 迷你雨棚

            Rect(t, 34, 0, 9, 13, oak); Frame(t, 34, 0, 9, 13, Shade(oak, 0.62f));
            P(t, 34, 12, butter); P(t, 42, 12, butter);                                           // 拱切角
            P(t, 40, 6, RGB(0xE2, 0xC8, 0x8A));
            FramedGlass(t, 44, 5, 4, 7, winSoft, g);                                              // 门侧气窗

            Rect(t, 16, 3, 15, 3, oak);                                                           // 木花槽
            P(t, 18, 2, RGB(0xE2, 0x7A, 0xB8)); P(t, 22, 2, RGB(0xF2, 0xD4, 0x5A)); P(t, 26, 2, RGB(0xE8, 0x8A, 0x4A));
            P(t, 20, 2, RGB(0x5A, 0x94, 0x46)); P(t, 24, 2, RGB(0x5A, 0x94, 0x46)); P(t, 28, 2, RGB(0x5A, 0x94, 0x46));

            P(t, 12, 19, oak); P(t, 51, 19, oak);                                                 // 檐下垂盆
            Rect(t, 11, 15, 3, 3, terra); P(t, 12, 14, RGB(0x5A, 0x94, 0x46));
            Rect(t, 50, 15, 3, 3, terra); P(t, 51, 14, RGB(0x5A, 0x94, 0x46));
            P(t, 12, 18, RGB(0xE2, 0x7A, 0xB8)); P(t, 51, 18, RGB(0xF2, 0xD4, 0x5A));

            Rect(t, 18, 25, 5, 10, brick); Frame(t, 18, 25, 5, 10, Shade(brick, 0.7f));           // 小烟囱
            Rect(t, 17, 35, 7, 2, Shade(brick, 0.72f));
            P(t, 20, 39, new Color32(0xe2, 0xe2, 0xe2, 165)); P(t, 22, 42, new Color32(0xd4, 0xd4, 0xd4, 145));

            Rect(t, 48, 1, 7, 10, oak); Rect(t, 49, 2, 5, 8, RGB(0x3A, 0x3A, 0x40));              // 小黑板
            HLine(t, 4, 50, 53, RGB(0xd8, 0xd0, 0xbc)); HLine(t, 7, 50, 52, RGB(0xd8, 0xd0, 0xbc));

            g.Add(new GlowRect(35, 1, 7, 10));

            s.Add(new SnowBand(16, 24, 37));
        }

        // ————— 角色(9 帧:下×3 / 上×3 / 侧×3)—————

        public struct CharacterFrames
        {
            public Sprite[] down, up, side;
        }

        public static CharacterFrames Character(string type)
        {
            if (string.IsNullOrEmpty(type)) type = "游客";

            CharacterFrames cachedFrames;
            if (characterCache.TryGetValue(type, out cachedFrames) && FramesValid(cachedFrames))
                return cachedFrames;

            Color hair, shirt, pants, accent;
            bool hat = false, backpack = false, tie = false;
            switch (type)
            {
                case "player":
                    hair = RGB(0x2d, 0x35, 0x4b); shirt = RGB(0x3f, 0x82, 0xc4);
                    pants = RGB(0x36, 0x43, 0x5c); accent = RGB(0xf2, 0xb8, 0x4b); backpack = true;
                    break;
                case "学生":
                    hair = RGB(0x32, 0x2b, 0x3b); shirt = RGB(0x83, 0x63, 0xb8);
                    pants = RGB(0x43, 0x47, 0x61); accent = RGB(0x4f, 0xa3, 0x98); backpack = true;
                    break;
                case "白领":
                    hair = RGB(0x56, 0x3d, 0x2e); shirt = RGB(0x43, 0x86, 0x82);
                    pants = RGB(0x35, 0x3d, 0x4b); accent = RGB(0xc9, 0x4d, 0x45); tie = true;
                    break;
                default:
                    hair = RGB(0x7a, 0x4f, 0x2d); shirt = RGB(0xd2, 0x7b, 0x3e);
                    pants = RGB(0x65, 0x55, 0x43); accent = RGB(0xe6, 0xb8, 0x58); hat = true;
                    break;
            }

            CharacterFrames f;
            f.down = new Sprite[3]; f.up = new Sprite[3]; f.side = new Sprite[3];
            for (int frame = 0; frame < 3; frame++)
            {
                Texture2D d = DrawChar(hair, shirt, pants, accent, Dir.Down, frame, hat, backpack, tie);
                Texture2D u = DrawChar(hair, shirt, pants, accent, Dir.Up, frame, hat, backpack, tie);
                Texture2D s = DrawChar(hair, shirt, pants, accent, Dir.Side, frame, hat, backpack, tie);
                f.down[frame] = FinishCharacter(d, type + "_d" + frame);
                f.up[frame] = FinishCharacter(u, type + "_u" + frame);
                f.side[frame] = FinishCharacter(s, type + "_s" + frame);
            }
            characterCache[type] = f;
            return f;
        }

        static bool FramesValid(CharacterFrames frames)
        {
            return frames.down != null && frames.down.Length == 3 && frames.down[0] != null &&
                   frames.up != null && frames.up.Length == 3 && frames.up[0] != null &&
                   frames.side != null && frames.side.Length == 3 && frames.side[0] != null;
        }

        static Sprite FinishCharacter(Texture2D texture, string key)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.08f), PPU, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        enum Dir { Down, Up, Side }

        static Texture2D DrawChar(Color hair, Color shirt, Color pants, Color accent, Dir dir, int frame, bool hat, bool backpack, bool tie)
        {
            Texture2D t = NewTex(20, 28);
            Color skin = RGB(0xf1, 0xc4, 0x96);
            Color skinDark = RGB(0xd7, 0x9d, 0x72);
            Color line = RGB(0x2d, 0x2b, 0x36);
            Color shoe = RGB(0x3b, 0x32, 0x32);
            Color white = RGB(0xf4, 0xed, 0xdd);
            Color shirtDark = Shade(shirt, 0.76f);
            Color pantsDark = Shade(pants, 0.72f);
            Color accentDark = Shade(accent, 0.72f);
            int leftLift = frame == 1 ? 1 : 0;
            int rightLift = frame == 2 ? 1 : 0;

            if (dir == Dir.Side)
            {
                // 侧面腿与鞋：远近腿错位，轮廓保持完整。
                Rect(t, 7, 2 + rightLift, 5, 7 - rightLift, line);
                Rect(t, 8, 3 + rightLift, 3, 5 - rightLift, pantsDark);
                Rect(t, 10, 2 + leftLift, 5, 7 - leftLift, line);
                Rect(t, 11, 3 + leftLift, 3, 5 - leftLift, pants);
                Rect(t, 6, 1 + rightLift, 6, 3, line);
                Rect(t, 7, 2 + rightLift, 5, 1, shoe);
                Rect(t, 10, 1 + leftLift, 7, 3, line);
                Rect(t, 11, 2 + leftLift, 5, 1, shoe);

                if (backpack)
                {
                    Rect(t, 3, 10, 7, 9, line);
                    Rect(t, 4, 11, 5, 7, accent);
                    VLine(t, 4, 12, 17, accentDark);
                }

                Rect(t, 6, 8, 10, 11, line);
                Rect(t, 7, 9, 8, 9, shirt);
                VLine(t, 7, 10, 17, shirtDark);
                Rect(t, 14, 10 + leftLift, 4, 7 - leftLift, line);
                Rect(t, 15, 11 + leftLift, 2, 4, shirt);
                Rect(t, 15, 9 + leftLift, 3, 3, line);
                P(t, 16, 10 + leftLift, skin); P(t, 17, 10 + leftLift, skinDark);

                Rect(t, 7, 17, 5, 3, line);
                Rect(t, 8, 18, 3, 2, skinDark);
                Rect(t, 6, 19, 10, 8, line);
                Rect(t, 7, 20, 8, 6, skin);
                Rect(t, 6, 23, 3, 4, hair);
                Rect(t, 7, 25, 8, 2, hair);
                P(t, 14, 23, line);
                P(t, 15, 21, skinDark);

                if (hat)
                {
                    Rect(t, 4, 24, 14, 3, line);
                    Rect(t, 5, 25, 12, 1, accent);
                    Rect(t, 7, 26, 8, 2, line);
                    Rect(t, 8, 26, 6, 2, accent);
                }
            }
            else
            {
                // 正面/背面双腿、鞋子与步行动画。
                Rect(t, 4, 2 + leftLift, 6, 7 - leftLift, line);
                Rect(t, 5, 3 + leftLift, 4, 5 - leftLift, pants);
                Rect(t, 10, 2 + rightLift, 6, 7 - rightLift, line);
                Rect(t, 11, 3 + rightLift, 4, 5 - rightLift, pantsDark);
                Rect(t, 3, 1 + leftLift, 7, 3, line);
                Rect(t, 4, 2 + leftLift, 5, 1, shoe);
                Rect(t, 10, 1 + rightLift, 7, 3, line);
                Rect(t, 11, 2 + rightLift, 5, 1, shoe);

                Rect(t, 4, 9, 12, 10, line);
                Rect(t, 5, 10, 10, 8, shirt);
                HLine(t, 10, 5, 14, shirtDark);

                Rect(t, 2, 10 + rightLift, 4, 8 - rightLift, line);
                Rect(t, 3, 11 + rightLift, 2, 5, shirt);
                Rect(t, 2, 9 + rightLift, 4, 3, line);
                P(t, 3, 10 + rightLift, skin); P(t, 4, 10 + rightLift, skinDark);
                Rect(t, 14, 10 + leftLift, 4, 8 - leftLift, line);
                Rect(t, 15, 11 + leftLift, 2, 5, shirtDark);
                Rect(t, 14, 9 + leftLift, 4, 3, line);
                P(t, 15, 10 + leftLift, skin); P(t, 16, 10 + leftLift, skinDark);

                if (backpack && dir == Dir.Up)
                {
                    Rect(t, 4, 10, 12, 9, line);
                    Rect(t, 5, 11, 10, 7, accent);
                    Frame(t, 6, 12, 8, 5, accentDark);
                }
                else if (backpack)
                {
                    Rect(t, 5, 12, 2, 6, accentDark);
                    Rect(t, 13, 12, 2, 6, accentDark);
                }

                if (tie && dir == Dir.Down)
                {
                    Rect(t, 8, 15, 4, 3, white);
                    Rect(t, 9, 13, 2, 4, accent);
                    P(t, 9, 12, accentDark); P(t, 10, 12, accentDark);
                }

                Rect(t, 7, 17, 6, 3, line);
                Rect(t, 8, 18, 4, 2, skinDark);
                Rect(t, 5, 19, 10, 8, line);

                if (dir == Dir.Down)
                {
                    Rect(t, 6, 20, 8, 6, skin);
                    Rect(t, 5, 23, 2, 4, hair);
                    Rect(t, 13, 23, 2, 4, hair);
                    Rect(t, 6, 25, 8, 2, hair);
                    P(t, 8, 23, line); P(t, 11, 23, line);
                    P(t, 9, 21, skinDark); P(t, 10, 21, skinDark);
                    HLine(t, 20, 9, 10, Shade(skinDark, 0.8f));
                }
                else
                {
                    Rect(t, 6, 20, 8, 6, hair);
                    Rect(t, 6, 20, 8, 2, Shade(hair, 0.78f));
                    Rect(t, 5, 22, 2, 5, hair);
                    Rect(t, 13, 22, 2, 5, hair);
                }

                if (hat)
                {
                    Rect(t, 3, 24, 14, 3, line);
                    Rect(t, 4, 25, 12, 1, accent);
                    Rect(t, 6, 26, 8, 2, line);
                    Rect(t, 7, 26, 6, 2, accent);
                }
            }
            Apply(t);
            return t;
        }

        // ————— V0.7.3《生长的小镇》新区建筑与道具 —————
        // 纪律与 2026-08-27 建筑改造一致：绘制时登记 GlowRect/SnowBand，夜光/积雪只读表。

        /// <summary>东街书店：靛蓝双层小楼 + 平顶女儿墙 + 楼顶招书牌，橱窗里是成排书脊。</summary>
        static void PaintBookstore(Texture2D t, List<GlowRect> g, List<SnowBand> s)
        {
            Color wall = RGB(0x5e, 0x6e, 0x9e), trim = RGB(0x2f, 0x3a, 0x5c), wood = RGB(0x8a, 0x5a, 0x38);
            Color shelf = RGB(0xd8, 0xb8, 0x7a);
            Rect(t, 14, 0, 36, 26, wall); Frame(t, 14, 0, 36, 26, trim);
            for (int y = 4; y < 26; y += 6) HLine(t, y, 15, 48, Shade(wall, 0.9f));      // 层间线脚

            // 女儿墙 + 楼顶招书牌（招牌即剪影记忆点）
            Rect(t, 12, 26, 40, 5, trim);
            Rect(t, 22, 31, 20, 12, wood); Frame(t, 22, 31, 20, 12, Shade(wood, 0.6f));
            Rect(t, 25, 35, 14, 4, RGB(0xf2, 0xe6, 0xc8));                                // 牌面
            for (int x = 27; x <= 36; x += 3) VLine(t, x, 36, 37, RGB(0x8a, 0x4a, 0x2e)); // 「书」意象竖排
            Rect(t, 30, 43, 4, 2, Shade(wood, 0.7f));
            s.Add(new SnowBand(12, 51, 26));
            s.Add(new SnowBand(22, 41, 31));

            // 底层大橱窗：三层书脊 + 斜放新书
            Rect(t, 18, 1, 20, 15, RGB(0x3a, 0x46, 0x66));
            Frame(t, 18, 1, 20, 15, trim);
            g.Add(new GlowRect(19, 2, 18, 13));
            for (int row = 0; row < 3; row++)
            {
                int ry = 4 + row * 4;
                HLine(t, ry, 19, 36, shelf);
                for (int x = 20; x <= 35; x += 2)
                {
                    Color spine = (x / 2 + row) % 3 == 0 ? RGB(0xc2, 0x56, 0x3c)
                        : (x / 2 + row) % 3 == 1 ? RGB(0x4e, 0x8a, 0x5e) : RGB(0xd8, 0xa8, 0x3c);
                    Rect(t, x, ry + 1, 1, 2, spine);
                }
            }
            P(t, 33, 14, RGB(0xe8, 0xe0, 0xcc)); P(t, 34, 15, RGB(0xe8, 0xe0, 0xcc));    // 斜靠新书

            // 门 + 雨棚 + 气窗
            Rect(t, 40, 0, 8, 13, wood); Frame(t, 40, 0, 8, 13, Shade(wood, 0.6f));
            P(t, 46, 6, RGB(0xe2, 0xc8, 0x8a));
            Rect(t, 40, 13, 8, 4, RGB(0x6a, 0x52, 0x38));
            g.Add(new GlowRect(41, 14, 6, 2));
            Awning(t, 16, 39, 18, 5, RGB(0xc2, 0x56, 0x3c), RGB(0xf2, 0xe6, 0xc8));
            // 围猎三#23 复审:旧整条 (16,39,19) 雪体 y19-21 横切两扇二层书房窗(x20-27/x36-43,y18-24),
            // 雪 overlay 排序提到 +2 后必然盖掉窗光——照阁楼窗口径收缩到窗两侧(含 1px 边距)。
            s.Add(new SnowBand(16, 19, 19));
            s.Add(new SnowBand(28, 35, 19));

            // 二层书房小窗（夜里有灯）
            FramedGlass(t, 20, 18, 8, 7, RGB(0x54, 0x74, 0x8E), g);
            FramedGlass(t, 36, 18, 8, 7, RGB(0x54, 0x74, 0x8E), g);
        }

        /// <summary>空置铺面：灰尘墙面 + 钉木板/糊报纸两种封法。夜不亮灯——闲置的铺面就是黑的（§125 教学对冲）。</summary>
        static void PaintEmptyShop(Texture2D t, List<GlowRect> g, List<SnowBand> s, int variant)
        {
            Color wall = variant == 0 ? RGB(0xb8, 0xae, 0x98) : RGB(0xae, 0xb2, 0xa2);
            Color board = RGB(0x9a, 0x76, 0x4c), paper = RGB(0xd8, 0xd2, 0xbe);
            Rect(t, 14, 0, 36, 24, wall); Frame(t, 14, 0, 36, 24, Shade(wall, 0.72f));
            for (int x = 17; x < 48; x += 7) P(t, x, 2, Shade(wall, 0.85f));              // 墙根污渍
            Rect(t, 12, 24, 40, 4, Shade(wall, 0.7f));                                    // 女儿墙
            s.Add(new SnowBand(12, 51, 24));

            if (variant == 0)
            {
                // 橱窗与门全部钉上交叉木板
                Rect(t, 18, 1, 18, 13, Shade(wall, 0.55f)); Frame(t, 18, 1, 18, 13, Shade(wall, 0.5f));
                for (int k = 0; k < 4; k++)
                {
                    int bx = 18 + k * 5;
                    for (int d = 0; d < 9; d++) P(t, bx + d, 2 + d, board);
                    for (int d = 0; d < 9; d++) P(t, bx + 8 - d, 2 + d, Shade(board, 0.85f));
                }
                Rect(t, 40, 0, 8, 13, Shade(wall, 0.6f));
                for (int d = 0; d < 10; d++) P(t, 40 + d, 0 + d, board);
            }
            else
            {
                // 糊报纸 + 褪色招租字残留
                Rect(t, 18, 1, 18, 13, paper); Frame(t, 18, 1, 18, 13, Shade(paper, 0.7f));
                HLine(t, 7, 19, 34, Shade(paper, 0.8f)); HLine(t, 11, 20, 33, Shade(paper, 0.8f));
                Rect(t, 25, 8, 5, 4, RGB(0xc2, 0x56, 0x3c));                              // 残留红纸
                Rect(t, 40, 0, 8, 13, Shade(paper, 0.9f)); Frame(t, 40, 0, 8, 13, Shade(paper, 0.65f));
                P(t, 41, 12, Shade(paper, 0.6f)); P(t, 46, 1, Shade(paper, 0.6f));
            }

            // 墙头枯草与褪色横幅勾一点萧条感，但构图仍保持小镇的整洁
            P(t, 15, 28, RGB(0x8a, 0x8a, 0x6a)); P(t, 44, 28, RGB(0x8a, 0x8a, 0x6a));
            Rect(t, 20, 27, 22, 2, Shade(wall, 0.62f));
        }

        // —— 新区道具（不进 Building 剪影体系：无窗光/积雪，由场景层直接摆放）——

        /// <summary>花亭：伞条纹小售花亭，桶装鲜花 + 悬挂盆栽（东商业新区「花店」位）。</summary>
        public static Sprite FloristKiosk()
        {
            Sprite cached;
            if (TryGetCached("prop_florist_kiosk", out cached)) return cached;
            Texture2D t = NewTex(44, 34);
            Color body = RGB(0xe8, 0xdc, 0xc0), wood = RGB(0x8a, 0x5a, 0x38);
            // 尖顶 + 条纹伞棚
            P(t, 22, 32, RGB(0xd8, 0xa8, 0x3c));
            for (int x = 8; x <= 36; x++) VLine(t, x, 27, 29, (x / 3) % 2 == 0 ? RGB(0xc2, 0x56, 0x3c) : RGB(0xf2, 0xe6, 0xc8));
            HLine(t, 26, 8, 36, Shade(wood, 0.9f));
            for (int x = 8; x <= 36; x += 3) P(t, x, 25, Shade(wood, 0.7f));              // 伞缘垂角
            // 亭身 + 开放柜台
            Rect(t, 10, 8, 24, 17, body); Frame(t, 10, 8, 24, 17, Shade(wood, 0.85f));
            Rect(t, 6, 8, 32, 3, wood);                                                   // 柜台板
            for (int x = 8; x <= 35; x += 2) P(t, x, 7, Shade(wood, 0.6f));
            // 桶装鲜花三桶 + 悬挂盆栽
            for (int k = 0; k < 3; k++)
            {
                int bx = 12 + k * 7;
                Rect(t, bx, 11, 4, 4, RGB(0x9a, 0xa8, 0xb8));
                P(t, bx + 1, 16, RGB(0xe2, 0x7a, 0xb8)); P(t, bx + 2, 17, RGB(0xf2, 0xd4, 0x5a));
                P(t, bx + 3, 16, RGB(0x5a, 0x94, 0x46)); P(t, bx + 1, 15, RGB(0x5a, 0x94, 0x46));
            }
            P(t, 14, 22, RGB(0x5a, 0x94, 0x46)); Rect(t, 13, 20, 3, 2, RGB(0xb8, 0x6a, 0x3c));
            P(t, 30, 22, RGB(0x5a, 0x94, 0x46)); Rect(t, 29, 20, 3, 2, RGB(0xb8, 0x6a, 0x3c));
            VLine(t, 22, 26, 30, Shade(wood, 0.8f));                                      // 伞柱
            Apply(t);
            return Finish(t, "prop_florist_kiosk");
        }

        /// <summary>行政广场旗杆（旗帜由 FlagCloth 单独挂载，颜色 = 财政立场）。</summary>
        public static Sprite Flagpole()
        {
            Sprite cached;
            if (TryGetCached("prop_flagpole", out cached)) return cached;
            Texture2D t = NewTex(12, 52);
            VLine(t, 5, 0, 48, RGB(0xc8, 0xcc, 0xd2)); VLine(t, 6, 0, 48, RGB(0x9a, 0xa2, 0xac));
            Rect(t, 2, 0, 8, 3, RGB(0x8a, 0x92, 0x9c)); Rect(t, 1, 2, 10, 2, RGB(0x6a, 0x72, 0x7c)); // 底座
            P(t, 5, 49, RGB(0xe8, 0xa8, 0x4c)); P(t, 6, 49, RGB(0xe8, 0xa8, 0x4c));       // 金色顶饰
            P(t, 5, 50, RGB(0xf2, 0xc8, 0x6a)); P(t, 6, 50, RGB(0xf2, 0xc8, 0x6a));
            Rect(t, 3, 44, 1, 1, RGB(0x7a, 0x54, 0x36));                                  // 挂环
            Apply(t);
            return Finish(t, "prop_flagpole");
        }

        /// <summary>白色旗面（SpriteRenderer.color 染色：扩张红 / 紧缩蓝 / 中性绿）。</summary>
        public static Sprite FlagCloth()
        {
            Sprite cached;
            if (TryGetCached("prop_flag_cloth", out cached)) return cached;
            Texture2D t = NewTex(16, 11);
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 14; x++)
                {
                    float wave = Mathf.Sin((x / 14f) * Mathf.PI);
                    if (x > 1 + wave * 1.5f && y > 1 - wave && y < 9 - wave * 0.5f)
                        P(t, x, y + 1, new Color32(255, 255, 255, 255));
                }
            for (int x = 0; x < 14; x++) P(t, x, 5 + (int)(Mathf.Sin(x / 14f * Mathf.PI) * 1.2f),
                new Color32(0, 0, 0, 36));                                                // 飘动暗纹
            Apply(t);
            return Finish(t, "prop_flag_cloth");
        }

        /// <summary>公告碑：石碑 + 碑文刻痕 + 基座（市长称号与任期的常驻载体）。</summary>
        public static Sprite Stele()
        {
            Sprite cached;
            if (TryGetCached("prop_stele", out cached)) return cached;
            Texture2D t = NewTex(22, 40);
            Color stone = RGB(0xb2, 0xb4, 0xae), dark = RGB(0x7e, 0x82, 0x7c);
            Rect(t, 5, 4, 12, 30, stone); Frame(t, 5, 4, 12, 30, dark);
            P(t, 6, 32, RGB(0xd8, 0xda, 0xd4)); P(t, 7, 33, RGB(0xd8, 0xda, 0xd4));       // 顶部高光
            for (int y = 24; y >= 10; y -= 4)
                HLine(t, y, 8, 13, dark);                                                 // 碑文刻痕
            Rect(t, 2, 0, 18, 4, RGB(0x8e, 0x90, 0x8a)); Rect(t, 1, 0, 20, 2, RGB(0x6e, 0x70, 0x6a)); // 基座
            Apply(t);
            return Finish(t, "prop_stele");
        }

        /// <summary>央行前政策利率牌：墨绿牌面 + 分段数码管意象（真实数字走按 E 面板，牌面只做意象）。</summary>
        public static Sprite RateBoard()
        {
            Sprite cached;
            if (TryGetCached("prop_rate_board", out cached)) return cached;
            Texture2D t = NewTex(34, 28);
            Color metal = RGB(0x5a, 0x62, 0x6a);
            Rect(t, 2, 6, 30, 18, RGB(0x1e, 0x30, 0x28)); Frame(t, 2, 6, 30, 18, metal);  // 墨绿牌面
            for (int k = 0; k < 3; k++)
            {
                int bx = 6 + k * 9;
                Rect(t, bx, 12, 6, 8, RGB(0x0e, 0x1a, 0x14));                             // 数码管窗
                P(t, bx + 1, 17, RGB(0x54, 0xd8, 0x8a)); P(t, bx + 2, 17, RGB(0x54, 0xd8, 0x8a));
                P(t, bx + 3, 14, RGB(0x2e, 0x88, 0x5a)); P(t, bx + 4, 13, RGB(0x2e, 0x88, 0x5a));
            }
            Rect(t, 6, 8, 22, 2, RGB(0x2e, 0x88, 0x5a));
            VLine(t, 6, 0, 6, metal); VLine(t, 27, 0, 6, metal);                          // 支腿
            Apply(t);
            return Finish(t, "prop_rate_board");
        }

        /// <summary>书店门口小黑板：A 字架 + 粉笔字迹（「本周概念」交互点的视觉锚）。</summary>
        public static Sprite ConceptChalkboard()
        {
            Sprite cached;
            if (TryGetCached("prop_concept_board", out cached)) return cached;
            Texture2D t = NewTex(30, 26);
            Color wood = RGB(0x8a, 0x5a, 0x38);
            Rect(t, 3, 8, 24, 14, RGB(0x2a, 0x3a, 0x30)); Frame(t, 3, 8, 24, 14, wood);   // 板面
            HLine(t, 18, 6, 23, new Color32(0xe8, 0xe6, 0xd8, 210));                      // 粉笔标题行
            HLine(t, 14, 7, 18, new Color32(0xd8, 0xd4, 0xc4, 170));
            HLine(t, 11, 7, 21, new Color32(0xd8, 0xd4, 0xc4, 170));
            P(t, 24, 14, new Color32(0xf2, 0xd4, 0x5a, 200));                             // 粉笔点缀
            VLine(t, 5, 0, 8, wood); VLine(t, 24, 0, 8, wood);                            // A 字架腿
            P(t, 6, 4, Shade(wood, 0.7f)); P(t, 23, 4, Shade(wood, 0.7f));
            Apply(t);
            return Finish(t, "prop_concept_board");
        }

        /// <summary>银行外立面亮化（金融街落成）：厚檐、柱头与入口壁灯的暖金 overlay，α 由夜幕驱动。</summary>
        public static Sprite BankFacadeLights()
        {
            Sprite cached;
            if (TryGetCached("prop_bank_facade_lights", out cached)) return cached;
            int w = BuildingSpecs.CanvasW("bank"), h = BuildingSpecs.CanvasH("bank");
            Texture2D t = NewTex(w, h);
            // M 档新檐口在 y53..56；S 分支只保留旧规格兼容。
            int eaveY = w == 64 ? 41 : 54;
            Color bulb = new Color32(0xff, 0xe2, 0xa0, 255);
            for (int x = w == 64 ? 5 : 10; x <= w - (w == 64 ? 6 : 11); x += 4)
            {
                P(t, x, eaveY, bulb);
                P(t, x, eaveY - 1, new Color32(0xff, 0xd2, 0x84, 120));
            }
            int[] colX = BankColumnX();
            for (int i = 0; i < colX.Length; i++)
            {
                int y = w == 64 ? eaveY - 3 : 43;
                P(t, colX[i] + 1, y, bulb);
                P(t, colX[i] + 1, y - 1, new Color32(0xff, 0xd2, 0x84, 120));
            }
            if (w == 88)
            {
                Rect(t, 30, 28, 3, 4, new Color32(0xff, 0xd2, 0x84, 105));
                Rect(t, 55, 28, 3, 4, new Color32(0xff, 0xd2, 0x84, 105));
                P(t, 31, 29, bulb); P(t, 56, 29, bulb);
            }
            Apply(t);
            return Finish(t, "prop_bank_facade_lights");
        }

        /// <summary>银行立柱横坐标（亮化灯位与 PaintClassicalBank 柱位共用；升档换画布时同步缩放）。</summary>
        static int[] BankColumnX()
        {
            if (BuildingSpecs.CanvasW("bank") == 64) return new[] { 13, 24, 39, 50 };
            // M 档 88 宽：四柱收拢成有进深的中央门廊（与 PaintClassicalBank 共用）。
            return new[] { 25, 32, 52, 59 };
        }

        /// <summary>施工尘土（§142：尘土粒子 ≤30 粒的视觉载体，由 TownGrowthDirector 驱动升腾消散）。</summary>
        public static Sprite DustPuff()
        {
            Sprite cached;
            if (TryGetCached("prop_dust_puff", out cached)) return cached;
            Texture2D t = NewTex(8, 8);
            Color dust = new Color32(0xc9, 0xba, 0x9c, 200);
            P(t, 3, 3, dust); P(t, 4, 3, dust); P(t, 3, 4, dust); P(t, 4, 4, dust);
            P(t, 2, 4, new Color32(0xc9, 0xba, 0x9c, 130)); P(t, 5, 4, new Color32(0xc9, 0xba, 0x9c, 130));
            P(t, 4, 5, new Color32(0xd8, 0xcc, 0xb2, 90));
            Apply(t);
            return Finish(t, "prop_dust_puff");
        }

        // ————— V0.7.4 过场大图(§149:320×180 逻辑分辨率 ×4 整数放大,分段绘制:天空/建筑/人物/前景) —————

        /// <summary>过场底图(id 对应 CutsceneId;V0.9.5 增第六张)。分层绘制进单张纹理,动效元素另出小精灵由导演驱动。</summary>
        public static Sprite Cutscene(int id)
        {
            string key = "cut" + id + "_base";
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(320, 180);
            switch (id)
            {
                case 1: PaintCutsceneOpening(t); break;
                case 2: PaintCutscenePriceWar(t); break;
                case 3: PaintCutsceneCrisis(t); break;
                case 4: PaintCutsceneTradeWar(t); break;
                case 5: PaintCutsceneFinale(t); break;
                case 6: PaintCutsceneHousingMania(t); break;
                case 7: PaintCutsceneMoveIn(t); break;
                case 8: PaintCutsceneCouncil(t); break;
                case 9: PaintCutsceneBureau(t); break;
                case 10: PaintCutsceneBankMandate(t); break;
                case 11: PaintCutsceneHomecoming(t); break;
            }
            Apply(t);
            return Finish(t, key);
        }

        // 天空渐变:bands 从地平线(低 y)到天顶(高 y)
        static void CutSky(Texture2D t, Color[] bands)
        {
            int bandH = 60 / bands.Length;
            for (int y = 120; y < 180; y++)
            {
                Color c = bands[Mathf.Clamp((y - 120) / bandH, 0, bands.Length - 1)];
                for (int x = 0; x < 320; x++) P(t, x, y, c);
            }
        }

        // 小镇剪影一排(暗色体块 + 亮窗点),画在 yBase 之上
        static void CutTownRow(Texture2D t, int yBase, int seed, Color body, Color win)
        {
            System.Random rng = new System.Random(seed);
            int x = 6;
            while (x < 310)
            {
                int w = 22 + rng.Next(0, 26);
                int h = 18 + rng.Next(0, 30);
                Rect(t, x, yBase, w, h, body);
                for (int wy = yBase + 5; wy < yBase + h - 3; wy += 6)
                    for (int wx = x + 4; wx < x + w - 4; wx += 7)
                        if (rng.Next(0, 100) < 55) Rect(t, wx, wy, 2, 3, win);
                x += w + 3 + rng.Next(0, 8);
            }
        }

        // 1 开业剪彩:清晨街道,左玩家果汁店/右 Sunny Tea,红绸横跨,人群围拢
        static void PaintCutsceneOpening(Texture2D t)
        {
            CutSky(t, new[] { RGB(0xff, 0xe2, 0xb8), RGB(0xff, 0xcf, 0xa0), RGB(0xf2, 0xb8, 0x92) });
            Rect(t, 118, 165, 7, 7, RGB(0xff, 0xf6, 0xd8)); Rect(t, 115, 162, 13, 13, new Color32(0xff, 0xf2, 0xcc, 90)); // 朝阳，避开左上角标题
            for (int k = 0; k < 3; k++) Rect(t, 210 + k * 34, 150 - k * 6, 26, 6, RGB(0xff, 0xea, 0xc8));              // 淡云
            Rect(t, 0, 56, 320, 64, RGB(0x8a, 0x9a, 0x78));                                            // 远景树篱
            for (int x = 4; x < 316; x += 11) Rect(t, x, 70 + (x * 7 % 13), 6, 10, RGB(0x6e, 0x82, 0x5c));
            Rect(t, 0, 0, 320, 56, RGB(0xc9, 0xba, 0x9c));                                             // 街面
            HLine(t, 52, 0, 319, RGB(0xb2, 0xa2, 0x84)); HLine(t, 12, 0, 319, RGB(0xb2, 0xa2, 0x84));
            for (int x = 8; x < 316; x += 24) Rect(t, x, 26, 14, 3, RGB(0xd8, 0xcc, 0xb2));            // 地砖缝
            // 左:玩家果汁店(橙)
            Rect(t, 14, 40, 118, 92, RGB(0xe8, 0xdc, 0xc0)); Frame(t, 14, 40, 118, 92, RGB(0x8a, 0x5a, 0x38));
            for (int x = 14; x <= 132; x++) HLine(t, 112, x, x, (x / 9) % 2 == 0 ? RGB(0xe8, 0x8a, 0x3c) : RGB(0xf6, 0xe8, 0xcc)); // 遮阳篷
            Rect(t, 20, 116, 106, 10, RGB(0x8a, 0x5a, 0x38));
            Rect(t, 38, 50, 70, 58, RGB(0x6a, 0x4a, 0x30)); Rect(t, 42, 54, 62, 50, RGB(0xff, 0xd9, 0x8a));   // 大橱窗
            VLine(t, 73, 54, 103, RGB(0x6a, 0x4a, 0x30));
            for (int k = 0; k < 4; k++) Rect(t, 48 + k * 14, 62, 6, 40, RGB(0xe8, 0x9a, 0x3a));        // 橱窗里果汁瓶
            // 右:Sunny Tea(青)
            Rect(t, 188, 40, 118, 92, RGB(0xe2, 0xea, 0xdc)); Frame(t, 188, 40, 118, 92, RGB(0x3c, 0x6a, 0x5a));
            for (int x = 188; x <= 306; x++) HLine(t, 112, x, x, (x / 9) % 2 == 0 ? RGB(0x3c, 0x96, 0x84) : RGB(0xf0, 0xf4, 0xe6));
            Rect(t, 194, 116, 106, 10, RGB(0x3c, 0x6a, 0x5a));
            Rect(t, 212, 50, 70, 58, RGB(0x2e, 0x4a, 0x40)); Rect(t, 216, 54, 62, 50, RGB(0xbe, 0xe8, 0xd8));
            VLine(t, 247, 54, 103, RGB(0x2e, 0x4a, 0x40));
            Rect(t, 236, 124, 26, 8, RGB(0x2e, 0x8a, 0x74));                                           // OPEN 牌
            Rect(t, 224, 132, 50, 8, RGB(0xf6, 0xf2, 0xe2)); HLine(t, 135, 226, 271, RGB(0x9a, 0xa2, 0x94));
            // 中缝:红绸 + 蝴蝶结(揭牌线)
            HLine(t, 116, 60, 132, RGB(0xc2, 0x3a, 0x3a)); HLine(t, 115, 66, 132, RGB(0xe2, 0x5a, 0x4a));
            Rect(t, 150, 66, 20, 16, RGB(0xd8, 0x46, 0x42)); Rect(t, 154, 70, 12, 8, RGB(0xf2, 0x7a, 0x6a));
            P(t, 148, 82, RGB(0xd8, 0x46, 0x42)); P(t, 171, 82, RGB(0xd8, 0x46, 0x42));
            Rect(t, 156, 56, 8, 12, RGB(0xe8, 0xc8, 0x6a));                                            // 金剪刀意象
            // 人群(小像素人,朝向中缝)
            int[] crowdX = { 30, 48, 66, 96, 118, 196, 214, 238, 262, 284 };
            Color[] shirts = { RGB(0x54, 0x74, 0x9c), RGB(0xb8, 0x5a, 0x4a), RGB(0x5a, 0x8a, 0x50), RGB(0x9a, 0x6a, 0xb2), RGB(0xc2, 0x92, 0x3c) };
            for (int k = 0; k < crowdX.Length; k++)
            {
                // 字幕带盖住画面最底约 37 像素。人站在店门口以上,身体才不会被字幕切成色块。
                int px = crowdX[k], py = 42 + k % 3 * 3;
                Rect(t, px, py + 4, 6, 8, shirts[k % shirts.Length]);                                   // 身
                Rect(t, px + 1, py + 12, 4, 3, RGB(0x3a, 0x32, 0x30));                                 // 头发
                Rect(t, px + 1, py + 9, 4, 3, RGB(0xe8, 0xc2, 0x9c));                                  // 脸
            }
            // 前景:彩带纸屑
            System.Random rng = new System.Random(11);
            for (int k = 0; k < 40; k++)
                P(t, rng.Next(4, 316), rng.Next(46, 108), k % 3 == 0 ? RGB(0xf2, 0xd4, 0x5a) : k % 3 == 1 ? RGB(0xe2, 0x7a, 0xb8) : RGB(0x7a, 0xc2, 0xd8));
        }

        // 2 价格战爆发:报纸头版铺满画面(标题文字由打字机层给出,版面只画骨架)
        static void PaintCutscenePriceWar(Texture2D t)
        {
            Rect(t, 0, 0, 320, 180, RGB(0x2a, 0x22, 0x1c));                                            // 桌面深底
            Rect(t, 10, 8, 300, 166, RGB(0xf4, 0xee, 0xda));                                           // 报纸
            for (int x = 12; x < 308; x += 3) P(t, x, 171, RGB(0x8a, 0x7c, 0x64));                     // 报纸投影
            Rect(t, 10, 128, 300, 46, RGB(0xf4, 0xee, 0xda)); Frame(t, 10, 128, 300, 46, RGB(0x3a, 0x32, 0x2c)); // 头版标题区(打字机占位)
            Rect(t, 118, 132, 84, 14, RGB(0x2c, 0x24, 0x20));                                          // 报头黑体条
            for (int x = 124; x < 196; x += 4) P(t, x, 139, RGB(0xf2, 0xe6, 0xc8));                    // 报头刻字
            HLine(t, 127, 12, 307, RGB(0x6a, 0x5a, 0x48)); HLine(t, 125, 12, 307, RGB(0x6a, 0x5a, 0x48));
            // 左:照片框(两家店对峙 + 降价箭头)
            Rect(t, 22, 66, 128, 54, RGB(0xd4, 0xca, 0xb2)); Frame(t, 22, 66, 128, 54, RGB(0x3a, 0x32, 0x2c));
            Rect(t, 34, 72, 40, 42, RGB(0xe8, 0x8a, 0x3c)); Rect(t, 94, 72, 40, 42, RGB(0x3c, 0x96, 0x84));
            Rect(t, 44, 78, 20, 18, RGB(0xff, 0xd9, 0x8a)); Rect(t, 104, 78, 20, 18, RGB(0xbe, 0xe8, 0xd8));
            for (int k = 0; k < 5; k++) P(t, 80 + k * 3, 88 + k * 4, RGB(0xc2, 0x3a, 0x3a));           // 红色下坠箭头
            Rect(t, 74, 96, 12, 3, RGB(0xc2, 0x3a, 0x3a));
            // 右:文字栏(灰条占位)
            for (int col = 0; col < 2; col++)
                for (int k = 0; k < 9; k++)
                    Rect(t, 166 + col * 68, 68 + k * 6, col == 0 ? (k % 3 == 2 ? 40 : 56) : (k % 4 == 3 ? 34 : 54), 3,
                        RGB(0xb2, 0xa8, 0x92));
            // 「号外」红章
            Rect(t, 258, 138, 44, 26, new Color32(0xc2, 0x3a, 0x3a, 235));
            for (int x = 262; x < 298; x += 4) P(t, x, 150, RGB(0xf6, 0xe8, 0xc8));
            HLine(t, 146, 261, 296, RGB(0xf6, 0xe8, 0xc8));
        }

        // 6 楼市狂热头版(V0.9.5):报纸版式(照 PriceWar 报纸模板),照片框换楼群天际线+上行价格折线,
        // 右侧加涨幅小柱图(装饰性等宽柱,真实数字由打字机层给出,§149 数字不进底图)。
        static void PaintCutsceneHousingMania(Texture2D t)
        {
            Rect(t, 0, 0, 320, 180, RGB(0x2a, 0x22, 0x1c));                                            // 桌面深底
            Rect(t, 10, 8, 300, 166, RGB(0xf4, 0xee, 0xda));                                           // 报纸
            for (int x = 12; x < 308; x += 3) P(t, x, 171, RGB(0x8a, 0x7c, 0x64));                     // 报纸投影
            Rect(t, 10, 128, 300, 46, RGB(0xf4, 0xee, 0xda)); Frame(t, 10, 128, 300, 46, RGB(0x3a, 0x32, 0x2c)); // 头版标题区(打字机占位)
            Rect(t, 118, 132, 84, 14, RGB(0x2c, 0x24, 0x20));                                          // 报头黑体条
            for (int x = 124; x < 196; x += 4) P(t, x, 139, RGB(0xf2, 0xe6, 0xc8));                    // 报头刻字
            HLine(t, 127, 12, 307, RGB(0x6a, 0x5a, 0x48)); HLine(t, 125, 12, 307, RGB(0x6a, 0x5a, 0x48));
            // 左:照片框(楼群天际线越起越高 + 一路上行的红色价格折线)
            Rect(t, 22, 66, 128, 54, RGB(0xd4, 0xca, 0xb2)); Frame(t, 22, 66, 128, 54, RGB(0x3a, 0x32, 0x2c));
            for (int k = 0; k < 6; k++)
                Rect(t, 28 + k * 20, 70 + (k % 3) * 4 + (k / 3) * 8, 16, 14 + k * 3, RGB(0x8a, 0x7a, 0x66));
            for (int k = 0; k < 9; k++)
                P(t, 32 + k * 13, 112 - k * 5, RGB(0xc2, 0x3a, 0x3a));
            HLine(t, 108, 30, 142, RGB(0xc2, 0x3a, 0x3a));                                             // 折线终点横杠(创新高)
            // 右:涨幅小柱图(共底逐高)+ 双栏灰条文字占位
            for (int k = 0; k < 7; k++)
                Rect(t, 166 + k * 8, 64, 5, 4 + k * 4, RGB(0x3c, 0x96, 0x84));
            for (int col = 0; col < 2; col++)
                for (int k = 0; k < 5; k++)
                    Rect(t, 166 + col * 68, 106 + k * 6, col == 0 ? (k % 3 == 2 ? 40 : 56) : (k % 4 == 3 ? 34 : 54), 3,
                        RGB(0xb2, 0xa8, 0x92));
            // 「号外」红章
            Rect(t, 258, 138, 44, 26, new Color32(0xc2, 0x3a, 0x3a, 235));
            for (int x = 262; x < 298; x += 4) P(t, x, 150, RGB(0xf6, 0xe8, 0xc8));
            HLine(t, 146, 261, 296, RGB(0xf6, 0xe8, 0xc8));
        }

        // 3 乌云压城:灰暗小镇全景,店门告示,雨幕(雨由 overlay 动效补)
        static void PaintCutsceneCrisis(Texture2D t)
        {
            CutSky(t, new[] { RGB(0x3a, 0x42, 0x50), RGB(0x4a, 0x52, 0x60), RGB(0x5a, 0x62, 0x70) });
            // 厚云层
            System.Random rng = new System.Random(7);
            for (int k = 0; k < 14; k++)
            {
                int cx = rng.Next(0, 300), cy = 148 + rng.Next(0, 24), cw = 30 + rng.Next(0, 40);
                for (int x = 0; x < cw; x++)
                {
                    float h = Mathf.Sin(x / (float)cw * Mathf.PI) * (8 + rng.Next(0, 6));
                    for (int y = 0; y < h; y++) P(t, cx + x, cy + y, x % 3 == 0 ? RGB(0x2e, 0x34, 0x40) : RGB(0x38, 0x40, 0x4c));
                }
            }
            Rect(t, 0, 64, 320, 20, RGB(0x5e, 0x6a, 0x64));                                            // 远景雾带
            CutTownRow(t, 66, 23, RGB(0x36, 0x3e, 0x46), RGB(0x8a, 0x94, 0x7c));                      // 小镇剪影
            Rect(t, 0, 0, 320, 66, RGB(0x4c, 0x50, 0x4a));                                             // 湿街面
            for (int x = 6; x < 316; x += 26) Rect(t, x, 30, 18, 5, RGB(0x5a, 0x60, 0x5c));            // 水洼
            HLine(t, 62, 0, 319, RGB(0x40, 0x44, 0x40));
            // 中央:你的店(雨里的暖色孤岛)
            Rect(t, 116, 26, 88, 78, RGB(0x6a, 0x5a, 0x4c)); Frame(t, 116, 26, 88, 78, RGB(0x4a, 0x3e, 0x34));
            for (int x = 116; x <= 204; x++) HLine(t, 92, x, x, (x / 8) % 2 == 0 ? RGB(0x9a, 0x5a, 0x2c) : RGB(0xc2, 0xae, 0x8c)); // 破雨篷
            Rect(t, 122, 84, 76, 8, RGB(0x4a, 0x3e, 0x34));
            Rect(t, 130, 34, 26, 44, RGB(0x2e, 0x2a, 0x26));                                           // 门洞
            Rect(t, 156, 34, 36, 30, RGB(0x2e, 0x2a, 0x26));                                           // 黑橱窗
            Rect(t, 160, 38, 28, 22, RGB(0x3e, 0x3a, 0x34));
            // 门上告示(白纸 + 红章)
            Rect(t, 135, 46, 16, 20, RGB(0xe8, 0xe2, 0xd2)); Frame(t, 135, 46, 16, 20, RGB(0x8a, 0x84, 0x74));
            HLine(t, 60, 137, 148, RGB(0x6a, 0x64, 0x58)); HLine(t, 55, 137, 145, RGB(0x6a, 0x64, 0x58));
            HLine(t, 50, 137, 148, RGB(0x6a, 0x64, 0x58));
            Rect(t, 138, 66, 10, 4, RGB(0xb2, 0x3a, 0x3a));                                            // 红章
            // 前景:屋檐滴水与暗角
            for (int x = 20; x < 300; x += 44) VLine(t, x, 22, 26, new Color32(0x8a, 0x94, 0xa4, 150));
            for (int x = 0; x < 320; x++) { P(t, x, 0, RGB(0x28, 0x2c, 0x34)); if (x % 2 == 0) P(t, x, 1, RGB(0x2c, 0x30, 0x38)); }
        }

        // 4 贸易战头版:左 A 镇暖色 / 右 B 国冷色,海关闸口 + 关税大牌(数字由打字机层给出)
        static void PaintCutsceneTradeWar(Texture2D t)
        {
            // 左右分色天空与地面
            for (int y = 0; y < 180; y++)
                for (int x = 0; x < 320; x++)
                {
                    bool left = x < 130;
                    if (y >= 110)
                        P(t, x, y, left ? Color.Lerp(RGB(0xf2, 0xd8, 0xb0), RGB(0xe2, 0xb8, 0x88), (y - 110) / 70f)
                                       : Color.Lerp(RGB(0x9a, 0xaa, 0xc4), RGB(0x6a, 0x7a, 0x9a), (y - 110) / 70f));
                    else
                        P(t, x, y, left ? RGB(0xc2, 0xa8, 0x88) : RGB(0x8a, 0x96, 0xac));
                }
            // 关税大牌(闸口横梁)
            Rect(t, 96, 96, 128, 34, RGB(0x2e, 0x34, 0x40)); Frame(t, 96, 96, 128, 34, RGB(0x5a, 0x62, 0x6e));
            for (int x = 104; x < 216; x += 6) P(t, x, 104, RGB(0xe2, 0x5a, 0x4a));                   // 警示纹
            Rect(t, 102, 100, 116, 12, RGB(0x1a, 0x1e, 0x26));                                         // 数字窗(打字机占位)
            VLine(t, 104, 44, 96, RGB(0x5a, 0x62, 0x6e)); VLine(t, 215, 44, 96, RGB(0x5a, 0x62, 0x6e)); // 闸柱
            Rect(t, 96, 40, 128, 6, RGB(0x4a, 0x52, 0x5e));
            // 左:A 镇(暖)小店与果箱
            Rect(t, 18, 52, 74, 56, RGB(0xe8, 0xd8, 0xb8)); Frame(t, 18, 52, 74, 56, RGB(0x8a, 0x6a, 0x4a));
            for (int x = 18; x <= 92; x++) HLine(t, 94, x, x, (x / 7) % 2 == 0 ? RGB(0xe8, 0x8a, 0x3c) : RGB(0xf6, 0xe8, 0xcc));
            Rect(t, 30, 60, 50, 28, RGB(0xff, 0xd9, 0x8a)); Frame(t, 30, 60, 50, 28, RGB(0x8a, 0x6a, 0x4a));
            for (int k = 0; k < 3; k++) Rect(t, 36 + k * 14, 66, 8, 16, RGB(0xe8, 0x9a, 0x3a));
            Rect(t, 8, 36, 20, 12, RGB(0xb2, 0x7a, 0x42)); Rect(t, 11, 39, 14, 3, RGB(0x6a, 0x42, 0x22)); // 木箱
            // 右:B 国(冷)海关楼 + 货柜
            Rect(t, 232, 56, 78, 52, RGB(0x8e, 0x9a, 0xae)); Frame(t, 232, 56, 78, 52, RGB(0x5a, 0x66, 0x7c));
            Rect(t, 244, 66, 54, 26, RGB(0x4e, 0x5a, 0x70)); VLine(t, 271, 66, 91, RGB(0x3a, 0x44, 0x58));
            Rect(t, 240, 48, 60, 8, RGB(0x6a, 0x76, 0x8c));
            for (int k = 0; k < 3; k++) Rect(t, 238 + k * 22, 30, 20, 14, k % 2 == 0 ? RGB(0x4a, 0x6a, 0x8a) : RGB(0x6a, 0x4a, 0x5a)); // 货柜
            // 右旗杆(B 旗由 overlay 摆动)
            VLine(t, 306, 40, 96, RGB(0xc8, 0xcc, 0xd2)); Rect(t, 302, 96, 10, 3, RGB(0x6a, 0x72, 0x7c));
            // 中缝警示路障
            for (int k = 0; k < 8; k++) Rect(t, 118 + k * 12, 22, 10, 6, k % 2 == 0 ? RGB(0xe2, 0xc0, 0x4a) : RGB(0x3a, 0x3e, 0x46));
            Rect(t, 114, 14, 106, 8, RGB(0x8a, 0x92, 0x9c));
            // 前景两侧暗角
            for (int y = 0; y < 14; y++) { HLine(t, y, 0, 40 - y * 3, RGB(0x2a, 0x2e, 0x38)); HLine(t, y, 280 + y * 3, 319, RGB(0x26, 0x2a, 0x34)); }
        }

        // 5 收官:金色港口全景 + 三元悖论三开关图 + 全线完成落款(V0.9.5 随第八篇收弧)
        static void PaintCutsceneFinale(Texture2D t)
        {
            CutSky(t, new[] { RGB(0xff, 0xd8, 0x9a), RGB(0xf2, 0xb0, 0x78), RGB(0xd8, 0x84, 0x60) });
            Rect(t, 246, 138, 18, 18, RGB(0xff, 0xf0, 0xc0)); Rect(t, 242, 134, 26, 26, new Color32(0xff, 0xe2, 0xa0, 120)); // 落日
            Rect(t, 0, 96, 320, 24, RGB(0x9a, 0x7a, 0x64));                                            // 远岸
            CutTownRow(t, 98, 31, RGB(0x6a, 0x52, 0x48), RGB(0xff, 0xc8, 0x78));                       // 小镇剪影(暖窗)
            Rect(t, 0, 84, 320, 14, RGB(0x4a, 0x6e, 0x84));                                            // 海面
            for (int y = 84; y < 98; y += 4) for (int x = (y * 5) % 16; x < 320; x += 16) HLine(t, y, x, x + 6, RGB(0x5c, 0x82, 0x98));
            Rect(t, 0, 0, 320, 84, RGB(0x3e, 0x5e, 0x74));                                             // 近海
            for (int y = 8; y < 84; y += 6) for (int x = (y * 7) % 22; x < 320; x += 22) HLine(t, y, x, x + 9, RGB(0x4a, 0x6c, 0x84));
            // 码头栈桥 + 龙门吊
            Rect(t, 40, 52, 150, 8, RGB(0x6a, 0x5a, 0x4a)); for (int x = 44; x < 188; x += 12) VLine(t, x, 44, 52, RGB(0x5a, 0x4c, 0x3e));
            Rect(t, 120, 60, 5, 40, RGB(0x7a, 0x6a, 0x56)); Rect(t, 108, 96, 30, 5, RGB(0x6a, 0x5a, 0x4a)); // 吊塔
            HLine(t, 92, 124, 128, RGB(0x8a, 0x7a, 0x64)); Rect(t, 88, 88, 6, 6, RGB(0xd8, 0xa8, 0x3c));   // 吊钩货箱
            // 出货轮船
            Rect(t, 196, 42, 84, 18, RGB(0x8a, 0x3a, 0x3a)); Frame(t, 196, 42, 84, 18, RGB(0x5e, 0x28, 0x28));
            Rect(t, 214, 60, 14, 12, RGB(0xe8, 0xe2, 0xd2)); Rect(t, 240, 60, 12, 9, RGB(0xe8, 0xe2, 0xd2));
            for (int k = 0; k < 4; k++) Rect(t, 204 + k * 18, 48, 12, 8, RGB(0xf2, 0xd4, 0x5a));       // 货舱
            VLine(t, 280, 60, 96, RGB(0x4a, 0x3a, 0x30)); Rect(t, 274, 96, 14, 4, RGB(0x3a, 0x2e, 0x26));
            // 灯塔(右) + 光带(overlay 动效)
            Rect(t, 288, 60, 14, 38, RGB(0xe8, 0xe2, 0xd2)); for (int y = 62; y < 96; y += 6) HLine(t, y, 288, 301, RGB(0xc2, 0x3a, 0x3a));
            Rect(t, 286, 96, 18, 6, RGB(0x6a, 0x5a, 0x4a)); Rect(t, 290, 98, 10, 4, RGB(0xff, 0xe2, 0x8a));
            // 前景右下:三元悖论三开关图(两开一关)
            Rect(t, 204, 6, 108, 52, new Color32(0x24, 0x28, 0x30, 235)); Frame(t, 204, 6, 108, 52, RGB(0x8a, 0x92, 0x9c));
            Rect(t, 210, 44, 96, 9, RGB(0x3a, 0x40, 0x4c));                                            // 图题条
            for (int k = 0; k < 3; k++)
            {
                int sx = 218 + k * 32;
                Rect(t, sx, 20, 14, 20, RGB(0x3a, 0x40, 0x4c));                                        // 开关槽
                Rect(t, sx + 3, k == 2 ? 12 : 28, 8, 8, k == 2 ? RGB(0x8a, 0x94, 0xa2) : RGB(0x54, 0xd8, 0x8a)); // 第三开关=关
                HLine(t, 36, sx + 1, sx + 12, RGB(0x5a, 0x62, 0x6e));                                  // 标签条
                P(t, sx + 6, 33, k == 2 ? RGB(0xe2, 0x5a, 0x4a) : RGB(0x54, 0xd8, 0x8a));
            }
            // 「全线完成」红落款放到标题右侧的空天空。原先 x=10、顶到 y=170，正压住左上角档名。
            Rect(t, 92, 146, 66, 24, new Color32(0xb2, 0x3a, 0x3a, 240)); Frame(t, 92, 146, 66, 24, RGB(0xe8, 0xc8, 0x6a));
            for (int k = 0; k < 4; k++) Rect(t, 98 + k * 15, 152, 11, 11, RGB(0xf6, 0xe8, 0xc8));      // 印章刻字意象
        }

        // ————— V0.9.17 篇间晋升线五段(授予场景,各挂对应篇阶段报告之前) —————

        // 7 搬进小镇(二→三篇):清晨的家——门半开漏暖光,窗台一杯果汁,门口行李箱与钥匙
        static void PaintCutsceneMoveIn(Texture2D t)
        {
            CutSky(t, new[] { RGB(0xff, 0xe8, 0xc0), RGB(0xf6, 0xc8, 0xa8), RGB(0xe8, 0xa8, 0x98) });
            // 渲图修正:月亮 9→14 加大光晕,天空补两只飞鸟——原稿天空大片平色显空、月不可见。
            Rect(t, 244, 156, 14, 14, RGB(0xff, 0xf6, 0xd8)); Rect(t, 239, 151, 24, 24, new Color32(0xff, 0xf0, 0xcc, 80));
            for (int k = 0; k < 3; k++) Rect(t, 34 + k * 92, 146 - k * 8, 34, 7, RGB(0xf6, 0xd8, 0xb4)); // 淡云
            for (int k = 0; k < 2; k++)                                                                 // 飞鸟两枚(V 形)
            {
                int bx = 108 + k * 46, by = 150 - k * 10;
                HLine(t, by, bx, bx + 5, RGB(0x8a, 0x7a, 0x74)); HLine(t, by - 2, bx + 5, bx + 10, RGB(0x8a, 0x7a, 0x74));
                HLine(t, by - 4, bx + 6, bx + 9, RGB(0x8a, 0x7a, 0x74));
            }
            Rect(t, 0, 62, 320, 58, RGB(0x8a, 0x9a, 0x78));                                              // 远景树篱
            for (int x = 4; x < 316; x += 11) Rect(t, x, 74 + (x * 5 % 11), 6, 9, RGB(0x6e, 0x82, 0x5c));
            CutTownRow(t, 64, 41, RGB(0x7a, 0x6a, 0x5c), RGB(0xff, 0xd8, 0x9a));                         // 远处小镇(暖窗)
            Rect(t, 0, 0, 320, 64, RGB(0xc2, 0xb4, 0x96));                                               // 街面
            HLine(t, 60, 0, 319, RGB(0xaa, 0x9a, 0x7e));
            for (int x = 8; x < 316; x += 24) Rect(t, x, 30, 14, 3, RGB(0xd4, 0xc8, 0xac));              // 地砖缝
            // 主体:广场东侧的家(暖木墙)
            Rect(t, 70, 46, 150, 96, RGB(0xe4, 0xd2, 0xb0)); Frame(t, 70, 46, 150, 96, RGB(0x7a, 0x5a, 0x3c));
            Rect(t, 76, 138, 138, 6, RGB(0x8a, 0x5a, 0x3c));                                             // 檐口
            for (int x = 76; x <= 214; x += 10) VLine(t, x, 138, 142, RGB(0x6a, 0x46, 0x2c));
            // 门:半开,漏出暖光
            Rect(t, 128, 52, 34, 66, RGB(0x4a, 0x34, 0x24)); Rect(t, 131, 55, 28, 60, RGB(0xff, 0xd8, 0x8a));
            Rect(t, 148, 52, 14, 66, RGB(0x8a, 0x5e, 0x38)); VLine(t, 152, 55, 110, RGB(0x6a, 0x46, 0x26));
            P(t, 150, 84, RGB(0xf2, 0xd4, 0x5a));                                                        // 门把
            // 右窗:亮,窗内下沿一杯果汁(渲图修正:原稿杯子悬在窗下墙面外——移进窗内窗台)
            Rect(t, 180, 76, 30, 30, RGB(0x5a, 0x42, 0x2c)); Rect(t, 183, 79, 24, 24, RGB(0xff, 0xe2, 0xa8));
            VLine(t, 195, 79, 103, RGB(0x5a, 0x42, 0x2c)); HLine(t, 90, 183, 206, RGB(0x5a, 0x42, 0x2c));
            Rect(t, 186, 82, 9, 11, RGB(0xe8, 0x9a, 0x3a)); Rect(t, 188, 79, 2, 4, RGB(0xd8, 0xb2, 0x4a));
            Rect(t, 187, 84, 2, 6, RGB(0xff, 0xc8, 0x78));                                               // 杯身高光
            // 左窗:暖帘
            Rect(t, 84, 76, 30, 30, RGB(0x5a, 0x42, 0x2c)); Rect(t, 87, 79, 24, 24, RGB(0xf2, 0xb8, 0x7a));
            // 门前:门垫 + 行李箱 + 钥匙串(渲图修正:门垫加宽、行李箱缩小、钥匙重画为可见尺寸)
            Rect(t, 120, 40, 54, 8, RGB(0x9a, 0x6a, 0x4a)); HLine(t, 43, 122, 172, RGB(0x7a, 0x52, 0x38));
            Rect(t, 228, 44, 38, 26, RGB(0xa0, 0x6a, 0x40)); Frame(t, 228, 44, 38, 26, RGB(0x6e, 0x46, 0x26));
            Rect(t, 240, 70, 14, 5, RGB(0x6e, 0x46, 0x26)); VLine(t, 247, 72, 76, RGB(0x6e, 0x46, 0x26));
            Rect(t, 233, 49, 9, 7, RGB(0xe2, 0x5a, 0x4a)); Rect(t, 245, 52, 7, 5, RGB(0x54, 0x8a, 0xc4)); // 行李贴纸
            // 钥匙串:金环 + 柄 + 两齿,落在门垫右侧地面上(20×10 可见尺寸)
            Rect(t, 186, 38, 20, 4, RGB(0xd8, 0xb2, 0x4a)); Rect(t, 188, 36, 5, 3, RGB(0xd8, 0xb2, 0x4a));
            Rect(t, 202, 42, 3, 5, RGB(0xd8, 0xb2, 0x4a)); Rect(t, 206, 42, 3, 3, RGB(0xd8, 0xb2, 0x4a));
            Rect(t, 187, 39, 18, 2, RGB(0xf6, 0xe0, 0x9a));
        }

        // 8 镇议会的聘书(三→四篇):深夜长桌尽头的空椅,亮着的台灯与窗外熄着的路灯,桌上一纸聘书
        static void PaintCutsceneCouncil(Texture2D t)
        {
            // 渲图修正稿:台灯补全座/杆/罩/光晕、窗降至桌面延长线上不再悬浮、窗内路灯
            // 落地加剪影、椅背加立柱顶球与座面、上半墙补吊灯与镇图挂画。
            for (int y = 0; y < 180; y++)
                for (int x = 0; x < 320; x++)
                    P(t, x, y, Color.Lerp(RGB(0x24, 0x2a, 0x3a), RGB(0x14, 0x18, 0x24), y / 180f));     // 深夜墙
            for (int x = 0; x < 320; x += 16) VLine(t, x, 88, 128, RGB(0x34, 0x2c, 0x26));               // 护墙板缝(加密)
            HLine(t, 128, 0, 319, RGB(0x46, 0x38, 0x2c)); HLine(t, 88, 0, 319, RGB(0x3a, 0x30, 0x28));
            // 上半墙:吊灯剪影(链条+罩)与小镇图挂画
            VLine(t, 160, 168, 178, RGB(0x2c, 0x24, 0x1e));
            Rect(t, 146, 156, 28, 12, RGB(0x3a, 0x30, 0x26)); HLine(t, 157, 152, 168, RGB(0x6a, 0x58, 0x3e));
            Rect(t, 150, 160, 20, 6, RGB(0xd8, 0xb0, 0x6a));                                              // 吊灯暖芯
            // 挂画下移，避开左上角档名。原先顶到 y=172，压住「小镇大事记」。
            Rect(t, 24, 112, 44, 32, RGB(0x3e, 0x34, 0x2a)); Frame(t, 24, 112, 44, 32, RGB(0x6a, 0x58, 0x3e));
            Rect(t, 28, 116, 36, 24, RGB(0x8a, 0x82, 0x70));                                              // 挂画:小镇图意象
            for (int k = 0; k < 3; k++) Rect(t, 32 + k * 11, 120, 7, 8, RGB(0x5a, 0x62, 0x6a));
            HLine(t, 130, 30, 62, RGB(0x4a, 0x52, 0x5a));
            // 窗(渲图三稿:夜景靠对比度——窗洞提亮成夜空蓝灰、窗框 3px 亮棕、星点纯白,
            // 否则深蓝墙上画深蓝窗等于没有窗)
            Rect(t, 206, 64, 82, 62, RGB(0x8a, 0x6a, 0x44));                                             // 窗框(亮棕粗框)
            Rect(t, 210, 68, 74, 54, RGB(0x2e, 0x3e, 0x56));                                             // 夜空窗洞
            HLine(t, 96, 210, 283, RGB(0x8a, 0x6a, 0x44)); VLine(t, 245, 68, 121, RGB(0x8a, 0x6a, 0x44)); // 中梃
            for (int k = 0; k < 10; k++) P(t, 215 + k * 7, 108 + (k * 7 % 12), RGB(0xe8, 0xee, 0xf8));
            // 窗内路灯:加粗 2px 中灰蓝,灯头有轮廓(熄着=无光晕)
            Rect(t, 270, 72, 2, 34, RGB(0x5a, 0x64, 0x78)); Rect(t, 265, 106, 12, 5, RGB(0x4e, 0x58, 0x6c));
            Rect(t, 267, 64, 8, 8, RGB(0x4e, 0x58, 0x6c)); Rect(t, 269, 66, 4, 4, RGB(0x38, 0x40, 0x50));
            // 长桌:横贯
            Rect(t, 24, 30, 272, 16, RGB(0x54, 0x3a, 0x24));                                             // 桌沿
            Rect(t, 30, 44, 260, 14, RGB(0x6a, 0x4c, 0x30)); Frame(t, 30, 44, 260, 14, RGB(0x4a, 0x34, 0x20));
            for (int x = 40; x < 280; x += 34) VLine(t, x, 30, 44, RGB(0x50, 0x38, 0x22));
            // 桌上:文件摞 + 亮台灯(渲图四稿:P 是纯 SetPixel 覆盖——光晕必须先画,
            // 否则后画的半透明矩形把灯座/杆/罩全部覆盖成 alpha 幽灵,台灯凭空消失)
            Rect(t, 58, 54, 60, 34, new Color32(0xff, 0xd8, 0x8a, 64));                                  // 暖光晕(先画)
            Rect(t, 36, 60, 30, 10, RGB(0xd8, 0xd0, 0xbc)); Rect(t, 39, 70, 24, 10, RGB(0xc8, 0xc0, 0xac));
            Rect(t, 76, 56, 22, 5, RGB(0x4a, 0x38, 0x28));                                               // 灯座(贴桌面)
            Rect(t, 85, 60, 4, 12, RGB(0x6a, 0x52, 0x38));                                               // 灯杆
            Rect(t, 74, 70, 26, 12, RGB(0xf2, 0xc8, 0x6a)); Frame(t, 74, 70, 26, 12, RGB(0xa8, 0x7a, 0x3a)); // 灯罩(亮黄)
            Rect(t, 78, 76, 18, 5, RGB(0xff, 0xe6, 0xa8));                                               // 罩口亮芯
            Rect(t, 150, 56, 54, 34, RGB(0xf2, 0xec, 0xd8)); Frame(t, 150, 56, 54, 34, RGB(0xb2, 0xa4, 0x8a));
            for (int k = 0; k < 4; k++) HLine(t, 62 + k * 6, 156, 198, RGB(0xb2, 0xa8, 0x94));           // 聘书行文
            Rect(t, 182, 58, 14, 10, RGB(0xb2, 0x3a, 0x3a)); P(t, 189, 63, RGB(0xe8, 0xc8, 0x6a));       // 红章
            // 尽头:高背空椅(渲图三稿:轮廓亮棕加粗,与暗墙拉开边界)
            Rect(t, 240, 126, 36, 8, RGB(0x6a, 0x4e, 0x32));                                             // 座面
            Rect(t, 236, 68, 4, 60, RGB(0x8a, 0x6a, 0x44)); Rect(t, 276, 68, 4, 60, RGB(0x8a, 0x6a, 0x44)); // 背柱(粗)
            Rect(t, 244, 74, 28, 52, RGB(0x46, 0x32, 0x22)); Frame(t, 244, 74, 28, 52, RGB(0x8a, 0x6a, 0x44));
            P(t, 236, 130, RGB(0xe8, 0xc8, 0x6a)); P(t, 279, 130, RGB(0xe8, 0xc8, 0x6a));                // 扶手球
            Rect(t, 233, 64, 50, 7, RGB(0x8a, 0x6a, 0x44));                                              // 椅顶横档
            // 前景暗角
            for (int y = 0; y < 16; y++) HLine(t, y, 0, 319, new Color32(0x0a, 0x0c, 0x14, (byte)(200 - y * 10)));
        }

        // 9 从小镇到整个经济(四→五篇):市政厅窗内望出的全镇鸟瞰,报表升起铺向画面
        static void PaintCutsceneBureau(Texture2D t)
        {
            CutSky(t, new[] { RGB(0xc8, 0xdc, 0xe8), RGB(0xb2, 0xca, 0xdc), RGB(0x9a, 0xb8, 0xd0) });    // 晨光
            Rect(t, 0, 0, 320, 58, RGB(0x4a, 0x36, 0x24));                                              // 窗下室内,底边不再透出游戏
            Rect(t, 0, 58, 320, 62, RGB(0x8e, 0xa4, 0x74));                                              // 镇区绿地
            for (int x = 10; x < 320; x += 48) VLine(t, x, 58, 112, RGB(0xb2, 0xb4, 0xa2));              // 纵街
            HLine(t, 100, 0, 319, RGB(0xb2, 0xb4, 0xa2)); HLine(t, 76, 0, 319, RGB(0xb2, 0xb4, 0xa2));   // 横街
            int[] seeds = { 12, 66, 130, 200, 262 };
            for (int k = 0; k < 5; k++)
            {
                int bx = seeds[k], by = 62 + (k % 3) * 12;
                Rect(t, bx, by, 30 - (k % 2) * 6, 14 + (k % 3) * 4, k % 2 == 0 ? RGB(0xc8, 0xb8, 0x98) : RGB(0xa8, 0xb4, 0xb0));
                Rect(t, bx + 4, by + 4, 8, 6, RGB(0xff, 0xe2, 0xa0)); Rect(t, bx + 16, by + 5, 6, 5, RGB(0xff, 0xe2, 0xa0));
            }
            Rect(t, 148, 92, 26, 22, RGB(0xd8, 0xd2, 0xc4)); Frame(t, 148, 92, 26, 22, RGB(0x8a, 0x8e, 0x94));
            Rect(t, 154, 114, 14, 10, RGB(0xb2, 0xae, 0xa4));                                            // 窗正对的市政厅
            // 渲图修正:晨空补两枚飞鸟(原稿窗上方大片平色)
            for (int k = 0; k < 2; k++)
            {
                int bx = 46 + k * 58, by = 150 - k * 8;
                HLine(t, by, bx, bx + 5, RGB(0x7a, 0x82, 0x8a)); HLine(t, by - 2, bx + 5, bx + 10, RGB(0x7a, 0x82, 0x8a));
                HLine(t, by - 4, bx + 6, bx + 9, RGB(0x7a, 0x82, 0x8a));
            }
            // 窗框上沿降到档名下面。原先横木画到 y=176，整条压住左上角标题。
            for (int x = 0; x < 320; x++) { P(t, x, 164, RGB(0x5a, 0x42, 0x2c)); P(t, x, 160, RGB(0x6a, 0x4e, 0x34)); }
            for (int x = 0; x < 320; x++) { P(t, x, 0, RGB(0x4a, 0x36, 0x24)); P(t, x, 4, RGB(0x5a, 0x42, 0x2c)); }
            VLine(t, 158, 0, 164, RGB(0x5a, 0x42, 0x2c)); VLine(t, 162, 0, 164, RGB(0x4a, 0x36, 0x24));
            Rect(t, 0, 118, 320, 42, RGB(0x5e, 0x46, 0x30)); HLine(t, 118, 0, 319, RGB(0x7a, 0x5c, 0x3c)); // 窗台，顶边停在 y=159
            // 升起的报表三张:柱图/折线/表格,错落上浮(微观→宏观的视觉隐喻)
            PaintRisingSheet(t, 26, 84, 74, 46, 0);
            PaintRisingSheet(t, 108, 100, 84, 40, 2);
            PaintRisingSheet(t, 188, 90, 70, 44, 1);
        }

        // 升起报表的共用绘制:白纸+表行,kind 0 柱图 / 1 折线 / 2 表格
        static void PaintRisingSheet(Texture2D t, int x, int y, int w, int h, int kind)
        {
            Rect(t, x, y, w, h, RGB(0xf2, 0xee, 0xe0)); Frame(t, x, y, w, h, RGB(0xb2, 0xaa, 0x94));
            for (int k = 0; k < 3; k++) HLine(t, y + h - 8 - k * 6, x + 5, x + w - 5, RGB(0xc8, 0xc2, 0xae));
            if (kind == 0)
                for (int k = 0; k < 6; k++) Rect(t, x + 8 + k * 10, y + 4, 6, 6 + (k * 5 % 16), RGB(0x54, 0x8a, 0xc4));
            if (kind == 1)
            {
                for (int k = 0; k < 8; k++) P(t, x + 8 + k * 7, y + 6 + (k * 9 % 18), RGB(0xe2, 0x5a, 0x4a));
                for (int k = 0; k < 7; k++) HLine(t, y + 6 + (k * 9 % 18), x + 8 + k * 7, x + 15 + k * 7, RGB(0xe2, 0x5a, 0x4a));
            }
            if (kind == 2)
            {
                for (int k = 0; k < 4; k++) HLine(t, y + 6 + k * 8, x + 6, x + w - 6, RGB(0x8a, 0x94, 0x7c));
                for (int k = 0; k < 6; k++) VLine(t, x + 10 + k * 12, y + 4, y + h - 6, RGB(0xd4, 0xce, 0xba));
            }
        }

        // 10 老行长的委任状(五→六篇):打烊后的私人办公室——绿灯、账本、怀表、委任状,窗外排队剪影
        static void PaintCutsceneBankMandate(Texture2D t)
        {
            for (int y = 0; y < 180; y++)
                for (int x = 0; x < 320; x++)
                    P(t, x, y, Color.Lerp(RGB(0x2e, 0x28, 0x22), RGB(0x1c, 0x18, 0x16), y / 180f));     // 暖木暗墙
            for (int x = 0; x < 320; x += 26) VLine(t, x, 100, 150, RGB(0x44, 0x36, 0x2a));
            HLine(t, 150, 0, 319, RGB(0x54, 0x42, 0x32));
            // 背景窗:门外排队剪影(暗色小人一排)
            Rect(t, 202, 92, 86, 58, RGB(0x3e, 0x4a, 0x5a)); Frame(t, 202, 92, 86, 58, RGB(0x6a, 0x52, 0x3a));
            VLine(t, 245, 92, 150, RGB(0x6a, 0x52, 0x3a));
            for (int k = 0; k < 5; k++)
            {
                int px = 210 + k * 15;
                Rect(t, px, 100, 7, 16, RGB(0x28, 0x30, 0x3c)); Rect(t, px + 1, 116, 5, 14, RGB(0x28, 0x30, 0x3c));
            }
            // 大桌
            Rect(t, 14, 24, 292, 16, RGB(0x5c, 0x42, 0x28));
            Rect(t, 20, 36, 280, 18, RGB(0x6e, 0x50, 0x32)); Frame(t, 20, 36, 280, 18, RGB(0x4e, 0x38, 0x22));
            // 桌面物件:绿灯(央行绿) + 摊开账本 + 怀表 + 委任状(渲图修正:绿光晕先画,
            // 后画的半透明矩形会把绿灯罩覆盖成 alpha 幽灵——同图 8 四稿教训)
            Rect(t, 20, 72, 44, 26, new Color32(0x8a, 0xe2, 0xa8, 36));                                  // 绿光晕(先画)
            Rect(t, 40, 54, 4, 30, RGB(0x2e, 0x24, 0x1c)); Rect(t, 30, 82, 24, 7, RGB(0x2e, 0x24, 0x1c));
            Rect(t, 26, 84, 32, 14, RGB(0x3e, 0x8a, 0x5a)); Rect(t, 30, 96, 24, 4, RGB(0x8a, 0xe2, 0xa8));
            Rect(t, 92, 50, 64, 22, RGB(0xe8, 0xe0, 0xcc)); HLine(t, 124, 50, 155, RGB(0xb2, 0xa8, 0x90));
            for (int k = 0; k < 3; k++) { HLine(t, 54 + k * 4, 96, 120, RGB(0xc2, 0xba, 0xa4)); HLine(t, 58 + k * 4, 128, 152, RGB(0xc2, 0xba, 0xa4)); }
            Rect(t, 168, 48, 20, 20, RGB(0xd8, 0xb2, 0x4a)); Frame(t, 168, 48, 20, 20, RGB(0xa8, 0x84, 0x2c)); // 怀表
            Rect(t, 172, 52, 12, 12, RGB(0xf6, 0xe8, 0xc0)); VLine(t, 178, 54, 62, RGB(0x8a, 0x6e, 0x2c)); P(t, 178, 58, RGB(0x8a, 0x6e, 0x2c));
            Rect(t, 178, 68, 2, 8, RGB(0xa8, 0x84, 0x2c));                                               // 表链
            Rect(t, 208, 48, 60, 36, RGB(0xf4, 0xee, 0xda)); Frame(t, 208, 48, 60, 36, RGB(0xb2, 0xa4, 0x8a));
            for (int k = 0; k < 4; k++) HLine(t, 56 + k * 6, 214, 262, RGB(0xb2, 0xa8, 0x94));
            Rect(t, 244, 50, 14, 10, RGB(0xb2, 0x3a, 0x3a));                                             // 红章
            for (int y = 0; y < 14; y++) HLine(t, y, 0, 319, new Color32(0x0c, 0x0a, 0x08, (byte)(190 - y * 12))); // 前景暗角
        }

        // 11 一纸房契(七→八篇):黄昏家门口——门垫、钥匙与房契,远处港口塔吊晚霞
        static void PaintCutsceneHomecoming(Texture2D t)
        {
            CutSky(t, new[] { RGB(0xff, 0xb4, 0x78), RGB(0xe8, 0x84, 0x6a), RGB(0xa4, 0x6a, 0x84) });    // 晚霞
            Rect(t, 118, 150, 12, 12, RGB(0xff, 0xf0, 0xc0)); Rect(t, 112, 144, 24, 24, new Color32(0xff, 0xd8, 0x9a, 90)); // 沉日，避开左上角标题
            // 渲图修正:霞带 4→2 条(等距横条像百叶窗,减两条让天空透气)
            Rect(t, 50, 138, 46, 5, RGB(0xf6, 0xc0, 0x94)); Rect(t, 178, 133, 52, 5, RGB(0xf6, 0xc0, 0x94));
            // 远处右:港口塔吊与桅杆剪影
            Rect(t, 236, 108, 6, 40, RGB(0x6a, 0x4c, 0x50)); Rect(t, 224, 112, 34, 4, RGB(0x6a, 0x4c, 0x50));
            Rect(t, 220, 116, 4, 8, RGB(0x6a, 0x4c, 0x50));
            Rect(t, 262, 114, 4, 34, RGB(0x5e, 0x44, 0x4a)); Rect(t, 258, 130, 12, 3, RGB(0x5e, 0x44, 0x4a));
            HLine(t, 108, 200, 319, RGB(0x8a, 0x5c, 0x60));                                              // 远岸线
            Rect(t, 0, 96, 320, 14, RGB(0x9a, 0x66, 0x5c));                                              // 远景屋影
            Rect(t, 0, 0, 320, 96, RGB(0xb2, 0x94, 0x7a)); HLine(t, 92, 0, 319, RGB(0x96, 0x7a, 0x62));  // 街面
            // 主体:家门口(左)
            Rect(t, 34, 40, 130, 100, RGB(0xe0, 0xc4, 0x9c)); Frame(t, 34, 40, 130, 100, RGB(0x74, 0x52, 0x34));
            Rect(t, 40, 136, 118, 6, RGB(0x8a, 0x5a, 0x3c)); for (int x = 40; x <= 158; x += 9) VLine(t, x, 136, 140, RGB(0x6a, 0x44, 0x28));
            Rect(t, 78, 44, 42, 62, RGB(0x46, 0x30, 0x22)); Rect(t, 80, 46, 38, 58, RGB(0xff, 0xc8, 0x82)); // 门漏暖光
            Rect(t, 96, 44, 22, 62, RGB(0x8a, 0x5a, 0x38)); VLine(t, 106, 46, 102, RGB(0x6a, 0x42, 0x26));
            P(t, 100, 76, RGB(0xf2, 0xd4, 0x5a));
            Rect(t, 130, 84, 26, 26, RGB(0x5a, 0x42, 0x2c)); Rect(t, 133, 87, 20, 20, RGB(0xff, 0xd0, 0x88)); // 亮窗
            Rect(t, 50, 84, 22, 26, RGB(0x5a, 0x42, 0x2c)); Rect(t, 53, 87, 16, 20, RGB(0xe8, 0x9a, 0x6a));   // 暖帘窗
            Rect(t, 120, 118, 24, 10, RGB(0x74, 0x52, 0x34)); Rect(t, 123, 120, 18, 6, RGB(0xf6, 0xe8, 0xc8)); // 门牌
            // 门前:门垫 + 房契 + 钥匙(渲图修正:钥匙重画为可见尺寸,落在房契左下地面上)
            Rect(t, 70, 30, 54, 9, RGB(0x9a, 0x6a, 0x4a)); HLine(t, 33, 72, 122, RGB(0x7a, 0x52, 0x38));
            Rect(t, 180, 48, 62, 40, RGB(0xf6, 0xf0, 0xdc)); Frame(t, 180, 48, 62, 40, RGB(0xb2, 0xa4, 0x8a));
            for (int k = 0; k < 4; k++) HLine(t, 56 + k * 7, 186, 236, RGB(0xc2, 0xba, 0xa4));
            Rect(t, 218, 50, 14, 10, RGB(0xb2, 0x3a, 0x3a));                                             // 房契红章
            Rect(t, 164, 66, 20, 4, RGB(0xd8, 0xb2, 0x4a)); Rect(t, 166, 64, 5, 3, RGB(0xd8, 0xb2, 0x4a));
            Rect(t, 180, 70, 3, 5, RGB(0xd8, 0xb2, 0x4a)); Rect(t, 184, 70, 3, 3, RGB(0xd8, 0xb2, 0x4a));
            Rect(t, 165, 67, 18, 2, RGB(0xf6, 0xe0, 0x9a));                                              // 金钥匙高光
        }

        // ————— V0.7.4 过场动效小精灵(§149:每段 1–2 处内动效,代码驱动) —————

        /// <summary>开业彩旗串(摇摆)。</summary>
        public static Sprite CutscenePennant()
        {
            Sprite cached;
            if (TryGetCached("cut_overlay_pennant", out cached)) return cached;
            Texture2D t = NewTex(34, 10);
            for (int x = 0; x < 34; x++) P(t, x, 9, RGB(0x8a, 0x6a, 0x4a));                            // 旗绳
            for (int k = 0; k < 5; k++)
            {
                int bx = 2 + k * 7;
                Color c = k % 2 == 0 ? RGB(0xe2, 0x5a, 0x4a) : RGB(0xf2, 0xd4, 0x5a);
                for (int y = 0; y < 6; y++)
                    for (int x = bx + y / 2; x < bx + 6 - y / 2; x++) P(t, x, 8 - y, c);
            }
            Apply(t);
            return Finish(t, "cut_overlay_pennant");
        }

        /// <summary>四角星光(开业/收官闪烁)。</summary>
        public static Sprite CutsceneSparkle()
        {
            Sprite cached;
            if (TryGetCached("cut_overlay_sparkle", out cached)) return cached;
            Texture2D t = NewTex(7, 7);
            Color c = new Color32(0xff, 0xf4, 0xc8, 235);
            VLine(t, 3, 0, 6, c); HLine(t, 3, 0, 6, c);
            P(t, 2, 2, c); P(t, 4, 2, c); P(t, 2, 4, c); P(t, 4, 4, c);
            Apply(t);
            return Finish(t, "cut_overlay_sparkle");
        }

        /// <summary>降价价签(价格战闪烁)。</summary>
        public static Sprite CutscenePriceTag()
        {
            Sprite cached;
            if (TryGetCached("cut_overlay_price_tag", out cached)) return cached;
            Texture2D t = NewTex(18, 12);
            Rect(t, 0, 0, 18, 12, RGB(0xe8, 0xe2, 0xd2)); Frame(t, 0, 0, 18, 12, RGB(0xb2, 0x3a, 0x3a));
            Rect(t, 7, 1, 3, 2, RGB(0xc2, 0x3a, 0x3a)); P(t, 8, 4, RGB(0xc2, 0x3a, 0x3a));
            P(t, 6, 5, RGB(0xc2, 0x3a, 0x3a)); P(t, 7, 5, RGB(0xc2, 0x3a, 0x3a)); P(t, 8, 5, RGB(0xc2, 0x3a, 0x3a)); P(t, 9, 5, RGB(0xc2, 0x3a, 0x3a));
            P(t, 5, 6, RGB(0xc2, 0x3a, 0x3a)); P(t, 10, 6, RGB(0xc2, 0x3a, 0x3a));                     // 下箭头
            HLine(t, 4, 2, 4, RGB(0x6a, 0x64, 0x58)); HLine(t, 4, 13, 15, RGB(0x6a, 0x64, 0x58));      // ¥ 意象
            Apply(t);
            return Finish(t, "cut_overlay_price_tag");
        }

        /// <summary>雨丝(乌云压城,两档速度下落)。</summary>
        public static Sprite CutsceneRaindrop()
        {
            Sprite cached;
            if (TryGetCached("cut_overlay_rain", out cached)) return cached;
            Texture2D t = NewTex(1, 7);
            for (int y = 0; y < 7; y++) P(t, 0, y, new Color32(0xae, 0xbe, 0xd4, (byte)(y < 5 ? 190 : 120)));
            Apply(t);
            return Finish(t, "cut_overlay_rain");
        }

        /// <summary>漂移乌云团(压城横移)。</summary>
        public static Sprite CutsceneCloudPatch()
        {
            Sprite cached;
            if (TryGetCached("cut_overlay_cloud", out cached)) return cached;
            Texture2D t = NewTex(44, 14);
            for (int x = 0; x < 44; x++)
            {
                float h = Mathf.Sin(x / 44f * Mathf.PI) * 12;
                for (int y = 0; y < h; y++)
                    P(t, x, 13 - y, x % 4 == 0 ? RGB(0x2a, 0x30, 0x3c) : RGB(0x34, 0x3c, 0x48));
            }
            Apply(t);
            return Finish(t, "cut_overlay_cloud");
        }

        /// <summary>
        /// 小镇俯视视角使用的云影。它不是天空中的完整云朵轮廓，而是低对比、斑驳且带抖动边缘的
        /// 地面明暗变化；与过场用的深色乌云分开，避免像一张黑色贴纸压在建筑上。
        /// </summary>
        public static Sprite WeatherCloudShadow(int variant)
        {
            variant = Mathf.Abs(variant) % 3;
            string key = "weather_cloud_shadow_" + variant;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;

            const int width = 72, height = 32, lobeCount = 6;
            Texture2D t = NewTex(width, height);
            System.Random random = new System.Random(9137 + variant * 271);
            float[] cx = new float[lobeCount];
            float[] cy = new float[lobeCount];
            float[] rx = new float[lobeCount];
            float[] ry = new float[lobeCount];
            for (int i = 0; i < lobeCount; i++)
            {
                cx[i] = 8f + i * 11f + random.Next(-3, 4);
                cy[i] = 14f + random.Next(-5, 6);
                rx[i] = 11f + random.Next(0, 7);
                ry[i] = 7f + random.Next(0, 6);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float field = -1f;
                    for (int i = 0; i < lobeCount; i++)
                    {
                        float dx = (x - cx[i]) / rx[i];
                        float dy = (y - cy[i]) / ry[i];
                        field = Mathf.Max(field, 1f - dx * dx - dy * dy);
                    }

                    int hash = unchecked(x * 73856093 ^ y * 19349663 ^ (variant + 1) * 83492791);
                    float grain = ((hash & 255) / 255f - 0.5f) * 0.18f;
                    float coverage = field + grain;
                    if (coverage <= 0f) continue;

                    float body = Mathf.Clamp01(coverage / 0.58f);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(10f, 42f, body * body));
                    if (body < 0.42f && ((hash >> 8) & 3) == 0) alpha = (byte)(alpha / 2);
                    if (((hash >> 11) & 15) == 0) alpha = (byte)(alpha * 0.65f); // 内部稀疏透光
                    P(t, x, y, new Color32(0x30, 0x46, 0x4b, alpha));
                }
            }

            Apply(t);
            return Finish(t, key);
        }

        /// <summary>警示灯(贸易战闸口闪烁)。</summary>
        public static Sprite CutsceneWarningLamp()
        {
            Sprite cached;
            if (TryGetCached("cut_overlay_lamp", out cached)) return cached;
            Texture2D t = NewTex(9, 9);
            Rect(t, 1, 1, 7, 7, RGB(0xe2, 0x5a, 0x4a)); Rect(t, 2, 2, 5, 5, RGB(0xff, 0xaa, 0x8a));
            P(t, 3, 3, RGB(0xff, 0xe2, 0xd2)); P(t, 4, 4, RGB(0xff, 0xe2, 0xd2));
            Rect(t, 3, 0, 3, 1, RGB(0x5a, 0x62, 0x6e)); Rect(t, 3, 8, 3, 1, RGB(0x5a, 0x62, 0x6e));
            Apply(t);
            return Finish(t, "cut_overlay_lamp");
        }

        /// <summary>灯塔光带(收官呼吸)。</summary>
        public static Sprite CutsceneBeam()
        {
            Sprite cached;
            if (TryGetCached("cut_overlay_beam", out cached)) return cached;
            Texture2D t = NewTex(60, 14);
            for (int x = 0; x < 60; x++)
            {
                byte a = (byte)(150 * (1f - x / 60f));
                for (int y = 0; y < 14; y++)
                    if (y > 5 - x / 12 && y < 9 + x / 12)
                        P(t, x, y, new Color32(0xff, 0xe2, 0xa0, a));
            }
            Apply(t);
            return Finish(t, "cut_overlay_beam");
        }

        // ————— UI 九宫格皮肤 —————

        static Sprite SlicedRound(string key, int size, int radius, Color fill, Color border)
        {
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;
            Texture2D t = NewTex(size, size);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    int dx = Mathf.Min(x, size - 1 - x);
                    int dy = Mathf.Min(y, size - 1 - y);
                    int d = Mathf.Min(dx, dy);
                    bool insideCorner = (dx < radius && dy < radius) &&
                        ((radius - dx) * (radius - dx) + (radius - dy) * (radius - dy) > radius * radius);
                    if (insideCorner) continue;   // 圆角外透明
                    Color c = d < 2 ? border : fill;
                    if (d == 2 && radius >= 4) c = Color.Lerp(border, fill, 0.35f);
                    P(t, x, y, c);
                }
            Apply(t);
            Sprite s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f, 0,
                SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            s.name = key;
            cache[key] = s;
            return s;
        }

        public static Sprite PanelSkin() { return SlicedRound("ui_panel", 24, 7, new Color32(0x50, 0x3b, 0x2d, 248), new Color32(0x8e, 0x68, 0x45, 255)); }
        public static Sprite PanelDarkSkin() { return SlicedRound("ui_panel_dark", 24, 7, new Color32(0x2f, 0x27, 0x22, 246), new Color32(0x62, 0x4b, 0x38, 255)); }
        public static Sprite ButtonSkin() { return SlicedRound("ui_button", 24, 7, new Color32(0xf4, 0xdf, 0xb9, 255), new Color32(0xb8, 0x78, 0x3f, 255)); }
        public static Sprite BarSkin() { return SlicedRound("ui_bar", 16, 5, new Color32(0xf0, 0xa3, 0x3e, 255), new Color32(0xff, 0xcf, 0x78, 255)); }
    }
}
