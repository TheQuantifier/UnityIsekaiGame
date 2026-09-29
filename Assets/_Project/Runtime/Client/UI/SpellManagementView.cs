using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    public sealed class SpellManagementView : MonoBehaviour
    {
        [SerializeField] private Button[] knownSpellButtons;
        [SerializeField] private Text[] knownSpellLabels;
        [SerializeField] private Button[] assignSlotButtons;
        [SerializeField] private Text[] assignSlotLabels;
        [SerializeField] private Button[] clearSlotButtons;
        [SerializeField] private Text[] clearSlotLabels;
        [SerializeField] private Text selectedSpellLabel;

        private Action<int> knownSpellSelected;
        private Action<int> slotAssigned;
        private Action<int> slotCleared;
        private int selectedKnownSpellIndex;

        public void Initialize(Action<int> onKnownSpellSelected, Action<int> onSlotAssigned, Action<int> onSlotCleared)
        {
            knownSpellSelected = onKnownSpellSelected;
            slotAssigned = onSlotAssigned;
            slotCleared = onSlotCleared;

            WireButtons(knownSpellButtons, InvokeKnownSpellSelected);
            WireButtons(assignSlotButtons, InvokeSlotAssigned);
            WireButtons(clearSlotButtons, InvokeSlotCleared);
            ApplyTheme();
        }

        public void Render(PlayerSpellLoadout loadout, int selectedKnownIndex)
        {
            selectedKnownSpellIndex = selectedKnownIndex;
            IReadOnlyList<SpellDefinition> knownSpells = loadout == null ? null : loadout.KnownSpells;
            IReadOnlyList<SpellDefinition> quickSlots = loadout == null ? null : loadout.QuickSlots;

            RenderKnownSpells(knownSpells);
            RenderSlots(quickSlots);

            if (selectedSpellLabel != null)
            {
                SpellDefinition selectedSpell = knownSpells != null && selectedKnownIndex >= 0 && selectedKnownIndex < knownSpells.Count
                    ? knownSpells[selectedKnownIndex]
                    : null;
                selectedSpellLabel.text = selectedSpell == null ? "Selected Spell: None" : $"Selected Spell: {selectedSpell.DisplayName}";
            }
        }

        private void RenderKnownSpells(IReadOnlyList<SpellDefinition> knownSpells)
        {
            if (knownSpellButtons == null)
            {
                return;
            }

            for (int i = 0; i < knownSpellButtons.Length; i++)
            {
                SpellDefinition spell = knownSpells != null && i < knownSpells.Count ? knownSpells[i] : null;
                if (knownSpellButtons[i] != null)
                {
                    knownSpellButtons[i].gameObject.SetActive(spell != null);
                    GameUiTheme.StyleButton(knownSpellButtons[i], i == selectedKnownSpellIndex ? GameUiButtonTone.Primary : GameUiButtonTone.Neutral);
                }

                if (knownSpellLabels != null && i < knownSpellLabels.Length && knownSpellLabels[i] != null)
                {
                    string prefix = i == selectedKnownSpellIndex ? "SELECTED  |  " : string.Empty;
                    knownSpellLabels[i].text = spell == null ? string.Empty : $"{prefix}{spell.DisplayName} ({spell.ManaCost:0} MP)";
                }
            }
        }

        private void RenderSlots(IReadOnlyList<SpellDefinition> quickSlots)
        {
            int slotCount = assignSlotButtons == null ? 0 : assignSlotButtons.Length;
            for (int i = 0; i < slotCount; i++)
            {
                SpellDefinition spell = quickSlots != null && i < quickSlots.Count ? quickSlots[i] : null;
                if (assignSlotLabels != null && i < assignSlotLabels.Length && assignSlotLabels[i] != null)
                {
                    assignSlotLabels[i].text = spell == null ? $"Assign Slot {i + 1}: Empty" : $"Replace Slot {i + 1}: {spell.DisplayName}";
                }

                if (clearSlotLabels != null && i < clearSlotLabels.Length && clearSlotLabels[i] != null)
                {
                    clearSlotLabels[i].text = $"Clear {i + 1}";
                }
            }
        }

        private void WireButtons(Button[] buttons, Action<int> callback)
        {
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    continue;
                }

                int index = i;
                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(() => callback(index));
            }
        }

        private void InvokeKnownSpellSelected(int index)
        {
            knownSpellSelected?.Invoke(index);
        }

        private void InvokeSlotAssigned(int index)
        {
            slotAssigned?.Invoke(index);
        }

        private void InvokeSlotCleared(int index)
        {
            slotCleared?.Invoke(index);
        }

        private void ApplyTheme()
        {
            StyleButtons(knownSpellButtons, GameUiButtonTone.Neutral);
            StyleButtons(assignSlotButtons, GameUiButtonTone.Primary);
            StyleButtons(clearSlotButtons, GameUiButtonTone.Danger);
            GameUiTheme.StyleText(selectedSpellLabel, GameUiTextRole.Heading);
        }

        private static void StyleButtons(Button[] buttons, GameUiButtonTone tone)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++) GameUiTheme.StyleButton(buttons[i], tone);
        }
    }
}
