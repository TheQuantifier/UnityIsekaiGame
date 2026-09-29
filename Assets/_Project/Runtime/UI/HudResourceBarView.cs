using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    [DisallowMultipleComponent]
    public sealed class HudResourceBarView : MonoBehaviour
    {
        private static readonly Color TubeTrackColor = new Color(0.12f, 0.07f, 0.035f, 0.72f);
        private static readonly Color TubeHighlightColor = new Color(1f, 0.96f, 0.82f, 0.18f);

        [SerializeField] private Image trackImage;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image liquidHighlightImage;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text valueLabel;

        private float displayedFill;
        private float lastCurrent = float.NaN;
        private float lastMaximum = float.NaN;
        private string lastName = string.Empty;
        private Color lastColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);

        public float DisplayedFill => displayedFill;
        public string DisplayedValue => valueLabel == null ? string.Empty : valueLabel.text;

        private void Awake()
        {
            ApplyTheme();
        }

        public void Configure(Image fill, Text resourceName, Text value)
        {
            Configure(GetComponent<Image>(), fill, fill == null ? null : fill.transform.Find("Liquid Highlight")?.GetComponent<Image>(), resourceName, value);
        }

        public void Configure(Image track, Image fill, Image liquidHighlight, Text resourceName, Text value)
        {
            trackImage = track;
            fillImage = fill;
            liquidHighlightImage = liquidHighlight;
            nameLabel = resourceName;
            valueLabel = value;
            lastCurrent = float.NaN;
            lastMaximum = float.NaN;
            lastName = string.Empty;
            lastColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
            ApplyTheme();
        }

        public void Render(string resourceName, float current, float maximum, Color color)
        {
            current = Sanitize(current);
            maximum = Mathf.Max(0f, Sanitize(maximum));
            if (string.Equals(lastName, resourceName, System.StringComparison.Ordinal)
                && Mathf.Approximately(lastCurrent, current)
                && Mathf.Approximately(lastMaximum, maximum)
                && lastColor == color)
            {
                return;
            }

            lastName = resourceName ?? string.Empty;
            lastCurrent = current;
            lastMaximum = maximum;
            lastColor = color;
            displayedFill = Normalize(current, maximum);
            if (nameLabel != null) nameLabel.text = lastName;
            if (valueLabel != null) valueLabel.text = $"{current:0} / {maximum:0}";
            if (fillImage != null)
            {
                fillImage.type = Image.Type.Sliced;
                fillImage.fillAmount = 1f;
                fillImage.color = new Color(color.r, color.g, color.b, 0.9f);
            }
            ApplyFillLayout();
        }

        public static float Normalize(float current, float maximum)
        {
            if (float.IsNaN(current) || float.IsInfinity(current) || float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(current / maximum);
        }

        private void ApplyTheme()
        {
            if (trackImage == null) trackImage = GetComponent<Image>();
            if (trackImage != null)
            {
                GameUiTheme.StylePanel(trackImage, raised: true);
                trackImage.color = TubeTrackColor;
                trackImage.raycastTarget = false;
            }
            if (fillImage != null)
            {
                GameUiTheme.StylePanel(fillImage);
                fillImage.type = Image.Type.Sliced;
                fillImage.raycastTarget = false;
            }
            if (liquidHighlightImage != null)
            {
                GameUiTheme.StylePanel(liquidHighlightImage);
                liquidHighlightImage.type = Image.Type.Sliced;
                liquidHighlightImage.color = TubeHighlightColor;
                liquidHighlightImage.raycastTarget = false;
                Outline outline = liquidHighlightImage.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
            }
            GameUiTheme.StyleText(nameLabel, GameUiTextRole.Heading);
            GameUiTheme.StyleText(valueLabel, GameUiTextRole.Body);
            if (nameLabel != null) nameLabel.raycastTarget = false;
            if (valueLabel != null) valueLabel.raycastTarget = false;
            ApplyFillLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyFillLayout();
        }

        private void ApplyFillLayout()
        {
            if (fillImage == null || fillImage.transform is not RectTransform fillRect)
            {
                return;
            }

            RectTransform trackRect = transform as RectTransform;
            float trackWidth = trackRect == null ? 0f : trackRect.rect.width;
            float trackHeight = trackRect == null ? 0f : trackRect.rect.height;
            const float inset = 3f;
            float width = Mathf.Max(0f, trackWidth - inset * 2f) * Mathf.Clamp01(displayedFill);
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(inset, 0f);
            fillRect.sizeDelta = new Vector2(width, Mathf.Max(0f, trackHeight - inset * 2f));
            fillImage.enabled = displayedFill > 0f && width > 0.5f;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
