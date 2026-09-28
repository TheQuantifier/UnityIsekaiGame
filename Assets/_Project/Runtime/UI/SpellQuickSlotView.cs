using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    public sealed class SpellQuickSlotView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text label;
        [SerializeField] private Color normalColor = new Color(0.1f, 0.12f, 0.14f, 0.82f);
        [SerializeField] private Color selectedColor = new Color(0.18f, 0.45f, 0.7f, 0.9f);

        public void Render(int slotIndex, SpellDefinition spell, bool selected)
        {
            normalColor = GameUiTheme.PanelRaised;
            selectedColor = GameUiTheme.AccentSoft;
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? selectedColor : normalColor;
            }

            if (label != null)
            {
                GameUiTheme.StyleText(label, selected ? GameUiTextRole.Heading : GameUiTextRole.Body);
                string spellText = spell == null ? "Empty" : $"{spell.DisplayName}\n{spell.ManaCost:0} MP";
                label.text = $"{slotIndex + 1}\n{spellText}";
            }
        }
    }
}
