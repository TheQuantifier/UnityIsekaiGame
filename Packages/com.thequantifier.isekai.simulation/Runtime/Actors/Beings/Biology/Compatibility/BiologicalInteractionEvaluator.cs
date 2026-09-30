using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;

namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    public static class BiologicalInteractionEvaluator
    {
        public static BiologicalInteractionEvaluationResult Evaluate(
            BiologicalInteractionEvaluationContext context,
            IEnumerable<RuntimeBiologicalInteractionRule> profileRules,
            IEnumerable<RuntimeBiologicalInteractionRule> dynamicRules,
            long compatibilityRevision)
        {
            if (context == null || context.Body == null)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.MissingBody, "Biological interaction evaluation requires an exact body snapshot.");
            }

            if (string.IsNullOrWhiteSpace(context.InteractionDefinitionId) && context.Category == BiologicalInteractionCategory.Unknown)
            {
                return BiologicalInteractionEvaluationResult.Failure(BiologicalCompatibilityResultCode.MissingInteraction, "Biological interaction evaluation requires an interaction ID or category.", context.ActorBodyId);
            }

            BiologicalCompatibilityState compatibility = context.Interaction == null ? BiologicalCompatibilityState.Compatible : context.Interaction.DefaultCompatibility;
            float rate = context.Interaction == null ? 1f : context.Interaction.DefaultRateMultiplier;
            float severity = context.Interaction == null ? 1f : context.Interaction.DefaultSeverityMultiplier;
            float consequence = context.Interaction == null ? 1f : context.Interaction.DefaultConsequenceMultiplier;
            float minimumEffectFloor = 0f;
            float maximumSeverity = context.Interaction == null ? float.PositiveInfinity : context.Interaction.DefaultMaximumSeverity;
            bool immune = false;
            bool suppressed = false;
            bool affinity = false;
            bool absorbed = false;
            string convertedInteractionId = string.Empty;
            List<BiologicalInteractionRuleTrace> traces = new List<BiologicalInteractionRuleTrace>();

            foreach (RuntimeBiologicalInteractionRule rule in BuildApplicableRuleStream(context, profileRules, dynamicRules))
            {
                if (!RuleTargetsContext(rule, context))
                {
                    traces.Add(new BiologicalInteractionRuleTrace(rule, false, "Rule target did not match interaction or category."));
                    continue;
                }

                if (!RuleRequirementsMatch(rule, context, out string requirementFailure))
                {
                    traces.Add(new BiologicalInteractionRuleTrace(rule, false, requirementFailure));
                    continue;
                }

                traces.Add(new BiologicalInteractionRuleTrace(rule, true, string.IsNullOrWhiteSpace(rule.Explanation) ? "Rule matched." : rule.Explanation));
                switch (rule.RuleKind)
                {
                    case BiologicalInteractionRuleKind.CompatibilityOverride:
                        compatibility = rule.CompatibilityState;
                        break;
                    case BiologicalInteractionRuleKind.Immunity:
                        immune = true;
                        break;
                    case BiologicalInteractionRuleKind.Resistance:
                        rate *= rule.RateMultiplier;
                        severity *= rule.SeverityMultiplier;
                        consequence *= rule.ConsequenceMultiplier;
                        break;
                    case BiologicalInteractionRuleKind.Vulnerability:
                        rate *= Math.Max(1f, rule.RateMultiplier);
                        severity *= Math.Max(1f, rule.SeverityMultiplier);
                        consequence *= Math.Max(1f, rule.ConsequenceMultiplier);
                        break;
                    case BiologicalInteractionRuleKind.Affinity:
                        affinity = true;
                        rate *= rule.RateMultiplier;
                        severity *= rule.SeverityMultiplier;
                        consequence *= rule.ConsequenceMultiplier;
                        break;
                    case BiologicalInteractionRuleKind.Suppression:
                        suppressed = true;
                        rate *= rule.RateMultiplier;
                        severity *= rule.SeverityMultiplier;
                        consequence *= rule.ConsequenceMultiplier;
                        break;
                    case BiologicalInteractionRuleKind.Conversion:
                        convertedInteractionId = rule.ConvertedInteractionDefinitionId;
                        break;
                    case BiologicalInteractionRuleKind.Absorption:
                        absorbed = true;
                        break;
                    case BiologicalInteractionRuleKind.MaximumSeverityLimit:
                        maximumSeverity = Math.Min(maximumSeverity, rule.MaximumSeverity);
                        break;
                    case BiologicalInteractionRuleKind.MinimumEffectFloor:
                        minimumEffectFloor = Math.Max(minimumEffectFloor, rule.MinimumEffectFloor);
                        break;
                }
            }

            rate = Math.Max(minimumEffectFloor, rate);
            severity = Math.Max(minimumEffectFloor, severity);
            consequence = Math.Max(minimumEffectFloor, consequence);
            string message = BuildMessage(compatibility, immune, suppressed, affinity, absorbed, convertedInteractionId);
            return new BiologicalInteractionEvaluationResult(
                BiologicalCompatibilityResultCode.Success,
                message,
                context.ActorBodyId,
                context.InteractionDefinitionId,
                context.Category,
                compatibility,
                immune,
                suppressed,
                affinity,
                absorbed,
                convertedInteractionId,
                rate,
                severity,
                consequence,
                minimumEffectFloor,
                maximumSeverity,
                context.Body.BodyRevision,
                context.Body.Anatomy == null ? 0L : context.Body.Anatomy.AnatomyRevision,
                context.Body.Condition == null ? 0L : context.Body.Condition.ConditionRevision,
                context.Body.VitalProcesses == null ? 0L : context.Body.VitalProcesses.VitalRevision,
                context.Body.BiologicalHazards == null ? 0L : context.Body.BiologicalHazards.HazardRevision,
                compatibilityRevision,
                traces);
        }

        private static IEnumerable<RuntimeBiologicalInteractionRule> BuildApplicableRuleStream(
            BiologicalInteractionEvaluationContext context,
            IEnumerable<RuntimeBiologicalInteractionRule> profileRules,
            IEnumerable<RuntimeBiologicalInteractionRule> dynamicRules)
        {
            RuntimeBiologicalInteractionRule implicitRule = BuildImplicitBodyRule(context);
            if (implicitRule != null)
            {
                yield return implicitRule;
            }

            IEnumerable<RuntimeBiologicalInteractionRule> all = (profileRules ?? Array.Empty<RuntimeBiologicalInteractionRule>())
                .Concat(dynamicRules ?? Array.Empty<RuntimeBiologicalInteractionRule>())
                .Where(rule => rule != null && rule.AlphaEnabled)
                .OrderBy(rule => rule.Priority)
                .ThenBy(rule => string.IsNullOrWhiteSpace(rule.InteractionDefinitionId) ? 0 : 1)
                .ThenBy(rule => rule.SourceKind.ToString(), StringComparer.Ordinal)
                .ThenBy(rule => rule.SourceId, StringComparer.Ordinal)
                .ThenBy(rule => rule.EntryId, StringComparer.Ordinal);

            foreach (RuntimeBiologicalInteractionRule rule in all)
            {
                yield return rule;
            }
        }

        private static RuntimeBiologicalInteractionRule BuildImplicitBodyRule(BiologicalInteractionEvaluationContext context)
        {
            if (context?.Body == null)
            {
                return null;
            }

            BodySnapshot body = context.Body;
            bool incompatible = false;
            string explanation = string.Empty;
            if (string.Equals(context.InteractionDefinitionId, BiologicalInteractionIds.Bleeding, StringComparison.Ordinal) && !body.HasBlood)
            {
                incompatible = true;
                explanation = "Body snapshot reports no Blood compatibility.";
            }
            else if (string.Equals(context.InteractionDefinitionId, BiologicalInteractionIds.Suffocation, StringComparison.Ordinal) && !body.RequiresBreathing)
            {
                incompatible = true;
                explanation = "Body snapshot reports no Breath requirement.";
            }
            else if (string.Equals(context.InteractionDefinitionId, BiologicalInteractionIds.BiologicalHealing, StringComparison.Ordinal) && !body.AcceptsBiologicalHealing)
            {
                incompatible = true;
                explanation = "Body snapshot rejects ordinary biological healing.";
            }
            else if (string.Equals(context.InteractionDefinitionId, BiologicalInteractionIds.ConstructRepair, StringComparison.Ordinal) && !body.AcceptsRepair)
            {
                incompatible = true;
                explanation = "Body snapshot rejects construct repair.";
            }

            if (!incompatible)
            {
                return null;
            }

            return new RuntimeBiologicalInteractionRule(
                $"implicit.{context.InteractionDefinitionId}.body-snapshot",
                BiologicalCompatibilitySourceKind.System,
                body.ActorBodyId,
                context.InteractionDefinitionId,
                context.Category,
                BiologicalInteractionRuleKind.CompatibilityOverride,
                BiologicalCompatibilityState.Incompatible,
                1f,
                1f,
                1f,
                0f,
                float.PositiveInfinity,
                -1000,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                explanation);
        }

        private static bool RuleTargetsContext(RuntimeBiologicalInteractionRule rule, BiologicalInteractionEvaluationContext context)
        {
            if (!string.IsNullOrWhiteSpace(rule.InteractionDefinitionId))
            {
                return string.Equals(rule.InteractionDefinitionId, context.InteractionDefinitionId, StringComparison.Ordinal);
            }

            return rule.Category != BiologicalInteractionCategory.Unknown && rule.Category == context.Category;
        }

        private static bool RuleRequirementsMatch(RuntimeBiologicalInteractionRule rule, BiologicalInteractionEvaluationContext context, out string failureReason)
        {
            failureReason = string.Empty;
            foreach (string required in rule.RequiredRuntimeCapabilityKeys)
            {
                BodyCapabilitySummary capability = context.Body.BiologicalCapabilities.FirstOrDefault(candidate => string.Equals(candidate.CapabilityId, required, StringComparison.Ordinal));
                if (capability == null || capability.Blocked || !capability.BooleanValue)
                {
                    failureReason = $"Required runtime Capability '{required}' was not active.";
                    return false;
                }
            }

            foreach (string blocked in rule.BlockingRuntimeCapabilityKeys)
            {
                BodyCapabilitySummary capability = context.Body.BiologicalCapabilities.FirstOrDefault(candidate => string.Equals(candidate.CapabilityId, blocked, StringComparison.Ordinal));
                if (capability != null && !capability.Blocked && capability.BooleanValue)
                {
                    failureReason = $"Blocking runtime Capability '{blocked}' was active.";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(rule.RequiredNodeId))
            {
                if (context.TargetNode == null || !string.Equals(context.TargetNode.NodeId, rule.RequiredNodeId, StringComparison.Ordinal))
                {
                    failureReason = $"Required anatomy node '{rule.RequiredNodeId}' was not targeted.";
                    return false;
                }
            }

            if (rule.RequiredNodeCategories.Count > 0)
            {
                if (context.TargetNode == null || !rule.RequiredNodeCategories.Contains(context.TargetNode.Category))
                {
                    failureReason = "Required anatomy node category was not targeted.";
                    return false;
                }
            }

            if (rule.RequiredAnatomyTagIds.Count > 0)
            {
                if (context.TargetNode == null)
                {
                    failureReason = "Required anatomy tag had no target node.";
                    return false;
                }

                HashSet<string> nodeTags = new HashSet<string>(context.TargetNode.DamageTagIds ?? Array.Empty<string>(), StringComparer.Ordinal);
                if (!rule.RequiredAnatomyTagIds.Any(nodeTags.Contains))
                {
                    failureReason = "Required anatomy tag was not present.";
                    return false;
                }
            }

            return true;
        }

        private static string BuildMessage(BiologicalCompatibilityState compatibility, bool immune, bool suppressed, bool affinity, bool absorbed, string convertedInteractionId)
        {
            List<string> parts = new List<string> { compatibility.ToString() };
            if (immune)
            {
                parts.Add("Immune");
            }

            if (suppressed)
            {
                parts.Add("Suppressed");
            }

            if (affinity)
            {
                parts.Add("Affinity");
            }

            if (absorbed)
            {
                parts.Add("Absorbed");
            }

            if (!string.IsNullOrWhiteSpace(convertedInteractionId))
            {
                parts.Add($"Converted={convertedInteractionId}");
            }

            return string.Join(" ", parts);
        }
    }
}
