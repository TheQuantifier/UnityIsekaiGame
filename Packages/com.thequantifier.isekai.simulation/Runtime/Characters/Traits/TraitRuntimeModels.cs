using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Traits
{
    [Serializable]
    public sealed class RuntimeTraitSourceRecord
    {
        public int sourceCategory;
        public string sourceId;
        public string reason;
        public string acquiredAtUtc;
        public double acquiredAtPlaytimeSeconds;
        public bool permanentSource;
        public bool revokeOnSourceRemoval = true;
        public string authority;
    }

    [Serializable]
    public sealed class RuntimeTraitTransitionRecord
    {
        public string transitionId;
        public string traitDefinitionId;
        public int oldLifecycleState;
        public int newLifecycleState;
        public int oldDiscoveryState;
        public int newDiscoveryState;
        public string reason;
        public string changedAtUtc;
        public double changedAtPlaytimeSeconds;
    }

    [Serializable]
    public sealed class RuntimeTraitReplacementRecord
    {
        public string oldTraitDefinitionId;
        public string newTraitDefinitionId;
        public string reason;
        public string replacedAtUtc;
        public double replacedAtPlaytimeSeconds;
    }

    [Serializable]
    public sealed class RuntimeTraitRecord
    {
        public string traitDefinitionId;
        public int lifecycleState;
        public int discoveryState;
        public string ownerId;
        public string firstAcquiredAtUtc;
        public double firstAcquiredAtPlaytimeSeconds;
        public string latestLifecycleChangedAtUtc;
        public double latestLifecycleChangedAtPlaytimeSeconds;
        public string latestDiscoveryChangedAtUtc;
        public double latestDiscoveryChangedAtPlaytimeSeconds;
        public List<RuntimeTraitSourceRecord> sourceRecords = new List<RuntimeTraitSourceRecord>();
        public List<RuntimeTraitSourceRecord> suppressionSourceRecords = new List<RuntimeTraitSourceRecord>();
        public string primaryAcquisitionReason;
        public string removalSourceId;
        public string removalReason;
        public List<RuntimeTraitReplacementRecord> replacementHistory = new List<RuntimeTraitReplacementRecord>();
        public List<RuntimeTraitTransitionRecord> transitionHistory = new List<RuntimeTraitTransitionRecord>();
        public List<string> linkedGrantMetadata = new List<string>();
        public string restorationMetadata;
        public float runtimeIntensity;
    }

    [Serializable]
    public sealed class PlayerTraitsSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string personId;
        public List<RuntimeTraitRecord> traits = new List<RuntimeTraitRecord>();
    }

    public sealed class TraitGrantRequest
    {
        public string OwnerId { get; set; }
        public string TraitDefinitionId { get; set; }
        public TraitLifecycleState? RequestedLifecycle { get; set; }
        public TraitDiscoveryState? RequestedDiscovery { get; set; }
        public TraitSourceCategory SourceCategory { get; set; } = TraitSourceCategory.Development;
        public string SourceId { get; set; } = "development.test-lab";
        public string Reason { get; set; } = "Trait grant";
        public bool AllowConflictReplacement { get; set; }
        public IReadOnlyList<string> TraitsAuthorizedForReplacement { get; set; } = Array.Empty<string>();
        public bool Restoration { get; set; }
        public bool Migration { get; set; }
        public string Authority { get; set; } = "local-prototype";
        public bool PermanentSource { get; set; }
        public bool RevokeOnSourceRemoval { get; set; } = true;
    }

    public sealed class TraitOperationResult
    {
        private TraitOperationResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? "Success" : "Failure" : code;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Rejected => !Succeeded;
        public string Code { get; }
        public string Message { get; }
        public string TraitId { get; private set; }
        public TraitLifecycleState? OldLifecycle { get; private set; }
        public TraitLifecycleState? NewLifecycle { get; private set; }
        public TraitDiscoveryState? OldDiscovery { get; private set; }
        public TraitDiscoveryState? NewDiscovery { get; private set; }
        public bool SourceAdded { get; private set; }
        public bool EffectsChanged { get; private set; }
        public bool Restoration { get; private set; }
        public List<string> Conflicts { get; } = new List<string>();
        public List<string> ReplacedTraitIds { get; } = new List<string>();
        public List<string> LinkedGrantIds { get; } = new List<string>();
        public List<string> SkillGrantIds { get; } = new List<string>();

        public static TraitOperationResult Success(string message, string code = "Success")
        {
            return new TraitOperationResult(true, code, message);
        }

        public static TraitOperationResult Failure(string code, string message)
        {
            return new TraitOperationResult(false, code, message);
        }

        public TraitOperationResult WithTrait(string traitId, TraitLifecycleState? oldLifecycle, TraitLifecycleState? newLifecycle, TraitDiscoveryState? oldDiscovery, TraitDiscoveryState? newDiscovery)
        {
            TraitId = traitId;
            OldLifecycle = oldLifecycle;
            NewLifecycle = newLifecycle;
            OldDiscovery = oldDiscovery;
            NewDiscovery = newDiscovery;
            return this;
        }

        public TraitOperationResult WithSourceAdded(bool value)
        {
            SourceAdded = value;
            return this;
        }

        public TraitOperationResult WithEffectsChanged(bool value)
        {
            EffectsChanged = value;
            return this;
        }

        public TraitOperationResult WithRestoration(bool value)
        {
            Restoration = value;
            return this;
        }
    }

    public sealed class TraitSnapshot
    {
        public RuntimeTraitRecord Record { get; set; }
        public TraitDefinition Definition { get; set; }
        public bool ShowTrueName { get; set; }
        public string PresentationName { get; set; }
    }

    public static class TraitRuntimeCloner
    {
        public static RuntimeTraitRecord Clone(RuntimeTraitRecord record)
        {
            return record == null
                ? null
                : new RuntimeTraitRecord
                {
                    traitDefinitionId = record.traitDefinitionId,
                    lifecycleState = record.lifecycleState,
                    discoveryState = record.discoveryState,
                    ownerId = record.ownerId,
                    firstAcquiredAtUtc = record.firstAcquiredAtUtc,
                    firstAcquiredAtPlaytimeSeconds = record.firstAcquiredAtPlaytimeSeconds,
                    latestLifecycleChangedAtUtc = record.latestLifecycleChangedAtUtc,
                    latestLifecycleChangedAtPlaytimeSeconds = record.latestLifecycleChangedAtPlaytimeSeconds,
                    latestDiscoveryChangedAtUtc = record.latestDiscoveryChangedAtUtc,
                    latestDiscoveryChangedAtPlaytimeSeconds = record.latestDiscoveryChangedAtPlaytimeSeconds,
                    sourceRecords = record.sourceRecords == null ? new List<RuntimeTraitSourceRecord>() : record.sourceRecords.Select(Clone).ToList(),
                    suppressionSourceRecords = record.suppressionSourceRecords == null ? new List<RuntimeTraitSourceRecord>() : record.suppressionSourceRecords.Select(Clone).ToList(),
                    primaryAcquisitionReason = record.primaryAcquisitionReason,
                    removalSourceId = record.removalSourceId,
                    removalReason = record.removalReason,
                    replacementHistory = record.replacementHistory == null ? new List<RuntimeTraitReplacementRecord>() : record.replacementHistory.Select(Clone).ToList(),
                    transitionHistory = record.transitionHistory == null ? new List<RuntimeTraitTransitionRecord>() : record.transitionHistory.Select(Clone).ToList(),
                    linkedGrantMetadata = record.linkedGrantMetadata == null ? new List<string>() : new List<string>(record.linkedGrantMetadata),
                    restorationMetadata = record.restorationMetadata,
                    runtimeIntensity = record.runtimeIntensity
                };
        }

        public static RuntimeTraitSourceRecord Clone(RuntimeTraitSourceRecord record)
        {
            return record == null
                ? null
                : new RuntimeTraitSourceRecord
                {
                    sourceCategory = record.sourceCategory,
                    sourceId = record.sourceId,
                    reason = record.reason,
                    acquiredAtUtc = record.acquiredAtUtc,
                    acquiredAtPlaytimeSeconds = record.acquiredAtPlaytimeSeconds,
                    permanentSource = record.permanentSource,
                    revokeOnSourceRemoval = record.revokeOnSourceRemoval,
                    authority = record.authority
                };
        }

        private static RuntimeTraitReplacementRecord Clone(RuntimeTraitReplacementRecord record)
        {
            return record == null
                ? null
                : new RuntimeTraitReplacementRecord
                {
                    oldTraitDefinitionId = record.oldTraitDefinitionId,
                    newTraitDefinitionId = record.newTraitDefinitionId,
                    reason = record.reason,
                    replacedAtUtc = record.replacedAtUtc,
                    replacedAtPlaytimeSeconds = record.replacedAtPlaytimeSeconds
                };
        }

        private static RuntimeTraitTransitionRecord Clone(RuntimeTraitTransitionRecord record)
        {
            return record == null
                ? null
                : new RuntimeTraitTransitionRecord
                {
                    transitionId = record.transitionId,
                    traitDefinitionId = record.traitDefinitionId,
                    oldLifecycleState = record.oldLifecycleState,
                    newLifecycleState = record.newLifecycleState,
                    oldDiscoveryState = record.oldDiscoveryState,
                    newDiscoveryState = record.newDiscoveryState,
                    reason = record.reason,
                    changedAtUtc = record.changedAtUtc,
                    changedAtPlaytimeSeconds = record.changedAtPlaytimeSeconds
                };
        }
    }
}
