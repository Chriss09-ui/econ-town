using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    public class CardDef
    {
        public string id;
        public string title;
        public string body;
        public string formula;
    }

    /// <summary>经济学知识卡库。数量必须从 All 动态读取，禁止把历史数量写入业务逻辑。</summary>
    public static class CardLibrary
    {
        public static readonly List<CardDef> All = new List<CardDef>
        {
            new CardDef { id="scarcity", title="稀缺", body="镇上的钱、原料、人手都是有限的。你不可能同时把所有资源用在这件事上——经济学的一切,都从“东西不够”开始。",
                formula=null },
            new CardDef { id="oppcost", title="机会成本", body="你把 600 元投进了原料,这 600 元就不能再用来雇人。放弃掉的最好用途,就是这笔钱的机会成本。",
                formula="机会成本 = 放弃的最佳选择的价值" },
            new CardDef { id="supply", title="供给", body="你决定生产多少杯果汁,就是在“供给”。价格越高、成本越低,企业通常愿意供给越多。",
                formula="供给量 = f(价格, 成本, 产能)" },
            new CardDef { id="demand", title="需求", body="居民想买多少杯,就是“需求”。价格越低,买的人通常越多——你第一天调价时已经亲眼见过。",
                formula=null },
            new CardDef { id="equilibrium", title="均衡价格", body="当“想买的人数”和“愿意卖的数量”刚好相等时,市场就处于均衡。试着找到你小镇的均衡价。",
                formula="Q需求(P*) = Q供给(P*)" },
            new CardDef { id="elasticity", title="需求价格弹性", body="弹性,就是消费者到底有多怕你涨价。涨价 10%,销量掉的百分比越大,需求越有弹性。",
                formula="Ed = (%ΔQ) ÷ (%ΔP)" },
            new CardDef { id="fixedcost", title="固定成本", body="租金和设备折旧每天都是 100 元,不管你卖 0 杯还是 100 杯——这就是固定成本。",
                formula="固定成本 = 租金 + 折旧" },
            new CardDef { id="variablecost", title="可变成本", body="原料和工资随产量变化:多生产一杯就多花一份钱。它们是可变成本。",
                formula="可变成本 = 原料 + 工资 + 包装" },
            new CardDef { id="averagecost", title="平均成本", body="总成本 ÷ 产量。产量越大,固定成本被摊得越薄,平均成本越低(在你的产能范围内)。",
                formula="AC = TC ÷ Q" },
            new CardDef { id="marginalcost", title="边际成本", body="多生产一杯,要额外花多少钱?在小镇里它约等于一份原料的钱。边际成本是很多决策的标尺。",
                formula="MC = ΔTC ÷ ΔQ" },
            new CardDef { id="marginalproduct", title="边际产量", body="多雇一名员工,能多产多少杯?第 2 名员工带来 +65 杯,这就是他的边际产量。",
                formula="MP = ΔQ ÷ ΔL" },
            new CardDef { id="diminishing", title="边际产量递减", body="+80 → +65 → +50 → +30:店铺和设备不变时,每新增一名员工带来的产量越来越少。",
                formula=null },
            new CardDef { id="revenue", title="收入", body="收入 = 价格 × 销量。注意:涨价不一定增收,因为销量会掉——除非需求缺乏弹性。",
                formula="TR = P × Q" },
            new CardDef { id="profit", title="利润", body="利润 = 总收入 - 总成本。它是经营的记分牌,但不是唯一目标。",
                formula="π = TR - TC" },
            new CardDef { id="profitmax", title="利润最大化", body="经济学结论:当边际收入 ≈ 边际成本时利润最大。多试几个价格和产量,感受这条规律。",
                formula="MR = MC 时利润最大" },

            new CardDef { id="substitute", title="替代品", body="你的果汁和 Sunny Tea 能满足相似需要。你涨价时,一部分顾客会转向对手——两种商品互为替代品。", formula=null },
            new CardDef { id="crosselasticity", title="交叉价格弹性", body="它衡量你的价格变化会让对手销量变化多少。替代品通常具有正的交叉价格弹性。", formula="Exy = %ΔQy ÷ %ΔPx" },
            new CardDef { id="differentiation", title="产品差异化", body="品质和品牌让两杯饮料不再完全相同。差异越明显,企业越不必只靠低价竞争。", formula=null },
            new CardDef { id="marketshare", title="市场份额", body="你的销量占两家店总销量的比例。份额说明顾客去了哪里,但份额高不等于利润高。", formula="市场份额 = 本店销量 ÷ 市场总销量" },
            new CardDef { id="marketpower", title="市场势力", body="品牌和差异化让顾客不容易离开,企业因此拥有一定定价能力。", formula=null },
            new CardDef { id="perfectcompetition", title="完全竞争", body="很多企业卖几乎相同的商品,单个企业很难自行定价。价格高于市场价,顾客就会离开。", formula=null },
            new CardDef { id="monopolisticcompetition", title="垄断竞争", body="市场里有多家企业,但产品又有差异。企业既面对竞争,也拥有一点定价能力。", formula=null },
            new CardDef { id="oligopoly", title="寡头", body="市场只有少数企业时,每家的行动都会影响其他企业,策略互动变得非常重要。", formula=null },
            new CardDef { id="monopoly", title="垄断", body="市场只有一个供应者时,企业能提高价格和减少产量,但消费者通常会受损。", formula=null },
            new CardDef { id="gametheory", title="博弈论", body="你的最好选择取决于对手怎么选。博弈论研究的就是这种互相影响的策略决策。", formula=null },
            new CardDef { id="prisonersdilemma", title="囚徒困境", body="双方都不降价时可能赚得更多,但每家都有偷偷降价抢客的动力,最后双方一起陷入低利润。", formula=null },
            new CardDef { id="nashequilibrium", title="纳什均衡", body="知道其他人的策略后,任何一方都不愿单独改变自己的选择,这种稳定状态叫纳什均衡。", formula=null },
            new CardDef { id="pricewar", title="价格战", body="企业轮番降价争夺顾客。销量和份额可能上涨,但利润会因为价格过低而共同下降。", formula=null },
            new CardDef { id="consumersurplus", title="消费者剩余", body="消费者愿意支付的最高价格减去实际支付价格,就是他从交易中获得的额外好处。", formula="消费者剩余 = 最高愿付价格 - 实际价格" },
            new CardDef { id="producersurplus", title="生产者剩余", body="实际售价减去企业最低愿意接受的价格,可以帮助理解生产者从交易中获得的好处。", formula="生产者剩余 = 实际价格 - 最低愿售价格" },
            new CardDef { id="imperfectinfo", title="信息不完全", body="你看得见对手的价格、排队和评分,却看不见其成本、库存和真实利润。现实竞争也常在信息不完整时发生。", formula=null },

            new CardDef { id="preference", title="偏好", body="同样的预算，不同人会选择不同组合。偏好描述你更看重什么，不代表哪一种生活更正确。", formula="选择 = f(偏好, 价格, 收入, 时间)" },
            new CardDef { id="utility", title="效用", body="效用是消费带来的主观满足。它帮助比较选择，却不是现实世界里能直接称重的东西。", formula=null },
            new CardDef { id="totalutility", title="总效用", body="一天里每次消费带来的满足累加起来，就是总效用。", formula="TU = ΣMU" },
            new CardDef { id="marginalutility", title="边际效用", body="再多消费一单位所增加的满足，就是边际效用。", formula="MU = ΔTU ÷ ΔQ" },
            new CardDef { id="diminishingutility", title="边际效用递减", body="第一杯最解渴，连续喝到第三杯时新增满足通常变少，这就是边际效用递减。", formula=null },
            new CardDef { id="budgetconstraint", title="预算约束", body="现金与时间都有限。所有买得起的组合构成你的预算范围。", formula="P₁Q₁ + P₂Q₂ ≤ 收入" },
            new CardDef { id="budgetline", title="预算线", body="刚好花完预算的商品组合连成预算线；收入增加会让它外移，某个商品涨价会让它转动。", formula="P₁Q₁ + P₂Q₂ = 收入" },
            new CardDef { id="indifferencecurve", title="无差异曲线", body="曲线上的不同消费组合能带来相近满足。人可以用少一点 A 换多一点 B。", formula=null },
            new CardDef { id="optimalbundle", title="最优消费组合", body="在预算范围里，能到达的最高无差异曲线与预算线相切处，是理论上的最优组合。", formula="MRS = P₁ ÷ P₂" },
            new CardDef { id="normalgood", title="正常品", body="收入增加后通常会买得更多的商品，称为正常品。", formula=null },
            new CardDef { id="inferiorgood", title="劣等品（低档品）", body="收入增加后可能买得更少、转向更好替代品的商品，称为劣等品；这不是对商品的道德评价。", formula=null },
            new CardDef { id="complement", title="互补品", body="电影与爆米花常被一起消费。一种商品变便宜，可能带动另一种商品的需求。", formula=null },
            new CardDef { id="incomeeffect", title="收入效应", body="价格下降让同样的钱能买更多，实际购买力上升，由此产生的选择变化叫收入效应。", formula=null },
            new CardDef { id="substitutioneffect", title="替代效应", body="一种商品相对变贵时，人会转向更划算的替代品，这部分变化叫替代效应。", formula=null },
            new CardDef { id="laborleisure", title="工作—闲暇选择", body="多工作能增加收入，却会减少休息。工作和闲暇之间没有适合所有人的唯一答案。", formula="24 小时 = 工作 + 闲暇 + 睡眠 + 必要活动" },
            new CardDef { id="saving", title="储蓄", body="储蓄是把今天的购买力留到未来。它会减少当期消费，却增加未来安全感和选择空间。", formula="储蓄 = 收入 - 消费" },
            new CardDef { id="realpurchasingpower", title="实际购买力", body="账户里的数字没变，价格上涨后能买到的东西却变少了；真正重要的是实际购买力。", formula="实际购买力 ≈ 名义收入 ÷ 价格水平" },

            new CardDef { id="marketfailure", title="市场失灵", body="当市场价格不能反映全部社会成本和收益，或者商品难以排除不付费者时，市场结果可能偏离社会更希望达到的状态。", formula=null },
            new CardDef { id="externality", title="外部性", body="一个人的生产或消费影响了没有参与交易的第三方，这部分影响没有完整进入市场价格，就形成外部性。", formula="社会成本 = 私人成本 + 外部成本" },
            new CardDef { id="positiveexternality", title="正外部性", body="培训不仅提高个人收入潜力，也可能让同事、企业和整个小镇受益。个人决策看到的收益小于社会总收益。", formula="社会边际收益 > 私人边际收益" },
            new CardDef { id="negativeexternality", title="负外部性", body="工厂获得生产收益，却没有自动支付居民承受的污染和健康损失，市场价格因此低估了社会成本。", formula="社会边际成本 > 私人边际成本" },
            new CardDef { id="publicgood", title="公共品", body="路灯和公共公园具有非排他性与一定的非竞争性：建成后很难只让付费者使用。", formula=null },
            new CardDef { id="freerider", title="搭便车", body="每个人都希望别人出钱、自己免费享受公共服务，自愿筹资因此可能失败。", formula=null },
            new CardDef { id="commoditytax", title="商品税", body="政府按每单位交易征税。它能带来财政收入，也会在买卖双方之间制造价格楔子并改变交易量。", formula="单位税 = 消费者支付价 - 企业实收价" },
            new CardDef { id="taxincidence", title="税负归宿", body="法律写着谁缴税，不代表经济上就由谁承担。缺乏弹性的一方更难退出市场，通常承担更多税负。", formula="消费者负担 + 企业负担 = 单位税" },
            new CardDef { id="taxelasticity", title="税收与弹性", body="需求越缺乏弹性，企业越容易把税转嫁给消费者；供给越缺乏弹性，企业承担的部分通常越多。", formula=null },
            new CardDef { id="subsidy", title="补贴", body="补贴降低消费者支付价或提高企业实收价，可以扩大交易与鼓励特定行为，但支出最终来自政府预算。", formula="政府补贴支出 = 单位补贴 × 交易量" },
            new CardDef { id="deadweightloss", title="无谓损失", body="政策使一部分原本对买卖双方都有好处的交易没有发生，这些损失既没有归消费者、企业，也没有变成政府收入。", formula="近似 DWL = 1/2 × 政策楔子 × 交易量变化" },
            new CardDef { id="priceceiling", title="价格上限", body="政府规定最高售价。只有当上限低于市场均衡价格时才真正约束市场，并可能带来短缺。", formula=null },
            new CardDef { id="pricefloor", title="价格下限", body="政府规定最低售价。只有当下限高于市场均衡价格时才真正约束市场，并可能带来过剩。", formula=null },
            new CardDef { id="shortage", title="短缺", body="需求量超过供给量时形成短缺。价格更低不等于每个人都能买到，排队与售罄也是分配方式。", formula="短缺 = 需求量 - 供给量" },
            new CardDef { id="surplus", title="过剩", body="供给量超过需求量时形成过剩。最低价格保护了单价，却可能让一部分商品卖不出去。", formula="过剩 = 供给量 - 需求量" },
            new CardDef { id="minimumwage", title="最低工资", body="最低工资对就业的影响取决于它是否高于现行工资、企业利润空间、劳动需求弹性、生产率和市场需求，不能写成固定结论。", formula=null },
            new CardDef { id="governmentspending", title="政府支出", body="公共设施、补贴、福利和环境治理都需要支出。建设费之外，公共服务往往还有持续维护成本。", formula=null },
            new CardDef { id="fiscalrevenue", title="财政收入", body="商品税、污染税和其他收费构成政府收入。税率提高不保证收入无限增加，因为交易与税基也会变化。", formula=null },
            new CardDef { id="fiscaldeficit", title="财政赤字", body="一个周期内政府支出高于收入，差额就是赤字。赤字描述当期资金缺口，是流量。", formula="财政余额 = 政府收入 - 政府支出" },
            new CardDef { id="governmentdebt", title="政府债务", body="过去赤字累积形成政府债务。债务是存量，并会带来利息，挤压未来财政空间。", formula="期末债务 = 期初债务 + 新增借款 - 偿还" },
            new CardDef { id="equityefficiency", title="公平与效率", body="有的政策提高总产出，有的政策改善低收入群体处境。游戏会同时展示效率、福利、成本、活力与公平，不给出唯一价值答案。", formula=null },
            new CardDef { id="governmentfailure", title="政府失灵", body="政府也可能信息不足、政策冲突、反应过度或忽视长期成本。市场失灵并不意味着任何干预都会自动变好。", formula=null },

            new CardDef { id="gdp", title="国内生产总值（GDP）", body="一个时期内，小镇生产的最终商品与服务的市场价值。中间品不能重复相加。", formula="GDP = C + I + G + NX" },
            new CardDef { id="valueadded", title="增加值", body="每个生产环节新创造的价值。小麦、面粉、面包逐层核算时，增加值之和等于最终面包价值。", formula="增加值 = 本环节产出价值 - 中间投入价值" },
            new CardDef { id="nominalgdp", title="名义 GDP", body="用当期价格计算的产出价值。它可能因为产量增加，也可能只是因为价格上涨。", formula="名义 GDP = Σ 当期价格 × 当期产量" },
            new CardDef { id="realgdp", title="实际 GDP", body="用基期价格计算产出，剔除价格变化后更适合比较经济是否真正增长。", formula="实际 GDP ≈ 名义 GDP ÷ GDP 平减指数" },
            new CardDef { id="percapitagdp", title="人均 GDP", body="把实际 GDP 除以人口，帮助观察平均到每个人的产出；它不说明收入是否平均分配。", formula="人均 GDP = 实际 GDP ÷ 人口" },
            new CardDef { id="consumption", title="消费（C）", body="居民购买最终商品与服务的支出。企业卖给居民的果汁只记一次，不能把居民支出和企业销售重复相加。", formula=null },
            new CardDef { id="investment", title="投资（I）", body="企业购买设备、扩建与增加资本存量的支出。购买原料是中间投入，不属于宏观投资。", formula=null },
            new CardDef { id="netexports", title="净出口（NX）", body="出口增加本地生产需求，进口是由外地生产的部分，因此在 GDP 恒等式中扣除。", formula="NX = 出口 X - 进口 M" },
            new CardDef { id="cpi", title="消费者价格指数（CPI）", body="用固定权重的一篮子商品追踪居民生活成本。某一种商品涨价不等于总体物价都在上涨。", formula="CPI = Σ 分类价格指数 × 分类权重" },
            new CardDef { id="inflation", title="通货膨胀", body="总体价格水平持续上升。需求过旺和供给成本上升都可能造成通胀，但政策含义不同。", formula="通胀率 = CPI 同比变化率" },
            new CardDef { id="unemploymentrate", title="失业率", body="没有工作、能够工作并且正在找工作的人，才计入失业。没有找工作的人不在劳动力人口中。", formula="失业率 = 失业人数 ÷ 劳动力人口" },
            new CardDef { id="frictionalunemployment", title="摩擦性失业", body="换工作、毕业求职等搜寻过程造成的短期失业，即使经济正常也会存在。", formula=null },
            new CardDef { id="structuralunemployment", title="结构性失业", body="劳动者技能或地区与新岗位不匹配造成的失业。技术进步可能先放大它，再通过培训缓解。", formula=null },
            new CardDef { id="cyclicalunemployment", title="周期性失业", body="总需求不足、企业减产时出现的失业，会在衰退中上升。", formula=null },
            new CardDef { id="businesscycle", title="经济周期", body="经济会经历复苏、扩张、过热、放缓、衰退和谷底。周期来自消费、投资、信心与冲击的共同变化。", formula=null },
            new CardDef { id="potentialgdp", title="潜在 GDP", body="在资本、劳动和技术正常使用时可持续生产的水平，不是绝对最大产量。", formula=null },
            new CardDef { id="outputgap", title="产出缺口", body="实际产出低于潜在产出表示资源闲置；高于潜在产出太多则可能过热。", formula="产出缺口 = (实际 GDP - 潜在 GDP) ÷ 潜在 GDP" },
            new CardDef { id="aggregatedemand", title="总需求（AD）", body="全社会对本地产出的总支出。消费、投资、政府购买或净出口变化都会移动总需求。", formula="AD = C + I + G + NX" },
            new CardDef { id="aggregatesupply", title="总供给（AS）", body="企业在不同总体价格下愿意并能够生产的产出，受劳动、资本、技术、工资、原料与能源影响。", formula=null },
            new CardDef { id="demandshock", title="需求冲击", body="消费信心骤降、节庆消费或海外需求变化，会先改变总需求，再传到生产和就业。", formula=null },
            new CardDef { id="supplyshock", title="供给冲击", body="能源危机、灾害等会推高成本并压低生产能力，使物价和产出朝相反方向变化。", formula=null },
            new CardDef { id="stagflation", title="滞胀", body="经济停滞、高失业与高通胀同时出现。刺激需求能保就业却可能加重通胀，因此没有无代价的单一答案。", formula=null },
            new CardDef { id="expansionaryfiscal", title="扩张性财政政策", body="衰退时增加政府购买或减税，能拉动订单、就业和收入，同时会增加赤字、债务和通胀风险。", formula=null },
            new CardDef { id="contractionaryfiscal", title="紧缩性财政政策", body="经济过热时减少支出或增税，帮助需求降温，但短期增长与就业也会放慢。", formula=null },
            new CardDef { id="fiscalmultiplier", title="财政乘数", body="政府新增支出成为他人收入，其中一部分再次消费，于是总需求变化可能大于最初支出。", formula="简单乘数 = 1 ÷ (1 - MPC)" },
            new CardDef { id="mpc", title="边际消费倾向（MPC）", body="收入每增加 1 元，居民愿意新增消费的比例。MPC 越高，简单财政乘数越大。", formula="MPC = 新增消费 ÷ 新增收入" },
            new CardDef { id="automaticstabilizer", title="自动稳定器", body="衰退时税收自然减少、福利支出自然增加，即使政府没有再次点击政策，也会缓冲收入下滑。", formula=null },
            new CardDef { id="productivity", title="生产率", body="每单位劳动和资本能创造多少产出。技术、教育和管理改善能提高生产率。", formula="劳动生产率 = 产出 ÷ 劳动投入" },
            new CardDef { id="longrungrowth", title="长期经济增长", body="资本积累、教育和技术进步会提高潜在 GDP。短期刺激主要影响需求，不能替代长期生产能力建设。", formula=null },

            new CardDef { id="money", title="货币", body="货币让陌生人不必同时需要对方的东西也能交易，并为价格和跨时期选择提供共同尺度。", formula=null },
            new CardDef { id="mediumofexchange", title="交换媒介", body="人们普遍接受它来付款，交易就不再依赖以物易物的需求双重巧合。", formula=null },
            new CardDef { id="unitofaccount", title="计价单位", body="货币给不同商品一个共同价格尺度，让成本、收入与债务可以直接比较。", formula=null },
            new CardDef { id="storeofvalue", title="价值储藏", body="货币可以把今天的购买力带到未来，但通货膨胀会降低它届时能买到的东西。", formula=null },
            new CardDef { id="commercialbank", title="商业银行", body="商业银行接受存款、审查贷款并提供支付服务，承担流动性与信用风险。", formula=null },
            new CardDef { id="bankbalancesheet", title="银行资产负债表", body="贷款、准备金和债券是银行资产；客户存款和央行借款是负债，差额是银行资本。", formula="资产 = 负债 + 银行资本" },
            new CardDef { id="bankdeposit", title="存款", body="存款对居民是资产，对银行是随时需要兑付的负债。同一笔交易必须双边入账。", formula=null },
            new CardDef { id="loan", title="贷款", body="贷款对借款人是债务，对银行是未来可收回本金与利息的资产。", formula=null },
            new CardDef { id="reserves", title="准备金", body="银行用准备金满足提款和银行间结算。它不等于存款总额。", formula=null },
            new CardDef { id="reserveratio", title="准备金率", body="准备金占存款的比例。比例过低会提高流动性风险，过高则压缩可贷资金。", formula="准备金率 = 准备金 ÷ 存款" },
            new CardDef { id="creditcreation", title="信用创造", body="银行发放贷款时，通常同时在借款人账户中生成存款；银行资产与负债同时增加。", formula="新贷款资产 +L ↔ 新存款负债 +L" },
            new CardDef { id="depositmoney", title="存款货币", body="银行账户里可用于支付的存款也是货币。它不是一叠在金库里原封不动的现钞。", formula=null },
            new CardDef { id="moneysupply", title="货币供应量", body="货币供应包括流通现金和可支付存款。广义程度不同，统计口径也不同。", formula="M1 ≈ M0 + 可支付存款" },
            new CardDef { id="moneymultiplier", title="货币乘数", body="在简化模型中，准备金率限制银行体系能支撑的存款规模；现实还受资本、风险与需求限制。", formula="简化货币乘数 = 1 ÷ 准备金率" },
            new CardDef { id="credit", title="信贷", body="信贷把未来收入的一部分移到今天使用，能支持投资和消费，也会带来还款和违约风险。", formula=null },
            new CardDef { id="interestrate", title="利率", body="利率是跨期使用资金的价格。它影响存款回报、贷款成本与债券收益。", formula="利息 = 本金 × 期间利率" },
            new CardDef { id="nominalinterest", title="名义利率", body="合同或报价上直接写出的利率，尚未扣除通货膨胀对购买力的侵蚀。", formula=null },
            new CardDef { id="realinterest", title="实际利率", body="实际利率衡量利息收入真正增加了多少购买力。", formula="实际利率 ≈ 名义利率 - 通胀率" },
            new CardDef { id="riskpremium", title="风险溢价", body="借款人违约概率越高、抵押和现金流越弱，银行通常要求更高利率作为风险补偿。", formula="贷款利率 = 基准利率 + 风险溢价 + 期限溢价" },
            new CardDef { id="interestsaving", title="利率与储蓄", body="其他条件不变时，存款实际回报提高会增强延后消费的吸引力。", formula=null },
            new CardDef { id="interestconsumption", title="利率与消费", body="利率上升会提高借款消费的成本，也可能让居民增加储蓄，因而压低当期消费。", formula=null },
            new CardDef { id="interestinvestment", title="利率与投资", body="项目的预期回报必须补偿融资成本。贷款利率越高，能通过这个门槛的投资越少。", formula="投资当 预期边际回报 > 资金成本" },
            new CardDef { id="centralbank", title="中央银行", body="央行通过政策利率、准备金和流动性工具维护价格与金融稳定，它不直接按钮修改 GDP。", formula=null },
            new CardDef { id="policyrate", title="政策利率", body="央行设定的短期基准利率，先影响银行资金成本和报价，再向实体经济传导。", formula=null },
            new CardDef { id="expansionarymonetary", title="扩张性货币政策", body="降低政策利率通常会降低融资成本、支持消费与投资，但需求过强时也可能增加通胀压力。", formula=null },
            new CardDef { id="contractionarymonetary", title="紧缩性货币政策", body="提高政策利率能让需求降温并抑制通胀，代价可能是投资、产出与就业短期放慢。", formula=null },
            new CardDef { id="monetarytransmission", title="货币政策传导", body="政策利率先推动存贷款利率，再影响储蓄、消费、投资与总需求，最后才体现在产出、就业和通胀。", formula="政策利率 → 银行利率 → C/I → AD → GDP、通胀" },
            new CardDef { id="policylag", title="政策时滞", body="从决策、银行调价、家庭与企业改变行为，到产出与物价反应需要时间，政策效果不会当天到齐。", formula=null },
            new CardDef { id="inflationtarget", title="通胀目标", body="央行公布中期通胀目标，为价格稳定提供可预期的标尺，但不代表每期都精确命中。", formula=null },
            new CardDef { id="inflationexpectations", title="通胀预期", body="人们对未来物价的判断会影响今天的工资、定价、消费与投资选择。", formula=null },
            new CardDef { id="liquidity", title="流动性", body="流动性是资产能否迅速、低损失地变成可支付资金。银行资产充足也可能暂时付不出现金。", formula=null },
            new CardDef { id="bankrun", title="银行挤兑", body="大量存款人同时提款会迅速耗尽准备金。即使长期资产大于负债，银行仍可能出现流动性危机。", formula=null },
            new CardDef { id="baddebt", title="坏账", body="借款人无法履约时，贷款资产减记，回收之外的损失直接侵蚀银行资本。", formula=null },
            new CardDef { id="bankcapital", title="银行资本", body="银行资本是资产减去负债的缓冲垫。坏账先冲减资本，资本耗尽后存款人面临损失风险。", formula="银行资本 = 资产 - 负债" },
            new CardDef { id="lenderoflastresort", title="最后贷款人", body="市场资金断裂时，央行可向有偿付能力但暂缺流动性的银行提供紧急贷款。", formula=null },
            new CardDef { id="financialstability", title="金融稳定", body="金融体系能持续支付、储蓄与融资，并吸收一定损失而不会连锁崩溃。", formula=null },
            new CardDef { id="creditcycle", title="信贷周期", body="景气时信贷和资产价格容易相互推升；风险爆发后银行收紧贷款，又会放大下行。", formula=null },
            new CardDef { id="financialaccelerator", title="金融加速器", body="企业资产与现金流走弱会提高融资溢价，投资再下降，从而把初始冲击放大。", formula=null },
            new CardDef { id="governmentbond", title="国债", body="政府通过发行债券把资金需求分摊到未来，债券对持有者是资产，对政府是债务。", formula=null },
            new CardDef { id="debtinterest", title="债务利息", body="既有固定利率债务不会因央行当天加息全部重定价；新债和到期续借才逐渐反映更高市场利率。", formula="利息支出 = 各期债务本金 × 其合同利率" },

            // ———————— V0.7 第七篇 · 世界市场(intl.*) ————————
            new CardDef { id="intl.trade", title="国际贸易", body="两国各自专注于自己更擅长的生产并交换，双方都能消费到超出自给水平的组合。贸易的本质是交换相对效率。", formula=null },
            new CardDef { id="intl.absoluteadvantage", title="绝对优势", body="一国用更少投入就能生产某种商品，称其在该商品上拥有绝对优势。但绝对优势并不能决定谁该生产什么。", formula=null },
            new CardDef { id="intl.comparativeadvantage", title="比较优势", body="即使一国样样都比别国强，只要机会成本不同，各国仍然各有所长。比较优势看的是放弃多少，而不是消耗多少。", formula="机会成本 = 放弃的另一种产品量 ÷ 得到的该产品量" },
            new CardDef { id="intl.specialization", title="专业化生产", body="按比较优势集中资源生产,总产量会高于各自自给。专业化同时让分工深化、经验积累。", formula=null },
            new CardDef { id="intl.gainsfromtrade", title="贸易利益", body="专业化并按互利价格交换后,两国可消费组合都优于自给状态;利益如何分配取决于交换价格。", formula=null },
            new CardDef { id="intl.exports", title="出口", body="把本国生产的产品卖给外国消费者。出口量由外国需求、汇率、运输与关税共同决定——不是想卖多少就卖多少。", formula=null },
            new CardDef { id="intl.imports", title="进口", body="购买外国生产的商品与服务。进口既有最终消费品,也有能源、芯片这类中间投入。", formula=null },
            new CardDef { id="intl.netexports2", title="净出口 NX", body="出口减进口是 GDP 恒等式中的 NX。它必须来自真实交易流，而不是凭公式生成。", formula="NX = X - M" },
            new CardDef { id="intl.tradesurplus", title="贸易顺差", body="出口大于进口。顺差意味着本国在对外净放贷：不必然是好事，也可能是本国消费不足。", formula=null },
            new CardDef { id="intl.tradedeficit", title="贸易逆差", body="进口大于出口。若逆差换来的是先进机器与能源，可能在为未来产能投资；顺差国则在对逆差国融资。", formula=null },
            new CardDef { id="intl.worldprice", title="世界价格", body="同一种商品在国际市场上的基准价格。国内价格与世界价格的差,决定了进口动力与出口动力。", formula=null },
            new CardDef { id="intl.tariffcard", title="关税", body="政府对进口商品征收的税。它抬高到岸价，保护了国内同类企业，却由消费者买单并产生无谓损失。", formula="到岸价 = 外币价 × 汇率 × (1 + 关税率)" },
            new CardDef { id="intl.quota", title="进口配额", body="直接限制进口数量而非价格。配额造成价格上升的收益归配额持有人，政府拿不到税收。", formula=null },
            new CardDef { id="intl.protectionism", title="贸易保护", body="用关税或配额扶持国内产业的政策。短期保护就业,长期削弱竞争与生产率,还可能招致报复。", formula=null },
            new CardDef { id="intl.tariffrevenue", title="关税收入", body="政府只对真实发生的进口征税：关税收 = 税率 × 实际完税进口额。加税会让税基缩小。", formula="关税收入 = 税率 × 实际进口额" },
            new CardDef { id="intl.tariffdwl", title="关税无谓损失", body="关税收之外,被高价格挤掉的交易本身就是净损失——消费者剩余的减少大于政府与生产者所得之和。", formula="无谓损失 = 消费者剩余损失 −(关税收入 + 生产者剩余增加)" },
            new CardDef { id="intl.exchangerate", title="汇率", body="两国货币的交换比例。本游戏全程统一报价「1 B币 = X A币」:X 变大即 A币贬值。", formula="1 B币 = X A币" },
            new CardDef { id="intl.nominalfx", title="名义汇率", body="市场上的实际货币报价,未剔除两国物价差异。日常新闻里的汇率指的就是它。", formula=null },
            new CardDef { id="intl.appreciation", title="本币升值", body="A币更值钱:X 变小。买进口货便宜了、出境游划算;但出口企业按外币收进来的钱折算变少。", formula=null },
            new CardDef { id="intl.depreciation", title="本币贬值", body="A币变便宜:X 变大。出口竞争力提升、外国订单可能增加;进口原料与消费品同步涨价。贬值不是单纯的利好。", formula=null },
            new CardDef { id="intl.realfx", title="实际汇率", body="用两国物价调整后的贸易竞争力指标:名义汇率 × 外国价格 ÷ 本国价格。通胀也会侵蚀升值带来的好处。", formula="实际汇率 ≈ 名义汇率 × P_外 / P_内" },
            new CardDef { id="intl.fxmarket", title="外汇市场", body="买卖不同货币的市场。本日成交价只反映昨日之前的供求,今天的流量要到收盘后才参与定价。", formula=null },
            new CardDef { id="intl.fxdemand", title="汇率的供求", body="外国购买本国商品与资产构成对 A币的需求;本国购买外国商品与资产构成供给。每项分解都能在面板里看到数值。", formula=null },
            new CardDef { id="intl.ratedifferential", title="利率差", body="两国政策利率之差减去风险溢价,决定资本流动方向。高利率只在风险相当时才吸引资本。", formula="利差 = 本国利率 − 外国利率 − 风险溢价" },
            new CardDef { id="intl.capitalinflow", title="资本流入", body="外国投资者买入本国债券、股票或设备。它推升本币需求带来升值压力,也累积未来偿付义务。", formula=null },
            new CardDef { id="intl.capitaloutflow", title="资本流出", body="本国资金转向海外资产。急剧流出会压垮本币,迫使央行动用储备或加息防守。", formula=null },
            new CardDef { id="intl.currentaccount", title="经常账户", body="简化口径下约等于净出口:记录商品与服务贸易的收入支出差额。", formula="经常账户 ≈ X − M" },
            new CardDef { id="intl.financialaccount", title="金融账户", body="记录资本的跨境流动。经常账户盈余的国家,通常对应金融账户的对外净投资。", formula=null },
            new CardDef { id="intl.bop", title="国际收支", body="经常账户与金融账户互为镜像:一国的贸易盈余必然体现为对他国的净债权变化。", formula=null },
            new CardDef { id="intl.importedinflation", title="输入型通胀", body="本币贬值让进口能源与食品涨价,推动国内 CPI 上行——即使国内需求毫无起色。这是开放经济特有的通胀渠道。", formula=null },
            new CardDef { id="intl.fxpassthrough", title="汇率传导", body="汇率变化先改变进出口相对价格,再传导至销量、企业利润、CPI 与 GDP。分解面板能看到每一环的数字贡献。", formula=null },
            new CardDef { id="intl.jcurve", title="J 曲线", body="贬值初期进出口数量来不及调整,以本币计价的账单反而恶化;随时间推移数量才逐步反应,净出口才改善。", formula=null },
            new CardDef { id="intl.demandshockfx", title="国际需求冲击", body="贸易伙伴衰退时,本国出口订单随之外滑。全球化经济里,别国的萧条会通过订单表敲开你的门。", formula=null },
            new CardDef { id="intl.supplychain", title="全球供应链", body="芯片、能源、原料常常横跨国境。供应链既带来成本优势,也把远方的事件变成你明天的采购单风险。", formula=null },
            new CardDef { id="intl.tradewar", title="贸易战", body="相互加征关税升级成对抗。保护了自己的产业,却也抬高消费价格、搅乱供应链——报复不是免费反击。", formula=null },
            new CardDef { id="intl.reserves2", title="外汇储备", body="央行持有的外币资产,是干预汇率的弹药。卖出多少储备都必须记入账本:储备不是无限的。", formula=null },
            new CardDef { id="intl.fxintervention", title="汇率干预", body="央行在外汇市场买卖本币以平滑波动或守住盯住。固定汇率制度下,每一次偏离带外的防守都要真金白银。", formula="干预效果 = 缺口 × (实际消耗 ÷ 所需弹药)" },
            new CardDef { id="intl.fixedrate", title="固定汇率", body="承诺把汇率钉在目标价。可预期性换来的是丧失货币政策独立性,且必须备足外储应对投机压力。", formula=null },
            new CardDef { id="intl.floatingrate", title="浮动汇率", body="由外汇市场供求决定价位,央行不承诺点位。缓冲外部冲击的同时,进出口商要承受波动风险。", formula=null },
            new CardDef { id="intl.trilemma", title="三元悖论", body="固定汇率、资本自由流动、独立货币政策,三者最多同时实现两个。尝试全要时,市场会用危机教你取舍。", formula="三选二:固定汇率 / 资本自由 / 货币独立" },

            // ———————— V0.9.5 第八篇 · 资产与杠杆(housing.*/asset.*) ————————
            new CardDef { id="housing.downpayment", title="首付与杠杆", body="首付三成意味着用 ¥1 控制 ¥3.33 的资产：房价涨 10%，你的净值涨约 33%；跌 10% 也一样。杠杆放大的是两侧。", formula="杠杆倍数 = 房价 ÷ 自有资金" },
            new CardDef { id="housing.installment", title="月供构成", body="每期月供＝偿还本金＋支付利息。等额本息下月供总额固定，但早期几乎都在付利息，本金占比逐期上升。", formula="月供 = 本金部分 + 利息部分" },
            new CardDef { id="housing.annuity", title="等额本息", body="把贷款本息摊成每期等额的年金。三十年间你付的利息总额可能接近本金——期限是利息的放大器。", formula="月供 = 本金 × r(1+r)^n ÷ ((1+r)^n − 1)" },
            new CardDef { id="housing.ltv", title="贷款价值比 LTV", body="贷款余额 ÷ 抵押物估值。LTV 越高，房价小幅下跌就可能吞掉全部净值；银行用 LTV 上限控制自己的敞口。", formula="LTV = 贷款余额 ÷ 抵押物估值" },
            new CardDef { id="housing.collateral", title="抵押物", body="房贷的房子本身就是抵押物：还不起时银行可以法拍收回。有抵押的贷款利率更低，因为出借人的下行有底。", formula=null },
            new CardDef { id="housing.amortization", title="摊销", body="摊销表列出每期有多少在还本、多少在付息。提前还款省的是「后面那些期的利息」，这正是它划算的原因。", formula="利息 = 剩余本金 × 期利率" },
            new CardDef { id="housing.rentVsBuy", title="租房还是买房", body="租房付的是灵活性溢价：随时能走。买房把存款与未来收入流锁进一个资产。正确的比较口径是机会成本，不是「租金白交了」。", formula=null },
            new CardDef { id="housing.priceBreakdown", title="房价分解", body="房价变动＝需求、供给、信贷与动能的合力。看分解而不是只看涨跌，你才知道下一期可能怎么走。", formula="Δ价 = 房价 × 6% × 失衡度（每期钳 ±3%）" },
            new CardDef { id="housing.supplyElasticity", title="供给弹性", body="住房供给越难扩张（土地、审批、施工周期），需求冲击就越多地变成价格上涨而不是数量增加。", formula=null },
            new CardDef { id="housing.capRate", title="租金资本化率", body="房价与年租金之比是房地产的「市盈率」。租金跟不上房价时，要么租金补涨，要么房价回落——总有一头要动。", formula="资本化率 ≈ 年租金 ÷ 房价" },
            new CardDef { id="housing.wealthEffect", title="财富效应", body="房价上涨让有房者觉得自己更有钱，消费随之增加；无房者面对的却是更贵的门槛——同一场上涨，两种心情。", formula=null },
            new CardDef { id="housing.nominalReal", title="名义与实际", body="名义房价涨 8%、CPI 涨 5%，真实涨幅只有约 3%。涨过 CPI 的才是真涨——住房页签的双指数就在提醒这一点。", formula="实际涨幅 ≈ 名义涨幅 − 通胀率" },
            new CardDef { id="housing.bubbleThree", title="泡沫三征", body="价格脱离租金（估值征）、涨速陡峭（动能征）、信贷扩张（融资征）。三征齐备时，崩盘不需要理由，只需要一根针。", formula=null },
            new CardDef { id="housing.minsky", title="明斯基时刻", body="稳定本身孕育不稳定：越久不崩，越多人敢上加杠杆，直到最脆弱的现金流断掉——那一刻所有人同时变成卖家。", formula=null },
            new CardDef { id="housing.deleveraging", title="去杠杆与衰退", body="资产跌、净值缩、银行收贷、抛售再压价。去杠杆是加杠杆的倒放，速度却快得多——衰退常常由此而来。", formula=null },
            new CardDef { id="asset.allocation", title="资产配置", body="把储蓄放进存款、国债与指数基金三档。决定长期结果的通常不是选中哪一次，而是三者的比例。", formula=null },
            new CardDef { id="asset.riskReturn", title="风险收益权衡", body="更高的预期收益总要付出代价：波动、锁定期或违约可能。不存在收益最高、风险最低又随取随用的资产。", formula=null },
            new CardDef { id="asset.liquidity", title="流动性", body="变现的速度与折价。存款随时可取，国债看期限，基金赎回有价差——流动性差的资产必须用收益补偿你。", formula=null },
            new CardDef { id="asset.indexFund", title="指数基金", body="买整个市场而不是挑个股：分散掉个股风险，留下整体市场的收益。费用低，是因为没人需要替你「精选」。", formula=null },
            new CardDef { id="asset.transactionCost", title="交易成本", body="申赎价差、税费与时间——每次换手都在磨损收益。交易越频繁，成本的复利越惊人。", formula="赎回到手 = 份额 × 净值 × (1 − 价差)" },
            new CardDef { id="asset.treasury", title="国债与无风险利率", body="政府以税收权背书的借款，是全镇利率的地基：其他资产的收益，都要在它之上加风险溢价。", formula=null },
        };

        static readonly Dictionary<string, CardDef> map = new Dictionary<string, CardDef>();
        static CardLibrary() { for (int i = 0; i < All.Count; i++) map[All[i].id] = All[i]; }
        public static CardDef Get(string id) { CardDef d; return map.TryGetValue(id, out d) ? d : null; }

        /// <summary>每日结算后检查所有解锁条件。</summary>
        public static void CheckAll(GameState s)
        {
            DayRecord r = s.LastRecord;
            if (r == null) return;
            DayRecord y = s.Yesterday;

            s.UnlockCard("scarcity");
            s.UnlockCard("demand");
            s.UnlockCard("revenue");
            s.UnlockCard("profit");

            if (s.day >= 2) s.UnlockCard("oppcost");
            if (s.CanProduction && r.planned > 0) s.UnlockCard("supply");
            if (s.CanMaterials) { s.UnlockCard("fixedcost"); s.UnlockCard("variablecost"); }
            if (s.day >= 4)
            {
                s.UnlockCard("marginalcost");
                s.UnlockCard("averagecost");
            }
            if (s.CanEmployees && s.employees >= 2) s.UnlockCard("marginalproduct");
            if (s.employees >= 3) s.UnlockCard("diminishing");
            if (s.day >= 5) s.UnlockCard("elasticity");
            if (s.day >= 6 && DistinctPrices(s) >= 4) s.UnlockCard("equilibrium");
            if (s.day >= 8 && DistinctPrices(s) >= 6) s.UnlockCard("profitmax");

            if (r.competitionActive)
            {
                s.UnlockCard("imperfectinfo");
                if (s.day >= 17) s.UnlockCard("marketshare");
                if (s.day >= 18 || r.switchedToCompetitor > 0) s.UnlockCard("substitute");
                if (s.playerQualityLevel > 1 || s.day >= 19) s.UnlockCard("differentiation");
                if (s.day >= 19) s.UnlockCard("monopolisticcompetition");
                if (s.playerBrand >= 55f) s.UnlockCard("marketpower");
                if (r.hasCrossPriceElasticity || ValidCompetitionPriceObservations(s) >= 2)
                    s.UnlockCard("crosselasticity");
                if (r.priceWar)
                {
                    s.UnlockCard("pricewar");
                    if (s.day >= 22) { s.UnlockCard("gametheory"); s.UnlockCard("prisonersdilemma"); }
                }
                // 纳什均衡是对多次策略实验的归纳，不再只按天数自动发放。
                if (s.CanStrategyLab && s.CompletedMarketExperiments >= 2) s.UnlockCard("nashequilibrium");
                if ((s.marketExperimentMask & 1) != 0) s.UnlockCard("perfectcompetition");
                if ((s.marketExperimentMask & 2) != 0) s.UnlockCard("oligopoly");
                if ((s.marketExperimentMask & 4) != 0)
                {
                    s.UnlockCard("monopoly"); s.UnlockCard("consumersurplus"); s.UnlockCard("producersurplus");
                }
            }

            s.Changed();
        }

        static int DistinctPrices(GameState s)
        {
            HashSet<float> set = new HashSet<float>();
            for (int i = 0; i < s.history.Count; i++) set.Add(s.history[i].price);
            return set.Count;
        }

        static int ValidCompetitionPriceObservations(GameState s)
        {
            HashSet<string> pricePairs = new HashSet<string>();
            for (int i = 0; i < s.history.Count; i++)
            {
                DayRecord record = s.history[i];
                if (!record.competitionActive || record.desiredDemand <= 0 || record.competitorDemand <= 0) continue;
                if (!string.IsNullOrEmpty(record.eventName) || !string.IsNullOrEmpty(record.competitionEventName)) continue;
                pricePairs.Add(Mathf.RoundToInt(record.price * 10f) + ":" +
                    Mathf.RoundToInt(record.competitorPrice * 10f));
            }
            return pricePairs.Count;
        }
    }
}
