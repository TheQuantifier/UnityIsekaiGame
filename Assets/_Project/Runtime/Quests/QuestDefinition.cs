using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Quests
{
    [CreateAssetMenu(fileName = "Quest", menuName = "Unity Isekai Game/Quests/Quest")]
    public sealed class QuestDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string questId;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 4)] private string summary;
        [SerializeField, TextArea(3, 8)] private string detailedDescription;
        [SerializeField] private QuestCategory category = QuestCategory.SideQuest;
        [Header("Identity Metadata")]
        [SerializeField] private QuestDefinitionImportance importance = QuestDefinitionImportance.Standard;
        [SerializeField] private QuestDefinitionRepeatabilityPolicy repeatabilityPolicy = QuestDefinitionRepeatabilityPolicy.Unique;
        [SerializeField] private QuestVisibility defaultVisibility = QuestVisibility.Public;
        [SerializeField] private QuestSourceChannel defaultSourceChannel = QuestSourceChannel.Manual;
        [SerializeField] private QuestIssuerType[] supportedIssuerTypes = Array.Empty<QuestIssuerType>();
        [SerializeField] private QuestRecipientScope[] supportedRecipientScopes = Array.Empty<QuestRecipientScope>();
        [SerializeField] private string[] defaultTagIds = Array.Empty<string>();
        [SerializeField] private string[] supportedSubjectRoleIds = Array.Empty<string>();
        [SerializeField] private bool allowDynamicInstances;
        [SerializeField] private bool allowMultipleSimultaneousInstances;
        [SerializeField] private bool uniquePerWorld = true;
        [SerializeField] private bool uniquePerRecipient;
        [SerializeField] private string identityNotes;
        [Header("Participation Policy")]
        [SerializeField] private QuestAssignmentPolicy assignmentPolicy = QuestAssignmentPolicy.Exclusive;
        [SerializeField] private QuestConsentPolicy consentPolicy = QuestConsentPolicy.ExplicitRecipientConsentRequired;
        [SerializeField] private QuestRefusalPolicy refusalPolicy = QuestRefusalPolicy.MayReoffer;
        [SerializeField] private QuestAbandonmentPolicy abandonmentPolicy = QuestAbandonmentPolicy.AllowedReleasesCapacity;
        [SerializeField] private int assignmentCapacity = 1;
        [SerializeField] private double availabilityStartWorldTime = -1d;
        [SerializeField] private double availabilityEndWorldTime = -1d;
        [SerializeField] private double defaultOfferDuration = -1d;
        [SerializeField] private bool issuerWithdrawalAllowed = true;
        [SerializeField] private bool prevalidateEligibilityForOffers = true;
        [SerializeField] private string[] offeringAuthorityRequirementIds = Array.Empty<string>();
        [SerializeField] private QuestEligibilityRequirementGroupData[] eligibilityRequirementGroups = Array.Empty<QuestEligibilityRequirementGroupData>();
        [Header("Objective Progress")]
        [SerializeField] private QuestObjectiveDefinitionData[] objectiveDefinitions = Array.Empty<QuestObjectiveDefinitionData>();
        [SerializeField] private QuestObjectiveGroupDefinitionData[] objectiveGroups = Array.Empty<QuestObjectiveGroupDefinitionData>();
        [Header("Terminal Outcomes")]
        [SerializeField] private QuestCompletionPolicyData completionPolicy = new QuestCompletionPolicyData();
        [SerializeField] private QuestDeadlineDefinitionData[] deadlineDefinitions = Array.Empty<QuestDeadlineDefinitionData>();
        [SerializeField] private QuestFailureConditionDefinitionData[] failureConditions = Array.Empty<QuestFailureConditionDefinitionData>();
        [SerializeField] private QuestRewardPackageDefinitionData[] rewardPackages = Array.Empty<QuestRewardPackageDefinitionData>();
        [SerializeField] private QuestConsequenceDefinitionData[] consequenceDefinitions = Array.Empty<QuestConsequenceDefinitionData>();

        public string QuestId => questId;
        public string Id => questId;
        public string DisplayName => Title;
        public string Title => string.IsNullOrWhiteSpace(title) ? "Untitled Quest" : title;
        public string Summary => summary;
        public string DetailedDescription => detailedDescription;
        public QuestCategory Category => category;
        public QuestDefinitionImportance Importance => importance;
        public QuestDefinitionRepeatabilityPolicy RepeatabilityPolicy => repeatabilityPolicy;
        public QuestVisibility DefaultVisibility => defaultVisibility;
        public QuestSourceChannel DefaultSourceChannel => defaultSourceChannel;
        public IReadOnlyList<QuestIssuerType> SupportedIssuerTypes => supportedIssuerTypes ?? Array.Empty<QuestIssuerType>();
        public IReadOnlyList<QuestRecipientScope> SupportedRecipientScopes => supportedRecipientScopes ?? Array.Empty<QuestRecipientScope>();
        public IReadOnlyList<string> DefaultTagIds => defaultTagIds ?? Array.Empty<string>();
        public IReadOnlyList<string> SupportedSubjectRoleIds => supportedSubjectRoleIds ?? Array.Empty<string>();
        public bool AllowDynamicInstances => allowDynamicInstances || RepeatabilityPolicy == QuestDefinitionRepeatabilityPolicy.DynamicTemplate;
        public bool AllowMultipleSimultaneousInstances => allowMultipleSimultaneousInstances || RepeatabilityPolicy == QuestDefinitionRepeatabilityPolicy.Reusable || RepeatabilityPolicy == QuestDefinitionRepeatabilityPolicy.DynamicTemplate;
        public bool UniquePerWorld => uniquePerWorld && !AllowMultipleSimultaneousInstances;
        public bool UniquePerRecipient => uniquePerRecipient;
        public string IdentityNotes => identityNotes ?? string.Empty;
        public QuestAssignmentPolicy AssignmentPolicy => assignmentPolicy == QuestAssignmentPolicy.Unknown ? QuestAssignmentPolicy.Exclusive : assignmentPolicy;
        public QuestConsentPolicy ConsentPolicy => consentPolicy == QuestConsentPolicy.Unknown ? QuestConsentPolicy.ExplicitRecipientConsentRequired : consentPolicy;
        public QuestRefusalPolicy RefusalPolicy => refusalPolicy == QuestRefusalPolicy.Unknown ? QuestRefusalPolicy.MayReoffer : refusalPolicy;
        public QuestAbandonmentPolicy AbandonmentPolicy => abandonmentPolicy;
        public int AssignmentCapacity => AssignmentPolicy == QuestAssignmentPolicy.Nonexclusive ? Math.Max(assignmentCapacity, 0) : Math.Max(1, assignmentCapacity);
        public double AvailabilityStartWorldTime => availabilityStartWorldTime;
        public double AvailabilityEndWorldTime => availabilityEndWorldTime;
        public double DefaultOfferDuration => defaultOfferDuration;
        public bool IssuerWithdrawalAllowed => issuerWithdrawalAllowed;
        public bool PrevalidateEligibilityForOffers => prevalidateEligibilityForOffers;
        public IReadOnlyList<string> OfferingAuthorityRequirementIds => offeringAuthorityRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<QuestEligibilityRequirementGroupData> EligibilityRequirementGroups => (eligibilityRequirementGroups ?? Array.Empty<QuestEligibilityRequirementGroupData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<QuestObjectiveDefinitionData> ObjectiveDefinitions => (objectiveDefinitions ?? Array.Empty<QuestObjectiveDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.sequenceOrder).ThenBy(value => value.objectiveDefinitionId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<QuestObjectiveGroupDefinitionData> ObjectiveGroups => (objectiveGroups ?? Array.Empty<QuestObjectiveGroupDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.sequenceOrder).ThenBy(value => value.groupDefinitionId, StringComparer.Ordinal).ToArray();
        public QuestCompletionPolicyData CompletionPolicy => completionPolicy?.Clone() ?? new QuestCompletionPolicyData();
        public IReadOnlyList<QuestDeadlineDefinitionData> DeadlineDefinitions => (deadlineDefinitions ?? Array.Empty<QuestDeadlineDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.deadlineDefinitionId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<QuestFailureConditionDefinitionData> FailureConditions => (failureConditions ?? Array.Empty<QuestFailureConditionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.failureConditionId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<QuestRewardPackageDefinitionData> RewardPackages => (rewardPackages ?? Array.Empty<QuestRewardPackageDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.rewardPackageId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<QuestConsequenceDefinitionData> ConsequenceDefinitions => (consequenceDefinitions ?? Array.Empty<QuestConsequenceDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.consequenceDefinitionId, StringComparer.Ordinal).ToArray();
        public bool Repeatable => RepeatabilityPolicy != QuestDefinitionRepeatabilityPolicy.Unique;
        public bool HiddenUntilDiscovered => DefaultVisibility == QuestVisibility.Hidden || DefaultVisibility == QuestVisibility.Secret;
        public bool CanAbandon => AbandonmentPolicy != QuestAbandonmentPolicy.NotAllowed;

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || report == null)
            {
                return;
            }

            ValidateIdentityMetadata(report);
            ValidateObjectiveProgressDefinitions(report);
            ValidateOutcomeDefinitions(report);
        }

        public void DevelopmentConfigureIdentity(
            string id,
            string displayTitle,
            QuestCategory questCategory,
            QuestDefinitionImportance questImportance = QuestDefinitionImportance.Standard,
            QuestDefinitionRepeatabilityPolicy repeatPolicy = QuestDefinitionRepeatabilityPolicy.Unique,
            QuestVisibility visibility = QuestVisibility.Public,
            QuestSourceChannel sourceChannel = QuestSourceChannel.Manual,
            IEnumerable<QuestIssuerType> issuerTypes = null,
            IEnumerable<QuestRecipientScope> recipientScopes = null,
            IEnumerable<string> tags = null,
            bool dynamicInstances = false,
            bool multipleSimultaneousInstances = false,
            bool perWorldUnique = true,
            bool perRecipientUnique = false)
        {
            questId = id ?? string.Empty;
            title = string.IsNullOrWhiteSpace(displayTitle) ? id : displayTitle;
            category = questCategory;
            importance = questImportance;
            repeatabilityPolicy = repeatPolicy;
            defaultVisibility = visibility;
            defaultSourceChannel = sourceChannel;
            supportedIssuerTypes = DistinctEnums(issuerTypes);
            supportedRecipientScopes = DistinctEnums(recipientScopes);
            defaultTagIds = Clean(tags);
            allowDynamicInstances = dynamicInstances;
            allowMultipleSimultaneousInstances = multipleSimultaneousInstances;
            uniquePerWorld = perWorldUnique;
            uniquePerRecipient = perRecipientUnique;
        }

        public void DevelopmentConfigureParticipation(
            QuestAssignmentPolicy assignment = QuestAssignmentPolicy.Exclusive,
            QuestConsentPolicy consent = QuestConsentPolicy.ExplicitRecipientConsentRequired,
            QuestRefusalPolicy refusal = QuestRefusalPolicy.MayReoffer,
            QuestAbandonmentPolicy abandonment = QuestAbandonmentPolicy.AllowedReleasesCapacity,
            int capacity = 1,
            double availableFrom = -1d,
            double availableUntil = -1d,
            double offerDuration = -1d,
            bool withdrawalAllowed = true,
            bool prevalidateOffers = true,
            IEnumerable<string> authorityRequirements = null,
            IEnumerable<QuestEligibilityRequirementGroupData> eligibilityGroups = null)
        {
            assignmentPolicy = assignment == QuestAssignmentPolicy.Unknown ? QuestAssignmentPolicy.Exclusive : assignment;
            consentPolicy = consent == QuestConsentPolicy.Unknown ? QuestConsentPolicy.ExplicitRecipientConsentRequired : consent;
            refusalPolicy = refusal == QuestRefusalPolicy.Unknown ? QuestRefusalPolicy.MayReoffer : refusal;
            abandonmentPolicy = abandonment == QuestAbandonmentPolicy.Unknown ? QuestAbandonmentPolicy.AllowedReleasesCapacity : abandonment;
            assignmentCapacity = Math.Max(assignmentPolicy == QuestAssignmentPolicy.Nonexclusive ? 0 : 1, capacity);
            availabilityStartWorldTime = availableFrom;
            availabilityEndWorldTime = availableUntil;
            defaultOfferDuration = offerDuration;
            issuerWithdrawalAllowed = withdrawalAllowed;
            prevalidateEligibilityForOffers = prevalidateOffers;
            offeringAuthorityRequirementIds = Clean(authorityRequirements);
            eligibilityRequirementGroups = (eligibilityGroups ?? Array.Empty<QuestEligibilityRequirementGroupData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        }

        public void DevelopmentConfigureObjectives(
            IEnumerable<QuestObjectiveDefinitionData> objectives,
            IEnumerable<QuestObjectiveGroupDefinitionData> groups = null)
        {
            objectiveDefinitions = (objectives ?? Array.Empty<QuestObjectiveDefinitionData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .OrderBy(value => value.sequenceOrder)
                .ThenBy(value => value.objectiveDefinitionId, StringComparer.Ordinal)
                .ToArray();
            objectiveGroups = (groups ?? Array.Empty<QuestObjectiveGroupDefinitionData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .OrderBy(value => value.sequenceOrder)
                .ThenBy(value => value.groupDefinitionId, StringComparer.Ordinal)
                .ToArray();
        }

        public void DevelopmentConfigureOutcomes(
            QuestCompletionPolicyData completion = null,
            IEnumerable<QuestDeadlineDefinitionData> deadlines = null,
            IEnumerable<QuestFailureConditionDefinitionData> failures = null,
            IEnumerable<QuestRewardPackageDefinitionData> rewards = null,
            IEnumerable<QuestConsequenceDefinitionData> consequences = null)
        {
            completionPolicy = completion?.Clone() ?? new QuestCompletionPolicyData();
            deadlineDefinitions = (deadlines ?? Array.Empty<QuestDeadlineDefinitionData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .OrderBy(value => value.deadlineDefinitionId, StringComparer.Ordinal)
                .ToArray();
            failureConditions = (failures ?? Array.Empty<QuestFailureConditionDefinitionData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .OrderBy(value => value.failureConditionId, StringComparer.Ordinal)
                .ToArray();
            rewardPackages = (rewards ?? Array.Empty<QuestRewardPackageDefinitionData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .OrderBy(value => value.rewardPackageId, StringComparer.Ordinal)
                .ToArray();
            consequenceDefinitions = (consequences ?? Array.Empty<QuestConsequenceDefinitionData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .OrderBy(value => value.consequenceDefinitionId, StringComparer.Ordinal)
                .ToArray();
        }

        private void ValidateIdentityMetadata(DefinitionValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                report.AddError($"QuestDefinition '{Title}' is missing a stable quest definition ID.");
            }

            if (category == QuestCategory.Unknown)
            {
                report.AddError($"QuestDefinition '{Title}' must declare a concrete quest category.");
            }

            if (RepeatabilityPolicy == QuestDefinitionRepeatabilityPolicy.Unknown)
            {
                report.AddError($"QuestDefinition '{Title}' must declare a concrete repeatability policy.");
            }

            if (DefaultVisibility == QuestVisibility.Unknown)
            {
                report.AddError($"QuestDefinition '{Title}' must declare a concrete default visibility.");
            }

            if (defaultSourceChannel == QuestSourceChannel.Unknown)
            {
                report.AddError($"QuestDefinition '{Title}' must declare a concrete source channel.");
            }

            if (AssignmentPolicy == QuestAssignmentPolicy.Unknown)
            {
                report.AddError($"QuestDefinition '{Title}' must declare a concrete assignment policy.");
            }

            if (ConsentPolicy == QuestConsentPolicy.Unknown)
            {
                report.AddError($"QuestDefinition '{Title}' must declare a concrete consent policy.");
            }

            if (AssignmentPolicy != QuestAssignmentPolicy.Nonexclusive && AssignmentCapacity <= 0)
            {
                report.AddError($"QuestDefinition '{Title}' assignment capacity must be positive for limited or exclusive assignment.");
            }

            if (AvailabilityEndWorldTime >= 0d && AvailabilityStartWorldTime >= 0d && AvailabilityEndWorldTime < AvailabilityStartWorldTime)
            {
                report.AddError($"QuestDefinition '{Title}' availability end cannot be before availability start.");
            }

            foreach (QuestEligibilityRequirementGroupData group in EligibilityRequirementGroups)
            {
                if (group.policy == QuestEligibilityGroupPolicy.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' has an eligibility group with an unknown policy.");
                }

                foreach (QuestEligibilityRequirementData requirement in group.requirements ?? Array.Empty<QuestEligibilityRequirementData>())
                {
                    if (requirement.kind == QuestEligibilityRequirementKind.Unknown)
                    {
                        report.AddError($"QuestDefinition '{Title}' has an eligibility requirement with unknown kind.");
                    }
                }
            }
        }

        private void ValidateObjectiveProgressDefinitions(DefinitionValidationReport report)
        {
            QuestObjectiveDefinitionData[] objectives = ObjectiveDefinitions.ToArray();
            HashSet<string> objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> groupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestObjectiveGroupDefinitionData group in ObjectiveGroups)
            {
                if (string.IsNullOrWhiteSpace(group.groupDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has an objective group without a stable ID.");
                    continue;
                }

                if (!groupIds.Add(group.groupDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has duplicate objective group ID '{group.groupDefinitionId}'.");
                }

                if (group.policy == QuestObjectiveGroupPolicy.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' objective group '{group.groupDefinitionId}' has unknown policy.");
                }

                if (group.policy == QuestObjectiveGroupPolicy.AtLeast && group.thresholdCount <= 0)
                {
                    report.AddError($"QuestDefinition '{Title}' objective group '{group.groupDefinitionId}' threshold must be positive.");
                }
            }

            foreach (QuestObjectiveDefinitionData objective in objectives)
            {
                if (string.IsNullOrWhiteSpace(objective.objectiveDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has an objective without a stable objective definition ID.");
                    continue;
                }

                if (!objectiveIds.Add(objective.objectiveDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has duplicate objective definition ID '{objective.objectiveDefinitionId}'.");
                }

                if (objective.category == QuestObjectiveCategory.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' has unknown category.");
                }

                if (objective.progressModel == QuestObjectiveProgressModel.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' has unknown progress model.");
                }

                if (objective.progressSource == QuestObjectiveProgressSource.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' has unknown progress source.");
                }

                if (objective.classification == QuestObjectiveRequirementClassification.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' has unknown requirement classification.");
                }

                if (objective.visibility == QuestObjectiveVisibility.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' has unknown visibility.");
                }

                if (objective.ownershipScope == QuestObjectiveOwnershipScope.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' has unknown ownership scope.");
                }

                if (objective.EffectiveTarget <= 0)
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' has invalid target amount.");
                }

                if (!string.IsNullOrWhiteSpace(objective.groupDefinitionId) && !groupIds.Contains(objective.groupDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' references missing objective group '{objective.groupDefinitionId}'.");
                }
            }

            foreach (QuestObjectiveGroupDefinitionData group in ObjectiveGroups)
            {
                foreach (string objectiveId in group.objectiveDefinitionIds ?? Array.Empty<string>())
                {
                    if (!objectiveIds.Contains(objectiveId))
                    {
                        report.AddError($"QuestDefinition '{Title}' objective group '{group.groupDefinitionId}' references missing objective '{objectiveId}'.");
                    }
                }
            }

            foreach (QuestObjectiveDefinitionData objective in objectives)
            {
                foreach (string prerequisite in objective.prerequisiteObjectiveDefinitionIds ?? Array.Empty<string>())
                {
                    if (!objectiveIds.Contains(prerequisite))
                    {
                        report.AddError($"QuestDefinition '{Title}' objective '{objective.objectiveDefinitionId}' references missing prerequisite objective '{prerequisite}'.");
                    }
                }
            }

            ValidateObjectiveDependencyCycles(objectives, report);
        }

        private void ValidateObjectiveDependencyCycles(IReadOnlyList<QuestObjectiveDefinitionData> objectives, DefinitionValidationReport report)
        {
            Dictionary<string, string[]> edges = objectives
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.objectiveDefinitionId))
                .ToDictionary(value => value.objectiveDefinitionId, value => value.prerequisiteObjectiveDefinitionIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string objectiveId in edges.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                Visit(objectiveId, edges, visiting, visited, report);
            }
        }

        private bool Visit(string objectiveId, IReadOnlyDictionary<string, string[]> edges, ISet<string> visiting, ISet<string> visited, DefinitionValidationReport report)
        {
            if (visited.Contains(objectiveId)) return false;
            if (visiting.Contains(objectiveId))
            {
                report.AddError($"QuestDefinition '{Title}' objective dependency graph has a cycle at '{objectiveId}'.");
                return true;
            }

            visiting.Add(objectiveId);
            if (edges.TryGetValue(objectiveId, out string[] prerequisites))
            {
                foreach (string prerequisite in prerequisites ?? Array.Empty<string>())
                {
                    if (edges.ContainsKey(prerequisite) && Visit(prerequisite, edges, visiting, visited, report))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(objectiveId);
            visited.Add(objectiveId);
            return false;
        }

        private void ValidateOutcomeDefinitions(DefinitionValidationReport report)
        {
            QuestCompletionPolicyData completion = CompletionPolicy;
            if (completion.policy == QuestCompletionPolicy.Unknown)
            {
                report.AddError($"QuestDefinition '{Title}' must declare a concrete completion policy.");
            }

            if (completion.policy == QuestCompletionPolicy.RequireTurnIn && string.IsNullOrWhiteSpace(completion.requiredInteractionPointId))
            {
                report.AddError($"QuestDefinition '{Title}' turn-in completion policy must declare a required interaction point ID.");
            }

            if (completion.policy == QuestCompletionPolicy.RequireIssuerVerification && string.IsNullOrWhiteSpace(completion.requiredIssuerId))
            {
                report.AddError($"QuestDefinition '{Title}' issuer-verification completion policy must declare a required issuer ID.");
            }

            HashSet<string> deadlineIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestDeadlineDefinitionData deadline in DeadlineDefinitions)
            {
                if (string.IsNullOrWhiteSpace(deadline.deadlineDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has a deadline without a stable deadline definition ID.");
                    continue;
                }

                if (!deadlineIds.Add(deadline.deadlineDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has duplicate deadline definition ID '{deadline.deadlineDefinitionId}'.");
                }

                if (deadline.startKind == QuestDeadlineStartKind.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' deadline '{deadline.deadlineDefinitionId}' has unknown start kind.");
                }

                if (deadline.expirationPolicy == QuestDeadlineExpirationPolicy.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' deadline '{deadline.deadlineDefinitionId}' has unknown expiration policy.");
                }

                if (deadline.startKind == QuestDeadlineStartKind.AbsoluteWorldTime && deadline.absoluteWorldTime < 0d)
                {
                    report.AddError($"QuestDefinition '{Title}' deadline '{deadline.deadlineDefinitionId}' requires a non-negative absolute world time.");
                }

                if (deadline.startKind != QuestDeadlineStartKind.AbsoluteWorldTime && deadline.durationFromStart < 0d)
                {
                    report.AddError($"QuestDefinition '{Title}' deadline '{deadline.deadlineDefinitionId}' requires a non-negative relative duration.");
                }
            }

            HashSet<string> failureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestFailureConditionDefinitionData failure in FailureConditions)
            {
                if (string.IsNullOrWhiteSpace(failure.failureConditionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has a failure condition without a stable failure condition ID.");
                    continue;
                }

                if (!failureIds.Add(failure.failureConditionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has duplicate failure condition ID '{failure.failureConditionId}'.");
                }

                if (failure.reasonCode == QuestFailureReasonCode.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' failure condition '{failure.failureConditionId}' has unknown reason code.");
                }

                if (failure.triggerKind == QuestFailureTriggerKind.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' failure condition '{failure.failureConditionId}' has unknown trigger kind.");
                }
            }

            HashSet<string> packageIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> rewardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestRewardPackageDefinitionData package in RewardPackages)
            {
                if (string.IsNullOrWhiteSpace(package.rewardPackageId))
                {
                    report.AddError($"QuestDefinition '{Title}' has a reward package without a stable package ID.");
                    continue;
                }

                if (!packageIds.Add(package.rewardPackageId))
                {
                    report.AddError($"QuestDefinition '{Title}' has duplicate reward package ID '{package.rewardPackageId}'.");
                }

                if (package.deliveryPolicy == QuestRewardDeliveryPolicy.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' reward package '{package.rewardPackageId}' has unknown delivery policy.");
                }

                if (package.atomicityPolicy == QuestRewardPackageAtomicityPolicy.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' reward package '{package.rewardPackageId}' has unknown atomicity policy.");
                }

                foreach (QuestRewardDefinitionData rewardDefinition in package.rewards ?? Array.Empty<QuestRewardDefinitionData>())
                {
                    if (string.IsNullOrWhiteSpace(rewardDefinition.rewardDefinitionId))
                    {
                        report.AddError($"QuestDefinition '{Title}' reward package '{package.rewardPackageId}' has a reward without a stable reward definition ID.");
                        continue;
                    }

                    if (!rewardIds.Add(rewardDefinition.rewardDefinitionId))
                    {
                        report.AddError($"QuestDefinition '{Title}' has duplicate reward definition ID '{rewardDefinition.rewardDefinitionId}'.");
                    }

                    if (rewardDefinition.category == QuestRewardCategory.Unknown)
                    {
                        report.AddError($"QuestDefinition '{Title}' reward '{rewardDefinition.rewardDefinitionId}' has unknown category.");
                    }

                    if (string.IsNullOrWhiteSpace(rewardDefinition.targetDefinitionId))
                    {
                        report.AddError($"QuestDefinition '{Title}' reward '{rewardDefinition.rewardDefinitionId}' must declare a target definition ID.");
                    }

                    if (rewardDefinition.quantity <= 0)
                    {
                        report.AddError($"QuestDefinition '{Title}' reward '{rewardDefinition.rewardDefinitionId}' has invalid quantity.");
                    }
                }
            }

            HashSet<string> consequenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestConsequenceDefinitionData consequence in ConsequenceDefinitions)
            {
                if (string.IsNullOrWhiteSpace(consequence.consequenceDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has a consequence without a stable consequence definition ID.");
                    continue;
                }

                if (!consequenceIds.Add(consequence.consequenceDefinitionId))
                {
                    report.AddError($"QuestDefinition '{Title}' has duplicate consequence definition ID '{consequence.consequenceDefinitionId}'.");
                }

                if (consequence.appliesTo == QuestTerminalOutcomeKind.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' consequence '{consequence.consequenceDefinitionId}' has unknown terminal outcome kind.");
                }

                if (consequence.category == QuestRewardCategory.Unknown)
                {
                    report.AddError($"QuestDefinition '{Title}' consequence '{consequence.consequenceDefinitionId}' has unknown category.");
                }
            }
        }

        private static QuestIssuerType[] DistinctEnums(IEnumerable<QuestIssuerType> values)
        {
            return (values ?? Array.Empty<QuestIssuerType>())
                .Where(value => value != QuestIssuerType.Unknown)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
        }

        private static QuestRecipientScope[] DistinctEnums(IEnumerable<QuestRecipientScope> values)
        {
            return (values ?? Array.Empty<QuestRecipientScope>())
                .Where(value => value != QuestRecipientScope.Unknown)
                .Distinct()
                .OrderBy(value => value)
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
