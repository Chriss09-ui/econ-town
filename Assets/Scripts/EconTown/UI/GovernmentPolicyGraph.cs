using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public enum GovernmentGraphMode
    {
        TaxAndPrice,
        Externality,
        CityHistory
    }

    /// <summary>税楔图模型解(纯函数,EditMode 可断言):全部量纲来自 PriceService.Quote 的真实弹性与转嫁份额。</summary>
    public readonly struct TaxGraphModel
    {
        public readonly float buyerPrice;      // 消费者支付价(含税)
        public readonly float sellerPrice;     // 企业实收净价
        public readonly float wedgePerUnit;    // 每单位税楔(=税−补贴,可为负)
        public readonly float noTaxPrice;      // 无税均衡价 D∩S
        public readonly float noTaxQuantity;   // 无税均衡量
        public readonly float quantity;        // 当前交易量
        public readonly float demandSlopeElasticity, supplySlopeElasticity;
        public readonly float consumerShare;   // 消费者承担份额(真实转嫁)

        public TaxGraphModel(PriceQuote quote, float quantity)
        {
            buyerPrice = quote.consumerPrice;
            sellerPrice = quote.producerNetPrice;
            wedgePerUnit = buyerPrice - sellerPrice;
            this.quantity = Mathf.Max(1f, quantity);
            demandSlopeElasticity = quote.demandElasticity;
            supplySlopeElasticity = quote.supplyElasticity;
            consumerShare = quote.consumerTaxShare;
            // 常弹性曲线过当前营业点:D(p)=q·(p/Pc)^(−ed),S(p)=q·(p/Pp)^(+es);
            // D∩S:−ed·ln(p/Pc)=es·ln(p/Pp) → ln p =(ed·lnPc+es·lnPp)/(ed+es)
            float ed = Mathf.Max(.05f, quote.demandElasticity), es = Mathf.Max(.05f, quote.supplyElasticity);
            noTaxPrice = Mathf.Exp((ed * Mathf.Log(Mathf.Max(.01f, buyerPrice)) +
                                    es * Mathf.Log(Mathf.Max(.01f, sellerPrice))) / (ed + es));
            noTaxQuantity = this.quantity * Mathf.Pow(noTaxPrice / Mathf.Max(.01f, buyerPrice), -ed);
        }

        // 围猎三#19:价格入参也要钳正——负价/楔后负价进 Pow(负底,非整指数)产生 NaN,
        // DrawCurve 的 q<=0 过滤拦不住 NaN(比较恒 false),NaN 顶点会把网格画损坏。
        public float DemandAt(float p) { return quantity * Mathf.Pow(Mathf.Max(.01f, p) / Mathf.Max(.01f, buyerPrice), -demandSlopeElasticity); }
        public float SupplyAt(float p) { return quantity * Mathf.Pow(Mathf.Max(.01f, p) / Mathf.Max(.01f, sellerPrice), supplySlopeElasticity); }
        /// <summary>税后供给(红线):把 S 按每单位税楔上移;与 D 的交点即当前营业点 (quantity, buyerPrice)。
        /// p 低于税楔时无净价、无供给——返回 0 让 DrawCurve 的 q<=0 过滤截断(围猎三#19 复审:
        /// 钳正后此处会画出贴轴假平段,而非本应的截断)。</summary>
        public float ShiftedSupplyAt(float p) { return p - wedgePerUnit < .01f ? 0f : SupplyAt(p - wedgePerUnit); }

        // 闭式反解(常弹性):由数量求价格,供填充区精确落位
        public float DemandPriceAt(float q) { return buyerPrice * Mathf.Pow(Mathf.Max(.01f, q) / quantity, -1f / Mathf.Max(.05f, demandSlopeElasticity)); }
        public float SupplyPriceAt(float q) { return sellerPrice * Mathf.Pow(Mathf.Max(.01f, q) / quantity, 1f / Mathf.Max(.05f, supplySlopeElasticity)); }
        public float ShiftedSupplyPriceAt(float q) { return SupplyPriceAt(q) + wedgePerUnit; }
    }

    /// <summary>外部性图模型解(纯函数):社会供给=S 上移污染税,社会最优点=D∩MSC 二分求解。</summary>
    public readonly struct ExternalityGraphModel
    {
        public readonly float marketPrice, marketQuantity;
        public readonly float socialPrice, socialQuantity;
        public readonly float perUnitTax;

        public ExternalityGraphModel(PriceQuote quote, float quantity, float pollutionTax)
        {
            marketPrice = quote.consumerPrice;
            marketQuantity = Mathf.Max(1f, quantity);
            perUnitTax = pollutionTax;
            TaxGraphModel baseModel = new TaxGraphModel(quote, quantity);
            // f(p)=−ed·ln(p/Pc)−es·ln((p−tp)/Pp),严格递减,在 (tp+ε, 上界] 二分求根
            float lo = pollutionTax + .01f;
            float hi = Mathf.Max(marketPrice * 2f, pollutionTax + marketPrice);
            float ed = Mathf.Max(.05f, quote.demandElasticity), es = Mathf.Max(.05f, quote.supplyElasticity);
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * .5f;
                float f = -ed * Mathf.Log(mid / Mathf.Max(.01f, marketPrice)) -
                          es * Mathf.Log(Mathf.Max(.01f, (mid - pollutionTax) / Mathf.Max(.01f, quote.producerNetPrice)));
                if (f > 0f) lo = mid; else hi = mid;
            }
            socialPrice = (lo + hi) * .5f;
            socialQuantity = baseModel.DemandAt(socialPrice);
        }
    }

    /// <summary>市政府面板使用的轻量曲线图，直接从同一份游戏状态绘制政策前后结果。
    /// V0.9 前置修复:供需线与均衡点从 PriceService.Quote 的真实弹性/转嫁份额与真实交易量推导,
    /// 均衡不再恒在画布中心;限价线按真实 ¥ 值落位(原稿装饰坐标已废)。</summary>
    public sealed class GovernmentPolicyGraph : Graphic
    {
        GovernmentGraphMode mode;
        TaxGraphModel taxModel;
        PriceQuote quote;
        float ceiling;
        float floor;
        float pollutionTax;
        float[] balanceHistory = new float[0];
        float[] approvalHistory = new float[0];

        static readonly Color Axis = new Color32(0xb7, 0xa1, 0x82, 180);
        static readonly Color Demand = new Color32(0x65, 0xb8, 0xb0, 255);
        static readonly Color Supply = new Color32(0xf2, 0xa9, 0x3b, 255);
        static readonly Color Policy = new Color32(0xd8, 0x64, 0x50, 255);
        static readonly Color Social = new Color32(0xb3, 0xd8, 0x66, 255);

        public void Configure(GameState state, GovernmentGraphMode graphMode)
        {
            mode = graphMode;
            if (state != null)
            {
                GovernmentSimulator.Ensure(state);
                ceiling = GovernmentSimulator.PolicyValue(state, PolicyType.PriceCeiling);
                floor = GovernmentSimulator.PolicyValue(state, PolicyType.PriceFloor);
                pollutionTax = GovernmentSimulator.PolicyValue(state, PolicyType.PollutionTax);
                // U6 同源:货架价读玩家挂牌价,不是基础价 state.price
                quote = PriceService.Quote(state, PriceService.PlayerListedPrice(state));
                DayRecord last = state.LastRecord;
                float quantity = last != null && last.sold > 0 ? last.sold :
                                 last != null && last.demandTotal > 0 ? last.demandTotal : 36f;
                taxModel = new TaxGraphModel(quote, quantity);
                int count = Mathf.Min(10, state.government.history.Count);
                balanceHistory = new float[count];
                approvalHistory = new float[count];
                int start = state.government.history.Count - count;
                for (int i = 0; i < count; i++)
                {
                    GovernmentCycleRecord record = state.government.history[start + i];
                    balanceHistory[i] = record.budgetBalance;
                    approvalHistory[i] = record.approvalRating;
                }
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width < 20f || r.height < 20f) return;
            Vector2 origin = new Vector2(r.xMin + 44f, r.yMin + 34f);
            Vector2 size = new Vector2(r.width - 66f, r.height - 54f);
            AddSeg(vh, origin, origin + new Vector2(size.x, 0f), Axis, 2f);
            AddSeg(vh, origin, origin + new Vector2(0f, size.y), Axis, 2f);

            if (mode == GovernmentGraphMode.TaxAndPrice) DrawTaxGraph(vh, origin, size);
            else if (mode == GovernmentGraphMode.Externality) DrawExternalityGraph(vh, origin, size);
            else DrawHistoryGraph(vh, origin, size);
        }

        void DrawTaxGraph(VertexHelper vh, Vector2 o, Vector2 s)
        {
            float pc = taxModel.buyerPrice, pp = taxModel.sellerPrice;
            float pStar = taxModel.noTaxPrice, qStar = taxModel.noTaxQuantity, q0 = taxModel.quantity;
            // 纵轴=真实 ¥,横轴=真实数量;量程取全部真实关键值
            float yMax = Mathf.Max(pc, pStar, ceiling > 0f ? ceiling : 0f, floor > 0f ? floor : 0f);
            float yMin = Mathf.Max(1f, Mathf.Min(pp, pStar) * .82f);
            if (yMax <= yMin) yMax = yMin + 4f;
            yMax = yMax * 1.06f + 1f;
            float xMax = Mathf.Max(qStar, q0) * 1.3f;

            System.Func<float, float, Vector2> pt = (q, p) =>
                new Vector2(o.x + s.x * Mathf.Clamp01(q / xMax), o.y + s.y * Mathf.Clamp01((p - yMin) / (yMax - yMin)));

            DrawCurve(vh, pt, taxModel.DemandAt, yMin, yMax, xMax, Demand, 4f);
            DrawCurve(vh, pt, taxModel.SupplyAt, yMin, yMax, xMax, Supply, 4f);
            if (Mathf.Abs(taxModel.wedgePerUnit) > .001f)
                DrawCurve(vh, pt, taxModel.ShiftedSupplyAt, yMin, yMax, xMax, Policy, 3f);

            // 税楔:当前交易量上的买方/卖方价差竖线 + 两端点
            Vector2 buyerPt = pt(q0, pc), sellerPt = pt(q0, pp);
            if (Mathf.Abs(taxModel.wedgePerUnit) > .001f)
            {
                AddSeg(vh, sellerPt, buyerPt, Policy, 3f);
                AddDot(vh, sellerPt, Policy, 5f);
                AddDot(vh, buyerPt, Policy, 5f);
                // 无谓损失:D 与 S 之间 q0→qStar 的透镜区
                FillLens(vh, pt, taxModel.DemandPriceAt, taxModel.SupplyPriceAt, q0, qStar, new Color32(0xd8, 0x64, 0x50, 70));
            }
            else AddDot(vh, pt(qStar, pStar), Color.white, 5f);   // 无税:均衡点即当前点
            AddDot(vh, pt(qStar, pStar), Color.white, 4f);

            if (ceiling > 0f)
            {
                float y = Mathf.Clamp01((ceiling - yMin) / (yMax - yMin));
                AddSeg(vh, new Vector2(o.x + s.x * .03f, o.y + s.y * y), new Vector2(o.x + s.x * .96f, o.y + s.y * y),
                    new Color32(0x72, 0xa7, 0xe0, 255), 3f);
            }
            if (floor > 0f)
            {
                float y = Mathf.Clamp01((floor - yMin) / (yMax - yMin));
                AddSeg(vh, new Vector2(o.x + s.x * .03f, o.y + s.y * y), new Vector2(o.x + s.x * .96f, o.y + s.y * y),
                    new Color32(0xd8, 0x91, 0xcd, 255), 3f);
            }
        }

        void DrawExternalityGraph(VertexHelper vh, Vector2 o, Vector2 s)
        {
            ExternalityGraphModel ext = new ExternalityGraphModel(quote, taxModel.quantity, pollutionTax);
            float pc = ext.marketPrice, q0 = ext.marketQuantity, ps = ext.socialPrice, qs = ext.socialQuantity;
            float yMax = Mathf.Max(pc, ps, ps + pollutionTax) * 1.12f + 1f;
            float yMin = Mathf.Max(1f, Mathf.Min(taxModel.sellerPrice, ps) * .8f);
            float xMax = Mathf.Max(q0, qs) * 1.3f;

            System.Func<float, float, Vector2> pt = (q, p) =>
                new Vector2(o.x + s.x * Mathf.Clamp01(q / xMax), o.y + s.y * Mathf.Clamp01((p - yMin) / (yMax - yMin)));

            DrawCurve(vh, pt, taxModel.DemandAt, yMin, yMax, xMax, Demand, 4f);
            DrawCurve(vh, pt, taxModel.SupplyAt, yMin, yMax, xMax, Supply, 4f);
            if (pollutionTax > .001f)
                DrawCurve(vh, pt, p => p - pollutionTax < .01f ? 0f : taxModel.SupplyAt(p - pollutionTax), yMin, yMax, xMax, Social, 3f);   // 低于污染税无供给,截断(围猎三#19 复审)

            Vector2 market = pt(q0, pc), social = pt(qs, ps);
            AddSeg(vh, new Vector2(market.x, o.y), market, new Color32(0xf2, 0xa9, 0x3b, 125), 2f);
            AddSeg(vh, new Vector2(social.x, o.y), social, new Color32(0xb3, 0xd8, 0x66, 125), 2f);
            if (pollutionTax > .001f)
                FillLens(vh, pt, q => taxModel.SupplyPriceAt(q) + pollutionTax, taxModel.SupplyPriceAt, qs, q0,
                    new Color32(0xb3, 0xd8, 0x66, 70));
            AddDot(vh, social, Social, 5f);
            AddDot(vh, market, Supply, 5f);
        }

        void DrawHistoryGraph(VertexHelper vh, Vector2 o, Vector2 s)
        {
            if (balanceHistory == null || balanceHistory.Length == 0)
            {
                AddSeg(vh, new Vector2(o.x + s.x * .08f, o.y + s.y * .5f), new Vector2(o.x + s.x * .92f, o.y + s.y * .5f), Axis, 2f);
                return;
            }
            float maxAbs = 1f;
            for (int i = 0; i < balanceHistory.Length; i++) maxAbs = Mathf.Max(maxAbs, Mathf.Abs(balanceHistory[i]));
            Vector2 previousBalance = Vector2.zero;
            Vector2 previousApproval = Vector2.zero;
            for (int i = 0; i < balanceHistory.Length; i++)
            {
                float x = balanceHistory.Length == 1 ? 0.5f : Mathf.Lerp(0.06f, 0.94f, i / (float)(balanceHistory.Length - 1));
                Vector2 balancePoint = new Vector2(o.x + s.x * x, o.y + s.y * (.5f + balanceHistory[i] / maxAbs * .38f));
                Vector2 approvalPoint = new Vector2(o.x + s.x * x, o.y + s.y * Mathf.Clamp01(approvalHistory[i] / 100f));
                if (i > 0)
                {
                    AddSeg(vh, previousBalance, balancePoint, Policy, 4f);
                    AddSeg(vh, previousApproval, approvalPoint, Demand, 4f);
                }
                AddDot(vh, balancePoint, Policy, 4f);
                AddDot(vh, approvalPoint, Demand, 4f);
                previousBalance = balancePoint;
                previousApproval = approvalPoint;
            }
        }

        /// <summary>按价格采样画一条 q(p) 曲线,超出量程的段截断。</summary>
        void DrawCurve(VertexHelper vh, System.Func<float, float, Vector2> pt,
            System.Func<float, float> qOf, float yMin, float yMax, float xMax, Color color, float width)
        {
            const int steps = 26;
            Vector2 previous = Vector2.zero;
            bool hasPrevious = false;
            for (int i = 0; i <= steps; i++)
            {
                float p = Mathf.Lerp(yMin, yMax, i / (float)steps);
                float q = qOf(p);
                if (float.IsNaN(q) || q <= 0f || q > xMax) { hasPrevious = false; continue; }   // 围猎三#19:NaN 显式拦(比较恒 false 挡不住)
                Vector2 point = pt(q, p);
                if (hasPrevious) AddSeg(vh, previous, point, color, width);
                previous = point;
                hasPrevious = true;
            }
        }

        /// <summary>两条价格曲线 priceUpper(q)≥priceLower(q) 在数量 qa..qb 围成的透镜区(扇形三角剖分)。</summary>
        void FillLens(VertexHelper vh, System.Func<float, float, Vector2> pt,
            System.Func<float, float> priceUpper, System.Func<float, float> priceLower,
            float qa, float qb, Color color)
        {
            if (qb <= qa) return;
            const int steps = 10;
            float midQ = (qa + qb) * .5f;
            Vector2 anchor = pt(midQ, (priceUpper(midQ) + priceLower(midQ)) * .5f);
            Vector2 prevUpper = Vector2.zero, prevLower = Vector2.zero;
            for (int i = 0; i <= steps; i++)
            {
                float q = Mathf.Lerp(qa, qb, i / (float)steps);
                Vector2 up = pt(q, priceUpper(q));
                Vector2 low = pt(q, priceLower(q));
                if (i > 0)
                {
                    AddTriangle(vh, anchor, prevUpper, up, color);
                    AddTriangle(vh, anchor, low, prevLower, color);
                }
                prevUpper = up;
                prevLower = low;
            }
        }

        static void AddSeg(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < 0.001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
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
            const int segments = 10;
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
            }
        }

        static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
