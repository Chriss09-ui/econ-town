#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    public class EconTownMinimapTests
    {
        [Test]
        public void TownCornersNormalizeToTheMapEdges()
        {
            Vector2 southwest = TownMinimap.Normalize(
                new Vector3(MapBuilder.WorldMinX, MapBuilder.WorldMinY, 0f),
                MapBuilder.WorldMinX, MapBuilder.WorldMaxX, MapBuilder.WorldMinY, MapBuilder.WorldMaxY);
            Vector2 northeast = TownMinimap.Normalize(
                new Vector3(MapBuilder.WorldMaxX, MapBuilder.WorldMaxY, 0f),
                MapBuilder.WorldMinX, MapBuilder.WorldMaxX, MapBuilder.WorldMinY, MapBuilder.WorldMaxY);
            Vector2 center = TownMinimap.Normalize(
                new Vector3((MapBuilder.WorldMinX + MapBuilder.WorldMaxX) * .5f,
                    (MapBuilder.WorldMinY + MapBuilder.WorldMaxY) * .5f, 0f),
                MapBuilder.WorldMinX, MapBuilder.WorldMaxX, MapBuilder.WorldMinY, MapBuilder.WorldMaxY);

            Assert.AreEqual(0f, southwest.x, .001f);
            Assert.AreEqual(0f, southwest.y, .001f);
            Assert.AreEqual(1f, northeast.x, .001f);
            Assert.AreEqual(1f, northeast.y, .001f);
            Assert.AreEqual(.5f, center.x, .001f);
            Assert.AreEqual(.5f, center.y, .001f);
        }

        [Test]
        public void MainlineStepsPointAtTheRightLandmark()
        {
            Assert.AreEqual("home", TownMinimap.ObjectiveKind(MainlineDestination.Resident));
            Assert.AreEqual("home", TownMinimap.ObjectiveKind(MainlineDestination.Housing));
            Assert.AreEqual("hall", TownMinimap.ObjectiveKind(MainlineDestination.Government));
            Assert.AreEqual("bank", TownMinimap.ObjectiveKind(MainlineDestination.Finance));
            Assert.AreEqual("centralbank", TownMinimap.ObjectiveKind(MainlineDestination.CentralBank));
            Assert.AreEqual("port", TownMinimap.ObjectiveKind(MainlineDestination.Port));
            Assert.AreEqual("shop", TownMinimap.ObjectiveKind(MainlineDestination.Shop));
            Assert.IsNull(TownMinimap.ObjectiveKind(MainlineDestination.Macro));
            Assert.AreEqual("家", TownMinimap.PlaceName("home"));
        }

        [Test]
        public void MinimapIsBuiltOnTheHud()
        {
            GameObject uiObject = new GameObject("MinimapUi");
            try
            {
                UIManager ui = uiObject.AddComponent<UIManager>();
                ui.InitializeForValidation();
                Assert.IsNotNull(ui.transform.Find("Canvas/townMinimap"), "小地图应挂在主界面上。");
                ui.RefreshMinimap();
                Text[] texts = ui.GetComponentsInChildren<Text>(true);
                bool found = false;
                for (int i = 0; i < texts.Length; i++)
                    if (texts[i] != null && texts[i].text != null && texts[i].text.Contains("小人是你"))
                        found = true;
                Assert.IsTrue(found, "小地图要说明黄点是玩家。");
            }
            finally
            {
                Object.DestroyImmediate(uiObject);
            }
        }
    }
}
#endif
