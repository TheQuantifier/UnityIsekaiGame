using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Progression
{
    [Serializable]
    public sealed class RuntimePermanentAttributeGrantRecord
    {
        public string sourceId;
        public string definitionId;
        public string attributeId;
        public float amount;
        public bool applied;
    }

    [Serializable]
    public sealed class RuntimeOriginAssignmentRecord
    {
        public bool assigned;
        public string originFamilyId;
        public string originId;
        public int randomSeed;
        public long startingGoldAmount;
        public bool originStatGrantsApplied;
        public bool startingCurrencyApplied;
        public string assignedAtUtc;
        public double assignedAtPlaytimeSeconds;
        public string assignmentSource;
    }

    [Serializable]
    public sealed class RuntimeBirthGiftRecord
    {
        public string giftDefinitionId;
        public string rarityId;
        public BirthGiftType giftType;
        public string originFamilyId;
        public string originId;
        public BirthGiftAwakeningMode awakeningMode;
        public float requiredActivePlaytimeSeconds;
        public float currentProgressSeconds;
        public BirthGiftRuntimeState state;
        public bool rewardApplied;
        public string assignedAtUtc;
        public double assignedAtPlaytimeSeconds;
        public string awakenedAtUtc;
        public double awakenedAtPlaytimeSeconds;
        public string assignmentSource;
    }

    [Serializable]
    public sealed class RuntimeRoleRecord
    {
        public string recordId;
        public string roleDefinitionId;
        public RoleLifecycleState lifecycleState;
        public string acquisitionSource;
        public string context;
        public string grantingFactionId;
        public string grantingPersonId;
        public string grantingOrganizationId;
        public string acquisitionReason;
        public string exitReason;
        public string startedAtUtc;
        public double startedAtPlaytimeSeconds;
        public string endedAtUtc;
        public double endedAtPlaytimeSeconds;
        public bool primary;
        public bool activeEffectsApplied;
    }

    [Serializable]
    public sealed class RuntimeSocialStatusRecord
    {
        public string recordId;
        public string socialStatusDefinitionId;
        public SocialStatusLifecycleState lifecycleState;
        public string source;
        public SocialStatusContextKind contextKind;
        public string contextTargetId;
        public string acquisitionReason;
        public string resolutionReason;
        public string startedAtUtc;
        public double startedAtPlaytimeSeconds;
        public string endedAtUtc;
        public double endedAtPlaytimeSeconds;
        public bool activeEffectsApplied;
    }

    [Serializable]
    public sealed class RuntimeTitleRecord
    {
        public string titleDefinitionId;
        public string source;
        public string assignedAtUtc;
        public double assignedAtPlaytimeSeconds;
        public bool active = true;
    }

    [Serializable]
    public sealed class ActivityOutcomeRecord
    {
        public string activityId;
        public ActivityType activityType;
        public string sourceId;
        public string playerId;
        public float difficulty;
        public string acceptedAtUtc;
        public string completedAtUtc;
        public ActivityOutcome outcome;
        public float contribution = 1f;
        public string sourceSystem;
        public bool serverAuthoritative;
    }

    [Serializable]
    public sealed class ParticipationRecord
    {
        public string participationId;
        public string sourceId;
        public string playerId;
        public string sourceSystem;
        public float contribution = 1f;
        public string recordedAtUtc;
    }

    public readonly struct ProgressionOperationResult
    {
        private ProgressionOperationResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? (succeeded ? "Success" : "Failed") : code;
            Message = string.IsNullOrWhiteSpace(message) ? Code : message;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }

        public static ProgressionOperationResult Success(string message)
        {
            return new ProgressionOperationResult(true, "Success", message);
        }

        public static ProgressionOperationResult Failure(string code, string message)
        {
            return new ProgressionOperationResult(false, code, message);
        }
    }

    public sealed class RoleConflictResult
    {
        public RoleConflictResult(RoleDefinition requestedRole, IReadOnlyList<RuntimeRoleRecord> blockers)
        {
            RequestedRole = requestedRole;
            Blockers = blockers ?? Array.Empty<RuntimeRoleRecord>();
        }

        public RoleDefinition RequestedRole { get; }
        public IReadOnlyList<RuntimeRoleRecord> Blockers { get; }
        public bool HasConflict => Blockers.Count > 0;
    }

    public readonly struct RoleAcquisitionResult
    {
        private RoleAcquisitionResult(bool succeeded, string code, string message, RuntimeRoleRecord record, RoleConflictResult conflict)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? (succeeded ? "Success" : "Failed") : code;
            Message = string.IsNullOrWhiteSpace(message) ? Code : message;
            Record = record;
            Conflict = conflict;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
        public RuntimeRoleRecord Record { get; }
        public RoleConflictResult Conflict { get; }

        public static RoleAcquisitionResult Success(RuntimeRoleRecord record, string message)
        {
            return new RoleAcquisitionResult(true, "Success", message, record, null);
        }

        public static RoleAcquisitionResult Failure(string code, string message)
        {
            return new RoleAcquisitionResult(false, code, message, null, null);
        }

        public static RoleAcquisitionResult ConflictDetected(RoleConflictResult conflict, string message)
        {
            return new RoleAcquisitionResult(false, "RoleConflict", message, null, conflict);
        }
    }

    public readonly struct OverallLevelBreakdown
    {
        public OverallLevelBreakdown(
            float rawTotalScore,
            int overallLevel,
            float normalizedActivityScore,
            float normalizedStatScore,
            float successComponent,
            float playtimeComponent,
            float accountAgeComponent,
            float participationComponent)
        {
            RawTotalScore = rawTotalScore;
            OverallLevel = overallLevel;
            NormalizedActivityScore = normalizedActivityScore;
            NormalizedStatScore = normalizedStatScore;
            SuccessComponent = successComponent;
            PlaytimeComponent = playtimeComponent;
            AccountAgeComponent = accountAgeComponent;
            ParticipationComponent = participationComponent;
        }

        public float RawTotalScore { get; }
        public int OverallLevel { get; }
        public float NormalizedActivityScore { get; }
        public float NormalizedStatScore { get; }
        public float SuccessComponent { get; }
        public float PlaytimeComponent { get; }
        public float AccountAgeComponent { get; }
        public float ParticipationComponent { get; }
    }
}
