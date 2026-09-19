using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>
    /// uGUI 折线图:坐标轴 + 网格 + 折线 + 数据点,全部用顶点色绘制(无贴图)。
    /// 数据以“归一化 0-1 坐标”传入,内部映射到绘图区。
    /// </summary>
    public class PolylineGraph : Graphic, IPointerMoveHandler, IPointerClickHandler, IPointerExitHandler
    {
        public Vector2 plotMin = new Vector2(0.10f, 0.10f);   // 绘图区左下(归一化)
        public Vector2 plotMax = new Vector2(0.96f, 0.92f);   // 绘图区右上
        public Color axisColor = new Color32(0x8a, 0x78, 0x62, 255);
        public Color gridColor = new Color32(0x55, 0x47, 0x38, 255);
        public Color lineColor = new Color32(0xe8, 0xa8, 0x4c, 255);
        public Color pointColor = new Color32(0xf2, 0xe8, 0xd8, 255);
        public Color fitColor = new Color32(0x6f, 0xc4, 0xd9, 180);
        public float lineWidth = 2.6f;
        public float pointSize = 5.0f;

        readonly List<Vector2> points = new List<Vector2>();
        readonly List<Vector2> fit = new List<Vector2>();
        readonly List<Vector2> pointsB = new List<Vector2>();
        readonly List<Text> axisLabels = new List<Text>();
        bool connectPrimaryPoints = true;
        float axisXMin, axisXMax = 1f, axisYMin, axisYMax = 1f;
        string axisXTitle = "X", axisYTitle = "Y", axisXSuffix = "", axisYSuffix = "";
        Vector2 currentPoint;
        bool hasCurrentPoint;
        int hoverIndex = -1;
        bool readoutPinned;
        Text readout;
        public Color lineColorB = new Color32(0x8b, 0xc3, 0x4a, 255);
        public int PointCount { get { return points.Count; } }
        public int FitPointCount { get { return fit.Count; } }
        public int SeriesBPointCount { get { return pointsB.Count; } }

        public void SetData(List<Vector2> dataPoints, List<Vector2> fitCurve = null, List<Vector2> seriesB = null)
        {
            points.Clear();
            if (dataPoints != null) points.AddRange(dataPoints);
            fit.Clear();
            if (fitCurve != null) fit.AddRange(fitCurve);
            pointsB.Clear();
            if (seriesB != null) pointsB.AddRange(seriesB);
            SetVerticesDirty();
        }

        public void SetPrimaryConnections(bool visible)
        {
            connectPrimaryPoints = visible;
            SetVerticesDirty();
        }

        public void ConfigureAxes(float xMin, float xMax, float yMin, float yMax,
            string xTitle, string yTitle, string xSuffix = "", string ySuffix = "")
        {
            axisXMin = xMin; axisXMax = Mathf.Max(xMin + .0001f, xMax);
            axisYMin = yMin; axisYMax = Mathf.Max(yMin + .0001f, yMax);
            axisXTitle = xTitle; axisYTitle = yTitle;
            axisXSuffix = xSuffix; axisYSuffix = ySuffix;
            BuildAxisLabels();
        }

        public void SetCurrentPoint(Vector2 normalizedPoint)
        {
            currentPoint = normalizedPoint;
            hasCurrentPoint = true;
            SetVerticesDirty();
        }

        void BuildAxisLabels()
        {
            for (int i = 0; i < axisLabels.Count; i++)
                if (axisLabels[i] != null) Destroy(axisLabels[i].gameObject);
            axisLabels.Clear();
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                Text xTick = AxisText(FormatAxis(Mathf.Lerp(axisXMin, axisXMax, t), axisXSuffix), 13, TextAnchor.UpperCenter);
                float tickPivotX = i == 0 ? 0f : i == 4 ? 1f : .5f;
                AnchorLabel(xTick.rectTransform, new Vector2(Mathf.Lerp(plotMin.x, plotMax.x, t), plotMin.y),
                    new Vector2(0f, -9f), new Vector2(92f, 24f), new Vector2(tickPivotX, .5f));
                Text yTick = AxisText(FormatAxis(Mathf.Lerp(axisYMin, axisYMax, t), axisYSuffix), 13, TextAnchor.MiddleRight);
                AnchorLabel(yTick.rectTransform, new Vector2(plotMin.x, Mathf.Lerp(plotMin.y, plotMax.y, t)),
                    new Vector2(-8f, 0f), new Vector2(80f, 22f), new Vector2(1f, .5f));
            }
            Text xName = AxisText(axisXTitle, 14, TextAnchor.LowerRight);
            AnchorLabel(xName.rectTransform, new Vector2(plotMax.x, plotMin.y),
                new Vector2(-8f, 10f), new Vector2(220f, 24f), new Vector2(1f, 0f));
            Text yName = AxisText(axisYTitle, 14, TextAnchor.UpperLeft);
            AnchorLabel(yName.rectTransform, new Vector2(plotMin.x, plotMax.y), new Vector2(8f, -22f),
                new Vector2(180f, 24f), new Vector2(0f, 1f));
            readout = AxisText("移动到数据点查看数值", 14, TextAnchor.UpperRight);
            AnchorLabel(readout.rectTransform, new Vector2(plotMax.x, plotMax.y), new Vector2(-6f, -5f),
                new Vector2(330f, 26f), new Vector2(1f, 1f));
            readout.color = UiTheme.Accent;
        }

        Text AxisText(string value, int size, TextAnchor anchor)
        {
            Text label = UiTheme.MakeText(transform, value, size, UiTheme.TextSub, anchor);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            axisLabels.Add(label);
            return label;
        }

        static void AnchorLabel(RectTransform target, Vector2 anchor, Vector2 position, Vector2 size,
            Vector2? pivot = null)
        {
            target.anchorMin = target.anchorMax = anchor;
            target.pivot = pivot ?? new Vector2(.5f, .5f);
            target.anchoredPosition = position;
            target.sizeDelta = size;
        }

        static string FormatAxis(float value, string suffix)
        {
            if (suffix == " 日") return Mathf.RoundToInt(value) + suffix;
            string preference = GameState.I != null && GameState.I.experience != null &&
                GameState.I.experience.settings != null
                ? GameState.I.experience.settings.numericUnit : "自动";
            float absolute = Mathf.Abs(value);
            if (preference != "完整数值")
            {
                bool forceCompact = preference == "万/亿";
                if (absolute >= 100000000f)
                    return (value / 100000000f).ToString("0.##") + "亿" + suffix;
                if ((forceCompact && absolute >= 10000f) || (preference == "自动" && absolute >= 10000f))
                    return (value / 10000f).ToString("0.##") + "万" + suffix;
            }
            string number = Mathf.Abs(value) >= 100f ? value.ToString("0") :
                Mathf.Abs(value) >= 10f ? value.ToString("0.#") : value.ToString("0.##");
            return number + suffix;
        }

        void AddSeg(VertexHelper vh, Vector2 a, Vector2 b, Color c, float w)
        {
            Vector2 dir = (b - a);
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector2 n = new Vector2(-dir.y, dir.x).normalized * (w * 0.5f);
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert; v.color = c;
            v.position = a + n; vh.AddVert(v);
            v.position = a - n; vh.AddVert(v);
            v.position = b + n; vh.AddVert(v);
            v.position = b - n; vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 1, start + 3);
        }

        void AddDot(VertexHelper vh, Vector2 center, Color c, float r)
        {
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert; v.color = c;
            v.position = center + new Vector2(-r, -r); vh.AddVert(v);
            v.position = center + new Vector2(r, -r); vh.AddVert(v);
            v.position = center + new Vector2(r, r); vh.AddVert(v);
            v.position = center + new Vector2(-r, r); vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width < 10 || rect.height < 10) return;

            Vector2 Map(Vector2 p)
            {
                float x = Mathf.Lerp(plotMin.x, plotMax.x, Mathf.Clamp01(p.x)) * rect.width;
                float y = Mathf.Lerp(plotMin.y, plotMax.y, Mathf.Clamp01(p.y)) * rect.height;
                // 使用 RectTransform 的真实局部边界，不假设 pivot 永远在中心。
                // 绝大多数面板以左上为 pivot；旧算法固定减半宽/半高，
                // 会把曲线整体推出卡片并压到相邻按钮上。
                return new Vector2(rect.xMin + x, rect.yMin + y);
            }

            // 网格
            for (int i = 0; i <= 4; i++)
            {
                float step = i / 4f;
                AddSeg(vh, Map(new Vector2(step, 0)), Map(new Vector2(step, 1)), gridColor, 1f);
                AddSeg(vh, Map(new Vector2(0, step)), Map(new Vector2(1, step)), gridColor, 1f);
            }
            // 坐标轴
            AddSeg(vh, Map(new Vector2(0, 0)), Map(new Vector2(1, 0)), axisColor, 2.5f);
            AddSeg(vh, Map(new Vector2(0, 0)), Map(new Vector2(0, 1)), axisColor, 2.5f);

            // 拟合曲线
            for (int i = 1; i < fit.Count; i++) AddSeg(vh, Map(fit[i - 1]), Map(fit[i]), fitColor, lineWidth * 0.9f);
            // 第二系列(细线)
            for (int i = 1; i < pointsB.Count; i++) AddSeg(vh, Map(pointsB[i - 1]), Map(pointsB[i]), lineColorB, lineWidth * 0.85f);
            // 折线
            if (connectPrimaryPoints)
                for (int i = 1; i < points.Count; i++)
                    AddSeg(vh, Map(points[i - 1]), Map(points[i]), lineColor, lineWidth);
            // 数据点
            for (int i = 0; i < points.Count; i++) AddDot(vh, Map(points[i]), pointColor, pointSize);
            if (hasCurrentPoint) AddDot(vh, Map(currentPoint), UiTheme.Accent, pointSize * 1.8f);
            if (hoverIndex >= 0 && hoverIndex < points.Count)
                AddDot(vh, Map(points[hoverIndex]), Color.white, pointSize * 1.45f);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (readoutPinned) return;
            UpdateReadout(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            readoutPinned = !readoutPinned;
            // 锁定状态切换会改变 readout 后缀,强制刷新一次。
            UpdateReadout(eventData, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (readoutPinned) return;
            if (hoverIndex == -1) return;
            hoverIndex = -1;
            if (readout != null) readout.text = "移动到数据点查看数值";
            SetVerticesDirty();
        }

        void UpdateReadout(PointerEventData eventData, bool force = false)
        {
            if (points.Count == 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            Rect rect = rectTransform.rect;
            Vector2 normalized = new Vector2((local.x - rect.xMin) / Mathf.Max(1f, rect.width),
                (local.y - rect.yMin) / Mathf.Max(1f, rect.height));
            int nearest = -1;
            float nearestDistance = .045f * .045f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 mapped = new Vector2(Mathf.Lerp(plotMin.x, plotMax.x, points[i].x),
                    Mathf.Lerp(plotMin.y, plotMax.y, points[i].y));
                float distance = (mapped - normalized).sqrMagnitude;
                if (distance < nearestDistance) { nearestDistance = distance; nearest = i; }
            }
            // 只在 hover 的数据点真正变化时才更新文本与重建网格:指针在图上扫动时
            // 每次移动事件都 SetVerticesDirty 是整图(网格线/轴/全部序列顶点)重建(P2-12)。
            int previousHover = hoverIndex;
            hoverIndex = nearest;
            if (!force && hoverIndex == previousHover) return;
            if (readout != null)
            {
                if (nearest < 0) readout.text = "移动到数据点查看数值";
                else
                {
                    float x = Mathf.Lerp(axisXMin, axisXMax, points[nearest].x);
                    float y = Mathf.Lerp(axisYMin, axisYMax, points[nearest].y);
                    readout.text = axisXTitle + " " + FormatAxis(x, axisXSuffix) + "  ·  " +
                        axisYTitle + " " + FormatAxis(y, axisYSuffix) + (readoutPinned ? "  ·  已锁定" : "");
                }
            }
            SetVerticesDirty();
        }
    }
}
