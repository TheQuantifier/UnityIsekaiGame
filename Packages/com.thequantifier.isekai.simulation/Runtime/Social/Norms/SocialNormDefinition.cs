using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Norms
{
    [CreateAssetMenu(fileName = "SocialNormDefinition", menuName = "Unity Isekai Game/Social/Social Norm Definition")]
    public sealed class SocialNormDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string normId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SocialNormCategory category = SocialNormCategory.Custom;
        [SerializeField] private SocialNormScope scope = SocialNormScope.Global;
        [SerializeField] private SocialNormConductStrength strength = SocialNormConductStrength.Neutral;
        [SerializeField] private SocialNormAssessmentClassification satisfiedClassification = SocialNormAssessmentClassification.Satisfied;
        [SerializeField] private SocialNormAssessmentClassification violatedClassification = SocialNormAssessmentClassification.Violation;
        [SerializeField] private int baseSeverity;
        [SerializeField] private int priority;
        [SerializeField] private int specificity;
        [SerializeField] private bool requiresTarget;
        [SerializeField] private bool requiresWitness;
        [SerializeField] private bool requiresPublic;
        [SerializeField] private string expectedInteractionDefinitionId;
        [SerializeField] private string expectedPromiseState;
        [SerializeField] private string[] overrideNormIds = Array.Empty<string>();
        [SerializeField] private SocialNormContextConditionData[] applicabilityConditions = Array.Empty<SocialNormContextConditionData>();
        [SerializeField] private SocialNormExceptionDefinitionData[] exceptions = Array.Empty<SocialNormExceptionDefinitionData>();
        [SerializeField] private SocialNormConsequenceDefinitionData[] consequences = Array.Empty<SocialNormConsequenceDefinitionData>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => normId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SocialNormCategory Category => category;
        public SocialNormScope Scope => scope;
        public SocialNormConductStrength Strength => strength;
        public SocialNormAssessmentClassification SatisfiedClassification => satisfiedClassification;
        public SocialNormAssessmentClassification ViolatedClassification => violatedClassification;
        public int BaseSeverity => baseSeverity;
        public int Priority => priority;
        public int Specificity => specificity;
        public bool RequiresTarget => requiresTarget;
        public bool RequiresWitness => requiresWitness;
        public bool RequiresPublic => requiresPublic;
        public string ExpectedInteractionDefinitionId => expectedInteractionDefinitionId ?? string.Empty;
        public string ExpectedPromiseState => expectedPromiseState ?? string.Empty;
        public IReadOnlyList<string> OverrideNormIds => overrideNormIds ?? Array.Empty<string>();
        public IReadOnlyList<SocialNormContextConditionData> ApplicabilityConditions => applicabilityConditions ?? Array.Empty<SocialNormContextConditionData>();
        public IReadOnlyList<SocialNormExceptionDefinitionData> Exceptions => exceptions ?? Array.Empty<SocialNormExceptionDefinitionData>();
        public IReadOnlyList<SocialNormConsequenceDefinitionData> Consequences => consequences ?? Array.Empty<SocialNormConsequenceDefinitionData>();
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            normId = normId?.Trim();
            expectedInteractionDefinitionId = expectedInteractionDefinitionId?.Trim();
            expectedPromiseState = expectedPromiseState?.Trim();
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            SocialNormCategory normCategory,
            SocialNormScope normScope,
            SocialNormConductStrength conductStrength,
            SocialNormAssessmentClassification satisfied,
            SocialNormAssessmentClassification violated,
            int severity,
            int normPriority,
            int normSpecificity,
            bool targetRequired = false,
            bool witnessRequired = false,
            bool publicRequired = false,
            string interactionDefinitionId = "",
            string promiseState = "",
            IEnumerable<SocialNormContextConditionData> conditions = null,
            IEnumerable<SocialNormExceptionDefinitionData> exceptionRules = null,
            IEnumerable<SocialNormConsequenceDefinitionData> consequenceRules = null,
            IEnumerable<string> overrides = null,
            IEnumerable<string> tagIds = null,
            string text = "")
        {
            normId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            description = text ?? string.Empty;
            category = normCategory;
            scope = normScope;
            strength = conductStrength;
            satisfiedClassification = satisfied;
            violatedClassification = violated;
            baseSeverity = severity;
            priority = normPriority;
            specificity = normSpecificity;
            requiresTarget = targetRequired;
            requiresWitness = witnessRequired;
            requiresPublic = publicRequired;
            expectedInteractionDefinitionId = interactionDefinitionId?.Trim();
            expectedPromiseState = promiseState?.Trim();
            applicabilityConditions = (conditions ?? Array.Empty<SocialNormContextConditionData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            exceptions = (exceptionRules ?? Array.Empty<SocialNormExceptionDefinitionData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            consequences = (consequenceRules ?? Array.Empty<SocialNormConsequenceDefinitionData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            overrideNormIds = Clean(overrides);
            tags = Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Social Norm Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("social-norm.", StringComparison.Ordinal))
            {
                report.AddWarning($"Social Norm Definition '{Id}' should use the 'social-norm.' namespace prefix.");
            }

            ValidateEnum(report);
            ValidateNumber(report);
            ValidateReferences(definitionsById ?? new Dictionary<string, IGameDefinition>(StringComparer.Ordinal), report);
        }

        private void ValidateEnum(DefinitionValidationReport report)
        {
            if (!Enum.IsDefined(typeof(SocialNormCategory), category))
            {
                report.AddError($"Social Norm '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(SocialNormScope), scope))
            {
                report.AddError($"Social Norm '{DisplayName}' has invalid scope '{scope}'.");
            }

            if (!Enum.IsDefined(typeof(SocialNormConductStrength), strength))
            {
                report.AddError($"Social Norm '{DisplayName}' has invalid strength '{strength}'.");
            }

            if (!Enum.IsDefined(typeof(SocialNormAssessmentClassification), satisfiedClassification)
                || !Enum.IsDefined(typeof(SocialNormAssessmentClassification), violatedClassification))
            {
                report.AddError($"Social Norm '{DisplayName}' has invalid classification metadata.");
            }
        }

        private void ValidateNumber(DefinitionValidationReport report)
        {
            if (version < 1)
            {
                report.AddError($"Social Norm '{DisplayName}' has invalid version '{version}'.");
            }

            if (baseSeverity < 0)
            {
                report.AddError($"Social Norm '{DisplayName}' has negative base severity.");
            }
        }

        private void ValidateReferences(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (!string.IsNullOrWhiteSpace(expectedInteractionDefinitionId) && !definitionsById.ContainsKey(expectedInteractionDefinitionId))
            {
                report.AddError($"Social Norm '{DisplayName}' references missing Social Interaction '{expectedInteractionDefinitionId}'.");
            }

            foreach (string overrideId in overrideNormIds ?? Array.Empty<string>())
            {
                if (string.Equals(overrideId, Id, StringComparison.Ordinal))
                {
                    report.AddError($"Social Norm '{DisplayName}' cannot override itself.");
                }
                else if (!definitionsById.ContainsKey(overrideId))
                {
                    report.AddError($"Social Norm '{DisplayName}' references missing override norm '{overrideId}'.");
                }
            }

            foreach (SocialNormContextConditionData condition in applicabilityConditions ?? Array.Empty<SocialNormContextConditionData>())
            {
                if (condition == null || string.IsNullOrWhiteSpace(condition.conditionId))
                {
                    report.AddError($"Social Norm '{DisplayName}' contains a context condition without a stable ID.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(condition.relationshipDefinitionId) && !definitionsById.ContainsKey(condition.relationshipDefinitionId))
                {
                    report.AddError($"Social Norm '{DisplayName}' condition '{condition.conditionId}' references missing Relationship Definition '{condition.relationshipDefinitionId}'.");
                }
            }

            foreach (SocialNormExceptionDefinitionData exception in exceptions ?? Array.Empty<SocialNormExceptionDefinitionData>())
            {
                if (exception == null || string.IsNullOrWhiteSpace(exception.exceptionId))
                {
                    report.AddError($"Social Norm '{DisplayName}' contains an exception without a stable ID.");
                    continue;
                }

                if (!Enum.IsDefined(typeof(SocialNormExceptionKind), exception.kind) || !Enum.IsDefined(typeof(SocialNormExceptionEffect), exception.effect))
                {
                    report.AddError($"Social Norm '{DisplayName}' exception '{exception.exceptionId}' has invalid enum metadata.");
                }

                if (!string.IsNullOrWhiteSpace(exception.redirectNormId) && !definitionsById.ContainsKey(exception.redirectNormId))
                {
                    report.AddError($"Social Norm '{DisplayName}' exception '{exception.exceptionId}' redirects to missing norm '{exception.redirectNormId}'.");
                }
            }

            foreach (SocialNormConsequenceDefinitionData consequence in consequences ?? Array.Empty<SocialNormConsequenceDefinitionData>())
            {
                ValidateConsequence(definitionsById, report, consequence);
            }
        }

        private void ValidateConsequence(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, SocialNormConsequenceDefinitionData consequence)
        {
            if (consequence == null || string.IsNullOrWhiteSpace(consequence.consequenceId))
            {
                report.AddError($"Social Norm '{DisplayName}' contains a consequence without a stable ID.");
                return;
            }

            if (!Enum.IsDefined(typeof(SocialNormConsequenceTargetRuntime), consequence.targetRuntime)
                || !Enum.IsDefined(typeof(SocialNormConsequenceOperation), consequence.operation)
                || !Enum.IsDefined(typeof(SocialNormConsequencePolicy), consequence.policy))
            {
                report.AddError($"Social Norm '{DisplayName}' consequence '{consequence.consequenceId}' has invalid enum metadata.");
            }

            if (consequence.targetRuntime == SocialNormConsequenceTargetRuntime.InterpersonalAttitude
                && !string.IsNullOrWhiteSpace(consequence.dimensionId)
                && !definitionsById.ContainsKey(consequence.dimensionId))
            {
                report.AddError($"Social Norm '{DisplayName}' consequence '{consequence.consequenceId}' references missing Attitude Dimension '{consequence.dimensionId}'.");
            }

            if (consequence.targetRuntime == SocialNormConsequenceTargetRuntime.Reputation)
            {
                if (!string.IsNullOrWhiteSpace(consequence.dimensionId) && !definitionsById.ContainsKey(consequence.dimensionId))
                {
                    report.AddError($"Social Norm '{DisplayName}' consequence '{consequence.consequenceId}' references missing Reputation Dimension '{consequence.dimensionId}'.");
                }

                if (!string.IsNullOrWhiteSpace(consequence.audienceId) && !definitionsById.ContainsKey(consequence.audienceId))
                {
                    report.AddError($"Social Norm '{DisplayName}' consequence '{consequence.consequenceId}' references missing Reputation Audience '{consequence.audienceId}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(consequence.relationshipDefinitionId) && !definitionsById.ContainsKey(consequence.relationshipDefinitionId))
            {
                report.AddError($"Social Norm '{DisplayName}' consequence '{consequence.consequenceId}' references missing Relationship Definition '{consequence.relationshipDefinitionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(consequence.rumorDefinitionId) && !definitionsById.ContainsKey(consequence.rumorDefinitionId))
            {
                report.AddError($"Social Norm '{DisplayName}' consequence '{consequence.consequenceId}' references missing Rumor Definition '{consequence.rumorDefinitionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(consequence.rumorChannelId) && !definitionsById.ContainsKey(consequence.rumorChannelId))
            {
                report.AddError($"Social Norm '{DisplayName}' consequence '{consequence.consequenceId}' references missing Rumor Channel '{consequence.rumorChannelId}'.");
            }
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
