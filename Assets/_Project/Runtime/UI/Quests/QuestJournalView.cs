using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.UI.Quests
{
    public sealed class QuestJournalView : MonoBehaviour
    {
        [SerializeField] private Transform questListRoot;
        [SerializeField] private Button questButtonTemplate;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private Text objectiveLabel;
        [SerializeField] private Text rewardLabel;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private Button abandonButton;
        [SerializeField] private Button claimRewardButton;

        private readonly List<Button> questButtons = new List<Button>();
        private Action<int> questSelected;
        private Action abandonRequested;
        private Action rewardClaimRequested;

        private void Awake()
        {
            if (questButtonTemplate != null) questButtonTemplate.gameObject.SetActive(false);
        }

        public void Initialize(Action<int> onQuestSelected, Action onAbandonRequested, Action onRewardClaimRequested)
        {
            questSelected = onQuestSelected;
            abandonRequested = onAbandonRequested;
            rewardClaimRequested = onRewardClaimRequested;
            if (abandonButton != null)
            {
                abandonButton.onClick.RemoveListener(InvokeAbandonRequested);
                abandonButton.onClick.AddListener(InvokeAbandonRequested);
            }
            if (claimRewardButton != null)
            {
                claimRewardButton.onClick.RemoveListener(InvokeRewardClaimRequested);
                claimRewardButton.onClick.AddListener(InvokeRewardClaimRequested);
            }
        }

        public void Render(IReadOnlyList<PrototypeQuestJournalEntry> quests, int selectedIndex)
        {
            RenderQuestList(quests, selectedIndex);
            PrototypeQuestJournalEntry selected = quests != null && selectedIndex >= 0 && selectedIndex < quests.Count ? quests[selectedIndex] : null;
            RenderDetails(selected);
        }

        public void SetFeedback(string message)
        {
            if (feedbackLabel != null) feedbackLabel.text = message;
        }

        private void RenderQuestList(IReadOnlyList<PrototypeQuestJournalEntry> quests, int selectedIndex)
        {
            ClearQuestButtons();
            if (quests == null || questButtonTemplate == null || questListRoot == null) return;
            for (int i = 0; i < quests.Count; i++)
            {
                int index = i;
                PrototypeQuestJournalEntry quest = quests[i];
                Button button = Instantiate(questButtonTemplate, questListRoot);
                button.gameObject.SetActive(true);
                button.onClick.AddListener(() => questSelected?.Invoke(index));
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    string marker = i == selectedIndex ? "> " : string.Empty;
                    label.text = $"{marker}{quest.Title}\n{quest.Assignment?.LifecycleState}";
                }
                questButtons.Add(button);
            }
        }

        private void RenderDetails(PrototypeQuestJournalEntry quest)
        {
            if (titleLabel != null) titleLabel.text = quest?.Title ?? "No Quest Selected";
            if (descriptionLabel != null) descriptionLabel.text = quest == null ? "Browse a quest source to accept work." : $"{quest.Summary}\nState: {quest.Assignment?.LifecycleState}";
            if (objectiveLabel != null) objectiveLabel.text = BuildObjectiveText(quest);
            if (rewardLabel != null) rewardLabel.text = BuildRewardText(quest);
            if (abandonButton != null) abandonButton.gameObject.SetActive(quest?.CanAbandon == true);
            if (claimRewardButton != null) claimRewardButton.gameObject.SetActive(quest?.ClaimableReward != null);
        }

        private static string BuildObjectiveText(PrototypeQuestJournalEntry quest)
        {
            if (quest == null || quest.Objectives.Count == 0) return "Objectives: None";
            StringBuilder builder = new StringBuilder("Objectives:");
            foreach (QuestObjectiveSnapshot objective in quest.Objectives)
            {
                builder.AppendLine();
                builder.Append("- ");
                builder.Append(objective.Category);
                builder.Append(" (");
                builder.Append(objective.CurrentValue);
                builder.Append(" / ");
                builder.Append(objective.TargetValue);
                if (objective.Satisfied) builder.Append(", complete");
                builder.Append(')');
            }
            return builder.ToString();
        }

        private static string BuildRewardText(PrototypeQuestJournalEntry quest)
        {
            if (quest == null || quest.Rewards.Count == 0) return "Rewards: Pending or none";
            return "Rewards:\n" + string.Join("\n", quest.Rewards.Select(value => $"- {value.Quantity} {value.Category}: {value.TargetDefinitionId} ({value.State})"));
        }

        private void ClearQuestButtons()
        {
            for (int i = questButtons.Count - 1; i >= 0; i--)
                if (questButtons[i] != null) Destroy(questButtons[i].gameObject);
            questButtons.Clear();
        }

        private void InvokeAbandonRequested() => abandonRequested?.Invoke();
        private void InvokeRewardClaimRequested() => rewardClaimRequested?.Invoke();
    }
}
