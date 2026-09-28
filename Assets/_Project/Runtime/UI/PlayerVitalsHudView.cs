using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerVitalsHudView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private PlayerMana mana;
        [FormerlySerializedAs("label")]
        [SerializeField] private Text legacyLabel;
        [SerializeField] private HudResourceBarView healthBar;
        [SerializeField] private HudResourceBarView staminaBar;
        [SerializeField] private HudResourceBarView manaBar;
        [SerializeField] private Text defeatedLabel;

        public PlayerHealth Health => health;
        public PlayerStamina Stamina => stamina;
        public PlayerMana Mana => mana;

        private void Awake()
        {
            if (legacyLabel != null)
            {
                GameUiTheme.StyleText(legacyLabel, GameUiTextRole.Heading);
                GameUiTheme.EnsureTextShadow(legacyLabel, 2f);
            }
            GameUiTheme.StyleText(defeatedLabel, GameUiTextRole.Danger);
            Refresh();
        }

        private void OnEnable()
        {
            if (health != null) health.HealthChanged += OnHealthChanged;
            if (stamina != null) stamina.StaminaChanged += OnStaminaChanged;
            if (mana != null) mana.ManaChanged += OnManaChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
            if (stamina != null) stamina.StaminaChanged -= OnStaminaChanged;
            if (mana != null) mana.ManaChanged -= OnManaChanged;
        }

        public void Configure(
            PlayerHealth healthSource,
            PlayerStamina staminaSource,
            PlayerMana manaSource,
            HudResourceBarView healthView,
            HudResourceBarView staminaView,
            HudResourceBarView manaView,
            Text defeatedText)
        {
            health = healthSource;
            stamina = staminaSource;
            mana = manaSource;
            healthBar = healthView;
            staminaBar = staminaView;
            manaBar = manaView;
            defeatedLabel = defeatedText;
            if (legacyLabel != null) legacyLabel.enabled = false;
            GameUiTheme.StyleText(defeatedLabel, GameUiTextRole.Danger);
            Refresh();
        }

        public void Refresh()
        {
            healthBar?.Render("HEALTH", health == null ? 0f : health.CurrentHealth, health == null ? 0f : health.MaximumHealth, GameUiTheme.Danger);
            staminaBar?.Render("STAMINA", stamina == null ? 0f : stamina.CurrentStamina, stamina == null ? 0f : stamina.MaximumStamina, GameUiTheme.Success);
            manaBar?.Render("MANA", mana == null ? 0f : mana.CurrentMana, mana == null ? 0f : mana.MaximumMana, new Color(0.28f, 0.48f, 0.82f, 1f));

            bool defeated = health != null && health.IsDefeated;
            if (defeatedLabel != null)
            {
                defeatedLabel.text = defeated ? "DEFEATED - Press R to recover" : string.Empty;
                defeatedLabel.gameObject.SetActive(defeated);
            }

            if (healthBar == null && legacyLabel != null)
            {
                legacyLabel.enabled = true;
                legacyLabel.text = BuildLegacyText(health, stamina, mana, defeated);
            }
        }

        private void OnHealthChanged(int current, int maximum) => Refresh();
        private void OnStaminaChanged(float current, float maximum) => Refresh();
        private void OnManaChanged(float current, float maximum) => Refresh();

        private static string BuildLegacyText(PlayerHealth healthSource, PlayerStamina staminaSource, PlayerMana manaSource, bool defeated)
        {
            string healthText = healthSource == null ? "HEALTH   -- / --" : $"HEALTH   {healthSource.CurrentHealth} / {healthSource.MaximumHealth}";
            string staminaText = staminaSource == null ? "STAMINA  -- / --" : $"STAMINA  {staminaSource.CurrentStamina:0} / {staminaSource.MaximumStamina:0}";
            string manaText = manaSource == null ? "MANA     -- / --" : $"MANA     {manaSource.CurrentMana:0} / {manaSource.MaximumMana:0}";
            return $"{healthText}\n{staminaText}\n{manaText}{(defeated ? "\nDefeated - Press R to recover" : string.Empty)}";
        }
    }
}
