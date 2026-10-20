using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EconTown
{
    /// <summary>
    /// 保留旧 Text 作为布局与 InputField 数据源，用 TMP SDF 子节点负责清晰显示。
    /// 字体缺少特殊符号时自动回退到系统字体，避免方框字。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrispTextMirror : MonoBehaviour
    {
        Text source;
        TextMeshProUGUI rendered;
        TMP_FontAsset fontAsset;
        string lastText;
        int lastStyleHash = int.MinValue;
        bool usingLegacyFallback;

        public Text Source { get { return source; } }
        public TextMeshProUGUI Rendered { get { return rendered; } }
        public bool UsingLegacyFallback { get { return usingLegacyFallback; } }

        public float VisiblePreferredHeight()
        {
            SyncNow(true);
            if (rendered != null && rendered.enabled)
            {
                rendered.ForceMeshUpdate(true);
                return rendered.preferredHeight;
            }
            return source != null ? source.preferredHeight : 0f;
        }

        public static CrispTextMirror Attach(Text text, TMP_FontAsset font)
        {
            if (text == null || font == null) return null;
            CrispTextMirror mirror = text.GetComponent<CrispTextMirror>();
            if (mirror == null) mirror = text.gameObject.AddComponent<CrispTextMirror>();
            mirror.Initialize(text, font);
            return mirror;
        }

        void Initialize(Text text, TMP_FontAsset font)
        {
            source = text;
            fontAsset = font;
            if (rendered == null)
            {
                GameObject go = new GameObject("sdfText", typeof(RectTransform), typeof(CanvasRenderer));
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                rendered = go.AddComponent<TextMeshProUGUI>();
                rendered.font = fontAsset;
                rendered.raycastTarget = false;
                rendered.rectTransform.Stretch();
                rendered.margin = Vector4.zero;
                go.SetActive(true);
            }
            SyncNow(true);
        }

        void LateUpdate()
        {
            SyncNow(false);
        }

        public void SyncNow(bool force)
        {
            if (source == null || rendered == null || fontAsset == null) return;
            int styleHash = StyleHash(source);
            bool textChanged = lastText != source.text;
            if (!force && !textChanged && styleHash == lastStyleHash) return;

            if (textChanged || force)
            {
                string visibleCharacters = VisibleCharacters(source.text);
                bool complete = string.IsNullOrEmpty(visibleCharacters) || fontAsset.HasCharacters(visibleCharacters);
                if (!complete)
                {
                    fontAsset.TryAddCharacters(visibleCharacters, false);
                    complete = fontAsset.HasCharacters(visibleCharacters);
                }
                usingLegacyFallback = !complete;
                lastText = source.text;
            }

            rendered.text = source.text;
            rendered.color = source.color;
            rendered.fontSize = Mathf.Max(UiTheme.MinimumBodyFontSize, source.fontSize);
            rendered.fontStyle = ToTmpStyle(source.fontStyle);
            rendered.fontWeight = source.fontStyle == FontStyle.Bold || source.fontStyle == FontStyle.BoldAndItalic
                ? FontWeight.Bold : FontWeight.Regular;
            rendered.alignment = ToTmpAlignment(source.alignment);
            rendered.richText = source.supportRichText;
            rendered.textWrappingMode = source.horizontalOverflow == HorizontalWrapMode.Wrap
                ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            rendered.overflowMode = source.verticalOverflow == VerticalWrapMode.Overflow
                ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
            rendered.enableAutoSizing = source.resizeTextForBestFit;
            if (source.resizeTextForBestFit)
            {
                rendered.fontSizeMin = source.resizeTextMinSize;
                rendered.fontSizeMax = source.resizeTextMaxSize;
            }
            rendered.lineSpacing = source.lineSpacing - 1f;
            rendered.raycastTarget = source.raycastTarget;

            rendered.enabled = !usingLegacyFallback;
            source.enabled = usingLegacyFallback;
            lastStyleHash = styleHash;
            rendered.SetAllDirty();
        }

        static int StyleHash(Text text)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + text.fontSize;
                hash = hash * 31 + (int)text.fontStyle;
                hash = hash * 31 + (int)text.alignment;
                hash = hash * 31 + text.color.GetHashCode();
                hash = hash * 31 + (int)text.horizontalOverflow;
                hash = hash * 31 + (int)text.verticalOverflow;
                hash = hash * 31 + (text.supportRichText ? 1 : 0);
                hash = hash * 31 + (text.resizeTextForBestFit ? 1 : 0);
                return hash;
            }
        }

        static string VisibleCharacters(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder result = new StringBuilder(value.Length);
            bool inTag = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '<') { inTag = true; continue; }
                if (inTag)
                {
                    if (c == '>') inTag = false;
                    continue;
                }
                if (!char.IsControl(c)) result.Append(c);
            }
            return result.ToString();
        }

        static FontStyles ToTmpStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold: return FontStyles.Bold;
                case FontStyle.Italic: return FontStyles.Italic;
                case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
                default: return FontStyles.Normal;
            }
        }

        static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Left;
            }
        }
    }
}
