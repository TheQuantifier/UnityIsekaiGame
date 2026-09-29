using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Interactions
{
    [Serializable]
    public sealed class SocialInteractionRoleDefinitionData
    {
        public SocialInteractionRole role;
        public bool required;
        public bool allowDuplicates;

        public SocialInteractionRoleDefinitionData Clone()
        {
            return new SocialInteractionRoleDefinitionData
            {
                role = role,
                required = required,
                allowDuplicates = allowDuplicates
            };
        }
    }

    [Serializable]
    public sealed class SocialInteractionConsequenceDefinitionData
    {
        public string consequenceId;
        public SocialConsequenceTargetRuntime targetRuntime;
        public SocialConsequenceOperation operation;
        public SocialInteractionRole actorRole = SocialInteractionRole.Initiator;
        public SocialInteractionRole subjectRole = SocialInteractionRole.Target;
        public string dimensionId;
        public string audienceId;
        public string relationshipDefinitionId;
        public string rumorDefinitionId;
        public string rumorChannelId;
        public int amount;
        public bool required = true;
        public bool onlyWhenWitnessed;
        public bool onlyWhenPublic;
        public SocialInteractionOutcome[] appliesToOutcomes = Array.Empty<SocialInteractionOutcome>();
        public string[] tags = Array.Empty<string>();

        public SocialInteractionConsequenceDefinitionData Clone()
        {
            return new SocialInteractionConsequenceDefinitionData
            {
                consequenceId = consequenceId ?? string.Empty,
                targetRuntime = targetRuntime,
                operation = operation,
                actorRole = actorRole,
                subjectRole = subjectRole,
                dimensionId = dimensionId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                relationshipDefinitionId = relationshipDefinitionId ?? string.Empty,
                rumorDefinitionId = rumorDefinitionId ?? string.Empty,
                rumorChannelId = rumorChannelId ?? string.Empty,
                amount = amount,
                required = required,
                onlyWhenWitnessed = onlyWhenWitnessed,
                onlyWhenPublic = onlyWhenPublic,
                appliesToOutcomes = appliesToOutcomes == null ? Array.Empty<SocialInteractionOutcome>() : appliesToOutcomes.ToArray(),
                tags = Clean(tags)
            };
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

    [CreateAssetMenu(fileName = "SocialInteractionDefinition", menuName = "Unity Isekai Game/Social/Social Interaction Definition")]
    public sealed class SocialInteractionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string interactionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SocialInteractionCategory category = SocialInteractionCategory.Custom;
        [SerializeField] private SocialInteractionRoleDefinitionData[] roles;
        [SerializeField] private bool allowSelfTarget;
        [SerializeField] private bool supportsWitnesses = true;
        [SerializeField] private bool supportsPublicAudience = true;
        [SerializeField] private bool requiresResponse;
        [SerializeField] private SocialInteractionResponse[] allowedResponses = Array.Empty<SocialInteractionResponse>();
        [SerializeField] private SocialInteractionOutcome baseOutcome = SocialInteractionOutcome.Success;
        [SerializeField] private SocialInteractionOutcome acceptedOutcome = SocialInteractionOutcome.Accepted;
        [SerializeField] private SocialInteractionOutcome refusedOutcome = SocialInteractionOutcome.Refused;
        [SerializeField] private SocialInteractionCooldownScope cooldownScope = SocialInteractionCooldownScope.None;
        [SerializeField] private double cooldownSeconds;
        [SerializeField] private SocialInteractionVisibility defaultVisibility = SocialInteractionVisibility.Private;
        [SerializeField] private bool createsHistoryReference;
        [SerializeField] private bool createsMemoryReference;
        [SerializeField] private SocialInteractionConsequenceDefinitionData[] consequences;
        [SerializeField] private string[] requirementIds = Array.Empty<string>();
        [SerializeField] private string[] capabilityIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => interactionDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SocialInteractionCategory Category => category;
        public IReadOnlyList<SocialInteractionRoleDefinitionData> Roles => roles ?? Array.Empty<SocialInteractionRoleDefinitionData>();
        public bool AllowSelfTarget => allowSelfTarget;
        public bool SupportsWitnesses => supportsWitnesses;
        public bool SupportsPublicAudience => supportsPublicAudience;
        public bool RequiresResponse => requiresResponse;
        public IReadOnlyList<SocialInteractionResponse> AllowedResponses => allowedResponses ?? Array.Empty<SocialInteractionResponse>();
        public SocialInteractionOutcome BaseOutcome => baseOutcome;
        public SocialInteractionOutcome AcceptedOutcome => acceptedOutcome;
        public SocialInteractionOutcome RefusedOutcome => refusedOutcome;
        public SocialInteractionCooldownScope CooldownScope => cooldownScope;
        public double CooldownSeconds => Math.Max(0d, cooldownSeconds);
        public SocialInteractionVisibility DefaultVisibility => defaultVisibility;
        public bool CreatesHistoryReference => createsHistoryReference;
        public bool CreatesMemoryReference => createsMemoryReference;
        public IReadOnlyList<SocialInteractionConsequenceDefinitionData> Consequences => consequences ?? Array.Empty<SocialInteractionConsequenceDefinitionData>();
        public IReadOnlyList<string> RequirementIds => requirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> CapabilityIds => capabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            interactionDefinitionId = interactionDefinitionId?.Trim();
            cooldownSeconds = Math.Max(0d, cooldownSeconds);
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            SocialInteractionCategory interactionCategory,
            SocialInteractionOutcome outcome,
            IEnumerable<SocialInteractionConsequenceDefinitionData> consequenceRules,
            bool responseRequired = false,
            IEnumerable<SocialInteractionResponse> responses = null,
            bool selfTargetAllowed = false,
            bool witnessesSupported = true,
            bool publicAudienceSupported = true,
            SocialInteractionVisibility visibility = SocialInteractionVisibility.Private,
            SocialInteractionCooldownScope repeatScope = SocialInteractionCooldownScope.None,
            double repeatCooldownSeconds = 0d,
            bool historyReference = false,
            bool memoryReference = false,
            IEnumerable<string> requiredIds = null,
            IEnumerable<string> requiredCapabilities = null,
            IEnumerable<string> tagIds = null)
        {
            interactionDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = interactionCategory;
            roles = new[]
            {
                Role(SocialInteractionRole.Initiator, true, false),
                Role(SocialInteractionRole.Target, true, false),
                Role(SocialInteractionRole.Witness, false, true),
                Role(SocialInteractionRole.Audience, false, true),
                Role(SocialInteractionRole.Subject, false, true)
            };
            baseOutcome = outcome;
            acceptedOutcome = SocialInteractionOutcome.Accepted;
            refusedOutcome = SocialInteractionOutcome.Refused;
            requiresResponse = responseRequired;
            allowedResponses = CleanResponses(responses);
            allowSelfTarget = selfTargetAllowed;
            supportsWitnesses = witnessesSupported;
            supportsPublicAudience = publicAudienceSupported;
            defaultVisibility = visibility;
            cooldownScope = repeatScope;
            cooldownSeconds = Math.Max(0d, repeatCooldownSeconds);
            createsHistoryReference = historyReference;
            createsMemoryReference = memoryReference;
            consequences = CleanConsequences(consequenceRules);
            requirementIds = Clean(requiredIds);
            capabilityIds = Clean(requiredCapabilities);
            tags = Clean(tagIds);
            version = 1;
        }

        public bool AllowsRole(SocialInteractionRole role)
        {
            return Roles.Any(item => item.role == role);
        }

        public bool RequiresRole(SocialInteractionRole role)
        {
            return Roles.Any(item => item.role == role && item.required);
        }

        public bool AllowsResponse(SocialInteractionResponse response)
        {
            return !RequiresResponse || AllowedResponses.Contains(response);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Social Interaction Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("social-interaction.", StringComparison.Ordinal))
            {
                report.AddWarning($"Social Interaction Definition '{Id}' should use the 'social-interaction.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(SocialInteractionCategory), category))
            {
                report.AddError($"Social Interaction Definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(SocialInteractionOutcome), baseOutcome))
            {
                report.AddError($"Social Interaction Definition '{DisplayName}' has invalid base outcome '{baseOutcome}'.");
            }

            if (!Enum.IsDefined(typeof(SocialInteractionCooldownScope), cooldownScope))
            {
                report.AddError($"Social Interaction Definition '{DisplayName}' has invalid cooldown scope '{cooldownScope}'.");
            }

            if (double.IsNaN(cooldownSeconds) || double.IsInfinity(cooldownSeconds) || cooldownSeconds < 0d)
            {
                report.AddError($"Social Interaction Definition '{DisplayName}' has invalid cooldown seconds '{cooldownSeconds}'.");
            }

            SocialInteractionRoleDefinitionData[] effectiveRoles = roles ?? Array.Empty<SocialInteractionRoleDefinitionData>();
            if (!effectiveRoles.Any(role => role != null && role.role == SocialInteractionRole.Initiator && role.required)
                || !effectiveRoles.Any(role => role != null && role.role == SocialInteractionRole.Target && role.required))
            {
                report.AddError($"Social Interaction Definition '{DisplayName}' must require Initiator and Target roles.");
            }

            foreach (IGrouping<SocialInteractionRole, SocialInteractionRoleDefinitionData> group in effectiveRoles.Where(role => role != null).GroupBy(role => role.role))
            {
                if (group.Count() > 1)
                {
                    report.AddError($"Social Interaction Definition '{DisplayName}' declares duplicate role '{group.Key}'.");
                }
            }

            if (requiresResponse && (allowedResponses == null || allowedResponses.Length == 0 || allowedResponses.All(response => response == SocialInteractionResponse.None)))
            {
                report.AddError($"Social Interaction Definition '{DisplayName}' requires a response but declares no concrete allowed responses.");
            }

            HashSet<string> consequenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialInteractionConsequenceDefinitionData raw in consequences ?? Array.Empty<SocialInteractionConsequenceDefinitionData>())
            {
                SocialInteractionConsequenceDefinitionData consequence = raw?.Clone();
                if (consequence == null || string.IsNullOrWhiteSpace(consequence.consequenceId) || !consequenceIds.Add(consequence.consequenceId))
                {
                    report.AddError($"Social Interaction Definition '{DisplayName}' contains a blank or duplicate consequence ID.");
                    continue;
                }

                if (!Enum.IsDefined(typeof(SocialConsequenceTargetRuntime), consequence.targetRuntime)
                    || !Enum.IsDefined(typeof(SocialConsequenceOperation), consequence.operation)
                    || !Enum.IsDefined(typeof(SocialInteractionRole), consequence.actorRole)
                    || !Enum.IsDefined(typeof(SocialInteractionRole), consequence.subjectRole))
                {
                    report.AddError($"Social Interaction consequence '{consequence.consequenceId}' has invalid enum metadata.");
                }

                if (!AllowsRole(consequence.actorRole) || !AllowsRole(consequence.subjectRole))
                {
                    report.AddError($"Social Interaction consequence '{consequence.consequenceId}' references a role not allowed by '{DisplayName}'.");
                }

                ValidateReferencedDefinition(definitionsById, report, DisplayName, consequence);
            }
        }

        private static void ValidateReferencedDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, string display, SocialInteractionConsequenceDefinitionData consequence)
        {
            if (definitionsById == null)
            {
                return;
            }

            if (consequence.targetRuntime == SocialConsequenceTargetRuntime.Attitude
                && (string.IsNullOrWhiteSpace(consequence.dimensionId) || !definitionsById.TryGetValue(consequence.dimensionId, out IGameDefinition attitude) || attitude is not AttitudeDimensionDefinition))
            {
                report.AddError($"Social Interaction Definition '{display}' consequence '{consequence.consequenceId}' references missing Attitude Dimension '{consequence.dimensionId}'.");
            }

            if (consequence.targetRuntime == SocialConsequenceTargetRuntime.Reputation)
            {
                if (string.IsNullOrWhiteSpace(consequence.dimensionId) || !definitionsById.TryGetValue(consequence.dimensionId, out IGameDefinition dimension) || dimension is not ReputationDimensionDefinition)
                {
                    report.AddError($"Social Interaction Definition '{display}' consequence '{consequence.consequenceId}' references missing Reputation Dimension '{consequence.dimensionId}'.");
                }

                if (string.IsNullOrWhiteSpace(consequence.audienceId) || !definitionsById.TryGetValue(consequence.audienceId, out IGameDefinition audience) || audience is not ReputationAudienceDefinition)
                {
                    report.AddError($"Social Interaction Definition '{display}' consequence '{consequence.consequenceId}' references missing Reputation Audience '{consequence.audienceId}'.");
                }
            }

            if (consequence.targetRuntime == SocialConsequenceTargetRuntime.Relationship
                && !string.IsNullOrWhiteSpace(consequence.relationshipDefinitionId)
                && (!definitionsById.TryGetValue(consequence.relationshipDefinitionId, out IGameDefinition relationship) || relationship is not RelationshipDefinition))
            {
                report.AddError($"Social Interaction Definition '{display}' consequence '{consequence.consequenceId}' references missing Relationship Definition '{consequence.relationshipDefinitionId}'.");
            }

            if (consequence.targetRuntime == SocialConsequenceTargetRuntime.Rumor
                && (!definitionsById.TryGetValue(consequence.rumorDefinitionId, out IGameDefinition rumor) || rumor is not RumorDefinition
                    || !definitionsById.TryGetValue(consequence.rumorChannelId, out IGameDefinition channel) || channel is not RumorCommunicationChannelDefinition))
            {
                report.AddError($"Social Interaction Definition '{display}' consequence '{consequence.consequenceId}' references missing Rumor or Channel definition.");
            }
        }

        private static SocialInteractionRoleDefinitionData Role(SocialInteractionRole role, bool required, bool duplicates)
        {
            return new SocialInteractionRoleDefinitionData { role = role, required = required, allowDuplicates = duplicates };
        }

        private static SocialInteractionResponse[] CleanResponses(IEnumerable<SocialInteractionResponse> values)
        {
            return (values ?? Array.Empty<SocialInteractionResponse>())
                .Where(value => value != SocialInteractionResponse.None)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
        }

        private static SocialInteractionConsequenceDefinitionData[] CleanConsequences(IEnumerable<SocialInteractionConsequenceDefinitionData> values)
        {
            return (values ?? Array.Empty<SocialInteractionConsequenceDefinitionData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.consequenceId))
                .Select(value => value.Clone())
                .GroupBy(value => value.consequenceId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.consequenceId, StringComparer.Ordinal)
                .ToArray();
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
