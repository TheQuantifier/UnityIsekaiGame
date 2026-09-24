using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Durability;

namespace UnityIsekaiGame.Inventory.Disassembly
{
    public enum ItemRecoveryOperationKind { Disassemble, Salvage, NaturalDecomposition }
    public enum DisassemblyOperationState { Prepared, Executing, Completed, Failed }
    public enum DisassemblyOperationStatus { Preview, Succeeded, InvalidRequest, MissingItem, MissingComposition, Ineligible, OutputCreationFailed, AtomicCommitFailed, RestoreFailed }
    public enum DisassemblyEfficiencyTier { Ruined, Poor, Standard, Skilled, Excellent, Masterful }

    [Serializable]
    public sealed class DisassemblyRequest
    {
        public string operationId;
        public string itemInstanceId;
        public string actorPersonId;
        public string ownerPersonId;
        public string worldTime;
        public string deterministicSeed;
        public ItemRecoveryOperationKind operationKind = ItemRecoveryOperationKind.Disassemble;
        public string skillId = "skill.disassembly";
        public int skillGrade;
        public bool skillUsed;
        public int itemLevel;
        public bool preview;
        public bool materializeOutputIdentities = true;

        public DisassemblyRequest Clone() => (DisassemblyRequest)MemberwiseClone();
    }

    [Serializable]
    public sealed class DisassemblyComponentOutcomeData
    {
        public string outcomeId;
        public string componentEntryId;
        public string materialEntryId;
        public string sourceItemDefinitionId;
        public string materialDefinitionId;
        public float sourceQuantity;
        public int itemRarityRank;
        public int componentRarityRank;
        public float componentCondition;
        public float recoveryChance;
        public float recoveryRoll;
        public float quantityEfficiency;
        public int quantityMultiplier = 1;
        public int returnedQuantity;
        public string[] outputItemInstanceIds = Array.Empty<string>();

        public bool Recovered => returnedQuantity > 0;
        public DisassemblyComponentOutcomeData Clone() => new DisassemblyComponentOutcomeData
        {
            outcomeId = outcomeId ?? string.Empty,
            componentEntryId = componentEntryId ?? string.Empty,
            materialEntryId = materialEntryId ?? string.Empty,
            sourceItemDefinitionId = sourceItemDefinitionId ?? string.Empty,
            materialDefinitionId = materialDefinitionId ?? string.Empty,
            sourceQuantity = sourceQuantity,
            itemRarityRank = itemRarityRank,
            componentRarityRank = componentRarityRank,
            componentCondition = componentCondition,
            recoveryChance = recoveryChance,
            recoveryRoll = recoveryRoll,
            quantityEfficiency = quantityEfficiency,
            quantityMultiplier = quantityMultiplier,
            returnedQuantity = returnedQuantity,
            outputItemInstanceIds = (outputItemInstanceIds ?? Array.Empty<string>()).ToArray()
        };
    }

    [Serializable]
    public sealed class DisassemblyOperationRecordData
    {
        public string operationId;
        public string itemInstanceId;
        public string itemDefinitionId;
        public string actorPersonId;
        public string worldTime;
        public string deterministicSeed;
        public ItemRecoveryOperationKind operationKind;
        public string skillId;
        public int skillGrade;
        public bool skillUsed;
        public int itemLevel;
        public int itemRarityRank;
        public float itemQuality;
        public float itemCondition;
        public float expectedEfficiency;
        public float actualEfficiency;
        public DisassemblyEfficiencyTier efficiencyTier;
        public DisassemblyOperationState state;
        public DisassemblyOperationStatus status;
        public List<DisassemblyComponentOutcomeData> outcomes = new List<DisassemblyComponentOutcomeData>();
        public string[] diagnostics = Array.Empty<string>();
        public long revision = 1L;

        public DisassemblyOperationRecordData Clone() => new DisassemblyOperationRecordData
        {
            operationId = operationId ?? string.Empty,
            itemInstanceId = itemInstanceId ?? string.Empty,
            itemDefinitionId = itemDefinitionId ?? string.Empty,
            actorPersonId = actorPersonId ?? string.Empty,
            worldTime = worldTime ?? string.Empty,
            deterministicSeed = deterministicSeed ?? string.Empty,
            operationKind = operationKind,
            skillId = skillId ?? string.Empty,
            skillGrade = skillGrade,
            skillUsed = skillUsed,
            itemLevel = itemLevel,
            itemRarityRank = itemRarityRank,
            itemQuality = itemQuality,
            itemCondition = itemCondition,
            expectedEfficiency = expectedEfficiency,
            actualEfficiency = actualEfficiency,
            efficiencyTier = efficiencyTier,
            state = state,
            status = status,
            outcomes = outcomes == null ? new List<DisassemblyComponentOutcomeData>() : outcomes.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
            diagnostics = (diagnostics ?? Array.Empty<string>()).ToArray(),
            revision = revision
        };
    }

    [Serializable]
    public sealed class DisassemblyRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<DisassemblyOperationRecordData> operations = new List<DisassemblyOperationRecordData>();
        public List<NaturalDecompositionRecordData> naturalDecompositions = new List<NaturalDecompositionRecordData>();
        public DisassemblyRuntimeSaveData Clone() => new DisassemblyRuntimeSaveData
        {
            schemaVersion = schemaVersion,
            revision = revision,
            operations = operations == null ? new List<DisassemblyOperationRecordData>() : operations.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
            naturalDecompositions = naturalDecompositions == null ? new List<NaturalDecompositionRecordData>() : naturalDecompositions.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList()
        };
    }

    public enum NaturalDecompositionRate { Slow, Fast, Immediate }
    public enum NaturalDecompositionState { Scheduled, Completed, Cancelled }

    [Serializable]
    public sealed class NaturalDecompositionSettings
    {
        public float fastThreshold = ItemDegradationPolicyDefinition.StandardFastThreshold;
        public float immediateThreshold = ItemDegradationPolicyDefinition.StandardImmediateThreshold;
        public double slowDurationSeconds = ItemDegradationPolicyDefinition.StandardSlowDurationSeconds;
        public double fastDurationSeconds = ItemDegradationPolicyDefinition.StandardFastDurationSeconds;

        public NaturalDecompositionSettings Clone() => (NaturalDecompositionSettings)MemberwiseClone();
    }

    [Serializable]
    public sealed class NaturalDecompositionRecordData
    {
        public string scheduleId;
        public string itemInstanceId;
        public string sourceWorldEntityId;
        public string sceneKey;
        public string operationId;
        public NaturalDecompositionRate rate;
        public NaturalDecompositionState state;
        public float lastKnownCondition;
        public double scheduledAtWorldSeconds;
        public double dueAtWorldSeconds;
        public double completedAtWorldSeconds;
        public long revision = 1L;

        public NaturalDecompositionRecordData Clone() => (NaturalDecompositionRecordData)MemberwiseClone();
    }

    public sealed class DisassemblyResult
    {
        private DisassemblyResult(bool succeeded, bool preview, bool duplicate, DisassemblyOperationStatus status, string message, DisassemblyOperationRecordData operation)
        {
            Succeeded = succeeded; Preview = preview; Duplicate = duplicate; Status = status; Message = message ?? string.Empty; Operation = operation?.Clone();
        }
        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public DisassemblyOperationStatus Status { get; }
        public string Message { get; }
        public DisassemblyOperationRecordData Operation { get; }
        public static DisassemblyResult Success(DisassemblyOperationRecordData operation, string message, bool preview = false, bool duplicate = false) => new DisassemblyResult(true, preview, duplicate, preview ? DisassemblyOperationStatus.Preview : DisassemblyOperationStatus.Succeeded, message, operation);
        public static DisassemblyResult Failure(DisassemblyOperationStatus status, string message, DisassemblyOperationRecordData operation = null) => new DisassemblyResult(false, false, false, status, message, operation);
    }
}
