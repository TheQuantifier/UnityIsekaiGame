using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class SaveLoadMenuView : MonoBehaviour
    {
        private static readonly SaveLoadAction[] Actions =
        {
            SaveLoadAction.Save,
            SaveLoadAction.Load,
            SaveLoadAction.Validate,
            SaveLoadAction.LoadBackup,
            SaveLoadAction.Delete,
            SaveLoadAction.ForceAutosave
        };

        private readonly List<SaveSlotDescriptor> descriptors = new List<SaveSlotDescriptor>();

        private PrototypePersistenceServiceBehaviour persistence;
        private Text slotValueText;
        private Text actionValueText;
        private Text detailsText;
        private Text feedbackText;
        private Button executeButton;
        private Button previousSlotButton;
        private Button nextSlotButton;
        private Button previousActionButton;
        private Button nextActionButton;
        private int selectedSlotIndex;
        private int selectedActionIndex;
        private string pendingConfirmationKey;

        public void Initialize(PrototypePersistenceServiceBehaviour persistenceService)
        {
            if (persistence != null && persistence.PlayerService != null)
            {
                persistence.PlayerService.SaveSlotsChanged -= Refresh;
            }

            persistence = persistenceService;
            BuildUi();
            if (persistence != null && persistence.PlayerService != null)
            {
                persistence.PlayerService.SaveSlotsChanged += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (persistence != null && persistence.PlayerService != null)
            {
                persistence.PlayerService.SaveSlotsChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            descriptors.Clear();
            if (persistence != null)
            {
                descriptors.AddRange(persistence.BuildSaveSlotDescriptors());
            }

            selectedSlotIndex = descriptors.Count == 0 ? 0 : Mathf.Clamp(selectedSlotIndex, 0, descriptors.Count - 1);
            selectedActionIndex = Mathf.Clamp(selectedActionIndex, 0, Actions.Length - 1);
            Render();
        }

        private void BuildUi()
        {
            if (detailsText != null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform root = GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(14f, 14f);
            root.offsetMax = new Vector2(-14f, -14f);

            GameObject panel = CreateChild("Save Load Panel", transform, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.055f, 0.065f, 0.075f, 0.96f);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text header = AddText(panel.transform, font, "Save / Load", 16, 28, FontStyle.Bold);
            header.color = new Color(0.92f, 0.95f, 0.98f, 1f);

            slotValueText = AddSelectorRow(
                panel.transform,
                font,
                "Slot",
                out previousSlotButton,
                out nextSlotButton,
                PreviousSlot,
                NextSlot);

            actionValueText = AddSelectorRow(
                panel.transform,
                font,
                "Action",
                out previousActionButton,
                out nextActionButton,
                PreviousAction,
                NextAction);

            GameObject actionRow = CreateRow("Execute Row", panel.transform, 36f);
            executeButton = AddButton(actionRow.transform, font, "Execute", ExecuteSelectedAction, 12);
            AddButton(actionRow.transform, font, "Refresh", Refresh, 12);

            detailsText = AddText(panel.transform, font, "No save slot selected.", 13, 300);
            feedbackText = AddText(panel.transform, font, string.Empty, 12, 48);
        }

        private void Render()
        {
            SaveSlotDescriptor descriptor = SelectedDescriptor();
            SaveLoadAction action = SelectedAction();

            if (slotValueText != null)
            {
                slotValueText.text = descriptor == null ? "None" : FormatSlotSelection(descriptor);
            }

            if (actionValueText != null)
            {
                actionValueText.text = FormatAction(action);
            }

            SetInteractable(previousSlotButton, descriptors.Count > 1);
            SetInteractable(nextSlotButton, descriptors.Count > 1);
            SetInteractable(previousActionButton, Actions.Length > 1);
            SetInteractable(nextActionButton, Actions.Length > 1);
            SetInteractable(executeButton, CanExecute(descriptor, action));

            RenderDetails(descriptor, action);
        }

        private void RenderDetails(SaveSlotDescriptor descriptor, SaveLoadAction action)
        {
            if (detailsText == null)
            {
                return;
            }

            if (descriptor == null)
            {
                detailsText.text = "No save slots are available.";
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(descriptor.displayName);
            builder.AppendLine($"Selected Action: {FormatAction(action)}");
            builder.AppendLine($"Action Available: {(CanExecute(descriptor, action) ? "Yes" : "No")}");
            builder.AppendLine($"Kind: {descriptor.slotKind}");
            builder.AppendLine($"Status: {(descriptor.exists ? descriptor.validationStatus.ToString() : "Empty")} / {descriptor.compatibilityStatus}");
            builder.AppendLine($"Saved: {PrototypeSaveSlotCatalog.FormatLocalTimestamp(descriptor.lastSavedAtUtc)}");
            builder.AppendLine($"Play Time: {PrototypeSaveSlotCatalog.FormatPlayTime(descriptor.playTimeSeconds)}");
            builder.AppendLine($"Scene: {EmptyFallback(descriptor.sceneKey)}");
            builder.AppendLine($"Place: {EmptyFallback(descriptor.placeDisplayName)}");
            builder.AppendLine($"Player: {EmptyFallback(descriptor.playerDisplayName)}");
            builder.AppendLine($"Primary: {(descriptor.primaryExists ? "Yes" : "No")}  Backup: {(descriptor.backupExists ? "Yes" : "No")}");
            builder.AppendLine($"Dirty: {(persistence != null && persistence.DirtyTracker != null && persistence.DirtyTracker.IsDirty ? "Yes" : "No")}");
            builder.AppendLine($"Operation: {persistence?.PlayerService?.OperationState.ToString() ?? "Missing"}");
            builder.AppendLine($"Phase: {persistence?.PlayerService?.CurrentPhase.ToString() ?? "Missing"}");
            builder.AppendLine($"Safety: {persistence?.PlayerService?.RuntimeSafety.ToString() ?? "Missing"}");
            builder.AppendLine($"Revision: {descriptor.saveRevision}  Transaction: {EmptyFallback(descriptor.transactionId)}");
            builder.AppendLine("World State: player state and location only; shared-world state is future server-owned persistence.");
            if (!string.IsNullOrWhiteSpace(descriptor.message))
            {
                builder.AppendLine(descriptor.message);
            }

            detailsText.text = builder.ToString().TrimEnd();
        }

        private void PreviousSlot()
        {
            CycleSlot(-1);
        }

        private void NextSlot()
        {
            CycleSlot(1);
        }

        private void PreviousAction()
        {
            CycleAction(-1);
        }

        private void NextAction()
        {
            CycleAction(1);
        }

        private void CycleSlot(int direction)
        {
            if (descriptors.Count == 0)
            {
                return;
            }

            selectedSlotIndex = (selectedSlotIndex + direction) % descriptors.Count;
            if (selectedSlotIndex < 0)
            {
                selectedSlotIndex += descriptors.Count;
            }

            pendingConfirmationKey = string.Empty;
            SetFeedback(string.Empty);
            Render();
        }

        private void CycleAction(int direction)
        {
            selectedActionIndex = (selectedActionIndex + direction) % Actions.Length;
            if (selectedActionIndex < 0)
            {
                selectedActionIndex += Actions.Length;
            }

            pendingConfirmationKey = string.Empty;
            SetFeedback(string.Empty);
            Render();
        }

        private void ExecuteSelectedAction()
        {
            switch (SelectedAction())
            {
                case SaveLoadAction.Save:
                    SaveSelected();
                    break;
                case SaveLoadAction.Load:
                    LoadSelected();
                    break;
                case SaveLoadAction.Validate:
                    ValidateSelected();
                    break;
                case SaveLoadAction.LoadBackup:
                    LoadBackupSelected();
                    break;
                case SaveLoadAction.Delete:
                    DeleteSelected();
                    break;
                case SaveLoadAction.ForceAutosave:
                    ForceAutosave();
                    break;
            }
        }

        private void SaveSelected()
        {
            SaveSlotDescriptor descriptor = SelectedDescriptor();
            if (descriptor == null || persistence == null || descriptor.slotKind != SaveSlotKind.Manual)
            {
                SetFeedback("Select a manual slot to save.");
                return;
            }

            string confirmation = "overwrite:" + descriptor.slotId;
            if (descriptor.exists && pendingConfirmationKey != confirmation)
            {
                pendingConfirmationKey = confirmation;
                SetFeedback($"Execute again to overwrite {descriptor.displayName}.");
                return;
            }

            PersistenceSaveResult result = persistence.SaveManualSlot(Mathf.Max(0, descriptor.saveGeneration));
            pendingConfirmationKey = string.Empty;
            SetFeedback(result.Message);
            Refresh();
        }

        private void LoadSelected()
        {
            SaveSlotDescriptor descriptor = SelectedDescriptor();
            if (descriptor == null || persistence == null)
            {
                return;
            }

            string confirmation = "load:" + descriptor.slotId;
            if (persistence.DirtyTracker != null && persistence.DirtyTracker.IsDirty && pendingConfirmationKey != confirmation)
            {
                pendingConfirmationKey = confirmation;
                SetFeedback($"Unsaved progress will be replaced. Execute again to load {descriptor.displayName}.");
                return;
            }

            PersistenceLoadResult result = persistence.LoadSaveSlot(descriptor.slotId);
            pendingConfirmationKey = string.Empty;
            SetFeedback(result.Message);
            Refresh();
        }

        private void LoadBackupSelected()
        {
            SaveSlotDescriptor descriptor = SelectedDescriptor();
            if (descriptor == null || persistence == null)
            {
                return;
            }

            string confirmation = "load-backup:" + descriptor.slotId;
            if (pendingConfirmationKey != confirmation)
            {
                pendingConfirmationKey = confirmation;
                SetFeedback($"Execute again to load backup for {descriptor.displayName}.");
                return;
            }

            PersistenceLoadResult result = persistence.LoadSaveSlot(descriptor.slotId, loadBackup: true);
            pendingConfirmationKey = string.Empty;
            SetFeedback(result.Message);
            Refresh();
        }

        private void ValidateSelected()
        {
            SaveSlotDescriptor descriptor = SelectedDescriptor();
            if (descriptor == null || persistence == null)
            {
                return;
            }

            PersistenceValidationResult result = persistence.ValidateSaveSlot(descriptor.slotId);
            pendingConfirmationKey = string.Empty;
            SetFeedback($"{result.Status}: {result.Message} Backup={result.BackupAvailable}");
            Refresh();
        }

        private void DeleteSelected()
        {
            SaveSlotDescriptor descriptor = SelectedDescriptor();
            if (descriptor == null || persistence == null || descriptor.slotKind != SaveSlotKind.Manual)
            {
                SetFeedback("Only manual slots can be deleted here.");
                return;
            }

            string confirmation = "delete:" + descriptor.slotId;
            if (pendingConfirmationKey != confirmation)
            {
                pendingConfirmationKey = confirmation;
                SetFeedback($"Execute again to delete {descriptor.displayName} and its backup.");
                return;
            }

            PersistenceDeleteResult result = persistence.DeleteSaveSlot(descriptor.slotId);
            pendingConfirmationKey = string.Empty;
            SetFeedback(result.Message);
            Refresh();
        }

        private void ForceAutosave()
        {
            if (persistence == null)
            {
                return;
            }

            PersistenceSaveResult result = persistence.ForceAutosave("SaveLoadMenu");
            pendingConfirmationKey = string.Empty;
            SetFeedback(result.Message);
            Refresh();
        }

        private bool CanExecute(SaveSlotDescriptor descriptor, SaveLoadAction action)
        {
            if (persistence == null || IsOperationActive())
            {
                return false;
            }

            if (action == SaveLoadAction.ForceAutosave)
            {
                return true;
            }

            if (descriptor == null)
            {
                return false;
            }

            return action switch
            {
                SaveLoadAction.Save => descriptor.CanSave,
                SaveLoadAction.Load => descriptor.CanLoad,
                SaveLoadAction.Validate => descriptor.CanValidate,
                SaveLoadAction.LoadBackup => descriptor.CanLoadBackup,
                SaveLoadAction.Delete => descriptor.CanDelete,
                _ => false
            };
        }

        private SaveSlotDescriptor SelectedDescriptor()
        {
            return descriptors.Count == 0 || selectedSlotIndex < 0 || selectedSlotIndex >= descriptors.Count
                ? null
                : descriptors[selectedSlotIndex];
        }

        private SaveLoadAction SelectedAction()
        {
            return Actions[Mathf.Clamp(selectedActionIndex, 0, Actions.Length - 1)];
        }

        private bool IsOperationActive()
        {
            return persistence != null && persistence.PlayerService != null && persistence.PlayerService.OperationInProgress;
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message ?? string.Empty;
            }
        }

        private static string FormatSlotSelection(SaveSlotDescriptor descriptor)
        {
            string status = descriptor.exists ? descriptor.compatibilityStatus.ToString() : "Empty";
            string stamp = PrototypeSaveSlotCatalog.FormatLocalTimestamp(descriptor.lastSavedAtUtc);
            string badge = descriptor.isNewestAutosave ? " [Newest]" : string.Empty;
            return $"{descriptor.displayName}{badge} | {status} | {stamp}";
        }

        private static string FormatAction(SaveLoadAction action)
        {
            return action switch
            {
                SaveLoadAction.LoadBackup => "Load Backup",
                SaveLoadAction.ForceAutosave => "Force Autosave",
                _ => action.ToString()
            };
        }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value;
        }

        private static Text AddSelectorRow(
            Transform parent,
            Font font,
            string label,
            out Button previousButton,
            out Button nextButton,
            Action previous,
            Action next)
        {
            GameObject row = CreateRow("Selector - " + label, parent, 36f);
            Text labelText = AddText(row.transform, font, label, 12, 30, FontStyle.Bold);
            SetElement(labelText.gameObject, 70f, 30f, 0f);
            previousButton = AddButton(row.transform, font, "Prev", previous, 11);
            SetElement(previousButton.gameObject, 70f, 30f, 0f);
            Text valueText = AddText(row.transform, font, "None", 12, 30);
            valueText.color = new Color(0.86f, 0.92f, 0.96f, 1f);
            SetElement(valueText.gameObject, 0f, 30f, 1f);
            nextButton = AddButton(row.transform, font, "Next", next, 11);
            SetElement(nextButton.gameObject, 70f, 30f, 0f);
            return valueText;
        }

        private static GameObject CreateRow(string name, Transform parent, float height)
        {
            GameObject row = CreateChild(name, parent, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            SetElement(row, 0f, height, 1f);
            return row;
        }

        private static Text AddText(Transform parent, Font font, string text, int fontSize, float height, FontStyle style = FontStyle.Normal)
        {
            GameObject obj = CreateChild("Text", parent, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            Text label = obj.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.text = text;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            SetElement(obj, 0f, height, 1f);
            return label;
        }

        private static Button AddButton(Transform parent, Font font, string label, Action action, int fontSize)
        {
            GameObject root = CreateChild(label, parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.16f, 0.95f);
            SetElement(root, 96f, 32f, 1f);

            Text text = AddText(root.transform, font, label, fontSize, 28, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 2f);
            textRect.offsetMax = new Vector2(-4f, -2f);

            Button button = root.GetComponent<Button>();
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }

        private static void SetElement(GameObject obj, float preferredWidth, float preferredHeight, float flexibleWidth)
        {
            LayoutElement element = obj.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = obj.AddComponent<LayoutElement>();
            }

            element.preferredWidth = preferredWidth <= 0f ? -1f : preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = 0f;
        }

        private static GameObject CreateChild(string name, Transform parent, params Type[] components)
        {
            GameObject child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return child;
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private enum SaveLoadAction
        {
            Save,
            Load,
            Validate,
            LoadBackup,
            Delete,
            ForceAutosave
        }
    }
}
