using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    [DisallowMultipleComponent]
    public sealed class CombatTargetHudView : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform ignoredRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image panelImage;
        [SerializeField] private Image healthFill;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text healthLabel;
        [SerializeField, Min(1f)] private float maximumDistance = 60f;
        [SerializeField] private LayerMask targetLayers = ~0;

        private readonly RaycastHit[] hits = new RaycastHit[32];
        private EnemyHealth currentTarget;

        public EnemyHealth CurrentTarget => currentTarget;

        private void OnEnable()
        {
            ApplyTheme();
            SetVisible(false);
        }

        private void OnDisable() => SetTarget(null);

        private void LateUpdate()
        {
            if (PlayerCursorMode.HasOpenMenu)
            {
                SetTarget(null);
                return;
            }

            targetCamera ??= Camera.main;
            if (targetCamera == null)
            {
                SetTarget(null);
                return;
            }

            Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int count = Physics.RaycastNonAlloc(ray, hits, maximumDistance, targetLayers, QueryTriggerInteraction.Ignore);
            EnemyHealth nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == null || (ignoredRoot != null && hitTransform.IsChildOf(ignoredRoot))) continue;
                EnemyHealth health = hitTransform.GetComponentInParent<EnemyHealth>();
                if (health != null && !health.IsDefeated && hits[i].distance < nearestDistance)
                {
                    nearest = health;
                    nearestDistance = hits[i].distance;
                }
            }
            SetTarget(nearest);
        }

        public void Configure(Camera camera, Transform playerRoot, CanvasGroup group, Image panel, Image fill, Text targetName, Text value)
        {
            targetCamera = camera;
            ignoredRoot = playerRoot;
            canvasGroup = group;
            panelImage = panel;
            healthFill = fill;
            nameLabel = targetName;
            healthLabel = value;
            ApplyTheme();
            Render();
        }

        public static string CleanDisplayName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return "Target";
            string value = objectName.Replace("Prototype ", string.Empty).Replace("(Clone)", string.Empty).Trim();
            return string.IsNullOrEmpty(value) ? "Target" : value;
        }

        private void SetTarget(EnemyHealth next)
        {
            if (ReferenceEquals(currentTarget, next)) return;
            if (currentTarget != null) currentTarget.HealthChanged -= OnHealthChanged;
            currentTarget = next;
            if (currentTarget != null) currentTarget.HealthChanged += OnHealthChanged;
            Render();
        }

        private void OnHealthChanged(float current, float maximum) => Render();

        private void Render()
        {
            if (currentTarget == null)
            {
                SetVisible(false);
                return;
            }

            float current = currentTarget.CurrentHealth;
            float maximum = currentTarget.MaximumHealth;
            if (nameLabel != null) nameLabel.text = CleanDisplayName(currentTarget.name);
            if (healthLabel != null) healthLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
            if (healthFill != null) healthFill.fillAmount = HudResourceBarView.Normalize(current, maximum);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ApplyTheme()
        {
            GameUiTheme.StylePanel(panelImage, raised: true);
            GameUiTheme.StyleText(nameLabel, GameUiTextRole.Heading);
            GameUiTheme.StyleText(healthLabel, GameUiTextRole.Body);
            if (healthFill != null)
            {
                healthFill.color = GameUiTheme.Danger;
                healthFill.type = Image.Type.Filled;
                healthFill.fillMethod = Image.FillMethod.Horizontal;
                healthFill.raycastTarget = false;
            }
            if (panelImage != null) panelImage.raycastTarget = false;
            if (nameLabel != null) nameLabel.raycastTarget = false;
            if (healthLabel != null) healthLabel.raycastTarget = false;
        }
    }
}
