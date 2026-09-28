using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.UI
{
    [DisallowMultipleComponent]
    public sealed class QuestTrackerHudView : MonoBehaviour
    {
        [SerializeField] private PrototypePersistenceServiceBehaviour services;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image panelImage;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text objectiveLabel;

        private PrototypeNarrativeCoordinator coordinator;
        private float nextResolveTime;

        public string DisplayedTitle => titleLabel == null ? string.Empty : titleLabel.text;
        public string DisplayedObjective => objectiveLabel == null ? string.Empty : objectiveLabel.text;

        private void OnEnable()
        {
            ApplyTheme();
            TryBind();
        }

        private void OnDisable() => Unbind();

        private void Update()
        {
            if (coordinator == null && Time.unscaledTime >= nextResolveTime)
            {
                nextResolveTime = Time.unscaledTime + 0.5f;
                TryBind();
            }
        }

        public void Configure(PrototypePersistenceServiceBehaviour persistence, CanvasGroup group, Image panel, Text title, Text objective)
        {
            Unbind();
            services = persistence;
            canvasGroup = group;
            panelImage = panel;
            titleLabel = title;
            objectiveLabel = objective;
            ApplyTheme();
            TryBind();
        }

        public void Refresh()
        {
            PrototypeQuestJournalEntry quest = coordinator?.GetJournal()?.FirstOrDefault(entry => entry != null && !entry.IsTerminal);
            if (quest == null)
            {
                SetVisible(false);
                return;
            }

            if (titleLabel != null) titleLabel.text = quest.Title;
            if (objectiveLabel != null) objectiveLabel.text = BuildObjectiveText(quest);
            SetVisible(true);
        }

        public static string BuildObjectiveText(PrototypeQuestJournalEntry quest)
        {
            if (quest == null) return string.Empty;
            QuestObjectiveSnapshot objective = quest.Objectives?.FirstOrDefault(value => value != null && !value.Satisfied);
            if (objective == null) return "Return for your reward";

            string label = Humanize(objective.ObjectiveDefinitionId);
            int target = Mathf.Max(1, objective.TargetValue);
            return $"{label}  {Mathf.Clamp(objective.CurrentValue, 0, target)}/{target}";
        }

        public static string Humanize(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return "Continue the objective";
            string leaf = identifier.Trim().Split('.').LastOrDefault() ?? identifier.Trim();
            string spaced = leaf.Replace('-', ' ').Replace('_', ' ');
            if (string.IsNullOrWhiteSpace(spaced)) return "Continue the objective";
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        private void TryBind()
        {
            services ??= FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            PrototypeNarrativeCoordinator resolved = services?.NarrativeCoordinator;
            if (ReferenceEquals(resolved, coordinator)) return;
            Unbind();
            coordinator = resolved;
            if (coordinator != null)
            {
                coordinator.Changed += Refresh;
                Refresh();
            }
            else
            {
                SetVisible(false);
            }
        }

        private void Unbind()
        {
            if (coordinator != null) coordinator.Changed -= Refresh;
            coordinator = null;
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
            GameUiTheme.StyleText(titleLabel, GameUiTextRole.Title);
            GameUiTheme.StyleText(objectiveLabel, GameUiTextRole.Body);
            if (panelImage != null) panelImage.raycastTarget = false;
            if (titleLabel != null) titleLabel.raycastTarget = false;
            if (objectiveLabel != null) objectiveLabel.raycastTarget = false;
        }
    }
}
