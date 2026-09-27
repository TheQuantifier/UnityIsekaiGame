using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    public sealed class StatusEffectReadoutView : MonoBehaviour
    {
        private const float TimedStatusRefreshIntervalSeconds = 0.1f;

        [SerializeField] private StatusEffectController statusController;
        [SerializeField] private Text label;

        private readonly StringBuilder textBuilder = new StringBuilder(192);
        private bool subscribed;
        private bool hasVisibleTimedStatus;
        private float nextTimedStatusRefreshAt;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponent<Text>();
            }

            PrototypeUiTheme.StyleText(label, PrototypeUiTextRole.Body);
            PrototypeUiTheme.EnsureTextShadow(label);

            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!hasVisibleTimedStatus || Time.unscaledTime < nextTimedStatusRefreshAt)
            {
                return;
            }

            Refresh();
        }

        public void SetStatusController(StatusEffectController controller)
        {
            if (statusController == controller)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            statusController = controller;
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (statusController == null)
            {
                return;
            }

            if (subscribed)
            {
                return;
            }

            statusController.StatusAdded += OnStatusChanged;
            statusController.StatusChanged += OnStatusChanged;
            statusController.StatusRemoved += OnStatusChanged;
            statusController.StatusExpired += OnStatusChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (statusController == null || !subscribed)
            {
                return;
            }

            statusController.StatusAdded -= OnStatusChanged;
            statusController.StatusChanged -= OnStatusChanged;
            statusController.StatusRemoved -= OnStatusChanged;
            statusController.StatusExpired -= OnStatusChanged;
            subscribed = false;
        }

        private void OnStatusChanged(RuntimeStatusEffect status)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (label == null)
            {
                label = GetComponent<Text>();
                if (label == null)
                {
                    return;
                }
            }

            StringBuilder builder = textBuilder;
            builder.Clear();
            builder.AppendLine("STATUS EFFECTS");
            hasVisibleTimedStatus = false;

            if (statusController == null || statusController.ActiveStatuses.Count == 0)
            {
                builder.Append("None");
                label.text = builder.ToString();
                return;
            }

            bool appendedAny = false;
            for (int i = 0; i < statusController.ActiveStatuses.Count; i++)
            {
                RuntimeStatusEffect status = statusController.ActiveStatuses[i];
                if (!status.IsActive || !status.Definition.VisibleInHud)
                {
                    continue;
                }

                appendedAny = true;
                builder.Append("• ");
                builder.Append(status.Definition.DisplayName);
                if (status.StackCount > 1)
                {
                    builder.Append(" x");
                    builder.Append(status.StackCount);
                }

                if (status.Definition.DurationModel == StatusDurationModel.Timed)
                {
                    hasVisibleTimedStatus = true;
                    builder.Append(" (");
                    builder.Append(status.RemainingDuration.ToString("0.0"));
                    builder.Append("s)");
                }
                else if (status.Definition.DurationModel == StatusDurationModel.Persistent)
                {
                    builder.Append(" (Persistent)");
                }

                builder.AppendLine();
            }

            if (!appendedAny)
            {
                builder.Append("None");
            }

            label.text = builder.ToString().TrimEnd();
            nextTimedStatusRefreshAt = Time.unscaledTime + TimedStatusRefreshIntervalSeconds;
        }
    }
}
