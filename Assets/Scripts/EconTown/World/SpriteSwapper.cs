using UnityEngine;

namespace EconTown
{
    /// <summary>角色动画:站立帧 + 两帧行走,按方向切换下/上/侧。</summary>
    public class SpriteSwapper : MonoBehaviour
    {
        public Sprite[] down, up, side;
        SpriteRenderer sr;
        SpriteRenderer shadow;
        float timer;
        int walkToggle;
        Vector3 lastPos;
        Sprite[] lastSet;
        Sprite lastShown;
        int lastSortingOrder = int.MinValue;

        public void SetFrames(PixelArt.CharacterFrames f)
        {
            down = f.down; up = f.up; side = f.side;
            lastSet = down;
            if (sr != null && down != null && down.Length > 0) sr.sprite = down[0];
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            Transform shadowTransform = transform.Find("shadow");
            if (shadowTransform != null) shadow = shadowTransform.GetComponent<SpriteRenderer>();
            lastPos = transform.position;
        }

        void Update()
        {
            if (sr == null || down == null || down.Length < 2) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            Vector2 v = (Vector2)(transform.position - lastPos) / dt;
            lastPos = transform.position;
            bool moving = v.sqrMagnitude > 0.02f;

            Sprite[] set = lastSet ?? down;
            int frame = 0;
            if (moving)
            {
                set = Mathf.Abs(v.x) > Mathf.Abs(v.y) ? side : (v.y > 0f ? up : down);
                lastSet = set;
                timer += Time.deltaTime;
                if (timer > 0.18f)
                {
                    timer = 0f;
                    walkToggle = 1 - walkToggle;
                }
                frame = set.Length >= 3 ? walkToggle + 1 : walkToggle;
                if (v.x < -0.02f) sr.flipX = true;
                else if (v.x > 0.02f) sr.flipX = false;
            }
            else timer = 0f;

            if (set == null || set.Length == 0 || frame < 0 || frame >= set.Length || set[frame] == null)
            {
                set = down;
                frame = Mathf.Clamp(frame, 0, down.Length - 1);
            }
            // 只在值真正变化时写渲染器:13 个角色每帧无条件写 sprite/sortingOrder
            // 是稳定的原生调用开销(P2-12)。
            if (set != null && frame < set.Length && set[frame] != null && set[frame] != lastShown)
            {
                lastShown = set[frame];
                sr.sprite = lastShown;
            }
            int order = MapBuilder.SortingForY(transform.position.y) + 2;
            if (order != lastSortingOrder)
            {
                lastSortingOrder = order;
                sr.sortingOrder = order;
                if (shadow != null) shadow.sortingOrder = order - 1;
            }
        }
    }
}
