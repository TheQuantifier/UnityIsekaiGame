using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    public sealed class SpellQuickSlotView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private Text shortcutLabel;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text costLabel;
        [SerializeField] private Text label;

        private SpellDefinition displayedSpell;
        private int displayedSlot = -1;
        private bool displayedSelected;

        public void Configure(Image background, Image selectedFrame, Text shortcut, Text spellName, Text cost)
        {
            if (label != null && label != shortcut && label != spellName && label != cost)
            {
                label.enabled = false;
            }
            label = null;
            backgroundImage = background;
            selectionFrame = selectedFrame;
            shortcutLabel = shortcut;
            nameLabel = spellName;
            costLabel = cost;
            ApplyTheme();
        }

        public void Render(int slotIndex, SpellDefinition spell, bool selected)
        {
            if (displayedSlot == slotIndex && displayedSpell == spell && displayedSelected == selected)
            {
                return;
            }
            displayedSlot = slotIndex;
            displayedSpell = spell;
            displayedSelected = selected;

            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? GameUiTheme.AccentSoft : GameUiTheme.PanelRaised;
            }

            if (selectionFrame != null)
            {
                selectionFrame.enabled = selected;
                selectionFrame.color = GameUiTheme.AccentBright;
            }

            if (shortcutLabel != null) shortcutLabel.text = (slotIndex + 1).ToString();
            if (nameLabel != null) nameLabel.text = spell == null ? "Empty" : spell.DisplayName;
            if (costLabel != null) costLabel.text = spell == null ? string.Empty : FormatCost(spell.ManaCost);

            if (label != null)
            {
                GameUiTheme.StyleText(label, selected ? GameUiTextRole.Heading : GameUiTextRole.Body);
                string spellText = spell == null ? "Empty" : $"{spell.DisplayName}\n{spell.ManaCost:0} MP";
                label.text = $"{slotIndex + 1}\n{spellText}";
            }
        }

        public static string FormatCost(float manaCost) => $"{Mathf.Max(0f, manaCost):0} MP";

        private void ApplyTheme()
        {
            GameUiTheme.StylePanel(backgroundImage, raised: true);
            GameUiTheme.StyleText(shortcutLabel, GameUiTextRole.Title);
            GameUiTheme.StyleText(nameLabel, GameUiTextRole.Heading);
            GameUiTheme.StyleText(costLabel, GameUiTextRole.Muted);
            if (backgroundImage != null) backgroundImage.raycastTarget = false;
            if (selectionFrame != null) selectionFrame.raycastTarget = false;
            if (shortcutLabel != null) shortcutLabel.raycastTarget = false;
            if (nameLabel != null) nameLabel.raycastTarget = false;
            if (costLabel != null) costLabel.raycastTarget = false;
        }
    }
}
