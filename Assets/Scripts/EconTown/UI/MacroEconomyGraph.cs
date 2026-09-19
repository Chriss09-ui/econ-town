using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public enum MacroGraphMode { GdpComponents, PriceTrend, Employment, AggregateDemandSupply, FiscalHistory, Housing }

    public struct AdAsGeometry
    {
        public float outputMin;
        public float outputMax;
        public float priceMin;
        public float priceMax;
        public float equilibriumOutput;
        public float equilibriumPrice;
        public float potentialOutput;
        public float aggregateSupplyOutput;
        public float adPriceAtMinOutput;
        public float adPriceAtMaxOutput;
        public float srasPriceAtMinOutput;
        public float srasPriceAtMaxOutput;
    }

    /// <summary>宏观中心的轻量数据图。所有点都读取 MacroEconomyState 的同一份历史。</summary>
    public sealed class MacroEconomyGraph : Graphic
    {
        MacroGraphMode mode;
        MacroEconomyState macro;
        HousingState housing;

        static readonly Color Axis = new Color32(0x9a, 0xb0, 0xb3, 150);
        static readonly Color Teal = new Color32(0x5d, 0xc3, 0xb6, 255);
        static readonly Color Amber = new Color32(0xf2, 0xa9, 0x3b, 255);
        static readonly Color Coral = new Color32(0xdd, 0x6b, 0x5a, 255);
        static readonly Color Blue = new Color32(0x78, 0xa9, 0xe6, 255);
        static readonly Color Paper = new Color32(0xe9, 0xd4, 0xa8, 255);

        public void Configure(GameState state, MacroGraphMode graphMode)
        {
            mode = graphMode;
            macro = state != null ? state.macro : null;
            housing = state != null ? state.housing : null;
            SetVerticesDirty();
        }

        public static AdAsGeometry CalculateAdAsGeometry(MacroEconomyState state)
        {
            float equilibriumOutput = Mathf.Max(1f, state == null ? 1f : state.realGdp);
            float potentialOutput = Mathf.Max(1f, state == null ? equilibriumOutput : state.potentialGdp);
            float aggregateSupply = Mathf.Max(1f, state == null ? potentialOutput : state.aggregateSupply);
            float equilibriumPrice = Mathf.Max(1f, state == null ? 100f : state.cpi);
            float lowReference = Mathf.Min(equilibriumOutput, potentialOutput, aggregateSupply);
            float highReference = Mathf.Max(equilibriumOutput, potentialOutput, aggregateSupply);
            float outputMin = Mathf.Max(0f, lowReference * .70f);
            float outputMax = Mathf.Max(outputMin + 100f, highReference * 1.30f);
            float energyDeviation = state == null ? 0f : Mathf.Abs(state.energyCostIndex - 100f);
            float priceSpread = Mathf.Max(12f, equilibriumPrice * .24f + energyDeviation * .10f);
            float priceMin = Mathf.Max(1f, equilibriumPrice - priceSpread);
            float priceMax = equilibriumPrice + priceSpread;
            float outputSpan = Mathf.Max(1f, outputMax - outputMin);
            float demandSlopeFactor = state == null ? .9f : Mathf.Clamp(
                .58f + state.marginalPropensityToConsume * .42f + state.consumerConfidence / 500f,
                .60f, 1.20f);
            float supplySlopeFactor = state == null ? .9f : Mathf.Clamp(
                1.15f - state.productivity / 240f + state.energyCostIndex / 280f,
                .58f, 1.25f);
            float adSlope = priceSpread * 1.45f / outputSpan * demandSlopeFactor;
            float srasSlope = priceSpread * 1.35f / outputSpan * supplySlopeFactor;
            return new AdAsGeometry
            {
                outputMin = outputMin,
                outputMax = outputMax,
                priceMin = priceMin,
                priceMax = priceMax,
                equilibriumOutput = equilibriumOutput,
                equilibriumPrice = equilibriumPrice,
                potentialOutput = potentialOutput,
                aggregateSupplyOutput = aggregateSupply,
                adPriceAtMinOutput = equilibriumPrice + adSlope * (equilibriumOutput - outputMin),
                adPriceAtMaxOutput = equilibriumPrice - adSlope * (outputMax - equilibriumOutput),
                srasPriceAtMinOutput = equilibriumPrice - srasSlope * (equilibriumOutput - outputMin),
                srasPriceAtMaxOutput = equilibriumPrice + srasSlope * (outputMax - equilibriumOutput)
            };
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width < 40f || rect.height < 40f || macro == null) return;
            Vector2 origin = new Vector2(rect.xMin + 42f, rect.yMin + 30f);
            Vector2 size = new Vector2(rect.width - 62f, rect.height - 48f);
            AddSeg(vh, origin, origin + new Vector2(size.x, 0f), Axis, 2f);
            AddSeg(vh, origin, origin + new Vector2(0f, size.y), Axis, 2f);

            if (mode == MacroGraphMode.GdpComponents) DrawComponents(vh, origin, size);
            else if (mode == MacroGraphMode.PriceTrend) DrawPriceTrend(vh, origin, size);
            else if (mode == MacroGraphMode.Employment) DrawEmployment(vh, origin, size);
            else if (mode == MacroGraphMode.AggregateDemandSupply) DrawAdAs(vh, origin, size);
            else if (mode == MacroGraphMode.Housing) DrawHousing(vh, origin, size);
            else DrawFiscal(vh, origin, size);
        }

        void DrawComponents(VertexHelper vh, Vector2 o, Vector2 s)
        {
            float max = Mathf.Max(1f, macro.consumption, macro.investment, macro.governmentSpending, Mathf.Abs(macro.netExports));
            float[] values = { macro.consumption, macro.investment, macro.governmentSpending, macro.netExports };
            Color[] colors = { Teal, Amber, Blue, macro.netExports >= 0f ? Paper : Coral };
            for (int i = 0; i < values.Length; i++)
            {
                float x = .10f + i * .22f;
                float height = Mathf.Abs(values[i]) / max * .74f;
                float y0 = values[i] >= 0f ? .08f : .08f - height * .18f;
                AddRect(vh, P(o, s, x, y0), P(o, s, x + .13f, y0 + height), colors[i]);
            }
        }

        void DrawPriceTrend(VertexHelper vh, Vector2 o, Vector2 s)
        {
            int count = Mathf.Min(10, macro.history.Count);
            if (count == 0)
            {
                AddSeg(vh, P(o, s, .05f, .5f), P(o, s, .95f, .5f), Paper, 3f);
                AddDot(vh, P(o, s, .5f, Mathf.Clamp01((macro.cpi - 80f) / 50f)), Teal, 5f);
                return;
            }
            Vector2 previous = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                MacroPeriodRecord record = macro.history[macro.history.Count - count + i];
                float x = count == 1 ? .5f : Mathf.Lerp(.05f, .95f, i / (float)(count - 1));
                Vector2 point = P(o, s, x, Mathf.InverseLerp(80f, 130f, record.cpi));
                if (i > 0) AddSeg(vh, previous, point, Teal, 4f);
                AddDot(vh, point, Teal, 4f);
                previous = point;
            }
        }

        void DrawEmployment(VertexHelper vh, Vector2 o, Vector2 s)
        {
            float labor = Mathf.Max(1f, macro.laborForce);
            float employed = macro.employment / labor;
            float frictional = macro.frictionalUnemployment / labor;
            float structural = macro.structuralUnemployment / labor;
            float cyclical = macro.cyclicalUnemployment / labor;
            float x = .10f;
            AddRect(vh, P(o, s, x, .20f), P(o, s, x + employed * .80f, .66f), Teal);
            x += employed * .80f;
            AddRect(vh, P(o, s, x, .20f), P(o, s, x + frictional * .80f, .66f), Amber);
            x += frictional * .80f;
            AddRect(vh, P(o, s, x, .20f), P(o, s, x + structural * .80f, .66f), Blue);
            x += structural * .80f;
            AddRect(vh, P(o, s, x, .20f), P(o, s, x + cyclical * .80f, .66f), Coral);
        }

        void DrawAdAs(VertexHelper vh, Vector2 o, Vector2 s)
        {
            AdAsGeometry geometry = CalculateAdAsGeometry(macro);
            float adY0 = Normalize(geometry.adPriceAtMinOutput, geometry.priceMin, geometry.priceMax);
            float adY1 = Normalize(geometry.adPriceAtMaxOutput, geometry.priceMin, geometry.priceMax);
            float srasY0 = Normalize(geometry.srasPriceAtMinOutput, geometry.priceMin, geometry.priceMax);
            float srasY1 = Normalize(geometry.srasPriceAtMaxOutput, geometry.priceMin, geometry.priceMax);
            AddSeg(vh, P(o, s, 0f, adY0), P(o, s, 1f, adY1), Teal, 5f);
            AddSeg(vh, P(o, s, 0f, srasY0), P(o, s, 1f, srasY1), Amber, 5f);

            float potentialX = Normalize(geometry.potentialOutput, geometry.outputMin, geometry.outputMax);
            AddSeg(vh, P(o, s, potentialX, 0f), P(o, s, potentialX, 1f), Blue, 2f);

            float x = Normalize(geometry.equilibriumOutput, geometry.outputMin, geometry.outputMax);
            float y = Normalize(geometry.equilibriumPrice, geometry.priceMin, geometry.priceMax);
            Vector2 eq = P(o, s, x, y);
            AddSeg(vh, P(o, s, x, 0f), eq, new Color32(0xe9, 0xd4, 0xa8, 100), 2f);
            AddSeg(vh, P(o, s, 0f, y), eq, new Color32(0xe9, 0xd4, 0xa8, 100), 2f);
            AddDot(vh, eq, Paper, 7f);
        }

        static float Normalize(float value, float minimum, float maximum)
        {
            return Mathf.Clamp01((value - minimum) / Mathf.Max(.0001f, maximum - minimum));
        }

        void DrawFiscal(VertexHelper vh, Vector2 o, Vector2 s)
        {
            int count = Mathf.Min(10, macro.history.Count);
            if (count == 0)
            {
                AddSeg(vh, P(o, s, .05f, .5f), P(o, s, .95f, .5f), Axis, 2f);
                return;
            }
            Vector2 previousGrowth = Vector2.zero;
            Vector2 previousDebt = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                MacroPeriodRecord record = macro.history[macro.history.Count - count + i];
                float x = count == 1 ? .5f : Mathf.Lerp(.05f, .95f, i / (float)(count - 1));
                Vector2 growth = P(o, s, x, Mathf.InverseLerp(-10f, 10f, record.gdpGrowth));
                Vector2 debt = P(o, s, x, Mathf.InverseLerp(0f, 80f, record.debtToGdp));
                if (i > 0)
                {
                    AddSeg(vh, previousGrowth, growth, Teal, 4f);
                    AddSeg(vh, previousDebt, debt, Coral, 4f);
                }
                AddDot(vh, growth, Teal, 4f);
                AddDot(vh, debt, Coral, 4f);
                previousGrowth = growth;
                previousDebt = debt;
            }
        }

        static Vector2 P(Vector2 origin, Vector2 size, float x, float y)
        {
            return origin + new Vector2(size.x * x, size.y * y);
        }

        /// <summary>V0.9.2 住房市场图:柱=每期房贷余额(存量),线=房贷/年化 GDP(杠杆口径,
        /// 分母 nominalGdp×52——房贷是存量而 GDP 是每期流量,§196.2 mgdpZ 同源)。</summary>
        void DrawHousing(VertexHelper vh, Vector2 o, Vector2 s)
        {
            int count = Mathf.Min(10, housing != null ? housing.history.Count : 0);
            if (count == 0)
            {
                AddSeg(vh, P(o, s, .05f, .5f), P(o, s, .95f, .5f), Axis, 2f);
                return;
            }
            float maxMortgage = 1f;
            for (int i = housing.history.Count - count; i < housing.history.Count; i++)
                maxMortgage = Mathf.Max(maxMortgage, housing.history[i].mortgageOutstanding);
            Vector2 previousRatio = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                HousingPeriodRecord record = housing.history[housing.history.Count - count + i];
                float x = count == 1 ? .5f : Mathf.Lerp(.08f, .92f, i / (float)(count - 1));
                float height = record.mortgageOutstanding / maxMortgage * .62f;
                AddRect(vh, P(o, s, x - .028f, .08f), P(o, s, x + .028f, .08f + height), Blue);
                // 杠杆线分母=同期宏观名义 GDP 年化;两序列由同一收日管线成对写入,尾部索引对齐。
                int macroIndex = macro.history.Count - count + i;
                float gdp = macroIndex >= 0 && macroIndex < macro.history.Count ? macro.history[macroIndex].nominalGdp : 0f;
                float ratio = gdp > 1f ? record.mortgageOutstanding / (gdp * 52f) : 0f;
                Vector2 point = P(o, s, x, Mathf.InverseLerp(0f, .8f, ratio));
                if (i > 0) AddSeg(vh, previousRatio, point, Coral, 3f);
                AddDot(vh, point, Coral, 4f);
                previousRatio = point;
            }
        }

        static void AddRect(VertexHelper vh, Vector2 a, Vector2 b, Color color)
        {
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            float y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            int index = vh.currentVertCount;
            vh.AddVert(new Vector2(x0, y0), color, Vector2.zero);
            vh.AddVert(new Vector2(x0, y1), color, Vector2.zero);
            vh.AddVert(new Vector2(x1, y1), color, Vector2.zero);
            vh.AddVert(new Vector2(x1, y0), color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        static void AddSeg(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            int index = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        static void AddDot(VertexHelper vh, Vector2 center, Color color, float radius)
        {
            const int segments = 12;
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
            }
        }
    }
}
