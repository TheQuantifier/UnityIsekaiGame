using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat.CombatState
{
    public sealed class CombatStateService : MonoBehaviour
    {
        private const int ProcessedTransactionLimit = 2048;

        [SerializeField] private CombatStatePolicyDefinition defaultPolicy;
        [SerializeField] private bool autoProcessWithUnityTime;

        private readonly Dictionary<string, RuntimeCombatParticipant> participantsByActorId = new Dictionary<string, RuntimeCombatParticipant>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeCombatEncounter> encountersById = new Dictionary<string, RuntimeCombatEncounter>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeCombatEngagement> engagementsByPairKey = new Dictionary<string, RuntimeCombatEngagement>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTransactions = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> processedTransactionOrder = new Queue<string>();

        private float currentTimeSeconds;
        private int nextEncounterSequence;
        private int nextEngagementSequence;
        private CombatEncounterSplitResult lastConnectivityResult;

        public event Action<CombatEntryResult> ActorEnteredCombat;
        public event Action<CombatEntryResult> ActorCombatActivityRefreshed;
        public event Action<CombatExitResult> ActorBeganDisengaging;
        public event Action<CombatExitResult> ActorLeftCombat;
        public event Action<CombatEngagementSnapshot> EngagementCreated;
        public event Action<CombatEngagementSnapshot> EngagementRefreshed;
        public event Action<CombatEngagementSnapshot> EngagementEnded;
        public event Action<CombatEncounterSnapshot> EncounterCreated;
        public event Action<CombatEntryResult> ParticipantJoinedEncounter;
        public event Action<CombatEncounterSnapshot> EncountersMerged;
        public event Action<CombatEncounterSplitResult> EncounterSplit;
        public event Action<CombatEncounterSnapshot> EncounterCreatedFromSplit;
        public event Action<CombatParticipantReassignmentResult> ParticipantReassignedToEncounter;
        public event Action<CombatExitResult> ParticipantLeftEncounter;
        public event Action<CombatEncounterSnapshot> EncounterEnded;

        public CombatStatePolicyDefinition Policy => defaultPolicy;
        public float CurrentTimeSeconds => currentTimeSeconds;
        public float CombatTimeoutSeconds => defaultPolicy == null ? 10f : defaultPolicy.CombatTimeoutSeconds;

        private void Update()
        {
            if (autoProcessWithUnityTime)
            {
                ProcessTimeouts(Time.deltaTime);
            }
        }

        public void Configure(CombatStatePolicyDefinition policy)
        {
            if (policy != null)
            {
                defaultPolicy = policy;
            }
        }

        public void SetClock(float now)
        {
            currentTimeSeconds = Mathf.Max(0f, now);
        }

        public CombatStateProcessResult AdvanceTime(float deltaSeconds)
        {
            return ProcessTimeouts(deltaSeconds);
        }

        public CombatEntryResult PreviewEnterCombat(CombatEngagementRequest request)
        {
            return EnterCombatInternal(request, execute: false);
        }

        public CombatEntryResult EnterCombat(CombatEngagementRequest request)
        {
            return EnterCombatInternal(request, execute: true);
        }

        public CombatExitResult PreviewLeaveCombat(CombatExitRequest request)
        {
            return LeaveCombatInternal(request, execute: false);
        }

        public CombatExitResult LeaveCombat(CombatExitRequest request)
        {
            return LeaveCombatInternal(request, execute: true);
        }

        public CombatEncounterSnapshot PreviewEndEncounter(CombatEncounterEndRequest request)
        {
            return EndEncounterInternal(request, execute: false);
        }

        public CombatEncounterSnapshot EndEncounter(CombatEncounterEndRequest request)
        {
            return EndEncounterInternal(request, execute: true);
        }

        public CombatEncounterSplitResult PreviewEndEngagement(CombatEngagementEndRequest request)
        {
            return EndEngagementInternal(request, execute: false);
        }

        public CombatEncounterSplitResult EndEngagement(CombatEngagementEndRequest request)
        {
            return EndEngagementInternal(request, execute: true);
        }

        public CombatEncounterSplitResult ProcessEncounterConnectivity(string transactionId, string encounterId, CombatExitReason reason = CombatExitReason.Forced)
        {
            if (IsDuplicateTransaction(transactionId))
            {
                return CreateSplitResult(false, true, CombatStateResultCode.DuplicateTransaction, "Duplicate connectivity transaction ignored.", transactionId, encounterId, encounterId, null, null, null, null, null, null, null, reason, 0, 0);
            }

            if (!encountersById.TryGetValue(encounterId ?? string.Empty, out RuntimeCombatEncounter encounter) || !encounter.Active)
            {
                return CreateSplitResult(false, false, CombatStateResultCode.InvalidEncounterId, "Encounter is missing or inactive.", transactionId, encounterId, string.Empty, null, null, null, null, null, null, null, reason, 0, 0);
            }

            RememberTransaction(transactionId);
            CombatEncounterSplitResult result = ReconcileEncounterConnectivity(encounter.EncounterId, transactionId, reason, Array.Empty<string>(), encounter.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList());
            EmitSplitEvents(result);
            return result;
        }

        public CombatStateProcessResult ProcessTimeouts(float deltaSeconds)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            currentTimeSeconds += delta;
            List<CombatExitResult> exits = new List<CombatExitResult>();
            List<CombatEncounterSnapshot> ended = new List<CombatEncounterSnapshot>();
            int cap = defaultPolicy == null ? 128 : defaultPolicy.MaximumTimeoutsProcessedPerUpdate;
            int processed = 0;
            bool capped = false;

            foreach (RuntimeCombatParticipant participant in participantsByActorId.Values.OrderBy(participant => participant.ActorId, StringComparer.Ordinal).ToList())
            {
                if (processed >= cap)
                {
                    capped = true;
                    break;
                }

                if (participant.State == CombatStateValue.OutOfCombat)
                {
                    continue;
                }

                if (ShouldRemoveForLifecycle(participant.ActorId))
                {
                    lastConnectivityResult = null;
                    CombatExitResult deadExit = LeaveCombatInternal(new CombatExitRequest($"combat.timeout.dead.{participant.ActorId}.{participant.Revision}", participant.ActorId, null, CombatExitReason.Dead, authoritative: true, participant.EncounterId), execute: true);
                    if (deadExit.Succeeded)
                    {
                        exits.Add(deadExit);
                        if (lastConnectivityResult != null)
                        {
                            exits.AddRange(lastConnectivityResult.ExitResults);
                            ended.AddRange(lastConnectivityResult.EndedEncounters);
                        }

                        processed += 1 + (lastConnectivityResult == null ? 0 : lastConnectivityResult.ExitResults.Count);
                    }

                    continue;
                }

                if (currentTimeSeconds + CharacterResourceCollection.Epsilon < participant.DisengageEligibleAt)
                {
                    continue;
                }

                lastConnectivityResult = null;
                CombatExitResult timeout = LeaveCombatInternal(new CombatExitRequest($"combat.timeout.{participant.ActorId}.{participant.Revision}", participant.ActorId, null, CombatExitReason.Timeout, authoritative: true, participant.EncounterId), execute: true);
                if (timeout.Succeeded)
                {
                    exits.Add(timeout);
                    if (lastConnectivityResult != null)
                    {
                        exits.AddRange(lastConnectivityResult.ExitResults);
                        ended.AddRange(lastConnectivityResult.EndedEncounters);
                    }

                    processed += 1 + (lastConnectivityResult == null ? 0 : lastConnectivityResult.ExitResults.Count);
                }
            }

            foreach (RuntimeCombatEncounter encounter in encountersById.Values.OrderBy(encounter => encounter.EncounterId, StringComparer.Ordinal).ToList())
            {
                if (encounter.Active && !HasActiveEncounterEngagements(encounter.EncounterId))
                {
                    EndEncounterInternal(new CombatEncounterEndRequest($"combat.encounter.timeout.{encounter.EncounterId}.{encounter.Revision}", encounter.EncounterId, CombatEncounterCompletionReason.NoActiveEngagements, authoritative: true), execute: true);
                    ended.Add(CreateEncounterSnapshot(encounter));
                }
            }

            return new CombatStateProcessResult(delta, processed, capped, exits, ended);
        }

        public CombatEntryResult RecordAttackResult(AttackResolutionResult result)
        {
            if (result == null)
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.InvalidRequest, "Attack result is missing.", string.Empty, string.Empty, string.Empty);
            }

            if (result.Preview || result.Duplicate || !result.Processed || !result.Succeeded)
            {
                return CombatEntryResult.Failure(false, result.Duplicate ? CombatStateResultCode.DuplicateTransaction : CombatStateResultCode.NonHostile, "Attack result does not qualify for combat entry.", result.AttackTransactionId, result.ResolvedAttackerActorId, result.ResolvedTargetActorId);
            }

            if (result.Outcome == AttackOutcome.Blocked || result.Outcome == AttackOutcome.Invalid)
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.NonHostile, "Blocked or invalid attacks do not enter combat.", result.AttackTransactionId, result.ResolvedAttackerActorId, result.ResolvedTargetActorId);
            }

            if (result.Outcome == AttackOutcome.Miss && defaultPolicy != null && !defaultPolicy.MissesStartCombat)
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.NonHostile, "Combat policy ignores missed attacks.", result.AttackTransactionId, result.ResolvedAttackerActorId, result.ResolvedTargetActorId);
            }

            CombatActivityClassification classification = result.Outcome == AttackOutcome.Miss ? CombatActivityClassification.AttackAttempted : CombatActivityClassification.AttackHit;
            return EnterCombat(new CombatEngagementRequest(result.AttackTransactionId, result.ResolvedAttackerActorId, result.Request.AttackerObject, result.ResolvedTargetActorId, result.Request.TargetObject, classification, result.OriginatingActionId, hostile: true, authorityValidated: result.Request.AuthorityValidated));
        }

        public CombatEntryResult RecordDamageResult(DamageApplicationResult result, bool hostile = true, string originatingId = "")
        {
            if (result == null)
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.InvalidRequest, "Damage result is missing.", string.Empty, string.Empty, string.Empty);
            }

            if (result.Preview || result.Duplicate || !result.Succeeded || !hostile)
            {
                return CombatEntryResult.Failure(false, result.Duplicate ? CombatStateResultCode.DuplicateTransaction : CombatStateResultCode.NonHostile, "Damage result does not qualify for combat entry.", result.Request.TransactionId, result.Request.SourceActorId, result.ResolvedTargetActorId);
            }

            if (string.IsNullOrWhiteSpace(result.Request.SourceActorId) && result.Request.SourceObject == null)
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.MissingSource, "Source-less damage does not imply a hostile engagement.", result.Request.TransactionId, result.Request.SourceActorId, result.ResolvedTargetActorId);
            }

            bool fullyPrevented = !result.HealthChanged && (result.Immune || result.FinalDamageAmount <= CharacterResourceCollection.Epsilon);
            if (!result.HealthChanged && (!fullyPrevented || (defaultPolicy != null && !defaultPolicy.PreventedDamageStartsCombat)))
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.NonHostile, "Damage did not produce qualifying hostile activity.", result.Request.TransactionId, result.Request.SourceActorId, result.ResolvedTargetActorId);
            }

            CombatActivityClassification classification = result.HealthChanged ? CombatActivityClassification.DamageApplied : CombatActivityClassification.DamagePrevented;
            return EnterCombat(new CombatEngagementRequest(result.Request.TransactionId, result.Request.SourceActorId, result.Request.SourceObject, result.ResolvedTargetActorId, result.Request.TargetObject, classification, string.IsNullOrWhiteSpace(originatingId) ? result.Request.Reason : originatingId, hostile: true, authorityValidated: result.Request.AuthorityValidated));
        }

        public CombatEntryResult RecordOngoingEffectApplication(OngoingEffectApplicationResult result)
        {
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded)
            {
                return CombatEntryResult.Failure(false, result != null && result.Duplicate ? CombatStateResultCode.DuplicateTransaction : CombatStateResultCode.NonHostile, "Ongoing effect application does not qualify for combat entry.", result == null ? string.Empty : result.TransactionId, result == null ? string.Empty : result.SourceActorId, result == null ? string.Empty : result.TargetActorId);
            }

            return EnterCombat(new CombatEngagementRequest(result.TransactionId, result.SourceActorId, null, result.TargetActorId, null, CombatActivityClassification.HostileOngoingEffectApplied, result.DefinitionId, hostile: true, authorityValidated: true));
        }

        public CombatEntryResult RecordOngoingEffectTick(OngoingEffectTickResult result)
        {
            if (result == null || !result.Succeeded || result.Outcome == OngoingEffectTickOutcome.Duplicate || result.OperationType != OngoingEffectOperationType.Damage)
            {
                return CombatEntryResult.Failure(false, result != null && result.Outcome == OngoingEffectTickOutcome.Duplicate ? CombatStateResultCode.DuplicateTransaction : CombatStateResultCode.NonHostile, "Ongoing effect tick does not qualify for combat refresh.", result == null ? string.Empty : result.TickTransactionId, string.Empty, string.Empty);
            }

            if (defaultPolicy != null && !defaultPolicy.HostileOngoingDamageRefreshesCombat)
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.NonHostile, "Combat policy ignores ongoing damage ticks.", result.TickTransactionId, string.Empty, string.Empty);
            }

            DamageApplicationResult damage = result.DamageResult;
            if (damage == null)
            {
                return CombatEntryResult.Failure(false, CombatStateResultCode.InvalidRequest, "Ongoing damage tick has no nested damage result.", result.TickTransactionId, string.Empty, string.Empty);
            }

            return EnterCombat(new CombatEngagementRequest(result.TickTransactionId, damage.Request.SourceActorId, damage.Request.SourceObject, damage.ResolvedTargetActorId, damage.Request.TargetObject, CombatActivityClassification.HostileOngoingEffectTicked, result.DefinitionId, hostile: true, authorityValidated: true));
        }

        public ActorCombatStateSnapshot GetCombatState(string actorId)
        {
            string normalized = actorId ?? string.Empty;
            return participantsByActorId.TryGetValue(normalized, out RuntimeCombatParticipant participant)
                ? CreateActorSnapshot(participant)
                : new ActorCombatStateSnapshot(normalized, CombatStateValue.OutOfCombat, string.Empty, 0f, 0f, 0f, 0, 0, 0, string.Empty);
        }

        public bool IsInCombat(string actorId)
        {
            return GetCombatState(actorId).IsInCombat;
        }

        public CombatEncounterSnapshot GetEncounter(string encounterId)
        {
            return encountersById.TryGetValue(encounterId ?? string.Empty, out RuntimeCombatEncounter encounter)
                ? CreateEncounterSnapshot(encounter)
                : null;
        }

        public CombatEncounterSnapshot GetEncounterForActor(string actorId)
        {
            if (!participantsByActorId.TryGetValue(actorId ?? string.Empty, out RuntimeCombatParticipant participant))
            {
                return null;
            }

            return GetEncounter(participant.EncounterId);
        }

        public IReadOnlyList<CombatEngagementSnapshot> GetActiveEngagements(string actorId)
        {
            return engagementsByPairKey.Values
                .Where(engagement => engagement.Active && engagement.Includes(actorId ?? string.Empty))
                .OrderBy(engagement => engagement.EngagementId, StringComparer.Ordinal)
                .Select(CreateEngagementSnapshot)
                .ToList();
        }

        public IReadOnlyList<string> GetParticipants(string encounterId)
        {
            if (!encountersById.TryGetValue(encounterId ?? string.Empty, out RuntimeCombatEncounter encounter))
            {
                return Array.Empty<string>();
            }

            return encounter.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        public IReadOnlyList<string> GetRecentOpponents(string actorId)
        {
            string normalized = actorId ?? string.Empty;
            return engagementsByPairKey.Values
                .Where(engagement => engagement.Includes(normalized))
                .Select(engagement => string.Equals(engagement.FirstActorId, normalized, StringComparison.Ordinal) ? engagement.SecondActorId : engagement.FirstActorId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        public CombatStateIntegrityResult ValidateIntegrity()
        {
            List<string> diagnostics = new List<string>();
            Dictionary<string, int> participantOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            HashSet<string> activeEngagementIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (RuntimeCombatEncounter encounter in encountersById.Values.Where(encounter => encounter.Active).OrderBy(encounter => encounter.EncounterId, StringComparer.Ordinal))
            {
                foreach (string participantId in encounter.ParticipantIds)
                {
                    participantOccurrences.TryGetValue(participantId, out int count);
                    participantOccurrences[participantId] = count + 1;
                    if (!participantsByActorId.TryGetValue(participantId, out RuntimeCombatParticipant participant))
                    {
                        diagnostics.Add($"Encounter '{encounter.EncounterId}' contains stale participant '{participantId}'.");
                    }
                    else if (!string.Equals(participant.EncounterId, encounter.EncounterId, StringComparison.Ordinal))
                    {
                        diagnostics.Add($"Participant '{participantId}' maps to '{participant.EncounterId}' but is listed in '{encounter.EncounterId}'.");
                    }
                }

                List<RuntimeCombatEngagement> activeEdges = GetActiveEncounterEdges(encounter.EncounterId).ToList();
                foreach (RuntimeCombatEngagement engagement in activeEdges)
                {
                    if (!activeEngagementIds.Add(engagement.EngagementId))
                    {
                        diagnostics.Add($"Duplicate active engagement ID '{engagement.EngagementId}'.");
                    }

                    if (!encounter.ParticipantIds.Contains(engagement.FirstActorId) || !encounter.ParticipantIds.Contains(engagement.SecondActorId))
                    {
                        diagnostics.Add($"Engagement '{engagement.EngagementId}' references encounter '{encounter.EncounterId}' but participants '{engagement.FirstActorId}' and '{engagement.SecondActorId}' are not both present.");
                    }
                }

                if (encounter.ParticipantIds.Count == 1 && activeEdges.Count == 0)
                {
                    diagnostics.Add($"Encounter '{encounter.EncounterId}' has one participant and no authored single-participant rule.");
                }

                if (encounter.ParticipantIds.Count > 0 && activeEdges.Count == 0)
                {
                    diagnostics.Add($"Encounter '{encounter.EncounterId}' is active with no active engagements.");
                }

                List<List<string>> components = BuildConnectedComponents(encounter.ParticipantIds, activeEdges);
                if (components.Count > 1 || components.Any(component => component.Count == 1))
                {
                    diagnostics.Add($"Encounter '{encounter.EncounterId}' is disconnected or contains isolated participants. Components={string.Join(";", components.Select(component => string.Join(",", component)))}.");
                }
            }

            foreach (KeyValuePair<string, RuntimeCombatParticipant> participant in participantsByActorId.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (participant.Value.State == CombatStateValue.OutOfCombat)
                {
                    continue;
                }

                if (!encountersById.TryGetValue(participant.Value.EncounterId, out RuntimeCombatEncounter encounter) || !encounter.Active)
                {
                    diagnostics.Add($"Participant '{participant.Key}' maps to missing or inactive encounter '{participant.Value.EncounterId}'.");
                }

                if (!participantOccurrences.ContainsKey(participant.Key))
                {
                    diagnostics.Add($"Participant '{participant.Key}' is not listed in active encounter '{participant.Value.EncounterId}'.");
                }
            }

            foreach (KeyValuePair<string, int> occurrence in participantOccurrences.Where(pair => pair.Value > 1).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                diagnostics.Add($"Participant '{occurrence.Key}' appears in {occurrence.Value} active encounters.");
            }

            foreach (RuntimeCombatEngagement engagement in engagementsByPairKey.Values.Where(engagement => engagement.Active).OrderBy(engagement => engagement.EngagementId, StringComparer.Ordinal))
            {
                if (!encountersById.TryGetValue(engagement.EncounterId, out RuntimeCombatEncounter encounter) || !encounter.Active)
                {
                    diagnostics.Add($"Active engagement '{engagement.EngagementId}' references missing or inactive encounter '{engagement.EncounterId}'.");
                    continue;
                }

                if (!participantsByActorId.ContainsKey(engagement.FirstActorId) || !participantsByActorId.ContainsKey(engagement.SecondActorId))
                {
                    diagnostics.Add($"Active engagement '{engagement.EngagementId}' has stale participant endpoint(s) '{engagement.FirstActorId}' and '{engagement.SecondActorId}'.");
                }
            }

            return new CombatStateIntegrityResult(diagnostics.Count == 0, diagnostics);
        }

        public void ClearTransientStateForRestore()
        {
            participantsByActorId.Clear();
            encountersById.Clear();
            engagementsByPairKey.Clear();
        }

        private CombatEntryResult EnterCombatInternal(CombatEngagementRequest request, bool execute)
        {
            if (!request.Hostile)
            {
                return CombatEntryResult.Failure(!execute, CombatStateResultCode.NonHostile, "Non-hostile requests do not enter combat.", request.TransactionId, request.SourceActorId, request.TargetActorId);
            }

            if (!TryResolveActor(request.SourceActorId, request.SourceObject, out string sourceActorId, out string sourceFailure, out string sourceCode, isSource: true))
            {
                return CombatEntryResult.Failure(!execute, sourceCode, sourceFailure, request.TransactionId, request.SourceActorId, request.TargetActorId);
            }

            if (!TryResolveActor(request.TargetActorId, request.TargetObject, out string targetActorId, out string targetFailure, out string targetCode, isSource: false))
            {
                return CombatEntryResult.Failure(!execute, targetCode, targetFailure, request.TransactionId, sourceActorId, request.TargetActorId);
            }

            if (string.Equals(sourceActorId, targetActorId, StringComparison.Ordinal))
            {
                return CombatEntryResult.Failure(!execute, CombatStateResultCode.SelfEngagementRejected, "Self-targeting hostile activity does not create an enemy engagement.", request.TransactionId, sourceActorId, targetActorId);
            }

            if (execute && defaultPolicy != null)
            {
                int projectedParticipantCount = participantsByActorId.Count;
                if (!participantsByActorId.ContainsKey(sourceActorId))
                {
                    projectedParticipantCount++;
                }

                if (!participantsByActorId.ContainsKey(targetActorId))
                {
                    projectedParticipantCount++;
                }

                if (projectedParticipantCount > defaultPolicy.MaximumParticipants)
                {
                    return CombatEntryResult.Failure(false, CombatStateResultCode.ProcessingCapReached, "Combat participant limit would be exceeded.", request.TransactionId, sourceActorId, targetActorId);
                }
            }

            ActorCombatStateSnapshot sourcePrevious = GetCombatState(sourceActorId);
            ActorCombatStateSnapshot targetPrevious = GetCombatState(targetActorId);
            RuntimeCombatEngagement existingEngagement = FindEngagement(sourceActorId, targetActorId);
            RuntimeCombatEncounter existingSourceEncounter = GetActiveEncounterForActor(sourceActorId);
            RuntimeCombatEncounter existingTargetEncounter = GetActiveEncounterForActor(targetActorId);
            bool previewEncounterCreated = existingSourceEncounter == null && existingTargetEncounter == null;
            bool previewMerged = existingSourceEncounter != null && existingTargetEncounter != null && !string.Equals(existingSourceEncounter.EncounterId, existingTargetEncounter.EncounterId, StringComparison.Ordinal);
            string previewEncounterId = ChoosePreviewEncounterId(existingSourceEncounter, existingTargetEncounter, request.EncounterId);
            string previewEngagementId = existingEngagement == null ? "preview.engagement" : existingEngagement.EngagementId;

            if (!execute)
            {
                ActorCombatStateSnapshot sourcePreview = BuildPreviewActorSnapshot(sourceActorId, previewEncounterId, sourcePrevious, request.Classification.ToString());
                ActorCombatStateSnapshot targetPreview = BuildPreviewActorSnapshot(targetActorId, previewEncounterId, targetPrevious, request.Classification.ToString());
                return CombatEntryResult.Success(true, false, CombatStateResultCode.Preview, "Combat entry preview calculated without mutation.", request.TransactionId, sourceActorId, targetActorId, sourcePrevious, targetPrevious, sourcePreview, targetPreview, previewEngagementId, previewEncounterId, previewEncounterCreated, !sourcePrevious.IsInCombat, !targetPrevious.IsInCombat, previewMerged, currentTimeSeconds);
            }

            if (IsDuplicateTransaction(request.TransactionId))
            {
                return CombatEntryResult.Success(false, true, CombatStateResultCode.DuplicateTransaction, "Duplicate combat transaction ignored.", request.TransactionId, sourceActorId, targetActorId, sourcePrevious, targetPrevious, sourcePrevious, targetPrevious, existingEngagement == null ? string.Empty : existingEngagement.EngagementId, sourcePrevious.EncounterId, false, false, false, false, currentTimeSeconds);
            }

            RememberTransaction(request.TransactionId);

            RuntimeCombatEncounter encounter = ResolveEncounter(sourceActorId, targetActorId, request.EncounterId, out bool encounterCreated, out bool merged, out CombatEncounterSnapshot mergeSnapshot);
            bool sourceAdded = AddOrRefreshParticipant(encounter, sourceActorId, request.SourceObject, request.Classification.ToString());
            bool targetAdded = AddOrRefreshParticipant(encounter, targetActorId, request.TargetObject, request.Classification.ToString());
            RuntimeCombatEngagement engagement = UpsertEngagement(sourceActorId, targetActorId, encounter.EncounterId, request.Classification, request.OriginatingId, out bool engagementCreated);
            encounter.EngagementIds.Add(engagement.EngagementId);
            encounter.Touch(currentTimeSeconds);

            ActorCombatStateSnapshot sourceResult = GetCombatState(sourceActorId);
            ActorCombatStateSnapshot targetResult = GetCombatState(targetActorId);
            CombatEntryResult result = CombatEntryResult.Success(false, false, CombatStateResultCode.Success, "Combat state updated.", request.TransactionId, sourceActorId, targetActorId, sourcePrevious, targetPrevious, sourceResult, targetResult, engagement.EngagementId, encounter.EncounterId, encounterCreated, sourceAdded, targetAdded, merged, currentTimeSeconds);

            if (encounterCreated)
            {
                EncounterCreated?.Invoke(CreateEncounterSnapshot(encounter));
            }

            if (merged && mergeSnapshot != null)
            {
                EncountersMerged?.Invoke(mergeSnapshot);
            }

            if (sourceAdded || targetAdded)
            {
                ParticipantJoinedEncounter?.Invoke(result);
            }

            if (engagementCreated)
            {
                EngagementCreated?.Invoke(CreateEngagementSnapshot(engagement));
            }
            else
            {
                EngagementRefreshed?.Invoke(CreateEngagementSnapshot(engagement));
            }

            if (!sourcePrevious.IsInCombat || !targetPrevious.IsInCombat)
            {
                ActorEnteredCombat?.Invoke(result);
            }
            else
            {
                ActorCombatActivityRefreshed?.Invoke(result);
            }

            return result;
        }

        private CombatExitResult LeaveCombatInternal(CombatExitRequest request, bool execute, bool reconcileConnectivity = true)
        {
            lastConnectivityResult = null;
            if (!TryResolveActor(request.ActorId, request.ActorObject, out string actorId, out string failure, out string code, isSource: false, allowIdOnly: true))
            {
                return CombatExitResult.Failure(!execute, code, failure, request.TransactionId, request.ActorId, request.EncounterId);
            }

            if (!participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant) || participant.State == CombatStateValue.OutOfCombat)
            {
                return CombatExitResult.Failure(!execute, CombatStateResultCode.ParticipantMissing, "Actor is not in combat.", request.TransactionId, actorId, request.EncounterId);
            }

            if (!string.IsNullOrWhiteSpace(request.EncounterId) && !string.Equals(participant.EncounterId, request.EncounterId, StringComparison.Ordinal))
            {
                return CombatExitResult.Failure(!execute, CombatStateResultCode.InvalidEncounterId, "Actor is not in the requested encounter.", request.TransactionId, actorId, request.EncounterId);
            }

            if (!request.Authoritative && HasActiveEngagements(actorId))
            {
                return CombatExitResult.Failure(!execute, CombatStateResultCode.ActiveEngagementPreventsExit, "Active engagements prevent non-authoritative combat exit.", request.TransactionId, actorId, participant.EncounterId);
            }

            if (request.Reason == CombatExitReason.Timeout && currentTimeSeconds + CharacterResourceCollection.Epsilon < participant.DisengageEligibleAt)
            {
                return CombatExitResult.Failure(!execute, CombatStateResultCode.TimeoutNotElapsed, "Combat timeout has not elapsed.", request.TransactionId, actorId, participant.EncounterId);
            }

            ActorCombatStateSnapshot previous = CreateActorSnapshot(participant);
            IReadOnlyList<string> endingEngagements = engagementsByPairKey.Values
                .Where(engagement => engagement.Active && engagement.Includes(actorId))
                .Select(engagement => engagement.EngagementId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (!execute)
            {
                ActorCombatStateSnapshot preview = new ActorCombatStateSnapshot(actorId, CombatStateValue.OutOfCombat, string.Empty, participant.EnteredAt, participant.LastActivityAt, participant.DisengageEligibleAt, previous.ParticipantCount, 0, participant.Revision + 1, request.Reason.ToString());
                return CombatExitResult.Success(true, false, CombatStateResultCode.Preview, "Combat exit preview calculated without mutation.", request.TransactionId, actorId, participant.EncounterId, previous, preview, endingEngagements, request.Reason, currentTimeSeconds);
            }

            if (IsDuplicateTransaction(request.TransactionId))
            {
                return CombatExitResult.Success(false, true, CombatStateResultCode.DuplicateTransaction, "Duplicate combat exit ignored.", request.TransactionId, actorId, participant.EncounterId, previous, previous, Array.Empty<string>(), request.Reason, currentTimeSeconds);
            }

            RememberTransaction(request.TransactionId);
            string encounterId = participant.EncounterId;
            List<string> previousParticipants = encountersById.TryGetValue(encounterId, out RuntimeCombatEncounter previousEncounter)
                ? previousEncounter.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList()
                : new List<string> { actorId };
            participant.State = CombatStateValue.Disengaging;
            participant.Revision++;
            CombatExitResult disengaging = CombatExitResult.Success(false, false, CombatStateResultCode.Success, "Actor began disengaging.", request.TransactionId, actorId, participant.EncounterId, previous, CreateActorSnapshot(participant), endingEngagements, request.Reason, currentTimeSeconds);
            ActorBeganDisengaging?.Invoke(disengaging);

            List<CombatEngagementSnapshot> endedEngagementSnapshots = new List<CombatEngagementSnapshot>();
            foreach (RuntimeCombatEngagement engagement in engagementsByPairKey.Values.Where(engagement => engagement.Active && engagement.Includes(actorId)).ToList())
            {
                engagement.End(request.Reason);
                endedEngagementSnapshots.Add(CreateEngagementSnapshot(engagement));
            }

            if (encountersById.TryGetValue(encounterId, out RuntimeCombatEncounter encounter))
            {
                encounter.RemoveParticipant(actorId);
            }

            participant.State = CombatStateValue.OutOfCombat;
            participant.EncounterId = string.Empty;
            participant.Revision++;
            participant.TransitionReason = request.Reason.ToString();
            ActorCombatStateSnapshot resulting = CreateActorSnapshot(participant);
            participantsByActorId.Remove(actorId);

            CombatExitResult result = CombatExitResult.Success(false, false, CombatStateResultCode.Success, "Actor left combat.", request.TransactionId, actorId, encounterId, previous, resulting, endingEngagements, request.Reason, currentTimeSeconds);
            CombatEncounterSplitResult splitResult = reconcileConnectivity
                ? ReconcileEncounterConnectivity(encounterId, request.TransactionId, request.Reason, endingEngagements, previousParticipants)
                : null;
            lastConnectivityResult = splitResult;
            foreach (CombatEngagementSnapshot engagementSnapshot in endedEngagementSnapshots)
            {
                EngagementEnded?.Invoke(engagementSnapshot);
            }

            EmitSplitEvents(splitResult);
            ParticipantLeftEncounter?.Invoke(result);
            ActorLeftCombat?.Invoke(result);
            return result;
        }

        private CombatEncounterSplitResult EndEngagementInternal(CombatEngagementEndRequest request, bool execute)
        {
            if (!request.Authoritative)
            {
                return CreateSplitResult(false, !execute, CombatStateResultCode.ActiveEngagementPreventsExit, "Ending an engagement requires authoritative combat control.", request.TransactionId, string.Empty, string.Empty, null, null, null, null, null, null, null, request.Reason, 0, 0);
            }

            if (execute && IsDuplicateTransaction(request.TransactionId))
            {
                return new CombatEncounterSplitResult(true, false, true, CombatStateResultCode.DuplicateTransaction, "Duplicate engagement-end transaction ignored.", request.TransactionId, string.Empty, string.Empty, null, null, null, null, null, null, null, null, request.Reason, 0, 0, currentTimeSeconds);
            }

            RuntimeCombatEngagement engagement = ResolveEngagementForEnd(request);
            if (engagement == null || !engagement.Active)
            {
                return CreateSplitResult(false, !execute, CombatStateResultCode.EngagementMissing, "Active engagement was not found.", request.TransactionId, string.Empty, string.Empty, null, null, null, null, null, null, null, request.Reason, 0, 0);
            }

            RuntimeCombatEncounter encounter = encountersById.TryGetValue(engagement.EncounterId, out RuntimeCombatEncounter foundEncounter) ? foundEncounter : null;
            if (encounter == null || !encounter.Active)
            {
                return CreateSplitResult(false, !execute, CombatStateResultCode.InvalidEncounterId, "Engagement encounter is missing or inactive.", request.TransactionId, engagement.EncounterId, string.Empty, null, null, null, null, null, null, null, request.Reason, 0, 0);
            }

            List<string> previousParticipants = encounter.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
            long revisionBefore = encounter.Revision;
            if (!execute)
            {
                return new CombatEncounterSplitResult(true, true, false, CombatStateResultCode.Preview, "Engagement end preview calculated without mutation.", request.TransactionId, encounter.EncounterId, encounter.EncounterId, null, previousParticipants, null, new[] { engagement.EngagementId }, null, null, null, null, request.Reason, revisionBefore, revisionBefore, currentTimeSeconds);
            }

            RememberTransaction(request.TransactionId);
            engagement.End(request.Reason);
            CombatEngagementSnapshot endedSnapshot = CreateEngagementSnapshot(engagement);
            CombatEncounterSplitResult splitResult = ReconcileEncounterConnectivity(encounter.EncounterId, request.TransactionId, request.Reason, new[] { engagement.EngagementId }, previousParticipants);
            EngagementEnded?.Invoke(endedSnapshot);
            EmitSplitEvents(splitResult);
            return splitResult;
        }

        private CombatEncounterSnapshot EndEncounterInternal(CombatEncounterEndRequest request, bool execute)
        {
            if (!encountersById.TryGetValue(request.EncounterId ?? string.Empty, out RuntimeCombatEncounter encounter))
            {
                return null;
            }

            CombatEncounterSnapshot snapshot = CreateEncounterSnapshot(encounter);
            if (!execute)
            {
                return snapshot;
            }

            if (!request.Authoritative && HasActiveEncounterEngagements(encounter.EncounterId))
            {
                return snapshot;
            }

            if (IsDuplicateTransaction(request.TransactionId))
            {
                return snapshot;
            }

            RememberTransaction(request.TransactionId);
            foreach (string participantId in encounter.ParticipantIds.ToList())
            {
                LeaveCombatInternal(new CombatExitRequest($"{request.TransactionId}.participant.{participantId}", participantId, null, CombatExitReason.EncounterEnded, authoritative: true, encounter.EncounterId), execute: true, reconcileConnectivity: false);
            }

            foreach (RuntimeCombatEngagement engagement in engagementsByPairKey.Values.Where(engagement => engagement.EncounterId == encounter.EncounterId && engagement.Active).ToList())
            {
                engagement.End(CombatExitReason.EncounterEnded);
            }

            encounter.End(request.Reason);
            CombatEncounterSnapshot ended = CreateEncounterSnapshot(encounter);
            EncounterEnded?.Invoke(ended);
            return ended;
        }

        private RuntimeCombatEncounter ResolveEncounter(string sourceActorId, string targetActorId, string requestedEncounterId, out bool created, out bool merged, out CombatEncounterSnapshot mergeSnapshot)
        {
            created = false;
            merged = false;
            mergeSnapshot = null;
            RuntimeCombatEncounter sourceEncounter = GetActiveEncounterForActor(sourceActorId);
            RuntimeCombatEncounter targetEncounter = GetActiveEncounterForActor(targetActorId);

            if (sourceEncounter == null && targetEncounter == null)
            {
                RuntimeCombatEncounter createdEncounter = CreateEncounter(string.IsNullOrWhiteSpace(requestedEncounterId) ? string.Empty : requestedEncounterId);
                created = true;
                return createdEncounter;
            }

            if (sourceEncounter != null && targetEncounter == null)
            {
                return sourceEncounter;
            }

            if (sourceEncounter == null)
            {
                return targetEncounter;
            }

            if (string.Equals(sourceEncounter.EncounterId, targetEncounter.EncounterId, StringComparison.Ordinal))
            {
                return sourceEncounter;
            }

            RuntimeCombatEncounter survivor = ChooseMergeSurvivor(sourceEncounter, targetEncounter);
            RuntimeCombatEncounter absorbed = ReferenceEquals(survivor, sourceEncounter) ? targetEncounter : sourceEncounter;
            MergeEncounterInto(survivor, absorbed);
            merged = true;
            mergeSnapshot = CreateEncounterSnapshot(survivor);
            return survivor;
        }

        private RuntimeCombatEncounter CreateEncounter(string requestedEncounterId)
        {
            string id = string.IsNullOrWhiteSpace(requestedEncounterId)
                ? $"encounter.runtime.{++nextEncounterSequence:0000}"
                : requestedEncounterId.Trim();
            while (encountersById.ContainsKey(id))
            {
                id = $"encounter.runtime.{++nextEncounterSequence:0000}";
            }

            RuntimeCombatEncounter encounter = new RuntimeCombatEncounter(id, currentTimeSeconds);
            encountersById.Add(id, encounter);
            return encounter;
        }

        private RuntimeCombatEncounter ChooseMergeSurvivor(RuntimeCombatEncounter first, RuntimeCombatEncounter second)
        {
            if (Math.Abs(first.CreatedAt - second.CreatedAt) > CharacterResourceCollection.Epsilon)
            {
                return first.CreatedAt < second.CreatedAt ? first : second;
            }

            return string.CompareOrdinal(first.EncounterId, second.EncounterId) <= 0 ? first : second;
        }

        private void MergeEncounterInto(RuntimeCombatEncounter survivor, RuntimeCombatEncounter absorbed)
        {
            foreach (string actorId in absorbed.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList())
            {
                survivor.AddParticipant(actorId);
                if (participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant))
                {
                    participant.EncounterId = survivor.EncounterId;
                    participant.Revision++;
                }
            }

            foreach (string engagementId in absorbed.EngagementIds.ToList())
            {
                survivor.EngagementIds.Add(engagementId);
            }

            foreach (RuntimeCombatEngagement engagement in engagementsByPairKey.Values.Where(engagement => engagement.EncounterId == absorbed.EncounterId))
            {
                engagement.ReassignEncounter(survivor.EncounterId);
            }

            survivor.LastActivityAt = Mathf.Max(survivor.LastActivityAt, absorbed.LastActivityAt);
            absorbed.End(CombatEncounterCompletionReason.Forced);
        }

        private CombatEncounterSplitResult ReconcileEncounterConnectivity(string encounterId, string transactionId, CombatExitReason reason, IReadOnlyList<string> endedEngagementIds, IReadOnlyList<string> previousParticipantIds)
        {
            if (!encountersById.TryGetValue(encounterId ?? string.Empty, out RuntimeCombatEncounter encounter) || !encounter.Active)
            {
                return CreateSplitResult(true, false, CombatStateResultCode.Success, "Encounter is already inactive; no connectivity update required.", transactionId, encounterId, string.Empty, null, previousParticipantIds, null, endedEngagementIds, null, null, null, reason, 0, 0);
            }

            long revisionBefore = encounter.Revision;
            List<string> previousParticipants = previousParticipantIds == null
                ? encounter.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList()
                : previousParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
            List<string> activeParticipants = encounter.ParticipantIds
                .Where(actorId => participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant)
                    && string.Equals(participant.EncounterId, encounter.EncounterId, StringComparison.Ordinal)
                    && participant.State != CombatStateValue.OutOfCombat)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            List<RuntimeCombatEngagement> activeEdges = GetActiveEncounterEdges(encounter.EncounterId)
                .Where(engagement => activeParticipants.Contains(engagement.FirstActorId) && activeParticipants.Contains(engagement.SecondActorId))
                .OrderBy(engagement => engagement.EngagementId, StringComparer.Ordinal)
                .ToList();
            List<List<string>> components = BuildConnectedComponents(activeParticipants, activeEdges);
            List<string> isolatedParticipants = components
                .Where(component => component.Count == 1 && !activeEdges.Any(edge => edge.Includes(component[0])))
                .Select(component => component[0])
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            List<EncounterComponentPlan> activeComponentPlans = components
                .Where(component => component.Count >= 2)
                .Select(component => CreateComponentPlan(component, activeEdges))
                .Where(plan => plan.Engagements.Count > 0)
                .OrderBy(plan => plan.EarliestJoinAt)
                .ThenBy(plan => plan.LowestActorId, StringComparer.Ordinal)
                .ToList();

            if (activeComponentPlans.Count == 1 && isolatedParticipants.Count == 0 && SetEquals(activeComponentPlans[0].ParticipantIds, activeParticipants))
            {
                List<CombatEncounterSplitComponentSnapshot> connectedComponent = new List<CombatEncounterSplitComponentSnapshot>
                {
                    CreateComponentSnapshot(encounter.EncounterId, activeComponentPlans[0], retainedOriginal: true, active: true)
                };
                return CreateSplitResult(true, false, CombatStateResultCode.Success, "Encounter connectivity remains intact.", transactionId, encounter.EncounterId, encounter.EncounterId, null, previousParticipants, connectedComponent, endedEngagementIds, null, null, null, reason, revisionBefore, encounter.Revision);
            }

            List<CombatExitResult> exitResults = new List<CombatExitResult>();
            foreach (string isolatedParticipant in isolatedParticipants)
            {
                CombatExitResult isolatedExit = RemoveIsolatedParticipant(isolatedParticipant, encounter.EncounterId, reason, $"{transactionId}.isolated.{isolatedParticipant}");
                if (isolatedExit != null && isolatedExit.Succeeded)
                {
                    exitResults.Add(isolatedExit);
                }
            }

            List<string> createdEncounterIds = new List<string>();
            List<CombatEncounterSnapshot> endedEncounters = new List<CombatEncounterSnapshot>();
            List<CombatParticipantReassignmentResult> reassignments = new List<CombatParticipantReassignmentResult>();
            List<CombatEncounterSplitComponentSnapshot> componentSnapshots = new List<CombatEncounterSplitComponentSnapshot>();
            string survivingEncounterId = string.Empty;

            if (activeComponentPlans.Count == 0)
            {
                encounter.ReplaceMembership(Array.Empty<string>(), Array.Empty<string>(), currentTimeSeconds);
                encounter.End(CombatEncounterCompletionReason.NoActiveEngagements);
                endedEncounters.Add(CreateEncounterSnapshot(encounter));
                return CreateSplitResult(true, false, CombatStateResultCode.Success, "Encounter ended because no connected active component remains.", transactionId, encounter.EncounterId, string.Empty, createdEncounterIds, previousParticipants, componentSnapshots, endedEngagementIds, exitResults.Select(exit => exit.ActorId).ToList(), exitResults, endedEncounters, reassignments, reason, revisionBefore, encounter.Revision);
            }

            EncounterComponentPlan survivor = activeComponentPlans[0];
            survivor.EncounterId = encounter.EncounterId;
            survivor.RetainedOriginalEncounterId = true;
            survivingEncounterId = encounter.EncounterId;

            foreach (EncounterComponentPlan plan in activeComponentPlans.Skip(1))
            {
                RuntimeCombatEncounter created = CreateEncounter(string.Empty);
                plan.EncounterId = created.EncounterId;
                createdEncounterIds.Add(created.EncounterId);
            }

            encounter.ReplaceMembership(survivor.ParticipantIds, survivor.Engagements.Select(engagement => engagement.EngagementId), currentTimeSeconds);
            foreach (RuntimeCombatEngagement engagement in survivor.Engagements)
            {
                engagement.ReassignEncounter(encounter.EncounterId);
            }

            foreach (string actorId in survivor.ParticipantIds)
            {
                if (participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant))
                {
                    participant.EncounterId = encounter.EncounterId;
                }
            }

            componentSnapshots.Add(CreateComponentSnapshot(encounter.EncounterId, survivor, retainedOriginal: true, active: true));

            foreach (EncounterComponentPlan plan in activeComponentPlans.Skip(1))
            {
                RuntimeCombatEncounter created = encountersById[plan.EncounterId];
                created.ReplaceMembership(plan.ParticipantIds, plan.Engagements.Select(engagement => engagement.EngagementId), currentTimeSeconds);
                foreach (RuntimeCombatEngagement engagement in plan.Engagements)
                {
                    engagement.ReassignEncounter(created.EncounterId);
                }

                foreach (string actorId in plan.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal))
                {
                    if (!participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant))
                    {
                        continue;
                    }

                    ActorCombatStateSnapshot previous = CreateActorSnapshot(participant);
                    string previousEncounterId = participant.EncounterId;
                    participant.EncounterId = created.EncounterId;
                    participant.Revision++;
                    participant.TransitionReason = "EncounterSplit";
                    reassignments.Add(new CombatParticipantReassignmentResult(actorId, previousEncounterId, created.EncounterId, previous, CreateActorSnapshot(participant)));
                }

                componentSnapshots.Add(CreateComponentSnapshot(created.EncounterId, plan, retainedOriginal: false, active: true));
            }

            return CreateSplitResult(true, false, CombatStateResultCode.Success, "Encounter connectivity reconciled.", transactionId, encounter.EncounterId, survivingEncounterId, createdEncounterIds, previousParticipants, componentSnapshots, endedEngagementIds, exitResults.Select(exit => exit.ActorId).ToList(), exitResults, endedEncounters, reassignments, reason, revisionBefore, encounter.Revision);
        }

        private CombatExitResult RemoveIsolatedParticipant(string actorId, string encounterId, CombatExitReason reason, string transactionId)
        {
            if (!participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant))
            {
                return null;
            }

            ActorCombatStateSnapshot previous = CreateActorSnapshot(participant);
            if (encountersById.TryGetValue(encounterId, out RuntimeCombatEncounter encounter))
            {
                encounter.RemoveParticipant(actorId);
            }

            participant.State = CombatStateValue.OutOfCombat;
            participant.EncounterId = string.Empty;
            participant.Revision++;
            participant.TransitionReason = reason.ToString();
            ActorCombatStateSnapshot resulting = CreateActorSnapshot(participant);
            participantsByActorId.Remove(actorId);
            return CombatExitResult.Success(false, false, CombatStateResultCode.Success, "Actor left combat after becoming isolated.", transactionId, actorId, encounterId, previous, resulting, Array.Empty<string>(), reason, currentTimeSeconds);
        }

        private RuntimeCombatEngagement ResolveEngagementForEnd(CombatEngagementEndRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.EngagementId))
            {
                return engagementsByPairKey.Values.FirstOrDefault(engagement => engagement.Active && string.Equals(engagement.EngagementId, request.EngagementId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(request.SourceActorId) && !string.IsNullOrWhiteSpace(request.TargetActorId))
            {
                return FindEngagement(request.SourceActorId, request.TargetActorId);
            }

            return null;
        }

        private void EmitSplitEvents(CombatEncounterSplitResult result)
        {
            if (result == null || !result.Succeeded || result.Preview || result.Duplicate || !result.SplitOccurred)
            {
                return;
            }

            EncounterSplit?.Invoke(result);
            foreach (string createdEncounterId in result.CreatedEncounterIds)
            {
                CombatEncounterSnapshot created = GetEncounter(createdEncounterId);
                if (created != null)
                {
                    EncounterCreatedFromSplit?.Invoke(created);
                }
            }

            foreach (CombatParticipantReassignmentResult reassignment in result.Reassignments)
            {
                ParticipantReassignedToEncounter?.Invoke(reassignment);
            }

            foreach (CombatExitResult exit in result.ExitResults)
            {
                ParticipantLeftEncounter?.Invoke(exit);
                ActorLeftCombat?.Invoke(exit);
            }

            foreach (CombatEncounterSnapshot ended in result.EndedEncounters)
            {
                EncounterEnded?.Invoke(ended);
            }
        }

        private CombatEncounterSplitResult CreateSplitResult(
            bool succeeded,
            bool duplicate,
            string code,
            string message,
            string transactionId,
            string originalEncounterId,
            string survivingEncounterId,
            IReadOnlyList<string> createdEncounterIds,
            IReadOnlyList<string> previousParticipantIds,
            IReadOnlyList<CombatEncounterSplitComponentSnapshot> components,
            IReadOnlyList<string> endedEngagementIds,
            IReadOnlyList<string> participantsLeftCombat,
            IReadOnlyList<CombatExitResult> exitResults,
            IReadOnlyList<CombatEncounterSnapshot> endedEncounters,
            CombatExitReason reason,
            long originalRevisionBefore,
            long originalRevisionAfter)
        {
            return new CombatEncounterSplitResult(succeeded, false, duplicate, code, message, transactionId, originalEncounterId, survivingEncounterId, createdEncounterIds, previousParticipantIds, components, endedEngagementIds, participantsLeftCombat, exitResults, endedEncounters, null, reason, originalRevisionBefore, originalRevisionAfter, currentTimeSeconds);
        }

        private CombatEncounterSplitResult CreateSplitResult(
            bool succeeded,
            bool duplicate,
            string code,
            string message,
            string transactionId,
            string originalEncounterId,
            string survivingEncounterId,
            IReadOnlyList<string> createdEncounterIds,
            IReadOnlyList<string> previousParticipantIds,
            IReadOnlyList<CombatEncounterSplitComponentSnapshot> components,
            IReadOnlyList<string> endedEngagementIds,
            IReadOnlyList<string> participantsLeftCombat,
            IReadOnlyList<CombatExitResult> exitResults,
            IReadOnlyList<CombatEncounterSnapshot> endedEncounters,
            IReadOnlyList<CombatParticipantReassignmentResult> reassignments,
            CombatExitReason reason,
            long originalRevisionBefore,
            long originalRevisionAfter)
        {
            return new CombatEncounterSplitResult(succeeded, false, duplicate, code, message, transactionId, originalEncounterId, survivingEncounterId, createdEncounterIds, previousParticipantIds, components, endedEngagementIds, participantsLeftCombat, exitResults, endedEncounters, reassignments, reason, originalRevisionBefore, originalRevisionAfter, currentTimeSeconds);
        }

        private static CombatEncounterSplitComponentSnapshot CreateComponentSnapshot(string encounterId, EncounterComponentPlan plan, bool retainedOriginal, bool active)
        {
            return new CombatEncounterSplitComponentSnapshot(
                encounterId,
                plan.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                plan.Engagements.Select(engagement => engagement.EngagementId).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                retainedOriginal,
                plan.SplitTimestamp,
                active);
        }

        private EncounterComponentPlan CreateComponentPlan(IReadOnlyList<string> participantIds, IReadOnlyList<RuntimeCombatEngagement> activeEdges)
        {
            List<string> sortedParticipants = participantIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
            List<RuntimeCombatEngagement> edges = activeEdges
                .Where(edge => sortedParticipants.Contains(edge.FirstActorId) && sortedParticipants.Contains(edge.SecondActorId))
                .OrderBy(edge => edge.EngagementId, StringComparer.Ordinal)
                .ToList();
            float earliestJoin = sortedParticipants
                .Select(actorId => participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant) ? participant.EnteredAt : float.MaxValue)
                .DefaultIfEmpty(float.MaxValue)
                .Min();
            string lowestActor = sortedParticipants.FirstOrDefault() ?? string.Empty;
            return new EncounterComponentPlan(sortedParticipants, edges, earliestJoin, lowestActor, currentTimeSeconds);
        }

        private IEnumerable<RuntimeCombatEngagement> GetActiveEncounterEdges(string encounterId)
        {
            return engagementsByPairKey.Values.Where(engagement => engagement.Active && string.Equals(engagement.EncounterId, encounterId, StringComparison.Ordinal));
        }

        private static List<List<string>> BuildConnectedComponents(IEnumerable<string> participantIds, IEnumerable<RuntimeCombatEngagement> activeEdges)
        {
            HashSet<string> remaining = new HashSet<string>(participantIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            Dictionary<string, List<string>> adjacency = remaining.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (RuntimeCombatEngagement engagement in activeEdges ?? Array.Empty<RuntimeCombatEngagement>())
            {
                if (!adjacency.ContainsKey(engagement.FirstActorId) || !adjacency.ContainsKey(engagement.SecondActorId))
                {
                    continue;
                }

                adjacency[engagement.FirstActorId].Add(engagement.SecondActorId);
                adjacency[engagement.SecondActorId].Add(engagement.FirstActorId);
            }

            List<List<string>> components = new List<List<string>>();
            while (remaining.Count > 0)
            {
                string start = remaining.OrderBy(id => id, StringComparer.Ordinal).First();
                Queue<string> queue = new Queue<string>();
                List<string> component = new List<string>();
                queue.Enqueue(start);
                remaining.Remove(start);
                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    component.Add(current);
                    foreach (string next in adjacency[current].OrderBy(id => id, StringComparer.Ordinal))
                    {
                        if (remaining.Remove(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                components.Add(component.OrderBy(id => id, StringComparer.Ordinal).ToList());
            }

            return components;
        }

        private static bool SetEquals(IReadOnlyCollection<string> first, IReadOnlyCollection<string> second)
        {
            return first.Count == second.Count && new HashSet<string>(first, StringComparer.Ordinal).SetEquals(second);
        }

        private bool AddOrRefreshParticipant(RuntimeCombatEncounter encounter, string actorId, GameObject actorObject, string reason)
        {
            bool added = false;
            if (!participantsByActorId.TryGetValue(actorId, out RuntimeCombatParticipant participant))
            {
                participant = new RuntimeCombatParticipant(actorId, encounter.EncounterId, currentTimeSeconds, reason);
                participantsByActorId.Add(actorId, participant);
                added = true;
            }

            participant.Refresh(encounter.EncounterId, currentTimeSeconds, CombatTimeoutSeconds, reason);
            encounter.AddParticipant(actorId);

            return added;
        }

        private RuntimeCombatEngagement UpsertEngagement(string sourceActorId, string targetActorId, string encounterId, CombatActivityClassification classification, string originatingId, out bool created)
        {
            string key = GetPairKey(sourceActorId, targetActorId);
            if (engagementsByPairKey.TryGetValue(key, out RuntimeCombatEngagement engagement) && engagement.Active)
            {
                engagement.Refresh(encounterId, currentTimeSeconds, classification, originatingId);
                created = false;
                return engagement;
            }

            string engagementId = $"engagement.runtime.{++nextEngagementSequence:0000}";
            string first = string.CompareOrdinal(sourceActorId, targetActorId) <= 0 ? sourceActorId : targetActorId;
            string second = string.Equals(first, sourceActorId, StringComparison.Ordinal) ? targetActorId : sourceActorId;
            engagement = new RuntimeCombatEngagement(engagementId, first, second, encounterId, currentTimeSeconds, classification, originatingId);
            engagementsByPairKey[key] = engagement;
            created = true;
            return engagement;
        }

        private RuntimeCombatEngagement FindEngagement(string sourceActorId, string targetActorId)
        {
            return engagementsByPairKey.TryGetValue(GetPairKey(sourceActorId, targetActorId), out RuntimeCombatEngagement engagement) && engagement.Active ? engagement : null;
        }

        private RuntimeCombatEncounter GetActiveEncounterForActor(string actorId)
        {
            if (!participantsByActorId.TryGetValue(actorId ?? string.Empty, out RuntimeCombatParticipant participant))
            {
                return null;
            }

            return encountersById.TryGetValue(participant.EncounterId, out RuntimeCombatEncounter encounter) && encounter.Active ? encounter : null;
        }

        private bool HasActiveEngagements(string actorId)
        {
            return engagementsByPairKey.Values.Any(engagement => engagement.Active && engagement.Includes(actorId));
        }

        private bool HasActiveEncounterEngagements(string encounterId)
        {
            return engagementsByPairKey.Values.Any(engagement => engagement.Active && string.Equals(engagement.EncounterId, encounterId, StringComparison.Ordinal));
        }

        private bool ShouldRemoveForLifecycle(string actorId)
        {
            if (defaultPolicy != null && !defaultPolicy.RemoveDeadParticipants)
            {
                return false;
            }

            GameObject found = FindActorObject(actorId);
            if (found == null)
            {
                return false;
            }

            return ActorLifecycleUtility.GetState(found) == ActorLifecycleState.Dead;
        }

        private GameObject FindActorObject(string actorId)
        {
            foreach (WorldEntityIdentity identity in UnityEngine.Object.FindObjectsByType<WorldEntityIdentity>(FindObjectsInactive.Include))
            {
                if (identity != null && string.Equals(identity.EntityId, actorId, StringComparison.Ordinal))
                {
                    return identity.gameObject;
                }
            }

            return null;
        }

        private bool TryResolveActor(string expectedActorId, GameObject actorObject, out string actorId, out string failure, out string code, bool isSource, bool allowIdOnly = false)
        {
            actorId = expectedActorId ?? string.Empty;
            failure = string.Empty;
            code = CombatStateResultCode.InvalidRequest;
            if (actorObject == null)
            {
                if (allowIdOnly && !string.IsNullOrWhiteSpace(actorId))
                {
                    return true;
                }

                code = isSource ? CombatStateResultCode.MissingSource : CombatStateResultCode.MissingTarget;
                failure = "Combat participant object is missing.";
                return false;
            }

            WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
            string resolved = identity == null ? actorId : identity.EntityId;
            if (string.IsNullOrWhiteSpace(resolved))
            {
                code = isSource ? CombatStateResultCode.MissingSource : CombatStateResultCode.MissingTarget;
                failure = "Combat participant has no actor/body identity.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(actorId) && !string.Equals(actorId, resolved, StringComparison.Ordinal))
            {
                code = isSource ? CombatStateResultCode.StaleSource : CombatStateResultCode.StaleTarget;
                failure = $"Combat participant expected '{actorId}' but resolved '{resolved}'.";
                return false;
            }

            actorId = resolved;
            return true;
        }

        private ActorCombatStateSnapshot CreateActorSnapshot(RuntimeCombatParticipant participant)
        {
            if (participant == null)
            {
                return null;
            }

            int participantCount = 0;
            if (!string.IsNullOrWhiteSpace(participant.EncounterId) && encountersById.TryGetValue(participant.EncounterId, out RuntimeCombatEncounter encounter))
            {
                participantCount = encounter.ParticipantIds.Count;
            }

            return new ActorCombatStateSnapshot(participant.ActorId, participant.State, participant.EncounterId, participant.EnteredAt, participant.LastActivityAt, participant.DisengageEligibleAt, participantCount, GetActiveEngagements(participant.ActorId).Count, participant.Revision, participant.TransitionReason);
        }

        private ActorCombatStateSnapshot BuildPreviewActorSnapshot(string actorId, string encounterId, ActorCombatStateSnapshot previous, string reason)
        {
            float enteredAt = previous != null && previous.IsInCombat ? previous.EnteredAt : currentTimeSeconds;
            long revision = previous == null ? 1 : previous.Revision + 1;
            return new ActorCombatStateSnapshot(actorId, CombatStateValue.InCombat, encounterId, enteredAt, currentTimeSeconds, currentTimeSeconds + CombatTimeoutSeconds, 2, 1, revision, reason);
        }

        private CombatEngagementSnapshot CreateEngagementSnapshot(RuntimeCombatEngagement engagement)
        {
            return new CombatEngagementSnapshot(engagement.EngagementId, engagement.FirstActorId, engagement.SecondActorId, engagement.EncounterId, engagement.CreatedAt, engagement.LastRefreshedAt, engagement.Classification, engagement.OriginatingId, engagement.Active, engagement.EndReason, engagement.Revision);
        }

        private CombatEncounterSnapshot CreateEncounterSnapshot(RuntimeCombatEncounter encounter)
        {
            List<CombatEngagementSnapshot> engagements = engagementsByPairKey.Values
                .Where(engagement => engagement.EncounterId == encounter.EncounterId && (!encounter.Active || engagement.Active))
                .OrderBy(engagement => engagement.EngagementId, StringComparer.Ordinal)
                .Select(CreateEngagementSnapshot)
                .ToList();
            return new CombatEncounterSnapshot(encounter.EncounterId, encounter.Active, encounter.CreatedAt, encounter.LastActivityAt, encounter.ParticipantIds.OrderBy(id => id, StringComparer.Ordinal).ToList(), engagements, encounter.Revision, encounter.CompletionReason);
        }

        private string ChoosePreviewEncounterId(RuntimeCombatEncounter sourceEncounter, RuntimeCombatEncounter targetEncounter, string requested)
        {
            if (sourceEncounter != null && targetEncounter != null)
            {
                return ChooseMergeSurvivor(sourceEncounter, targetEncounter).EncounterId;
            }

            if (sourceEncounter != null)
            {
                return sourceEncounter.EncounterId;
            }

            if (targetEncounter != null)
            {
                return targetEncounter.EncounterId;
            }

            return string.IsNullOrWhiteSpace(requested) ? "preview.encounter" : requested;
        }

        private static string GetPairKey(string firstActorId, string secondActorId)
        {
            return string.CompareOrdinal(firstActorId, secondActorId) <= 0
                ? $"{firstActorId}|{secondActorId}"
                : $"{secondActorId}|{firstActorId}";
        }

        private bool IsDuplicateTransaction(string transactionId)
        {
            return !string.IsNullOrWhiteSpace(transactionId) && processedTransactions.Contains(transactionId);
        }

        private void RememberTransaction(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || !processedTransactions.Add(transactionId))
            {
                return;
            }

            processedTransactionOrder.Enqueue(transactionId);
            while (processedTransactionOrder.Count > ProcessedTransactionLimit)
            {
                processedTransactions.Remove(processedTransactionOrder.Dequeue());
            }
        }

        private sealed class EncounterComponentPlan
        {
            public EncounterComponentPlan(IReadOnlyList<string> participantIds, IReadOnlyList<RuntimeCombatEngagement> engagements, float earliestJoinAt, string lowestActorId, float splitTimestamp)
            {
                ParticipantIds = participantIds;
                Engagements = engagements;
                EarliestJoinAt = earliestJoinAt;
                LowestActorId = lowestActorId ?? string.Empty;
                SplitTimestamp = splitTimestamp;
            }

            public IReadOnlyList<string> ParticipantIds { get; }
            public IReadOnlyList<RuntimeCombatEngagement> Engagements { get; }
            public float EarliestJoinAt { get; }
            public string LowestActorId { get; }
            public float SplitTimestamp { get; }
            public string EncounterId { get; set; }
            public bool RetainedOriginalEncounterId { get; set; }
        }
    }
}
