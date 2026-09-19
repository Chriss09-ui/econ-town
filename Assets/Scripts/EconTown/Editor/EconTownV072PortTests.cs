#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EconTown
{
    /// <summary>V0.7.2《通往大海》回归：真实账本投影、港区结构、演出频率与一次性开港队列。</summary>
    public class EconTownV072PortTests
    {
        GameObject stateObject;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            stateObject = new GameObject("V072PortState");
            state = stateObject.AddComponent<GameState>();
            GameState.I = state;
            DaySimulator.SetRandomSeed(20260828);
            InternationalTradeService.Ensure(state);
            LongTermExperienceService.Ensure(state);
            state.progression.initialized = true;
            state.progression.completedChapter = 43;
            state.progression.currentChapter = 44;
            state.government.cash = 200000f;
            state.money = 100000f;
            state.materials = 5000;
            state.economy.financialCrisisActive = false;
        }

        [TearDown]
        public void TearDown()
        {
            GameState.I = null;
            if (stateObject != null) Object.DestroyImmediate(stateObject);
        }

        [Test]
        public void ClosedExportChannel_ForThreeSettlements_LeavesPortIdleAndEmptyOfExports()
        {
            state.exportChannelEnabled = false;
            for (int i = 0; i < 3; i++)
            {
                // 本用例只验证港口关停；随机城市事件属于独立强制决策，夹具中视为已处理。
                if (state.government.currentEvent != null) state.government.currentEvent.resolved = true;
                state.phase = GamePhase.Morning;
                Assert.IsTrue(WorldSettlementService.TrySettleAndAdvance(state, out _, out string reason), reason);
            }

            PortDisplaySnapshot view = PortPresentationService.Read(state);
            Assert.AreEqual(3, state.worldTrade.history.Count);
            Assert.AreEqual(0f, view.playerExportsLastSevenDays, .001f);
            Assert.AreEqual(0, view.exportContainerCount);
            Assert.AreEqual(PortCraneTier.Idle, view.craneTier);
            Assert.IsFalse(view.shipShouldDock, "关闭出海后即使结算日可被 3 整除，也不得出现货船。");
        }

        [Test]
        public void Snapshot_UsesExactLedgerRows_ForContainersCraneAndCustomsNumbers()
        {
            state.worldTrade.history.Clear();
            for (int day = 3; day <= 9; day++)
                state.worldTrade.history.Add(new TradeDayRecord
                {
                    worldDay = day,
                    exchangeRateUsed = 7.12f,
                    playerExportUnits = 90f,
                    exportsTotal = 1200f + day,
                    importsTotal = 900f,
                    netExports = 300f + day
                });
            state.worldTrade.countryB.tariffOnAExports = .16f;

            PortDisplaySnapshot view = PortPresentationService.Read(state);
            Assert.AreSame(state.worldTrade.history[^1], view.row, "报关行与世界经济页必须引用同一条真实账本行。");
            Assert.AreEqual(630f, view.playerExportsLastSevenDays, .001f);
            Assert.AreEqual(18, view.exportContainerCount, "630 杯应恰好触顶 18 个出口箱。");
            Assert.AreEqual(6, view.importContainerCount, "7×900 的进口额应触顶 6 个进口箱。");
            Assert.AreEqual(PortCraneTier.Full, view.craneTier);
            Assert.AreEqual(1.6f, view.cranePeriodSeconds, .001f);
            Assert.IsTrue(view.shipShouldDock, "最近行 Day 9 且有出货，应停船。");
            Assert.IsTrue(view.tariffStripe, "B 国关税高于基准 8% 时出口箱必须贴红条。");
            StringAssert.Contains("+309", PortPresentationService.NetExportsTeachingLine(view));
        }

        [Test]
        public void CraneTiers_AndArrivalFrequency_FollowPrdBoundaries()
        {
            Assert.AreEqual(PortCraneTier.Idle, PortPresentationService.CraneTier(0f));
            Assert.AreEqual(PortCraneTier.Slow, PortPresentationService.CraneTier(1f));
            Assert.AreEqual(PortCraneTier.Slow, PortPresentationService.CraneTier(30f));
            Assert.AreEqual(PortCraneTier.Medium, PortPresentationService.CraneTier(31f));
            Assert.AreEqual(PortCraneTier.Medium, PortPresentationService.CraneTier(60f));
            Assert.AreEqual(PortCraneTier.Full, PortPresentationService.CraneTier(61f));

            state.worldTrade.history.Clear();
            state.worldTrade.history.Add(new TradeDayRecord { worldDay = 3, playerExportUnits = 20f });
            PortDisplaySnapshot view = PortPresentationService.Read(state);
            PortPresentationState presentation = state.experience.portPresentation;
            Assert.IsTrue(PortPresentationService.ShouldPlayFullArrival(view, presentation,
                state.experience.settings, true), "首次靠港应播放完整驶入。");

            presentation.hasSeenArrival = true;
            presentation.lastArrivalTier = (int)PortCraneTier.Slow;
            Assert.IsFalse(PortPresentationService.ShouldPlayFullArrival(view, presentation,
                state.experience.settings, true), "同档位靠港直接停泊。");
            state.worldTrade.history[^1].playerExportUnits = 45f;
            view = PortPresentationService.Read(state);
            Assert.IsTrue(PortPresentationService.ShouldPlayFullArrival(view, presentation,
                state.experience.settings, true), "出货档位变化应恢复完整驶入。");
            state.experience.settings.fullPortAnimations = false;
            Assert.IsFalse(PortPresentationService.ShouldPlayFullArrival(view, presentation,
                state.experience.settings, true), "精简设置永远只呈现停泊态。");
        }

        [Test]
        public void ForeignRecession_AddsFogSignalButNeverCancelsARealShip()
        {
            state.worldTrade.history.Clear();
            state.worldTrade.history.Add(new TradeDayRecord { worldDay = 6, playerExportUnits = 32f });
            state.worldTrade.countryB.recessionActive = false;
            PortDisplaySnapshot normal = PortPresentationService.Read(state);
            state.worldTrade.countryB.recessionActive = true;
            PortDisplaySnapshot recession = PortPresentationService.Read(state);

            Assert.IsFalse(normal.recessionActive);
            Assert.IsTrue(recession.recessionActive);
            Assert.IsTrue(normal.shipShouldDock);
            Assert.IsTrue(recession.shipShouldDock, "B 国衰退只能让船吃水变浅和海面起雾，不能撤船。");
        }

        [Test]
        public void PortProjection_HasNoEconomicSideEffects_RegardlessOfPresentationSettings()
        {
            state.worldTrade.history.Add(new TradeDayRecord
            {
                worldDay = 3, playerExportUnits = 25f, exportsTotal = 700f,
                importsTotal = 820f, netExports = -120f, exchangeRateUsed = 7.05f
            });
            string tradeBefore = JsonUtility.ToJson(state.worldTrade);
            string economyBefore = JsonUtility.ToJson(state.economy);
            string governmentBefore = JsonUtility.ToJson(state.government);
            string macroBefore = JsonUtility.ToJson(state.macro);
            float moneyBefore = state.money;

            state.experience.settings.atmosphereEffects = true;
            PortPresentationService.Read(state);
            state.experience.settings.atmosphereEffects = false;
            state.experience.settings.ecoVisibility = false;
            PortPresentationService.Read(state);

            Assert.AreEqual(tradeBefore, JsonUtility.ToJson(state.worldTrade));
            Assert.AreEqual(economyBefore, JsonUtility.ToJson(state.economy));
            Assert.AreEqual(governmentBefore, JsonUtility.ToJson(state.government));
            Assert.AreEqual(macroBefore, JsonUtility.ToJson(state.macro));
            Assert.AreEqual(moneyBefore, state.money);
        }

        [Test]
        public void PortUsesIndependentSceneRoot_WithSeaCollisionAndTownCoordinatesPreserved()
        {
            GameObject mapObject = new GameObject("V072PortMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                Transform town = mapObject.transform.Find("Town");
                Transform port = mapObject.transform.Find("PortScene");
                Tilemap townGround = town.Find("Ground").GetComponent<Tilemap>();
                Tilemap portGround = port.Find("Ground").GetComponent<Tilemap>();
                var water = new HashSet<Sprite>
                {
                    PixelArt.HarborWater(0), PixelArt.HarborWater(1), PixelArt.HarborWater(2)
                };

                Assert.AreEqual(72, MapBuilder.W, "主城地块化扩为 72 列，港区仍走独立场景。");
                Assert.AreEqual(32, MapBuilder.PortW);
                Assert.AreEqual(-36, MapBuilder.OriginX);
                Assert.IsNull(town.Find("PortDistrict"), "Town 下不得再挂贴边码头。");
                Assert.IsNotNull(port, "港区必须是独立世界根。");
                Assert.IsFalse(port.gameObject.activeSelf, "启动时只显示小镇，港区等待 E 进入。");
                foreach (Vector3Int cell in townGround.cellBounds.allPositionsWithin)
                    Assert.IsFalse(water.Contains(townGround.GetSprite(cell)), "小镇地表不应混入港区海水。");

                Assert.IsTrue(water.Contains(portGround.GetSprite(
                    new Vector3Int(MapBuilder.PortSeaMinX, 0, 0))));
                Assert.AreEqual(PixelArt.HarborShore(),
                    portGround.GetSprite(new Vector3Int(MapBuilder.PortShoreX, 0, 0)));
                Assert.IsNotNull(port.Find("port_sea_barrier").GetComponent<BoxCollider2D>(),
                    "海面必须有连续碰撞带。");
                Assert.IsNotNull(port.Find("customs_board"));
                Assert.IsNotNull(port.Find("port_scene_terminal"));
                Assert.AreEqual(new Vector3(31f, -9.35f, 0f), MapBuilder.PortDoor,
                    "港口接驳入口应跟随东南边缘锚点，且仍在人行砖上。");
                Assert.Greater(MapBuilder.PortDoor.x, MapBuilder.TownMaxX - 5f,
                    "港口必须位于小镇东部边缘，而不是产业街中段。");
                Assert.AreEqual(new Vector3(-32f, -10.75f, 0f), MapBuilder.ShopDoor,
                    "核心经营入口应跟随果汁店新锚点，且仍在人行砖上。");
                Assert.That(MapBuilder.NpcWanderMaxX, Is.LessThanOrEqualTo(MapBuilder.TownMaxX),
                    "NPC 漫步目标只能留在小镇。");
                float townCenterX = (MapBuilder.WorldMinX + MapBuilder.WorldMaxX) * .5f;
                Assert.AreEqual(townCenterX,
                    CameraFollow.ClampInsideMap(999f, 40f, MapBuilder.WorldMinX, MapBuilder.WorldMaxX), .001f,
                    "视口宽于扩图后的小镇时应回到小镇中心。");
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        [Test]
        public void PortCargoDecor_FillsTheYard_WithoutBlockingEntryOrCustomsRoute()
        {
            GameObject mapObject = new GameObject("V072PortCargoDecorMap");
            try
            {
                MapBuilder.Build(mapObject.transform);
                Transform port = mapObject.transform.Find("PortScene");
                Transform cargo = port.Find("port_static_cargo");
                Assert.IsNotNull(cargo, "港区应有独立的常驻货场装饰根。");

                int stacks = 0, pallets = 0, drums = 0, bollards = 0, lanes = 0, forklifts = 0;
                Vector3[] protectedPoints = {
                    MapBuilder.PortEntryPosition, MapBuilder.PortExitDoor, MapBuilder.CustomsDoor
                };
                for (int i = 0; i < cargo.childCount; i++)
                {
                    Transform child = cargo.GetChild(i);
                    if (child.name.Contains("container_stack")) stacks++;
                    else if (child.name.Contains("pallet")) pallets++;
                    else if (child.name.Contains("drums")) drums++;
                    else if (child.name.Contains("bollard")) bollards++;
                    else if (child.name.Contains("lane_marking")) lanes++;
                    else if (child.name.Contains("forklift")) forklifts++;

                    if (!child.name.StartsWith("port_decor_solid_")) continue;
                    Assert.IsNotNull(child.GetComponent<BoxCollider2D>(), child.name + " 应有实体碰撞。");
                    for (int p = 0; p < protectedPoints.Length; p++)
                        Assert.Greater(Vector2.Distance(child.position, protectedPoints[p]), 2.2f,
                            child.name + " 不得堵住进场、返回或报关行交互路线。");
                }

                Assert.AreEqual(7, stacks, "需要成片集装箱堆，而不是零散一两个箱子。");
                Assert.AreEqual(3, pallets);
                Assert.AreEqual(2, drums);
                Assert.AreEqual(1, forklifts);
                Assert.AreEqual(5, bollards);
                Assert.AreEqual(4, lanes);
            }
            finally
            {
                Object.DestroyImmediate(mapObject);
            }
        }

        [Test]
        public void PortSceneTransition_HidesTownTeleportsPlayer_AndReturnsToOriginalPosition()
        {
            GameObject mapObject = new GameObject("V072PortTransitionMap");
            GameObject playerObject = new GameObject("V072PortTransitionPlayer");
            GameObject cameraObject = new GameObject("V072PortTransitionCamera");
            GameObject followObject = new GameObject("V072PortTransitionFollow");
            try
            {
                MapBuilder.Build(mapObject.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 7.6f;
                CameraFollow follow = followObject.AddComponent<CameraFollow>();
                follow.target = playerObject.transform;
                follow.cam = camera;
                Vector3 originalPosition = MapBuilder.PortDoor + new Vector3(0f, -.8f, 0f);
                playerObject.transform.position = originalPosition;

                PortSceneController.Create(playerObject.transform, follow, camera);
                PortSceneController.EnterPort();
                Assert.IsTrue(PortSceneController.IsInsidePort);
                Assert.IsFalse(MapBuilder.TownRoot.gameObject.activeSelf);
                Assert.IsTrue(MapBuilder.PortDistrictRoot.gameObject.activeSelf);
                Assert.AreEqual(MapBuilder.PortEntryPosition, playerObject.transform.position);

                PortSceneController.ExitPort();
                Assert.IsFalse(PortSceneController.IsInsidePort);
                Assert.IsTrue(MapBuilder.TownRoot.gameObject.activeSelf);
                Assert.IsFalse(MapBuilder.PortDistrictRoot.gameObject.activeSelf);
                Assert.AreEqual(originalPosition, playerObject.transform.position);

                Interactable townInteraction = Interactables.Register("town", Vector3.zero, null);
                Interactable portInteraction = Interactables.Register("port", Vector3.zero, null, WorldArea.Port);
                Assert.AreEqual(WorldArea.Town, townInteraction.area);
                Assert.AreEqual(WorldArea.Port, portInteraction.area);
            }
            finally
            {
                if (PortSceneController.I != null) Object.DestroyImmediate(PortSceneController.I.gameObject);
                Object.DestroyImmediate(followObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(mapObject);
            }
        }

        [Test]
        public void Chapter42_QueuesPortOpeningOnlyAtCrossing_AndOldSaveDoesNotBackfill()
        {
            state.progression = new ProgressionState
            {
                initialized = true,
                completedChapter = 41,
                currentChapter = 42,
                chapterStartedWorldDay = state.day
            };
            ProgressionService.RecordSignal(state, ProgressSignals.PolicyMixObserved);
            Assert.AreEqual(42, state.progression.completedChapter);
            CollectionAssert.Contains(state.progression.pendingCeremonies, ProgressSignals.PortOpeningCeremony);
            Assert.IsTrue(ProgressionService.MarkCeremonyViewed(state, ProgressSignals.PortOpeningCeremony));
            CollectionAssert.DoesNotContain(state.progression.pendingCeremonies, ProgressSignals.PortOpeningCeremony);
            CollectionAssert.Contains(state.progression.signals, ProgressSignals.PortOpeningCeremony);

            state.progression = new ProgressionState
            {
                initialized = true,
                completedChapter = 42,
                currentChapter = 43
            };
            ProgressionService.Ensure(state);
            Assert.IsEmpty(state.progression.pendingCeremonies,
                "旧档只凭 completedChapter 不得补播开港演出。");
        }
    }
}

#endif
