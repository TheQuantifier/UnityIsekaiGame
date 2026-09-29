using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    public sealed class BiologicalCompatibilityRuntime : IDisposable
    {
        private readonly Dictionary<string, BiologicalInteractionDefinition> interactionsById = new Dictionary<string, BiologicalInteractionDefinition>(StringComparer.Ordinal);
        private readonly List<BiologicalCompatibilityProfileDefinition> profiles = new List<BiologicalCompatibilityProfileDefinition>();
        private readonly Dictionary<string, RuntimeBiologicalInteractionRule> dynamicRulesByKey = new Dictionary<string, RuntimeBiologicalInteractionRule>(StringComparer.Ordinal);
        private string actorBodyId;
        private string activeProfileId;
        private long bodyRevision;
        private long anatomyRevision;
        private long conditionRevision;
        private long vitalRevision;
        private long hazardRevision;

        public BiologicalCompatibilityReadinessState Readiness { get; private set; } = BiologicalCompatibilityReadinessState.Uninitialized;
        public long CompatibilityRevision { get; private set; }
        public bool IsDirty { get; private set; }
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public bool IsReady => Readiness == BiologicalCompatibilityReadinessState.Ready;

        public BiologicalCompatibilityOperationResult BuildForBody(BodySnapshot body, DefinitionRegistry registry, bool restoring = false, bool preserveRevision = false)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.ActorBodyId))
            {
                Readiness = BiologicalCompatibilityReadinessState.WaitingForBody;
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.MissingBody, "Biological compatibility requires an exact body snapshot.", CreateSnapshot());
            }

            actorBodyId = body.ActorBodyId;
            bodyRevision = body.BodyRevision;
            anatomyRevision = body.Anatomy == null ? 0L : body.Anatomy.AnatomyRevision;
            conditionRevision = body.Condition == null ? 0L : body.Condition.ConditionRevision;
            vitalRevision = body.VitalProcesses == null ? 0L : body.VitalProcesses.VitalRevision;
            hazardRevision = body.BiologicalHazards == null ? 0L : body.BiologicalHazards.HazardRevision;
            interactionsById.Clear();
            foreach (BiologicalInteractionDefinition interaction in registry?.DefinitionsById.Values.OfType<BiologicalInteractionDefinition>().Where(definition => definition != null && definition.AlphaEnabled) ?? Enumerable.Empty<BiologicalInteractionDefinition>())
            {
                interactionsById[interaction.Id] = interaction;
            }

            profiles.Clear();
            profiles.AddRange(registry?.DefinitionsById.Values.OfType<BiologicalCompatibilityProfileDefinition>().Where(profile => profile != null && profile.AlphaEnabled).OrderBy(profile => profile.Id, StringComparer.Ordinal) ?? Enumerable.Empty<BiologicalCompatibilityProfileDefinition>());
            if (interactionsById.Count == 0)
            {
                Readiness = BiologicalCompatibilityReadinessState.WaitingForDefinitions;
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.MissingInteraction, "No Biological Interaction definitions are registered.", CreateSnapshot());
            }

            activeProfileId = string.Join(",", profiles.Where(profile => profile.AppliesTo(body)).Select(profile => profile.Id));
            if (!preserveRevision)
            {
                CompatibilityRevision++;
            }

            IsDirty = !restoring && dynamicRulesByKey.Count > 0;
            Readiness = BiologicalCompatibilityReadinessState.Ready;
            return BiologicalCompatibilityOperationResult.Success("Biological compatibility initialized.", CreateSnapshot());
        }

        public BiologicalInteractionEvaluationResult Evaluate(BodySnapshot body, string interactionDefinitionId, BiologicalInteractionCategory category = BiologicalInteractionCategory.Unknown, AnatomyNodeSnapshot targetNode = null, string sourceId = "", string transactionId = "", bool preview = true)
        {
            if (!IsReady)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.RuntimeNotReady, "Biological compatibility runtime is not Ready.", ActorBodyId, interactionDefinitionId);
            }

            if (body == null)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.MissingBody, "Biological compatibility evaluation requires a body snapshot.", ActorBodyId, interactionDefinitionId);
            }

            if (!string.Equals(body.ActorBodyId, ActorBodyId, StringComparison.Ordinal))
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.StaleBody, $"Body '{body.ActorBodyId}' does not match compatibility runtime body '{ActorBodyId}'.", ActorBodyId, interactionDefinitionId);
            }

            if (body.BodyRevision != bodyRevision)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.StaleBody, $"Body snapshot revision {body.BodyRevision} does not match compatibility runtime body revision {bodyRevision}.", ActorBodyId, interactionDefinitionId);
            }

            long snapshotAnatomyRevision = body.Anatomy == null ? 0L : body.Anatomy.AnatomyRevision;
            if (snapshotAnatomyRevision != anatomyRevision)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.StaleBody, $"Body snapshot anatomy revision {snapshotAnatomyRevision} does not match compatibility runtime anatomy revision {anatomyRevision}.", ActorBodyId, interactionDefinitionId);
            }

            interactionsById.TryGetValue(interactionDefinitionId ?? string.Empty, out BiologicalInteractionDefinition interaction);
            if (!string.IsNullOrWhiteSpace(interactionDefinitionId) && interaction == null)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.MissingInteraction, $"Biological interaction '{interactionDefinitionId}' is not registered.", ActorBodyId, interactionDefinitionId);
            }

            BiologicalInteractionEvaluationContext context = new BiologicalInteractionEvaluationContext(body, interaction, interactionDefinitionId, category, targetNode, body.Condition, body.VitalProcesses, sourceId, transactionId, preview);
            return BiologicalInteractionEvaluator.Evaluate(context, BuildProfileRules(body), dynamicRulesByKey.Values, CompatibilityRevision);
        }

        public BiologicalCompatibilityOperationResult AddOrUpdateContribution(RuntimeBiologicalInteractionRule rule, bool restoring = false)
        {
            if (!IsReady)
            {
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.RuntimeNotReady, "Biological compatibility runtime is not Ready.", CreateSnapshot());
            }

            if (rule == null || string.IsNullOrWhiteSpace(rule.EntryId) || string.IsNullOrWhiteSpace(rule.SourceId))
            {
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.InvalidRequest, "Biological compatibility contribution requires sourceId and entryId.", CreateSnapshot());
            }

            if (string.IsNullOrWhiteSpace(rule.InteractionDefinitionId) && rule.Category == BiologicalInteractionCategory.Unknown)
            {
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.MissingInteraction, "Biological compatibility contribution requires an interaction ID or category.", CreateSnapshot());
            }

            if (!string.IsNullOrWhiteSpace(rule.InteractionDefinitionId) && !interactionsById.ContainsKey(rule.InteractionDefinitionId))
            {
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.MissingInteraction, $"Biological interaction '{rule.InteractionDefinitionId}' is not registered.", CreateSnapshot());
            }

            if (rule.RuleKind == BiologicalInteractionRuleKind.Conversion)
            {
                if (string.IsNullOrWhiteSpace(rule.ConvertedInteractionDefinitionId) || !interactionsById.ContainsKey(rule.ConvertedInteractionDefinitionId))
                {
                    return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.MissingInteraction, $"Biological conversion rule '{rule.EntryId}' requires a registered converted interaction.", CreateSnapshot());
                }

                if (string.Equals(rule.InteractionDefinitionId, rule.ConvertedInteractionDefinitionId, StringComparison.Ordinal))
                {
                    return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.InvalidRequest, $"Biological conversion rule '{rule.EntryId}' cannot convert an interaction to itself.", CreateSnapshot());
                }
            }

            if (rule.RuleKind == BiologicalInteractionRuleKind.Absorption && string.IsNullOrWhiteSpace(rule.Explanation))
            {
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.InvalidRequest, $"Biological absorption rule '{rule.EntryId}' requires an authored outcome explanation.", CreateSnapshot());
            }

            string key = rule.StableKey;
            bool duplicate = dynamicRulesByKey.TryGetValue(key, out RuntimeBiologicalInteractionRule existing)
                && ExistingEquals(existing, rule);
            if (!duplicate)
            {
                dynamicRulesByKey[key] = rule;
                CompatibilityRevision++;
                IsDirty = !restoring;
            }

            return BiologicalCompatibilityOperationResult.Success($"Biological compatibility contribution '{rule.EntryId}' applied.", CreateSnapshot(), duplicate);
        }

        public BiologicalCompatibilityOperationResult RemoveContribution(string sourceId, string entryId, bool restoring = false)
        {
            string key = dynamicRulesByKey.Keys.FirstOrDefault(candidate => candidate.EndsWith($":{entryId ?? string.Empty}", StringComparison.Ordinal) && candidate.Contains($":{sourceId ?? string.Empty}:", StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(key) || !dynamicRulesByKey.Remove(key))
            {
                return BiologicalCompatibilityOperationResult.Failure(BiologicalCompatibilityResultCode.MissingContribution, $"Biological compatibility contribution '{entryId}' from '{sourceId}' is not active.", CreateSnapshot());
            }

            CompatibilityRevision++;
            IsDirty = !restoring;
            return BiologicalCompatibilityOperationResult.Success($"Biological compatibility contribution '{entryId}' removed.", CreateSnapshot());
        }

        public BiologicalCompatibilityOperationResult ClearSource(string sourceId, bool restoring = false)
        {
            string[] keys = dynamicRulesByKey.Where(pair => string.Equals(pair.Value.SourceId, sourceId, StringComparison.Ordinal)).Select(pair => pair.Key).ToArray();
            foreach (string key in keys)
            {
                dynamicRulesByKey.Remove(key);
            }

            if (keys.Length > 0)
            {
                CompatibilityRevision++;
                IsDirty = !restoring;
            }

            return BiologicalCompatibilityOperationResult.Success($"Biological compatibility source '{sourceId}' cleared.", CreateSnapshot(), duplicate: keys.Length == 0);
        }

        public BiologicalCompatibilitySnapshot CreateSnapshot()
        {
            List<string> diagnostics = new List<string>();
            bool coherent = true;
            if (Readiness == BiologicalCompatibilityReadinessState.Ready && string.IsNullOrWhiteSpace(ActorBodyId))
            {
                coherent = false;
                diagnostics.Add("Biological compatibility runtime is Ready without an Actor/body ID.");
            }

            IReadOnlyList<BiologicalCompatibilityRuleSnapshot> ruleSnapshots = dynamicRulesByKey.Values
                .OrderBy(rule => rule.SourceId, StringComparer.Ordinal)
                .ThenBy(rule => rule.EntryId, StringComparer.Ordinal)
                .Select(rule => new BiologicalCompatibilityRuleSnapshot(rule))
                .ToArray();
            return new BiologicalCompatibilitySnapshot(ActorBodyId, activeProfileId, Readiness, bodyRevision, anatomyRevision, conditionRevision, vitalRevision, hazardRevision, CompatibilityRevision, ruleSnapshots, IsDirty, coherent, diagnostics);
        }

        public BiologicalInteractionDefinition ResolveInteraction(string interactionDefinitionId)
        {
            return interactionsById.TryGetValue(interactionDefinitionId ?? string.Empty, out BiologicalInteractionDefinition definition) ? definition : null;
        }

        public void Dispose()
        {
            Readiness = BiologicalCompatibilityReadinessState.Disposed;
            interactionsById.Clear();
            profiles.Clear();
            dynamicRulesByKey.Clear();
        }

        private IEnumerable<RuntimeBiologicalInteractionRule> BuildProfileRules(BodySnapshot body)
        {
            return profiles
                .Where(profile => profile.AppliesTo(body))
                .SelectMany(profile => profile.Rules.Where(rule => rule != null && rule.AlphaEnabled).Select(rule => rule.ToRuntimeRule(profile.Id)))
                .OrderBy(rule => rule.Priority)
                .ThenBy(rule => rule.SourceId, StringComparer.Ordinal)
                .ThenBy(rule => rule.EntryId, StringComparer.Ordinal);
        }

        private static bool ExistingEquals(RuntimeBiologicalInteractionRule left, RuntimeBiologicalInteractionRule right)
        {
            return left != null
                && right != null
                && left.RuleKind == right.RuleKind
                && left.InteractionDefinitionId == right.InteractionDefinitionId
                && left.Category == right.Category
                && left.CompatibilityState == right.CompatibilityState
                && left.ConvertedInteractionDefinitionId == right.ConvertedInteractionDefinitionId
                && Math.Abs(left.RateMultiplier - right.RateMultiplier) < 0.0001f
                && Math.Abs(left.SeverityMultiplier - right.SeverityMultiplier) < 0.0001f
                && Math.Abs(left.ConsequenceMultiplier - right.ConsequenceMultiplier) < 0.0001f
                && Math.Abs(left.MinimumEffectFloor - right.MinimumEffectFloor) < 0.0001f
                && Math.Abs(left.MaximumSeverity - right.MaximumSeverity) < 0.0001f
                && left.Priority == right.Priority
                && left.AlphaEnabled == right.AlphaEnabled
                && left.RequiredNodeId == right.RequiredNodeId
                && left.RequiredRuntimeCapabilityKeys.SequenceEqual(right.RequiredRuntimeCapabilityKeys)
                && left.BlockingRuntimeCapabilityKeys.SequenceEqual(right.BlockingRuntimeCapabilityKeys)
                && left.RequiredAnatomyTagIds.SequenceEqual(right.RequiredAnatomyTagIds)
                && left.RequiredNodeCategories.SequenceEqual(right.RequiredNodeCategories);
        }
    }
}
