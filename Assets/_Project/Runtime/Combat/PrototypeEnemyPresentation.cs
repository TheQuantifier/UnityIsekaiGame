using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.Combat
{
    public class PrototypeEnemyPresentation : MonoBehaviour
    {
        [SerializeField] private EnemyHealth health;
        [SerializeField] private Text healthLabel;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color hitColor = new Color(1f, 0.35f, 0.25f);
        [SerializeField] private Color defeatedColor = new Color(0.15f, 0.15f, 0.15f);
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.12f;

        private Coroutine flashRoutine;
        private float previousHealth = -1f;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            PrototypeUiTheme.StyleText(healthLabel, PrototypeUiTextRole.Body);
            PrototypeUiTheme.EnsureTextShadow(healthLabel, 2f);

            SetColor(normalColor);
            previousHealth = health == null ? -1f : health.CurrentHealth;
            Refresh();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
                health.Defeated += OnDefeated;
            }

            previousHealth = health == null ? -1f : health.CurrentHealth;
            Refresh();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                health.Defeated -= OnDefeated;
            }
        }

        private void OnValidate()
        {
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
        }

        private void OnHealthChanged(float currentHealth, float maximumHealth)
        {
            bool tookDamage = previousHealth >= 0f && currentHealth < previousHealth;
            previousHealth = currentHealth;

            if (tookDamage && health != null && !health.IsDefeated)
            {
                FlashHitColor();
            }

            if (!tookDamage && health != null && !health.IsDefeated && flashRoutine == null)
            {
                SetColor(normalColor);
            }

            Refresh();
        }

        private void OnDefeated()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            SetColor(defeatedColor);
            Refresh();
        }

        private void FlashHitColor()
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            SetColor(hitColor);
            yield return new WaitForSeconds(hitFlashDuration);
            SetColor(health != null && health.IsDefeated ? defeatedColor : normalColor);
            flashRoutine = null;
        }

        private void Refresh()
        {
            if (healthLabel == null || health == null)
            {
                return;
            }

            string status = health.IsDefeated ? "Defeated" : $"{health.CurrentHealth:0} / {health.MaximumHealth:0}";
            healthLabel.text = $"{name}: {status}";
            float ratio = health.MaximumHealth <= 0f ? 0f : health.CurrentHealth / health.MaximumHealth;
            healthLabel.color = health.IsDefeated
                ? PrototypeUiTheme.TextMuted
                : ratio <= 0.25f
                    ? PrototypeUiTheme.Danger
                    : PrototypeUiTheme.TextPrimary;
        }

        private void SetColor(Color color)
        {
            if (targetRenderer != null)
            {
                targetRenderer.material.color = color;
            }
        }
    }
}
