using UnityEngine;
using UnityEngine.SceneManagement;

namespace EconTown
{
    /// <summary>
    /// V0.7.2 小镇与港区的场景门：两边共用 GameState/UI/玩家，但世界根、物理和交互互斥。
    /// Play 模式下港区根会移入独立的运行时 Scene，避免继续成为 Town 的贴边子区域。
    /// </summary>
    public sealed class PortSceneController : MonoBehaviour
    {
        const string RuntimeSceneName = "EconTown_PortDistrict_Runtime";

        public static PortSceneController I { get; private set; }
        public static bool IsInsidePort => I != null && I.insidePort;

        Transform player;
        CameraFollow cameraFollow;
        Camera worldCamera;
        Transform townRoot;
        Transform portRoot;
        Vector3 townReturnPosition;
        Color townBackground;
        Scene townScene;
        Scene portScene;
        bool insidePort;

        public static void Create(Transform playerTransform, CameraFollow follow, Camera camera)
        {
            if (I != null)
            {
                I.Configure(playerTransform, follow, camera);
                return;
            }
            GameObject go = new GameObject("PortSceneController");
            PortSceneController controller = go.AddComponent<PortSceneController>();
            I = controller;
            controller.Configure(playerTransform, follow, camera);
        }

        public static void EnterPort()
        {
            if (I != null) I.EnterPortInternal();
        }

        public static void ExitPort()
        {
            if (I != null) I.ExitPortInternal();
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
            portRoot = MapBuilder.PortDistrictRoot;
            townBackground = worldCamera != null ? worldCamera.backgroundColor : Color.black;
            if (townRoot != null) townScene = townRoot.gameObject.scene;

            if (portRoot != null)
            {
                portRoot.gameObject.SetActive(false);
                MovePortRootToRuntimeScene();
            }
        }

        void MovePortRootToRuntimeScene()
        {
            if (!Application.isPlaying || portRoot == null) return;
            portScene = SceneManager.GetSceneByName(RuntimeSceneName);
            if (!portScene.IsValid()) portScene = SceneManager.CreateScene(RuntimeSceneName);
            if (portRoot.gameObject.scene == portScene) return;
            portRoot.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(portRoot.gameObject, portScene);
        }

        void EnterPortInternal()
        {
            if (insidePort || player == null || townRoot == null || portRoot == null) return;
            if (UIManager.I != null && UIManager.I.InputBlocked) return;

            townReturnPosition = player.position;
            insidePort = true;
            townRoot.gameObject.SetActive(false);
            portRoot.gameObject.SetActive(true);
            TeleportPlayer(MapBuilder.PortEntryPosition);
            if (Application.isPlaying && portScene.IsValid()) SceneManager.SetActiveScene(portScene);
            if (worldCamera != null) worldCamera.backgroundColor = new Color32(0x72, 0xa7, 0xad, 255);
            if (cameraFollow != null) cameraFollow.SnapToTarget();
            if (UIManager.I != null)
            {
                UIManager.I.CloseAll();
                UIManager.I.ShowPrompt(null);
                UIManager.I.ShowToast("已抵达国际贸易港 · 走到接驳站按 E 返回小镇");
            }
        }

        void ExitPortInternal()
        {
            if (!insidePort || player == null || townRoot == null || portRoot == null) return;
            if (UIManager.I != null && UIManager.I.InputBlocked) return;

            insidePort = false;
            portRoot.gameObject.SetActive(false);
            townRoot.gameObject.SetActive(true);
            Vector3 returnPosition = townReturnPosition == Vector3.zero
                ? MapBuilder.PortDoor + new Vector3(0f, -.8f, 0f) : townReturnPosition;
            TeleportPlayer(returnPosition);
            if (Application.isPlaying && townScene.IsValid()) SceneManager.SetActiveScene(townScene);
            if (worldCamera != null) worldCamera.backgroundColor = townBackground;
            if (cameraFollow != null) cameraFollow.SnapToTarget();
            if (UIManager.I != null)
            {
                UIManager.I.ShowPrompt(null);
                UIManager.I.ShowToast("已返回经济小镇");
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
