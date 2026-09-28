using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    public sealed class HealthReadoutView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private PlayerMana mana;
        [SerializeField] private Text label;

        private string lastRenderedText = string.Empty;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponent<Text>();
            }

            GameUiTheme.StyleText(label, GameUiTextRole.Heading);
            GameUiTheme.EnsureTextShadow(label, 2f);

            Refresh();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
            }

            if (stamina != null)
            {
                stamina.StaminaChanged += OnStaminaChanged;
            }

            if (mana != null)
            {
                mana.ManaChanged += OnManaChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
            }

            if (stamina != null)
            {
                stamina.StaminaChanged -= OnStaminaChanged;
            }

            if (mana != null)
            {
                mana.ManaChanged -= OnManaChanged;
            }
        }

        private void OnHealthChanged(int currentHealth, int maximumHealth)
        {
            Refresh();
        }

        private void OnStaminaChanged(float currentStamina, float maximumStamina)
        {
            Refresh();
        }

        private void OnManaChanged(float currentMana, float maximumMana)
        {
            Refresh();
        }

        private void Refresh()
        {
            SetText();
        }

        private void SetText()
        {
            if (label == null)
            {
                return;
            }

            string healthText = health == null ? "HEALTH   -- / --" : $"HEALTH   {health.CurrentHealth} / {health.MaximumHealth}";
            string staminaText = stamina == null ? "STAMINA  -- / --" : $"STAMINA  {stamina.CurrentStamina:0} / {stamina.MaximumStamina:0}";
            string manaText = mana == null ? "MANA     -- / --" : $"MANA     {mana.CurrentMana:0} / {mana.MaximumMana:0}";
            string defeatedText = health != null && health.IsDefeated ? "\nDefeated - Press R to reset" : string.Empty;
            string text = $"{healthText}\n{staminaText}\n{manaText}{defeatedText}";
            if (string.Equals(text, lastRenderedText, System.StringComparison.Ordinal))
            {
                return;
            }

            lastRenderedText = text;
            label.text = text;
        }
    }
}
