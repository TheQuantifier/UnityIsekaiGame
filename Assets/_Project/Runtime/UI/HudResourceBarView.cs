using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    [DisallowMultipleComponent]
    public sealed class HudResourceBarView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text valueLabel;

        private float lastCurrent = float.NaN;
        private float lastMaximum = float.NaN;
        private string lastName = string.Empty;

        public float DisplayedFill => fillImage == null ? 0f : fillImage.fillAmount;
        public string DisplayedValue => valueLabel == null ? string.Empty : valueLabel.text;

        private void Awake()
        {
            ApplyTheme();
        }

        public void Configure(Image fill, Text resourceName, Text value)
        {
            fillImage = fill;
            nameLabel = resourceName;
            valueLabel = value;
            ApplyTheme();
        }

        public void Render(string resourceName, float current, float maximum, Color color)
        {
            current = Sanitize(current);
            maximum = Mathf.Max(0f, Sanitize(maximum));
            if (string.Equals(lastName, resourceName, System.StringComparison.Ordinal)
                && Mathf.Approximately(lastCurrent, current)
                && Mathf.Approximately(lastMaximum, maximum))
            {
                return;
            }

            lastName = resourceName ?? string.Empty;
            lastCurrent = current;
            lastMaximum = maximum;
            if (nameLabel != null) nameLabel.text = lastName;
            if (valueLabel != null) valueLabel.text = $"{current:0} / {maximum:0}";
            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = 0;
                fillImage.fillAmount = Normalize(current, maximum);
                fillImage.color = color;
            }
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
            GameUiTheme.StyleText(nameLabel, GameUiTextRole.Heading);
            GameUiTheme.StyleText(valueLabel, GameUiTextRole.Body);
            if (nameLabel != null) nameLabel.raycastTarget = false;
            if (valueLabel != null) valueLabel.raycastTarget = false;
            if (fillImage != null) fillImage.raycastTarget = false;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
