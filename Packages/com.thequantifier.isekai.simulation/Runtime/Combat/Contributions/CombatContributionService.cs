using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Reactions;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat.Contributions
{
    [DisallowMultipleComponent]
    public sealed class CombatContributionService : MonoBehaviour
    {
        [SerializeField] private CombatContributionPolicyDefinition defaultPolicy;

        private readonly Dictionary<string, CombatContributionLedger> ledgersById = new Dictionary<string, CombatContributionLedger>(StringComparer.Ordinal);
        private readonly Dictionary<string, CombatCreditResolutionResult> creditResultsByKey = new Dictionary<string, CombatCreditResolutionResult>(StringComparer.Ordinal);
        private readonly HashSet<string> processedContributionKeys = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private float simulationTime;

        public event Action<CombatContributionRecordResult> ContributionRecorded;
        public event Action<CombatCreditResolutionResult> CreditResolved;
        public event Action<CombatContributionLedgerSnapshot> LedgerFinalized;
        public event Action<CombatContributionLedgerMergeResult> LedgersMerged;
        public event Action<CombatContributionLedgerPartitionResult> LedgersPartitioned;

        public CombatContributionPolicyDefinition DefaultPolicy => defaultPolicy;
        public float SimulationTime => simulationTime;
        public long Revision => revision;

        public void Configure(CombatContributionPolicyDefinition policy)
        {
            defaultPolicy = policy == null ? defaultPolicy : policy;
        }

        public void SetClock(float now)
        {
            if (!IsFinite(now) || now < 0f)
            {
                return;
            }

            simulationTime = now;
        }

        public void AdvanceClock(float deltaSeconds)
        {
            if (!IsFinite(deltaSeconds) || deltaSeconds < 0f)
            {
                return;
            }

            simulationTime += deltaSeconds;
        }

        public void ClearTransientStateForRestore()
        {
            ledgersById.Clear();
            creditResultsByKey.Clear();
            processedContributionKeys.Clear();
            revision = 0L;
            simulationTime = 0f;
        }

        public CombatContributionRecordResult PreviewContribution(CombatContributionRecordRequest request)
        {
            return RecordContributionInternal(request, execute: false);
        }

        public CombatContributionRecordResult RecordContribution(CombatContributionRecordRequest request)
        {
            return RecordContributionInternal(request, execute: true);
        }

        public CombatContributionRecordResult RecordDamage(DamageApplicationResult result, string encounterId = "", CombatContributionSourceKind sourceKind = CombatContributionSourceKind.Direct, string rootTransactionId = "", string parentTransactionId = "")
        {
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded)
            {
                return CombatContributionRecordResult.Failure(false, CombatContributionResultCode.InvalidRequest, "Damage result is not a committed contribution source.", revision, revision);
            }

            CombatContributionType type = sourceKind == CombatContributionSourceKind.OngoingEffect
                ? CombatContributionType.OngoingDamageApplied
                : sourceKind == CombatContributionSourceKind.Reaction
                    ? CombatContributionType.ReactionDamageApplied
                    : CombatContributionType.DamageApplied;
            return RecordContribution(new CombatContributionRecordRequest(
                result.Request.TransactionId,
                type,
                result.Request.SourceActorId,
                ResolvePersonId(result.Request.SourceObject),
                string.Empty,
                result.ResolvedTargetActorId,
                encounterId,
                result.RequestedAmount,
                result.HealthChanged ? result.FinalDamageAmount : 0f,
                result.DefenseMitigatedAmount + result.ResistanceMitigatedAmount,
                simulationTime,
                sourceKind,
                rootTransactionId,
                parentTransactionId,
                result.Request.Reason,
                result.Request.DamageType == null ? string.Empty : result.Request.DamageType.Id,
                authorityValidated: result.Request.AuthorityValidated));
        }

        public CombatContributionRecordResult RecordHealing(HealingApplicationResult result, string encounterId = "", CombatContributionSourceKind sourceKind = CombatContributionSourceKind.Direct, string rootTransactionId = "", string parentTransactionId = "")
        {
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded)
            {
                return CombatContributionRecordResult.Failure(false, CombatContributionResultCode.InvalidRequest, "Healing result is not a committed contribution source.", revision, revision);
            }

            CombatContributionType type = sourceKind == CombatContributionSourceKind.OngoingEffect
                ? CombatContributionType.OngoingHealingApplied
                : sourceKind == CombatContributionSourceKind.Reaction
                    ? CombatContributionType.ReactionHealingApplied
                    : CombatContributionType.HealingApplied;
            return RecordContribution(new CombatContributionRecordRequest(
                result.Request.TransactionId,
                type,
                result.Request.SourceActorId,
                ResolvePersonId(result.Request.SourceObject),
                result.ResolvedTargetActorId,
                string.Empty,
                encounterId,
                result.RequestedAmount,
                result.HealthChanged ? result.FinalHealingAmount : 0f,
                0f,
                simulationTime,
                sourceKind,
                rootTransactionId,
                parentTransactionId,
                result.Request.Reason,
                string.Empty,
                authorityValidated: result.Request.AuthorityValidated));
        }

        public CombatContributionRecordResult RecordDefense(DefenseResolutionResult result, string encounterId = "")
        {
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded || !result.DefenseSucceeded)
            {
                return CombatContributionRecordResult.Failure(false, CombatContributionResultCode.InvalidRequest, "Defense result is not a committed successful contribution source.", revision, revision);
            }

            CombatContributionType type = result.DefensiveActionType == DefensiveActionType.Block
                ? CombatContributionType.SuccessfulBlock
                : result.DefensiveActionType == DefensiveActionType.Parry
                    ? CombatContributionType.SuccessfulParry
                    : result.DefensiveActionType == DefensiveActionType.Dodge
                        ? CombatContributionType.SuccessfulDodge
                        : CombatContributionType.DamagePrevented;
            return RecordContribution(new CombatContributionRecordRequest(
                result.Request.TransactionId,
                type,
                result.Request.DefenderActorId,
                ResolvePersonId(result.Request.DefenderObject),
                result.Request.DefenderActorId,
                result.Request.AttackerActorId,
                encounterId,
                result.Request.IncomingDamage,
                type == CombatContributionType.DamagePrevented ? result.PreventedDamage : 1f,
                result.PreventedDamage,
                simulationTime,
                CombatContributionSourceKind.Defense,
                result.Request.ParentAttackTransactionId,
                result.Request.ParentAttackTransactionId,
                result.DefensiveActionId,
                result.Request.DamageType == null ? string.Empty : result.Request.DamageType.Id,
                authorityValidated: result.Request.AuthorityValidated));
        }

        public CombatContributionRecordResult RecordReaction(CombatReactionExecutionResult result, string encounterId = "")
        {
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded)
            {
                return CombatContributionRecordResult.Failure(false, CombatContributionResultCode.InvalidRequest, "Reaction result is not a committed contribution source.", revision, revision);
            }

            if (result.DamageResult != null)
            {
                return RecordDamage(result.DamageResult, encounterId, CombatContributionSourceKind.Reaction, result.Context == null ? string.Empty : result.Context.RootTransactionId, result.TransactionId);
            }

            if (result.HealingResult != null)
            {
                return RecordHealing(result.HealingResult, encounterId, CombatContributionSourceKind.Reaction, result.Context == null ? string.Empty : result.Context.RootTransactionId, result.TransactionId);
            }

            return CombatContributionRecordResult.Failure(false, CombatContributionResultCode.ZeroEffectiveContribution, "Reaction produced no contribution-owned damage or healing result.", revision, revision);
        }

        public CombatCreditResolutionResult ResolveDefeatCredit(ActorLifecycleResult lifecycleResult, string encounterId = "", string transactionId = "")
        {
            return ResolveLifecycleCredit(lifecycleResult, CombatCreditType.Defeat, encounterId, transactionId);
        }

        public CombatCreditResolutionResult ResolveKillCredit(ActorLifecycleResult lifecycleResult, string encounterId = "", string transactionId = "")
        {
            return ResolveLifecycleCredit(lifecycleResult, CombatCreditType.Kill, encounterId, transactionId);
        }

        public CombatContributionLedgerSnapshot FinalizeLedger(string ledgerId)
        {
            if (!ledgersById.TryGetValue(ledgerId ?? string.Empty, out CombatContributionLedger ledger))
            {
                return null;
            }

            ledger.Finalized = true;
            revision++;
            CombatContributionLedgerSnapshot snapshot = BuildSnapshot(ledger);
            LedgerFinalized?.Invoke(snapshot);
            return snapshot;
        }

        public IReadOnlyList<CombatContributionLedgerSnapshot> GetLedgerSnapshots()
        {
            return ledgersById.Values.OrderBy(ledger => ledger.LedgerId, StringComparer.Ordinal).Select(BuildSnapshot).ToList();
        }

        public CombatContributionLedgerSnapshot GetLedgerSnapshot(string ledgerId)
        {
            return ledgersById.TryGetValue(ledgerId ?? string.Empty, out CombatContributionLedger ledger)
                ? BuildSnapshot(ledger)
                : null;
        }

        public CombatContributionLedgerMergeResult MergeEncounterLedgers(CombatEncounterSnapshot survivingEncounter)
        {
            long before = revision;
            if (survivingEncounter == null || string.IsNullOrWhiteSpace(survivingEncounter.EncounterId))
            {
                return new CombatContributionLedgerMergeResult(false, CombatContributionResultCode.MissingTarget, "Surviving encounter snapshot is missing.", string.Empty, null, null, before, revision);
            }

            HashSet<string> participants = new HashSet<string>(survivingEncounter.ParticipantIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<CombatContributionLedger> candidates = ledgersById.Values
                .Where(ledger => string.Equals(ledger.EncounterId, survivingEncounter.EncounterId, StringComparison.Ordinal)
                    || ledger.Records.Any(record => RecordTouchesAny(record, participants)))
                .OrderBy(ledger => ledger.LedgerId, StringComparer.Ordinal)
                .ToList();

            if (candidates.Count == 0)
            {
                CombatContributionLedger empty = EnsureEncounterLedger(survivingEncounter.EncounterId);
                empty.SetActiveParticipants(participants);
                CombatContributionLedgerMergeResult unchanged = new CombatContributionLedgerMergeResult(true, CombatContributionResultCode.NoLedgerChanged, "No contribution ledger required merging.", survivingEncounter.EncounterId, Array.Empty<string>(), BuildSnapshot(empty), before, revision);
                LedgersMerged?.Invoke(unchanged);
                return unchanged;
            }

            CombatContributionLedger survivor = EnsureEncounterLedger(survivingEncounter.EncounterId);
            survivor.SetActiveParticipants(participants);
            Dictionary<string, CombatContributionRecord> unique = new Dictionary<string, CombatContributionRecord>(StringComparer.Ordinal);
            foreach (CombatContributionLedger candidate in candidates.Prepend(survivor).OrderBy(ledger => ledger.LedgerId, StringComparer.Ordinal))
            {
                foreach (CombatContributionRecord record in candidate.Records.OrderBy(record => record.RecordId, StringComparer.Ordinal))
                {
                    unique[record.RecordId] = record;
                }
            }

            survivor.ReplaceRecords(unique.Values.OrderBy(record => record.SimulationTime).ThenBy(record => record.RecordId, StringComparer.Ordinal));
            List<string> mergedLedgerIds = candidates
                .Where(ledger => !ReferenceEquals(ledger, survivor))
                .Select(ledger => ledger.LedgerId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            foreach (string ledgerId in mergedLedgerIds)
            {
                ledgersById.Remove(ledgerId);
            }

            if (mergedLedgerIds.Count > 0)
            {
                revision++;
            }

            CombatContributionLedgerMergeResult result = new CombatContributionLedgerMergeResult(true, CombatContributionResultCode.Success, "Contribution ledgers merged.", survivingEncounter.EncounterId, mergedLedgerIds, BuildSnapshot(survivor), before, revision);
            LedgersMerged?.Invoke(result);
            return result;
        }

        public CombatContributionLedgerPartitionResult PartitionEncounterLedgers(CombatEncounterSplitResult splitResult)
        {
            long before = revision;
            if (splitResult == null || splitResult.Preview || splitResult.Duplicate || !splitResult.Succeeded || !splitResult.SplitOccurred)
            {
                return new CombatContributionLedgerPartitionResult(false, CombatContributionResultCode.InvalidRequest, "Committed encounter split result is required.", splitResult == null ? string.Empty : splitResult.OriginalEncounterId, null, null, before, revision);
            }

            List<CombatContributionLedger> sourceLedgers = ledgersById.Values
                .Where(ledger => string.Equals(ledger.EncounterId, splitResult.OriginalEncounterId, StringComparison.Ordinal)
                    || ledger.Records.Any(record => splitResult.PreviousParticipantIds.Any(actorId => RecordTouchesActor(record, actorId))))
                .OrderBy(ledger => ledger.LedgerId, StringComparer.Ordinal)
                .ToList();
            if (sourceLedgers.Count == 0)
            {
                CombatContributionLedgerPartitionResult unchanged = new CombatContributionLedgerPartitionResult(true, CombatContributionResultCode.NoLedgerChanged, "No contribution ledger required partitioning.", splitResult.OriginalEncounterId, Array.Empty<CombatContributionLedgerSnapshot>(), Array.Empty<CombatContributionLedgerSnapshot>(), before, revision);
                LedgersPartitioned?.Invoke(unchanged);
                return unchanged;
            }

            List<CombatContributionRecord> historical = sourceLedgers
                .SelectMany(ledger => ledger.Records)
                .GroupBy(record => record.RecordId, StringComparer.Ordinal)
                .Select(group => group.OrderBy(record => record.SimulationTime).First())
                .OrderBy(record => record.SimulationTime)
                .ThenBy(record => record.RecordId, StringComparer.Ordinal)
                .ToList();
            CombatContributionLedger historicalLedger = new CombatContributionLedger(ResolveLedgerId(splitResult.OriginalEncounterId, string.Empty, string.Empty), splitResult.OriginalEncounterId, string.Empty);
            historicalLedger.ReplaceRecords(historical);
            List<CombatContributionLedgerSnapshot> historicalSnapshots = new List<CombatContributionLedgerSnapshot>
            {
                BuildSnapshot(historicalLedger)
            };

            List<CombatContributionLedgerSnapshot> components = new List<CombatContributionLedgerSnapshot>();
            foreach (CombatEncounterSplitComponentSnapshot component in splitResult.Components.Where(component => component.Active).OrderBy(component => component.EncounterId, StringComparer.Ordinal))
            {
                HashSet<string> participants = new HashSet<string>(component.ParticipantIds ?? Array.Empty<string>(), StringComparer.Ordinal);
                CombatContributionLedger componentLedger = EnsureEncounterLedger(component.EncounterId);
                componentLedger.SetActiveParticipants(participants);
                componentLedger.ReplaceRecords(historical.Where(record => RecordEligibleForParticipants(record, participants)).OrderBy(record => record.SimulationTime).ThenBy(record => record.RecordId, StringComparer.Ordinal));
                components.Add(BuildSnapshot(componentLedger));
            }

            foreach (string leftActorId in splitResult.ParticipantsLeftCombat ?? Array.Empty<string>())
            {
                foreach (CombatContributionLedger ledger in ledgersById.Values)
                {
                    ledger.ActiveParticipantIds.Remove(leftActorId);
                }
            }

            revision++;
            CombatContributionLedgerPartitionResult result = new CombatContributionLedgerPartitionResult(true, CombatContributionResultCode.Success, "Contribution eligibility partitioned for encounter split.", splitResult.OriginalEncounterId, components, historicalSnapshots, before, revision);
            LedgersPartitioned?.Invoke(result);
            return result;
        }

        private CombatContributionRecordResult RecordContributionInternal(CombatContributionRecordRequest request, bool execute)
        {
            long before = revision;
            if (defaultPolicy == null)
            {
                return CombatContributionRecordResult.Failure(!execute, CombatContributionResultCode.MissingPolicy, "Combat contribution policy is missing.", before, revision);
            }

            if (request.Preview || !execute)
            {
                if (!ValidateContributionRequest(request, out string previewCode, out string previewMessage))
                {
                    return CombatContributionRecordResult.Failure(true, previewCode, previewMessage, before, revision);
                }

                CombatContributionRecord previewRecord = BuildRecord(request, before + 1);
                return CombatContributionRecordResult.Success(true, false, "Contribution preview calculated without mutation.", previewRecord, before, before);
            }

            if (!ValidateContributionRequest(request, out string code, out string message))
            {
                return CombatContributionRecordResult.Failure(false, code, message, before, revision);
            }

            string key = BuildContributionKey(request);
            if (!processedContributionKeys.Add(key))
            {
                return CombatContributionRecordResult.Success(false, true, "Duplicate contribution transaction ignored.", null, before, revision);
            }

            CombatContributionLedger ledger = GetOrCreateLedger(request);
            if (ledger.Finalized)
            {
                return CombatContributionRecordResult.Failure(false, CombatContributionResultCode.LedgerFinalized, "Contribution ledger is finalized.", before, revision);
            }

            CombatContributionRecord record = BuildRecord(request, before + 1);
            ledger.Records.Add(record);
            PruneLedger(ledger);
            revision++;
            CombatContributionRecordResult result = CombatContributionRecordResult.Success(false, false, "Contribution recorded.", record, before, revision);
            ContributionRecorded?.Invoke(result);
            return result;
        }

        private CombatCreditResolutionResult ResolveLifecycleCredit(ActorLifecycleResult lifecycleResult, CombatCreditType creditType, string encounterId, string transactionId)
        {
            if (lifecycleResult == null || lifecycleResult.Preview || lifecycleResult.Duplicate || !lifecycleResult.Succeeded)
            {
                return BuildCreditFailure(CombatContributionResultCode.InvalidRequest, "Lifecycle result is not a committed credit source.", creditType, lifecycleResult, encounterId, transactionId);
            }

            bool transitionMatches = creditType == CombatCreditType.Defeat
                ? lifecycleResult.ResultingState == ActorLifecycleState.Defeated || lifecycleResult.ResultingState == ActorLifecycleState.Unconscious
                : lifecycleResult.ResultingState == ActorLifecycleState.Dead;
            if (!transitionMatches)
            {
                return BuildCreditFailure(CombatContributionResultCode.InvalidRequest, $"Lifecycle transition '{lifecycleResult.ResultingState}' does not qualify for {creditType} credit.", creditType, lifecycleResult, encounterId, transactionId);
            }

            string tx = string.IsNullOrWhiteSpace(transactionId) ? $"{lifecycleResult.TransactionId}.credit.{creditType}" : transactionId;
            string key = $"{creditType}|{lifecycleResult.TargetActorId}|{lifecycleResult.TransactionId}";
            if (creditResultsByKey.TryGetValue(key, out CombatCreditResolutionResult duplicate))
            {
                return new CombatCreditResolutionResult(true, true, CombatContributionResultCode.CreditAlreadyResolved, "Credit was already resolved for this lifecycle transition.", tx, duplicate.EncounterId, duplicate.TargetActorId, lifecycleResult.TransactionId, duplicate.PolicyId, creditType, duplicate.PrimaryContributorActorId, duplicate.Contributors, duplicate.Assists, duplicate.DisqualifiedReasons, simulationTime);
            }

            CombatContributionLedger ledger = FindLedger(encounterId, lifecycleResult.TargetActorId);
            if (ledger == null)
            {
                return BuildCreditFailure(CombatContributionResultCode.MissingTarget, "No contribution ledger exists for the target or encounter.", creditType, lifecycleResult, encounterId, tx);
            }

            IReadOnlyList<CombatContributorSummary> summaries = BuildSummaries(ledger, lifecycleResult.TargetActorId, simulationTime, defaultPolicy.ContributionWindowSeconds, out Dictionary<string, string> disqualified);
            CombatContributionRecord primaryRecord = ledger.Records
                .Where(record => record.IsHostileDamage
                    && string.Equals(record.TargetActorId, lifecycleResult.TargetActorId, StringComparison.Ordinal)
                    && record.ActualAmount >= defaultPolicy.MinimumDamageContribution
                    && !IsExpired(record, simulationTime, defaultPolicy.ContributionWindowSeconds)
                    && !string.Equals(record.ContributorActorId, record.TargetActorId, StringComparison.Ordinal))
                .OrderByDescending(record => record.SimulationTime)
                .ThenByDescending(record => record.ActualAmount)
                .ThenBy(record => record.ContributorActorId, StringComparer.Ordinal)
                .FirstOrDefault();

            string primary = primaryRecord == null ? string.Empty : primaryRecord.ContributorActorId;
            List<CombatContributorSummary> assists = summaries
                .Where(summary => !string.Equals(summary.ContributorActorId, primary, StringComparison.Ordinal)
                    && (summary.TotalActualDamage >= defaultPolicy.MinimumDamageContribution
                        || summary.TotalEffectiveHealing >= defaultPolicy.MinimumHealingAssistContribution
                        || summary.TotalDamagePrevented >= defaultPolicy.MinimumDefensiveAssistContribution))
                .OrderBy(summary => summary.ContributorActorId, StringComparer.Ordinal)
                .ToList();

            CombatCreditResolutionResult result = new CombatCreditResolutionResult(
                true,
                false,
                string.IsNullOrWhiteSpace(primary) ? CombatContributionResultCode.NoEligibleContributor : CombatContributionResultCode.Success,
                string.IsNullOrWhiteSpace(primary) ? "Credit resolved without an assigned primary contributor." : "Credit resolved.",
                tx,
                ledger.EncounterId,
                lifecycleResult.TargetActorId,
                lifecycleResult.TransactionId,
                defaultPolicy.Id,
                creditType,
                primary,
                summaries,
                assists,
                disqualified,
                simulationTime);
            creditResultsByKey[key] = result;
            CreditResolved?.Invoke(result);
            return result;
        }

        private CombatCreditResolutionResult BuildCreditFailure(string code, string message, CombatCreditType creditType, ActorLifecycleResult lifecycleResult, string encounterId, string transactionId)
        {
            return new CombatCreditResolutionResult(false, false, code, message, transactionId, encounterId, lifecycleResult == null ? string.Empty : lifecycleResult.TargetActorId, lifecycleResult == null ? string.Empty : lifecycleResult.TransactionId, defaultPolicy == null ? string.Empty : defaultPolicy.Id, creditType, string.Empty, null, null, null, simulationTime);
        }

        private bool ValidateContributionRequest(CombatContributionRecordRequest request, out string code, out string message)
        {
            code = CombatContributionResultCode.Success;
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(request.TargetActorId) && string.IsNullOrWhiteSpace(request.BeneficiaryActorId))
            {
                code = CombatContributionResultCode.MissingTarget;
                message = "Contribution target or beneficiary is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ContributorActorId)
                && (request.SourceKind != CombatContributionSourceKind.Environment || !defaultPolicy.EnvironmentalCreditCanBeUnassigned))
            {
                code = CombatContributionResultCode.MissingContributor;
                message = "Contribution contributor is missing.";
                return false;
            }

            if (!IsFinite(request.ActualAmount) || !IsFinite(request.PreventedAmount) || !IsFinite(request.SimulationTime))
            {
                code = CombatContributionResultCode.InvalidRequest;
                message = "Contribution contains non-finite values.";
                return false;
            }

            if (IsDamageContribution(request.ContributionType) && request.ActualAmount <= CharacterResourceCollection.Epsilon)
            {
                code = CombatContributionResultCode.ZeroEffectiveContribution;
                message = "Damage contribution removed no Health.";
                return false;
            }

            if (IsHealingContribution(request.ContributionType) && request.ActualAmount <= CharacterResourceCollection.Epsilon)
            {
                code = CombatContributionResultCode.ZeroEffectiveContribution;
                message = "Healing contribution restored no Health.";
                return false;
            }

            if (IsDefensiveContribution(request.ContributionType)
                && request.ActualAmount <= CharacterResourceCollection.Epsilon
                && request.PreventedAmount <= CharacterResourceCollection.Epsilon)
            {
                code = CombatContributionResultCode.ZeroEffectiveContribution;
                message = "Defensive contribution prevented no damage.";
                return false;
            }

            if (!IsDamageContribution(request.ContributionType)
                && !IsHealingContribution(request.ContributionType)
                && !IsDefensiveContribution(request.ContributionType)
                && request.ActualAmount <= CharacterResourceCollection.Epsilon
                && request.PreventedAmount <= CharacterResourceCollection.Epsilon)
            {
                code = CombatContributionResultCode.ZeroEffectiveContribution;
                message = "Contribution has no effective committed value.";
                return false;
            }

            if (IsDamageContribution(request.ContributionType))
            {
                if (!defaultPolicy.SelfDamageCountsForHostileCredit && string.Equals(request.ContributorActorId, request.TargetActorId, StringComparison.Ordinal))
                {
                    code = CombatContributionResultCode.ZeroEffectiveContribution;
                    message = "Self-damage is not hostile contribution under the current policy.";
                    return false;
                }
            }

            if (IsDirectHealingContribution(request.ContributionType)
                && !defaultPolicy.SelfHealingCountsAsSupport
                && string.Equals(request.ContributorActorId, request.BeneficiaryActorId, StringComparison.Ordinal))
            {
                code = CombatContributionResultCode.ZeroEffectiveContribution;
                message = "Self-healing is not support contribution under the current policy.";
                return false;
            }

            return true;
        }

        private static bool IsDamageContribution(CombatContributionType type)
        {
            return type == CombatContributionType.DamageApplied
                || type == CombatContributionType.OngoingDamageApplied
                || type == CombatContributionType.ReactionDamageApplied;
        }

        private static bool IsHealingContribution(CombatContributionType type)
        {
            return IsDirectHealingContribution(type)
                || type == CombatContributionType.RecoveryProvided
                || type == CombatContributionType.RevivalProvided;
        }

        private static bool IsDirectHealingContribution(CombatContributionType type)
        {
            return type == CombatContributionType.HealingApplied
                || type == CombatContributionType.OngoingHealingApplied
                || type == CombatContributionType.ReactionHealingApplied;
        }

        private static bool IsDefensiveContribution(CombatContributionType type)
        {
            return type == CombatContributionType.DamagePrevented
                || type == CombatContributionType.SuccessfulBlock
                || type == CombatContributionType.SuccessfulParry
                || type == CombatContributionType.SuccessfulDodge;
        }

        private CombatContributionRecord BuildRecord(CombatContributionRecordRequest request, long nextRevision)
        {
            string id = BuildRecordId(request);
            float weight = CalculateWeight(request);
            return new CombatContributionRecord(id, request, weight, nextRevision);
        }

        private float CalculateWeight(CombatContributionRecordRequest request)
        {
            switch (request.ContributionType)
            {
                case CombatContributionType.DamageApplied:
                case CombatContributionType.OngoingDamageApplied:
                case CombatContributionType.ReactionDamageApplied:
                    return request.ActualAmount * defaultPolicy.DamageScoreWeight;
                case CombatContributionType.HealingApplied:
                case CombatContributionType.OngoingHealingApplied:
                case CombatContributionType.ReactionHealingApplied:
                    return request.ActualAmount * defaultPolicy.HealingScoreWeight;
                case CombatContributionType.DamagePrevented:
                case CombatContributionType.SuccessfulBlock:
                case CombatContributionType.SuccessfulParry:
                case CombatContributionType.SuccessfulDodge:
                    return Mathf.Max(request.PreventedAmount, request.ActualAmount) * defaultPolicy.DefensiveScoreWeight;
                default:
                    return request.ActualAmount;
            }
        }

        private CombatContributionLedger GetOrCreateLedger(CombatContributionRecordRequest request)
        {
            string ledgerId = ResolveLedgerId(request.EncounterId, request.TargetActorId, request.BeneficiaryActorId);
            if (!ledgersById.TryGetValue(ledgerId, out CombatContributionLedger ledger))
            {
                ledger = new CombatContributionLedger(ledgerId, request.EncounterId, request.TargetActorId);
                ledgersById.Add(ledgerId, ledger);
            }

            return ledger;
        }

        private CombatContributionLedger EnsureEncounterLedger(string encounterId)
        {
            string id = ResolveLedgerId(encounterId, string.Empty, string.Empty);
            if (!ledgersById.TryGetValue(id, out CombatContributionLedger ledger))
            {
                ledger = new CombatContributionLedger(id, encounterId, string.Empty);
                ledgersById.Add(id, ledger);
            }

            return ledger;
        }

        private CombatContributionLedger FindLedger(string encounterId, string targetActorId)
        {
            string encounter = encounterId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(encounter))
            {
                CombatContributionLedger encounterLedger = ledgersById.Values.FirstOrDefault(ledger => string.Equals(ledger.EncounterId, encounter, StringComparison.Ordinal));
                if (encounterLedger != null)
                {
                    return encounterLedger;
                }
            }

            string target = targetActorId ?? string.Empty;
            return ledgersById.Values.FirstOrDefault(ledger => string.Equals(ledger.TargetActorId, target, StringComparison.Ordinal)
                || ledger.Records.Any(record => RecordAppliesToActor(record, target)));
        }

        private CombatContributionLedgerSnapshot BuildSnapshot(CombatContributionLedger ledger)
        {
            return new CombatContributionLedgerSnapshot(ledger.LedgerId, ledger.EncounterId, ledger.TargetActorId, ledger.Finalized, revision, ledger.Records, BuildSummaries(ledger, string.Empty, simulationTime, defaultPolicy == null ? 0f : defaultPolicy.ContributionWindowSeconds, out _), ledger.ActiveParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList());
        }

        private IReadOnlyList<CombatContributorSummary> BuildSummaries(CombatContributionLedger ledger, string creditTargetActorId, float now, float window, out Dictionary<string, string> disqualified)
        {
            disqualified = new Dictionary<string, string>(StringComparer.Ordinal);
            List<CombatContributorSummary> summaries = new List<CombatContributorSummary>();
            foreach (IGrouping<string, CombatContributionRecord> group in ledger.Records.GroupBy(record => record.ContributorActorId ?? string.Empty).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    disqualified[group.Key] = "MissingContributor";
                    continue;
                }

                List<CombatContributionRecord> active = group.Where(record => !IsExpired(record, now, window) && RecordEligibleForParticipants(record, ledger.ActiveParticipantIds)).ToList();
                if (active.Count == 0)
                {
                    disqualified[group.Key] = CombatContributionResultCode.ExpiredContribution;
                    continue;
                }

                float damage = active.Where(record => record.IsHostileDamage && (string.IsNullOrWhiteSpace(creditTargetActorId) || string.Equals(record.TargetActorId, creditTargetActorId, StringComparison.Ordinal))).Sum(record => record.ActualAmount);
                float healing = active.Where(record => record.IsHealingSupport && (string.IsNullOrWhiteSpace(creditTargetActorId) || string.Equals(record.BeneficiaryActorId, creditTargetActorId, StringComparison.Ordinal))).Sum(record => record.ActualAmount);
                float prevented = active.Where(record => record.IsDefensiveSupport && (string.IsNullOrWhiteSpace(creditTargetActorId) || RecordAppliesToActor(record, creditTargetActorId))).Sum(record => Mathf.Max(record.PreventedAmount, record.ActualAmount));
                summaries.Add(new CombatContributorSummary(
                    group.Key,
                    active.FirstOrDefault(record => !string.IsNullOrWhiteSpace(record.ContributorPersonId))?.ContributorPersonId ?? string.Empty,
                    damage,
                    healing,
                    prevented,
                    active.Count(record => record.ContributionType == CombatContributionType.SuccessfulBlock),
                    active.Count(record => record.ContributionType == CombatContributionType.SuccessfulParry),
                    active.Count(record => record.ContributionType == CombatContributionType.SuccessfulDodge),
                    active.Count(record => record.ContributionType == CombatContributionType.RecoveryProvided),
                    active.Count(record => record.ContributionType == CombatContributionType.RevivalProvided),
                    active.Min(record => record.SimulationTime),
                    active.Max(record => record.SimulationTime),
                    active.Select(record => record.RecordId).ToList(),
                    ResolveEligibility(damage, healing, prevented)));
            }

            return summaries;
        }

        private IReadOnlyList<CombatRewardEligibilityCategory> ResolveEligibility(float damage, float healing, float prevented)
        {
            List<CombatRewardEligibilityCategory> categories = new List<CombatRewardEligibilityCategory> { CombatRewardEligibilityCategory.DiagnosticOnly };
            if (damage >= defaultPolicy.MinimumDamageContribution)
            {
                categories.Add(CombatRewardEligibilityCategory.FutureExperience);
                categories.Add(CombatRewardEligibilityCategory.FutureSkillProgression);
                categories.Add(CombatRewardEligibilityCategory.FutureLootEligibility);
            }

            if (healing >= defaultPolicy.MinimumHealingAssistContribution || prevented >= defaultPolicy.MinimumDefensiveAssistContribution)
            {
                categories.Add(CombatRewardEligibilityCategory.FutureSkillProgression);
                categories.Add(CombatRewardEligibilityCategory.FutureQuestHook);
            }

            return categories.Distinct().ToList();
        }

        private void PruneLedger(CombatContributionLedger ledger)
        {
            int max = defaultPolicy.MaximumRetainedRecordsPerLedger;
            while (ledger.Records.Count > max)
            {
                ledger.Records.RemoveAt(0);
            }
        }

        private static string ResolveLedgerId(string encounterId, string targetActorId, string beneficiaryActorId)
        {
            if (!string.IsNullOrWhiteSpace(encounterId))
            {
                return $"encounter.{encounterId}";
            }

            string target = !string.IsNullOrWhiteSpace(targetActorId) ? targetActorId : beneficiaryActorId;
            return $"target.{target}";
        }

        private static string BuildContributionKey(CombatContributionRecordRequest request)
        {
            return $"{request.TransactionId}|{request.ContributionType}|{request.ContributorActorId}|{request.TargetActorId}|{request.BeneficiaryActorId}";
        }

        private static string BuildRecordId(CombatContributionRecordRequest request)
        {
            string tx = string.IsNullOrWhiteSpace(request.TransactionId) ? $"{request.ContributionType}.{request.SimulationTime:0.###}" : request.TransactionId;
            return $"contribution.{tx}.{request.ContributionType}.{request.ContributorActorId}.{request.TargetActorId}.{request.BeneficiaryActorId}";
        }

        private static bool IsExpired(CombatContributionRecord record, float now, float window)
        {
            return window > 0f && now - record.SimulationTime > window;
        }

        private static bool RecordEligibleForParticipants(CombatContributionRecord record, IReadOnlyCollection<string> participants)
        {
            if (record == null || participants == null || participants.Count == 0)
            {
                return true;
            }

            bool targetInComponent = string.IsNullOrWhiteSpace(record.TargetActorId) || participants.Contains(record.TargetActorId);
            bool beneficiaryInComponent = string.IsNullOrWhiteSpace(record.BeneficiaryActorId) || participants.Contains(record.BeneficiaryActorId);
            bool contributorInComponent = string.IsNullOrWhiteSpace(record.ContributorActorId) || participants.Contains(record.ContributorActorId);
            return contributorInComponent && targetInComponent && beneficiaryInComponent;
        }

        private static bool RecordTouchesAny(CombatContributionRecord record, IReadOnlyCollection<string> participants)
        {
            return record != null && participants != null && participants.Any(actorId => RecordTouchesActor(record, actorId));
        }

        private static bool RecordAppliesToActor(CombatContributionRecord record, string actorId)
        {
            return record != null
                && !string.IsNullOrWhiteSpace(actorId)
                && (string.Equals(record.TargetActorId, actorId, StringComparison.Ordinal)
                    || string.Equals(record.BeneficiaryActorId, actorId, StringComparison.Ordinal));
        }

        private static bool RecordTouchesActor(CombatContributionRecord record, string actorId)
        {
            if (record == null || string.IsNullOrWhiteSpace(actorId))
            {
                return false;
            }

            return string.Equals(record.ContributorActorId, actorId, StringComparison.Ordinal)
                || string.Equals(record.TargetActorId, actorId, StringComparison.Ordinal)
                || string.Equals(record.BeneficiaryActorId, actorId, StringComparison.Ordinal);
        }

        private static string ResolvePersonId(GameObject actorObject)
        {
            if (actorObject == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator coordinator = actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            return coordinator == null ? string.Empty : coordinator.PersonId;
        }

        public static string ResolveActorId(GameObject actorObject)
        {
            if (actorObject == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator coordinator = actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            if (coordinator != null && !string.IsNullOrWhiteSpace(coordinator.ActorId))
            {
                return coordinator.ActorId;
            }

            WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class CombatContributionLedger
        {
            public CombatContributionLedger(string ledgerId, string encounterId, string targetActorId)
            {
                LedgerId = ledgerId ?? string.Empty;
                EncounterId = encounterId ?? string.Empty;
                TargetActorId = targetActorId ?? string.Empty;
            }

            public string LedgerId { get; }
            public string EncounterId { get; }
            public string TargetActorId { get; }
            public bool Finalized { get; set; }
            public List<CombatContributionRecord> Records { get; } = new List<CombatContributionRecord>();
            public HashSet<string> ActiveParticipantIds { get; } = new HashSet<string>(StringComparer.Ordinal);

            public void ReplaceRecords(IEnumerable<CombatContributionRecord> records)
            {
                Records.Clear();
                if (records == null)
                {
                    return;
                }

                Records.AddRange(records);
            }

            public void SetActiveParticipants(IEnumerable<string> actorIds)
            {
                ActiveParticipantIds.Clear();
                if (actorIds == null)
                {
                    return;
                }

                foreach (string actorId in actorIds)
                {
                    if (!string.IsNullOrWhiteSpace(actorId))
                    {
                        ActiveParticipantIds.Add(actorId);
                    }
                }
            }
        }
    }
}
