using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>小地图用的世界坐标换算，和主线下一步对应的地标。</summary>
    public static class TownMinimap
    {
        public const int PixelsPerUnit = 3;

        public static Vector2 Normalize(Vector3 world, float minX, float maxX, float minY, float maxY)
        {
            return new Vector2(
                Mathf.InverseLerp(minX, maxX, world.x),
                Mathf.InverseLerp(minY, maxY, world.y));
        }

        public static string ObjectiveKind(MainlineDestination destination)
        {
            switch (destination)
            {
                case MainlineDestination.Shop: return "shop";
                case MainlineDestination.Market:
                case MainlineDestination.MarketExperiments: return "market";
                case MainlineDestination.Competition:
                case MainlineDestination.PriceWar: return "competitor";
                case MainlineDestination.Resident:
                case MainlineDestination.Housing:
                case MainlineDestination.Assets: return "home";
                case MainlineDestination.Government: return "hall";
                case MainlineDestination.Finance: return "bank";
                case MainlineDestination.CentralBank: return "centralbank";
                case MainlineDestination.World:
                case MainlineDestination.Port:
                case MainlineDestination.Comparative:
                case MainlineDestination.Trilemma: return "port";
                default: return null;
            }
        }

        public static string PlaceName(string kind)
        {
            switch (kind)
            {
                case "shop": return "果汁店";
                case "home": return "家";
                case "hall": return "市政厅";
                case "bank": return "银行";
                case "centralbank": return "央行";
                case "port": return "港口";
                case "market": return "原料市场";
                case "competitor": return "Sunny Tea";
                case "factory": return "工厂";
                case "restaurant": return "餐厅";
                case "cinema": return "影院";
                case "convenience": return "便利屋";
                case "training": return "培训";
                case "school": return "学校";
                default: return null;
            }
        }
    }

    /// <summary>小镇地图是一张平整的图版：草地、两条街、固定大小的建筑章。</summary>
    static class TownMinimapAtlas
    {
        const int P = TownMinimap.PixelsPerUnit;
        const int AtlasRevision = 8;
        const int LargeScale = 4;
        const int IconN = 16;

        static Sprite townSprite;
        static Sprite townLargeSprite;
        static Sprite portSprite;
        static Sprite roomSprite;
        static int townSeason = -2;
        static int townLargeSeason = -2;
        static int townBake;
        static int townLargeBake;
        static string roomKind;
        static int paintScale = P;
        static Dictionary<string, Color32[]> iconPixels;
        static Dictionary<string, Sprite> iconSprites;

        public static Sprite Town(int season)
        {
            int expectW = Mathf.RoundToInt((MapBuilder.WorldMaxX - MapBuilder.WorldMinX) * P);
            if (townSprite != null && townSprite.texture != null && townSprite.texture.width == expectW
                && townSeason == season && townBake == AtlasRevision)
                return townSprite;
            townSeason = season;
            townBake = AtlasRevision;
            Replace(ref townSprite, PaintTown(season, P));
            return townSprite;
        }

        public static Sprite TownLarge(int season)
        {
            int expectW = Mathf.RoundToInt((MapBuilder.WorldMaxX - MapBuilder.WorldMinX) * LargeScale);
            if (townLargeSprite != null && townLargeSprite.texture != null &&
                townLargeSprite.texture.width == expectW && townLargeSeason == season && townLargeBake == AtlasRevision)
                return townLargeSprite;
            townLargeSeason = season;
            townLargeBake = AtlasRevision;
            Replace(ref townLargeSprite, PaintTown(season, LargeScale));
            return townLargeSprite;
        }

        public static Sprite IconSprite(string kind)
        {
            if (iconSprites == null) iconSprites = new Dictionary<string, Sprite>();
            Sprite sprite;
            if (iconSprites.TryGetValue(kind, out sprite) && sprite != null) return sprite;
            Color32[] pixels = IconPixels(kind);
            Texture2D texture = Finish("mapIcon_" + kind, IconN, IconN, pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, IconN, IconN), new Vector2(0.5f, 0.5f), IconN);
            sprite.name = texture.name;
            iconSprites[kind] = sprite;
            return sprite;
        }

        public static Sprite Port()
        {
            int expectW = Mathf.RoundToInt((MapBuilder.PortWorldMaxX - MapBuilder.PortWorldMinX) * P);
            if (portSprite != null && portSprite.texture != null && portSprite.texture.width == expectW) return portSprite;
            Replace(ref portSprite, PaintPort());
            return portSprite;
        }

        public static Sprite Room(string kind)
        {
            if (roomSprite != null && roomKind == kind) return roomSprite;
            roomKind = kind;
            InteriorSpec spec = InteriorCatalog.Get(kind);
            Replace(ref roomSprite, PaintRoom(spec));
            return roomSprite;
        }

        static void Replace(ref Sprite sprite, Texture2D texture)
        {
            if (sprite != null)
            {
                Texture old = sprite.texture;
                if (Application.isPlaying) Object.Destroy(sprite);
                else Object.DestroyImmediate(sprite);
                if (old != null)
                {
                    if (Application.isPlaying) Object.Destroy(old);
                    else Object.DestroyImmediate(old);
                }
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
        }

        static Texture2D PaintTown(int season, int scale)
        {
            paintScale = scale;
            float minX = MapBuilder.WorldMinX, minY = MapBuilder.WorldMinY;
            int texW = Mathf.RoundToInt((MapBuilder.WorldMaxX - minX) * scale);
            int texH = Mathf.RoundToInt((MapBuilder.WorldMaxY - minY) * scale);
            Color32[] pixels = new Color32[texW * texH];
            Color32 grass, grassDark;
            GrassPalette(season, out grass, out grassDark, out _);
            Color32 ink = C(0x2a, 0x22, 0x1c);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = grass;

            for (int cellY = -MapBuilder.H / 2; cellY < MapBuilder.H / 2; cellY++)
            {
                for (int cellX = MapBuilder.OriginX; cellX < MapBuilder.OriginX + MapBuilder.W; cellX++)
                {
                    string parcel = ParcelId(cellX + 0.5f, cellY + 0.5f);
                    Color32 fill = parcel == "park" ? grassDark : grass;
                    StampCell(pixels, texW, texH, minX, minY, cellX, cellY, fill, fill);
                }
            }

            PaintCopiedRoads(pixels, texW, texH, minX, minY);

            for (int i = 0; i < TownLayoutData.BuildingSlots.Length; i++)
            {
                TownBuildingSlot slot = TownLayoutData.BuildingSlots[i];
                if (!Pictured(slot.kind)) continue;
                Vector2 anchor = IconAnchor(slot);
                int cx = Mathf.RoundToInt((anchor.x - minX) * scale);
                int cy = Mathf.RoundToInt((anchor.y - minY) * scale);
                StampIcon(pixels, texW, texH, cx, cy, slot.kind);
            }

            for (int y = 0; y < texH; y++)
                for (int x = 0; x < texW; x++)
                    if (x == 0 || y == 0 || x == texW - 1 || y == texH - 1)
                        pixels[y * texW + x] = ink;
            return Finish("townMinimapTex", texW, texH, pixels);
        }

        static Texture2D PaintPort()
        {
            float minX = MapBuilder.PortWorldMinX, minY = MapBuilder.PortWorldMinY;
            int texW = Mathf.RoundToInt((MapBuilder.PortWorldMaxX - minX) * P);
            int texH = Mathf.RoundToInt((MapBuilder.PortWorldMaxY - minY) * P);
            Color32[] pixels = new Color32[texW * texH];
            Color32 yard = C(0x9e, 0x9a, 0x86);
            Color32 yardAlt = C(0xb5, 0xb0, 0x98);
            Color32 shore = C(0xc8, 0xb4, 0x84);
            Color32 water = C(0x35, 0x72, 0x86);
            Color32 waterLit = C(0x6a, 0xa8, 0xb0);
            Color32 road = C(0xcf, 0xb7, 0x89);
            Color32 walk = C(0xd9, 0xbe, 0x8a);
            int y0 = -MapBuilder.PortH / 2;
            for (int cellY = y0; cellY < y0 + MapBuilder.PortH; cellY++)
            {
                for (int cellX = MapBuilder.PortOriginX; cellX < MapBuilder.PortOriginX + MapBuilder.PortW; cellX++)
                {
                    Color32 fill;
                    if (cellX >= MapBuilder.PortSeaMinX)
                        fill = ((cellX + cellY) % 3) == 0 ? waterLit : water;
                    else if (cellX == MapBuilder.PortShoreX) fill = shore;
                    else fill = ((cellX + cellY) & 1) == 0 ? yard : yardAlt;
                    if (cellX <= MapBuilder.PortShoreX - 1 && (cellY == -8 || cellY == -7)) fill = road;
                    else if (cellX <= MapBuilder.PortShoreX - 1 && cellY == -6) fill = walk;
                    StampCell(pixels, texW, texH, minX, minY, cellX, cellY, fill, fill);
                }
            }
            Blit(pixels, texW, texH, minX, minY, P, -11.6f, -4.6f, -8.4f, -1.6f, C(0x2f, 0x6e, 0x86), true);
            FrameEdge(pixels, texW, texH, C(0x5c, 0x4e, 0x40), C(0x2a, 0x4a, 0x58));
            return Finish("portMinimapTex", texW, texH, pixels);
        }

        static Texture2D PaintRoom(InteriorSpec spec)
        {
            Vector4 bounds = spec.RoomBounds;
            float minX = bounds.x, minY = bounds.y, maxX = bounds.z, maxY = bounds.w;
            const int scale = 8;
            int texW = Mathf.Max(8, Mathf.RoundToInt((maxX - minX) * scale));
            int texH = Mathf.Max(8, Mathf.RoundToInt((maxY - minY) * scale));
            Color32[] pixels = new Color32[texW * texH];
            Color32 floor = C(0xc4, 0xa0, 0x72);
            Color32 floorAlt = C(0xb5, 0x90, 0x62);
            Color32 wall = C(0x6a, 0x58, 0x48);
            for (int y = 0; y < texH; y++)
                for (int x = 0; x < texW; x++)
                    pixels[y * texW + x] = ((x / 4 + y / 4) & 1) == 0 ? floor : floorAlt;
            FrameEdge(pixels, texW, texH, wall, wall);
            Blit(pixels, texW, texH, minX, minY, scale,
                spec.CounterWorld.x - 0.7f, spec.CounterWorld.y - 0.35f,
                spec.CounterWorld.x + 0.7f, spec.CounterWorld.y + 0.35f, C(0x8a, 0x5a, 0x3c), true);
            Blit(pixels, texW, texH, minX, minY, scale,
                spec.ExitWorld.x - 0.35f, spec.ExitWorld.y - 0.35f,
                spec.ExitWorld.x + 0.35f, spec.ExitWorld.y + 0.35f, C(0xf0, 0xe2, 0xc4), false);
            return Finish("roomMinimapTex", texW, texH, pixels);
        }

        static void GrassPalette(int season, out Color32 grass, out Color32 dark, out Color32 light)
        {
            switch (season)
            {
                case 1: grass = C(0x58, 0xa0, 0x4a); dark = C(0x46, 0x82, 0x3a); light = C(0x84, 0xc2, 0x66); break;
                case 2: grass = C(0xc4, 0xa8, 0x52); dark = C(0x9a, 0x83, 0x3e); light = C(0xdc, 0xc8, 0x78); break;
                case 3: grass = C(0xad, 0xb8, 0xa6); dark = C(0x8d, 0x9a, 0x8a); light = C(0xd4, 0xdc, 0xcf); break;
                default: grass = C(0x74, 0xae, 0x5a); dark = C(0x5a, 0x94, 0x46); light = C(0x8f, 0xc4, 0x72); break;
            }
        }

        static void PaintCopiedRoads(Color32[] pixels, int texW, int texH, float minX, float minY)
        {
            Color32 road = C(0xcf, 0xb7, 0x89);
            Color32 walk = C(0xd9, 0xbe, 0x8a);
            Color32 court = C(0xce, 0xc8, 0xb6);
            PaintHorizontalStreet(pixels, texW, texH, minX, minY, TownLayoutData.MainStreetY, road, walk);
            PaintHorizontalStreet(pixels, texW, texH, minX, minY, TownLayoutData.TradeStreetY, road, walk);
            int left = TownLayoutData.CivicAxisLeftX;
            for (int y = TownLayoutData.CivicAxisMinY; y <= TownLayoutData.CivicAxisMaxY; y++)
            {
                StampCell(pixels, texW, texH, minX, minY, left, y, road, road);
                StampCell(pixels, texW, texH, minX, minY, left + 1, y, road, road);
                if (y >= -1 && y <= 1) continue;
                StampCell(pixels, texW, texH, minX, minY, left - 1, y, walk, walk);
                StampCell(pixels, texW, texH, minX, minY, left + 2, y, walk, walk);
            }

            for (int i = 0; i < TownLayoutData.PedestrianPaths.Length; i++)
            {
                TownPathRect path = TownLayoutData.PedestrianPaths[i];
                StampRect(pixels, texW, texH, minX, minY, path.x0, path.y0, path.x1, path.y1, walk);
            }

            // 前庭石板跟 MapBuilder.PaintParcelGround 同一批格子：门口前廊、院路、装卸坪。
            int[] courts =
            {
                -5, 8, 4, 11, 22, 8, 30, 11,
                9, 11, 18, 11, 11, 9, 12, 10, 15, 9, 16, 10,
                -31, 8, -27, 10, -17, 9, -15, 12, -8, -20, -3, -19, -4, -19, -3, -14,
                -35, 2, -12, 3, 8, 2, 24, 3, 29, 2, 35, 3, -35, -10, -8, -9,
                -21, -8, -14, -6, 5, -10, 15, -3, 18, -10, 22, -9, 24, -10, 35, -5
            };
            for (int i = 0; i < courts.Length; i += 4)
                StampRect(pixels, texW, texH, minX, minY, courts[i], courts[i + 1], courts[i + 2], courts[i + 3], court);

            for (int x = MapBuilder.OriginX; x <= MapBuilder.TownMaxX; x++)
            {
                StampCell(pixels, texW, texH, minX, minY, x, TownLayoutData.MainStreetY, road, road);
                StampCell(pixels, texW, texH, minX, minY, x, TownLayoutData.MainStreetY - 1, road, road);
                StampCell(pixels, texW, texH, minX, minY, x, TownLayoutData.TradeStreetY, road, road);
                StampCell(pixels, texW, texH, minX, minY, x, TownLayoutData.TradeStreetY - 1, road, road);
            }
            for (int y = TownLayoutData.CivicAxisMinY; y <= TownLayoutData.CivicAxisMaxY; y++)
            {
                StampCell(pixels, texW, texH, minX, minY, left, y, road, road);
                StampCell(pixels, texW, texH, minX, minY, left + 1, y, road, road);
            }
        }

        static void PaintHorizontalStreet(Color32[] pixels, int texW, int texH, float minX, float minY,
            int upperRoadY, Color32 road, Color32 walk)
        {
            for (int x = MapBuilder.OriginX; x <= MapBuilder.TownMaxX; x++)
            {
                StampCell(pixels, texW, texH, minX, minY, x, upperRoadY, road, road);
                StampCell(pixels, texW, texH, minX, minY, x, upperRoadY - 1, road, road);
                StampCell(pixels, texW, texH, minX, minY, x, upperRoadY + 1, walk, walk);
                StampCell(pixels, texW, texH, minX, minY, x, upperRoadY - 2, walk, walk);
            }
        }

        static void StampRect(Color32[] pixels, int texW, int texH, float minX, float minY,
            int x0, int y0, int x1, int y1, Color32 color)
        {
            int ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1);
            int ay = Mathf.Min(y0, y1), by = Mathf.Max(y0, y1);
            for (int y = ay; y <= by; y++)
                for (int x = ax; x <= bx; x++)
                    StampCell(pixels, texW, texH, minX, minY, x, y, color, color);
        }

        static string ParcelId(float x, float y)
        {
            Vector2 point = new Vector2(x, y);
            for (int i = 0; i < TownLayoutData.Parcels.Length; i++)
                if (TownLayoutData.Parcels[i].bounds.Contains(point)) return TownLayoutData.Parcels[i].id;
            return null;
        }

        static void StampCell(Color32[] pixels, int texW, int texH, float minX, float minY,
            int cellX, int cellY, Color32 fill, Color32 speck)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((cellX - minX) * paintScale), 0, texW);
            int x1 = Mathf.Clamp(Mathf.FloorToInt((cellX + 1 - minX) * paintScale), 0, texW);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((cellY - minY) * paintScale), 0, texH);
            int y1 = Mathf.Clamp(Mathf.FloorToInt((cellY + 1 - minY) * paintScale), 0, texH);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    pixels[y * texW + x] = (x == x0 && y == y0) ? speck : fill;
        }

        static bool Pictured(string kind)
        {
            switch (kind)
            {
                case "shop":
                case "competitor":
                case "market":
                case "factory":
                case "port":
                case "hall":
                case "training":
                case "school":
                case "convenience":
                case "home":
                case "restaurant":
                case "cinema":
                case "bank":
                case "centralbank":
                    return true;
                default:
                    return false;
            }
        }

        static Vector2 IconAnchor(TownBuildingSlot slot)
        {
            float x = slot.center.x;
            float y = slot.center.y;
            switch (slot.kind)
            {
                case "shop":
                case "competitor":
                case "market":
                case "factory":
                case "port":
                    y = -8.05f;
                    break;
                case "convenience":
                case "restaurant":
                case "cinema":
                case "bank":
                    y = 4.15f;
                    break;
                case "training":
                case "home":
                case "hall":
                case "centralbank":
                    y = 14.55f;
                    break;
            }
            return new Vector2(x, y);
        }

        static void StampIcon(Color32[] pixels, int texW, int texH, int cx, int cy, string kind)
        {
            Color32[] icon = IconPixels(kind);
            int x0 = cx - IconN / 2;
            int y0 = cy - IconN / 2;
            for (int y = 0; y < IconN; y++)
                for (int x = 0; x < IconN; x++)
                {
                    Color32 color = icon[y * IconN + x];
                    if (color.a == 0) continue;
                    Px(pixels, texW, texH, x0 + x, y0 + y, color);
                }
        }

        static Color32[] IconPixels(string kind)
        {
            if (iconPixels == null) iconPixels = new Dictionary<string, Color32[]>();
            Color32[] cached;
            if (iconPixels.TryGetValue(kind, out cached)) return cached;
            Color32[] icon = new Color32[IconN * IconN];
            switch (kind)
            {
                case "shop":
                    GabledIcon(icon, C(0xc4, 0x5a, 0x3a), false, C(0xe8, 0x84, 0x3a), C(0xff, 0xf4, 0xd4), true);
                    break;
                case "home":
                    GabledIcon(icon, C(0xd8, 0x6a, 0x55), true, default(Color32), default(Color32), false);
                    break;
                case "competitor":
                    GabledIcon(icon, C(0x2e, 0x7a, 0x5a), false, default(Color32), default(Color32), false);
                    break;
                case "restaurant":
                    GabledIcon(icon, C(0xa3, 0x3b, 0x32), true, C(0xc4, 0x48, 0x38), C(0xf6, 0xe6, 0xc0), true);
                    break;
                case "convenience":
                    GabledIcon(icon, C(0xd0, 0x70, 0x48), false, C(0xf2, 0xc1, 0x4e), C(0x2c, 0x6e, 0x96), true);
                    break;
                case "school":
                    GabledIcon(icon, C(0x8d, 0x4e, 0x3a), false, default(Color32), default(Color32), false);
                    break;
                case "market": MarketIcon(icon); break;
                case "factory": FactoryIcon(icon); break;
                case "port": PortIcon(icon); break;
                case "hall": CivicIcon(icon, C(0xc4, 0x38, 0x30), true); break;
                case "bank": CivicIcon(icon, C(0xe2, 0xb0, 0x4a), false); break;
                case "centralbank": DomeIcon(icon); break;
                case "cinema": CinemaIcon(icon); break;
                case "training": TrainingIcon(icon); break;
                default:
                    GabledIcon(icon, C(0x8a, 0x74, 0x62), false, default(Color32), default(Color32), false);
                    break;
            }
            IconOutline(icon);
            iconPixels[kind] = icon;
            return icon;
        }

        static void GabledIcon(Color32[] icon, Color32 roof, bool chimney, Color32 awnA, Color32 awnB, bool awning)
        {
            Color32 wall = C(0xf7, 0xf1, 0xe4);
            Color32 door = C(0x5e, 0x3a, 0x2a);
            Color32 glass = C(0x8f, 0xc9, 0xd6);
            IconFill(icon, 4, 2, 12, 8, wall);
            IconFill(icon, 7, 2, 9, 5, door);
            if (!awning)
            {
                IconFill(icon, 5, 5, 7, 7, glass);
                IconFill(icon, 9, 5, 11, 7, glass);
            }
            int[] widths = { 10, 8, 6, 4, 2 };
            for (int i = 0; i < widths.Length; i++)
            {
                int span = widths[i];
                int left = 8 - span / 2;
                IconFill(icon, left, 8 + i, left + span, 9 + i, roof);
            }
            if (awning)
            {
                for (int x = 3; x <= 12; x++)
                {
                    Color32 stripe = (x & 1) == 0 ? awnA : awnB;
                    IconPlot(icon, x, 7, stripe);
                    IconPlot(icon, x, 6, stripe);
                }
            }
            if (chimney) IconFill(icon, 10, 10, 12, 14, C(0x7d, 0x40, 0x32));
        }

        static void MarketIcon(Color32[] icon)
        {
            int[] stalls = { 1, 6, 11 };
            Color32[] cloths = { C(0xc4, 0x48, 0x38), C(0xf3, 0xe6, 0xc4), C(0xc4, 0x48, 0x38) };
            Color32 wood = C(0xb0, 0x7a, 0x48);
            Color32 pole = C(0x6a, 0x48, 0x32);
            for (int i = 0; i < stalls.Length; i++)
            {
                int x0 = stalls[i];
                IconFill(icon, x0, 2, x0 + 4, 6, wood);
                IconFill(icon, x0, 6, x0 + 1, 10, pole);
                IconFill(icon, x0 + 3, 6, x0 + 4, 10, pole);
                IconFill(icon, x0, 8, x0 + 4, 10, cloths[i]);
                IconFill(icon, x0 + 1, 10, x0 + 3, 12, cloths[i]);
            }
        }

        static void FactoryIcon(Color32[] icon)
        {
            IconFill(icon, 3, 2, 14, 8, C(0xc8, 0xc0, 0xb2));
            IconFill(icon, 8, 4, 10, 6, C(0x8f, 0xc9, 0xd6));
            IconFill(icon, 11, 4, 13, 6, C(0x8f, 0xc9, 0xd6));
            IconFill(icon, 3, 8, 14, 10, C(0x5a, 0x54, 0x4e));
            IconFill(icon, 4, 8, 7, 14, C(0x3a, 0x34, 0x30));
            IconFill(icon, 7, 13, 10, 15, C(0xe8, 0xe4, 0xdc));
        }

        static void PortIcon(Color32[] icon)
        {
            IconFill(icon, 1, 2, 15, 4, C(0xb0, 0x7a, 0x48));
            IconFill(icon, 1, 3, 10, 7, C(0xe6, 0xde, 0xc8));
            IconFill(icon, 1, 7, 11, 9, C(0x2f, 0x6e, 0x86));
            IconFill(icon, 3, 4, 6, 6, C(0x8f, 0xc9, 0xd6));
            IconFill(icon, 10, 3, 12, 13, C(0x2c, 0x28, 0x24));
            IconFill(icon, 10, 11, 15, 13, C(0x2c, 0x28, 0x24));
            IconFill(icon, 14, 7, 15, 12, C(0x2c, 0x28, 0x24));
        }

        static void CivicIcon(Color32[] icon, Color32 pediment, bool flag)
        {
            Color32 stone = C(0xe3, 0xd9, 0xc4);
            Color32 column = C(0xff, 0xf8, 0xee);
            IconFill(icon, 2, 2, 14, 8, stone);
            IconFill(icon, 3, 3, 5, 8, column);
            IconFill(icon, 7, 3, 9, 8, column);
            IconFill(icon, 11, 3, 13, 8, column);
            IconFill(icon, 6, 2, 10, 4, C(0x3e, 0x34, 0x28));
            int[] widths = { 12, 10, 8, 6, 4, 2 };
            for (int i = 0; i < widths.Length; i++)
            {
                int span = widths[i];
                int left = 8 - span / 2;
                IconFill(icon, left, 8 + i, left + span, 9 + i, pediment);
            }
            if (!flag) return;
            IconFill(icon, 7, 13, 8, 15, column);
            IconFill(icon, 8, 13, 11, 15, C(0xc4, 0x38, 0x30));
        }

        static void DomeIcon(Color32[] icon)
        {
            Color32 stone = C(0xe3, 0xd9, 0xc4);
            Color32 column = C(0xff, 0xf8, 0xee);
            Color32 dome = C(0xd7, 0xb0, 0x5a);
            IconFill(icon, 2, 2, 14, 7, stone);
            IconFill(icon, 6, 2, 10, 5, C(0x3a, 0x48, 0x52));
            IconFill(icon, 4, 3, 5, 7, column);
            IconFill(icon, 11, 3, 12, 7, column);
            for (int y = 6; y <= 14; y++)
                for (int x = 2; x <= 13; x++)
                {
                    float nx = (x + 0.5f - 8f) / 5f;
                    float ny = (y + 0.5f - 9.4f) / 4.3f;
                    if (nx * nx + ny * ny <= 1f) IconPlot(icon, x, y, dome);
                }
            IconPlot(icon, 7, 14, column);
            IconPlot(icon, 8, 14, column);
        }

        static void CinemaIcon(Color32[] icon)
        {
            IconFill(icon, 2, 2, 14, 9, C(0x32, 0x24, 0x44));
            IconFill(icon, 6, 2, 10, 5, C(0x1a, 0x14, 0x22));
            IconFill(icon, 2, 9, 14, 12, C(0x1c, 0x14, 0x28));
            for (int x = 3; x <= 13; x += 2)
                IconFill(icon, x, 9, x + 1, 11, C(0xf6, 0xd2, 0x6a));
        }

        static void TrainingIcon(Color32[] icon)
        {
            Color32 roof = C(0x8d, 0x4e, 0x3a);
            Color32 wall = C(0xf7, 0xf1, 0xe4);
            IconFill(icon, 2, 2, 6, 12, C(0xe3, 0xd9, 0xc4));
            IconFill(icon, 3, 8, 5, 10, C(0xf2, 0xd6, 0x8c));
            IconPlot(icon, 3, 9, C(0x2a, 0x22, 0x1c));
            IconPlot(icon, 4, 8, C(0x2a, 0x22, 0x1c));
            IconFill(icon, 2, 12, 6, 14, roof);
            IconFill(icon, 3, 14, 5, 15, roof);
            IconFill(icon, 6, 2, 14, 8, wall);
            IconFill(icon, 9, 2, 11, 5, C(0x5e, 0x3a, 0x2a));
            IconFill(icon, 7, 5, 8, 7, C(0x8f, 0xc9, 0xd6));
            IconFill(icon, 12, 5, 13, 7, C(0x8f, 0xc9, 0xd6));
            IconFill(icon, 6, 8, 14, 10, roof);
            IconFill(icon, 7, 10, 13, 11, roof);
            IconFill(icon, 9, 11, 11, 12, roof);
        }

        static void IconFill(Color32[] icon, int x0, int y0, int x1, int y1, Color32 color)
        {
            if (x1 < x0) { int t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { int t = y0; y0 = y1; y1 = t; }
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    IconPlot(icon, x, y, color);
        }

        static void IconPlot(Color32[] icon, int x, int y, Color32 color)
        {
            if ((uint)x >= (uint)IconN || (uint)y >= (uint)IconN) return;
            icon[y * IconN + x] = color;
        }

        static void IconOutline(Color32[] icon)
        {
            Color32 ink = C(0x2a, 0x22, 0x1c);
            Color32[] src = (Color32[])icon.Clone();
            for (int y = 0; y < IconN; y++)
                for (int x = 0; x < IconN; x++)
                {
                    if (src[y * IconN + x].a != 0) continue;
                    bool hit = false;
                    if (x > 0 && src[y * IconN + x - 1].a != 0) hit = true;
                    else if (x + 1 < IconN && src[y * IconN + x + 1].a != 0) hit = true;
                    else if (y > 0 && src[(y - 1) * IconN + x].a != 0) hit = true;
                    else if (y + 1 < IconN && src[(y + 1) * IconN + x].a != 0) hit = true;
                    if (hit) icon[y * IconN + x] = ink;
                }
        }

        static void Px(Color32[] pixels, int texW, int texH, int x, int y, Color32 color)
        {
            if ((uint)x >= (uint)texW || (uint)y >= (uint)texH) return;
            pixels[y * texW + x] = color;
        }

        static void Blit(Color32[] pixels, int texW, int texH, float minX, float minY, int scale,
            float x0, float y0, float x1, float y1, Color32 color, bool roof)
        {
            int ix0 = Mathf.Clamp(Mathf.FloorToInt((x0 - minX) * scale), 0, texW);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt((x1 - minX) * scale), 0, texW);
            int iy0 = Mathf.Clamp(Mathf.FloorToInt((y0 - minY) * scale), 0, texH);
            int iy1 = Mathf.Clamp(Mathf.CeilToInt((y1 - minY) * scale), 0, texH);
            if (ix1 <= ix0 || iy1 <= iy0) return;
            Color32 edge = Darken(color, 0.62f);
            Color32 lit = Lighten(color, 1.22f);
            Color32 window = C(0x2a, 0x38, 0x44);
            int midX = (ix0 + ix1) / 2;
            int winY = iy0 + Mathf.Max(1, (iy1 - iy0) / 3);
            for (int y = iy0; y < iy1; y++)
            {
                for (int x = ix0; x < ix1; x++)
                {
                    bool border = x == ix0 || y == iy0 || x == ix1 - 1 || y == iy1 - 1;
                    bool ridge = roof && y == iy1 - 2;
                    bool windowPixel = roof && y == winY && (x == midX - 2 || x == midX + 1) && x > ix0 && x < ix1 - 1;
                    pixels[y * texW + x] = windowPixel ? window : border ? edge : ridge ? lit : color;
                }
            }
        }

        static void FrameEdge(Color32[] pixels, int texW, int texH, Color32 inner, Color32 outer)
        {
            for (int y = 0; y < texH; y++)
                for (int x = 0; x < texW; x++)
                {
                    int inset = Mathf.Min(Mathf.Min(x, texW - 1 - x), Mathf.Min(y, texH - 1 - y));
                    if (inset == 0) pixels[y * texW + x] = outer;
                    else if (inset == 1) pixels[y * texW + x] = inner;
                }
        }

        static Texture2D Finish(string name, int texW, int texH, Color32[] pixels)
        {
            Texture2D texture = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        static Color32 C(byte r, byte g, byte b) { return new Color32(r, g, b, 255); }

        static Color32 Darken(Color32 color, float mul)
        {
            return new Color32((byte)(color.r * mul), (byte)(color.g * mul), (byte)(color.b * mul), 255);
        }

        static Color32 Lighten(Color32 color, float mul)
        {
            return new Color32(
                (byte)Mathf.Min(255, color.r * mul),
                (byte)Mathf.Min(255, color.g * mul),
                (byte)Mathf.Min(255, color.b * mul), 255);
        }
    }

    public partial class UIManager
    {
        const float MinimapWellW = (MapBuilder.WorldMaxX - MapBuilder.WorldMinX) * TownMinimap.PixelsPerUnit;
        const float MinimapWellH = (MapBuilder.WorldMaxY - MapBuilder.WorldMinY) * TownMinimap.PixelsPerUnit;

        public const float ScreenMargin = 8f;
        public static float FrameWidth { get { return MinimapWellW + 16f; } }
        public static float FrameHeight { get { return MinimapWellH + 44f; } }
        public static float DockLeft { get { return ScreenMargin + FrameWidth + 10f; } }
        public const float DockBottom = 8f;
        public const float DockHeight = 58f;
        public const float ColumnGap = 8f;

        RectTransform minimapRoot;
        RectTransform minimapMap;
        Image minimapGround;
        Text minimapTitle;
        Text minimapCaption;
        RectTransform minimapPlayer;
        Image minimapPlayerImage;
        RectTransform minimapObjective;
        Image minimapObjectiveImage;
        string minimapMode;
        GameObject townMapRoot;
        RectTransform townMapPlayer;
        RectTransform townMapObjective;
        Image townMapObjectiveImage;

        void BuildMinimap(Transform canvas)
        {
            Image frame = UiTheme.MakeImage(canvas, UiTheme.Panel, "townMinimap");
            UiTheme.ApplyPixelFrame(frame);
            minimapRoot = frame.rectTransform;
            minimapRoot.anchorMin = minimapRoot.anchorMax = new Vector2(0f, 0f);
            minimapRoot.pivot = new Vector2(0f, 0f);
            minimapRoot.anchoredPosition = new Vector2(ScreenMargin, DockBottom);
            minimapRoot.sizeDelta = new Vector2(FrameWidth, FrameHeight);

            minimapTitle = UiTheme.MakeText(frame.transform, "小镇", 15, UiTheme.TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceTopLeft(minimapTitle.rectTransform, 8f, 3f, MinimapWellW, 16f);

            minimapGround = UiTheme.MakeImage(frame.transform, Color.white, "minimapGround");
            minimapGround.raycastTarget = true;
            minimapMap = minimapGround.rectTransform;
            FitMap(TownMinimapAtlas.Town(0), "town");

            minimapObjectiveImage = UiTheme.MakeImage(minimapMap, Color.white, "minimapObjective");
            minimapObjectiveImage.raycastTarget = false;
            minimapObjectiveImage.sprite = MarkerSprite(true);
            minimapObjective = minimapObjectiveImage.rectTransform;

            minimapPlayerImage = UiTheme.MakeImage(minimapMap, Color.white, "minimapPlayer");
            minimapPlayerImage.raycastTarget = false;
            minimapPlayerImage.sprite = MarkerSprite(false);
            minimapPlayer = minimapPlayerImage.rectTransform;

            minimapCaption = UiTheme.MakeText(frame.transform, "小人是你", 15, UiTheme.TextSub, TextAnchor.MiddleLeft);
            PlaceTopLeft(minimapCaption.rectTransform, 8f, 22f + MinimapWellH, MinimapWellW, 18f);
            minimapCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
            minimapCaption.verticalOverflow = VerticalWrapMode.Truncate;

            minimapGround.raycastTarget = false;
            minimapTitle.raycastTarget = false;
            minimapCaption.raycastTarget = false;
            Button openMap = frame.gameObject.AddComponent<Button>();
            openMap.targetGraphic = frame;
            openMap.transition = Selectable.Transition.None;
            openMap.onClick.AddListener(ShowTownMap);
        }

        void FitMap(Sprite sprite, string mode)
        {
            if (minimapGround == null || sprite == null) return;
            minimapMode = mode;
            minimapGround.sprite = sprite;
            minimapGround.color = Color.white;
            minimapGround.type = Image.Type.Simple;
            minimapGround.preserveAspect = false;
            float x = 8f + (MinimapWellW - sprite.rect.width) * 0.5f;
            float y = 20f + (MinimapWellH - sprite.rect.height) * 0.5f;
            PlaceTopLeft(minimapMap, x, y, sprite.rect.width, sprite.rect.height);
        }

        public void RefreshMinimap()
        {
            if (minimapRoot == null || minimapMap == null) return;
            bool interior = ShopInteriorController.IsInsideShopInterior;
            bool port = !interior && PortSceneController.IsInsidePort;
            float minX, maxX, minY, maxY;
            string mode;
            if (interior)
            {
                minX = MapBuilder.InteriorMinX;
                maxX = MapBuilder.InteriorMaxX;
                minY = MapBuilder.InteriorMinY;
                maxY = MapBuilder.InteriorMaxY;
                mode = "room:" + MapBuilder.ActiveInteriorKind;
                if (minimapMode != mode)
                    FitMap(TownMinimapAtlas.Room(MapBuilder.ActiveInteriorKind), mode);
            }
            else if (port)
            {
                minX = MapBuilder.PortWorldMinX;
                maxX = MapBuilder.PortWorldMaxX;
                minY = MapBuilder.PortWorldMinY;
                maxY = MapBuilder.PortWorldMaxY;
                mode = "port";
                if (minimapMode != mode) FitMap(TownMinimapAtlas.Port(), mode);
            }
            else
            {
                minX = MapBuilder.WorldMinX;
                maxX = MapBuilder.WorldMaxX;
                minY = MapBuilder.WorldMinY;
                maxY = MapBuilder.WorldMaxY;
                int season = GameState.I != null ? AtmosphereService.SeasonIndex(GameState.I.day) : 0;
                mode = "town:" + season;
                if (minimapMode != mode) FitMap(TownMinimapAtlas.Town(season), mode);
            }

            string objective = null;
            if (GameState.I != null && !interior && !port)
            {
                MainlineGuide.Cue cue = MainlineGuide.Build(GameState.I);
                objective = TownMinimap.ObjectiveKind(cue.destination);
            }

            if (minimapObjective != null)
            {
                bool showRing = !interior && !port && objective != null;
                minimapObjective.gameObject.SetActive(showRing);
                if (showRing)
                {
                    PlaceOnMap(minimapObjective, PinWorld(objective), minX, maxX, minY, maxY, 18f);
                    float pulse = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                    minimapObjectiveImage.color = new Color(1f, 0.96f, 0.82f, pulse);
                }
            }

            if (minimapPlayer != null)
            {
                bool hasPlayer = PlayerController.I != null;
                minimapPlayer.gameObject.SetActive(hasPlayer);
                if (hasPlayer)
                {
                    float bob = 28f + Mathf.Sin(Time.unscaledTime * 5f) * 2f;
                    PlaceOnMap(minimapPlayer, PlayerController.I.transform.position, minX, maxX, minY, maxY, bob, 0.14f);
                    minimapPlayer.SetAsLastSibling();
                }
            }

            if (minimapTitle != null)
                minimapTitle.text = (interior ? "店内" : port ? "港口" : "小镇") + "  ·  点击放大";
            RefreshTownMapMarkers(minX, maxX, minY, maxY, objective, interior, port);
            if (minimapCaption != null)
            {
                string place = TownMinimap.PlaceName(objective);
                minimapCaption.text = interior ? "小人是你 · 浅色是门口"
                    : port ? "小人是你 · 西边石路回镇"
                    : place == null ? "小人是你" : "小人是你 · 白圈 " + place;
            }
        }

        public void ShowTownMap()
        {
            if (townMapRoot != null) return;
            bool interior = ShopInteriorController.IsInsideShopInterior;
            bool port = !interior && PortSceneController.IsInsidePort;
            int season = GameState.I != null ? AtmosphereService.SeasonIndex(GameState.I.day) : 0;
            Sprite sprite = interior ? TownMinimapAtlas.Room(MapBuilder.ActiveInteriorKind)
                : port ? TownMinimapAtlas.Port() : TownMinimapAtlas.TownLarge(season);
            if (sprite == null) return;

            string title = interior ? "店内地图" : port ? "港口地图" : "小镇地图";
            int zoom = interior || port
                ? Mathf.Max(4, Mathf.RoundToInt(640f / Mathf.Max(1f, sprite.rect.width)))
                : 3;
            float mapW = sprite.rect.width * zoom;
            float mapH = sprite.rect.height * zoom;
            float legendH = interior || port ? 0f : 68f;
            float hintH = 22f;
            float afterMap = legendH > 0f ? 10f : 0f;
            float panelW = mapW + 48f;
            float panelH = mapH + afterMap + legendH + 8f + hintH + 88f;
            townMapRoot = BeginPanel(title, panelW, panelH);
            RectTransform content = ContentOf(townMapRoot);

            Image map = UiTheme.MakeImage(content, Color.white, "bigMap");
            map.sprite = sprite;
            map.type = Image.Type.Simple;
            map.preserveAspect = false;
            map.raycastTarget = false;
            PlaceTopLeft(map.rectTransform, 0f, 0f, mapW, mapH);

            townMapObjectiveImage = UiTheme.MakeImage(map.transform, Color.white, "bigObjective");
            townMapObjectiveImage.raycastTarget = false;
            townMapObjectiveImage.sprite = MarkerSprite(true);
            townMapObjective = townMapObjectiveImage.rectTransform;
            Image you = UiTheme.MakeImage(map.transform, Color.white, "bigPlayer");
            you.raycastTarget = false;
            you.sprite = MarkerSprite(false);
            townMapPlayer = you.rectTransform;

            if (legendH > 0f)
                BuildMapLegend(content, mapH + afterMap, mapW);

            Text hint = UiTheme.MakeText(content, "蓝衣小人是你。白圈标出下一步要去的地方。",
                16, UiTheme.TextSub, TextAnchor.MiddleCenter);
            hint.raycastTarget = false;
            PlaceTopLeft(hint.rectTransform, 0f, mapH + afterMap + legendH + 8f, mapW, hintH);
            RefreshMinimap();
        }

        void BuildMapLegend(RectTransform content, float y, float width)
        {
            Image card = UiTheme.MakeImage(content, UiTheme.Paper, "legendCard");
            card.raycastTarget = false;
            PlaceTopLeft(card.rectTransform, 0f, y, width, 68f);
            Image tab = UiTheme.MakeImage(card.rectTransform, UiTheme.Market, "legendTab");
            tab.raycastTarget = false;
            PlaceTopLeft(tab.rectTransform, 0f, 0f, 8f, 68f);

            string[] kinds = { "home", "hall", "bank", "centralbank", "cinema",
                "shop", "competitor", "market", "factory", "port" };
            float chipW = (width - 8f) / 5f;
            for (int i = 0; i < kinds.Length; i++)
            {
                float x = 8f + (i % 5) * chipW;
                float rowY = 4f + (i / 5) * 32f;
                Image icon = UiTheme.MakeImage(card.rectTransform, Color.white, "key_" + kinds[i]);
                icon.sprite = TownMinimapAtlas.IconSprite(kinds[i]);
                icon.raycastTarget = false;
                PlaceTopLeft(icon.rectTransform, x + 8f, rowY + 6f, 16f, 16f);
                Text name = UiTheme.MakeText(card.rectTransform, TownMinimap.PlaceName(kinds[i]), 16, UiTheme.Ink,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                name.raycastTarget = false;
                PlaceTopLeft(name.rectTransform, x + 30f, rowY, chipW - 36f, 28f);
            }
        }

        void RefreshTownMapMarkers(float minX, float maxX, float minY, float maxY, string objective, bool interior, bool port)
        {
            if (townMapRoot == null) return;
            bool showRing = townMapObjective != null && !interior && !port && objective != null;
            if (townMapObjective != null)
            {
                townMapObjective.gameObject.SetActive(showRing);
                if (showRing)
                {
                    PlaceOnMap(townMapObjective, PinWorld(objective), minX, maxX, minY, maxY, 36f);
                    float pulse = 0.62f + 0.38f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
                    if (townMapObjectiveImage != null)
                        townMapObjectiveImage.color = new Color(1f, 0.96f, 0.82f, pulse);
                }
            }
            if (townMapPlayer != null)
            {
                bool hasPlayer = PlayerController.I != null;
                townMapPlayer.gameObject.SetActive(hasPlayer);
                if (hasPlayer)
                {
                    PlaceOnMap(townMapPlayer, PlayerController.I.transform.position, minX, maxX, minY, maxY, 32f, 0.14f);
                    townMapPlayer.SetAsLastSibling();
                }
            }
        }

        static Vector3 PinWorld(string kind)
        {
            switch (kind)
            {
                case "shop": return MapBuilder.ShopDoor;
                case "home": return MapBuilder.HomeDoor;
                case "hall": return MapBuilder.HallDoor;
                case "bank": return MapBuilder.BankDoor;
                case "centralbank": return MapBuilder.CentralBankDoor;
                case "port": return MapBuilder.PortDoor;
                case "market": return MapBuilder.MarketDoor;
                case "competitor": return MapBuilder.CompetitorDoor;
                case "factory": return MapBuilder.FactoryDoor;
                case "restaurant": return MapBuilder.RestaurantDoor;
                case "cinema": return MapBuilder.CinemaDoor;
                case "convenience": return MapBuilder.ConvenienceDoor;
                case "training": return MapBuilder.TrainingDoor;
                default: return Vector3.zero;
            }
        }

        static void PlaceOnMap(RectTransform marker, Vector3 world, float minX, float maxX, float minY, float maxY,
            float size, float pivotY = 0.5f)
        {
            if (marker == null) return;
            Vector2 n = TownMinimap.Normalize(world, minX, maxX, minY, maxY);
            marker.anchorMin = marker.anchorMax = new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y));
            marker.pivot = new Vector2(0.5f, pivotY);
            marker.anchoredPosition = Vector2.zero;
            marker.sizeDelta = new Vector2(size, size);
        }

        static Sprite markerYou;
        static Sprite markerRing;

        static Sprite MarkerSprite(bool ring)
        {
            if (ring && markerRing != null) return markerRing;
            if (!ring && markerYou != null) return markerYou;
            Sprite sprite = ring ? MakeRingSprite() : MakePlayerSprite();
            if (ring) markerRing = sprite;
            else markerYou = sprite;
            return sprite;
        }

        static Sprite MakeRingSprite()
        {
            const int n = 16;
            Color32[] pixels = new Color32[n * n];
            Color32 ink = new Color32(0xff, 0xf4, 0xd2, 255);
            for (int i = 2; i < n - 2; i++)
            {
                pixels[2 * n + i] = ink;
                pixels[(n - 3) * n + i] = ink;
                pixels[i * n + 2] = ink;
                pixels[i * n + (n - 3)] = ink;
            }
            return SpriteFromPixels(pixels, n, "minimapRing");
        }

        static Sprite MakePlayerSprite()
        {
            const int n = 32;
            Color32[] pixels = new Color32[n * n];
            Color32 halo = new Color32(0xff, 0xd2, 0x3a, 255);
            Color32 shirt = new Color32(0x2f, 0x74, 0xe8, 255);
            Color32 skin = new Color32(0xf6, 0xd2, 0xa8, 255);
            Color32 hair = new Color32(0x2a, 0x1c, 0x14, 255);
            Color32 pants = new Color32(0x1e, 0x2c, 0x44, 255);
            Disc(pixels, n, 16, 7, 8, halo);
            Disc(pixels, n, 16, 26, 5, skin);
            Disc(pixels, n, 16, 28, 4, hair);
            FillBox(pixels, n, 12, 22, 20, 24, hair);
            FillBox(pixels, n, 11, 13, 21, 22, shirt);
            FillBox(pixels, n, 8, 15, 11, 20, shirt);
            FillBox(pixels, n, 21, 15, 24, 20, shirt);
            FillBox(pixels, n, 12, 5, 15, 13, pants);
            FillBox(pixels, n, 17, 5, 20, 13, pants);
            FillBox(pixels, n, 11, 3, 16, 6, hair);
            FillBox(pixels, n, 16, 3, 21, 6, hair);
            Outline(pixels, n, new Color32(0x14, 0x0e, 0x08, 255));
            return SpriteFromPixels(pixels, n, "minimapYou");
        }

        static void Disc(Color32[] pixels, int n, int cx, int cy, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) Plot(pixels, n, x, y, color);
                }
        }

        static void FillBox(Color32[] pixels, int n, int x0, int y0, int x1, int y1, Color32 color)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    Plot(pixels, n, x, y, color);
        }

        static void Outline(Color32[] pixels, int n, Color32 ink)
        {
            Color32[] copy = (Color32[])pixels.Clone();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (copy[y * n + x].a != 0) continue;
                    bool touch = false;
                    for (int oy = -1; oy <= 1 && !touch; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            if ((uint)nx >= (uint)n || (uint)ny >= (uint)n) continue;
                            if (copy[ny * n + nx].a != 0) { touch = true; break; }
                        }
                    if (touch) pixels[y * n + x] = ink;
                }
        }

        static void Plot(Color32[] pixels, int n, int x, int y, Color32 color)
        {
            if ((uint)x >= (uint)n || (uint)y >= (uint)n) return;
            pixels[y * n + x] = color;
        }

        static Sprite SpriteFromPixels(Color32[] pixels, int n, string name)
        {
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
