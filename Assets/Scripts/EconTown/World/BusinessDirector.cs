using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// 营业时段导演:把已算好的当日需求“演”给玩家看(NPC 排队、+1 飘字、实时计数),
    /// 结束后调用 DaySimulator.Settle 结算。
    /// </summary>
    public class BusinessDirector : MonoBehaviour
    {
        public static BusinessDirector I { get; private set; }

        public bool Running { get; private set; }
        public int ServedSoFar { get; private set; }
        public int DemandTotal { get; private set; }
        public int Produced { get; private set; }
        public int Available { get; private set; }
        public float Progress01 { get { return Mathf.Clamp01(elapsed / duration); } }

        public event System.Action OnLive;
        public event System.Action<DayRecord> OnFinished;

        float duration = BusinessDurationSeconds;
        float elapsed;
        DaySimulator.DemandRoll demand;
        CompetitionSimulator.MarketRoll market;
        bool competitionDay;
        int serveTarget;
        int visualQueued;
        int competitorVisualQueued;
        int competitorServeTarget;
        float nextVisualSpawnAt;
        int visualSequence;
        readonly List<NpcAgent> activeVisualCustomers = new List<NpcAgent>();

        public const float BusinessDurationSeconds = 28f;
        public const int VisualCustomerLimit = 1;
        public const float MinimumVisualSpawnInterval = 4.2f;
        public const float MaximumVisualSpawnInterval = 5.4f;

        public static BusinessDirector Create()
        {
            GameObject go = new GameObject("BusinessDirector");
            return go.AddComponent<BusinessDirector>();
        }

        void Awake() { I = this; }

        public void Begin()
        {
            ClearVisualCustomers();
            GameState s = GameState.I;
            competitionDay = s.CompetitionActive;
            if (competitionDay)
            {
                market = CompetitionSimulator.RollMarket(s);
                demand = market.player;
            }
            else demand = DaySimulator.RollDemand(s);
            Produced = DaySimulator.Production(s);
            Available = Produced + s.inventory;
            DemandTotal = demand.Total;
            serveTarget = Mathf.Min(DemandTotal, Available);
            elapsed = 0f;
            ServedSoFar = 0;
            visualQueued = 0;
            competitorVisualQueued = 0;
            visualSequence = 0;
            nextVisualSpawnAt = 0.8f;
            competitorServeTarget = competitionDay
                ? Mathf.Min(market.competitor.Total, s.competitor.plannedProduction + s.competitor.inventory)
                : 0;
            Running = true;
            s.phase = GamePhase.Business;
            s.Changed();
            if (OnLive != null) OnLive();
        }

        public void SkipToEnd() { if (Running) elapsed = duration; }

        void Update()
        {
            if (!Running) return;
            elapsed += Time.deltaTime;
            float k = Progress01;

            int shouldServed = Mathf.RoundToInt(serveTarget * Mathf.SmoothStep(0f, 1f, k));
            if (shouldServed != ServedSoFar)
            {
                ServedSoFar = shouldServed;
                if (OnLive != null) OnLive();
            }

            if (k >= 1f)
            {
                Running = false;
                Finish();
                return;
            }

            // 视觉顾客只代表营业节奏，不再按实际销量逐个生成。
            // 全场只保留一位代表性顾客，抵达后再等待一段时间补下一位。
            PruneVisualCustomers();
            if (elapsed >= nextVisualSpawnAt && activeVisualCustomers.Count < VisualCustomerLimit)
            {
                bool canVisitPlayer = visualQueued < Mathf.Min(serveTarget, 5);
                bool canVisitRival = competitionDay &&
                    competitorVisualQueued < Mathf.Min(competitorServeTarget, 3);
                // 围猎三#13:玩家在港区/店内时 TownRoot 隐藏、建筑碰撞体不参与物理查询,
                // 出生点避障全盲的顾客会生成在建筑里——回镇前跳过生成与计数(计数照加会把生成闸烧空)。
                bool townHidden = PortSceneController.IsInsidePort || ShopInteriorController.IsInsideShopInterior;
                if (!townHidden && (canVisitPlayer || canVisitRival))
                {
                    bool rival = canVisitRival && (!canVisitPlayer || visualSequence % 3 == 2);
                    NpcAgent customer = SpawnCustomer(rival);
                    if (customer != null) activeVisualCustomers.Add(customer);
                    if (rival) competitorVisualQueued++;
                    else visualQueued++;
                    visualSequence++;
                }
                nextVisualSpawnAt = elapsed + Random.Range(MinimumVisualSpawnInterval, MaximumVisualSpawnInterval);
            }
        }

        NpcAgent SpawnCustomer(bool rival)
        {
            string type = "白领";
            float r = Random.value;
            int total = Mathf.Max(1, demand.Total);
            float sShare = (float)demand.students / total;
            float wShare = (float)demand.whitecollars / total;
            if (r < sShare) type = "学生";
            else if (r < sShare + wShare) type = "白领";
            else type = "游客";

            Vector3 destination = rival ? MapBuilder.CompetitorDoor : MapBuilder.ShopDoor;
            Vector3 from = TownLayoutData.StreetActivityPoints[
                Random.Range(0, TownLayoutData.StreetActivityPoints.Length)];
            NpcAgent n = NpcAgent.Spawn(type, from, dialogue: false);
            n.SendBuying(destination);
            return n;
        }

        void PruneVisualCustomers()
        {
            for (int i = activeVisualCustomers.Count - 1; i >= 0; i--)
                if (activeVisualCustomers[i] == null) activeVisualCustomers.RemoveAt(i);
        }

        void ClearVisualCustomers()
        {
            for (int i = 0; i < activeVisualCustomers.Count; i++)
                if (activeVisualCustomers[i] != null) Destroy(activeVisualCustomers[i].gameObject);
            activeVisualCustomers.Clear();
        }

        void Finish()
        {
            ClearVisualCustomers();
            GameState s = GameState.I;
            DayRecord r = competitionDay
                ? CompetitionSimulator.Settle(s, market, Produced)
                : DaySimulator.Settle(s, demand, Produced);
            CardLibrary.CheckAll(s);
            s.phase = GamePhase.Report;
            s.Changed();
            if (OnFinished != null) OnFinished(r);
        }
    }
}
