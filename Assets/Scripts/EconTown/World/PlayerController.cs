#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace EconTown
{
    /// <summary>玩家移动(WASD/方向键)与交互(E)。</summary>
    public class PlayerController : MonoBehaviour
    {
        public float speed = 4.2f;
        Rigidbody2D rb;

        void Awake()
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.6f, 0.8f);
            col.offset = new Vector2(0f, 0.5f);

            SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            AddShadow();
            SpriteSwapper sw = gameObject.AddComponent<SpriteSwapper>();
            sw.SetFrames(PixelArt.Character("player"));
        }

        void AddShadow()
        {
            GameObject go = new GameObject("shadow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            SpriteRenderer shadow = go.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelArt.CharacterShadow();
            shadow.sortingOrder = MapBuilder.SortingForY(transform.position.y) + 1;
        }

        void FixedUpdate()
        {
            if (UIManager.I != null && UIManager.I.InputBlocked) { rb.linearVelocity = Vector2.zero; return; }
            Vector2 mv = ReadMove();
            rb.linearVelocity = mv * speed;
        }

        static Vector2 ReadMove()
        {
            float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            x = Input.GetAxisRaw("Horizontal");
            y = Input.GetAxisRaw("Vertical");
#endif
            Vector2 v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        public static bool ConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        public static bool CancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        /// <summary>演出打断信号(任意移动/Esc/E,PRD §121/§134/§142)。
        /// 项目的 activeInputHandler=Input System,裸调 legacy Input.GetAxisRaw 会每帧抛
        /// InvalidOperationException(2026-08-28 Play 实测炸掉三个导演的 Update),统一走本入口。</summary>
        public static bool WantsControlInput()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            if (k.aKey.isPressed || k.dKey.isPressed || k.wKey.isPressed || k.sKey.isPressed ||
                k.leftArrowKey.isPressed || k.rightArrowKey.isPressed ||
                k.upArrowKey.isPressed || k.downArrowKey.isPressed)
                return true;
            return k.escapeKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f ||
                Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        /// <summary>EventSystem 当前聚焦的是否为文本输入框(legacy InputField 或 TMP_InputField)。</summary>
        public static bool TextInputFocused()
        {
            return TextInputFocused(EventSystem.current);
        }

        /// <summary>可注入 EventSystem 实例的重载,供 EditMode 测试使用(EditMode 下 current 为 null)。</summary>
        public static bool TextInputFocused(EventSystem es)
        {
            if (es == null) return false;
            GameObject selected = es.currentSelectedGameObject;
            return selected != null &&
                (selected.GetComponent<InputField>() != null || selected.GetComponent<TMP_InputField>() != null);
        }

        string lastPrompt;

        void Update()
        {
            if (UIManager.I == null) return;

            if (UIManager.I.InputBlocked)
            {
                // V0.7.4 过场播放中:Esc/跳过只归 CutsceneDirector(§149 两路口径),
                // 这里不得用 CloseAll 摧毁过场层——那会绕过「视为已看」的完成回调。
                if (UIManager.I.CutsceneActive) return;
                bool cancel = CancelPressed();
                bool confirm = !cancel && ConfirmPressed();
                if (!cancel && !confirm) return;
                // 输入框聚焦时,E(拼音输入必含)与 Esc(退出输入法/取消聚焦)都属于文本编辑,
                // 直读键盘的关面板逻辑必须放行——否则整个面板连同已输入的内容一起被销毁(P1-3)。
                if (TextInputFocused()) return;
                // Esc 作为全局脱困键，一次清掉可能叠加或隐藏的面板；E 只关闭顶层。
                if (cancel) UIManager.I.CloseAll();
                else if (UIManager.I.IsAnyPanelOpen) UIManager.I.CloseTop();
                return;
            }

            // 找最近的交互对象:建筑门 / NPC(放在 InputBlocked 之后——面板打开期间的
            // 全量距离扫描是白做的(P2-12))
            Interactable best = null; float bestD = 1.7f * 1.7f;
            WorldArea currentArea = ShopInteriorController.IsInsideShopInterior ? WorldArea.ShopInterior :
                PortSceneController.IsInsidePort ? WorldArea.Port : WorldArea.Town;
            for (int i = 0; i < Interactables.All.Count; i++)
            {
                if (Interactables.All[i].area != currentArea) continue;
                float d = (Interactables.All[i].position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = Interactables.All[i]; }
            }

            NpcAgent bestNpc = null; bestD = 1.3f * 1.3f;
            for (int i = 0; currentArea == WorldArea.Town && i < NpcAgent.All.Count; i++)
            {
                NpcAgent n = NpcAgent.All[i];
                if (n == null || !n.IsDialogueTarget) continue;
                float d = (n.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; bestNpc = n; }
            }

            // prompt 只在目标变化时重建:站在交互物旁每帧拼接字符串是稳定的 GC 源(P2-12)。
            string prompt = null;
            if (bestNpc != null && best == null)
            {
                prompt = "[E] 和" + bestNpc.TypeName + "聊聊";
                if (prompt != lastPrompt) { lastPrompt = prompt; UIManager.I.ShowPrompt(prompt); }
                if (ConfirmPressed()) UIManager.I.ShowToast(bestNpc.RandomLine());
            }
            else if (best != null)
            {
                prompt = "[E] " + best.label;
                if (prompt != lastPrompt) { lastPrompt = prompt; UIManager.I.ShowPrompt(prompt); }
                if (ConfirmPressed() && best.onUse != null) best.onUse();
            }
            else if (lastPrompt != null) { lastPrompt = null; UIManager.I.ShowPrompt(null); }
        }
    }
}
