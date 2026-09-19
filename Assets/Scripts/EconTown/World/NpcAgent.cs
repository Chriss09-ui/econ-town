using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>小镇上的 NPC:闲逛、营业时段排队买果汁、可对话。</summary>
    public class NpcAgent : MonoBehaviour
    {
        public static readonly List<NpcAgent> All = new List<NpcAgent>();

        public string TypeName { get; private set; }
        public bool IsDialogueTarget { get; private set; }

        /// <summary>V0.7.1 街景氛围补回的临时漫步者:不计入对话目标(无 [E] 提示,PRD §124),
        /// 但计入街景人数仪表的"在场人数"统计——否则执行器永远认为人不够,会无限刷人。</summary>
        public bool IsAmbientStroller { get; private set; }

        Vector3 target;
        float retargetAt;
        float walkSpeed = 1.3f;
        bool buying;
        bool usesTownActivityNetwork = true;
        Vector3 queueSpot;
        Rigidbody2D body;

        static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>
        {
            { "学生", new string[] {
                "学生:一杯果汁对我来说有点小贵……降价的话我天天来!",
                "学生:今天学校有活动,同学们都想喝点凉的。",
                "学生:隔壁镇奶茶卖 12 元,你们家 15 元了。" } },
            { "白领", new string[] {
                "白领:上班前来一杯,15 元左右都能接受。",
                "白领:比起价格,我更在意排队时间长不长。",
                "白领:今天加班,想买两杯,你家还有货吗?" } },
            { "游客", new string[] {
                "游客:听说这是小镇最有名的果汁店!",
                "游客:来旅游就不在乎价格啦,好喝就行。",
                "游客:节日的时候小镇特别热闹。" } },
            { "工厂主", new string[] {
                "工厂主:订单和岗位都很重要，治理污染也需要给企业调整时间。",
                "工厂主:环保设备不便宜，但稳定的政策比突然变化更容易安排。" } },
            { "居民代表", new string[] {
                "居民代表:公共服务大家都用得到，可预算也得让所有人看明白。",
                "居民代表:我们关心物价，也关心买不买得到。" } },
        };

        // V0.9.7:漫游边界实例化——店内顾客用房间矩形,默认取小镇全域(字段初始化即默认值)
        float wanderMinX = MapBuilder.NpcWanderMinX, wanderMaxX = MapBuilder.NpcWanderMaxX;
        float wanderMinY = MapBuilder.NpcWanderMinY, wanderMaxY = MapBuilder.NpcWanderMaxY;

        public static NpcAgent Spawn(string typeName, Vector3 pos, bool dialogue = true, bool ambient = false,
            Transform parentOverride = null, Vector4 bounds = default(Vector4))
        {
            // 出生点避开建筑内部:物理化后生成在墙里的 NPC 会被持续推出/卡住(P2-9)。
            // V0.9.6g 起建筑挂 isTrigger 的点击碰撞体——查询必须忽略 trigger(否则站在
            // 建筑附近的合法出生点被误判「有墙」),店内顾客(bounds 有值)在房间矩形内重试。
            bool useBounds = bounds != default(Vector4);
            float minX = useBounds ? bounds.x : MapBuilder.NpcWanderMinX;
            float maxX = useBounds ? bounds.z : MapBuilder.NpcWanderMaxX;
            float minY = useBounds ? bounds.y : MapBuilder.NpcWanderMinY;
            float maxY = useBounds ? bounds.w : MapBuilder.NpcWanderMaxY;
            for (int attempt = 0; attempt < 8 && SpawnPointBlocked(pos); attempt++)
                pos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0);
            GameObject go = new GameObject("NPC_" + typeName);
            go.transform.position = pos;
            // NPC 默认归属小镇场景根；进入独立港区/店内(V0.9.7)时随 Town 一起隐藏，不会穿帮进另一张世界根。
            // 店内顾客经 parentOverride 挂店内根(随店显隐),bounds 用房间矩形约束漫游。
            if (parentOverride != null) go.transform.SetParent(parentOverride, true);
            else if (MapBuilder.TownRoot != null) go.transform.SetParent(MapBuilder.TownRoot, true);
            // 动态刚体 + 小圆碰撞:与建筑/玩家产生物理推挤并沿墙滑动,不再直接写 transform 穿墙(P2-9)。
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.34f;
            col.offset = new Vector2(0f, 0.34f);
            NpcAgent n = go.AddComponent<NpcAgent>();
            n.TypeName = typeName;
            n.IsDialogueTarget = dialogue;
            n.IsAmbientStroller = ambient;
            n.usesTownActivityNetwork = !useBounds;
            if (bounds != default(Vector4))
            {
                n.wanderMinX = bounds.x; n.wanderMinY = bounds.y;
                n.wanderMaxX = bounds.z; n.wanderMaxY = bounds.w;
            }
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            // 初始帧直挂:SpriteSwapper.Awake 在 EditMode 的 AddComponent 时机不可靠,
            // 无头渲染(EditMode 出图)里不设初始帧角色就是空的。
            PixelArt.CharacterFrames frames = PixelArt.Character(typeName);
            if (frames.down != null && frames.down.Length > 0) sr.sprite = frames.down[0];
            GameObject shadowGo = new GameObject("shadow");
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            SpriteRenderer shadow = shadowGo.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelArt.CharacterShadow();
            shadow.sortingOrder = MapBuilder.SortingForY(pos.y) + 1;
            SpriteSwapper sw = go.AddComponent<SpriteSwapper>();
            sw.SetFrames(frames);
            n.PickTarget();
            All.Add(n);
            return n;
        }

        /// <summary>出生点是否被实体碰撞体占住——isTrigger 的点击碰撞体(V0.9.6g)不算,
        /// 否则建筑附近的所有合法出生点都被误判(实际事故:店内顾客被银行点击触发器赶跑)。</summary>
        static bool SpawnPointBlocked(Vector2 pos)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(pos);
            for (int i = 0; i < hits.Length; i++)
                if (!hits[i].isTrigger) return true;
            return false;
        }

        void PickTarget()
        {
            // 目标点不得落在建筑内部:物理化后朝墙内走会原地推挤到换目标为止(P2-9)。
            // V0.9.7:边界实例化——店内顾客取房间矩形,不撞世界围墙。
            // 与出生点同口径:忽略 isTrigger 的点击碰撞体(否则店内初次选点被 Town
            // 激活着的银行触发器盖掉近半房间)。
            Vector3 candidate = TargetCandidate();
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (!SpawnPointBlocked(candidate)) break;
                candidate = TargetCandidate();
            }
            target = candidate;
            retargetAt = Time.time + Random.Range(3f, 7f);
        }

        Vector3 TargetCandidate()
        {
            if (!usesTownActivityNetwork)
                return new Vector3(Random.Range(wanderMinX, wanderMaxX),
                    Random.Range(wanderMinY, wanderMaxY), 0f);

            Vector3[] points = TownLayoutData.StreetActivityPoints;
            int start = Random.Range(0, points.Length);
            Vector3 nearest = points[start];
            float nearestDistance = (nearest - transform.position).sqrMagnitude;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 point = points[(start + i) % points.Length];
                float distance = (point - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = point;
                    nearestDistance = distance;
                }
                // 只挑附近公共空间，避免 NPC 为跨城直线折返长时间顶着院墙。
                if (distance >= 1f && distance <= 225f) return point;
            }
            return nearest;
        }

        void Update()
        {
            if (buying)
            {
                // 购买顾客保持自然步速到店，不再像人群冲刺一样涌向门口。
                Steer(queueSpot, walkSpeed * 1.4f);
                if ((transform.position - queueSpot).sqrMagnitude < 0.04f)
                {
                    FloatText.Pop(queueSpot + new Vector3(0, 0.6f, 0), "+1", new Color32(0x2e,0x7d,0x32,255));
                    Destroy(gameObject);
                }
                return;
            }

            Steer(target, walkSpeed);
            if (Time.time > retargetAt || (transform.position - target).sqrMagnitude < 0.05f) PickTarget();
        }

        void Steer(Vector3 destination, float speed)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
                return;
            }
            Vector2 to = (Vector2)(destination - transform.position);
            body.linearVelocity = to.sqrMagnitude > 0.01f ? to.normalized * speed : Vector2.zero;
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        public string RandomLine()
        {
            GameState state = GameState.I;
            if (state != null && state.currentView == PlayerViewMode.Government)
            {
                GovernmentSimulator.Ensure(state);
                GovernmentState government = state.government;
                if (TypeName == "学生")
                    return "学生代表:培训与公共服务让我受益，目前学生支持率 " + government.studentSupport.ToString("0") + "% 。";
                if (TypeName == "工厂主")
                    return "工厂主:税、工资和环保要求都会进入成本，目前企业支持率 " + government.businessSupport.ToString("0") + "% 。";
                if (TypeName == "居民代表")
                    return "居民代表:服务改善值得欢迎，但也要说明谁付钱。目前居民支持率 " + government.residentSupport.ToString("0") + "% 。";
                if (TypeName == "游客")
                    return "游客:环境质量 " + government.environmentQuality.ToString("0") + "，会影响我是否愿意再来。";
                if (!string.IsNullOrEmpty(government.lastNpcFeedback)) return government.lastNpcFeedback;
            }
            string[] arr;
            if (lines.TryGetValue(TypeName, out arr)) return arr[Random.Range(0, arr.Length)];
            return "……";
        }

        /// <summary>营业时段:派他去商店门口“买一杯”。</summary>
        public void SendBuying(Vector3 queuePos)
        {
            buying = true;
            IsDialogueTarget = false;
            // 围猎三(#10):y 散布只向门点下方取——门点=建筑碰撞带下沿-0.5,旧的正负散布约有
            // 一半概率把 queueSpot 落进碰撞带内(NPC 从任何方向都进不了 0.2 到达半径,贴墙
            // 站桩永不消失,VisualCustomerLimit=1 锁死整场营业演出)。NPC 碰撞体占位 0.68,
            // 向下 0.6–1.0 保证目标点距碰撞带下沿 ≥1.1。competitor 门点同公式自动生效。
            queueSpot = queuePos + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-1.0f, -0.6f), 0);
        }
    }

    /// <summary>世界中飘字(+1 / 提示)。</summary>
    public static class FloatText
    {
        public static void Pop(Vector3 worldPos, string text, Color color)
        {
            GameObject go = new GameObject("float");
            go.transform.position = worldPos;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UiTheme.GameFont;
            tm.fontSize = 30;
            tm.characterSize = 0.42f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = color;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 20;
            Runner.Create(go.transform, color);
        }

        class Runner : MonoBehaviour
        {
            Transform tf; TextMesh tm; float t; Color c0;
            public static void Create(Transform t, Color c)
            {
                Runner r = t.gameObject.AddComponent<Runner>();
                r.tf = t; r.tm = t.GetComponent<TextMesh>(); r.c0 = c;
            }
            void Update()
            {
                t += Time.deltaTime;
                tf.position += Vector3.up * 0.9f * Time.deltaTime;
                Color c = c0; c.a = Mathf.Clamp01(1f - t / 1.1f);
                if (tm != null) tm.color = c;
                if (t > 1.1f) Destroy(gameObject);
            }
        }
    }
}
