using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat.CombatState;

namespace UnityIsekaiGame.Combat.Contributions
{
    public readonly struct CombatContributionRecordRequest
    {
        public CombatContributionRecordRequest(
            string transactionId,
            CombatContributionType contributionType,
            string contributorActorId,
            string contributorPersonId,
            string beneficiaryActorId,
            string targetActorId,
            string encounterId,
            float requestedAmount,
            float actualAmount,
            float preventedAmount,
            float simulationTime,
            CombatContributionSourceKind sourceKind = CombatContributionSourceKind.Unknown,
            string rootTransactionId = "",
            string parentTransactionId = "",
            string originDefinitionId = "",
            string damageTypeId = "",
            IReadOnlyList<string> tags = null,
            bool preview = false,
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            ContributionType = contributionType;
            ContributorActorId = contributorActorId ?? string.Empty;
            ContributorPersonId = contributorPersonId ?? string.Empty;
            BeneficiaryActorId = beneficiaryActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            RequestedAmount = requestedAmount;
            ActualAmount = actualAmount;
            PreventedAmount = preventedAmount;
            SimulationTime = simulationTime;
            SourceKind = sourceKind;
            RootTransactionId = rootTransactionId ?? string.Empty;
            ParentTransactionId = parentTransactionId ?? string.Empty;
            OriginDefinitionId = originDefinitionId ?? string.Empty;
            DamageTypeId = damageTypeId ?? string.Empty;
            Tags = tags == null ? Array.Empty<string>() : new List<string>(tags);
            Preview = preview;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public CombatContributionType ContributionType { get; }
        public string ContributorActorId { get; }
        public string ContributorPersonId { get; }
        public string BeneficiaryActorId { get; }
        public string TargetActorId { get; }
        public string EncounterId { get; }
        public float RequestedAmount { get; }
        public float ActualAmount { get; }
        public float PreventedAmount { get; }
        public float SimulationTime { get; }
        public CombatContributionSourceKind SourceKind { get; }
        public string RootTransactionId { get; }
        public string ParentTransactionId { get; }
        public string OriginDefinitionId { get; }
        public string DamageTypeId { get; }
        public IReadOnlyList<string> Tags { get; }
        public bool Preview { get; }
        public bool AuthorityValidated { get; }
    }

    public sealed class CombatContributionRecord
    {
        public CombatContributionRecord(
            string recordId,
            CombatContributionRecordRequest request,
            float contributionWeight,
            long revision)
        {
            RecordId = recordId ?? string.Empty;
            EncounterId = request.EncounterId;
            RootTransactionId = string.IsNullOrWhiteSpace(request.RootTransactionId) ? request.TransactionId : request.RootTransactionId;
            SourceTransactionId = request.TransactionId;
            ParentTransactionId = request.ParentTransactionId;
            ContributorActorId = request.ContributorActorId;
            ContributorPersonId = request.ContributorPersonId;
            BeneficiaryActorId = request.BeneficiaryActorId;
            TargetActorId = request.TargetActorId;
            ContributionType = request.ContributionType;
            SourceKind = request.SourceKind;
            OriginDefinitionId = request.OriginDefinitionId;
            DamageTypeId = request.DamageTypeId;
            RequestedAmount = Mathf.Max(0f, request.RequestedAmount);
            ActualAmount = Mathf.Max(0f, request.ActualAmount);
            PreventedAmount = Mathf.Max(0f, request.PreventedAmount);
            SimulationTime = Mathf.Max(0f, request.SimulationTime);
            ContributionWeight = Mathf.Max(0f, contributionWeight);
            Tags = request.Tags == null ? Array.Empty<string>() : new List<string>(request.Tags);
            Revision = revision;
        }

        public string RecordId { get; }
        public string EncounterId { get; }
        public string RootTransactionId { get; }
        public string SourceTransactionId { get; }
        public string ParentTransactionId { get; }
        public string ContributorActorId { get; }
        public string ContributorPersonId { get; }
        public string BeneficiaryActorId { get; }
        public string TargetActorId { get; }
        public CombatContributionType ContributionType { get; }
        public CombatContributionSourceKind SourceKind { get; }
        public string OriginDefinitionId { get; }
        public string DamageTypeId { get; }
        public float RequestedAmount { get; }
        public float ActualAmount { get; }
        public float PreventedAmount { get; }
        public float SimulationTime { get; }
        public float ContributionWeight { get; }
        public IReadOnlyList<string> Tags { get; }
        public long Revision { get; }

        public bool IsHostileDamage => ContributionType == CombatContributionType.DamageApplied
            || ContributionType == CombatContributionType.OngoingDamageApplied
            || ContributionType == CombatContributionType.ReactionDamageApplied;

        public bool IsHealingSupport => ContributionType == CombatContributionType.HealingApplied
            || ContributionType == CombatContributionType.OngoingHealingApplied
            || ContributionType == CombatContributionType.ReactionHealingApplied
            || ContributionType == CombatContributionType.RecoveryProvided
            || ContributionType == CombatContributionType.RevivalProvided;

        public bool IsDefensiveSupport => ContributionType == CombatContributionType.DamagePrevented
            || ContributionType == CombatContributionType.SuccessfulBlock
            || ContributionType == CombatContributionType.SuccessfulParry
            || ContributionType == CombatContributionType.SuccessfulDodge;
    }

    public sealed class CombatContributionRecordResult
    {
        private CombatContributionRecordResult(bool succeeded, bool preview, bool duplicate, string code, string message, CombatContributionRecord record, long revisionBefore, long revisionAfter)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatContributionResultCode.Success : CombatContributionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Record = record;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public CombatContributionRecord Record { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }

        public static CombatContributionRecordResult Success(bool preview, bool duplicate, string message, CombatContributionRecord record, long before, long after)
        {
            return new CombatContributionRecordResult(true, preview, duplicate, preview ? CombatContributionResultCode.Preview : CombatContributionResultCode.Success, message, record, before, after);
        }

        public static CombatContributionRecordResult Failure(bool preview, string code, string message, long before, long after)
        {
            return new CombatContributionRecordResult(false, preview, false, code, message, null, before, after);
        }
    }

    public sealed class CombatContributorSummary
    {
        public CombatContributorSummary(
            string contributorActorId,
            string contributorPersonId,
            float totalDamage,
            float totalHealing,
            float totalPrevented,
            int successfulBlocks,
            int successfulParries,
            int successfulDodges,
            int recoveries,
            int revivals,
            float firstContributionTime,
            float lastContributionTime,
            IReadOnlyList<string> recordIds,
            IReadOnlyList<CombatRewardEligibilityCategory> eligibility)
        {
            ContributorActorId = contributorActorId ?? string.Empty;
            ContributorPersonId = contributorPersonId ?? string.Empty;
            TotalActualDamage = Mathf.Max(0f, totalDamage);
            TotalEffectiveHealing = Mathf.Max(0f, totalHealing);
            TotalDamagePrevented = Mathf.Max(0f, totalPrevented);
            SuccessfulBlocks = successfulBlocks;
            SuccessfulParries = successfulParries;
            SuccessfulDodges = successfulDodges;
            Recoveries = recoveries;
            Revivals = revivals;
            FirstContributionTime = firstContributionTime;
            LastContributionTime = lastContributionTime;
            RecordIds = recordIds == null ? Array.Empty<string>() : new List<string>(recordIds);
            Eligibility = eligibility == null ? Array.Empty<CombatRewardEligibilityCategory>() : new List<CombatRewardEligibilityCategory>(eligibility);
        }

        public string ContributorActorId { get; }
        public string ContributorPersonId { get; }
        public float TotalActualDamage { get; }
        public float TotalEffectiveHealing { get; }
        public float TotalDamagePrevented { get; }
        public int SuccessfulBlocks { get; }
        public int SuccessfulParries { get; }
        public int SuccessfulDodges { get; }
        public int Recoveries { get; }
        public int Revivals { get; }
        public float FirstContributionTime { get; }
        public float LastContributionTime { get; }
        public IReadOnlyList<string> RecordIds { get; }
        public IReadOnlyList<CombatRewardEligibilityCategory> Eligibility { get; }
        public bool HasHostileDamage => TotalActualDamage > 0f;
        public bool HasSupport => TotalEffectiveHealing > 0f || TotalDamagePrevented > 0f || Recoveries > 0 || Revivals > 0 || SuccessfulBlocks > 0 || SuccessfulParries > 0 || SuccessfulDodges > 0;
    }

    public sealed class CombatCreditResolutionResult
    {
        public CombatCreditResolutionResult(
            bool succeeded,
            bool duplicate,
            string code,
            string message,
            string creditTransactionId,
            string encounterId,
            string targetActorId,
            string lifecycleTransactionId,
            string policyId,
            CombatCreditType creditType,
            string primaryContributorActorId,
            IReadOnlyList<CombatContributorSummary> contributors,
            IReadOnlyList<CombatContributorSummary> assists,
            IReadOnlyDictionary<string, string> disqualifiedReasons,
            float resolvedAt)
        {
            Succeeded = succeeded;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatContributionResultCode.Success : CombatContributionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            CreditTransactionId = creditTransactionId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            LifecycleTransactionId = lifecycleTransactionId ?? string.Empty;
            PolicyId = policyId ?? string.Empty;
            CreditType = creditType;
            PrimaryContributorActorId = primaryContributorActorId ?? string.Empty;
            Contributors = contributors == null ? Array.Empty<CombatContributorSummary>() : new List<CombatContributorSummary>(contributors);
            Assists = assists == null ? Array.Empty<CombatContributorSummary>() : new List<CombatContributorSummary>(assists);
            DisqualifiedReasons = disqualifiedReasons == null ? new Dictionary<string, string>() : new Dictionary<string, string>(disqualifiedReasons, StringComparer.Ordinal);
            ResolvedAt = resolvedAt;
        }

        public bool Succeeded { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string CreditTransactionId { get; }
        public string EncounterId { get; }
        public string TargetActorId { get; }
        public string LifecycleTransactionId { get; }
        public string PolicyId { get; }
        public CombatCreditType CreditType { get; }
        public string PrimaryContributorActorId { get; }
        public IReadOnlyList<CombatContributorSummary> Contributors { get; }
        public IReadOnlyList<CombatContributorSummary> Assists { get; }
        public IReadOnlyDictionary<string, string> DisqualifiedReasons { get; }
        public float ResolvedAt { get; }
        public bool GrantsConcreteRewards => false;
    }

    public sealed class CombatContributionLedgerSnapshot
    {
        public CombatContributionLedgerSnapshot(string ledgerId, string encounterId, string targetActorId, bool finalized, long revision, IReadOnlyList<CombatContributionRecord> records, IReadOnlyList<CombatContributorSummary> summaries, IReadOnlyList<string> activeParticipantIds = null)
        {
            LedgerId = ledgerId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            Finalized = finalized;
            Revision = revision;
            Records = records == null ? Array.Empty<CombatContributionRecord>() : new List<CombatContributionRecord>(records);
            Summaries = summaries == null ? Array.Empty<CombatContributorSummary>() : new List<CombatContributorSummary>(summaries);
            ActiveParticipantIds = activeParticipantIds == null ? Array.Empty<string>() : new List<string>(activeParticipantIds);
        }

        public string LedgerId { get; }
        public string EncounterId { get; }
        public string TargetActorId { get; }
        public bool Finalized { get; }
        public long Revision { get; }
        public IReadOnlyList<CombatContributionRecord> Records { get; }
        public IReadOnlyList<CombatContributorSummary> Summaries { get; }
        public IReadOnlyList<string> ActiveParticipantIds { get; }
    }

    public sealed class CombatContributionLedgerMergeResult
    {
        public CombatContributionLedgerMergeResult(bool succeeded, string code, string message, string survivingEncounterId, IReadOnlyList<string> mergedLedgerIds, CombatContributionLedgerSnapshot snapshot, long revisionBefore, long revisionAfter)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatContributionResultCode.Success : CombatContributionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            SurvivingEncounterId = survivingEncounterId ?? string.Empty;
            MergedLedgerIds = mergedLedgerIds == null ? Array.Empty<string>() : new List<string>(mergedLedgerIds);
            Snapshot = snapshot;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
        public string SurvivingEncounterId { get; }
        public IReadOnlyList<string> MergedLedgerIds { get; }
        public CombatContributionLedgerSnapshot Snapshot { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
    }

    public sealed class CombatContributionLedgerPartitionResult
    {
        public CombatContributionLedgerPartitionResult(bool succeeded, string code, string message, string originalEncounterId, IReadOnlyList<CombatContributionLedgerSnapshot> componentSnapshots, IReadOnlyList<CombatContributionLedgerSnapshot> historicalSnapshots, long revisionBefore, long revisionAfter)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatContributionResultCode.Success : CombatContributionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            OriginalEncounterId = originalEncounterId ?? string.Empty;
            ComponentSnapshots = componentSnapshots == null ? Array.Empty<CombatContributionLedgerSnapshot>() : new List<CombatContributionLedgerSnapshot>(componentSnapshots);
            HistoricalSnapshots = historicalSnapshots == null ? Array.Empty<CombatContributionLedgerSnapshot>() : new List<CombatContributionLedgerSnapshot>(historicalSnapshots);
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
        public string OriginalEncounterId { get; }
        public IReadOnlyList<CombatContributionLedgerSnapshot> ComponentSnapshots { get; }
        public IReadOnlyList<CombatContributionLedgerSnapshot> HistoricalSnapshots { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
    }

    public static class CombatContributionResultCode
    {
        public const string Success = "Success";
        public const string Preview = "Preview";
        public const string InvalidRequest = "InvalidRequest";
        public const string DuplicateContribution = "DuplicateContribution";
        public const string ZeroEffectiveContribution = "ZeroEffectiveContribution";
        public const string MissingContributor = "MissingContributor";
        public const string MissingTarget = "MissingTarget";
        public const string LedgerFinalized = "LedgerFinalized";
        public const string CreditAlreadyResolved = "CreditAlreadyResolved";
        public const string MissingPolicy = "MissingPolicy";
        public const string ExpiredContribution = "ExpiredContribution";
        public const string NoEligibleContributor = "NoEligibleContributor";
        public const string NoLedgerChanged = "NoLedgerChanged";
    }
}
