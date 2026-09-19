using System.Collections.Generic;
using UnityEngine;

namespace EconTown
{
    /// <summary>
    /// V0.7.2 港区演出导演。只读取 PortPresentationService 的账本投影；
    /// 除演出已读标记外不写任何游戏状态，动画从不占用 InputBlocked 或收日流程。
    /// </summary>
    public sealed class PortDistrictDirector : MonoBehaviour
    {
        public static PortDistrictDirector I { get; private set; }

        /// <summary>今晨是否已播放开港演出：TownGrowthDirector 用它计算 §142 的单日演出段数上限。</summary>
        public static bool MorningCeremonyPlayed { get; private set; }

        readonly List<SpriteRenderer> exportContainers = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> importContainers = new List<SpriteRenderer>();
        Transform operationalRoot;
        Transform constructionRoot;
        Transform craneA;
        Transform craneB;
        SpriteRenderer craneLightA;
        SpriteRenderer craneLightB;
        SpriteRenderer ship;
        SpriteRenderer fog;
        SpriteRenderer flag;
        PortDisplaySnapshot snapshot;
        readonly Vector3 shipStart = new Vector3(18f, -3.2f, 0f);
        readonly Vector3 shipBerth = new Vector3(11f, -3.2f, 0f);
        bool arriving;
        bool departing;
        bool refreshAfterDeparture;
        float animationTime;
        float ceremonyTime;
        float nextRefreshAt;
        int appliedWorldDay = -1;
        bool appliedRecession;
        bool appliedRedFlag;
        bool appliedTariffStripe;
        int appliedExportBoxes = -1;
        int appliedImportBoxes = -1;
        bool appliedUnlocked;

        public static void Create()
        {
            if (I != null) return;
            new GameObject("PortDistrictDirector").AddComponent<PortDistrictDirector>();
        }

        public static void BeginMorning(GameState state)
        {
            if (I != null) I.BeginMorningInternal(state);
        }

        void Awake()
        {
            I = this;
        }

        void Start()
        {
            BuildVisuals();
            ApplySnapshot(GameState.I, false, true);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void BuildVisuals()
        {
            Transform port = MapBuilder.PortDistrictRoot;
            if (port == null) return;

            constructionRoot = new GameObject("port_construction").transform;
            constructionRoot.SetParent(port, false);
            for (int i = 0; i < 5; i++)
                CreateSprite(constructionRoot, "construction_fence_" + i, PixelArt.ConstructionFence(),
                    new Vector3(6.65f, -8f + i * 4f, 0f), 1f);
            BoxCollider2D fenceCollider = constructionRoot.gameObject.AddComponent<BoxCollider2D>();
            fenceCollider.size = new Vector2(.55f, 20f);
            fenceCollider.offset = new Vector2(6.65f, 0f);
            GameObject constructionText = new GameObject("construction_sign_text");
            constructionText.transform.SetParent(constructionRoot, false);
            constructionText.transform.position = new Vector3(6.55f, -7.5f, 0f);
            TextMesh sign = constructionText.AddComponent<TextMesh>();
            sign.text = "施工中";
            sign.font = UiTheme.GameFont;
            sign.fontSize = 24;
            sign.characterSize = .24f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.color = new Color32(0x64, 0x35, 0x28, 255);
            MeshRenderer signRenderer = constructionText.GetComponent<MeshRenderer>();
            if (signRenderer == null) signRenderer = constructionText.AddComponent<MeshRenderer>();
            if (sign.font != null) signRenderer.sharedMaterial = sign.font.material;
            signRenderer.sortingOrder = 920;

            operationalRoot = new GameObject("port_operational").transform;
            operationalRoot.SetParent(port, false);
            craneA = CreateSprite(operationalRoot, "quay_crane_a", PixelArt.HarborCrane(),
                new Vector3(5.35f, 4.1f, 0f), .9f).transform;
            craneB = CreateSprite(operationalRoot, "quay_crane_b", PixelArt.HarborCrane(),
                new Vector3(5.35f, -3.4f, 0f), .9f).transform;
            craneLightA = CreateSprite(craneA, "crane_light", PixelArt.HarborCraneLight(false),
                new Vector3(.7f, 4.45f, 0f), 1f, true).GetComponent<SpriteRenderer>();
            craneLightB = CreateSprite(craneB, "crane_light", PixelArt.HarborCraneLight(false),
                new Vector3(.7f, 4.45f, 0f), 1f, true).GetComponent<SpriteRenderer>();

            for (int i = 0; i < PortPresentationService.MaxExportContainers; i++)
            {
                int col = i % 3, row = i / 3;
                SpriteRenderer renderer = CreateSprite(operationalRoot, "export_container_" + i,
                    PixelArt.HarborContainer(false, false),
                    new Vector3(2.15f + col * .9f, .25f + row * .72f, 0f), .72f).GetComponent<SpriteRenderer>();
                exportContainers.Add(renderer);
            }
            for (int i = 0; i < PortPresentationService.MaxImportContainers; i++)
            {
                int col = i % 2, row = i / 2;
                SpriteRenderer renderer = CreateSprite(operationalRoot, "import_container_" + i,
                    PixelArt.HarborContainer(true, false),
                    new Vector3(1.7f + col * 1.05f, -5.7f + row * .8f, 0f), .82f).GetComponent<SpriteRenderer>();
                importContainers.Add(renderer);
            }

            ship = CreateSprite(operationalRoot, "cargo_ship", PixelArt.CargoShip(false, 0),
                shipBerth, 1f).GetComponent<SpriteRenderer>();
            ship.sortingOrder = MapBuilder.SortingForY(shipBerth.y) - 2;
            fog = CreateSprite(operationalRoot, "country_b_recession_fog", PixelArt.HarborFog(),
                new Vector3(11f, .8f, 0f), 1.25f).GetComponent<SpriteRenderer>();
            fog.sortingOrder = 850;
            Transform staticFlag = port.Find("port_flag");
            flag = staticFlag != null ? staticFlag.GetComponent<SpriteRenderer>() : null;
        }

        static GameObject CreateSprite(Transform parent, string name, Sprite sprite, Vector3 position,
            float scale, bool localPosition = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (localPosition) go.transform.localPosition = position;
            else go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = MapBuilder.SortingForY(go.transform.position.y);
            return go;
        }

        void BeginMorningInternal(GameState state)
        {
            if (state == null || operationalRoot == null) return;
            MorningCeremonyPlayed = false;
            TryPlayOpeningCeremony(state);
            if (ship != null && ship.gameObject.activeSelf)
            {
                arriving = false;
                departing = true;
                refreshAfterDeparture = true;
                animationTime = 0f;
                Color c = ship.color; c.a = 1f; ship.color = c;
            }
            else ApplySnapshot(state, true, true);
        }

        void TryPlayOpeningCeremony(GameState state)
        {
            if (!ProgressionService.MarkCeremonyViewed(state, ProgressSignals.PortOpeningCeremony)) return;
            MorningCeremonyPlayed = true;
            ceremonyTime = 3f;
            UpdateConstructionState(state);
            SetCraneLights(true);
            if (UIManager.I != null) UIManager.I.ShowToast("国际贸易港正式开港");
            SaveSystem.SaveKeyMoment(state, "国际贸易港正式开港");
        }

        void Update()
        {
            GameState state = GameState.I;
            if (state == null || operationalRoot == null) return;

            if (ceremonyTime > 0f)
            {
                ceremonyTime -= Time.deltaTime;
                if (ceremonyTime <= 0f) SetCraneLights(false);
            }

            if (arriving)
            {
                if (ArrivalInterrupted()) SnapShipToBerth();
                else
                {
                    animationTime += Time.deltaTime;
                    float t = Mathf.Clamp01(animationTime / 3f);
                    ship.transform.position = Vector3.Lerp(CurrentShipStart(), CurrentShipBerth(),
                        t * t * (3f - 2f * t));
                    if (t >= 1f) SnapShipToBerth();
                }
            }
            else if (departing)
            {
                animationTime += Time.deltaTime;
                float t = Mathf.Clamp01(animationTime / .5f);
                Color c = ship.color; c.a = 1f - t; ship.color = c;
                if (t >= 1f)
                {
                    departing = false;
                    ship.gameObject.SetActive(false);
                    c.a = 1f; ship.color = c;
                    if (refreshAfterDeparture)
                    {
                        refreshAfterDeparture = false;
                        ApplySnapshot(state, true, true);
                    }
                }
            }

            AnimateCranes(state);
            if (Time.unscaledTime >= nextRefreshAt && !arriving && !departing)
            {
                nextRefreshAt = Time.unscaledTime + .5f;
                PortDisplaySnapshot latest = PortPresentationService.Read(state);
                bool changed = latest.settledWorldDay != appliedWorldDay || latest.recessionActive != appliedRecession ||
                    latest.redPortFlag != appliedRedFlag || latest.tariffStripe != appliedTariffStripe ||
                    latest.exportContainerCount != appliedExportBoxes || latest.importContainerCount != appliedImportBoxes ||
                    IsUnlocked(state) != appliedUnlocked;
                if (changed) ApplySnapshot(state, false, false);
            }
        }

        void AnimateCranes(GameState state)
        {
            if (craneA == null || snapshot == null) return;
            bool animate = state.experience != null && state.experience.settings != null &&
                state.experience.settings.atmosphereEffects && IsUnlocked(state);
            if (!animate)
            {
                craneA.localRotation = Quaternion.identity;
                craneB.localRotation = Quaternion.identity;
                return;
            }
            float period = Mathf.Max(.5f, snapshot.cranePeriodSeconds);
            float angleA = Mathf.Sin(Time.time * Mathf.PI * 2f / period) *
                (snapshot.craneTier == PortCraneTier.Idle ? .8f : 2.2f);
            float angleB = Mathf.Sin(Time.time * Mathf.PI * 2f / period + Mathf.PI) *
                (snapshot.craneTier == PortCraneTier.Idle ? .8f : 2.2f);
            craneA.localRotation = Quaternion.Euler(0f, 0f, angleA);
            craneB.localRotation = Quaternion.Euler(0f, 0f, angleB);
        }

        void ApplySnapshot(GameState state, bool allowFullArrival, bool markConsidered)
        {
            if (state == null) return;
            LongTermExperienceService.Ensure(state);
            snapshot = PortPresentationService.Read(state);
            UpdateConstructionState(state);
            RefreshContainers(snapshot);
            if (flag != null) flag.sprite = PixelArt.PortFlag(snapshot.redPortFlag);
            if (fog != null) fog.gameObject.SetActive(appliedUnlocked && snapshot.recessionActive &&
                state.experience.settings.atmosphereEffects);

            bool shouldAnimateArrival = false;
            PortPresentationState presentation = state.experience.portPresentation;
            if (snapshot.shipShouldDock)
            {
                shouldAnimateArrival = PortPresentationService.ShouldPlayFullArrival(snapshot,
                    presentation, state.experience.settings, allowFullArrival);
                ship.sprite = PixelArt.CargoShip(snapshot.recessionActive, (int)snapshot.craneTier);
                ship.gameObject.SetActive(appliedUnlocked);
                Color c = ship.color; c.a = 1f; ship.color = c;
                if (shouldAnimateArrival && appliedUnlocked)
                {
                    ship.transform.position = CurrentShipStart();
                    arriving = true;
                    animationTime = 0f;
                }
                else ship.transform.position = CurrentShipBerth();

                if (markConsidered && snapshot.settledWorldDay > 0)
                {
                    presentation.hasSeenArrival = true;
                    presentation.lastArrivalTier = (int)snapshot.craneTier;
                    presentation.lastArrivalConsideredWorldDay = snapshot.settledWorldDay;
                }
            }
            else
            {
                arriving = false;
                if (!departing) ship.gameObject.SetActive(false);
                if (markConsidered && snapshot.settledWorldDay > 0)
                    presentation.lastArrivalConsideredWorldDay = snapshot.settledWorldDay;
            }

            appliedWorldDay = snapshot.settledWorldDay;
            appliedRecession = snapshot.recessionActive;
            appliedRedFlag = snapshot.redPortFlag;
            appliedTariffStripe = snapshot.tariffStripe;
            appliedExportBoxes = snapshot.exportContainerCount;
            appliedImportBoxes = snapshot.importContainerCount;
        }

        void UpdateConstructionState(GameState state)
        {
            appliedUnlocked = IsUnlocked(state);
            if (constructionRoot != null) constructionRoot.gameObject.SetActive(!appliedUnlocked);
            if (operationalRoot != null) operationalRoot.gameObject.SetActive(appliedUnlocked);
        }

        static bool IsUnlocked(GameState state)
        {
            return state != null && state.progression != null &&
                state.progression.completedChapter >= InternationalTradeService.ExportUnlockChapter;
        }

        void RefreshContainers(PortDisplaySnapshot value)
        {
            for (int i = 0; i < exportContainers.Count; i++)
            {
                SpriteRenderer renderer = exportContainers[i];
                renderer.gameObject.SetActive(i < value.exportContainerCount);
                renderer.sprite = PixelArt.HarborContainer(false, value.tariffStripe);
            }
            for (int i = 0; i < importContainers.Count; i++)
                importContainers[i].gameObject.SetActive(i < value.importContainerCount);
        }

        void SnapShipToBerth()
        {
            arriving = false;
            ship.transform.position = CurrentShipBerth();
        }

        Vector3 CurrentShipBerth()
        {
            // B 国衰退时货量较少，船体整体抬高一点，形成明确的浅吃水视觉。
            return shipBerth + (snapshot != null && snapshot.recessionActive ? Vector3.up * .22f : Vector3.zero);
        }

        Vector3 CurrentShipStart()
        {
            return shipStart + (snapshot != null && snapshot.recessionActive ? Vector3.up * .22f : Vector3.zero);
        }

        static bool ArrivalInterrupted()
        {
            // 统一走 PlayerController.WantsControlInput(项目为 Input System,裸调 legacy Input 会抛)
            return PlayerController.WantsControlInput();
        }

        void SetCraneLights(bool lit)
        {
            Sprite sprite = PixelArt.HarborCraneLight(lit);
            if (craneLightA != null) craneLightA.sprite = sprite;
            if (craneLightB != null) craneLightB.sprite = sprite;
        }
    }
}
