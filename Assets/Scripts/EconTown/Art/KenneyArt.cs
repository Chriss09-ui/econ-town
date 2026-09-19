using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// Kenney「Roguelike Modern City」(CC0)地块素材加载器。
    /// 素材位于 StreamingAssets/Kenney/,运行时按需加载。
    /// 注意:建筑与角色拼贴已改走 PixelArt(见 MapBuilder/PlayerController),
    /// 这里只负责 16×16 地块贴图(地图装饰道具使用)。
    /// </summary>
    public static class KenneyArt
    {
        public const int PPU = 16;
        static string BasePath { get { return Path.Combine(Application.streamingAssetsPath, "Kenney"); } }

        static readonly Dictionary<int, Texture2D> texCache = new Dictionary<int, Texture2D>();
        static readonly Dictionary<int, Sprite> spriteCache = new Dictionary<int, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            // 禁用 Domain Reload 时静态缓存跨 Play 存活:每次进 Play 重新加载纹理,旧的原生
            // Texture2D/Sprite 永不释放,逐次累积泄漏(P2-8,样板见 PixelArt.ResetCache)。
            foreach (Texture2D t in texCache.Values) if (t != null) DestroyObject(t);
            foreach (Sprite s in spriteCache.Values) if (s != null) DestroyObject(s);
            texCache.Clear();
            spriteCache.Clear();
        }

        static void DestroyObject(UnityEngine.Object o)
        {
            // EditMode(如测试反射调用)下 Destroy 不会执行,须用 DestroyImmediate。
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        public static Texture2D TileTex(int id)
        {
            Texture2D t;
            if (texCache.TryGetValue(id, out t))
            {
                if (t != null) return t;
                texCache.Remove(id);
            }
            string p = Path.Combine(BasePath, "tile_" + id.ToString("0000") + ".png");
            if (!File.Exists(p))
            {
                Debug.LogWarning("Kenney tile missing: " + id);
                return null;
            }
            byte[] b = File.ReadAllBytes(p);
            t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.LoadImage(b);
            texCache[id] = t;
            return t;
        }

        public static Sprite TileSprite(int id)
        {
            Sprite s;
            if (spriteCache.TryGetValue(id, out s))
            {
                if (s != null) return s;
                spriteCache.Remove(id);
            }
            Texture2D t = TileTex(id);
            if (t == null) return null;
            s = Sprite.Create(t, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), PPU);
            spriteCache[id] = s;
            return s;
        }
    }
}
