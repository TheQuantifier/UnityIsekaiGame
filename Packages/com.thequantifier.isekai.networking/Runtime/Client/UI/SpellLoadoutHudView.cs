using UnityEngine;
using UnityIsekaiGame.Magic;

namespace UnityIsekaiGame.UI
{
    public sealed class SpellLoadoutHudView : MonoBehaviour
    {
        [SerializeField] private PlayerSpellLoadout loadout;
        [SerializeField] private SpellQuickSlotView[] slotViews;

        private void Awake()
        {
            Refresh();
        }

        private void OnEnable()
        {
            if (loadout != null)
            {
                loadout.SlotChanged += OnSlotChanged;
                loadout.ActiveSlotChanged += OnActiveSlotChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (loadout != null)
            {
                loadout.SlotChanged -= OnSlotChanged;
                loadout.ActiveSlotChanged -= OnActiveSlotChanged;
            }
        }

        public void Configure(PlayerSpellLoadout playerLoadout, SpellQuickSlotView[] views)
        {
            loadout = playerLoadout;
            slotViews = views;
            Refresh();
        }

        public void Refresh()
        {
            if (slotViews == null)
            {
                return;
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].Render(i, loadout == null ? null : loadout.GetSlotSpell(i), loadout != null && i == loadout.SelectedSlotIndex);
                }
            }
        }

        private void OnSlotChanged(SpellLoadoutSlotChangedEventArgs args)
        {
            Refresh();
        }

        private void OnActiveSlotChanged(int slotIndex, SpellDefinition spell)
        {
            Refresh();
        }
    }
}
