using UnityEngine;
using UnityEngine.SceneManagement;

namespace EconTown
{
    /// <summary>
    /// V0.9.7 小镇与果汁店店内的场景门(照 PortSceneController 范式):门口按 E 进店,
    /// 店内门垫按 E/点击出店。两边共用 GameState/UI/玩家,世界根与交互互斥;
    /// Play 模式下店内根移入独立运行时 Scene。营业纯自动(BusinessDirector 纯计时),
    /// 玩家在不在店内都不影响结算;视觉顾客/NPC 挂 TownRoot 下,进店时随 Town 隐藏。
    /// 与港区互斥:Enter 时若 IsInsidePort 直接拒绝(两把场景钥匙不同时持有)。
    /// V0.9.11 泛化为 per-kind 店内门:EnterInterior(kind) 按 InteriorCatalog 激活对应子房间
    /// (同刻只亮一间),底色/toast/返回兜底门点全部随 kind;EnterShopInterior 旧壳转调 shop。
    /// </summary>
    public sealed class ShopInteriorController : MonoBehaviour
    {
        const string RuntimeSceneName = "EconTown_ShopInterior_Runtime";

        public static ShopInteriorController I { get; private set; }
        public static bool IsInsideShopInterior => I != null && I.inside;
        /// <summary>当前店内的 kind(未进店时保持上一次值,默认 shop——旧锚点属性按它查表)。</summary>
        public static string CurrentKind => MapBuilder.ActiveInteriorKind;

        Transform player;
        CameraFollow cameraFollow;
        Camera worldCamera;
        Transform townRoot;
        Transform interiorRoot;
        Vector3 townReturnPosition;
        Color townBackground;
        Scene townScene;
        Scene interiorScene;
        bool inside;

        public static void Create(Transform playerTransform, CameraFollow follow, Camera camera)
        {
            if (I != null)
            {
                I.Configure(playerTransform, follow, camera);
                return;
            }
            GameObject go = new GameObject("ShopInteriorController");
            ShopInteriorController controller = go.AddComponent<ShopInteriorController>();
            I = controller;
            controller.Configure(playerTransform, follow, camera);
        }

        /// <summary>V0.9.11:进任意店内(目录外 kind 拒绝)。门口按 E 与点击建筑共用。</summary>
        public static void EnterInterior(string kind)
        {
            if (I != null) I.EnterInternal(kind);
        }

        /// <summary>V0.9.11:走出当前店(门垫按 E/点击共用,不分店)。</summary>
        public static void ExitInterior()
        {
            if (I != null) I.ExitInternal();
        }

        public static void EnterShopInterior()
        {
            EnterInterior("shop");
        }

        public static void ExitShopInterior()
        {
            ExitInterior();
        }

        void Awake()
        {
            I = this;
        }

        void Configure(Transform playerTransform, CameraFollow follow, Camera camera)
        {
            player = playerTransform;
            cameraFollow = follow;
            worldCamera = camera;
            townRoot = MapBuilder.TownRoot;
            interiorRoot = MapBuilder.ShopInteriorRoot;
            townBackground = worldCamera != null ? worldCamera.backgroundColor : Color.black;
            if (townRoot != null) townScene = townRoot.gameObject.scene;

            if (interiorRoot != null)
            {
                interiorRoot.gameObject.SetActive(false);
                MoveInteriorRootToRuntimeScene();
            }
        }

        void MoveInteriorRootToRuntimeScene()
        {
            if (!Application.isPlaying || interiorRoot == null) return;
            interiorScene = SceneManager.GetSceneByName(RuntimeSceneName);
            if (!interiorScene.IsValid()) interiorScene = SceneManager.CreateScene(RuntimeSceneName);
            if (interiorRoot.gameObject.scene == interiorScene) return;
            interiorRoot.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(interiorRoot.gameObject, interiorScene);
        }

        void EnterInternal(string kind)
        {
            if (inside || player == null || townRoot == null || interiorRoot == null) return;
            if (!InteriorCatalog.Exists(kind)) return;                          // 目录外 kind 拒绝
            if (PortSceneController.IsInsidePort) return;                      // 与港区互斥
            if (UIManager.I != null && UIManager.I.InputBlocked) return;

            InteriorSpec spec = InteriorCatalog.Get(kind);
            townReturnPosition = player.position;
            inside = true;
            MapBuilder.SetActiveInteriorKind(kind);
            townRoot.gameObject.SetActive(false);
            interiorRoot.gameObject.SetActive(true);
            // 同刻只亮一间:根下其余子房间保持隐藏(坐标网格错开,同亮会互相叠印)
            for (int i = 0; i < InteriorCatalog.All.Length; i++)
            {
                Transform room = MapBuilder.InteriorRoom(InteriorCatalog.All[i].kind);
                if (room != null) room.gameObject.SetActive(InteriorCatalog.All[i].kind == kind);
            }
            TeleportPlayer(spec.EntryWorld);
            if (Application.isPlaying && interiorScene.IsValid()) SceneManager.SetActiveScene(interiorScene);
            // 底色随店:房间比相机窗口窄,四周露出的底色读作「盒子视图」的暗框
            if (worldCamera != null) worldCamera.backgroundColor = spec.backgroundColor;
            if (cameraFollow != null) cameraFollow.SnapToTarget();
            if (UIManager.I != null)
            {
                UIManager.I.CloseAll();
                UIManager.I.ShowPrompt(null);
                UIManager.I.ShowToast(spec.enterToast);
            }
        }

        void ExitInternal()
        {
            if (!inside || player == null || townRoot == null || interiorRoot == null) return;
            if (UIManager.I != null && UIManager.I.InputBlocked) return;

            InteriorSpec spec = InteriorCatalog.Get(MapBuilder.ActiveInteriorKind);
            inside = false;
            interiorRoot.gameObject.SetActive(false);
            townRoot.gameObject.SetActive(true);
            Vector3 returnPosition = townReturnPosition == Vector3.zero
                ? spec.door() + new Vector3(0f, -.8f, 0f) : townReturnPosition;   // 返回兜底=本店小镇门点下方
            TeleportPlayer(returnPosition);
            if (Application.isPlaying && townScene.IsValid()) SceneManager.SetActiveScene(townScene);
            if (worldCamera != null) worldCamera.backgroundColor = townBackground;
            if (cameraFollow != null) cameraFollow.SnapToTarget();
            MapBuilder.SetActiveInteriorKind("shop");    // 复位:旧锚点属性(相机/测试读)回果汁店语义
            if (UIManager.I != null)
            {
                UIManager.I.ShowPrompt(null);
                UIManager.I.ShowToast(spec.exitToast);
            }
        }

        void TeleportPlayer(Vector3 position)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = position;
            }
            player.position = position;
            Physics2D.SyncTransforms();
        }

        void OnDestroy()
        {
            if (townRoot != null) townRoot.gameObject.SetActive(true);
            if (I == this) I = null;
        }
    }
}
