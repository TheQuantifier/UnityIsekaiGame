using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Magic
{
    public sealed class PlayerSpellLoadoutInputController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerSpellLoadout loadout;

        private void Awake()
        {
            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            if (loadout == null)
            {
                loadout = GetComponent<PlayerSpellLoadout>();
            }
        }

        private void Update()
        {
            if (input == null || loadout == null)
            {
                return;
            }

            if (input.ConsumeSpellSlotSelection(out int slotIndex))
            {
                Report(loadout.SelectSlot(slotIndex));
            }

            if (input.ConsumeSpellCycle(out int direction))
            {
                Report(loadout.SelectNextAssignedSlot(direction));
            }
        }

        private static void Report(SpellLoadoutOperationResult result)
        {
            Debug.Log(result.Message);
            if (!result.Succeeded || result.Message.Contains("Empty"))
            {
                GameHudMessageBus.Show(result.Message);
            }
        }
    }
}
