using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Dialogue
{
    [CreateAssetMenu(fileName = "DialogueGraphDefinition", menuName = "Unity Isekai Game/Dialogue/Dialogue Graph Definition")]
    public sealed class DialogueGraphDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string graphId;
        [SerializeField] private string displayName;
        [SerializeField] private string conversationDefinitionId;
        [SerializeField] private string canonicalEntryNodeId;
        [SerializeField] private string fallbackNodeId;
        [SerializeField, Min(1)] private int automaticTransitionLimit = 8;
        [SerializeField] private DialogueNodeDefinitionData[] nodes = Array.Empty<DialogueNodeDefinitionData>();
        [SerializeField] private DialogueConditionData[] entryConditions = Array.Empty<DialogueConditionData>();
        [SerializeField] private string[] tagIds = Array.Empty<string>();

        public string Id => graphId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string ConversationDefinitionId => conversationDefinitionId ?? string.Empty;
        public string CanonicalEntryNodeId => canonicalEntryNodeId ?? string.Empty;
        public string FallbackNodeId => fallbackNodeId ?? string.Empty;
        public int AutomaticTransitionLimit => Math.Max(1, automaticTransitionLimit);
        public IReadOnlyList<DialogueNodeDefinitionData> Nodes => (nodes ?? Array.Empty<DialogueNodeDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<DialogueConditionData> EntryConditions => (entryConditions ?? Array.Empty<DialogueConditionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<string> TagIds => DialogueFlowModelUtility.Clean(tagIds);

        public DialogueGraphDefinitionData ToRecordData()
        {
            return new DialogueGraphDefinitionData
            {
                graphId = Id,
                displayName = DisplayName,
                conversationDefinitionId = ConversationDefinitionId,
                canonicalEntryNodeId = CanonicalEntryNodeId,
                fallbackNodeId = FallbackNodeId,
                automaticTransitionLimit = AutomaticTransitionLimit,
                nodes = Nodes.ToArray(),
                entryConditions = EntryConditions.ToArray(),
                tagIds = TagIds.ToArray()
            };
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            string conversationId,
            string entryNodeId,
            string fallbackId,
            IEnumerable<DialogueNodeDefinitionData> nodeDefinitions,
            IEnumerable<DialogueConditionData> conditions = null,
            IEnumerable<string> tags = null,
            int autoLimit = 8)
        {
            graphId = id ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(name) ? graphId : name;
            conversationDefinitionId = conversationId ?? string.Empty;
            canonicalEntryNodeId = entryNodeId ?? string.Empty;
            fallbackNodeId = fallbackId ?? string.Empty;
            automaticTransitionLimit = Math.Max(1, autoLimit);
            nodes = (nodeDefinitions ?? Array.Empty<DialogueNodeDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
            entryConditions = (conditions ?? Array.Empty<DialogueConditionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
            tagIds = DialogueFlowModelUtility.Clean(tags);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;

            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Dialogue Graph definition is missing a stable ID.");
            else if (!Id.StartsWith("dialogue-graph.", StringComparison.Ordinal)) report.AddWarning($"Dialogue Graph definition '{DisplayName}' should use the 'dialogue-graph.' namespace prefix.");
            if (string.IsNullOrWhiteSpace(ConversationDefinitionId)) report.AddError($"Dialogue Graph definition '{DisplayName}' must declare a Conversation definition ID.");
            else if (definitionsById != null && !definitionsById.ContainsKey(ConversationDefinitionId)) report.AddWarning($"Dialogue Graph definition '{DisplayName}' references missing Conversation definition '{ConversationDefinitionId}'.");

            DialogueFlowValidationReport graphReport = DialogueGraphValidator.Validate(ToRecordData());
            foreach (string error in graphReport.Errors) report.AddError($"Dialogue Graph definition '{DisplayName}': {error}");
            foreach (string warning in graphReport.Warnings) report.AddWarning($"Dialogue Graph definition '{DisplayName}': {warning}");
        }
    }

    public static class DialogueGraphValidator
    {
        public static DialogueFlowValidationReport Validate(DialogueGraphDefinitionData graph)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            if (graph == null)
            {
                errors.Add("Dialogue graph is missing.");
                return new DialogueFlowValidationReport(errors, warnings);
            }

            DialogueGraphDefinitionData data = graph.Clone();
            if (string.IsNullOrWhiteSpace(data.graphId)) errors.Add("Dialogue graph has no ID.");
            if (string.IsNullOrWhiteSpace(data.conversationDefinitionId)) errors.Add($"Dialogue graph '{data.graphId}' has no Conversation definition ID.");
            if (string.IsNullOrWhiteSpace(data.canonicalEntryNodeId)) errors.Add($"Dialogue graph '{data.graphId}' has no canonical entry node.");

            Dictionary<string, DialogueNodeDefinitionData> nodes = new Dictionary<string, DialogueNodeDefinitionData>(StringComparer.Ordinal);
            foreach (DialogueNodeDefinitionData node in data.nodes ?? Array.Empty<DialogueNodeDefinitionData>())
            {
                if (node == null) continue;
                if (string.IsNullOrWhiteSpace(node.nodeId)) errors.Add($"Dialogue graph '{data.graphId}' has a node without an ID.");
                else if (nodes.ContainsKey(node.nodeId)) errors.Add($"Dialogue graph '{data.graphId}' has duplicate node '{node.nodeId}'.");
                else nodes[node.nodeId] = node.Clone();

                if (node.category == DialogueNodeCategory.Unknown) errors.Add($"Dialogue node '{node.nodeId}' has unknown category.");
                ValidateConditions(node.entryConditions, $"node '{node.nodeId}' entry", errors);
                ValidateEffects(node.entryEffects, $"node '{node.nodeId}' entry", errors);

                HashSet<string> choiceIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DialogueChoiceDefinitionData choice in node.choices ?? Array.Empty<DialogueChoiceDefinitionData>())
                {
                    if (choice == null) continue;
                    if (string.IsNullOrWhiteSpace(choice.choiceId)) errors.Add($"Dialogue node '{node.nodeId}' has a choice without an ID.");
                    else if (!choiceIds.Add(choice.choiceId)) errors.Add($"Dialogue node '{node.nodeId}' has duplicate choice '{choice.choiceId}'.");
                    if (choice.category == DialogueChoiceCategory.Unknown) errors.Add($"Dialogue choice '{choice.choiceId}' has unknown category.");
                    ValidateConditions(choice.conditions, $"choice '{choice.choiceId}'", errors);
                    ValidateEffects(choice.effects, $"choice '{choice.choiceId}'", errors);
                }

                HashSet<string> transitionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DialogueTransitionDefinitionData transition in node.transitions ?? Array.Empty<DialogueTransitionDefinitionData>())
                {
                    if (transition == null) continue;
                    if (string.IsNullOrWhiteSpace(transition.transitionId)) errors.Add($"Dialogue node '{node.nodeId}' has a transition without an ID.");
                    else if (!transitionIds.Add(transition.transitionId)) errors.Add($"Dialogue node '{node.nodeId}' has duplicate transition '{transition.transitionId}'.");
                    if (transition.category == DialogueTransitionCategory.Unknown) errors.Add($"Dialogue transition '{transition.transitionId}' has unknown category.");
                    ValidateConditions(transition.conditions, $"transition '{transition.transitionId}'", errors);
                    ValidateEffects(transition.effects, $"transition '{transition.transitionId}'", errors);
                }
            }

            if (!string.IsNullOrWhiteSpace(data.canonicalEntryNodeId) && !nodes.ContainsKey(data.canonicalEntryNodeId)) errors.Add($"Entry node '{data.canonicalEntryNodeId}' is missing.");
            if (!string.IsNullOrWhiteSpace(data.fallbackNodeId) && !nodes.ContainsKey(data.fallbackNodeId)) errors.Add($"Fallback node '{data.fallbackNodeId}' is missing.");

            foreach (DialogueNodeDefinitionData node in nodes.Values)
            {
                foreach (DialogueChoiceDefinitionData choice in node.choices ?? Array.Empty<DialogueChoiceDefinitionData>())
                {
                    if (!string.IsNullOrWhiteSpace(choice.targetNodeId) && !nodes.ContainsKey(choice.targetNodeId)) errors.Add($"Choice '{choice.choiceId}' targets missing node '{choice.targetNodeId}'.");
                    if (!string.IsNullOrWhiteSpace(choice.effectFailureNodeId) && !nodes.ContainsKey(choice.effectFailureNodeId)) errors.Add($"Choice '{choice.choiceId}' failure node '{choice.effectFailureNodeId}' is missing.");
                }

                foreach (DialogueTransitionDefinitionData transition in node.transitions ?? Array.Empty<DialogueTransitionDefinitionData>())
                {
                    if (!string.IsNullOrWhiteSpace(transition.targetNodeId) && !nodes.ContainsKey(transition.targetNodeId)) errors.Add($"Transition '{transition.transitionId}' targets missing node '{transition.targetNodeId}'.");
                }

                foreach (DialogueEffectData effect in (node.entryEffects ?? Array.Empty<DialogueEffectData>()).Concat((node.choices ?? Array.Empty<DialogueChoiceDefinitionData>()).SelectMany(choice => choice.effects ?? Array.Empty<DialogueEffectData>())))
                {
                    if (!string.IsNullOrWhiteSpace(effect.successNodeId) && !nodes.ContainsKey(effect.successNodeId)) errors.Add($"Effect '{effect.effectId}' success node '{effect.successNodeId}' is missing.");
                    if (!string.IsNullOrWhiteSpace(effect.failureNodeId) && !nodes.ContainsKey(effect.failureNodeId)) errors.Add($"Effect '{effect.effectId}' failure node '{effect.failureNodeId}' is missing.");
                }
            }

            if (errors.Count == 0) ValidateReachability(data, nodes, warnings);
            if (errors.Count == 0) ValidateAutomaticLoops(nodes, data.AutomaticLimit(), errors);
            return new DialogueFlowValidationReport(errors, warnings);
        }

        private static void ValidateReachability(DialogueGraphDefinitionData graph, IReadOnlyDictionary<string, DialogueNodeDefinitionData> nodes, ICollection<string> warnings)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(graph.canonicalEntryNodeId);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                if (!visited.Add(id) || !nodes.TryGetValue(id, out DialogueNodeDefinitionData node)) continue;
                foreach (string target in Targets(node).Where(target => nodes.ContainsKey(target)).OrderBy(value => value, StringComparer.Ordinal)) queue.Enqueue(target);
            }

            foreach (string id in nodes.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!visited.Contains(id)) warnings.Add($"Node '{id}' is unreachable from entry '{graph.canonicalEntryNodeId}'.");
            }
        }

        private static void ValidateAutomaticLoops(IReadOnlyDictionary<string, DialogueNodeDefinitionData> nodes, int limit, ICollection<string> errors)
        {
            foreach (DialogueNodeDefinitionData start in nodes.Values.Where(IsPureAutomatic))
            {
                HashSet<string> path = new HashSet<string>(StringComparer.Ordinal);
                string current = start.nodeId;
                for (int i = 0; i <= limit; i++)
                {
                    if (!nodes.TryGetValue(current, out DialogueNodeDefinitionData node) || !IsPureAutomatic(node)) break;
                    if (!path.Add(current))
                    {
                        errors.Add($"Automatic transition loop reaches '{current}' without player input or state-changing effects.");
                        break;
                    }

                    DialogueTransitionDefinitionData next = node.transitions.OrderBy(value => value.priority).ThenBy(value => value.transitionId, StringComparer.Ordinal).FirstOrDefault();
                    if (next == null || string.IsNullOrWhiteSpace(next.targetNodeId)) break;
                    current = next.targetNodeId;
                }
            }
        }

        private static bool IsPureAutomatic(DialogueNodeDefinitionData node)
        {
            return node != null
                && (node.choices == null || node.choices.Length == 0)
                && (node.entryEffects == null || node.entryEffects.Length == 0)
                && (node.transitions ?? Array.Empty<DialogueTransitionDefinitionData>()).Any()
                && (node.transitions ?? Array.Empty<DialogueTransitionDefinitionData>()).All(value => value.category == DialogueTransitionCategory.Automatic || value.category == DialogueTransitionCategory.Redirect);
        }

        private static IEnumerable<string> Targets(DialogueNodeDefinitionData node)
        {
            foreach (DialogueChoiceDefinitionData choice in node.choices ?? Array.Empty<DialogueChoiceDefinitionData>())
            {
                if (!string.IsNullOrWhiteSpace(choice.targetNodeId)) yield return choice.targetNodeId;
            }

            foreach (DialogueTransitionDefinitionData transition in node.transitions ?? Array.Empty<DialogueTransitionDefinitionData>())
            {
                if (!string.IsNullOrWhiteSpace(transition.targetNodeId)) yield return transition.targetNodeId;
            }
        }

        private static void ValidateConditions(IEnumerable<DialogueConditionData> conditions, string owner, ICollection<string> errors)
        {
            foreach (DialogueConditionData condition in conditions ?? Array.Empty<DialogueConditionData>())
            {
                if (condition == null) continue;
                if (condition.kind == DialogueConditionKind.Unknown) errors.Add($"{owner} has an unknown condition.");
                if (condition.kind != DialogueConditionKind.Always && string.IsNullOrWhiteSpace(condition.requiredId)) errors.Add($"{owner} condition '{condition.conditionId}' requires a target ID.");
            }
        }

        private static void ValidateEffects(IEnumerable<DialogueEffectData> effects, string owner, ICollection<string> errors)
        {
            foreach (DialogueEffectData effect in effects ?? Array.Empty<DialogueEffectData>())
            {
                if (effect == null) continue;
                if (effect.kind == DialogueEffectKind.Unknown) errors.Add($"{owner} has an unknown effect.");
                if (effect.kind != DialogueEffectKind.None && string.IsNullOrWhiteSpace(effect.effectId)) errors.Add($"{owner} has an effect without a stable ID.");
            }
        }

        private static int AutomaticLimit(this DialogueGraphDefinitionData graph) => Math.Max(1, graph?.automaticTransitionLimit ?? 8);
    }
}
